// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Tests.Native.Api;

/// <summary>Checks the native spatial and shape values; the Swift tests use the same inputs and results.</summary>
[TestClass]
public sealed class GesGeometryTests
{
    [TestMethod]
    public void PointsAndVectorsFollowTheLanguage()
    {
        var start = new GesPoint(1, 2);
        var end = new GesPoint(4, 6);
        var step = end - start;
        Assert.AreEqual(new GesVector(3, 4), step);
        Assert.AreEqual(5d, step.Length);
        Assert.AreEqual(25d, step.LengthSquared);
        Assert.AreEqual(5d, start.DistanceTo(end));
        Assert.AreEqual(end, start + step);
        Assert.AreEqual(start, end - step);
        Assert.AreEqual(new GesVector(0.6, 0.8), step.Normalized);
        Assert.IsNull(GesVector.Zero.Normalized);
        Assert.AreEqual(new GesVector(6, 8), step * 2);
        Assert.AreEqual(new GesVector(-3, -4), -step);
        Assert.AreEqual(new GesVector(1.5, 2), step / 2);
        Assert.AreEqual(11d, step.Dot(new GesVector(1, 2)));
        Assert.AreEqual(new GesVector(0, 0, 2), step.Cross(new GesVector(1, 2)));
        Assert.AreEqual(new GesPoint(0, -0d), GesPoint.Zero);
        Assert.AreEqual(new GesPoint(0, -0d).GetHashCode(), GesPoint.Zero.GetHashCode());
        Assert.IsFalse(new GesVector(double.NaN, 0).IsFinite);
    }

    [TestMethod]
    public void EqualityIsBinary64Equality()
    {
        Assert.IsTrue(new GesPoint(0, 1, 2) == new GesPoint(-0d, 1, 2));
        Assert.AreEqual(new GesPoint(0, 1, 2).GetHashCode(), new GesPoint(-0d, 1, 2).GetHashCode());
        Assert.IsFalse(new GesPoint(double.NaN, 0) == new GesPoint(double.NaN, 0), "A NaN component is equal to nothing, as in Swift.");
        Assert.IsFalse(new GesVector(0, double.NaN).Equals(new GesVector(0, double.NaN)));
        Assert.IsFalse(new GesBox(new GesPoint(double.NaN, 0), new GesPoint(1, 1)) == new GesBox(new GesPoint(double.NaN, 0), new GesPoint(1, 1)));
    }

    [TestMethod]
    public void ValuesCarryAnExplicitUnit()
    {
        var value = new GesPoint(1, 2, 3).ToValue(GesQuantityUnit.UnitMeter);
        Assert.AreEqual(GesBytecodeTypeKind.Point, value.ValueKind);
        Assert.AreEqual(GesQuantityUnit.UnitMeter, value.ValueUnit);
        Assert.AreEqual(new GesPoint(1, 2, 3), GesPoint.FromValue(value, GesQuantityUnit.UnitMeter));
        Assert.IsNull(GesPoint.FromValue(value), "Units never convert.");
        Assert.IsNull(GesVector.FromValue(value, GesQuantityUnit.UnitMeter), "A Point is no Vector.");
        Assert.IsTrue(new GesVector(double.NaN, 0).ToValue().IsNothing);
        var box = new GesBox(new GesPoint(0, 0), new GesPoint(2, 2));
        Assert.AreEqual(box, GesBox.FromValue(box.ToValue(GesQuantityUnit.UnitMeter), GesQuantityUnit.UnitMeter));
        Assert.IsNull(GesBox.FromValue(box.ToValue(GesQuantityUnit.UnitMeter)));
        var poly = new GesPoly([new GesPoint(0, 0), new GesPoint(1, 0), new GesPoint(0, 1)]);
        Assert.AreEqual(poly, GesPoly.FromValue(poly.ToValue()));
    }

