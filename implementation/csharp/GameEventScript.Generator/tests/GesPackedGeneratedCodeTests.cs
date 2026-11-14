// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Generator;
using GameEventScript.Tests.Generator.Packed.Wire;
using Model = GameEventScript.Tests.Generator.Packed.Model;
using Own = GameEventScript.Tests.Generator.Packed.Own;
using Wire = GameEventScript.Tests.Generator.Packed.Wire;

namespace GameEventScript.Tests.Generator;

// The checked-in code of conformance/fixtures/SchemaGeneration/packed.gess: packed records as structs over one word, shared
// between models and wire code that `--models` names, and the direct conversion between wire records and models.
[TestClass]
public sealed class GesPackedGeneratedCodeTests
{
    private const string ModelSpace = "GameEventScript.Tests.Generator.Packed.Model";

    private static GesSchemaSet Set()
    {
        var fixture = Path.Combine(TestRepositoryPaths.ConformanceDirectory, "fixtures", "SchemaGeneration", "packed.gess");
        return GesSchemaSet.Parse(new GesSchemaSource("packed.gess", File.ReadAllText(fixture)));
    }

    private static string Generated(string name) => File.ReadAllText(Path.Combine(TestRepositoryPaths.CSharpDirectory, "GameEventScript.Generator", "tests", "Generated", name));

    [TestMethod]
    public void CheckedInCodeIsCurrent()
    {
        var set = Set();
        var wire = GesSchemaGenerator.WireRoots(set, "packed");
        var model = GesSchemaGenerator.ModelRoots(set, "packed");
        var parameters = new GesSchemaParameters();
        GesSchemaGenerateOptions Options(string space, bool values, string? models = null) => new() { Language = GesSchemaLanguage.CSharp, Namespace = space, Access = GesSchemaAccess.Internal, Values = values, Models = models };
        Assert.AreEqual(Generated("PackedModel.g.cs"), GesSchemaGenerator.GenerateModels(set, model, Options(ModelSpace, true)));
        Assert.AreEqual(Generated("PackedWire.g.cs"), GesSchemaGenerator.Generate(set, wire, parameters, Options("GameEventScript.Tests.Generator.Packed.Wire", true, ModelSpace)));
        Assert.AreEqual(Generated("PackedOwn.g.cs"), GesSchemaGenerator.Generate(set, wire, parameters, Options("GameEventScript.Tests.Generator.Packed.Own", false)));
        var swift = Path.Combine(TestRepositoryPaths.Root, "implementation", "swift", "GameEventScriptConformance", "Tests", "GameEventScriptConformanceTests", "Generated");
        GesSchemaGenerateOptions Swift(string space, bool values, string? models = null) => new() { Language = GesSchemaLanguage.Swift, Namespace = space, Access = GesSchemaAccess.Internal, Values = values, Models = models };
        Assert.AreEqual(File.ReadAllText(Path.Combine(swift, "PackedModel.swift")), GesSchemaGenerator.GenerateModels(set, model, Swift("PackedModel", true)));
        Assert.AreEqual(File.ReadAllText(Path.Combine(swift, "PackedWire.swift")), GesSchemaGenerator.Generate(set, wire, parameters, Swift("PackedWire", true, "PackedModel")));
        Assert.AreEqual(File.ReadAllText(Path.Combine(swift, "PackedOwn.swift")), GesSchemaGenerator.Generate(set, wire, parameters, Swift("PackedOwn", false)));
        Assert.AreEqual(Generated("PackedGlobalModel.g.cs"), GesSchemaGenerator.GenerateModels(set, model, Options("", true)));
        Assert.AreEqual(Generated("PackedGlobalWire.g.cs"), GesSchemaGenerator.Generate(set, wire, parameters, Options("GameEventScript.Tests.Generator.Packed.Global", true, "")));
        Assert.AreEqual(File.ReadAllText(Path.Combine(swift, "PackedGlobalModel.swift")), GesSchemaGenerator.GenerateModels(set, model, Swift("", true)));
        Assert.AreEqual(File.ReadAllText(Path.Combine(swift, "PackedGlobalWire.swift")), GesSchemaGenerator.Generate(set, wire, parameters, Swift("PackedGlobal", true, "")));
    }

