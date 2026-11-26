// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text;
using GameEventScript.Api;
using GameEventScript.Runtime;
using GameEventScript.Syntax;
using static GameEventScript.Syntax.GesTokenKind;
using NodeKind = GameEventScript.Syntax.GesSyntaxNodeKind;

namespace GameEventScript.Compiler;

// Builds the semantic tree of one source from its lossless syntax tree. Each node's tokens are read in the order the
// parser consumed them; a semantic node ranges over the tokens of its syntax, except where noted.
internal sealed partial class GesSyntaxBinder
{
    private readonly GesSyntaxTree _tree;
    private readonly IReadOnlyList<GesToken> _tokens;
    private readonly string _sourceName;
    private readonly uint? _sourceId;
    private string _moduleName = string.Empty;

    private GesSyntaxBinder(GesSyntaxTree tree, string? sourceName, uint? sourceId)
    {
        _tree = tree;
        _tokens = tree.Tokens;
        _sourceName = string.IsNullOrWhiteSpace(sourceName) ? "UnknownSource" : sourceName!;
        _sourceId = sourceId;
    }

    // Parses one source and binds its syntax tree; syntax errors fail the parse phase with their diagnostics. The
    // source's own validation diagnostics are added to `validation`.
    internal static GesParsedScript Parse(string text, string? sourceName = null, uint? sourceId = null, List<GesDiagnostic>? validation = null)
    {
        var tree = GesSyntaxParser.Parse(text);
        var name = string.IsNullOrWhiteSpace(sourceName) ? "UnknownSource" : sourceName!;
        if (tree.Diagnostics.Count != 0)
        {
            var diagnostics = new List<GesDiagnostic>(tree.Diagnostics.Count);
            foreach (var diagnostic in tree.Diagnostics) diagnostics.Add(Convert(diagnostic, GesDiagnosticPhase.Parse, name, sourceId));
            throw new GesCompileException(diagnostics);
        }

        if (validation is not null)
            foreach (var diagnostic in GesSyntaxValidator.Validate(tree)) validation.Add(Convert(diagnostic, GesDiagnosticPhase.Validate, name, sourceId));
        return Bind(tree, sourceName, sourceId);
    }

    private static GesDiagnostic Convert(GesSyntaxDiagnostic diagnostic, GesDiagnosticPhase phase, string sourceName, uint? sourceId)
    {
        var location = new GesSourceLocation(sourceName, diagnostic.Line, diagnostic.Column, diagnostic.EndLine, diagnostic.EndColumn, diagnostic.ModuleName, sourceId);
        var code = diagnostic.Code switch
        {
            GesSyntaxDiagnostic.SourceNestingExceeded => GesDiagnosticCodes.ParseSourceNestingExceeded,
            GesSyntaxDiagnostic.Syntax => GesDiagnosticCodes.ParseSyntax,
            _ => diagnostic.Code
        };
        var kind = diagnostic.SymbolKind switch
        {
            GesSyntaxSymbolKind.Type => GesDiagnosticSymbolKind.Type,
            GesSyntaxSymbolKind.Predicate => GesDiagnosticSymbolKind.Predicate,
            GesSyntaxSymbolKind.Function => GesDiagnosticSymbolKind.Function,
            GesSyntaxSymbolKind.Handler => GesDiagnosticSymbolKind.Handler,
            GesSyntaxSymbolKind.Message => GesDiagnosticSymbolKind.Message,
            GesSyntaxSymbolKind.Variable => GesDiagnosticSymbolKind.Variable,
            GesSyntaxSymbolKind.GlobalDefinition => GesDiagnosticSymbolKind.GlobalDefinition,
            _ => GesDiagnosticSymbolKind.Unknown
        };
        return new GesDiagnostic(phase, code, diagnostic.Message, diagnostic.Symbol, kind, location, diagnostic.ModuleName);
    }

