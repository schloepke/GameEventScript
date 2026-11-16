// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using Wire = GameEventScript.Tests.Generator.Network.Wire;

namespace GameEventScript.Tests.Generator;

// The checked-in code of conformance/fixtures/SchemaGeneration/network.gess: the constants of a network, which agree with the
// network the runtime binds with the same parameter values.
[TestClass]
public sealed class GesNetworkGeneratedCodeTests
{
    [TestMethod]
    public void ConstantsMatchTheBoundNetwork()
    {
        var fixture = Path.Combine(TestRepositoryPaths.ConformanceDirectory, "fixtures", "SchemaGeneration", "network.gess");
        var network = GesSchemaSet.Parse(new GesSchemaSource("network.gess", File.ReadAllText(fixture))).Network("Game");
        Assert.AreEqual(Wire.Game.Hash, network.Bind(new GesSchemaParameters().With("worldSize", "2km")).Hash);
        Assert.AreNotEqual(Wire.Game.Hash, network.Bind(new GesSchemaParameters().With("worldSize", "3km")).Hash, "A parameter enters the hash.");
        Assert.AreEqual(Wire.Game.ClientSendsCommands, network.Routes[0].Index);
        Assert.AreEqual(Wire.Game.ServerPublishesWorld, network.Routes[1].Index);
        Assert.AreEqual(Wire.Game.ServerSendsChunk, network.Routes[2].Index);
        Assert.AreEqual(Wire.Game.ServerPublishesWorldEveryMicroseconds, network.Routes[1].RateMicroseconds);
    }
}