    private static Model.Grid Grid()
    {
        var cell = new Model.Cell(7, true, Model.Cell.KindTag.Water, 0.3, Model.Cell.MarksFlags.Held);
        return new Model.Grid(
            "grid",
            [cell, cell.WithLevel(2).WithOpen(false), new Model.Cell(9, false, Model.Cell.KindTag.Rock, 1, Model.Cell.MarksFlags.Seen | Model.Cell.MarksFlags.Held)],
            [new Model.Pair(cell, cell.WithKind(Model.Cell.KindTag.Sand))],
            cell.WithSlope(0.9),
            "note",
            new Model.Grid.ReadingAlternative.Code("ab"),
            ["a", "bc"],
            2.5,
            [new Model.Grid_Row(3, [cell])],
            new Model.Offset(-3, 12, 0.5, Model.Offset.KindTag.High));
    }

    [TestMethod]
    public void ModelsNeedAnotherNamespaceThanTheWireCode()
    {
        var set = Set();
        foreach (var language in new[] { GesSchemaLanguage.CSharp, GesSchemaLanguage.Swift })
            foreach (var space in new[] { "", "Game.Shared" })
            {
                var options = new GesSchemaGenerateOptions { Language = language, Namespace = space, Models = space };
                var error = Assert.ThrowsExactly<GesGeneratorException>(() => GesSchemaGenerator.Generate(set, GesSchemaGenerator.WireRoots(set, "packed"), new GesSchemaParameters(), options));
                Assert.AreEqual("generate.nameCollision", error.Code);
            }

        // Swift namespace components are enums: the wire code extends the one it shares with the models, which declare it.
        var prefix = new GesSchemaGenerateOptions { Language = GesSchemaLanguage.Swift, Namespace = "Game.Wire", Models = "Game.Model" };
        StringAssert.Contains(GesSchemaGenerator.Generate(set, GesSchemaGenerator.WireRoots(set, "packed"), new GesSchemaParameters(), prefix), "extension Game {\n    /// The namespace `Wire` of generated schema types.\n    public enum Wire {");
        var models = GesSchemaGenerator.GenerateModels(set, GesSchemaGenerator.ModelRoots(set, "packed"), new GesSchemaGenerateOptions { Language = GesSchemaLanguage.Swift, Namespace = "Game.Panels", ExtendNamespace = "Game" });
        StringAssert.Contains(models, "extension Game {\n    /// The namespace `Panels` of generated model types.\n    public enum Panels {");
        // A namespace that starts in the global one of the other side must not take the name of a type there.
        var global = new GesSchemaGenerateOptions { Language = GesSchemaLanguage.Swift, Namespace = "Grid.Wire", Models = "" };
        Assert.AreEqual("generate.nameCollision", Assert.ThrowsExactly<GesGeneratorException>(() => GesSchemaGenerator.Generate(set, GesSchemaGenerator.WireRoots(set, "packed"), new GesSchemaParameters(), global)).Code);
        global = new GesSchemaGenerateOptions { Language = GesSchemaLanguage.CSharp, Namespace = "", Models = "Grid.Model" };
        Assert.AreEqual("generate.nameCollision", Assert.ThrowsExactly<GesGeneratorException>(() => GesSchemaGenerator.Generate(set, GesSchemaGenerator.WireRoots(set, "packed"), new GesSchemaParameters(), global)).Code);
        // Nor where one namespace continues the other: models in Game hold Grid, so wire code cannot live in Game.Grid.
        var nested = new GesSchemaGenerateOptions { Language = GesSchemaLanguage.Swift, Namespace = "Game.Grid", Models = "Game" };
        Assert.AreEqual("generate.nameCollision", Assert.ThrowsExactly<GesGeneratorException>(() => GesSchemaGenerator.Generate(set, GesSchemaGenerator.WireRoots(set, "packed"), new GesSchemaParameters(), nested)).Code);
        // Wire code in the whole namespace that the models' one begins with extends it with one brace for every one it opens.
        var whole = GesSchemaGenerator.Generate(set, GesSchemaGenerator.WireRoots(set, "packed"), new GesSchemaParameters(), new GesSchemaGenerateOptions { Language = GesSchemaLanguage.Swift, Namespace = "Game", Models = "Game.Model" });
        StringAssert.Contains(whole, "\nextension Game {\n");
        Assert.AreEqual(whole.Count(character => character == '{'), whole.Count(character => character == '}'));
        // C# merges namespaces, so extending them is a usage error there.
        var csharp = new GesSchemaGenerateOptions { Language = GesSchemaLanguage.CSharp, Namespace = "Game.Panels", ExtendNamespace = "Game" };
        Assert.AreEqual("generate.usage", Assert.ThrowsExactly<GesGeneratorException>(() => GesSchemaGenerator.GenerateModels(set, GesSchemaGenerator.ModelRoots(set, "packed"), csharp)).Code);
        // A one-part namespace can be extended as a whole, and only a leading part of the namespace can be extended.
        var single = GesSchemaGenerator.GenerateModels(set, GesSchemaGenerator.ModelRoots(set, "packed"), new GesSchemaGenerateOptions { Language = GesSchemaLanguage.Swift, Namespace = "Game", ExtendNamespace = "Game" });
        StringAssert.Contains(single, "\nextension Game {\n");
        Assert.AreEqual(single.Count(character => character == '{'), single.Count(character => character == '}'));
        var notLeading = new GesSchemaGenerateOptions { Language = GesSchemaLanguage.Swift, Namespace = "Game.Panels", ExtendNamespace = "Panels" };
        Assert.AreEqual("generate.usage", Assert.ThrowsExactly<GesGeneratorException>(() => GesSchemaGenerator.GenerateModels(set, GesSchemaGenerator.ModelRoots(set, "packed"), notLeading)).Code);
        // A type of any model group counts, since the models' namespace holds them all: models in Game hold Report, so wire code
        // cannot live in Game.Report, whether Report shares the wire root's group or not.
        foreach (var source in new[] { "wire model schema :Grid as { a: :Boolean }\nmodel schema :Report as { b: :Boolean }\n", "wire model schema :Grid as { a: :Boolean }\nmodel(hud) schema :Report as { b: :Boolean }\n" })
        {
            var report = new GesSchemaGenerateOptions { Language = GesSchemaLanguage.Swift, Namespace = "Game.Report", Models = "Game" };
            Assert.AreEqual("generate.nameCollision", Assert.ThrowsExactly<GesGeneratorException>(() => GesSchemaGenerator.Generate(GesSchemaSet.Parse(source), ["Grid"], new GesSchemaParameters(), report)).Code);
        }

        // The wire namespace holds the `<Name>Wire` members of packed roots, which a models' namespace continuing it cannot name.
        var sharedRoot = new GesSchemaGenerateOptions { Language = GesSchemaLanguage.Swift, Namespace = "Game", Models = "Game.CellWire" };
        Assert.AreEqual("generate.nameCollision", Assert.ThrowsExactly<GesGeneratorException>(() => GesSchemaGenerator.Generate(set, GesSchemaGenerator.WireRoots(set, "packed"), new GesSchemaParameters(), sharedRoot)).Code);
        // Swift wire code names model types through the models' namespace, which no wire type, nested type or later wire
        // namespace component may hide; C# qualifies with global:: and is not affected.
        var hiding = GesSchemaSet.Parse("wire(w) model(m) packed schema :Cell as { level: :Number integer between 0 and 9 }\nwire(w) schema :Model as { cell: :Cell }\nwire(w) schema :Holder as { cell: :Cell\n  model: :Flags(#a, #b) }\n");
        foreach (var (space, modelSpace, roots) in new[] { ("Wire", "Model", new[] { "Cell", "Model" }), ("Wire.Model", "Model", new[] { "Cell" }), ("Wire", "ModelFlags", new[] { "Cell", "Holder" }) })
        {
            var options = new GesSchemaGenerateOptions { Language = GesSchemaLanguage.Swift, Namespace = space, Models = modelSpace };
            Assert.AreEqual("generate.nameCollision", Assert.ThrowsExactly<GesGeneratorException>(() => GesSchemaGenerator.Generate(hiding, roots, new GesSchemaParameters(), options)).Code, space + " " + modelSpace);
            options.Language = GesSchemaLanguage.CSharp;
            StringAssert.Contains(GesSchemaGenerator.Generate(hiding, roots, new GesSchemaParameters(), options), $"global::{modelSpace}.Cell");
        }

        var nestedHiding = new GesSchemaGenerateOptions { Language = GesSchemaLanguage.Swift, Namespace = "Wire", Models = "ModelFlags" };
        StringAssert.Contains(Assert.ThrowsExactly<GesGeneratorException>(() => GesSchemaGenerator.Generate(hiding, ["Cell", "Holder"], new GesSchemaParameters(), nestedHiding)).Message, "hides the models' namespace");
        // Only the first component counts, only where the wire code names model types, and the packed types the models
        // declare are no wire types; nor is the namespace subject to the names reserved for types.
        var accepted = new (GesSchemaSet Set, IReadOnlyList<string> Roots, string Space, string Models)[]
        {
            (hiding, ["Cell", "Model"], "Wire", "Game.Model"),
            (GesSchemaSet.Parse("wire(w) schema :Model as { a: :Boolean }\n"), ["Model"], "Wire", "Model"),
            (set, GesSchemaGenerator.WireRoots(set, "packed"), "Game", "Game.Cell"),
            (set, GesSchemaGenerator.WireRoots(set, "packed"), "Wire", "GesModels")
        };
        foreach (var (acceptedSet, acceptedRoots, space, modelSpace) in accepted)
            GesSchemaGenerator.Generate(acceptedSet, acceptedRoots, new GesSchemaParameters(), new GesSchemaGenerateOptions { Language = GesSchemaLanguage.Swift, Namespace = space, Models = modelSpace });

        // Wire generation also extends only a leading part, and the larger of that part and the one shared with the models.
        var wireNotLeading = new GesSchemaGenerateOptions { Language = GesSchemaLanguage.Swift, Namespace = "Game.Wire", ExtendNamespace = "Wire" };
        Assert.AreEqual("generate.usage", Assert.ThrowsExactly<GesGeneratorException>(() => GesSchemaGenerator.Generate(set, GesSchemaGenerator.WireRoots(set, "packed"), new GesSchemaParameters(), wireNotLeading)).Code);
        var deeper = GesSchemaGenerator.Generate(set, GesSchemaGenerator.WireRoots(set, "packed"), new GesSchemaParameters(), new GesSchemaGenerateOptions { Language = GesSchemaLanguage.Swift, Namespace = "Game.Wire.Packed", Models = "Game.Model", ExtendNamespace = "Game.Wire" });
        StringAssert.Contains(deeper, "\nextension Game.Wire {\n");
        // Models that read documents extend the namespace too.
        var documents = GesSchemaGenerator.GenerateModels(set, GesSchemaGenerator.ModelRoots(set, "packed"), new GesSchemaGenerateOptions { Language = GesSchemaLanguage.Swift, Namespace = "Game.Panels", ExtendNamespace = "Game", Documents = true });
        StringAssert.Contains(documents, "extension Game {\n    /// The namespace `Panels` of generated model types.\n    public enum Panels {");
        // An invalid namespace reports itself first.
        var invalid = new GesSchemaGenerateOptions { Language = GesSchemaLanguage.Swift, Namespace = ".Wire", Models = ".Wire" };
        Assert.AreEqual("generate.invalidNamespace", Assert.ThrowsExactly<GesGeneratorException>(() => GesSchemaGenerator.Generate(set, GesSchemaGenerator.WireRoots(set, "packed"), new GesSchemaParameters(), invalid)).Code);
        // The wire members of a shared packed root take the name <Name>Wire, which no schema may have.
        var taken = GesSchemaSet.Parse("wire model packed schema :Cell as { a: :Boolean }\nwire schema :CellWire as { b: :Boolean }\n");
        var shared = new GesSchemaGenerateOptions { Language = GesSchemaLanguage.CSharp, Namespace = "Wire", Models = "" };
        Assert.AreEqual("generate.nameCollision", Assert.ThrowsExactly<GesGeneratorException>(() => GesSchemaGenerator.Generate(taken, ["Cell", "CellWire"], new GesSchemaParameters(), shared)).Code);
    }

