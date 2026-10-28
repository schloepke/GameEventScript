// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using GameEventScript.Runtime.Vm;
using static GameEventScript.Api.GesBindingSegment;
using Code = GameEventScript.Api.GesDocumentErrorCodes;

namespace GameEventScript.Runtime;

internal struct GesLiteralParser
{
    internal const int MaximumInputScalars = 1048576;
    internal const int MaximumDepth = 64;
    internal const int MaximumItems = 65536;

    private readonly string _text;
    // Absent outside a running Program: Program Record constructors are then unknown types.
    private readonly GesVirtualMachine? _state;
    // Document mode adds comments, line-break separators, trailing commas, duplicate-key errors and error positions.
    private readonly bool _document;
    private int _position;
    private int _items;
    private string? _limit;
    private int _limitPosition;
    private string? _error;
    private int _errorPosition;

    private GesLiteralParser(string text, GesVirtualMachine? state, bool document = false)
    {
        _text = text;
        _state = state;
        _document = document;
        _position = 0;
        _items = 0;
        _limit = null;
        _limitPosition = 0;
        _error = null;
        _errorPosition = 0;
    }

    internal static GesValue Parse(in GesValue input, bool strict, GesContext context, GesVirtualMachine state)
    {
        if (input.Kind != GesBytecodeTypeKind.Text) return GesValue.GesNothing();
        if (input.Length > MaximumInputScalars)
        {
            context.RuntimeBudget.Exhaust("MaxLiteralInputScalars", "Literal input exceeds the scalar limit.", MaximumInputScalars);
            return GesValue.GesNothing();
        }
        var parser = new GesLiteralParser(input.TextValue, state);
        var result = parser.Recognize();
        if (parser._limit is { } limit)
        {
            context.RuntimeBudget.Exhaust(limit, "Literal parsing exceeds the resource limit.", LimitValue(limit));
            return GesValue.GesNothing();
        }
        return result ?? (strict ? GesValue.GesNothing() : input);
    }

    /// <summary>Recognizes data literals outside a running Program, for the public GesValue API.</summary>
    /// <returns>The evaluated value, or <see langword="null"/> when the input is not a complete data literal.</returns>
    /// <exception cref="GesLiteralLimitException">A fixed literal parsing bound is exceeded.</exception>
    internal static GesValue? ParseData(in GesValue input)
    {
        if (input.Length > MaximumInputScalars) throw new GesLiteralLimitException("MaxLiteralInputScalars", MaximumInputScalars);
        var parser = new GesLiteralParser(input.TextValue, null);
        var result = parser.Recognize();
        if (parser._limit is { } limit) throw new GesLiteralLimitException(limit, LimitValue(limit));
        return result is { } value ? GesLiteralEvaluation.Evaluate(value) : null;
    }

    /// <summary>Reads GES document text into its evaluated Map or Record root and optional schema reference.</summary>
    /// <exception cref="GesDocumentException">The text is not a valid document or exceeds a literal parsing limit.</exception>
    internal static GesValue ParseDocument(in GesValue input, out GesDocumentSchema? schema)
    {
        var text = input.TextValue;
        if (input.Length > MaximumInputScalars)
            throw DocumentLimit(text, "MaxLiteralInputScalars", MaximumInputScalars, 0);
        var parser = new GesLiteralParser(text, null, document: true);
        var root = parser.ReadDocument(out schema);
        if (parser._limit is { } limit) throw DocumentLimit(text, limit, LimitValue(limit), parser._limitPosition);
        if (root is not { } node)
            throw DocumentError(text, parser._error ?? Code.InvalidLiteral, parser._errorPosition);
        GesValue map;
        try
        {
            map = GesLiteralEvaluation.EvaluateDocument(node, out var invalidOffset)
                ?? throw DocumentError(text, Code.InvalidLiteral, invalidOffset);
        }
        catch (GesLiteralLimitException exception)
        {
            throw DocumentLimit(text, exception.LimitName, exception.Limit, exception.Offset);
        }
        if (schema is not { Kind: GesDocumentSchemaKind.Type } type) return map;
        var fields = map.AsMap()!;
        var keys = new string[fields.Length];
        var values = new GesValue[fields.Length];
        for (var i = 0; i < keys.Length; i++)
        {
            keys[i] = fields.KeyAt(i);
            values[i] = fields.ValueAt(i);
        }
        return GesValue.GesRecord(type.Name, keys, values);
    }

