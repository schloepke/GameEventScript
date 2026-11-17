// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;

namespace GameEventScript.Network;

// The tables of the Deflate format (RFC 1951) shared by the encoder and the decoder.
internal static class GesNetworkDeflateTables
{
    internal static readonly ushort[] LengthBase = [3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 15, 17, 19, 23, 27, 31, 35, 43, 51, 59, 67, 83, 99, 115, 131, 163, 195, 227, 258];
    internal static readonly byte[] LengthExtra = [0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 0];

    internal static readonly ushort[] DistanceBase =
    [
        1, 2, 3, 4, 5, 7, 9, 13, 17, 25, 33, 49, 65, 97, 129, 193, 257, 385, 513, 769, 1025, 1537, 2049, 3073, 4097, 6145, 8193, 12289, 16385, 24577,
    ];

    internal static readonly byte[] DistanceExtra = [0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13];

    // The order in which a dynamic block lists the lengths of the code length code.
    internal static readonly byte[] CodeLengthOrder = [16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15];

    internal static uint Reverse(uint code, int length)
    {
        uint reversed = 0;
        for (var index = 0; index < length; index++)
        {
            reversed = (reversed << 1) | (code & 1);
            code >>= 1;
        }

        return reversed;
    }
}

// The reference Deflate encoder of transfers (specs/Network.md#deflate): one final block with the fixed Huffman code,
// greedy LZ77 matches over hash chains. Both ports produce the same bytes. The chain arrays are reused, so a warmed
// encoder allocates nothing.
internal sealed class GesNetworkDeflater
{
    private const int Window = 32768;
    private const int Mask = Window - 1;
    private const int MaximumChain = 32;
    private const int MinimumMatch = 3;
    private const int MaximumMatch = 258;

    // The bit-reversed fixed codes and their lengths of the literal/length symbols 0-287.
    private static readonly ushort[] LiteralCodes = new ushort[288];
    private static readonly byte[] LiteralLengths = new byte[288];

    // The length symbol (minus 257) of each match length 3-258.
    private static readonly byte[] LengthSymbols = new byte[MaximumMatch + 1];

    private readonly int[] _head = new int[Window];
    private readonly int[] _previous = new int[Window];

    static GesNetworkDeflater()
    {
        for (var symbol = 0; symbol < 288; symbol++)
        {
            uint code;
            int length;
            if (symbol < 144) (code, length) = ((uint)(0x30 + symbol), 8);
            else if (symbol < 256) (code, length) = ((uint)(0x190 + symbol - 144), 9);
            else if (symbol < 280) (code, length) = ((uint)(symbol - 256), 7);
            else (code, length) = ((uint)(0xc0 + symbol - 280), 8);
            LiteralCodes[symbol] = (ushort)GesNetworkDeflateTables.Reverse(code, length);
            LiteralLengths[symbol] = (byte)length;
        }

        for (var symbol = 0; symbol < 29; symbol++)
        {
            var end = symbol == 28 ? MaximumMatch : GesNetworkDeflateTables.LengthBase[symbol + 1] - 1;
            for (int length = GesNetworkDeflateTables.LengthBase[symbol]; length <= end; length++) LengthSymbols[length] = (byte)symbol;
        }
    }

    // Compresses the data into the output. Returns the length written, or -1 when the stream does not fit the output.
    internal int Compress(ReadOnlySpan<byte> data, Span<byte> output)
    {
        Array.Fill(_head, -1);
        var writer = new BitWriter(output);
        writer.Write(1, 1);
        writer.Write(1, 2);
        var position = 0;
        while (position < data.Length && !writer.Overflow)
        {
            var best = 0;
            var distance = 0;
            if (position + MinimumMatch <= data.Length)
            {
                var limit = Math.Min(MaximumMatch, data.Length - position);
                var candidate = _head[Hash(data, position)];
                for (var chain = 0; candidate >= 0 && chain < MaximumChain; chain++)
                {
                    var gap = position - candidate;
                    if (gap > Window) break;
                    var length = 0;
                    while (length < limit && data[candidate + length] == data[position + length]) length++;
                    if (length > best)
                    {
                        best = length;
                        distance = gap;
                        if (length == limit) break;
                    }

                    candidate = _previous[candidate & Mask];
                }
            }

            if (best >= MinimumMatch)
            {
                WriteMatch(ref writer, best, distance);
                var end = position + best;
                for (; position < end; position++) Insert(data, position);
            }
            else
            {
                var literal = data[position];
                writer.Write(LiteralCodes[literal], LiteralLengths[literal]);
                Insert(data, position);
                position++;
            }
        }

        writer.Write(LiteralCodes[256], LiteralLengths[256]);
        return writer.Finish();
    }

