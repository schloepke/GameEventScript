// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Runtime.VM;

internal sealed class GesVmListBuilder
{
    private GesValue[] _items;

    internal GesVmListBuilder(int capacity = 0)
    {
        _items = new GesValue[capacity <= 0 ? 4 : capacity];
    }

    internal int Count { get; private set; }

    internal GesValue this[int index] => _items[index];
    internal GesValue[] Items => _items;

    internal void Add(in GesValue value)
    {
        if (Count == _items.Length)
        {
            var resized = new GesValue[_items.Length << 1];
            Array.Copy(_items, resized, _items.Length);
            _items = resized;
        }

        _items[Count++] = value;
    }

    internal GesValue[] ToList()
    {
        if (Count == 0) return [];

        var list = new GesValue[Count];
        Array.Copy(_items, list, Count);
        return list;
    }

    internal GesValue[] ToList(int start, int length)
    {
        if (length <= 0) return [];

        var list = new GesValue[length];
        Array.Copy(_items, start, list, 0, length);
        return list;
    }
}

internal sealed class GesVmMapBuilder
{
    private string[] _keys;
    private GesValue[] _values;
    private int _count;

    internal GesVmMapBuilder(int capacity = 0)
    {
        var size = capacity <= 0 ? 4 : capacity;
        _keys = new string[size];
        _values = new GesValue[size];
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

    internal void Set(string key, GesValue value, GesRuntimeBudget? budget = null)
    {
        for (var i = 0; i < _count; i++)
        {
            if (!string.Equals(_keys[i], key, StringComparison.Ordinal)) continue;
            _values[i] = value;
            return;
        }

        if (budget is not null && !budget.CheckGeneratedCollectionItemCountWithinLimit((long)_count + 1)) return;

        if (_count == _keys.Length)
        {
            var nextKeys = new string[_keys.Length << 1];
            var nextValues = new GesValue[_values.Length << 1];
            Array.Copy(_keys, nextKeys, _keys.Length);
            Array.Copy(_values, nextValues, _values.Length);
            _keys = nextKeys;
            _values = nextValues;
        }

        _keys[_count] = key;
        _values[_count] = value;
        _count++;
    }

    internal GesValueMap ToMap() => new(_keys, _values, _count);
}

internal sealed class GesVmGroupBuilder
{
    private readonly GesVmState _state;
    private string[] _keys;
    private GesValue[][] _buckets;
    private int[] _counts;
    private int _count;

    internal GesVmGroupBuilder(GesVmState state, int capacity = 0)
    {
        _state = state;
        var size = capacity <= 0 ? 4 : capacity;
        _keys = new string[size];
        _buckets = new GesValue[size][];
        _counts = new int[size];
    }

    internal void Add(string key, GesValue value, GesRuntimeBudget budget)
    {
        var groupIndex = -1;
        for (var i = 0; i < _count; i++)
        {
            if (!string.Equals(_keys[i], key, StringComparison.Ordinal)) continue;
            groupIndex = i;
            break;
        }

        if (groupIndex < 0)
        {
            if (!budget.CheckGeneratedCollectionItemCountWithinLimit((long)_count + 1)) return;
            if (_count == _keys.Length)
            {
                var nextSize = _keys.Length << 1;
                var nextKeys = new string[nextSize];
                var nextBuckets = new GesValue[nextSize][];
                var nextCounts = new int[nextSize];
                Array.Copy(_keys, nextKeys, _keys.Length);
                Array.Copy(_buckets, nextBuckets, _buckets.Length);
                Array.Copy(_counts, nextCounts, _counts.Length);
                _keys = nextKeys;
                _buckets = nextBuckets;
                _counts = nextCounts;
            }

            groupIndex = _count++;
            _keys[groupIndex] = key;
            _buckets[groupIndex] = new GesValue[4];
        }

        var bucket = _buckets[groupIndex];
        var itemCount = _counts[groupIndex];
        if (!budget.CheckGeneratedCollectionItemCountWithinLimit((long)itemCount + 1)) return;
        if (itemCount == bucket.Length)
        {
            var resized = new GesValue[bucket.Length << 1];
            Array.Copy(bucket, resized, bucket.Length);
            bucket = resized;
            _buckets[groupIndex] = bucket;
        }

        bucket[itemCount] = value;
        _counts[groupIndex] = itemCount + 1;
    }

