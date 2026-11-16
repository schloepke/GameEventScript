// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Text;
using GameEventScript.Runtime;
using GameEventScript.Syntax;

namespace GameEventScript.Api;

// The operations of a checked computed expression.
internal enum GesComputedOp : byte
{
    Literal,
    Field,
    Item,
    Negate,
    Not,
    Abs,
    Add,
    Subtract,
    Multiply,
    Divide,
    Less,
    LessOrEqual,
    Greater,
    GreaterOrEqual,
    Equal,
    NotEqual,
    And,
    Or,
    Choice,
    Min,
    Max,
    Clamp,
    Default,
    HasValue,
    Sum,
    Count,
    Power,
    Sqrt,
    Cbrt,
    Sin,
    Cos,
    Tan,
    Asin,
    Acos,
    Atan,
    Atan2,
    Hypot,
    Exp,
    Ln,
    Floor,
    Ceil,
    Truncate,
    RoundHalfEven,
    RoundHalfUp,
    RoundHalfDown,
    Rad,
    Deg,
    Vector,
    Point,
    Box,
    BoxAround,
    Disc,
    DiscAround,
    Poly,
    PolyOf,
    Part,
    PointCount
}

// The static type of a node: a number with a unit, a percentage, a Boolean, a spatial value with a unit, or a record or list
// that only a path, `has value` or an aggregate reads.
internal enum GesComputedType : byte
{
    Number,
    Percentage,
    Boolean,
    Record,
    List,
    Vector,
    Point,
    Box,
    Disc,
    Poly
}

// A checked expression. Units are resolved at binding: each node knows its static type and unit, and an operation knows the
// unit checks that GES makes at run time, as flags, since a percentage that becomes infinite turns into a plain number there.
// Add and Subtract: First = equal units, Second = the left unit takes a percentage. Multiply: First = a product unit exists,
// Second and Third = the left and right unit take a percentage. Divide: First = a quotient unit exists, Second = the left unit
// takes a percentage, Third = the right side has no unit. Comparisons, Min, Max and Clamp: First = equal units. Sin, Cos and
// Tan: First = the angle is in degrees. The other functions check their units at binding alone.
internal sealed class GesComputedNode
{
    internal GesComputedOp Op { get; init; }

    internal GesComputedType Type { get; init; }

    internal GesQuantityUnit Unit { get; init; }

    internal GesComputedNode[] Children { get; init; } = [];

    internal GesComputedValue Literal { get; init; }

    // The literal as an exact decimal, which the canonical form writes.
    internal GesSchemaDecimal Decimal { get; init; }

    // The field names from the record, or from the element for Item; for Sum and Count, the list; for Part, the part's name.
    internal string[] Path { get; init; } = [];

    // For a path: whether each field on it is optional, and how many of its leading fields `has value` around the read
    // proves present.
    internal bool[] OptionalSteps { get; set; } = [];

    internal int Present { get; set; }

    // Whether the node gives nothing where an optional field it reads is absent; NaN, which is nothing too, is not counted.
    internal bool MayBeAbsent { get; set; }

    internal bool First { get; init; }

    internal bool Second { get; init; }

    internal bool Third { get; init; }
}

internal enum GesComputedKind : byte
{
    Nothing,
    Integer,
    Float,
    Percentage,
    False,
    True,
    Vector,
    Point,
    Box,
    Disc,
    Poly
}

