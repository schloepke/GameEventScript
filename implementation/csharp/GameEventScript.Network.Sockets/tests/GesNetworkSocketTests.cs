// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using GameEventScript.Api;
using GameEventScript.Network;
using GameEventScript.Network.Sockets;

namespace GameEventScript.Tests.Native.Network;

[TestClass]
public sealed class GesNetworkSocketTests
{
    private const string Source = """
        wire schema :Blob as { data: :List(:Number integer between 0 and 255) max 6000 }
        wire messages :Commands as (Move)
        message Move(x: :Number integer between 0 and 255)

        network :Game as {
          client sends :Commands reliable
          server sends :Blob reliable
        }
        """;

    private const int Commands = 0;
    private const int Blob = 1;
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

    private static readonly Stopwatch Clock = Stopwatch.StartNew();

    private static long Now => Clock.Elapsed.Ticks / 10;

    private static GesBoundNetwork Game() => GesSchemaSet.Parse(Source).Network("Game").Bind();

    private static IPEndPoint Loopback => new(IPAddress.Loopback, 0);

    private static readonly GesNetworkKey ServerKey = GesNetworkKey.FromPrivateKey(Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());

    private static GesNetworkOptions Options(GesNetworkTransport transport) => new() { Transport = transport, MinimumSecurity = GesNetworkSecurity.None };

    private static GesNetworkOptions SecureServer(GesNetworkTransport transport) => new() { Transport = transport, Key = ServerKey };

    private static GesNetworkOptions SecureClient(GesNetworkTransport transport) => new() { Transport = transport, ServerPublicKey = ServerKey.PublicKey.ToArray() };

    private static string? Next(GesNetworkConnection connection)
    {
        var next = connection.NextEvent();
        return next.Kind switch
        {
            GesNetworkEventKind.None => null,
            GesNetworkEventKind.Connecting => "connecting " + Encoding.UTF8.GetString(next.Payload),
            GesNetworkEventKind.Connected => "connected",
            GesNetworkEventKind.Message => $"message {next.Route} {Convert.ToHexString(next.Payload)}",
            _ => $"closed {next.Code} {(next.ByPeer ? "peer" : "local")}",
        };
    }

    // Updates the sessions until the session's next event arrives.
    private static string Await(GesNetworkSocketSession session, params GesNetworkSocketSession[] others)
    {
        var deadline = Clock.Elapsed + Limit;
        while (Clock.Elapsed < deadline)
        {
            foreach (var other in others) other.Update(Now);
            session.Update(Now);
            if (Next(session.Connection) is { } next) return next;
            Thread.Sleep(1);
        }

        throw new AssertFailedException("No event arrived in time.");
    }

    // Updates the given client sessions, which a datagram handshake needs, until the listener offers a client.
    private static GesNetworkSocketSession AcceptOne(GesNetworkSocketListener listener, params GesNetworkSocketSession[] clients)
    {
        var deadline = Clock.Elapsed + Limit;
        while (Clock.Elapsed < deadline)
        {
            foreach (var client in clients) client.Update(Now);
            if (listener.Accept(Now) is { } session) return session;
            Thread.Sleep(1);
        }

        throw new AssertFailedException("No client was accepted in time.");
    }

    private static byte[] Bytes(int length)
    {
        var bytes = new byte[length];
        for (var index = 0; index < length; index++) bytes[index] = (byte)(index * 7 + 3);
        return bytes;
    }

    private static void RoundTrip(GesNetworkSocketSession client, GesNetworkSocketSession server)
    {
        Assert.AreEqual("connecting tok", Await(server, client));
        server.Connection.Accept();
        Assert.AreEqual("connected", Await(server, client));
        Assert.AreEqual("connected", Await(client, server));

        var command = new byte[] { 1 };
        Assert.AreEqual(GesNetworkSendStatus.Queued, client.Connection.Send(Commands, command));
        Assert.AreEqual("message 0 01", Await(server, client));

        // Larger than one packet, so the message arrives in fragments.
        var blob = Bytes(6000);
        Assert.AreEqual(GesNetworkSendStatus.Queued, server.Connection.Send(Blob, blob));
        Assert.AreEqual("message 1 " + Convert.ToHexString(blob), Await(client, server));

        server.Connection.Close("bye");
        Assert.AreEqual("closed network.closed local", Next(server.Connection));
        Assert.AreEqual("closed network.closed peer", Await(client, server));
    }

