// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace GameEventScript.Runtime;
// A bounded JSON syntax layer; no host, Compiler, filesystem or JSON platform dependency.
internal static class GesJson
{
    internal static GameEventScriptMessageFormatException Error(string code = "message.invalidValue") => new(code);
    internal static GesValue Object(params (string, GesValue)[] fields)
    {
        var keys = new string[fields.Length];
        var values = new GesValue[fields.Length];
        for (var i = 0; i < fields.Length; i++)
        {
            keys[i] = fields[i].Item1;
            values[i] = fields[i].Item2;
        }

        var result = new GesValue();
        result.SetMap(new GesValueMap(keys, values, keys.Length));
        return result;
    }

    internal static GesValue Array(GesValue[] values)
    {
        var result = new GesValue();
        result.SetList(values);
        return result;
    }

    internal static string Write(GesValue value)
    {
        var builder = new StringBuilder();
        var items = 0;
        WriteValue(value, builder, 0, ref items);
        return builder.ToString();
    }

    private static void WriteValue(GesValue value, StringBuilder builder, int depth, ref int items)
    {
        if (depth > 256 || ++items > 524288 || builder.Length > 4194304)
            throw Error("message.resourceLimit");
        switch (value.Kind)
        {
            case Text:
                Quote(builder, value.TextValue);
                break;
            case GameEventScriptBytecodeTypeKind.Boolean:
                builder.Append(value.IsTrue ? "true" : "false");
                break;
            case Integer:
                builder.Append(value.IntegerValue.ToString(CultureInfo.InvariantCulture));
                break;
            case Nothing:
                builder.Append("null");
                break;
            case List:
                builder.Append('[');
                var list = value.AsList();
                for (var i = 0; i < list.Length; i++)
                {
                    if (i > 0)
                        builder.Append(',');
                    WriteValue(list[i], builder, depth + 1, ref items);
                }

                builder.Append(']');
                break;
            case Map:
                builder.Append('{');
                var map = value.AsMap()!;
                for (var i = 0; i < map.Length; i++)
                {
                    if (i > 0)
                        builder.Append(',');
                    Quote(builder, map.KeyAt(i));
                    builder.Append(':');
                    WriteValue(map.ValueAt(i), builder, depth + 1, ref items);
                }

                builder.Append('}');
                break;
            default:
                throw Error();
        }

        if (builder.Length > 4194304)
            throw Error("message.resourceLimit");
    }

    private static void Quote(StringBuilder b, string text)
    {
        GameEventScriptText.RequireValidUnicode(text, nameof(text));
        if (text.Length > 4194304)
            throw Error("message.resourceLimit");
        b.Append('"');
        foreach (var c in text)
            if (c is '"' or '\\')
                b.Append('\\').Append(c);
            else if (c < 32)
                b.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
            else
                b.Append(c);
        b.Append('"');
    }

    internal static GesValue Read(string text)
    {
        if (text is null)
            throw new ArgumentNullException(nameof(text));
        if (text.Length > 4194304)
            throw Error("message.resourceLimit");
        var parser = new Parser(text);
        var value = parser.Value(0);
        parser.Space();
        if (parser.Position != text.Length)
            throw Error("message.invalidJson");
        return value;
    }

    private sealed class Parser(string text)
    {
        internal int Position;
        private int _items;
        internal void Space()
        {
            while (Position < text.Length && text[Position] is ' ' or '\t' or '\n' or '\r')
                Position++;
        }

        private bool Take(char c)
        {
            if (Position >= text.Length || text[Position] != c)
                return false;
            Position++;
            return true;
        }

        internal GesValue Value(int depth)
        {
            if (depth > 256 || ++_items > 524288)
                throw Error("message.resourceLimit");
            Space();
            if (Position == text.Length)
                throw Error("message.invalidJson");
            if (text[Position] == '"')
                return GesValue.GesText(String());
            if (Take('['))
            {
                var values = new List<GesValue>();
                Space();
                if (Take(']'))
                    return Array([]);
                do
                {
                    values.Add(Value(depth + 1));
                    Space();
                    if (Take(']'))
                        return Array(values.ToArray());
                }
                while (Take(','));
                throw Error("message.invalidJson");
            }

            if (Take('{'))
            {
                var fields = new List<(string, GesValue)>();
                var names = new HashSet<string>(StringComparer.Ordinal);
                Space();
                if (Take('}'))
                    return Object();
                do
                {
                    Space();
                    var key = String();
                    if (!names.Add(key))
                        throw Error("message.invalidJson");
                    Space();
                    if (!Take(':'))
                        throw Error("message.invalidJson");
                    fields.Add((key, Value(depth + 1)));
                    Space();
                    if (Take('}'))
                        return Object(fields.ToArray());
                }
                while (Take(','));
                throw Error("message.invalidJson");
            }

            foreach (var token in new[]
            {
                "true",
                "false",
                "null"
            }

            )
                if (text.AsSpan(Position).StartsWith(token.AsSpan(), StringComparison.Ordinal))
                {
                    Position += token.Length;
                    return token == "null" ? default : GesValue.GesBoolean(token == "true");
                }

            var start = Position;
            Take('-');
            if (!Take('0'))
            {
                if (Position == text.Length || text[Position] is < '1' or > '9')
                    throw Error("message.invalidJson");
                while (Position < text.Length && text[Position] is >= '0' and <= '9')
                    Position++;
            }

            // Envelope versions are integer JSON numbers; all GES numeric data uses strings.
            if (!long.TryParse(text.AsSpan(start, Position - start), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number))
                throw Error("message.invalidJson");
            return GesValue.GesInteger(number);
        }

        private string String()
        {
            if (!Take('"'))
                throw Error("message.invalidJson");
            var b = new StringBuilder();
            while (Position < text.Length)
            {
                var c = text[Position++];
                if (c == '"')
                {
                    var result = b.ToString();
                    try
                    {
                        GameEventScriptText.RequireValidUnicode(result, nameof(text));
                    }
                    catch (ArgumentException)
                    {
                        throw Error("message.invalidJson");
                    }

                    return result;
                }

                if (c < 32)
                    throw Error("message.invalidJson");
                if (c != '\\')
                {
                    b.Append(c);
                    continue;
                }

                if (Position == text.Length)
                    throw Error("message.invalidJson");
                c = text[Position++];
                switch (c)
                {
                    case '"':
                    case '\\':
                    case '/':
                        b.Append(c);
                        break;
                    case 'b':
                        b.Append('\b');
                        break;
                    case 'f':
                        b.Append('\f');
                        break;
                    case 'n':
                        b.Append('\n');
                        break;
                    case 'r':
                        b.Append('\r');
                        break;
                    case 't':
                        b.Append('\t');
                        break;
                    case 'u':
                        if (Position + 4 > text.Length || !ushort.TryParse(text.AsSpan(Position, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var hex))
                            throw Error("message.invalidJson");
                        b.Append((char)hex);
                        Position += 4;
                        break;
                    default:
                        throw Error("message.invalidJson");
                }
            }

            throw Error("message.invalidJson");
        }
    }
}
