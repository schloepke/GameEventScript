// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Buffers.Binary;
using System.Text;
using GameEventScript.Syntax;

namespace GameEventScript.Api;

// The running lists of a writer or reader, without allocation. Lists nest at most four levels for encoding.
internal struct GesListFrames
{
    private int _end0, _end1, _end2, _end3;
    private int _element0, _element1, _element2, _element3;
    private long _left0, _left1, _left2, _left3;
    private int _depth;

    internal void Push(int end, int element, long count)
    {
        switch (_depth++)
        {
            case 0: _end0 = end; _element0 = element; _left0 = count; break;
            case 1: _end1 = end; _element1 = element; _left1 = count; break;
            case 2: _end2 = end; _element2 = element; _left2 = count; break;
            default: _end3 = end; _element3 = element; _left3 = count; break;
        }
    }

    // The node that follows `next`: the same element again, or on past finished lists.
    internal int Advance(int next)
    {
        while (_depth > 0)
        {
            switch (_depth)
            {
                case 1:
                    if (next != _end0) return next;
                    if (--_left0 > 0) return _element0;
                    break;
                case 2:
                    if (next != _end1) return next;
                    if (--_left1 > 0) return _element1;
                    break;
                case 3:
                    if (next != _end2) return next;
                    if (--_left2 > 0) return _element2;
                    break;
                default:
                    if (next != _end3) return next;
                    if (--_left3 > 0) return _element3;
                    break;
            }

            _depth--;
        }

        return next;
    }
}

// The exact number rules of a quantized field and UTF-8 validation, shared by writer and reader.
internal static class GesSchemaNumbers
{
    private const double LargestSteps = 1125899906842624d;

    // The index of a value; a value without one throws its wire error.
    internal static ulong Index(in GesSchemaNode node, double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) throw new GesWireException(GesWireException.OutOfRange, node.Path);
        if (!node.Wrapping && (value < node.LowerValue || value > node.UpperValue)) throw new GesWireException(GesWireException.OutOfRange, node.Path);
        var quotient = value * node.SBinary / node.PBinary;
        if (quotient > LargestSteps || quotient < -LargestSteps) throw new GesWireException(GesWireException.OutOfRange, node.Path);
        var steps = (long)Math.Round(quotient, MidpointRounding.ToEven);
        return node.Wrapping ? Wrap(steps, node.Lower, node.MaxIndex + 1) : (ulong)(steps - node.Lower);
    }

    internal static ulong Wrap(long value, long lower, ulong period)
    {
        if (value >= lower) return ((ulong)value - (ulong)lower) % period;
        var remainder = ((ulong)lower - (ulong)value) % period;
        return remainder == 0 ? 0 : period - remainder;
    }

    internal static double Value(in GesSchemaNode node, ulong index) => (((long)index + node.Lower) * node.P) / node.SBinary;

    internal static int Alternative(GesSchemaNode[] nodes, int union, int alternative)
    {
        var index = union + 1;
        for (var skipped = 0; skipped < alternative; skipped++) index = nodes[index].Next;
        return index;
    }

    // The portable tag grammar on bytes: a lowercase ASCII letter, then ASCII letters and digits.
    internal static bool IsTagName(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0 || bytes[0] is < 0x61 or > 0x7A) return false;
        for (var index = 1; index < bytes.Length; index++)
            if (bytes[index] is not (>= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A or >= 0x61 and <= 0x7A)) return false;
        return true;
    }

    internal static bool IsValidUtf8(ReadOnlySpan<byte> bytes)
    {
        var index = 0;
        while (index < bytes.Length)
        {
            var first = bytes[index];
            int more;
            int least;
            int value;
            if (first < 0x80)
            {
                index++;
                continue;
            }

            if (first is >= 0xC2 and <= 0xDF) (more, least, value) = (1, 0x80, first & 0x1F);
            else if (first is >= 0xE0 and <= 0xEF) (more, least, value) = (2, 0x800, first & 0x0F);
            else if (first is >= 0xF0 and <= 0xF4) (more, least, value) = (3, 0x10000, first & 0x07);
            else return false;
            if (index + more >= bytes.Length) return false;
            for (var offset = 1; offset <= more; offset++)
            {
                var next = bytes[index + offset];
                if ((next & 0xC0) != 0x80) return false;
                value = (value << 6) | (next & 0x3F);
            }

            if (value < least || value > 0x10FFFF || value is >= 0xD800 and <= 0xDFFF) return false;
            index += more + 1;
        }

        return true;
    }
}

