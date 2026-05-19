#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace StepH.GameEventScript.BytecodeExecutor;

internal enum VmTableColumnKind : byte
{
    Boolean = 1,
    Integer = 2,
    Float = 3,
    Text = 4,
    Tag = 5,
    Uuid = 6
}

[Flags]
internal enum VmTableColumnFlags : byte
{
    None = 0,
    Unique = 1,
    Indexed = 2
}

internal readonly record struct VmUuid128(ulong High, ulong Low);

internal readonly record struct VmTableColumnDefinition(ushort NameIndex, VmTableColumnKind Kind, VmTableColumnFlags Flags = VmTableColumnFlags.None)
{
    public bool IsUnique => (Flags & VmTableColumnFlags.Unique) != 0;
    public bool IsIndexed => (Flags & VmTableColumnFlags.Indexed) != 0;
    public bool RequiresIndex => IsUnique || IsIndexed;
}

internal readonly record struct VmTableColumnSegment(ushort NameIndex, VmTableColumnKind Kind, VmTableColumnFlags Flags, uint DataStart, uint RowCount)
{
    public bool IsUnique => (Flags & VmTableColumnFlags.Unique) != 0;
    public bool IsIndexed => (Flags & VmTableColumnFlags.Indexed) != 0;
    public bool RequiresIndex => IsUnique || IsIndexed;
}

internal sealed class VmTableData
{
    public VmTableData(VmTableColumnSegment[] columns, ulong[] data, uint rowCount)
    {
        Columns = columns ?? throw new ArgumentNullException(nameof(columns));
        Data = data ?? throw new ArgumentNullException(nameof(data));
        RowCount = rowCount;
        Validate();
    }

    public VmTableColumnSegment[] Columns { get; }
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetColumnWidth(VmTableColumnKind kind) => kind switch
    {
        VmTableColumnKind.Boolean => 1,
        VmTableColumnKind.Integer => 1,
        VmTableColumnKind.Float => 1,
        VmTableColumnKind.Text => 1,
        VmTableColumnKind.Tag => 1,
        VmTableColumnKind.Uuid => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}

internal sealed class VmTableShape
{
    private readonly Dictionary<ushort, ushort>? _columnsByName;

    public VmTableShape(params VmTableColumnDefinition[] columns)
    {
        if (columns.Length == 0)
        {
            throw new ArgumentException("A VM table shape needs at least one column.", nameof(columns));
        }

        Columns = new VmTableColumnDefinition[columns.Length];
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

    public VmTableColumnDefinition[] Columns { get; }
    public int ColumnCount => Columns.Length;

    public static VmTableShape List(VmTableColumnKind itemKind, ushort valueNameIndex = 0) =>
        new(new VmTableColumnDefinition(valueNameIndex, itemKind));

    public static VmTableShape KeyTable(VmTableColumnKind keyKind, ushort keyNameIndex = 0) =>
        new(new VmTableColumnDefinition(keyNameIndex, keyKind, VmTableColumnFlags.Unique));

    public static VmTableShape Map(VmTableColumnKind keyKind, VmTableColumnKind valueKind, ushort keyNameIndex = 0, ushort valueNameIndex = 1) =>
        new(
            new VmTableColumnDefinition(keyNameIndex, keyKind, VmTableColumnFlags.Unique),
            new VmTableColumnDefinition(valueNameIndex, valueKind));

    public bool TryGetColumnIndex(ushort nameIndex, out ushort index)
    {
        index = 0;
        return _columnsByName?.TryGetValue(nameIndex, out index) == true;
    }
}

internal readonly struct VmTableCell
{
    private VmTableCell(VmTableColumnKind kind, ulong a, ulong b = 0)
    {
        Kind = kind;
        A = a;
        B = b;
    }

    public VmTableColumnKind Kind { get; }
    public ulong A { get; }
    public ulong B { get; }

    public static VmTableCell FromBoolean(bool value) => new(VmTableColumnKind.Boolean, value ? 1UL : 0UL);
    public static VmTableCell FromInteger(long value) => new(VmTableColumnKind.Integer, unchecked((ulong)value));
    public static VmTableCell FromFloat(double value) => new(VmTableColumnKind.Float, unchecked((ulong)BitConverter.DoubleToInt64Bits(value)));
    public static VmTableCell FromTextIndex(ushort value) => new(VmTableColumnKind.Text, value);
    public static VmTableCell FromTagIndex(ushort value) => new(VmTableColumnKind.Tag, value);
    public static VmTableCell FromUuid(VmUuid128 value) => new(VmTableColumnKind.Uuid, value.High, value.Low);
}

internal sealed class VmTableBuilder
{
    private readonly VmTableShape _shape;
    private readonly List<ulong>[] _columns;
    private readonly Dictionary<ulong, int>?[] _scalarIndexes;
    private readonly Dictionary<VmUuid128, int>?[] _uuidIndexes;
    private int _rowCount;

