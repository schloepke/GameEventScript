// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Network;

namespace GameEventScript.Tests.Native.Network;

[TestClass]
public sealed class GesNetworkEndTests
{
    private const string Source = """
        wire schema :Pos as { v: :Number integer between 0 and 255 }
        wire schema :Aim as { v: :Number integer between 0 and 255 }
        wire schema :State as { v: :Number integer between 0 and 255 }
        wire schema :Log as { v: :Number integer between 0 and 255 }
        wire messages :Commands as (Move)
        message Move(x: :Number integer between 0 and 255)

        network :Game as {
          client sends :Commands reliable
          client publishes :Pos latest
          client publishes :Aim latest
          server publishes :State latest
          server publishes :Log reliable
        }
        """;

    private const int Commands = 0;
    private const int Pos = 1;
    private const int Aim = 2;
    private const int State = 3;
    private const int Log = 4;

    private static GesBoundNetwork Network() => GesSchemaSet.Parse(Source).Network("Game").Bind();

    private sealed class Recorder : IGesNetworkEndHandler
    {
        internal readonly List<string> Events = [];
        internal Action<GesNetworkEndEventKind, int>? OnEvent;

        public void Handle(GesNetworkEndEvent networkEvent)
        {
            Events.Add(networkEvent.Kind switch
            {
                GesNetworkEndEventKind.Message => $"message {networkEvent.Route} {Convert.ToHexString(networkEvent.Payload)}",
                GesNetworkEndEventKind.Tick => $"tick {networkEvent.TickNumber}",
                GesNetworkEndEventKind.Connecting => $"connecting {networkEvent.Client} {Convert.ToHexString(networkEvent.Payload)}",
                GesNetworkEndEventKind.Disconnected or GesNetworkEndEventKind.ClientDisconnected =>
                    $"{networkEvent.Kind} {networkEvent.Client} {networkEvent.Code} {networkEvent.ByPeer}",
                _ => $"{networkEvent.Kind} {networkEvent.Client}",
            });
            OnEvent?.Invoke(networkEvent.Kind, networkEvent.Client);
        }

        internal List<string> Take()
        {
            var taken = Events.ToList();
            Events.Clear();
            return taken;
        }
    }

    private static (Recorder Client, Recorder Server) Poll(GesNetworkPair pair, int rounds = 1, Recorder? client = null, Recorder? server = null)
    {
        client ??= new Recorder();
        server ??= new Recorder();
        for (var round = 0; round < rounds; round++)
        {
            pair.Client.Poll(ref client);
            pair.Server.Poll(ref server);
        }

        return (client, server);
    }

    [TestMethod]
    public void SynchronousPairIsConnectedAndDeliversBeforeTheNextPoll()
    {
        using var pair = GesNetworkPair.Synchronous(Network(), "t"u8);
        Assert.AreEqual(GesNetworkState.Connected, pair.Client.Connection.State);
        Assert.AreEqual(1, pair.Server.ClientCount);
        var (client, server) = Poll(pair);
        CollectionAssert.AreEqual(new[] { "Connected -1" }, client.Take());
        CollectionAssert.AreEqual(new[] { "ClientConnected 0" }, server.Take());

        Assert.AreEqual(GesNetworkSendStatus.Queued, pair.Client.Send(Commands, [5]));
        Assert.AreEqual(GesNetworkSendStatus.Queued, pair.Server.Publish(State, [7]));
        Assert.AreEqual(GesNetworkSendStatus.InvalidRoute, pair.Server.Send(Commands, [1]), "A route the server does not send.");
        Poll(pair, 1, client, server);
        CollectionAssert.AreEqual(new[] { $"message {State} 07", "Received -1" }, client.Take());
        CollectionAssert.AreEqual(new[] { $"message {Commands} 05", "Received -1" }, server.Take());
    }