    [TestMethod]
    public async Task TcpCarriesASession()
    {
        var network = Game();
        using var listener = GesNetworkSocketListener.ListenTcp(Loopback, network, Options(GesNetworkTransport.Stream));
        var connection = GesNetworkConnection.CreateClient(network, "tok"u8, Now, Options(GesNetworkTransport.Stream));
        using var client = await GesNetworkSocketSession.ConnectTcpAsync("127.0.0.1", listener.LocalEndPoint.Port, connection);
        using var server = AcceptOne(listener);
        RoundTrip(client, server);
    }

    [TestMethod]
    public async Task WebSocketCarriesASession()
    {
        var network = Game();
        using var listener = GesNetworkSocketListener.ListenWebSocket(Loopback, "/game", network, Options(GesNetworkTransport.Message));
        var connection = GesNetworkConnection.CreateClient(network, "tok"u8, Now, Options(GesNetworkTransport.Message));
        var uri = new Uri($"ws://127.0.0.1:{listener.LocalEndPoint.Port}/game?room=1");
        using var client = await GesNetworkSocketSession.ConnectWebSocketAsync(uri, connection);
        using var server = AcceptOne(listener);
        RoundTrip(client, server);
    }

    [TestMethod]
    public async Task UdpCarriesASession()
    {
        var network = Game();
        using var listener = GesNetworkSocketListener.ListenUdp(Loopback, network, Options(GesNetworkTransport.Datagram));
        var connection = GesNetworkConnection.CreateClient(network, "tok"u8, Now, Options(GesNetworkTransport.Datagram));
        using var client = await GesNetworkSocketSession.ConnectUdpAsync("127.0.0.1", listener.LocalEndPoint.Port, connection);
        using var server = AcceptOne(listener, client);
        RoundTrip(client, server);
    }

    [TestMethod]
    public async Task TcpCarriesAnEncryptedSession()
    {
        var network = Game();
        using var listener = GesNetworkSocketListener.ListenTcp(Loopback, network, SecureServer(GesNetworkTransport.Stream));
        var connection = GesNetworkConnection.CreateClient(network, "tok"u8, Now, SecureClient(GesNetworkTransport.Stream));
        using var client = await GesNetworkSocketSession.ConnectTcpAsync("127.0.0.1", listener.LocalEndPoint.Port, connection);
        using var server = AcceptOne(listener);
        RoundTrip(client, server);
        Assert.AreEqual(GesNetworkSecurity.Verified, connection.Security);
    }

    [TestMethod]
    public async Task UdpCarriesAnEncryptedSession()
    {
        var network = Game();
        using var listener = GesNetworkSocketListener.ListenUdp(Loopback, network, SecureServer(GesNetworkTransport.Datagram));
        var connection = GesNetworkConnection.CreateClient(network, "tok"u8, Now, SecureClient(GesNetworkTransport.Datagram));
        using var client = await GesNetworkSocketSession.ConnectUdpAsync("127.0.0.1", listener.LocalEndPoint.Port, connection);
        using var server = AcceptOne(listener, client);
        RoundTrip(client, server);
        Assert.AreEqual(GesNetworkSecurity.Verified, connection.Security);
    }

    [TestMethod]
    public void TheDefaultOptionsNeedAKey()
    {
        Assert.ThrowsExactly<ArgumentException>(() => GesNetworkSocketListener.ListenTcp(Loopback, Game()));
    }

