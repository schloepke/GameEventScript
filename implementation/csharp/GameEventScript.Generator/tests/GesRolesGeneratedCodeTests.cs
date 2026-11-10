// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Generator;
using Model = GameEventScript.Tests.Generator.Roles.Model;
using Wire = GameEventScript.Tests.Generator.Roles.Wire;

namespace GameEventScript.Tests.Generator;

// The checked-in wire and model code of conformance/fixtures/SchemaGeneration/roles: groups that reference each other, a
// part reached from both roles, spatial fields in 2D and 3D, and model types of every supported field type.
[TestClass]
public sealed class GesRolesGeneratedCodeTests
{
    private static readonly string[] Files = ["World.gess", "Panels.gess", "View.gess"];

    private static GesSchemaSet Set()
    {
        var directory = Path.Combine(TestRepositoryPaths.ConformanceDirectory, "fixtures", "SchemaGeneration", "roles");
        return GesSchemaSet.Parse([.. Files.Select(file => new GesSchemaSource(file, File.ReadAllText(Path.Combine(directory, file))))]);
    }

    private static GesSchemaGenerateOptions Options(GesSchemaLanguage language, string space) => new() { Language = language, Namespace = space, Access = GesSchemaAccess.Internal };

    [TestMethod]
    public void CheckedInCodeIsCurrent()
    {
        var set = Set();
        var csharp = Path.Combine(TestRepositoryPaths.CSharpDirectory, "GameEventScript.Generator", "tests", "Generated");
        var swift = Path.Combine(TestRepositoryPaths.Root, "implementation", "swift", "GameEventScriptConformance", "Tests", "GameEventScriptConformanceTests", "Generated");
        var wire = GesSchemaGenerator.WireRoots(set, "world");
        var model = GesSchemaGenerator.ModelRoots(set, "view");
        CollectionAssert.AreEqual(new[] { "World", "World_Cards" }, wire.ToArray());
        CollectionAssert.AreEqual(new[] { "WorldView" }, model.ToArray());
        var parameters = new GesSchemaParameters().With("worldWidth", "2000m");
        Assert.AreEqual(File.ReadAllText(Path.Combine(csharp, "RolesWire.g.cs")), GesSchemaGenerator.Generate(set, wire, parameters, Options(GesSchemaLanguage.CSharp, "GameEventScript.Tests.Generator.Roles.Wire")));
        Assert.AreEqual(File.ReadAllText(Path.Combine(csharp, "RolesModel.g.cs")), GesSchemaGenerator.GenerateModels(set, model, Options(GesSchemaLanguage.CSharp, "GameEventScript.Tests.Generator.Roles.Model")));
        Assert.AreEqual(File.ReadAllText(Path.Combine(swift, "RolesWire.swift")), GesSchemaGenerator.Generate(set, wire, parameters, Options(GesSchemaLanguage.Swift, "RolesWire")));
        Assert.AreEqual(File.ReadAllText(Path.Combine(swift, "RolesModel.swift")), GesSchemaGenerator.GenerateModels(set, model, Options(GesSchemaLanguage.Swift, "RolesModel")));
    }

    [TestMethod]
    public void ARootOfAnotherGroupIsAPartWithoutItsGroup()
    {
        var layout = GesSchemaGenerator.Layout(Set(), GesSchemaGenerator.WireRoots(Set(), "world"), new GesSchemaParameters().With("worldWidth", "2000m"));
        StringAssert.Contains(layout, "\"name\": \"Panel_Badge\"");
        Assert.IsFalse(layout.Contains("\"name\": \"Panel\"", StringComparison.Ordinal));
        Assert.IsFalse(layout.Contains("Panel_Log", StringComparison.Ordinal));
        Assert.IsFalse(layout.Contains("BotView", StringComparison.Ordinal));
    }

    [TestMethod]
    public void SpatialFieldsUseTheRuntimeTypesIn2DAnd3D()
    {
        var world = new Wire.World { Tick = 7, Arena = new GesVector(2000, 1500) };
        world.Bots.Add(new Wire.World_Bot { Id = 3, Position = new GesPoint(12.34, 56.78), Velocity = new GesVector(-1.5, 2.25, 0.5), Body = new Wire.World_Body { Mass = 120 } });
        var read = new Wire.World { Arena = new GesVector(0, 0, 9) };
        read.Read(world.ToBytes());
        Assert.AreEqual(new GesVector(2000, 1500), read.Arena, "A 2D field reads its omitted axis as zero.");
        Assert.AreEqual(new GesPoint(12.34, 56.78), read.Bots[0].Position);
        Assert.AreEqual(new GesVector(-1.5, 2.25, 0.5), read.Bots[0].Velocity);
        Assert.IsTrue(read.WireEquals(world));
    }