    [TestMethod]
    public void ServerEndSendsToOneClientAndChecksRoutes()
    {
        using var pair = GesNetworkPair.Synchronous(Network(), "t"u8);
        var (client, server) = Poll(pair);
        client.Take();
        server.Take();
        Assert.AreEqual(GesNetworkSendStatus.InvalidRoute, pair.Server.Send(State, [1]));
        Assert.AreEqual(GesNetworkSendStatus.InvalidRoute, pair.Server.Publish(Pos, [1]));
        Assert.AreEqual(GesNetworkSendStatus.InvalidRoute, pair.Client.Publish(State, [1]));
        Assert.ThrowsExactly<ArgumentException>(() => pair.Server.Close(5, "x"));
        Assert.AreEqual(GesNetworkSendStatus.Queued, pair.Server.Publish(Log, [9]));
        pair.Server.Unsubscribe(0, Log);
        Assert.AreEqual(GesNetworkSendStatus.Queued, pair.Server.Publish(Log, [8]), "An unsubscribed client is skipped.");
        Poll(pair, 1, client, server);
        CollectionAssert.AreEqual(new[] { $"message {Log} 09", "Received -1" }, client.Take());
    }

    [TestMethod]
    public void ServerTicksReachTheClientAndAClientThatJoinsDuringOne()
    {
        using var pair = GesNetworkPair.Synchronous(Network(), "t"u8);
        var (client, server) = Poll(pair);
        client.Take();
        pair.Server.Publish(State, [1]);
        pair.Server.BeginTick();
        pair.Server.Include(State);
        Assert.AreEqual(GesNetworkSendStatus.Queued, pair.Server.Publish(Log, [2]));
        Assert.AreEqual(GesNetworkSendStatus.Queued, pair.Server.EndTick());
        pair.Server.BeginTick();
        pair.Server.Publish(State, [3]);
        pair.Server.EndTick();
        Poll(pair, 1, client, server);
        CollectionAssert.AreEqual(new[] { $"message {State} 01", $"message {Log} 02", "tick 0", $"message {State} 03", "tick 1", "Received -1" }, client.Take());
        Assert.ThrowsExactly<InvalidOperationException>(() => pair.Server.EndTick());
        pair.Server.BeginTick();
        Assert.ThrowsExactly<InvalidOperationException>(() => pair.Server.BeginTick());
        Assert.ThrowsExactly<ArgumentException>(() => pair.Server.Include(Log));
        pair.Server.EndTick();
    }

    [TestMethod]
    public void AnIncludedStateReachesAClientThatMissedIt()
    {
        using var pair = GesNetworkPair.Synchronous(Network(), "t"u8);
        var (client, server) = Poll(pair);
        client.Take();
        pair.Server.Publish(State, [1]);
        Poll(pair, 1, client, server);
        client.Take();
        pair.Server.Unsubscribe(0, State);
        pair.Server.Publish(State, [2]);
        pair.Server.Subscribe(0, State);
        pair.Server.BeginTick();
        pair.Server.Include(State);
        pair.Server.EndTick();
        Poll(pair, 1, client, server);
        // The client's last state is 01, so the tick sends it the kept 02 instead of naming 01.
        CollectionAssert.AreEqual(new[] { $"message {State} 02", "tick 0", "Received -1" }, client.Take());
    }

