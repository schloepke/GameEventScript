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

    // Parses one source and binds its syntax tree; syntax errors fail the parse phase with their diagnostics.
    internal static GesParsedScript Parse(string text, string? sourceName = null, uint? sourceId = null)
    {
        var tree = GesSyntaxParser.Parse(text);
        if (tree.Diagnostics.Count == 0) return Bind(tree, sourceName, sourceId);

        var name = string.IsNullOrWhiteSpace(sourceName) ? "UnknownSource" : sourceName!;
        var diagnostics = new List<GesDiagnostic>(tree.Diagnostics.Count);
        foreach (var diagnostic in tree.Diagnostics)
        {
            var location = new GesSourceLocation(name, diagnostic.Line, diagnostic.Column, diagnostic.EndLine, diagnostic.EndColumn, diagnostic.ModuleName, sourceId);
            var code = diagnostic.Code == GesSyntaxDiagnostic.SourceNestingExceeded ? GesDiagnosticCodes.ParseSourceNestingExceeded : GesDiagnosticCodes.ParseSyntax;
            diagnostics.Add(new GesDiagnostic(GesDiagnosticPhase.Parse, code, diagnostic.Message, SourceLocation: location, ProgramName: diagnostic.ModuleName));
        }

        throw new GesCompileException(diagnostics);
    }

    // The tree must come from a source without syntax errors.
    internal static GesParsedScript Bind(GesSyntaxTree tree, string? sourceName = null, uint? sourceId = null)
        => new GesSyntaxBinder(tree ?? throw new ArgumentNullException(nameof(tree)), sourceName, sourceId).BindScript();

    // Reads the elements of one node: its own tokens and its children, in source order.
    private struct Cursor
    {
        private readonly IReadOnlyList<GesToken> _tokens;
        private readonly IReadOnlyList<GesSyntaxNode> _children;
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

        internal readonly bool IsWord(string word) => Is(Identifier) && string.Equals(_tokens[_position].Text, word, StringComparison.Ordinal);

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
        var name = c.Expect(Identifier).Text;
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
        while (true)
        {
            c.SkipNewLines();
            if (!c.IsNode(NodeKind.HandlerTagFilter)) break;
            BindTagFilter(c.TakeNode(), requiredTags, excludedTags);
        }

        c.SkipNewLines();
        var statements = BindBlock(c.TakeNode(NodeKind.Block));
        return Set(new GesEventHandlerNode(message, dispatchKind, parameters, statements, requiredTags, excludedTags), start, c.Previous);
    }

    private void BindTagFilter(GesSyntaxNode node, List<string> requiredTags, List<string> excludedTags)
    {
        var c = new Cursor(this, node);
        var tags = c.Advance().Kind == Matching ? requiredTags : excludedTags;
        do
        {
            c.SkipNewLines();
            var normalized = GameEventScriptMessage.NormalizeTagName(c.Expect(Tag).Text[1..]);
            if (normalized.Length > 0 && !tags.Contains(normalized)) tags.Add(normalized);
            c.SkipNewLines();
        }
        while (c.Match(Comma));
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
        _ => typeName
    };

    private string BindTypeName(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        var typeName = c.Expect(TypeName).Text[1..];
        if (!c.Is(LeftParen)) return CanonicalTypeName(typeName);

        // Only a quantity type owns a parenthesized unit, such as `:Quantity(m/s)`.
        c.Advance();
        c.SkipNewLines();
        var unit = new StringBuilder();
        while (!c.Is(RightParen))
        {
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
                if (c.IsNode(NodeKind.SendExpression))
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
