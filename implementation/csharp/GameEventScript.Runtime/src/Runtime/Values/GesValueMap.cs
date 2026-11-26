// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Generic;
using GameEventScript.Api;
using GameEventScript.Syntax;

namespace GameEventScript.Runtime.Values;

/// <summary>
/// The immutable entries of a Map or the fields of a Record, with unique keys in canonical order: Text keys sorted by
/// Unicode scalar values, then the handle keys of a Map in handle order. Indexing and enumeration follow that order;
/// the enumerated key of a handle key is its text form.
/// </summary>
public sealed class GesValueMap : IEnumerable<KeyValuePair<string, GesValue>>
{
    private static readonly string[] EntryKeys = ["key", "value"];

    // The map without entries, which every caller shares.
    internal static GesValueMap Empty { get; } = new([], []);

    private readonly string[] _keys;
    private readonly GesValue[] _values;
    // The handle keys, which follow the Text keys; _keys holds their text forms. Null without handle keys.
    private readonly GesHandle[]? _handles;
    private readonly int _textCount;
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
        _textCount = count;
    }

    // Trusted canonical keys with handle keys: unique Text keys in scalar order, then unique handles in handle order,
    // whose values follow those of the Text keys.
    internal GesValueMap(string[] canonicalKeys, GesHandle[] canonicalHandles, GesValue[] values)
    {
        _textCount = canonicalKeys.Length;
        if (canonicalHandles.Length == 0)
        {
            _keys = canonicalKeys;
        }
        else
        {
            _keys = new string[canonicalKeys.Length + canonicalHandles.Length];
            Array.Copy(canonicalKeys, _keys, canonicalKeys.Length);
            for (var index = 0; index < canonicalHandles.Length; index++) _keys[_textCount + index] = canonicalHandles[index].ToString();
            _handles = canonicalHandles;
        }

        _values = values;
    }

    // Trusted canonical keys: validated, scalar-sorted and unique, and never written to afterwards. A GesMapLayout and
    // an external type definition share one key array between all their maps; the value factories own theirs.
    internal GesValueMap(string[] canonicalKeys, GesValue[] values)
    {
        _keys = canonicalKeys;
        _values = values;
        _textCount = canonicalKeys.Length;
    }

    // Creates a map of Text and handle keys in any order; the last value of an equal key wins. Null when a key is
    // neither a Text nor a handle.
    internal static GesValueMap? FromKeyValues(ReadOnlySpan<GesValue> keys, ReadOnlySpan<GesValue> values)
    {
        var texts = new Dictionary<string, GesValue>(StringComparer.Ordinal);
        var handles = new Dictionary<GesHandle, (GesHandle Key, GesValue Value)>();
        for (var index = 0; index < keys.Length; index++)
        {
            var key = keys[index];
            if (key.ValueKind == GesBytecodeTypeKind.Text)
            {
                GesText.RequireValidUnicode(key.AsText(), nameof(keys));
                texts[key.AsText()] = values[index];
            }
            else if (key.Handle is { } handle)
            {
                handles[handle] = (handle, values[index]);
            }
            else
            {
                return null;
            }
        }

        var textKeys = new string[texts.Count];
        var allValues = new GesValue[texts.Count + handles.Count];
        texts.Keys.CopyTo(textKeys, 0);
        Array.Sort(textKeys, GesText.ScalarComparer);
        for (var index = 0; index < textKeys.Length; index++) allValues[index] = texts[textKeys[index]];
        var handleKeys = new GesHandle[handles.Count];
        handles.Keys.CopyTo(handleKeys, 0);
        Array.Sort(handleKeys);
        for (var index = 0; index < handleKeys.Length; index++)
        {
            var entry = handles[handleKeys[index]];
            handleKeys[index] = entry.Key;
            allValues[textKeys.Length + index] = entry.Value;
        }

        return new GesValueMap(textKeys, handleKeys, allValues);
    }

    /// <summary>
    /// Gets the number of entries.
    /// </summary>
    public int Length => _keys.Length;

    // The number of Text keys, which come first.
    internal int TextKeyCount => _textCount;

    internal bool HasHandleKeys => _handles is not null;

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
    /// Looks up the value of a handle key by type and identity.
    /// </summary>
    /// <param name="key">The handle to look up.</param>
    /// <returns>The stored value, or <see langword="null"/> when the key is absent.</returns>
    public GesValue? Get(GesHandle key)
    {
        var index = FindHandleIndex(key);
        return index >= 0 ? _values[index] : null;
    }

    /// <summary>
    /// Determines whether the map has an entry for a handle key, by type and identity.
    /// </summary>
    /// <param name="key">The handle to look up.</param>
    /// <returns><see langword="true"/> when the key is present.</returns>
    public bool ContainsKey(GesHandle key) => FindHandleIndex(key) >= 0;

    /// <summary>
    /// Gets the handle key at a position in canonical key order.
    /// </summary>
    /// <param name="index">The zero-based position, less than <see cref="Length"/>.</param>
    /// <returns>The handle, or <see langword="null"/> for a Text key.</returns>
    /// <exception cref="IndexOutOfRangeException">Thrown when <paramref name="index"/> is out of range.</exception>
    public GesHandle? HandleKey(int index)
    {
        if ((uint)index >= (uint)_keys.Length) throw new IndexOutOfRangeException();
        return index < _textCount ? null : _handles![index - _textCount];
    }

    // The key at a position as a value: a Text or a handle.
    internal GesValue KeyValueAt(int index)
    {
        var key = new GesValue();
        if (index < _textCount) key.SetText(_keys[index]);
        else key.SetHandle(_handles![index - _textCount]);
        return key;
    }

    // Whether the keys at two positions are equal: Texts by scalars, handles by type and identity.
    internal bool KeyEquals(int index, GesValueMap other, int otherIndex)
    {
        var handle = index < _textCount ? null : _handles![index - _textCount];
        var otherHandle = otherIndex < other._textCount ? null : other._handles![otherIndex - other._textCount];
        if (handle is null || otherHandle is null) return handle is null && otherHandle is null && string.Equals(_keys[index], other._keys[otherIndex], StringComparison.Ordinal);
        return handle.Equals(otherHandle);
    }

    internal void AddKeyHash(ref HashCode hash, int index)
    {
        if (index < _textCount) hash.Add(_keys[index], StringComparer.Ordinal);
        else hash.Add(_handles![index - _textCount]);
    }
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
        var max = _textCount - 1;
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

    private int FindHandleIndex(GesHandle key)
    {
        if (_handles is null) return -1;
        var index = Array.BinarySearch(_handles, key);
        return index >= 0 ? _textCount + index : -1;
    }

    private GesValue[] CreateListOfKeys()
    {
        var list = new GesValue[_keys.Length];
        for (var index = 0; index < _keys.Length; index++)
        {
            list[index] = KeyValueAt(index);
        }
        return list;
    }

    private GesValue[] CreateListOfEntries()
    {
        var list = new GesValue[_keys.Length];
        for (var index = 0; index < _keys.Length; index++)
        {
            var entryValues = new GesValue[2];
            entryValues[0] = KeyValueAt(index);
            entryValues[1] = _values[index];
            list[index].SetMap(new GesValueMap(EntryKeys, entryValues, 2));
        }

        return list;
    }

}