    [TestMethod]
    public void ClientEndQueuesUntilTheHandshakeInOrder()
    {
        using var pair = GesNetworkPair.Realtime(Network(), "AB"u8);
        Assert.AreEqual(GesNetworkSendStatus.Queued, pair.Client.Send(Commands, [1]));
        Assert.AreEqual(GesNetworkSendStatus.Queued, pair.Client.Publish(Pos, [1]));
        Assert.AreEqual(GesNetworkSendStatus.Queued, pair.Client.Publish(Pos, [2]));
        Assert.AreEqual(GesNetworkSendStatus.Queued, pair.Client.Publish(Aim, [9]));
        pair.Client.BeginTick();
        Assert.AreEqual(GesNetworkSendStatus.Queued, pair.Client.Publish(Aim, [3]));
        pair.Client.Include(Pos);
        Assert.AreEqual(GesNetworkSendStatus.Queued, pair.Client.EndTick());
        Assert.AreEqual(GesNetworkSendStatus.Queued, pair.Client.Publish(Pos, [4]));
        Assert.AreEqual(GesNetworkSendStatus.InvalidRoute, pair.Client.Send(Pos, [1]));
        Assert.AreEqual(GesNetworkSendStatus.MessageTooLarge, pair.Client.Send(Commands, new byte[100]));
        Assert.ThrowsExactly<ArgumentException>(() =>
        {
            pair.Client.BeginTick();
            try
            {
                pair.Client.Include(Commands);
            }
            finally
            {
                pair.Client.EndTick();
            }
        });

        var (client, server) = Poll(pair, 10);
        CollectionAssert.AreEqual(new[] { "Connected -1" }, client.Events);
        var events = server.Events.Where(entry => entry != "Received -1").ToList();
        // The Aim state the tick replaced and the Pos state older than the tick never travel; as on a connected client,
        // the newer Pos state replaces the tick's pending one before the packet leaves.
        CollectionAssert.AreEqual(
            new[] { "connecting 0 4142", "ClientConnected 0", $"message {Commands} 01", $"message {Pos} 04", $"message {Aim} 03", "tick 0" },
            events,
            string.Join(" | ", events));
    }

    [TestMethod]
    public void AReplacedTickBeforeTheHandshakeKeepsItsStates()
    {
        using var pair = GesNetworkPair.Realtime(Network(), default);
        pair.Client.BeginTick();
        pair.Client.Publish(Pos, [1]);
        pair.Client.Publish(Aim, [2]);
        pair.Client.EndTick();
        pair.Client.BeginTick();
        pair.Client.Publish(Aim, [3]);
        pair.Client.EndTick();
        var (_, server) = Poll(pair, 10);
        // The replaced tick's Pos state becomes a latest state; its Aim state is replaced by the newer tick's.
        CollectionAssert.AreEqual(
            new[] { "connecting 0 ", "ClientConnected 0", $"message {Pos} 01", $"message {Aim} 03", "tick 0" },
            server.Events.Where(entry => entry != "Received -1").ToList());
    }

    [TestMethod]
    public void QueuedReliablePublishesCountTheirModeByte()
    {
        var network = GesSchemaSet.Parse("wire schema :Note as { v: :Number integer between 0 and 255 }\nnetwork :Game as { client publishes :Note reliable }\n")
            .Network("Game").Bind();
        var options = GesNetworkOptions.InProcess();
        options.MaxSendQueueBytes = 3;
        using var pair = GesNetworkPair.Realtime(network, default, options);
        // A one-byte state takes two bytes in the queue, as on the connection, which could not take the second.
        Assert.AreEqual(GesNetworkSendStatus.Queued, pair.Client.Publish(0, [1]));
        Assert.AreEqual(GesNetworkSendStatus.QueueFull, pair.Client.Publish(0, [2]));
        var (_, server) = Poll(pair, 10);
        CollectionAssert.AreEqual(new[] { "connecting 0 ", "ClientConnected 0", "message 0 01" }, server.Events.Where(entry => entry != "Received -1").ToList());
    }

    [TestMethod]
    public void ATickOpenAtTheHandshakeMovesToTheConnection()
    {
        using var pair = GesNetworkPair.Realtime(Network(), default);
        pair.Client.BeginTick();
        pair.Client.Publish(Pos, [1]);
        var (client, server) = Poll(pair, 10);
        Assert.AreEqual(GesNetworkState.Connected, pair.Client.Connection.State);
        pair.Client.Publish(Aim, [2]);
        pair.Client.EndTick();
        Poll(pair, 5, client, server);
        CollectionAssert.AreEqual(
            new[] { "connecting 0 ", "ClientConnected 0", $"message {Pos} 01", $"message {Aim} 02", "tick 0" },
            server.Events.Where(entry => entry != "Received -1").ToList());
    }