// A GES value as computed fields see it: an exact Int64, a binary64, a percentage ratio, a Boolean, a spatial value, or
// nothing. Units are static, so the value carries none. A Vector or Point never holds NaN, which makes it nothing, and holds
// zero as +0, as the virtual machine stores them; a Box, Disc or Poly holds the geometry library's value.
internal readonly struct GesComputedValue
{
    private GesComputedValue(GesComputedKind kind, long integer, double number, object? shape = null)
    {
        Kind = kind;
        Integer = integer;
        Number = number;
        Shape = shape;
    }

    internal static GesComputedValue Nothing => default;

    internal GesComputedKind Kind { get; }

    internal long Integer { get; }

    // The binary64 of a Float, or the ratio of a Percentage.
    internal double Number { get; }

    internal bool IsNothing => Kind == GesComputedKind.Nothing;

    internal bool IsNumeric => Kind is GesComputedKind.Integer or GesComputedKind.Float or GesComputedKind.Percentage;

    // The numeric view: an Int64 converted to binary64, or the binary64 or ratio.
    internal double Numeric => Kind == GesComputedKind.Integer ? Integer : Number;

    internal static GesComputedValue OfInteger(long value) => new(GesComputedKind.Integer, value, 0);

    // A binary64 result: NaN is nothing, and an integral value within Int64 is an integer.
    internal static GesComputedValue OfFloat(double value)
    {
        if (double.IsNaN(value)) return Nothing;
        return GesNumber.CanRepresentAsInteger(value) ? OfInteger((long)value) : new GesComputedValue(GesComputedKind.Float, 0, value);
    }

    // A percentage result: NaN is nothing, zero is positive, and an infinite ratio is a plain number.
    internal static GesComputedValue OfPercentage(double ratio)
    {
        if (double.IsNaN(ratio)) return Nothing;
        ratio = GesNumber.CanonicalizeZero(ratio);
        return new GesComputedValue(double.IsFinite(ratio) ? GesComputedKind.Percentage : GesComputedKind.Float, 0, ratio);
    }

    internal static GesComputedValue OfBoolean(bool value) => new(value ? GesComputedKind.True : GesComputedKind.False, 0, 0);

    // The spatial value: a GesVector, GesPoint, GesBox, GesDisc or GesPoly.
    internal object? Shape { get; }

    internal GesVector Vector => (GesVector)Shape!;

    internal GesPoint Point => (GesPoint)Shape!;

    internal static GesComputedValue OfVector(double x, double y, double z)
        => double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(z) ? Nothing
            : new GesComputedValue(GesComputedKind.Vector, 0, 0, new GesVector(GesNumber.CanonicalizeZero(x), GesNumber.CanonicalizeZero(y), GesNumber.CanonicalizeZero(z)));

    internal static GesComputedValue OfPoint(double x, double y, double z)
        => double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(z) ? Nothing
            : new GesComputedValue(GesComputedKind.Point, 0, 0, new GesPoint(GesNumber.CanonicalizeZero(x), GesNumber.CanonicalizeZero(y), GesNumber.CanonicalizeZero(z)));

    internal static GesComputedValue OfBox(GesBox box) => new(GesComputedKind.Box, 0, 0, box);

    internal static GesComputedValue OfDisc(GesDisc disc) => new(GesComputedKind.Disc, 0, 0, disc);

    internal static GesComputedValue OfPoly(GesPoly poly) => new(GesComputedKind.Poly, 0, 0, poly);

    // A rounded number: an Int64, saturated at its bounds as the virtual machine rounds.
    internal static GesComputedValue OfRounded(double value) => OfInteger(GesNumber.ToIntegerSaturated(value));
}

// The operations of the GES virtual machine on the scalars of computed fields, with the unit checks resolved at binding.
// Generated code repeats them in C# and Swift; the three must give the same bits.
internal static class GesComputedMath
{
    internal static GesComputedValue Add(GesComputedValue a, GesComputedValue b, bool sameUnit, bool percentage)
        => Sum(a, b, sameUnit, percentage, subtract: false);

    internal static GesComputedValue Subtract(GesComputedValue a, GesComputedValue b, bool sameUnit, bool percentage)
        => Sum(a, b, sameUnit, percentage, subtract: true);

    private static GesComputedValue Sum(GesComputedValue a, GesComputedValue b, bool sameUnit, bool percentage, bool subtract)
    {
        if (a.IsNothing || b.IsNothing) return GesComputedValue.Nothing;
        switch (a.Kind)
        {
            case GesComputedKind.Integer when b.Kind == GesComputedKind.Integer:
                if (!sameUnit) return GesComputedValue.Nothing;
                var exact = subtract ? GesNumber.SubtractExact(a.Integer, b.Integer) : GesNumber.AddExact(a.Integer, b.Integer);
                return exact is { } integer ? GesComputedValue.OfInteger(integer) : GesComputedValue.OfFloat(subtract ? (double)a.Integer - b.Integer : (double)a.Integer + b.Integer);
            case GesComputedKind.Integer or GesComputedKind.Float when b.Kind is GesComputedKind.Integer or GesComputedKind.Float:
                if (!sameUnit) return GesComputedValue.Nothing;
                return GesComputedValue.OfFloat(subtract ? a.Numeric - b.Numeric : a.Numeric + b.Numeric);
            case GesComputedKind.Integer or GesComputedKind.Float when b.Kind == GesComputedKind.Percentage:
                if (!percentage) return GesComputedValue.Nothing;
                var x = a.Numeric;
                return GesComputedValue.OfFloat(subtract ? x - x * b.Number : x + x * b.Number);
            case GesComputedKind.Percentage when b.Kind == GesComputedKind.Percentage:
                return GesComputedValue.OfPercentage(subtract ? a.Number - b.Number : a.Number + b.Number);
            default:
                return GesComputedValue.Nothing;
        }
    }

    internal static GesComputedValue Multiply(GesComputedValue a, GesComputedValue b, bool product, bool leftPercentage, bool rightPercentage)
    {
        if (a.IsNothing || b.IsNothing) return GesComputedValue.Nothing;
        switch (a.Kind)
        {
            case GesComputedKind.Integer when b.Kind == GesComputedKind.Integer:
                if (!product) return GesComputedValue.Nothing;
                return GesNumber.MultiplyExact(a.Integer, b.Integer) is { } integer ? GesComputedValue.OfInteger(integer) : GesComputedValue.OfFloat((double)a.Integer * b.Integer);
            case GesComputedKind.Integer or GesComputedKind.Float when b.Kind is GesComputedKind.Integer or GesComputedKind.Float:
                return product ? GesComputedValue.OfFloat(a.Numeric * b.Numeric) : GesComputedValue.Nothing;
            case GesComputedKind.Integer or GesComputedKind.Float when b.Kind == GesComputedKind.Percentage:
                return leftPercentage ? GesComputedValue.OfFloat(a.Numeric * b.Number) : GesComputedValue.Nothing;
            case GesComputedKind.Percentage when b.Kind == GesComputedKind.Percentage:
                return GesComputedValue.OfPercentage(a.Number * b.Number);
            case GesComputedKind.Percentage:
                return rightPercentage ? GesComputedValue.OfFloat(a.Number * b.Numeric) : GesComputedValue.Nothing;
            default:
                return GesComputedValue.Nothing;
        }
    }

