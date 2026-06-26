using System;
using StepH.GameEventScript.Runtime.Values;

namespace StepH.GameEventScript.Runtime.VM;

internal sealed class GesVmValueListBuilder
{
    private GesVmValue[] _items;

    internal GesVmValueListBuilder(int capacity = 0)
    {
        _items = new GesVmValue[capacity <= 0 ? 4 : capacity];
    }

    internal int Count { get; private set; }

    internal void Add(in GesVmValue value)
    {
        if (Count == _items.Length)
        {
            var resized = new GesVmValue[_items.Length << 1];
            Array.Copy(_items, resized, _items.Length);
            _items = resized;
        }

        _items[Count++] = value;
    }

    internal GesVmValue[] ToList()
    {
        if (Count == 0) return [];

        var list = new GesVmValue[Count];
        Array.Copy(_items, list, Count);
        return list;
    }
}

internal sealed class GesVmValueMapBuilder
{
    private string[] _keys;
    private GesVmValue[] _values;
    private int _count;

    internal GesVmValueMapBuilder(int capacity = 0)
    {
        var size = capacity <= 0 ? 4 : capacity;
        _keys = new string[size];
        _values = new GesVmValue[size];
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
            var nextValues = new GesVmValue[_values.Length << 1];
            Array.Copy(_keys, nextKeys, _keys.Length);
            Array.Copy(_values, nextValues, _values.Length);
            _keys = nextKeys;
            _values = nextValues;
        }

        _keys[_count] = key;
        _values[_count] = value;
        _count++;
    }

    internal GesVmValueMap ToMap() => new(_keys, _values, _count);
}

internal sealed class GesVmTableBuilder
{
    private readonly GesVmTableShape _shape;
    private readonly ulong[][] _columns;
    private readonly int[] _columnLengths;
    private int _rowCount;

    public GesVmTableBuilder(GesVmTableShape shape, int capacity = 0)
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
            var width = checked((int)GesVmTableData.GetColumnWidth(column.Kind));
            var size = capacity <= 0 ? width * 4 : capacity * width;
            _columns[i] = new ulong[size];
        }
    }

    public int RowCount => _rowCount;
    public int ColumnCount => _shape.ColumnCount;

    public static GesVmTableBuilder List(GesVmTableColumnKind itemKind, int capacity = 0, ushort valueNameIndex = 0) =>
        new(GesVmTableShape.List(itemKind, valueNameIndex), capacity);

    public static GesVmTableBuilder KeyTable(GesVmTableColumnKind keyKind, int capacity = 0, ushort keyNameIndex = 0) =>
        new(GesVmTableShape.KeyTable(keyKind, keyNameIndex), capacity);

    public static GesVmTableBuilder Map(GesVmTableColumnKind keyKind, GesVmTableColumnKind valueKind, int capacity = 0, ushort keyNameIndex = 0, ushort valueNameIndex = 1) =>
        new(GesVmTableShape.Map(keyKind, valueKind, keyNameIndex, valueNameIndex), capacity);

    public bool TryAddRow(ReadOnlySpan<GesVmTableCell> cells, out int rowIndex)
    {
        rowIndex = -1;
        if (cells.Length != _shape.ColumnCount)
        {
            return false;
        }

        for (var i = 0; i < cells.Length; i++)
        {
            if (cells[i].Kind != _shape.Columns[i].Kind || !CanWrite(i, cells[i]))
            {
                return false;
            }
        }

        rowIndex = _rowCount++;
        for (var i = 0; i < cells.Length; i++)
        {
            Write(i, cells[i], rowIndex);
        }

        return true;
    }

    public GesVmTable ToTable() => new(ToData());

    public GesVmTableData ToData()
    {
        var columns = new GesVmTableColumnSegment[_shape.ColumnCount];
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
            columns[i] = new GesVmTableColumnSegment(
                _shape.Columns[i].NameIndex,
                _shape.Columns[i].Kind,
                _shape.Columns[i].Flags,
                checked((uint)offset),
                checked((uint)_rowCount));
            offset += _columnLengths[i];
        }

        return new GesVmTableData(columns, data, checked((uint)_rowCount));
    }

    private bool CanWrite(int columnIndex, in GesVmTableCell cell)
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

    private void Write(int columnIndex, in GesVmTableCell cell, int rowIndex)
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

