#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using static StepH.Flow.EventScript.Types.EventScriptValueFactory;
using System.Collections.Generic;
using System.Linq;

namespace StepH.Flow.EventScript.Types;

public sealed class EventScriptSetValue : EventScriptValue
{
    public static readonly EventScriptSetValue Empty = new(new SortedSet<EventScriptValue>(StableComparer));

    public static EventScriptSetValue EventScriptSet(IEnumerable<EventScriptValue>? values, IComparer<EventScriptValue>? comparer = null)
    {
        if (values == null) return Empty;
        var set = new SortedSet<EventScriptValue>(values.Select(value => value ?? Nothing), comparer ?? StableComparer);
        return set.Count == 0 ? Empty : new EventScriptSetValue(set);
    }

    private EventScriptSetValue(SortedSet<EventScriptValue> set)
    {
        Items = set;
    }

    public SortedSet<EventScriptValue> Items { get; }
    public override EventScriptValueKind Kind => EventScriptValueKind.Set;

    public override IReadOnlyList<EventScriptValue> AsList() => CreateReadOnlyList(Items);

    public override ISet<EventScriptValue> AsSet() => new SortedSet<EventScriptValue>(Items, StableComparer);

    public override EventScriptDiceValue AsDice() => TryConvertToDice(out var value) ? value.AsDice() : EventScriptDiceValue.Empty;

    public override IEnumerable<EventScriptValue> AsEnumerable() => Items;

    public override bool HasSemanticValue() => Items.Count > 0;

    public override bool IsSemanticallyEmpty() => Items.Count == 0;

    public override bool Contains(EventScriptValue needle) => Items.Any(item => item.Equals(needle));

    internal override bool TryConvertToList(out EventScriptValue value)
    {
        value = List(AsSet());
        return true;
    }

    internal override bool TryConvertToSet(out EventScriptValue value)
    {
        value = this;
        return true;
    }

    internal override bool TryConvertToDice(out EventScriptValue value) => TryConvertSequenceToDice(AsSet(), out value);

}
