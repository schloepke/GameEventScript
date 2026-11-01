// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Api;
using Code = GameEventScript.Syntax.GesDocumentDiagnostic;

namespace GameEventScript.Syntax;

/// <summary>
/// Reads GES document text: a bracket-free root of entries, an optional schema header, comments and line-break
/// separators around the data literals of <c>parse</c>. Reading stops at the first error.
/// </summary>
internal sealed class GesDocumentReader
{
    internal const int MaximumInputScalars = 1048576;
    internal const int MaximumDepth = 64;
    internal const int MaximumItems = 65536;

    private readonly string _text;
    private int _position;
    private int _items;
    private GesDocumentDiagnostic? _error;

    private GesDocumentReader(string text) => _text = text;

    internal static GesDocumentSyntax Read(string text)
    {
        if (GesText.CountScalars(text) > MaximumInputScalars)
            return new GesDocumentSyntax(text, null, GesDocumentSchemaSyntax.None, "", new Code(Code.LimitExceeded, 0, 0, "MaxLiteralInputScalars", MaximumInputScalars));
        var reader = new GesDocumentReader(text);
        var root = reader.ReadDocument(out var schema, out var schemaName);
        return new GesDocumentSyntax(text, reader._error is null ? root : null, schema, schemaName, reader._error ?? (root is null ? reader.Error(Code.InvalidLiteral, reader._position) : null));
    }

