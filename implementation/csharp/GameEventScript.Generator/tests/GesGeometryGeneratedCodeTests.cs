// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Generator;
using Model = GameEventScript.Tests.Generator.Geometry.Model;
using Wire = GameEventScript.Tests.Generator.Geometry.Wire;

namespace GameEventScript.Tests.Generator;

// The checked-in code of conformance/fixtures/SchemaGeneration/geometry.gess: Boxes and Polys in wire records, where a Poly is
// a reusable list of Points with a polygon view, and in models, which hold GesBox and GesPoly.
[TestClass]
public sealed class GesGeometryGeneratedCodeTests
{
    private static GesSchemaSet Set()
    {
        var fixture = Path.Combine(TestRepositoryPaths.ConformanceDirectory, "fixtures", "SchemaGeneration", "geometry.gess");
        return GesSchemaSet.Parse(new GesSchemaSource("geometry.gess", File.ReadAllText(fixture)));
    }

    private static GesSchemaGenerateOptions Options(GesSchemaLanguage language, string space, bool model) => new() { Language = language, Namespace = space, Access = GesSchemaAccess.Internal, Values = true, Bindings = model, Documents = model };

    private static string Generated(string name) => File.ReadAllText(Path.Combine(TestRepositoryPaths.CSharpDirectory, "GameEventScript.Generator", "tests", "Generated", name));

    [TestMethod]
    public void CheckedInCodeIsCurrent()
    {
        var set = Set();
        var swift = Path.Combine(TestRepositoryPaths.Root, "implementation", "swift", "GameEventScriptConformance", "Tests", "GameEventScriptConformanceTests", "Generated");
        var wire = GesSchemaGenerator.WireRoots(set, "geometry");
        var model = GesSchemaGenerator.ModelRoots(set, "geometry");
        var parameters = new GesSchemaParameters();
        Assert.AreEqual(Generated("GeometryWire.g.cs"), GesSchemaGenerator.Generate(set, wire, parameters, Options(GesSchemaLanguage.CSharp, "GameEventScript.Tests.Generator.Geometry.Wire", false)));
        Assert.AreEqual(File.ReadAllText(Path.Combine(swift, "GeometryWire.swift")), GesSchemaGenerator.Generate(set, wire, parameters, Options(GesSchemaLanguage.Swift, "GeometryWire", false)));
        Assert.AreEqual(Generated("GeometryModel.g.cs"), GesSchemaGenerator.GenerateModels(set, model, Options(GesSchemaLanguage.CSharp, "GameEventScript.Tests.Generator.Geometry.Model", true)));
        Assert.AreEqual(File.ReadAllText(Path.Combine(swift, "GeometryModel.swift")), GesSchemaGenerator.GenerateModels(set, model, Options(GesSchemaLanguage.Swift, "GeometryModel", true)));
    }

    private static Wire.Zone Zone()
    {
        var zone = new Wire.Zone
        {
            Id = 7,
            Area = new GesBox(new GesPoint(20, 3), new GesPoint(5, 1)),
            BoundsOptional = new GesBox(new GesPoint(-1.5, 0, 2), new GesPoint(4, 8, -2)),
            Pick = Wire.Zone.PickAlternative.Box,
            PickBox = new GesBox(new GesPoint(0, 0), new GesPoint(1, 2))
        };
        zone.Outline.AddRange([new GesPoint(0, 0), new GesPoint(10, 0), new GesPoint(0, 2)]);
        zone.Free.AddRange([new GesPoint(0.5, -0.25), new GesPoint(3, 1)]);
        var ring = new Wire.Zone.RingsItem();
        ring.Value.AddRange([new GesPoint(1, 1), new GesPoint(2, 2)]);
        zone.Rings.Add(ring);
        return zone;
    }

