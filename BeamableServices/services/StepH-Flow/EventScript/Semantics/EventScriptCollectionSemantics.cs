#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.Flow.EventScript.Types;

namespace StepH.Flow.EventScript.Semantics;

public static class EventScriptCollectionSemantics
{
    public static bool ContainsSingle(EventScriptValue target, IReadOnlyList<EventScriptValue> items, EventScriptValue value)
    {
        if (target.Type == EventScriptValueType.Text)
        {
            return target.AsText().Contains(value.AsText(), StringComparison.Ordinal);
        }

        if (target.Type == EventScriptValueType.Dictionary)
        {
            return target.AsDictionary().ContainsKey(value.AsText());
        }

        return items.Any(item => item.Equals(value));
    }

    public static bool ContainsAll(EventScriptValue target, IReadOnlyList<EventScriptValue> items, EventScriptValue value)
    {
        var required = value.AsList();
        if (target.Type == EventScriptValueType.Text)
        {
            return required.All(item => target.AsText().Contains(item.AsText(), StringComparison.Ordinal));
        }

        if (target.Type == EventScriptValueType.Dictionary)
        {
            return required.All(item => target.AsDictionary().ContainsKey(item.AsText()));
        }

        return required.All(requiredItem => items.Any(item => item.Equals(requiredItem)));
    }

    public static bool ContainsAny(EventScriptValue target, IReadOnlyList<EventScriptValue> items, EventScriptValue value)
    {
        var required = value.AsList();
        if (target.Type == EventScriptValueType.Text)
        {
            return required.Any(item => target.AsText().Contains(item.AsText(), StringComparison.Ordinal));
        }

        if (target.Type == EventScriptValueType.Dictionary)
        {
            return required.Any(item => target.AsDictionary().ContainsKey(item.AsText()));
        }

        return required.Any(requiredItem => items.Any(item => item.Equals(requiredItem)));
    }

    public static EventScriptValue Sort(EventScriptValue target, IEnumerable<EventScriptValue> items, string direction)
    {
        var comparer = CreateDirectionComparer(direction);
        var sortedItems = items.OrderBy(item => item, comparer).ToArray();
        return MaterializeOrderedResult(target, sortedItems);
    }

    public static EventScriptValue OrderBy(
        EventScriptValue target,
        IEnumerable<EventScriptValue> items,
        string direction,
        Func<EventScriptValue, EventScriptValue> keySelector)
    {
        var comparer = CreateDirectionComparer(direction);
        var orderedItems = items
            .Select(item => (Item: item, Key: keySelector(item)))
            .OrderBy(pair => pair.Key, comparer)
            .Select(pair => pair.Item)
            .ToArray();

        return MaterializeOrderedResult(target, orderedItems);
    }

    public static EventScriptValue Distinct(EventScriptValue target, IEnumerable<EventScriptValue> items)
    {
        var distinctItems = new List<EventScriptValue>();
        foreach (var item in items)
        {
            if (distinctItems.Any(existing => existing.Equals(item)))
            {
                continue;
            }

            distinctItems.Add(item);
        }

        return MaterializeDistinctResult(target, distinctItems);
    }

    public static EventScriptValue DistinctBy(
        EventScriptValue target,
        IEnumerable<EventScriptValue> items,
        Func<EventScriptValue, EventScriptValue> keySelector)
    {
        var distinctItems = new List<EventScriptValue>();
        var seenKeys = new List<EventScriptValue>();
        foreach (var item in items)
        {
            var key = keySelector(item);
            if (seenKeys.Any(existing => existing.Equals(key)))
            {
                continue;
            }

            seenKeys.Add(key);
            distinctItems.Add(item);
        }

        return MaterializeDistinctResult(target, distinctItems);
    }

    public static EventScriptValue GroupBy(
        IEnumerable<EventScriptValue> items,
        Func<EventScriptValue, EventScriptValue> keySelector)
    {
        var groups = new Dictionary<string, List<EventScriptValue>>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            var key = keySelector(item).AsText();
            if (!groups.TryGetValue(key, out var bucket))
            {
                bucket = new List<EventScriptValue>();
                groups[key] = bucket;
            }

            bucket.Add(item);
        }

        return EventScriptValue.Dictionary(groups.ToDictionary(
            pair => pair.Key,
            pair => EventScriptValue.List(pair.Value),
            StringComparer.Ordinal));
    }

    private static IComparer<EventScriptValue> CreateDirectionComparer(string direction)
        => direction == "descending"
            ? Comparer<EventScriptValue>.Create((left, right) => EventScriptValue.StableComparer.Compare(right, left))
            : EventScriptValue.StableComparer;

    private static EventScriptValue MaterializeOrderedResult(EventScriptValue target, IReadOnlyList<EventScriptValue> items)
    {
        return target.Type switch
        {
            EventScriptValueType.Dice => EventScriptValue.List(items),
            EventScriptValueType.List => EventScriptValue.List(items),
            EventScriptValueType.Set => EventScriptValue.List(items),
            EventScriptValueType.Range => EventScriptValue.List(items),
            _ => EventScriptValue.Nothing
        };
    }

    private static EventScriptValue MaterializeDistinctResult(EventScriptValue target, IReadOnlyList<EventScriptValue> items)
    {
        return target.Type switch
        {
            EventScriptValueType.Set => EventScriptValue.Set(items),
            EventScriptValueType.List => EventScriptValue.List(items),
            EventScriptValueType.Dice => EventScriptValue.List(items),
            EventScriptValueType.Range => EventScriptValue.List(items),
            _ => EventScriptValue.Nothing
        };
    }
}