    // The tree must come from a source without syntax errors.
    internal static GesParsedScript Bind(GesSyntaxTree tree, string? sourceName = null, uint? sourceId = null)
        => new GesSyntaxBinder(tree ?? throw new ArgumentNullException(nameof(tree)), sourceName, sourceId).BindScript();

    // Reads the elements of one node: its own tokens and its children, in source order.
    private struct Cursor
    {
        private readonly IReadOnlyList<GesToken> _tokens;
        private readonly GesSyntaxNode.ChildList _children;
        private readonly int _end;
        private int _position;
        private int _child;
        private int _previous;

        internal Cursor(GesSyntaxBinder binder, GesSyntaxNode node)
        {
            _tokens = binder._tokens;
            _children = node.Children;
            _end = node.EndToken;
            _position = node.FirstToken;
            _previous = node.FirstToken - 1;
        }

        // The token at the cursor, also where a child starts; past the node's end it is the next token of the source.
        internal readonly GesToken Current => _tokens[Math.Min(_position, _tokens.Count - 1)];

        // The last token consumed, as the parser's previous token.
        internal readonly GesToken Previous => _tokens[Math.Max(_previous, 0)];

        internal readonly bool AtEnd => _position >= _end;

        internal readonly GesSyntaxNode? Node => _child < _children.Count && _children[_child].FirstToken == _position ? _children[_child] : null;

        internal readonly bool IsNode(NodeKind kind) => Node is { } node && node.Kind == kind;

        // Whether the cursor is at a token of this node itself rather than at a child.
        internal readonly bool IsOwnToken => _position < _end && Node is null;

        internal readonly bool Is(GesTokenKind kind) => IsOwnToken && _tokens[_position].Kind == kind;

        internal readonly bool IsWord(string word) => IsOwnToken && _tokens[_position].IsWord(word);

        // A record field name, which may be one of the keyword-like words of an argument label.
        internal GesToken ExpectFieldName() => IsOwnToken && GesSyntaxParser.IsArgumentLabelKind(_tokens[_position].Kind) ? Advance() : throw Mismatch(_position);

        internal readonly bool IsSelector(string text) => Is(Selector) && string.Equals(_tokens[_position].Text, text, StringComparison.Ordinal);

        // The own token `offset` significant tokens ahead; EndOfFile where a child or the node's end comes first.
        internal readonly GesToken PeekSignificant(int offset)
        {
            var limit = Math.Min(_end, _child < _children.Count ? _children[_child].FirstToken : int.MaxValue);
            var seen = 0;
            for (var index = _position; index < limit; index++)
            {
                if (_tokens[index].Kind == NewLine) continue;
                if (seen++ == offset) return _tokens[index];
            }

            return _tokens[_tokens.Count - 1];
        }

        internal GesToken Advance()
        {
            if (!IsOwnToken) throw Mismatch(_position);
            _previous = _position;
            return _tokens[_position++];
        }

        internal GesSyntaxNode TakeNode()
        {
            var node = Node ?? throw Mismatch(_position);
            _child++;
            if (node.EndToken > node.FirstToken)
            {
                _position = node.EndToken;
                _previous = node.EndToken - 1;
            }

            return node;
        }

        internal GesSyntaxNode TakeNode(NodeKind kind) => IsNode(kind) ? TakeNode() : throw Mismatch(_position);

        internal GesToken Expect(GesTokenKind kind) => Is(kind) ? Advance() : throw Mismatch(_position);

        internal bool Match(GesTokenKind kind)
        {
            if (!Is(kind)) return false;
            Advance();
            return true;
        }

        internal bool MatchWord(string word)
        {
            if (!IsWord(word)) return false;
            Advance();
            return true;
        }

        internal bool MatchSelector(string text)
        {
            if (!IsSelector(text)) return false;
            Advance();
            return true;
        }

        internal void ExpectWord(string word)
        {
            if (!MatchWord(word)) throw Mismatch(_position);
        }

