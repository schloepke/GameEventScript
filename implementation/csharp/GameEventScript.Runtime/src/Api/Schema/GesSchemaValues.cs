// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
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
        if (schema.Kind == GesSchemaKind.Messages)
        {
            if (value.Message is not { } message) throw new GesWireException(GesWireException.TypeMismatch, "");
            if (schema.Nodes.Length > 0)
            {
                // The envelope of a set with parameters: the parameters, then the carried message.
                if (message.Name != schema.Name || message.Arguments.Count != schema.Labels.Length + 1) throw new GesWireException(GesWireException.TypeMismatch, "");
                // The envelope carries no tags of its own; the carried message keeps its tags.
                if (message.Tags.Count > 0) throw new GesWireException(GesWireException.UnknownTag, "");
                var argument = 0;
                for (var index = 0; index < schema.Nodes.Length; index = schema.Nodes[index].Next, argument++)
                {
                    if (message.Arguments.NameAt(argument) != schema.Labels[argument]) throw new GesWireException(GesWireException.TypeMismatch, schema.Nodes[index].Path);
                    EncodeNode(ref writer, schema, index, message.Arguments.ValueAt(argument), false);
                }

                var carried = message.Arguments.ValueAt(argument);
                if (message.Arguments.NameAt(argument) != MessageLabel || carried.Message is not { } inner) throw new GesWireException(GesWireException.TypeMismatch, MessageLabel);
                // Errors of the carried message name it as the argument message, as the lenient read does.
                try
                {
                    EncodeMember(ref writer, schema, carried, inner);
                }
                catch (GesWireException exception)
                {
                    throw new GesWireException(exception.Code, exception.Path.Length > 0 ? MessageLabel + "." + exception.Path : MessageLabel, exception.Detail);
                }

                return writer.Finish();
            }

            EncodeMember(ref writer, schema, value, message);
            return writer.Finish();
        }

        if (schema.Kind == GesSchemaKind.Message) EncodeMessage(ref writer, schema, value);
        else
        {
            if (value.CustomTypeName is { } name && name != schema.Name) throw new GesWireException(GesWireException.TypeMismatch, "");
            var fields = value.MaterializedMap() ?? throw new GesWireException(GesWireException.TypeMismatch, "");
            EncodeFields(ref writer, schema, 0, schema.Nodes.Length, fields, false);
        }

        return writer.Finish();
    }

    // Selects the member of a set that a message names and encodes the message.
    private static void EncodeMember(ref GesSchemaWriter writer, GesBoundSchema schema, GesValue value, GameEventScriptMessage message)
    {
        var member = Array.FindIndex(schema.Members, candidate => candidate.Name == message.Name);
        if (member < 0) throw new GesWireException(GesWireException.TypeMismatch, "");
        writer.SelectMember(member);
        EncodeMessage(ref writer, schema.Members[member], value);
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
        List<GesMessageArgument>? envelope = null;
        if (schema.Kind == GesSchemaKind.Messages)
        {
            if (schema.Nodes.Length > 0)
            {
                envelope = new List<GesMessageArgument>(schema.Labels.Length + 1);
                for (var index = 0; index < schema.Nodes.Length; index = schema.Nodes[index].Next)
                    envelope.Add(new GesMessageArgument(schema.Labels[envelope.Count], DecodeNode(ref reader, schema, index)));
            }

            current = schema.Members[reader.ReadMember()];
        }

        var value = current.Kind == GesSchemaKind.Message ? DecodeMessage(ref reader, current) : DecodeRecord(ref reader, current, current.Name, 0, current.Nodes.Length);
        if (envelope is not null)
        {
            envelope.Add(new GesMessageArgument(MessageLabel, value));
            value = GesValue.GesMessage(GameEventScriptMessage.Create(schema.Name, envelope, []));
        }

        return new GesSchemaDecoded(value, reader.Finish());
    }

    /// <summary>Reads the key of an encoded record without decoding the rest.</summary>
    /// <param name="schema">The bound schema, which has a key.</param>
    /// <param name="data">The data.</param>
    /// <returns>The key as an Int64, or the tag index of a Tag key.</returns>
    public static long ReadKey(GesBoundSchema schema, ReadOnlySpan<byte> data) => new GesSchemaReader(schema, data).ReadKey();

    /// <summary>
    /// Completes a Message whose arguments are a subsequence of the declared labels in declared order: a missing argument
    /// takes its default, or Nothing when it is optional. The result has every declared argument and encodes strictly. This
    /// lenient read belongs to in-process receivers and to the typing step before encoding, never to a decoder; it checks
    /// labels only, so kinds and ranges are checked when the result is encoded or read.
    /// </summary>
    /// <param name="schema">The bound message or message set; a set selects its member by the message name.</param>
    /// <param name="message">The message.</param>
    /// <returns>The completed message with the same name and tags, or the message itself when nothing is missing.</returns>
    /// <exception cref="GesWireException">
    /// <c>wire.missingField</c> with the argument path when an argument without default is missing and not optional;
    /// <c>wire.typeMismatch</c> when the name is not a message of the schema or a label is unknown, repeated or out of order.
    /// </exception>
    public static GameEventScriptMessage CompleteMessage(GesBoundSchema schema, GameEventScriptMessage message)
    {
        if (schema is null) throw new ArgumentNullException(nameof(schema));
        if (message is null) throw new ArgumentNullException(nameof(message));
        return Complete(schema.Schema.Declaration, message);
    }

    /// <summary>
    /// Completes a Message like <see cref="CompleteMessage(GesBoundSchema, GameEventScriptMessage)"/> without binding the
    /// schema, since the lenient read depends on no parameter value. It also completes messages whose arguments hold free
    /// data, such as a <c>:Map</c>, which have no wire form; generated models use it.
    /// </summary>
    /// <param name="schema">The message or message set; a set selects its member by the message name.</param>
    /// <param name="message">The message.</param>
    /// <returns>The completed message with the same name and tags, or the message itself when nothing is missing.</returns>
    /// <exception cref="GesWireException">
    /// <c>wire.missingField</c> with the argument path when an argument without default is missing and not optional;
    /// <c>wire.typeMismatch</c> when the name is not a message of the schema or a label is unknown, repeated or out of order.
    /// </exception>
    public static GameEventScriptMessage CompleteMessage(GesSchema schema, GameEventScriptMessage message)
    {
        if (schema is null) throw new ArgumentNullException(nameof(schema));
        if (message is null) throw new ArgumentNullException(nameof(message));
        return Complete(schema.Declaration, message);
    }

    private static GameEventScriptMessage Complete(GesResolvedDeclaration schema, GameEventScriptMessage message)
    {
        if (schema.Kind == GesSchemaKind.Messages && schema.Fields.Length > 0)
        {
            // The envelope of a set with parameters: its own arguments, then the carried message, completed in turn.
            if (message.Name != schema.Name) throw new GesWireException(GesWireException.TypeMismatch, "");
            var arguments = new List<GesMessageArgument>(message.Arguments);
            var last = arguments.Count - 1;
            if (last < 0 || arguments[last].Name != MessageLabel)
            {
                if (arguments.Exists(candidate => candidate.Name == MessageLabel)) throw new GesWireException(GesWireException.TypeMismatch, MessageLabel);
                throw new GesWireException(GesWireException.MissingField, MessageLabel);
            }

            if (arguments[last].Value.Message is not { } carried) throw new GesWireException(GesWireException.TypeMismatch, MessageLabel);
            GameEventScriptMessage inner;
            try
            {
                inner = Complete(schema.Members.Length > 0 ? MemberOf(schema, carried) : schema, carried);
            }
            catch (GesWireException exception)
            {
                throw new GesWireException(exception.Code, exception.Path.Length > 0 ? MessageLabel + "." + exception.Path : MessageLabel, exception.Detail);
            }

            arguments.RemoveAt(last);
            var own = Complete(schema, arguments);
            if (own is null && ReferenceEquals(inner, carried)) return message;
            own ??= arguments;
            own.Add(new GesMessageArgument(MessageLabel, GesValue.GesMessage(inner)));
            return GameEventScriptMessage.Create(message.Name, own, message.Tags);
        }

        var current = schema.Kind == GesSchemaKind.Messages ? MemberOf(schema, message) : schema;
        if (current.Kind != GesSchemaKind.Message || message.Name != current.Name) throw new GesWireException(GesWireException.TypeMismatch, "");
        var completed = Complete(current, new List<GesMessageArgument>(message.Arguments));
        return completed is null ? message : GameEventScriptMessage.Create(message.Name, completed, message.Tags);
    }

    // The label of the argument that carries the message of a set with parameters.
    internal const string MessageLabel = "message";

    private static GesResolvedDeclaration MemberOf(GesResolvedDeclaration set, GameEventScriptMessage message)
    {
        var member = Array.FindIndex(set.Members, candidate => candidate.Name == message.Name);
        return member < 0 ? throw new GesWireException(GesWireException.TypeMismatch, "") : set.Members[member];
    }

    // The label of an argument; an unlabeled one has the label _.
    private static string Label(GesResolvedField field) => field.Unlabeled ? "_" : field.Name;

    // The arguments with every declared label, or null when none is missing.
    private static List<GesMessageArgument>? Complete(GesResolvedDeclaration current, List<GesMessageArgument> given)
    {
        var fields = current.Fields;
        var completed = new List<GesMessageArgument>(fields.Length);
        var position = 0;
        for (var argument = 0; argument < fields.Length; argument++)
        {
            var field = fields[argument];
            var label = Label(field);
            if (position < given.Count && given[position].Name == label) completed.Add(given[position++]);
            else if (field.Default is { } fallback) completed.Add(new GesMessageArgument(label, Literal(field, fallback)));
            else if (field.Marker == GesSchemaFieldMarker.Optional) completed.Add(new GesMessageArgument(label, GesValue.GesNothing()));
            // An unknown label names its own argument (the message itself), as a label left over at the end does.
            else if (position < given.Count && !Declared(current, argument, given[position].Name)) throw new GesWireException(GesWireException.TypeMismatch, ArgumentPath(current, given[position].Name));
            else throw new GesWireException(GesWireException.MissingField, field.Name);
        }

        if (position < given.Count) throw new GesWireException(GesWireException.TypeMismatch, ArgumentPath(current, given[position].Name));
        return given.Count == completed.Count ? null : completed;
    }

    // The value of an argument's literal default; the kind, unit and integer form come from its only type, as when it binds,
    // and an empty default is the empty Map of a Map and the empty List otherwise.
    private static GesValue Literal(GesResolvedField field, GesResolvedDefault fallback)
    {
        if (field.Alternatives.Length != 1) return Literal(fallback, false, false, default);
        var type = field.Alternatives[0];
        if (fallback.Kind == GesSchemaDefaultKind.Empty) return type.Kind == GesSchemaTypeKind.Map ? GesValue.GesMap([], []) : GesValue.GesList([]);
        var number = type.Kind is GesSchemaTypeKind.Number or GesSchemaTypeKind.Percentage or GesSchemaTypeKind.Quantity;
        return Literal(fallback, number && type.Constraints.Integer && type.Constraints.Step is null, type.Kind == GesSchemaTypeKind.Percentage, type.Unit);
    }

    // Whether a label is declared at or after an argument, so that a missing argument rather than a wrong label stops a read.
    private static bool Declared(GesResolvedDeclaration schema, int from, string label)
    {
        for (var index = from; index < schema.Fields.Length; index++)
            if (Label(schema.Fields[index]) == label) return true;
        return false;
    }

    // The path of the argument a label names, or the empty path of the message for a label it does not declare.
    private static string ArgumentPath(GesResolvedDeclaration schema, string label)
    {
        foreach (var field in schema.Fields)
            if (Label(field) == label) return field.Name;
        return "";
    }

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

    // The value of a literal default that names no sibling: a Boolean, a tag, the Map of a record literal, the empty List,
    // or a number of the field's kind and unit.
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
            case GesSchemaDefaultKind.Empty:
                // Only a List binds, so an empty default that a bound schema holds is the empty List.
                return GesValue.GesList([]);
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
            case GesSchemaNodeKind.List when node.ValueKind == GesSchemaTypeKind.Map:
                EncodeHandleMap(ref writer, schema, index, value, strict);
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
            case GesSchemaTypeKind.Handle: return value.Handle?.TypeName == candidate.SchemaName;
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

        if (node.ValueKind == GesSchemaTypeKind.Handle)
        {
            EncodeHandle(ref writer, schema, index, value);
            return;
        }

        if (value.CustomTypeName is { } name2 && name2 != node.SchemaName) throw new GesWireException(GesWireException.TypeMismatch, node.Path);
        var fields = value.MaterializedMap() ?? throw new GesWireException(GesWireException.TypeMismatch, node.Path);
        if (strict && value.CustomTypeName is null && !HasEveryKey(schema, index, fields)) throw new GesWireException(GesWireException.TypeMismatch, node.Path);
        writer.BeginNested(field);
        EncodeFields(ref writer, schema, index + 1, node.Next, fields, strict);
    }

    // A handle sends its identity: the id, the version of an `@Name~` field and a composite's part; a version in an `@Name`
    // field is dropped.
    private static void EncodeHandle(ref GesSchemaWriter writer, GesBoundSchema schema, int index, GesValue value)
    {
        ref readonly var node = ref schema.Nodes[index];
        if (value.Handle is not { } handle || handle.TypeName != node.SchemaName) throw new GesWireException(GesWireException.TypeMismatch, node.Path);
        if (!handle.Ref.IsNothing) throw new GesWireException(GesWireException.UnresolvedRef, node.Path);
        writer.BeginNested(new GesSchemaField(schema, index));
        var child = index + 1;
        EncodeNode(ref writer, schema, child, handle.Id, false);
        child = schema.Nodes[child].Next;
        if (child < node.Next && schema.Nodes[child].Name == "version")
        {
            if (!handle.HasVersion) throw new GesWireException(GesWireException.MissingVersion, schema.Nodes[child].Path);
            EncodeNode(ref writer, schema, child, handle.Version, false);
            child = schema.Nodes[child].Next;
        }

        if (child < node.Next) EncodeNode(ref writer, schema, child, handle.Part, false);
    }

    // A handle Map sends its entries in ascending key order, which is the canonical order of its handle keys.
    private static void EncodeHandleMap(ref GesSchemaWriter writer, GesBoundSchema schema, int index, GesValue value, bool strict)
    {
        ref readonly var node = ref schema.Nodes[index];
        if (value.CustomTypeName is not null || value.AsMap() is not { } map || map.TextKeyCount != 0) throw new GesWireException(GesWireException.TypeMismatch, node.Path);
        writer.BeginList(new GesSchemaField(schema, index), map.Length);
        var entry = new GesSchemaField(schema, index + 1);
        var key = index + 2;
        var element = schema.Nodes[key].Next;
        for (var position = 0; position < map.Length; position++)
        {
            writer.BeginNested(entry);
            EncodeNode(ref writer, schema, key, map.KeyValueAt(position), strict);
            EncodeNode(ref writer, schema, element, map.ValueAt(position), strict);
        }
    }

    private static GesValue DecodeHandle(ref GesSchemaReader reader, GesBoundSchema schema, int index)
    {
        ref readonly var node = ref schema.Nodes[index];
        reader.BeginNested(new GesSchemaField(schema, index));
        var child = index + 1;
        var id = DecodeNode(ref reader, schema, child);
        child = schema.Nodes[child].Next;
        var version = default(GesValue);
        if (child < node.Next && schema.Nodes[child].Name == "version")
        {
            version = DecodeNode(ref reader, schema, child);
            child = schema.Nodes[child].Next;
        }

        var composite = child < node.Next;
        var part = composite ? DecodeNode(ref reader, schema, child) : default;
        return GesValue.GesHandle(GesHandle.FromWire(node.SchemaName, id, version, part, composite));
    }

    private static GesValue DecodeHandleMap(ref GesSchemaReader reader, GesBoundSchema schema, int index)
    {
        ref readonly var node = ref schema.Nodes[index];
        var count = reader.BeginList(new GesSchemaField(schema, index));
        var entry = new GesSchemaField(schema, index + 1);
        var key = index + 2;
        var element = schema.Nodes[key].Next;
        var keys = new List<GesHandle>(Math.Min(count, 1024));
        var values = new List<GesValue>(Math.Min(count, 1024));
        for (var position = 0; position < count; position++)
        {
            reader.BeginNested(entry);
            var handle = DecodeHandle(ref reader, schema, key).Handle!;
            if (keys.Count > 0 && keys[^1].CompareTo(handle) >= 0) throw new GesWireException(GesWireException.InvalidKeyOrder, schema.Nodes[key].Path);
            keys.Add(handle);
            values.Add(DecodeNode(ref reader, schema, element));
        }

        return GesValue.GesMap(new GesValueMap([], keys.ToArray(), values.ToArray()));
    }

    // A checked document writes a Box as the Map of its corners, a Poly as the List of its points and a Disc as the Map of its
    // centre and radius, since documents have no geometry literal; this gives them the geometry library's values, as the
    // schema's fields and their alternatives expect. `chosen` holds the alternative that held for each union of several by the
    // exact path of its value, so that a value takes the alternative the check chose rather than the first one its shape fits;
    // a Map of a chosen schema alternative becomes a Record of that schema, which a model reads as that alternative.
    internal static GesValue DocumentShapes(GesResolvedDeclaration schema, GesValue value, IReadOnlyDictionary<(string Position, GesResolvedType[] Alternatives), GesResolvedType>? chosen = null)
        => DocumentShapes(schema, value, "", chosen, value.CustomTypeName);

    private static GesValue DocumentShapes(GesResolvedDeclaration schema, GesValue value, string position, IReadOnlyDictionary<(string Position, GesResolvedType[] Alternatives), GesResolvedType>? chosen, string? name)
    {
        if (value.AsMap() is not { } map) return value;
        var keys = new string[map.Length];
        var values = new GesValue[map.Length];
        for (var index = 0; index < map.Length; index++)
        {
            keys[index] = map.KeyAt(index);
            var entry = map.ValueAt(index);
            var field = Array.Find(schema.Fields, candidate => candidate.Name == keys[index]);
            values[index] = field is null ? entry : DocumentShape(field.Alternatives, entry, GesSchemaDocumentCheck.Join(position, field.Name), chosen);
        }

        return name is not null ? GesValue.GesRecord(name, keys, values) : GesValue.GesMap(keys, values);
    }

    private static GesValue DocumentShape(GesResolvedType[] alternatives, GesValue value, string position, IReadOnlyDictionary<(string Position, GesResolvedType[] Alternatives), GesResolvedType>? chosen)
    {
        GesResolvedType? held = null;
        var candidates = chosen is not null && chosen.TryGetValue((position, alternatives), out held) ? [held] : alternatives;
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
                    var shaped = new GesValue[list.Length];
                    for (var index = 0; index < shaped.Length; index++) shaped[index] = DocumentShape(element, list[index], GesSchemaDocumentCheck.Element(position, index), chosen);
                    return GesValue.GesList(shaped);
                case GesSchemaTypeKind.Map when type.Handle is not null && type.Element is { } values && value.AsMap() is { } entries && value.CustomTypeName is null:
                {
                    var keys = new GesValue[entries.Length];
                    var shapedValues = new GesValue[entries.Length];
                    for (var index = 0; index < keys.Length; index++)
                    {
                        keys[index] = entries.KeyValueAt(index);
                        var entry = entries.HandleKey(index) is { } handle ? GesSchemaDocumentCheck.Entry(position, handle) : GesSchemaDocumentCheck.Element(position, index);
                        shapedValues[index] = DocumentShape(values, entries.ValueAt(index), GesSchemaDocumentCheck.Join(entry, "value"), chosen);
                    }

                    return GesValue.GesMapOfKeys(keys, shapedValues);
                }
                case GesSchemaTypeKind.Reference when value.AsMap() is not null && (value.CustomTypeName is not { } name || name == type.ReferenceName):
                    return DocumentShapes(type.Reference!, value, position, chosen, held is null ? value.CustomTypeName : type.ReferenceName);
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
                GesComputedKind.Vector => value.Vector.ToValue(field.Unit),
                GesComputedKind.Point => value.Point.ToValue(field.Unit),
                GesComputedKind.Box => ((GesBox)value.Shape!).ToValue(field.Unit),
                GesComputedKind.Disc => ((GesDisc)value.Shape!).ToValue(field.Unit),
                GesComputedKind.Poly => ((GesPoly)value.Shape!).ToValue(field.Unit),
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
                GesBytecodeTypeKind.Vector => GesComputedValue.OfVector(value.X, value.Y, value.Z),
                GesBytecodeTypeKind.Point => GesComputedValue.OfPoint(value.X, value.Y, value.Z),
                _ => value.ExternalValue switch
                {
                    GesGeometryLibrary.BoxValue box => GesComputedValue.OfBox(box.Box),
                    GesGeometryLibrary.DiscValue disc => GesComputedValue.OfDisc(disc.Disc),
                    GesGeometryLibrary.PolyValue poly => GesComputedValue.OfPoly(poly.Poly),
                    _ => GesComputedValue.Nothing
                }
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
            case GesSchemaNodeKind.List when node.ValueKind == GesSchemaTypeKind.Map:
                return DecodeHandleMap(ref reader, schema, index);
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
            case GesSchemaNodeKind.Group when node.ValueKind == GesSchemaTypeKind.Handle:
                return DecodeHandle(ref reader, schema, index);
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

    // Content hashes of handle states.
    /// <summary>
    /// Computes the content hash of a handle's state (specs/Schemas.md#content-hash): SHA-256 over its compact product JSON,
    /// normalized against the state schema with defaults applied and absent or <c>nothing</c> optional fields, computed
    /// fields and undeclared keys left out. It is the version of a <see cref="GesHandleVersionKind.ContentHash"/> type and is
    /// available for every handle type.
    /// </summary>
    /// <param name="handleType">The handle type whose state schema normalizes the state.</param>
    /// <param name="state">A Record or Map of the state schema; the kinds and ranges of other values are not checked.</param>
    /// <returns>The content hash.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handleType"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="handleType"/> is a store file type, which has no state.</exception>
    /// <exception cref="GesWireException"><c>wire.typeMismatch</c> with the path when the state, or a value of a field that
    /// only takes Records, is no Record or Map; <c>wire.missingField</c> with the path when a field without default is missing and not optional.</exception>
    public static GesHash32 ContentHash(GesHandleType handleType, GesValue state)
    {
        if (handleType is null) throw new ArgumentNullException(nameof(handleType));
        if (handleType.StoreOf is not null) throw new ArgumentException($"The store file type '@{handleType.Name}' has no state.", nameof(handleType));
        var declarations = handleType.Declarations;
        var normalized = NormalizeState(handleType.Resolved.State, state, "", declarations);
        var json = GameEventScript.Runtime.GesJson.Write(new GameEventScript.Runtime.GesMessageJsonCodec().Encode(normalized, 0));
        var bytes = Encoding.UTF8.GetBytes(json);
        using var sha256 = SHA256.Create();
        return GesHash32.FromBytes(sha256.ComputeHash(bytes));
    }

    /// <summary>
    /// Computes the content hash of a handle's state read through a projection (specs/Schemas.md#projections): the content
    /// hash of the projection's Record, normalized against the projection. It is the version of <c>@Name(:P)~</c> for a
    /// <see cref="GesHandleVersionKind.ContentHash"/> type.
    /// </summary>
    /// <param name="handleType">The handle type whose state the projection narrows.</param>
    /// <param name="projection">A projection of the handle type, one of <see cref="GesHandleType.Projections"/>.</param>
    /// <param name="state">A Record of the state schema, of a schema the projection narrows, or of the projection itself.</param>
    /// <returns>The content hash of the projected state.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handleType"/> or <paramref name="projection"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="projection"/> is no projection of the handle type.</exception>
    /// <exception cref="GesWireException">As <see cref="Project(GesSchema, GesValue)"/> and <see cref="ContentHash(GesHandleType, GesValue)"/>.</exception>
    public static GesHash32 ContentHash(GesHandleType handleType, GesSchema projection, GesValue state)
    {
        if (handleType is null) throw new ArgumentNullException(nameof(handleType));
        if (projection is null) throw new ArgumentNullException(nameof(projection));
        if (handleType.StoreOf is not null || !projection.Declaration.Narrows(handleType.Resolved.State))
            throw new ArgumentException($"':{projection.Name}' is no projection of '@{handleType.Name}'.", nameof(projection));
        return ProjectedHash(handleType, projection.Declaration, Project(projection.Declaration, state));
    }

    // The content hash of a Record of the projection.
    internal static GesHash32 ProjectedHash(GesHandleType handleType, GesResolvedDeclaration projection, GesValue projected)
    {
        var normalized = NormalizeState(projection, projected, "", handleType.Declarations);
        var json = GameEventScript.Runtime.GesJson.Write(new GameEventScript.Runtime.GesMessageJsonCodec().Encode(normalized, 0));
        using var sha256 = SHA256.Create();
        return GesHash32.FromBytes(sha256.ComputeHash(Encoding.UTF8.GetBytes(json)));
    }

    /// <summary>
    /// Converts a value of a projection's source, or of any schema the projection narrows, to the projection
    /// (specs/Schemas.md#projections): each taken field is read along its path, and an absent or <c>nothing</c> Record on
    /// the path leaves the field absent. A Record of the projection itself is returned unchanged.
    /// </summary>
    /// <param name="projection">The projection, a schema whose <see cref="GesSchema.Source"/> is set.</param>
    /// <param name="state">A Record of a schema the projection narrows; a Map without name counts as a value of the schema
    /// at the end of the projection's sources.</param>
    /// <returns>A Record of the projection's name with the taken fields that are present, in the projection's order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="projection"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="projection"/> is no projection.</exception>
    /// <exception cref="GesWireException"><c>wire.typeMismatch</c> with the path when the value is no Record or Map, a Record
    /// of a schema the projection does not narrow, or a Record on a path is no Record or Map.</exception>
    public static GesValue Project(GesSchema projection, GesValue state)
    {
        if (projection is null) throw new ArgumentNullException(nameof(projection));
        if (projection.Source is null) throw new ArgumentException($"':{projection.Name}' is no projection.", nameof(projection));
        return Project(projection.Declaration, state);
    }

    internal static GesValue Project(GesResolvedDeclaration projection, GesValue state)
    {
        if (state.AsMap() is null) throw new GesWireException(GesWireException.TypeMismatch, "");
        if (state.CustomTypeName == projection.Name) return state;
        var source = projection.Source!;
        // A value of the source passes as it is; one of a schema further down first converts to the source.
        if (source.IsProjection && state.CustomTypeName != source.Name)
            state = Project(source, state);
        else if (state.CustomTypeName is { } name && name != source.Name)
            throw new GesWireException(GesWireException.TypeMismatch, "");
        return Select(projection, state);
    }

    // The taken fields of a value of the projection's source, along their paths.
    internal static GesValue Select(GesResolvedDeclaration projection, GesValue state)
    {
        var keys = new List<string>(projection.Fields.Length);
        var values = new List<GesValue>(projection.Fields.Length);
        for (var index = 0; index < projection.Fields.Length; index++)
        {
            var path = projection.Paths[index];
            var current = state;
            var present = true;
            for (var segment = 0; segment < path.Length && present; segment++)
            {
                if (current.AsMap() is not { } map)
                    throw new GesWireException(GesWireException.TypeMismatch, FieldPath(path, segment));
                if (map.Get(path[segment].Name) is { } next && (segment == path.Length - 1 || next.Kind != GesBytecodeTypeKind.Nothing))
                    current = next;
                else present = false;
            }

            if (!present) continue;
            keys.Add(projection.Fields[index].Name);
            values.Add(current);
        }

        return GesValue.GesRecord(projection.Name, keys.ToArray(), values.ToArray());
    }

    private static string FieldPath(GesResolvedField[] path, int count)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < count; index++)
        {
            if (index > 0) builder.Append('.');
            builder.Append(path[index].Name);
        }

        return builder.ToString();
    }

    // The Record of a schema with defaults applied and absent or nothing optional fields, computed fields and undeclared keys left out.
    internal static GesValue NormalizeState(GesResolvedDeclaration schema, GesValue value, string path, IReadOnlyDictionary<string, GesResolvedDeclaration> declarations)
    {
        if (value.AsMap() is not { } fields) throw new GesWireException(GesWireException.TypeMismatch, path);
        var keys = new List<string>(schema.Fields.Length);
        var values = new List<GesValue>(schema.Fields.Length);
        foreach (var field in schema.Fields)
        {
            var fieldPath = path.Length == 0 ? field.Name : path + "." + field.Name;
            if (!HeldState(schema, field, fields, out var held))
            {
                if (field.Marker == GesSchemaFieldMarker.Optional) continue;
                throw new GesWireException(GesWireException.MissingField, fieldPath);
            }

            if (held.Kind == GesBytecodeTypeKind.Nothing && field.Marker == GesSchemaFieldMarker.Optional) continue;
            keys.Add(field.Name);
            values.Add(StateElement(field.Alternatives, held, fieldPath, declarations));
        }

        return GesValue.GesRecord(schema.Name, keys.ToArray(), values.ToArray());
    }

    // The value of a field, or else of its default: a literal, or the value of the sibling it names, whose own default applies in turn.
    private static bool HeldState(GesResolvedDeclaration schema, GesResolvedField field, GesValueMap fields, out GesValue value)
    {
        for (var depth = 0; depth <= schema.Fields.Length; depth++)
        {
            if (fields.Get(field.Name) is { } given)
            {
                value = given;
                return true;
            }

            switch (field.Default)
            {
                case null:
                    value = default;
                    return false;
                case { Kind: GesSchemaDefaultKind.Field } fallback:
                    var sibling = Array.Find(schema.Fields, candidate => candidate.Name == fallback.Name);
                    if (sibling is null) break;
                    field = sibling;
                    continue;
                case var fallback:
                    value = StateLiteral(field, fallback);
                    return true;
            }

            break;
        }

        value = default;
        return false;
    }

    private static GesValue StateLiteral(GesResolvedField field, GesResolvedDefault fallback)
    {
        if (field.Alternatives.Length != 1) return Literal(fallback, false, false, default);
        var type = field.Alternatives[0];
        if (fallback.Kind == GesSchemaDefaultKind.Empty) return type.Kind == GesSchemaTypeKind.Map ? GesValue.GesMap([], []) : GesValue.GesList([]);
        var number = type.Kind is GesSchemaTypeKind.Number or GesSchemaTypeKind.Percentage or GesSchemaTypeKind.Quantity;
        return Literal(fallback, number && type.Constraints.Integer && type.Constraints.Step is null, type.Kind == GesSchemaTypeKind.Percentage, type.Unit);
    }

    // Nested Records normalize against their schema, chosen by the Record's name among the alternatives; Lists and Map
    // values normalize element by element.
    private static GesValue StateElement(GesResolvedType[] alternatives, GesValue value, string path, IReadOnlyDictionary<string, GesResolvedDeclaration> declarations)
    {
        if (value.Kind == GesBytecodeTypeKind.Nothing) return value;
        foreach (var type in alternatives)
        {
            switch (type.Kind)
            {
                case GesSchemaTypeKind.Reference when type.Reference is { } reference && value.AsMap() is not null:
                {
                    var declaration = reference;
                    if (value.CustomTypeName is { } name && name != reference.Name && declarations.TryGetValue(name, out var named)) declaration = named;
                    if (declaration.Kind == GesSchemaKind.Schema && !declaration.Abstract) return NormalizeState(declaration, value, path, declarations);
                    break;
                }
                case GesSchemaTypeKind.List when type.Element is { } element && value.Kind == GesBytecodeTypeKind.List && value.ObjectValue is GesValue[] items:
                {
                    var normalized = new GesValue[items.Length];
                    for (var index = 0; index < items.Length; index++)
                        normalized[index] = StateElement(element, items[index], path + "[]", declarations);
                    return GesValue.GesList(normalized);
                }
                case GesSchemaTypeKind.Map when type.Element is { } mapElement && value.AsMap() is { } map && value.CustomTypeName is null:
                {
                    var keys = new GesValue[map.Length];
                    var values = new GesValue[map.Length];
                    for (var index = 0; index < map.Length; index++)
                    {
                        keys[index] = map.KeyValueAt(index);
                        values[index] = StateElement(mapElement, map.ValueAt(index), path + "[]", declarations);
                    }

                    return GesValue.GesMapOfKeys(keys, values);
                }
            }
        }

        // A field that only takes Records accepts no other value; other values are hashed as they are.
        if (value.AsMap() is null && Array.TrueForAll(alternatives, type => type.Kind == GesSchemaTypeKind.Reference))
            throw new GesWireException(GesWireException.TypeMismatch, path);
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
