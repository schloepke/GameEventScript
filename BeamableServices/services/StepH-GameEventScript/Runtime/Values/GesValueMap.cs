using System;

namespace StepH.GameEventScript.Runtime.Values;

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
        Array.Sort(_keys, _values, StringComparer.Ordinal);
    }
    
    public int Length => _keys.Length;
    public int StorageLength => _keys.Length;
    
    internal GesValue[] KeyList => _keyList ??= CreateListOfKeys();
    internal GesValue[] ValueList => _valueList ??= CreateListOfValues();
    internal GesValue[] EntryList => _entries ??= CreateListOfEntries();

    public GesValue? Get(string key)
    {
        var index = FindKeyIndex(key);
        if (index >= 0)
        {
            return _values[index];
        }

        return null;
    }

    public bool ContainsKey(string key) => FindKeyIndex(key) >= 0;
    public string KeyAt(int index) => _keys[index];
    public GesValue ValueAt(int index) => _values[index];

    private int FindKeyIndex(string key)
    {
        var min = 0;
        var max = _keys.Length - 1;
        while (min <= max)
        {
            var mid = min + ((max - min) >> 1);
            var comparison = string.CompareOrdinal(_keys[mid], key);
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