    private static int LimitValue(string limit) => limit == "MaxLiteralDepth" ? MaximumDepth : MaximumItems;

    private static GesDocumentException DocumentError(string text, string code, int offset)
    {
        var (line, column) = Location(text, offset);
        return new GesDocumentException(code, line, column, null, null);
    }

    private static GesDocumentException DocumentLimit(string text, string limit, int value, int offset)
    {
        var (line, column) = Location(text, offset);
        return new GesDocumentException(Code.LimitExceeded, line, column, limit, value);
    }

    // 1-based line and column in Unicode scalars; CRLF is one line break and a leading byte order mark is not counted.
    private static (int Line, int Column) Location(string text, int offset)
    {
        var line = 1;
        var column = 1;
        var index = text.Length > 0 && text[0] == '\uFEFF' ? 1 : 0;
        while (index < offset && index < text.Length)
        {
            var c = text[index++];
            if (c is '\n' or '\r')
            {
                if (c == '\r' && index < text.Length && text[index] == '\n' && index < offset) index++;
                line++;
                column = 1;
            }
            else
            {
                if (char.IsHighSurrogate(c) && index < text.Length && char.IsLowSurrogate(text[index])) index++;
                column++;
            }
        }
        return (line, column);
    }

    private GesValue? Recognize()
    {
        var result = ReadValue(0);
        SkipWhitespace();
        return _limit is null && _position == _text.Length ? result : null;
    }

    private GesValue? ReadDocument(out GesDocumentSchema? schema)
    {
        schema = null;
        if (_text.Length > 0 && _text[0] == '\uFEFF') _position = 1;
        SkipWhitespace();
        if (IsSchemaHeader())
        {
            schema = ReadSchemaHeader();
            if (schema is null) return null;
            SkipSpacesAndComments();
            if (_position < _text.Length && !AtLineBreak) return Fail(Code.UnexpectedToken, _position);
            SkipWhitespace();
        }
        var record = schema is { Kind: GesDocumentSchemaKind.Type };
        var keys = new List<string>();
        var values = new List<GesValue>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (_position < _text.Length)
        {
            var entryStart = _position;
            if (IsSchemaHeader()) return Fail(Code.MisplacedSchemaHeader, entryStart);
            if (_items >= MaximumItems) return Limit("MaxLiteralItems", entryStart);
            _items++;
            var key = ReadKey();
            if (key is null) return Fail(_text[entryStart] is '\'' or '"' ? Code.UnclosedText : Code.InvalidKey, entryStart);
            if (record && !GesText.IsFieldName(key)) return Fail(Code.InvalidKey, entryStart);
            if (!seen.Add(key)) return Fail(Code.DuplicateKey, entryStart);
            SkipSpacesAndComments();
            if (!Consume(':')) return Fail(Code.MissingColon, _position);
            if (ReadEntryValue(0) is not { } value) return null;
            keys.Add(key);
            values.Add(value);
            SkipSpacesAndComments();
            if (_position == _text.Length) break;
            if (!Consume(',') && !AtLineBreak) return Fail(Code.UnexpectedToken, _position);
            SkipWhitespace();
            if (_position < _text.Length && _text[_position] == ',') return Fail(Code.UnexpectedToken, _position);
        }
        return new GesLiteralNode("@map", keys.ToArray(), values.ToArray()).Value;
    }

    private readonly bool IsSchemaHeader()
        => _text.AsSpan(_position).StartsWith(":Schema".AsSpan(), StringComparison.Ordinal)
            && (_position + 7 == _text.Length || !IsNameCharacter(_text[_position + 7]));