        internal void SkipNewLines()
        {
            while (Is(NewLine)) Advance();
        }

        internal void SkipSeparators()
        {
            while (Is(NewLine) || Is(Semicolon)) Advance();
        }
    }

    private static InvalidOperationException Mismatch(int token) => new($"The syntax tree does not have the expected form at token {token}.");

    private GesSourceLocation Range(GesToken token) => new(_sourceName, token.Line, token.Column, token.EndLine, token.EndColumn, _moduleName, _sourceId);

    private GesSourceLocation Range(GesToken start, GesToken end) => new(_sourceName, start.Line, start.Column, end.EndLine, end.EndColumn, _moduleName, _sourceId);

    private GesSourceLocation? MergeRanges(GesScriptNode? first, GesScriptNode? last = null)
    {
        var start = first?.SourceRange;
        var end = (last ?? first)?.SourceRange;
        if (start is null || end is null || start.Line is null || start.Column is null) return null;

        var endLine = end.EndLine ?? end.Line;
        var endColumn = end.EndColumn ?? end.Column;
        return endLine is null || endColumn is null
            ? null
            : new GesSourceLocation(_sourceName, start.Line, start.Column, endLine, endColumn, _moduleName, _sourceId);
    }

    private static T Set<T>(T node, GesSourceLocation? location) where T : GesScriptNode
    {
        node.SourceRange = location;
        return node;
    }

    private T Set<T>(T node, GesToken start, GesToken end) where T : GesScriptNode => Set(node, Range(start, end));

    private T Merge<T>(T node, GesScriptNode? first, GesScriptNode? last = null) where T : GesScriptNode => Set(node, MergeRanges(first, last));

    private GesParsedScript BindScript()
    {
        var constants = new List<GesConstantDefinitionNode>();
        var types = new List<GesTypeDefinitionNode>();
        var predicates = new List<GesPredicateDefinitionNode>();
        var functions = new List<GesFunctionDefinitionNode>();
        var handlers = new List<GesEventHandlerNode>();
        var c = new Cursor(this, _tree.Root);
        c.SkipSeparators();
        if (c.IsNode(NodeKind.ModuleDeclaration)) BindModule(c.TakeNode());
        c.SkipSeparators();
        while (c.Node is { } node)
        {
            c.TakeNode();
            switch (node.Kind)
            {
                case NodeKind.ConstantDefinition: constants.Add(BindConstant(node)); break;
                case NodeKind.RecordDefinition: types.Add(BindRecord(node)); break;
                case NodeKind.PredicateDefinition: predicates.Add(BindPredicate(node)); break;
                case NodeKind.FunctionDefinition: functions.Add(BindFunction(node)); break;
                case NodeKind.EventHandler: handlers.Add(BindHandler(node)); break;
                default: throw Mismatch(node.FirstToken);
            }

            c.SkipSeparators();
        }

        if (!c.Is(EndOfFile)) throw Mismatch(_tokens.Count - 1);
        var first = _tokens[0];
        return new GesParsedScript(_moduleName, _sourceName, constants, types, predicates, functions, handlers)
        {
            SourceRange = first.Kind == EndOfFile ? Range(first) : Range(first, _tokens[_tokens.Count - 2])
        };
    }

    private void BindModule(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        c.Expect(Module);
        c.SkipNewLines();
        var name = new StringBuilder(c.Expect(Identifier).Text);
        while (c.Match(Dot)) name.Append('.').Append(c.Expect(Identifier).Text);
        _moduleName = name.ToString();
    }

    private GesConstantDefinitionNode BindConstant(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        var start = c.Expect(Constant);
        var name = c.Expect(ConstantReference);
        c.Expect(Be);
        c.SkipNewLines();
        var value = Operand(ref c);
        return Set(new GesConstantDefinitionNode(name.Text[1..], value), start, c.Previous);
    }

