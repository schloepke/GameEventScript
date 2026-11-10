// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Api;

/// <summary>
/// A closed polygon in the plane z = 0, without a unit: its points in order, the last connecting to the first. A polygon is
/// valid with at least three finite points in the plane, no edge of zero length, no two edges that cross or overlap, and an
/// area other than zero; the operations of an invalid one give null, as GES operations give <c>nothing</c> for a domain
/// failure. Scripts see it as the external type <c>:Poly</c> of <see cref="GesGeometryLibrary"/>.
/// </summary>
public sealed class GesPoly : IEquatable<GesPoly>
{
    private readonly GesPoint[] _points;

    /// <summary>Creates a polygon from its points, which are copied; validity is checked once.</summary>
    /// <param name="points">The points in order.</param>
    public GesPoly(IEnumerable<GesPoint> points)
    {
        if (points is null) throw new ArgumentNullException(nameof(points));
        _points = new List<GesPoint>(points).ToArray();
        Points = new GesReadOnlyArray<GesPoint>(_points);
        IsValid = Valid(_points);
    }

    /// <summary>Gets the points in order.</summary>
    public GesReadOnlyArray<GesPoint> Points { get; }

    /// <summary>Gets whether the polygon is a valid simple polygon with an area.</summary>
    public bool IsValid { get; }

    /// <summary>Gets the signed area: positive when the points run counterclockwise, negative when clockwise.</summary>
    public double? Area => IsValid ? SignedArea(_points) : null;

    /// <summary>Gets the smallest box that holds every point.</summary>
    public GesBox? Bounds
    {
        get
        {
            if (!IsValid) return null;
            var min = _points[0];
            var max = _points[0];
            foreach (var point in _points)
            {
                min = new GesPoint(GesBox.Lower(min.X, point.X), GesBox.Lower(min.Y, point.Y));
                max = new GesPoint(GesBox.Upper(max.X, point.X), GesBox.Upper(max.Y, point.Y));
            }

            return new GesBox(min, max);
        }
    }

    /// <summary>Gets the centre of the area.</summary>
    public GesPoint? Centroid
    {
        get
        {
            if (!IsValid) return null;
            double x = 0, y = 0, twice = 0;
            for (var index = 0; index < _points.Length; index++)
            {
                var a = _points[index];
                var b = _points[(index + 1) % _points.Length];
                var cross = (a.X * b.Y) - (b.X * a.Y);
                twice += cross;
                x += (a.X + b.X) * cross;
                y += (a.Y + b.Y) * cross;
            }

            var divisor = twice * 3;
            return new GesPoint(x / divisor, y / divisor);
        }
    }

    /// <summary>Tests whether a point lies in the polygon, its outline included.</summary>
    /// <param name="point">The point.</param>
    /// <returns>Whether the polygon holds the point, or null when the polygon is invalid or the point not finite.</returns>
    public bool? Contains(GesPoint point)
    {
        if (!IsValid || !point.IsFinite) return null;
        if (point.Z != 0) return false;
        var inside = false;
        for (var index = 0; index < _points.Length; index++)
        {
            var a = _points[index];
            var b = _points[(index + 1) % _points.Length];
            if (OnSegment(a, b, point)) return true;
            if ((a.Y > point.Y) != (b.Y > point.Y) && point.X < a.X + ((point.Y - a.Y) / (b.Y - a.Y) * (b.X - a.X))) inside = !inside;
        }

        return inside;
    }

    /// <summary>Computes the distance from a point to the outline, zero on it, for a point inside as well as outside.</summary>
    /// <param name="point">The point.</param>
    /// <returns>The distance, or null when the polygon is invalid or the point not finite.</returns>
    public double? DistanceTo(GesPoint point)
    {
        if (!IsValid || !point.IsFinite) return null;
        var best = double.PositiveInfinity;
        for (var index = 0; index < _points.Length; index++)
        {
            var distance = SegmentDistance(_points[index], _points[(index + 1) % _points.Length], point);
            if (distance < best) best = distance;
        }

        return best;
    }

    /// <summary>Tests whether two polygons share a point: crossing or touching outlines, or one inside the other.</summary>
    /// <param name="other">The other polygon.</param>
    /// <returns>Whether they intersect, or null when either is invalid.</returns>
    public bool? Intersects(GesPoly other)
    {
        if (other is null) throw new ArgumentNullException(nameof(other));
        if (!IsValid || !other.IsValid) return null;
        for (var index = 0; index < _points.Length; index++)
        {
            var a = _points[index];
            var b = _points[(index + 1) % _points.Length];
            for (var next = 0; next < other._points.Length; next++)
                if (SegmentsIntersect(a, b, other._points[next], other._points[(next + 1) % other._points.Length])) return true;
        }

        return Contains(other._points[0]) == true || other.Contains(_points[0]) == true;
    }

    /// <summary>Converts the polygon to the external value <c>:Poly</c> of <see cref="GesGeometryLibrary"/>.</summary>
    /// <param name="unit">The unit of its points.</param>
    /// <returns>The GES value.</returns>
    public GesValue ToValue(GesQuantityUnit unit = GesQuantityUnit.UnitNone) => GesValue.GesExternal(new GesGeometryLibrary.PolyValue(this, unit));