    private static int Hash(ReadOnlySpan<byte> data, int position) => ((data[position] << 10) ^ (data[position + 1] << 5) ^ data[position + 2]) & Mask;

    private void Insert(ReadOnlySpan<byte> data, int position)
    {
        if (position + MinimumMatch > data.Length) return;
        var hash = Hash(data, position);
        _previous[position & Mask] = _head[hash];
        _head[hash] = position;
    }

    private static void WriteMatch(ref BitWriter writer, int length, int distance)
    {
        int symbol = LengthSymbols[length];
        writer.Write(LiteralCodes[257 + symbol], LiteralLengths[257 + symbol]);
        writer.Write((uint)(length - GesNetworkDeflateTables.LengthBase[symbol]), GesNetworkDeflateTables.LengthExtra[symbol]);
        var code = 0;
        while (code < 29 && GesNetworkDeflateTables.DistanceBase[code + 1] <= distance) code++;
        writer.Write(GesNetworkDeflateTables.Reverse((uint)code, 5), 5);
        writer.Write((uint)(distance - GesNetworkDeflateTables.DistanceBase[code]), GesNetworkDeflateTables.DistanceExtra[code]);
    }

    // Packs bits from the least significant end and stops writing once the output is full.
    private ref struct BitWriter(Span<byte> output)
    {
        private readonly Span<byte> _output = output;
        private ulong _bits;
        private int _count;
        private int _position;

        internal bool Overflow;

        internal void Write(uint value, int length)
        {
            _bits |= (ulong)value << _count;
            _count += length;
            while (_count >= 8)
            {
                Put((byte)_bits);
                _bits >>= 8;
                _count -= 8;
            }
        }

        internal int Finish()
        {
            if (_count > 0) Put((byte)_bits);
            _bits = 0;
            _count = 0;
            return Overflow ? -1 : _position;
        }

        private void Put(byte value)
        {
            if (_position == _output.Length)
            {
                Overflow = true;
                return;
            }

            _output[_position++] = value;
        }
    }
}

// A Deflate decoder (RFC 1951) for all block types. It accepts a stream only when it ends in a final block, fills the
// output exactly, and has no bytes after the final block's last byte; it rejects the code tables zlib rejects. The
// tables are preallocated, so decoding allocates nothing.
internal sealed class GesNetworkInflater
{
    private const int MaximumBits = 15;

    private readonly ushort[] _literals = new ushort[1 << MaximumBits];
    private readonly ushort[] _distances = new ushort[1 << MaximumBits];
    private readonly ushort[] _codes = new ushort[1 << 7];
    private readonly byte[] _lengths = new byte[286 + 30];
    private readonly byte[] _codeLengths = new byte[19];
    private readonly int[] _counts = new int[MaximumBits + 1];
    private readonly int[] _next = new int[MaximumBits + 1];

