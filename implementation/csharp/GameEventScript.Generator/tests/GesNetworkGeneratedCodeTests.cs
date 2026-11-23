// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Network;
using Wire = GameEventScript.Tests.Generator.Network.Wire;

namespace GameEventScript.Tests.Generator;

// The checked-in code of conformance/fixtures/SchemaGeneration/network.gess: the constants of a network, which agree with the
// network the runtime binds with the same parameter values, and its typed send and publish functions.
[TestClass]
public sealed class GesNetworkGeneratedCodeTests
{
    private static GesNetwork Network()
    {
        var fixture = Path.Combine(TestRepositoryPaths.ConformanceDirectory, "fixtures", "SchemaGeneration", "network.gess");
        return GesSchemaSet.Parse(new GesSchemaSource("network.gess", File.ReadAllText(fixture))).Network("Game");
    }

    [TestMethod]
    public void ConstantsMatchTheBoundNetwork()
    {
        var network = Network();
        Assert.AreEqual(Wire.Game.Hash, network.Bind(new GesSchemaParameters().With("worldSize", "2km")).Hash);
        Assert.AreNotEqual(Wire.Game.Hash, network.Bind(new GesSchemaParameters().With("worldSize", "3km")).Hash, "A parameter enters the hash.");
        Assert.AreEqual(Wire.Game.ClientSendsCommands, network.Routes[0].Index);
        Assert.AreEqual(Wire.Game.ServerPublishesWorld, network.Routes[1].Index);
        Assert.AreEqual(Wire.Game.ServerSendsChunk, network.Routes[2].Index);
        Assert.AreEqual(Wire.Game.ServerPublishesWorldEveryMicroseconds, network.Routes[1].RateMicroseconds);
    }

    [TestMethod]
    public void TypedFunctionsSendTheEncodedRecords()
    {
        var network = Network().Bind(new GesSchemaParameters().With("worldSize", "2km"));
        var plain = new GesNetworkOptions { ClientSecurity = GesNetworkClientSecurity.None, ServerSecurity = GesNetworkServerSecurity.None };
        var loop = new GesNetworkSimulator(GesNetworkConnection.CreateClient(network, default, 0, plain), GesNetworkConnection.CreateServer(network, 0, plain), 0, 0, 0);
        loop.Advance(0);
        Assert.AreEqual(GesNetworkEventKind.Connecting, loop.Server.NextEvent().Kind);
        loop.Server.Accept();
        loop.Advance(0);
        Assert.AreEqual(GesNetworkEventKind.Connected, loop.Client.NextEvent().Kind);
        Assert.AreEqual(GesNetworkEventKind.Connected, loop.Server.NextEvent().Kind);

        Span<byte> buffer = stackalloc byte[16];
        var world = new Wire.World { Tick = 7, X = 12 };
        Assert.AreEqual(GesNetworkSendStatus.NotSubscribed, Wire.Game.ServerPublishWorld(loop.Server, world, buffer));
        loop.Server.Subscribe(Wire.Game.ServerPublishesWorld);
        Assert.AreEqual(GesNetworkSendStatus.Queued, Wire.Game.ServerPublishWorld(loop.Server, world, buffer));
        Assert.AreEqual(GesNetworkSendStatus.Queued, Wire.Game.ServerSendChunk(loop.Server, new Wire.Chunk { Index = 3 }, buffer));
        var commands = new Wire.Commands();
        commands.Selected = Wire.Commands.Member.Move;
        commands.Move.X = 5;
        Assert.AreEqual(GesNetworkSendStatus.Queued, Wire.Game.ClientSendCommands(loop.Client, commands, buffer));
        Assert.AreEqual(GesNetworkSendStatus.InvalidRoute, Wire.Game.ServerSendChunk(loop.Client, new Wire.Chunk { Index = 3 }, buffer));
        loop.Advance(1_000);

        var received = loop.Client.NextEvent();
        Assert.AreEqual(Wire.Game.ServerPublishesWorld, received.Route);
        var read = new Wire.World();
        read.Read(received.Payload);
        Assert.AreEqual(7, read.Tick);
        Assert.AreEqual(12d, read.X);
        received = loop.Client.NextEvent();
        Assert.AreEqual(Wire.Game.ServerSendsChunk, received.Route);
        received = loop.Server.NextEvent();
        Assert.AreEqual(Wire.Game.ClientSendsCommands, received.Route);
        var move = new Wire.Commands();
        move.Read(received.Payload);
        Assert.AreEqual(5d, move.Move.X);
    }

    private static GesBoundNetwork Bound() => Network().Bind(new GesSchemaParameters().With("worldSize", "2km"));

    private sealed class ClientHandler : Wire.Game.IClientHandler
    {
        internal readonly List<string> Events = [];

        public void OnConnected(GesNetworkSecurity security) => Events.Add("connected");

        public void OnDisconnected(string code, string detail, bool byPeer) => Events.Add($"disconnected {code} {detail} {byPeer}");

        public void OnWorld(in Wire.World value) => Events.Add($"world {value.Tick} {value.X}");

        public void OnChunk(in Wire.Chunk value) => Events.Add($"chunk {value.Index}");

        public void OnTick(int number) => Events.Add($"tick {number}");
    }

    private struct ServerHandler : Wire.Game.IServerHandler
    {
        internal List<string> Events;
        internal int Count;

