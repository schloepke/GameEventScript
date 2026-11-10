// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Api;

/// <summary>
/// An axis-aligned box between two corners, without a unit. Creating one orders its corners, so <see cref="Min"/> is never
/// above <see cref="Max"/> on an axis; a 2D box has <c>Z</c> 0 on both. Scripts see it as the external type <c>:Box</c> of
/// <see cref="GesGeometryLibrary"/>.
/// </summary>
public readonly struct GesBox : IEquatable<GesBox>
{
    /// <summary>Creates the box between two opposite corners, in any order.</summary>
    /// <param name="corner">One corner.</param>
    /// <param name="opposite">The opposite corner.</param>
    public GesBox(GesPoint corner, GesPoint opposite)
    {
        Min = new GesPoint(Lower(corner.X, opposite.X), Lower(corner.Y, opposite.Y), Lower(corner.Z, opposite.Z));
        Max = new GesPoint(Upper(corner.X, opposite.X), Upper(corner.Y, opposite.Y), Upper(corner.Z, opposite.Z));
    }

    /// <summary>Creates the box with a centre and a size; a negative size component counts as its magnitude.</summary>
    /// <param name="center">The centre.</param>
    /// <param name="size">The extent on each axis.</param>
    /// <returns>The box.</returns>
    public static GesBox FromCenter(GesPoint center, GesVector size)
    {
        var half = size * 0.5;
        return new GesBox(center - half, center + half);
    }

    /// <summary>Gets the corner with the smallest components.</summary>
    public GesPoint Min { get; }

    /// <summary>Gets the corner with the largest components.</summary>
    public GesPoint Max { get; }

    /// <summary>Gets whether both corners lie in the plane z = 0.</summary>
    public bool Is2D => Min.Z == 0 && Max.Z == 0;

    /// <summary>Gets whether every component is finite.</summary>
    public bool IsFinite => Min.IsFinite && Max.IsFinite;

    /// <summary>Gets the extent on each axis.</summary>
    public GesVector Size => Max - Min;

    /// <summary>Gets the centre.</summary>
    public GesPoint Center => new((Min.X + Max.X) * 0.5, (Min.Y + Max.Y) * 0.5, (Min.Z + Max.Z) * 0.5);

    /// <summary>Tests whether a point lies in the box, its boundary included.</summary>
    /// <param name="point">The point.</param>
    /// <returns>Whether the point lies within the corners on every axis.</returns>
    public bool Contains(GesPoint point) => Min.X <= point.X && point.X <= Max.X && Min.Y <= point.Y && point.Y <= Max.Y && Min.Z <= point.Z && point.Z <= Max.Z;

    /// <summary>Tests whether two boxes share a point; touching boxes intersect.</summary>
    /// <param name="other">The other box.</param>
    /// <returns>Whether the boxes overlap or touch on every axis.</returns>
    public bool Intersects(GesBox other) => Min.X <= other.Max.X && other.Min.X <= Max.X && Min.Y <= other.Max.Y && other.Min.Y <= Max.Y && Min.Z <= other.Max.Z && other.Min.Z <= Max.Z;

    /// <summary>Computes the smallest box that holds both.</summary>
    /// <param name="other">The other box.</param>
    /// <returns>The enclosing box.</returns>
    public GesBox Union(GesBox other)
        => new(new GesPoint(Lower(Min.X, other.Min.X), Lower(Min.Y, other.Min.Y), Lower(Min.Z, other.Min.Z)), new GesPoint(Upper(Max.X, other.Max.X), Upper(Max.Y, other.Max.Y), Upper(Max.Z, other.Max.Z)));

    /// <summary>
    /// Moves every side inward by a distance, outward when it is negative: x and y of a 2D box, all three axes otherwise.
    /// </summary>
    /// <param name="by">The distance.</param>
    /// <returns>The inset box, or null when the distance is not finite or the sides would cross.</returns>
    public GesBox? Inset(double by)
    {
        if (!GesVector.Finite(by)) return null;
        var depth = Is2D ? 0 : by;
        var min = new GesPoint(Min.X + by, Min.Y + by, Min.Z + depth);
        var max = new GesPoint(Max.X - by, Max.Y - by, Max.Z - depth);
        if (min.X > max.X || min.Y > max.Y || min.Z > max.Z) return null;
        return new GesBox(min, max);
    }

    /// <summary>Moves a point onto the nearest point of the box.</summary>
    /// <param name="point">The point.</param>
    /// <returns>The point itself when the box holds it, otherwise the nearest point of its boundary.</returns>
    public GesPoint Clamp(GesPoint point) => new(Clamp(point.X, Min.X, Max.X), Clamp(point.Y, Min.Y, Max.Y), Clamp(point.Z, Min.Z, Max.Z));

    /// <summary>Converts the box to the external value <c>:Box</c> of <see cref="GesGeometryLibrary"/>.</summary>
    /// <param name="unit">The unit of its corners.</param>
    /// <returns>The GES value.</returns>
    public GesValue ToValue(GesQuantityUnit unit = GesQuantityUnit.UnitNone) => GesValue.GesExternal(new GesGeometryLibrary.BoxValue(this, unit));

    /// <summary>Reads a <c>:Box</c> of exactly the given unit. Units never convert.</summary>
    /// <param name="value">The value.</param>
    /// <param name="unit">The expected unit.</param>
    /// <returns>The box, or null when the value is no <c>:Box</c> of <see cref="GesGeometryLibrary"/> or has another unit.</returns>
    public static GesBox? FromValue(in GesValue value, GesQuantityUnit unit = GesQuantityUnit.UnitNone)
        => value.ExternalValue is GesGeometryLibrary.BoxValue box && box.Unit == unit ? box.Box : null;

    /// <summary>Compares the corners.</summary>
    /// <param name="left">The first box.</param>
    /// <param name="right">The second box.</param>
    /// <returns>Whether both corners are equal.</returns>
    public static bool operator ==(GesBox left, GesBox right) => left.Equals(right);

    /// <summary>Compares the corners.</summary>
    /// <param name="left">The first box.</param>
    /// <param name="right">The second box.</param>
    /// <returns>Whether a corner differs.</returns>
    public static bool operator !=(GesBox left, GesBox right) => !left.Equals(right);

    /// <summary>Compares the corners.</summary>
    /// <param name="other">The other box.</param>
    /// <returns>Whether both corners are equal.</returns>
    public bool Equals(GesBox other) => Min.Equals(other.Min) && Max.Equals(other.Max);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is GesBox other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => unchecked((Min.GetHashCode() * 397) ^ Max.GetHashCode());

    /// <summary>Writes the box for diagnostics.</summary>
    /// <returns>The corners.</returns>
    public override string ToString() => ":Box(min: " + Min + ", max: " + Max + ")";

    // Written out rather than Math.Min and Math.Max, so that both ports treat NaN alike.
    internal static double Lower(double a, double b) => b < a ? b : a;

    internal static double Upper(double a, double b) => b > a ? b : a;

    private static double Clamp(double value, double lower, double upper) => value < lower ? lower : value > upper ? upper : value;
}
