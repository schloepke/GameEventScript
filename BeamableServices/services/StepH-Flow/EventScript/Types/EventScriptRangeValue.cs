#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using static StepH.Flow.EventScript.Types.EventScriptValueFactory;
using System.Collections.Generic;

namespace StepH.Flow.EventScript.Types;

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
