#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.Runtime;

internal static class GameEventScriptCollectionOperators
{
    public static GameEventScriptValue Sort(GameEventScriptValue target, IEnumerable<GameEventScriptValue> items, string direction)
    {
        var comparer = CreateDirectionComparer(direction);
        var sortedItems = items.OrderBy(item => item, comparer).ToArray();
        return MaterializeOrderedResult(target, sortedItems);
    }

    public static GameEventScriptValue OrderBy(
        GameEventScriptValue target,
        IEnumerable<GameEventScriptValue> items,
        string direction,
        Func<GameEventScriptValue, GameEventScriptValue> keySelector)
    {
        var comparer = CreateDirectionComparer(direction);
        var orderedItems = items
            .Select(item => (Item: item, Key: keySelector(item)))
            .OrderBy(pair => pair.Key, comparer)
            .Select(pair => pair.Item)
            .ToArray();

        return MaterializeOrderedResult(target, orderedItems);
    }

    public static GameEventScriptValue Distinct(GameEventScriptValue target, IEnumerable<GameEventScriptValue> items)
    {
        var distinctItems = new List<GameEventScriptValue>();
        var seen = new HashSet<GameEventScriptValue>();
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

    public static GameEventScriptValue DistinctBy(
        GameEventScriptValue target,
        IEnumerable<GameEventScriptValue> items,
        Func<GameEventScriptValue, GameEventScriptValue> keySelector)
    {
        var distinctItems = new List<GameEventScriptValue>();
        var seenKeys = new HashSet<GameEventScriptValue>();
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

    public static GameEventScriptValue GroupBy(
        IEnumerable<GameEventScriptValue> items,
        Func<GameEventScriptValue, GameEventScriptValue> keySelector)
    {
        var groups = new Dictionary<string, List<GameEventScriptValue>>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            var key = keySelector(item).AsText();
            if (!groups.TryGetValue(key, out var bucket))
            {
                bucket = new List<GameEventScriptValue>();
                groups[key] = bucket;
            }

            bucket.Add(item);
        }

        return GameEventScriptValueFactory.GseDictionary(groups.ToDictionary(
            pair => pair.Key,
            pair => GameEventScriptValueFactory.GesList(pair.Value),
            StringComparer.Ordinal));
    }

    private static IComparer<GameEventScriptValue> CreateDirectionComparer(string direction)
        => direction == "descending"
            ? Comparer<GameEventScriptValue>.Create((left, right) => GameEventScriptValue.StableComparer.Compare(right, left))
            : GameEventScriptValue.StableComparer;

    private static GameEventScriptValue MaterializeOrderedResult(GameEventScriptValue target, IReadOnlyList<GameEventScriptValue> items)
    {
        return target.Kind switch
        {
            GameEventScriptValueKind.Dice => GameEventScriptValueFactory.GesList(items),
            GameEventScriptValueKind.List => GameEventScriptValueFactory.GesList(items),
            GameEventScriptValueKind.Set => GameEventScriptValueFactory.GesList(items),
            GameEventScriptValueKind.Range => GameEventScriptValueFactory.GesList(items),
            _ => GameEventScriptValue.Nothing
        };
    }

    private static GameEventScriptValue MaterializeDistinctResult(GameEventScriptValue target, IReadOnlyList<GameEventScriptValue> items)
    {
        return target.Kind switch
        {
            GameEventScriptValueKind.Set => GameEventScriptValueFactory.GseSet(items),
            GameEventScriptValueKind.List => GameEventScriptValueFactory.GesList(items),
            GameEventScriptValueKind.Dice => GameEventScriptValueFactory.GesList(items),
            GameEventScriptValueKind.Range => GameEventScriptValueFactory.GesList(items),
            _ => GameEventScriptValue.Nothing
        };
    }
}
