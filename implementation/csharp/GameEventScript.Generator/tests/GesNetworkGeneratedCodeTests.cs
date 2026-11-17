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
        var loop = new GesNetworkLoopback(GesNetworkConnection.CreateClient(network, default, 0), GesNetworkConnection.CreateServer(network, 0));
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
}
