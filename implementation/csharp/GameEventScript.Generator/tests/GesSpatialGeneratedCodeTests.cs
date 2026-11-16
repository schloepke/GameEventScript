// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Generator;
using GameEventScript.Runtime.Values;
using Model = GameEventScript.Tests.Generator.Spatial.Model;
using Wire = GameEventScript.Tests.Generator.Spatial.Wire;

namespace GameEventScript.Tests.Generator;

// The checked-in code of conformance/fixtures/SchemaGeneration/spatial.gess: computed vectors, points, boxes, discs and
// polygons, which generated code computes on the runtime's types with the bits of the value path.
[TestClass]
public sealed class GesSpatialGeneratedCodeTests
{
    private static GesSchemaSet Set()
    {
        var fixture = Path.Combine(TestRepositoryPaths.ConformanceDirectory, "fixtures", "SchemaGeneration", "spatial.gess");
        return GesSchemaSet.Parse(new GesSchemaSource("spatial.gess", File.ReadAllText(fixture)));
    }

    private static GesSchemaGenerateOptions Options(GesSchemaLanguage language, string space, bool bindings = false)
        => new() { Language = language, Namespace = space, Access = GesSchemaAccess.Internal, Values = true, Bindings = bindings };

    private static string Generated(string name) => File.ReadAllText(Path.Combine(TestRepositoryPaths.CSharpDirectory, "GameEventScript.Generator", "tests", "Generated", name));

    [TestMethod]
    public void CheckedInCodeIsCurrent()
    {
        var set = Set();
        var swift = Path.Combine(TestRepositoryPaths.Root, "implementation", "swift", "GameEventScriptConformance", "Tests", "GameEventScriptConformanceTests", "Generated");
        var wire = GesSchemaGenerator.WireRoots(set, "spatial");
        var model = GesSchemaGenerator.ModelRoots(set, "spatial");
        var parameters = new GesSchemaParameters();
        Assert.AreEqual(Generated("SpatialWire.g.cs"), GesSchemaGenerator.Generate(set, wire, parameters, Options(GesSchemaLanguage.CSharp, "GameEventScript.Tests.Generator.Spatial.Wire")));
        Assert.AreEqual(File.ReadAllText(Path.Combine(swift, "SpatialWire.swift")), GesSchemaGenerator.Generate(set, wire, parameters, Options(GesSchemaLanguage.Swift, "SpatialWire")));
        Assert.AreEqual(Generated("SpatialModel.g.cs"), GesSchemaGenerator.GenerateModels(set, model, Options(GesSchemaLanguage.CSharp, "GameEventScript.Tests.Generator.Spatial.Model", bindings: true)));
        Assert.AreEqual(File.ReadAllText(Path.Combine(swift, "SpatialModel.swift")), GesSchemaGenerator.GenerateModels(set, model, Options(GesSchemaLanguage.Swift, "SpatialModel", bindings: true)));
    }

    [TestMethod]
    public void SpatialFieldsUseTheDynamicFormOnRuntimeTypes()
    {
        var code = Generated("SpatialWire.g.cs");
        string[] lines =
        [
            "public GesVector Size => GesComputed.RequiredVector(__Size());",
            "internal GesBox? __Bounds() => GesComputed.Box(this.Origin, GesComputed.MovePoint(this.Origin, this.__Size()));",
            "internal GesComputed.Value __Diagonal() => GesComputed.Length(this.__Size());",
            "public GesPoint? Shifted => __Shifted();",
            "internal GesPoint? __Moved() => (GesComputed.MovePoint(this.Origin, (this.HasDrift ? (GesVector?)this.Drift : null)) ?? this.Origin);",
            "public GesPoly Fence => GesComputed.RequiredPoly(__Fence());"
        ];
        foreach (var line in lines) StringAssert.Contains(code, line);
    }

    private static Wire.Ground Ground(long columns, long rows, double spacing, GesPoint origin, GesVector? drift, params (double X, double Y)[] posts)
    {
        var ground = new Wire.Ground
        {
            Arena = new Wire.Arena { Columns = columns, Rows = rows, Spacing = spacing },
            Origin = origin,
            DriftOptional = drift
        };
        foreach (var (x, y) in posts)
        {
            ground.Posts.Add(new Wire.Post { X = x, Y = y });
            ground.Marks.Add(new GesPoint(y, x));
        }

        return ground;
    }

    private static GesValue Field(GesValue record, string name) => record.AsMap()!.Get(name) ?? throw new AssertFailedException($"The record has no field {name}.");