    [TestMethod]
    public void EveryPackedFieldLandsAtItsBits()
    {
        // delta -3 is index 5 at bit 0, gear 12 index 2 at bit 4, lift 0.5m index 3 at bit 6, high index 2 at bit 9.
        const ushort expected = 5 | (2 << 4) | (3 << 6) | (2 << 9);
        Assert.AreEqual(expected, new Model.Offset(-3, 12, 0.5, Model.Offset.KindTag.High).RawValue);
        var offset = new Model.Offset(7, 13, 1, Model.Offset.KindTag.Low) { Delta = -3, Gear = 12, Lift = 0.5, Kind = Model.Offset.KindTag.High };
        Assert.AreEqual(expected, offset.RawValue);
        Assert.AreEqual(0, new Model.Offset(-8, 10, -1, Model.Offset.KindTag.Low).RawValue);
    }

    [TestMethod]
    public void PackedRecordIsOneWordWithCheckedFields()
    {
        var cell = new Model.Cell(7, true, Model.Cell.KindTag.Water, 0.34, Model.Cell.MarksFlags.Held);
        // level 7 at bit 0, open at 4, water (2) at 5, slope 3 tenths at 7, held (2) at 11.
        Assert.AreEqual((ushort)(7 | (1 << 4) | (2 << 5) | (3 << 7) | (2 << 11)), cell.RawValue);
        Assert.AreEqual(0.3, cell.Slope, 1e-12);
        Assert.AreEqual(cell, Model.Cell.FromRaw(cell.RawValue));
        Assert.IsNull(Model.Cell.FromRaw(15));
        Assert.IsNull(Model.Cell.FromRaw((ushort)(3 << 5)));
        Assert.IsNull(Model.Cell.FromRaw(1 << 13));
        Assert.AreEqual(15, Model.Cell.FromRawUnchecked(15).RawValue);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => cell.WithLevel(10));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => cell.WithSlope(1.5));
        var pair = new Model.Pair(cell, cell.WithLevel(1));
        Assert.AreEqual(cell.RawValue | ((uint)cell.WithLevel(1).RawValue << 13), pair.RawValue);
        Assert.AreEqual(cell.WithLevel(1), pair.High);
    }

    [TestMethod]
    public void WireRecordsConvertToModelsDirectly()
    {
        var model = Grid();
        var wire = new Wire.Grid();
        wire.CopyFrom(model);
        Assert.AreSame(typeof(Model.Cell), wire.Cells[0].GetType());
        var bytes = wire.ToBytes();
        Assert.AreEqual(model.ToValue(), wire.ToValue());
        var back = new Wire.Grid();
        back.Read(bytes);
        Assert.AreEqual(model, back.ToModel());
        // Wire code that declares its own packed types writes the same bytes.
        var own = new Own.Grid();
        own.Read(bytes);
        CollectionAssert.AreEqual(bytes, own.ToBytes());
    }

    [TestMethod]
    public void ListsOfPackedRecordsCheckEachWord()
    {
        var wire = new Wire.Grid();
        wire.Cells.Add(new Model.Cell(0, false, Model.Cell.KindTag.Rock, 0, default));
        var bytes = wire.ToBytes();
        // An empty name takes 5 bits and the count 7, so the first cell's level starts at bit 12.
        bytes[1] |= 0xf0;
        var error = Assert.ThrowsExactly<GesWireException>(() => new Wire.Grid().Read(bytes));
        Assert.AreEqual(GesWireException.InvalidIndex, error.Code);
        Assert.AreEqual("cells[].level", error.Path);
        wire.Cells[0] = Model.Cell.FromRawUnchecked(15);
        error = Assert.ThrowsExactly<GesWireException>(() => wire.ToBytes());
        Assert.AreEqual(GesWireException.OutOfRange, error.Code);
        Assert.AreEqual("cells[].level", error.Path);
    }

    [TestMethod]
    public void SharedPackedRootsWriteThroughExtensions()
    {
        var cell = new Model.Cell(9, true, Model.Cell.KindTag.Sand, 1, Model.Cell.MarksFlags.Seen);
        var bytes = cell.ToBytes();
        Assert.HasCount(Wire.CellWire.MaxByteCount, bytes);
        CollectionAssert.AreEqual(new[] { (byte)cell.RawValue, (byte)(cell.RawValue >> 8) }, bytes);
        var read = default(Model.Cell);
        read.Read(bytes);
        Assert.AreEqual(cell, read);
        CollectionAssert.AreEqual(new byte[] { 0 }, cell.ToDeltaBytes(cell));
        var changed = cell.WithLevel(1);
        var delta = changed.ToDeltaBytes(cell);
        var target = default(Model.Cell);
        target.ReadDelta(delta, cell);
        Assert.AreEqual(changed, target);
        var error = Assert.ThrowsExactly<GesWireException>(() => Model.Cell.FromRawUnchecked(1 << 14).ToBytes());
        Assert.AreEqual(GesWireException.OutOfRange, error.Code);
    }
}