    private GesTypeDefinitionNode BindRecord(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        var start = c.Expect(Record);
        var name = BindTypeName(c.TakeNode(NodeKind.TypeReference));
        c.Expect(As);
        c.SkipNewLines();
        c.Expect(LeftBrace);
        c.SkipSeparators();
        var fields = new List<GesTypeFieldDefinitionNode>();
        while (c.IsNode(NodeKind.RecordField))
        {
            fields.Add(BindRecordField(c.TakeNode()));
            c.Match(Comma);
            c.SkipSeparators();
        }

        c.Expect(RightBrace);
        return Set(new GesTypeDefinitionNode(name, fields), start, c.Previous);
    }

    private GesTypeFieldDefinitionNode BindRecordField(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        var start = c.Current;
        var unlabeled = c.Match(Underscore);
        if (unlabeled) c.SkipNewLines();
        var name = c.ExpectFieldName().Text;
        c.Expect(Colon);
        c.SkipNewLines();
        var typeName = BindTypeName(c.TakeNode(NodeKind.TypeReference));
        c.SkipNewLines();

        GesExpressionNode? minimum = null;
        GesExpressionNode? maximum = null;
        GesExpressionNode? computed = null;
        if (c.MatchWord("clamped"))
        {
            c.ExpectWord("between");
            minimum = Operand(ref c);
            c.Expect(OperatorAnd);
            maximum = Operand(ref c);
            c.SkipNewLines();
        }

        if (c.MatchWord("computed"))
        {
            c.ExpectWord("by");
            computed = AssemblyOrExpression(ref c, name);
        }

        var constructorLabel = computed is null ? unlabeled ? GesMessageSignature.UnlabeledParameterName : name : null;
        return Set(new GesTypeFieldDefinitionNode(name, typeName, minimum, maximum, computed, constructorLabel), start, c.Previous);
    }

    private GesPredicateDefinitionNode BindPredicate(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        var start = c.Expect(Predicate);
        var name = c.Expect(Identifier).Text;
        var parameters = BindParameterList(c.TakeNode(NodeKind.ParameterList));
        c.Expect(Be);
        c.SkipNewLines();
        var expression = AssemblyOrExpression(ref c, name, predicate: true);
        return Set(new GesPredicateDefinitionNode(name, parameters, expression), start, c.Previous);
    }

    private GesFunctionDefinitionNode BindFunction(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        var start = c.Expect(Function);
        var name = c.Expect(Identifier).Text;
        var parameters = BindParameterList(c.TakeNode(NodeKind.ParameterList));
        c.Expect(Be);
        c.SkipNewLines();
        var expression = AssemblyOrExpression(ref c, name);
        return Set(new GesFunctionDefinitionNode(name, parameters, expression), start, c.Previous);
    }

    private List<GesParameterNode> BindParameterList(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        var parameters = new List<GesParameterNode>();
        c.Expect(LeftParen);
        if (c.IsNode(NodeKind.Parameter))
        {
            parameters.Add(BindParameter(c.TakeNode()));
            while (c.Match(Comma)) parameters.Add(BindParameter(c.TakeNode(NodeKind.Parameter)));
        }

        c.Expect(RightParen);
        return parameters;
    }

    private GesParameterNode BindParameter(GesSyntaxNode node, bool allowDeclaredType = true)
    {
        var c = new Cursor(this, node);
        var start = c.Current;
        if (c.Match(Underscore))
        {
            c.SkipNewLines();
            var localName = c.Expect(Identifier).Text;
            var declaredType = allowDeclaredType ? OptionalDeclaredType(ref c) : null;
            return Set(new GesParameterNode(null, localName, declaredType), start, c.Previous);
        }

        var name = c.Expect(Identifier).Text;
        var typeName = allowDeclaredType ? OptionalDeclaredType(ref c) : null;
        return Set(new GesParameterNode(name, name, typeName), start, c.Previous);
    }