    internal static GesComputedValue Divide(GesComputedValue a, GesComputedValue b, bool quotient, bool leftPercentage, bool rightUnitless)
    {
        if (a.IsNothing || b.IsNothing) return GesComputedValue.Nothing;
        switch (a.Kind)
        {
            case GesComputedKind.Integer or GesComputedKind.Float when b.Kind is GesComputedKind.Integer or GesComputedKind.Float:
                return quotient ? GesComputedValue.OfFloat(a.Numeric / b.Numeric) : GesComputedValue.Nothing;
            case GesComputedKind.Percentage when b.Kind == GesComputedKind.Percentage:
                return GesComputedValue.OfFloat(a.Number / b.Number);
            case GesComputedKind.Percentage:
                return rightUnitless ? GesComputedValue.OfPercentage(a.Number / b.Numeric) : GesComputedValue.Nothing;
            case GesComputedKind.Integer or GesComputedKind.Float when b.Kind == GesComputedKind.Percentage:
                return leftPercentage ? GesComputedValue.OfFloat(a.Numeric / b.Number) : GesComputedValue.Nothing;
            default:
                return GesComputedValue.Nothing;
        }
    }

    internal static GesComputedValue Negate(GesComputedValue a) => a.Kind switch
    {
        GesComputedKind.Integer => a.Integer == long.MinValue ? GesComputedValue.OfFloat(-(double)a.Integer) : GesComputedValue.OfInteger(-a.Integer),
        GesComputedKind.Float => GesComputedValue.OfFloat(-a.Number),
        GesComputedKind.Percentage => GesComputedValue.OfPercentage(-a.Number),
        _ => GesComputedValue.Nothing
    };

    internal static GesComputedValue Abs(GesComputedValue a) => a.Kind switch
    {
        GesComputedKind.Integer => a.Integer == long.MinValue ? GesComputedValue.OfFloat(-(double)a.Integer) : GesComputedValue.OfInteger(Math.Abs(a.Integer)),
        GesComputedKind.Float => GesComputedValue.OfFloat(Math.Abs(a.Number)),
        GesComputedKind.Percentage => GesComputedValue.OfPercentage(Math.Abs(a.Number)),
        _ => GesComputedValue.Nothing
    };

    // Ordering: 0 less, 1 less or equal, 2 greater, 3 greater or equal; a unit mismatch is false, nothing is nothing.
    internal static GesComputedValue Compare(GesComputedValue a, GesComputedValue b, bool sameUnit, int relation)
    {
        if (a.IsNothing || b.IsNothing) return GesComputedValue.Nothing;
        if (!sameUnit) return GesComputedValue.OfBoolean(false);
        if (a.Kind == GesComputedKind.Integer && b.Kind == GesComputedKind.Integer)
            return GesComputedValue.OfBoolean(relation switch { 0 => a.Integer < b.Integer, 1 => a.Integer <= b.Integer, 2 => a.Integer > b.Integer, _ => a.Integer >= b.Integer });
        var x = a.Numeric;
        var y = b.Numeric;
        return GesComputedValue.OfBoolean(relation switch { 0 => x < y, 1 => x <= y, 2 => x > y, _ => x >= y });
    }

    internal static GesComputedValue Equal(GesComputedValue a, GesComputedValue b, bool sameUnit, bool negate)
    {
        if (a.IsNothing || b.IsNothing) return GesComputedValue.Nothing;
        bool equal;
        if (!a.IsNumeric || !b.IsNumeric) equal = a.Kind == b.Kind;
        else if (a.Kind == GesComputedKind.Integer && b.Kind == GesComputedKind.Integer) equal = sameUnit && a.Integer == b.Integer;
        else equal = sameUnit && GesNumber.EqualsWithinUlps(a.Numeric, b.Numeric, GesNumber.RuntimeEqualityUlps);
        return GesComputedValue.OfBoolean(equal != negate);
    }

    internal static GesComputedValue Not(GesComputedValue a)
        => a.Kind switch { GesComputedKind.True => GesComputedValue.OfBoolean(false), GesComputedKind.False => GesComputedValue.OfBoolean(true), _ => GesComputedValue.Nothing };

    internal static GesComputedValue And(GesComputedValue a, GesComputedValue b)
    {
        if (a.Kind == GesComputedKind.False || b.Kind == GesComputedKind.False) return GesComputedValue.OfBoolean(false);
        return a.Kind == GesComputedKind.True && b.Kind == GesComputedKind.True ? GesComputedValue.OfBoolean(true) : GesComputedValue.Nothing;
    }

