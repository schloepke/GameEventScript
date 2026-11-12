// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Generator;
using Model = GameEventScript.Tests.Generator.Tree.Model;
using Wire = GameEventScript.Tests.Generator.Tree.Wire;

namespace GameEventScript.Tests.Generator;

// The checked-in code of conformance/fixtures/SchemaGeneration/tree.gess: recursive Lists, whose records take their level
// and fail with the depth they exceed, in wire code that agrees with the value path of the runtime.
[TestClass]
public sealed class GesTreeGeneratedCodeTests
{
    private const string TooDeep = "deeper than depth 3";

    private static GesSchemaSet Set()
    {
        var fixture = Path.Combine(TestRepositoryPaths.ConformanceDirectory, "fixtures", "SchemaGeneration", "tree.gess");
        return GesSchemaSet.Parse(new GesSchemaSource("tree.gess", File.ReadAllText(fixture)));
    }

    private static GesSchemaGenerateOptions Options(GesSchemaLanguage language, string space) => new() { Language = language, Namespace = space, Access = GesSchemaAccess.Internal, Values = true };

    [TestMethod]
    public void CheckedInCodeIsCurrent()
    {
        var set = Set();
        var csharp = Path.Combine(TestRepositoryPaths.CSharpDirectory, "GameEventScript.Generator", "tests", "Generated");
        var swift = Path.Combine(TestRepositoryPaths.Root, "implementation", "swift", "GameEventScriptConformance", "Tests", "GameEventScriptConformanceTests", "Generated");
        var wire = GesSchemaGenerator.WireRoots(set, "tree");
        var model = GesSchemaGenerator.ModelRoots(set, "tree");
        var parameters = new GesSchemaParameters();
        Assert.AreEqual(File.ReadAllText(Path.Combine(csharp, "TreeWire.g.cs")), GesSchemaGenerator.Generate(set, wire, parameters, Options(GesSchemaLanguage.CSharp, "GameEventScript.Tests.Generator.Tree.Wire")));
        Assert.AreEqual(File.ReadAllText(Path.Combine(swift, "TreeWire.swift")), GesSchemaGenerator.Generate(set, wire, parameters, Options(GesSchemaLanguage.Swift, "TreeWire")));
        Assert.AreEqual(File.ReadAllText(Path.Combine(csharp, "TreeModel.g.cs")), GesSchemaGenerator.GenerateModels(set, model, Options(GesSchemaLanguage.CSharp, "GameEventScript.Tests.Generator.Tree.Model")));
        Assert.AreEqual(File.ReadAllText(Path.Combine(swift, "TreeModel.swift")), GesSchemaGenerator.GenerateModels(set, model, Options(GesSchemaLanguage.Swift, "TreeModel")));
    }

    private static Wire.Bind Bind(string name, params Wire.Bind[] units)
    {
        var bind = new Wire.Bind { Name = name };
        bind.Units.AddRange(units);
        return bind;
    }

    private static Wire.Unit Unit(long id, params Wire.Unit[] units)
    {
        var unit = new Wire.Unit { Id = id };
        unit.Units.AddRange(units);
        return unit;
    }

    private static Wire.Node Node(string tag, params Wire.Node[] kids)
    {
        var node = new Wire.Node { Tag = tag };
        node.Kids.AddRange(kids);
        return node;
    }

    // Every tree as deep as its depth: three binds, three units and, below the leaf, three nodes.
    private static Wire.Loadout Full()
    {
        var loadout = new Wire.Loadout { Root = Bind("a", Bind("b", Bind("c")), Bind("d")), Team = Unit(1, Unit(2, Unit(3)), Unit(4)) };
        loadout.Leaf.Tag = "l";
        loadout.Leaf.Name = "n";
        loadout.Leaf.Kids.Add(Node("x", Node("y", Node("z"))));
        return loadout;
    }

