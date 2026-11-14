// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Generic;
using GameEventScript.Syntax;

namespace GameEventScript.Runtime.Values;

/// <summary>
/// The immutable Text-keyed entries of a Map or the fields of a Record, with unique keys sorted by Unicode scalar
/// values; indexing and enumeration follow that canonical order.
/// </summary>
public sealed class GesValueMap : IEnumerable<KeyValuePair<string, GesValue>>
{
    private static readonly string[] EntryKeys = ["key", "value"];

    private readonly string[] _keys;
    private readonly GesValue[] _values;
    private GesValue[]? _keyList;
    private GesValue[]? _entries;

    internal GesValueMap(string[] keys, GesValue[] values, int count)
    {
        _keys = new string[count];
        _values = new GesValue[count];

        Array.Copy(keys, _keys, count);
        Array.Copy(values, _values, count);
        for (var index = 0; index < _keys.Length; index++)
            GesText.RequireValidUnicode(_keys[index], nameof(keys));
        Array.Sort(_keys, _values, GesText.ScalarComparer);
    }

    // Trusted canonical keys: validated, scalar-sorted and unique, and never written to afterwards. A GesMapLayout and
    // an external type definition share one key array between all their maps; the value factories own theirs.
    internal GesValueMap(string[] canonicalKeys, GesValue[] values)
    {
        _keys = canonicalKeys;
        _values = values;
    }

    /// <summary>
    /// Gets the number of entries.
    /// </summary>
    public int Length => _keys.Length;

    internal GesValue[] KeyList => _keyList ??= CreateListOfKeys();
    // Values are never mutated after construction, so value lists and iterators share the storage array.
    internal GesValue[] ValueList => _values;
    internal GesValue[] EntryList => _entries ??= CreateListOfEntries();

    /// <summary>
    /// Looks up the value of a key by binary search, matching Unicode scalars exactly.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <returns>The stored value, or <see langword="null"/> when the key is absent.</returns>
    public GesValue? Get(string key)
    {
        var index = FindKeyIndex(key);
        if (index >= 0)
        {
            return _values[index];
        }

        return null;
    }

    /// <summary>
    /// Determines whether the map has an entry for a key, matching Unicode scalars exactly.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <returns><see langword="true"/> when the key is present.</returns>
    public bool ContainsKey(string key) => FindKeyIndex(key) >= 0;
    /// <summary>
    /// Gets the key at a position in canonical key order.
    /// </summary>
    /// <param name="index">The zero-based position, less than <see cref="Length"/>.</param>
    /// <returns>The key.</returns>
    /// <exception cref="IndexOutOfRangeException">Thrown when <paramref name="index"/> is out of range.</exception>
    public string KeyAt(int index) => _keys[index];
    /// <summary>
    /// Gets the value at a position in canonical key order.
    /// </summary>
    /// <param name="index">The zero-based position, less than <see cref="Length"/>.</param>
    /// <returns>The value.</returns>
    /// <exception cref="IndexOutOfRangeException">Thrown when <paramref name="index"/> is out of range.</exception>
    public GesValue ValueAt(int index) => _values[index];

    /// <summary>
    /// Returns an allocation-free enumerator over the entries in canonical key order.
    /// </summary>
    /// <returns>The enumerator.</returns>
    public Enumerator GetEnumerator() => new(this);

    IEnumerator<KeyValuePair<string, GesValue>> IEnumerable<KeyValuePair<string, GesValue>>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Enumerates the entries of a <see cref="GesValueMap"/> in canonical key order.
    /// </summary>
    public struct Enumerator : IEnumerator<KeyValuePair<string, GesValue>>
    {
        private readonly GesValueMap _map;
        private int _index;

        internal Enumerator(GesValueMap map)
        {
            _map = map;
            _index = -1;
        }

        /// <summary>
        /// Gets the entry at the current position.
        /// </summary>
        public readonly KeyValuePair<string, GesValue> Current => new(_map._keys[_index], _map._values[_index]);

        readonly object IEnumerator.Current => Current;

        /// <summary>
        /// Advances to the next entry.
        /// </summary>
        /// <returns><see langword="true"/> when another entry exists.</returns>
        public bool MoveNext() => ++_index < _map._keys.Length;

        /// <summary>
        /// Returns to the position before the first entry.
        /// </summary>
        public void Reset() => _index = -1;

        /// <summary>
        /// Releases nothing; the enumerator owns no resources.
        /// </summary>
        public readonly void Dispose()
        {
        }
    }

    private int FindKeyIndex(string key)
    {
        var min = 0;
        var max = _keys.Length - 1;
        while (min <= max)
        {
            var mid = min + ((max - min) >> 1);
            var comparison = GesText.CompareScalarOrdinal(_keys[mid], key);
            if (comparison == 0) return mid;
            if (comparison < 0) min = mid + 1;
            else max = mid - 1;
        }

        return -1;
    }

    private GesValue[] CreateListOfKeys()
    {
        var list = new GesValue[_keys.Length];
        for (var index = 0; index < _keys.Length; index++)
        {
            list[index].SetText(_keys[index]);
        }
        return list;
    }

    private GesValue[] CreateListOfEntries()
    {
        var list = new GesValue[_keys.Length];
        for (var index = 0; index < _keys.Length; index++)
        {
            var key = _keys[index];
            var entryValues = new GesValue[2];
            entryValues[0].SetText(key);
            entryValues[1] = _values[index];
            list[index].SetMap(new GesValueMap(EntryKeys, entryValues, 2));
        }

        return list;
    }

}
