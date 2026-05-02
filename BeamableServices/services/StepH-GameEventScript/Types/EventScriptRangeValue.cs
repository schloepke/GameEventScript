#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Collections.Generic;
using static StepH.GameEventScript.Types.EventScriptValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class EventScriptRangeValue : EventScriptValue
{
    public static EventScriptRangeValue EventScriptRange(long from, long to, long step) => new(from, to, step);

    private EventScriptRangeValue(long from, long to, long step)
    {
        From = from;
        To = to;
        Step = step;
    }

    public long From { get; }
    public long To { get; }
    public long Step { get; }

    public override EventScriptValueKind Kind => EventScriptValueKind.Range;

    public override string AsText() => ToString();

    public override IReadOnlyList<EventScriptValue> AsList() => CreateReadOnlyList(AsEnumerable());

    public override ISet<EventScriptValue> AsSet() => TryConvertToSet(out var value) ? value.AsSet() : new SortedSet<EventScriptValue>(StableComparer);

    public override bool HasSemanticValue() => GetLength() > 0;

    public override bool IsSemanticallyEmpty() => GetLength() == 0;

    public override bool Contains(EventScriptValue needle)
    {
        if (!needle.IsNumber())
        {
            return false;
        }

        var value = needle.AsInteger();
        if (!Integer(value).Equals(needle))
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

    public override IEnumerable<EventScriptValue> AsEnumerable()
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
                    yield return Integer(current);
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
            yield return Integer(descendingCurrent);
            var next = descendingCurrent + Step;
            if (next >= descendingCurrent)
            {
                yield break;
            }

            descendingCurrent = next;
        }
    }

    protected override EventScriptValue LookupCore(EventScriptValue selector)
    {
        var index = selector.AsInteger();
        if (index <= 0 || index > GetLength())
        {
            return Nothing;
        }

        var value = (decimal)From + ((decimal)index - 1m) * Step;
        if (value < long.MinValue || value > long.MaxValue)
        {
            return Nothing;
        }

        return Integer((long)value);
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

    internal override bool TryConvertToText(out EventScriptValue value)
    {
        value = Text(ToString());
        return true;
    }

    internal override bool TryConvertToList(out EventScriptValue value)
    {
        value = List(AsEnumerable());
        return true;
    }

    internal override bool TryConvertToSet(out EventScriptValue value)
    {
        value = Set(AsEnumerable());
        return true;
    }

}