    [TestMethod]
    public void BoxesOrderTheirCorners()
    {
        var box = new GesBox(new GesPoint(10, 0), new GesPoint(0, 4));
        Assert.AreEqual(new GesPoint(0, 0), box.Min);
        Assert.AreEqual(new GesPoint(10, 4), box.Max);
        Assert.IsTrue(box.Is2D);
        Assert.AreEqual(new GesVector(10, 4), box.Size);
        Assert.AreEqual(new GesPoint(5, 2), box.Center);
        Assert.IsTrue(box.Contains(new GesPoint(5, 4)));
        Assert.IsFalse(box.Contains(new GesPoint(5, 5)));
        Assert.IsFalse(box.Contains(new GesPoint(1, 1, 1)));
        var other = new GesBox(new GesPoint(8, 2), new GesPoint(12, 6));
        Assert.IsTrue(box.Intersects(other));
        Assert.AreEqual(new GesBox(new GesPoint(0, 0), new GesPoint(12, 6)), box.Union(other));
        Assert.AreEqual(new GesBox(new GesPoint(1, 1), new GesPoint(9, 3)), box.Inset(1));
        Assert.AreEqual(new GesBox(new GesPoint(-1, -1), new GesPoint(11, 5)), box.Inset(-1));
        Assert.IsNull(box.Inset(3));
        Assert.IsNull(box.Inset(double.NaN));
        Assert.AreEqual(new GesPoint(10, 0), box.Clamp(new GesPoint(12, -1)));
        var far = GesBox.FromCenter(new GesPoint(20, 20), new GesVector(2, -4));
        Assert.AreEqual(new GesPoint(19, 18), far.Min);
        Assert.AreEqual(new GesPoint(21, 22), far.Max);
        Assert.IsFalse(box.Intersects(far));
        var deep = new GesBox(new GesPoint(0, 0, 0), new GesPoint(4, 4, 4));
        Assert.AreEqual(new GesPoint(3, 3, 3), deep.Inset(1)!.Value.Max);
        Assert.AreEqual(new GesPoint(4, 0, 2), deep.Clamp(new GesPoint(5, -1, 2)));
    }

    [TestMethod]
    public void CompoundAssignmentsEqualTheirBinaryForms()
    {
        var start = new GesPoint(1, 2, 3);
        var step = new GesVector(4, -6, 0.5);
        var point = start;
        point += step;
        Assert.AreEqual(start + step, point);
        point -= step;
        Assert.AreEqual(start + step - step, point);
        var vector = step;
        vector += new GesVector(1, 1, 1);
        Assert.AreEqual(step + new GesVector(1, 1, 1), vector);
        vector -= new GesVector(1, 1, 1);
        Assert.AreEqual(step + new GesVector(1, 1, 1) - new GesVector(1, 1, 1), vector);
        vector *= 2.0;
        Assert.AreEqual(step * 2.0, vector);
        vector /= 2.0;
        Assert.AreEqual(step * 2.0 / 2.0, vector);
    }

    [TestMethod]
    public void BoxesExposeComponentBoundsAndASize()
    {
        var box = new GesBox(new GesPoint(10, -2, 3), new GesPoint(0, 4, -1));
        Assert.AreEqual(0, box.MinX);
        Assert.AreEqual(10, box.MaxX);
        Assert.AreEqual(-2, box.MinY);
        Assert.AreEqual(4, box.MaxY);
        Assert.AreEqual(-1, box.MinZ);
        Assert.AreEqual(3, box.MaxZ);
        var arena = GesBox.FromSize(new GesVector(70, 50));
        Assert.AreEqual(new GesBox(GesPoint.Zero, new GesPoint(70, 50)), arena);
        Assert.AreEqual(new GesVector(70, 50), arena.Size);
        var negative = GesBox.FromSize(new GesVector(-3, 2));
        Assert.AreEqual(new GesPoint(-3, 0), negative.Min);
        Assert.AreEqual(new GesPoint(0, 2), negative.Max);
    }

    [TestMethod]
    public void PolygonsMeasureTheirArea()
    {
        var square = new GesPoly([new GesPoint(0, 0), new GesPoint(4, 0), new GesPoint(4, 4), new GesPoint(0, 4)]);
        Assert.IsTrue(square.IsValid);
        Assert.AreEqual(16d, square.Area);
        Assert.AreEqual(-16d, new GesPoly([new GesPoint(0, 0), new GesPoint(0, 4), new GesPoint(4, 4), new GesPoint(4, 0)]).Area);
        Assert.AreEqual(new GesPoint(2, 2), square.Centroid);
        Assert.AreEqual(new GesBox(new GesPoint(0, 0), new GesPoint(4, 4)), square.Bounds);
        Assert.IsTrue(square.Contains(new GesPoint(2, 2)));
        Assert.IsTrue(square.Contains(new GesPoint(4, 2)), "The outline counts as inside.");
        Assert.IsFalse(square.Contains(new GesPoint(5, 5)));
        Assert.AreEqual(1d, square.DistanceTo(new GesPoint(2, 1)));
        Assert.AreEqual(5d, square.DistanceTo(new GesPoint(7, 8)));
        Assert.IsTrue(square.Intersects(new GesPoly([new GesPoint(3, 3), new GesPoint(6, 3), new GesPoint(6, 6)])));
        Assert.IsTrue(square.Intersects(new GesPoly([new GesPoint(1, 1), new GesPoint(2, 1), new GesPoint(2, 2)])), "A polygon inside another intersects it.");
        Assert.IsFalse(square.Intersects(new GesPoly([new GesPoint(10, 10), new GesPoint(12, 10), new GesPoint(11, 12)])));
        var triangle = new GesPoly([new GesPoint(0, 0), new GesPoint(3, 0), new GesPoint(0, 3)]);
        Assert.AreEqual(4.5, triangle.Area);
        Assert.AreEqual(new GesPoint(1, 1), triangle.Centroid);
    }

