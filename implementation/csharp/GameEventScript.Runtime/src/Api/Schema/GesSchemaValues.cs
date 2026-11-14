// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text;
using GameEventScript.Runtime.Values;
using GameEventScript.Syntax;

namespace GameEventScript.Api;

/// <summary>A decoded top-level value and the bytes it occupied.</summary>
public readonly struct GesSchemaDecoded
{
    internal GesSchemaDecoded(GesValue value, int byteCount)
    {
        Value = value;
        ByteCount = byteCount;
    }

    /// <summary>Gets the Record of a schema or the Message of a message or message set.</summary>
    public GesValue Value { get; }

    /// <summary>Gets the bytes the encoding occupied; further records may follow them.</summary>
    public int ByteCount { get; }
}

/// <summary>
/// The convenient path between GES values and the wire, for rare data. A Record or Map encodes by field name, and fields
/// without a schema entry, such as computed fields, are not transmitted. Only a Map that selects a union alternative must
/// have a field for each of its keys. Time-critical code uses
/// <see cref="GesSchemaWriter"/> and <see cref="GesSchemaReader"/>.
/// </summary>
public static class GesSchemaValues
{
    /// <summary>Encodes a Record or Map through a schema, or a Message through a message.</summary>
    /// <param name="schema">The bound schema.</param>
    /// <param name="value">The value.</param>
    /// <param name="buffer">The buffer that receives the encoding.</param>
    /// <returns>The number of bytes written.</returns>
    /// <exception cref="GesWireException">The value does not fit the schema.</exception>
    /// <exception cref="GesFatalRuntimeException">An external field getter of the value failed.</exception>
    public static int Encode(GesBoundSchema schema, GesValue value, Span<byte> buffer)
    {
        if (schema is null) throw new ArgumentNullException(nameof(schema));
        var writer = new GesSchemaWriter(schema, buffer);
        var current = schema;
        if (schema.Kind == GesSchemaKind.Messages)
        {
            if (value.Message is not { } message) throw new GesWireException(GesWireException.TypeMismatch, "");
            var member = Array.FindIndex(schema.Members, candidate => candidate.Name == message.Name);
            if (member < 0) throw new GesWireException(GesWireException.TypeMismatch, "");
            writer.SelectMember(member);
            current = schema.Members[member];
        }

        if (current.Kind == GesSchemaKind.Message) EncodeMessage(ref writer, current, value);
        else
        {
            if (value.CustomTypeName is { } name && name != current.Name) throw new GesWireException(GesWireException.TypeMismatch, "");
            var fields = value.MaterializedMap() ?? throw new GesWireException(GesWireException.TypeMismatch, "");
            EncodeFields(ref writer, current, 0, current.Nodes.Length, fields, false);
        }

        return writer.Finish();
    }

    /// <summary>Encodes a Record or Map as a delta against a baseline value of the same schema.</summary>
    /// <param name="schema">The bound schema.</param>
    /// <param name="value">The value.</param>
    /// <param name="baseline">The value that the receiver holds.</param>
    /// <param name="buffer">The buffer that receives the delta; <see cref="GesBoundSchema.MaxByteCount"/> always suffices. Encoding also uses it as scratch space, so a smaller buffer can fail with <c>wire.bufferTooSmall</c> even when the delta would fit.</param>
    /// <returns>The number of bytes written.</returns>
    /// <exception cref="GesWireException">A value does not fit the schema, or their keys differ.</exception>
    /// <exception cref="GesFatalRuntimeException">An external field getter of a value failed.</exception>
    public static int Encode(GesBoundSchema schema, GesValue value, GesValue baseline, Span<byte> buffer)
    {
        if (schema is null) throw new ArgumentNullException(nameof(schema));
        // This path is for rare data, so it allocates its scratch space: a pool would put shared, synchronized state into the
        // Runtime. Time-critical code passes its own buffers to GesSchemaDelta. One scratch of the largest encoding serves
        // both records; only the bytes of the first one are kept, since a count pass would run external getters twice.
        var scratch = new byte[schema.MaxByteCount];
        var record = scratch.AsSpan(0, Encode(schema, value, scratch)).ToArray();
        var previousLength = Encode(schema, baseline, scratch);
        return GesSchemaDelta.Encode(schema, record, scratch.AsSpan(0, previousLength), buffer);
    }

