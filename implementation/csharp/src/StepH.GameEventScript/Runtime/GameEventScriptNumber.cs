// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Globalization;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime.Values;

namespace StepH.GameEventScript.Runtime;

/// <summary>
/// Language-neutral binary64 and signed-64 helpers shared by compiler, runtime,
/// and conformance tooling. None of these methods relies on the CLR overflow
/// context or on the platform's default number formatting.
/// </summary>
internal static class GameEventScriptNumber
{
    internal const int RuntimeEqualityUlps = 2;
    internal const double Int64UpperExclusive = 9223372036854775808d;
    internal const double Int64LowerInclusive = -9223372036854775808d;

    internal static GesValue Cast(in GesValue value)
    {
        if (value.Kind == GameEventScriptBytecodeTypeKind.Integer) return value;
        if (value.Kind == GameEventScriptBytecodeTypeKind.Text)
        {
            return TextNumberCast.Read(value.TextValue) ?? GesValue.GesNothing();
        }

        if (value.Kind == GameEventScriptBytecodeTypeKind.Series && value.ObjectValue is GesSeries series)
        {
            var first = series.GetTerm(0);
            return Cast(in first);
        }

        return GesValue.GesFloat(value.AsNumeric, value.Kind == GameEventScriptBytecodeTypeKind.Float ? value.Unit : GameEventScriptBytecodeInstructionUnit.UnitNone);
    }

    internal static GesValue Power(in GesValue value, in GesValue exponent)
    {
        if (value.IsNothing || exponent.IsNothing) return GesValue.GesNothing();
        if (exponent.HasUnit) return GesValue.GesFloat(double.NaN);

        var right = exponent.AsNumeric;
        if (double.IsNaN(right)) return GesValue.GesFloat(double.NaN);

        var unit = GameEventScriptBytecodeInstructionUnit.UnitNone;
        if (value.HasUnit)
        {
            if (right == 1d) unit = value.Unit;
            else if (right != 0d) return GesValue.GesFloat(double.NaN);
        }

        var left = value.AsNumeric;
        return GesValue.GesFloat(double.IsNaN(left) ? double.NaN : Math.Pow(left, right), unit);
    }

    internal static bool CanRepresentAsInteger(double value)
        => double.IsFinite(value) &&
           value >= Int64LowerInclusive &&
           value < Int64UpperExclusive &&
           value == Math.Truncate(value);

    internal static long ToIntegerSaturated(double value)
    {
        if (double.IsNaN(value)) return 0;
        if (value >= Int64UpperExclusive) return long.MaxValue;
        if (value < Int64LowerInclusive) return long.MinValue;
        return (long)Math.Truncate(value);
    }

    internal static long? AddExact(long left, long right)
    {
        var result = unchecked(left + right);
        return ((left ^ result) & (right ^ result)) < 0 ? null : result;
    }

    internal static long? SubtractExact(long left, long right)
    {
        var result = unchecked(left - right);
        return ((left ^ right) & (left ^ result)) < 0 ? null : result;
    }

    internal static long? MultiplyExact(long left, long right)
    {
        if (left == 0 || right == 0) return 0;
        if (left == -1) return right == long.MinValue ? null : -right;
        if (right == -1) return left == long.MinValue ? null : -left;

        var result = unchecked(left * right);
        return result / right == left ? result : null;
    }

    internal static long? FloorDivideExact(long left, long right)
    {
        if (right == 0 || left == long.MinValue && right == -1) return null;
        var quotient = left / right;
        var remainder = left % right;
        return remainder != 0 && (left < 0) != (right < 0) ? quotient - 1 : quotient;
    }

    internal static long Remainder(long left, long right)
        => left == long.MinValue && right == -1 ? 0 : left % right;

    internal static long Modulo(long left, long right)
    {
        var remainder = Remainder(left, right);
        return remainder != 0 && (remainder < 0) != (right < 0)
            ? remainder + right
            : remainder;
    }

    internal static double RoundHalfTowardZero(double value)
    {
        var absolute = Math.Abs(value);
        var floor = Math.Floor(absolute);
        var roundedAbsolute = absolute - floor > 0.5d ? floor + 1d : floor;
        return value < 0d ? -roundedAbsolute : roundedAbsolute;
    }

    internal static double CanonicalizeZero(double value) => value == 0d ? 0d : value;

    internal static bool EqualsWithinUlps(double left, double right, ulong maxUlps)
    {
        if (double.IsNaN(left) || double.IsNaN(right)) return false;
        if (double.IsInfinity(left) || double.IsInfinity(right)) return left == right;
        if (left == right) return true; // Includes +0 and -0.

        var leftBits = OrderedBits(left);
        var rightBits = OrderedBits(right);
        var distance = leftBits >= rightBits ? leftBits - rightBits : rightBits - leftBits;
        return distance <= maxUlps;
    }

    internal static string FormatCanonicalFloat(double value)
    {
        if (double.IsPositiveInfinity(value)) return "Infinity";
        if (double.IsNegativeInfinity(value)) return "-Infinity";
        if (double.IsNaN(value)) return "NaN";
        if (value == 0d) return "0";

        var text = value.ToString("R", CultureInfo.InvariantCulture);
        var exponentIndex = text.IndexOf('E');
        if (exponentIndex < 0) return text;

        var exponent = int.Parse(text.AsSpan(exponentIndex + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        return text.Substring(0, exponentIndex) + "e" + exponent.ToString(CultureInfo.InvariantCulture);
    }

    private static ulong OrderedBits(double value)
    {
        var bits = unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
        return (bits & 0x8000000000000000UL) != 0
            ? ~bits
            : bits | 0x8000000000000000UL;
    }
}
