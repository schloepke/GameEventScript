// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Generic;

namespace StepH.GameEventScript.Conformance;

internal sealed class ConformanceReadOnlyDictionary<TValue> : IReadOnlyDictionary<string, TValue>
{
    private readonly Dictionary<string, TValue> _items;

    internal ConformanceReadOnlyDictionary(IReadOnlyDictionary<string, TValue> items)
        => _items = new Dictionary<string, TValue>(items, StringComparer.Ordinal);

    public int Count => _items.Count;
    public TValue this[string key] => _items[key];

    // Dictionary key/value collections also expose the dictionary through ICollection.SyncRoot.
    public IEnumerable<string> Keys
    {
        get { foreach (var item in _items) yield return item.Key; }
    }

    public IEnumerable<TValue> Values
    {
        get { foreach (var item in _items) yield return item.Value; }
    }

    public bool ContainsKey(string key) => _items.ContainsKey(key);
    public bool TryGetValue(string key, out TValue value) => _items.TryGetValue(key, out value!);
    public IEnumerator<KeyValuePair<string, TValue>> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
