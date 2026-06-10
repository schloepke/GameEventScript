#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;

namespace StepH.GameEventScript.VirtualMachine;

internal enum GesVmTableColumnKind : byte
{
    Boolean = 1,
    Integer = 2,
    Float = 3,
    Text = 4,
    Tag = 5
}

[Flags]
internal enum GesVmTableColumnFlags : byte
{
    None = 0,
    Unique = 1,
    Indexed = 2
}

internal readonly record struct GesVmTableColumnDefinition(ushort NameIndex, GesVmTableColumnKind Kind, GesVmTableColumnFlags Flags = GesVmTableColumnFlags.None)
{
    public bool IsUnique => (Flags & GesVmTableColumnFlags.Unique) != 0;
    public bool IsIndexed => (Flags & GesVmTableColumnFlags.Indexed) != 0;
    public bool RequiresIndex => IsUnique || IsIndexed;
}

internal readonly record struct GesVmTableColumnSegment(ushort NameIndex, GesVmTableColumnKind Kind, GesVmTableColumnFlags Flags, uint DataStart, uint RowCount)
{
    public bool IsUnique => (Flags & GesVmTableColumnFlags.Unique) != 0;
    public bool IsIndexed => (Flags & GesVmTableColumnFlags.Indexed) != 0;
    public bool RequiresIndex => IsUnique || IsIndexed;
}

internal sealed class GesVmTableData
{
    public GesVmTableData(GesVmTableColumnSegment[] columns, ulong[] data, uint rowCount)
    {
        Columns = columns ?? throw new ArgumentNullException(nameof(columns));
        Data = data ?? throw new ArgumentNullException(nameof(data));
        RowCount = rowCount;
        Validate();
    }

    public GesVmTableColumnSegment[] Columns { get; }
    public ulong[] Data { get; }
    public uint RowCount { get; }
    public int ColumnCount => Columns.Length;

    private void Validate()
    {
        if (Columns.Length == 0) throw new ArgumentException("A VM table needs at least one column.", nameof(Columns));
        var expectedRows = RowCount;
        for (var i = 0; i < Columns.Length; i++)
        {
            var column = Columns[i];
            if (column.RowCount != expectedRows) throw new ArgumentException("All VM table columns must have the same row count.", nameof(Columns));
            if (column.IsIndexed && !column.IsUnique) throw new ArgumentException("Non-unique VM table indexes are not implemented yet.", nameof(Columns));
            var width = GetColumnWidth(column.Kind);
            var requiredEnd = checked(column.DataStart + column.RowCount * width);
            if (requiredEnd > Data.Length) throw new ArgumentException("A VM table column points outside of the data segment.", nameof(Columns));
        }
    }
    public static uint GetColumnWidth(GesVmTableColumnKind kind) => kind switch
    {
        GesVmTableColumnKind.Boolean => 1,
        GesVmTableColumnKind.Integer => 1,
        GesVmTableColumnKind.Float => 1,
        GesVmTableColumnKind.Text => 1,
        GesVmTableColumnKind.Tag => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}

internal sealed class GesVmTableShape
{
    private readonly Dictionary<ushort, ushort>? _columnsByName;

    public GesVmTableShape(params GesVmTableColumnDefinition[] columns)
    {
        if (columns.Length == 0)
        {
            throw new ArgumentException("A VM table shape needs at least one column.", nameof(columns));
        }

        Columns = new GesVmTableColumnDefinition[columns.Length];
        Array.Copy(columns, Columns, columns.Length);

        Dictionary<ushort, ushort>? columnsByName = null;
        for (var i = 0; i < Columns.Length; i++)
        {
            var column = Columns[i];
            if (column.IsIndexed && !column.IsUnique)
            {
                throw new ArgumentException("Non-unique VM table indexes are not implemented yet.", nameof(columns));
            }

            columnsByName ??= new Dictionary<ushort, ushort>();
            if (!columnsByName.TryAdd(column.NameIndex, checked((ushort)i)))
            {
                throw new ArgumentException($"Duplicate VM table column name index '{column.NameIndex}'.", nameof(columns));
            }
        }

        _columnsByName = columnsByName;
    }

    public GesVmTableColumnDefinition[] Columns { get; }
    public int ColumnCount => Columns.Length;

