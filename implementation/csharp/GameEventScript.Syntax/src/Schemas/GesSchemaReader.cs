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
internal sealed partial class GesSchemaSourceReader
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

        var isAbstract = false;
        if (word == "abstract" && _position < _text.Length && _text[_position] is ' ' or '\t')
        {
            if (wire is not null || model is not null) throw new SyntaxFailure(offset, "An abstract schema only lends its fields and takes no role.", Code.InvalidRole);
            isAbstract = true;
            SkipSpaces();
            wordOffset = _position;
            word = ReadWord();
            if (word != "schema") throw new SyntaxFailure(wordOffset, "Expected 'schema' after 'abstract'.");
        }

        if (wire is not null || model is not null)
        {
            if (word is "constant" or "parameter" or "tags") throw new SyntaxFailure(offset, "Only a schema, message or message set can be a root.", Code.InvalidRole);
            if (model is not null && word is "message" or "messages") throw new SyntaxFailure(offset, "A model root is a schema.", Code.InvalidRole);
        }
        else if (word.Length > 0 && word is not ("constant" or "parameter" or "schema" or "message" or "messages" or "tags") && AtRoleEnd())
            throw new SyntaxFailure(offset, $"'{word}' is no role; a root is marked wire or model.", Code.InvalidRole);

        var declaration = ReadDeclaration(offset, wordOffset, word);
        declaration.Abstract = isAbstract;
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
        var role = _position > start && (AtWord("schema") || AtWord("message") || AtWord("messages") || AtWord("abstract"));
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
                var computed = new List<GesSchemaFieldSyntax>();
                var lets = new List<GesSchemaLetSyntax>();
                var withs = new List<GesSchemaWithSyntax>();
                SkipSeparators();
                for (var sequence = 0; !Consume('}'); sequence++)
                {
                    if (ReadLet() is { } let) lets.Add(let);
                    else if (ReadWith(fields.Count, sequence) is { } with) withs.Add(with);
                    else
                    {
                        var field = ReadField(allowUnlabeled: false, allowDefault: true);
                        field.Sequence = sequence;
                        (field.Computed is null ? fields : computed).Add(field);
                    }

                    EndEntry('}');
                    SkipSeparators();
                }

                return new GesSchemaDeclarationSyntax
                {
                    Kind = GesSchemaDeclarationKind.Schema,
                    Name = name,
                    Offset = offset,
                    Fields = fields.ToArray(),
                    Computed = computed.ToArray(),
                    Lets = lets.ToArray(),
                    Withs = withs.ToArray()
                };
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

                    fields.Add(ReadField(allowUnlabeled: true, allowDefault: false));
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

    // `let name be value`; a field named let is followed by its colon instead of a name.
    private GesSchemaLetSyntax? ReadLet()
    {
        var offset = _position;
        if (!AtWord("let")) return null;
        ReadWord();
        SkipSpaces();
        if (_position >= _text.Length || !GesText.IsAsciiLower(_text[_position]))
        {
            _position = offset;
            return null;
        }

        var name = ReadName(GesText.IsFieldName, "let name");
        SkipSpaces();
        ExpectWord("be");
        SkipSpaces();
        if (_position < _text.Length && GesText.IsAsciiLower(_text[_position])) throw Failure("A let is a literal or a constant.");
        return new GesSchemaLetSyntax { Name = name, Value = ReadValue(), Offset = offset };
    }

    // `with :Name [all optional] [without (a, b)]`; a field named with is followed by its colon instead of a type name.
    private GesSchemaWithSyntax? ReadWith(int position, int sequence)
    {
        var offset = _position;
        if (!AtWord("with")) return null;
        ReadWord();
        SkipSpaces();
        if (_position == offset + 4 || _position + 1 >= _text.Length || _text[_position] != ':' || !GesText.IsAsciiUpper(_text[_position + 1]))
        {
            _position = offset;
            return null;
        }

        var name = ReadTypeName();
        SkipSpaces();
        var allOptional = false;
        if (AtWord("all"))
        {
            ReadWord();
            SkipSpaces();
            ExpectWord("optional");
            allOptional = true;
            SkipSpaces();
        }

        var without = new List<string>();
        var offsets = new List<int>();
        if (AtWord("without"))
        {
            ReadWord();
            SkipSpaces();
            Expect('(');
            do
            {
                SkipTrivia();
                offsets.Add(_position);
                without.Add(ReadName(GesText.IsFieldName, "field name"));
                SkipTrivia();
            }
            while (Consume(','));
            Expect(')');
        }

        return new GesSchemaWithSyntax { Name = name, AllOptional = allOptional, Without = without.ToArray(), WithoutOffsets = offsets.ToArray(), Position = position, Sequence = sequence, Offset = offset };
    }

    private GesSchemaFieldSyntax ReadField(bool allowUnlabeled, bool allowDefault)
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

        GesSchemaDefaultSyntax? fallback = null;
        SkipSpaces();
        if (AtWord("computed"))
        {
            if (!allowDefault) throw Failure("A message argument is not computed.");
            ReadWord();
            SkipSpaces();
            ExpectWord("by");
            SkipContinuation();
            _expressionNesting = 0;
            var expression = ReadExpression();
            return new GesSchemaFieldSyntax { Name = name, Alternatives = alternatives, Marker = marker, Computed = expression, Offset = offset };
        }

        if (allowDefault && AtWord("default"))
        {
            var start = _position;
            ReadWord();
            SkipSpaces();
            fallback = ReadDefault(start);
        }

        return new GesSchemaFieldSyntax { Name = name, Unlabeled = unlabeled, Alternatives = alternatives, Marker = marker, Default = fallback, Offset = offset };
    }

    // A tag, `true`, `false`, a record literal, or a value: a number, a constant, or the name of a let or a sibling field.
    private GesSchemaDefaultSyntax ReadDefault(int offset)
    {
        return _position < _text.Length && _text[_position] == '[' ? ReadRecordDefault(offset, 1) : ReadEntryValue(offset);
    }

    // `[:]`, or `[name: value, ...]` with comma-separated entries; comments and line breaks may stand between the tokens.
    private GesSchemaDefaultSyntax ReadRecordDefault(int offset, int depth)
    {
        if (depth > MaximumTypeNesting) throw Failure($"Record defaults nest at most {MaximumTypeNesting} levels.");
        Expect('[');
        SkipTrivia();
        var entries = new List<GesSchemaRecordEntrySyntax>();
        if (Consume(':'))
        {
            SkipTrivia();
            Expect(']');
            return new GesSchemaDefaultSyntax { Record = [], Offset = offset };
        }

        do
        {
            SkipTrivia();
            var start = _position;
            var name = ReadName(GesText.IsFieldName, "field name");
            SkipTrivia();
            Expect(':');
            SkipTrivia();
            var value = _position < _text.Length && _text[_position] == '[' ? ReadRecordDefault(_position, depth + 1) : ReadEntryValue(_position);
            entries.Add(new GesSchemaRecordEntrySyntax { Name = name, Value = value, Offset = start });
            SkipTrivia();
        }
        while (Consume(','));
        Expect(']');
        return new GesSchemaDefaultSyntax { Record = entries.ToArray(), Offset = offset };
    }

    private GesSchemaDefaultSyntax ReadEntryValue(int offset)
    {
        if (Consume('#')) return new GesSchemaDefaultSyntax { Tag = ReadName(GesText.IsTagName, "tag name"), Offset = offset };
        foreach (var word in new[] { "true", "false" })
            if (AtWord(word))
            {
                ReadWord();
                return new GesSchemaDefaultSyntax { Boolean = word == "true", Offset = offset };
            }

        return new GesSchemaDefaultSyntax { Value = ReadValue(), Offset = offset };
    }

    // Alternatives separated by `|`, which may end a line or start the next one; comments and blank lines may stand between.
    private GesSchemaTypeSyntax[] ReadUnion()
    {
        var alternatives = new List<GesSchemaTypeSyntax> { ReadAlternative() };
        SkipSpaces();
        while (ConsumeBar())
        {
            SkipTrivia();
            alternatives.Add(ReadAlternative());
            SkipSpaces();
        }

        return alternatives.ToArray();
    }

    // A `|` on this line, or as the first token of a later line, which continues the union; a field never starts with it.
    private bool ConsumeBar()
    {
        var start = _position;
        SkipTrivia();
        if (Consume('|')) return true;
        _position = start;
        return false;
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
            case GesSchemaTypeKind.Box:
            case GesSchemaTypeKind.Poly:
                return ReadSpatial(GesSchemaTypeNames.Kind(name)!.Value, offset);
            case GesSchemaTypeKind.Tag:
            {
                var tags = _position < _text.Length && _text[_position] == '(' ? ReadTagSet() : null;
                var tagMax = tags is null ? ReadMax() : (null, "", 0);
                return new GesSchemaTypeSyntax { Kind = GesSchemaTypeKind.Tag, Tags = tags, Max = tagMax.Value, MaxName = tagMax.Name, MaxOffset = tagMax.Offset, Offset = offset };
            }
            case GesSchemaTypeKind.Flags:
                return new GesSchemaTypeSyntax { Kind = GesSchemaTypeKind.Flags, Tags = ReadTagSet(), Offset = offset };
            case GesSchemaTypeKind.Text:
            case GesSchemaTypeKind.Bytes:
            {
                var max = ReadMax();
                return new GesSchemaTypeSyntax { Kind = name == "Text" ? GesSchemaTypeKind.Text : GesSchemaTypeKind.Bytes, Max = max.Value, MaxName = max.Name, MaxOffset = max.Offset, Offset = offset };
            }
            case GesSchemaTypeKind.List:
            {
                GesSchemaTypeSyntax[]? element = null;
                (long? Value, string Name, int Offset) max = (null, "", 0);
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

                var depth = 0;
                var depthOffset = 0;
                if (element is not null && AtWord("depth"))
                {
                    depthOffset = _position;
                    ReadWord();
                    SkipSpaces();
                    var start = _position;
                    while (_position < _text.Length && _text[_position] is >= '0' and <= '9' && _position - start < 3) depth = depth * 10 + (_text[_position++] - '0');
                    if (_position == start || _text[start] == '0' || (_position < _text.Length && _text[_position] is >= '0' and <= '9'))
                        throw new SyntaxFailure(start, "Expected a depth from 1 to 999.");
                    SkipSpaces();
                }

                return new GesSchemaTypeSyntax
                {
                    Kind = GesSchemaTypeKind.List,
                    Element = element,
                    Max = max.Value,
                    MaxName = max.Name,
                    MaxOffset = max.Offset,
                    Depth = depth,
                    DepthOffset = depthOffset,
                    Offset = offset
                };
            }
            case GesSchemaTypeKind.Map: return new GesSchemaTypeSyntax { Kind = GesSchemaTypeKind.Map, Offset = offset };
            case GesSchemaTypeKind.Range: return new GesSchemaTypeSyntax { Kind = GesSchemaTypeKind.Range, Offset = offset };
            case GesSchemaTypeKind.Series: return new GesSchemaTypeSyntax { Kind = GesSchemaTypeKind.Series, Offset = offset };
            case GesSchemaTypeKind.Dice:
            {
                var max = ReadMax();
                return new GesSchemaTypeSyntax { Kind = GesSchemaTypeKind.Dice, Max = max.Value, MaxName = max.Name, MaxOffset = max.Offset, Constraints = ReadConstraints(), Offset = offset };
            }
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
        // A polygon lies in the plane z = 0, so it has a count and only the axes x and y.
        var max = kind == GesSchemaTypeKind.Poly ? ReadMax() : (null, "", 0);
        GesSchemaAxisSyntax[]? axes = null;
        if (Consume('{'))
        {
            var list = new List<GesSchemaAxisSyntax>();
            SkipSeparators();
            while (!Consume('}'))
            {
                var axisOffset = _position;
                var axis = ReadWord();
                if (kind == GesSchemaTypeKind.Poly ? axis is not ("x" or "y") : axis is not ("x" or "y" or "z"))
                    throw new SyntaxFailure(axisOffset, kind == GesSchemaTypeKind.Poly ? "Expected the axis x or y of a Poly." : "Expected the axis x, y or z.");
                SkipSpaces();
                list.Add(new GesSchemaAxisSyntax { Axis = axis[0], Offset = axisOffset, Constraints = ReadConstraints() });
                EndEntry('}');
                SkipSeparators();
            }

            if (list.Count == 0) throw new SyntaxFailure(offset, "An axis block names at least one axis.");
            axes = list.ToArray();
        }

        return new GesSchemaTypeSyntax { Kind = kind, Unit = unit ?? GesQuantityUnit.UnitNone, Axes = axes, Max = max.Value, MaxName = max.Name, MaxOffset = max.Offset, Offset = offset };
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

    // A whole number, or the name of a let that holds one.
    private (long? Value, string Name, int Offset) ReadMax()
    {
        if (!AtWord("max")) return (null, "", 0);
        ReadWord();
        SkipSpaces();
        if (_position < _text.Length && GesText.IsAsciiLower(_text[_position]))
        {
            var nameOffset = _position;
            var name = ReadName(GesText.IsFieldName, "let name");
            SkipSpaces();
            return (null, name, nameOffset);
        }

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
        return (value, "", start);
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
            lower = ReadValue();
            SkipSpaces();
            ExpectWord("and");
            SkipSpaces();
            upper = ReadValue();
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

    // A literal, a constant, or the name of a let or parameter.
    private GesSchemaValueSyntax ReadValue()
    {
        var offset = _position;
        if (Consume('$')) return new GesSchemaValueSyntax { Constant = ReadName(GesText.IsConstantName, "constant name"), Offset = offset };
        if (_position < _text.Length && GesText.IsAsciiLower(_text[_position])) return new GesSchemaValueSyntax { Name = ReadName(GesText.IsFieldName, "name"), Offset = offset };
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
        return c is ' ' or '\t' or '\r' or '\n' or ',' or '(' or ')' or '{' or '}' or '[' or ']' or '|' or '#' || (c == '/' && index + 1 < _text.Length && _text[index + 1] == '/');
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