    /// <summary>Decodes a delta against the baseline value it was encoded with.</summary>
    /// <param name="schema">The bound schema.</param>
    /// <param name="data">The delta; it may continue after it.</param>
    /// <param name="baseline">The value that this side holds.</param>
    /// <param name="byteLimit">An optional limit on the bytes of this delta.</param>
    /// <returns>The value and the bytes the delta occupied.</returns>
    /// <exception cref="GesWireException">The data is not a valid delta for this baseline.</exception>
    /// <exception cref="GesFatalRuntimeException">An external field getter of the baseline failed.</exception>
    public static GesSchemaDecoded Decode(GesBoundSchema schema, ReadOnlySpan<byte> data, GesValue baseline, long byteLimit = long.MaxValue)
    {
        if (schema is null) throw new ArgumentNullException(nameof(schema));
        // One scratch of the largest encoding holds the baseline and then the decoded record, as in Encode.
        var scratch = new byte[schema.MaxByteCount];
        var previous = scratch.AsSpan(0, Encode(schema, baseline, scratch)).ToArray();
        var decoded = GesSchemaDelta.Decode(schema, data, previous, scratch, byteLimit);
        return new GesSchemaDecoded(Decode(schema, scratch.AsSpan(0, decoded.ByteCount)).Value, decoded.DeltaByteCount);
    }

    /// <summary>Decodes a Record or Message.</summary>
    /// <param name="schema">The bound schema.</param>
    /// <param name="data">The data; it may continue after the record.</param>
    /// <param name="byteLimit">An optional limit on the bytes of this record.</param>
    /// <returns>The value and the bytes it occupied.</returns>
    /// <exception cref="GesWireException">The data is not a valid encoding.</exception>
    public static GesSchemaDecoded Decode(GesBoundSchema schema, ReadOnlySpan<byte> data, long byteLimit = long.MaxValue)
    {
        if (schema is null) throw new ArgumentNullException(nameof(schema));
        var reader = new GesSchemaReader(schema, data, byteLimit);
        var current = schema;
        if (schema.Kind == GesSchemaKind.Messages) current = schema.Members[reader.ReadMember()];
        var value = current.Kind == GesSchemaKind.Message ? DecodeMessage(ref reader, current) : DecodeRecord(ref reader, current, current.Name, 0, current.Nodes.Length);
        return new GesSchemaDecoded(value, reader.Finish());
    }

    /// <summary>Reads the key of an encoded record without decoding the rest.</summary>
    /// <param name="schema">The bound schema, which has a key.</param>
    /// <param name="data">The data.</param>
    /// <returns>The key as an Int64, or the tag index of a Tag key.</returns>
    public static long ReadKey(GesBoundSchema schema, ReadOnlySpan<byte> data) => new GesSchemaReader(schema, data).ReadKey();

    private static void EncodeMessage(ref GesSchemaWriter writer, GesBoundSchema schema, GesValue value)
    {
        if (value.Message is not { } message || message.Name != schema.Name) throw new GesWireException(GesWireException.TypeMismatch, "");
        var argument = 0;
        var nodes = schema.Nodes;
        foreach (var tag in message.Tags)
            if (!Array.Exists(nodes, node => node.IsTagBit && node.Name == tag)) throw new GesWireException(GesWireException.UnknownTag, "");

        var arguments = new List<GesMessageArgument>(message.Arguments);
        for (var index = 0; index < nodes.Length; index = nodes[index].Next)
        {
            if (nodes[index].IsTagBit)
            {
                writer.WriteBoolean(new GesSchemaField(schema, index), Contains(message.Tags, nodes[index].Name));
                continue;
            }

            if (argument >= arguments.Count) throw new GesWireException(GesWireException.MissingField, nodes[index].Path);
            var current = arguments[argument];
            if (current.Name != schema.Labels[argument]) throw new GesWireException(GesWireException.TypeMismatch, nodes[index].Path);
            argument++;
            EncodeNode(ref writer, schema, index, current.Value, false);
        }

        if (argument != arguments.Count) throw new GesWireException(GesWireException.TypeMismatch, "");
    }

    private static bool Contains(IReadOnlyList<string> tags, string tag)
    {
        foreach (var candidate in tags)
            if (candidate == tag) return true;
        return false;
    }

    private static GesValue DecodeMessage(ref GesSchemaReader reader, GesBoundSchema schema)
    {
        var nodes = schema.Nodes;
        var arguments = new List<GesMessageArgument>();
        var tags = new List<string>();
        for (var index = 0; index < nodes.Length; index = nodes[index].Next)
        {
            if (nodes[index].IsTagBit)
            {
                if (reader.ReadBoolean(new GesSchemaField(schema, index))) tags.Add(nodes[index].Name);
                continue;
            }

            arguments.Add(new GesMessageArgument(schema.Labels[arguments.Count], DecodeNode(ref reader, schema, index)));
        }

        return GesValue.GesMessage(GameEventScriptMessage.Create(schema.Name, arguments, tags));
    }

    private static void EncodeFields(ref GesSchemaWriter writer, GesBoundSchema schema, int start, int end, GesValueMap fields, bool strict)
    {
        for (var index = start; index < end; index = schema.Nodes[index].Next)
        {
            var last = Held(schema, start, end, fields, index, out var value);
            if (last >= 0 && !AcceptsNothing(schema, last)) throw new GesWireException(GesWireException.MissingField, schema.Nodes[last].Path);
            EncodeNode(ref writer, schema, index, value, strict);
        }
    }