    [TestMethod]
    public void BoxesAndPolysRoundTripThroughBytesAndValues()
    {
        var zone = Zone();
        Assert.AreEqual(new GesPoint(5, 1), zone.Area.Min, "A box orders its corners.");
        var bytes = zone.ToBytes();
        var read = new Wire.Zone();
        read.Read(bytes);
        Assert.IsTrue(read.WireEquals(zone));
        Assert.AreEqual(zone.Area, read.Area);
        Assert.AreEqual(zone.BoundsOptional, read.BoundsOptional);
        Assert.AreEqual(zone.PickBox, read.PickBox);
        CollectionAssert.AreEqual(zone.Outline, read.Outline);
        Assert.AreEqual(new GesPoly(zone.Outline), read.OutlinePoly);
        Assert.AreEqual(10d, read.OutlinePoly.Area!.Value, "The view is a polygon of the points.");

        // The value path holds the geometry library's external values, which the wire record and the model convert alike.
        var value = read.ToValue();
        var fields = value.AsMap()!;
        Assert.AreEqual(zone.Area, GesBox.FromValue(fields.Get("area")!.Value, GesQuantityUnit.UnitMeter));
        Assert.AreEqual(new GesPoly(zone.Free), GesPoly.FromValue(fields.Get("free")!.Value));
        var copy = new Wire.Zone();
        copy.ReadValue(value);
        CollectionAssert.AreEqual(bytes, copy.ToBytes());

        var model = Model.Zone.FromValue(value);
        Assert.AreEqual(zone.Area, model.Area);
        Assert.AreEqual(zone.BoundsOptional, model.Bounds);
        Assert.AreEqual(new GesPoly(zone.Outline), model.Outline);
        Assert.AreEqual(new GesPoly(zone.Rings[0].Value), model.Rings[0]);
        Assert.AreEqual(value.ToJson(), model.ToValue().ToJson());
    }

    [TestMethod]
    public void BoxDeltasChangeOneCorner()
    {
        var baseline = Zone();
        var next = Zone();
        next.Area = new GesBox(next.Area.Min, new GesPoint(22, 3));
        var delta = next.ToDeltaBytes(baseline);
        var read = new Wire.Zone();
        read.Read(baseline.ToBytes());
        read.ReadDelta(delta, baseline);
        Assert.AreEqual(next.Area, read.Area);
        Assert.IsTrue(read.WireEquals(next));
    }

    private static Wire.Reach Reach()
    {
        var reach = new Wire.Reach
        {
            Zone = new GesDisc(new GesPoint(20, 3), -5),
            HasFree = true,
            Free = new GesDisc(new GesPoint(0.5, -0.25), 1.5),
            Aura = new GesDisc(2.5),
            Mark = new GesDisc(new GesPoint(9, 0), 0),
            Pick = Wire.Reach.PickAlternative.Disc,
            PickDisc = new GesDisc(new GesPoint(1, 2), 3)
        };
        reach.Rings.Add(new GesDisc(2));
        return reach;
    }

    [TestMethod]
    public void DiscsRoundTripThroughBytesAndValues()
    {
        var reach = Reach();
        Assert.AreEqual(5d, reach.Zone.Radius, "A disc takes the radius's magnitude.");
        var bytes = reach.ToBytes();
        var read = new Wire.Reach();
        read.Read(bytes);
        Assert.IsTrue(read.WireEquals(reach));
        Assert.AreEqual(reach.Zone, read.Zone);
        Assert.AreEqual(reach.Free, read.Free);
        Assert.AreEqual(reach.Aura, read.Aura);
        Assert.AreEqual(reach.Mark, read.Mark);
        Assert.AreEqual(reach.Rings[0], read.Rings[0]);

        // The value path holds the geometry library's external values, which the wire record and the model convert alike.
        var value = read.ToValue();
        Assert.AreEqual(reach.Zone, GesDisc.FromValue(value.AsMap()!.Get("zone")!.Value, GesQuantityUnit.UnitMeter));
        var copy = new Wire.Reach();
        copy.ReadValue(value);
        CollectionAssert.AreEqual(bytes, copy.ToBytes());
        var model = Model.Reach.FromValue(value);
        Assert.AreEqual(reach.Zone, model.Zone);
        Assert.AreEqual(reach.Free, model.Free);
        Assert.AreEqual(value.ToJson(), model.ToValue().ToJson());

        // A block without the radius transmits none, so only a disc of radius 0 can be written.
        reach.Mark = new GesDisc(new GesPoint(9, 0), 1);
        Assert.AreEqual(GesWireException.TypeMismatch, Assert.ThrowsExactly<GesWireException>(() => reach.ToBytes()).Code);
    }