    // Decodes the stream into the output; true when it is valid and yields exactly the output's length.
    internal bool Inflate(ReadOnlySpan<byte> input, Span<byte> output)
    {
        var bits = new BitReader(input);
        var written = 0;
        while (true)
        {
            var final = bits.Read(1);
            var type = bits.Read(2);
            if (final < 0 || type < 0) return false;
            switch (type)
            {
                case 0:
                    if (!Stored(ref bits, output, ref written)) return false;
                    break;
                case 1:
                    if (!Fixed(out var literalBits, out var distanceBits)) return false;
                    if (!Codes(ref bits, output, ref written, literalBits, distanceBits)) return false;
                    break;
                case 2:
                    if (!Dynamic(ref bits, out literalBits, out distanceBits)) return false;
                    if (!Codes(ref bits, output, ref written, literalBits, distanceBits)) return false;
                    break;
                default:
                    return false;
            }

            if (final == 1) return written == output.Length && bits.Consumed == input.Length;
        }
    }

    private static bool Stored(ref BitReader bits, Span<byte> output, ref int written)
    {
        bits.Align();
        var length = bits.Read(16);
        var complement = bits.Read(16);
        if (length < 0 || complement < 0 || (length ^ 0xffff) != complement) return false;
        if (length > output.Length - written) return false;
        if (!bits.CopyBytes(output.Slice(written, length))) return false;
        written += length;
        return true;
    }

    private bool Fixed(out int literalBits, out int distanceBits)
    {
        var lengths = _lengths.AsSpan();
        lengths.Slice(0, 144).Fill(8);
        lengths.Slice(144, 112).Fill(9);
        lengths.Slice(256, 24).Fill(7);
        lengths.Slice(280, 8).Fill(8);
        Build(lengths.Slice(0, 288), _literals, false, out literalBits);
        // The fixed distance code has 32 codes; the last two are invalid when used.
        lengths.Slice(0, 32).Fill(5);
        return Build(lengths.Slice(0, 32), _distances, false, out distanceBits);
    }

    private bool Dynamic(ref BitReader bits, out int literalBits, out int distanceBits)
    {
        literalBits = distanceBits = 0;
        var literalCount = bits.Read(5);
        var distanceCount = bits.Read(5);
        var codeCount = bits.Read(4);
        if (literalCount < 0 || distanceCount < 0 || codeCount < 0) return false;
        literalCount += 257;
        distanceCount += 1;
        codeCount += 4;
        if (literalCount > 286 || distanceCount > 30) return false;
        Array.Clear(_codeLengths, 0, _codeLengths.Length);
        for (var index = 0; index < codeCount; index++)
        {
            var length = bits.Read(3);
            if (length < 0) return false;
            _codeLengths[GesNetworkDeflateTables.CodeLengthOrder[index]] = (byte)length;
        }

        if (!Build(_codeLengths, _codes, true, out var codeBits)) return false;
        var total = literalCount + distanceCount;
        var lengths = _lengths.AsSpan(0, total);
        var filled = 0;
        while (filled < total)
        {
            var symbol = bits.Decode(_codes, codeBits);
            if (symbol < 0) return false;
            if (symbol < 16)
            {
                lengths[filled++] = (byte)symbol;
                continue;
            }

            byte value = 0;
            int repeat;
            if (symbol == 16)
            {
                if (filled == 0) return false;
                value = lengths[filled - 1];
                repeat = bits.Read(2);
                if (repeat < 0) return false;
                repeat += 3;
            }
            else if (symbol == 17)
            {
                repeat = bits.Read(3);
                if (repeat < 0) return false;
                repeat += 3;
            }
            else
            {
                repeat = bits.Read(7);
                if (repeat < 0) return false;
                repeat += 11;
            }

            if (repeat > total - filled) return false;
            lengths.Slice(filled, repeat).Fill(value);
            filled += repeat;
        }

        if (lengths[256] == 0) return false;
        return Build(lengths.Slice(0, literalCount), _literals, false, out literalBits)
            && Build(lengths.Slice(literalCount, distanceCount), _distances, false, out distanceBits);
    }