/// <summary>
/// Encodes one top-level record or message into a caller's buffer without allocating on success. Fields are written in
/// declaration order; a nested schema, Point or Vector is entered with <see cref="BeginNested"/>.
/// </summary>
public ref struct GesSchemaWriter
{
    private readonly Span<byte> _buffer;
    private GesBoundSchema _schema;
    private GesSchemaNode[] _nodes;
    private GesListFrames _lists;
    private int _cursor;
    private int _position;
    private ulong _pending;
    private int _pendingBits;

    /// <summary>Starts a record or message in a buffer.</summary>
    /// <param name="schema">The bound schema; for a message set, select a member first.</param>
    /// <param name="buffer">The buffer that receives the encoding.</param>
    public GesSchemaWriter(GesBoundSchema schema, Span<byte> buffer)
    {
        _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        _nodes = schema.Nodes;
        _buffer = buffer;
        _lists = default;
        _cursor = 0;
        _position = 0;
        _pending = 0;
        _pendingBits = 0;
    }

    /// <summary>Selects a member of a message set and writes its index.</summary>
    /// <param name="member">The member's index in declared order.</param>
    /// <exception cref="GesWireException">The schema is no message set, or a member was already selected.</exception>
    public void SelectMember(int member)
    {
        if (_schema.Kind != GesSchemaKind.Messages) throw new GesWireException(GesWireException.TypeMismatch, "");
        if (member < 0 || member >= _schema.Members.Length) throw new GesWireException(GesWireException.OutOfRange, "");
        Put((ulong)member, _schema.MemberWidth);
        _schema = _schema.Members[member];
        _nodes = _schema.Nodes;
    }

    /// <summary>Writes a Boolean field or a message tag.</summary>
    /// <param name="field">The field's handle.</param>
    /// <param name="value">The value.</param>
    public void WriteBoolean(GesSchemaField field, bool value)
    {
        ref readonly var node = ref Enter(field, GesSchemaNodeKind.Boolean);
        Put(value ? 1UL : 0UL, 1);
        Leave(node.After);
    }

    /// <summary>Writes an Int64 to a numeric field.</summary>
    /// <param name="field">The field's handle.</param>
    /// <param name="value">The value.</param>
    public void WriteInteger(GesSchemaField field, long value)
    {
        ref readonly var node = ref Current(field);
        switch (node.Kind)
        {
            case GesSchemaNodeKind.Integer:
                ulong index;
                if (!node.HasRange) index = (ulong)value;
                else if (node.Wrapping) index = GesSchemaNumbers.Wrap(value, node.Lower, node.MaxIndex + 1);
                else
                {
                    if (value < node.Lower || (ulong)value - (ulong)node.Lower > node.MaxIndex) throw new GesWireException(GesWireException.OutOfRange, node.Path);
                    index = (ulong)value - (ulong)node.Lower;
                }

                Presence(in node);
                Put(index, node.Width);
                Leave(node.After);
                break;
            case GesSchemaNodeKind.Quantized:
                if (value is >= (1L << 53) or <= -(1L << 53)) throw new GesWireException(GesWireException.OutOfRange, node.Path);
                WriteNumber(field, value);
                break;
            case GesSchemaNodeKind.Float:
                var converted = (double)value;
                if (converted >= 9223372036854775808d || (long)converted != value) throw new GesWireException(GesWireException.NotRepresentable, node.Path);
                WriteNumber(field, converted);
                break;
            default:
                throw new GesWireException(GesWireException.TypeMismatch, node.Path);
        }
    }

    /// <summary>Writes a Binary64 to a numeric field. A quantized field rounds it to its step.</summary>
    /// <param name="field">The field's handle.</param>
    /// <param name="value">The value.</param>
    public void WriteNumber(GesSchemaField field, double value)
    {
        ref readonly var node = ref Current(field);
        switch (node.Kind)
        {
            case GesSchemaNodeKind.Quantized:
                var index = GesSchemaNumbers.Index(in node, value);
                Presence(in node);
                Put(index, node.Width);
                Leave(node.After);
                break;
            case GesSchemaNodeKind.Float:
                if (double.IsNaN(value)) throw new GesWireException(GesWireException.NotRepresentable, node.Path);
                if (node.HasRange && (value < node.LowerValue || value > node.UpperValue)) throw new GesWireException(GesWireException.OutOfRange, node.Path);
                Presence(in node);
                Put((ulong)BitConverter.DoubleToInt64Bits(value == 0 ? 0d : value), 64);
                Leave(node.After);
                break;
            case GesSchemaNodeKind.Integer:
                if (value != Math.Floor(value) || value is >= 9223372036854775808d or < -9223372036854775808d || double.IsNaN(value))
                    throw new GesWireException(GesWireException.NotRepresentable, node.Path);
                WriteInteger(field, (long)value);
                break;
            default:
                throw new GesWireException(GesWireException.TypeMismatch, node.Path);
        }
    }

    /// <summary>Writes the raw wire index of a ranged field, for callers that store quantized values.</summary>
    /// <param name="field">The field's handle.</param>
    /// <param name="index">The index.</param>
    public void WriteIndex(GesSchemaField field, ulong index)
    {
        ref readonly var node = ref Current(field);
        if (node.Kind is not (GesSchemaNodeKind.Integer or GesSchemaNodeKind.Quantized or GesSchemaNodeKind.Tag)) throw new GesWireException(GesWireException.TypeMismatch, node.Path);
        if (index > node.MaxIndex) throw new GesWireException(GesWireException.OutOfRange, node.Path);
        Presence(in node);
        Put(index, node.Width);
        Leave(node.After);
    }

    /// <summary>Writes a tag of a closed set by its index in declared order.</summary>
    /// <param name="field">The field's handle.</param>
    /// <param name="tagIndex">The index.</param>
    public void WriteTag(GesSchemaField field, int tagIndex)
    {
        ref readonly var node = ref Current(field);
        if (node.Kind != GesSchemaNodeKind.Tag) throw new GesWireException(GesWireException.TypeMismatch, node.Path);
        if (tagIndex < 0 || (ulong)tagIndex > node.MaxIndex) throw new GesWireException(GesWireException.UnknownTag, node.Path);
        Presence(in node);
        Put((ulong)tagIndex, node.Width);
        Leave(node.After);
    }

    /// <summary>Writes a Text from its UTF-8 bytes, or a <c>:Tag max n</c> from its name.</summary>
    /// <param name="field">The field's handle.</param>
    /// <param name="utf8">The bytes, which must be valid UTF-8, or a tag name for a <c>:Tag max n</c>, without its <c>#</c>.</param>
    public void WriteText(GesSchemaField field, ReadOnlySpan<byte> utf8)
    {
        ref readonly var node = ref Current(field);
        if (node.Kind != GesSchemaNodeKind.Text) throw new GesWireException(GesWireException.TypeMismatch, node.Path);
        if (utf8.Length > node.MaxCount) throw new GesWireException(GesWireException.TooLong, node.Path);
        if (node.IsTagText)
        {
            if (!GesSchemaNumbers.IsTagName(utf8)) throw new GesWireException(GesWireException.InvalidTag, node.Path);
        }
        else if (!GesSchemaNumbers.IsValidUtf8(utf8)) throw new GesWireException(GesWireException.InvalidText, node.Path);
        Presence(in node);
        Put((ulong)utf8.Length, node.Width);
        foreach (var value in utf8) Put(value, 8);
        Leave(node.After);
    }

    /// <summary>Starts a list with its element count; the elements follow.</summary>
    /// <param name="field">The field's handle.</param>
    /// <param name="count">The element count.</param>
    public void BeginList(GesSchemaField field, int count)
    {
        ref readonly var node = ref Current(field);
        if (node.Kind != GesSchemaNodeKind.List) throw new GesWireException(GesWireException.TypeMismatch, node.Path);
        if (count < 0 || count > node.MaxCount) throw new GesWireException(GesWireException.TooLong, node.Path);
        Presence(in node);
        Put((ulong)count, node.Width);
        if (count == 0)
        {
            Leave(node.After);
            return;
        }

        _lists.Push(node.After, field.Index + 1, count);
        _cursor = field.Index + 1;
    }

    /// <summary>Enters a nested schema, Point or Vector; its fields follow.</summary>
    /// <param name="field">The field's handle.</param>
    public void BeginNested(GesSchemaField field)
    {
        ref readonly var node = ref Enter(field, GesSchemaNodeKind.Group);
        Leave(node.Next == field.Index + 1 ? node.After : field.Index + 1);
    }

    /// <summary>Selects an alternative of a union and writes its index; the alternative follows, unless it is <c>:Nothing</c>.</summary>
    /// <param name="field">The union's handle.</param>
    /// <param name="alternative">The alternative's index in declared order.</param>
    public void SelectAlternative(GesSchemaField field, int alternative)
    {
        ref readonly var node = ref Current(field);
        if (node.Kind != GesSchemaNodeKind.Union) throw new GesWireException(GesWireException.TypeMismatch, node.Path);
        if (alternative < 0 || (ulong)alternative > node.MaxIndex) throw new GesWireException(GesWireException.OutOfRange, node.Path);
        Presence(in node);
        Put((ulong)alternative, node.Width);
        var selected = GesSchemaNumbers.Alternative(_nodes, field.Index, alternative);
        if (_nodes[selected].Kind == GesSchemaNodeKind.Nothing) Leave(_nodes[selected].After);
        else _cursor = selected;
    }

    /// <summary>Marks an <c>optional</c> field as absent.</summary>
    /// <param name="field">The field's handle.</param>
    public void WriteAbsent(GesSchemaField field)
    {
        ref readonly var node = ref Current(field);
        if (!node.Optional) throw new GesWireException(GesWireException.MissingField, node.Path);
        Put(0, 1);
        Leave(node.After);
    }

    /// <summary>Ends the record with padding to a byte boundary.</summary>
    /// <returns>The number of bytes written.</returns>
    public int Finish()
    {
        if (_schema.Kind == GesSchemaKind.Messages) throw new GesWireException(GesWireException.MissingField, "");
        if (_cursor != _nodes.Length) throw new GesWireException(GesWireException.MissingField, _nodes[_cursor].Path);
        return FinishBits();
    }

    internal readonly long BitPosition => (_position * 8L) + _pendingBits;

    internal void PutBits(ulong value, int width) => Put(value, width);

    // Sets a bit that was already written as zero, in the buffer or still pending.
    internal void SetBit(long bit)
    {
        var flushed = _position * 8L;
        if (bit >= flushed) _pending |= 1UL << (int)(bit - flushed);
        else _buffer[(int)(bit >> 3)] |= (byte)(1 << (int)(bit & 7));
    }

    // Goes back to an earlier bit. Whole words are flushed, so the word that holds it can be read back.
    internal void Rewind(long bit)
    {
        var position = (int)((bit >> 6) << 3);
        if (position < _position) _pending = BinaryPrimitives.ReadUInt64LittleEndian(_buffer.Slice(position));
        _position = position;
        _pendingBits = (int)(bit & 63);
        _pending = _pendingBits == 0 ? 0 : _pending & ((1UL << _pendingBits) - 1);
    }

    internal int FinishBits()
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

    private ref readonly GesSchemaNode Current(GesSchemaField field)
    {
        if (!ReferenceEquals(field.Owner, _schema) || field.Index != _cursor || _cursor >= _nodes.Length)
            throw new GesWireException(GesWireException.FieldOrder, field.Owner is null ? "" : field.Path);
        return ref _nodes[_cursor];
    }

    private ref readonly GesSchemaNode Enter(GesSchemaField field, GesSchemaNodeKind kind)
    {
        ref readonly var node = ref Current(field);
        if (node.Kind != kind) throw new GesWireException(GesWireException.TypeMismatch, node.Path);
        Presence(in node);
        return ref node;
    }

    private void Presence(in GesSchemaNode node)
    {
        if (node.Optional) Put(1, 1);
    }

    private void Leave(int next) => _cursor = _lists.Advance(next);

    private void Put(ulong value, int width)
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
}

