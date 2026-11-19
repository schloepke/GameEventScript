// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace GameEventScript.Network.Sockets;

/// <summary>
/// Carries one <see cref="GesNetworkConnection"/> over a TCP stream, a WebSocket or UDP. Background tasks only move bytes:
/// the host calls <see cref="Receive"/> and <see cref="Transmit"/>, or <see cref="Update"/> for both, on its own thread,
/// which feed received bytes into the connection, advance it and hand its packets to the sender. The connection is
/// touched only by the host thread. As an <see cref="IGesNetworkLink"/> it is the transport of an end.
/// </summary>
public sealed class GesNetworkSocketSession : IGesNetworkLink
{
    private const int ReadBytes = 16384;
    private const int ReceivedChunks = 64;

    // A stream write carries the packets of one update together, up to this many bytes.
    private const int WriteBytes = 65536;

    // Packets wait in the connection while buffers of this many bytes are handed to the socket but not yet written,
    // so a slow peer fills the connection's send queue rather than this session.
    private const int UnwrittenBytes = 262144;

    // A datagram is dropped rather than waited for when this many are not yet taken.
    internal const int ReceivedDatagrams = 256;

    // A disposed session of a closed connection writes its last packets for at most this long.
    private static readonly TimeSpan OrderlyEnd = TimeSpan.FromSeconds(10);

    // Set once the transport is open, before the background tasks start; the host thread never reads them.
    private Stream? _stream;
    private WebSocket? _webSocket;
    private Socket? _datagram;
    private IDisposable? _owner;
    private readonly GesNetworkDatagramPeer? _peer;
    private readonly GesNetworkSocketSignal? _signal;
    private readonly Channel<Chunk> _received;
    private readonly Channel<Chunk> _outgoing = Channel.CreateUnbounded<Chunk>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    private readonly CancellationTokenSource _cancel = new();
    private readonly int _packetBytes;
    private readonly int _chunkBytes;
    private readonly bool _batch;
    private readonly CancellationToken _cancelToken;
    private readonly object _gate = new();
    private Task? _writer;
    // Packets are written straight into a buffer that the write task gives back here. The unwritten limit bounds
    // their number, so the session keeps its own rather than relying on the shared pool across threads.
    private readonly ConcurrentQueue<byte[]> _free = new();
    private byte[]? _transmit;
    private long _unwritten;
    // Set while packets wait for the unwritten limit, so the write task wakes the end once it falls below it.
    private volatile bool _held;
    private volatile bool _transportEnded;
    private bool _outgoingCompleted;
    private bool _disposed;
    // Guarded by the gate: whether the transport still opens, and whether a disposed session writes the last packets of
    // its closed connection once it opened.
    private bool _opening;
    private bool _orderlyOpening;

    private GesNetworkSocketSession(GesNetworkConnection connection, GesNetworkDatagramPeer? peer, GesNetworkSocketSignal? signal)
    {
        Connection = connection;
        _peer = peer;
        _signal = signal;
        _cancelToken = _cancel.Token;
        _batch = connection.Transport == GesNetworkTransport.Stream;
        _packetBytes = connection.MaxPacketBytes + 3;
        _chunkBytes = _batch ? Math.Max(WriteBytes, _packetBytes) : _packetBytes;
        // A server's datagrams arrive through its listener, which reads the shared socket.
        _received = peer?.Received ?? (connection.Transport == GesNetworkTransport.Datagram
            ? DatagramChannel()
            : Channel.CreateBounded<Chunk>(new BoundedChannelOptions(ReceivedChunks) { SingleReader = true, SingleWriter = true }));
    }

    /// <summary>Gets the connection this session carries. Use it only on the thread that calls <see cref="Receive"/> and <see cref="Transmit"/>.</summary>
    public GesNetworkConnection Connection { get; }

