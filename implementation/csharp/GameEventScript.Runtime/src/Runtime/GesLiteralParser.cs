// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using GameEventScript.Runtime.VM;
using static GameEventScript.Api.GameEventScriptBindingSegment;

namespace GameEventScript.Runtime;

internal struct GesLiteralParser
{
    internal const int MaximumInputScalars = 1048576;
    internal const int MaximumDepth = 64;
    internal const int MaximumItems = 65536;

    private readonly string _text;
    private readonly GesVmState _state;
    private int _position;
    private int _items;
    private string? _limit;

    private GesLiteralParser(string text, GesVmState state)
    {
        _text = text;
        _state = state;
        _position = 0;
        _items = 0;
        _limit = null;
    }

    internal static GesValue Parse(in GesValue input, GameEventScriptContext context, GesVmState state)
    {
        if (input.Kind != GameEventScriptBytecodeTypeKind.Text) return GesValue.GesNothing();
        if (input.Length > MaximumInputScalars)
        {
            context.RuntimeBudget.Exhaust("MaxLiteralInputScalars", "Literal input exceeds the scalar limit.", MaximumInputScalars);
            return GesValue.GesNothing();
        }
        var parser = new GesLiteralParser(input.TextValue, state);
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
        if (_text[_position] == ':')
        {
            if (_text.AsSpan(_position).StartsWith(":Vector".AsSpan(), StringComparison.Ordinal)) return ReadSpatial(depth, point: false);
            if (_text.AsSpan(_position).StartsWith(":Point".AsSpan(), StringComparison.Ordinal)) return ReadSpatial(depth, point: true);
        }
        if (_text[_position] == ':' && !_text.AsSpan(_position).StartsWith(":Dice[".AsSpan(), StringComparison.Ordinal))
        {
            var saved = _position;
            var typed = ReadTyped(depth);
            if (typed is not null) return typed;
            _position = saved;
        }
        var dice = _text[_position] == ':' && _text.AsSpan(_position).StartsWith(":Dice".AsSpan(), StringComparison.Ordinal);
        if (dice)
        {
            _position += 5;
            SkipWhitespace();
            if (_position == _text.Length || _text[_position] != '[') return null;
        }
        if (_text[_position] == '[')
        {
            if (depth >= MaximumDepth)
            {
                _limit = "MaxLiteralDepth";
                return null;
            }
            return dice ? ReadDice() : ReadCollection(depth + 1);
        }

        var start = _position;
        while (_position < _text.Length && !IsWhitespace(_text[_position]) && _text[_position] is not (',' or ']' or '[' or ')')) _position++;
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

    private GesValue? ReadTyped(int depth)
    {
        if (depth >= MaximumDepth) { _limit = "MaxLiteralDepth"; return null; }
        _position++;
        var name = ReadName();
        if (name is null || !GameEventScriptText.IsTypeName(name)) return null;
        var builtin = name.ToLowerInvariant();
        // Built-in names are case-sensitive; user Record definitions retain their declared spelling.
        var isBuiltin = GesDataConstruction.IsType(builtin) && name == char.ToUpperInvariant(builtin[0]) + builtin.Substring(1);
        GameEventScriptBinaryBindEntry? constructor = null;
        if (!isBuiltin)
        {
            foreach (var binding in _state.Program.Bindings.Entries)
                if (binding.Kind == GameEventScriptBinaryBindKind.Record && _state.FetchStringByPointer(binding.Name) == name) { constructor = binding; break; }
            if (constructor is null) return null;
        }
        SkipWhitespace();
        if (!Consume('(')) return null;
        SkipWhitespace();
        if (builtin is "handler" or "message" && _position < _text.Length && (char.IsUpper(_text[_position])
                || _text.AsSpan(_position).StartsWith("initialization".AsSpan(), StringComparison.Ordinal)
                || _text.AsSpan(_position).StartsWith("undeliverable".AsSpan(), StringComparison.Ordinal)))
            return ReadMessageForm(depth + 1, builtin == "handler");
        var labels = new List<string>();
        var values = new List<GesValue>();
        if (!Consume(')'))
        {
            while (_position < _text.Length)
            {
                if (_items++ >= MaximumItems) { _limit = "MaxLiteralItems"; return null; }
                var saved = _position;
                var label = ReadKey();
                SkipWhitespace();
                if (label is null || !Consume(':')) { label = "_"; _position = saved; }
                else if (!GameEventScriptText.IsFieldName(label) || labels.Contains(label)) return null;
                SkipWhitespace();
                GesValue? value;
                if (builtin == "series" && values.Count == 0 && _position < _text.Length && _text[_position] is >= 'a' and <= 'z')
                {
                    var valueStart = _position;
                    var kind = ReadName();
                    if (kind is "fibonacci" or "factorial") value = GesValue.GesText(kind);
                    else { _position = valueStart; value = ReadValue(depth + 1); }
                }
                else value = ReadValue(depth + 1);
                if (value is not { } item) return null;
                labels.Add(label);
                values.Add(item);
                SkipWhitespace();
                if (Consume(')')) break;
                if (!Consume(',')) return null;
                SkipWhitespace();
                if (_position == _text.Length || _text[_position] == ')') return null;
            }
            if (_position == 0 || _text[_position - 1] != ')') return null;
        }
        if (isBuiltin && GesDataConstruction.Positions(builtin, labels) is null) return null;
        if (constructor is { } record)
        {
            var allowed = new List<string>();
            foreach (var parameter in record.ArgumentNames) allowed.Add(_state.FetchStringByPointer(parameter));
            var count = 0;
            foreach (var label in labels)
            {
                if (label == "_") count++;
                else if (!allowed.Contains(label)) return null;
            }
            if (count > allowed.FindAll(label => label == "_").Count) return null;
        }
        return new GesLiteralNode(builtin, labels.ToArray(), values.ToArray(), constructor).Value;
    }

    private GesValue? ReadMessageForm(int depth, bool handler)
    {
        var name = ReadName();
        if (name is null) return null;
        try { _ = GameEventScriptMessageSignature.Create(name, null); }
        catch (ArgumentException) { return null; }
        SkipWhitespace();
        if (!Consume('(')) return null;
        SkipWhitespace();
        var labels = new List<string>();
        var values = new List<GesValue>();
        if (!Consume(')'))
        {
            while (_position < _text.Length)
            {
                if (_items++ >= MaximumItems) { _limit = "MaxLiteralItems"; return null; }
                if (handler)
                {
                    var label = ReadName();
                    if (label is null || label != "_" && (!GameEventScriptText.IsFieldName(label) || labels.Contains(label))) return null;
                    labels.Add(label);
                }
                else
                {
                    var saved = _position;
                    var label = ReadKey();
                    SkipWhitespace();
                    if (label is null || !Consume(':')) { label = "_"; _position = saved; }
                    else if (!GameEventScriptText.IsFieldName(label) || labels.Contains(label)) return null;
                    var value = ReadValue(depth);
                    if (value is not { } item) return null;
                    labels.Add(label);
                    values.Add(item);
                }
                SkipWhitespace();
                if (Consume(')')) break;
                if (!Consume(',')) return null;
                SkipWhitespace();
                if (_position == _text.Length || _text[_position] == ')') return null;
            }
        }
        SkipWhitespace();
        GesValue result = handler ? GesValue.GesHandler(GameEventScriptMessageSignature.Create(name, labels)) : new GesLiteralNode("@message:" + name, labels.ToArray(), values.ToArray()).Value;
        if (!handler && _text.AsSpan(_position).StartsWith("with".AsSpan(), StringComparison.Ordinal))
        {
            _position += 4;
            var tags = new List<GesValue>();
            do
            {
                var value = ReadValue(depth);
                if (value is not { } tag) return null;
                tags.Add(tag);
                SkipWhitespace();
            } while (Consume(','));
            var list = new GesValue();
            list.SetList(tags.ToArray());
            result = new GesLiteralNode("message", ["_", "tags"], [result, list]).Value;
        }
        return Consume(')') ? result : null;
    }

    private string? ReadName()
    {
        SkipWhitespace();
        var start = _position;
        while (_position < _text.Length && _text[_position] is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_') _position++;
        return _position == start ? null : _text.Substring(start, _position - start);
    }

    private GesValue? ReadSpatial(int depth, bool point)
    {
        _position += point ? 6 : 7;
        SkipWhitespace();
        if (!Consume('(')) return null;
        if (depth >= MaximumDepth)
        {
            _limit = "MaxLiteralDepth";
            return null;
        }
        var x = 0d;
        var y = 0d;
        var z = 0d;
        GameEventScriptBytecodeInstructionUnit? unit = null;
        bool? labeled = null;
        var previousIndex = -1;
        SkipWhitespace();
        if (!Consume(')'))
        {
            while (_position < _text.Length)
            {
                if (_items >= MaximumItems)
                {
                    _limit = "MaxLiteralItems";
                    return null;
                }
                _items++;
                var hasLabel = _text[_position] is 'x' or 'y' or 'z';
                if (labeled.HasValue && labeled != hasLabel) return null;
                labeled = hasLabel;
                var index = previousIndex + 1;
                if (hasLabel)
                {
                    index = _text[_position++] - 'x';
                    SkipWhitespace();
                    if (!Consume(':')) return null;
                    SkipWhitespace();
                }
                if (index > 2 || index <= previousIndex) return null;
                previousIndex = index;
                var start = _position;
                while (_position < _text.Length && !IsWhitespace(_text[_position]) && _text[_position] is not (',' or ')')) _position++;
                if (_position == start) return null;
                var value = TextNumberCast.Read(_text, start, _position - start, percentage: _text[_position - 1] == '%', allowGrouping: false);
                if (value is not { } component || component.Kind is not (GameEventScriptBytecodeTypeKind.Integer or GameEventScriptBytecodeTypeKind.Float or GameEventScriptBytecodeTypeKind.Percentage))
                    return null;
                if (unit.HasValue && unit != component.Unit) return null;
                unit = component.Unit;
                var number = component.AsNumeric;
                if (double.IsNaN(number)) return null;
                switch (index)
                {
                    case 0: x = number; break;
                    case 1: y = number; break;
                    case 2: z = number; break;
                }
                SkipWhitespace();
                if (Consume(')')) return CreateSpatial(point, x, y, z, unit.Value);
                if (!Consume(',')) return null;
                SkipWhitespace();
                if (_position == _text.Length || _text[_position] == ')') return null;
            }
            return null;
        }
        return CreateSpatial(point, x, y, z, GameEventScriptBytecodeInstructionUnit.UnitNone);
    }

    private static GesValue CreateSpatial(bool point, double x, double y, double z, GameEventScriptBytecodeInstructionUnit unit)
        => point ? GesValue.GesPoint(x, y, z, unit) : GesValue.GesVector(x, y, z, unit);

    private GesValue? ReadDice()
    {
        _position++;
        SkipWhitespace();
        if (Consume(']')) return CreateDice(Array.Empty<int>());
        var rolls = new List<int>();
        while (_position < _text.Length)
        {
            if (_items >= MaximumItems)
            {
                _limit = "MaxLiteralItems";
                return null;
            }
            _items++;
            if (_text[_position] is < '0' or > '9') return null;
            var value = ReadValue(0);
            if (value is not { } roll || roll.Kind != GameEventScriptBytecodeTypeKind.Integer || roll.Unit != GameEventScriptBytecodeInstructionUnit.UnitNone || roll.IntegerValue is <= 0 or > int.MaxValue)
                return null;
            rolls.Add((int)roll.IntegerValue);
            SkipWhitespace();
            if (Consume(']')) return CreateDice(rolls.ToArray());
            if (!Consume(',')) return null;
            SkipWhitespace();
            if (_position == _text.Length || _text[_position] == ']') return null;
        }
        return null;
    }

    private static GesValue CreateDice(int[] rolls)
    {
        var result = new GesValue();
        result.SetDice(rolls);
        return result;
    }

    private GesValue? ReadCollection(int depth)
    {
        _position++;
        SkipWhitespace();
        if (Consume(']')) return EmptyList();
        var contentStart = _position;
        if (Consume(':'))
        {
            SkipWhitespace();
            if (Consume(']')) return CreateMap(new Dictionary<string, GesValue>(StringComparer.Ordinal));
            // A leading typed literal, such as [:Dice[]], is a List item.
            _position = contentStart;
        }

        var saved = _position;
        var key = ReadKey();
        SkipWhitespace();
        var isMap = key is not null && Consume(':');
        _position = saved;
        var list = isMap ? null : new List<GesValue>();
        var map = isMap ? new List<KeyValuePair<string, GesValue>>() : null;
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
                map!.Add(new(key, entry));
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
                if (isMap)
                {
                    var keys = new string[map!.Count]; var values = new GesValue[map.Count];
                    for (var i = 0; i < keys.Length; i++) { keys[i] = map[i].Key; values[i] = map[i].Value; }
                    return new GesLiteralNode("@map", keys, values).Value;
                }
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
