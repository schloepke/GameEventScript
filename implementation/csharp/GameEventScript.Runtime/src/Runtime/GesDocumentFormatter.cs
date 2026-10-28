// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime;

// The recommended document layout of specs/Documents.md. Only its round trip through GesLiteralParser is normative.
internal static class GesDocumentFormatter
{
    internal static string Print(GesDocument document, GesDocumentFormat format)
    {
        var builder = new StringBuilder();
        if (document.Schema is { } schema)
        {
            builder.Append(":Schema(");
            if (schema.Kind == GesDocumentSchemaKind.Type) builder.Append(':').Append(schema.Name);
            else GesTextLiteralReader.AppendQuoted(builder, schema.Name);
            builder.Append(")\n");
        }
        var entries = document.Entries;
        for (var i = 0; i < entries.Length; i++)
        {
            var start = builder.Length;
            AppendKey(builder, entries.KeyAt(i));
            builder.Append(": ");
            WriteValue(builder, entries.ValueAt(i), 0, ScalarLength(builder, start), format, commas: false);
            builder.Append('\n');
        }
        return builder.ToString();
    }

    internal static string Format(in GesValue value, GesDocumentFormat format)
    {
        if (!IsPrintable(in value)) throw new ArgumentException("The value has no readable literal text.", nameof(value));
        var builder = new StringBuilder();
        WriteValue(builder, value, 0, 0, format, commas: true);
        return builder.ToString();
    }

    // Values whose literal text reads back: no External values, positive Dice rolls and Record type names that
    // follow the TypeName grammar, at every level.
    internal static bool IsPrintable(in GesValue value)
    {
        switch (value.Kind)
        {
            case List when value.ObjectValue is GesValue[] list:
                foreach (var item in list)
                    if (!IsPrintable(in item)) return false;
                return true;
            case Map when value.ObjectValue is GesValueMap map:
                return IsPrintable(map);
            case Custom:
                return value.ObjectValue is GesCustomObject record && GesText.IsTypeName(record.TypeName) && IsPrintable(record.Map);
            case Dice:
                if (value.ObjectValue is int[] rolls)
                    foreach (var roll in rolls)
                        if (roll <= 0) return false;
                return true;
            case Message when value.ObjectValue is GameEventScriptMessage message:
                for (var i = 0; i < message.Arguments.Count; i++)
                    if (!IsPrintable(message.Arguments[i])) return false;
                return true;
            case Nothing or GesBytecodeTypeKind.Boolean or Integer or Float or Percentage or Tag or Text or Vector or Point
                or GesBytecodeTypeKind.Range or Series or Handler:
                return true;
            default:
                return false;
        }
    }

    private static bool IsPrintable(GesValueMap map)
    {
        for (var i = 0; i < map.Length; i++)
        {
            var item = map.ValueAt(i);
            if (!IsPrintable(in item)) return false;
        }
        return true;
    }

    // Writes a value inline when it fits from the current column, otherwise a non-empty container in block form.
    // Documents separate block entries by line breaks; single values add commas so that `parse` reads them back.
    private static void WriteValue(StringBuilder builder, GesValue value, int level, int column, GesDocumentFormat format, bool commas)
    {
        if (!IsBlockContainer(in value))
        {
            AppendInline(builder, in value, format, int.MaxValue);
            return;
        }
        // Measuring stops once the inline text cannot fit: more than twice the remaining UTF-16 units exceed the
        // remaining scalars, so each nesting level renders at most about one line.
        var remaining = format.LineWidth - column;
        var inline = new StringBuilder();
        var stop = remaining > int.MaxValue / 2 ? int.MaxValue - 1 : 2 * remaining;
        if (remaining >= 0 && AppendInline(inline, in value, format, stop) && ScalarLength(inline, 0) <= remaining)
        {
            builder.Append(inline);
            return;
        }
        switch (value.ObjectValue)
        {
            case GesValue[] list:
                builder.Append("[\n");
                for (var i = 0; i < list.Length; i++)
                {
                    Indent(builder, level + 1, format);
                    WriteValue(builder, list[i], level + 1, (level + 1) * format.Indentation, format, commas);
                    if (commas && i < list.Length - 1) builder.Append(',');
                    builder.Append('\n');
                }
                Indent(builder, level, format);
                builder.Append(']');
                break;
            case GesValueMap map:
                WriteMapBlock(builder, map, level, format, commas);
                break;
            case GesCustomObject record:
                builder.Append(":Record(");
                GesTextLiteralReader.AppendQuoted(builder, record.TypeName);
                builder.Append(", ");
                WriteMapBlock(builder, record.Map, level, format, commas);
                builder.Append(')');
                break;
        }
    }