    [TestMethod]
    public void ServerEndRefusesAndClosesClients()
    {
        using var pair = GesNetworkPair.Realtime(Network(), default);
        var server = new Recorder();
        server.OnEvent = (kind, id) =>
        {
            if (kind == GesNetworkEndEventKind.Connecting) pair.Server.Refuse(id, "full");
        };
        pair.Client.Send(Commands, [1]);
        var (client, _) = Poll(pair, 10, server: server);
        CollectionAssert.AreEqual(new[] { "Disconnected -1 network.refused True" }, client.Events);
        CollectionAssert.AreEqual(new[] { "connecting 0 ", "ClientDisconnected 0 network.refused False" }, server.Events);
        Assert.AreEqual(0, pair.Server.ClientCount);
        Assert.AreEqual(GesNetworkSendStatus.NotConnected, pair.Client.Send(Commands, [1]));
    }

    [TestMethod]
    public void ClosingAndDisposingEndsReportDisconnects()
    {
        using var pair = GesNetworkPair.Synchronous(Network(), default);
        var (client, server) = Poll(pair);
        client.Take();
        server.Take();
        pair.Client.Close("bye");
        Poll(pair, 1, client, server);
        CollectionAssert.AreEqual(new[] { "Disconnected -1 network.closed False" }, client.Take());
        CollectionAssert.AreEqual(new[] { "ClientDisconnected 0 network.closed True" }, server.Take());
        Assert.AreEqual(0, pair.Server.ClientCount);

        using var second = GesNetworkPair.Synchronous(Network(), default);
        (client, server) = Poll(second);
        server.Take();
        second.Client.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() => second.Client.Poll(ref client));
        second.Server.Poll(ref server);
        CollectionAssert.AreEqual(new[] { "ClientDisconnected 0 network.transportClosed False" }, server.Take());
    }

    [TestMethod]
    public void SynchronousPairAdvancesItsVirtualClock()
    {
        var options = GesNetworkOptions.InProcess();
        options.TimeoutMicroseconds = 1_000_000;
        options.KeepaliveMicroseconds = 200_000;
        using var pair = GesNetworkPair.Synchronous(Network(), default, options);
        Assert.IsTrue(pair.IsSynchronous);
        for (var step = 0; step < 50; step++) pair.Advance(100_000);
        Assert.AreEqual(5_000_000, pair.Now);
        Assert.AreEqual(GesNetworkState.Connected, pair.Client.Connection.State, "Keepalives keep the session.");
        using var realtime = GesNetworkPair.Realtime(Network(), default);
        Assert.ThrowsExactly<InvalidOperationException>(() => realtime.Advance(1));
        var reentrant = new Recorder();
        reentrant.OnEvent = (_, _) =>
        {
            var inner = new Recorder();
            realtime.Client.Poll(ref inner);
        };
        Assert.ThrowsExactly<InvalidOperationException>(() => Poll(realtime, 10, client: reentrant));
    }

    [TestMethod]
    public void BulkSendsBeforeTheHandshakeAreBounded()
    {
        var network = GesSchemaSet.Parse("wire schema :Blob as { data: :List(:Number integer between 0 and 255) max 60 }\nnetwork :Game as { client sends :Blob bulk }\n")
            .Network("Game").Bind();
        var options = GesNetworkOptions.InProcess();
        // The first transfer takes 39 bytes: its 34-byte header and its data.
        options.MaxTransferBytes = 41;
        using var pair = GesNetworkPair.Realtime(network, default, options);
        // As on the connection, the queued transfers of this side stay within its transfer limit.
        Assert.AreEqual(GesNetworkSendStatus.Queued, pair.Client.Send(0, [1, 2, 3, 4, 5]));
        Assert.AreEqual(GesNetworkSendStatus.QueueFull, pair.Client.Send(0, [6, 7, 8]));
        Assert.AreEqual(GesNetworkSendStatus.Queued, pair.Client.Send(0, [6, 7]));
        var (_, server) = Poll(pair, 50);
        CollectionAssert.AreEqual(
            new[] { "connecting 0 ", "ClientConnected 0", "message 0 0102030405", "message 0 0607" },
            server.Events.Where(entry => entry is not "Received -1" and not "TransferProgress 0").ToList(),
            string.Join(" | ", server.Events));
    }

    [TestMethod]
    public void AdvancingThePairDuringAPollThrows()
    {
        using var pair = GesNetworkPair.Synchronous(Network(), default);
        var client = new Recorder();
        client.OnEvent = (_, _) => pair.Advance(1);
        Assert.ThrowsExactly<InvalidOperationException>(() => Poll(pair, client: client));
    }

    [TestMethod]
    public void AThrowingConnectingHandlerStillAcceptsTheClient()
    {
        using var pair = GesNetworkPair.Realtime(Network(), default);
        var server = new Recorder();
        server.OnEvent = (kind, _) =>
        {
            if (kind == GesNetworkEndEventKind.Connecting) throw new InvalidOperationException("handler");
        };
        var client = new Recorder();
        var thrown = false;
        for (var round = 0; round < 10; round++)
        {
            pair.Client.Poll(ref client);
            try
            {
                pair.Server.Poll(ref server);
            }
            catch (InvalidOperationException exception) when (exception.Message == "handler")
            {
                thrown = true;
            }
        }

        Assert.IsTrue(thrown);
        Assert.AreEqual(GesNetworkState.Connected, pair.Client.Connection.State);
        CollectionAssert.AreEqual(new[] { "Connected -1" }, client.Events);
    }

    [TestMethod]
    public void ASendOutsideAPollBringsTheNextDeadlineForward()
    {
        using var pair = GesNetworkPair.Realtime(Network(), default);
        Poll(pair, 10);
        Assert.AreEqual(GesNetworkState.Connected, pair.Client.Connection.State);
        var idle = pair.Client.NextDeadline;
        pair.Client.Send(Commands, [1]);
        Assert.IsLessThan(idle, pair.Client.NextDeadline, "The send is due with the next poll.");
        var server = new Recorder();
        pair.Server.Poll(ref server);
        idle = pair.Server.NextDeadline;
        pair.Server.Publish(Log, [2]);
        Assert.IsLessThan(idle, pair.Server.NextDeadline, "The publish is due with the next poll.");
    }

    [TestMethod]
    public void AConnectionClosedByTheHandlerSkipsItsRemainingEvents()
    {
        using var pair = GesNetworkPair.Synchronous(Network(), default);
        var (client, server) = Poll(pair);
        client.Take();
        server.Take();
        server.OnEvent = (kind, id) =>
        {
            if (kind == GesNetworkEndEventKind.Message) pair.Server.Close(id, "undecodable");
        };
        pair.Client.Send(Commands, [1]);
        pair.Client.Send(Commands, [2]);
        pair.Server.Poll(ref server);
        CollectionAssert.AreEqual(new[] { $"message {Commands} 01", "ClientDisconnected 0 network.closed False", "Received -1" }, server.Take());

        using var second = GesNetworkPair.Synchronous(Network(), default);
        (client, server) = Poll(second);
        client.Take();
        client.OnEvent = (kind, _) =>
        {
            if (kind == GesNetworkEndEventKind.Message) second.Client.Close("undecodable");
        };
        second.Server.Publish(Log, [3]);
        second.Server.Publish(Log, [4]);
        second.Client.Poll(ref client);
        CollectionAssert.AreEqual(new[] { $"message {Log} 03", "Disconnected -1 network.closed False", "Received -1" }, client.Take());
    }
}
