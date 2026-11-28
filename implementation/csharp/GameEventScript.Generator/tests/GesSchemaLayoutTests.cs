// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Generator;

namespace GameEventScript.Tests.Generator;

/// <summary>Checks the layout model that the shared corpus cannot reach: deduplication and dependencies.</summary>
[TestClass]
public sealed class GesSchemaLayoutTests
{
    [TestMethod]
    public void SharedTypesAppearOnceWithTheirDependencies()
    {
        var set = GesSchemaSet.Parse("""
            parameter size: :Quantity(m)

            schema :Cell as {
              x: :Quantity(m) between 0m and size step 1m
            }

            schema :A as {
              cell: :Cell
            }

            schema :B as {
              cells: :List(:Cell) max 2
            }
            """);
        var parameters = new GesSchemaParameters().With("size", "10m");
        var text = GesSchemaGenerator.Layout(set, ["A", "B"], parameters);
        Assert.HasCount(2, text.Split("\"name\": \"Cell\",\n      \"kind\""));
        Assert.Contains("\"maxByteCountDepends\": [\n        \"size\"\n      ]", text);
        Assert.AreEqual("generate.usage", Assert.ThrowsExactly<GesGeneratorException>(() => GesSchemaGenerator.Layout(set, [], parameters)).Code);
    }

    [TestMethod]
    public void OwnersCarryTheirProjectionAndSendingState()
    {
        var set = GesSchemaSet.Parse("""
            schema :Composition as { name: :Text max 8, mass: :Number integer between 0 and 9 }
            projection :UnitPublic of :Composition as { name }
            handle @Unit of :Composition with id :Text max 8 versioned by hash(#ges)
            handle @Drone of :Composition with id :Number integer between 0 and 9 versioned by hash(#ges)
            wire schema :World as { unit: @Unit(:UnitPublic), drone: @Drone }
            network :Game as {
              server publishes :World latest
              server owns @Unit as :UnitPublic sending state
              server owns @Drone
            }
            """);
        var text = GesSchemaGenerator.Layout(set, ["Game"], new GesSchemaParameters());
        Assert.Contains("\"handle\": \"Drone\"\n", text);
        Assert.Contains("\"handle\": \"Unit\",\n          \"projection\": \"UnitPublic\",\n          \"sendingState\": true\n", text);
    }

    [TestMethod]
    public void TheLayoutIsCanonicalJson()
    {
        var set = GesSchemaSet.Parse("schema :S as {\n  on: :Boolean\n}\n");
        var text = GesSchemaGenerator.Layout(set, ["S", "S"], new GesSchemaParameters());
        Assert.StartsWith("{\n  \"layout\": 1,\n  \"roots\": [\n    \"S\"\n  ],\n  \"parameters\": [],\n", text);
        Assert.EndsWith("}\n", text);
        Assert.Contains("\"encoding\": {\n            \"kind\": \"boolean\"\n          }", text);
    }
}
