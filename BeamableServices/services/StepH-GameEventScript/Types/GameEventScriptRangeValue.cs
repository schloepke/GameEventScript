#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Collections.Generic;
using static StepH.GameEventScript.Api.GameEventScriptValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class GameEventScriptRangeValue : GameEventScriptValue
{
    public static GameEventScriptRangeValue Create(long from, long to, long step) => new(from, to, step);

    private GameEventScriptRangeValue(long from, long to, long step)
    {
        From = from;
        To = to;
        Step = step;
    }

    public long From { get; }
    public long To { get; }
    public long Step { get; }

    public override GameEventScriptValueKind Kind => GameEventScriptValueKind.Range;

    public override string AsText() => ToString();

    public override IReadOnlyList<GameEventScriptValue> AsList() => CreateReadOnlyList(AsEnumerable());

    public override ISet<GameEventScriptValue> AsSet() => TryConvertToSet(out var value) ? value.AsSet() : new SortedSet<GameEventScriptValue>(StableComparer);

    public override bool HasSemanticValue() => GetLength() > 0;

    public override bool IsSemanticallyEmpty() => GetLength() == 0;

    public override bool Contains(GameEventScriptValue needle)
    {
        if (!needle.IsNumber())
        {
            return false;
        }

        var value = needle.AsInteger();
        if (!GesInteger(value).Equals(needle))
        {
            return false;
        }

        if (Step == 0)
        {
            return false;
        }

        if (Step > 0)
        {
            if (value < From || value > To)
            {
                return false;
            }
        }
        else if (value > From || value < To)
        {
            return false;
        }

        return ((decimal)value - From) % Step == 0m;
    }

    public override IEnumerable<GameEventScriptValue> AsEnumerable()
    {
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
        var index = selector.AsInteger();
        if (index <= 0 || index > GetLength())
        {
            return GameEventScriptNothingValue.Instance;
        }

        var value = (decimal)From + ((decimal)index - 1m) * Step;
        if (value < long.MinValue || value > long.MaxValue)
        {
            return GameEventScriptNothingValue.Instance;
        }

        return GesInteger((long)value);
    }

    private long GetLength()
    {
        if (Step == 0)
        {
            return 0;
        }

        if (Step > 0)
        {
            if (From > To)
            {
                return 0;
            }

            return ClampLength(((decimal)To - From) / Step);
        }

        if (From < To)
        {
            return 0;
        }

        return ClampLength(((decimal)From - To) / -(decimal)Step);
    }

    private static long ClampLength(decimal zeroBasedDistance)
    {
        var length = decimal.Floor(zeroBasedDistance) + 1m;
        if (length <= 0m)
        {
            return 0;
        }

        return length > long.MaxValue ? long.MaxValue : (long)length;
    }

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

    internal override bool TryConvertToSet(out GameEventScriptValue value)
    {
        value = GseSet(AsEnumerable());
        return true;
    }

}
