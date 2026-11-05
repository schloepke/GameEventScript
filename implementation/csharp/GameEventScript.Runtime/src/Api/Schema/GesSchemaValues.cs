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
/// without a schema entry, such as computed fields, are not transmitted. Time-critical code uses
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
            var fields = value.AsMap() ?? throw new GesWireException(GesWireException.TypeMismatch, "");
            EncodeFields(ref writer, current, 0, current.Nodes.Length, fields);
        }

        return writer.Finish();
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
            EncodeNode(ref writer, schema, index, current.Value);
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

    private static void EncodeFields(ref GesSchemaWriter writer, GesBoundSchema schema, int start, int end, GesValueMap fields)
    {
        for (var index = start; index < end; index = schema.Nodes[index].Next)
        {
            GesValue value = default;
            var name = schema.Nodes[index].Name;
            for (var key = 0; key < fields.Length; key++)
                if (fields.KeyAt(key) == name) value = fields.ValueAt(key);
            EncodeNode(ref writer, schema, index, value);
        }
    }

    private static void EncodeNode(ref GesSchemaWriter writer, GesBoundSchema schema, int index, GesValue value)
    {
        ref readonly var node = ref schema.Nodes[index];
        var field = new GesSchemaField(schema, index);
        if (value.IsNothing)
        {
            if (!node.Optional) throw new GesWireException(GesWireException.MissingField, node.Path);
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
            case GesSchemaNodeKind.Text:
                if (value.ValueKind != GesBytecodeTypeKind.Text) throw new GesWireException(GesWireException.TypeMismatch, node.Path);
                writer.WriteText(field, Encoding.UTF8.GetBytes(value.AsText()));
                break;
            case GesSchemaNodeKind.List:
                if (value.ValueKind != GesBytecodeTypeKind.List) throw new GesWireException(GesWireException.TypeMismatch, node.Path);
                var items = value.AsList();
                writer.BeginList(field, items.Length);
                for (var item = 0; item < items.Length; item++) EncodeNode(ref writer, schema, index + 1, items[item]);
                break;
            case GesSchemaNodeKind.Group:
                EncodeGroup(ref writer, schema, index, value);
                break;
            default:
                EncodeNumber(ref writer, field, in node, value);
                break;
        }
    }

    private static void EncodeGroup(ref GesSchemaWriter writer, GesBoundSchema schema, int index, GesValue value)
    {
        ref readonly var node = ref schema.Nodes[index];
        var field = new GesSchemaField(schema, index);
        if (node.ValueKind is GesSchemaTypeKind.Point or GesSchemaTypeKind.Vector)
        {
            var kind = node.ValueKind == GesSchemaTypeKind.Point ? GesBytecodeTypeKind.Point : GesBytecodeTypeKind.Vector;
            if (value.ValueKind != kind || value.ValueUnit != node.Unit) throw new GesWireException(GesWireException.TypeMismatch, node.Path);
            var seenX = false;
            var seenY = false;
            var seenZ = false;
            writer.BeginNested(field);
            for (var axis = index + 1; axis < node.Next; axis++)
            {
                var name = schema.Nodes[axis].Name;
                seenX |= name == "x";
                seenY |= name == "y";
                seenZ |= name == "z";
                writer.WriteNumber(new GesSchemaField(schema, axis), name == "x" ? value.X : name == "y" ? value.Y : value.Z);
            }

            if ((!seenX && value.X != 0) || (!seenY && value.Y != 0) || (!seenZ && value.Z != 0)) throw new GesWireException(GesWireException.TypeMismatch, node.Path);
            return;
        }

        if (value.CustomTypeName is { } name2 && name2 != node.SchemaName) throw new GesWireException(GesWireException.TypeMismatch, node.Path);
        var fields = value.AsMap() ?? throw new GesWireException(GesWireException.TypeMismatch, node.Path);
        writer.BeginNested(field);
        EncodeFields(ref writer, schema, index + 1, node.Next, fields);
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

        return GesValue.GesRecord(name, keys.ToArray(), values.ToArray());
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
            case GesSchemaNodeKind.Text: return GesValue.GesText(reader.ReadTextValue(field));
            case GesSchemaNodeKind.List:
                var length = reader.BeginList(field);
                var items = new List<GesValue>(Math.Min(length, 1024));
                for (var item = 0; item < length; item++) items.Add(DecodeNode(ref reader, schema, index + 1));
                return GesValue.GesList(items.ToArray());
            case GesSchemaNodeKind.Group:
                reader.BeginNested(field);
                if (node.ValueKind is not (GesSchemaTypeKind.Point or GesSchemaTypeKind.Vector)) return DecodeRecord(ref reader, schema, node.SchemaName, index + 1, node.Next);
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

                return node.ValueKind == GesSchemaTypeKind.Point ? GesValue.GesPoint(x, y, z, node.Unit) : GesValue.GesVector(x, y, z, node.Unit);
            case GesSchemaNodeKind.Integer:
                return GesValue.GesInteger(reader.ReadInteger(field), node.Unit);
            default:
                var number = reader.ReadNumber(field);
                return node.ValueKind == GesSchemaTypeKind.Percentage ? GesValue.GesPercentage(number) : GesValue.GesNumber(number, node.Unit);
        }
    }
}