    public static GesVmTableShape List(GesVmTableColumnKind itemKind, ushort valueNameIndex = 0) =>
        new(new GesVmTableColumnDefinition(valueNameIndex, itemKind));

    public static GesVmTableShape KeyTable(GesVmTableColumnKind keyKind, ushort keyNameIndex = 0) =>
        new(new GesVmTableColumnDefinition(keyNameIndex, keyKind, GesVmTableColumnFlags.Unique));

    public static GesVmTableShape Map(GesVmTableColumnKind keyKind, GesVmTableColumnKind valueKind, ushort keyNameIndex = 0, ushort valueNameIndex = 1) =>
        new(
            new GesVmTableColumnDefinition(keyNameIndex, keyKind, GesVmTableColumnFlags.Unique),
            new GesVmTableColumnDefinition(valueNameIndex, valueKind));

    public bool TryGetColumnIndex(ushort nameIndex, out ushort index)
    {
        index = 0;
        return _columnsByName?.TryGetValue(nameIndex, out index) == true;
    }
}

internal readonly struct GesVmTableCell
{
    private GesVmTableCell(GesVmTableColumnKind kind, ulong a, ulong b = 0)
    {
        Kind = kind;
        A = a;
        B = b;
    }

    public GesVmTableColumnKind Kind { get; }
    public ulong A { get; }
    public ulong B { get; }

    public static GesVmTableCell FromBoolean(bool value) => new(GesVmTableColumnKind.Boolean, value ? 1UL : 0UL);
    public static GesVmTableCell FromInteger(long value) => new(GesVmTableColumnKind.Integer, unchecked((ulong)value));
    public static GesVmTableCell FromFloat(double value) => new(GesVmTableColumnKind.Float, unchecked((ulong)BitConverter.DoubleToInt64Bits(value)));
    public static GesVmTableCell FromTextIndex(ushort value) => new(GesVmTableColumnKind.Text, value);
    public static GesVmTableCell FromTagIndex(ushort value) => new(GesVmTableColumnKind.Tag, value);
}

internal sealed class GesVmTableBuilder
{
    private readonly GesVmTableShape _shape;
    private readonly List<ulong>[] _columns;
    private readonly Dictionary<ulong, int>?[] _scalarIndexes;
    private int _rowCount;

    public GesVmTableBuilder(GesVmTableShape shape, int capacity = 0)
    {
        if (capacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _shape = shape;
        _columns = new List<ulong>[shape.ColumnCount];
        _scalarIndexes = new Dictionary<ulong, int>?[shape.ColumnCount];

        for (var i = 0; i < _columns.Length; i++)
        {
            var column = shape.Columns[i];
            var width = checked((int)GesVmTableData.GetColumnWidth(column.Kind));
            _columns[i] = new List<ulong>(capacity * width);
            if (!column.RequiresIndex)
            {
                continue;
            }

            _scalarIndexes[i] = new Dictionary<ulong, int>();
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
            dataLength += _columns[i].Count;
        }

        var data = new ulong[dataLength];
        var offset = 0;
        for (var i = 0; i < _columns.Length; i++)
        {
            var source = _columns[i];
            source.CopyTo(data, offset);
            columns[i] = new GesVmTableColumnSegment(
                _shape.Columns[i].NameIndex,
                _shape.Columns[i].Kind,
                _shape.Columns[i].Flags,
                checked((uint)offset),
                checked((uint)_rowCount));
            offset += source.Count;
        }

        return new GesVmTableData(columns, data, checked((uint)_rowCount));
    }

    private bool CanWrite(int columnIndex, in GesVmTableCell cell)
    {
        if (!_shape.Columns[columnIndex].RequiresIndex)
        {
            return true;
        }

        return _scalarIndexes[columnIndex]?.ContainsKey(cell.A) != true;
    }

    private void Write(int columnIndex, in GesVmTableCell cell, int rowIndex)
    {
        var target = _columns[columnIndex];
        target.Add(cell.A);
        _scalarIndexes[columnIndex]?.Add(cell.A, rowIndex);
    }
}

internal sealed class GesVmTable
{
    private readonly GesVmTableIndex?[] _indexes;

    public GesVmTable(GesVmTableData data)
    {
        Data = data;
        _indexes = new GesVmTableIndex?[data.ColumnCount];
    }

