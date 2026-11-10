// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Api;

/// <summary>
/// A position with three Binary64 components and no unit, the native counterpart of a GES Point. A 2D point has
/// <see cref="Z"/> 0. As in the language, a point moves by a <see cref="GesVector"/>, two points give a vector, and a
/// point does not scale. The unit belongs to the schema or host and is applied when it converts to or from a
/// <see cref="GesValue"/>.
/// </summary>
public struct GesPoint : IEquatable<GesPoint>
{
    /// <summary>Creates a point from its components.</summary>
    /// <param name="x">The x component.</param>
    /// <param name="y">The y component.</param>
    /// <param name="z">The z component; 0 for a 2D point.</param>
    public GesPoint(double x, double y, double z = 0d)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>Gets the origin.</summary>
    public static GesPoint Zero => default;

    /// <summary>Gets or sets the x component.</summary>
    public double X { readonly get; set; }

    /// <summary>Gets or sets the y component.</summary>
    public double Y { readonly get; set; }

    /// <summary>Gets or sets the z component, 0 for a 2D point.</summary>
    public double Z { readonly get; set; }

    /// <summary>Gets whether every component is finite.</summary>
    public readonly bool IsFinite => GesVector.Finite(X) && GesVector.Finite(Y) && GesVector.Finite(Z);

    /// <summary>Computes the Euclidean distance, as the language's <c>distance</c> does.</summary>
    /// <param name="other">The other point.</param>
    /// <returns>The distance.</returns>
    public readonly double DistanceTo(GesPoint other) => (other - this).Length;

    /// <summary>Converts the point to a GES Point with a unit.</summary>
    /// <param name="unit">The unit of every component.</param>
    /// <returns>The GES value, which is Nothing when a component is NaN, as in the language.</returns>
    public readonly GesValue ToValue(GesQuantityUnit unit = GesQuantityUnit.UnitNone) => GesValue.GesPoint(X, Y, Z, unit);

    /// <summary>Reads a GES Point of exactly the given unit. Units never convert.</summary>
    /// <param name="value">The value.</param>
    /// <param name="unit">The expected unit.</param>
    /// <returns>The point, or null when the value is no Point or has another unit.</returns>
    public static GesPoint? FromValue(in GesValue value, GesQuantityUnit unit = GesQuantityUnit.UnitNone)
        => value.ValueKind == GesBytecodeTypeKind.Point && value.ValueUnit == unit ? new GesPoint(value.X, value.Y, value.Z) : null;

    /// <summary>Moves a point by a vector.</summary>
    /// <param name="point">The point.</param>
    /// <param name="vector">The displacement.</param>
    /// <returns>The moved point.</returns>
    public static GesPoint operator +(GesPoint point, GesVector vector) => new(point.X + vector.X, point.Y + vector.Y, point.Z + vector.Z);

    /// <summary>Moves a point back by a vector.</summary>
    /// <param name="point">The point.</param>
    /// <param name="vector">The displacement to undo.</param>
    /// <returns>The moved point.</returns>
    public static GesPoint operator -(GesPoint point, GesVector vector) => new(point.X - vector.X, point.Y - vector.Y, point.Z - vector.Z);

    /// <summary>Computes the vector from one point to another.</summary>
    /// <param name="end">The end point.</param>
    /// <param name="start">The start point.</param>
    /// <returns>The vector from <paramref name="start"/> to <paramref name="end"/>.</returns>
    public static GesVector operator -(GesPoint end, GesPoint start) => new(end.X - start.X, end.Y - start.Y, end.Z - start.Z);

    /// <summary>Compares the components with Binary64 equality, so that 0 and -0 are equal.</summary>
    /// <param name="left">The first point.</param>
    /// <param name="right">The second point.</param>
    /// <returns>Whether all components are equal.</returns>
    public static bool operator ==(GesPoint left, GesPoint right) => left.Equals(right);

    /// <summary>Compares the components with Binary64 equality.</summary>
    /// <param name="left">The first point.</param>
    /// <param name="right">The second point.</param>
    /// <returns>Whether a component differs.</returns>
    public static bool operator !=(GesPoint left, GesPoint right) => !left.Equals(right);

    /// <summary>Compares the components; 0 and -0 are equal.</summary>
    /// <param name="other">The other point.</param>
    /// <returns>Whether all components are equal.</returns>
    public readonly bool Equals(GesPoint other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is GesPoint other && Equals(other);

    /// <inheritdoc />
    public override readonly int GetHashCode() => GesVector.ComponentHash(X, Y, Z);

    /// <summary>Writes the point as a unitless GES literal.</summary>
    /// <returns>The text, such as <c>:Point(x: 1, y: 2, z: 0)</c>.</returns>
    public override readonly string ToString() => GesVector.LiteralText("Point", X, Y, Z);
}