    public VmTableBuilder(VmTableShape shape, int capacity = 0)
    {
        if (capacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _shape = shape;
        _columns = new List<ulong>[shape.ColumnCount];
        _scalarIndexes = new Dictionary<ulong, int>?[shape.ColumnCount];
        _uuidIndexes = new Dictionary<VmUuid128, int>?[shape.ColumnCount];

        for (var i = 0; i < _columns.Length; i++)
        {
            var column = shape.Columns[i];
            var width = checked((int)VmTableData.GetColumnWidth(column.Kind));
            _columns[i] = new List<ulong>(capacity * width);
            if (!column.RequiresIndex)
            {
                continue;
            }

            if (column.Kind == VmTableColumnKind.Uuid)
            {
                _uuidIndexes[i] = new Dictionary<VmUuid128, int>();
            }
            else
            {
                _scalarIndexes[i] = new Dictionary<ulong, int>();
            }
        }
    }

    public int RowCount => _rowCount;
    public int ColumnCount => _shape.ColumnCount;

    public static VmTableBuilder List(VmTableColumnKind itemKind, int capacity = 0, ushort valueNameIndex = 0) =>
        new(VmTableShape.List(itemKind, valueNameIndex), capacity);

    public static VmTableBuilder KeyTable(VmTableColumnKind keyKind, int capacity = 0, ushort keyNameIndex = 0) =>
        new(VmTableShape.KeyTable(keyKind, keyNameIndex), capacity);

    public static VmTableBuilder Map(VmTableColumnKind keyKind, VmTableColumnKind valueKind, int capacity = 0, ushort keyNameIndex = 0, ushort valueNameIndex = 1) =>
        new(VmTableShape.Map(keyKind, valueKind, keyNameIndex, valueNameIndex), capacity);

    public bool TryAddRow(ReadOnlySpan<VmTableCell> cells, out int rowIndex)
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

    public VmTable ToTable() => new(ToData());

    public VmTableData ToData()
    {
        var columns = new VmTableColumnSegment[_shape.ColumnCount];
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
            columns[i] = new VmTableColumnSegment(
                _shape.Columns[i].NameIndex,
                _shape.Columns[i].Kind,
                _shape.Columns[i].Flags,
                checked((uint)offset),
                checked((uint)_rowCount));
            offset += source.Count;
        }

        return new VmTableData(columns, data, checked((uint)_rowCount));
    }

    private bool CanWrite(int columnIndex, in VmTableCell cell)
    {
        if (!_shape.Columns[columnIndex].RequiresIndex)
        {
            return true;
        }

        if (cell.Kind == VmTableColumnKind.Uuid)
        {
            return _uuidIndexes[columnIndex]?.ContainsKey(new VmUuid128(cell.A, cell.B)) != true;
        }

        return _scalarIndexes[columnIndex]?.ContainsKey(cell.A) != true;
    }

    private void Write(int columnIndex, in VmTableCell cell, int rowIndex)
    {
        var target = _columns[columnIndex];
        target.Add(cell.A);
        if (cell.Kind == VmTableColumnKind.Uuid)
        {
            target.Add(cell.B);
            _uuidIndexes[columnIndex]?.Add(new VmUuid128(cell.A, cell.B), rowIndex);
        }
        else
        {
            _scalarIndexes[columnIndex]?.Add(cell.A, rowIndex);
        }
    }
}

internal sealed class VmTable : IVmLengthAccess
{
    private readonly VmTableIndex?[] _indexes;

    public VmTable(VmTableData data)
    {
        Data = data;
        _indexes = new VmTableIndex?[data.ColumnCount];
    }

    public VmTableData Data { get; }
    public int RowCount => checked((int)Data.RowCount);
    public int Length => RowCount;
    public int ColumnCount => Data.ColumnCount;

    public static VmTable CreateList(VmTableColumnKind itemKind, int capacity = 0, ushort valueNameIndex = 0) =>
        VmTableBuilder.List(itemKind, capacity, valueNameIndex).ToTable();

    public static VmTable CreateKeyTable(VmTableColumnKind keyKind, int capacity = 0, ushort keyNameIndex = 0) =>
        VmTableBuilder.KeyTable(keyKind, capacity, keyNameIndex).ToTable();