    private GesDocumentSchema? ReadSchemaHeader()
    {
        _position += 7;
        SkipWhitespace();
        var open = _position;
        if (!Consume('(')) { Fail(Code.InvalidSchemaHeader, _position); return null; }
        SkipWhitespace();
        var start = _position;
        GesDocumentSchema? schema = null;
        if (_position == _text.Length) { Fail(Code.UnclosedBracket, open); return null; }
        if (Consume(':'))
        {
            var name = ReadName();
            if (name is not null && GesDocumentSchema.IsRecordTypeName(name)) schema = new GesDocumentSchema(GesDocumentSchemaKind.Type, name);
        }
        else if (_text[_position] is '\'' or '"')
        {
            var path = GesTextLiteralReader.Read(_text, ref _position);
            if (path is null) { Fail(Code.UnclosedText, start); return null; }
            if (path.Length > 0) schema = new GesDocumentSchema(GesDocumentSchemaKind.File, path);
        }
        if (schema is null) { Fail(Code.InvalidSchemaHeader, start); return null; }
        switch (Separator(')', open))
        {
            case 1: return schema;
            case 0: Fail(Code.InvalidSchemaHeader, _position); return null;
            default: return null;
        }
    }

    // The value after a Map or root key: key-only entries are true; otherwise the value starts on the key's line.
    private GesValue? ReadEntryValue(int depth)
    {
        if (!_document)
        {
            SkipWhitespace();
            return _position < _text.Length && _text[_position] is ',' or ']' ? GesValue.GesBoolean(true) : ReadValue(depth);
        }
        SkipSpacesAndComments();
        return _position == _text.Length || AtLineBreak || _text[_position] is ',' or ']' ? GesValue.GesBoolean(true) : ReadValue(depth);
    }

    private GesValue? ReadValue(int depth)
    {
        SkipWhitespace();
        if (_position == _text.Length) return Fail(Code.UnexpectedToken, _position);
        var valueStart = _position;
        if (_text[_position] is '\'' or '"')
        {
            var text = GesTextLiteralReader.Read(_text, ref _position);
            return text is null ? Fail(Code.UnclosedText, valueStart) : GesValue.GesText(text);
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
            if (_document && (_error is not null || _limit is not null)) return null;
            _position = saved;
        }
        var dice = _text[_position] == ':' && _text.AsSpan(_position).StartsWith(":Dice".AsSpan(), StringComparison.Ordinal);
        if (dice)
        {
            _position += 5;
            SkipWhitespace();
            if (_position == _text.Length || _text[_position] != '[') return Fail(Code.InvalidLiteral, valueStart);
        }
        if (_text[_position] == '[')
        {
            if (depth >= MaximumDepth) return Limit("MaxLiteralDepth", _position);
            return dice ? ReadDice(valueStart) : ReadCollection(depth + 1);
        }

        var start = _position;
        while (_position < _text.Length && !IsWhitespace(_text[_position]) && _text[_position] is not (',' or ']' or '[' or ')')) _position++;
        if (_document) EndTokenAtComment(start);
        if (_position == start) return Fail(Code.UnexpectedToken, _position);
        var token = _text.AsSpan(start, _position - start);
        if (token.SequenceEqual("true".AsSpan())) return GesValue.GesBoolean(true);
        if (token.SequenceEqual("false".AsSpan())) return GesValue.GesBoolean(false);
        if (token.SequenceEqual("nothing".AsSpan())) return GesValue.GesNothing();
        if (token[0] == '#')
        {
            var name = _text.Substring(start + 1, _position - start - 1);
            return GesTagRules.IsValidTagName(name) ? GesValue.GesTag(name) : Fail(Code.InvalidLiteral, start);
        }
        return GesTextNumberCast.Read(_text, start, _position - start, percentage: token[token.Length - 1] == '%', allowGrouping: false)
            ?? TokenFailure(start, _position);
    }

