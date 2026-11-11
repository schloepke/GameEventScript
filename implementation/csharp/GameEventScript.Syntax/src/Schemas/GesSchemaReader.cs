// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Api;
using Code = GameEventScript.Syntax.GesSchemaDiagnostic;

namespace GameEventScript.Syntax;

/// <summary>
/// Reads schema source text (<c>.gess</c>) into its declarations. Reading stops at the first syntax error; the rules
/// across declarations belong to the Runtime.
/// </summary>
internal sealed class GesSchemaSourceReader
{
    internal const int MaximumInputScalars = 1048576;
    private const int MaximumTypeNesting = 32;

    private sealed class SyntaxFailure(int offset, string message, string code = Code.Syntax) : Exception(message)
    {
        internal int Offset { get; } = offset;

        internal string Code { get; } = code;
    }

    private readonly string _text;
    private int _position;
    private int _typeNesting;

    private GesSchemaSourceReader(string text) => _text = text;

    internal static GesSchemaSourceSyntax Read(string text)
    {
        if (GesText.CountScalars(text) > MaximumInputScalars)
            return new GesSchemaSourceSyntax(text, [], new Code(Code.TooLarge, text, 0, "The schema source is too long."));
        var reader = new GesSchemaSourceReader(text);
        var declarations = new List<GesSchemaDeclarationSyntax>();
        try
        {
            if (text.Length > 0 && text[0] == '﻿') reader._position = 1;
            reader.SkipTrivia();
            while (reader._position < text.Length)
            {
                declarations.Add(reader.ReadDeclaration());
                reader.SkipTrivia();
            }
        }
        catch (SyntaxFailure failure)
        {
            return new GesSchemaSourceSyntax(text, declarations.ToArray(), new Code(failure.Code, text, failure.Offset, failure.Message));
        }

        return new GesSchemaSourceSyntax(text, declarations.ToArray(), null);
    }

    private GesSchemaDeclarationSyntax ReadDeclaration()
    {
        var offset = _position;
        string? wire = null;
        string? model = null;
        var wordOffset = _position;
        var word = ReadWord();
        while (word is "wire" or "model")
        {
            var group = "default";
            if (Consume('('))
            {
                var groupOffset = _position;
                group = ReadWord();
                if (!GesText.IsFieldName(group)) throw new SyntaxFailure(groupOffset, "A role group is a lowercase name.", Code.InvalidRole);
                Expect(')');
            }

            if ((word == "wire" ? wire : model) is not null) throw new SyntaxFailure(wordOffset, $"The declaration is already a {word} root.", Code.DuplicateRole);
            if (word == "wire") wire = group;
            else model = group;
            SkipSpaces();
            wordOffset = _position;
            word = ReadWord();
        }

        if (wire is not null || model is not null)
        {
            if (word is "constant" or "parameter" or "tags") throw new SyntaxFailure(offset, "Only a schema, message or message set can be a root.", Code.InvalidRole);
            if (model is not null && word is "message" or "messages") throw new SyntaxFailure(offset, "A model root is a schema.", Code.InvalidRole);
        }
        else if (word.Length > 0 && word is not ("constant" or "parameter" or "schema" or "message" or "messages" or "tags") && AtRoleEnd())
            throw new SyntaxFailure(offset, $"'{word}' is no role; a root is marked wire or model.", Code.InvalidRole);

        var declaration = ReadDeclaration(offset, wordOffset, word);
        declaration.WireGroup = wire;
        declaration.ModelGroup = model;
        return declaration;
    }

    // Whether an unknown word stands where a role does: before a group or a declaration keyword.
    private bool AtRoleEnd()
    {
        if (_position < _text.Length && _text[_position] == '(') return true;
        var start = _position;
        SkipSpaces();
        var role = _position > start && (AtWord("schema") || AtWord("message") || AtWord("messages"));
        _position = start;
        return role;
    }

