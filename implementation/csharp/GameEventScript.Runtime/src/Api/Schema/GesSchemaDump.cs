// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Buffers;
using System.Globalization;
using System.Text;
using GameEventScript.Syntax;

namespace GameEventScript.Api;

/// <summary>The readable form of encoded data.</summary>
public readonly struct GesSchemaDumpResult
{
    internal GesSchemaDumpResult(string text, bool isValid)
    {
        Text = text;
        IsValid = isValid;
    }

    /// <summary>Gets one line per field. The first invalid field ends the text with its error, field path and bit offset.</summary>
    public string Text { get; }

    /// <summary>Gets whether the data was a valid encoding.</summary>
    public bool IsValid { get; }
}

/// <summary>Makes binary data readable through a bound schema.</summary>
public static class GesSchemaDump
{
    /// <summary>Lists each field of an encoded record with its path, bit offset, width and decoded value.</summary>
    /// <param name="schema">The bound schema.</param>
    /// <param name="data">The data.</param>
    /// <returns>The text and whether the data was valid.</returns>
    public static GesSchemaDumpResult Read(GesBoundSchema schema, ReadOnlySpan<byte> data)
    {
        if (schema is null) throw new ArgumentNullException(nameof(schema));
        var builder = new StringBuilder();
        var reader = new GesSchemaReader(schema, data);
        try
        {
            var current = schema;
            if (schema.Kind == GesSchemaKind.Messages)
            {
                var member = reader.ReadMember();
                current = schema.Members[member];
                builder.Append("message ").Append(current.Name).Append(" index=").Append(member.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }

            Walk(ref reader, current, 0, current.Nodes.Length, builder);
            var count = reader.Finish();
            builder.Append("bytes ").Append(count.ToString(CultureInfo.InvariantCulture)).Append('\n');
            return new GesSchemaDumpResult(builder.ToString(), true);
        }
        catch (GesWireException exception)
        {
            builder.Append("error ").Append(exception.Code).Append(" path=").Append(exception.Path).Append(" bit=").Append(reader.BitPosition.ToString(CultureInfo.InvariantCulture)).Append('\n');
            return new GesSchemaDumpResult(builder.ToString(), false);
        }
    }

    /// <summary>Lists the fields of the record that a delta produces against a baseline, after a line with the delta's size.</summary>
    /// <param name="schema">The bound schema.</param>
    /// <param name="data">The delta.</param>
    /// <param name="baseline">The full record the delta was encoded against.</param>
    /// <returns>The text and whether the delta was valid.</returns>
    public static GesSchemaDumpResult Read(GesBoundSchema schema, ReadOnlySpan<byte> data, ReadOnlySpan<byte> baseline)
    {
        if (schema is null) throw new ArgumentNullException(nameof(schema));
        var held = Read(schema, baseline);
        if (!held.IsValid) return new GesSchemaDumpResult("baseline " + held.Text.Substring(held.Text.LastIndexOf('\n', held.Text.Length - 2) + 1), false);
        var record = GesSchemaValues.Rent(schema);
        long bit = 0;
        try
        {
            var decoded = GesSchemaDelta.Decode(schema, data, baseline, record, long.MaxValue, ref bit);
            var full = Read(schema, record.AsSpan(0, decoded.ByteCount));
            return new GesSchemaDumpResult("delta bytes " + decoded.DeltaByteCount.ToString(CultureInfo.InvariantCulture) + "\n" + full.Text, full.IsValid);
        }
        catch (GesWireException exception)
        {
            return new GesSchemaDumpResult("error " + exception.Code + " path=" + exception.Path + " bit=" + bit.ToString(CultureInfo.InvariantCulture) + "\n", false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(record);
        }
    }

    private static void Walk(ref GesSchemaReader reader, GesBoundSchema schema, int start, int end, StringBuilder builder)
    {
        for (var index = start; index < end; index = schema.Nodes[index].Next) Field(ref reader, schema, index, builder);
    }

    private static void Field(ref GesSchemaReader reader, GesBoundSchema schema, int index, StringBuilder builder)
    {
        ref readonly var node = ref schema.Nodes[index];
        var field = new GesSchemaField(schema, index);
        var offset = reader.BitPosition;
        if (node.Optional && !reader.ReadPresence(field))
        {
            Line(builder, node.Path, offset, 1, "absent");
            return;
        }

        switch (node.Kind)
        {
            case GesSchemaNodeKind.Boolean:
                var flag = reader.ReadBoolean(field);
                Line(builder, node.Path, offset, reader.BitPosition - offset, flag ? "true" : "false");
                break;
            case GesSchemaNodeKind.Tag:
                var tag = reader.ReadTag(field);
                Line(builder, node.Path, offset, reader.BitPosition - offset, "#" + node.Tags![tag] + " index=" + tag.ToString(CultureInfo.InvariantCulture));
                break;
            case GesSchemaNodeKind.Text:
                var text = reader.ReadTextValue(field);
                Line(builder, node.Path, offset, reader.BitPosition - offset, node.IsTagText ? "#" + text : Quoted(text));
                break;
            case GesSchemaNodeKind.List:
                var length = reader.BeginList(field);
                Line(builder, node.Path, offset, reader.BitPosition - offset, "count=" + length.ToString(CultureInfo.InvariantCulture));
                for (var item = 0; item < length; item++) Field(ref reader, schema, index + 1, builder);
                break;
            case GesSchemaNodeKind.Union:
                var position = reader.ReadAlternative(field);
                var selected = GesSchemaNumbers.Alternative(schema.Nodes, index, position);
                var alternative = schema.Nodes[selected].Path;
                Line(builder, node.Path, offset, reader.BitPosition - offset, alternative.Substring(alternative.LastIndexOf(':') + 1) + " index=" + position.ToString(CultureInfo.InvariantCulture));
                if (schema.Nodes[selected].Kind != GesSchemaNodeKind.Nothing) Field(ref reader, schema, selected, builder);
                break;
            case GesSchemaNodeKind.Group:
                reader.BeginNested(field);
                Walk(ref reader, schema, index + 1, node.Next, builder);
                break;
            case GesSchemaNodeKind.Integer:
                var integer = reader.ReadInteger(field);
                Line(builder, node.Path, offset, reader.BitPosition - offset, integer.ToString(CultureInfo.InvariantCulture));
                break;
            default:
                var number = reader.ReadNumber(field);
                Line(builder, node.Path, offset, reader.BitPosition - offset, Number(number));
                break;
        }
    }

    // A Text stays on its line, whatever the data holds.
    private static string Quoted(string text)
    {
        var builder = new StringBuilder("'");
        foreach (var c in text)
        {
            switch (c)
            {
                case '\\': builder.Append("\\\\"); break;
                case '\'': builder.Append("\\'"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    if (c < ' ' || c == '\u007F') builder.Append("\\u{").Append(((int)c).ToString("x", CultureInfo.InvariantCulture)).Append('}');
                    else builder.Append(c);
                    break;
            }
        }

        return builder.Append('\'').ToString();
    }

    private static string Number(double value)
    {
        if (double.IsInfinity(value)) return value > 0 ? "Infinity" : "-Infinity";
        return value == 0 ? "0" : Spelled(value.ToString("R", CultureInfo.InvariantCulture));
    }

    // The same spelling in every port, from the shortest digits of a roundtrip text.
    private static string Spelled(string text)
    {
        var sign = text[0] == '-' ? "-" : "";
        var mark = text.IndexOfAny(['e', 'E']);
        var mantissa = text.Substring(sign.Length, (mark < 0 ? text.Length : mark) - sign.Length);
        var exponent = mark < 0 ? 0 : int.Parse(text.Substring(mark + 1), CultureInfo.InvariantCulture);
        var dot = mantissa.IndexOf('.');
        var point = (dot < 0 ? mantissa.Length : dot) + exponent;
        var digits = mantissa.Replace(".", "");
        var start = 0;
        while (digits[start] == '0') start++;
        point -= start;
        digits = digits.Substring(start).TrimEnd('0');
        if (point > 0 && point <= 21) return sign + (digits.Length <= point ? digits.PadRight(point, '0') : digits.Substring(0, point) + "." + digits.Substring(point));
        if (point <= 0 && point > -4) return sign + "0." + new string('0', -point) + digits;
        var written = point - 1;
        return sign + digits.Substring(0, 1) + (digits.Length > 1 ? "." + digits.Substring(1) : "") + (written < 0 ? "e-" : "e+") + Math.Abs(written).ToString(CultureInfo.InvariantCulture);
    }

    private static void Line(StringBuilder builder, string path, long offset, long width, string value)
        => builder.Append(path).Append(" bit=").Append(offset.ToString(CultureInfo.InvariantCulture)).Append(" width=").Append(width.ToString(CultureInfo.InvariantCulture)).Append(" value=").Append(value).Append('\n');
}