    // The value of a field, or else of its default: a literal, or the value of the sibling it names, whose own default
    // applies in turn. Returns -1, or the field at the end of the chain that has no value, which reads as nothing.
    private static int Held(GesBoundSchema schema, int start, int end, GesValueMap fields, int index, out GesValue value)
    {
        ref readonly var node = ref schema.Nodes[index];
        for (var key = 0; key < fields.Length; key++)
            if (fields.KeyAt(key) == node.Name)
            {
                value = fields.ValueAt(key);
                return -1;
            }

        value = default;
        switch (node.Default)
        {
            case null:
                return index;
            case { Kind: GesSchemaDefaultKind.Field } fallback:
                for (var sibling = start; sibling < end; sibling = schema.Nodes[sibling].Next)
                    if (schema.Nodes[sibling].Name == fallback.Name) return Held(schema, start, end, fields, sibling, out value);
                return index;
            case var fallback:
                value = Literal(fallback, node.Kind == GesSchemaNodeKind.Integer, node.ValueKind == GesSchemaTypeKind.Percentage, node.Unit);
                return -1;
        }
    }

    // The value of a literal default that names no sibling: a Boolean, a tag, the Map of a record literal, or a number of
    // the field's kind and unit.
    internal static GesValue Literal(GesResolvedDefault fallback, bool integer, bool percentage, GesQuantityUnit unit)
    {
        switch (fallback.Kind)
        {
            case GesSchemaDefaultKind.Boolean:
                return GesValue.GesBoolean(fallback.Boolean);
            case GesSchemaDefaultKind.Tag:
                return GesValue.GesTag(fallback.Name);
            case GesSchemaDefaultKind.Record:
                return fallback.Record!.Value;
            case var _ when integer:
                var whole = fallback.Value.Significand;
                for (var power = 0; power < fallback.Value.Exponent; power++) whole *= 10;
                return GesValue.GesInteger(whole, unit);
            default:
                var number = double.Parse(fallback.Value.Canonical, System.Globalization.CultureInfo.InvariantCulture);
                return percentage ? GesValue.GesPercentage(number) : GesValue.GesFloat(number, unit);
        }
    }

    // A flag Map sets a member for true and leaves it unset for false or an absent key. Returns the wire code that
    // rejects the value, or null; a union alternative holds exactly the Maps that encoding accepts.
    private static string? FlagMask(in GesSchemaNode node, GesValue value, out ulong mask)
    {
        mask = 0;
        if (value.ValueKind != GesBytecodeTypeKind.Map || value.CustomTypeName is not null || value.MaterializedMap() is not { } map)
            return GesWireException.TypeMismatch;
        for (var entry = 0; entry < map.Length; entry++)
        {
            var member = Array.IndexOf(node.Tags!, map.KeyAt(entry));
            if (member < 0) return GesWireException.UnknownTag;
            var set = map.ValueAt(entry);
            if (set.ValueKind != GesBytecodeTypeKind.Boolean) return GesWireException.TypeMismatch;
            if (set.AsBoolean()) mask |= 1UL << member;
        }

        return null;
    }

    // The Map of the set members, each true.
    private static GesValue FlagMap(string[] members, ulong mask)
    {
        var keys = new List<string>();
        for (var member = 0; member < members.Length; member++)
            if ((mask >> member & 1) == 1) keys.Add(members[member]);
        var values = new GesValue[keys.Count];
        for (var index = 0; index < values.Length; index++) values[index] = GesValue.GesBoolean(true);
        return GesValue.GesMap(keys.ToArray(), values);
    }

    private static bool AcceptsNothing(GesBoundSchema schema, int index)
    {
        ref readonly var node = ref schema.Nodes[index];
        if (node.Optional) return true;
        if (node.Kind != GesSchemaNodeKind.Union) return false;
        for (var alternative = index + 1; alternative < node.Next; alternative = schema.Nodes[alternative].Next)
            if (schema.Nodes[alternative].Kind == GesSchemaNodeKind.Nothing) return true;
        return false;
    }

