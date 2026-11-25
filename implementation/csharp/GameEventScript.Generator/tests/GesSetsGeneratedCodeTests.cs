// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Generator;
using GameEventScript.Runtime.Values;
using Model = GameEventScript.Tests.Generator.Sets.Model;
using Wire = GameEventScript.Tests.Generator.Sets.Wire;

namespace GameEventScript.Tests.Generator;

// The checked-in code of conformance/fixtures/SchemaGeneration/sets.gess: message sets with parameters as the envelope
// message named after the set, the converters between model sets, and the wire code that converts to the models.
[TestClass]
public sealed class GesSetsGeneratedCodeTests
{
    private static GesSchemaSet Set()
    {
        var fixture = Path.Combine(TestRepositoryPaths.ConformanceDirectory, "fixtures", "SchemaGeneration", "sets.gess");
        return GesSchemaSet.Parse(new GesSchemaSource("sets.gess", File.ReadAllText(fixture)));
    }

    private static Model.DroneCommands Commands() => new(
        new Model.DroneCommands.Case.MoveTo(new Model.MoveTo(new GesPoint(1.5, -2, 3), "north", fast: true)),
        [new Model.DroneId(7), new Model.DroneId(4095)],
        urgent: true);

    [TestMethod]
    public void CheckedInCodeIsCurrent()
    {
        var set = Set();
        var wire = GesSchemaGenerator.WireRoots(set, "sets");
        var model = GesSchemaGenerator.ModelRoots(set, "sets");
        var parameters = new GesSchemaParameters();
        const string modelSpace = "GameEventScript.Tests.Generator.Sets.Model";
        GesSchemaGenerateOptions Options(GesSchemaLanguage language, string space, string? models = null) => new() { Language = language, Namespace = space, Access = GesSchemaAccess.Internal, Values = true, Models = models };
        var csharp = Path.Combine(TestRepositoryPaths.CSharpDirectory, "GameEventScript.Generator", "tests", "Generated");
        var swift = Path.Combine(TestRepositoryPaths.Root, "implementation", "swift", "GameEventScriptConformance", "Tests", "GameEventScriptConformanceTests", "Generated");
        Assert.AreEqual(File.ReadAllText(Path.Combine(csharp, "SetsModel.g.cs")), GesSchemaGenerator.GenerateModels(set, model, Options(GesSchemaLanguage.CSharp, modelSpace)));
        Assert.AreEqual(File.ReadAllText(Path.Combine(csharp, "SetsWire.g.cs")), GesSchemaGenerator.Generate(set, wire, parameters, Options(GesSchemaLanguage.CSharp, "GameEventScript.Tests.Generator.Sets.Wire", modelSpace)));
        Assert.AreEqual(File.ReadAllText(Path.Combine(swift, "SetsModel.swift")), GesSchemaGenerator.GenerateModels(set, model, Options(GesSchemaLanguage.Swift, "SetsModel")));
        Assert.AreEqual(File.ReadAllText(Path.Combine(swift, "SetsWire.swift")), GesSchemaGenerator.Generate(set, wire, parameters, Options(GesSchemaLanguage.Swift, "SetsWire", "SetsModel")));
    }

    [TestMethod]
    public void ModelHashesMatchTheSchemas()
    {
        var set = Set();
        Assert.AreEqual(Model.DroneCommands.SchemaHash, set.Schema("DroneCommands").Hash);
        Assert.AreEqual(Model.Landing.SchemaHash, set.Schema("Landing").Hash);
    }

    [TestMethod]
    public void ModelSetIsTheEnvelopeMessage()
    {
        var commands = Commands();
        var message = commands.ToMessage();
        Assert.AreEqual("DroneCommands", message.Name);
        CollectionAssert.AreEqual(new[] { "ids", "urgent", "message" }, message.Arguments.Select(argument => argument.Name).ToArray());
        Assert.AreEqual(commands, Model.DroneCommands.FromMessage(message));

        var bound = Set().Schema("DroneCommands").Bind();
        var bytes = new byte[Wire.DroneCommands.MaxByteCount];
        var count = GesSchemaValues.Encode(bound, GesValue.GesMessage(message), bytes);
        Assert.AreEqual(message, GesSchemaValues.Decode(bound, bytes.AsSpan(0, count)).Value.Message);
    }

    [TestMethod]
    public void ModelSetDefaultsItsParameters()
    {
        var commands = new Model.DroneCommands(new Model.DroneCommands.Case.Stop(new Model.Stop()));
        Assert.IsEmpty(commands.Ids);
        Assert.IsNull(commands.Urgent);
        Assert.AreEqual(0, new Model.Landing(new Model.Landing.Case.Land(new Model.Land())).Meta.Length);
    }

    [TestMethod]
    public void LenientReadCompletesParametersAndMessage()
    {
        var inner = GameEventScriptMessage.Create("Land");
        var message = GameEventScriptMessage.Create("Landing", [new GesMessageArgument("message", GesValue.GesMessage(inner))]);
        var landing = Model.Landing.FromMessageLenient(message);
        Assert.AreEqual(0, landing.Meta.Length);
        var land = Assert.IsInstanceOfType<Model.Landing.Case.Land>(landing.Message);
        Assert.IsFalse(land.Value.Slow);
        Assert.ThrowsExactly<GesWireException>(() => Model.Landing.FromMessage(message));
    }

    [TestMethod]
    public void ReadRejectsTheBareMessage()
    {
        var bare = new Model.Stop().ToMessage();
        var error = Assert.ThrowsExactly<GesWireException>(() => Model.DroneCommands.FromMessage(bare));
        Assert.AreEqual(GesWireException.TypeMismatch, error.Code);
        Assert.AreEqual("", error.Path);
    }

    [TestMethod]
    public void ConvertersTakeSharedMembers()
    {
        var move = Commands().Message;
        var landing = Model.Landing.Case.From(move);
        Assert.IsInstanceOfType<Model.Landing.Case.MoveTo>(landing);
        Assert.AreEqual(move, Model.DroneCommands.Case.From(landing!));
        Assert.IsNull(Model.Landing.Case.From(new Model.DroneCommands.Case.Stop(new Model.Stop())));
        Assert.IsNull(Model.DroneCommands.Case.From(new Model.Landing.Case.Land(new Model.Land(slow: true))));
    }

    [TestMethod]
    public void WireConvertsToTheModel()
    {
        var commands = Commands();
        var wire = new Wire.DroneCommands();
        wire.CopyFrom(commands);
        Assert.AreEqual(Wire.DroneCommands.Member.MoveTo, wire.Selected);
        Assert.AreEqual(commands, wire.ToModel());
        Assert.AreEqual(commands.ToMessage(), wire.ToMessage());

        var read = new Wire.DroneCommands();
        read.Read(wire.ToBytes());
        Assert.AreEqual(commands, read.ToModel());

        var fromMessage = new Wire.DroneCommands();
        fromMessage.ReadMessage(commands.ToMessage());
        CollectionAssert.AreEqual(wire.ToBytes(), fromMessage.ToBytes());
    }
}