    private GesDocumentNode? ReadDocument(out GesDocumentSchemaSyntax schema, out string schemaName)
    {
        schema = GesDocumentSchemaSyntax.None;
        schemaName = "";
        if (_text.Length > 0 && _text[0] == '\uFEFF') _position = 1;
        SkipWhitespace();
        if (IsSchemaHeader())
        {
            if (!ReadSchemaHeader(out schema, out schemaName)) return null;
            SkipSpacesAndComments();
            if (_position < _text.Length && !AtLineBreak) return Fail(Code.UnexpectedToken, _position);
            SkipWhitespace();
        }
        var record = schema == GesDocumentSchemaSyntax.Type;
        var keys = new List<string>();
        var values = new List<GesDocumentNode>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (_position < _text.Length)
        {
            var entryStart = _position;
            if (IsSchemaHeader()) return Fail(Code.MisplacedSchemaHeader, entryStart);
            if (_items >= MaximumItems) return Limit("MaxLiteralItems", MaximumItems, entryStart);
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
        return GesDocumentNode.Map(keys.ToArray(), values.ToArray());
    }

    private bool IsSchemaHeader() => StartsWord(":Schema");

    private bool ReadSchemaHeader(out GesDocumentSchemaSyntax schema, out string name)
    {
        schema = GesDocumentSchemaSyntax.None;
        name = "";
        _position += 7;
        SkipWhitespace();
        var open = _position;
        if (!Consume('(')) return Fail(Code.InvalidSchemaHeader, _position) is not null;
        SkipWhitespace();
        var start = _position;
        if (_position == _text.Length) return Fail(Code.UnclosedBracket, open) is not null;
        if (Consume(':'))
        {
            var type = ReadName();
            if (type is not null && GesLiteralForms.IsRecordTypeName(type))
            {
                schema = GesDocumentSchemaSyntax.Type;
                name = type;
            }
        }
        else if (_text[_position] is '\'' or '"')
        {
            var path = GesTextLiteralReader.Read(_text, ref _position);
            if (path is null) return Fail(Code.UnclosedText, start) is not null;
            if (path.Length > 0)
            {
                schema = GesDocumentSchemaSyntax.File;
                name = path;
            }
        }
        if (schema == GesDocumentSchemaSyntax.None) return Fail(Code.InvalidSchemaHeader, start) is not null;
        switch (Separator(')', open))
        {
            case 1: return true;
            case 0: Fail(Code.InvalidSchemaHeader, _position); return false;
            default: return false;
        }
    }

    // The value after a Map or root key: key-only entries are true; otherwise the value starts on the key's line.
    private GesDocumentNode? ReadEntryValue(int depth)
    {
        SkipSpacesAndComments();
        return _position == _text.Length || AtLineBreak || _text[_position] is ',' or ']' ? GesDocumentNode.BooleanValue(true) : ReadValue(depth);
    }

    private GesDocumentNode? ReadValue(int depth)
    {
        SkipWhitespace();
        if (_position == _text.Length) return Fail(Code.UnexpectedToken, _position);
        var valueStart = _position;
        if (_text[_position] is '\'' or '"')
        {
            var text = GesTextLiteralReader.Read(_text, ref _position);
            return text is null ? Fail(Code.UnclosedText, valueStart) : GesDocumentNode.Text(text);
        }
        if (_text[_position] == ':')
        {
            if (StartsWord(":Vector")) return ReadSpatial(depth, point: false);
            if (StartsWord(":Point")) return ReadSpatial(depth, point: true);
        }
        if (_text[_position] == ':' && !_text.AsSpan(_position).StartsWith(":Dice[".AsSpan(), StringComparison.Ordinal))
        {
            var saved = _position;
            var typed = ReadTyped(depth);
            if (typed is not null || _error is not null) return typed;
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
            if (depth >= MaximumDepth) return Limit("MaxLiteralDepth", MaximumDepth, _position);
            return dice ? ReadDice(valueStart) : ReadCollection(depth + 1);
        }

        var start = _position;
        while (_position < _text.Length && !IsWhitespace(_text[_position]) && _text[_position] is not (',' or ']' or '[' or ')')) _position++;
        EndTokenAtComment(start);
        if (_position == start) return Fail(Code.UnexpectedToken, _position);
        var token = _text.AsSpan(start, _position - start);
        if (token.SequenceEqual("true".AsSpan())) return GesDocumentNode.BooleanValue(true);
        if (token.SequenceEqual("false".AsSpan())) return GesDocumentNode.BooleanValue(false);
        if (token.SequenceEqual("nothing".AsSpan())) return GesDocumentNode.NothingValue();
        if (token[0] == '#')
        {
            var name = _text.Substring(start + 1, _position - start - 1);
            return GesText.IsTagName(name) ? GesDocumentNode.Tag(name) : Fail(Code.InvalidLiteral, start, _position);
        }
        var number = GesNumberLiteral.Read(_text, start, _position - start, percentage: token[token.Length - 1] == '%', allowGrouping: false);
        if (!number.IsValid) return TokenFailure(start, _position);
        // A number token that constructs nothing, such as `NaN` or `Infinity%`, is no value in a document.
        return number.Kind == GesNumberLiteralKind.Nothing ? Fail(Code.InvalidLiteral, start, _position) : GesDocumentNode.NumberValue(number);
    }

    private GesDocumentNode? ReadTyped(int depth)
    {
        var formStart = _position;
        if (depth >= MaximumDepth) return Limit("MaxLiteralDepth", MaximumDepth, formStart);
        _position++;
        var name = ReadName();
        if (name is null || !GesText.IsTypeName(name)) return Fail(Code.InvalidLiteral, formStart);
        // Built-in names are case-sensitive; documents know no Program Record constructors.
        if (!GesLiteralForms.IsBuiltinName(name)) return Fail(Code.UnknownType, formStart);
        var builtin = name.ToLowerInvariant();
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
        var values = new List<GesDocumentNode>();
        if (_position == _text.Length) return Fail(Code.UnclosedBracket, open);
        if (!Consume(')'))
        {
            while (_position < _text.Length)
            {
                if (_items++ >= MaximumItems) return Limit("MaxLiteralItems", MaximumItems, _position);
                var saved = _position;
                var label = ReadKey();
                SkipSpacesAndComments();
                if (label is null || !Consume(':')) { label = "_"; _position = saved; }
                else if (!GesText.IsFieldName(label)) return Fail(Code.InvalidKey, saved);
                else if (labels.Contains(label)) return Fail(Code.DuplicateKey, saved);
                SkipWhitespace();
                if (_position == _text.Length) return Fail(Code.UnclosedBracket, open);
                GesDocumentNode? value;
                if (builtin == "series" && values.Count == 0 && _text[_position] is >= 'a' and <= 'z')
                {
                    var valueStart = _position;
                    var kind = ReadName();
                    if (kind is "fibonacci" or "factorial") value = GesDocumentNode.Text(kind);
                    else { _position = valueStart; value = ReadValue(depth + 1); }
                }
                else value = ReadValue(depth + 1);
                if (value is null) return null;
                labels.Add(label);
                values.Add(value);
                var separator = Separator(')', open);
                if (separator < 0) return null;
                if (separator > 0) break;
            }
            if (_position == 0 || _text[_position - 1] != ')') return null;
        }
        if (GesLiteralForms.Positions(builtin, labels) is null) return Fail(Code.InvalidLiteral, formStart);
        return GesDocumentNode.Form(builtin, labels.ToArray(), values.ToArray(), formStart);
    }

    private GesDocumentNode? ReadMessageForm(int depth, bool handler, int formStart, int outerOpen)
    {
        var name = ReadName();
        if (name is null || !GesText.IsMessageName(name) && !GesText.IsSystemEndpointName(name)) return Fail(Code.InvalidLiteral, formStart);
        SkipWhitespace();
        var open = _position;
        if (!Consume('(')) return Fail(Code.InvalidLiteral, formStart);
        SkipWhitespace();
        var labels = new List<string>();
        var values = new List<GesDocumentNode>();
        if (_position == _text.Length) return Fail(Code.UnclosedBracket, open);
        if (!Consume(')'))
        {
            while (_position < _text.Length)
            {
                if (_items++ >= MaximumItems) return Limit("MaxLiteralItems", MaximumItems, _position);
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
                    SkipSpacesAndComments();
                    if (label is null || !Consume(':')) { label = "_"; _position = saved; }
                    else if (!GesText.IsFieldName(label)) return Fail(Code.InvalidKey, saved);
                    else if (labels.Contains(label)) return Fail(Code.DuplicateKey, saved);
                    SkipWhitespace();
                    if (_position == _text.Length) return Fail(Code.UnclosedBracket, open);
                    if (ReadValue(depth) is not { } value) return null;
                    labels.Add(label);
                    values.Add(value);
                }
                var separator = Separator(')', open);
                if (separator < 0) return null;
                if (separator > 0) break;
            }
        }
        SkipWhitespace();
        var result = handler ? GesDocumentNode.Handler(name, labels.ToArray()) : GesDocumentNode.Message(name, labels.ToArray(), values.ToArray(), formStart);
        if (!handler && StartsWord("with"))
        {
            _position += 4;
            var tags = new List<GesDocumentNode>();
            while (true)
            {
                if (ReadValue(depth) is not { } tag) return null;
                tags.Add(tag);
                var separator = Separator(')', outerOpen);
                if (separator < 0) return null;
                if (separator > 0) break;
            }
            return GesDocumentNode.Form("message", ["_", "tags"], [result, GesDocumentNode.List(tags.ToArray())], formStart);
        }
        if (Consume(')')) return result;
        return _position == _text.Length ? Fail(Code.UnclosedBracket, outerOpen) : Fail(Code.UnexpectedToken, _position);
    }