    [TestMethod]
    public void ANonzeroOmittedAxisIsATypeMismatch()
    {
        var world = new Wire.World { Arena = new GesVector(1, 2, 3) };
        var error = Assert.ThrowsExactly<GesWireException>(() => world.ToBytes());
        Assert.AreEqual(GesWireException.TypeMismatch, error.Code);
        Assert.AreEqual("arena", error.Path);
        var bot = new Wire.World { };
        bot.Bots.Add(new Wire.World_Bot { Position = new GesPoint(1, 1, 1) });
        error = Assert.ThrowsExactly<GesWireException>(() => bot.ToBytes());
        Assert.AreEqual(GesWireException.TypeMismatch, error.Code);
        Assert.AreEqual("bots[].position", error.Path);
        var baseline = new Wire.World();
        var delta = new byte[Wire.World.MaxByteCount];
        error = Assert.ThrowsExactly<GesWireException>(() => world.WriteDelta(baseline, delta));
        Assert.AreEqual(GesWireException.TypeMismatch, error.Code);
    }

    [TestMethod]
    public void ModelsCompareByValue()
    {
        static Model.BotView Bot() => new()
        {
            Bot = 4,
            Name = "Ada",
            Position = new GesPoint(1, 2),
            Body = new Model.World_Body(80, Model.World_Body.HullTag.Heavy),
            Facing = new Model.BotView.FacingAlternative.Tag(Model.BotView.FacingTagTag.North),
            Marks = { new GesPoint(0, -0.0), new GesPoint(3, 4) },
            Units = { new Model.BotView.UnitsItemAlternative.Text("x"), new Model.BotView.UnitsItemAlternative.World_Body(new Model.World_Body()) },
            Grid = { new List<long> { 1, 2 }, new List<long>() },
            Rolls = { 6, 3 },
            Hints = { Model.BotView.HintsItemTag.Left }
        };

        var left = Bot();
        var right = Bot();
        right.Marks[0] = new GesPoint(0, 0);
        Assert.AreEqual(left, right, "0 and -0 are equal.");
        Assert.AreEqual(left.GetHashCode(), right.GetHashCode());
        Assert.IsTrue(left == right);
        right.Grid[0].Add(3);
        Assert.AreNotEqual(left, right);
        right = Bot();
        right.Facing = new Model.BotView.FacingAlternative.Quantity(0);
        Assert.AreNotEqual(left, right);
        right.Facing = null;
        Assert.AreNotEqual(left, right);
        var view = new Model.WorldView { Bots = { left }, Badge = new Model.Panel_Badge("hi") };
        Assert.AreEqual(view, new Model.WorldView { Bots = { Bot() }, Badge = new Model.Panel_Badge("hi") });
    }

    [TestMethod]
    public void ModelDefaultsAreZeroValues()
    {
        var bot = new Model.BotView();
        Assert.AreEqual("", bot.Name);
        Assert.AreEqual("", bot.Team);
        Assert.AreEqual(GesPoint.Zero, bot.Position);
        Assert.IsNull(bot.Radar);
        Assert.IsNull(bot.Facing);
        Assert.IsNull(bot.Note);
        Assert.IsEmpty(bot.Marks);
        Assert.AreEqual(new Model.World_Body(), bot.Body);
    }

    [TestMethod]
    public void ModelRunsRejectWireOptions()
    {
        var error = Assert.ThrowsExactly<GesGeneratorException>(() => GesSchemaGenerator.GenerateModels(Set(), ["WorldView"], new GesSchemaGenerateOptions { Values = true }));
        Assert.AreEqual("generate.usage", error.Code);
        error = Assert.ThrowsExactly<GesGeneratorException>(() => GesSchemaGenerator.ModelLayout(GesSchemaSet.Parse("message M()\n"), ["M"]));
        Assert.AreEqual("generate.usage", error.Code);
        error = Assert.ThrowsExactly<GesGeneratorException>(() => GesSchemaGenerator.ModelLayout(GesSchemaSet.Parse("schema :A as { m: :Map }\n"), ["A"]));
        Assert.AreEqual("generate.unsupported", error.Code);
    }
}