/// <summary>
/// Decodes one top-level record or message without allocating on success. Data is untrusted: every limit is checked, and
/// invalid data fails with a <see cref="GesWireException"/>. Fields are read in declaration order.
/// </summary>
public ref struct GesSchemaReader
{
    private readonly ReadOnlySpan<byte> _data;
    private readonly long _bitCount;
    private GesBoundSchema _schema;
    private GesSchemaNode[] _nodes;
    private GesListFrames _lists;
    private int _cursor;
    private long _bit;
    private bool _present;
    private readonly bool _limited;

    /// <summary>Starts reading a record or message.</summary>
    /// <param name="schema">The bound schema; for a message set, read the member first.</param>
    /// <param name="data">The data; it may continue after the record.</param>
    /// <param name="byteLimit">An optional limit on the bytes of this record.</param>
    public GesSchemaReader(GesBoundSchema schema, ReadOnlySpan<byte> data, long byteLimit = long.MaxValue)
    {
        _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        _nodes = schema.Nodes;
        _data = data;
        _bitCount = Math.Min(data.Length, byteLimit) * 8L;
        _limited = data.Length > byteLimit;
        _lists = default;
        _cursor = 0;
        _bit = 0;
        _present = false;
    }

    /// <summary>Reads which member of a message set follows, and selects it.</summary>
    /// <returns>The member's index in declared order.</returns>
    public int ReadMember()
    {
        if (_schema.Kind != GesSchemaKind.Messages) throw new GesWireException(GesWireException.TypeMismatch, "");
        var index = Get(_schema.MemberWidth, "");
        if (index >= (ulong)_schema.Members.Length) throw new GesWireException(GesWireException.InvalidIndex, "");
        _schema = _schema.Members[(int)index];
        _nodes = _schema.Nodes;
        return (int)index;
    }

    /// <summary>Reads the key, which comes first, before the caller chooses a baseline.</summary>
    /// <returns>The key as an Int64, or the tag index of a Tag key.</returns>
    public long ReadKey()
    {
        if (_cursor != 0 || _nodes.Length == 0 || !_nodes[0].Key) throw new GesWireException(GesWireException.FieldOrder, "");
        var field = new GesSchemaField(_schema, 0);
        return _nodes[0].Kind == GesSchemaNodeKind.Tag ? ReadTag(field) : ReadInteger(field);
    }

    /// <summary>Reads whether an <c>optional</c> field is present. An absent field is complete.</summary>
    /// <param name="field">The field's handle.</param>
    /// <returns>Whether a value follows.</returns>
    public bool ReadPresence(GesSchemaField field)
    {
        ref readonly var node = ref Current(field);
        if (!node.Optional || _present) return true;
        if (Get(1, node.Path) == 1)
        {
            _present = true;
            return true;
        }

        Leave(node.After);
        return false;
    }

    /// <summary>Reads a Boolean field or a message tag.</summary>
    /// <param name="field">The field's handle.</param>
    /// <returns>The value.</returns>
    public bool ReadBoolean(GesSchemaField field)
    {
        ref readonly var node = ref Enter(field, GesSchemaNodeKind.Boolean);
        var value = Get(1, node.Path) == 1;
        Leave(node.After);
        return value;
    }

    /// <summary>Reads an <c>integer</c> field.</summary>
    /// <param name="field">The field's handle.</param>
    /// <returns>The value.</returns>
    public long ReadInteger(GesSchemaField field)
    {
        ref readonly var node = ref Enter(field, GesSchemaNodeKind.Integer);
        var index = Get(node.Width, node.Path);
        if (index > node.MaxIndex) throw new GesWireException(GesWireException.InvalidIndex, node.Path);
        Leave(node.After);
        return node.HasRange ? (long)((ulong)node.Lower + index) : (long)index;
    }

    /// <summary>Reads a numeric field as a Binary64, without boxing.</summary>
    /// <param name="field">The field's handle.</param>
    /// <returns>The decoded value.</returns>
    public double ReadNumber(GesSchemaField field)
    {
        ref readonly var node = ref Current(field);
        switch (node.Kind)
        {
            case GesSchemaNodeKind.Quantized:
                var index = EnteredIndex(in node);
                Leave(node.After);
                return GesSchemaNumbers.Value(in node, index);
            case GesSchemaNodeKind.Float:
                RequirePresence(in node);
                var bits = (long)Get(64, node.Path);
                var value = BitConverter.Int64BitsToDouble(bits);
                if (double.IsNaN(value) || bits == long.MinValue || (node.HasRange && double.IsInfinity(value)) || (node.HasRange && (value < node.LowerValue || value > node.UpperValue)))
                    throw new GesWireException(double.IsNaN(value) || bits == long.MinValue || double.IsInfinity(value) ? GesWireException.InvalidNumber : GesWireException.OutOfRange, node.Path);
                _present = false;
                Leave(node.After);
                return value;
            case GesSchemaNodeKind.Integer:
                return ReadInteger(field);
            default:
                throw new GesWireException(GesWireException.TypeMismatch, node.Path);
        }
    }

    /// <summary>Reads the raw wire index of a ranged field, for callers that store quantized values.</summary>
    /// <param name="field">The field's handle.</param>
    /// <returns>The index.</returns>
    public ulong ReadIndex(GesSchemaField field)
    {
        ref readonly var node = ref Current(field);
        if (node.Kind is not (GesSchemaNodeKind.Integer or GesSchemaNodeKind.Quantized or GesSchemaNodeKind.Tag)) throw new GesWireException(GesWireException.TypeMismatch, node.Path);
        var index = EnteredIndex(in node);
        Leave(node.After);
        return index;
    }

    /// <summary>Reads a tag of a closed set as its index in declared order.</summary>
    /// <param name="field">The field's handle.</param>
    /// <returns>The index.</returns>
    public int ReadTag(GesSchemaField field)
    {
        ref readonly var node = ref Enter(field, GesSchemaNodeKind.Tag);
        var index = Get(node.Width, node.Path);
        if (index > node.MaxIndex) throw new GesWireException(GesWireException.InvalidIndex, node.Path);
        Leave(node.After);
        return (int)index;
    }

    /// <summary>Reads a Text into a caller's buffer as validated UTF-8, or the name of a <c>:Tag max n</c>.</summary>
    /// <param name="field">The field's handle.</param>
    /// <param name="destination">The buffer; its maximum is the field's <c>max</c>.</param>
    /// <returns>The number of bytes.</returns>
    public int ReadText(GesSchemaField field, Span<byte> destination)
    {
        ref readonly var node = ref Enter(field, GesSchemaNodeKind.Text);
        var count = Get(node.Width, node.Path);
        if (count > (ulong)node.MaxCount) throw new GesWireException(GesWireException.InvalidCount, node.Path);
        if ((long)count > destination.Length) throw new GesWireException(GesWireException.BufferTooSmall, node.Path);
        if (_bit + ((long)count * 8) > _bitCount) throw new GesWireException(End, node.Path);
        ReadTextBytes(in node, destination.Slice(0, (int)count));
        return (int)count;
    }

    // Allocates only what the data holds, for the rare paths that build a string.
    internal string ReadTextValue(GesSchemaField field)
    {
        ref readonly var node = ref Enter(field, GesSchemaNodeKind.Text);
        var count = Get(node.Width, node.Path);
        if (count > (ulong)node.MaxCount) throw new GesWireException(GesWireException.InvalidCount, node.Path);
        if (_bit + ((long)count * 8) > _bitCount) throw new GesWireException(End, node.Path);
        var bytes = new byte[(int)count];
        ReadTextBytes(in node, bytes);
        return Encoding.UTF8.GetString(bytes);
    }

    private void ReadTextBytes(in GesSchemaNode node, Span<byte> destination)
    {
        for (var index = 0; index < destination.Length; index++) destination[index] = (byte)Get(8, node.Path);
        if (node.IsTagText)
        {
            if (!GesSchemaNumbers.IsTagName(destination)) throw new GesWireException(GesWireException.InvalidTag, node.Path);
        }
        else if (!GesSchemaNumbers.IsValidUtf8(destination)) throw new GesWireException(GesWireException.InvalidText, node.Path);
        Leave(node.After);
    }

    /// <summary>Starts a list; its elements follow.</summary>
    /// <param name="field">The field's handle.</param>
    /// <returns>The element count.</returns>
    public int BeginList(GesSchemaField field)
    {
        ref readonly var node = ref Enter(field, GesSchemaNodeKind.List);
        var count = Get(node.Width, node.Path);
        if (count > (ulong)node.MaxCount) throw new GesWireException(GesWireException.InvalidCount, node.Path);
        if (count == 0)
        {
            Leave(node.After);
            return 0;
        }

        _lists.Push(node.After, field.Index + 1, (long)count);
        _cursor = field.Index + 1;
        return (int)count;
    }

    /// <summary>Enters a nested schema, Point or Vector; its fields follow.</summary>
    /// <param name="field">The field's handle.</param>
    public void BeginNested(GesSchemaField field)
    {
        ref readonly var node = ref Enter(field, GesSchemaNodeKind.Group);
        Leave(node.Next == field.Index + 1 ? node.After : field.Index + 1);
    }

    /// <summary>Reads which alternative of a union follows, and selects it. A <c>:Nothing</c> alternative is complete.</summary>
    /// <param name="field">The union's handle.</param>
    /// <returns>The alternative's index in declared order.</returns>
    public int ReadAlternative(GesSchemaField field)
    {
        ref readonly var node = ref Enter(field, GesSchemaNodeKind.Union);
        var index = Get(node.Width, node.Path);
        if (index > node.MaxIndex) throw new GesWireException(GesWireException.InvalidIndex, node.Path);
        var selected = GesSchemaNumbers.Alternative(_nodes, field.Index, (int)index);
        if (_nodes[selected].Kind == GesSchemaNodeKind.Nothing) Leave(_nodes[selected].After);
        else _cursor = selected;
        return (int)index;
    }

    /// <summary>Ends the record and checks its padding.</summary>
    /// <returns>The bytes the record occupies; further records may follow them.</returns>
    public int Finish()
    {
        if (_schema.Kind == GesSchemaKind.Messages) throw new GesWireException(GesWireException.FieldOrder, "");
        if (_cursor != _nodes.Length) throw new GesWireException(GesWireException.FieldOrder, _nodes[_cursor].Path);
        return FinishBits();
    }

    internal readonly long BitPosition => _bit;

    internal ulong GetBits(int width, string path) => Get(width, path);

    // Fails like reading when the data ends before these bits do.
    internal readonly void Require(long bits, string path)
    {
        if (_bit + bits > _bitCount) throw new GesWireException(End, path);
    }

    internal void Seek(long bit) => _bit = bit;

    internal int FinishBits()
    {
        var rest = (int)(-_bit & 7);
        if (rest > 0 && Get(rest, "") != 0) throw new GesWireException(GesWireException.InvalidPadding, "");
        return (int)(_bit / 8);
    }

    private readonly string End => _limited ? GesWireException.LimitExceeded : GesWireException.Truncated;

    private ulong EnteredIndex(in GesSchemaNode node)
    {
        RequirePresence(in node);
        _present = false;
        var index = Get(node.Width, node.Path);
        if (index > node.MaxIndex) throw new GesWireException(GesWireException.InvalidIndex, node.Path);
        return index;
    }

    private void RequirePresence(in GesSchemaNode node)
    {
        if (node.Optional && !_present) throw new GesWireException(GesWireException.FieldOrder, node.Path);
    }

    private ref readonly GesSchemaNode Current(GesSchemaField field)
    {
        if (!ReferenceEquals(field.Owner, _schema) || field.Index != _cursor || _cursor >= _nodes.Length)
            throw new GesWireException(GesWireException.FieldOrder, field.Owner is null ? "" : field.Path);
        return ref _nodes[_cursor];
    }

    private ref readonly GesSchemaNode Enter(GesSchemaField field, GesSchemaNodeKind kind)
    {
        ref readonly var node = ref Current(field);
        if (node.Kind != kind) throw new GesWireException(GesWireException.TypeMismatch, node.Path);
        RequirePresence(in node);
        _present = false;
        return ref node;
    }

    private void Leave(int next) => _cursor = _lists.Advance(next);

    private ulong Get(int width, string path)
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
}