    private GesSchemaDeclarationSyntax ReadDeclaration(int offset, int wordOffset, string word)
    {
        switch (word)
        {
            case "constant":
            {
                SkipSpaces();
                if (!Consume('$')) throw Failure("Expected a constant name.");
                var name = ReadName(GesText.IsConstantName, "constant name");
                SkipSpaces();
                ExpectWord("be");
                SkipSpaces();
                return new GesSchemaDeclarationSyntax { Kind = GesSchemaDeclarationKind.Constant, Name = name, Offset = offset, Value = ReadLiteral() };
            }
            case "parameter":
            {
                SkipSpaces();
                var name = ReadName(GesText.IsFieldName, "parameter name");
                SkipSpaces();
                Expect(':');
                SkipSpaces();
                var start = _position;
                var type = ReadType();
                if (type.Kind is not (GesSchemaTypeKind.Number or GesSchemaTypeKind.Percentage or GesSchemaTypeKind.Quantity))
                    throw new SyntaxFailure(start, "A parameter is a Number, Percentage or Quantity.");
                var constraints = type.Constraints;
                if (constraints.Integer || constraints.Lower is not null || constraints.Step is not null || constraints.Delta is not null)
                    throw new SyntaxFailure(start, "A parameter has only a kind and a unit; its value comes with the binding.");
                return new GesSchemaDeclarationSyntax { Kind = GesSchemaDeclarationKind.Parameter, Name = name, Offset = offset, Type = type };
            }
            case "schema":
            {
                var name = ReadTypeName();
                SkipSpaces();
                ExpectWord("as");
                SkipTrivia();
                Expect('{');
                var fields = new List<GesSchemaFieldSyntax>();
                SkipSeparators();
                while (!Consume('}'))
                {
                    fields.Add(ReadField(allowUnlabeled: false));
                    EndEntry('}');
                    SkipSeparators();
                }

                return new GesSchemaDeclarationSyntax { Kind = GesSchemaDeclarationKind.Schema, Name = name, Offset = offset, Fields = fields.ToArray() };
            }
            case "message":
            {
                SkipSpaces();
                var name = ReadName(GesText.IsMessageName, "message name");
                SkipSpaces();
                Expect('(');
                var fields = new List<GesSchemaFieldSyntax>();
                SkipTrivia();
                while (!Consume(')'))
                {
                    if (fields.Count > 0)
                    {
                        Expect(',');
                        SkipTrivia();
                    }

                    fields.Add(ReadField(allowUnlabeled: true));
                    SkipTrivia();
                }

                SkipSpaces();
                string[] tags = [];
                if (AtWord("tags"))
                {
                    ReadWord();
                    SkipSpaces();
                    tags = ReadTagSet();
                }

                return new GesSchemaDeclarationSyntax { Kind = GesSchemaDeclarationKind.Message, Name = name, Offset = offset, Fields = fields.ToArray(), Tags = tags };
            }
            case "messages":
            {
                var name = ReadTypeName();
                SkipSpaces();
                ExpectWord("as");
                SkipTrivia();
                Expect('(');
                var members = new List<string>();
                var offsets = new List<int>();
                do
                {
                    SkipTrivia();
                    offsets.Add(_position);
                    members.Add(ReadName(GesText.IsMessageName, "message name"));
                    SkipTrivia();
                }
                while (Consume(','));
                Expect(')');
                return new GesSchemaDeclarationSyntax { Kind = GesSchemaDeclarationKind.Messages, Name = name, Offset = offset, Members = members.ToArray(), MemberOffsets = offsets.ToArray() };
            }
            case "tags":
            {
                var name = ReadTypeName();
                SkipSpaces();
                ExpectWord("as");
                SkipSpaces();
                return new GesSchemaDeclarationSyntax { Kind = GesSchemaDeclarationKind.TagSet, Name = name, Offset = offset, Tags = ReadTagSet() };
            }
            default:
                throw new SyntaxFailure(wordOffset, "Expected constant, parameter, tags, schema, message or messages.");
        }
    }

    private GesSchemaFieldSyntax ReadField(bool allowUnlabeled)
    {
        var offset = _position;
        var unlabeled = false;
        if (allowUnlabeled && Consume('_'))
        {
            unlabeled = true;
            SkipSpaces();
        }

        var name = ReadName(GesText.IsFieldName, "field name");
        SkipSpaces();
        Expect(':');
        SkipSpaces();
        var alternatives = ReadUnion();
        SkipSpaces();
        var marker = GesSchemaFieldMarker.None;
        if (AtWord("optional"))
        {
            ReadWord();
            marker = GesSchemaFieldMarker.Optional;
        }
        else if (AtWord("key"))
        {
            ReadWord();
            marker = GesSchemaFieldMarker.Key;
        }

        return new GesSchemaFieldSyntax { Name = name, Unlabeled = unlabeled, Alternatives = alternatives, Marker = marker, Offset = offset };
    }

