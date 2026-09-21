// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;

namespace GameEventScript.Runtime.Values;

internal enum GesTableColumnKind : byte
{
    Boolean = 1,
    Integer = 2,
    Float = 3,
    Text = 4,
    Tag = 5
}

[Flags]
internal enum GesTableColumnFlags : byte
{
    None = 0,
    Unique = 1,
    Indexed = 2
}

internal readonly record struct GesTableColumnDefinition(ushort NameIndex, GesTableColumnKind Kind, GesTableColumnFlags Flags = GesTableColumnFlags.None)
{
    public bool IsUnique => (Flags & GesTableColumnFlags.Unique) != 0;
    public bool IsIndexed => (Flags & GesTableColumnFlags.Indexed) != 0;
    public bool RequiresIndex => IsUnique || IsIndexed;
}

internal readonly record struct GesTableColumnSegment(ushort NameIndex, GesTableColumnKind Kind, GesTableColumnFlags Flags, uint DataStart, uint RowCount)
{
    public bool IsUnique => (Flags & GesTableColumnFlags.Unique) != 0;
    public bool IsIndexed => (Flags & GesTableColumnFlags.Indexed) != 0;
    public bool RequiresIndex => IsUnique || IsIndexed;
}

internal sealed class GesTableData
{
    public GesTableData(GesTableColumnSegment[] columns, ulong[] data, uint rowCount)
    {
        Columns = columns ?? throw new ArgumentNullException(nameof(columns));
        Data = data ?? throw new ArgumentNullException(nameof(data));
        RowCount = rowCount;
        Validate();
    }

    public GesTableColumnSegment[] Columns { get; }
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
    public static uint GetColumnWidth(GesTableColumnKind kind) => kind switch
    {
        GesTableColumnKind.Boolean => 1,
        GesTableColumnKind.Integer => 1,
        GesTableColumnKind.Float => 1,
        GesTableColumnKind.Text => 1,
        GesTableColumnKind.Tag => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}

internal sealed class GesTableShape
{
    public GesTableShape(params GesTableColumnDefinition[] columns)
    {
        if (columns.Length == 0)
        {
            throw new ArgumentException("A VM table shape needs at least one column.", nameof(columns));
        }

        Columns = new GesTableColumnDefinition[columns.Length];
        Array.Copy(columns, Columns, columns.Length);

        for (var i = 0; i < Columns.Length; i++)
        {
            var column = Columns[i];
            if (column.IsIndexed && !column.IsUnique)
            {
                throw new ArgumentException("Non-unique VM table indexes are not implemented yet.", nameof(columns));
            }

            for (var previous = 0; previous < i; previous++)
            {
                if (Columns[previous].NameIndex == column.NameIndex)
                {
                    throw new ArgumentException($"Duplicate VM table column name index '{column.NameIndex}'.", nameof(columns));
                }
            }
        }
    }

    public GesTableColumnDefinition[] Columns { get; }
    public int ColumnCount => Columns.Length;

    public static GesTableShape List(GesTableColumnKind itemKind, ushort valueNameIndex = 0) =>
        new(new GesTableColumnDefinition(valueNameIndex, itemKind));

    public static GesTableShape KeyTable(GesTableColumnKind keyKind, ushort keyNameIndex = 0) =>
        new(new GesTableColumnDefinition(keyNameIndex, keyKind, GesTableColumnFlags.Unique));

    public static GesTableShape Map(GesTableColumnKind keyKind, GesTableColumnKind valueKind, ushort keyNameIndex = 0, ushort valueNameIndex = 1) =>
        new(
            new GesTableColumnDefinition(keyNameIndex, keyKind, GesTableColumnFlags.Unique),
            new GesTableColumnDefinition(valueNameIndex, valueKind));

