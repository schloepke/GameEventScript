// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Net;
using GameEventScript.Api;
using GameEventScript.Network;
using GameEventScript.Network.Sockets;

namespace GameEventScript.Tests.Native.Network;

[TestClass]
public sealed class GesNetworkSocketEndTests
{
    private const string Source = """
        wire messages :Commands as (Move)
        message Move(x: :Number integer between 0 and 255)
        wire schema :State as { v: :Number integer between 0 and 255 }

        network :Game as {
          client sends :Commands reliable
          server publishes :State latest
        }
        """;

    private const int Commands = 0;
    private const int State = 1;
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

    private static GesBoundNetwork Game() => GesSchemaSet.Parse(Source).Network("Game").Bind();

    private static GesNetworkOptions Options(GesNetworkTransport transport) =>
        new() { Transport = transport, ClientSecurity = GesNetworkClientSecurity.None, ServerSecurity = GesNetworkServerSecurity.None };

    private sealed class Recorder : IGesNetworkEndHandler
    {
        internal readonly List<string> Events = [];

        public void Handle(GesNetworkEndEvent networkEvent)
        {
            if (networkEvent.Kind == GesNetworkEndEventKind.Received) return;
            Events.Add(networkEvent.Kind switch
            {
                GesNetworkEndEventKind.Message => $"message {networkEvent.Route} {Convert.ToHexString(networkEvent.Payload)}",
                GesNetworkEndEventKind.Disconnected or GesNetworkEndEventKind.ClientDisconnected => $"{networkEvent.Kind} {networkEvent.Code}",
                _ => networkEvent.Kind.ToString(),
            });
        }
    }

    private static (GesNetworkSocketListener Listener, GesNetworkSocketConnector Connector) Transport(GesNetworkTransport transport)
    {
        var any = new IPEndPoint(IPAddress.Loopback, 0);
        var listener = transport switch
        {
            GesNetworkTransport.Stream => GesNetworkSocketListener.ListenTcp(any, Game(), Options(transport)),
            GesNetworkTransport.Message => GesNetworkSocketListener.ListenWebSocket(any, "/game", Game(), Options(transport)),
            _ => GesNetworkSocketListener.ListenUdp(any, Game(), Options(transport)),
        };
        var port = listener.LocalEndPoint.Port;
        var connector = transport switch
        {
            GesNetworkTransport.Stream => GesNetworkSocketConnector.Tcp("127.0.0.1", port),
            GesNetworkTransport.Message => GesNetworkSocketConnector.WebSocket(new Uri($"ws://127.0.0.1:{port}/game")),
            _ => GesNetworkSocketConnector.Udp("127.0.0.1", port),
        };
        return (listener, connector);
    }

    // Polls both ends, waiting for either between polls, until the condition holds.
    private static async Task Run(GesNetworkClientEnd client, Recorder clientEvents, GesNetworkServerEnd server, Recorder serverEvents, Func<bool> done)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < Limit)
        {
            client.Poll(ref clientEvents);
            server.Poll(ref serverEvents);
            if (done()) return;
            using var first = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            var waits = new[] { client.WaitAsync(first.Token).AsTask(), server.WaitAsync(first.Token).AsTask() };
            await Task.WhenAny(waits);
            first.Cancel();
            try
            {
                await Task.WhenAll(waits);
            }
            catch (OperationCanceledException)
            {
                // The other wait was cancelled.
            }
        }

        Assert.Fail($"Not done in time: client {string.Join(", ", clientEvents.Events)}; server {string.Join(", ", serverEvents.Events)}.");
    }

    [TestMethod]
    [DataRow(GesNetworkTransport.Stream)]
    [DataRow(GesNetworkTransport.Message)]
    [DataRow(GesNetworkTransport.Datagram)]
    public async Task EndsConnectExchangeAndCloseOverSockets(GesNetworkTransport transport)
    {
        var (listener, connector) = Transport(transport);
        var clock = new GesSystemClock();
        using var server = new GesNetworkServerEnd(Game(), listener, clock);
        using var client = new GesNetworkClientEnd(Game(), connector, Options(transport), "tok"u8, clock);
        Assert.AreSame(connector, client.Connector);
        Assert.AreSame(listener, server.Acceptor);
        var clientEvents = new Recorder();
        var serverEvents = new Recorder();

        // Queued before the handshake, sent once connected.
        Assert.AreEqual(GesNetworkSendStatus.Queued, client.Send(Commands, [1]));
        await Run(client, clientEvents, server, serverEvents, () => serverEvents.Events.Contains("message 0 01"));
        CollectionAssert.AreEqual(new[] { "Connected" }, clientEvents.Events);
        CollectionAssert.AreEqual(new[] { "Connecting", "ClientConnected", "message 0 01" }, serverEvents.Events);

        Assert.AreEqual(GesNetworkSendStatus.Queued, server.Publish(State, [7]));
        await Run(client, clientEvents, server, serverEvents, () => clientEvents.Events.Contains("message 1 07"));

        client.Close("bye");
        await Run(client, clientEvents, server, serverEvents, () => server.ClientCount == 0);
        CollectionAssert.AreEqual(new[] { "Connected", "message 1 07", "Disconnected network.closed" }, clientEvents.Events);
        Assert.AreEqual("ClientDisconnected network.closed", serverEvents.Events[^1]);
    }

    [TestMethod]
    public async Task AConnectorThatCannotOpenEndsTheTransport()
    {
        var clock = new GesSystemClock();
        using var client = new GesNetworkClientEnd(Game(), GesNetworkSocketConnector.Tcp("127.0.0.1", 1), Options(GesNetworkTransport.Stream), default, clock);
        var events = new Recorder();
        var watch = Stopwatch.StartNew();
        while (events.Events.Count == 0 && watch.Elapsed < Limit)
        {
            client.Poll(ref events);
            if (events.Events.Count == 0) await client.WaitAsync();
        }

        CollectionAssert.AreEqual(new[] { "Disconnected network.transportClosed" }, events.Events);
    }

    [TestMethod]
    public void WaitReturnsWhenBytesArrive()
    {
        var (listener, connector) = Transport(GesNetworkTransport.Stream);
        var clock = new GesSystemClock();
        using var server = new GesNetworkServerEnd(Game(), listener, clock);
        using var client = new GesNetworkClientEnd(Game(), connector, Options(GesNetworkTransport.Stream), default, clock);
        var clientEvents = new Recorder();
        var serverEvents = new Recorder();
        Run(client, clientEvents, server, serverEvents, () => server.ClientCount == 1 && clientEvents.Events.Count == 1).GetAwaiter().GetResult();

        // Without input the server end has no deadline before its keepalive; the client's next poll sends and wakes it.
        var sender = new Thread(() =>
        {
            Thread.Sleep(100);
            client.Send(Commands, [3]);
            client.Poll(ref clientEvents);
        });
        var watch = Stopwatch.StartNew();
        sender.Start();
        while (!serverEvents.Events.Contains("message 0 03") && watch.Elapsed < Limit)
        {
            server.Wait();
            server.Poll(ref serverEvents);
        }

        sender.Join();
        CollectionAssert.Contains(serverEvents.Events, "message 0 03");
        Assert.ThrowsExactly<ArgumentException>(() => GesNetworkPair.Synchronous(Game(), default).Client.Wait());
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => GesNetworkSocketConnector.Udp("x", 0));
        Assert.ThrowsExactly<ArgumentException>(() => GesNetworkSocketConnector.WebSocket(new Uri("http://x/")));
    }
}