        public void OnConnecting(int client, ReadOnlySpan<byte> token) => Events.Add($"connecting {client} {token.Length}");

        public void OnClientConnected(int client, GesNetworkSecurity security) => Events.Add($"connected {client}");

        public void OnClientDisconnected(int client, string code, string detail, bool byPeer) => Events.Add($"disconnected {client}");

        public void OnCommands(int client, Wire.Commands.Case value)
        {
            Count++;
            Events.Add(value switch
            {
                Wire.Commands.Case.Move move => $"move {client} {move.Value.X}",
                Wire.Commands.Case.Fire => $"fire {client}",
                _ => "unknown",
            });
        }

        public void OnTick(int client, int number) => Events.Add($"tick {client} {number}");

        public void OnTransferProgress(int client, int route, int transferId, long bytes, long total)
        {
        }

        public void OnTransferCancelled(int client, int route, int transferId, string code)
        {
        }

        public void OnReceived() => Events.Add("received");
    }

    [TestMethod]
    public void TypedEndsSendTicksAndDispatchTypedEvents()
    {
        using var pair = GesNetworkPair.Synchronous(Bound(), "t"u8);
        var client = new Wire.Game.Client(pair.Client);
        var server = new Wire.Game.Server(pair.Server);
        var clientHandler = new ClientHandler();
        var serverHandler = new ServerHandler { Events = [] };
        client.Poll(ref clientHandler);
        server.Poll(ref serverHandler);
        CollectionAssert.AreEqual(new[] { "connected" }, clientHandler.Events);
        CollectionAssert.AreEqual(new[] { "connected 0" }, serverHandler.Events);
        clientHandler.Events.Clear();
        serverHandler.Events.Clear();

        Assert.AreEqual(GesNetworkSendStatus.Queued, client.Send(new Wire.Commands.Case.Move(new Wire.Move { X = 5 })));
        Assert.AreEqual(GesNetworkSendStatus.Queued, client.Send(new Wire.Commands.Case.Fire()));
        Assert.AreEqual(GesNetworkSendStatus.Queued, server.Send(new Wire.Chunk { Index = 3 }, 0));
        using (var tick = server.BeginTick())
        {
            Assert.AreEqual(GesNetworkSendStatus.Queued, tick.Publish(new Wire.World { Tick = 7, X = 12 }));
        }

        client.Poll(ref clientHandler);
        server.Poll(ref serverHandler);
        CollectionAssert.AreEqual(new[] { "chunk 3", "world 7 12", "tick 0" }, clientHandler.Events);
        CollectionAssert.AreEqual(new[] { "move 0 5", "fire 0", "received" }, serverHandler.Events);
        Assert.AreEqual(2, serverHandler.Count, "The struct handler is copied back after the poll.");
        Assert.ThrowsExactly<ArgumentNullException>(() => client.Send((Wire.Commands.Case)null!));

        // Disposing a tick again, also through a copy, does nothing and never closes a later tick.
        var first = server.BeginTick();
        var copy = first;
        first.Dispose();
        copy.Dispose();
        var second = server.BeginTick();
        copy.Dispose();
        Assert.ThrowsExactly<InvalidOperationException>(() => server.BeginTick());
        second.Dispose();
        server.BeginTick().Dispose();
    }

    [TestMethod]
    public void TypedEndRejectsAnotherNetworkAndClosesOnAnUndecodablePayload()
    {
        var other = Network().Bind(new GesSchemaParameters().With("worldSize", "3km"));
        using (var otherPair = GesNetworkPair.Synchronous(other, "t"u8))
            Assert.ThrowsExactly<ArgumentException>(() => new Wire.Game.Client(otherPair.Client));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Wire.Game.Server(null!));

        using var pair = GesNetworkPair.Synchronous(Bound(), "t"u8);
        var client = new Wire.Game.Client(pair.Client);
        var handler = new ClientHandler();
        client.Poll(ref handler);
        handler.Events.Clear();
        // The x of :World holds at most 2000, so 2047 is no valid value.
        Assert.AreEqual(GesNetworkSendStatus.Queued, pair.Server.Publish(Wire.Game.ServerPublishesWorld, [0xFF, 0xFF, 0xFF, 0xFF]));
        client.Poll(ref handler);
        Assert.IsFalse(handler.Events.Exists(entry => entry.StartsWith("world", StringComparison.Ordinal)), string.Join(", ", handler.Events));
        Assert.AreNotEqual(GesNetworkState.Connected, pair.Client.Connection.State);
    }

    [TestMethod]
    public void MessageCasesConvertBothWays()
    {
        var commands = new Wire.Commands { Selected = Wire.Commands.Member.Move };
        commands.Move.X = 9;
        var converted = commands.ToCase();
        Assert.IsInstanceOfType<Wire.Commands.Case.Move>(converted);
        Assert.AreEqual(Wire.Commands.Member.Move, converted.Selected);
        Assert.AreEqual(9d, ((Wire.Commands.Case.Move)converted).Value.X);
        var assigned = new Wire.Commands();
        assigned.Assign(new Wire.Commands.Case.Fire());
        Assert.AreEqual(Wire.Commands.Member.Fire, assigned.Selected);
        assigned.Assign(converted);
        Assert.AreEqual(9d, assigned.Move.X);
    }
}