    // Strict encoding is the trial of a Map against a union alternative: a key without a field means that the alternative does not hold.
    private static void EncodeNode(ref GesSchemaWriter writer, GesBoundSchema schema, int index, GesValue value, bool strict)
    {
        ref readonly var node = ref schema.Nodes[index];
        var field = new GesSchemaField(schema, index);
        if (value.IsNothing && (node.Optional || node.Kind != GesSchemaNodeKind.Union))
        {
            if (!node.Optional) throw new GesWireException(GesWireException.NothingValue, node.Path);
            writer.WriteAbsent(field);
            return;
        }

        switch (node.Kind)
        {
            case GesSchemaNodeKind.Boolean:
                if (value.ValueKind != GesBytecodeTypeKind.Boolean) throw new GesWireException(GesWireException.TypeMismatch, node.Path);
                writer.WriteBoolean(field, value.AsBoolean());
                break;
            case GesSchemaNodeKind.Tag:
                if (value.ValueKind != GesBytecodeTypeKind.Tag) throw new GesWireException(GesWireException.TypeMismatch, node.Path);
                var tag = Array.IndexOf(node.Tags!, value.AsText());
                if (tag < 0) throw new GesWireException(GesWireException.UnknownTag, node.Path);
                writer.WriteTag(field, tag);
                break;
            case GesSchemaNodeKind.Flags:
                if (FlagMask(in node, value, out var mask) is { } rejected) throw new GesWireException(rejected, node.Path);
                writer.WriteFlags(field, mask);
                break;
            case GesSchemaNodeKind.Text when node.IsBytes:
                if (value.ValueKind != GesBytecodeTypeKind.Bytes) throw new GesWireException(GesWireException.TypeMismatch, node.Path);
                writer.WriteBytes(field, value.AsBytes());
                break;
            case GesSchemaNodeKind.Text:
                if (value.ValueKind != (node.IsTagText ? GesBytecodeTypeKind.Tag : GesBytecodeTypeKind.Text)) throw new GesWireException(GesWireException.TypeMismatch, node.Path);
                writer.WriteText(field, Encoding.UTF8.GetBytes(value.AsText()));
                break;
            case GesSchemaNodeKind.List when node.ValueKind == GesSchemaTypeKind.Dice:
                if (value.ValueKind != GesBytecodeTypeKind.Dice) throw new GesWireException(GesWireException.TypeMismatch, node.Path);
                var rolls = value.AsDice();
                writer.BeginList(field, rolls.Length);
                foreach (var roll in rolls) writer.WriteInteger(new GesSchemaField(schema, index + 1), roll);
                break;
            case GesSchemaNodeKind.List when node.ValueKind == GesSchemaTypeKind.Poly:
                var poly = GesPoly.FromValue(value, node.Unit) ?? throw new GesWireException(GesWireException.TypeMismatch, node.Path);
                writer.BeginList(field, poly.Points.Count);
                foreach (var point in poly.Points) EncodePoint(ref writer, schema, index + 1, point.X, point.Y, point.Z);
                break;
            case GesSchemaNodeKind.List:
                if (value.ValueKind != GesBytecodeTypeKind.List) throw new GesWireException(GesWireException.TypeMismatch, node.Path);
                var items = value.AsList();
                writer.BeginList(field, items.Length);
                for (var item = 0; item < items.Length; item++) EncodeNode(ref writer, schema, index + 1, items[item], strict);
                break;
            case GesSchemaNodeKind.Group:
                EncodeGroup(ref writer, schema, index, value, strict);
                break;
            case GesSchemaNodeKind.Union:
                EncodeUnion(ref writer, schema, index, value, strict);
                break;
            default:
                EncodeNumber(ref writer, field, in node, value);
                break;
        }
    }

    // The kind and type name of a value select one alternative. Only a Map can fit several schemas, and the first that holds decides.
    private static void EncodeUnion(ref GesSchemaWriter writer, GesBoundSchema schema, int index, GesValue value, bool strict)
    {
        ref readonly var node = ref schema.Nodes[index];
        var field = new GesSchemaField(schema, index);
        var position = 0;
        GesWireException? failure = null;
        for (var alternative = index + 1; alternative < node.Next; alternative = schema.Nodes[alternative].Next, position++)
        {
            ref readonly var candidate = ref schema.Nodes[alternative];
            if (candidate.Kind == GesSchemaNodeKind.Group && candidate.SchemaName.Length > 0 && value.ValueKind == GesBytecodeTypeKind.Map && value.CustomTypeName is null)
            {
                var attempt = writer;
                try
                {
                    attempt.SelectAlternative(field, position);
                    EncodeNode(ref attempt, schema, alternative, value, true);
                    writer = attempt;
                    return;
                }
                catch (GesWireException exception) when (exception.Code != GesWireException.BufferTooSmall)
                {
                    // The next schema may hold; otherwise the last one reports why not.
                    failure = exception;
                }
            }
            else if (Selects(in candidate, value))
            {
                writer.SelectAlternative(field, position);
                if (candidate.Kind != GesSchemaNodeKind.Nothing) EncodeNode(ref writer, schema, alternative, value, strict);
                return;
            }
        }

        throw failure ?? new GesWireException(value.IsNothing ? GesWireException.NothingValue : GesWireException.TypeMismatch, node.Path);
    }