    public static VmTable CreateMap(VmTableColumnKind keyKind, VmTableColumnKind valueKind, int capacity = 0, ushort keyNameIndex = 0, ushort valueNameIndex = 1) =>
        VmTableBuilder.Map(keyKind, valueKind, capacity, keyNameIndex, valueNameIndex).ToTable();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool GetBoolean(int rowIndex, int columnIndex) => ReadScalar(rowIndex, columnIndex) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long GetInteger(int rowIndex, int columnIndex) => unchecked((long)ReadScalar(rowIndex, columnIndex));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetFloat(int rowIndex, int columnIndex) => BitConverter.Int64BitsToDouble(unchecked((long)ReadScalar(rowIndex, columnIndex)));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort GetTextIndex(int rowIndex, int columnIndex) => checked((ushort)ReadScalar(rowIndex, columnIndex));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort GetTagIndex(int rowIndex, int columnIndex) => checked((ushort)ReadScalar(rowIndex, columnIndex));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public VmUuid128 GetUuid(int rowIndex, int columnIndex)
    {
        var offset = GetOffset(rowIndex, columnIndex);
        return new VmUuid128(Data.Data[offset], Data.Data[offset + 1]);
    }

    public bool TryFindBoolean(int columnIndex, bool value, out int rowIndex) => TryFindScalar(columnIndex, value ? 1UL : 0UL, out rowIndex);
    public bool TryFindInteger(int columnIndex, long value, out int rowIndex) => TryFindScalar(columnIndex, unchecked((ulong)value), out rowIndex);
    public bool TryFindFloat(int columnIndex, double value, out int rowIndex) => TryFindScalar(columnIndex, unchecked((ulong)BitConverter.DoubleToInt64Bits(value)), out rowIndex);
    public bool TryFindTextIndex(int columnIndex, ushort value, out int rowIndex) => TryFindScalar(columnIndex, value, out rowIndex);
    public bool TryFindTagIndex(int columnIndex, ushort value, out int rowIndex) => TryFindScalar(columnIndex, value, out rowIndex);
    public bool TryFindUuid(int columnIndex, VmUuid128 value, out int rowIndex) => GetIndex(columnIndex).TryFind(value, out rowIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ulong ReadScalar(int rowIndex, int columnIndex) => Data.Data[GetOffset(rowIndex, columnIndex)];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetOffset(int rowIndex, int columnIndex)
    {
        var column = Data.Columns[columnIndex];
        return checked((int)(column.DataStart + (uint)rowIndex * VmTableData.GetColumnWidth(column.Kind)));
    }

    private bool TryFindScalar(int columnIndex, ulong value, out int rowIndex) => GetIndex(columnIndex).TryFind(value, out rowIndex);

    private VmTableIndex GetIndex(int columnIndex)
    {
        var index = _indexes[columnIndex];
        if (index is not null)
        {
            return index;
        }

        index = VmTableIndex.Build(Data, columnIndex);
        _indexes[columnIndex] = index;
        return index;
    }

    private sealed class VmTableIndex
    {
        private readonly Dictionary<ulong, int>? _scalarIndex;
        private readonly Dictionary<VmUuid128, int>? _uuidIndex;

        private VmTableIndex(Dictionary<ulong, int>? scalarIndex, Dictionary<VmUuid128, int>? uuidIndex)
        {
            _scalarIndex = scalarIndex;
            _uuidIndex = uuidIndex;
        }

        public static VmTableIndex Build(VmTableData data, int columnIndex)
        {
            var column = data.Columns[columnIndex];
            var width = VmTableData.GetColumnWidth(column.Kind);
            var offset = column.DataStart;

            if (column.Kind == VmTableColumnKind.Uuid)
            {
                var uuidIndex = new Dictionary<VmUuid128, int>(checked((int)column.RowCount));
                for (var row = 0; row < column.RowCount; row++)
                {
                    uuidIndex.TryAdd(new VmUuid128(data.Data[offset], data.Data[offset + 1]), checked((int)row));
                    offset += width;
                }

                return new VmTableIndex(null, uuidIndex);
            }

            var scalarIndex = new Dictionary<ulong, int>(checked((int)column.RowCount));
            for (var row = 0; row < column.RowCount; row++)
            {
                scalarIndex.TryAdd(data.Data[offset], checked((int)row));
                offset += width;
            }

            return new VmTableIndex(scalarIndex, null);
        }

        public bool TryFind(ulong value, out int rowIndex)
        {
            rowIndex = -1;
            return _scalarIndex?.TryGetValue(value, out rowIndex) == true;
        }

        public bool TryFind(VmUuid128 value, out int rowIndex)
        {
            rowIndex = -1;
            return _uuidIndex?.TryGetValue(value, out rowIndex) == true;
        }
    }
}