internal sealed class GesVmValueGroupBuilder
{
    private readonly GesVmState _state;
    private string[] _keys;
    private GesVmValue[][] _buckets;
    private int[] _counts;
    private int _count;

    internal GesVmValueGroupBuilder(GesVmState state, int capacity = 0)
    {
        _state = state;
        var size = capacity <= 0 ? 4 : capacity;
        _keys = new string[size];
        _buckets = new GesVmValue[size][];
        _counts = new int[size];
    }

    internal void Add(string key, GesVmValue value)
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
                var nextBuckets = new GesVmValue[nextSize][];
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
            _buckets[groupIndex] = new GesVmValue[4];
        }

        var bucket = _buckets[groupIndex];
        var itemCount = _counts[groupIndex];
        if (itemCount == bucket.Length)
        {
            var resized = new GesVmValue[bucket.Length << 1];
            Array.Copy(bucket, resized, bucket.Length);
            bucket = resized;
            _buckets[groupIndex] = bucket;
        }

        bucket[itemCount] = value;
        _counts[groupIndex] = itemCount + 1;
    }

    internal void WriteTo(ref GesVmValue destination)
    {
        var map = new GesVmValueMapBuilder(_count);
        for (var groupIndex = 0; groupIndex < _count; groupIndex++)
        {
            var itemCount = _counts[groupIndex];
            var groupedList = new GesVmValue[itemCount];
            for (var i = 0; i < itemCount; i++) groupedList[i] = _buckets[groupIndex][i];
            var groupedValue = new GesVmValue();
            groupedValue.SetList(groupedList);
            map.Set(_keys[groupIndex], groupedValue);
        }

        destination.SetMap(map.ToMap());
    }
}

internal sealed class GesVmValueDistinctBuilder
{
    private GesVmValue[] _keys;
    private GesVmValue[] _values;
    private int _count;

    internal GesVmValueDistinctBuilder(int capacity = 0)
    {
        var size = capacity <= 0 ? 4 : capacity;
        _keys = new GesVmValue[size];
        _values = new GesVmValue[size];
    }

    internal void Add(in GesVmValue key, in GesVmValue value)
    {
        for (var i = 0; i < _count; i++)
        {
            var existing = _keys[i];
            var candidate = key;
            if (existing.EqualsValue(ref candidate)) return;
        }

        if (_count == _keys.Length)
        {
            var nextSize = _keys.Length << 1;
            var nextKeys = new GesVmValue[nextSize];
            var nextValues = new GesVmValue[nextSize];
            Array.Copy(_keys, nextKeys, _keys.Length);
            Array.Copy(_values, nextValues, _values.Length);
            _keys = nextKeys;
            _values = nextValues;
        }

        _keys[_count] = key;
        _values[_count] = value;
        _count++;
    }

    internal GesVmValue[] ToList()
    {
        if (_count == 0) return [];
        var result = new GesVmValue[_count];
        for (var i = 0; i < _count; i++) result[i] = _values[i];
        return result;
    }
}

internal sealed class GesVmValueOrderBuilder
{
    private GesVmValue[] _keys;
    private GesVmValue[] _values;
    private int _count;

    internal GesVmValueOrderBuilder(int capacity = 0)
    {
        var size = capacity <= 0 ? 4 : capacity;
        _keys = new GesVmValue[size];
        _values = new GesVmValue[size];
    }

    internal void Add(in GesVmValue key, in GesVmValue value)
    {
        if (_count == _keys.Length)
        {
            var nextSize = _keys.Length << 1;
            var nextKeys = new GesVmValue[nextSize];
            var nextValues = new GesVmValue[nextSize];
            Array.Copy(_keys, nextKeys, _keys.Length);
            Array.Copy(_values, nextValues, _values.Length);
            _keys = nextKeys;
            _values = nextValues;
        }

        _keys[_count] = key;
        _values[_count] = value;
        _count++;
    }

    internal bool TryToList(bool descending, out GesVmValue[] result)
    {
        if (_count == 0)
        {
            result = [];
            return true;
        }

        var keys = new GesVmValue[_count];
        var values = new GesVmValue[_count];
        for (var i = 0; i < _count; i++)
        {
            keys[i] = _keys[i];
            values[i] = _values[i];
        }

        if (!GesVmRegisterSortGroupDistinct.SortValuesByKeys(values, keys, _count, descending))
        {
            result = [];
            return false;
        }

        result = values;
        return true;
    }
}