    private static bool Selects(in GesSchemaNode candidate, GesValue value)
    {
        switch (candidate.ValueKind)
        {
            case GesSchemaTypeKind.Nothing: return value.IsNothing;
            case GesSchemaTypeKind.Boolean: return value.ValueKind == GesBytecodeTypeKind.Boolean;
            case GesSchemaTypeKind.Tag: return value.ValueKind == GesBytecodeTypeKind.Tag;
            case GesSchemaTypeKind.Text: return value.ValueKind == GesBytecodeTypeKind.Text;
            case GesSchemaTypeKind.Bytes: return value.ValueKind == GesBytecodeTypeKind.Bytes;
            case GesSchemaTypeKind.Flags: return FlagMask(in candidate, value, out _) is null;
            case GesSchemaTypeKind.List: return value.ValueKind == GesBytecodeTypeKind.List;
            case GesSchemaTypeKind.Dice: return value.ValueKind == GesBytecodeTypeKind.Dice;
            case GesSchemaTypeKind.Point: return value.ValueKind == GesBytecodeTypeKind.Point;
            case GesSchemaTypeKind.Vector: return value.ValueKind == GesBytecodeTypeKind.Vector;
            case GesSchemaTypeKind.Box: return value.ExternalValue is GesGeometryLibrary.BoxValue;
            case GesSchemaTypeKind.Poly: return value.ExternalValue is GesGeometryLibrary.PolyValue;
            case GesSchemaTypeKind.Disc: return value.ExternalValue is GesGeometryLibrary.DiscValue;
            case GesSchemaTypeKind.Percentage: return value.ValueKind == GesBytecodeTypeKind.Percentage;
            case GesSchemaTypeKind.Reference: return value.CustomTypeName == candidate.SchemaName;
            default: return value.ValueKind is GesBytecodeTypeKind.Integer or GesBytecodeTypeKind.Float && value.ValueUnit == candidate.Unit;
        }
    }

    private static void EncodeGroup(ref GesSchemaWriter writer, GesBoundSchema schema, int index, GesValue value, bool strict)
    {
        ref readonly var node = ref schema.Nodes[index];
        var field = new GesSchemaField(schema, index);
        if (node.ValueKind is GesSchemaTypeKind.Point or GesSchemaTypeKind.Vector)
        {
            var kind = node.ValueKind == GesSchemaTypeKind.Point ? GesBytecodeTypeKind.Point : GesBytecodeTypeKind.Vector;
            if (value.ValueKind != kind || value.ValueUnit != node.Unit) throw new GesWireException(GesWireException.TypeMismatch, node.Path);
            EncodePoint(ref writer, schema, index, value.X, value.Y, value.Z);
            return;
        }

        if (node.ValueKind == GesSchemaTypeKind.Box)
        {
            var box = GesBox.FromValue(value, node.Unit) ?? throw new GesWireException(GesWireException.TypeMismatch, node.Path);
            writer.BeginNested(field);
            EncodePoint(ref writer, schema, index + 1, box.Min.X, box.Min.Y, box.Min.Z);
            EncodePoint(ref writer, schema, schema.Nodes[index + 1].Next, box.Max.X, box.Max.Y, box.Max.Z);
            return;
        }

        if (node.ValueKind == GesSchemaTypeKind.Disc)
        {
            // The centre, then the radius, which a block can leave out: it is then zero, as an axis it leaves out is.
            var disc = GesDisc.FromValue(value, node.Unit) ?? throw new GesWireException(GesWireException.TypeMismatch, node.Path);
            writer.BeginNested(field);
            EncodePoint(ref writer, schema, index + 1, disc.Center.X, disc.Center.Y, disc.Center.Z);
            var radius = schema.Nodes[index + 1].Next;
            if (radius < node.Next) writer.WriteNumber(new GesSchemaField(schema, radius), disc.Radius);
            else if (disc.Radius != 0) throw new GesWireException(GesWireException.TypeMismatch, node.Path);
            return;
        }

        if (value.CustomTypeName is { } name2 && name2 != node.SchemaName) throw new GesWireException(GesWireException.TypeMismatch, node.Path);
        var fields = value.MaterializedMap() ?? throw new GesWireException(GesWireException.TypeMismatch, node.Path);
        if (strict && value.CustomTypeName is null && !HasEveryKey(schema, index, fields)) throw new GesWireException(GesWireException.TypeMismatch, node.Path);
        writer.BeginNested(field);
        EncodeFields(ref writer, schema, index + 1, node.Next, fields, strict);
    }

    // A checked document writes a Box as the Map of its corners, a Poly as the List of its points and a Disc as the Map of its
    // centre and radius, since documents have no
    // geometry literal; this gives them the geometry library's values, as the schema's fields and their alternatives expect.
    // `node` is the value's node in the checked document and `chosen` the alternative that held for each union of several, so
    // that a value takes the alternative the check chose rather than the first one its shape fits.
    internal static GesValue DocumentShapes(GesResolvedDeclaration schema, GesValue value, GesDocumentNode? node = null, IReadOnlyDictionary<(int Start, GesResolvedType[] Alternatives), GesResolvedType>? chosen = null)
    {
        if (value.AsMap() is not { } map) return value;
        var keys = new string[map.Length];
        var values = new GesValue[map.Length];
        for (var index = 0; index < map.Length; index++)
        {
            keys[index] = map.KeyAt(index);
            var entry = map.ValueAt(index);
            var field = Array.Find(schema.Fields, candidate => candidate.Name == keys[index]);
            var label = node is { Kind: GesDocumentNodeKind.Map } ? Array.IndexOf(node.Labels, keys[index]) : -1;
            values[index] = field is null ? entry : DocumentShape(field.Alternatives, entry, label < 0 ? null : node!.Children[label], chosen);
        }

        return value.CustomTypeName is { } name ? GesValue.GesRecord(name, keys, values) : GesValue.GesMap(keys, values);
    }