    public ushort? GetColumnIndex(ushort nameIndex)
    {
        for (var i = 0; i < Columns.Length; i++)
        {
            if (Columns[i].NameIndex != nameIndex) continue;
            return checked((ushort)i);
        }

        return null;
    }
}

internal readonly struct GesTableCell
{
    private GesTableCell(GesTableColumnKind kind, ulong a, ulong b = 0)
    {
        Kind = kind;
        A = a;
        B = b;
    }

    public GesTableColumnKind Kind { get; }
    public ulong A { get; }
    public ulong B { get; }

    public static GesTableCell FromBoolean(bool value) => new(GesTableColumnKind.Boolean, value ? 1UL : 0UL);
    public static GesTableCell FromInteger(long value) => new(GesTableColumnKind.Integer, unchecked((ulong)value));
    public static GesTableCell FromFloat(double value) => new(GesTableColumnKind.Float, unchecked((ulong)BitConverter.DoubleToInt64Bits(value)));
    public static GesTableCell FromTextIndex(ushort value) => new(GesTableColumnKind.Text, value);
    public static GesTableCell FromTagIndex(ushort value) => new(GesTableColumnKind.Tag, value);
}

internal sealed class GesTable
{
    public GesTable(GesTableData data)
    {
        Data = data;
    }

    public GesTableData Data { get; }
    public int RowCount => checked((int)Data.RowCount);
    public int ColumnCount => Data.ColumnCount;

    public static GesTable CreateList(GesTableColumnKind itemKind, int capacity = 0, ushort valueNameIndex = 0) =>
        CreateEmpty(GesTableShape.List(itemKind, valueNameIndex));

    public static GesTable CreateKeyTable(GesTableColumnKind keyKind, int capacity = 0, ushort keyNameIndex = 0) =>
        CreateEmpty(GesTableShape.KeyTable(keyKind, keyNameIndex));

    public static GesTable CreateMap(GesTableColumnKind keyKind, GesTableColumnKind valueKind, int capacity = 0, ushort keyNameIndex = 0, ushort valueNameIndex = 1) =>
        CreateEmpty(GesTableShape.Map(keyKind, valueKind, keyNameIndex, valueNameIndex));

    private static GesTable CreateEmpty(GesTableShape shape)
    {
        var columns = new GesTableColumnSegment[shape.ColumnCount];
        for (var i = 0; i < columns.Length; i++)
        {
            var source = shape.Columns[i];
            columns[i] = new GesTableColumnSegment(source.NameIndex, source.Kind, source.Flags, 0, 0);
        }

        return new GesTable(new GesTableData(columns, [], 0));
    }

    public bool GetBoolean(int rowIndex, int columnIndex) => ReadScalar(rowIndex, columnIndex) != 0;
    public long GetInteger(int rowIndex, int columnIndex) => unchecked((long)ReadScalar(rowIndex, columnIndex));
    public double GetFloat(int rowIndex, int columnIndex) => BitConverter.Int64BitsToDouble(unchecked((long)ReadScalar(rowIndex, columnIndex)));
    public ushort GetTextIndex(int rowIndex, int columnIndex) => checked((ushort)ReadScalar(rowIndex, columnIndex));
    public ushort GetTagIndex(int rowIndex, int columnIndex) => checked((ushort)ReadScalar(rowIndex, columnIndex));

    public int? FindBoolean(int columnIndex, bool value) => FindScalar(columnIndex, value ? 1UL : 0UL);
    public int? FindInteger(int columnIndex, long value) => FindScalar(columnIndex, unchecked((ulong)value));
    public int? FindFloat(int columnIndex, double value) => FindScalar(columnIndex, unchecked((ulong)BitConverter.DoubleToInt64Bits(value)));
    public int? FindTextIndex(int columnIndex, ushort value) => FindScalar(columnIndex, value);
    public int? FindTagIndex(int columnIndex, ushort value) => FindScalar(columnIndex, value);
    private ulong ReadScalar(int rowIndex, int columnIndex) => Data.Data[GetOffset(rowIndex, columnIndex)];
    private int GetOffset(int rowIndex, int columnIndex)
    {
        var column = Data.Columns[columnIndex];
        return checked((int)(column.DataStart + (uint)rowIndex * GesTableData.GetColumnWidth(column.Kind)));
    }

    private int? FindScalar(int columnIndex, ulong value)
    {
        var column = Data.Columns[columnIndex];
        var width = GesTableData.GetColumnWidth(column.Kind);
        var offset = column.DataStart;
        for (var row = 0; row < column.RowCount; row++)
        {
            if (Data.Data[offset] == value)
            {
                return checked((int)row);
            }

            offset += width;
        }

        return null;
    }
}