    private GesSchemaTypeSyntax[] ReadUnion()
    {
        var alternatives = new List<GesSchemaTypeSyntax> { ReadAlternative() };
        SkipSpaces();
        while (Consume('|'))
        {
            SkipTrivia();
            alternatives.Add(ReadAlternative());
            SkipSpaces();
        }

        return alternatives.ToArray();
    }

    // A type with an optional label in front, as in `bind: :Loadout_UnitBind`; a type itself starts with ':'.
    private GesSchemaTypeSyntax ReadAlternative()
    {
        if (_position >= _text.Length || !GesText.IsAsciiLower(_text[_position])) return ReadType();
        var offset = _position;
        var label = ReadName(GesText.IsFieldName, "alternative label");
        SkipSpaces();
        Expect(':');
        SkipSpaces();
        var type = ReadType();
        type.Label = label;
        type.LabelOffset = offset;
        return type;
    }

    private GesSchemaTypeSyntax ReadType()
    {
        var offset = _position;
        Expect(':');
        var name = ReadName(GesText.IsTypeName, "type name");
        SkipSpaces();
        switch (GesSchemaTypeNames.Kind(name))
        {
            case GesSchemaTypeKind.Boolean: return new GesSchemaTypeSyntax { Kind = GesSchemaTypeKind.Boolean, Offset = offset };
            case GesSchemaTypeKind.Number: return new GesSchemaTypeSyntax { Kind = GesSchemaTypeKind.Number, Offset = offset, Constraints = ReadConstraints() };
            case GesSchemaTypeKind.Percentage: return new GesSchemaTypeSyntax { Kind = GesSchemaTypeKind.Percentage, Offset = offset, Constraints = ReadConstraints() };
            case GesSchemaTypeKind.Quantity:
            {
                var unit = ReadUnit() ?? throw new SyntaxFailure(_position, "Expected a unit in parentheses.");
                SkipSpaces();
                var kind = unit == GesQuantityUnit.UnitNone ? GesSchemaTypeKind.Number : GesSchemaTypeKind.Quantity;
                return new GesSchemaTypeSyntax { Kind = kind, Unit = unit, Offset = offset, Constraints = ReadConstraints() };
            }
            case GesSchemaTypeKind.Point:
            case GesSchemaTypeKind.Vector:
                return ReadSpatial(name == "Point" ? GesSchemaTypeKind.Point : GesSchemaTypeKind.Vector, offset);
            case GesSchemaTypeKind.Tag:
            {
                var tags = _position < _text.Length && _text[_position] == '(' ? ReadTagSet() : null;
                return new GesSchemaTypeSyntax { Kind = GesSchemaTypeKind.Tag, Tags = tags, Max = tags is null ? ReadMax() : null, Offset = offset };
            }
            case GesSchemaTypeKind.Flags:
                return new GesSchemaTypeSyntax { Kind = GesSchemaTypeKind.Flags, Tags = ReadTagSet(), Offset = offset };
            case GesSchemaTypeKind.Text:
            case GesSchemaTypeKind.Bytes:
                return new GesSchemaTypeSyntax { Kind = name == "Text" ? GesSchemaTypeKind.Text : GesSchemaTypeKind.Bytes, Max = ReadMax(), Offset = offset };
            case GesSchemaTypeKind.List:
            {
                GesSchemaTypeSyntax[]? element = null;
                long? max = null;
                if (Consume('('))
                {
                    // Every pass over a type recurses, so its nesting has a limit below any stack.
                    if (++_typeNesting > MaximumTypeNesting) throw new SyntaxFailure(offset, $"List types nest at most {MaximumTypeNesting} levels.", Code.Cycle);
                    SkipTrivia();
                    element = ReadUnion();
                    SkipTrivia();
                    Expect(')');
                    _typeNesting--;
                    SkipSpaces();
                    max = ReadMax();
                }

                return new GesSchemaTypeSyntax { Kind = GesSchemaTypeKind.List, Element = element, Max = max, Offset = offset };
            }
            case GesSchemaTypeKind.Map: return new GesSchemaTypeSyntax { Kind = GesSchemaTypeKind.Map, Offset = offset };
            case GesSchemaTypeKind.Range: return new GesSchemaTypeSyntax { Kind = GesSchemaTypeKind.Range, Offset = offset };
            case GesSchemaTypeKind.Series: return new GesSchemaTypeSyntax { Kind = GesSchemaTypeKind.Series, Offset = offset };
            case GesSchemaTypeKind.Dice: return new GesSchemaTypeSyntax { Kind = GesSchemaTypeKind.Dice, Max = ReadMax(), Constraints = ReadConstraints(), Offset = offset };
            case GesSchemaTypeKind.Message: return new GesSchemaTypeSyntax { Kind = GesSchemaTypeKind.Message, Offset = offset };
            case GesSchemaTypeKind.Handler: return new GesSchemaTypeSyntax { Kind = GesSchemaTypeKind.Handler, Offset = offset };
            case GesSchemaTypeKind.Nothing: return new GesSchemaTypeSyntax { Kind = GesSchemaTypeKind.Nothing, Offset = offset };
            default: return new GesSchemaTypeSyntax { Kind = GesSchemaTypeKind.Reference, Name = name, Offset = offset };
        }
    }