    private static void WriteMapBlock(StringBuilder builder, GesValueMap map, int level, GesDocumentFormat format, bool commas)
    {
        builder.Append("[\n");
        for (var i = 0; i < map.Length; i++)
        {
            Indent(builder, level + 1, format);
            var start = builder.Length;
            AppendKey(builder, map.KeyAt(i));
            builder.Append(": ");
            WriteValue(builder, map.ValueAt(i), level + 1, (level + 1) * format.Indentation + ScalarLength(builder, start), format, commas);
            if (commas && i < map.Length - 1) builder.Append(',');
            builder.Append('\n');
        }
        Indent(builder, level, format);
        builder.Append(']');
    }

    private static bool IsBlockContainer(in GesValue value) => value.ObjectValue switch
    {
        GesValue[] list => value.Kind == List && list.Length > 0,
        GesValueMap map => map.Length > 0,
        GesCustomObject record => record.Map.Length > 0,
        _ => false
    };

    // Appends the single-line form. With a finite stop, measuring ends early and returns false once the text exceeds
    // stop UTF-16 units or contains a line break, because it then cannot be printed inline.
    private static bool AppendInline(StringBuilder builder, in GesValue value, GesDocumentFormat format, int stop)
    {
        switch (value.Kind)
        {
            case Text:
                if (stop != int.MaxValue && value.TextValue.IndexOf('\n') >= 0) return false;
                GesTextLiteralReader.AppendQuoted(builder, value.TextValue);
                break;
            case Integer or Float when value.Unit != GesQuantityUnit.UnitNone:
                builder.Append(format.PrefixedUnits ? FormatQuantity(in value) : value.ToText);
                break;
            case List when value.ObjectValue is GesValue[] list:
                builder.Append('[');
                for (var i = 0; i < list.Length; i++)
                {
                    if (i > 0) builder.Append(", ");
                    if (!AppendInline(builder, in list[i], format, stop)) return false;
                }
                builder.Append(']');
                break;
            case Map when value.ObjectValue is GesValueMap map:
                if (!AppendInlineMap(builder, map, format, stop)) return false;
                break;
            case Custom when value.ObjectValue is GesCustomObject record:
                builder.Append(":Record(");
                GesTextLiteralReader.AppendQuoted(builder, record.TypeName);
                builder.Append(", ");
                if (!AppendInlineMap(builder, record.Map, format, stop)) return false;
                builder.Append(')');
                break;
            case Message when value.ObjectValue is GameEventScriptMessage message:
                builder.Append(":Message(").Append(message.Name).Append('(');
                for (var i = 0; i < message.Arguments.Count; i++)
                {
                    if (i > 0) builder.Append(", ");
                    var name = message.Arguments.NameAt(i);
                    if (name != "_") builder.Append(name).Append(": ");
                    if (!AppendInline(builder, message.Arguments[i], format, stop)) return false;
                }
                builder.Append(')');
                for (var i = 0; i < message.Tags.Count; i++) builder.Append(i == 0 ? " with #" : ", #").Append(message.Tags[i]);
                builder.Append(')');
                break;
            default:
                builder.Append(value.ToText);
                break;
        }
        return builder.Length <= stop;
    }

    private static bool AppendInlineMap(StringBuilder builder, GesValueMap map, GesDocumentFormat format, int stop)
    {
        if (map.Length == 0)
        {
            builder.Append("[:]");
            return builder.Length <= stop;
        }
        builder.Append('[');
        for (var i = 0; i < map.Length; i++)
        {
            if (i > 0) builder.Append(", ");
            AppendKey(builder, map.KeyAt(i));
            builder.Append(": ");
            var item = map.ValueAt(i);
            if (!AppendInline(builder, in item, format, stop)) return false;
        }
        builder.Append(']');
        return builder.Length <= stop;
    }