    private static void AssertTooDeep(string path, Action action)
    {
        var error = Assert.ThrowsExactly<GesWireException>(action);
        Assert.AreEqual(GesWireException.TooDeep, error.Code);
        Assert.AreEqual(path, error.Path);
        Assert.AreEqual(TooDeep, error.Detail);
        Assert.AreEqual($"wire.tooDeep at '{path}': {TooDeep}", error.Message);
    }

    [TestMethod]
    public void TreesAsDeepAsTheirDepthRoundTripLikeTheValuePath()
    {
        var loadout = Full();
        var bytes = loadout.ToBytes();
        var read = new Wire.Loadout();
        Assert.AreEqual(bytes.Length, read.Read(bytes));
        Assert.IsTrue(read.WireEquals(loadout));
        Assert.AreEqual("c", read.Root.Units[0].Units[0].Name);
        Assert.AreEqual(3L, read.Team.Units[0].Units[0].Id);
        Assert.AreEqual("z", read.Leaf.Kids[0].Kids[0].Kids[0].Tag);

        // The value path of the runtime writes the same bytes and reads the same tree.
        var value = loadout.ToValue();
        var again = new Wire.Loadout();
        again.ReadValue(value);
        CollectionAssert.AreEqual(bytes, again.ToBytes());
        read.Quantize();
        CollectionAssert.AreEqual(bytes, read.ToBytes());
    }

    [TestMethod]
    public void ARecordBelowTheDepthNamesTheDepth()
    {
        var loadout = Full();
        loadout.Root.Units[0].Units[0].Units.Add(Bind("e"));
        AssertTooDeep("root.units[].units[].units", () => loadout.ToBytes());
        AssertTooDeep("root.units[].units[].units", () => loadout.ByteCount());
        AssertTooDeep("root.units[].units[].units", () => loadout.Quantize());
        AssertTooDeep("root.units[].units[].units", () => loadout.WireEquals(Full()));
        AssertTooDeep("root.units[].units[].units", () => loadout.ToDeltaBytes(Full()));
        AssertTooDeep("root.units[].units[].units", () => loadout.ToValue());

        // Where `with` copies the recursive List, three records of the declaring schema nest below the copy.
        var leaf = Full();
        leaf.Leaf.Kids[0].Kids[0].Kids[0].Kids.Add(Node("w"));
        AssertTooDeep("leaf.kids[].kids[].kids[].kids", () => leaf.ToBytes());

        // A keyed tree is no keyed List at its depth either.
        var team = Full();
        team.Team.Units[0].Units[0].Units.Add(Unit(9));
        AssertTooDeep("team.units[].units[].units", () => team.ToDeltaBytes(Full()));
    }

    [TestMethod]
    public void DeltasDescendThroughEveryLevel()
    {
        var baseline = Full();
        var changed = Full();
        changed.Root.Units[0].Units[0].Name = "e";
        changed.Team.Units[0].Units.Add(Unit(5));
        changed.Leaf.Kids[0].Kids[0].Kids[0].Tag = "q";
        var delta = changed.ToDeltaBytes(baseline);
        var read = new Wire.Loadout();
        Assert.AreEqual(delta.Length, read.ReadDelta(delta, baseline));
        Assert.IsTrue(read.WireEquals(changed));
        var unchanged = Full().ToDeltaBytes(baseline);
        Assert.AreEqual(unchanged.Length, read.ReadDelta(unchanged, baseline));
        Assert.IsTrue(read.WireEquals(baseline));
    }

    [TestMethod]
    public void ModelsHoldTheirTreeAndConvertThroughTheValuePath()
    {
        var model = Model.Loadout.FromValue(Full().ToValue());
        Assert.AreEqual("c", model.Root.Units[0].Units[0].Name);
        Assert.AreEqual(3L, model.Team.Units[0].Units[0].Id);
        var wire = new Wire.Loadout();
        wire.ReadValue(model.ToValue());
        CollectionAssert.AreEqual(Full().ToBytes(), wire.ToBytes());
    }
}