    // Callers skip any permitted whitespace first; a type name follows its `:` directly, like the source type token.
    private string? ReadName()
    {
        var start = _position;
        while (_position < _text.Length && IsNameCharacter(_text[_position])) _position++;
        return _position == start ? null : _text.Substring(start, _position - start);
    }

    // Whether the text continues with `word` and no further name character.
    private bool StartsWord(string word)
        => _text.AsSpan(_position).StartsWith(word.AsSpan(), StringComparison.Ordinal)
            && (_position + word.Length == _text.Length || !IsNameCharacter(_text[_position + word.Length]));

    private static bool IsNameCharacter(char value) => value is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_';

    private GesDocumentNode? ReadSpatial(int depth, bool point)
    {
        var formStart = _position;
        _position += point ? 6 : 7;
        SkipWhitespace();
        var open = _position;
        if (!Consume('(')) return Fail(Code.InvalidLiteral, formStart);
        if (depth >= MaximumDepth) return Limit("MaxLiteralDepth", MaximumDepth, formStart);
        var x = 0d;
        var y = 0d;
        var z = 0d;
        GesQuantityUnit? unit = null;
        bool? labeled = null;
        var previousIndex = -1;
        SkipWhitespace();
        if (_position == _text.Length) return Fail(Code.UnclosedBracket, open);
        if (Consume(')')) return GesDocumentNode.Spatial(point, x, y, z, GesQuantityUnit.UnitNone);
        while (_position < _text.Length)
        {
            if (_items >= MaximumItems) return Limit("MaxLiteralItems", MaximumItems, _position);
            _items++;
            var hasLabel = _text[_position] is 'x' or 'y' or 'z';
            if (labeled.HasValue && labeled != hasLabel) return Fail(Code.InvalidLiteral, formStart);
            labeled = hasLabel;
            var index = previousIndex + 1;
            var labelStart = _position;
            if (hasLabel)
            {
                index = _text[_position++] - 'x';
                SkipSpacesAndComments();
                if (!Consume(':')) return Fail(Code.InvalidLiteral, formStart);
                SkipWhitespace();
                if (_position == _text.Length) return Fail(Code.UnclosedBracket, open);
            }
            if (hasLabel && index == previousIndex) return Fail(Code.DuplicateKey, labelStart);
            if (index > 2 || index <= previousIndex) return Fail(Code.InvalidLiteral, formStart);
            previousIndex = index;
            var start = _position;
            while (_position < _text.Length && !IsWhitespace(_text[_position]) && _text[_position] is not (',' or ')')) _position++;
            EndTokenAtComment(start);
            if (_position == start) return Fail(Code.UnexpectedToken, _position);
            var component = GesNumberLiteral.Read(_text, start, _position - start, percentage: _text[_position - 1] == '%', allowGrouping: false);
            if (!component.IsValid) return TokenFailure(start, _position);
            if (component.Kind is not (GesNumberLiteralKind.Integer or GesNumberLiteralKind.Float or GesNumberLiteralKind.Percentage))
                return Fail(Code.InvalidLiteral, formStart);
            if (unit.HasValue && unit != component.Unit) return Fail(Code.InvalidLiteral, formStart);
            unit = component.Unit;
            var number = component.Kind == GesNumberLiteralKind.Integer ? component.IntegerValue : component.FloatValue;
            if (double.IsNaN(number)) return Fail(Code.InvalidLiteral, formStart);
            switch (index)
            {
                case 0: x = number; break;
                case 1: y = number; break;
                case 2: z = number; break;
            }
            var separator = Separator(')', open);
            if (separator < 0) return null;
            if (separator > 0) return GesDocumentNode.Spatial(point, x, y, z, unit.Value);
        }
        return null;
    }

