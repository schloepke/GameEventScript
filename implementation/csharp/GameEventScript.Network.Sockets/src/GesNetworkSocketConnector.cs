// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace GameEventScript.Network.Sockets;

/// <summary>
/// Opens the TCP, WebSocket or UDP transport of a client end. Opening runs in the background: the end's first poll
/// starts it, packets wait for it, and a failure to open ends the transport, so the connection closes with
/// <c>network.transportClosed</c>. One connector serves one client end.
/// </summary>
public sealed class GesNetworkSocketConnector : IGesNetworkConnector
{
    private readonly Func<CancellationToken, Task<GesNetworkSocketSession.Opened>> _open;

    private GesNetworkSocketConnector(GesNetworkTransport transport, Func<CancellationToken, Task<GesNetworkSocketSession.Opened>> open)
    {
        Transport = transport;
        _open = open;
    }

    internal GesNetworkSocketSignal Signal { get; } = new();

    /// <inheritdoc/>
    public GesNetworkTransport Transport { get; }

    /// <summary>Creates a connector that opens a TCP connection for the stream transport.</summary>
    /// <param name="host">The server's host name or address.</param>
    /// <param name="port">The server's port.</param>
    /// <returns>The connector.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="port"/> is not 1 to 65535.</exception>
    public static GesNetworkSocketConnector Tcp(string host, int port)
    {
        Check(host, port);
        return new(GesNetworkTransport.Stream, async cancellationToken =>
        {
            var client = new TcpClient { NoDelay = true };
            try
            {
                await GesNetworkSocketSession.ConnectTcpAsync(client, host, port, cancellationToken).ConfigureAwait(false);
                return new(client.GetStream(), null, null, client);
            }
            catch
            {
                client.Dispose();
                throw;
            }
        });
    }

    /// <summary>Creates a connector that opens a WebSocket for the message transport.</summary>
    /// <param name="uri">The <c>ws</c> or <c>wss</c> endpoint.</param>
    /// <returns>The connector.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="uri"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="uri"/> is no <c>ws</c> or <c>wss</c> URI.</exception>
    public static GesNetworkSocketConnector WebSocket(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri || (uri.Scheme != "ws" && uri.Scheme != "wss")) throw new ArgumentException("The URI must use ws or wss.", nameof(uri));
        return new(GesNetworkTransport.Message, async cancellationToken =>
        {
            var socket = new ClientWebSocket();
            socket.Options.KeepAliveInterval = TimeSpan.Zero;
            try
            {
                await GesNetworkSocketSession.ConnectWebSocketAsync(socket, uri, cancellationToken).ConfigureAwait(false);
                return new(null, socket, null, socket);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        });
    }

    /// <summary>
    /// Creates a connector that opens a UDP socket for the datagram transport. UDP has no connection setup, so a
    /// missing server shows only as the connection's timeout.
    /// </summary>
    /// <param name="host">The server's host name or address; the first address it resolves to is used.</param>
    /// <param name="port">The server's port.</param>
    /// <returns>The connector.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="port"/> is not 1 to 65535.</exception>
    public static GesNetworkSocketConnector Udp(string host, int port)
    {
        Check(host, port);
        return new(GesNetworkTransport.Datagram, async cancellationToken =>
        {
            var socket = await GesNetworkSocketSession.OpenUdpAsync(host, port, cancellationToken).ConfigureAwait(false);
            return new(null, null, socket, socket);
        });
    }

    /// <summary>Starts opening the transport of the client end's connection without blocking.</summary>
    /// <param name="connection">The client connection, of this connector's transport.</param>
    /// <returns>The session, a <see cref="GesNetworkSocketSession"/>.</returns>
    /// <exception cref="ArgumentException">The connection is no client of this transport or uses insecure test keys.</exception>
    public IGesNetworkLink Connect(GesNetworkConnection connection)
    {
        GesNetworkSocketSession.Require(connection, Transport, client: true);
        return GesNetworkSocketSession.Opening(connection, Signal, _open);
    }

    private static void Check(string host, int port)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port), port, "The port is 1 to 65535.");
    }
}