    /// <summary>Opens a TCP connection for a client connection of the stream transport.</summary>
    /// <param name="host">The server's host name or address.</param>
    /// <param name="port">The server's port.</param>
    /// <param name="connection">A client connection created with <see cref="GesNetworkTransport.Stream"/>; its timeout runs from its creation.</param>
    /// <param name="cancellationToken">Cancels connecting.</param>
    /// <returns>The session.</returns>
    /// <exception cref="ArgumentException">The connection is no client of the stream transport or uses insecure test keys.</exception>
    /// <exception cref="SocketException">The connection could not be opened.</exception>
    public static async Task<GesNetworkSocketSession> ConnectTcpAsync(string host, int port, GesNetworkConnection connection, CancellationToken cancellationToken = default)
    {
        Require(connection, GesNetworkTransport.Stream, client: true);
        var client = new TcpClient { NoDelay = true };
        try
        {
            await client.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
            return FromStream(connection, client.GetStream(), client);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    /// <summary>Opens a WebSocket for a client connection of the message transport.</summary>
    /// <param name="uri">The <c>ws</c> or <c>wss</c> endpoint.</param>
    /// <param name="connection">A client connection created with <see cref="GesNetworkTransport.Message"/>; its timeout runs from its creation.</param>
    /// <param name="cancellationToken">Cancels connecting.</param>
    /// <returns>The session.</returns>
    /// <exception cref="ArgumentException">The connection is no client of the message transport or uses insecure test keys.</exception>
    /// <exception cref="WebSocketException">The WebSocket could not be opened.</exception>
    public static async Task<GesNetworkSocketSession> ConnectWebSocketAsync(Uri uri, GesNetworkConnection connection, CancellationToken cancellationToken = default)
    {
        Require(connection, GesNetworkTransport.Message, client: true);
        var socket = new ClientWebSocket();
        socket.Options.KeepAliveInterval = TimeSpan.Zero;
        try
        {
            await socket.ConnectAsync(uri, cancellationToken).ConfigureAwait(false);
            return FromWebSocket(connection, socket, socket);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>Opens a UDP socket for a client connection of the datagram transport.</summary>
    /// <param name="host">The server's host name or address; the first address it resolves to is used.</param>
    /// <param name="port">The server's port.</param>
    /// <param name="connection">A client connection created with <see cref="GesNetworkTransport.Datagram"/>; its timeout runs from its creation.</param>
    /// <param name="cancellationToken">Cancels resolving the host.</param>
    /// <returns>The session. UDP has no connection setup, so a missing server shows only as the connection's timeout.</returns>
    /// <exception cref="ArgumentException">The connection is no client of the datagram transport or uses insecure test keys.</exception>
    /// <exception cref="SocketException">The host cannot be resolved or the socket cannot be opened.</exception>
    public static async Task<GesNetworkSocketSession> ConnectUdpAsync(string host, int port, GesNetworkConnection connection, CancellationToken cancellationToken = default)
    {
        Require(connection, GesNetworkTransport.Datagram, client: true);
        var socket = await OpenUdpAsync(host, port, cancellationToken).ConfigureAwait(false);
        var session = new GesNetworkSocketSession(connection, null, null);
        session.Start(null, null, socket, socket);
        return session;
    }

    internal static async Task<Socket> OpenUdpAsync(string host, int port, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
        if (addresses.Length == 0) throw new SocketException((int)SocketError.HostNotFound);
        var socket = new Socket(addresses[0].AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        try
        {
            await socket.ConnectAsync(new IPEndPoint(addresses[0], port), cancellationToken).ConfigureAwait(false);
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    internal static GesNetworkSocketSession FromStream(GesNetworkConnection connection, Stream stream, IDisposable owner, GesNetworkSocketSignal? signal = null)
    {
        var session = new GesNetworkSocketSession(connection, null, signal);
        session.Start(stream, null, null, owner);
        return session;
    }

    internal static GesNetworkSocketSession FromWebSocket(GesNetworkConnection connection, WebSocket socket, IDisposable owner, GesNetworkSocketSignal? signal = null)
    {
        var session = new GesNetworkSocketSession(connection, null, signal);
        session.Start(null, socket, null, owner);
        return session;
    }

    internal static GesNetworkSocketSession FromPeer(GesNetworkConnection connection, GesNetworkDatagramPeer peer, GesNetworkSocketSignal? signal)
    {
        var session = new GesNetworkSocketSession(connection, peer, signal);
        session.Start(null, null, peer.Socket, peer);
        return session;
    }

    // Creates a session whose transport opens in the background; packets wait for it, and a failure ends the transport.
    internal static GesNetworkSocketSession Opening(GesNetworkConnection connection, GesNetworkSocketSignal signal, Func<CancellationToken, Task<Opened>> open)
    {
        var session = new GesNetworkSocketSession(connection, null, signal) { _opening = true };
        _ = Task.Run(async () =>
        {
            try
            {
                var opened = await open(session._cancelToken).ConfigureAwait(false);
                session.Start(opened.Stream, opened.WebSocket, opened.Datagram, opened.Owner);
            }
            catch (Exception)
            {
                // Any failure to open, whatever its kind, is the end of the transport.
                session.End();
                session.OpeningFailed();
            }
        });
        return session;
    }

    // Takes the open transport and starts moving bytes. A session disposed meanwhile releases it at once, unless it
    // still writes the last packets of its closed connection: then it releases the transport once they are written.
    private void Start(Stream? stream, WebSocket? webSocket, Socket? datagram, IDisposable owner)
    {
        lock (_gate)
        {
            _opening = false;
            if (_disposed && !_orderlyOpening)
            {
                owner.Dispose();
                return;
            }

            _stream = stream;
            _webSocket = webSocket;
            _datagram = datagram;
            _owner = owner;
            if (_peer == null) _ = Task.Run(ReadAsync);
            _writer = Task.Run(WriteAsync);
            if (_orderlyOpening) _ = _writer.ContinueWith(_ => Release(owner), TaskScheduler.Default);
        }

        _signal?.Set();
    }

    // A disposed session whose transport failed to open releases what remains.
    private void OpeningFailed()
    {
        lock (_gate)
        {
            _opening = false;
            if (!_orderlyOpening) return;
        }

        Release(null);
    }

    private void End()
    {
        _transportEnded = true;
        _signal?.Set();
    }

    internal static Channel<Chunk> DatagramChannel() =>
        Channel.CreateBounded<Chunk>(new BoundedChannelOptions(ReceivedDatagrams) { SingleReader = true, SingleWriter = true });

    internal static void Require(GesNetworkConnection connection, GesNetworkTransport transport, bool client)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (connection.Transport != transport) throw new ArgumentException($"The connection must use the {transport} transport.", nameof(connection));
        if (connection.IsClient != client) throw new ArgumentException(client ? "The connection must be a client." : "The connection must be a server.", nameof(connection));
        if (connection.UsesInsecureTestKeys) throw new ArgumentException("A socket transport refuses insecure test keys.", nameof(connection));
    }

    /// <summary>
    /// Feeds the received bytes into the connection, advances it to <paramref name="now"/>, reports the end of the
    /// transport and sends every due packet: <see cref="Receive"/> followed by <see cref="Transmit"/>. Call it on the
    /// host thread, then take the connection's events.
    /// </summary>
    /// <param name="now">The current time in microseconds, not earlier than the connection's time.</param>
    /// <exception cref="ObjectDisposedException">The session is disposed.</exception>
    public void Update(long now)
    {
        Receive(now);
        Transmit();
    }

    /// <summary>
    /// Feeds the received bytes into the connection, advances it to <paramref name="now"/> and reports the end of the
    /// transport. Call it on the host thread.
    /// </summary>
    /// <param name="now">The current time in microseconds, not earlier than the connection's time.</param>
    /// <exception cref="ObjectDisposedException">The session is disposed.</exception>
    public void Receive(long now)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        // Read the end flag before draining, so no chunk written before the end is left behind.
        var ended = _transportEnded;
        while (_received.Reader.TryRead(out var chunk))
        {
            Connection.Receive(chunk.Bytes.AsSpan(0, chunk.Length), now);
            ArrayPool<byte>.Shared.Return(chunk.Bytes);
        }

        Connection.Advance(now);
        if (ended) Connection.TransportClosed();
    }

    /// <summary>
    /// Hands every packet the connection has due to the sender. While the transport still opens the packets wait for
    /// it. Call it on the host thread.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The session is disposed.</exception>
    public void Transmit()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_outgoingCompleted) return;
        var more = true;
        while (more)
        {
            if (Interlocked.Read(ref _unwritten) >= UnwrittenBytes)
            {
                Hold();
                break;
            }

            // A datagram or WebSocket message carries one packet; a stream write as many as fit.
            var bytes = _transmit ??= _free.TryDequeue(out var free) ? free : new byte[_chunkBytes];
            var length = 0;
            do
            {
                var written = Connection.Transmit(bytes.AsSpan(length, _packetBytes));
                more = written > 0;
                length += written;
            }
            while (more && _batch && length + _packetBytes <= _chunkBytes);

            if (length == 0) break;
            _transmit = null;
            Interlocked.Add(ref _unwritten, bytes.Length);
            _outgoing.Writer.TryWrite(new Chunk(bytes, length));
        }

        if (Connection.State == GesNetworkState.Closed)
        {
            _outgoingCompleted = true;
            _outgoing.Writer.TryComplete();
        }
    }

    // Packets wait in the connection until the write task has written enough; it wakes the end then. Checking again
    // after setting the flag covers a write that finished before the task could see it.
    private void Hold()
    {
        _held = true;
        if (Interlocked.Read(ref _unwritten) < UnwrittenBytes && _held)
        {
            _held = false;
            _signal?.Set();
        }
    }

    /// <summary>
    /// Releases the socket. When the connection is closed and its last packets are handed to the sender, they are
    /// still written, once the transport opened if it still opens, and the transport ends in order, within the
    /// connection's timeout; otherwise both background tasks stop at once without sending anything more.
    /// </summary>
    public void Dispose()
    {
        Task? writer;
        IDisposable? owner;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            writer = _writer;
            owner = _owner;

            // The last packets wait for the transport that still opens; starting it writes them. The time limit covers the
            // opening too, and is set before a started transport can release the cancellation.
            _orderlyOpening = _outgoingCompleted && _opening;
            if (_orderlyOpening) _cancel.CancelAfter(OrderlyEnd);
        }

        _outgoing.Writer.TryComplete();
        if (_orderlyOpening) return;

        if (_outgoingCompleted && writer is { IsCompleted: false })
        {
            _cancel.CancelAfter(OrderlyEnd);
            _ = writer.ContinueWith(_ => Release(owner), TaskScheduler.Default);
            return;
        }

        Release(owner);
    }

    private void Release(IDisposable? owner)
    {
        _cancel.Cancel();
        owner?.Dispose();
        _cancel.Dispose();
    }

    private async Task ReadAsync()
    {
        try
        {
            if (_stream != null) await ReadStreamAsync(_stream).ConfigureAwait(false);
            else if (_webSocket != null) await ReadWebSocketAsync(_webSocket).ConfigureAwait(false);
            else await ReadDatagramsAsync(_datagram!).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or SocketException or WebSocketException or ObjectDisposedException or OperationCanceledException or ChannelClosedException)
        {
            // Any end of reading is the end of the transport.
        }
        finally
        {
            End();
        }
    }

    private async Task ReadStreamAsync(Stream stream)
    {
        while (true)
        {
            var bytes = ArrayPool<byte>.Shared.Rent(ReadBytes);
            var length = await stream.ReadAsync(bytes.AsMemory(0, ReadBytes), _cancelToken).ConfigureAwait(false);
            if (length == 0)
            {
                ArrayPool<byte>.Shared.Return(bytes);
                return;
            }

            await _received.Writer.WriteAsync(new Chunk(bytes, length), _cancelToken).ConfigureAwait(false);
            _signal?.Set();
        }
    }

    // Reads one binary message per packet. A text message is passed as an empty packet and a message longer than
    // the packet limit is cut one byte beyond it, so the connection closes with network.malformed or
    // network.packetTooLarge; reading ends there.
    private async Task ReadWebSocketAsync(WebSocket socket)
    {
        var limit = Connection.MaxPacketBytes;
        while (true)
        {
            var bytes = ArrayPool<byte>.Shared.Rent(limit + 1);
            var length = 0;
            ValueWebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(bytes.AsMemory(length, limit + 1 - length), _cancelToken).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    ArrayPool<byte>.Shared.Return(bytes);
                    return;
                }

                length += result.Count;
            }
            while (!result.EndOfMessage && length <= limit);

            var last = result.MessageType == WebSocketMessageType.Text || length > limit;
            if (result.MessageType == WebSocketMessageType.Text) length = 0;
            await _received.Writer.WriteAsync(new Chunk(bytes, length), _cancelToken).ConfigureAwait(false);
            _signal?.Set();
            if (last) return;
        }
    }

