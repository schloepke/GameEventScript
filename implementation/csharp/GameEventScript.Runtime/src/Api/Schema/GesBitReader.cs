// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Buffers.Binary;

namespace GameEventScript.Api;

/// <summary>
/// Reads values of 0 to 64 bits from untrusted data in the bit order of the schema wire format: least significant bit
/// first, bytes in Little Endian order. It knows no schema; generated schema code and <see cref="GesSchemaReader"/> build
/// on it. Reading allocates nothing.
/// </summary>
/// <remarks>
/// Every read checks the end of the data. Reading past it fails with <c>wire.truncated</c>, or with
/// <c>wire.limitExceeded</c> when the byte limit ends before the data does. The path passed to a read names the field
/// in that error.
/// </remarks>
public ref struct GesBitReader
{
    private readonly ReadOnlySpan<byte> _data;
    private readonly long _bitCount;
    private readonly bool _limited;
    private long _bit;

    /// <summary>Starts reading at the beginning of the data.</summary>
    /// <param name="data">The data; it may continue after the record.</param>
    /// <param name="byteLimit">An optional limit on the bytes that may be read.</param>
    public GesBitReader(ReadOnlySpan<byte> data, long byteLimit = long.MaxValue)
    {
        _data = data;
        _bitCount = Math.Min(data.Length, byteLimit) * 8L;
        _limited = data.Length > byteLimit;
        _bit = 0;
    }

    /// <summary>Gets the position of the next bit.</summary>
    public readonly long BitPosition => _bit;

    /// <summary>Reads a value of <paramref name="width"/> bits.</summary>
    /// <param name="width">The number of bits, from 0 to 64.</param>
    /// <param name="path">The field path for an error, such as <c>position.x</c>; empty for the record as a whole.</param>
    /// <returns>The value, with all bits above <paramref name="width"/> zero.</returns>
    /// <exception cref="GesWireException">The data ends before the value does.</exception>
    public ulong Read(int width, string path)
    {
        if (width == 0) return 0;
        if (_bit + width > _bitCount) throw new GesWireException(End, path);
        var start = (int)(_bit >> 3);
        var shift = (int)(_bit & 7);
        _bit += width;
        ulong value;
        if (start + 8 <= _data.Length)
        {
            value = BinaryPrimitives.ReadUInt64LittleEndian(_data.Slice(start)) >> shift;
            if (shift + width > 64) value |= (ulong)_data[start + 8] << (64 - shift);
        }
        else
        {
            value = 0;
            var bytes = (shift + width + 7) / 8;
            for (var index = 0; index < bytes && index < 8; index++) value |= (ulong)_data[start + index] << (8 * index);
            value >>= shift;
            if (bytes > 8) value |= (ulong)_data[start + 8] << (64 - shift);
        }

        return width == 64 ? value : value & ((1UL << width) - 1);
    }

    /// <summary>Reads bytes of eight bits each, as the bytes of a Text are read.</summary>
    /// <param name="destination">The buffer that receives exactly its length in bytes.</param>
    /// <param name="path">The field path for an error.</param>
    /// <exception cref="GesWireException">The data ends before the bytes do; nothing is read then.</exception>
    public void ReadBytes(Span<byte> destination, string path)
    {
        Require(destination.Length * 8L, path);
        for (var index = 0; index < destination.Length; index++) destination[index] = (byte)Read(8, path);
    }

    /// <summary>Fails like a read when fewer than <paramref name="bits"/> bits remain, without reading them.</summary>
    /// <param name="bits">The number of bits that must remain.</param>
    /// <param name="path">The field path for an error.</param>
    /// <exception cref="GesWireException">The data ends before these bits do.</exception>
    public readonly void Require(long bits, string path)
    {
        if (_bit + bits > _bitCount) throw new GesWireException(End, path);
    }

    /// <summary>Moves to a position, for example back to a value that was read before.</summary>
    /// <param name="bit">The position, from 0 to the end of the data.</param>
    public void Seek(long bit) => _bit = bit;

    /// <summary>Reads the padding to the next byte boundary, which must be zero.</summary>
    /// <returns>The number of bytes read; further records may follow them.</returns>
    /// <exception cref="GesWireException"><c>wire.invalidPadding</c> when a padding bit is set.</exception>
    public int Finish()
    {
        var rest = (int)(-_bit & 7);
        if (rest > 0 && Read(rest, "") != 0) throw new GesWireException(GesWireException.InvalidPadding, "");
        return (int)(_bit / 8);
    }

    private readonly string End => _limited ? GesWireException.LimitExceeded : GesWireException.Truncated;
}
