// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Generator;
using GameEventScript.Runtime.Values;
using Model = GameEventScript.Tests.Generator.Computed.Model;
using Wire = GameEventScript.Tests.Generator.Computed.Wire;

namespace GameEventScript.Tests.Generator;

// The checked-in code of conformance/fixtures/SchemaGeneration/computed.gess: computed fields in plain binary64 and Boolean
// code where binding proves the bits of the virtual machine, the dynamic form where it cannot, and both agree with the value
// path of the runtime.
[TestClass]
public sealed class GesComputedGeneratedCodeTests
{
    private static GesSchemaSet Set()
    {
        var fixture = Path.Combine(TestRepositoryPaths.ConformanceDirectory, "fixtures", "SchemaGeneration", "computed.gess");
        return GesSchemaSet.Parse(new GesSchemaSource("computed.gess", File.ReadAllText(fixture)));
    }

    private static GesSchemaGenerateOptions Options(GesSchemaLanguage language, string space) => new() { Language = language, Namespace = space, Access = GesSchemaAccess.Internal, Values = true };

    private static string Generated(string name) => File.ReadAllText(Path.Combine(TestRepositoryPaths.CSharpDirectory, "GameEventScript.Generator", "tests", "Generated", name));

    [TestMethod]
    public void CheckedInCodeIsCurrent()
    {
        var set = Set();
        var swift = Path.Combine(TestRepositoryPaths.Root, "implementation", "swift", "GameEventScriptConformance", "Tests", "GameEventScriptConformanceTests", "Generated");
        var wire = GesSchemaGenerator.WireRoots(set, "computed");
        var model = GesSchemaGenerator.ModelRoots(set, "computed");
        var parameters = new GesSchemaParameters();
        Assert.AreEqual(Generated("ComputedWire.g.cs"), GesSchemaGenerator.Generate(set, wire, parameters, Options(GesSchemaLanguage.CSharp, "GameEventScript.Tests.Generator.Computed.Wire")));
        Assert.AreEqual(File.ReadAllText(Path.Combine(swift, "ComputedWire.swift")), GesSchemaGenerator.Generate(set, wire, parameters, Options(GesSchemaLanguage.Swift, "ComputedWire")));
        Assert.AreEqual(Generated("ComputedModel.g.cs"), GesSchemaGenerator.GenerateModels(set, model, Options(GesSchemaLanguage.CSharp, "GameEventScript.Tests.Generator.Computed.Model")));
        Assert.AreEqual(File.ReadAllText(Path.Combine(swift, "ComputedModel.swift")), GesSchemaGenerator.GenerateModels(set, model, Options(GesSchemaLanguage.Swift, "ComputedModel")));
    }

    [TestMethod]
    public void BoundExpressionsBecomePlainCode()
    {
        var code = Generated("ComputedWire.g.cs");
        string[] lines =
        [
            "public double Power => Duration <= 0.0 ? 0.0 : Energy / Duration;",
            "public double Boosted => GesComputed.AddPercentage(Power, Boost);",
            "public double Level => HasTrim ? Trim : 0.0;",
            "public bool Marked => HasTrim && Trim > 1.0;",
            "public double CellCount => (double)Cells.Count;",
            "public bool Steady => GesComputed.GreaterOrNothing(Energy / Duration, 1000.0) == false;",
            "public bool Balanced => GesComputed.Equal(Energy / Duration, Power);"
        ];
        foreach (var line in lines) StringAssert.Contains(code, line);

        // An integer field without a range may hold whole numbers beyond 2^53, which the virtual machine multiplies exactly.
        StringAssert.Contains(code, "public double Doubled => GesComputed.Number(__Doubled());");
        Assert.IsFalse(code.Contains("internal double __Power", StringComparison.Ordinal), "A static field has no exact-value method.");
    }

    private static Wire.Reactor Reactor(double energy, double duration, double? trim, long ticks, params double[] draws)
    {
        var reactor = new Wire.Reactor { Energy = energy, Duration = duration, Boost = 0.25, TrimOptional = trim, Ticks = ticks };
        foreach (var draw in draws) reactor.Cells.Add(new Wire.Cell { Draw = draw });
        return reactor;
    }

    private static GesValue Field(GesValue record, string name) => record.AsMap()!.Get(name) ?? throw new AssertFailedException($"The record has no field {name}.");

    private static void AssertNumber(GesValue expected, double? actual, string name)
    {
        if (expected.IsNothing)
        {
            Assert.IsTrue(actual is null || double.IsNaN(actual.Value), $"{name}: expected nothing, got {actual}");
            return;
        }

        Assert.AreEqual(BitConverter.DoubleToInt64Bits(expected.AsNumber()), BitConverter.DoubleToInt64Bits(actual!.Value), $"{name}: expected {expected.AsNumber()}, got {actual}");
    }

    [TestMethod]
    public void StaticAndDynamicFieldsGiveTheBitsOfTheValuePath()
    {
        Wire.Reactor[] reactors =
        [
            Reactor(900000, 0.3, 2.5, 12, 100, 250),
            Reactor(0, 0, null, -7),
            Reactor(1, 3, -10, long.MaxValue / 4, 5000, 5000, 5000),
            Reactor(1000000, 60, 1, 1L << 53, 1),
            Reactor(250, 0.001, 0.5, 0)
        ];
        foreach (var reactor in reactors)
        {
            var value = reactor.ToValue();
            AssertNumber(Field(value, "power"), reactor.Power, "power");
            AssertNumber(Field(value, "boosted"), reactor.Boosted, "boosted");
            AssertNumber(Field(value, "level"), reactor.Level, "level");
            AssertNumber(Field(value, "trimmed"), reactor.Trimmed, "trimmed");
            AssertNumber(Field(value, "load"), reactor.Load, "load");
            AssertNumber(Field(value, "cellCount"), reactor.CellCount, "cellCount");
            AssertNumber(Field(value, "doubled"), reactor.Doubled, "doubled");
            Assert.AreEqual(Field(value, "marked").AsBoolean(), reactor.Marked, "marked");
            Assert.AreEqual(Field(value, "steady").AsBoolean(), reactor.Steady, "steady");
            Assert.AreEqual(Field(value, "balanced").AsBoolean(), reactor.Balanced, "balanced");

            // A model computes the same values from the same fields and converts them back unchanged.
            var model = Model.Reactor.FromValue(value);
            Assert.AreEqual(BitConverter.DoubleToInt64Bits(reactor.Power), BitConverter.DoubleToInt64Bits(model.Power));
            Assert.AreEqual(reactor.Steady, model.Steady);
            Assert.AreEqual(value, model.ToValue());
        }
    }
}