    internal static GesComputedValue Or(GesComputedValue a, GesComputedValue b)
    {
        if (a.Kind == GesComputedKind.True || b.Kind == GesComputedKind.True) return GesComputedValue.OfBoolean(true);
        return a.Kind == GesComputedKind.False && b.Kind == GesComputedKind.False ? GesComputedValue.OfBoolean(false) : GesComputedValue.Nothing;
    }

    // The smaller or larger of two numbers of one unit; on a tie the left one, keeping its kind.
    internal static GesComputedValue Extreme(GesComputedValue a, GesComputedValue b, bool sameUnit, bool maximum)
    {
        if (a.IsNothing || b.IsNothing || !sameUnit) return GesComputedValue.Nothing;
        var right = a.Kind == GesComputedKind.Integer && b.Kind == GesComputedKind.Integer
            ? maximum ? b.Integer > a.Integer : b.Integer < a.Integer
            : maximum ? b.Numeric > a.Numeric : b.Numeric < a.Numeric;
        return right ? b : a;
    }

    internal static GesComputedValue Clamp(GesComputedValue value, GesComputedValue first, GesComputedValue second, bool sameUnit)
    {
        if (value.IsNothing || first.IsNothing || second.IsNothing || !sameUnit) return GesComputedValue.Nothing;
        if (value.Kind == GesComputedKind.Integer && first.Kind == GesComputedKind.Integer && second.Kind == GesComputedKind.Integer)
        {
            var low = first.Integer <= second.Integer ? first.Integer : second.Integer;
            var high = first.Integer <= second.Integer ? second.Integer : first.Integer;
            return GesComputedValue.OfInteger(value.Integer < low ? low : value.Integer > high ? high : value.Integer);
        }

        var x = value.Numeric;
        var a = first.Numeric;
        var b = second.Numeric;
        var lower = a <= b ? a : b;
        var upper = a <= b ? b : a;
        return GesComputedValue.OfFloat(x < lower ? lower : x > upper ? upper : x);
    }

    // A power of the virtual machine: nothing for nothing, the platform's pow, and the correctly rounded square root for the
    // exponent 0.5 that `sqrt` lowers to. The unit checks are made at binding.
    internal static GesComputedValue Power(GesComputedValue a, GesComputedValue b)
        => a.IsNothing || b.IsNothing ? GesComputedValue.Nothing : GesComputedValue.OfFloat(GesNumber.Power(a.Numeric, b.Numeric));

    // A function of one number in binary64: nothing for nothing, the function's result otherwise.
    internal static GesComputedValue Function(GesComputedOp op, GesComputedValue a, bool degrees)
    {
        if (a.IsNothing) return GesComputedValue.Nothing;
        var x = a.Numeric;
        return op switch
        {
            GesComputedOp.Sqrt => GesComputedValue.OfFloat(GesNumber.SquareRoot(x)),
            GesComputedOp.Cbrt => GesComputedValue.OfFloat(Math.Pow(x, 1d / 3d)),
            GesComputedOp.Sin => GesComputedValue.OfFloat(Math.Sin(degrees ? x / 180d * GesMathConstants.Pi : x)),
            GesComputedOp.Cos => GesComputedValue.OfFloat(Math.Cos(degrees ? x / 180d * GesMathConstants.Pi : x)),
            GesComputedOp.Tan => GesComputedValue.OfFloat(Math.Tan(degrees ? x / 180d * GesMathConstants.Pi : x)),
            GesComputedOp.Asin => GesComputedValue.OfFloat(Math.Asin(x)),
            GesComputedOp.Acos => GesComputedValue.OfFloat(Math.Acos(x)),
            GesComputedOp.Atan => GesComputedValue.OfFloat(Math.Atan(x)),
            GesComputedOp.Exp => GesComputedValue.OfFloat(Math.Exp(x)),
            GesComputedOp.Ln => GesComputedValue.OfFloat(Math.Log(x)),
            GesComputedOp.Floor => GesComputedValue.OfRounded(Math.Floor(x)),
            GesComputedOp.Ceil => GesComputedValue.OfRounded(Math.Ceiling(x)),
            GesComputedOp.Truncate => GesComputedValue.OfRounded(Math.Truncate(x)),
            GesComputedOp.RoundHalfEven => GesComputedValue.OfRounded(Math.Round(x, 0, MidpointRounding.ToEven)),
            GesComputedOp.RoundHalfUp => GesComputedValue.OfRounded(Math.Round(x, 0, MidpointRounding.AwayFromZero)),
            GesComputedOp.RoundHalfDown => GesComputedValue.OfRounded(GesNumber.RoundHalfTowardZero(x)),
            GesComputedOp.Rad => GesComputedValue.OfFloat(double.IsFinite(x) ? x / 180d * GesMathConstants.Pi : double.NaN),
            _ => GesComputedValue.OfFloat(double.IsFinite(x) ? x / GesMathConstants.Pi * 180d : double.NaN)
        };
    }

    // `atan2(y, x)` in radians, 0 for (0, 0).
    internal static GesComputedValue Atan2(GesComputedValue y, GesComputedValue x)
    {
        if (y.IsNothing || x.IsNothing) return GesComputedValue.Nothing;
        return GesComputedValue.OfFloat(y.Numeric == 0d && x.Numeric == 0d ? 0d : Math.Atan2(y.Numeric, x.Numeric));
    }

