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
    public void TheLayoutIsCanonicalJson()
    {
        var set = GesSchemaSet.Parse("schema :S as {\n  on: :Boolean\n}\n");
        var text = GesSchemaGenerator.Layout(set, ["S", "S"], new GesSchemaParameters());
        Assert.StartsWith("{\n  \"layout\": 1,\n  \"roots\": [\n    \"S\"\n  ],\n  \"parameters\": [],\n", text);
        Assert.EndsWith("}\n", text);
        Assert.Contains("\"encoding\": {\n            \"kind\": \"boolean\"\n          }", text);
    }
}