    private string? OptionalDeclaredType(ref Cursor c)
    {
        c.SkipNewLines();
        if (!c.Match(As)) return null;
        c.SkipNewLines();
        return BindTypeName(c.TakeNode(NodeKind.TypeReference));
    }

    private GesEventHandlerNode BindHandler(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        var start = c.Expect(On);
        c.SkipNewLines();
        var message = c.Advance().Text;
        var dispatchKind = GesEventHandlerDispatchKind.ExactSignature;
        var parameters = new List<GesParameterNode>();
        if (c.IsNode(NodeKind.Parameter))
        {
            var p = new Cursor(this, c.TakeNode());
            p.Expect(As);
            p.SkipNewLines();
            var messageToken = p.Current;
            var localName = p.Expect(Identifier).Text;
            dispatchKind = GesEventHandlerDispatchKind.MessageName;
            parameters.Add(Set(new GesParameterNode(GesSystemEndpoints.MessageArgumentName, localName, "message"), messageToken, p.Previous));
        }
        else if (c.IsNode(NodeKind.ParameterList))
        {
            parameters.AddRange(BindParameterList(c.TakeNode()));
        }

        var requiredTags = new List<string>();
        var excludedTags = new List<string>();
        List<List<string>>? alternatives = null;
        var tagSets = new TagSets();
        while (true)
        {
            c.SkipNewLines();
            if (!c.IsNode(NodeKind.HandlerTagFilter)) break;
            BindTagFilter(c.TakeNode(), requiredTags, excludedTags, ref alternatives, tagSets);
        }

        c.SkipNewLines();
        var statements = BindBlock(c.TakeNode(NodeKind.Block));
        return Set(new GesEventHandlerNode(message, dispatchKind, parameters, statements, requiredTags, excludedTags, ReduceTagAlternatives(requiredTags, alternatives)), start, c.Previous);
    }

    // A `matching` filter is a conjunction of groups: a single tag is required, a `|` group needs one of its tags.
    // `without` filters hold no `|`, which the syntax validator reports.
    // Sets beside the ordered tag lists keep long filters linear.
    private sealed class TagSets
    {
        internal HashSet<string> Group { get; } = new(StringComparer.Ordinal);
        internal HashSet<string> Required { get; } = new(StringComparer.Ordinal);
        internal HashSet<string> Excluded { get; } = new(StringComparer.Ordinal);
    }

    private void BindTagFilter(GesSyntaxNode node, List<string> requiredTags, List<string> excludedTags, ref List<List<string>>? alternatives, TagSets sets)
    {
        var c = new Cursor(this, node);
        var matching = c.Advance().Kind == Matching;
        var group = new List<string>();
        do
        {
            group.Clear();
            sets.Group.Clear();
            do
            {
                c.SkipNewLines();
                var normalized = GameEventScriptMessage.NormalizeTagName(c.Expect(Tag).Text[1..]);
                if (normalized.Length > 0 && sets.Group.Add(normalized)) group.Add(normalized);
                c.SkipNewLines();
            }
            while (c.Match(OperatorCollectionUnion));

            if (group.Count > 1 && matching)
            {
                (alternatives ??= []).Add([.. group]);
                continue;
            }

            var tags = matching ? requiredTags : excludedTags;
            var seen = matching ? sets.Required : sets.Excluded;
            foreach (var tag in group)
                if (seen.Add(tag)) tags.Add(tag);
        }
        while (c.Match(Comma));
    }

