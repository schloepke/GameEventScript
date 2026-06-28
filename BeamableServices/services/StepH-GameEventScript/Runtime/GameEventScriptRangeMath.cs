#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

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
        if (!double.IsFinite(value) ||
            !double.IsFinite(from) ||
            !double.IsFinite(to) ||
            !double.IsFinite(step) ||
            step == 0d)
        {
            return false;
        }

        return step > 0d
            ? value >= from && value <= to && IsIntegerMultiple(value - from, step)
            : value <= from && value >= to && IsIntegerMultiple(from - value, -step);
    }

    public static double? GetTerm(double from, double to, double step, long oneBasedIndex)
    {
        if (oneBasedIndex <= 0 || oneBasedIndex > GetLength(from, to, step))
        {
            return null;
        }

        var value = from + step * (oneBasedIndex - 1);
        return step > 0d
            ? value <= to ? value : null
            : value >= to ? value : null;
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

    private static bool IsIntegerMultiple(double distance, double stepMagnitude)
    {
        var quotient = distance / stepMagnitude;
        var nearest = System.Math.Round(quotient);
        var tolerance = 1e-10d * System.Math.Max(1d, System.Math.Abs(quotient));
        return System.Math.Abs(quotient - nearest) <= tolerance;
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