    private GesValue? ReadTyped(int depth)
    {
        var formStart = _position;
        if (depth >= MaximumDepth) return Limit("MaxLiteralDepth", formStart);
        _position++;
        var name = ReadName();
        if (name is null || !GesText.IsTypeName(name)) return Fail(Code.InvalidLiteral, formStart);
        var builtin = name.ToLowerInvariant();
        // Built-in names are case-sensitive; user Record definitions retain their declared spelling.
        var isBuiltin = GesDataConstruction.IsType(builtin) && name == char.ToUpperInvariant(builtin[0]) + builtin.Substring(1);
        GesBindingSegment.Entry? constructor = null;
        if (!isBuiltin)
        {
            if (_state is null) return Fail(Code.UnknownType, formStart);
            foreach (var binding in _state.Program.Bindings.Entries)
                if (binding.Kind == GesBinaryBindKind.Record && _state.FetchStringByPointer(binding.Name) == name) { constructor = binding; break; }
            if (constructor is null) return Fail(Code.UnknownType, formStart);
        }
        SkipWhitespace();
        var open = _position;
        // `:Dice [...]` with whitespace is the Dice result literal, read after this form fails.
        if (!Consume('(')) return builtin == "dice" ? null : Fail(Code.InvalidLiteral, formStart);
        SkipWhitespace();
        if (builtin is "handler" or "message" && _position < _text.Length && (char.IsUpper(_text[_position])
                || _text.AsSpan(_position).StartsWith("initialization".AsSpan(), StringComparison.Ordinal)
                || _text.AsSpan(_position).StartsWith("undeliverable".AsSpan(), StringComparison.Ordinal)))
            return ReadMessageForm(depth + 1, builtin == "handler", formStart, open);
        var labels = new List<string>();
        var values = new List<GesValue>();
        if (_document && _position == _text.Length) return Fail(Code.UnclosedBracket, open);
        if (!Consume(')'))
        {
            while (_position < _text.Length)
            {
                if (_items++ >= MaximumItems) return Limit("MaxLiteralItems", _position);
                var saved = _position;
                var label = ReadKey();
                SkipInline();
                if (label is null || !Consume(':')) { label = "_"; _position = saved; }
                else if (!GesText.IsFieldName(label)) return Fail(Code.InvalidKey, saved);
                else if (labels.Contains(label)) return Fail(Code.DuplicateKey, saved);
                SkipWhitespace();
                if (_document && _position == _text.Length) return Fail(Code.UnclosedBracket, open);
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
                var separator = Separator(')', open);
                if (separator < 0) return null;
                if (separator > 0) break;
            }
            if (_position == 0 || _text[_position - 1] != ')') return null;
        }
        if (isBuiltin && GesDataConstruction.Positions(builtin, labels) is null) return Fail(Code.InvalidLiteral, formStart);
        if (constructor is { } record)
        {
            var allowed = new List<string>();
            foreach (var parameter in record.ArgumentNames) allowed.Add(_state!.FetchStringByPointer(parameter));
            var count = 0;
            foreach (var label in labels)
            {
                if (label == "_") count++;
                else if (!allowed.Contains(label)) return null;
            }
            if (count > allowed.FindAll(label => label == "_").Count) return null;
        }
        return new GesLiteralNode(builtin, labels.ToArray(), values.ToArray(), constructor, formStart).Value;
    }

    private GesValue? ReadMessageForm(int depth, bool handler, int formStart, int outerOpen)
    {
        var name = ReadName();
        if (name is null) return Fail(Code.InvalidLiteral, formStart);
        try { _ = GesMessageSignature.Create(name, null); }
        catch (ArgumentException) { return Fail(Code.InvalidLiteral, formStart); }
        SkipWhitespace();
        var open = _position;
        if (!Consume('(')) return Fail(Code.InvalidLiteral, formStart);
        SkipWhitespace();
        var labels = new List<string>();
        var values = new List<GesValue>();
        if (_document && _position == _text.Length) return Fail(Code.UnclosedBracket, open);
        if (!Consume(')'))
        {
            while (_position < _text.Length)
            {
                if (_items++ >= MaximumItems) return Limit("MaxLiteralItems", _position);
                if (handler)
                {
                    var labelStart = _position;
                    var label = ReadName();
                    if (label is null) return Fail(Code.InvalidLiteral, formStart);
                    if (label != "_" && !GesText.IsFieldName(label)) return Fail(Code.InvalidKey, labelStart);
                    if (label != "_" && labels.Contains(label)) return Fail(Code.DuplicateKey, labelStart);
                    labels.Add(label);
                }
                else
                {
                    var saved = _position;
                    var label = ReadKey();
                    SkipInline();
                    if (label is null || !Consume(':')) { label = "_"; _position = saved; }
                    else if (!GesText.IsFieldName(label)) return Fail(Code.InvalidKey, saved);
                    else if (labels.Contains(label)) return Fail(Code.DuplicateKey, saved);
                    if (_document)
                    {
                        SkipWhitespace();
                        if (_position == _text.Length) return Fail(Code.UnclosedBracket, open);
                    }
                    var value = ReadValue(depth);
                    if (value is not { } item) return null;
                    labels.Add(label);
                    values.Add(item);
                }
                var separator = Separator(')', open);
                if (separator < 0) return null;
                if (separator > 0) break;
            }
        }
        SkipWhitespace();
        GesValue result = handler ? GesValue.GesHandler(GesMessageSignature.Create(name, labels)) : new GesLiteralNode("@message:" + name, labels.ToArray(), values.ToArray(), null, formStart).Value;
        if (!handler && _text.AsSpan(_position).StartsWith("with".AsSpan(), StringComparison.Ordinal))
        {
            _position += 4;
            var tags = new List<GesValue>();
            if (_document)
            {
                while (true)
                {
                    if (ReadValue(depth) is not { } tag) return null;
                    tags.Add(tag);
                    var separator = Separator(')', outerOpen);
                    if (separator < 0) return null;
                    if (separator > 0) break;
                }
                return Tagged(result, tags, formStart);
            }
            do
            {
                var value = ReadValue(depth);
                if (value is not { } tag) return null;
                tags.Add(tag);
                SkipWhitespace();
            } while (Consume(','));
            result = Tagged(result, tags, formStart);
        }
        if (Consume(')')) return result;
        return Fail(_position == _text.Length ? Code.UnclosedBracket : Code.UnexpectedToken, _position == _text.Length ? outerOpen : _position);
    }

