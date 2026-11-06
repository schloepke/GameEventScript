// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Syntax;

namespace GameEventScript.Api;

/// <summary>The sizes of a decoded delta.</summary>
public readonly struct GesSchemaDeltaDecoded
{
    internal GesSchemaDeltaDecoded(int byteCount, int deltaByteCount)
    {
        ByteCount = byteCount;
        DeltaByteCount = deltaByteCount;
    }

    /// <summary>Gets the bytes of the resulting full record in the buffer.</summary>
    public int ByteCount { get; }

    /// <summary>Gets the bytes the delta occupied; further data may follow them.</summary>
    public int DeltaByteCount { get; }
}

/// <summary>
/// Converts between full records of a schema and deltas against a baseline, the full record the receiver already holds.
/// Both directions compare wire values and allocate nothing on success. Data is untrusted: every limit is checked, and invalid
/// data fails with a <see cref="GesWireException"/>.
/// </summary>
public static class GesSchemaDelta
{
    /// <summary>Writes what differs between a full record and its baseline.</summary>
    /// <param name="schema">The bound schema, of the kind <c>schema</c>.</param>
    /// <param name="record">The full record to send.</param>
    /// <param name="baseline">The full record the receiver holds.</param>
    /// <param name="buffer">The buffer that receives the delta, separate from both records; <see cref="GesBoundSchema.MaxByteCount"/> always suffices.</param>
    /// <returns>The number of bytes written.</returns>
    /// <exception cref="GesWireException">A record is invalid, their keys differ, the buffer overlaps a record, or the schema is no <c>schema</c>.</exception>
    public static int Encode(GesBoundSchema schema, ReadOnlySpan<byte> record, ReadOnlySpan<byte> baseline, Span<byte> buffer)
    {
        Check(schema);
        CheckSeparate(buffer, record, baseline);
        var encoder = new Encoder(schema, record, baseline, buffer);
        return encoder.Run();
    }

    /// <summary>Writes the full record that a delta produces against its baseline.</summary>
    /// <param name="schema">The bound schema, of the kind <c>schema</c>.</param>
    /// <param name="delta">The delta; it may continue after it.</param>
    /// <param name="baseline">The full record this side holds.</param>
    /// <param name="buffer">The buffer that receives the record, separate from the delta and the baseline; <see cref="GesBoundSchema.MaxByteCount"/> always suffices.</param>
    /// <param name="byteLimit">An optional limit on the bytes of this delta.</param>
    /// <returns>The bytes of the record and of the delta.</returns>
    /// <exception cref="GesWireException">The data is no valid delta for this baseline, the buffer overlaps an input, or the schema is no <c>schema</c>.</exception>
    public static GesSchemaDeltaDecoded Decode(GesBoundSchema schema, ReadOnlySpan<byte> delta, ReadOnlySpan<byte> baseline, Span<byte> buffer, long byteLimit = long.MaxValue)
    {
        long bit = 0;
        return Decode(schema, delta, baseline, buffer, byteLimit, ref bit);
    }

    // `bit` holds the position in the delta when it fails, for the dump.
    internal static GesSchemaDeltaDecoded Decode(GesBoundSchema schema, ReadOnlySpan<byte> delta, ReadOnlySpan<byte> baseline, Span<byte> buffer, long byteLimit, ref long bit)
    {
        Check(schema);
        CheckSeparate(buffer, delta, baseline);
        var decoder = new Decoder(schema, delta, baseline, buffer, byteLimit);
        try
        {
            return decoder.Run();
        }
        catch (GesWireException)
        {
            bit = decoder.BitPosition;
            throw;
        }
    }

    private static void Check(GesBoundSchema schema)
    {
        if (schema is null) throw new ArgumentNullException(nameof(schema));
        if (schema.Kind != GesSchemaKind.Schema) throw new GesWireException(GesWireException.TypeMismatch, "");
    }

    // A target that overlaps an input would overwrite bytes that are still to be read.
    private static void CheckSeparate(ReadOnlySpan<byte> buffer, ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        if (buffer.Overlaps(first) || buffer.Overlaps(second)) throw new GesWireException(GesWireException.OverlappingBuffers, "");
    }