    // The Euclidean length of two or three components of one unit.
    internal static GesComputedValue Hypot(GesComputedValue a, GesComputedValue b, GesComputedValue? c)
    {
        if (a.IsNothing || b.IsNothing || c is { IsNothing: true }) return GesComputedValue.Nothing;
        var x = a.Numeric;
        var y = b.Numeric;
        if (c is not { } third) return GesComputedValue.OfFloat(Math.Sqrt(x * x + y * y));
        var z = third.Numeric;
        return GesComputedValue.OfFloat(Math.Sqrt(x * x + y * y + z * z));
    }

    // The conversion to the field's type that a script record makes: a Percentage takes a finite number as its ratio, and a
    // Boolean is true only for true.
    internal static GesComputedValue Cast(GesComputedValue value, GesComputedType type)
    {
        switch (type)
        {
            case GesComputedType.Percentage when value.Kind is GesComputedKind.Integer or GesComputedKind.Float:
                var ratio = value.Numeric;
                return double.IsFinite(ratio) ? GesComputedValue.OfPercentage(ratio) : GesComputedValue.Nothing;
            case GesComputedType.Boolean:
                return GesComputedValue.OfBoolean(value.Kind == GesComputedKind.True);
            default:
                return value;
        }
    }
}

// The spatial operations of the GES virtual machine and the geometry library on computed values: components are binary64, a
// NaN component makes a Vector or Point nothing, a scalar must be finite and a divisor finite and not zero. The unit checks
// are made at binding.
internal static class GesComputedSpatial
{
    internal static GesComputedValue Vector(GesComputedValue x, GesComputedValue y, GesComputedValue z, bool point)
    {
        if (x.IsNothing || y.IsNothing || z.IsNothing) return GesComputedValue.Nothing;
        return point ? GesComputedValue.OfPoint(x.Numeric, y.Numeric, z.Numeric) : GesComputedValue.OfVector(x.Numeric, y.Numeric, z.Numeric);
    }

    // vector + vector, point + vector; with subtract also point - point, which gives a vector.
    internal static GesComputedValue Add(GesComputedValue a, GesComputedValue b, bool subtract)
    {
        if (a.IsNothing || b.IsNothing) return GesComputedValue.Nothing;
        var left = a.Kind == GesComputedKind.Point ? a.Point.X : a.Vector.X;
        var leftY = a.Kind == GesComputedKind.Point ? a.Point.Y : a.Vector.Y;
        var leftZ = a.Kind == GesComputedKind.Point ? a.Point.Z : a.Vector.Z;
        var right = b.Kind == GesComputedKind.Point ? b.Point.X : b.Vector.X;
        var rightY = b.Kind == GesComputedKind.Point ? b.Point.Y : b.Vector.Y;
        var rightZ = b.Kind == GesComputedKind.Point ? b.Point.Z : b.Vector.Z;
        var x = subtract ? left - right : left + right;
        var y = subtract ? leftY - rightY : leftY + rightY;
        var z = subtract ? leftZ - rightZ : leftZ + rightZ;
        return a.Kind == GesComputedKind.Point && b.Kind == GesComputedKind.Vector ? GesComputedValue.OfPoint(x, y, z) : GesComputedValue.OfVector(x, y, z);
    }

    // A vector times a finite number or a percentage's ratio.
    internal static GesComputedValue Scale(GesComputedValue vector, GesComputedValue scalar)
    {
        if (vector.IsNothing || scalar.IsNothing) return GesComputedValue.Nothing;
        var s = scalar.Numeric;
        if (!double.IsFinite(s)) return GesComputedValue.Nothing;
        var v = vector.Vector;
        return GesComputedValue.OfVector(v.X * s, v.Y * s, v.Z * s);
    }

    // A vector divided by a finite number or percentage ratio other than zero.
    internal static GesComputedValue Divide(GesComputedValue vector, GesComputedValue scalar)
    {
        if (vector.IsNothing || scalar.IsNothing) return GesComputedValue.Nothing;
        var s = scalar.Numeric;
        if (!double.IsFinite(s) || s == 0d) return GesComputedValue.Nothing;
        var v = vector.Vector;
        return GesComputedValue.OfVector(v.X / s, v.Y / s, v.Z / s);
    }

    internal static GesComputedValue Negate(GesComputedValue vector)
    {
        if (vector.IsNothing) return GesComputedValue.Nothing;
        var v = vector.Vector;
        return GesComputedValue.OfVector(-v.X, -v.Y, -v.Z);
    }