    // Reads one datagram per packet. A datagram longer than the packet limit is passed one byte beyond it, so the
    // connection closes with network.packetTooLarge. Errors reported for single datagrams, such as an unreachable
    // port, do not end the transport: UDP has no end, and the connection's timeout covers a lost peer.
    private async Task ReadDatagramsAsync(Socket socket)
    {
        var limit = Connection.MaxPacketBytes;
        while (true)
        {
            var bytes = ArrayPool<byte>.Shared.Rent(limit + 1);
            int length;
            try
            {
                length = await socket.ReceiveAsync(bytes.AsMemory(0, limit + 1), SocketFlags.None, _cancelToken).ConfigureAwait(false);
            }
            catch (SocketException exception) when (exception.SocketErrorCode == SocketError.MessageSize)
            {
                length = limit + 1;
            }
            catch (SocketException exception) when (IsTransient(exception.SocketErrorCode))
            {
                ArrayPool<byte>.Shared.Return(bytes);
                continue;
            }

            if (_received.Writer.TryWrite(new Chunk(bytes, length))) _signal?.Set();
            else ArrayPool<byte>.Shared.Return(bytes);
        }
    }

    // Errors that concern one datagram on its way, not the socket.
    internal static bool IsTransient(SocketError error) =>
        error is SocketError.ConnectionReset or SocketError.ConnectionRefused or SocketError.HostUnreachable or SocketError.NetworkUnreachable
            or SocketError.HostDown or SocketError.NetworkDown or SocketError.NoBufferSpaceAvailable;

