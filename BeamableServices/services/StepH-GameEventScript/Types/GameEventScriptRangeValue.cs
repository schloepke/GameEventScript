#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using static StepH.GameEventScript.Api.GameEventScriptValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class GameEventScriptRangeValue : GameEventScriptValue
{
    public static GameEventScriptRangeValue Create(long from, long to, long step) => new(from, to, step);

    public static GameEventScriptRangeValue Create(double from, double to, double step)
    {
        if (IsExactInteger(from, out var integerFrom) &&
            IsExactInteger(to, out var integerTo) &&
            IsExactInteger(step, out var integerStep))
        {
            return Create(integerFrom, integerTo, integerStep);
        }

        return new GameEventScriptRangeValue(from, to, step);
    }

    private GameEventScriptRangeValue(long from, long to, long step)
    {
        From = from;
        To = to;
        Step = step;
        FromNumber = from;
        ToNumber = to;
        StepNumber = step;
        IsIntegerRange = true;
    }

    private GameEventScriptRangeValue(double from, double to, double step)
    {
        From = ToIntegerSaturated(from);
        To = ToIntegerSaturated(to);
        Step = ToIntegerSaturated(step);
        FromNumber = from;
        ToNumber = to;
        StepNumber = step;
    }

    public long From { get; }
    public long To { get; }
    public long Step { get; }
    public double FromNumber { get; }
    public double ToNumber { get; }
    public double StepNumber { get; }
    public bool IsIntegerRange { get; }

    public override GameEventScriptValueKind Kind => GameEventScriptValueKind.Range;

    public override string AsText() => ToString();

    public override IReadOnlyList<GameEventScriptValue> AsList() => CreateReadOnlyList(AsEnumerable());

    public override bool HasSemanticValue() => GetLength() > 0;

    public override bool IsSemanticallyEmpty() => GetLength() == 0;

    public override bool Contains(GameEventScriptValue needle)
    {
        if (!needle.IsNumber())
        {
            return false;
        }

        if (IsIntegerRange)
        {
            var value = needle.AsInteger();
            if (!GesInteger(value).Equals(needle))
            {
                return false;
            }

            return Step != 0 && GameEventScriptRangeMath.Contains(From, To, Step, value);
        }

        return GameEventScriptRangeMath.Contains(FromNumber, ToNumber, StepNumber, needle.AsNumber());
    }

    public override IEnumerable<GameEventScriptValue> AsEnumerable()
    {
        if (!IsIntegerRange)
        {
            foreach (var value in EnumerateFloatRange())
            {
                yield return GesFloat(value);
            }

            yield break;
        }

        switch (Step)
        {
            case 0:
                yield break;
            case > 0:
            {
                var current = From;
                while (current <= To)
                {
                    yield return GesInteger(current);
                    var next = current + Step;
                    if (next <= current)
                    {
                        yield break;
                    }

                    current = next;
                }

                yield break;
            }
        }

        var descendingCurrent = From;
        while (descendingCurrent >= To)
        {
            yield return GesInteger(descendingCurrent);
            var next = descendingCurrent + Step;
            if (next >= descendingCurrent)
            {
                yield break;
            }

            descendingCurrent = next;
        }
    }

    protected override GameEventScriptValue LookupCore(GameEventScriptValue selector)
    {
        if (IsIntegerRange)
        {
            if (!GameEventScriptRangeMath.TryGetTerm(From, To, Step, selector.AsInteger(), out var value))
            {
                return GameEventScriptNothingValue.Instance;
            }

            return GesInteger(value);
        }

        if (!GameEventScriptRangeMath.TryGetTerm(FromNumber, ToNumber, StepNumber, selector.AsInteger(), out var floatValue))
        {
            return GameEventScriptNothingValue.Instance;
        }

        return GesFloat(floatValue);
    }

    private long GetLength()
        => IsIntegerRange
            ? GameEventScriptRangeMath.GetLength(From, To, Step)
            : GameEventScriptRangeMath.GetLength(FromNumber, ToNumber, StepNumber);

    internal override bool TryConvertToText(out GameEventScriptValue value)
    {
        value = GesText(ToString());
        return true;
    }

    internal override bool TryConvertToList(out GameEventScriptValue value)
    {
        value = GesList(AsEnumerable());
        return true;
    }

    private IEnumerable<double> EnumerateFloatRange()
    {
        if (!double.IsFinite(FromNumber) ||
            !double.IsFinite(ToNumber) ||
            !double.IsFinite(StepNumber) ||
            StepNumber == 0d)
        {
            yield break;
        }

        var current = FromNumber;
        if (StepNumber > 0d)
        {
            while (current <= ToNumber)
            {
                yield return current;
                var next = current + StepNumber;
                if (next <= current)
                {
                    yield break;
                }

                current = next;
            }

            yield break;
        }

        while (current >= ToNumber)
        {
            yield return current;
            var next = current + StepNumber;
            if (next >= current)
            {
                yield break;
            }

            current = next;
        }
    }

    private static bool IsExactInteger(double value, out long integer)
    {
        if (double.IsFinite(value) &&
            value >= long.MinValue &&
            value <= long.MaxValue &&
            value == Math.Truncate(value))
        {
            integer = (long)value;
            return true;
        }

        integer = 0L;
        return false;
    }

}
