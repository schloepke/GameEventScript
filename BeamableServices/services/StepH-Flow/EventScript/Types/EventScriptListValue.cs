#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using static StepH.Flow.EventScript.Types.EventScriptValueFactory;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace StepH.Flow.EventScript.Types;

public sealed class EventScriptListValue : EventScriptValue
{
    private static readonly ReadOnlyCollection<EventScriptValue> EmptyItems = new(Array.Empty<EventScriptValue>());

    public static readonly EventScriptListValue Empty = new(EmptyItems);

    public static EventScriptListValue EventScriptList(IEnumerable<EventScriptValue>? values)
    {
        if (values == null) return Empty;
        var list = values.Select(value => value ?? Nothing).ToArray();
        return list.Length == 0 ? Empty : new EventScriptListValue(new ReadOnlyCollection<EventScriptValue>(list));
    }

    private EventScriptListValue(ReadOnlyCollection<EventScriptValue> items)
    {
        Items = items;
    }

    internal ReadOnlyCollection<EventScriptValue> Items { get; }
    public override EventScriptValueKind Kind => EventScriptValueKind.List;

    public override IReadOnlyList<EventScriptValue> AsList() => Items;

    public override ISet<EventScriptValue> AsSet() => TryConvertToSet(out var value) ? value.AsSet() : new SortedSet<EventScriptValue>(StableComparer);

    public override EventScriptDiceValue AsDice() => TryConvertToDice(out var value) ? value.AsDice() : EventScriptDiceValue.Empty;

    public override IEnumerable<EventScriptValue> AsEnumerable() => Items;

    internal override bool TryConvertToList(out EventScriptValue value)
    {
        value = this;
        return true;
    }

    internal override bool TryConvertToSet(out EventScriptValue value)
    {
        value = Set(Items);
        return true;
    }

    internal override bool TryConvertToDice(out EventScriptValue value) => TryConvertSequenceToDice(Items, out value);

}
