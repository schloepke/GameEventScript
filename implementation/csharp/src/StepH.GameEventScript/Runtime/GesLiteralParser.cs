// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime.Values;

namespace StepH.GameEventScript.Runtime;

internal struct GesLiteralParser
{
    internal const int MaximumInputScalars = 1048576;
    internal const int MaximumDepth = 64;
    internal const int MaximumItems = 65536;

    private readonly string _text;
    private int _position;
    private int _items;
    private string? _limit;

    private GesLiteralParser(string text)
    {
        _text = text;
        _position = 0;
        _items = 0;
        _limit = null;
    }

    internal static GesValue Parse(in GesValue input, GameEventScriptContext context)
    {
        if (input.Kind != GameEventScriptBytecodeTypeKind.Text) return GesValue.GesNothing();
        if (input.Length > MaximumInputScalars)
        {
            context.RuntimeBudget.Exhaust("MaxLiteralInputScalars", "Literal input exceeds the scalar limit.", MaximumInputScalars);
            return GesValue.GesNothing();
        }
        var parser = new GesLiteralParser(input.TextValue);
        var result = parser.ReadValue(0);
        parser.SkipWhitespace();
        if (parser._limit is { } limit)
        {
            context.RuntimeBudget.Exhaust(limit, "Literal parsing exceeds the resource limit.", limit == "MaxLiteralDepth" ? MaximumDepth : MaximumItems);
            return GesValue.GesNothing();
        }
        return result is { } value && parser._position == parser._text.Length ? value : input;
    }

    private GesValue? ReadValue(int depth)
    {
        SkipWhitespace();
        if (_position == _text.Length) return null;
        if (_text[_position] is '\'' or '"')
        {
            var text = TextLiteralReader.Read(_text, ref _position);
            return text is null ? null : GesValue.GesText(text);
        }
        if (_text[_position] == '[')
        {
            if (depth >= MaximumDepth)
            {
                _limit = "MaxLiteralDepth";
                return null;
            }
            return ReadCollection(depth + 1);
        }

        var start = _position;
        while (_position < _text.Length && !IsWhitespace(_text[_position]) && _text[_position] is not (',' or ']' or '[')) _position++;
        if (_position == start) return null;
        var token = _text.AsSpan(start, _position - start);
        if (token.SequenceEqual("true".AsSpan())) return GesValue.GesBoolean(true);
        if (token.SequenceEqual("false".AsSpan())) return GesValue.GesBoolean(false);
        if (token.SequenceEqual("nothing".AsSpan())) return GesValue.GesNothing();
        if (token[0] == '#')
        {
            var name = _text.Substring(start + 1, _position - start - 1);
            return GameEventScriptTagRules.IsValidTagName(name) ? GesValue.GesTag(name) : null;
        }
        return TextNumberCast.Read(_text, start, _position - start, percentage: token[token.Length - 1] == '%', allowGrouping: false);
    }

    private GesValue? ReadCollection(int depth)
    {
        _position++;
        SkipWhitespace();
        if (Consume(']')) return EmptyList();
        if (Consume(':'))
        {
            SkipWhitespace();
            return Consume(']') ? CreateMap(new Dictionary<string, GesValue>(StringComparer.Ordinal)) : null;
        }

        var saved = _position;
        var key = ReadKey();
        SkipWhitespace();
        var isMap = key is not null && Consume(':');
        _position = saved;
        var list = isMap ? null : new List<GesValue>();
        var map = isMap ? new Dictionary<string, GesValue>(StringComparer.Ordinal) : null;
        while (_position < _text.Length)
        {
            if (_items >= MaximumItems)
            {
                _limit = "MaxLiteralItems";
                return null;
            }
            _items++;
            GesValue? value;
            if (isMap)
            {
                key = ReadKey();
                SkipWhitespace();
                if (key is null || !Consume(':')) return null;
                SkipWhitespace();
                value = _position < _text.Length && _text[_position] is ',' or ']'
                    ? GesValue.GesBoolean(true)
                    : ReadValue(depth);
                if (value is not { } entry) return null;
                map![key] = entry;
            }
            else
            {
                value = ReadValue(depth);
                if (value is not { } item) return null;
                list!.Add(item);
            }
            SkipWhitespace();
            if (Consume(']'))
            {
                if (isMap) return CreateMap(map!);
                var result = new GesValue();
                result.SetList(list!.ToArray());
                return result;
            }
            if (!Consume(',')) return null;
            SkipWhitespace();
            if (_position == _text.Length || _text[_position] == ']') return null;
        }
        return null;
    }

    private string? ReadKey()
    {
        SkipWhitespace();
        if (_position < _text.Length && _text[_position] is '\'' or '"') return TextLiteralReader.Read(_text, ref _position);
        var start = _position;
        if (_position == _text.Length || _text[_position] is < 'a' or > 'z') return null;
        _position++;
        while (_position < _text.Length && _text[_position] is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9') _position++;
        return _text.Substring(start, _position - start);
    }

    private static GesValue EmptyList()
    {
        var value = new GesValue();
        value.SetList(Array.Empty<GesValue>());
        return value;
    }

    private static GesValue CreateMap(Dictionary<string, GesValue> entries)
    {
        var keys = new string[entries.Count];
        var values = new GesValue[entries.Count];
        var index = 0;
        foreach (var entry in entries)
        {
            keys[index] = entry.Key;
            values[index++] = entry.Value;
        }
        var value = new GesValue();
        value.SetMap(new GesValueMap(keys, values, keys.Length));
        return value;
    }

    private bool Consume(char value)
    {
        if (_position == _text.Length || _text[_position] != value) return false;
        _position++;
        return true;
    }

    private void SkipWhitespace()
    {
        while (_position < _text.Length && IsWhitespace(_text[_position])) _position++;
    }

    private static bool IsWhitespace(char value) => value is ' ' or '\t' or '\r' or '\n';
}
