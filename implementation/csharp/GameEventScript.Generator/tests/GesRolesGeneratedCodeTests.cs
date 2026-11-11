// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Generator;
using GameEventScript.Runtime.Values;
using Bound = GameEventScript.Tests.Generator.Roles.Bound;
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

    private static GesSchemaGenerateOptions Options(GesSchemaLanguage language, string space, bool values = false) => new() { Language = language, Namespace = space, Access = GesSchemaAccess.Internal, Values = values };

    [TestMethod]
    public void CheckedInCodeIsCurrent()
    {
        var set = Set();
        var csharp = Path.Combine(TestRepositoryPaths.CSharpDirectory, "GameEventScript.Generator", "tests", "Generated");
        var swift = Path.Combine(TestRepositoryPaths.Root, "implementation", "swift", "GameEventScriptConformance", "Tests", "GameEventScriptConformanceTests", "Generated");
        var wire = GesSchemaGenerator.WireRoots(set, "world");
        var model = GesSchemaGenerator.ModelRoots(set, "view");
        CollectionAssert.AreEqual(new[] { "World", "World_Cards", "Panel_Report" }, wire.ToArray());
        CollectionAssert.AreEqual(new[] { "Panel_Report", "WorldView" }, model.ToArray());
        var parameters = new GesSchemaParameters().With("worldWidth", "2000m");
        Assert.AreEqual(File.ReadAllText(Path.Combine(csharp, "RolesWire.g.cs")), GesSchemaGenerator.Generate(set, wire, parameters, Options(GesSchemaLanguage.CSharp, "GameEventScript.Tests.Generator.Roles.Wire", true)));
        Assert.AreEqual(File.ReadAllText(Path.Combine(csharp, "RolesModel.g.cs")), GesSchemaGenerator.GenerateModels(set, model, Options(GesSchemaLanguage.CSharp, "GameEventScript.Tests.Generator.Roles.Model", true)));
        Assert.AreEqual(File.ReadAllText(Path.Combine(swift, "RolesWire.swift")), GesSchemaGenerator.Generate(set, wire, parameters, Options(GesSchemaLanguage.Swift, "RolesWire", true)));
        Assert.AreEqual(File.ReadAllText(Path.Combine(swift, "RolesModel.swift")), GesSchemaGenerator.GenerateModels(set, model, Options(GesSchemaLanguage.Swift, "RolesModel", true)));
        var bound = Options(GesSchemaLanguage.CSharp, "GameEventScript.Tests.Generator.Roles.Bound", true);
        bound.Bindings = true;
        Assert.AreEqual(File.ReadAllText(Path.Combine(csharp, "RolesBound.g.cs")), GesSchemaGenerator.GenerateModels(set, model, bound));
        bound = Options(GesSchemaLanguage.Swift, "RolesBound", true);
        bound.Bindings = true;
        Assert.AreEqual(File.ReadAllText(Path.Combine(swift, "RolesBound.swift")), GesSchemaGenerator.GenerateModels(set, model, bound));
        Assert.AreEqual("generate.usage", Assert.ThrowsExactly<GesGeneratorException>(() => GesSchemaGenerator.Generate(set, wire, parameters, bound)).Code);
    }

    private static Model.Panel_Report Report(string title = "Pump") => new(
        title,
        Model.Panel_Report.StatusTag.Warn,
        640,
        new GesPoint(1.5, 2.25),
        null,
        [new Model.Panel_Entry("left", 0.25), new Model.Panel_Entry("right", 1)],
        new Model.Panel_Report.ReadingAlternative.Number(42),
        Model.Panel_Report.AlarmsFlags.Power);

    [TestMethod]
    public void AModelRoundTripsThroughItsPlainValue()
    {
        var report = Report();
        var value = report.ToValue();
        Assert.AreEqual("Panel_Report", value.CustomTypeName);
        var fields = value.AsMap()!;
        CollectionAssert.AreEqual(new[] { "title", "status", "load", "spot", "note", "entries", "reading", "alarms" }, Enumerable.Range(0, fields.Length).Select(fields.KeyAt).OrderBy(key => Array.IndexOf(new[] { "title", "status", "load", "spot", "note", "entries", "reading", "alarms" }, key)).ToArray());
        Assert.AreEqual("Panel_Entry", fields.Get("entries")!.Value.AsList()[0].CustomTypeName, "A nested record is a Record, not an external value.");
        Assert.IsTrue(fields.Get("note")!.Value.IsNothing, "An absent optional is nothing.");
        Assert.AreEqual(42L, fields.Get("reading")!.Value.AsInteger(), "A union holds its alternative's value.");
        Assert.AreEqual(report, Model.Panel_Report.FromValue(value));
        var other = Report();
        other.Note = "spare";
        other.Reading = new Model.Panel_Report.ReadingAlternative.Text("x");
        other.Alarms = Model.Panel_Report.AlarmsFlags.Heat | Model.Panel_Report.AlarmsFlags.Power;
        Assert.AreEqual(other, Model.Panel_Report.FromValue(other.ToValue()));
    }

    [TestMethod]
    public void AModelConvertsToAWireRecordAndBack()
    {
        var report = Report();
        var wire = new Wire.Panel_Report();
        wire.ReadValue(report.ToValue());
        Assert.AreEqual("Pump", wire.Title);
        Assert.HasCount(2, wire.Entries);
        Assert.AreEqual(report, Model.Panel_Report.FromValue(wire.ToValue()));
        var back = new Wire.Panel_Report();
        back.Read(wire.ToBytes());
        Assert.AreEqual(report, Model.Panel_Report.FromValue(back.ToValue()));
    }

    [TestMethod]
    public void AModelValueThatDoesNotFitFailsOnTheWireSide()
    {
        var report = Report("a title that is far too long");
        var value = report.ToValue();
        Assert.AreEqual(report, Model.Panel_Report.FromValue(value), "The model value itself holds any text.");
        var wire = new Wire.Panel_Report();
        var failure = Assert.ThrowsExactly<GesWireException>(() => wire.ReadValue(value));
        Assert.AreEqual(GesWireException.TooLong, failure.Code);
        Assert.AreEqual("title", failure.Path);
    }

    [TestMethod]
    public void AValueThatDoesNotConvertNamesItsField()
    {
        var fields = Report().ToValue().AsMap()!;
        var keys = Enumerable.Range(0, fields.Length).Select(fields.KeyAt).ToArray();
        var values = keys.Select(key => key == "load" ? GesValue.GesFloat(640, GesQuantityUnit.UnitMeter) : fields.Get(key)!.Value).ToArray();
        var failure = Assert.ThrowsExactly<GesWireException>(() => Model.Panel_Report.FromValue(GesValue.GesRecord("Panel_Report", keys, values)));
        Assert.AreEqual(GesWireException.TypeMismatch, failure.Code);
        Assert.AreEqual("load", failure.Path);
        Assert.AreEqual("", Assert.ThrowsExactly<GesWireException>(() => Model.Panel_Report.FromValue(GesValue.GesRecord("Other", keys, values))).Path);
        var map = GesValue.GesMap(keys, keys.Select(key => fields.Get(key)!.Value).ToArray());
        Assert.AreEqual(Report(), Model.Panel_Report.FromValue(map), "A Map of only the fields reads too.");
    }

    [TestMethod]
    public void ANestedValueThatDoesNotConvertNamesItsPathAsTheWireSideDoes()
    {
        var fields = Report().ToValue().AsMap()!;
        var keys = Enumerable.Range(0, fields.Length).Select(fields.KeyAt).ToArray();
        var entry = GesValue.GesRecord("Panel_Entry", ["name", "level"], [GesValue.GesText("left"), GesValue.GesFloat(0.25, GesQuantityUnit.UnitMeter)]);
        foreach (var (entries, path) in new[]
        {
            (GesValue.GesList([entry]), "entries[].level"),
            (GesValue.GesList([GesValue.GesText("left")]), "entries[]"),
            (GesValue.GesText("left"), "entries"),
        })
        {
            var value = GesValue.GesRecord("Panel_Report", keys, keys.Select(key => key == "entries" ? entries : fields.Get(key)!.Value).ToArray());
            var failure = Assert.ThrowsExactly<GesWireException>(() => Model.Panel_Report.FromValue(value));
            Assert.AreEqual(GesWireException.TypeMismatch, failure.Code);
            Assert.AreEqual(path, failure.Path);
            Assert.AreEqual(path, Assert.ThrowsExactly<GesWireException>(() => new Wire.Panel_Report().ReadValue(value)).Path);
        }
    }

    [TestMethod]
    public void BoundModelsAlsoConvertToPlainValues()
    {
        var value = Report().ToValue();
        var bound = Bound.Panel_Report.FromValue(value);
        Assert.AreEqual(value, bound.ToValue());
        Assert.AreEqual(bound, Bound.Panel_Report.FromGesValue(bound.ToGesValue()));
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
        world.Bots[0].SetToken([0xa0, 0x00, 0xff]);
        var read = new Wire.World { Arena = new GesVector(0, 0, 9) };
        read.Read(world.ToBytes());
        Assert.AreEqual(new GesVector(2000, 1500), read.Arena, "A 2D field reads its omitted axis as zero.");
        Assert.AreEqual(new GesPoint(12.34, 56.78), read.Bots[0].Position);
        Assert.AreEqual(new GesVector(-1.5, 2.25, 0.5), read.Bots[0].Velocity);
        CollectionAssert.AreEqual(new byte[] { 0xa0, 0x00, 0xff }, read.Bots[0].Token.ToArray(), "Bytes need not be UTF-8.");
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
        var error = Assert.ThrowsExactly<GesGeneratorException>(() => GesSchemaGenerator.GenerateModels(Set(), ["WorldView"], new GesSchemaGenerateOptions { CheckedExclusivity = true }));
        Assert.AreEqual("generate.usage", error.Code);
        error = Assert.ThrowsExactly<GesGeneratorException>(() => GesSchemaGenerator.ModelLayout(GesSchemaSet.Parse("message M()\n"), ["M"]));
        Assert.AreEqual("generate.usage", error.Code);
        error = Assert.ThrowsExactly<GesGeneratorException>(() => GesSchemaGenerator.ModelLayout(GesSchemaSet.Parse("schema :A as { m: :Map }\n"), ["A"]));
        Assert.AreEqual("generate.unsupported", error.Code);
    }
}

