// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
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

// The node-based forms of the wire rules, shared by writer, reader, deltas and values.
internal static class GesSchemaNumbers
{
    // The index of a value; a value without one throws its wire error.
    internal static ulong Index(in GesSchemaNode node, double value) => node.Wrapping
        ? GesWireRules.WrappedQuantizedIndex(value, node.SBinary, node.PBinary, node.Lower, node.MaxIndex + 1, node.Path)
        : GesWireRules.QuantizedIndex(value, node.LowerValue, node.UpperValue, node.SBinary, node.PBinary, node.Lower, node.Path);

    internal static double Value(in GesSchemaNode node, ulong index) => GesWireRules.QuantizedValue(index, node.Lower, node.P, node.SBinary);

    internal static int Alternative(GesSchemaNode[] nodes, int union, int alternative)
    {
        var index = union + 1;
        for (var skipped = 0; skipped < alternative; skipped++) index = nodes[index].Next;
        return index;
    }
}

/// <summary>
/// Encodes one top-level record or message into a caller's buffer without allocating on success. Fields are written in
/// declaration order; a nested schema, Point or Vector is entered with <see cref="BeginNested"/>.
/// </summary>
public ref struct GesSchemaWriter
{
    private GesBitWriter _bits;
    private GesBoundSchema _schema;
    private GesSchemaNode[] _nodes;
    private GesListFrames _lists;
    private int _cursor;

    /// <summary>Starts a record or message in a buffer.</summary>
    /// <param name="schema">The bound schema; for a message set, select a member first.</param>
    /// <param name="buffer">The buffer that receives the encoding.</param>
    public GesSchemaWriter(GesBoundSchema schema, Span<byte> buffer)
    {
        _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        _nodes = schema.Nodes;
        _bits = new GesBitWriter(buffer);
        _lists = default;
        _cursor = 0;
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
                else if (node.Wrapping) index = GesWireRules.WrappedIndex(value, node.Lower, node.MaxIndex + 1);
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

    /// <summary>Writes a <c>:Flags</c> field as a mask whose bit <c>i</c> sets the member at index <c>i</c> in declared order.</summary>
    /// <param name="field">The field's handle.</param>
    /// <param name="mask">The mask; a bit beyond the last member is an unknown tag.</param>
    public void WriteFlags(GesSchemaField field, ulong mask)
    {
        ref readonly var node = ref Current(field);
        if (node.Kind != GesSchemaNodeKind.Flags) throw new GesWireException(GesWireException.TypeMismatch, node.Path);
        if (mask > node.MaxIndex) throw new GesWireException(GesWireException.UnknownTag, node.Path);
        Presence(in node);
        Put(mask, node.Width);
        Leave(node.After);
    }

    /// <summary>Writes a Text from its UTF-8 bytes, or a <c>:Tag max n</c> from its name.</summary>
    /// <param name="field">The field's handle.</param>
    /// <param name="utf8">The bytes, which must be valid UTF-8, or a tag name for a <c>:Tag max n</c>, without its <c>#</c>.</param>
    public void WriteText(GesSchemaField field, ReadOnlySpan<byte> utf8)
    {
        ref readonly var node = ref Current(field);
        if (node.Kind != GesSchemaNodeKind.Text || node.IsBytes) throw new GesWireException(GesWireException.TypeMismatch, node.Path);
        if (utf8.Length > node.MaxCount) throw new GesWireException(GesWireException.TooLong, node.Path);
        if (node.IsTagText)
        {
            if (!GesWireRules.IsTagName(utf8)) throw new GesWireException(GesWireException.InvalidTag, node.Path);
        }
        else if (!GesWireRules.IsValidUtf8(utf8)) throw new GesWireException(GesWireException.InvalidText, node.Path);
        Presence(in node);
        Put((ulong)utf8.Length, node.Width);
        _bits.WriteBytes(utf8);
        Leave(node.After);
    }

    /// <summary>Writes a <c>:Bytes max n</c> field.</summary>
    /// <param name="field">The field's handle.</param>
    /// <param name="bytes">The bytes.</param>
    public void WriteBytes(GesSchemaField field, ReadOnlySpan<byte> bytes)
    {
        ref readonly var node = ref Current(field);
        if (node.Kind != GesSchemaNodeKind.Text || !node.IsBytes) throw new GesWireException(GesWireException.TypeMismatch, node.Path);
        if (bytes.Length > node.MaxCount) throw new GesWireException(GesWireException.TooLong, node.Path);
        Presence(in node);
        Put((ulong)bytes.Length, node.Width);
        _bits.WriteBytes(bytes);
        Leave(node.After);
    }

    // A recursive List at its deepest level holds nothing; data that goes on is deeper than the depth.
    internal static GesWireException TooDeep(string path, int depth) => new(GesWireException.TooDeep, path, $"deeper than depth {depth}");

    /// <summary>Starts a list with its element count; the elements follow.</summary>
    /// <param name="field">The field's handle.</param>
    /// <param name="count">The element count.</param>
    public void BeginList(GesSchemaField field, int count)
    {
        ref readonly var node = ref Current(field);
        if (node.Kind != GesSchemaNodeKind.List) throw new GesWireException(GesWireException.TypeMismatch, node.Path);
        if (count > 0 && node.DepthLimit > 0) throw TooDeep(node.Path, node.DepthLimit);
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

    internal readonly long BitPosition => _bits.BitPosition;

    internal void PutBits(ulong value, int width) => _bits.Write(value, width);

    internal void SetBit(long bit) => _bits.SetBit(bit);

    internal void Rewind(long bit) => _bits.Rewind(bit);

    internal int FinishBits() => _bits.Finish();

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

    private void Put(ulong value, int width) => _bits.Write(value, width);
}

/// <summary>
/// Decodes one top-level record or message without allocating on success. Data is untrusted: every limit is checked, and
/// invalid data fails with a <see cref="GesWireException"/>. Fields are read in declaration order.
/// </summary>
public ref struct GesSchemaReader
{
    private GesBitReader _bits;
    private GesBoundSchema _schema;
    private GesSchemaNode[] _nodes;
    private GesListFrames _lists;
    private int _cursor;
    private bool _present;

    /// <summary>Starts reading a record or message.</summary>
    /// <param name="schema">The bound schema; for a message set, read the member first.</param>
    /// <param name="data">The data; it may continue after the record.</param>
    /// <param name="byteLimit">An optional limit on the bytes of this record.</param>
    public GesSchemaReader(GesBoundSchema schema, ReadOnlySpan<byte> data, long byteLimit = long.MaxValue)
    {
        _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        _nodes = schema.Nodes;
        _bits = new GesBitReader(data, byteLimit);
        _lists = default;
        _cursor = 0;
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

    /// <summary>Reads a <c>:Flags</c> field as a mask whose bit <c>i</c> sets the member at index <c>i</c> in declared order.</summary>
    /// <param name="field">The field's handle.</param>
    /// <returns>The mask.</returns>
    public ulong ReadFlags(GesSchemaField field)
    {
        ref readonly var node = ref Enter(field, GesSchemaNodeKind.Flags);
        var mask = Get(node.Width, node.Path);
        Leave(node.After);
        return mask;
    }

    /// <summary>Reads a Text into a caller's buffer as validated UTF-8, or the name of a <c>:Tag max n</c>.</summary>
    /// <param name="field">The field's handle.</param>
    /// <param name="destination">The buffer; its maximum is the field's <c>max</c>.</param>
    /// <returns>The number of bytes.</returns>
    public int ReadText(GesSchemaField field, Span<byte> destination)
    {
        ref readonly var node = ref Enter(field, GesSchemaNodeKind.Text);
        if (node.IsBytes) throw new GesWireException(GesWireException.TypeMismatch, node.Path);
        var count = Get(node.Width, node.Path);
        if (count > (ulong)node.MaxCount) throw new GesWireException(GesWireException.InvalidCount, node.Path);
        if ((long)count > destination.Length) throw new GesWireException(GesWireException.BufferTooSmall, node.Path);
        _bits.Require((long)count * 8, node.Path);
        ReadTextBytes(in node, destination.Slice(0, (int)count));
        return (int)count;
    }

    /// <summary>Reads a <c>:Bytes max n</c> field into a caller's buffer.</summary>
    /// <param name="field">The field's handle.</param>
    /// <param name="destination">The buffer; its maximum is the field's <c>max</c>.</param>
    /// <returns>The number of bytes.</returns>
    public int ReadBytes(GesSchemaField field, Span<byte> destination)
    {
        ref readonly var node = ref Enter(field, GesSchemaNodeKind.Text);
        if (!node.IsBytes) throw new GesWireException(GesWireException.TypeMismatch, node.Path);
        var count = Get(node.Width, node.Path);
        if (count > (ulong)node.MaxCount) throw new GesWireException(GesWireException.InvalidCount, node.Path);
        if ((long)count > destination.Length) throw new GesWireException(GesWireException.BufferTooSmall, node.Path);
        _bits.Require((long)count * 8, node.Path);
        _bits.ReadBytes(destination.Slice(0, (int)count), node.Path);
        Leave(node.After);
        return (int)count;
    }

    // Allocates only what the data holds, for the value path.
    internal byte[] ReadBytesValue(GesSchemaField field)
    {
        ref readonly var node = ref Enter(field, GesSchemaNodeKind.Text);
        var count = Get(node.Width, node.Path);
        if (count > (ulong)node.MaxCount) throw new GesWireException(GesWireException.InvalidCount, node.Path);
        _bits.Require((long)count * 8, node.Path);
        var bytes = new byte[(int)count];
        _bits.ReadBytes(bytes, node.Path);
        Leave(node.After);
        return bytes;
    }

    // Allocates only what the data holds, for the rare paths that build a string.
    internal string ReadTextValue(GesSchemaField field)
    {
        ref readonly var node = ref Enter(field, GesSchemaNodeKind.Text);
        var count = Get(node.Width, node.Path);
        if (count > (ulong)node.MaxCount) throw new GesWireException(GesWireException.InvalidCount, node.Path);
        _bits.Require((long)count * 8, node.Path);
        var bytes = new byte[(int)count];
        ReadTextBytes(in node, bytes);
        return Encoding.UTF8.GetString(bytes);
    }

    private void ReadTextBytes(in GesSchemaNode node, Span<byte> destination)
    {
        _bits.ReadBytes(destination, node.Path);
        if (node.IsTagText)
        {
            if (!GesWireRules.IsTagName(destination)) throw new GesWireException(GesWireException.InvalidTag, node.Path);
        }
        else if (!GesWireRules.IsValidUtf8(destination)) throw new GesWireException(GesWireException.InvalidText, node.Path);
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

    internal readonly long BitPosition => _bits.BitPosition;

    internal ulong GetBits(int width, string path) => _bits.Read(width, path);

    // Fails like reading when the data ends before these bits do.
    internal readonly void Require(long bits, string path) => _bits.Require(bits, path);

    internal void Seek(long bit) => _bits.Seek(bit);

    internal int FinishBits() => _bits.Finish();

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

    private ulong Get(int width, string path) => _bits.Read(width, path);
}