    // The difference of two indices without overflow; a wrapping field takes the shorter way, and an exact half period is negative.
    private static ulong Difference(in GesSchemaNode node, ulong index, ulong baseline, out bool negative)
    {
        if (!node.Wrapping)
        {
            negative = index < baseline;
            return negative ? baseline - index : index - baseline;
        }

        var period = node.MaxIndex + 1;
        var forward = index >= baseline ? index - baseline : period - (baseline - index);
        negative = forward >= period - (period / 2);
        return negative ? period - forward : forward;
    }

    // One validated value of a fixed width, or the bits of a number.
    private static ulong Wire(ref GesSchemaReader source, in GesSchemaNode node)
    {
        var value = source.GetBits(node.Width, node.Path);
        if (node.Kind != GesSchemaNodeKind.Float)
        {
            if (value > node.MaxIndex) throw new GesWireException(GesWireException.InvalidIndex, node.Path);
            return value;
        }

        var number = BitConverter.Int64BitsToDouble((long)value);
        if (double.IsNaN(number) || (long)value == long.MinValue || (node.HasRange && double.IsInfinity(number))) throw new GesWireException(GesWireException.InvalidNumber, node.Path);
        if (node.HasRange && (number < node.LowerValue || number > node.UpperValue)) throw new GesWireException(GesWireException.OutOfRange, node.Path);
        return value;
    }

    // A field in its full encoding with its presence bit, validated and written only when asked for.
    private static void FullField(ref GesSchemaReader source, GesSchemaNode[] nodes, int index, ref GesSchemaWriter target, bool write)
    {
        ref readonly var node = ref nodes[index];
        if (node.Optional)
        {
            var present = source.GetBits(1, node.Path);
            if (write) target.PutBits(present, 1);
            if (present == 0) return;
        }

        FullValue(ref source, nodes, index, ref target, write);
    }

    private static void FullValue(ref GesSchemaReader source, GesSchemaNode[] nodes, int index, ref GesSchemaWriter target, bool write)
    {
        ref readonly var node = ref nodes[index];
        switch (node.Kind)
        {
            case GesSchemaNodeKind.Nothing:
                break;
            case GesSchemaNodeKind.Boolean:
                var flag = source.GetBits(1, node.Path);
                if (write) target.PutBits(flag, 1);
                break;
            case GesSchemaNodeKind.Text:
                FullText(ref source, in node, ref target, write);
                break;
            case GesSchemaNodeKind.List:
                var count = source.GetBits(node.Width, node.Path);
                if (count > (ulong)node.MaxCount) throw new GesWireException(GesWireException.InvalidCount, node.Path);
                if (write) target.PutBits(count, node.Width);
                for (var item = 0UL; item < count; item++) FullField(ref source, nodes, index + 1, ref target, write);
                break;
            case GesSchemaNodeKind.Group:
                for (var child = index + 1; child < node.Next; child = nodes[child].Next) FullField(ref source, nodes, child, ref target, write);
                break;
            case GesSchemaNodeKind.Union:
                var position = source.GetBits(node.Width, node.Path);
                if (position > node.MaxIndex) throw new GesWireException(GesWireException.InvalidIndex, node.Path);
                if (write) target.PutBits(position, node.Width);
                FullValue(ref source, nodes, GesSchemaNumbers.Alternative(nodes, index, (int)position), ref target, write);
                break;
            default:
                var value = Wire(ref source, in node);
                if (write) target.PutBits(value, node.Width);
                break;
        }
    }

    // The bytes need not start at a byte boundary, so they are checked as they pass, without a buffer.
    private static void FullText(ref GesSchemaReader source, in GesSchemaNode node, ref GesSchemaWriter target, bool write)
    {
        var count = source.GetBits(node.Width, node.Path);
        if (count > (ulong)node.MaxCount) throw new GesWireException(GesWireException.InvalidCount, node.Path);
        source.Require((long)count * 8, node.Path);
        if (write) target.PutBits(count, node.Width);
        var text = new Utf8Scanner();
        var valid = !node.IsTagText || count > 0;
        for (var index = 0UL; index < count; index++)
        {
            var value = (byte)source.GetBits(8, node.Path);
            if (write) target.PutBits(value, 8);
            if (!valid) continue;
            if (node.IsTagText) valid = value is >= 0x61 and <= 0x7A || (index > 0 && value is >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A);
            else valid = text.Next(value);
        }

        if (!valid) throw new GesWireException(node.IsTagText ? GesWireException.InvalidTag : GesWireException.InvalidText, node.Path);
        if (!node.IsTagText && !text.IsComplete) throw new GesWireException(GesWireException.InvalidText, node.Path);
    }

