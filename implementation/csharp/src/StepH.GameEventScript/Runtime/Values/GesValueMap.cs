// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;

namespace StepH.GameEventScript.Runtime.Values;

/// <summary>
/// Represents a ges value map.
/// </summary>
public sealed class GesValueMap
{
    private static readonly string[] EntryKeys = ["key", "value"];

    private readonly string[] _keys;
    private readonly GesValue[] _values;
    private GesValue[]? _keyList;
    private GesValue[]? _valueList;
    private GesValue[]? _entries;

    internal GesValueMap(string[] keys, GesValue[] values, int count)
    {
        _keys = new string[count];
        _values = new GesValue[count];

        Array.Copy(keys, _keys, count);
        Array.Copy(values, _values, count);
        for (var index = 0; index < _keys.Length; index++)
            GameEventScriptText.RequireValidUnicode(_keys[index], nameof(keys));
        Array.Sort(_keys, _values, GameEventScriptText.ScalarComparer);
    }

    /// <summary>
    /// Gets the length.
    /// </summary>
    public int Length => _keys.Length;
    /// <summary>
    /// Gets the storage length.
    /// </summary>
    public int StorageLength => _keys.Length;

    internal GesValue[] KeyList => _keyList ??= CreateListOfKeys();
    internal GesValue[] ValueList => _valueList ??= CreateListOfValues();
    internal GesValue[] EntryList => _entries ??= CreateListOfEntries();

    /// <summary>
    /// Gets the value.
    /// </summary>
    /// <param name="key">The key value.</param>
    /// <returns>The result of the operation.</returns>
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
    /// Performs the contains key operation.
    /// </summary>
    /// <param name="key">The key value.</param>
    /// <returns>The result of the operation.</returns>
    public bool ContainsKey(string key) => FindKeyIndex(key) >= 0;
    /// <summary>
    /// Performs the key at operation.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
    public string KeyAt(int index) => _keys[index];
    /// <summary>
    /// Performs the value at operation.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
    public GesValue ValueAt(int index) => _values[index];

    private int FindKeyIndex(string key)
    {
        var min = 0;
        var max = _keys.Length - 1;
        while (min <= max)
        {
            var mid = min + ((max - min) >> 1);
            var comparison = GameEventScriptText.CompareScalarOrdinal(_keys[mid], key);
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
            list[index].SetTag(_keys[index]);
        }
        return list;
    }

    private GesValue[] CreateListOfValues()
    {
        var list = new GesValue[_values.Length];
        for (var index = 0; index < _keys.Length; index++)
        {
            list[index] = _values[index];
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
            entryValues[0].SetTag(key);
            entryValues[1] = _values[index];
            list[index].SetMap(new GesValueMap(EntryKeys, entryValues, 2));
        }

        return list;
    }

}