    // `abs` of a vector: its length, the correctly rounded square root of the summed squares.
    internal static GesComputedValue Length(GesComputedValue vector)
    {
        if (vector.IsNothing) return GesComputedValue.Nothing;
        var v = vector.Vector;
        return GesComputedValue.OfFloat(Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z));
    }

    internal static GesComputedValue Box(GesComputedValue a, GesComputedValue b, bool around)
    {
        if (a.IsNothing || b.IsNothing) return GesComputedValue.Nothing;
        return GesComputedValue.OfBox(around ? GesBox.FromCenter(a.Point, b.Vector) : new GesBox(a.Point, b.Point));
    }

    internal static GesComputedValue Disc(GesComputedValue center, GesComputedValue radius)
    {
        if (center.IsNothing || radius.IsNothing) return GesComputedValue.Nothing;
        return GesComputedValue.OfDisc(new GesDisc(center.Point, radius.Numeric));
    }

    internal static GesComputedValue Disc(GesComputedValue radius) => radius.IsNothing ? GesComputedValue.Nothing : GesComputedValue.OfDisc(new GesDisc(radius.Numeric));

    // A part of a spatial value as a script reads it: an axis, a corner, centre or size of a box, or a centre or radius of a
    // disc. A NaN part is nothing.
    internal static GesComputedValue Part(GesComputedValue value, string name)
    {
        switch (value.Kind)
        {
            case GesComputedKind.Vector or GesComputedKind.Point:
            {
                var v = value.Kind == GesComputedKind.Point ? new GesVector(value.Point.X, value.Point.Y, value.Point.Z) : value.Vector;
                return GesComputedValue.OfFloat(name == "x" ? v.X : name == "y" ? v.Y : v.Z);
            }
            case GesComputedKind.Box:
            {
                var box = (GesBox)value.Shape!;
                switch (name)
                {
                    case "lower": return GesComputedValue.OfPoint(box.Min.X, box.Min.Y, box.Min.Z);
                    case "upper": return GesComputedValue.OfPoint(box.Max.X, box.Max.Y, box.Max.Z);
                    case "center":
                        var center = box.Center;
                        return GesComputedValue.OfPoint(center.X, center.Y, center.Z);
                    default:
                        var size = box.Size;
                        return GesComputedValue.OfVector(size.X, size.Y, size.Z);
                }
            }
            case GesComputedKind.Disc:
            {
                var disc = (GesDisc)value.Shape!;
                return name == "center" ? GesComputedValue.OfPoint(disc.Center.X, disc.Center.Y, disc.Center.Z) : GesComputedValue.OfFloat(disc.Radius);
            }
            default:
                return GesComputedValue.Nothing;
        }
    }

    internal static GesComputedValue PointCount(GesComputedValue poly)
        => poly.Kind == GesComputedKind.Poly ? GesComputedValue.OfInteger(((GesPoly)poly.Shape!).Points.Count) : GesComputedValue.Nothing;
}

// What an expression reads: the fields of a record, and the elements of its lists. An element that is a number reads with an
// empty path.
internal abstract class GesComputedScope
{
    internal abstract GesComputedValue Read(string[] path, bool percentage);

    internal abstract bool Has(string[] path);

    internal abstract int Count(string[] path);

    internal abstract GesComputedScope Element(string[] path, int index);
}