    private static GesValue DocumentShape(GesResolvedType[] alternatives, GesValue value, GesDocumentNode? node, IReadOnlyDictionary<(int Start, GesResolvedType[] Alternatives), GesResolvedType>? chosen)
    {
        var candidates = node is not null && chosen is not null && chosen.TryGetValue((node.Start, alternatives), out var held) ? [held] : alternatives;
        foreach (var type in candidates)
        {
            switch (type.Kind)
            {
                case GesSchemaTypeKind.Box when value.AsMap() is { Length: 2 } corners && corners.Get("lower") is { } lower && corners.Get("upper") is { } upper
                    && GesPoint.FromValue(lower, type.Unit) is { } low && GesPoint.FromValue(upper, type.Unit) is { } high:
                    return new GesBox(low, high).ToValue(type.Unit);
                case GesSchemaTypeKind.Poly when value.ValueKind == GesBytecodeTypeKind.List && Points(value.AsList(), type.Unit) is { } points:
                    return new GesPoly(points).ToValue(type.Unit);
                case GesSchemaTypeKind.Disc when value.AsMap() is { Length: 2 } parts && parts.Get("center") is { } center && parts.Get("radius") is { } radius
                    && GesPoint.FromValue(center, type.Unit) is { } middle && radius.ValueKind is GesBytecodeTypeKind.Integer or GesBytecodeTypeKind.Float && radius.ValueUnit == type.Unit:
                    return new GesDisc(middle, radius.AsNumber()).ToValue(type.Unit);
                case GesSchemaTypeKind.List when type.Element is { } element && value.ValueKind == GesBytecodeTypeKind.List:
                    var list = value.AsList();
                    var items = node is { Kind: GesDocumentNodeKind.List } && node.Children.Length == list.Length ? node.Children : null;
                    var shaped = new GesValue[list.Length];
                    for (var index = 0; index < shaped.Length; index++) shaped[index] = DocumentShape(element, list[index], items?[index], chosen);
                    return GesValue.GesList(shaped);
                case GesSchemaTypeKind.Reference when value.AsMap() is not null && (value.CustomTypeName is not { } name || name == type.ReferenceName):
                    return DocumentShapes(type.Reference!, value, node, chosen);
            }
        }

        return value;
    }

    // The Points of a document Poly in the field's unit, or null when an item is no such Point.
    private static GesPoint[]? Points(GesValueSlice items, GesQuantityUnit unit)
    {
        var points = new GesPoint[items.Length];
        for (var index = 0; index < points.Length; index++)
        {
            if (GesPoint.FromValue(items[index], unit) is not { } point) return null;
            points[index] = point;
        }

        return points;
    }

    // The components of a Point, Vector, Box corner or Poly point; an axis that the block leaves out must be zero.
    private static void EncodePoint(ref GesSchemaWriter writer, GesBoundSchema schema, int index, double x, double y, double z)
    {
        ref readonly var node = ref schema.Nodes[index];
        var seenX = false;
        var seenY = false;
        var seenZ = false;
        writer.BeginNested(new GesSchemaField(schema, index));
        for (var axis = index + 1; axis < node.Next; axis++)
        {
            var name = schema.Nodes[axis].Name;
            seenX |= name == "x";
            seenY |= name == "y";
            seenZ |= name == "z";
            writer.WriteNumber(new GesSchemaField(schema, axis), name == "x" ? x : name == "y" ? y : z);
        }

        if ((!seenX && x != 0) || (!seenY && y != 0) || (!seenZ && z != 0)) throw new GesWireException(GesWireException.TypeMismatch, node.Path);
    }

    private static bool HasEveryKey(GesBoundSchema schema, int index, GesValueMap fields)
    {
        for (var key = 0; key < fields.Length; key++)
        {
            var found = false;
            for (var child = index + 1; child < schema.Nodes[index].Next && !found; child = schema.Nodes[child].Next) found = schema.Nodes[child].Name == fields.KeyAt(key);
            if (!found) return false;
        }

        return true;
    }

    private static void EncodeNumber(ref GesSchemaWriter writer, GesSchemaField field, in GesSchemaNode node, GesValue value)
    {
        // Boolean and Dice are numeric in the language, but a field takes them only after an explicit conversion.
        var percent = value.ValueKind == GesBytecodeTypeKind.Percentage;
        if (!(value.ValueKind is GesBytecodeTypeKind.Integer or GesBytecodeTypeKind.Float || percent) || percent != (node.ValueKind == GesSchemaTypeKind.Percentage) || value.ValueUnit != node.Unit)
            throw new GesWireException(GesWireException.TypeMismatch, node.Path);
        if (value.ValueKind == GesBytecodeTypeKind.Integer) writer.WriteInteger(field, value.AsInteger());
        else writer.WriteNumber(field, value.AsNumeric);
    }

