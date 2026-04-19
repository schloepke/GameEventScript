#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Collections.Generic;
using System.Linq;

namespace StepH.Flow.EventScript.Types;

internal sealed class EventScriptSetValue : EventScriptValue
{
    private EventScriptSetValue(SortedSet<EventScriptValue> set)
    {
        Items = set;
    }

    public SortedSet<EventScriptValue> Items { get; }
    public override EventScriptValueType Type => EventScriptValueType.Set;

    public static EventScriptSetValue Create(IEnumerable<EventScriptValue>? values, IComparer<EventScriptValue> comparer)
    {
        var set = new SortedSet<EventScriptValue>((values ?? []).Select(value => value ?? EventScriptValue.Nothing), comparer);
        return new EventScriptSetValue(set);
    }
}