    // Builds a lookup table indexed by the next bits of the stream; each entry holds symbol << 4 | length, 0 marks a
    // code that does not exist. Over-subscribed lengths fail; incomplete ones only for a single code of length 1 in a
    // literal or distance table, and an empty table only fails when it is used.
    private bool Build(ReadOnlySpan<byte> lengths, ushort[] table, bool codeLengthCode, out int tableBits)
    {
        Array.Clear(_counts, 0, _counts.Length);
        foreach (var length in lengths) _counts[length]++;
        _counts[0] = 0;
        tableBits = MaximumBits;
        while (tableBits > 0 && _counts[tableBits] == 0) tableBits--;
        if (tableBits == 0) return true;
        var left = 1;
        for (var length = 1; length <= MaximumBits; length++)
        {
            left = (left << 1) - _counts[length];
            if (left < 0) return false;
        }

        if (left > 0 && (codeLengthCode || tableBits != 1)) return false;
        var code = 0;
        for (var length = 1; length <= MaximumBits; length++)
        {
            code = (code + _counts[length - 1]) << 1;
            _next[length] = code;
        }

        var size = 1 << tableBits;
        Array.Clear(table, 0, size);
        for (var symbol = 0; symbol < lengths.Length; symbol++)
        {
            int length = lengths[symbol];
            if (length == 0) continue;
            var start = (int)GesNetworkDeflateTables.Reverse((uint)_next[length]++, length);
            var entry = (ushort)((symbol << 4) | length);
            for (var index = start; index < size; index += 1 << length) table[index] = entry;
        }

        return true;
    }

    private bool Codes(ref BitReader bits, Span<byte> output, ref int written, int literalBits, int distanceBits)
    {
        while (true)
        {
            var symbol = bits.Decode(_literals, literalBits);
            if (symbol < 0) return false;
            if (symbol < 256)
            {
                if (written == output.Length) return false;
                output[written++] = (byte)symbol;
                continue;
            }

            if (symbol == 256) return true;
            symbol -= 257;
            if (symbol >= 29) return false;
            var extra = bits.Read(GesNetworkDeflateTables.LengthExtra[symbol]);
            if (extra < 0) return false;
            var length = GesNetworkDeflateTables.LengthBase[symbol] + extra;
            var code = bits.Decode(_distances, distanceBits);
            if (code < 0 || code >= 30) return false;
            extra = bits.Read(GesNetworkDeflateTables.DistanceExtra[code]);
            if (extra < 0) return false;
            var distance = GesNetworkDeflateTables.DistanceBase[code] + extra;
            if (distance > written || length > output.Length - written) return false;
            for (var index = 0; index < length; index++, written++) output[written] = output[written - distance];
        }
    }

    // Reads bits from the least significant end of each byte.
    private ref struct BitReader(ReadOnlySpan<byte> input)
    {
        private readonly ReadOnlySpan<byte> _input = input;
        private int _position;
        private ulong _bits;
        private int _count;

        // The bytes read so far, not counting whole bytes still buffered.
        internal readonly int Consumed => _position - (_count >> 3);

        // Reads up to 16 bits; -1 when the stream ends first.
        internal int Read(int length)
        {
            if (length == 0) return 0;
            Fill(length);
            if (_count < length) return -1;
            var value = (int)(_bits & ((1UL << length) - 1));
            _bits >>= length;
            _count -= length;
            return value;
        }

        internal int Decode(ushort[] table, int tableBits)
        {
            if (tableBits == 0) return -1;
            Fill(tableBits);
            int entry = table[(int)(_bits & ((1UL << tableBits) - 1))];
            var length = entry & 15;
            if (length == 0 || length > _count) return -1;
            _bits >>= length;
            _count -= length;
            return entry >> 4;
        }

        // Drops the bits up to the next byte boundary and returns buffered whole bytes to the input.
        internal void Align()
        {
            _position -= _count >> 3;
            _bits = 0;
            _count = 0;
        }

        internal bool CopyBytes(Span<byte> target)
        {
            if (target.Length > _input.Length - _position) return false;
            _input.Slice(_position, target.Length).CopyTo(target);
            _position += target.Length;
            return true;
        }

        private void Fill(int length)
        {
            while (_count < length && _position < _input.Length)
            {
                _bits |= (ulong)_input[_position++] << _count;
                _count += 8;
            }
        }
    }
}