    // Drops every group that a required tag or a smaller or earlier equal group already satisfies, so duplicates and
    // absorbed groups vanish; the kept groups keep their source order.
    private static List<IReadOnlyList<string>>? ReduceTagAlternatives(List<string> requiredTags, List<List<string>>? alternatives)
    {
        if (alternatives is null) return null;
        var required = new HashSet<string>(requiredTags, StringComparer.Ordinal);
        var kept = new List<IReadOnlyList<string>>();
        if (alternatives.Count == 1)
        {
            if (!ContainsAny(required, alternatives[0])) kept.Add(alternatives[0]);
            return kept;
        }

        // Tags become dense numbers. A group satisfied by a required tag is dropped, and of equal groups the first stays.
        var ids = new Dictionary<string, int>(StringComparer.Ordinal);
        var counts = new List<int>();
        var groups = new List<(List<string> Tags, int[] Ids)>();
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in alternatives)
        {
            if (ContainsAny(required, group)) continue;
            var groupIds = new int[group.Count];
            for (var tag = 0; tag < group.Count; tag++)
            {
                if (!ids.TryGetValue(group[tag], out var id))
                {
                    ids.Add(group[tag], id = ids.Count);
                    counts.Add(0);
                }

                groupIds[tag] = id;
            }

            Array.Sort(groupIds);
            if (!unique.Add(string.Join(",", groupIds))) continue;
            foreach (var id in groupIds) counts[id]++;
            groups.Add((group, groupIds));
        }

        // A group is absorbed by a smaller group whose tags it all contains, so it contains that group's rarest tag:
        // candidates are listed under it, and a group's shared tags do not make every group a candidate.
        var byRarestTag = new List<int>?[ids.Count];
        var signatures = new ulong[groups.Count];
        for (var index = 0; index < groups.Count; index++)
        {
            var rarest = groups[index].Ids[0];
            foreach (var id in groups[index].Ids)
            {
                if (counts[id] < counts[rarest]) rarest = id;
                signatures[index] |= 1UL << (id & 63);
            }

            (byRarestTag[rarest] ??= []).Add(index);
        }

        var marks = new int[ids.Count];
        for (var index = 0; index < groups.Count; index++)
        {
            var group = groups[index].Ids;
            foreach (var id in group) marks[id] = index + 1;
            var absorbed = false;
            for (var tag = 0; tag < group.Length && !absorbed; tag++)
            {
                var candidates = byRarestTag[group[tag]];
                if (candidates is null) continue;
                for (var position = 0; position < candidates.Count && !absorbed; position++)
                {
                    var other = candidates[position];
                    var candidate = groups[other].Ids;
                    // Distinct groups of equal size never contain each other; the signature rejects most others at once.
                    absorbed = candidate.Length < group.Length && (signatures[other] & ~signatures[index]) == 0 && Contains(marks, index + 1, candidate);
                }
            }

            if (!absorbed) kept.Add(groups[index].Tags);
        }

