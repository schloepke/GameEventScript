// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections;

namespace GameEventScript.Tests;

internal static class CollectionMutationProbe
{
    internal static void ReplaceFirstIfWritable<T>(IReadOnlyList<T> view, T replacement)
    {
        Assert.IsGreaterThan(0, view.Count);
        Attempt(() => { if (view is IList<T> list) list[0] = replacement; });
        Attempt(() => { if (view is IList list) list[0] = replacement; });
        Attempt(() => { if (view is ICollection { SyncRoot: IList storage }) storage[0] = replacement; });
        var copy = view.ToArray();
        copy[0] = replacement;
    }

    internal static void ReplaceIfWritable<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> view, TKey key, TValue replacement) where TKey : notnull
    {
        Attempt(() => { if (view is IDictionary<TKey, TValue> dictionary) dictionary[key] = replacement; });
        Attempt(() => { if (view is IDictionary dictionary) dictionary[key] = replacement; });
        Attempt(() => { if (view is ICollection { SyncRoot: IDictionary storage }) storage[key] = replacement; });
        Attempt(() => { if (view.Keys is ICollection { SyncRoot: IDictionary storage }) storage[key] = replacement; });
        Attempt(() => { if (view.Values is ICollection { SyncRoot: IDictionary storage }) storage[key] = replacement; });
    }

    private static void Attempt(Action mutation)
    {
        try { mutation(); }
        catch (NotSupportedException) { }
    }
}