    private GesSchemaTypeSyntax ReadSpatial(GesSchemaTypeKind kind, int offset)
    {
        var unit = _position < _text.Length && _text[_position] == '(' ? ReadUnit() : GesQuantityUnit.UnitNone;
        SkipSpaces();
        GesSchemaAxisSyntax[]? axes = null;
        if (Consume('{'))
        {
            var list = new List<GesSchemaAxisSyntax>();
            SkipSeparators();
            while (!Consume('}'))
            {
                var axisOffset = _position;
                var axis = ReadWord();
                if (axis is not ("x" or "y" or "z")) throw new SyntaxFailure(axisOffset, "Expected the axis x, y or z.");
                SkipSpaces();
                list.Add(new GesSchemaAxisSyntax { Axis = axis[0], Offset = axisOffset, Constraints = ReadConstraints() });
                EndEntry('}');
                SkipSeparators();
            }

            if (list.Count == 0) throw new SyntaxFailure(offset, "An axis block names at least one axis.");
            axes = list.ToArray();
        }

        return new GesSchemaTypeSyntax { Kind = kind, Unit = unit ?? GesQuantityUnit.UnitNone, Axes = axes, Offset = offset };
    }

    private GesQuantityUnit? ReadUnit()
    {
        if (!Consume('(')) return null;
        var start = _position;
        var end = _text.IndexOf(')', start);
        if (end < 0) throw new SyntaxFailure(start, "Expected ')'.");
        var builder = new System.Text.StringBuilder();
        for (var index = start; index < end; index++)
            if (_text[index] is not (' ' or '\t')) builder.Append(_text[index] == '÷' ? '/' : _text[index]);
        _position = end + 1;
        return GesQuantityUnits.ParseQuantityName(builder.ToString()) ?? throw new SyntaxFailure(start, "A type names a base unit; a prefixed spelling such as kN is only for literals.");
    }

    private long? ReadMax()
    {
        if (!AtWord("max")) return null;
        ReadWord();
        SkipSpaces();
        // Up to 12 decimal digits without a leading zero; a single `_` may separate two digits, as in number literals.
        var start = _position;
        var value = 0L;
        var digits = 0;
        while (_position < _text.Length)
        {
            var c = _text[_position];
            if (c is >= '0' and <= '9')
            {
                value = value * 10 + (c - '0');
                digits++;
            }
            else if (c != '_' || digits == 0 || _position + 1 >= _text.Length || _text[_position + 1] is not (>= '0' and <= '9')) break;
            _position++;
            if (digits > 12) break;
        }

        if (digits == 0 || digits > 12 || (_text[start] == '0' && digits > 1)) throw new SyntaxFailure(start, "Expected a whole number.");
        SkipSpaces();
        return value;
    }

    private string[] ReadTagSet()
    {
        Expect('(');
        var tags = new List<string>();
        do
        {
            SkipTrivia();
            Expect('#');
            tags.Add(ReadName(GesText.IsTagName, "tag name"));
            SkipTrivia();
        }
        while (Consume(','));
        Expect(')');
        return tags.ToArray();
    }

