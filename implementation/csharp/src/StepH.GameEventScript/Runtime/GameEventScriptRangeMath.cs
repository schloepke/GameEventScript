// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

namespace StepH.GameEventScript.Runtime;

internal static class GameEventScriptRangeMath
{
    public static long GetLength(long from, long to, long step)
    {
        if (step == 0)
        {
            return 0;
        }

        if (step > 0)
        {
            return from > to ? 0 : CountInclusive(unchecked((ulong)to - (ulong)from), (ulong)step);
        }

        return from < to ? 0 : CountInclusive(unchecked((ulong)from - (ulong)to), StepMagnitude(step));
    }

    public static bool Contains(long from, long to, long step, long value)
    {
        if (step == 0)
        {
            return false;
        }

        if (step > 0)
        {
            return value >= from &&
                   value <= to &&
                   unchecked((ulong)value - (ulong)from) % (ulong)step == 0UL;
        }

        return value <= from &&
               value >= to &&
               unchecked((ulong)from - (ulong)value) % StepMagnitude(step) == 0UL;
    }

    public static long? GetTerm(long from, long to, long step, long oneBasedIndex)
    {
        if (oneBasedIndex <= 0 || oneBasedIndex > GetLength(from, to, step))
        {
            return null;
        }

        var value = Offset(from, step, (ulong)(oneBasedIndex - 1));
        if (value is null)
        {
            return null;
        }

        return step > 0
            ? value <= to ? value : null
            : value >= to ? value : null;
    }

    public static long GetLength(double from, double to, double step)
    {
        if (!double.IsFinite(from) || !double.IsFinite(to) || !double.IsFinite(step) || step == 0d)
        {
            return 0;
        }

        if (step > 0d)
        {
            return from > to ? 0 : CountInclusive((to - from) / step);
        }

        return from < to ? 0 : CountInclusive((from - to) / -step);
    }

    public static bool Contains(double from, double to, double step, double value)
    {
        if (!double.IsFinite(value)) return false;
        var length = GetLength(from, to, step);
        if (length == 0 || (step > 0d ? value < from || value > to : value > from || value < to)) return false;

        // The generated sequence is monotone, but rounding can produce repeated terms.
        // Searching its actual values avoids quotient rounding, tolerances and enumeration.
        var low = 0L;
        var high = length - 1;
        while (low <= high)
        {
            var index = low + (high - low) / 2;
            var term = GetFloatTerm(from, to, step, index);
            if (term == value) return true;
            if (step > 0d ? term < value : term > value) low = index + 1;
            else high = index - 1;
        }

        return false;
    }

    public static double? GetTerm(double from, double to, double step, long oneBasedIndex)
    {
        if (oneBasedIndex <= 0 || oneBasedIndex > GetLength(from, to, step))
        {
            return null;
        }

        return GetFloatTerm(from, to, step, oneBasedIndex - 1);
    }

    // The caller has already checked the index against the precomputed length.
    // Keep multiplication and addition separate: neither repeated addition nor FMA defines a range term.
    internal static double GetFloatTerm(double from, double to, double step, long zeroBasedIndex)
    {
        var offset = step * zeroBasedIndex;
        var value = from + offset;
        return step > 0d ? System.Math.Min(value, to) : System.Math.Max(value, to);
    }

    private static long CountInclusive(ulong zeroBasedDistance, ulong stepMagnitude)
    {
        var zeroBasedCount = zeroBasedDistance / stepMagnitude;
        return zeroBasedCount >= (ulong)long.MaxValue
            ? long.MaxValue
            : (long)(zeroBasedCount + 1UL);
    }

    private static long CountInclusive(double zeroBasedDistanceInSteps)
    {
        if (double.IsNaN(zeroBasedDistanceInSteps) || zeroBasedDistanceInSteps < 0d)
        {
            return 0;
        }

        if (double.IsPositiveInfinity(zeroBasedDistanceInSteps) || zeroBasedDistanceInSteps >= long.MaxValue)
        {
            return long.MaxValue;
        }

        return (long)System.Math.Floor(zeroBasedDistanceInSteps) + 1L;
    }

    private static ulong StepMagnitude(long step)
        => unchecked(0UL - (ulong)step);

    private static long? Offset(long from, long step, ulong offset)
    {
        var magnitude = step > 0 ? (ulong)step : StepMagnitude(step);
        if (offset != 0UL && offset > ulong.MaxValue / magnitude)
        {
            return null;
        }

        var delta = offset * magnitude;
        var value = step > 0
            ? unchecked((long)((ulong)from + delta))
            : unchecked((long)((ulong)from - delta));

        return step > 0
            ? value >= from ? value : null
            : value <= from ? value : null;
    }
}