    private static GesValue DecodeRecord(ref GesSchemaReader reader, GesBoundSchema schema, string name, int start, int end)
    {
        var keys = new List<string>();
        var values = new List<GesValue>();
        for (var index = start; index < end; index = schema.Nodes[index].Next)
        {
            keys.Add(schema.Nodes[index].Name);
            values.Add(DecodeNode(ref reader, schema, index));
        }

        if (schema.Computed.TryGetValue(name, out var computed)) Derive(computed, keys, values);
        return GesValue.GesRecord(name, keys.ToArray(), values.ToArray());
    }

    // Adds the computed fields in order, each reading the fields and the computed fields before it, as a script record does.
    private static void Derive(GesResolvedComputed[] computed, List<string> keys, List<GesValue> values)
    {
        var scope = new RecordScope(keys, values);
        foreach (var field in computed)
        {
            var value = GesComputedMath.Cast(GesComputedEvaluation.Evaluate(field.Expression, scope, null), field.Type);
            keys.Add(field.Name);
            values.Add(value.Kind switch
            {
                GesComputedKind.Integer => GesValue.GesInteger(value.Integer, field.Unit),
                GesComputedKind.Float => GesValue.GesFloat(value.Number, field.Unit),
                GesComputedKind.Percentage => GesValue.GesPercentage(value.Number),
                GesComputedKind.True or GesComputedKind.False => GesValue.GesBoolean(value.Kind == GesComputedKind.True),
                _ => default
            });
        }
    }

    // The decoded fields of a record, or a value of a list element, as computed fields read them.
    private sealed class RecordScope : GesComputedScope
    {
        private readonly List<string>? _keys;
        private readonly List<GesValue>? _values;
        private readonly GesValue _value;

        internal RecordScope(List<string> keys, List<GesValue> values)
        {
            _keys = keys;
            _values = values;
        }

        private RecordScope(GesValue value) => _value = value;

        internal override GesComputedValue Read(string[] path, bool percentage)
        {
            var value = Walk(path);
            return value.Kind switch
            {
                GesBytecodeTypeKind.Integer => GesComputedValue.OfInteger(value.IntegerValue),
                GesBytecodeTypeKind.Float => GesComputedValue.OfFloat(value.FloatValue),
                GesBytecodeTypeKind.Percentage => GesComputedValue.OfPercentage(value.FloatValue),
                GesBytecodeTypeKind.Boolean => GesComputedValue.OfBoolean(value.IsTrue),
                _ => GesComputedValue.Nothing
            };
        }

        internal override bool Has(string[] path) => Walk(path).Kind != GesBytecodeTypeKind.Nothing;

        internal override int Count(string[] path) => Walk(path).ObjectValue is GesValue[] items ? items.Length : 0;

        internal override GesComputedScope Element(string[] path, int index) => new RecordScope(((GesValue[])Walk(path).ObjectValue!)[index]);

        private GesValue Walk(string[] path)
        {
            var current = _value;
            for (var segment = 0; segment < path.Length; segment++)
            {
                if (segment == 0 && _keys is not null)
                {
                    var index = _keys.IndexOf(path[0]);
                    current = index >= 0 ? _values![index] : default;
                    continue;
                }

                if (current.MaterializedMap() is not { } fields) return default;
                var found = default(GesValue);
                for (var key = 0; key < fields.Length; key++)
                    if (fields.KeyAt(key) == path[segment]) found = fields.ValueAt(key);
                current = found;
            }

            return current;
        }
    }

