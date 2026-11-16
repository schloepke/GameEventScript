// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Api;

/// <summary>
/// A filled circle in the plane z = 0, without a unit: a centre and a radius. Creating one takes the radius's magnitude. A
/// disc is valid with a finite centre in the plane and a finite radius; the operations of an invalid one give null, as GES
/// operations give <c>nothing</c> for a domain failure. Scripts see it as the external type <c>:Disc</c> of
/// <see cref="GesGeometryLibrary"/>.
/// </summary>
public readonly struct GesDisc : IEquatable<GesDisc>
{
    /// <summary>Creates the disc with a centre and a radius; a negative radius counts as its magnitude.</summary>
    /// <param name="center">The centre.</param>
    /// <param name="radius">The radius.</param>
    public GesDisc(GesPoint center, double radius)
    {
        Center = center;
        Radius = Math.Abs(radius);
    }

    /// <summary>Creates the disc around the origin; a negative radius counts as its magnitude.</summary>
    /// <param name="radius">The radius.</param>
    public GesDisc(double radius)
        : this(GesPoint.Zero, radius)
    {
    }

    /// <summary>Gets the centre.</summary>
    public GesPoint Center { get; }

    /// <summary>Gets the radius, never negative.</summary>
    public double Radius { get; }

    /// <summary>Gets whether the centre is finite and lies in the plane z = 0 and the radius is finite.</summary>
    public bool IsValid => Center.IsFinite && Center.Z == 0 && GesVector.Finite(Radius);

    /// <summary>Gets the area, π·r², unitless like a product of two lengths; null for an invalid disc.</summary>
    public double? Area => IsValid ? Math.PI * Radius * Radius : null;

    /// <summary>Gets the smallest box that holds the disc; null for an invalid disc.</summary>
    public GesBox? Bounds => IsValid ? new GesBox(new GesPoint(Center.X - Radius, Center.Y - Radius), new GesPoint(Center.X + Radius, Center.Y + Radius)) : null;

    /// <summary>Tests whether a point lies in the disc, its outline included.</summary>
    /// <param name="point">The point.</param>
    /// <returns>Whether it lies within the radius in the plane, false off the plane, or null for an invalid disc or point.</returns>
    public bool? Contains(GesPoint point)
    {
        if (!IsValid || !point.IsFinite) return null;
        if (point.Z != 0) return false;
        var x = point.X - Center.X;
        var y = point.Y - Center.Y;
        return (x * x) + (y * y) <= Radius * Radius;
    }

    /// <summary>Computes the distance from a point to the outline, for points inside and outside.</summary>
    /// <param name="point">The point.</param>
    /// <returns>The distance, or null for an invalid disc or point.</returns>
    public double? DistanceTo(GesPoint point)
    {
        if (!IsValid || !point.IsFinite) return null;
        var x = point.X - Center.X;
        var y = point.Y - Center.Y;
        var across = Math.Sqrt((x * x) + (y * y)) - Radius;
        return Math.Sqrt((across * across) + (point.Z * point.Z));
    }

    /// <summary>Moves a point onto the nearest point of the disc.</summary>
    /// <param name="point">The point.</param>
    /// <returns>The point itself in the plane when the disc holds it, otherwise the nearest point of its outline; null for an
    /// invalid disc or point.</returns>
    public GesPoint? Clamp(GesPoint point)
    {
        if (!IsValid || !point.IsFinite) return null;
        var x = point.X - Center.X;
        var y = point.Y - Center.Y;
        var length = Math.Sqrt((x * x) + (y * y));
        if (length <= Radius) return new GesPoint(point.X, point.Y);
        var scale = Radius / length;
        return new GesPoint(Center.X + (x * scale), Center.Y + (y * scale));
    }

    /// <summary>Shrinks the radius by a distance, or grows it when the distance is negative.</summary>
    /// <param name="by">The distance.</param>
    /// <returns>The inset disc, or null for an invalid disc, a distance that is not finite or a radius that would turn
    /// negative.</returns>
    public GesDisc? Inset(double by)
    {
        if (!IsValid || !GesVector.Finite(by)) return null;
        var radius = Radius - by;
        return radius < 0 ? null : new GesDisc(Center, radius);
    }

    /// <summary>Tests whether two discs share a point; touching discs intersect.</summary>
    /// <param name="other">The other disc.</param>
    /// <returns>Whether they meet, or null when one is invalid.</returns>
    public bool? Intersects(GesDisc other)
    {
        if (!IsValid || !other.IsValid) return null;
        var x = other.Center.X - Center.X;
        var y = other.Center.Y - Center.Y;
        var reach = Radius + other.Radius;
        return (x * x) + (y * y) <= reach * reach;
    }

    /// <summary>Tests whether the disc shares a point with a 2D box; touching counts.</summary>
    /// <param name="box">The box.</param>
    /// <returns>Whether they meet, or null for an invalid disc or a box that is not 2D or not finite.</returns>
    public bool? Intersects(GesBox box)
    {
        if (!IsValid || !box.Is2D || !box.IsFinite) return null;
        return Contains(box.Clamp(Center));
    }

    /// <summary>Tests whether the disc shares a point with a polygon: the polygon holds the centre, or its outline comes
    /// within the radius.</summary>
    /// <param name="poly">The polygon.</param>
    /// <returns>Whether they meet, or null when one is invalid.</returns>
    public bool? Intersects(GesPoly poly)
    {
        if (poly is null) throw new ArgumentNullException(nameof(poly));
        if (!IsValid || !poly.IsValid) return null;
        return poly.Contains(Center) == true || poly.DistanceTo(Center) <= Radius;
    }

    /// <summary>Converts the disc to the external value <c>:Disc</c> of <see cref="GesGeometryLibrary"/>.</summary>
    /// <param name="unit">The unit of its centre and radius.</param>
    /// <returns>The GES value.</returns>
    public GesValue ToValue(GesQuantityUnit unit = GesQuantityUnit.UnitNone) => GesValue.GesExternal(new GesGeometryLibrary.DiscValue(this, unit));

    /// <summary>Reads a <c>:Disc</c> of exactly the given unit. Units never convert.</summary>
    /// <param name="value">The GES value to read.</param>
    /// <param name="unit">The expected unit.</param>
    /// <returns>The disc, or null when the value is no <c>:Disc</c> of <see cref="GesGeometryLibrary"/> or has another unit.</returns>
    public static GesDisc? FromValue(in GesValue value, GesQuantityUnit unit = GesQuantityUnit.UnitNone)
        => value.ExternalValue is GesGeometryLibrary.DiscValue disc && disc.Unit == unit ? disc.Disc : null;

    /// <summary>Compares centre and radius.</summary>
    /// <param name="left">The first disc.</param>
    /// <param name="right">The second disc.</param>
    /// <returns>Whether both are equal.</returns>
    public static bool operator ==(GesDisc left, GesDisc right) => left.Equals(right);

    /// <summary>Compares centre and radius.</summary>
    /// <param name="left">The first disc.</param>
    /// <param name="right">The second disc.</param>
    /// <returns>Whether one differs.</returns>
    public static bool operator !=(GesDisc left, GesDisc right) => !left.Equals(right);

    /// <summary>Compares centre and radius.</summary>
    /// <param name="other">The other disc.</param>
    /// <returns>Whether both are equal.</returns>
    public bool Equals(GesDisc other) => Center.Equals(other.Center) && Radius.Equals(other.Radius);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is GesDisc other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => unchecked((Center.GetHashCode() * 397) ^ Radius.GetHashCode());

    /// <summary>Writes the disc for diagnostics.</summary>
    /// <returns>The centre and radius.</returns>
    public override string ToString() => ":Disc(center: " + Center + ", radius: " + Radius.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
}
