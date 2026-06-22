using System;

namespace StepH.GameEventScript.Api;

internal static class GameEventScriptNumericRules
{
    internal enum NumericKind
    {
        Finite,
        NaN,
        PositiveInfinity,
        NegativeInfinity
    }

    internal readonly record struct NumericValue(NumericKind Kind, double Value)
    {
        public bool IsFinite => Kind == NumericKind.Finite;
        public bool IsNaN => Kind == NumericKind.NaN;
        public bool IsPositiveInfinity => Kind == NumericKind.PositiveInfinity;
        public bool IsNegativeInfinity => Kind == NumericKind.NegativeInfinity;

        public static NumericValue Finite(double value) => new(NumericKind.Finite, value);
        public static NumericValue NaN() => new(NumericKind.NaN, 0d);
        public static NumericValue PositiveInfinity() => new(NumericKind.PositiveInfinity, 0d);
        public static NumericValue NegativeInfinity() => new(NumericKind.NegativeInfinity, 0d);
    }

    internal static long ToIntegerSaturated(double number)
    {
        var truncated = Math.Truncate(number);
        return truncated switch
        {
            > long.MaxValue => long.MaxValue,
            < long.MinValue => long.MinValue,
            _ => (long)truncated
        };
    }

    internal static double WrapDegrees(double degrees)
    {
        var wrapped = degrees % 360d;
        if (wrapped < 0d)
        {
            wrapped += 360d;
        }

        return wrapped == 360d ? 0d : wrapped;
    }
}