    [TestMethod]
    public void DiscDeltasChangeTheRadiusAlone()
    {
        var baseline = Reach();
        var next = Reach();
        next.Zone = new GesDisc(next.Zone.Center, 6);
        next.Aura = new GesDisc(2.75);
        var delta = next.ToDeltaBytes(baseline);
        var read = new Wire.Reach();
        read.Read(baseline.ToBytes());
        read.ReadDelta(delta, baseline);
        Assert.AreEqual(next.Zone, read.Zone);
        Assert.AreEqual(next.Aura, read.Aura);
        Assert.IsTrue(read.WireEquals(next));
        var quantized = Reach();
        quantized.Aura = new GesDisc(2.504);
        quantized.Quantize();
        Assert.AreEqual(new GesDisc(2.5), quantized.Aura);
    }

    private const string ZoneDocument = """
        :Schema(:Zone)
        id: 7
        area: [lower: :Point(20m, 1m), upper: :Point(5m, 3m)]
        outline: [:Point(0m, 0m), :Point(10m, 0m), :Point(0m, 2m)]
        free: [:Point(0.5, -0.25)]
        rings: [[:Point(1m, 1m), :Point(2m, 2m)]]

        """;

    [TestMethod]
    public void ModelsReadTheirDocuments()
    {
        var zone = Model.Zone.FromDocument(ZoneDocument);
        Assert.AreEqual(7L, zone.Id);
        Assert.AreEqual(new GesBox(new GesPoint(5, 1), new GesPoint(20, 3)), zone.Area);
        Assert.IsNull(zone.Bounds);
        Assert.AreEqual(10d, zone.Outline.Area);
        Assert.AreEqual(new GesPoly([new GesPoint(1, 1), new GesPoint(2, 2)]), zone.Rings[0]);

        // The document type chooses the root by the header.
        var document = Model.Document.FromDocument(ZoneDocument);
        Assert.AreEqual(zone, ((Model.Document.Zone)document).Value);
    }

    [TestMethod]
    public void DocumentErrorsHaveTheirPositionAndPath()
    {
        var other = Assert.ThrowsExactly<GesDocumentException>(() => Model.Zone.FromDocument(ZoneDocument.Replace(":Schema(:Zone)", ":Schema(:Area)")));
        Assert.AreEqual((GesSchemaDocument.SchemaMismatch, 1, 1, ""), (other.Code, other.Line, other.Column, other.Path));
        StringAssert.Contains(other.Message, ":Area");
        StringAssert.Contains(other.Message, ":Zone");
        var range = Assert.ThrowsExactly<GesDocumentException>(() => Model.Document.FromDocument(ZoneDocument.Replace("id: 7", "id: 300")));
        Assert.AreEqual(("document.outOfRange", 2, 5, "id"), (range.Code, range.Line, range.Column, range.Path));
        var unit = Assert.ThrowsExactly<GesDocumentException>(() => Model.Zone.FromDocument(ZoneDocument.Replace("[:Point(1m, 1m),", "[:Point(1s, 1s),")));
        Assert.AreEqual(("document.fieldType", 6, 10, "rings[][]"), (unit.Code, unit.Line, unit.Column, unit.Path));
    }

    [TestMethod]
    public void PolyPointsOutsideThePlaneOrBeyondMaxCannotBeWritten()
    {
        var zone = Zone();
        zone.Free[0] = new GesPoint(0, 0, 1);
        var mismatch = Assert.ThrowsExactly<GesWireException>(() => zone.ToBytes());
        Assert.AreEqual((GesWireException.TypeMismatch, "free[]"), (mismatch.Code, mismatch.Path));
        zone = Zone();
        zone.Outline.Add(new GesPoint(1, 1));
        var tooLong = Assert.ThrowsExactly<GesWireException>(() => zone.ToBytes());
        Assert.AreEqual((GesWireException.TooLong, "outline"), (tooLong.Code, tooLong.Path));
    }
}
