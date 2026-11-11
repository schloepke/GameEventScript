// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GameEventScript.Api;

namespace GameEventScript.Generator;

// The canonical JSON form of a layout: members in a fixed order, two spaces of indentation, one value per line, integers
// as exact decimals and binary64 values as their bit patterns. Both ports write identical bytes.
internal static class GesSchemaLayoutJson
{
    internal static string Write(GesSchemaLayout layout)
    {
        var json = new Writer();
        json.BeginObject();
        json.Number("layout", GesSchemaLayout.Version);
        json.Strings("roots", layout.Roots);
        json.BeginArray("parameters");
        foreach (var parameter in layout.Parameters)
        {
            json.BeginObject();
            json.String("name", parameter.Name);
            json.String("type", parameter.Type);
            json.String("unit", parameter.Unit);
            json.String("value", parameter.Value);
            json.EndObject();
        }

        json.EndArray();
        json.BeginArray("types");
        foreach (var type in layout.Types) Type(json, type);
        json.EndArray();
        json.EndObject();
        return json.ToString();
    }

    private static void Type(Writer json, GesLayoutType type)
    {
        json.BeginObject();
        json.String("name", type.Name);
        json.String("kind", type.Kind switch { GesSchemaKind.Schema => "schema", GesSchemaKind.Message => "message", _ => "messages" });
        json.String("hash", Hex(type.Hash));
        json.String("bindingHash", Hex(type.BindingHash));
        json.Strings("bindingHashDepends", type.BindingHashDepends);
        json.Number("maxByteCount", type.MaxByteCount);
        json.Strings("maxByteCountDepends", type.MaxByteCountDepends);
        if (type.Kind == GesSchemaKind.Messages)
        {
            json.Strings("members", type.Members);
            json.Number("memberWidth", type.MemberWidth);
        }
        else
        {
            json.BeginArray("fields");
            foreach (var field in type.Fields) Field(json, field, member: true);
            json.EndArray();
            Constants(json, type.Constants);
            if (type.Kind == GesSchemaKind.Message) json.Strings("tags", type.Tags);
        }

        json.EndObject();
    }

    // Only a schema with lets carries them.
    internal static void Constants(Writer json, List<GesLayoutConstant> constants)
    {
        if (constants.Count == 0) return;
        json.BeginArray("constants");
        foreach (var constant in constants)
        {
            json.BeginObject();
            json.String("name", constant.Name);
            json.String("type", constant.Type);
            json.String("unit", constant.Unit);
            json.String("value", constant.Value);
            json.Boolean("integer", constant.Integer);
            json.EndObject();
        }

        json.EndArray();
    }

    private static void Field(Writer json, GesLayoutField field, bool member, bool value = false)
    {
        if (value) json.BeginValueObject();
        else json.BeginObject();
        json.String("name", field.Name);
        json.String("path", field.Path);
        if (member)
        {
            json.Boolean("optional", field.Optional);
            json.Boolean("key", field.Key);
            json.Boolean("unlabeled", field.Unlabeled);
        }

        json.Key("encoding");
        Encoding(json, field.Encoding);
        json.EndObject();
    }

    private static void Encoding(Writer json, GesLayoutEncoding encoding)
    {
        json.BeginValueObject();
        json.String("kind", Kind(encoding.Kind));
        switch (encoding.Kind)
        {
            case GesLayoutKind.Integer or GesLayoutKind.Quantized or GesLayoutKind.Float:
                json.String("type", encoding.Type);
                json.String("unit", encoding.Unit);
                json.Boolean("range", encoding.Range);
                if (encoding.Kind != GesLayoutKind.Float)
                {
                    json.Boolean("wrapping", encoding.Wrapping);
                    json.Number("lower", encoding.Lower);
                    json.Unsigned("maxIndex", encoding.MaxIndex);
                }

                json.Number("width", encoding.Width);
                if (encoding.Kind == GesLayoutKind.Quantized)
                {
                    json.Number("numerator", encoding.Numerator);
                    json.Number("tenPower", encoding.TenPower);
                }

                if (encoding.Kind == GesLayoutKind.Quantized || (encoding.Kind == GesLayoutKind.Float && encoding.Range))
                {
                    json.String("lowerValue", Bits(encoding.LowerValue));
                    json.String("upperValue", Bits(encoding.UpperValue));
                }

                json.Unsigned("deltaRange", encoding.DeltaRange);
                json.Number("deltaWidth", encoding.DeltaWidth);
                json.Strings("depends", encoding.Depends);
                break;
            case GesLayoutKind.Tag or GesLayoutKind.Flags:
                json.Strings("tags", encoding.Tags);
                if (encoding.TagSet.Length > 0) json.String("tagSet", encoding.TagSet);
                json.Number("width", encoding.Width);
                break;
            case GesLayoutKind.Text:
                json.Boolean("tagName", encoding.TagName);
                json.Number("maxCount", encoding.MaxCount);
                json.Number("width", encoding.Width);
                break;
            case GesLayoutKind.Bytes:
                json.Number("maxCount", encoding.MaxCount);
                json.Number("width", encoding.Width);
                break;
            case GesLayoutKind.List:
                json.Boolean("dice", encoding.Dice);
                json.Number("maxCount", encoding.MaxCount);
                json.Number("width", encoding.Width);
                json.Boolean("keyed", encoding.Keyed);
                json.Number("positionWidth", encoding.PositionWidth);
                json.Key("element");
                Field(json, encoding.Element!, member: false, value: true);
                break;
            case GesLayoutKind.Point or GesLayoutKind.Vector:
                json.String("unit", encoding.Unit);
                json.BeginArray("axes");
                foreach (var axis in encoding.Axes) Field(json, axis, member: false);
                json.EndArray();
                break;
            case GesLayoutKind.Schema:
                json.String("name", encoding.Schema);
                break;
            case GesLayoutKind.Union:
                json.Number("width", encoding.Width);
                json.BeginArray("alternatives");
                foreach (var alternative in encoding.Alternatives) Field(json, alternative, member: false);
                json.EndArray();
                break;
        }

        json.EndObject();
    }

