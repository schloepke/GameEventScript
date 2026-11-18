// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using GameEventScript.Api;

namespace GameEventScript.Network.Sockets;

/// <summary>
/// Accepts TCP, WebSocket or UDP clients for a server. A background task accepts sockets and, for WebSocket, answers
/// the HTTP upgrade, or for UDP reads the shared socket, validates unknown addresses and routes each datagram to its
/// client; the host takes each ready client with <see cref="Accept"/> on its own thread. At most 64 clients wait or
/// upgrade at once, accepting pauses while they do, and one IP address holds at most 8 of them; its further sockets
/// are closed.
/// </summary>
public sealed partial class GesNetworkSocketListener : IDisposable
{
    private const int MaxRequestBytes = 8192;
    private const int PendingClients = 64;
    private const int PendingPerAddress = 8;
    private static readonly TimeSpan UpgradeTimeout = TimeSpan.FromSeconds(10);
    private const string WebSocketGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    private readonly TcpListener? _listener;
    private readonly GesBoundNetwork _network;
    private readonly GesNetworkOptions _options;
    private readonly string? _path;
    private readonly Channel<Pending> _pending = Channel.CreateBounded<Pending>(new BoundedChannelOptions(PendingClients) { SingleReader = true });
    // The places of the clients that wait or upgrade; accepting waits for one.
    private readonly SemaphoreSlim _places = new(PendingClients);
    // The waiting and upgrading clients of each IP address, under its own lock.
    private readonly Dictionary<IPAddress, int> _addresses = [];
    private readonly CancellationTokenSource _cancel = new();
    private bool _disposed;

    private GesNetworkSocketListener(IPEndPoint endPoint, GesBoundNetwork network, GesNetworkOptions? options, GesNetworkTransport transport, string? path)
    {
        ArgumentNullException.ThrowIfNull(endPoint);
        _network = network ?? throw new ArgumentNullException(nameof(network));
        options ??= new GesNetworkOptions { Transport = transport };
        if (options.Transport != transport) throw new ArgumentException($"The options must use the {transport} transport.", nameof(options));
        if (options.InsecureTestKeySeed.HasValue) throw new ArgumentException("A socket transport refuses insecure test keys.", nameof(options));
        // Creating a connection checks the options, so a listener never accepts clients it cannot serve.
        _ = GesNetworkConnection.CreateServer(network, 0, options);
        _options = options;
        _path = path;
        if (transport == GesNetworkTransport.Datagram)
        {
            LocalEndPoint = StartDatagrams(endPoint);
            return;
        }

        _listener = new TcpListener(endPoint);
        _listener.Start();
        LocalEndPoint = (IPEndPoint)_listener.LocalEndpoint;
        _ = Task.Run(AcceptLoopAsync);
    }

    /// <summary>Gets the address and port the listener accepts on, with the port the system chose for port 0.</summary>
    public IPEndPoint LocalEndPoint { get; }

    /// <summary>Starts accepting TCP clients for server connections of the stream transport.</summary>
    /// <param name="endPoint">The local address and port; port 0 lets the system choose.</param>
    /// <param name="network">The bound network of every session.</param>
    /// <param name="options">The options of every server connection, using <see cref="GesNetworkTransport.Stream"/>; the defaults when omitted.</param>
    /// <returns>The listener.</returns>
    /// <exception cref="ArgumentException">The options use another transport, name an insecure test key seed or are out of range, or name no key while their lowest security level is above none, as the defaults do.</exception>
    /// <exception cref="SocketException">The address cannot be bound.</exception>
    public static GesNetworkSocketListener ListenTcp(IPEndPoint endPoint, GesBoundNetwork network, GesNetworkOptions? options = null) =>
        new(endPoint, network, options, GesNetworkTransport.Stream, null);

