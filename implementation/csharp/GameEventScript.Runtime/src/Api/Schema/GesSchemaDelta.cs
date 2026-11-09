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
    /// <param name="buffer">The buffer that receives the delta, separate from both records; <see cref="GesBoundSchema.MaxByteCount"/> always suffices. Encoding also uses it as scratch space, so a smaller buffer can fail with <c>wire.bufferTooSmall</c> even when the delta would fit.</param>
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

    private static ulong Difference(in GesSchemaNode node, ulong index, ulong baseline, out bool negative)
        => GesWireRules.Difference(index, baseline, node.MaxIndex, node.Wrapping, out negative);

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

    // The sort value of a key: an integer without a range compares with its sign, every other key by its index.
    private static ulong KeyOrder(in GesSchemaNode key, ulong value) => key.Kind == GesSchemaNodeKind.Integer && !key.HasRange ? value ^ (1UL << 63) : value;

    // A keyed list in its full encoding, validated and written only when asked for; returns whether its keys ascend strictly.
    private static bool Ascending(ref GesSchemaReader source, GesSchemaNode[] nodes, int index, ref GesSchemaWriter target, bool write)
    {
        ref readonly var node = ref nodes[index];
        ref readonly var key = ref nodes[index + 2];
        var count = source.GetBits(node.Width, node.Path);
        if (count > (ulong)node.MaxCount) throw new GesWireException(GesWireException.InvalidCount, node.Path);
        if (write) target.PutBits(count, node.Width);
        var ascending = true;
        ulong previous = 0;
        for (var item = 0UL; item < count; item++)
        {
            var value = Wire(ref source, in key);
            if (write) target.PutBits(value, key.Width);
            var order = KeyOrder(in key, value);
            if (item > 0 && order <= previous) ascending = false;
            previous = order;
            Rest(ref source, nodes, index, ref target, write);
        }

        return ascending;
    }

    // The fields of a keyed list's element after its key.
    private static void Rest(ref GesSchemaReader source, GesSchemaNode[] nodes, int index, ref GesSchemaWriter target, bool write)
    {
        var end = nodes[index + 1].Next;
        for (var child = nodes[index + 2].Next; child < end; child = nodes[child].Next) FullField(ref source, nodes, child, ref target, write);
    }

    // The first pass over a keyed list validates the rest of an element it does not write; later passes only skip it.
    private static void Pass(ref GesSchemaReader source, GesSchemaNode[] nodes, int index, ref GesSchemaWriter target, bool validate)
    {
        if (validate) Rest(ref source, nodes, index, ref target, false);
        else SkipRest(ref source, nodes, index);
    }

    // Passes the fields of a keyed list's element after its key, which an earlier pass validated, without checking them again.
    private static void SkipRest(ref GesSchemaReader source, GesSchemaNode[] nodes, int index)
    {
        var end = nodes[index + 1].Next;
        for (var child = nodes[index + 2].Next; child < end; child = nodes[child].Next) Skip(ref source, nodes, child);
    }

    // Copies the fields of a keyed list's element after its key, which an earlier pass validated, as raw bits.
    private static void CopyRest(ref GesSchemaReader source, GesSchemaNode[] nodes, int index, ref GesSchemaWriter target)
    {
        var start = source.BitPosition;
        SkipRest(ref source, nodes, index);
        var end = source.BitPosition;
        source.Seek(start);
        for (var left = end - start; left > 0;)
        {
            var width = (int)Math.Min(left, 64);
            target.PutBits(source.GetBits(width, ""), width);
            left -= width;
        }
    }

    private static void Skip(ref GesSchemaReader source, GesSchemaNode[] nodes, int index)
    {
        ref readonly var node = ref nodes[index];
        if (node.Optional && source.GetBits(1, node.Path) == 0) return;
        SkipValue(ref source, nodes, index);
    }

    private static void SkipValue(ref GesSchemaReader source, GesSchemaNode[] nodes, int index)
    {
        ref readonly var node = ref nodes[index];
        switch (node.Kind)
        {
            case GesSchemaNodeKind.Text:
                var length = source.GetBits(node.Width, node.Path);
                source.Seek(source.BitPosition + ((long)length * 8));
                break;
            case GesSchemaNodeKind.List:
                var count = source.GetBits(node.Width, node.Path);
                for (var item = 0UL; item < count; item++) Skip(ref source, nodes, index + 1);
                break;
            case GesSchemaNodeKind.Group:
                for (var child = index + 1; child < node.Next; child = nodes[child].Next) Skip(ref source, nodes, child);
                break;
            case GesSchemaNodeKind.Union:
                var position = source.GetBits(node.Width, node.Path);
                SkipValue(ref source, nodes, GesSchemaNumbers.Alternative(nodes, index, (int)position));
                break;
            default:
                source.Seek(source.BitPosition + node.Width);
                break;
        }
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
        private const int Removed = 0;
        private const int Added = 1;
        private const int Changed = 2;

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
                case GesSchemaNodeKind.List when node.KeyedElements:
                    return KeyedList(index);
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

        // Unchanged, whole when one side's keys do not ascend strictly, and otherwise by element. The first pass validates both
        // lists, finds whether they ascend and already writes the removed section, which is dropped again when the list is
        // unchanged or goes whole.
        private bool KeyedList(int index)
        {
            var entry = _writer.BitPosition;
            var recordStart = _record.BitPosition;
            var baselineStart = _baseline.BitPosition;
            _writer.PutBits(0, 1);
            var ascending = Section(index, recordStart, baselineStart, Removed);
            var recordEnd = _record.BitPosition;
            var baselineEnd = _baseline.BitPosition;
            if (Equal(ref _record, recordStart, recordEnd, ref _baseline, baselineStart, baselineEnd))
            {
                _writer.Rewind(entry);
                return false;
            }

            if (!ascending)
            {
                _writer.Rewind(entry);
                _writer.PutBits(1, 1);
                _record.Seek(recordStart);
                FullValue(ref _record, _nodes, index, ref _writer, true);
                return true;
            }

            Section(index, recordStart, baselineStart, Added);
            Section(index, recordStart, baselineStart, Changed);
            _record.Seek(recordEnd);
            _baseline.Seek(baselineEnd);
            return true;
        }

        // One merge pass over both lists writes one section; its count is set afterwards, since only the nested deltas tell
        // whether a kept element changed. Returns whether the keys of both lists ascend strictly; the merge still ends when
        // they do not, and the result is then discarded.
        private bool Section(int index, long recordStart, long baselineStart, int section)
        {
            ref readonly var node = ref _nodes[index];
            ref readonly var key = ref _nodes[index + 2];
            _record.Seek(recordStart);
            _baseline.Seek(baselineStart);
            var recordLeft = _record.GetBits(node.Width, node.Path);
            if (recordLeft > (ulong)node.MaxCount) throw new GesWireException(GesWireException.InvalidCount, node.Path);
            var baselineCount = _baseline.GetBits(node.Width, node.Path);
            if (baselineCount > (ulong)node.MaxCount) throw new GesWireException(GesWireException.InvalidCount, node.Path);
            var countBit = _writer.BitPosition;
            _writer.PutBits(0, node.Width);
            var count = 0UL;
            var position = 0UL;
            var ascending = true;
            var recordOrder = recordLeft > 0 ? KeyOrder(in key, Wire(ref _record, in key)) : 0;
            var baselineOrder = baselineCount > 0 ? KeyOrder(in key, Wire(ref _baseline, in key)) : 0;
            while (recordLeft > 0 || position < baselineCount)
            {
                var holdsRecord = recordLeft > 0;
                var holdsBaseline = position < baselineCount;
                var useRecord = holdsRecord && (!holdsBaseline || recordOrder <= baselineOrder);
                var useBaseline = holdsBaseline && (!holdsRecord || baselineOrder <= recordOrder);
                if (useRecord && useBaseline)
                {
                    if (section == Changed)
                    {
                        var entry = _writer.BitPosition;
                        _writer.PutBits(position, node.PositionWidth);
                        if (Group(key.Next, _nodes[index + 1].Next, _nodes[index + 1].ChildCount - 1)) count++;
                        else _writer.Rewind(entry);
                    }
                    else
                    {
                        Pass(ref _record, _nodes, index, ref _writer, section == Removed);
                        Pass(ref _baseline, _nodes, index, ref _writer, section == Removed);
                    }
                }
                else if (useRecord)
                {
                    if (section == Added)
                    {
                        _writer.PutBits(KeyOrder(in key, recordOrder), key.Width);
                        CopyRest(ref _record, _nodes, index, ref _writer);
                        count++;
                    }
                    else
                    {
                        Pass(ref _record, _nodes, index, ref _writer, section == Removed);
                    }
                }
                else
                {
                    if (section == Removed)
                    {
                        _writer.PutBits(position, node.PositionWidth);
                        count++;
                    }

                    Pass(ref _baseline, _nodes, index, ref _writer, section == Removed);
                }

                if (useRecord && --recordLeft > 0) ascending &= Advance(ref _record, in key, ref recordOrder);
                if (useBaseline && ++position < baselineCount) ascending &= Advance(ref _baseline, in key, ref baselineOrder);
            }

            for (var bit = 0; bit < node.Width; bit++)
                if (((count >> bit) & 1) != 0) _writer.SetBit(countBit + bit);
            return ascending;
        }

        // Reads the next key of one list; returns whether it follows the previous one strictly.
        private static bool Advance(ref GesSchemaReader source, in GesSchemaNode key, ref ulong order)
        {
            var next = KeyOrder(in key, Wire(ref source, in key));
            var follows = next > order;
            order = next;
            return follows;
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
                case GesSchemaNodeKind.List when node.KeyedElements:
                    KeyedList(index);
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

        private void KeyedList(int index)
        {
            ref readonly var node = ref _nodes[index];
            if (_delta.GetBits(1, node.Path) == 1) WholeKeyedList(index);
            else ElementKeyedList(index);
        }

        private void WholeKeyedList(int index)
        {
            ref readonly var node = ref _nodes[index];
            var deltaStart = _delta.BitPosition;
            var deltaAscending = Ascending(ref _delta, _nodes, index, ref _writer, true);
            var deltaEnd = _delta.BitPosition;
            var baselineStart = _baseline.BitPosition;
            var baselineAscending = Ascending(ref _baseline, _nodes, index, ref _writer, false);
            if ((deltaAscending && baselineAscending) || Equal(ref _delta, deltaStart, deltaEnd, ref _baseline, baselineStart, _baseline.BitPosition))
                throw new GesWireException(GesWireException.InvalidDelta, node.Path);
        }

        private ulong Count(in GesSchemaNode node)
        {
            var count = _delta.GetBits(node.Width, node.Path);
            if (count > (ulong)node.MaxCount) throw new GesWireException(GesWireException.InvalidCount, node.Path);
            return count;
        }

        // The removed positions ascending inside the baseline.
        private void Positions(in GesSchemaNode node, ulong count, ulong baselineCount)
        {
            var previous = 0UL;
            for (var item = 0UL; item < count; item++)
            {
                var position = _delta.GetBits(node.PositionWidth, node.Path);
                if (position >= baselineCount || (item > 0 && position <= previous)) throw new GesWireException(GesWireException.InvalidDelta, node.Path);
                previous = position;
            }
        }

        // Checks run in wire order; one that needs the baseline belongs to the item that triggers it. The baseline's order
        // belongs to the form bit, so it is checked first, and an added key that the baseline holds to that added element.
        // The sections are then read side by side through their own cursors. Later walks of the baseline skip what the first one
        // validated, and only copy what stays.
        private void ElementKeyedList(int index)
        {
            ref readonly var node = ref _nodes[index];
            ref readonly var key = ref _nodes[index + 2];
            var baselineStart = _baseline.BitPosition;
            if (!Ascending(ref _baseline, _nodes, index, ref _writer, false)) throw new GesWireException(GesWireException.InvalidDelta, node.Path);
            _baseline.Seek(baselineStart);
            var baselineCount = _baseline.GetBits(node.Width, node.Path);
            var elementsStart = _baseline.BitPosition;

            var removedLeft = Count(in node);
            var removedCursor = _delta.BitPosition;
            Positions(in node, removedLeft, baselineCount);
            var addedLeft = Count(in node);
            var addedCursor = _delta.BitPosition;
            var previous = 0UL;
            var heldLeft = baselineCount;
            var heldOrder = 0UL;
            var holds = false;
            for (var item = 0UL; item < addedLeft; item++)
            {
                var order = KeyOrder(in key, Wire(ref _delta, in key));
                if (item > 0 && order <= previous) throw new GesWireException(GesWireException.InvalidDelta, node.Path);
                previous = order;
                while (true)
                {
                    if (!holds)
                    {
                        if (heldLeft == 0) break;
                        heldOrder = KeyOrder(in key, Wire(ref _baseline, in key));
                        SkipRest(ref _baseline, _nodes, index);
                        heldLeft--;
                        holds = true;
                    }

                    if (heldOrder == order) throw new GesWireException(GesWireException.InvalidDelta, node.Path);
                    if (heldOrder > order) break;
                    holds = false;
                }

                Rest(ref _delta, _nodes, index, ref _writer, false);
            }

            _baseline.Seek(elementsStart);
            var changedLeft = Count(in node);
            var changedCursor = _delta.BitPosition;
            if (removedLeft == 0 && addedLeft == 0 && changedLeft == 0) throw new GesWireException(GesWireException.InvalidDelta, node.Path);
            var resultCount = baselineCount - removedLeft + addedLeft;
            if (resultCount > (ulong)node.MaxCount) throw new GesWireException(GesWireException.InvalidDelta, node.Path);
            _writer.PutBits(resultCount, node.Width);

            var nextRemoved = NextPosition(in node, ref removedCursor, ref removedLeft);
            var nextChanged = NextPosition(in node, ref changedCursor, ref changedLeft);
            var addedKey = addedLeft > 0 ? AddedKey(in key, ref addedCursor) : 0;
            for (var position = 0UL; position < baselineCount; position++)
            {
                var held = Wire(ref _baseline, in key);
                var order = KeyOrder(in key, held);
                while (addedLeft > 0 && KeyOrder(in key, addedKey) < order) WriteAdded(index, ref addedCursor, ref addedLeft, ref addedKey);
                if (position == nextRemoved)
                {
                    if (position == nextChanged) throw new GesWireException(GesWireException.InvalidDelta, node.Path);
                    SkipRest(ref _baseline, _nodes, index);
                    nextRemoved = NextPosition(in node, ref removedCursor, ref removedLeft);
                }
                else if (position == nextChanged)
                {
                    _writer.PutBits(held, key.Width);
                    _delta.Seek(changedCursor);
                    if (!Group(key.Next, _nodes[index + 1].Next, _nodes[index + 1].ChildCount - 1, _nodes[index + 1].Path))
                        throw new GesWireException(GesWireException.InvalidDelta, _nodes[index + 1].Path);
                    changedCursor = _delta.BitPosition;
                    var before = nextChanged;
                    nextChanged = NextPosition(in node, ref changedCursor, ref changedLeft);
                    if (nextChanged <= before) throw new GesWireException(GesWireException.InvalidDelta, node.Path);
                }
                else
                {
                    _writer.PutBits(held, key.Width);
                    CopyRest(ref _baseline, _nodes, index, ref _writer);
                }
            }

            // A changed position beyond the baseline was never reached.
            if (nextChanged != ulong.MaxValue) throw new GesWireException(GesWireException.InvalidDelta, node.Path);
            while (addedLeft > 0) WriteAdded(index, ref addedCursor, ref addedLeft, ref addedKey);
            _delta.Seek(changedCursor);
        }

        // The next position of a section, or the largest number when it has ended.
        private ulong NextPosition(in GesSchemaNode node, ref long cursor, ref ulong left)
        {
            if (left == 0) return ulong.MaxValue;
            left--;
            _delta.Seek(cursor);
            var position = _delta.GetBits(node.PositionWidth, node.Path);
            cursor = _delta.BitPosition;
            return position;
        }

        private ulong AddedKey(in GesSchemaNode key, ref long cursor)
        {
            _delta.Seek(cursor);
            var value = Wire(ref _delta, in key);
            cursor = _delta.BitPosition;
            return value;
        }

        private void WriteAdded(int index, ref long cursor, ref ulong left, ref ulong addedKey)
        {
            ref readonly var key = ref _nodes[index + 2];
            _writer.PutBits(addedKey, key.Width);
            _delta.Seek(cursor);
            CopyRest(ref _delta, _nodes, index, ref _writer);
            cursor = _delta.BitPosition;
            if (--left > 0) addedKey = AddedKey(in key, ref cursor);
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