internal static class GesComputedEvaluation
{
    internal static GesComputedValue Evaluate(GesComputedNode node, GesComputedScope scope, GesComputedScope? item)
    {
        var children = node.Children;
        switch (node.Op)
        {
            case GesComputedOp.Literal:
                return node.Literal;
            case GesComputedOp.Field:
                return scope.Read(node.Path, node.Type == GesComputedType.Percentage);
            case GesComputedOp.Item:
                return item!.Read(node.Path, node.Type == GesComputedType.Percentage);
            case GesComputedOp.Negate when node.Type == GesComputedType.Vector:
                return GesComputedSpatial.Negate(Evaluate(children[0], scope, item));
            case GesComputedOp.Negate:
                return GesComputedMath.Negate(Evaluate(children[0], scope, item));
            case GesComputedOp.Not:
                return GesComputedMath.Not(Evaluate(children[0], scope, item));
            case GesComputedOp.Abs when children[0].Type == GesComputedType.Vector:
                return GesComputedSpatial.Length(Evaluate(children[0], scope, item));
            case GesComputedOp.Abs:
                return GesComputedMath.Abs(Evaluate(children[0], scope, item));
            case GesComputedOp.Add or GesComputedOp.Subtract when node.Type is GesComputedType.Vector or GesComputedType.Point:
                return GesComputedSpatial.Add(Evaluate(children[0], scope, item), Evaluate(children[1], scope, item), node.Op == GesComputedOp.Subtract);
            case GesComputedOp.Multiply when node.Type == GesComputedType.Vector:
            {
                var a = Evaluate(children[0], scope, item);
                var b = Evaluate(children[1], scope, item);
                return children[0].Type == GesComputedType.Vector ? GesComputedSpatial.Scale(a, b) : GesComputedSpatial.Scale(b, a);
            }
            case GesComputedOp.Divide when node.Type == GesComputedType.Vector:
                return GesComputedSpatial.Divide(Evaluate(children[0], scope, item), Evaluate(children[1], scope, item));
            case GesComputedOp.Vector or GesComputedOp.Point:
                return GesComputedSpatial.Vector(Evaluate(children[0], scope, item), Evaluate(children[1], scope, item), Evaluate(children[2], scope, item), node.Op == GesComputedOp.Point);
            case GesComputedOp.Box or GesComputedOp.BoxAround:
                return GesComputedSpatial.Box(Evaluate(children[0], scope, item), Evaluate(children[1], scope, item), node.Op == GesComputedOp.BoxAround);
            case GesComputedOp.Disc:
                return GesComputedSpatial.Disc(Evaluate(children[0], scope, item), Evaluate(children[1], scope, item));
            case GesComputedOp.DiscAround:
                return GesComputedSpatial.Disc(Evaluate(children[0], scope, item));
            case GesComputedOp.Poly:
            {
                var points = new GesPoint[children.Length];
                for (var index = 0; index < children.Length; index++)
                {
                    var point = Evaluate(children[index], scope, item);
                    if (point.IsNothing) return GesComputedValue.Nothing;
                    points[index] = point.Point;
                }

                return GesComputedValue.OfPoly(new GesPoly(points));
            }
            case GesComputedOp.PolyOf:
            {
                var source = children[0];
                var list = source.Op == GesComputedOp.Item ? item! : scope;
                var count = list.Count(source.Path);
                var points = new GesPoint[count];
                for (var index = 0; index < count; index++)
                {
                    var element = list.Element(source.Path, index);
                    var point = children.Length > 1 ? Evaluate(children[1], scope, element) : element.Read([], false);
                    if (point.IsNothing) return GesComputedValue.Nothing;
                    points[index] = point.Point;
                }

                return GesComputedValue.OfPoly(new GesPoly(points));
            }
            case GesComputedOp.Part:
                return GesComputedSpatial.Part(Evaluate(children[0], scope, item), node.Path[0]);
            case GesComputedOp.PointCount:
                return GesComputedSpatial.PointCount(Evaluate(children[0], scope, item));
            case GesComputedOp.Add or GesComputedOp.Subtract:
            {
                var a = Evaluate(children[0], scope, item);
                var b = Evaluate(children[1], scope, item);
                return node.Op == GesComputedOp.Add ? GesComputedMath.Add(a, b, node.First, node.Second) : GesComputedMath.Subtract(a, b, node.First, node.Second);
            }
            case GesComputedOp.Multiply:
                return GesComputedMath.Multiply(Evaluate(children[0], scope, item), Evaluate(children[1], scope, item), node.First, node.Second, node.Third);
            case GesComputedOp.Divide:
                return GesComputedMath.Divide(Evaluate(children[0], scope, item), Evaluate(children[1], scope, item), node.First, node.Second, node.Third);
            case GesComputedOp.Less or GesComputedOp.LessOrEqual or GesComputedOp.Greater or GesComputedOp.GreaterOrEqual:
                return GesComputedMath.Compare(Evaluate(children[0], scope, item), Evaluate(children[1], scope, item), node.First, node.Op - GesComputedOp.Less);
            case GesComputedOp.Equal or GesComputedOp.NotEqual:
                return GesComputedMath.Equal(Evaluate(children[0], scope, item), Evaluate(children[1], scope, item), node.First, node.Op == GesComputedOp.NotEqual);
            case GesComputedOp.And:
                return GesComputedMath.And(Evaluate(children[0], scope, item), Evaluate(children[1], scope, item));
            case GesComputedOp.Or:
                return GesComputedMath.Or(Evaluate(children[0], scope, item), Evaluate(children[1], scope, item));
            case GesComputedOp.Choice:
                for (var index = 0; index + 1 < children.Length; index += 2)
                    if (Evaluate(children[index + 1], scope, item).Kind == GesComputedKind.True) return Evaluate(children[index], scope, item);
                return Evaluate(children[^1], scope, item);
            case GesComputedOp.Min or GesComputedOp.Max:
            {
                var result = Evaluate(children[0], scope, item);
                for (var index = 1; index < children.Length; index++)
                    result = GesComputedMath.Extreme(result, Evaluate(children[index], scope, item), node.First, node.Op == GesComputedOp.Max);
                return result;
            }
            case GesComputedOp.Clamp:
                return GesComputedMath.Clamp(Evaluate(children[0], scope, item), Evaluate(children[1], scope, item), Evaluate(children[2], scope, item), node.First);
            case GesComputedOp.Default:
            {
                var value = Evaluate(children[0], scope, item);
                return value.IsNothing ? Evaluate(children[1], scope, item) : value;
            }
            case GesComputedOp.HasValue:
            {
                var child = children[0];
                if (child.Type is GesComputedType.Record) return GesComputedValue.OfBoolean(child.Op == GesComputedOp.Item ? item!.Has(child.Path) : scope.Has(child.Path));
                return GesComputedValue.OfBoolean(!Evaluate(child, scope, item).IsNothing);
            }
            case GesComputedOp.Sum:
            {
                var source = children[0];
                var list = source.Op == GesComputedOp.Item ? item! : scope;
                var count = list.Count(source.Path);
                var sum = GesComputedValue.Nothing;
                for (var index = 0; index < count; index++)
                {
                    var element = list.Element(source.Path, index);
                    var value = children.Length > 1 ? Evaluate(children[1], scope, element) : element.Read([], node.Type == GesComputedType.Percentage);
                    sum = index == 0 ? value : GesComputedMath.Add(sum, value, true, true);
                }

                return count == 0 ? GesComputedValue.OfInteger(0) : sum;
            }
            case GesComputedOp.Count:
            {
                var source = children[0];
                return GesComputedValue.OfInteger((source.Op == GesComputedOp.Item ? item! : scope).Count(source.Path));
            }
            case GesComputedOp.Power:
                return GesComputedMath.Power(Evaluate(children[0], scope, item), Evaluate(children[1], scope, item));
            case GesComputedOp.Atan2:
                return GesComputedMath.Atan2(Evaluate(children[0], scope, item), Evaluate(children[1], scope, item));
            case GesComputedOp.Hypot:
                return GesComputedMath.Hypot(Evaluate(children[0], scope, item), Evaluate(children[1], scope, item), children.Length > 2 ? Evaluate(children[2], scope, item) : null);
            default:
                return GesComputedMath.Function(node.Op, Evaluate(children[0], scope, item), node.First);
        }
    }
}