    private static void AppendKey(StringBuilder builder, string key)
    {
        if (GesText.IsFieldName(key)) builder.Append(key);
        else GesTextLiteralReader.AppendQuoted(builder, key);
    }

    // Writes a scalar quantity with the exact prefixed spelling whose magnitude is closest to [1, 1000).
    private static string FormatQuantity(in GesValue value)
    {
        var baseText = value.ToText;
        var number = value.Kind == Integer ? value.IntegerValue.ToString(CultureInfo.InvariantCulture) : GesNumber.FormatCanonicalFloat(value.FloatValue);
        if (!Decompose(number, out var negative, out var digits, out var exponent)) return baseText;
        var lead = digits.Length - 1 + exponent;
        string? spelling = null;
        var power = 0;
        var distance = Distance(lead);
        foreach (var suffix in GesQuantityUnits.Suffixes)
        {
            if (suffix.Unit != value.Unit || suffix.Factor != 1 || suffix.Exponent == 0) continue;
            var candidate = Distance(lead - suffix.Exponent);
            if (candidate >= distance) continue;
            distance = candidate;
            spelling = suffix.Text;
            power = suffix.Exponent;
        }
        if (spelling is null) return baseText;
        var text = DecimalText(negative, digits, exponent - power) + spelling;
        return GesTextNumberCast.Read(text, allowGrouping: false) is { } read && read.Kind == value.Kind && read.Unit == value.Unit
            && (value.Kind == Integer ? read.IntegerValue == value.IntegerValue : BitConverter.DoubleToInt64Bits(read.FloatValue) == BitConverter.DoubleToInt64Bits(value.FloatValue))
            ? text
            : baseText;
    }

    // Distance of a decimal leading-digit exponent from the magnitude interval [1, 1000).
    private static int Distance(int lead) => lead < 0 ? -lead : lead > 2 ? lead - 2 : 0;

    // Splits finite numeric text into sign, significant digits without leading or trailing zeros, and the exponent
    // of the last digit. Zero and infinities have no digits.
    private static bool Decompose(string text, out bool negative, out string digits, out int exponent)
    {
        negative = text.StartsWith("-", StringComparison.Ordinal);
        var body = negative ? text.Substring(1) : text;
        exponent = 0;
        var exponentIndex = body.IndexOfAny(['e', 'E']);
        if (exponentIndex >= 0)
        {
            exponent = int.Parse(body.AsSpan(exponentIndex + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            body = body.Substring(0, exponentIndex);
        }
        var point = body.IndexOf('.');
        if (point >= 0)
        {
            exponent -= body.Length - point - 1;
            body = body.Remove(point, 1);
        }
        digits = body.TrimStart('0');
        var trimmed = digits.TrimEnd('0');
        exponent += digits.Length - trimmed.Length;
        digits = trimmed;
        foreach (var c in digits)
            if (c is < '0' or > '9') return false;
        return digits.Length > 0;
    }

    private static string DecimalText(bool negative, string digits, int exponent)
    {
        var builder = new StringBuilder();
        if (negative) builder.Append('-');
        var lead = digits.Length - 1 + exponent;
        if (lead is < -6 or >= 21)
        {
            builder.Append(digits[0]);
            if (digits.Length > 1) builder.Append('.').Append(digits, 1, digits.Length - 1);
            return builder.Append('e').Append(lead.ToString(CultureInfo.InvariantCulture)).ToString();
        }
        if (exponent >= 0) return builder.Append(digits).Append('0', exponent).ToString();
        var point = digits.Length + exponent;
        if (point > 0) return builder.Append(digits, 0, point).Append('.').Append(digits, point, digits.Length - point).ToString();
        return builder.Append("0.").Append('0', -point).Append(digits).ToString();
    }

    private static void Indent(StringBuilder builder, int level, GesDocumentFormat format) => builder.Append(' ', level * format.Indentation);

    private static int ScalarLength(StringBuilder builder, int start)
    {
        var length = 0;
        for (var i = start; i < builder.Length; i++)
            if (!char.IsLowSurrogate(builder[i])) length++;
        return length;
    }
}