// Generated models with --bindings: scripts read and construct them as external types.
[TestClass]
public sealed class GesRolesBindingTests
{
    private const string Script = """
        on Start(view) {
          let bot be view.bots[1]
          let made be :World_Body(fields: [mass: 90kg, hull: #light])
          let radar be :RadarView(fields: [range: 300m, contacts: [1, 2]])
          let built be :BotView(fields: [bot: 7, name: 'Bo', alive: true, health: 50%, speed: 2.5, heading: 90°, mood: #angry, team: 'blue', position: :Point(3m, 4m), velocity: :Vector(1m, 0m), body: made, radar: radar, facing: #north, marks: [:Point(1m, 1m)], units: ['x', made], grid: [[1, 2]], rolls: :Dice[6, 3], seal: :Bytes("a0ff"), chunks: [:Bytes[1], :Bytes[]], status: [armed:, cloaked: false], ranks: [[gold:, silver:], [:]], hints: [#left, #right]])
          emit Seen(name: bot.name, x: bot.position.x, mass: bot.body.mass, radar: bot.radar, facing: bot.facing, units: bot.units[:count], hint: bot.hints[1], built: built, bad: :World_Body(fields: [mass: 90m, hull: #light]), unknown: :World_Body(fields: [mass: 1kg, hull: #light, extra: 1]), copied: :World_Body(fields: made.fields))
        }
        """;