    private GesSchemaConstraintsSyntax ReadConstraints()
    {
        var integer = false;
        var wrapping = false;
        GesSchemaValueSyntax? lower = null;
        GesSchemaValueSyntax? upper = null;
        GesSchemaValueSyntax? step = null;
        GesSchemaValueSyntax? delta = null;
        if (AtWord("integer"))
        {
            ReadWord();
            integer = true;
            SkipSpaces();
        }

        if (AtWord("wrapping") || AtWord("between"))
        {
            if (AtWord("wrapping"))
            {
                ReadWord();
                wrapping = true;
                SkipSpaces();
            }

            ExpectWord("between");
            SkipSpaces();
            lower = ReadBound();
            SkipSpaces();
            ExpectWord("and");
            SkipSpaces();
            upper = ReadBound();
            SkipSpaces();
        }

        if (AtWord("step"))
        {
            ReadWord();
            SkipSpaces();
            step = ReadValue();
            SkipSpaces();
        }

        if (AtWord("delta"))
        {
            ReadWord();
            SkipSpaces();
            ExpectWord("within");
            SkipSpaces();
            delta = ReadValue();
            SkipSpaces();
        }

        return new GesSchemaConstraintsSyntax { Integer = integer, Wrapping = wrapping, Lower = lower, Upper = upper, Step = step, Delta = delta };
    }

    private GesSchemaValueSyntax ReadBound()
    {
        if (_position < _text.Length && GesText.IsAsciiLower(_text[_position]))
        {
            var offset = _position;
            return new GesSchemaValueSyntax { Parameter = ReadName(GesText.IsFieldName, "parameter name"), Offset = offset };
        }

        return ReadValue();
    }

    private GesSchemaValueSyntax ReadValue()
    {
        var offset = _position;
        if (Consume('$')) return new GesSchemaValueSyntax { Constant = ReadName(GesText.IsConstantName, "constant name"), Offset = offset };
        return ReadLiteral();
    }

    // A number with its suffix. The value stays an exact decimal, since quantization and the hash depend on it.
    private GesSchemaValueSyntax ReadLiteral()
    {
        var offset = _position;
        var end = _position;
        while (end < _text.Length && !IsLiteralEnd(end)) end++;
        if (end == offset) throw Failure("Expected a number.");
        var value = GesSchemaLiterals.Read(_text, offset, end) ?? throw new SyntaxFailure(offset, "Expected an exact finite number with a unit known to literals.");
        _position = end;
        return new GesSchemaValueSyntax { Value = value.Value, Unit = value.Unit, IsPercent = value.IsPercent, Text = _text.Substring(offset, end - offset), Offset = offset };
    }

    private bool IsLiteralEnd(int index)
    {
        var c = _text[index];
        return c is ' ' or '\t' or '\r' or '\n' or ',' or '(' or ')' or '{' or '}' or '|' or '#' || (c == '/' && index + 1 < _text.Length && _text[index + 1] == '/');
    }

    private void EndEntry(char closing)
    {
        SkipSpacesAndComments();
        if (_position == _text.Length) throw Failure("Expected '" + closing + "'.");
        if (_text[_position] is ',' or '\r' or '\n' || _text[_position] == closing) return;
        throw Failure("Expected a comma, a line break or '" + closing + "'.");
    }

    private void SkipSeparators()
    {
        SkipTrivia();
        while (Consume(',')) SkipTrivia();
    }

    private void SkipSpaces()
    {
        while (_position < _text.Length && _text[_position] is ' ' or '\t') _position++;
    }

    private void SkipSpacesAndComments()
    {
        SkipSpaces();
        if (AtComment) while (_position < _text.Length && _text[_position] is not ('\r' or '\n')) _position++;
    }

    private void SkipTrivia()
    {
        while (_position < _text.Length)
        {
            if (_text[_position] is ' ' or '\t' or '\r' or '\n') _position++;
            else if (AtComment) while (_position < _text.Length && _text[_position] is not ('\r' or '\n')) _position++;
            else return;
        }
    }

    private bool AtComment => _position + 1 < _text.Length && _text[_position] == '/' && _text[_position + 1] == '/';

    private bool Consume(char c)
    {
        if (_position >= _text.Length || _text[_position] != c) return false;
        _position++;
        return true;
    }

    private void Expect(char c)
    {
        if (!Consume(c)) throw Failure("Expected '" + c + "'.");
    }