    private static GesValue Tagged(GesValue message, List<GesValue> tags, int formStart)
    {
        var list = new GesValue();
        list.SetList(tags.ToArray());
        return new GesLiteralNode("message", ["_", "tags"], [message, list], null, formStart).Value;
    }

    private string? ReadName()
    {
        // Documents read type and message names directly after `:` or `(`, as the Swift parser does for `parse`.
        if (!_document) SkipWhitespace();
        var start = _position;
        while (_position < _text.Length && IsNameCharacter(_text[_position])) _position++;
        return _position == start ? null : _text.Substring(start, _position - start);
    }

    private static bool IsNameCharacter(char value) => value is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_';

    private GesValue? ReadSpatial(int depth, bool point)
    {
        var formStart = _position;
        _position += point ? 6 : 7;
        SkipWhitespace();
        var open = _position;
        if (!Consume('(')) return Fail(Code.InvalidLiteral, formStart);
        if (depth >= MaximumDepth) return Limit("MaxLiteralDepth", formStart);
        var x = 0d;
        var y = 0d;
        var z = 0d;
        GesQuantityUnit? unit = null;
        bool? labeled = null;
        var previousIndex = -1;
        SkipWhitespace();
        if (_document && _position == _text.Length) return Fail(Code.UnclosedBracket, open);
        if (!Consume(')'))
        {
            while (_position < _text.Length)
            {
                if (_items >= MaximumItems) return Limit("MaxLiteralItems", _position);
                _items++;
                var hasLabel = _text[_position] is 'x' or 'y' or 'z';
                if (labeled.HasValue && labeled != hasLabel) return Fail(Code.InvalidLiteral, formStart);
                labeled = hasLabel;
                var index = previousIndex + 1;
                var labelStart = _position;
                if (hasLabel)
                {
                    index = _text[_position++] - 'x';
                    SkipInline();
                    if (!Consume(':')) return Fail(Code.InvalidLiteral, formStart);
                    SkipWhitespace();
                    if (_document && _position == _text.Length) return Fail(Code.UnclosedBracket, open);
                }
                if (hasLabel && index == previousIndex) return Fail(Code.DuplicateKey, labelStart);
                if (index > 2 || index <= previousIndex) return Fail(Code.InvalidLiteral, formStart);
                previousIndex = index;
                var start = _position;
                while (_position < _text.Length && !IsWhitespace(_text[_position]) && _text[_position] is not (',' or ')')) _position++;
                if (_document) EndTokenAtComment(start);
                if (_position == start) return Fail(Code.UnexpectedToken, _position);
                var value = GesTextNumberCast.Read(_text, start, _position - start, percentage: _text[_position - 1] == '%', allowGrouping: false);
                if (value is not { } component) return TokenFailure(start, _position);
                if (component.Kind is not (GesBytecodeTypeKind.Integer or GesBytecodeTypeKind.Float or GesBytecodeTypeKind.Percentage))
                    return Fail(Code.InvalidLiteral, formStart);
                if (unit.HasValue && unit != component.Unit) return Fail(Code.InvalidLiteral, formStart);
                unit = component.Unit;
                var number = component.AsNumeric;
                if (double.IsNaN(number)) return Fail(Code.InvalidLiteral, formStart);
                switch (index)
                {
                    case 0: x = number; break;
                    case 1: y = number; break;
                    case 2: z = number; break;
                }
                var separator = Separator(')', open);
                if (separator < 0) return null;
                if (separator > 0) return CreateSpatial(point, x, y, z, unit.Value);
            }
            return null;
        }
        return CreateSpatial(point, x, y, z, GesQuantityUnit.UnitNone);
    }