    internal GesValue ToMapValue()
    {
        var map = new GesVmMapBuilder(_count);
        for (var groupIndex = 0; groupIndex < _count; groupIndex++)
        {
            var itemCount = _counts[groupIndex];
            var groupedList = new GesValue[itemCount];
            for (var i = 0; i < itemCount; i++) groupedList[i] = _buckets[groupIndex][i];
            var groupedValue = new GesValue();
            groupedValue.SetList(groupedList);
            map.Set(_keys[groupIndex], groupedValue);
        }

        var result = new GesValue();
        result.SetMap(map.ToMap());
        return result;
    }
}

internal sealed class GesVmDistinctBuilder
{
    private GesValue[] _keys;
    private GesValue[] _values;
    private int _count;

    internal GesVmDistinctBuilder(int capacity = 0)
    {
        var size = capacity <= 0 ? 4 : capacity;
        _keys = new GesValue[size];
        _values = new GesValue[size];
    }

    internal void Add(in GesValue key, in GesValue value, GesRuntimeBudget budget)
    {
        for (var i = 0; i < _count; i++)
        {
            var existing = _keys[i];
            var candidate = key;
            if (existing.EqualsValue(in candidate)) return;
        }

        if (!budget.CheckGeneratedCollectionItemCountWithinLimit((long)_count + 1)) return;

        if (_count == _keys.Length)
        {
            var nextSize = _keys.Length << 1;
            var nextKeys = new GesValue[nextSize];
            var nextValues = new GesValue[nextSize];
            Array.Copy(_keys, nextKeys, _keys.Length);
            Array.Copy(_values, nextValues, _values.Length);
            _keys = nextKeys;
            _values = nextValues;
        }

        _keys[_count] = key;
        _values[_count] = value;
        _count++;
    }

    internal GesValue[] ToList()
    {
        if (_count == 0) return [];
        var result = new GesValue[_count];
        for (var i = 0; i < _count; i++) result[i] = _values[i];
        return result;
    }
}

internal sealed class GesVmOrderBuilder
{
    private GesValue[] _keys;
    private GesValue[] _values;
    private int _count;

    internal GesVmOrderBuilder(int capacity = 0)
    {
        var size = capacity <= 0 ? 4 : capacity;
        _keys = new GesValue[size];
        _values = new GesValue[size];
    }

    internal void Add(in GesValue key, in GesValue value, GesRuntimeBudget budget)
    {
        if (!budget.CheckGeneratedCollectionItemCountWithinLimit((long)_count + 1)) return;
        if (_count == _keys.Length)
        {
            var nextSize = _keys.Length << 1;
            var nextKeys = new GesValue[nextSize];
            var nextValues = new GesValue[nextSize];
            Array.Copy(_keys, nextKeys, _keys.Length);
            Array.Copy(_values, nextValues, _values.Length);
            _keys = nextKeys;
            _values = nextValues;
        }

        _keys[_count] = key;
        _values[_count] = value;
        _count++;
    }

    internal GesValue[]? ToList(bool descending)
    {
        if (_count == 0)
        {
            return [];
        }

        var keys = new GesValue[_count];
        var values = new GesValue[_count];
        for (var i = 0; i < _count; i++)
        {
            keys[i] = _keys[i];
            values[i] = _values[i];
        }

        if (!GesVmRegisterSortGroupDistinct.SortValuesByKeys(values, keys, _count, descending))
        {
            return null;
        }

        return values;
    }
}
