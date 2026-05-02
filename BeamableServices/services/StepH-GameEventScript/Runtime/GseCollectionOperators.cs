#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.Runtime;

internal static class GseCollectionOperators
{
    public static GseValue Sort(GseValue target, IEnumerable<GseValue> items, string direction)
    {
        var comparer = CreateDirectionComparer(direction);
        var sortedItems = items.OrderBy(item => item, comparer).ToArray();
        return MaterializeOrderedResult(target, sortedItems);
    }

    public static GseValue OrderBy(
        GseValue target,
        IEnumerable<GseValue> items,
        string direction,
        Func<GseValue, GseValue> keySelector)
    {
        var comparer = CreateDirectionComparer(direction);
        var orderedItems = items
            .Select(item => (Item: item, Key: keySelector(item)))
            .OrderBy(pair => pair.Key, comparer)
            .Select(pair => pair.Item)
            .ToArray();

        return MaterializeOrderedResult(target, orderedItems);
    }

    public static GseValue Distinct(GseValue target, IEnumerable<GseValue> items)
    {
        var distinctItems = new List<GseValue>();
        var seen = new HashSet<GseValue>();
        foreach (var item in items)
        {
            if (!seen.Add(item))
            {
                continue;
            }

            distinctItems.Add(item);
        }

        return MaterializeDistinctResult(target, distinctItems);
    }

    public static GseValue DistinctBy(
        GseValue target,
        IEnumerable<GseValue> items,
        Func<GseValue, GseValue> keySelector)
    {
        var distinctItems = new List<GseValue>();
        var seenKeys = new HashSet<GseValue>();
        foreach (var item in items)
        {
            var key = keySelector(item);
            if (!seenKeys.Add(key))
            {
                continue;
            }

            distinctItems.Add(item);
        }

        return MaterializeDistinctResult(target, distinctItems);
    }

    public static GseValue GroupBy(
        IEnumerable<GseValue> items,
        Func<GseValue, GseValue> keySelector)
    {
        var groups = new Dictionary<string, List<GseValue>>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            var key = keySelector(item).AsText();
            if (!groups.TryGetValue(key, out var bucket))
            {
                bucket = new List<GseValue>();
                groups[key] = bucket;
            }

            bucket.Add(item);
        }

        return GseValueFactory.Dictionary(groups.ToDictionary(
            pair => pair.Key,
            pair => GseValueFactory.List(pair.Value),
            StringComparer.Ordinal));
    }

    private static IComparer<GseValue> CreateDirectionComparer(string direction)
        => direction == "descending"
            ? Comparer<GseValue>.Create((left, right) => GseValue.StableComparer.Compare(right, left))
            : GseValue.StableComparer;

    private static GseValue MaterializeOrderedResult(GseValue target, IReadOnlyList<GseValue> items)
    {
        return target.Kind switch
        {
            GseValueKind.Dice => GseValueFactory.List(items),
            GseValueKind.List => GseValueFactory.List(items),
            GseValueKind.Set => GseValueFactory.List(items),
            GseValueKind.Range => GseValueFactory.List(items),
            _ => GseValue.Nothing
        };
    }

    private static GseValue MaterializeDistinctResult(GseValue target, IReadOnlyList<GseValue> items)
    {
        return target.Kind switch
        {
            GseValueKind.Set => GseValueFactory.Set(items),
            GseValueKind.List => GseValueFactory.List(items),
            GseValueKind.Dice => GseValueFactory.List(items),
            GseValueKind.Range => GseValueFactory.List(items),
            _ => GseValue.Nothing
        };
    }
}