    [TestMethod]
    public void InvalidPolygonsGiveNothing()
    {
        GesPoint[][] invalid =
        [
            [new GesPoint(0, 0), new GesPoint(4, 0)],
            [new GesPoint(0, 0), new GesPoint(4, 4), new GesPoint(4, 0), new GesPoint(0, 4)],
            [new GesPoint(0, 0), new GesPoint(4, 0), new GesPoint(4, 0), new GesPoint(0, 4)],
            [new GesPoint(0, 0), new GesPoint(2, 0), new GesPoint(4, 0)],
            [new GesPoint(0, 0), new GesPoint(4, 0), new GesPoint(2, 0), new GesPoint(2, 3)],
            [new GesPoint(0, 0, 1), new GesPoint(4, 0, 1), new GesPoint(0, 4, 1)],
            [new GesPoint(0, 0), new GesPoint(double.NaN, 0), new GesPoint(0, 4)]
        ];
        foreach (var points in invalid)
        {
            var poly = new GesPoly(points);
            Assert.IsFalse(poly.IsValid, poly.ToString());
            Assert.IsNull(poly.Area);
            Assert.IsNull(poly.Bounds);
            Assert.IsNull(poly.Centroid);
            Assert.IsNull(poly.Contains(new GesPoint(1, 1)));
            Assert.IsNull(poly.DistanceTo(new GesPoint(1, 1)));
        }
    }

    [TestMethod]
    public void DiscsTakeTheMagnitudeOfTheirRadius()
    {
        var disc = new GesDisc(new GesPoint(2, 3), -5);
        Assert.AreEqual(5d, disc.Radius);
        Assert.AreEqual(new GesDisc(GesPoint.Zero, 2), new GesDisc(2));
        Assert.AreEqual(new GesBox(new GesPoint(-3, -2), new GesPoint(7, 8)), disc.Bounds);
        Assert.IsTrue(disc.Contains(new GesPoint(5, 7))!.Value);
        Assert.IsFalse(disc.Contains(new GesPoint(6, 7))!.Value);
        Assert.AreEqual(2d, disc.DistanceTo(new GesPoint(2, 0)));
        Assert.AreEqual(new GesPoint(2, 8), disc.Clamp(new GesPoint(2, 13)));
        Assert.AreEqual(new GesDisc(new GesPoint(2, 3), 4), disc.Inset(1));
        Assert.IsNull(disc.Inset(6));
        Assert.IsTrue(disc.Intersects(new GesDisc(new GesPoint(12, 3), 5))!.Value);
        Assert.IsTrue(new GesDisc(2).Intersects(new GesBox(new GesPoint(1, 1), new GesPoint(4, 4)))!.Value);
        Assert.IsNull(new GesDisc(2).Intersects(new GesBox(GesPoint.Zero, new GesPoint(1, 1, 1))));
        Assert.IsTrue(new GesDisc(2).Intersects(new GesPoly([new GesPoint(2, -1), new GesPoint(6, -1), new GesPoint(6, 1), new GesPoint(2, 1)]))!.Value);
        var lifted = new GesDisc(new GesPoint(0, 0, 1), 1);
        Assert.IsFalse(lifted.IsValid);
        Assert.IsNull(lifted.Area);
        Assert.AreEqual(disc, GesDisc.FromValue(disc.ToValue(GesQuantityUnit.UnitMeter), GesQuantityUnit.UnitMeter));
        Assert.IsNull(GesDisc.FromValue(disc.ToValue(GesQuantityUnit.UnitMeter)));
    }
}