    private sealed class Seen : IGesNativeMessageHandler
    {
        public GameEventScriptMessage? Message { get; private set; }

        public void Handle(GameEventScriptMessage message, GesContext context) => Message = message;
    }

    [TestMethod]
    public void ScriptsReadAndConstructGeneratedRecords()
    {
        var types = new Bound.GesTypes();
        var program = new GesProgramBuilder().WithExternalTypeCatalog(types).AddScript(Script, "bound.ges").Compile();
        var host = GameEventScriptHost.CreateBuilder().WithExternalTypeRegistry(types).Build();
        host.Load(program);
        var seen = new Seen();
        host.Subscribe(GesMessageSignature.Create("Seen", ["name", "x", "mass", "radar", "facing", "units", "hint", "built", "bad", "unknown", "copied"]), seen);
        Assert.AreEqual(GesStartState.Ready, host.Start().State);
        var view = new Bound.WorldView { Tick = 1 };
        view.Bots.Add(new Bound.BotView
        {
            Bot = 4,
            Name = "Ada",
            Position = new GesPoint(2, 5),
            Body = new Bound.World_Body(120, Bound.World_Body.HullTag.Heavy),
            Facing = new Bound.BotView.FacingAlternative.Quantity(45),
            Units = { new Bound.BotView.UnitsItemAlternative.Text("a") },
            Hints = { Bound.BotView.HintsItemTag.Right }
        });
        Assert.IsTrue(host.Receive(GameEventScriptMessage.Create("Start", [new("view", view.ToGesValue())])));
        Assert.AreEqual(GesExecutionState.Completed, host.RunToCompletion().State);
        var arguments = seen.Message!.Arguments;
        Assert.AreEqual("Ada", arguments[0].AsText());
        Assert.AreEqual(2d, arguments[1].AsNumber());
        Assert.AreEqual(GesQuantityUnit.UnitMeter, arguments[1].ValueUnit);
        Assert.AreEqual(120d, arguments[2].AsNumber());
        Assert.IsTrue(arguments[3].IsNothing, "An absent optional record reads as nothing.");
        Assert.AreEqual(45d, arguments[4].AsNumber());
        Assert.AreEqual(1L, arguments[5].AsInteger());
        Assert.AreEqual("right", arguments[6].AsText());
        var built = Bound.BotView.FromGesValue(arguments[7]);
        Assert.IsNotNull(built);
        Assert.AreEqual(7L, built.Bot);
        Assert.AreEqual(0.5, built.Health);
        Assert.AreEqual(Bound.BotView.MoodTag.Angry, built.Mood);
        Assert.AreEqual(new GesPoint(3, 4), built.Position);
        Assert.AreEqual(new Bound.World_Body(90, Bound.World_Body.HullTag.Light), built.Body);
        Assert.AreEqual(new Bound.RadarView(300, [1, 2]), built.Radar);
        Assert.AreEqual(new Bound.BotView.FacingAlternative.Tag(Bound.BotView.FacingTagTag.North), built.Facing);
        Assert.IsNull(built.Note);
        Assert.HasCount(2, built.Units);
        Assert.AreEqual(new Bound.BotView.UnitsItemAlternative.World_Body(new Bound.World_Body(90, Bound.World_Body.HullTag.Light)), built.Units[1]);
        CollectionAssert.AreEqual(new List<long> { 1, 2 }, built.Grid[0]);
        CollectionAssert.AreEqual(new List<int> { 6, 3 }, built.Rolls);
        CollectionAssert.AreEqual(new[] { Bound.BotView.HintsItemTag.Left, Bound.BotView.HintsItemTag.Right }, built.Hints);
        Assert.AreEqual(Bound.BotView.StatusFlags.Armed, built.Status);
        CollectionAssert.AreEqual(new[] { Bound.BotView.RanksItemFlags.Gold | Bound.BotView.RanksItemFlags.Silver, default }, built.Ranks);
        Assert.IsTrue(arguments[8].IsNothing, "A value of another unit constructs nothing.");
        Assert.IsTrue(arguments[9].IsNothing, "A field that the type does not declare constructs nothing.");
        Assert.AreEqual(new Bound.World_Body(90, Bound.World_Body.HullTag.Light), Bound.World_Body.FromGesValue(arguments[10]), "The fields of a record construct an equal record.");
    }

    [TestMethod]
    public void ASetRefusesATypeThatTheNextCatalogDeclares()
    {
        var first = new Bound.GesTypes();
        Assert.ThrowsExactly<ArgumentException>(() => new Bound.GesTypes(first));
        CollectionAssert.AreEqual(new[] { "World_Body", "RadarView", "BotView", "Panel_Badge", "Panel_Entry", "Panel_Report", "WorldView" }, new Bound.GesTypes().Types.Select(type => type.Name).ToArray());
    }
}
