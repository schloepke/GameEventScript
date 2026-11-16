// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Buffers;
using System.IO;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace GameEventScript.Network.Sockets;

/// <summary>
/// Carries one <see cref="GesNetworkConnection"/> over a TCP stream or a WebSocket. Background tasks only move bytes:
/// the host calls <see cref="Update"/> on its own thread, which feeds received bytes into the connection, advances
/// it and hands its packets to the sender. The connection is touched only by the host thread.
/// </summary>
public sealed class GesNetworkSocketSession : IDisposable
{
    private const int ReadBytes = 16384;
    private const int ReceivedChunks = 64;

    private readonly Stream? _stream;
    private readonly WebSocket? _webSocket;
    private readonly IDisposable _owner;
    private readonly Channel<Chunk> _received = Channel.CreateBounded<Chunk>(new BoundedChannelOptions(ReceivedChunks) { SingleReader = true, SingleWriter = true });
    private readonly Channel<Chunk> _outgoing = Channel.CreateUnbounded<Chunk>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    private readonly CancellationTokenSource _cancel = new();
    private readonly byte[] _transmit;
    private volatile bool _transportEnded;
    private bool _outgoingCompleted;
    private bool _disposed;

    private GesNetworkSocketSession(GesNetworkConnection connection, Stream? stream, WebSocket? webSocket, IDisposable owner)
    {
        Connection = connection;
        _stream = stream;
        _webSocket = webSocket;
        _owner = owner;
        _transmit = new byte[connection.MaxPacketBytes + 3];
        _ = Task.Run(ReadAsync);
        _ = Task.Run(WriteAsync);
    }

    /// <summary>Gets the connection this session carries. Use it only on the thread that calls <see cref="Update"/>.</summary>
    public GesNetworkConnection Connection { get; }

    /// <summary>Opens a TCP connection for a client connection of the stream transport.</summary>
    /// <param name="host">The server's host name or address.</param>
    /// <param name="port">The server's port.</param>
    /// <param name="connection">A client connection created with <see cref="GesNetworkTransport.Stream"/>; its timeout runs from its creation.</param>
    /// <param name="cancellationToken">Cancels connecting.</param>
    /// <returns>The session.</returns>
    /// <exception cref="ArgumentException">The connection is no client of the stream transport.</exception>
    /// <exception cref="SocketException">The connection could not be opened.</exception>
    public static async Task<GesNetworkSocketSession> ConnectTcpAsync(string host, int port, GesNetworkConnection connection, CancellationToken cancellationToken = default)
    {
        Require(connection, GesNetworkTransport.Stream, client: true);
        var client = new TcpClient { NoDelay = true };
        try
        {
            await client.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
            return new GesNetworkSocketSession(connection, client.GetStream(), null, client);
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
    /// <exception cref="ArgumentException">The connection is no client of the message transport.</exception>
    /// <exception cref="WebSocketException">The WebSocket could not be opened.</exception>
    public static async Task<GesNetworkSocketSession> ConnectWebSocketAsync(Uri uri, GesNetworkConnection connection, CancellationToken cancellationToken = default)
    {
        Require(connection, GesNetworkTransport.Message, client: true);
        var socket = new ClientWebSocket();
        socket.Options.KeepAliveInterval = TimeSpan.Zero;
        try
        {
            await socket.ConnectAsync(uri, cancellationToken).ConfigureAwait(false);
            return new GesNetworkSocketSession(connection, null, socket, socket);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    internal static GesNetworkSocketSession FromStream(GesNetworkConnection connection, Stream stream, IDisposable owner) => new(connection, stream, null, owner);

    internal static GesNetworkSocketSession FromWebSocket(GesNetworkConnection connection, WebSocket socket, IDisposable owner) => new(connection, null, socket, owner);

    internal static void Require(GesNetworkConnection connection, GesNetworkTransport transport, bool client)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (connection.Transport != transport) throw new ArgumentException($"The connection must use the {transport} transport.", nameof(connection));
        if (connection.IsClient != client) throw new ArgumentException(client ? "The connection must be a client." : "The connection must be a server.", nameof(connection));
    }

    /// <summary>
    /// Feeds the received bytes into the connection, advances it to <paramref name="now"/>, reports the end of the
    /// transport and sends every due packet. Call it on the host thread, then take the connection's events.
    /// </summary>
    /// <param name="now">The current time in microseconds, not earlier than the connection's time.</param>
    /// <exception cref="ObjectDisposedException">The session is disposed.</exception>
    public void Update(long now)
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
        if (_outgoingCompleted) return;
        int length;
        while ((length = Connection.Transmit(_transmit)) > 0)
        {
            var bytes = ArrayPool<byte>.Shared.Rent(length);
            _transmit.AsSpan(0, length).CopyTo(bytes);
            _outgoing.Writer.TryWrite(new Chunk(bytes, length));
        }

        if (Connection.State == GesNetworkState.Closed)
        {
            _outgoingCompleted = true;
            _outgoing.Writer.TryComplete();
        }
    }

    /// <summary>Stops both background tasks and releases the socket without sending anything more.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cancel.Cancel();
        _outgoing.Writer.TryComplete();
        _owner.Dispose();
        _cancel.Dispose();
    }

    private async Task ReadAsync()
    {
        try
        {
            if (_stream != null) await ReadStreamAsync(_stream).ConfigureAwait(false);
            else await ReadWebSocketAsync(_webSocket!).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or SocketException or WebSocketException or ObjectDisposedException or OperationCanceledException or ChannelClosedException)
        {
            // Any end of reading is the end of the transport.
        }
        finally
        {
            _transportEnded = true;
        }
    }

    private async Task ReadStreamAsync(Stream stream)
    {
        while (true)
        {
            var bytes = ArrayPool<byte>.Shared.Rent(ReadBytes);
            var length = await stream.ReadAsync(bytes.AsMemory(0, ReadBytes), _cancel.Token).ConfigureAwait(false);
            if (length == 0)
            {
                ArrayPool<byte>.Shared.Return(bytes);
                return;
            }

            await _received.Writer.WriteAsync(new Chunk(bytes, length), _cancel.Token).ConfigureAwait(false);
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
                result = await socket.ReceiveAsync(bytes.AsMemory(length, limit + 1 - length), _cancel.Token).ConfigureAwait(false);
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
            await _received.Writer.WriteAsync(new Chunk(bytes, length), _cancel.Token).ConfigureAwait(false);
            if (last) return;
        }
    }

    private async Task WriteAsync()
    {
        try
        {
            await foreach (var chunk in _outgoing.Reader.ReadAllAsync(_cancel.Token).ConfigureAwait(false))
            {
                try
                {
                    if (_stream != null) await _stream.WriteAsync(chunk.Bytes.AsMemory(0, chunk.Length), _cancel.Token).ConfigureAwait(false);
                    else await _webSocket!.SendAsync(chunk.Bytes.AsMemory(0, chunk.Length), WebSocketMessageType.Binary, true, _cancel.Token).ConfigureAwait(false);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(chunk.Bytes);
                }
            }

            // The connection closed and its last packet is sent: end the transport in order.
            if (_stream != null)
            {
                await _stream.FlushAsync(_cancel.Token).ConfigureAwait(false);
                if (_owner is TcpClient client) client.Client.Shutdown(SocketShutdown.Send);
            }
            else if (_webSocket!.State == WebSocketState.Open)
            {
                await _webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, _cancel.Token).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is IOException or SocketException or WebSocketException or ObjectDisposedException or OperationCanceledException)
        {
            _transportEnded = true;
        }
    }

    private readonly record struct Chunk(byte[] Bytes, int Length);
}
