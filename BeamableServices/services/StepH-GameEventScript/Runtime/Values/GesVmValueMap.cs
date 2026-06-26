using System;

namespace StepH.GameEventScript.Runtime.Values;

internal sealed class GesVmValueMap
{
    private static readonly string[] EntryKeys = ["key", "value"];

    private readonly string[] _keys;
    private readonly GesVmValue[] _values;
    private readonly int _length;
    private GesVmValue[]? _keyList;
    private GesVmValue[]? _valueList;
    private GesVmValue[]? _entries;

    internal GesVmValueMap(string[] keys, GesVmValue[] values, int count)
    {
        _keys = new string[count];
        _values = new GesVmValue[count];

        Array.Copy(keys, _keys, count);
        Array.Copy(values, _values, count);
        Array.Sort(_keys, _values, StringComparer.Ordinal);

        var visibleLength = 0;
        for (var i = 0; i < _keys.Length; i++)
        {
            if (!_keys[i].StartsWith("_", StringComparison.Ordinal)) visibleLength++;
        }

        _length = visibleLength;
    }
    
    public int Length => _length;
    internal int StorageLength => _keys.Length;
    internal bool HasHiddenEntries => _length != _keys.Length;
    
    internal GesVmValue[] KeyList => _keyList ??= CreateListOfKeys();
    internal GesVmValue[] ValueList => _valueList ??= CreateListOfValues();
    internal GesVmValue[] EntryList => _entries ??= CreateListOfEntries();

    public bool TryGet(string key, out GesVmValue value)
    {
        var index = FindKeyIndex(key);
        if (index >= 0)
        {
            value = _values[index];
            return true;
        }

        value = default;
        return false;
    }

    internal bool ContainsKey(string key) => FindKeyIndex(key) >= 0;
    internal string KeyAt(int index) => _keys[index];
    internal GesVmValue ValueAt(int index) => _values[index];
    internal ref GesVmValue ValueRefAt(int index) => ref _values[index];
    internal bool IsVisibleAt(int index) => !_keys[index].StartsWith("_", StringComparison.Ordinal);

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

    private GesVmValue[] CreateListOfKeys()
    {
        var list = new GesVmValue[_length];
        var i = 0;
        for (var index = 0; index < _keys.Length; index++)
        {
            var key = _keys[index];
            if (key.StartsWith("_", StringComparison.Ordinal)) continue;
            list[i++].SetTag(key);
        }
        return list;
    }

    private GesVmValue[] CreateListOfValues()
    {
        var list = new GesVmValue[_length];
        var i = 0;
        for (var index = 0; index < _keys.Length; index++)
        {
            if (_keys[index].StartsWith("_", StringComparison.Ordinal)) continue;
            list[i++] = _values[index];
        }
        return list;
    }

    private GesVmValue[] CreateListOfEntries()
    {
        var list = new GesVmValue[_length];
        var i = 0;
        for (var index = 0; index < _keys.Length; index++)
        {
            var key = _keys[index];
            if (key.StartsWith("_", StringComparison.Ordinal)) continue;
            var entryValues = new GesVmValue[2];
            entryValues[0].SetTag(key);
            entryValues[1] = _values[index];
            list[i++].SetMap(new GesVmValueMap(EntryKeys, entryValues, 2));
        }

        return list;
    }

}