    private static string Kind(GesLayoutKind kind) => kind switch
    {
        GesLayoutKind.Boolean => "boolean",
        GesLayoutKind.Integer => "integer",
        GesLayoutKind.Quantized => "quantized",
        GesLayoutKind.Float => "float",
        GesLayoutKind.Tag => "tag",
        GesLayoutKind.Flags => "flags",
        GesLayoutKind.Text => "text",
        GesLayoutKind.Bytes => "bytes",
        GesLayoutKind.List => "list",
        GesLayoutKind.Point => "point",
        GesLayoutKind.Vector => "vector",
        GesLayoutKind.Schema => "schema",
        GesLayoutKind.Union => "union",
        _ => "nothing"
    };

    internal static string Hex(ulong value) => value.ToString("x16", CultureInfo.InvariantCulture);

    private static string Bits(double value) => "0x" + Hex((ulong)BitConverter.DoubleToInt64Bits(value));

    // Writes members in call order: every member or element on its own line, a container closing on its own line.
    internal sealed class Writer
    {
        private readonly StringBuilder _text = new();
        private readonly List<bool> _filled = [];

        // An object as the whole document or as an array element.
        internal void BeginObject()
        {
            if (_filled.Count > 0) Item();
            Open('{');
        }

        // An object as the value of the member just started with Key.
        internal void BeginValueObject() => Open('{');

        internal void EndObject() => Close('}');

        internal void BeginArray(string key)
        {
            Key(key);
            Open('[');
        }

        internal void EndArray() => Close(']');

        // A member whose value is the object that follows.
        internal void Key(string key)
        {
            Item();
            Quote(key);
            _text.Append(": ");
        }

        internal void String(string key, string value)
        {
            Key(key);
            Quote(value);
        }

        internal void Number(string key, long value)
        {
            Key(key);
            _text.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        internal void Unsigned(string key, ulong value)
        {
            Key(key);
            _text.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        internal void Null(string key)
        {
            Key(key);
            _text.Append("null");
        }

        internal void Boolean(string key, bool value)
        {
            Key(key);
            _text.Append(value ? "true" : "false");
        }

        internal void Strings(string key, IReadOnlyList<string> values)
        {
            Key(key);
            Open('[');
            foreach (var value in values)
            {
                Item();
                Quote(value);
            }

            Close(']');
        }

        public override string ToString() => _text.ToString() + "\n";

        // Starts a member or element on its own line, after a comma unless it is the first.
        private void Item()
        {
            var last = _filled.Count - 1;
            if (_filled[last]) _text.Append(',');
            _filled[last] = true;
            _text.Append('\n').Append(' ', _filled.Count * 2);
        }

        private void Open(char bracket)
        {
            _text.Append(bracket);
            _filled.Add(false);
        }

        private void Close(char bracket)
        {
            var filled = _filled[_filled.Count - 1];
            _filled.RemoveAt(_filled.Count - 1);
            if (filled) _text.Append('\n').Append(' ', _filled.Count * 2);
            _text.Append(bracket);
        }

        private void Quote(string value)
        {
            _text.Append('"');
            foreach (var character in value)
            {
                switch (character)
                {
                    case '"': _text.Append("\\\""); break;
                    case '\\': _text.Append("\\\\"); break;
                    case < ' ': _text.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture)); break;
                    default: _text.Append(character); break;
                }
            }

            _text.Append('"');
        }
    }
}