    private async Task WriteAsync()
    {
        try
        {
            // Completing the channel ends this loop; a cancellable wait would allocate on every empty queue.
            await foreach (var chunk in _outgoing.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                try
                {
                    if (_stream != null) await _stream.WriteAsync(chunk.Bytes.AsMemory(0, chunk.Length), _cancelToken).ConfigureAwait(false);
                    else if (_webSocket != null) await _webSocket.SendAsync(chunk.Bytes.AsMemory(0, chunk.Length), WebSocketMessageType.Binary, true, _cancelToken).ConfigureAwait(false);
                    else await SendDatagramAsync(chunk).ConfigureAwait(false);
                }
                finally
                {
                    _free.Enqueue(chunk.Bytes);
                    if (Interlocked.Add(ref _unwritten, -chunk.Bytes.Length) < UnwrittenBytes && _held)
                    {
                        _held = false;
                        _signal?.Set();
                    }
                }
            }

            // The connection closed and its last packet is sent: end the transport in order.
            if (_stream != null)
            {
                await _stream.FlushAsync(_cancelToken).ConfigureAwait(false);
                if (_owner is TcpClient client) client.Client.Shutdown(SocketShutdown.Send);
            }
            else if (_webSocket?.State == WebSocketState.Open)
            {
                await _webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, _cancelToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is IOException or SocketException or WebSocketException or ObjectDisposedException or OperationCanceledException)
        {
            End();
        }
        finally
        {
            // A closed server connection gives its address back to address validation.
            _peer?.Dispose();
        }
    }

    private async ValueTask SendDatagramAsync(Chunk chunk)
    {
        try
        {
            if (_peer != null) await _datagram!.SendToAsync(chunk.Bytes.AsMemory(0, chunk.Length), SocketFlags.None, _peer.Address, _cancelToken).ConfigureAwait(false);
            else await _datagram!.SendAsync(chunk.Bytes.AsMemory(0, chunk.Length), SocketFlags.None, _cancelToken).ConfigureAwait(false);
        }
        catch (SocketException exception) when (IsTransient(exception.SocketErrorCode))
        {
            // A datagram may be lost; the connection resends what it needs.
        }
    }

    internal readonly record struct Chunk(byte[] Bytes, int Length);

    internal readonly record struct Opened(Stream? Stream, WebSocket? WebSocket, Socket? Datagram, IDisposable Owner);
}