    private static bool Equal(ref GesSchemaReader first, long firstStart, long firstEnd, ref GesSchemaReader second, long secondStart, long secondEnd)
    {
        if (firstEnd - firstStart != secondEnd - secondStart) return false;
        var equal = true;
        first.Seek(firstStart);
        second.Seek(secondStart);
        for (var left = firstEnd - firstStart; left > 0 && equal;)
        {
            var width = (int)Math.Min(left, 64);
            equal = first.GetBits(width, "") == second.GetBits(width, "");
            left -= width;
        }

        first.Seek(firstEnd);
        second.Seek(secondEnd);
        return equal;
    }

    private struct Utf8Scanner
    {
        private int _more;
        private int _least;
        private int _value;

        internal readonly bool IsComplete => _more == 0;

        internal bool Next(byte next)
        {
            if (_more > 0)
            {
                if ((next & 0xC0) != 0x80) return false;
                _value = (_value << 6) | (next & 0x3F);
                return --_more > 0 || (_value >= _least && _value <= 0x10FFFF && _value is < 0xD800 or > 0xDFFF);
            }

            if (next < 0x80) return true;
            if (next is >= 0xC2 and <= 0xDF) (_more, _least, _value) = (1, 0x80, next & 0x1F);
            else if (next is >= 0xE0 and <= 0xEF) (_more, _least, _value) = (2, 0x800, next & 0x0F);
            else if (next is >= 0xF0 and <= 0xF4) (_more, _least, _value) = (3, 0x10000, next & 0x07);
            else return false;
            return true;
        }
    }

    private ref struct Encoder
    {
        private readonly GesSchemaNode[] _nodes;
        private readonly int _fieldCount;
        private GesSchemaReader _record;
        private GesSchemaReader _baseline;
        private GesSchemaWriter _writer;

        internal Encoder(GesBoundSchema schema, ReadOnlySpan<byte> record, ReadOnlySpan<byte> baseline, Span<byte> buffer)
        {
            _nodes = schema.Nodes;
            _fieldCount = schema.DeltaFieldCount;
            _record = new GesSchemaReader(schema, record);
            _baseline = new GesSchemaReader(schema, baseline);
            _writer = new GesSchemaWriter(schema, buffer);
        }

        internal int Run()
        {
            var start = 0;
            if (_nodes.Length > 0 && _nodes[0].Key)
            {
                ref readonly var key = ref _nodes[0];
                var value = Wire(ref _record, in key);
                if (value != Wire(ref _baseline, in key)) throw new GesWireException(GesWireException.KeyMismatch, key.Path);
                _writer.PutBits(value, key.Width);
                start = key.Next;
            }

            Group(start, _nodes.Length, _fieldCount);
            _record.FinishBits();
            _baseline.FinishBits();
            return _writer.FinishBits();
        }

        // The change bits come first and are set as the fields turn out to differ.
        private bool Group(int start, int end, int count)
        {
            var first = _writer.BitPosition;
            for (var left = count; left > 0; left -= 64) _writer.PutBits(0, Math.Min(left, 64));
            var changed = false;
            var ordinal = 0;
            for (var index = start; index < end; index = _nodes[index].Next, ordinal++)
            {
                if (!Field(index)) continue;
                _writer.SetBit(first + ordinal);
                changed = true;
            }

            return changed;
        }

        // Writes the payload of a changed field and nothing for an unchanged one.
        private bool Field(int index)
        {
            ref readonly var node = ref _nodes[index];
            if (!node.Optional) return Payload(index);
            var present = _record.GetBits(1, node.Path) == 1;
            var held = _baseline.GetBits(1, node.Path) == 1;
            if (!present && !held) return false;
            if (present != held)
            {
                _writer.PutBits(present ? 1UL : 0UL, 1);
                if (present) FullValue(ref _record, _nodes, index, ref _writer, true);
                else FullValue(ref _baseline, _nodes, index, ref _writer, false);
                return true;
            }

            var entry = _writer.BitPosition;
            _writer.PutBits(1, 1);
            if (Payload(index)) return true;
            _writer.Rewind(entry);
            return false;
        }

        private bool Payload(int index)
        {
            ref readonly var node = ref _nodes[index];
            switch (node.Kind)
            {
                case GesSchemaNodeKind.Nothing:
                    return false;
                case GesSchemaNodeKind.Boolean:
                    return _record.GetBits(1, node.Path) != _baseline.GetBits(1, node.Path);
                case GesSchemaNodeKind.Text or GesSchemaNodeKind.List:
                    return Whole(index);
                case GesSchemaNodeKind.Group:
                    var start = _writer.BitPosition;
                    if (Group(index + 1, node.Next, node.ChildCount)) return true;
                    _writer.Rewind(start);
                    return false;
                case GesSchemaNodeKind.Union:
                    return Union(index);
                default:
                    return Number(in node);
            }
        }

        private bool Number(in GesSchemaNode node)
        {
            var value = Wire(ref _record, in node);
            var held = Wire(ref _baseline, in node);
            if (value == held) return false;
            if (node.DeltaRange == 0)
            {
                _writer.PutBits(value, node.Width);
                return true;
            }

            var difference = Difference(in node, value, held, out var negative);
            if (difference > node.DeltaRange)
            {
                _writer.PutBits(1, 1);
                _writer.PutBits(value, node.Width);
                return true;
            }

            _writer.PutBits(0, 1);
            _writer.PutBits(negative ? node.DeltaRange - difference : node.DeltaRange + difference, node.DeltaWidth);
            return true;
        }

        // A Text or List is compared and replaced as a whole.
        private bool Whole(int index)
        {
            var recordStart = _record.BitPosition;
            FullValue(ref _record, _nodes, index, ref _writer, false);
            var recordEnd = _record.BitPosition;
            var baselineStart = _baseline.BitPosition;
            FullValue(ref _baseline, _nodes, index, ref _writer, false);
            if (Equal(ref _record, recordStart, recordEnd, ref _baseline, baselineStart, _baseline.BitPosition)) return false;
            _record.Seek(recordStart);
            FullValue(ref _record, _nodes, index, ref _writer, true);
            return true;
        }

        private bool Union(int index)
        {
            ref readonly var node = ref _nodes[index];
            var position = Wire(ref _record, in node);
            var held = Wire(ref _baseline, in node);
            var alternative = GesSchemaNumbers.Alternative(_nodes, index, (int)position);
            if (position != held)
            {
                _writer.PutBits(position, node.Width);
                FullValue(ref _record, _nodes, alternative, ref _writer, true);
                FullValue(ref _baseline, _nodes, GesSchemaNumbers.Alternative(_nodes, index, (int)held), ref _writer, false);
                return true;
            }

            var entry = _writer.BitPosition;
            _writer.PutBits(position, node.Width);
            if (Payload(alternative)) return true;
            _writer.Rewind(entry);
            return false;
        }
    }