    private static GesValue CreateSpatial(bool point, double x, double y, double z, GesQuantityUnit unit)
        => point ? GesValue.GesPoint(x, y, z, unit) : GesValue.GesVector(x, y, z, unit);

    private GesValue? ReadDice(int formStart)
    {
        var open = _position;
        _position++;
        SkipWhitespace();
        if (Consume(']')) return CreateDice(Array.Empty<int>());
        if (_document && _position == _text.Length) return Fail(Code.UnclosedBracket, open);
        var rolls = new List<int>();
        while (_position < _text.Length)
        {
            if (_items >= MaximumItems) return Limit("MaxLiteralItems", _position);
            _items++;
            if (_text[_position] is < '0' or > '9') return Fail(Code.InvalidLiteral, formStart);
            var value = ReadValue(0);
            if (value is not { } roll) return null;
            if (roll.Kind != GesBytecodeTypeKind.Integer || roll.Unit != GesQuantityUnit.UnitNone || roll.IntegerValue is <= 0 or > int.MaxValue)
                return Fail(Code.InvalidLiteral, formStart);
            rolls.Add((int)roll.IntegerValue);
            var separator = Separator(']', open);
            if (separator < 0) return null;
            if (separator > 0) return CreateDice(rolls.ToArray());
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
        var open = _position;
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
        if (_document && _position == _text.Length) return Fail(Code.UnclosedBracket, open);

        var saved = _position;
        var key = ReadKey();
        SkipInline();
        var isMap = key is not null && Consume(':');
        _position = saved;
        var list = isMap ? null : new List<GesValue>();
        var map = isMap ? new List<KeyValuePair<string, GesValue>>() : null;
        var seen = isMap && _document ? new HashSet<string>(StringComparer.Ordinal) : null;
        while (_position < _text.Length)
        {
            if (_items >= MaximumItems) return Limit("MaxLiteralItems", _position);
            _items++;
            GesValue? value;
            if (isMap)
            {
                SkipWhitespace();
                var keyStart = _position;
                key = ReadKey();
                if (key is null) return Fail(keyStart < _text.Length && _text[keyStart] is '\'' or '"' ? Code.UnclosedText : Code.InvalidKey, keyStart);
                if (seen is not null && !seen.Add(key)) return Fail(Code.DuplicateKey, keyStart);
                SkipInline();
                if (!Consume(':')) return Fail(Code.MissingColon, _position);
                value = ReadEntryValue(depth);
                if (value is not { } entry) return null;
                map!.Add(new(key, entry));
            }
            else
            {
                value = ReadValue(depth);
                if (value is not { } item) return null;
                list!.Add(item);
            }
            var separator = Separator(']', open);
            if (separator < 0) return null;
            if (separator > 0)
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
        }
        return null;
    }

    private string? ReadKey()
    {
        SkipWhitespace();
        if (_position < _text.Length && _text[_position] is '\'' or '"') return GesTextLiteralReader.Read(_text, ref _position);
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

    // Ends one container entry: 1 when the container closes, 0 when another entry follows, -1 on failure.
    // Inlining keeps `parse` collection loops as fast as the former inline separator code (measured on
    // Vector, List and Map literals); the document branch stays out of line.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Separator(char close, int open)
    {
        if (_document) return DocumentSeparator(close, open);
        SkipWhitespace();
        if (Consume(close)) return 1;
        if (!Consume(',')) return -1;
        SkipWhitespace();
        return _position == _text.Length || _text[_position] == close ? -1 : 0;
    }

    // Documents also end entries at line breaks and accept one trailing comma.
    private int DocumentSeparator(char close, int open)
    {
        SkipSpacesAndComments();
        if (Consume(close)) return 1;
        if (_position == _text.Length) { Fail(Code.UnclosedBracket, open); return -1; }
        if (!Consume(',') && !AtLineBreak) { Fail(Code.UnexpectedToken, _position); return -1; }
        SkipWhitespace();
        if (_position == _text.Length) { Fail(Code.UnclosedBracket, open); return -1; }
        if (Consume(close)) return 1;
        if (_text[_position] == ',') { Fail(Code.UnexpectedToken, _position); return -1; }
        return 0;
    }

    // Classifies an unreadable value token for document errors.
    private GesValue? TokenFailure(int start, int end)
    {
        var first = _text[start];
        if (first is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or '_') return Fail(Code.UnquotedText, start);
        return Fail(HasUnknownUnit(start, end) ? Code.UnknownUnit : Code.InvalidLiteral, start);
    }

    // A numeric prefix followed by an ASCII letter, `°` or `/` is a number with an unknown unit suffix.
    private readonly bool HasUnknownUnit(int start, int end)
    {
        var index = start;
        if (index < end && _text[index] is '+' or '-') index++;
        var digits = SkipDigits(ref index, end);
        if (index < end && _text[index] == '.')
        {
            index++;
            digits |= SkipDigits(ref index, end);
        }
        if (!digits) return false;
        if (index < end && _text[index] is 'e' or 'E')
        {
            var exponent = index + 1;
            if (exponent < end && _text[exponent] is '+' or '-') exponent++;
            if (SkipDigits(ref exponent, end)) index = exponent;
        }
        return index < end && _text[index] is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or '°' or '/';
    }

    private readonly bool SkipDigits(ref int index, int end)
    {
        if (index >= end || _text[index] is < '0' or > '9') return false;
        index++;
        while (index < end && _text[index] is >= '0' and <= '9' or '_') index++;
        return true;
    }

    // Records the first document error; outside document mode failures carry no diagnostics.
    private GesValue? Fail(string code, int position)
    {
        if (_document && _error is null && _limit is null)
        {
            _error = code;
            _errorPosition = position;
        }
        return null;
    }

    private GesValue? Limit(string name, int position)
    {
        if (_limit is null)
        {
            _limit = name;
            _limitPosition = position;
        }
        return null;
    }

    private bool Consume(char value)
    {
        if (_position == _text.Length || _text[_position] != value) return false;
        _position++;
        return true;
    }

    // A document comment ends an unquoted token; the scan stays comment-free so `parse` keeps its tight loop.
    private void EndTokenAtComment(int start)
    {
        var comment = _text.IndexOf("//", start, _position - start, StringComparison.Ordinal);
        if (comment >= 0) _position = comment;
    }

    private readonly bool AtComment => _document && _position + 1 < _text.Length && _text[_position] == '/' && _text[_position + 1] == '/';

    private readonly bool AtLineBreak => _position < _text.Length && _text[_position] is '\n' or '\r';

    private void SkipWhitespace()
    {
        if (_document)
        {
            SkipDocumentWhitespace();
            return;
        }
        while (_position < _text.Length && IsWhitespace(_text[_position])) _position++;
    }

    private void SkipDocumentWhitespace()
    {
        SkipSpacesAndComments();
        while (AtLineBreak)
        {
            _position++;
            SkipSpacesAndComments();
        }
    }

    // Trivia between a key or label and its colon or value: documents keep both on one line.
    private void SkipInline()
    {
        if (_document) SkipSpacesAndComments();
        else SkipWhitespace();
    }

    private void SkipSpacesAndComments()
    {
        while (_position < _text.Length)
        {
            if (_text[_position] is ' ' or '\t') _position++;
            else if (AtComment) while (_position < _text.Length && _text[_position] is not ('\n' or '\r')) _position++;
            else return;
        }
    }

    private static bool IsWhitespace(char value) => value is ' ' or '\t' or '\r' or '\n';
}
