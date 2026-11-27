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
    internal const int MaximumRoutes = 64;

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

        // `packed` or `packed(width)` makes a schema one machine word of 8, 16, 32 or 64 bits.
        var packed = false;
        var packedWidth = 0;
        var packedOffset = 0;
        if (word == "packed" && _position < _text.Length && _text[_position] is ' ' or '\t' or '(')
        {
            if (isAbstract) throw new SyntaxFailure(wordOffset, "An abstract schema is not packed.");
            packed = true;
            packedOffset = wordOffset;
            if (Consume('('))
            {
                SkipSpaces();
                var widthOffset = _position;
                var width = ReadWord();
                packedWidth = width switch { "8" => 8, "16" => 16, "32" => 32, "64" => 64, _ => throw new SyntaxFailure(widthOffset, "A packed width is 8, 16, 32 or 64.") };
                SkipSpaces();
                Expect(')');
            }

            SkipSpaces();
            wordOffset = _position;
            word = ReadWord();
            if (word != "schema") throw new SyntaxFailure(wordOffset, "Expected 'schema' after 'packed'.");
        }

        if (wire is not null || model is not null)
        {
            if (word == "handle") throw new SyntaxFailure(offset, "A handle type takes no role; the schemas that name it reach it.", Code.InvalidHandle);
            if (word is "constant" or "parameter" or "tags" or "network") throw new SyntaxFailure(offset, "Only a schema, message or message set can be a root.", Code.InvalidRole);
            if (model is not null && word is "message") throw new SyntaxFailure(offset, "A model root is a schema or message set.", Code.InvalidRole);
        }
        else if (word.Length > 0 && word is not ("constant" or "parameter" or "schema" or "message" or "messages" or "tags" or "network" or "handle" or "projection") && AtRoleEnd())
            throw new SyntaxFailure(offset, $"'{word}' is no role; a root is marked wire or model.", Code.InvalidRole);

        var declaration = ReadDeclaration(offset, wordOffset, word);
        declaration.Abstract = isAbstract;
        declaration.Packed = packed;
        declaration.PackedWidth = packedWidth;
        declaration.PackedOffset = packedOffset;
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
        var role = _position > start && (AtWord("schema") || AtWord("message") || AtWord("messages") || AtWord("abstract") || AtWord("packed") || AtWord("projection"));
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
                        var field = ReadField(message: false);
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

                    fields.Add(ReadField(message: true));
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
                SkipSpaces();
                if (_position < _text.Length && _text[_position] == ':')
                    throw new SyntaxFailure(_position, "A message set is named without ':', as a message is: messages Name as (...).");
                var name = ReadName(GesText.IsMessageName, "message set name");
                SkipSpaces();
                var parameters = new List<GesSchemaFieldSyntax>();
                if (Consume('('))
                {
                    SkipTrivia();
                    while (!Consume(')'))
                    {
                        if (parameters.Count > 0)
                        {
                            Expect(',');
                            SkipTrivia();
                        }

                        parameters.Add(ReadField(message: true));
                        SkipTrivia();
                    }

                    SkipSpaces();
                }

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
                return new GesSchemaDeclarationSyntax { Kind = GesSchemaDeclarationKind.Messages, Name = name, Offset = offset, Fields = parameters.ToArray(), Members = members.ToArray(), MemberOffsets = offsets.ToArray() };
            }
            case "tags":
            {
                var name = ReadTypeName();
                SkipSpaces();
                ExpectWord("as");
                SkipSpaces();
                return new GesSchemaDeclarationSyntax { Kind = GesSchemaDeclarationKind.TagSet, Name = name, Offset = offset, Tags = ReadTagSet() };
            }
            case "network":
            {
                var name = ReadTypeName();
                SkipSpaces();
                ExpectWord("as");
                SkipTrivia();
                Expect('{');
                var routes = new List<GesSchemaRouteSyntax>();
                SkipSeparators();
                if (_position < _text.Length && _text[_position] == '}') throw Failure("A network has at least one route.");
                // Owns routes carry no payload and do not count towards the route limit.
                var carrying = 0;
                while (!Consume('}'))
                {
                    var routeOffset = _position;
                    var route = ReadRoute();
                    if (!route.Owns && ++carrying > MaximumRoutes) throw new SyntaxFailure(routeOffset, "A network has at most 64 routes.", Code.TooLarge);
                    routes.Add(route);
                    EndEntry('}');
                    SkipSeparators();
                }

                if (carrying == 0) throw new SyntaxFailure(offset, "A network has at least one route that sends or publishes.");

                return new GesSchemaDeclarationSyntax { Kind = GesSchemaDeclarationKind.Network, Name = name, Offset = offset, Routes = routes.ToArray() };
            }
            case "handle":
                return ReadHandle(offset);
            case "projection":
                return ReadProjection(offset);
            default:
                throw new SyntaxFailure(wordOffset, "Expected constant, parameter, tags, schema, message, messages, network, handle or projection.");
        }
    }

    // `handle @Name of :State as { id: ..., version: ... }` or `handle @Name of :State composite @Root as { part: ... }`, where
    // `:Store(@Type)` may stand for the state.
    private GesSchemaDeclarationSyntax ReadHandle(int offset)
    {
        SkipSpaces();
        var name = ReadHandleName();
        SkipSpaces();
        ExpectWord("of");
        SkipSpaces();
        var stateOffset = _position;
        var state = ReadTypeName();
        var store = "";
        var storeOffset = 0;
        if (state == "Store" && _position < _text.Length && _text[_position] == '(')
        {
            // `:Store(@Unit)` declares a store file type; its state stays empty.
            _position++;
            SkipSpaces();
            storeOffset = _position;
            store = ReadHandleName();
            SkipSpaces();
            Expect(')');
            state = "";
        }
        else if (_position < _text.Length && _text[_position] == '(')
        {
            // Only `:Store` takes a handle type here; any other name with an argument names no type.
            throw new SyntaxFailure(stateOffset, $"Type ':{state}(...)' is not declared.", Code.UnknownName);
        }

        SkipSpaces();
        var root = "";
        var rootOffset = 0;
        if (AtWord("composite"))
        {
            ReadWord();
            SkipSpaces();
            rootOffset = _position;
            root = ReadHandleName();
            SkipSpaces();
        }

        ExpectWord("as");
        SkipTrivia();
        Expect('{');
        SkipSeparators();
        GesSchemaTypeSyntax id;
        var version = GesHandleVersionKind.None;
        GesSchemaTypeSyntax? revision = null;
        var versionOffset = 0;
        if (root.Length > 0)
        {
            id = ReadHandleField("part");
        }
        else
        {
            id = ReadHandleField("id");
            EndEntry('}');
            SkipSeparators();
            versionOffset = _position;
            var field = ReadWord();
            if (field != "version") throw new SyntaxFailure(versionOffset, "A handle type has exactly the fields id and version.", Code.InvalidHandle);
            SkipSpaces();
            Expect(':');
            SkipSpaces();
            versionOffset = _position;
            Expect(':');
            var typeName = ReadName(GesText.IsTypeName, "version type");
            SkipSpaces();
            switch (typeName)
            {
                case "Nothing":
                    break;
                case "ContentHash":
                    version = GesHandleVersionKind.ContentHash;
                    break;
                case "Hash":
                {
                    var algorithmOffset = _position;
                    var algorithms = ReadTagSet();
                    version = algorithms is ["sha256"] ? GesHandleVersionKind.Sha256 : algorithms is ["sha1"] ? GesHandleVersionKind.Sha1
                        : throw new SyntaxFailure(algorithmOffset, "A hash version is :Hash(#sha256) or :Hash(#sha1).", Code.InvalidHandle);
                    SkipSpaces();
                    break;
                }
                case "Number":
                    version = GesHandleVersionKind.Revision;
                    revision = new GesSchemaTypeSyntax { Kind = GesSchemaTypeKind.Number, Offset = versionOffset, Constraints = ReadConstraints() };
                    var constraints = revision.Constraints;
                    if (!constraints.Integer || constraints.Wrapping || constraints.Step is not null || constraints.Delta is not null || constraints.Lower?.Name.Length > 0 || constraints.Upper?.Name.Length > 0)
                        throw new SyntaxFailure(versionOffset, "A revision is :Number integer with an optional range.", Code.InvalidHandle);
                    break;
                default:
                    throw new SyntaxFailure(versionOffset, "A version is :Nothing, :Number integer, :Hash(#sha256), :Hash(#sha1) or :ContentHash.", Code.InvalidHandle);
            }
        }

        EndEntry('}');
        SkipSeparators();
        if (!Consume('}'))
            throw new SyntaxFailure(_position, root.Length > 0 ? "A composite handle type has exactly the field part." : "A handle type has exactly the fields id and version.", Code.InvalidHandle);
        return new GesSchemaDeclarationSyntax
        {
            Kind = GesSchemaDeclarationKind.Handle,
            Name = name,
            Offset = offset,
            State = state,
            StateOffset = stateOffset,
            Store = store,
            StoreOffset = storeOffset,
            Root = root,
            RootOffset = rootOffset,
            Id = id,
            Version = version,
            Revision = revision,
            VersionOffset = versionOffset
        };
    }

    // `id: :Number integer between a and b` or `id: :Text max n`, and `part` alike; the bounds' values are checked on loading.
    private GesSchemaTypeSyntax ReadHandleField(string expected)
    {
        var offset = _position;
        var field = ReadWord();
        if (field != expected)
            throw new SyntaxFailure(offset, expected == "part" ? "A composite handle type has exactly the field part." : "A handle type has exactly the fields id and version.", Code.InvalidHandle);
        SkipSpaces();
        Expect(':');
        SkipSpaces();
        var start = _position;
        if (_position >= _text.Length || _text[_position] != ':') throw Failure("Expected ':'.");
        var type = ReadType();
        var valid = type.Kind switch
        {
            GesSchemaTypeKind.Number => type.Constraints is { Integer: true, Wrapping: false, Lower: not null, Step: null, Delta: null }
                && type.Constraints.Lower.Name.Length == 0 && type.Constraints.Upper!.Name.Length == 0,
            GesSchemaTypeKind.Text => type.Max is not null,
            _ => false
        };
        if (!valid) throw new SyntaxFailure(start, $"An {expected} is :Number integer between two literals or constants, or :Text max with a number.", Code.InvalidHandle);
        return type;
    }

    // `projection :Name of :Source as { name, alias: path.to.field }`: the fields a projection takes, separated like fields.
    private GesSchemaDeclarationSyntax ReadProjection(int offset)
    {
        var name = ReadTypeName();
        SkipSpaces();
        ExpectWord("of");
        SkipSpaces();
        var sourceOffset = _position;
        var source = ReadTypeName();
        SkipSpaces();
        ExpectWord("as");
        SkipTrivia();
        Expect('{');
        var taken = new List<GesSchemaTakenSyntax>();
        SkipSeparators();
        while (!Consume('}'))
        {
            var fieldOffset = _position;
            var field = ReadName(GesText.IsFieldName, "field name");
            SkipSpaces();
            var path = new List<string> { field };
            var offsets = new List<int> { fieldOffset };
            if (Consume(':'))
            {
                SkipSpaces();
                path.Clear();
                offsets.Clear();
                do
                {
                    offsets.Add(_position);
                    path.Add(ReadName(GesText.IsFieldName, "field name"));
                }
                while (Consume('.'));
                SkipSpaces();
            }

            taken.Add(new GesSchemaTakenSyntax { Name = field, Path = path.ToArray(), PathOffsets = offsets.ToArray(), Offset = fieldOffset });
            EndEntry('}');
            SkipSeparators();
        }

        return new GesSchemaDeclarationSyntax { Kind = GesSchemaDeclarationKind.Projection, Name = name, Offset = offset, State = source, StateOffset = sourceOffset, Taken = taken.ToArray() };
    }

    // `@Name` of a handle type, returned without `@`.
    private string ReadHandleName()
    {
        if (!Consume('@')) throw Failure("Expected '@' and a handle type name.");
        return ReadName(GesText.IsTypeName, "handle type name");
    }

    // `client|server sends|publishes :Root|Message reliable|latest|bulk [at most every value]` or
    // `client|server owns @Handle [as :Projection] [sending state]` on one line.
    private GesSchemaRouteSyntax ReadRoute()
    {
        var offset = _position;
        var side = ReadWord();
        if (side is not ("client" or "server")) throw new SyntaxFailure(offset, "A route starts with client or server.");
        SkipSpaces();
        var verbOffset = _position;
        var verb = ReadWord();
        if (verb == "owns")
        {
            SkipSpaces();
            var handleOffset = _position;
            var handle = ReadHandleName();
            SkipSpaces();
            var projection = "";
            var projectionOffset = 0;
            if (AtWord("as"))
            {
                ReadWord();
                SkipSpaces();
                projectionOffset = _position;
                projection = ReadTypeName();
                SkipSpaces();
            }

            var sending = AtWord("sending");
            if (sending)
            {
                ReadWord();
                SkipSpaces();
                ExpectWord("state");
                SkipSpaces();
            }

            return new GesSchemaRouteSyntax
            {
                Server = side == "server",
                Owns = true,
                Root = handle,
                RootOffset = handleOffset,
                Offset = offset,
                Projection = projection,
                ProjectionOffset = projectionOffset,
                SendingState = sending
            };
        }

        if (verb is not ("sends" or "publishes")) throw new SyntaxFailure(verbOffset, "Expected 'sends', 'publishes' or 'owns'.");
        SkipSpaces();
        var rootOffset = _position;
        var isMessage = _position < _text.Length && _text[_position] != ':';
        var root = isMessage ? ReadName(GesText.IsMessageName, "type name or message name") : ReadTypeName();
        SkipSpaces();
        var classOffset = _position;
        var routeClass = ReadWord() switch
        {
            "reliable" => GesSchemaRouteClass.Reliable,
            "latest" => GesSchemaRouteClass.Latest,
            "bulk" => GesSchemaRouteClass.Bulk,
            _ => throw new SyntaxFailure(classOffset, "Expected 'reliable', 'latest' or 'bulk'.")
        };
        SkipSpaces();
        GesSchemaValueSyntax? rate = null;
        if (AtWord("at"))
        {
            ReadWord();
            SkipSpaces();
            ExpectWord("most");
            SkipSpaces();
            ExpectWord("every");
            SkipSpaces();
            if (_position < _text.Length && GesText.IsAsciiLower(_text[_position])) throw Failure("A rate is a literal or a constant.");
            rate = ReadValue();
        }

        return new GesSchemaRouteSyntax { Server = side == "server", Publishes = verb == "publishes", Root = root, IsMessage = isMessage, RootOffset = rootOffset, Class = routeClass, ClassOffset = classOffset, Rate = rate, Offset = offset };
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

    private GesSchemaFieldSyntax ReadField(bool message)
    {
        var offset = _position;
        var unlabeled = false;
        if (message && Consume('_'))
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
            if (message) throw Failure("A message argument is not computed.");
            ReadWord();
            SkipSpaces();
            ExpectWord("by");
            SkipContinuation();
            _expressionNesting = 0;
            var expression = ReadExpression();
            return new GesSchemaFieldSyntax { Name = name, Alternatives = alternatives, Marker = marker, Computed = expression, Offset = offset };
        }

        if (AtWord("default"))
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

    // `[:]`, `[]`, or `[name: value, ...]` with comma-separated entries; comments and line breaks may stand between the tokens.
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

        if (Consume(']')) return new GesSchemaDefaultSyntax { EmptyList = true, Offset = offset };

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
        if (Consume('@'))
        {
            var handle = ReadName(GesText.IsTypeName, "handle type name");
            var projection = "";
            var projectionOffset = 0;
            if (Consume('('))
            {
                // `@Name(:P)`: a handle read through the projection `:P`.
                SkipSpaces();
                projectionOffset = _position;
                projection = ReadTypeName();
                SkipSpaces();
                Expect(')');
            }

            var versioned = Consume('~');
            SkipSpaces();
            return new GesSchemaTypeSyntax { Kind = GesSchemaTypeKind.Handle, Name = handle, Versioned = versioned, Projection = projection, ProjectionOffset = projectionOffset, Offset = offset };
        }

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
            case GesSchemaTypeKind.Disc:
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
            case GesSchemaTypeKind.Map:
            {
                if (!Consume('(')) return new GesSchemaTypeSyntax { Kind = GesSchemaTypeKind.Map, Offset = offset };
                // `:Map(@Name → T) max n`: a Map from handles of one type to values of the union T.
                if (++_typeNesting > MaximumTypeNesting) throw new SyntaxFailure(offset, $"Map types nest at most {MaximumTypeNesting} levels.", Code.Cycle);
                SkipTrivia();
                var keyOffset = _position;
                if (_position >= _text.Length || _text[_position] != '@') throw new SyntaxFailure(keyOffset, "A Map key is one handle type.", Code.InvalidHandle);
                var key = ReadType();
                if (key.Versioned) throw new SyntaxFailure(keyOffset, "A Map key is a handle type without '~'.", Code.InvalidHandle);
                if (key.Projection.Length > 0) throw new SyntaxFailure(key.ProjectionOffset, "A Map key is a handle type without a projection.", Code.InvalidProjection);
                SkipTrivia();
                if (!Consume('→'))
                {
                    if (!(Consume('-') && Consume('>'))) throw Failure("Expected '→' or '->'.");
                }

                SkipTrivia();
                var values = ReadUnion();
                SkipTrivia();
                Expect(')');
                _typeNesting--;
                SkipSpaces();
                var max = ReadMax();
                return new GesSchemaTypeSyntax { Kind = GesSchemaTypeKind.Map, Key = key.Name, KeyOffset = keyOffset, Element = values, Max = max.Value, MaxName = max.Name, MaxOffset = max.Offset, Offset = offset };
            }
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
        // A polygon lies in the plane z = 0, so it has a count and only the axes x and y; a disc lies there too and has a radius.
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
                if (kind switch { GesSchemaTypeKind.Poly => axis is not ("x" or "y"), GesSchemaTypeKind.Disc => axis is not ("x" or "y" or "radius"), _ => axis is not ("x" or "y" or "z") })
                    throw new SyntaxFailure(axisOffset, kind switch { GesSchemaTypeKind.Poly => "Expected the axis x or y of a Poly.", GesSchemaTypeKind.Disc => "Expected the axis x or y or the radius of a Disc.", _ => "Expected the axis x, y or z." });
                SkipSpaces();
                list.Add(new GesSchemaAxisSyntax { Axis = axis, Offset = axisOffset, Constraints = ReadConstraints() });
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
        // A unit such as `J/(kg·K)` nests parentheses; the closing one at depth zero ends it.
        var end = start;
        for (var depth = 0; end < _text.Length && (_text[end] != ')' || depth > 0); end++)
            depth += _text[end] == '(' ? 1 : _text[end] == ')' ? -1 : 0;
        if (end == _text.Length) throw new SyntaxFailure(start, "Expected ')'.");
        var builder = new System.Text.StringBuilder();
        for (var index = start; index < end; index++)
            if (_text[index] is not (' ' or '\t')) builder.Append(_text[index] switch { '÷' => '/', '×' or '⋅' => '*', var other => other });
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
        var end = LiteralEnd(offset, expression: false);
        if (end == offset) throw Failure("Expected a number.");
        var value = GesSchemaLiterals.Read(_text, offset, end) ?? throw new SyntaxFailure(offset, "Expected an exact finite number with a unit known to literals.");
        _position = end;
        return new GesSchemaValueSyntax { Value = value.Value, Unit = value.Unit, IsPercent = value.IsPercent, Text = _text.Substring(offset, end - offset), Offset = offset };
    }

    // The end of a literal from `start`. A parenthesized unit part after `/`, as in `3J/(kg·K)`, belongs to the literal; in an
    // expression a `*` between letters also continues it, as in `5N*m`, and reading falls back to a shorter literal before it.
    private int LiteralEnd(int start, bool expression)
    {
        var end = start;
        while (end < _text.Length)
        {
            var c = _text[end];
            if (c == '(' && end > start && _text[end - 1] == '/')
            {
                var close = end + 1;
                var depth = 1;
                for (; close < _text.Length && depth > 0 && _text[close] is not (' ' or '\t' or '\r' or '\n'); close++)
                    depth += _text[close] switch { '(' => 1, ')' => -1, _ => 0 };
                if (depth != 0) break;
                end = close;
                continue;
            }

            if (expression ? IsExpressionLiteralEnd(end) && !(c == '*' && end > start && _text[end - 1] is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z')) : IsLiteralEnd(end)) break;
            end++;
        }

        return end;
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