    /// <summary>Reads a <c>:Poly</c> of exactly the given unit. Units never convert.</summary>
    /// <param name="value">The value.</param>
    /// <param name="unit">The expected unit.</param>
    /// <returns>The polygon, or null when the value is no <c>:Poly</c> of <see cref="GesGeometryLibrary"/> or has another unit.</returns>
    public static GesPoly? FromValue(in GesValue value, GesQuantityUnit unit = GesQuantityUnit.UnitNone)
        => value.ExternalValue is GesGeometryLibrary.PolyValue poly && poly.Unit == unit ? poly.Poly : null;

    /// <summary>Compares the points in order.</summary>
    /// <param name="other">The other polygon.</param>
    /// <returns>Whether both hold equal points in the same order.</returns>
    public bool Equals(GesPoly? other)
    {
        if (other is null || other._points.Length != _points.Length) return false;
        for (var index = 0; index < _points.Length; index++)
            if (!_points[index].Equals(other._points[index])) return false;
        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is GesPoly other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 19;
            foreach (var point in _points) hash = (hash * 31) + point.GetHashCode();
            return hash;
        }
    }

    /// <summary>Compares two polygons by their points.</summary>
    /// <param name="left">The first polygon.</param>
    /// <param name="right">The second polygon.</param>
    /// <returns>Whether both are null or equal.</returns>
    public static bool operator ==(GesPoly? left, GesPoly? right) => left is null ? right is null : left.Equals(right);

    /// <summary>Compares two polygons by their points.</summary>
    /// <param name="left">The first polygon.</param>
    /// <param name="right">The second polygon.</param>
    /// <returns>Whether they differ.</returns>
    public static bool operator !=(GesPoly? left, GesPoly? right) => !(left == right);

    /// <summary>Writes the polygon for diagnostics.</summary>
    /// <returns>Its points.</returns>
    public override string ToString() => ":Poly(points: [" + string.Join(", ", Array.ConvertAll(_points, point => point.ToString())) + "])";

    private static double SignedArea(GesPoint[] points)
    {
        double twice = 0;
        for (var index = 0; index < points.Length; index++)
        {
            var a = points[index];
            var b = points[(index + 1) % points.Length];
            twice += (a.X * b.Y) - (b.X * a.Y);
        }

        return twice * 0.5;
    }

    private static bool Valid(GesPoint[] points)
    {
        var count = points.Length;
        if (count < 3) return false;
        foreach (var point in points)
            if (!point.IsFinite || point.Z != 0) return false;
        for (var index = 0; index < count; index++)
            if (points[index].Equals(points[(index + 1) % count])) return false;
        for (var first = 0; first < count; first++)
        {
            var a = points[first];
            var b = points[(first + 1) % count];
            for (var second = first + 1; second < count; second++)
            {
                var c = points[second];
                var d = points[(second + 1) % count];
                if (second == first + 1)
                {
                    // Neighbours share b; they must not fold back over each other.
                    if (Orientation(a, b, d) == 0 && Dot(a - b, d - b) > 0) return false;
                }
                else if (first == 0 && second == count - 1)
                {
                    // The closing edge shares a with the first edge.
                    if (Orientation(c, a, b) == 0 && Dot(c - a, b - a) > 0) return false;
                }
                else if (SegmentsIntersect(a, b, c, d)) return false;
            }
        }

        return SignedArea(points) != 0;
    }

    private static double Dot(GesVector a, GesVector b) => (a.X * b.X) + (a.Y * b.Y);

    // The sign of the turn from a over b to c: positive counterclockwise, negative clockwise, zero when collinear.
    private static int Orientation(GesPoint a, GesPoint b, GesPoint c)
    {
        var turn = ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X));
        return turn > 0 ? 1 : turn < 0 ? -1 : 0;
    }

    // Whether c lies on the closed segment from a to b.
    private static bool OnSegment(GesPoint a, GesPoint b, GesPoint c)
        => Orientation(a, b, c) == 0 && GesBox.Lower(a.X, b.X) <= c.X && c.X <= GesBox.Upper(a.X, b.X) && GesBox.Lower(a.Y, b.Y) <= c.Y && c.Y <= GesBox.Upper(a.Y, b.Y);

    // Whether the closed segments ab and cd share a point.
    private static bool SegmentsIntersect(GesPoint a, GesPoint b, GesPoint c, GesPoint d)
    {
        var o1 = Orientation(a, b, c);
        var o2 = Orientation(a, b, d);
        var o3 = Orientation(c, d, a);
        var o4 = Orientation(c, d, b);
        if (o1 != o2 && o3 != o4) return true;
        return (o1 == 0 && OnSegment(a, b, c)) || (o2 == 0 && OnSegment(a, b, d)) || (o3 == 0 && OnSegment(c, d, a)) || (o4 == 0 && OnSegment(c, d, b));
    }

    private static double SegmentDistance(GesPoint a, GesPoint b, GesPoint point)
    {
        var edge = b - a;
        var t = Dot(point - a, edge) / Dot(edge, edge);
        t = t < 0 ? 0 : t > 1 ? 1 : t;
        var closest = new GesPoint(a.X + (edge.X * t), a.Y + (edge.Y * t));
        return (point - closest).Length;
    }
}