    private ref struct Decoder
    {
        private readonly GesSchemaNode[] _nodes;
        private readonly int _fieldCount;
        private GesSchemaReader _delta;
        private GesSchemaReader _baseline;
        private GesSchemaWriter _writer;

        internal Decoder(GesBoundSchema schema, ReadOnlySpan<byte> delta, ReadOnlySpan<byte> baseline, Span<byte> buffer, long byteLimit)
        {
            _nodes = schema.Nodes;
            _fieldCount = schema.DeltaFieldCount;
            _delta = new GesSchemaReader(schema, delta, byteLimit);
            _baseline = new GesSchemaReader(schema, baseline);
            _writer = new GesSchemaWriter(schema, buffer);
        }

        internal readonly long BitPosition => _delta.BitPosition;

        internal GesSchemaDeltaDecoded Run()
        {
            var start = 0;
            if (_nodes.Length > 0 && _nodes[0].Key)
            {
                ref readonly var key = ref _nodes[0];
                var value = Wire(ref _delta, in key);
                if (value != Wire(ref _baseline, in key)) throw new GesWireException(GesWireException.InvalidDelta, key.Path);
                _writer.PutBits(value, key.Width);
                start = key.Next;
            }

            Group(start, _nodes.Length, _fieldCount, "");
            var deltaBytes = _delta.FinishBits();
            _baseline.FinishBits();
            return new GesSchemaDeltaDecoded(_writer.FinishBits(), deltaBytes);
        }

        private bool Group(int start, int end, int count, string path)
        {
            _delta.Require(count, path);
            var bit = _delta.BitPosition;
            _delta.Seek(bit + count);
            var changed = false;
            for (var index = start; index < end; index = _nodes[index].Next, bit++)
            {
                var payload = _delta.BitPosition;
                _delta.Seek(bit);
                var set = _delta.GetBits(1, _nodes[index].Path) == 1;
                _delta.Seek(payload);
                if (set)
                {
                    Field(index);
                    changed = true;
                }
                else
                {
                    FullField(ref _baseline, _nodes, index, ref _writer, true);
                }
            }

            return changed;
        }

        private void Field(int index)
        {
            ref readonly var node = ref _nodes[index];
            if (!node.Optional)
            {
                Payload(index);
                return;
            }

            var present = _delta.GetBits(1, node.Path) == 1;
            var held = _baseline.GetBits(1, node.Path) == 1;
            if (!present && !held) throw new GesWireException(GesWireException.InvalidDelta, node.Path);
            _writer.PutBits(present ? 1UL : 0UL, 1);
            if (present && held) Payload(index);
            else if (present) FullValue(ref _delta, _nodes, index, ref _writer, true);
            else FullValue(ref _baseline, _nodes, index, ref _writer, false);
        }

        // The payload of a field whose change bit is set, which must be a change.
        private void Payload(int index)
        {
            ref readonly var node = ref _nodes[index];
            switch (node.Kind)
            {
                case GesSchemaNodeKind.Nothing:
                    throw new GesWireException(GesWireException.InvalidDelta, node.Path);
                case GesSchemaNodeKind.Boolean:
                    _writer.PutBits(_baseline.GetBits(1, node.Path) ^ 1, 1);
                    break;
                case GesSchemaNodeKind.Text or GesSchemaNodeKind.List:
                    var deltaStart = _delta.BitPosition;
                    FullValue(ref _delta, _nodes, index, ref _writer, true);
                    var deltaEnd = _delta.BitPosition;
                    var baselineStart = _baseline.BitPosition;
                    FullValue(ref _baseline, _nodes, index, ref _writer, false);
                    if (Equal(ref _delta, deltaStart, deltaEnd, ref _baseline, baselineStart, _baseline.BitPosition)) throw new GesWireException(GesWireException.InvalidDelta, node.Path);
                    break;
                case GesSchemaNodeKind.Group:
                    if (!Group(index + 1, node.Next, node.ChildCount, node.Path)) throw new GesWireException(GesWireException.InvalidDelta, node.Path);
                    break;
                case GesSchemaNodeKind.Union:
                    Union(index);
                    break;
                default:
                    _writer.PutBits(Number(in node), node.Width);
                    break;
            }
        }

        private ulong Number(in GesSchemaNode node)
        {
            var held = Wire(ref _baseline, in node);
            if (node.DeltaRange == 0 || _delta.GetBits(1, node.Path) == 1)
            {
                var value = Wire(ref _delta, in node);
                if (value == held || (node.DeltaRange > 0 && Difference(in node, value, held, out _) <= node.DeltaRange))
                    throw new GesWireException(GesWireException.InvalidDelta, node.Path);
                return value;
            }

            var range = _delta.GetBits(node.DeltaWidth, node.Path);
            if (range > node.DeltaRange * 2 || range == node.DeltaRange) throw new GesWireException(GesWireException.InvalidDelta, node.Path);
            return range > node.DeltaRange ? Forward(in node, held, range - node.DeltaRange) : Backward(in node, held, node.DeltaRange - range);
        }

        private static ulong Forward(in GesSchemaNode node, ulong held, ulong difference)
        {
            if (node.Wrapping) return difference >= node.MaxIndex + 1 - held ? held - (node.MaxIndex + 1 - difference) : held + difference;
            if (difference > node.MaxIndex - held) throw new GesWireException(GesWireException.InvalidDelta, node.Path);
            return held + difference;
        }

        private static ulong Backward(in GesSchemaNode node, ulong held, ulong difference)
        {
            if (difference <= held) return held - difference;
            if (!node.Wrapping) throw new GesWireException(GesWireException.InvalidDelta, node.Path);
            return node.MaxIndex + 1 - (difference - held);
        }

        private void Union(int index)
        {
            ref readonly var node = ref _nodes[index];
            var position = Wire(ref _delta, in node);
            var held = Wire(ref _baseline, in node);
            _writer.PutBits(position, node.Width);
            var alternative = GesSchemaNumbers.Alternative(_nodes, index, (int)position);
            if (position == held)
            {
                Payload(alternative);
                return;
            }

            FullValue(ref _delta, _nodes, alternative, ref _writer, true);
            FullValue(ref _baseline, _nodes, GesSchemaNumbers.Alternative(_nodes, index, (int)held), ref _writer, false);
        }
    }
}
