using System;

namespace StepH.GameEventScript.VirtualMachine;

internal sealed class GesVmValueMap
{
    internal const string HiddenRecordTypeField = "__type";

    private readonly GesVmState _ownerState;
    private readonly string[] _keys;
    private readonly GesVmValue[] _values;
    private readonly int _length;
    private GesVmValue[]? _keyList;
    private GesVmValue[]? _valueList;
    private GesVmValue[]? _entries;

    internal GesVmValueMap(GesVmState ownerState, string[] keys, GesVmValue[] values, int count)
    {
        _ownerState = ownerState;
        _keys = new string[count];
        _values = ownerState.CreateRegisterArray(count);

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
        var list = _ownerState.CreateList(_length);
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
        var list = _ownerState.CreateList(_length);
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
        var list = _ownerState.CreateList(_length);
        var i = 0;
        for (var index = 0; index < _keys.Length; index++)
        {
            var key = _keys[index];
            if (key.StartsWith("_", StringComparison.Ordinal)) continue;
            var entry = new GesVmValueMapBuilder(_ownerState, 2);
            entry.Set("key", _ownerState.CreateTag(key));
            entry.Set("value", _values[index]);
            list[i++].SetMap(entry.ToMap());
        }

        return list;
    }

}

internal sealed class GesVmValueMapBuilder
{
    private readonly GesVmState _ownerState;
    private string[] _keys;
    private GesVmValue[] _values;
    private int _count;

    internal GesVmValueMapBuilder(GesVmState ownerState, int capacity = 0)
    {
        _ownerState = ownerState;
        var size = capacity <= 0 ? 4 : capacity;
        _keys = new string[size];
        _values = ownerState.CreateRegisterArray(size);
    }

    internal int Count => _count;

    internal bool ContainsKey(string key)
    {
        for (var i = 0; i < _count; i++)
        {
            if (string.Equals(_keys[i], key, StringComparison.Ordinal)) return true;
        }

        return false;
    }

    internal void Set(string key, GesVmValue value)
    {
        for (var i = 0; i < _count; i++)
        {
            if (!string.Equals(_keys[i], key, StringComparison.Ordinal)) continue;
            _values[i] = value;
            return;
        }

        if (_count == _keys.Length)
        {
            var nextKeys = new string[_keys.Length << 1];
            var nextValues = _ownerState.CreateRegisterArray(_values.Length << 1);
            Array.Copy(_keys, nextKeys, _keys.Length);
            Array.Copy(_values, nextValues, _values.Length);
            _keys = nextKeys;
            _values = nextValues;
        }

        _keys[_count] = key;
        _values[_count] = value;
        _count++;
    }

    internal GesVmValueMap ToMap() => new(_ownerState, _keys, _values, _count);
}