    private GesDocumentNode? ReadDice(int formStart)
    {
        var open = _position;
        _position++;
        SkipWhitespace();
        if (Consume(']')) return GesDocumentNode.Dice([]);
        if (_position == _text.Length) return Fail(Code.UnclosedBracket, open);
        var rolls = new List<int>();
        while (_position < _text.Length)
        {
            if (_items >= MaximumItems) return Limit("MaxLiteralItems", MaximumItems, _position);
            _items++;
            if (_text[_position] is < '0' or > '9') return Fail(Code.InvalidLiteral, formStart);
            if (ReadValue(0) is not { } roll) return null;
            if (roll.Kind != GesDocumentNodeKind.Number || roll.Number.Kind != GesNumberLiteralKind.Integer || roll.Number.Unit != GesQuantityUnit.UnitNone
                || roll.Number.IntegerValue is <= 0 or > int.MaxValue)
                return Fail(Code.InvalidLiteral, formStart);
            rolls.Add((int)roll.Number.IntegerValue);
            var separator = Separator(']', open);
            if (separator < 0) return null;
            if (separator > 0) return GesDocumentNode.Dice(rolls.ToArray());
        }
        return null;
    }

    private GesDocumentNode? ReadCollection(int depth)
    {
        var open = _position;
        _position++;
        SkipWhitespace();
        if (Consume(']')) return GesDocumentNode.List([]);
        var contentStart = _position;
        if (Consume(':'))
        {
            SkipWhitespace();
            if (Consume(']')) return GesDocumentNode.Map([], []);
            // A leading typed literal, such as [:Dice[]], is a List item.
            _position = contentStart;
        }
        if (_position == _text.Length) return Fail(Code.UnclosedBracket, open);

        var saved = _position;
        var key = ReadKey();
        SkipSpacesAndComments();
        var isMap = key is not null && Consume(':');
        _position = saved;
        var keys = isMap ? new List<string>() : null;
        var seen = isMap ? new HashSet<string>(StringComparer.Ordinal) : null;
        var values = new List<GesDocumentNode>();
        while (_position < _text.Length)
        {
            if (_items >= MaximumItems) return Limit("MaxLiteralItems", MaximumItems, _position);
            _items++;
            if (isMap)
            {
                SkipWhitespace();
                var keyStart = _position;
                key = ReadKey();
                if (key is null) return Fail(keyStart < _text.Length && _text[keyStart] is '\'' or '"' ? Code.UnclosedText : Code.InvalidKey, keyStart);
                if (!seen!.Add(key)) return Fail(Code.DuplicateKey, keyStart);
                SkipSpacesAndComments();
                if (!Consume(':')) return Fail(Code.MissingColon, _position);
                if (ReadEntryValue(depth) is not { } entry) return null;
                keys!.Add(key);
                values.Add(entry);
            }
            else
            {
                if (ReadValue(depth) is not { } item) return null;
                values.Add(item);
            }
            var separator = Separator(']', open);
            if (separator < 0) return null;
            if (separator > 0) return isMap ? GesDocumentNode.Map(keys!.ToArray(), values.ToArray()) : GesDocumentNode.List(values.ToArray());
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

    // Ends one container entry: 1 when the container closes, 0 when another entry follows, -1 on failure. Line
    // breaks also end entries, and one trailing comma is accepted.
    private int Separator(char close, int open)
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

    // Classifies an unreadable value token.
    private GesDocumentNode? TokenFailure(int start, int end)
    {
        var first = _text[start];
        if (first is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or '_') return Fail(Code.UnquotedText, start, end);
        return Fail(HasUnknownUnit(start, end) ? Code.UnknownUnit : Code.InvalidLiteral, start, end);
    }

    // A numeric prefix followed by an ASCII letter, `°` or `/` is a number with an unknown unit suffix.
    private bool HasUnknownUnit(int start, int end)
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

    private bool SkipDigits(ref int index, int end)
    {
        if (index >= end || _text[index] is < '0' or > '9') return false;
        index++;
        while (index < end && _text[index] is >= '0' and <= '9' or '_') index++;
        return true;
    }

    // Records the first error. Without an explicit end, the range covers the `:Name` of a form, or the scalar at the
    // position, or nothing at a line break and at the end.
    private GesDocumentNode? Fail(string code, int position, int end = -1)
    {
        _error ??= Error(code, position, end);
        return null;
    }

    private GesDocumentDiagnostic Error(string code, int position, int end = -1)
    {
        if (end < 0)
        {
            end = position;
            if (position < _text.Length && _text[position] == ':' && position + 1 < _text.Length && GesText.IsAsciiLetter(_text[position + 1]))
            {
                end = position + 1;
                while (end < _text.Length && IsNameCharacter(_text[end])) end++;
            }
            else if (position < _text.Length && _text[position] is not ('\n' or '\r'))
                end = position + GesText.ScalarUtf16LengthAt(_text, position);
        }
        return new GesDocumentDiagnostic(code, position, end);
    }

    private GesDocumentNode? Limit(string name, int value, int position)
    {
        _error ??= new GesDocumentDiagnostic(Code.LimitExceeded, position, position, name, value);
        return null;
    }

    private bool Consume(char value)
    {
        if (_position == _text.Length || _text[_position] != value) return false;
        _position++;
        return true;
    }

    // A comment ends an unquoted token.
    private void EndTokenAtComment(int start)
    {
        var comment = _text.IndexOf("//", start, _position - start, StringComparison.Ordinal);
        if (comment >= 0) _position = comment;
    }

    private bool AtComment => _position + 1 < _text.Length && _text[_position] == '/' && _text[_position + 1] == '/';

    private bool AtLineBreak => _position < _text.Length && _text[_position] is '\n' or '\r';

    private void SkipWhitespace()
    {
        SkipSpacesAndComments();
        while (AtLineBreak)
        {
            _position++;
            SkipSpacesAndComments();
        }
    }

    // Trivia between a key or label and its colon or value: documents keep both on one line.
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
