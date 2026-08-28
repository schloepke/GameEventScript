using System;
using StepH.GameEventScript.Runtime.Values;

namespace StepH.GameEventScript.Runtime.VM;

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

    internal void Set(string key, GesValue value)
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

internal sealed class GesVmTableBuilder
{
    private readonly GesTableShape _shape;
    private readonly ulong[][] _columns;
    private readonly int[] _columnLengths;
    private int _rowCount;

    public GesVmTableBuilder(GesTableShape shape, int capacity = 0)
    {
        if (capacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _shape = shape;
        _columns = new ulong[shape.ColumnCount][];
        _columnLengths = new int[shape.ColumnCount];

        for (var i = 0; i < _columns.Length; i++)
        {
            var column = shape.Columns[i];
            var width = checked((int)GesTableData.GetColumnWidth(column.Kind));
            var size = capacity <= 0 ? width * 4 : capacity * width;
            _columns[i] = new ulong[size];
        }
    }

    public int RowCount => _rowCount;
    public int ColumnCount => _shape.ColumnCount;

    public static GesVmTableBuilder List(GesTableColumnKind itemKind, int capacity = 0, ushort valueNameIndex = 0) =>
        new(GesTableShape.List(itemKind, valueNameIndex), capacity);

    public static GesVmTableBuilder KeyTable(GesTableColumnKind keyKind, int capacity = 0, ushort keyNameIndex = 0) =>
        new(GesTableShape.KeyTable(keyKind, keyNameIndex), capacity);

    public static GesVmTableBuilder Map(GesTableColumnKind keyKind, GesTableColumnKind valueKind, int capacity = 0, ushort keyNameIndex = 0, ushort valueNameIndex = 1) =>
        new(GesTableShape.Map(keyKind, valueKind, keyNameIndex, valueNameIndex), capacity);

    public int? AddRow(GesTableCell[] cells)
    {
        if (cells.Length != _shape.ColumnCount)
        {
            return null;
        }

        for (var i = 0; i < cells.Length; i++)
        {
            if (cells[i].Kind != _shape.Columns[i].Kind || !CanWrite(i, cells[i]))
            {
                return null;
            }
        }

        var rowIndex = _rowCount++;
        for (var i = 0; i < cells.Length; i++)
        {
            Write(i, cells[i], rowIndex);
        }

        return rowIndex;
    }

    public GesTable ToTable() => new(ToData());

    public GesTableData ToData()
    {
        var columns = new GesTableColumnSegment[_shape.ColumnCount];
        var dataLength = 0;
        for (var i = 0; i < _columns.Length; i++)
        {
            dataLength += _columnLengths[i];
        }

        var data = new ulong[dataLength];
        var offset = 0;
        for (var i = 0; i < _columns.Length; i++)
        {
            var source = _columns[i];
            Array.Copy(source, 0, data, offset, _columnLengths[i]);
            columns[i] = new GesTableColumnSegment(
                _shape.Columns[i].NameIndex,
                _shape.Columns[i].Kind,
                _shape.Columns[i].Flags,
                checked((uint)offset),
                checked((uint)_rowCount));
            offset += _columnLengths[i];
        }

        return new GesTableData(columns, data, checked((uint)_rowCount));
    }

    private bool CanWrite(int columnIndex, in GesTableCell cell)
    {
        if (!_shape.Columns[columnIndex].RequiresIndex)
        {
            return true;
        }

        var data = _columns[columnIndex];
        var length = _columnLengths[columnIndex];
        for (var i = 0; i < length; i++)
        {
            if (data[i] == cell.A) return false;
        }

        return true;
    }

    private void Write(int columnIndex, in GesTableCell cell, int rowIndex)
    {
        var target = _columns[columnIndex];
        var length = _columnLengths[columnIndex];
        if (length == target.Length)
        {
            var resized = new ulong[target.Length << 1];
            Array.Copy(target, resized, target.Length);
            target = resized;
            _columns[columnIndex] = target;
        }

        target[length] = cell.A;
        _columnLengths[columnIndex] = length + 1;
    }
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

    internal void Add(string key, GesValue value)
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

    internal void Add(in GesValue key, in GesValue value)
    {
        for (var i = 0; i < _count; i++)
        {
            var existing = _keys[i];
            var candidate = key;
            if (existing.EqualsValue(in candidate)) return;
        }

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

    internal void Add(in GesValue key, in GesValue value)
    {
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