    public GesVmTableData Data { get; }
    public int RowCount => checked((int)Data.RowCount);
    public int ColumnCount => Data.ColumnCount;

    public static GesVmTable CreateList(GesVmTableColumnKind itemKind, int capacity = 0, ushort valueNameIndex = 0) =>
        GesVmTableBuilder.List(itemKind, capacity, valueNameIndex).ToTable();

    public static GesVmTable CreateKeyTable(GesVmTableColumnKind keyKind, int capacity = 0, ushort keyNameIndex = 0) =>
        GesVmTableBuilder.KeyTable(keyKind, capacity, keyNameIndex).ToTable();

    public static GesVmTable CreateMap(GesVmTableColumnKind keyKind, GesVmTableColumnKind valueKind, int capacity = 0, ushort keyNameIndex = 0, ushort valueNameIndex = 1) =>
        GesVmTableBuilder.Map(keyKind, valueKind, capacity, keyNameIndex, valueNameIndex).ToTable();
    public bool GetBoolean(int rowIndex, int columnIndex) => ReadScalar(rowIndex, columnIndex) != 0;
    public long GetInteger(int rowIndex, int columnIndex) => unchecked((long)ReadScalar(rowIndex, columnIndex));
    public double GetFloat(int rowIndex, int columnIndex) => BitConverter.Int64BitsToDouble(unchecked((long)ReadScalar(rowIndex, columnIndex)));
    public ushort GetTextIndex(int rowIndex, int columnIndex) => checked((ushort)ReadScalar(rowIndex, columnIndex));
    public ushort GetTagIndex(int rowIndex, int columnIndex) => checked((ushort)ReadScalar(rowIndex, columnIndex));

    public bool TryFindBoolean(int columnIndex, bool value, out int rowIndex) => TryFindScalar(columnIndex, value ? 1UL : 0UL, out rowIndex);
    public bool TryFindInteger(int columnIndex, long value, out int rowIndex) => TryFindScalar(columnIndex, unchecked((ulong)value), out rowIndex);
    public bool TryFindFloat(int columnIndex, double value, out int rowIndex) => TryFindScalar(columnIndex, unchecked((ulong)BitConverter.DoubleToInt64Bits(value)), out rowIndex);
    public bool TryFindTextIndex(int columnIndex, ushort value, out int rowIndex) => TryFindScalar(columnIndex, value, out rowIndex);
    public bool TryFindTagIndex(int columnIndex, ushort value, out int rowIndex) => TryFindScalar(columnIndex, value, out rowIndex);
    private ulong ReadScalar(int rowIndex, int columnIndex) => Data.Data[GetOffset(rowIndex, columnIndex)];
    private int GetOffset(int rowIndex, int columnIndex)
    {
        var column = Data.Columns[columnIndex];
        return checked((int)(column.DataStart + (uint)rowIndex * GesVmTableData.GetColumnWidth(column.Kind)));
    }

    private bool TryFindScalar(int columnIndex, ulong value, out int rowIndex) => GetIndex(columnIndex).TryFind(value, out rowIndex);

    private GesVmTableIndex GetIndex(int columnIndex)
    {
        var index = _indexes[columnIndex];
        if (index is not null)
        {
            return index;
        }

        index = GesVmTableIndex.Build(Data, columnIndex);
        _indexes[columnIndex] = index;
        return index;
    }

    private sealed class GesVmTableIndex
    {
        private readonly Dictionary<ulong, int>? _scalarIndex;

        private GesVmTableIndex(Dictionary<ulong, int>? scalarIndex)
        {
            _scalarIndex = scalarIndex;
        }

        public static GesVmTableIndex Build(GesVmTableData data, int columnIndex)
        {
            var column = data.Columns[columnIndex];
            var width = GesVmTableData.GetColumnWidth(column.Kind);
            var offset = column.DataStart;

            var scalarIndex = new Dictionary<ulong, int>(checked((int)column.RowCount));
            for (var row = 0; row < column.RowCount; row++)
            {
                scalarIndex.TryAdd(data.Data[offset], checked((int)row));
                offset += width;
            }

            return new GesVmTableIndex(scalarIndex);
        }

        public bool TryFind(ulong value, out int rowIndex)
        {
            rowIndex = -1;
            return _scalarIndex?.TryGetValue(value, out rowIndex) == true;
        }

    }
}