    /// <summary>Starts accepting WebSocket clients for server connections of the message transport.</summary>
    /// <param name="endPoint">The local address and port; port 0 lets the system choose.</param>
    /// <param name="path">The request path clients upgrade on, starting with <c>/</c>; other paths receive 404.</param>
    /// <param name="network">The bound network of every session.</param>
    /// <param name="options">The options of every server connection, using <see cref="GesNetworkTransport.Message"/>; the defaults when omitted.</param>
    /// <returns>The listener. It speaks plain <c>ws</c>; a browser client needs <c>wss</c> through a TLS-terminating proxy.</returns>
    /// <exception cref="ArgumentException">The path does not start with <c>/</c>, or the options use another transport, name an insecure test key seed or are out of range, or name no key while their lowest security level is above none, as the defaults do.</exception>
    /// <exception cref="SocketException">The address cannot be bound.</exception>
    public static GesNetworkSocketListener ListenWebSocket(IPEndPoint endPoint, string path, GesBoundNetwork network, GesNetworkOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!path.StartsWith('/')) throw new ArgumentException("The path starts with '/'.", nameof(path));
        return new(endPoint, network, options, GesNetworkTransport.Message, path);
    }

    /// <summary>
    /// Takes the next client that is ready, without blocking, and creates its server connection at
    /// <paramref name="now"/>. Call <see cref="GesNetworkSocketSession.Update"/> to run it.
    /// </summary>
    /// <param name="now">The current time in microseconds, from which the connection's timeout runs.</param>
    /// <returns>The session of the client, or null when no client is ready.</returns>
    /// <exception cref="ObjectDisposedException">The listener is disposed.</exception>
    public GesNetworkSocketSession? Accept(long now)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_pending.Reader.TryRead(out var pending)) return null;
        Leave(pending.Address);
        var connection = GesNetworkConnection.CreateServer(_network, now, _options);
        if (pending.Peer != null) return GesNetworkSocketSession.FromPeer(connection, pending.Peer);
        return pending.WebSocket != null
            ? GesNetworkSocketSession.FromWebSocket(connection, pending.WebSocket, pending.Owner!)
            : GesNetworkSocketSession.FromStream(connection, pending.Owner!.GetStream(), pending.Owner!);
    }

    /// <summary>
    /// Stops accepting and closes every client not yet taken. TCP and WebSocket sessions already taken stay open; UDP
    /// sessions share the listener's socket, so they stop carrying packets and their connections time out.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cancel.Cancel();
        _listener?.Stop();
        StopDatagrams();
        _pending.Writer.TryComplete();
        while (_pending.Reader.TryRead(out var pending)) pending.Dispose();
        _cancel.Dispose();
    }

    private async Task AcceptLoopAsync()
    {
        try
        {
            while (true)
            {
                await _places.WaitAsync(_cancel.Token).ConfigureAwait(false);
                TcpClient client;
                try
                {
                    client = await _listener!.AcceptTcpClientAsync(_cancel.Token).ConfigureAwait(false);
                }
                catch
                {
                    _places.Release();
                    throw;
                }

                var address = (client.Client.RemoteEndPoint as IPEndPoint)?.Address;
                if (!Enter(address))
                {
                    _places.Release();
                    client.Dispose();
                    continue;
                }

                client.NoDelay = true;
                if (_path == null) Offer(new Pending(client, null, null, address));
                else _ = UpgradeAsync(client, address);
            }
        }
        catch (Exception exception) when (exception is SocketException or ObjectDisposedException or OperationCanceledException)
        {
            // The listener stopped.
        }
    }

    // Queues a client that holds a place; a disposed listener closes it.
    private void Offer(Pending pending)
    {
        if (_pending.Writer.TryWrite(pending)) return;
        Leave(pending.Address);
        pending.Dispose();
    }

    // Counts a client of the address; false when the address already holds its limit or the listener is disposed.
    private bool Enter(IPAddress? address)
    {
        if (address == null) return !_disposed;
        lock (_addresses)
        {
            if (_disposed) return false;
            _addresses.TryGetValue(address, out var count);
            if (count >= PendingPerAddress) return false;
            _addresses[address] = count + 1;
            return true;
        }
    }

    // Gives back the place of a client that no longer waits or upgrades.
    private void Leave(IPAddress? address)
    {
        if (address != null)
        {
            lock (_addresses)
            {
                if (_addresses.TryGetValue(address, out var count))
                {
                    if (count <= 1) _addresses.Remove(address);
                    else _addresses[address] = count - 1;
                }
            }
        }

        _places.Release();
    }

    private async Task UpgradeAsync(TcpClient client, IPAddress? address)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_cancel.Token);
            timeout.CancelAfter(UpgradeTimeout);
            var stream = client.GetStream();
            var request = await ReadRequestAsync(stream, timeout.Token).ConfigureAwait(false);
            var response = request == null ? Status(400, "Bad Request") : Answer(request, _path!);
            await stream.WriteAsync(Encoding.ASCII.GetBytes(response.Text), timeout.Token).ConfigureAwait(false);
            if (!response.Upgrade)
            {
                Leave(address);
                client.Dispose();
                return;
            }

            var socket = WebSocket.CreateFromStream(stream, isServer: true, subProtocol: null, keepAliveInterval: TimeSpan.Zero);
            Offer(new Pending(client, socket, null, address));
        }
        catch (Exception exception) when (exception is IOException or SocketException or ObjectDisposedException or OperationCanceledException)
        {
            Leave(address);
            client.Dispose();
        }
    }

    // Reads the request head byte by byte, so no WebSocket byte after it is consumed; null when it is too long or
    // the stream ends first.
    private static async Task<string?> ReadRequestAsync(Stream stream, CancellationToken cancellationToken)
    {
        var bytes = new byte[MaxRequestBytes];
        var length = 0;
        while (length < MaxRequestBytes)
        {
            if (await stream.ReadAsync(bytes.AsMemory(length, 1), cancellationToken).ConfigureAwait(false) == 0) return null;
            length++;
            if (length >= 4 && bytes[length - 4] == '\r' && bytes[length - 3] == '\n' && bytes[length - 2] == '\r' && bytes[length - 1] == '\n')
                return Encoding.ASCII.GetString(bytes, 0, length);
        }

        return null;
    }

    internal static (string Text, bool Upgrade) Answer(string request, string path)
    {
        var lines = request.Split("\r\n");
        var start = lines[0].Split(' ');
        if (start.Length != 3 || start[0] != "GET" || !start[2].StartsWith("HTTP/1.", StringComparison.Ordinal)) return Status(400, "Bad Request");
        var target = start[1];
        var query = target.IndexOf('?');
        if ((query < 0 ? target : target[..query]) != path) return Status(404, "Not Found");
        string? upgrade = null, connection = null, version = null, key = null;
        foreach (var line in lines.AsSpan(1))
        {
            var colon = line.IndexOf(':');
            if (colon <= 0) continue;
            var name = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            if (name.Equals("Upgrade", StringComparison.OrdinalIgnoreCase)) upgrade = value;
            else if (name.Equals("Connection", StringComparison.OrdinalIgnoreCase)) connection = value;
            else if (name.Equals("Sec-WebSocket-Version", StringComparison.OrdinalIgnoreCase)) version = value;
            else if (name.Equals("Sec-WebSocket-Key", StringComparison.OrdinalIgnoreCase)) key = value;
        }

        if (!"websocket".Equals(upgrade, StringComparison.OrdinalIgnoreCase)
            || connection == null
            || !connection.Split(',').Any(token => token.Trim().Equals("Upgrade", StringComparison.OrdinalIgnoreCase))
            || key == null)
            return Status(400, "Bad Request");
        if (version != "13") return ("HTTP/1.1 426 Upgrade Required\r\nSec-WebSocket-Version: 13\r\nContent-Length: 0\r\nConnection: close\r\n\r\n", false);
        var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + WebSocketGuid)));
        return ($"HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {accept}\r\n\r\n", true);
    }

    private static (string Text, bool Upgrade) Status(int code, string reason) =>
        ($"HTTP/1.1 {code} {reason}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n", false);

    // A client that waits for the host, holding a place of its IP address.
    private readonly record struct Pending(TcpClient? Owner, WebSocket? WebSocket, GesNetworkDatagramPeer? Peer, IPAddress? Address) : IDisposable
    {
        public void Dispose()
        {
            WebSocket?.Dispose();
            Owner?.Dispose();
            Peer?.Discard();
        }
    }
}
