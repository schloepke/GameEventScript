// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Globalization;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Api;

/// <summary>
/// A displacement with three Binary64 components and no unit, the native counterpart of a GES Vector. A 2D vector has
/// <see cref="Z"/> 0. The unit belongs to the schema or host that uses the value and is applied when it converts to or
/// from a <see cref="GesValue"/>.
/// </summary>
public struct GesVector : IEquatable<GesVector>
{
    /// <summary>Creates a vector from its components.</summary>
    /// <param name="x">The x component.</param>
    /// <param name="y">The y component.</param>
    /// <param name="z">The z component; 0 for a 2D vector.</param>
    public GesVector(double x, double y, double z = 0d)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>Gets the zero vector.</summary>
    public static GesVector Zero => default;

    /// <summary>Gets or sets the x component.</summary>
    public double X { readonly get; set; }

    /// <summary>Gets or sets the y component.</summary>
    public double Y { readonly get; set; }

    /// <summary>Gets or sets the z component, 0 for a 2D vector.</summary>
    public double Z { readonly get; set; }

    /// <summary>Gets whether every component is finite.</summary>
    public readonly bool IsFinite => Finite(X) && Finite(Y) && Finite(Z);

    /// <summary>Gets the Euclidean length, computed as the language's <c>length</c> computes it.</summary>
    public readonly double Length => ComponentLength(X, Y, Z);

    /// <summary>Gets the squared length.</summary>
    public readonly double LengthSquared => (X * X) + (Y * Y) + (Z * Z);

    /// <summary>Gets the vector of length 1 in the same direction, or null for a zero or non-finite vector, as
    /// <c>normalize</c> gives <c>nothing</c>.</summary>
    public readonly GesVector? Normalized
    {
        get
        {
            var length = Length;
            if (length == 0 || !Finite(length)) return null;
            return new GesVector(X / length, Y / length, Z / length);
        }
    }

    /// <summary>Computes the dot product.</summary>
    /// <param name="other">The other vector.</param>
    /// <returns>The sum of the component products.</returns>
    public readonly double Dot(GesVector other) => (X * other.X) + (Y * other.Y) + (Z * other.Z);

    /// <summary>Computes the three-dimensional cross product; for 2D vectors only its <see cref="Z"/> is nonzero.</summary>
    /// <param name="other">The other vector.</param>
    /// <returns>The vector perpendicular to both.</returns>
    public readonly GesVector Cross(GesVector other) => new((Y * other.Z) - (Z * other.Y), (Z * other.X) - (X * other.Z), (X * other.Y) - (Y * other.X));

    /// <summary>Converts the vector to a GES Vector with a unit.</summary>
    /// <param name="unit">The unit of every component.</param>
    /// <returns>The GES value, which is Nothing when a component is NaN, as in the language.</returns>
    public readonly GesValue ToValue(GesQuantityUnit unit = GesQuantityUnit.UnitNone) => GesValue.GesVector(X, Y, Z, unit);

    /// <summary>Reads a GES Vector of exactly the given unit. Units never convert.</summary>
    /// <param name="value">The GES value to read.</param>
    /// <param name="unit">The expected unit.</param>
    /// <returns>The vector, or null when the value is no Vector or has another unit.</returns>
    public static GesVector? FromValue(in GesValue value, GesQuantityUnit unit = GesQuantityUnit.UnitNone)
        => value.ValueKind == GesBytecodeTypeKind.Vector && value.ValueUnit == unit ? new GesVector(value.X, value.Y, value.Z) : null;

    /// <summary>Adds two vectors.</summary>
    /// <param name="left">The first vector.</param>
    /// <param name="right">The second vector.</param>
    /// <returns>The sum.</returns>
    public static GesVector operator +(GesVector left, GesVector right) => new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

    /// <summary>Subtracts a vector.</summary>
    /// <param name="left">The vector.</param>
    /// <param name="right">The vector to subtract.</param>
    /// <returns>The difference.</returns>
    public static GesVector operator -(GesVector left, GesVector right) => new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

    /// <summary>Negates every component.</summary>
    /// <param name="vector">The vector.</param>
    /// <returns>The opposite vector.</returns>
    public static GesVector operator -(GesVector vector) => new(-vector.X, -vector.Y, -vector.Z);

    /// <summary>Scales a vector.</summary>
    /// <param name="vector">The vector.</param>
    /// <param name="scale">The factor.</param>
    /// <returns>The scaled vector.</returns>
    public static GesVector operator *(GesVector vector, double scale) => new(vector.X * scale, vector.Y * scale, vector.Z * scale);

    /// <summary>Scales a vector.</summary>
    /// <param name="scale">The factor.</param>
    /// <param name="vector">The vector.</param>
    /// <returns>The scaled vector.</returns>
    public static GesVector operator *(double scale, GesVector vector) => vector * scale;

    /// <summary>Divides every component by a scalar, following IEEE division.</summary>
    /// <param name="vector">The vector.</param>
    /// <param name="divisor">The divisor.</param>
    /// <returns>The divided vector.</returns>
    public static GesVector operator /(GesVector vector, double divisor) => new(vector.X / divisor, vector.Y / divisor, vector.Z / divisor);

    /// <summary>Compares the components with Binary64 equality, so that 0 and -0 are equal.</summary>
    /// <param name="left">The first vector.</param>
    /// <param name="right">The second vector.</param>
    /// <returns>Whether all components are equal.</returns>
    public static bool operator ==(GesVector left, GesVector right) => left.Equals(right);

    /// <summary>Compares the components with Binary64 equality.</summary>
    /// <param name="left">The first vector.</param>
    /// <param name="right">The second vector.</param>
    /// <returns>Whether a component differs.</returns>
    public static bool operator !=(GesVector left, GesVector right) => !left.Equals(right);

    /// <summary>Compares the components with binary64 equality, as Swift does: 0 and -0 are equal, and a NaN component is
    /// equal to nothing, itself included.</summary>
    /// <param name="other">The other vector.</param>
    /// <returns>Whether all components are equal.</returns>
    public readonly bool Equals(GesVector other) => X == other.X && Y == other.Y && Z == other.Z;

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is GesVector other && Equals(other);

    /// <inheritdoc />
    public override readonly int GetHashCode() => ComponentHash(X, Y, Z);

    /// <summary>Writes the vector as a unitless GES literal.</summary>
    /// <returns>The text, such as <c>:Vector(x: 1, y: 2, z: 0)</c>.</returns>
    public override readonly string ToString() => LiteralText("Vector", X, Y, Z);

    // Helpers shared with GesPoint, written without APIs that netstandard2.1 lacks.
    internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    // The length as the language computes it, so that hosts and scripts agree bit for bit.
    internal static double ComponentLength(double x, double y, double z) => Math.Sqrt((x * x) + (y * y) + (z * z));

    // 0 and -0 hash alike, as they compare equal.
    internal static int ComponentHash(double x, double y, double z)
    {
        unchecked
        {
            var hash = (x == 0 ? 0d : x).GetHashCode();
            hash = (hash * 397) ^ (y == 0 ? 0d : y).GetHashCode();
            return (hash * 397) ^ (z == 0 ? 0d : z).GetHashCode();
        }
    }

    internal static string LiteralText(string kind, double x, double y, double z)
        => ":" + kind + "(x: " + x.ToString("R", CultureInfo.InvariantCulture) + ", y: " + y.ToString("R", CultureInfo.InvariantCulture) + ", z: " + z.ToString("R", CultureInfo.InvariantCulture) + ")";
}