    private static GesValue DecodeNode(ref GesSchemaReader reader, GesBoundSchema schema, int index)
    {
        ref readonly var node = ref schema.Nodes[index];
        var field = new GesSchemaField(schema, index);
        if (node.Optional && !reader.ReadPresence(field)) return default;
        switch (node.Kind)
        {
            case GesSchemaNodeKind.Boolean: return GesValue.GesBoolean(reader.ReadBoolean(field));
            case GesSchemaNodeKind.Tag: return GesValue.GesTag(node.Tags![reader.ReadTag(field)]);
            case GesSchemaNodeKind.Flags: return FlagMap(node.Tags!, reader.ReadFlags(field));
            case GesSchemaNodeKind.Text when node.IsBytes: return BytesValue(reader.ReadBytesValue(field));
            case GesSchemaNodeKind.Text: return node.IsTagText ? GesValue.GesTag(reader.ReadTextValue(field)) : GesValue.GesText(reader.ReadTextValue(field));
            case GesSchemaNodeKind.List when node.ValueKind == GesSchemaTypeKind.Dice:
                var count = reader.BeginList(field);
                var rolls = new List<int>(Math.Min(count, 1024));
                for (var roll = 0; roll < count; roll++) rolls.Add((int)reader.ReadInteger(new GesSchemaField(schema, index + 1)));
                return GesValue.GesDice(rolls.ToArray());
            case GesSchemaNodeKind.Union:
                var selected = GesSchemaNumbers.Alternative(schema.Nodes, index, reader.ReadAlternative(field));
                return schema.Nodes[selected].Kind == GesSchemaNodeKind.Nothing ? default : DecodeNode(ref reader, schema, selected);
            case GesSchemaNodeKind.List when node.ValueKind == GesSchemaTypeKind.Poly:
                var pointCount = reader.BeginList(field);
                // The count is untrusted until the points are read, so the capacity grows with the data as for a List.
                var points = new List<GesPoint>(Math.Min(pointCount, 1024));
                for (var point = 0; point < pointCount; point++) points.Add(DecodePoint(ref reader, schema, index + 1));
                return new GesPoly(points).ToValue(node.Unit);
            case GesSchemaNodeKind.List:
                var length = reader.BeginList(field);
                var items = new List<GesValue>(Math.Min(length, 1024));
                for (var item = 0; item < length; item++) items.Add(DecodeNode(ref reader, schema, index + 1));
                return GesValue.GesList(items.ToArray());
            case GesSchemaNodeKind.Group when node.ValueKind is GesSchemaTypeKind.Point or GesSchemaTypeKind.Vector:
                var spatial = DecodePoint(ref reader, schema, index);
                return node.ValueKind == GesSchemaTypeKind.Point ? GesValue.GesPoint(spatial.X, spatial.Y, spatial.Z, node.Unit) : GesValue.GesVector(spatial.X, spatial.Y, spatial.Z, node.Unit);
            case GesSchemaNodeKind.Group when node.ValueKind == GesSchemaTypeKind.Box:
                // The corners are two Points on the wire; the box orders them.
                reader.BeginNested(field);
                var lower = DecodePoint(ref reader, schema, index + 1);
                var upper = DecodePoint(ref reader, schema, schema.Nodes[index + 1].Next);
                return new GesBox(lower, upper).ToValue(node.Unit);
            case GesSchemaNodeKind.Group when node.ValueKind == GesSchemaTypeKind.Disc:
                reader.BeginNested(field);
                var center = DecodePoint(ref reader, schema, index + 1);
                var radius = schema.Nodes[index + 1].Next;
                return new GesDisc(center, radius < node.Next ? reader.ReadNumber(new GesSchemaField(schema, radius)) : 0).ToValue(node.Unit);
            case GesSchemaNodeKind.Group:
                reader.BeginNested(field);
                return DecodeRecord(ref reader, schema, node.SchemaName, index + 1, node.Next);
            case GesSchemaNodeKind.Integer:
                return GesValue.GesInteger(reader.ReadInteger(field), node.Unit);
            default:
                var number = reader.ReadNumber(field);
                return node.ValueKind == GesSchemaTypeKind.Percentage ? GesValue.GesPercentage(number) : GesValue.GesFloat(number, node.Unit);
        }
    }

    // The components of a Point, Vector, Box corner, Poly point or Disc centre; an axis that the block leaves out is zero.
    private static GesPoint DecodePoint(ref GesSchemaReader reader, GesBoundSchema schema, int index)
    {
        ref readonly var node = ref schema.Nodes[index];
        reader.BeginNested(new GesSchemaField(schema, index));
        var (x, y, z) = (0d, 0d, 0d);
        for (var axis = index + 1; axis < node.Next; axis++)
        {
            var component = reader.ReadNumber(new GesSchemaField(schema, axis));
            switch (schema.Nodes[axis].Name)
            {
                case "x": x = component; break;
                case "y": y = component; break;
                default: z = component; break;
            }
        }

        return new GesPoint(x, y, z);
    }

    private static GesValue BytesValue(byte[] bytes)
    {
        var value = new GesValue();
        value.SetBytes(bytes);
        return value;
    }
}

// The named fields of a record literal in the field order of its schema, filled once every schema is composed and immutable
// after loading. Setting them also builds the Map that a left-out field takes, once, so that reading and encoding share it;
// encoding gives every field the Map leaves out its own default.
internal sealed class GesResolvedRecord
{
    private GesResolvedRecordEntry[] _entries = [];

    internal GesResolvedRecordEntry[] Entries
    {
        get => _entries;
        set
        {
            _entries = value;
            var keys = new string[value.Length];
            var values = new GesValue[value.Length];
            for (var index = 0; index < value.Length; index++)
            {
                var entry = value[index];
                var type = entry.Field.Alternatives[0];
                keys[index] = entry.Field.Name;
                values[index] = GesSchemaValues.Literal(entry.Value, type.Constraints.Integer, type.Kind == GesSchemaTypeKind.Percentage, type.Unit);
            }

            Value = GesValue.GesMap(keys, values);
        }
    }

    internal GesValue Value { get; private set; } = GesValue.GesMap([], []);
}