    [TestMethod]
    public async Task UdpValidatesAnUnknownAddressWithoutState()
    {
        var network = Game();
        using var listener = GesNetworkSocketListener.ListenUdp(Loopback, network, Options(GesNetworkTransport.Datagram));
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        await socket.ConnectAsync(listener.LocalEndPoint);
        var buffer = new byte[2048];

        // Anything but a full-size hello is dropped without an answer.
        await socket.SendAsync(new byte[] { 3, 0, 0, 0 }, SocketFlags.None);
        var hello = new byte[2048];
        var length = GesNetworkConnection.CreateClient(network, "tok"u8, Now, Options(GesNetworkTransport.Datagram)).Transmit(hello);
        Assert.AreEqual(1200, length);
        await socket.SendAsync(hello.AsMemory(0, length), SocketFlags.None);
        using var timeout = new CancellationTokenSource(Limit);
        var received = await socket.ReceiveAsync(buffer, SocketFlags.None, timeout.Token);
        Assert.AreEqual(GesNetworkCookies.RetryBytes, received);
        Assert.AreEqual(2, buffer[0]);
        Assert.AreEqual(24, buffer[1]);
        Assert.IsNull(listener.Accept(Now));
    }

    [TestMethod]
    public async Task AClosedUdpSessionReleasesItsAddress()
    {
        var network = Game();
        using var listener = GesNetworkSocketListener.ListenUdp(Loopback, network, Options(GesNetworkTransport.Datagram));
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(Loopback);
        await socket.ConnectAsync(listener.LocalEndPoint);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var connection = GesNetworkConnection.CreateClient(network, "tok"u8, Now, Options(GesNetworkTransport.Datagram));
            using var server = await Handshake(socket, connection, listener);
            server.Connection.Refuse("full");
            server.Update(Now);
            Assert.AreEqual(GesNetworkState.Closed, server.Connection.State);
            var deadline = Clock.Elapsed + Limit;
            var buffer = new byte[2048];
            while (connection.State != GesNetworkState.Closed && Clock.Elapsed < deadline)
            {
                using var timeout = new CancellationTokenSource(Limit);
                var length = await socket.ReceiveAsync(buffer, SocketFlags.None, timeout.Token);
                connection.Receive(buffer.AsSpan(0, length), Now);
            }

            Assert.AreEqual(GesNetworkState.Closed, connection.State);
        }
    }

    // Runs the client side of a datagram handshake by hand on a socket until the listener offers the server session.
    private static async Task<GesNetworkSocketSession> Handshake(Socket socket, GesNetworkConnection connection, GesNetworkSocketListener listener)
    {
        var buffer = new byte[2048];
        var deadline = Clock.Elapsed + Limit;
        while (Clock.Elapsed < deadline)
        {
            int length;
            while ((length = connection.Transmit(buffer)) > 0) await socket.SendAsync(buffer.AsMemory(0, length), SocketFlags.None);
            if (listener.Accept(Now) is { } session)
            {
                Assert.AreEqual("connecting tok", Await(session));
                return session;
            }

            if (socket.Available > 0)
            {
                length = await socket.ReceiveAsync(buffer, SocketFlags.None);
                connection.Receive(buffer.AsSpan(0, length), Now);
            }

            connection.Advance(Now);
            Thread.Sleep(1);
        }

        throw new AssertFailedException("No client was accepted in time.");
    }

    [TestMethod]
    public async Task TheEndOfTheTransportClosesTheConnection()
    {
        var network = Game();
        using var listener = GesNetworkSocketListener.ListenTcp(Loopback, network, Options(GesNetworkTransport.Stream));
        var connection = GesNetworkConnection.CreateClient(network, "tok"u8, Now, Options(GesNetworkTransport.Stream));
        var client = await GesNetworkSocketSession.ConnectTcpAsync("127.0.0.1", listener.LocalEndPoint.Port, connection);
        using var server = AcceptOne(listener);
        Assert.AreEqual("connecting tok", Await(server, client));
        client.Dispose();
        Assert.AreEqual("closed network.transportClosed local", Await(server));
    }

    [TestMethod]
    public async Task AWebSocketTextMessageIsMalformed()
    {
        var network = Game();
        using var listener = GesNetworkSocketListener.ListenWebSocket(Loopback, "/game", network, Options(GesNetworkTransport.Message));
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri($"ws://127.0.0.1:{listener.LocalEndPoint.Port}/game"), CancellationToken.None);
        using var server = AcceptOne(listener);
        await socket.SendAsync("hello"u8.ToArray(), WebSocketMessageType.Text, true, CancellationToken.None);
        Assert.AreEqual("closed network.malformed local", Await(server));
    }

    [TestMethod]
    public async Task AnOversizedWebSocketMessageIsTooLarge()
    {
        var network = Game();
        using var listener = GesNetworkSocketListener.ListenWebSocket(Loopback, "/game", network, Options(GesNetworkTransport.Message));
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri($"ws://127.0.0.1:{listener.LocalEndPoint.Port}/game"), CancellationToken.None);
        using var server = AcceptOne(listener);
        await socket.SendAsync(new byte[16385], WebSocketMessageType.Binary, true, CancellationToken.None);
        Assert.AreEqual("closed network.packetTooLarge local", Await(server));
    }

    [TestMethod]
    public async Task AnotherPathIsNotFound()
    {
        using var listener = GesNetworkSocketListener.ListenWebSocket(Loopback, "/game", Game(), Options(GesNetworkTransport.Message));
        using var socket = new ClientWebSocket();
        var failure = await Assert.ThrowsExactlyAsync<WebSocketException>(
            () => socket.ConnectAsync(new Uri($"ws://127.0.0.1:{listener.LocalEndPoint.Port}/other"), CancellationToken.None));
        StringAssert.Contains(failure.Message, "404");
        Assert.IsNull(listener.Accept(Now));
    }

    [TestMethod]
    public void TheUpgradeAnswerFollowsRfc6455()
    {
        const string request = "GET /game HTTP/1.1\r\nHost: x\r\nUpgrade: websocket\r\nConnection: keep-alive, Upgrade\r\n"
            + "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\nSec-WebSocket-Version: 13\r\n\r\n";
        var (text, upgrade) = GesNetworkSocketListener.Answer(request, "/game");
        Assert.IsTrue(upgrade);
        StringAssert.Contains(text, "Sec-WebSocket-Accept: s3pPLMBiTxaQ9kYGzzhZRbK+xOo=\r\n");
        Assert.IsFalse(GesNetworkSocketListener.Answer(request.Replace("Version: 13", "Version: 8"), "/game").Upgrade);
        StringAssert.StartsWith(GesNetworkSocketListener.Answer(request.Replace("Version: 13", "Version: 8"), "/game").Text, "HTTP/1.1 426");
        StringAssert.StartsWith(GesNetworkSocketListener.Answer(request.Replace("Upgrade: websocket\r\n", ""), "/game").Text, "HTTP/1.1 400");
        StringAssert.StartsWith(GesNetworkSocketListener.Answer(request.Replace("GET", "POST"), "/game").Text, "HTTP/1.1 400");
    }

    [TestMethod]
    public async Task TheTransportMustMatch()
    {
        var network = Game();
        Assert.ThrowsExactly<ArgumentException>(() => GesNetworkSocketListener.ListenTcp(Loopback, network, Options(GesNetworkTransport.Message)));
        Assert.ThrowsExactly<ArgumentException>(() => GesNetworkSocketListener.ListenWebSocket(Loopback, "game", network, Options(GesNetworkTransport.Message)));
        var message = GesNetworkConnection.CreateClient(network, "tok"u8, Now);
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => GesNetworkSocketSession.ConnectTcpAsync("127.0.0.1", 1, message));
        var server = GesNetworkConnection.CreateServer(network, Now, Options(GesNetworkTransport.Message));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => GesNetworkSocketSession.ConnectWebSocketAsync(new Uri("ws://127.0.0.1:1/"), server));
        Assert.ThrowsExactly<ArgumentException>(() => GesNetworkSocketListener.ListenUdp(Loopback, network, Options(GesNetworkTransport.Stream)));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => GesNetworkSocketSession.ConnectUdpAsync("127.0.0.1", 1, message));
    }
}