// The canonical spelling of computed fields, which the schema hash covers: a prefix form with literals as exact decimals.
internal static class GesComputedCanonical
{
    internal static void Write(GesComputedNode node, StringBuilder builder)
    {
        builder.Append('(');
        switch (node.Op)
        {
            case GesComputedOp.Literal:
                builder.Append(node.Literal.Kind switch
                {
                    GesComputedKind.True => "true",
                    GesComputedKind.False => "false",
                    _ => node.Type == GesComputedType.Percentage ? "percentage " + node.Decimal.Canonical
                        : node.Unit == GesQuantityUnit.UnitNone ? "number " + node.Decimal.Canonical : "quantity " + node.Unit.ToTypeName() + " " + node.Decimal.Canonical
                });
                break;
            case GesComputedOp.Field or GesComputedOp.Item:
                builder.Append(node.Op == GesComputedOp.Field ? "field" : "item");
                foreach (var name in node.Path) builder.Append(' ').Append(name);
                break;
            case GesComputedOp.Part:
                builder.Append("part ").Append(node.Path[0]).Append(' ');
                Write(node.Children[0], builder);
                break;
            case GesComputedOp.Sum or GesComputedOp.Count:
                builder.Append(node.Op == GesComputedOp.Sum ? "sum" : "count");
                foreach (var child in node.Children)
                {
                    builder.Append(' ');
                    Write(child, builder);
                }

                break;
            default:
                builder.Append(Name(node.Op));
                foreach (var child in node.Children)
                {
                    builder.Append(' ');
                    Write(child, builder);
                }

                break;
        }

        builder.Append(')');
    }

    internal static string Name(GesComputedOp op) => op switch
    {
        GesComputedOp.Negate => "negate",
        GesComputedOp.Not => "not",
        GesComputedOp.Abs => "abs",
        GesComputedOp.Add => "add",
        GesComputedOp.Subtract => "subtract",
        GesComputedOp.Multiply => "multiply",
        GesComputedOp.Divide => "divide",
        GesComputedOp.Less => "less",
        GesComputedOp.LessOrEqual => "lessOrEqual",
        GesComputedOp.Greater => "greater",
        GesComputedOp.GreaterOrEqual => "greaterOrEqual",
        GesComputedOp.Equal => "equal",
        GesComputedOp.NotEqual => "notEqual",
        GesComputedOp.And => "and",
        GesComputedOp.Or => "or",
        GesComputedOp.Choice => "choice",
        GesComputedOp.Min => "min",
        GesComputedOp.Max => "max",
        GesComputedOp.Clamp => "clamp",
        GesComputedOp.Default => "default",
        GesComputedOp.HasValue => "hasValue",
        GesComputedOp.Sum => "sum",
        GesComputedOp.Count => "count",
        GesComputedOp.Field => "field",
        GesComputedOp.Item => "item",
        GesComputedOp.Power => "power",
        GesComputedOp.Sqrt => "sqrt",
        GesComputedOp.Cbrt => "cbrt",
        GesComputedOp.Sin => "sin",
        GesComputedOp.Cos => "cos",
        GesComputedOp.Tan => "tan",
        GesComputedOp.Asin => "asin",
        GesComputedOp.Acos => "acos",
        GesComputedOp.Atan => "atan",
        GesComputedOp.Atan2 => "atan2",
        GesComputedOp.Hypot => "hypot",
        GesComputedOp.Exp => "exp",
        GesComputedOp.Ln => "ln",
        GesComputedOp.Floor => "floor",
        GesComputedOp.Ceil => "ceil",
        GesComputedOp.Truncate => "truncate",
        GesComputedOp.RoundHalfEven => "roundHalfEven",
        GesComputedOp.RoundHalfUp => "roundHalfUp",
        GesComputedOp.RoundHalfDown => "roundHalfDown",
        GesComputedOp.Rad => "rad",
        GesComputedOp.Deg => "deg",
        GesComputedOp.Vector => "vector",
        GesComputedOp.Point => "point",
        GesComputedOp.Box => "box",
        GesComputedOp.BoxAround => "boxAround",
        GesComputedOp.Disc => "disc",
        GesComputedOp.DiscAround => "discAround",
        GesComputedOp.Poly => "poly",
        GesComputedOp.PolyOf => "polyOf",
        GesComputedOp.Part => "part",
        GesComputedOp.PointCount => "pointCount",
        _ => "literal"
    };
}