    private static void AssertBits(double expected, double actual, string name)
        => Assert.AreEqual(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual), $"{name}: expected {expected}, got {actual}");

    private static void AssertPoint(GesPoint expected, GesPoint actual, string name)
    {
        AssertBits(expected.X, actual.X, name + ".x");
        AssertBits(expected.Y, actual.Y, name + ".y");
        AssertBits(expected.Z, actual.Z, name + ".z");
    }

    private static void AssertVector(GesValue expected, GesVector actual, string name)
    {
        if (expected.IsNothing)
        {
            Assert.IsTrue(double.IsNaN(actual.X), $"{name}: expected nothing, got {actual}");
            return;
        }

        var vector = GesVector.FromValue(expected, GesQuantityUnit.UnitMeter) ?? throw new AssertFailedException($"{name} is no vector in metres.");
        AssertPoint(new GesPoint(vector.X, vector.Y, vector.Z), new GesPoint(actual.X, actual.Y, actual.Z), name);
    }

    private static void AssertPoint(GesValue expected, GesPoint? actual, string name)
    {
        if (expected.IsNothing)
        {
            Assert.IsTrue(actual is null || double.IsNaN(actual.Value.X), $"{name}: expected nothing, got {actual}");
            return;
        }

        AssertPoint(GesPoint.FromValue(expected, GesQuantityUnit.UnitMeter) ?? throw new AssertFailedException($"{name} is no point in metres."), actual!.Value, name);
    }

    private static void AssertNumber(GesValue expected, double actual, string name)
    {
        if (expected.IsNothing) Assert.IsTrue(double.IsNaN(actual), $"{name}: expected nothing, got {actual}");
        else AssertBits(expected.AsNumber(), actual, name);
    }

    [TestMethod]
    public void SpatialFieldsGiveTheBitsOfTheValuePath()
    {
        Wire.Ground[] grounds =
        [
            Ground(4, 5, 1.5, new GesPoint(1, -2), new GesVector(0.5, 0), (5, 6), (7, 8)),
            Ground(1, 1000, 0.01, new GesPoint(-100, 100), null),
            Ground(1000, 3, 9.99, new GesPoint(0, 0), new GesVector(-10, 10), (0, 0), (100, 100), (3, 4))
        ];
        foreach (var ground in grounds)
        {
            var value = ground.ToValue();
            AssertVector(Field(value, "size"), ground.Size, "size");
            AssertVector(Field(value, "half"), ground.Half, "half");
            AssertVector(Field(value, "back"), ground.Back, "back");
            AssertVector(Field(value, "span"), ground.Span, "span");
            AssertVector(Field(value, "pick"), ground.Pick, "pick");
            AssertPoint(Field(value, "middle"), ground.Middle, "middle");
            AssertPoint(Field(value, "shifted"), ground.Shifted, "shifted");
            AssertPoint(Field(value, "moved"), ground.Moved, "moved");
            AssertNumber(Field(value, "area"), ground.Area, "area");
            AssertNumber(Field(value, "diagonal"), ground.Diagonal, "diagonal");
            AssertNumber(Field(value, "reach"), ground.Reach, "reach");
            AssertNumber(Field(value, "corners"), ground.Corners, "corners");
            Assert.AreEqual(Field(value, "drifting").AsBoolean(), ground.Drifting, "drifting");
            foreach (var (name, box) in new[] { ("bounds", ground.Bounds), ("around", ground.Around) })
            {
                var expected = GesBox.FromValue(Field(value, name), GesQuantityUnit.UnitMeter) ?? throw new AssertFailedException($"{name} is no box in metres.");
                AssertPoint(expected.Min, box.Min, name + ".lower");
                AssertPoint(expected.Max, box.Max, name + ".upper");
            }

            foreach (var (name, disc) in new[] { ("zone", ground.Zone), ("ring", ground.Ring) })
            {
                var expected = GesDisc.FromValue(Field(value, name), GesQuantityUnit.UnitMeter) ?? throw new AssertFailedException($"{name} is no disc in metres.");
                AssertPoint(expected.Center, disc.Center, name + ".center");
                AssertBits(expected.Radius, disc.Radius, name + ".radius");
            }

            foreach (var (name, poly) in new[] { ("outline", ground.Outline), ("path", ground.Path), ("fence", ground.Fence) })
            {
                var expected = GesPoly.FromValue(Field(value, name), GesQuantityUnit.UnitMeter) ?? throw new AssertFailedException($"{name} is no polygon in metres.");
                Assert.AreEqual(expected.Points.Count, poly.Points.Count, name + " points");
                for (var index = 0; index < poly.Points.Count; index++) AssertPoint(expected.Points[index], poly.Points[index], $"{name}[{index}]");
            }

            // A model computes the same values from the same fields; geometry values compare by their parts, as external
            // values of a record compare by identity.
            var model = Model.Ground.FromValue(value);
            AssertVector(Field(value, "size"), model.Size, "model size");
            AssertPoint(ground.Bounds.Max, model.Bounds.Max, "model bounds");
            AssertBits(ground.Zone.Radius, model.Zone.Radius, "model zone");
            Assert.AreEqual(ground.Fence.Points.Count, model.Fence.Points.Count, "model fence");
            Assert.AreEqual(Field(value, "drifting").AsBoolean(), Field(model.ToValue(), "drifting").AsBoolean(), "model drifting");
        }
    }
}
