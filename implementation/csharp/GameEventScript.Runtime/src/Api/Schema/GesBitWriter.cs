// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Buffers.Binary;

namespace GameEventScript.Api;

/// <summary>
/// Writes values of 0 to 64 bits into a caller's buffer in the bit order of the schema wire format: least significant bit
/// first, bytes in Little Endian order. It knows no schema; generated schema code and <see cref="GesSchemaWriter"/> build
/// on it. Writing allocates nothing.
/// </summary>
/// <remarks>
/// Bits are collected in a 64-bit word that is stored once it is full, so a buffer of the record's byte count suffices; a
/// full buffer fails with <c>wire.bufferTooSmall</c>. The writer must not be copied while it is used, since each copy
/// holds its own pending bits.
/// </remarks>
public ref struct GesBitWriter
{
    private readonly Span<byte> _buffer;
    private int _position;
    private ulong _pending;
    private int _pendingBits;

    /// <summary>Starts writing at the beginning of a buffer.</summary>
    /// <param name="buffer">The buffer that receives the bits.</param>
    public GesBitWriter(Span<byte> buffer)
    {
        _buffer = buffer;
        _position = 0;
        _pending = 0;
        _pendingBits = 0;
    }

    /// <summary>Gets the number of bits written so far.</summary>
    public readonly long BitPosition => (_position * 8L) + _pendingBits;

    /// <summary>Writes the low <paramref name="width"/> bits of a value.</summary>
    /// <param name="value">The value; its bits above <paramref name="width"/> must be zero.</param>
    /// <param name="width">The number of bits, from 0 to 64.</param>
    /// <exception cref="GesWireException"><c>wire.bufferTooSmall</c> when the buffer is full.</exception>
    public void Write(ulong value, int width)
    {
        if (width == 0) return;
        _pending |= value << _pendingBits;
        var total = _pendingBits + width;
        if (total < 64)
        {
            _pendingBits = total;
            return;
        }

        if (_position + 8 > _buffer.Length) throw new GesWireException(GesWireException.BufferTooSmall, "");
        BinaryPrimitives.WriteUInt64LittleEndian(_buffer.Slice(_position), _pending);
        _position += 8;
        _pendingBits = total - 64;
        _pending = _pendingBits == 0 ? 0 : value >> (width - _pendingBits);
    }

    /// <summary>Writes bytes of eight bits each, as the bytes of a Text are written.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <exception cref="GesWireException"><c>wire.bufferTooSmall</c> when the buffer is full.</exception>
    public void WriteBytes(ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes) Write(value, 8);
    }

    /// <summary>Sets a bit that was already written as zero, for example a flag that is known only later.</summary>
    /// <param name="bit">The bit's position, below <see cref="BitPosition"/>.</param>
    public void SetBit(long bit)
    {
        var flushed = _position * 8L;
        if (bit >= flushed) _pending |= 1UL << (int)(bit - flushed);
        else _buffer[(int)(bit >> 3)] |= (byte)(1 << (int)(bit & 7));
    }

    /// <summary>Goes back to an earlier position; the bits from there on are discarded and written again.</summary>
    /// <param name="bit">The position, at most <see cref="BitPosition"/>.</param>
    public void Rewind(long bit)
    {
        // Whole words are flushed, so the word that holds the position can be read back.
        var position = (int)((bit >> 6) << 3);
        if (position < _position) _pending = BinaryPrimitives.ReadUInt64LittleEndian(_buffer.Slice(position));
        _position = position;
        _pendingBits = (int)(bit & 63);
        _pending = _pendingBits == 0 ? 0 : _pending & ((1UL << _pendingBits) - 1);
    }

    /// <summary>Pads the bits with zeros to a byte boundary and stores them.</summary>
    /// <returns>The number of bytes written.</returns>
    /// <exception cref="GesWireException"><c>wire.bufferTooSmall</c> when the buffer is full.</exception>
    public int Finish()
    {
        var bytes = (_pendingBits + 7) / 8;
        if (_position + bytes > _buffer.Length) throw new GesWireException(GesWireException.BufferTooSmall, "");
        for (var index = 0; index < bytes; index++)
        {
            _buffer[_position++] = (byte)_pending;
            _pending >>= 8;
        }

        _pendingBits = 0;
        return _position;
    }
}