        return kept.Count == 0 ? null : kept;
    }

    private static bool ContainsAny(HashSet<string> set, List<string> tags)
    {
        foreach (var tag in tags)
            if (set.Contains(tag)) return true;
        return false;
    }

    private static bool Contains(int[] marks, int mark, int[] tags)
    {
        foreach (var id in tags)
            if (marks[id] != mark) return false;
        return true;
    }

    private static string CanonicalTypeName(string typeName) => typeName switch
    {
        "Record" => "record",
        "Nothing" => "nothing",
        "Boolean" => "boolean",
        "Number" => "number",
        "Percentage" => "percentage",
        "Vector" => "vector",
        "Point" => "point",
        "Series" => "series",
        "Range" => "range",
        "Message" => "message",
        "Handler" => "handler",
        "Tag" => "tag",
        "Text" => "text",
        "List" => "list",
        "Map" => "map",
        "Dice" => "dice",
        "Bytes" => "bytes",
        _ => typeName
    };

    private string BindTypeName(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        // A handle type keeps its `@` and `~`; a union joins its alternatives with `|`, such as `@Drone|:Number`.
        if (c.Is(HandleTypeName) || c.PeekSignificant(1).Kind == OperatorCollectionUnion)
        {
            var alternatives = new StringBuilder();
            while (true)
            {
                alternatives.Append(c.Is(HandleTypeName) ? c.Advance().Text : CanonicalTypeName(c.Expect(TypeName).Text[1..]));
                if (!c.Match(OperatorCollectionUnion)) return alternatives.ToString();
                c.SkipNewLines();
                alternatives.Append('|');
            }
        }

        var typeName = c.Expect(TypeName).Text[1..];
        if (!c.Is(LeftParen)) return CanonicalTypeName(typeName);

        // Only a quantity type owns a parenthesized unit, such as `:Quantity(m/s)`.
        c.Advance();
        c.SkipNewLines();
        var unit = new StringBuilder();
        var depth = 0;
        while (!c.Is(RightParen) || depth > 0)
        {
            if (c.Is(LeftParen)) depth++;
            else if (c.Is(RightParen)) depth--;
            var part = c.Advance();
            unit.Append(part.Kind == SuperscriptInteger ? part.Text == "2" ? "²" : "³" : part.Text);
            c.SkipNewLines();
        }

        c.Expect(RightParen);
        return $"quantity:{unit}";
    }

    private List<GesStatementNode> BindBlock(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        var statements = new List<GesStatementNode>();
        c.Expect(LeftBrace);
        c.SkipSeparators();
        while (c.Node is { } statement)
        {
            c.TakeNode();
            statements.Add(BindStatement(statement));
            c.SkipSeparators();
        }

        c.Expect(RightBrace);
        return statements;
    }

    private GesStatementNode BindStatement(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        switch (node.Kind)
        {
            case NodeKind.AssemblyBlock:
            {
                c.ExpectWord("asm");
                var block = BindAssemblyBlock(ref c, null);
                return Merge(new GesAssemblyStatementNode(block), block);
            }
            case NodeKind.SeededRandomStatement:
            {
                var start = c.Expect(KeywordRandom);
                c.SkipNewLines();
                c.Expect(With);
                c.SkipNewLines();
                var seed = Expression(ref c);
                var body = BindBody(ref c);
                return Set(new GesSeededRandomStatementNode(seed, body), start, c.Previous);
            }
            case NodeKind.EmitStatement or NodeKind.PublishStatement:
            {
                var start = c.Advance();
                var kind = start.Kind == Emit ? GesPublishStatementKind.Emit : GesPublishStatementKind.Publish;
                c.SkipNewLines();
                // A statement's delayed send starts at `after`; a send expression as the sent value, as in
                // `emit emit Done()`, starts with its own keyword and binds as an ordinary value.
                if (c.IsNode(NodeKind.SendExpression) && c.Current.Kind is not (Emit or Publish))
                {
                    var send = BindSend(c.TakeNode(), kind, start);
                    return Set(new GesExpressionStatementNode(send), start, c.Previous);
                }

                var message = Expression(ref c);
                var tags = OptionalTags(ref c);
                return Set(new GesPublishStatementNode(kind, message, tags), start, c.Previous);
            }
            case NodeKind.LetStatement:
            {
                var start = c.Expect(Let);
                var identifier = c.Expect(Identifier).Text;
                c.Expect(Be);
                var expression = AssemblyOrExpression(ref c, identifier, allowSend: true);
                return Set(new GesLetStatementNode(identifier, expression), start, c.Previous);
            }
            case NodeKind.IfStatement: return BindIf(node);
            case NodeKind.ForStatement: return BindFor(node, nested: false);
            case NodeKind.ExpressionStatement:
            {
                var expression = Expression(ref c);
                return Merge(new GesExpressionStatementNode(expression), expression);
            }
            default: throw Mismatch(node.FirstToken);
        }
    }

    private IReadOnlyList<GesExpressionNode> OptionalTags(ref Cursor c)
    {
        if (!c.IsNode(NodeKind.TagClause)) return [];
        var t = new Cursor(this, c.TakeNode());
        t.Expect(With);
        var tags = new List<GesExpressionNode>();
        do
        {
            t.SkipNewLines();
            tags.Add(Expression(ref t));
            t.SkipNewLines();
        }
        while (t.Match(Comma));

        return tags;
    }

    private GesIfStatementNode BindIf(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        var start = c.Expect(If);
        var conditions = new List<GesIfConditionNode>();
        do
        {
            c.SkipNewLines();
            if (c.IsNode(NodeKind.Block) && conditions.Count > 0) break;
            var condition = new Cursor(this, c.TakeNode(NodeKind.IfCondition));
            var conditionStart = condition.Current;
            string? binding = null;
            if (condition.Match(Let))
            {
                binding = condition.Expect(Identifier).Text;
                condition.Expect(Be);
            }

            conditions.Add(Set(new GesIfConditionNode(Expression(ref condition), binding), conditionStart, condition.Previous));
            c.SkipNewLines();
        }
        while (c.Match(Semicolon));

        var thenBody = BindBody(ref c);
        GesStatementBodyNode? elseBody = null;
        c.SkipNewLines();
        if (c.IsNode(NodeKind.ElseClause))
        {
            var e = new Cursor(this, c.TakeNode());
            e.Expect(Else);
            elseBody = BindBody(ref e);
        }

        return Set(new GesIfStatementNode(conditions, thenBody, elseBody), start, c.Previous);
    }

    private GesForStatementNode BindFor(GesSyntaxNode node, bool nested)
    {
        var c = new Cursor(this, node);
        // A nested clause starts with `and`; its range starts at the last token before its bindings, as in the parser.
        c.Advance();
        if (nested) c.SkipNewLines();
        var start = c.Previous;
        var (identifier, bindings) = BindingNames(ref c);
        c.SkipNewLines();
        GesIterationSourceNode source;
        if (c.Match(In))
        {
            c.SkipNewLines();
            var sourceStart = c.Current;
            source = Set(new GesCollectionIterationSourceNode(Expression(ref c)), sourceStart, c.Previous);
        }
        else
        {
            source = BindRangeSource(c.TakeNode(NodeKind.RangeExpression));
        }

        c.SkipNewLines();
        GesStatementBodyNode body;
        if (c.IsNode(NodeKind.ForStatement))
        {
            var inner = BindFor(c.TakeNode(), nested: true);
            body = Set(new GesStatementBodyNode(false, [inner]), start, c.Previous);
        }
        else body = BindBody(ref c);

        return Set(new GesForStatementNode(identifier, source, body) { BindingNames = bindings }, start, c.Previous);
    }

    private GesRangeIterationSourceNode BindRangeSource(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        var from = c.Advance();
        return Set(new GesRangeIterationSourceNode(BindRangeCore(ref c)), from, c.Previous);
    }

    private GesStatementBodyNode BindBody(ref Cursor c)
    {
        var start = c.Current;
        c.SkipNewLines();
        if (c.IsNode(NodeKind.Block))
        {
            var statements = BindBlock(c.TakeNode());
            return Set(new GesStatementBodyNode(true, statements), start, c.Previous);
        }

        var statement = BindStatement(c.TakeNode());
        return Set(new GesStatementBodyNode(false, [statement]), start, c.Previous);
    }

    private (string Identifier, IReadOnlyList<string> Names) BindingNames(ref Cursor c)
    {
        var identifier = c.Expect(Identifier).Text;
        if (!c.Is(Comma)) return (identifier, []);
        var names = new List<string> { identifier };
        while (c.Match(Comma))
        {
            c.SkipNewLines();
            names.Add(c.Expect(Identifier).Text);
        }

        return (identifier, names);
    }

    private GesExpressionNode AssemblyOrExpression(ref Cursor c, string output, bool predicate = false, bool allowSend = false)
    {
        if (!c.IsNode(NodeKind.AssemblyBlock)) return Expression(ref c);
        var a = new Cursor(this, c.TakeNode());
        a.ExpectWord("asm");
        var block = BindAssemblyBlock(ref a, output) with { Predicate = predicate, AllowSend = allowSend };
        return Merge(new GesAssemblyExpressionNode(block), block);
    }
}