    private bool AtWord(string word)
    {
        if (string.CompareOrdinal(_text, _position, word, 0, word.Length) != 0) return false;
        var end = _position + word.Length;
        return end == _text.Length || !IsNameCharacter(_text[end]);
    }

    private void ExpectWord(string word)
    {
        if (!AtWord(word)) throw Failure("Expected '" + word + "'.");
        _position += word.Length;
    }

    private string ReadWord()
    {
        var start = _position;
        while (_position < _text.Length && IsNameCharacter(_text[_position])) _position++;
        return _text.Substring(start, _position - start);
    }

    private string ReadTypeName()
    {
        SkipSpaces();
        Expect(':');
        return ReadName(GesText.IsTypeName, "type name");
    }

    private string ReadName(Func<string, bool> valid, string description)
    {
        var start = _position;
        var name = ReadWord();
        if (name.Length > 0 && GesText.IsAsciiUpper(name[0]))
        {
            var end = GesText.UpperNameEnd(_text, _position);
            if (end != _position)
            {
                _position = end;
                name = _text.Substring(start, end - start);
            }
        }
        if (!valid(name)) throw new SyntaxFailure(start, "Expected a " + description + ".");
        return name;
    }

    private static bool IsNameCharacter(char c) => GesText.IsAsciiLetter(c) || c is >= '0' and <= '9';

    private SyntaxFailure Failure(string message) => new(_position, message);
}

/// <summary>Reads schema literals exactly, with the number grammar and spelling table of GES literals.</summary>
internal static class GesSchemaLiterals
{
    internal readonly struct Literal(GesSchemaDecimal value, GesQuantityUnit unit, bool isPercent)
    {
        internal GesSchemaDecimal Value { get; } = value;

        internal GesQuantityUnit Unit { get; } = unit;

        internal bool IsPercent { get; } = isPercent;
    }

    internal static Literal? Read(string text, int start, int end)
    {
        var unit = GesQuantityUnit.UnitNone;
        var shift = 0;
        var factor = 1L;
        var percent = false;
        var last = end;
        if (last > start && text[last - 1] == '%')
        {
            percent = true;
            shift = -2;
            last--;
        }
        else if (GesQuantityUnits.TryMatchTrailingSuffix(text, start, end, out var suffix))
        {
            unit = suffix.Unit;
            shift = suffix.Exponent;
            factor = suffix.Factor;
            last -= suffix.Text.Length;
        }

        var position = start;
        var negative = false;
        if (position < last && text[position] is '-' or '+') negative = text[position++] == '-';
        ulong magnitude = 0;
        var limit = negative ? 1UL << 63 : long.MaxValue;
        var exponent = shift;
        var digits = 0;
        var afterPoint = false;
        var previousDigit = false;
        for (; position < last; position++)
        {
            var c = text[position];
            if (c is >= '0' and <= '9')
            {
                if (magnitude > (limit - (ulong)(c - '0')) / 10)
                {
                    // Trailing zeros beyond the range keep the value; other digits are too exact for a schema.
                    if (c != '0') return null;
                    if (!afterPoint) exponent++;
                }
                else
                {
                    magnitude = (magnitude * 10) + (ulong)(c - '0');
                    if (afterPoint) exponent--;
                }

                digits++;
                previousDigit = true;
            }
            else if (c == '_' && previousDigit && position + 1 < last && text[position + 1] is >= '0' and <= '9') previousDigit = false;
            else if (c == '.' && !afterPoint && previousDigit)
            {
                afterPoint = true;
                previousDigit = false;
            }
            else if (c is 'e' or 'E' && previousDigit) break;
            else return null;
        }

        if (digits == 0 || !previousDigit && position == last) return null;
        if (position < last)
        {
            position++;
            var exponentNegative = false;
            if (position < last && text[position] is '-' or '+') exponentNegative = text[position++] == '-';
            if (position == last || last - position > 4) return null;
            var written = 0;
            for (; position < last; position++)
            {
                if (text[position] is < '0' or > '9') return null;
                written = (written * 10) + (text[position] - '0');
            }

            exponent += exponentNegative ? -written : written;
        }

        if (factor != 1)
        {
            if (magnitude > limit / (ulong)factor) return null;
            magnitude *= (ulong)factor;
        }

        return new Literal(new GesSchemaDecimal(negative ? unchecked((long)(0UL - magnitude)) : (long)magnitude, exponent), unit, percent);
    }
}
