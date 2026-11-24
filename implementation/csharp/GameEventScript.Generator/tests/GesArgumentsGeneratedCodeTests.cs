// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Generator;
using GameEventScript.Runtime.Values;
using Wire = GameEventScript.Tests.Generator.Arguments.Wire;

namespace GameEventScript.Tests.Generator;

// The checked-in code of conformance/fixtures/SchemaGeneration/arguments.gess: message argument defaults in generated
// initializers and the lenient read, which agrees with GesSchemaValues.CompleteMessage.
[TestClass]
public sealed class GesArgumentsGeneratedCodeTests
{
    private static GesSchemaSet Set()
    {
        var fixture = Path.Combine(TestRepositoryPaths.ConformanceDirectory, "fixtures", "SchemaGeneration", "arguments.gess");
        return GesSchemaSet.Parse(new GesSchemaSource("arguments.gess", File.ReadAllText(fixture)));
    }

    private static GesSchemaGenerateOptions Options(GesSchemaLanguage language, string space) => new() { Language = language, Namespace = space, Access = GesSchemaAccess.Internal, Values = true };

    private static GameEventScriptMessage Message(string name, params GesMessageArgument[] arguments) => GameEventScriptMessage.Create(name, arguments);

    [TestMethod]
    public void CheckedInCodeIsCurrent()
    {
        var set = Set();
        var csharp = Path.Combine(TestRepositoryPaths.CSharpDirectory, "GameEventScript.Generator", "tests", "Generated");
        var swift = Path.Combine(TestRepositoryPaths.Root, "implementation", "swift", "GameEventScriptConformance", "Tests", "GameEventScriptConformanceTests", "Generated");
        var wire = GesSchemaGenerator.WireRoots(set, "arguments");
        var parameters = new GesSchemaParameters();
        Assert.AreEqual(File.ReadAllText(Path.Combine(csharp, "ArgumentsWire.g.cs")), GesSchemaGenerator.Generate(set, wire, parameters, Options(GesSchemaLanguage.CSharp, "GameEventScript.Tests.Generator.Arguments.Wire")));
        Assert.AreEqual(File.ReadAllText(Path.Combine(swift, "ArgumentsWire.swift")), GesSchemaGenerator.Generate(set, wire, parameters, Options(GesSchemaLanguage.Swift, "ArgumentsWire")));
    }

    [TestMethod]
    public void InitializerTakesTheDefaults()
    {
        var fire = new Wire.Fire(count: 3);
        Assert.AreEqual(3L, fire.Count);
        Assert.IsNull(fire.ChargeOptional);
        Assert.AreEqual(Wire.FireWhen.Aimed, fire.When);
        Assert.AreEqual(2.0, fire.Delay);
        Assert.AreEqual(0.5, fire.Ratio);
        Assert.IsFalse(fire.Loud);
        Assert.AreEqual(4L, new Wire.Fire(charge: 4).ChargeOptional);

        var say = new Wire.Say("hi");
        Assert.AreEqual("hi", say.Text);
        Assert.IsTrue(say.Loud);
    }

    [TestMethod]
    public void ParameterlessCreationStaysZero()
    {
        var fire = new Wire.Fire();
        Assert.AreEqual(0L, fire.Count);
        Assert.AreEqual(0.0, fire.Delay);
        Assert.IsFalse(new Wire.Say().Loud);
    }

    [TestMethod]
    public void DefaultsCountOnlyOnTrailingParameters()
    {
        var turn = typeof(Wire.Turn).GetConstructor([typeof(long), typeof(Wire.Turn.SideTag)]);
        Assert.IsNotNull(turn);
        Assert.IsFalse(turn.GetParameters()[0].HasDefaultValue);
        Assert.HasCount(1, typeof(Wire.Mark).GetConstructors());
        Assert.IsEmpty(typeof(Wire.Stop).GetConstructors());
    }

    [TestMethod]
    public void LenientReadFillsTheDefaults()
    {
        var message = Message("Fire", new GesMessageArgument("count", GesValue.GesInteger(5)), new GesMessageArgument("loud", GesValue.GesBoolean(true)));
        var fire = new Wire.Fire();
        fire.ReadMessageLenient(message);
        Assert.AreEqual(5L, fire.Count);
        Assert.IsNull(fire.ChargeOptional);
        Assert.AreEqual(Wire.FireWhen.Aimed, fire.When);
        Assert.AreEqual(2.0, fire.Delay);
        Assert.AreEqual(0.5, fire.Ratio);
        Assert.IsTrue(fire.Loud);

        var bound = Set().Schema("Fire").Bind();
        var expected = new byte[Wire.Fire.MaxByteCount];
        var count = GesSchemaValues.Encode(bound, GesValue.GesMessage(GesSchemaValues.CompleteMessage(bound, message)), expected);
        CollectionAssert.AreEqual(expected[..count], fire.ToBytes());
        CollectionAssert.AreEqual(new Wire.Fire(count: 5, loud: true).ToBytes(), fire.ToBytes());
    }

    [TestMethod]
    public void LenientReadOfASetFillsTheMember()
    {
        var actions = new Wire.Actions();
        actions.ReadMessageLenient(Message("Turn", new GesMessageArgument("side", GesValue.GesTag("right"))));
        Assert.AreEqual(Wire.Actions.Member.Turn, actions.Selected);
        Assert.AreEqual(3L, actions.Turn.Speed);
        Assert.AreEqual(Wire.Turn.SideTag.Right, actions.Turn.Side);
    }

    [TestMethod]
    public void LenientReadRejectsAMissingArgument()
    {
        var turn = new Wire.Turn();
        var error = Assert.ThrowsExactly<GesWireException>(() => turn.ReadMessageLenient(Message("Turn", new GesMessageArgument("speed", GesValue.GesInteger(2)))));
        Assert.AreEqual(GesWireException.MissingField, error.Code);
        Assert.AreEqual("side", error.Path);
    }
}
