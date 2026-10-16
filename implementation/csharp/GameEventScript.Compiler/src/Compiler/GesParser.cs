// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GameEventScript.Api;
using GameEventScript.Runtime;
using static GameEventScript.Compiler.GesTokenKind;

namespace GameEventScript.Compiler;

internal sealed partial class GesParser
{
    private const double SquareRootExponent = 0.5d;
    private const double CubeRootExponent = 0.3333333333333333333333333333d;

    public static GesParsedScript Parse(string script, string? sourceName = null, uint? sourceId = null)
    {
        _ = script ?? throw new ArgumentNullException(nameof(script));

        var reader = new GesTokenReader(new GesLexer(script));
        return new GesParser(reader, sourceName, sourceId).ParseScript();
    }

    private sealed class GameEventScriptParseException(string message, int line, int column) : Exception($"{message} (line {line}, col {column})")
    {
        public int Line { get; } = line;
        public int Column { get; } = column;
        public int EndLine { get; } = line;
        public int EndColumn { get; } = column;

        public GameEventScriptParseException(string message, GesToken token) : this(message, token.Line, token.Column, token.EndLine, token.EndColumn)
        {
        }

        private GameEventScriptParseException(string message, int line, int column, int endLine, int endColumn) : this(message, line, column)
        {
            EndLine = endLine;
            EndColumn = endColumn;
        }
    }

    private readonly GesTokenReader _reader;
    private readonly string? _requestedSourceName;
    private readonly uint? _sourceId;
    private readonly List<GesDiagnostic> _errors;
    private string? _moduleName;
    private string? _sourceName;
    private int _expressionNesting;
    private int _statementNesting;
    private int _delimiterDepth;
    private int? _forSourceDepth;
    private int? _choiceConditionDepth;

    private GesParser(GesTokenReader reader, string? sourceName, uint? sourceId)
    {
        _reader = reader;
        _requestedSourceName = sourceName;
        _sourceId = sourceId;
        _errors = [];
    }

    private string ModuleName => _moduleName ?? string.Empty;

    private string SourceName => _sourceName ??= string.IsNullOrWhiteSpace(_requestedSourceName) ? "UnknownSource" : _requestedSourceName;

    private GesSourceLocation CreateRange(GesToken token) => new(SourceName, token.Line, token.Column, token.EndLine, token.EndColumn, ModuleName, _sourceId);

    private GesSourceLocation CreateRange(GesToken start, GesToken end) => new(SourceName, start.Line, start.Column, end.EndLine, end.EndColumn, ModuleName, _sourceId);

    private GesSourceLocation? MergeRanges(GesScriptNode? first, GesScriptNode? last = null)
    {
        var start = first?.SourceRange;
        var end = (last ?? first)?.SourceRange;
        if (start is null || end is null || start.Line is null || start.Column is null) return null;

        var endLine = end.EndLine ?? end.Line;
        var endColumn = end.EndColumn ?? end.Column;
        return endLine is null || endColumn is null
            ? null
            : new GesSourceLocation(SourceName, start.Line, start.Column, endLine, endColumn, ModuleName, _sourceId);
    }

    private T WithRange<T>(T node, GesToken startToken) where T : GesScriptNode
        => WithRange(node, CreateRange(startToken, Previous));

    private T WithRange<T>(T node, GesToken startToken, GesToken endToken) where T : GesScriptNode
        => WithRange(node, CreateRange(startToken, endToken));

    private T WithRange<T>(T node, GesScriptNode? first, GesScriptNode? last = null) where T : GesScriptNode
        => WithRange(node, MergeRanges(first, last));

    private T WithRange<T>(T node, GesSourceLocation? location) where T : GesScriptNode
    {
        var depth = GesSourceNesting.Measure(node);
        if (depth > GesSourceNesting.MaximumExpressionDepth) ThrowNestingLimit(location);
        node.SourceRange = location;
        node.SourceNesting = depth;
        return node;
    }

    private T CheckNesting<T>(T node) where T : GesScriptNode
        => WithRange(node, node.SourceRange);

    private void ThrowNestingLimit(GesSourceLocation? location)
        => throw new GesCompileException([new GesDiagnostic(
            GesDiagnosticPhase.Parse,
            GesDiagnosticCodes.ParseSourceNestingExceeded,
            "Source exceeds the portable expression or statement nesting limit.",
            SourceLocation: location ?? CreateRange(Current),
            ProgramName: ModuleName)]);

    private GesExpressionNode ApplyIsNegation(GesExpressionNode expression, bool negated)
        => negated
            ? WithRange(new GesUnaryExpressionNode(GesUnaryOperator.Not, expression), expression)
            : expression;

    private GesParsedScript ParseScript()
    {
        var constantDefinitions = new List<GesConstantDefinitionNode>();
        var typeDefinitions = new List<GesTypeDefinitionNode>();
        var predicateDefinitions = new List<GesPredicateDefinitionNode>();
        var functionDefinitions = new List<GesFunctionDefinitionNode>();
        var handlers = new List<GesEventHandlerNode>();
        SkipStatementSeparators();
        try
        {
            ParseOptionalModuleDeclaration();
        }
        catch (GameEventScriptParseException ex)
        {
            AddParseError(ex);
            SynchronizeTopLevel();
        }

        SkipStatementSeparators();
        while (!Is(EndOfFile))
        {
            try
            {
                if (Is(Illegal))
                {
                    var token = Advance();
                    throw new GameEventScriptParseException($"Illegal token '{token.Text}'", token);
                }

                if (Match(Constant))
                {
                    constantDefinitions.Add(ParseConstantDefinition());
                }
                else if (Match(Record))
                {
                    typeDefinitions.Add(ParseTypeDefinition());
                }
                else if (Match(Predicate))
                {
                    predicateDefinitions.Add(ParsePredicateDefinition());
                }
                else if (Match(Function))
                {
                    functionDefinitions.Add(ParseFunctionDefinition());
                }
                else
                {
                    handlers.Add(ParseEventHandler());
                }

                RequireHandlerSeparatorOrEndOfFile();
                SkipStatementSeparators();
            }
            catch (GameEventScriptParseException ex)
            {
                AddParseError(ex);
                SynchronizeTopLevel();
            }
        }

        if (_errors.Count > 0) throw new GesCompileException(_errors);

        var module = new GesParsedScript(ModuleName, SourceName, constantDefinitions, typeDefinitions, predicateDefinitions, functionDefinitions, handlers);
        var firstToken = _reader.FirstToken;
        if (firstToken.Kind == EndOfFile) return module with { SourceRange = CreateRange(firstToken) };

        return module with { SourceRange = CreateRange(firstToken, _reader.LastNonEofToken) };
    }

    private void ParseOptionalModuleDeclaration()
    {
        if (!Match(Module)) return;

        SkipNewLines();
        ThrowIfIllegalToken();
        if (!Is(Identifier)) throw new GameEventScriptParseException($"Expected module name but found {Current.Kind}", Current.Line, Current.Column);

        var builder = new StringBuilder(Advance().Text);
        while (Match(Dot))
        {
            if (!Is(Identifier)) throw new GameEventScriptParseException($"Expected module segment but found {Current.Kind}", Current);
            builder.Append('.').Append(Advance().Text);
        }

        _moduleName = builder.ToString();
        if (!GesText.IsModuleName(_moduleName)) throw new GameEventScriptParseException($"Invalid module identifier '{_moduleName}'", Previous);
    }

    private GesConstantDefinitionNode ParseConstantDefinition()
    {
        var startToken = Previous;
        var nameToken = Expect(ConstantReference);
        Expect(Be);
        SkipNewLines();
        var value = ParseUnaryExpression();
        if (!IsConstantValue(value)) throw new GameEventScriptParseException("Constant initializers must be scalar literals.", nameToken);
        return WithRange(new GesConstantDefinitionNode(nameToken.Text[1..], value), startToken, Previous);
    }

    private static bool IsConstantValue(GesExpressionNode expression)
        => expression is GesBooleanLiteralExpressionNode or GesNothingLiteralExpressionNode or GesIntegerLiteralExpressionNode or GesFloatLiteralExpressionNode or
            GesPercentageLiteralExpressionNode or GesUnitIntegerLiteralExpressionNode or GesUnitFloatLiteralExpressionNode or GesTextLiteralExpressionNode or GesTagLiteralExpressionNode ||
           expression is GesUnaryExpressionNode
           {
               Operator: GesUnaryOperator.Negate,
               Operand: GesIntegerLiteralExpressionNode or GesFloatLiteralExpressionNode or GesPercentageLiteralExpressionNode or GesUnitIntegerLiteralExpressionNode or GesUnitFloatLiteralExpressionNode
           };

    private GesTypeDefinitionNode ParseTypeDefinition()
    {
        var startToken = Previous;
        var name = ParseTypeName();
        Expect(As);
        SkipNewLines();
        Expect(LeftBrace);
        SkipStatementSeparators();

        var fields = new List<GesTypeFieldDefinitionNode>();
        while (!Is(RightBrace))
        {
            fields.Add(ParseTypeFieldDefinition());
            if (Match(Comma))
            {
                SkipStatementSeparators();
                continue;
            }

            SkipStatementSeparators();
        }

        Expect(RightBrace);
        return WithRange(new GesTypeDefinitionNode(name, fields), startToken);
    }

    private GesTypeFieldDefinitionNode ParseTypeFieldDefinition()
    {
        var startToken = Current;
        var isUnlabeledConstructorParameter = Match(Underscore);
        if (isUnlabeledConstructorParameter) SkipNewLines();

        var name = ExpectIdentifier();
        Expect(Colon);
        SkipNewLines();
        var typeName = ParseTypeName();
        SkipNewLines();

        GesExpressionNode? minimumExpression = null;
        GesExpressionNode? maximumExpression = null;
        GesExpressionNode? computedExpression = null;

        if (MatchWord("clamped"))
        {
            ExpectWord("between");
            minimumExpression = ParseEqualityExpression();
            Expect(OperatorAnd);
            maximumExpression = ParseEqualityExpression();
            SkipNewLines();
        }

        if (MatchWord("computed"))
        {
            ExpectWord("by");
            computedExpression = ParseAssemblyOrExpression(name);
        }

        if (isUnlabeledConstructorParameter && computedExpression is not null) throw new GameEventScriptParseException("Computed record fields cannot be constructor parameters.", startToken);

        var constructorLabel = computedExpression is null
            ? isUnlabeledConstructorParameter
                ? GesMessageSignature.UnlabeledParameterName
                : name
            : null;

        return WithRange(new GesTypeFieldDefinitionNode(name, typeName, minimumExpression, maximumExpression, computedExpression, constructorLabel), startToken);
    }

    private GesPredicateDefinitionNode ParsePredicateDefinition()
    {
        var startToken = Previous;
        var name = ExpectIdentifier();
        var parameters = ParseDefinitionParameters();
        Expect(Be);
        SkipNewLines();
        var expression = ParseAssemblyOrExpression(name, predicate: true);
        return WithRange(new GesPredicateDefinitionNode(name, parameters, expression), startToken);
    }

    private GesFunctionDefinitionNode ParseFunctionDefinition()
    {
        var startToken = Previous;
        var name = ExpectIdentifier();
        var parameters = ParseDefinitionParameters();
        Expect(Be);
        SkipNewLines();
        var expression = ParseAssemblyOrExpression(name);
        return WithRange(new GesFunctionDefinitionNode(name, parameters, expression), startToken);
    }

    private IReadOnlyList<GesParameterNode> ParseDefinitionParameters()
    {
        var parameters = new List<GesParameterNode>();
        Expect(LeftParen);
        if (!Is(RightParen))
        {
            parameters.Add(ParseParameter());
            while (Match(Comma)) parameters.Add(ParseParameter());
        }

        Expect(RightParen);
        return parameters;
    }

    private GesParameterNode ParseParameter(bool allowDeclaredType = true)
    {
        var startToken = Current;
        if (Match(Underscore))
        {
            SkipNewLines();
            var localName = ExpectIdentifier();
            var declaredType = allowDeclaredType ? ParseOptionalParameterDeclaredType() : null;
            return WithRange(new GesParameterNode(null, localName, declaredType), startToken);
        }

        var name = ExpectIdentifier();
        var typeName = allowDeclaredType ? ParseOptionalParameterDeclaredType() : null;
        return WithRange(new GesParameterNode(name, name, typeName), startToken);
    }

    private string? ParseOptionalParameterDeclaredType()
    {
        SkipNewLines();
        if (!Match(As)) return null;

        SkipNewLines();
        return ParseTypeName();
    }

    private GesEventHandlerNode ParseEventHandler()
    {
        var startToken = Current;
        Expect(On);
        SkipNewLines();
        var message = ParseEventHandlerMessageName();

        var dispatchKind = GesEventHandlerDispatchKind.ExactSignature;
        var parameters = new List<GesParameterNode>();
        if (Match(As))
        {
            SkipNewLines();
            var messageToken = Current;
            var messageLocalName = ExpectIdentifier();
            dispatchKind = GesEventHandlerDispatchKind.MessageName;
            parameters.Add(WithRange(new GesParameterNode(
                GesSystemEndpoints.MessageArgumentName,
                messageLocalName,
                "message"), messageToken));
        }
        else if (Match(LeftParen))
        {
            if (!Is(RightParen))
            {
                parameters.Add(ParseParameter());
                while (Match(Comma)) parameters.Add(ParseParameter());
            }

            Expect(RightParen);
        }

        var requiredTags = new List<string>();
        var excludedTags = new List<string>();
        ParseOptionalHandlerTagFilters(requiredTags, excludedTags);

        SkipNewLines();
        Expect(LeftBrace);
        var statements = ParseStatementsUntil(RightBrace);
        Expect(RightBrace);

        return WithRange(new GesEventHandlerNode(message, dispatchKind, parameters, statements, requiredTags, excludedTags), startToken);
    }

    private string ParseEventHandlerMessageName()
    {
        ThrowIfIllegalToken();

        if (Is(Message)) return Advance().Text;

        if (Current.Kind == Identifier &&
            GesSystemEndpoints.IsSystemEndpointName(Current.Text))
        {
            return Advance().Text;
        }

        var token = Current;
        throw new GameEventScriptParseException($"Expected {Message} or system endpoint but found {token.Kind}", token);
    }

    private void ParseOptionalHandlerTagFilters(List<string> requiredTags, List<string> excludedTags)
    {
        while (true)
        {
            SkipNewLines();
            if (Match(Matching))
            {
                ParseHandlerTagList(requiredTags);
                continue;
            }

            if (Match(Without))
            {
                ParseHandlerTagList(excludedTags);
                continue;
            }

            return;
        }
    }

    private void ParseHandlerTagList(List<string> tags)
    {
        do
        {
            SkipNewLines();
            var tagToken = Expect(Tag);
            var tag = tagToken.Text[1..];
            var normalized = GameEventScriptMessage.NormalizeTagName(tag);
            if (normalized.Length > 0 && !ContainsTag(tags, normalized)) tags.Add(normalized);

            SkipNewLines();
        }
        while (Match(Comma));
    }

    private static bool ContainsTag(IReadOnlyList<string> tags, string normalized)
    {
        for (var index = 0; index < tags.Count; index++)
        {
            if (string.Equals(tags[index], normalized, StringComparison.Ordinal)) return true;
        }

        return false;
    }

    private IReadOnlyList<GesStatementNode> ParseStatementsUntil(GesTokenKind closingKind)
    {
        var statements = new List<GesStatementNode>();
        SkipStatementSeparators();

        while (!Is(closingKind))
        {
            if (Is(EndOfFile))
            {
                var token = Current;
                AddParseError(new GameEventScriptParseException($"Expected '{closingKind}' before end of input", token.Line, token.Column));
                return statements;
            }

            try
            {
                statements.Add(ParseStatement());
                RequireStatementSeparatorOrClosing(closingKind);
            }
            catch (GameEventScriptParseException ex)
            {
                AddParseError(ex);
                if (!SynchronizeStatement(closingKind)) return statements;
            }
        }

        return statements;
    }

    private GesStatementNode ParseStatement()
    {
        if (MatchWord("asm"))
        {
            var block = ParseAssemblyBlock(null);
            return WithRange(new GesAssemblyStatementNode(block), block);
        }

        if (IsSeededRandomStatementStart()) return ParseSeededRandomStatement();

        if (Match(Emit)) return ParsePublishStatement(GesPublishStatementKind.Emit);

        if (Match(Publish)) return ParsePublishStatement(GesPublishStatementKind.Publish);

        if (Match(Let)) return ParseLetStatement();

        if (Match(If)) return ParseIfStatement();

        if (Match(For)) return ParseForStatement();

        var expression = ParseExpression();
        return WithRange(new GesExpressionStatementNode(expression), expression);
    }

    private GesStatementNode ParsePublishStatement(GesPublishStatementKind kind)
    {
        var startToken = Previous;
        SkipNewLines();
        if (IsWord("after"))
        {
            var send = ParseSendExpression(kind, startToken);
            return WithRange(new GesExpressionStatementNode(send), startToken);
        }
        var messageExpression = ParsePublishMessageExpression();
        var tagExpressions = ParseOptionalTagExpressions();
        return WithRange(new GesPublishStatementNode(kind, messageExpression, tagExpressions), startToken);
    }

    private GesExpressionNode ParseSendExpression(GesPublishStatementKind kind, GesToken startToken)
    {
        SkipNewLines();
        GesExpressionNode? delay = null;
        if (MatchWord("after")) delay = ParseExpression();
        var message = Current.Kind == Message ? ParseMessageLiteralExpressionCore() : ParseUnaryExpression();
        return WithRange(new GesSendExpressionNode(kind, message, ParseOptionalTagExpressions(resultSend: true), delay), startToken);
    }

    private GesExpressionNode ParsePublishMessageExpression()
    {
        if (Current.Kind == Message) return ParseMessageLiteralExpressionCore();

        return ParseExpression();
    }

    private IReadOnlyList<GesExpressionNode> ParseOptionalTagExpressions(bool resultSend = false)
    {
        if (!Match(With)) return [];

        var tags = new List<GesExpressionNode>();
        do
        {
            SkipNewLines();
            tags.Add(resultSend ? ParseUnaryExpression() : ParseExpression());
        }
        while (Match(Comma));

        return tags;
    }

    private GesArgumentNode ParseArgument()
    {
        var startToken = Current;
        if (IsArgumentLabelStart())
        {
            var label = ExpectArgumentLabel();
            SkipNewLines();
            if (Match(Colon))
            {
                SkipNewLines();
                var expression = ParseExpression();
                return WithRange(new GesArgumentNode(label, expression), startToken);
            }
        }

        var value = ParseExpression();
        return WithRange(new GesArgumentNode(null, value), startToken);
    }

    private GesArgumentListNode ParseArgumentListAfterLeftParen()
    {
        SkipNewLines();
        if (Match(RightParen)) return GesArgumentListNode.Empty;

        var arguments = ParseArgumentListAfterFirstArgument(ParseArgument(), RightParen);
        Expect(RightParen);
        return arguments;
    }

    private GesArgumentListNode ParseArgumentListAfterFirstArgument(GesArgumentNode firstArgument, GesTokenKind closingKind)
    {
        var arguments = new List<GesArgumentNode> { firstArgument };
        while (true)
        {
            SkipNewLines();
            if (Match(Comma))
            {
                SkipNewLines();
                arguments.Add(ParseArgument());
                continue;
            }

            if (!Is(closingKind) && IsArgumentLabelStart())
            {
                arguments.Add(ParseArgument());
                continue;
            }

            return new GesArgumentListNode(arguments);
        }
    }

    private GesArgumentListNode ParseUngroupedLabeledArgumentList()
    {
        if (!IsArgumentLabelStart()) return GesArgumentListNode.Empty;

        var arguments = new List<GesArgumentNode>();
        do
        {
            arguments.Add(ParseArgument());
        }
        while (IsArgumentLabelStart());

        return new GesArgumentListNode(arguments);
    }

    private GesArgumentListNode ParseOfArgumentList()
    {
        var arguments = new List<GesArgumentNode>();
        Expect(Of);
        SkipNewLines();
        arguments.Add(new GesArgumentNode(null, ParseEqualityExpression()));
        while (!IsForClauseStart() && Match(OperatorAnd))
        {
            SkipNewLines();
            arguments.Add(new GesArgumentNode(null, ParseEqualityExpression()));
        }

        return new GesArgumentListNode(arguments);
    }

    private GesLetStatementNode ParseLetStatement()
    {
        var startToken = Previous;
        var identifier = ExpectIdentifier();
        if (Match(Be))
        {
            var expression = ParseAssemblyOrExpression(identifier, allowSend: true);
            return WithRange(new GesLetStatementNode(identifier, expression), startToken);
        }
        var token = Current;
        throw new GameEventScriptParseException($"Expected {Be} but found {token.Kind}", token.Line, token.Column);
    }

    private GesIfStatementNode ParseIfStatement()
    {
        var startToken = Previous;
        var conditions = new List<GesIfConditionNode>();
        do
        {
            SkipNewLines();
            if (Is(LeftBrace) && conditions.Count > 0) break;
            var conditionStart = Current;
            string? binding = null;
            if (Match(Let))
            {
                binding = ExpectIdentifier();
                Expect(Be);
            }
            conditions.Add(WithRange(new GesIfConditionNode(ParseExpression(), binding), conditionStart));
            SkipNewLines();
        } while (Match(Semicolon));
        var thenBody = ParseStatementBody();

        GesStatementBodyNode? elseBody = null;
        if (IsElseClauseStart())
        {
            SkipNewLines();
            Expect(Else);
            elseBody = ParseStatementBody();
        }

        return WithRange(new GesIfStatementNode(conditions, thenBody, elseBody), startToken);
    }

    private GesForStatementNode ParseForStatement()
    {
        var startToken = Previous;
        var (identifier, bindings) = ParseBindingNames();
        SkipNewLines();
        GesIterationSourceNode source;
        var previousSourceDepth = _forSourceDepth;
        _forSourceDepth = _delimiterDepth;
        try
        {
            if (Match(In))
            {
                SkipNewLines();
                if (Current.Kind == Identifier && string.Equals(Current.Text, "from", StringComparison.Ordinal))
                {
                    throw new GameEventScriptParseException("Direct ranges are not allowed after 'in'; use 'for item from ... to ...' or iterate a range variable", Current.Line, Current.Column);
                }

                var sourceStart = Current;
                source = WithRange(new GesCollectionIterationSourceNode(ParseExpression()), sourceStart);
            }
            else if (MatchWord("from"))
            {
                var fromToken = Previous;
                source = WithRange(new GesRangeIterationSourceNode(ParseRangeExpressionCore()), fromToken);
            }
            else
            {
                var token = Current;
                throw new GameEventScriptParseException($"Expected {In} or 'from' but found {token.Text}", token.Line, token.Column);
            }
        }
        finally { _forSourceDepth = previousSourceDepth; }

        SkipNewLines();
        GesStatementBodyNode body;
        if (IsForClauseStart(requireSourceContext: false))
        {
            if (_statementNesting >= GesSourceNesting.MaximumStatementDepth) ThrowNestingLimit(CreateRange(Current));
            Advance();
            SkipNewLines();
            _statementNesting++;
            try { body = WithRange(new GesStatementBodyNode(false, [ParseForStatement()]), startToken); }
            finally { _statementNesting--; }
        }
        else body = ParseStatementBody();
        return WithRange(new GesForStatementNode(identifier, source, body) { BindingNames = bindings }, startToken);
    }

    private bool IsForClauseStart(bool requireSourceContext = true)
    {
        if (requireSourceContext && _forSourceDepth != _delimiterDepth || !Is(OperatorAnd)) return false;
        var offset = 1;
        if (_reader.PeekSignificant(offset++).Kind != Identifier) return false;
        while (_reader.PeekSignificant(offset).Kind == Comma)
        {
            offset++;
            if (_reader.PeekSignificant(offset++).Kind != Identifier) return false;
        }
        var token = _reader.PeekSignificant(offset);
        return token.Kind == In || token.Kind == Identifier && token.Text == "from";
    }

    private GesSeededRandomStatementNode ParseSeededRandomStatement()
    {
        var startToken = Current;
        Expect(KeywordRandom);
        SkipNewLines();
        Expect(With);
        SkipNewLines();
        var seedExpression = ParseExpression();
        var body = ParseStatementBody();
        return WithRange(new GesSeededRandomStatementNode(seedExpression, body), startToken);
    }

    private GesStatementBodyNode ParseStatementBody()
    {
        if (_statementNesting >= GesSourceNesting.MaximumStatementDepth) ThrowNestingLimit(CreateRange(Current));
        _statementNesting++;
        try { return ParseStatementBodyCore(); }
        finally { _statementNesting--; }
    }

    private GesStatementBodyNode ParseStatementBodyCore()
    {
        var startToken = Current;
        SkipNewLines();
        if (Match(LeftBrace))
        {
            var statements = ParseStatementsUntil(RightBrace);
            Expect(RightBrace);
            return WithRange(new GesStatementBodyNode(true, statements), startToken);
        }

        return WithRange(new GesStatementBodyNode(false, [ParseStatement()]), startToken);
    }

    private GesExpressionNode ParseExpression() => ParseGuardedChoiceExpression();

    private GesExpressionNode ParseGuardedChoiceExpression()
    {
        SkipNewLines();
        if (Match(When)) return ParseConditionFirstChoiceExpression();
        var expression = ParseImplicationExpression();
        if (Match(When))
        {
            var branches = new List<GesGuardedChoiceBranchNode>();
            var conditionExpression = ParseImplicationExpression();
            branches.Add(new GesGuardedChoiceBranchNode(expression, conditionExpression));

            while (Match(Comma))
            {
                SkipNewLines();
                if (Is(Otherwise)) break;

                Match(OperatorOr);
                SkipNewLines();

                var branchValue = ParseImplicationExpression();
                SkipNewLines();
                Expect(When);
                SkipNewLines();
                var branchCondition = ParseImplicationExpression();
                branches.Add(new GesGuardedChoiceBranchNode(branchValue, branchCondition));
            }

            SkipNewLines();
            Expect(Otherwise);
            SkipNewLines();
            var otherwiseExpression = ParseImplicationExpression();
            expression = WithRange(new GesGuardedChoiceExpressionNode(branches, otherwiseExpression), expression, otherwiseExpression);
        }

        return expression;
    }

    private GesExpressionNode ParseConditionFirstChoiceExpression()
    {
        var startToken = Previous;
        var branches = new List<GesGuardedChoiceBranchNode>();
        while (true)
        {
            SkipNewLines();
            var savedConditionDepth = _choiceConditionDepth;
            _choiceConditionDepth = _delimiterDepth;
            GesExpressionNode condition;
            try { condition = ParseImplicationExpression(); }
            finally { _choiceConditionDepth = savedConditionDepth; }
            SkipNewLines();
            ExpectWord("then");
            SkipNewLines();
            var value = ParseImplicationExpression();
            branches.Add(new GesGuardedChoiceBranchNode(value, condition));
            if (!Match(Comma)) break;
            SkipNewLines();
            if (Is(Otherwise)) break;
            Match(OperatorOr);
            SkipNewLines();
            Expect(When);
        }
        SkipNewLines();
        Expect(Otherwise);
        SkipNewLines();
        var fallback = ParseImplicationExpression();
        return WithRange(new GesGuardedChoiceExpressionNode(branches, fallback), startToken);
    }

    private GesExpressionNode ParseImplicationExpression()
    {
        var expression = ParseDefaultExpression();
        if (!Match(OperatorImplication)) return expression;

        SkipNewLines();
        if (_expressionNesting >= GesSourceNesting.MaximumExpressionDepth) ThrowNestingLimit(CreateRange(Current));
        _expressionNesting++;
        GesExpressionNode right;
        try { right = ParseImplicationExpression(); }
        finally { _expressionNesting--; }
        return WithRange(new GesBinaryExpressionNode(expression, GesBinaryOperator.Implies, right), expression, right);
    }

    private GesExpressionNode ParseDefaultExpression()
    {
        var expression = ParseCollectionUnionExpression();

        while (Match(Default))
        {
            SkipNewLines();
            var right = ParseCollectionUnionExpression();
            expression = WithRange(new GesBinaryExpressionNode(expression, GesBinaryOperator.Default, right), expression, right);
        }

        return expression;
    }

    private GesExpressionNode ParseCollectionUnionExpression()
    {
        var expression = ParseCollectionIntersectExpression();

        while (Match(OperatorCollectionUnion))
        {
            SkipNewLines();
            var right = ParseCollectionIntersectExpression();
            expression = WithRange(new GesBinaryExpressionNode(expression, GesBinaryOperator.Union, right), expression, right);
        }

        return expression;
    }

    private GesExpressionNode ParseCollectionIntersectExpression()
    {
        var expression = ParseCollectionZipExpression();

        while (Match(OperatorCollectionIntersect))
        {
            SkipNewLines();
            var right = ParseCollectionZipExpression();
            expression = WithRange(new GesBinaryExpressionNode(expression, GesBinaryOperator.Intersect, right), expression, right);
        }

        return expression;
    }

    private GesExpressionNode ParseCollectionZipExpression()
    {
        var expression = ParseOrExpression();

        while (Current.Kind == Zip)
        {
            Advance();
            SkipNewLines();
            var right = ParseOrExpression();
            expression = WithRange(new GesBinaryExpressionNode(expression, GesBinaryOperator.Zip, right), expression, right);
        }

        return expression;
    }

    private GesExpressionNode ParseOrExpression()
    {
        var expression = ParseXorExpression();

        while (Match(OperatorOr))
        {
            SkipNewLines();
            var right = ParseXorExpression();
            expression = WithRange(new GesBinaryExpressionNode(expression, GesBinaryOperator.Or, right), expression, right);
        }

        return expression;
    }

    private GesExpressionNode ParseXorExpression()
    {
        var expression = ParseAndExpression();

        while (Match(OperatorXor))
        {
            SkipNewLines();
            var right = ParseAndExpression();
            expression = WithRange(new GesBinaryExpressionNode(expression, GesBinaryOperator.Xor, right), expression, right);
        }

        return expression;
    }

    private GesExpressionNode ParseAndExpression()
    {
        var expression = ParseEqualityExpression();

        while (!IsForClauseStart() && Match(OperatorAnd))
        {
            SkipNewLines();
            var right = ParseEqualityExpression();
            expression = WithRange(new GesBinaryExpressionNode(expression, GesBinaryOperator.And, right), expression, right);
        }

        return expression;
    }

    private GesExpressionNode ParseEqualityExpression()
    {
        var expression = ParseMembershipExpression();

        while (Match(OperatorEqual, OperatorNotEqual))
        {
            var op = Previous.Kind switch
            {
                OperatorEqual => GesBinaryOperator.Equal,
                _ => GesBinaryOperator.NotEqual
            };
            SkipNewLines();
            var right = ParseMembershipExpression();
            expression = WithRange(new GesBinaryExpressionNode(expression, op, right), expression, right);
        }

        return expression;
    }

    private GesExpressionNode ParseMembershipExpression()
    {
        var expression = ParseTypeOperationExpression();

        while (true)
        {
            if (Match(Has))
            {
                SkipNewLines();
                ExpectValueWord();
                expression = WithRange(new GesUnaryExpressionNode(GesUnaryOperator.HasValue, expression), expression);
                continue;
            }

            if (Match(In))
            {
                SkipNewLines();
                if (IsValuesOfOperator())
                {
                    ExpectWord("values");
                    SkipNewLines();
                    Expect(Of);
                    SkipNewLines();
                    var container = ParseTypeOperationExpression();
                    expression = WithRange(new GesBinaryExpressionNode(expression, GesBinaryOperator.ContainsValue, container), expression, container);
                    continue;
                }

                var right = ParseTypeOperationExpression();
                expression = WithRange(new GesBinaryExpressionNode(expression, GesBinaryOperator.Contains, right), expression, right);
                continue;
            }

            if (Match(NotIn))
            {
                SkipNewLines();
                var right = ParseTypeOperationExpression();
                var membership = WithRange(new GesBinaryExpressionNode(expression, GesBinaryOperator.Contains, right), expression, right);
                expression = WithRange(new GesUnaryExpressionNode(GesUnaryOperator.Not, membership), membership);
                continue;
            }

            if (Match(Starts))
            {
                SkipNewLines();
                Expect(With);
                SkipNewLines();
                var right = ParseTypeOperationExpression();
                expression = WithRange(new GesBinaryExpressionNode(expression, GesBinaryOperator.StartsWith, right), expression, right);
                continue;
            }

            if (Match(Ends))
            {
                SkipNewLines();
                Expect(With);
                SkipNewLines();
                var right = ParseTypeOperationExpression();
                expression = WithRange(new GesBinaryExpressionNode(expression, GesBinaryOperator.EndsWith, right), expression, right);
                continue;
            }

            break;
        }

        return expression;
    }

    private bool IsValuesOfOperator()
    {
        var first = _reader.PeekSignificant(0);
        if (first.Kind != Identifier || !string.Equals(first.Text, "values", StringComparison.Ordinal)) return false;

        var second = _reader.PeekSignificant(1);
        return second.Kind == Of;
    }

    private GesDicePatternNode ParseDicePattern()
    {
        if (MatchWord("pair"))
        {
            var startToken = Previous;
            SkipNewLines();
            if (Match(Of))
            {
                SkipNewLines();
                return WithRange(new GesDiceCountPatternNode(2, ParsePatternFace()), startToken);
            }

            return WithRange(new GesDiceCountPatternNode(2, null), startToken);
        }

        if (MatchWord("three")) return ParseOfPattern(3);

        if (MatchWord("four")) return ParseOfPattern(4);

        if (MatchWord("five")) return ParseOfPattern(5);

        if (MatchWord("six")) return ParseOfPattern(6);

        if (MatchWord("seven")) return ParseOfPattern(7);

        if (MatchWord("full"))
        {
            var startToken = Previous;
            SkipNewLines();
            ExpectWord("house");
            return WithRange(new GesDiceFullHousePatternNode(), startToken);
        }

        if (MatchWord("straight")) return WithRange(new GesDiceStraightPatternNode(), Previous);

        var token = Current;
        throw new GameEventScriptParseException($"Expected dice pattern but found {token.Text}", token.Line, token.Column);
    }

    private GesDicePatternNode ParseOfPattern(int count)
    {
        var startToken = Previous;
        SkipNewLines();
        Expect(Of);
        SkipNewLines();

        if (MatchWord("a"))
        {
            SkipNewLines();
            ExpectWord("kind");
            return WithRange(new GesDiceCountPatternNode(count, null), startToken);
        }

        return WithRange(new GesDiceCountPatternNode(count, ParsePatternFace()), startToken);
    }

    private GesExpressionNode ParsePatternFace() => ParseUnaryExpression();

    private GesExpressionNode ParseTypeOperationExpression()
    {
        var expression = ParseRelationalExpression();

        while (true)
        {
            if (Match(GesTokenKind.Is))
            {
                SkipNewLines();
                var negated = Match(OperatorNot);
                if (negated) SkipNewLines();

                if (IsExtensionCallStart())
                {
                    var (extensionName, functionName, _) = ParseExtensionSymbol();
                    expression = ApplyIsNegation(WithRange(new GesExtensionPredicateExpressionNode(expression, extensionName, functionName), expression), negated);
                    continue;
                }

                if (Match(Numeric))
                {
                    expression = ApplyIsNegation(WithRange(new GesTypeCheckExpressionNode(expression, "numeric"), expression), negated);
                    continue;
                }

                if (MatchWord("integer"))
                {
                    expression = ApplyIsNegation(WithRange(new GesTypeCheckExpressionNode(expression, "numeric:integer"), expression), negated);
                    continue;
                }

                if (MatchWord("fractional"))
                {
                    expression = ApplyIsNegation(WithRange(new GesTypeCheckExpressionNode(expression, "numeric:fractional"), expression), negated);
                    continue;
                }

                if (Match(Nothing))
                {
                    expression = ApplyIsNegation(WithRange(new GesNothingCheckExpressionNode(expression), expression), negated);
                    continue;
                }

                if (Is(TypeName))
                {
                    var typeName = ParseTypeName();
                    expression = ApplyIsNegation(WithRange(new GesTypeCheckExpressionNode(expression, typeName), expression), negated);
                    continue;
                }

                if (Match(Empty))
                {
                    expression = ApplyIsNegation(WithRange(new GesUnaryExpressionNode(GesUnaryOperator.Empty, expression), expression), negated);
                    continue;
                }

                if (MatchWord("between"))
                {
                    SkipNewLines();
                    var minimum = ParseRelationalComparisonOperand();
                    SkipNewLines();
                    Expect(OperatorAnd);
                    SkipNewLines();
                    var maximum = ParseRelationalComparisonOperand();
                    expression = ApplyIsNegation(WithRange(new GesBetweenExpressionNode(expression, minimum, maximum), expression, maximum), negated);
                    continue;
                }

                if (MatchWord("at"))
                {
                    SkipNewLines();
                    if (MatchWord("least"))
                    {
                        SkipNewLines();
                        var right = ParseRelationalComparisonOperand();
                        expression = ApplyIsNegation(WithRange(new GesBinaryExpressionNode(expression, GesBinaryOperator.GreaterOrEqual, right), expression, right), negated);
                        continue;
                    }

                    if (MatchWord("most"))
                    {
                        SkipNewLines();
                        var right = ParseRelationalComparisonOperand();
                        expression = ApplyIsNegation(WithRange(new GesBinaryExpressionNode(expression, GesBinaryOperator.LessOrEqual, right), expression, right), negated);
                        continue;
                    }

                    var token = Current;
                    throw new GameEventScriptParseException($"Expected least or most but found {token.Text}", token.Line, token.Column);
                }

                if (MatchWord("less"))
                {
                    SkipNewLines();
                    ExpectWord("than");
                    SkipNewLines();
                    var right = ParseRelationalComparisonOperand();
                    expression = ApplyIsNegation(WithRange(new GesBinaryExpressionNode(expression, GesBinaryOperator.Less, right), expression, right), negated);
                    continue;
                }

                if (MatchWord("more"))
                {
                    SkipNewLines();
                    ExpectWord("than");
                    SkipNewLines();
                    var right = ParseRelationalComparisonOperand();
                    expression = ApplyIsNegation(WithRange(new GesBinaryExpressionNode(expression, GesBinaryOperator.Greater, right), expression, right), negated);
                    continue;
                }

                if (Current.Kind == Identifier)
                {
                    var predicateName = Advance().Text;
                    expression = ApplyIsNegation(WithRange(new GesPredicateCallExpressionNode(expression, predicateName), expression), negated);
                    continue;
                }

                var threshold = ParseRelationalComparisonOperand();
                SkipNewLines();
                if (Match(OperatorOr))
                {
                    SkipNewLines();
                    if (MatchWord("less"))
                    {
                        expression = ApplyIsNegation(WithRange(new GesBinaryExpressionNode(expression, GesBinaryOperator.LessOrEqual, threshold), expression, threshold), negated);
                        continue;
                    }

                    if (MatchWord("more"))
                    {
                        expression = ApplyIsNegation(WithRange(new GesBinaryExpressionNode(expression, GesBinaryOperator.GreaterOrEqual, threshold), expression, threshold), negated);
                        continue;
                    }

                    var token = Current;
                    throw new GameEventScriptParseException($"Expected less or more but found {token.Text}", token.Line, token.Column);
                }

                expression = ApplyIsNegation(WithRange(new GesBinaryExpressionNode(expression, GesBinaryOperator.Equal, threshold), expression, threshold), negated);
                continue;
            }

            if (Match(As))
            {
                SkipNewLines();
                var typeName = ParseTypeName();
                expression = WithRange(new GesTypeCastExpressionNode(expression, typeName), expression);
                continue;
            }

            break;
        }

        return expression;
    }

    private GesExpressionNode ParseRelationalComparisonOperand() => ParseAdditiveExpression();

    private GesExpressionNode ParseRelationalExpression()
    {
        var expression = ParseAdditiveExpression();

        while (Match(OperatorLess, OperatorGreater, OperatorLessOrEqual, OperatorGreaterOrEqual))
        {
            var op = Previous.Kind switch
            {
                OperatorLess => GesBinaryOperator.Less,
                OperatorGreater => GesBinaryOperator.Greater,
                OperatorLessOrEqual => GesBinaryOperator.LessOrEqual,
                _ => GesBinaryOperator.GreaterOrEqual
            };
            SkipNewLines();
            var right = ParseAdditiveExpression();
            expression = WithRange(new GesBinaryExpressionNode(expression, op, right), expression, right);
        }

        return expression;
    }

    private GesExpressionNode ParseAdditiveExpression()
    {
        var expression = ParseMultiplicativeExpression();

        while (Match(OperatorPlus, OperatorMinus))
        {
            var op = Previous.Kind == OperatorPlus
                ? GesBinaryOperator.Add
                : GesBinaryOperator.Subtract;
            SkipNewLines();
            var right = ParseMultiplicativeExpression();
            expression = WithRange(new GesBinaryExpressionNode(expression, op, right), expression, right);
        }

        return expression;
    }

    private GesExpressionNode ParseMultiplicativeExpression()
    {
        var expression = ParseUnaryExpression();

        while (true)
        {
            if (Match(OperatorMultiply, OperatorDivide, OperatorIntegerDivide, OperatorModulo, OperatorRemainder))
            {
                var op = Previous.Kind switch
                {
                    OperatorMultiply => GesBinaryOperator.Multiply,
                    OperatorDivide => GesBinaryOperator.Divide,
                    OperatorIntegerDivide => GesBinaryOperator.IntegerDivide,
                    OperatorModulo => GesBinaryOperator.Modulo,
                    _ => GesBinaryOperator.Remainder
                };
                SkipNewLines();
                var right = ParseUnaryExpression();
                expression = WithRange(new GesBinaryExpressionNode(expression, op, right), expression, right);
                continue;
            }

            if (IsImplicitMultiplicationLeftExpression(expression) &&
                IsImplicitMultiplicationRightStart() &&
                AreAdjacent(Previous, Current))
            {
                var right = ParseUnaryExpression();
                expression = WithRange(new GesBinaryExpressionNode(expression, GesBinaryOperator.Multiply, right), expression, right);
                continue;
            }

            break;
        }

        return expression;
    }

    private GesExpressionNode ParsePowerExpression()
    {
        var expression = ParsePowerBaseExpression();

        if (Match(OperatorPower))
        {
            SkipNewLines();
            var right = ParseUnaryExpression();
            expression = WithRange(new GesBinaryExpressionNode(expression, GesBinaryOperator.Power, right), expression, right);
        }
        else if (Match(SuperscriptInteger))
        {
            var exponentToken = Previous;
            var exponent = long.Parse(exponentToken.Text, CultureInfo.InvariantCulture);
            var right = WithRange(new GesIntegerLiteralExpressionNode(exponent), exponentToken);
            expression = WithRange(new GesBinaryExpressionNode(expression, GesBinaryOperator.Power, right), expression, right);
        }

        return expression;
    }

    private static bool IsImplicitMultiplicationLeftExpression(GesExpressionNode expression) => expression is GesIntegerLiteralExpressionNode or GesFloatLiteralExpressionNode;

    private bool IsImplicitMultiplicationRightStart() => Current.Kind is Identifier or MathConstantPi or MathConstantE or MathConstantTau or MathConstantInfinity;

    private static bool AreAdjacent(GesToken left, GesToken right)
        => left.EndLine == right.Line &&
           left.EndColumn == right.Column;

    private GesExpressionNode ParseUnaryExpression()
    {
        if (_expressionNesting >= GesSourceNesting.MaximumExpressionDepth) ThrowNestingLimit(CreateRange(Current));
        _expressionNesting++;
        try { return ParseUnaryExpressionCore(); }
        finally { _expressionNesting--; }
    }

    private GesExpressionNode ParseUnaryExpressionCore()
    {
        SkipNewLines();
        if (Match(OperatorMinus))
        {
            var opToken = Previous;
            SkipNewLines();
            var negativeOperand = ParseUnaryExpression();
            return WithRange(new GesUnaryExpressionNode(GesUnaryOperator.Negate, negativeOperand), opToken, Previous);
        }

        if (Match(KeywordParse))
        {
            var opToken = Previous;
            var parsedOperand = ParseUnaryExpression();
            return WithRange(new GesUnaryExpressionNode(GesUnaryOperator.Parse, parsedOperand), opToken, Previous);
        }

        if (Match(Empty))
        {
            var opToken = Previous;
            SkipNewLines();
            var emptyOperand = ParseUnaryExpression();
            return WithRange(new GesUnaryExpressionNode(GesUnaryOperator.Empty, emptyOperand), opToken, Previous);
        }

        if (!Match(OperatorNot))
        {
            if (ParseIntrinsicCallExpression() is { } intrinsicCall) return intrinsicCall;

            if (ParseKeywordUnaryOperator() is { } keywordOperator)
            {
                var keywordOperand = ParseUnaryIntrinsicOperand();
                if (CreateRootPowerExpression(keywordOperator.RootExponent, keywordOperand, keywordOperator.StartToken) is { } rootExpression) return rootExpression;

                return WithRange(new GesUnaryExpressionNode(keywordOperator.Operator, keywordOperand), keywordOperator.StartToken, Previous);
            }

            return ParsePowerExpression();
        }

        var startToken = Previous;
        SkipNewLines();
        var operand = ParseUnaryExpression();
        return WithRange(new GesUnaryExpressionNode(GesUnaryOperator.Not, operand), startToken, Previous);
    }

    private GesExpressionNode ParsePowerBaseExpression()
    {
        if (Match(Series)) return ParseSeriesExpression(Previous);

        if (IsExtensionCallStart()) return ParseExtensionCallExpression();

        if (Match(Clamp)) return ParseClampExpression();

        if (Is(Min) && IsNextSignificantToken(Of))
        {
            Advance();
            return ParseVariadicTaggedExpression("min");
        }

        if (Is(Max) && IsNextSignificantToken(Of))
        {
            Advance();
            return ParseVariadicTaggedExpression("max");
        }

        return ParsePostfixExpression();
    }

    private GesExpressionNode ParseSeriesExpression(GesToken startToken)
    {
        SkipNewLines();
        if (Match(Fibonacci)) return WithRange(new GesSeriesExpressionNode(GesBytecodeSeriesKind.Fibonacci), startToken, Previous);

        if (Match(Factorial)) return WithRange(new GesSeriesExpressionNode(GesBytecodeSeriesKind.Factorial), startToken, Previous);

        var token = Current;
        throw new GameEventScriptParseException("Expected series kind 'fibonacci' or 'factorial'.", token);
    }

    private (GesUnaryOperator Operator, double? RootExponent, GesToken StartToken)? ParseKeywordUnaryOperator()
    {
        var op = default(GesUnaryOperator);
        double? rootExponent = null;
        var startToken = Current;
        if (!Match(IntrinsicAbs))
        {
            if (Match(IntrinsicLn)) op = GesUnaryOperator.NaturalLog;
            else if (Match(IntrinsicExp)) op = GesUnaryOperator.Exp;
            else if (Match(IntrinsicSqrt)) rootExponent = SquareRootExponent;
            else if (Match(IntrinsicCbrt)) rootExponent = CubeRootExponent;
            else if (Match(IntrinsicChance)) op = GesUnaryOperator.Chance;
            else if (Match(IntrinsicFloor)) op = GesUnaryOperator.Floor;
            else if (Match(IntrinsicCeil)) op = GesUnaryOperator.Ceil;
            else if (Match(IntrinsicTruncate)) op = GesUnaryOperator.Truncate;
            else if (Match(IntrinsicRad)) op = GesUnaryOperator.DegreeToRadians;
            else if (Match(IntrinsicDeg)) op = GesUnaryOperator.DegreeFromRadians;
            else if (Match(IntrinsicSin)) op = GesUnaryOperator.Sin;
            else if (Match(IntrinsicCos)) op = GesUnaryOperator.Cos;
            else if (Match(IntrinsicTan)) op = GesUnaryOperator.Tan;
            else if (Match(IntrinsicAsin)) op = GesUnaryOperator.Asin;
            else if (Match(IntrinsicAcos)) op = GesUnaryOperator.Acos;
            else if (Match(IntrinsicAtan)) op = GesUnaryOperator.Atan;
            else if (Match(IntrinsicWrap))
            {
                SkipNewLines();
                ExpectWord("degree");
                op = GesUnaryOperator.WrapDegree;
            }
            else if (Match(IntrinsicRound))
            {
                SkipNewLines();
                ExpectWord("half");
                SkipNewLines();
                if (MatchWord("even")) op = GesUnaryOperator.RoundHalfEven;
                else if (MatchWord("up")) op = GesUnaryOperator.RoundHalfUp;
                else if (MatchWord("down")) op = GesUnaryOperator.RoundHalfDown;
                else
                {
                    var token = Current;
                    throw new GameEventScriptParseException("Expected round mode 'even', 'up', or 'down'.", token);
                }
            }
            else
            {
                return null;
            }
        }
        else
        {
            op = GesUnaryOperator.Abs;
        }

        return (op, rootExponent, startToken);
    }

    private GesExpressionNode? ParseIntrinsicCallExpression()
    {
        var startToken = Current;
        GesIntrinsicFunction function;

        if (Match(IntrinsicAtan2))
        {
            function = GesIntrinsicFunction.Atan2;
        }
        else if (Match(IntrinsicHypot))
        {
            function = GesIntrinsicFunction.Hypot;
        }
        else if (Match(IntrinsicDistance))
        {
            SkipNewLines();
            function = Match(IntrinsicSquared)
                ? GesIntrinsicFunction.DistanceSquared
                : GesIntrinsicFunction.Distance;
        }
        else if (Match(IntrinsicLength))
        {
            SkipNewLines();
            Expect(IntrinsicSquared);
            function = GesIntrinsicFunction.LengthSquared;
        }
        else if (Match(IntrinsicNormalize))
        {
            function = GesIntrinsicFunction.Normalize;
        }
        else if (Match(IntrinsicDot))
        {
            function = GesIntrinsicFunction.Dot;
        }
        else if (Match(IntrinsicCross))
        {
            function = GesIntrinsicFunction.Cross;
        }
        else if (Match(IntrinsicAngle))
        {
            SkipNewLines();
            ExpectWord("between");
            function = GesIntrinsicFunction.AngleBetween;
        }
        else
        {
            return null;
        }

        SkipNewLines();
        var arguments = ParseIntrinsicArgumentExpressions(function);
        return WithRange(new GesIntrinsicCallExpressionNode(function, arguments), startToken, Previous);
    }

    private IReadOnlyList<GesExpressionNode> ParseIntrinsicArgumentExpressions(GesIntrinsicFunction function)
    {
        Expect(LeftParen);
        SkipNewLines();

        if (Match(RightParen)) return [];

        var arguments = new List<GesExpressionNode>();
        while (true)
        {
            arguments.Add(ParseExpression());
            SkipNewLines();

            if (Match(RightParen)) return arguments;

            if (!Match(Comma))
            {
                var token = Current;
                throw new GameEventScriptParseException($"Expected ',' or ')' in {function.ToSourceText()} argument list.", token);
            }

            SkipNewLines();
        }
    }

    private static double? GetMathConstant(GesTokenKind kind)
    {
        switch (kind)
        {
            case MathConstantPi: return GesMathConstants.Pi;
            case MathConstantE: return GesMathConstants.E;
            case MathConstantTau: return GesMathConstants.Tau;
            case MathConstantInfinity: return GesMathConstants.Infinity;
            default: return null;
        }
    }

    private GesExpressionNode ParseUnaryIntrinsicOperand()
    {
        SkipNewLines();
        if (!Match(LeftParen)) return ParseUnaryExpression();

        SkipNewLines();
        var operand = ParseExpression();
        SkipNewLines();
        Expect(RightParen);
        return operand;
    }

    private GesExpressionNode? CreateRootPowerExpression(double? rootExponent, GesExpressionNode operand, GesToken startToken)
    {
        if (!rootExponent.HasValue) return null;

        var exponentNode = new GesFloatLiteralExpressionNode(rootExponent.Value)
        {
            SourceRange = CreateRange(startToken, Previous)
        };
        return WithRange(new GesBinaryExpressionNode(operand, GesBinaryOperator.Power, exponentNode), startToken, Previous);
    }

    private GesExpressionNode ParsePostfixExpression() => ParsePostfixSuffixes(ParsePrimaryExpression());

    private GesExpressionNode ParsePostfixSuffixes(GesExpressionNode expression)
    {
        while (true)
        {
            if (Match(Dot))
            {
                SkipNewLines();
                var member = ExpectIdentifier();
                expression = WithRange(new GesMemberAccessExpressionNode(expression, member), expression);
                continue;
            }

            if (Match(LeftBracket))
            {
                SkipNewLines();
                expression = ParsePipelineSteps(expression);
                Expect(RightBracket);
                continue;
            }

            return expression;
        }
    }

    private (string Identifier, IReadOnlyList<string> Names) ParseBindingNames()
    {
        var identifier = ExpectIdentifier();
        if (!Is(Comma)) return (identifier, []);
        var names = new List<string> { identifier };
        while (Match(Comma)) { SkipNewLines(); names.Add(ExpectIdentifier()); }
        return (identifier, names);
    }

    private bool IsCombinedSelector() => Current.Text is ":cartesian" or ":lockstep" or ":zip" or ":union" or ":intersect" or ":difference";

    private bool IsPipelineSelectorStart()
        => !IsExtensionCallStart() && (IsCombinedSelector() || Current.Text is
            ":entries" or ":keys" or ":values" or ":filter" or ":select" or ":foreach" or ":any" or ":all" or ":count" or
            ":sum" or ":average" or ":min" or ":max" or ":highest" or ":lowest" or ":fold" or ":reduce" or ":map" or
            ":first" or ":last" or ":single" or ":distinct" or ":order" or ":group" or ":sort" or ":reverse" or ":shuffle" or
            ":split" or ":choose" or ":draw" or ":take" or ":drop" or ":contains" or ":has" or ":term");

    private GesExpressionNode ParsePipelineSteps(GesExpressionNode? source)
    {
        do
        {
            var start = Current;
            if (MatchSelector(":split"))
            {
                if (source is null) throw new GameEventScriptParseException("Split requires a source.", start);
                Expect(On);
                var whitespace = MatchWord("whitespace");
                GesExpressionNode delimiter = whitespace ? new GesNothingLiteralExpressionNode() : ParseExpression();
                var arguments = new GesArgumentListNode(whitespace ? [new GesArgumentNode(null, source)] : [new GesArgumentNode(null, source), new GesArgumentNode(null, delimiter)]);
                source = WithRange(new GesTypeConstructorExpressionNode(whitespace ? "__splitWhitespace" : "__split", arguments), source);
            }
            else if (IsCombinedSelector())
            {
                var operation = Advance().Text[1..];
                var sources = new List<GesExpressionNode>();
                if (source is not null) sources.Add(source);
                SkipNewLines();
                sources.Add(ParseExpression());
                while (Match(Comma)) { SkipNewLines(); sources.Add(ParseExpression()); }
                if (sources.Count < 2) throw new GameEventScriptParseException("Combined selectors require at least two sources.", start);
                source = WithRange(new GesCombinedCollectionExpressionNode(operation == "zip" ? "lockstep" : operation, sources), start);
            }
            else
            {
                if (source is null) throw new GameEventScriptParseException("Selector requires a source.", start);
                var selector = ParseCollectionSelector();
                source = WithRange(new GesCollectionAccessExpressionNode(source, selector), source, selector);
            }
            SkipNewLines();
        } while (IsPipelineSelectorStart());
        return source!;
    }

    private GesCollectionSelectorNode ParseCollectionSelector()
    {
        var startToken = Current;
        SkipNewLines();
        if (MatchSelector(":foreach"))
        {
            var (identifier, names) = ParseBindingNames();
            Expect(ProjectionArrow);
            return WithRange(new GesForeachSelectorNode(identifier, ParseExpression()) { BindingNames = names }, startToken);
        }
        if (Match(SelectorAny, SelectorAll))
        {
            var op = Previous.Kind == SelectorAny ? "any" : "all";
            var (identifier, bindings) = ParseBindingNames();
            ExpectWord("where");
            var predicate = ParseExpression();
            return WithRange(new GesPredicateSelectorNode(op, identifier, predicate) { BindingNames = bindings }, startToken);
        }

        if (Match(SelectorHas))
        {
            SkipNewLines();
            if (Is(LeftBracket)) return CheckNesting(new GesObjectMatchSelectorNode(ParseObjectMatchPattern()));

            return WithRange(new GesPatternSelectorNode(ParseDicePattern()), startToken);
        }

        if (Match(SelectorTake)) return ParseTakeSelector();

        if (Match(SelectorDrop)) return ParseDropSelector();

        if (MatchSelector(":term")) return ParseSeriesTermSelector();

        if (Match(SelectorCount))
        {
            SkipNewLines();
            if (Is(RightBracket) || IsPipelineSelectorStart()) return WithRange(new GesCountSelectorNode("value", new GesBooleanLiteralExpressionNode(true)), startToken);

            var (identifier, bindings) = ParseBindingNames();
            ExpectWord("where");
            var predicate = ParseExpression();
            return WithRange(new GesCountSelectorNode(identifier, predicate) { BindingNames = bindings }, startToken);
        }

        if (Match(SelectorChoose)) return ParseChooseSelector();

        if (Match(SelectorDraw))
        {
            SkipNewLines();
            var countToken = Expect(GesTokenKind.Float);
            return WithRange(new GesDrawSelectorNode(ParsePositiveInteger(countToken, "draw count")), startToken);
        }

        if (Match(SelectorShuffle)) return WithRange(new GesShuffleSelectorNode(), startToken);

        if (Match(SelectorReverse)) return WithRange(new GesReverseSelectorNode(), startToken);

        if (MatchSelector(":first")) return ParseEdgeSelector("first");

        if (MatchSelector(":last")) return ParseEdgeSelector("last");

        if (MatchSelector(":single")) return ParseEdgeSelector("single");

        if (Match(SelectorFilter))
        {
            var (identifier, bindings) = ParseBindingNames();
            ExpectWord("where");
            var predicate = ParseExpression();
            return WithRange(new GesFilterSelectorNode(identifier, predicate) { BindingNames = bindings }, startToken);
        }

        if (Match(SelectorSum))
        {
            SkipNewLines();
            if (Is(RightBracket) || IsPipelineSelectorStart()) return WithRange(new GesSumSelectorNode("value", new GesIdentifierExpressionNode("value")), startToken);

            var (identifier, bindings) = ParseBindingNames();
            Expect(ProjectionArrow);
            var projection = ParseExpression();
            return WithRange(new GesSumSelectorNode(identifier, projection) { BindingNames = bindings }, startToken);
        }

        if (Match(SelectorAverage))
        {
            SkipNewLines();
            if (Is(RightBracket) || IsPipelineSelectorStart()) return WithRange(new GesAverageSelectorNode("value", new GesIdentifierExpressionNode("value")), startToken);

            var (identifier, bindings) = ParseBindingNames();
            Expect(ProjectionArrow);
            var projection = ParseExpression();
            return WithRange(new GesAverageSelectorNode(identifier, projection) { BindingNames = bindings }, startToken);
        }

        if (MatchSelector(":min")) return ParseProjectionSelector("min");

        if (MatchSelector(":max")) return ParseProjectionSelector("max");

        if (MatchSelector(":highest")) return ParseProjectionSelector("highest");

        if (MatchSelector(":lowest")) return ParseProjectionSelector("lowest");

        if (Current.Kind == Selector && Current.Text is ":fold" or ":reduce")
        {
            var reduce = MatchSelector(":reduce");
            if (!reduce) MatchSelector(":fold");
            var accumulator = ExpectIdentifier();
            GesExpressionNode? seed = null;
            if (!reduce)
            {
                Expect(Be);
                seed = ParseExpression();
            }
            Expect(Comma);
            SkipNewLines();
            var (identifier, bindings) = ParseBindingNames();
            Expect(ProjectionArrow);
            SkipNewLines();
            return WithRange(new GesFoldSelectorNode(accumulator, identifier, seed, ParseExpression()) { BindingNames = bindings }, startToken);
        }

        if (Match(SelectorSelect))
        {
            var (identifier, bindings) = ParseBindingNames();
            Expect(ProjectionArrow);
            var projection = ParseExpression();
            return WithRange(new GesSelectSelectorNode(identifier, projection) { BindingNames = bindings }, startToken);
        }

        if (MatchSelector(":map"))
        {
            SkipNewLines();
            var (identifier, bindings) = ParseBindingNames();
            SkipNewLines();
            ExpectWord("by");
            SkipNewLines();
            var keyProjection = ParseExpression();
            SkipNewLines();
            GesExpressionNode? valueProjection = null;
            if (Match(ProjectionArrow))
            {
                SkipNewLines();
                valueProjection = ParseExpression();
            }

            return WithRange(new GesMapSelectorNode(identifier, keyProjection, valueProjection) { BindingNames = bindings }, startToken);
        }

        if (Match(SelectorContains)) return ParseContainsSelector();

        if (MatchSelector(":distinct")) return ParseDistinctSelector();

        if (MatchSelector(":group")) return ParseGroupBySelector();

        if (Match(SelectorSort)) return ParseSortSelector();

        if (MatchSelector(":order")) return ParseOrderBySelector();

        if (Current.Kind == Selector && Current.Text is ":keys" or ":values" or ":entries")
        {
            var selectorToken = Advance();
            var selector = WithRange(new GesTagLiteralExpressionNode(selectorToken.Text[1..]), selectorToken);
            return WithRange(new GesExpressionSelectorNode(selector), startToken);
        }

        return WithRange(new GesExpressionSelectorNode(ParseExpression()), startToken);
    }

    private string ParseSortDirection()
    {
        SkipNewLines();
        if (MatchWord("ascending")) return "ascending";

        if (MatchWord("descending")) return "descending";

        var token = Current;
        throw new GameEventScriptParseException($"Expected sort direction but found {token.Text}", token.Line, token.Column);
    }

    private GesCollectionSelectorNode ParseSortSelector()
    {
        var startToken = Previous;
        SkipNewLines();
        return WithRange(new GesSortSelectorNode(ParseSortDirection()), startToken);
    }

    private GesCollectionSelectorNode ParseEdgeSelector(string mode)
    {
        var startToken = Previous;
        SkipNewLines();
        if (Current.Kind != Identifier) return WithRange(new GesEdgeSelectorNode(mode, null, null), startToken);

        var (identifier, bindings) = ParseBindingNames();
        ExpectWord("where");
        var predicate = ParseExpression();
        return WithRange(new GesEdgeSelectorNode(mode, identifier, predicate) { BindingNames = bindings }, startToken);
    }

    private GesCollectionSelectorNode ParseOrderBySelector()
    {
        var startToken = Previous;
        SkipNewLines();
        ExpectWord("by");
        SkipNewLines();
        var (identifier, bindings) = ParseBindingNames();
        Expect(ProjectionArrow);
        var projection = ParseExpression();
        var direction = ParseSortDirection();
        return WithRange(new GesOrderBySelectorNode(direction, identifier, projection) { BindingNames = bindings }, startToken);
    }

    private GesCollectionSelectorNode ParseProjectionSelector(string op)
    {
        var startToken = Previous;
        SkipNewLines();
        var (identifier, bindings) = ParseBindingNames();
        Expect(ProjectionArrow);
        var projection = ParseExpression();

        return op switch
        {
            "min" or "lowest" => WithRange(new GesMinSelectorNode(identifier, projection) { BindingNames = bindings }, startToken),
            "max" or "highest" => WithRange(new GesMaxSelectorNode(identifier, projection) { BindingNames = bindings }, startToken),
            _ => throw new InvalidOperationException($"Unknown projection selector '{op}'")
        };
    }

    private GesCollectionSelectorNode ParseContainsSelector()
    {
        var startToken = Previous;
        SkipNewLines();
        if (MatchWord("all"))
        {
            SkipNewLines();
            return WithRange(new GesContainsSelectorNode("all", ParseExpression()), startToken);
        }

        if (MatchWord("any"))
        {
            SkipNewLines();
            return WithRange(new GesContainsSelectorNode("any", ParseExpression()), startToken);
        }

        return WithRange(new GesContainsSelectorNode("single", ParseExpression()), startToken);
    }

    private GesCollectionSelectorNode ParseDistinctSelector()
    {
        var startToken = Previous;
        SkipNewLines();
        if (!MatchWord("by")) return WithRange(new GesDistinctSelectorNode(null, null), startToken);

        SkipNewLines();
        var (identifier, bindings) = ParseBindingNames();
        Expect(ProjectionArrow);
        var projection = ParseExpression();
        return WithRange(new GesDistinctSelectorNode(identifier, projection) { BindingNames = bindings }, startToken);
    }

    private GesCollectionSelectorNode ParseGroupBySelector()
    {
        var startToken = Previous;
        SkipNewLines();
        ExpectWord("by");
        SkipNewLines();
        var (identifier, bindings) = ParseBindingNames();
        Expect(ProjectionArrow);
        var projection = ParseExpression();
        return WithRange(new GesGroupBySelectorNode(identifier, projection) { BindingNames = bindings }, startToken);
    }

    private GesObjectMatchPatternNode ParseObjectMatchPattern()
    {
        if (_expressionNesting >= GesSourceNesting.MaximumExpressionDepth) ThrowNestingLimit(CreateRange(Current));
        _expressionNesting++;
        try { return ParseObjectMatchPatternCore(); }
        finally { _expressionNesting--; }
    }

    private GesObjectMatchPatternNode ParseObjectMatchPatternCore()
    {
        var startToken = Current;
        Expect(LeftBracket);
        SkipNewLines();
        var entries = new List<GesObjectMatchEntryNode>();
        if (!Is(RightBracket))
        {
            entries.Add(ParseObjectMatchEntry());
            while (Match(Comma))
            {
                SkipNewLines();
                entries.Add(ParseObjectMatchEntry());
            }
        }

        SkipNewLines();
        Expect(RightBracket);
        return WithRange(new GesObjectMatchPatternNode(entries), startToken);
    }

    private GesObjectMatchEntryNode ParseObjectMatchEntry()
    {
        var startToken = Current;
        var key = ExpectIdentifier();
        Expect(Colon);
        SkipNewLines();
        GesObjectMatchValueNode value = Is(LeftBracket)
            ? new GesObjectMatchNestedValueNode(ParseObjectMatchPattern())
            : new GesObjectMatchExpressionValueNode(ParseExpression());
        return WithRange(new GesObjectMatchEntryNode(key, value), startToken);
    }

    private GesExpressionNode ParsePrimaryExpression()
    {
        if (Match(GesTokenKind.Is))
        {
            var startToken = Previous;
            SkipNewLines();
            var negated = Match(OperatorNot);
            SkipNewLines();
            var name = Expect(Identifier).Text;
            return ApplyIsNegation(WithRange(new GesPredicateCallExpressionNode(null, name), startToken), negated);
        }
        if (Match(Emit)) return ParseSendExpression(GesPublishStatementKind.Emit, Previous);
        if (Match(Publish)) return ParseSendExpression(GesPublishStatementKind.Publish, Previous);
        ThrowIfIllegalToken();

        if (IsTypeConstructorStart()) return ParseTypeConstructorExpression();

        if (Current.Kind == TypeName && string.Equals(Current.Text, ":Dice", StringComparison.Ordinal) && IsNextSignificantToken(LeftBracket)) return ParseDiceLiteralExpression();

        if (MatchWord("from")) return ParseRangeExpressionCore();

        if (Match(KeywordRandom))
        {
            SkipNewLines();
            if (Match(With)) return ParseSeededRandomExpression();

            return ParseRandomExpression();
        }

        if (Is(Roll) && IsNextSignificantWord("dice"))
        {
            Advance();
            SkipNewLines();
            ExpectWord("dice");
            return ParseDiceExpression();
        }

        if (Current.Kind == TypeName && string.Equals(Current.Text, ":List", StringComparison.Ordinal) && IsNextSignificantToken(LeftBracket))
        {
            Advance();
            return ParseCollectionFactoryExpression("list");
        }

        if (Current.Kind == Selector &&
            string.Equals(Current.Text, ":set", StringComparison.Ordinal) &&
            IsNextSignificantToken(LeftBracket))
        {
            var setToken = Current;
            throw new GameEventScriptParseException("Type ':set' is not supported; use lists or key-only maps.", setToken);
        }

        if (Match(GesTokenKind.Float))
        {
            return Previous.GetIntegerValue() is { } integerValue
                ? WithRange(new GesIntegerLiteralExpressionNode(integerValue, Previous.Text.IndexOf('.') >= 0), Previous)
                : WithRange(new GesFloatLiteralExpressionNode(Previous.FloatValue), Previous);
        }

        if (Match(Percentage))
        {
            var ratio = Previous.FloatValue;
            return double.IsFinite(ratio)
                ? WithRange(new GesPercentageLiteralExpressionNode(ratio), Previous)
                : WithRange(new GesNothingLiteralExpressionNode(), Previous);
        }

        if (Match(GesTokenKind.UnitNumber))
        {
            return Previous.GetIntegerValue() is { } integerValue
                ? WithRange(new GesUnitIntegerLiteralExpressionNode(integerValue, Previous.UnitName, Previous.Text.IndexOf('.') >= 0), Previous)
                : WithRange(new GesUnitFloatLiteralExpressionNode(Previous.FloatValue, Previous.UnitName), Previous);
        }

        if (Match(Text)) return WithRange(new GesTextLiteralExpressionNode(Previous.Text), Previous);

        if (Match(LeftBracket)) return ParseBracketLiteralExpression();

        if (Match(True)) return WithRange(new GesBooleanLiteralExpressionNode(true), Previous);

        if (Match(False)) return WithRange(new GesBooleanLiteralExpressionNode(false), Previous);

        if (Match(Nothing)) return WithRange(new GesNothingLiteralExpressionNode(), Previous);

        if (Current.Kind == Tag)
        {
            var tagToken = Advance();
            return WithRange(new GesTagLiteralExpressionNode(tagToken.Text[1..]), tagToken);
        }

        if (Match(ConstantReference)) return WithRange(new GesConstantReferenceExpressionNode(Previous.Text[1..]), Previous);

        if (GetMathConstant(Current.Kind) is { } numericConstant)
        {
            var constantToken = Current;
            Advance();
            return WithRange(new GesFloatLiteralExpressionNode(numericConstant), constantToken);
        }

        if ((Current.Kind == Identifier || Current.Kind == Message) && IsCallExpressionStart()) return ParseCallOrHandlerBindExpression();

        if (Current.Kind == Identifier)
        {
            var identifierToken = Advance();
            return WithRange(new GesIdentifierExpressionNode(identifierToken.Text), identifierToken);
        }

        if (Match(LeftParen))
        {
            SkipNewLines();
            var expression = ParseExpression();
            SkipNewLines();
            Expect(RightParen);
            return CheckNesting(expression with { SourceNesting = expression.SourceNesting + 1 });
        }

        var token = Current;
        throw new GameEventScriptParseException($"Unexpected token '{token.Text}'", token.Line, token.Column);
    }

    private GesExpressionNode ParseBracketLiteralExpression()
    {
        var startToken = Previous;
        SkipNewLines();
        if (IsCombinedSelector())
        {
            var combined = ParsePipelineSteps(null);
            Expect(RightBracket);
            return combined;
        }
        if (Match(Colon))
        {
            SkipNewLines();
            Expect(RightBracket);
            return WithRange(new GesMapLiteralExpressionNode(Array.Empty<GesMapEntryNode>()), startToken);
        }

        if (Is(RightBracket))
        {
            Expect(RightBracket);
            return WithRange(new GesListLiteralExpressionNode(Array.Empty<GesExpressionNode>()), startToken);
        }

        return IsMapLiteralEntryStart()
            ? ParseDictionaryLiteralExpression()
            : ParseListLiteralExpression();
    }

    private GesMessageLiteralExpressionNode ParseMessageLiteralExpressionCore()
    {
        SkipNewLines();
        var startToken = Current;
        var message = Expect(Message).Text;
        var arguments = GesArgumentListNode.Empty;
        if (Match(LeftParen)) arguments = ParseArgumentListAfterLeftParen();

        return WithRange(new GesMessageLiteralExpressionNode(message, arguments), startToken);
    }

    private GesExpressionNode ParseCallOrHandlerBindExpression()
    {
        var startToken = Current;
        var isMessageCallee = Current.Kind == Message;
        var name = isMessageCallee ? Expect(Message).Text : ExpectIdentifier();
        Expect(LeftParen);
        SkipNewLines();
        if (Is(RightParen))
        {
            Expect(RightParen);
            return isMessageCallee
                ? WithRange(new GesHandlerLiteralExpressionNode(name, Array.Empty<GesParameterNode>()), startToken)
                : WithRange(new GesCallExpressionNode(name, GesArgumentListNode.Empty), startToken);
        }

        if (IsArgumentLabelStart())
        {
            var labeledArguments = ParseArgumentListAfterFirstArgument(ParseArgument(), RightParen);
            Expect(RightParen);
            return isMessageCallee
                ? WithRange(new GesMessageLiteralExpressionNode(name, labeledArguments), startToken)
                : WithRange(new GesCallExpressionNode(name, labeledArguments), startToken);
        }

        if (isMessageCallee)
        {
            var parameters = new List<GesParameterNode> { ParseParameter(allowDeclaredType: false) };
            while (Match(Comma))
            {
                SkipNewLines();
                parameters.Add(ParseParameter(allowDeclaredType: false));
            }

            SkipNewLines();
            Expect(RightParen);
            return WithRange(new GesHandlerLiteralExpressionNode(name, parameters), startToken);
        }

        var arguments = new List<GesArgumentNode> { new(null, ParseExpression()) };
        while (Match(Comma))
        {
            SkipNewLines();
            arguments.Add(new GesArgumentNode(null, ParseExpression()));
        }

        SkipNewLines();
        Expect(RightParen);
        return WithRange(new GesCallExpressionNode(name, new GesArgumentListNode(arguments)), startToken);
    }

    private GesListLiteralExpressionNode ParseListLiteralExpression()
    {
        var startToken = Previous;
        var items = new List<GesExpressionNode>();
        SkipNewLines();
        if (!Is(RightBracket))
        {
            items.Add(ParseExpression());
            while (Match(Comma))
            {
                SkipNewLines();
                items.Add(ParseExpression());
            }
        }

        SkipNewLines();
        Expect(RightBracket);
        return WithRange(new GesListLiteralExpressionNode(items), startToken);
    }

    private GesTypeConstructorExpressionNode ParseDiceLiteralExpression()
    {
        var startToken = Advance();
        SkipNewLines();
        var bracketToken = Expect(LeftBracket);
        var items = new List<GesExpressionNode>();
        SkipNewLines();
        if (!Is(RightBracket))
        {
            while (true)
            {
                var token = Expect(GesTokenKind.Float);
                if (token.GetIntegerValue() is not { } value || value is <= 0 or > int.MaxValue)
                    throw new GameEventScriptParseException("Dice literals require positive Int32 numeric literals.", token);
                items.Add(WithRange(new GesIntegerLiteralExpressionNode(value), token));
                SkipNewLines();
                if (!Match(Comma)) break;
                SkipNewLines();
            }
        }
        Expect(RightBracket);
        var values = WithRange(new GesListLiteralExpressionNode(items), bracketToken);
        // Reuse deterministic list-to-Dice conversion; CreateDice is a random roll.
        return WithRange(new GesTypeConstructorExpressionNode("dice", new GesArgumentListNode([new GesArgumentNode(null, values)])), startToken);
    }

    private GesExpressionNode ParseCollectionFactoryExpression(string collectionType)
    {
        var startToken = Previous;
        Expect(LeftBracket);
        SkipNewLines();

        if (Match(SelectorSelect))
        {
            var expression = ParseGeneratedCollectionExpression(collectionType);
            SkipNewLines();
            Expect(RightBracket);
            return expression;
        }

        var token = Current;
        throw new GameEventScriptParseException($"Expected :select but found {token.Text}", token.Line, token.Column);
    }

    private GesGeneratedCollectionExpressionNode ParseGeneratedCollectionExpression(string collectionType)
    {
        var startToken = Previous;
        SkipNewLines();
        var identifier = ExpectIdentifier();
        SkipNewLines();
        GesIterationSourceNode source;
        if (MatchWord("from"))
        {
            var fromToken = Previous;
            source = WithRange(new GesRangeIterationSourceNode(ParseRangeExpressionCore()), fromToken);
        }
        else if (Match(In))
        {
            SkipNewLines();
            if (Current.Kind == Identifier && string.Equals(Current.Text, "from", StringComparison.Ordinal))
            {
                throw new GameEventScriptParseException("Direct ranges are not allowed after 'in'; use ':select item from ... to ...' or iterate a range value", Current.Line, Current.Column);
            }

            var sourceStart = Current;
            source = WithRange(new GesCollectionIterationSourceNode(ParseExpression()), sourceStart);
        }
        else
        {
            var token = Current;
            throw new GameEventScriptParseException($"Expected 'from' or {In} but found {token.Text}", token.Line, token.Column);
        }

        GesExpressionNode? predicate = null;
        SkipNewLines();
        if (MatchWord("where"))
        {
            SkipNewLines();
            predicate = ParseExpression();
        }

        SkipNewLines();
        Expect(ProjectionArrow);
        SkipNewLines();
        var projection = ParseExpression();
        return WithRange(new GesGeneratedCollectionExpressionNode(collectionType, identifier, source, predicate, projection), startToken);
    }

    private GesMapLiteralExpressionNode ParseDictionaryLiteralExpression()
    {
        var startToken = Previous;
        var entries = new List<GesMapEntryNode>();
        SkipNewLines();
        if (!Is(RightBracket))
        {
            entries.Add(ParseDictionaryEntry());
            while (Match(Comma))
            {
                SkipNewLines();
                entries.Add(ParseDictionaryEntry());
            }
        }

        SkipNewLines();
        Expect(RightBracket);
        return WithRange(new GesMapLiteralExpressionNode(entries), startToken);
    }

    private GesMapEntryNode ParseDictionaryEntry()
    {
        var startToken = Current;
        var key = ExpectIdentifier();
        Expect(Colon);
        SkipNewLines();
        var value = Is(Comma) || Is(RightBracket)
            ? WithRange(new GesBooleanLiteralExpressionNode(true), startToken)
            : ParseExpression();
        return WithRange(new GesMapEntryNode(key, value), startToken);
    }

    private bool IsMapLiteralEntryStart()
    {
        if (Current.Kind != Identifier) return false;

        return _reader.PeekSignificant(1).Kind == Colon;
    }

    private GesRandomExpressionNode ParseRandomExpression()
    {
        var startToken = Previous;
        MatchWord("from");
        SkipNewLines();
        var fromExpression = ParseRandomBoundExpression();
        SkipNewLines();
        Expect(To);
        SkipNewLines();
        var toExpression = ParseRandomBoundExpression();

        return WithRange(new GesRandomExpressionNode(fromExpression, toExpression), startToken);
    }

    private GesRangeExpressionNode ParseRangeExpressionCore()
    {
        var startToken = Previous;
        SkipNewLines();
        var fromExpression = ParseRangeBoundExpression();
        SkipNewLines();
        Expect(To);
        SkipNewLines();
        var toExpression = ParseRangeBoundExpression();

        GesExpressionNode? stepExpression = null;
        if (MatchWord("step"))
        {
            SkipNewLines();
            stepExpression = ParseExpression();
        }

        return WithRange(new GesRangeExpressionNode(fromExpression, toExpression, stepExpression), startToken);
    }

    private GesSeededRandomExpressionNode ParseSeededRandomExpression()
    {
        var startToken = Previous;
        SkipNewLines();
        var seedExpression = ParseExpression();
        SkipNewLines();
        var bodyExpression = ParseExpression();
        return WithRange(new GesSeededRandomExpressionNode(seedExpression, bodyExpression), startToken);
    }

    private GesExpressionNode ParseRandomBoundExpression()
    {
        SkipNewLines();
        return ParseAdditiveExpression();
    }

    private GesExpressionNode ParseRangeBoundExpression()
    {
        SkipNewLines();
        return ParseAdditiveExpression();
    }

    private GesDiceExpressionNode ParseDiceExpression()
    {
        var startToken = Previous;
        var diceCountToken = Expect(GesTokenKind.Float);
        var sideCountToken = ParseDiceSideCountToken();

        var diceCount = ParsePositiveInteger(diceCountToken, "dice count");
        var sideCount = ParsePositiveInteger(sideCountToken, "side count");

        return WithRange(new GesDiceExpressionNode(diceCount, sideCount), startToken);
    }

    private GesCollectionSelectorNode ParseTakeSelector()
    {
        var startToken = Previous;
        SkipNewLines();
        if (ParseSliceScope() is { } scope)
        {
            SkipNewLines();
            var countToken = Expect(GesTokenKind.Float);
            return WithRange(new GesSequenceSliceSelectorNode("take", scope, ParsePositiveInteger(countToken, "take count")), startToken);
        }

        return WithRange(new GesTakePatternSelectorNode(ParseDicePattern()), startToken);
    }

    private GesCollectionSelectorNode ParseDropSelector()
    {
        var startToken = Previous;
        SkipNewLines();
        var scope = ParseSliceScope();
        if (scope is null)
        {
            var token = Current;
            throw new GameEventScriptParseException($"Expected drop scope but found {token.Text}", token.Line, token.Column);
        }

        SkipNewLines();
        var countToken = Expect(GesTokenKind.Float);
        return WithRange(new GesSequenceSliceSelectorNode("drop", scope, ParsePositiveInteger(countToken, "drop count")), startToken);
    }

    private GesCollectionSelectorNode ParseSeriesTermSelector()
    {
        var startToken = Previous;
        SkipNewLines();
        return WithRange(new GesSeriesTermSelectorNode(ParseExpression()), startToken);
    }

    private string? ParseSliceScope()
    {
        if (MatchWord("first")) return "first";

        if (MatchWord("last")) return "last";

        if (MatchWord("highest")) return "highest";

        if (MatchWord("lowest")) return "lowest";

        return null;
    }

    private GesCollectionSelectorNode ParseChooseSelector()
    {
        var startToken = Previous;
        SkipNewLines();
        var countToken = Expect(GesTokenKind.Float);
        var count = ParsePositiveInteger(countToken, "choose count");
        SkipNewLines();

        var atRandom = false;
        if (MatchWord("at"))
        {
            SkipNewLines();
            Expect(KeywordRandom);
            atRandom = true;
            SkipNewLines();
        }

        string? identifier = null;
        GesExpressionNode? predicate = null;
        string? weightIdentifier = null;
        IReadOnlyList<string> names = [];
        IReadOnlyList<string> weightNames = [];
        GesExpressionNode? weightExpression = null;
        if (Current.Kind == Identifier)
        {
            var lookahead = _reader.PeekSignificant(1);
            if (lookahead.Kind == Comma || lookahead.Kind == Identifier && string.Equals(lookahead.Text, "where", StringComparison.Ordinal))
            {
                (identifier, names) = ParseBindingNames();
                ExpectWord("where");
                predicate = ParseExpression();
            }
        }

        SkipNewLines();
        if (MatchWord("weighted"))
        {
            SkipNewLines();
            ExpectWord("by");
            SkipNewLines();
            (weightIdentifier, weightNames) = ParseBindingNames();
            SkipNewLines();
            Expect(ProjectionArrow);
            SkipNewLines();
            weightExpression = ParseExpression();
        }

        return WithRange(new GesChooseSelectorNode(count, atRandom, identifier, predicate, weightIdentifier, weightExpression) { BindingNames = names, WeightBindingNames = weightNames }, startToken);
    }

    private GesExpressionNode ParseExtensionCallExpression()
    {
        var startToken = Current;
        var (extensionName, functionName, _) = ParseExtensionSymbol();
        if (extensionName is "integer" or "degree") throw new GameEventScriptParseException($"Standard extension namespace ':{extensionName}' is not supported; use direct math intrinsics.", startToken);

        SkipNewLines();

        GesArgumentListNode arguments;
        var parenthesized = Match(LeftParen);
        if (parenthesized)
        {
            arguments = ParseArgumentListAfterLeftParen();
        }
        else if (Current.Kind == Of)
        {
            arguments = ParseOfArgumentList();
        }
        else if (IsArgumentLabelStart())
        {
            arguments = ParseUngroupedLabeledArgumentList();
        }
        else if (IsExtensionUnaryArgumentStart() && !(_choiceConditionDepth == _delimiterDepth && Current.Kind == Identifier && Current.Text == "then"))
        {
            arguments = new GesArgumentListNode([new GesArgumentNode(null, ParseUnaryExpression())]);
        }
        else
        {
            arguments = GesArgumentListNode.Empty;
        }

        var expression = WithRange(new GesExtensionCallExpressionNode(extensionName, functionName, arguments), startToken);
        return parenthesized ? ParsePostfixSuffixes(expression) : expression;
    }

    private GesTypeConstructorExpressionNode ParseTypeConstructorExpression()
    {
        var startToken = Current;
        var typeName = ParseTypeName();
        SkipNewLines();
        Expect(LeftParen);
        if (typeName == "handler" && Current.Kind == Message)
        {
            var name = Advance().Text;
            Expect(LeftParen); SkipNewLines();
            var parameters = new List<GesParameterNode>();
            if (!Is(RightParen))
            {
                do
                {
                    SkipNewLines();
                    if (Match(Underscore)) parameters.Add(new GesParameterNode(null, "literalArgument" + parameters.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                    else { var label = ExpectIdentifier(); parameters.Add(new GesParameterNode(label, label)); }
                    SkipNewLines();
                } while (Match(Comma));
            }
            var usedNames = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var parameter in parameters) if (parameter.ExternalLabel is not null) usedNames.Add(parameter.LocalName);
            for (var i = 0; i < parameters.Count; i++) if (parameters[i].ExternalLabel is null)
            {
                var local = parameters[i].LocalName;
                while (!usedNames.Add(local)) local += "x";
                parameters[i] = parameters[i] with { LocalName = local };
            }
            Expect(RightParen); SkipNewLines(); Expect(RightParen);
            return WithRange(new GesTypeConstructorExpressionNode(typeName, new GesArgumentListNode([new GesArgumentNode(null, new GesHandlerLiteralExpressionNode(name, parameters))])), startToken);
        }
        if (typeName == "message" && Current.Kind == Message)
        {
            var message = ParseMessageLiteralExpressionCore();
            var tags = ParseOptionalTagExpressions();
            Expect(RightParen);
            var items = new List<GesArgumentNode> { new(null, message) };
            if (tags.Count > 0) items.Add(new("tags", new GesListLiteralExpressionNode(tags)));
            return WithRange(new GesTypeConstructorExpressionNode(typeName, new GesArgumentListNode(items)), startToken);
        }
        if (typeName == "series" && (Current.Kind is Fibonacci or Factorial))
        {
            var series = Advance();
            var items = new List<GesArgumentNode> { new(null, new GesTextLiteralExpressionNode(series.Text)) };
            if (Match(Comma)) items.AddRange(ParseArgumentListAfterLeftParen().Arguments);
            else Expect(RightParen);
            return WithRange(new GesTypeConstructorExpressionNode(typeName, new GesArgumentListNode(items)), startToken);
        }
        var arguments = ParseArgumentListAfterLeftParen();
        return WithRange(new GesTypeConstructorExpressionNode(typeName, arguments), startToken);
    }

    private GesExpressionNode ParseClampExpression()
    {
        var startToken = Previous;
        SkipNewLines();
        var value = ParseUnaryExpression();
        SkipNewLines();
        ExpectWord("between");
        SkipNewLines();
        var minimum = ParseEqualityExpression();
        SkipNewLines();
        Expect(OperatorAnd);
        SkipNewLines();
        var maximum = ParseEqualityExpression();
        return WithRange(new GesClampExpressionNode(value, minimum, maximum), startToken);
    }

    private GesExpressionNode ParseVariadicTaggedExpression(string op)
    {
        var startToken = Previous;
        SkipNewLines();
        Expect(Of);
        SkipNewLines();
        var arguments = new List<GesExpressionNode> { ParseEqualityExpression() };
        while (!IsForClauseStart() && Match(OperatorAnd))
        {
            SkipNewLines();
            arguments.Add(ParseEqualityExpression());
        }

        return WithRange(new GesVariadicTaggedExpressionNode(op, arguments), startToken);
    }

    private GesToken ParseDiceSideCountToken()
    {
        if (Is(Identifier))
        {
            var token = Current;
            if (string.Equals(token.Text, "d", StringComparison.Ordinal))
            {
                Advance();
                return Expect(GesTokenKind.Float);
            }

            if (IsCompactDiceToken(token.Text))
            {
                Advance();
                return new GesToken(
                    GesTokenKind.Float,
                    token.Text[1..],
                    token.Line,
                    token.Column + 1,
                    token.EndLine,
                    token.EndColumn);
            }
        }

        var current = Current;
        throw new GameEventScriptParseException($"Expected dice separator but found {current.Kind}", current.Line, current.Column);
    }

    private static bool IsCompactDiceToken(string text)
    {
        if (text.Length <= 1 || text[0] != 'd') return false;

        for (var i = 1; i < text.Length; i++)
        {
            if (!GesText.IsAsciiDigit(text[i])) return false;
        }

        return true;
    }

    private int ParsePositiveInteger(GesToken numberToken, string name)
    {
        var value = numberToken.FloatValue;
        if (!double.IsFinite(value) ||
            value <= 0 ||
            value != Math.Truncate(value))
        {
            throw new GameEventScriptParseException($"Expected positive integer for {name}", numberToken.Line, numberToken.Column);
        }

        if (value > int.MaxValue) throw new GameEventScriptParseException($"{name} is too large", numberToken.Line, numberToken.Column);

        return (int)value;
    }

    private GesToken Expect(GesTokenKind kind)
    {
        ThrowIfIllegalToken();

        if (Is(kind)) return Advance();

        var token = Current;
        throw new GameEventScriptParseException($"Expected {kind} but found {token.Kind}", token);
    }

    private string ExpectIdentifier()
    {
        ThrowIfIllegalToken();

        if (Is(Identifier)) return Advance().Text;

        var token = Current;
        throw new GameEventScriptParseException($"Expected Identifier but found {token.Kind}", token);
    }

    private bool Match(GesTokenKind kind)
    {
        if (!Is(kind)) return false;
        Advance();
        return true;
    }

    // Matches any of the given kinds; the span is stack-allocated, so matching allocates nothing.
    private bool Match(params ReadOnlySpan<GesTokenKind> kinds)
    {
        foreach (var kind in kinds)
        {
            if (!Is(kind)) continue;
            Advance();
            return true;
        }

        return false;
    }

    private bool MatchSelector(string selectorText)
    {
        if (Current.Kind == Selector && string.Equals(Current.Text, selectorText, StringComparison.Ordinal))
        {
            Advance();
            return true;
        }

        return false;
    }

    private bool Is(GesTokenKind kind) => Current.Kind == kind;

    private bool IsNextSignificantToken(GesTokenKind kind) => _reader.PeekSignificant(1).Kind == kind;

    private bool IsSeededRandomStatementStart()
    {
        if (Current.Kind != KeywordRandom) return false;

        return _reader.PeekSignificant(1).Kind == With;
    }

    private bool IsElseClauseStart()
    {
        return _reader.PeekSignificant(0).Kind == Else;
    }

    private bool IsCallExpressionStart()
    {
        if (Current.Kind is not (Identifier or Message)) return false;

        return _reader.PeekSignificant(1).Kind == LeftParen;
    }

    private bool IsExtensionCallStart()
    {
        if (!Current.Text.StartsWith(":", StringComparison.Ordinal) || Current.Kind == TypeName) return false;

        if (_reader.PeekSignificant(1).Kind != Dot) return false;

        if (!IsExtensionFunctionNameKind(_reader.PeekSignificant(2).Kind)) return false;

        return true;
    }

    private bool IsTypeConstructorStart()
    {
        if (Current.Kind != TypeName) return false;

        return _reader.PeekSignificant(1).Kind == LeftParen;
    }

    private (string ExtensionName, string FunctionName, GesToken EndToken) ParseExtensionSymbol()
    {
        var extensionToken = Advance();
        SkipNewLines();
        var dot = Expect(Dot);
        SkipNewLines();
        var functionToken = ExpectExtensionFunctionName();
        if (extensionToken.EndLine != dot.Line || extensionToken.EndColumn != dot.Column || dot.EndLine != functionToken.Line || dot.EndColumn != functionToken.Column)
            throw new GameEventScriptParseException("Extension names must be contiguous: :namespace.name.", extensionToken);
        return (extensionToken.Text[1..], functionToken.Text, functionToken);
    }

    private bool IsExtensionUnaryArgumentStart()
        => !IsPipelineSelectorStart() && Current.Kind is Identifier or Message or Tag or Selector or TypeName or ConstantReference or GesTokenKind.Float or Percentage or UnitNumber or Text or True or False or Nothing or
            MathConstantPi or MathConstantE or MathConstantTau or MathConstantInfinity or
            IntrinsicAbs or IntrinsicLn or IntrinsicExp or IntrinsicSqrt or IntrinsicCbrt or IntrinsicChance or
            IntrinsicFloor or IntrinsicCeil or IntrinsicTruncate or IntrinsicRad or IntrinsicDeg or IntrinsicWrap or IntrinsicRound or
            IntrinsicSin or IntrinsicCos or IntrinsicTan or IntrinsicAsin or IntrinsicAcos or IntrinsicAtan or IntrinsicAtan2 or
            IntrinsicHypot or IntrinsicDistance or IntrinsicSquared or IntrinsicLength or IntrinsicNormalize or IntrinsicDot or IntrinsicCross or IntrinsicAngle or
            KeywordParse or KeywordRandom or Series or Clamp or Min or Max or Roll or
            LeftBracket or LeftParen or OperatorMinus or Has or Empty or OperatorNot;

    private bool IsArgumentLabelStart()
    {
        if (!IsArgumentLabelKind(Current.Kind)) return false;

        return _reader.PeekSignificant(1).Kind == Colon;
    }

    private string ExpectArgumentLabel()
    {
        ThrowIfIllegalToken();

        if (IsArgumentLabelKind(Current.Kind)) return Advance().Text;

        var token = Current;
        throw new GameEventScriptParseException($"Expected argument label but found {token.Kind}", token);
    }

    private static bool IsArgumentLabelKind(GesTokenKind kind)
        => kind is Identifier or To or Nothing or MathConstantPi or MathConstantE or MathConstantTau or MathConstantInfinity or
            IntrinsicAbs or IntrinsicLn or IntrinsicExp or IntrinsicSqrt or IntrinsicCbrt or IntrinsicChance or
            IntrinsicFloor or IntrinsicCeil or IntrinsicTruncate or IntrinsicRad or IntrinsicDeg or IntrinsicWrap or IntrinsicRound or
            IntrinsicSin or IntrinsicCos or IntrinsicTan or IntrinsicAsin or IntrinsicAcos or IntrinsicAtan or IntrinsicAtan2 or
            IntrinsicHypot or IntrinsicDistance or IntrinsicSquared or IntrinsicLength or IntrinsicNormalize or IntrinsicDot or IntrinsicCross or IntrinsicAngle;

    private static bool IsExtensionFunctionNameKind(GesTokenKind kind)
        => kind is Identifier or
            IntrinsicAbs or IntrinsicLn or IntrinsicExp or IntrinsicSqrt or IntrinsicCbrt or IntrinsicChance or
            IntrinsicFloor or IntrinsicCeil or IntrinsicTruncate or IntrinsicRad or IntrinsicDeg or IntrinsicWrap or IntrinsicRound or
            IntrinsicSin or IntrinsicCos or IntrinsicTan or IntrinsicAsin or IntrinsicAcos or IntrinsicAtan or IntrinsicAtan2 or
            IntrinsicHypot or IntrinsicDistance or IntrinsicSquared or IntrinsicLength or IntrinsicNormalize or IntrinsicDot or IntrinsicCross or IntrinsicAngle;

    private GesToken ExpectExtensionFunctionName()
    {
        ThrowIfIllegalToken();

        if (IsExtensionFunctionNameKind(Current.Kind)) return Advance();

        var token = Current;
        throw new GameEventScriptParseException($"Expected extension function name but found {token.Kind}", token);
    }

    private string ParseTypeName()
    {
        ThrowIfIllegalToken();

        if (!Is(TypeName))
        {
            var token = Current;
            throw new GameEventScriptParseException($"Expected type name but found {token.Kind}", token);
        }

        var typeToken = Advance();
        var typeName = typeToken.Text[1..];

        if (string.Equals(typeName, "Numeric", StringComparison.Ordinal)) throw new GameEventScriptParseException("The numeric type helper is a keyword; use numeric without ':'.", typeToken);

        if (string.Equals(typeName, "Quantity", StringComparison.Ordinal) &&
            Is(LeftParen) &&
            IsQuantityTypeSpecifierAhead())
        {
            SkipNewLines();
            Expect(LeftParen);
            SkipNewLines();
            var unitToken = Expect(Identifier);
            SkipNewLines();
            Expect(RightParen);
            return $"quantity:{unitToken.Text}";
        }

        return typeName switch
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
    }

    private bool IsQuantityTypeSpecifierAhead()
    {
        if (_reader.PeekSignificant(0).Kind != LeftParen) return false;

        if (_reader.PeekSignificant(1).Kind != Identifier) return false;

        return _reader.PeekSignificant(2).Kind == RightParen;
    }

    private void ExpectValueWord()
    {
        if (Current.Kind == Identifier && string.Equals(Current.Text, "value", StringComparison.Ordinal))
        {
            Advance();
            return;
        }

        throw new GameEventScriptParseException(
            $"Expected value but found {Current.Text}",
            Current);
    }

    private bool MatchWord(string word)
    {
        if (IsWord(word))
        {
            Advance();
            return true;
        }

        return false;
    }

    private bool IsWord(string word) => Current.Kind == Identifier && string.Equals(Current.Text, word, StringComparison.Ordinal);

    private bool IsNextSignificantWord(string word)
    {
        var lookahead = _reader.PeekSignificant(1);
        return lookahead.Kind == Identifier && string.Equals(lookahead.Text, word, StringComparison.Ordinal);
    }

    private void ExpectWord(string word)
    {
        ThrowIfIllegalToken();

        if (MatchWord(word)) return;

        throw new GameEventScriptParseException(
            $"Expected {word} but found {Current.Text}",
            Current);
    }

    private void ThrowIfIllegalToken()
    {
        if (Current.Kind == Illegal) throw new GameEventScriptParseException($"Illegal token '{Current.Text}'", Current);
    }

    private void SkipNewLines()
    {
        while (Is(NewLine)) Advance();
    }

    private void SkipStatementSeparators()
    {
        while (Match(Semicolon, NewLine))
        {
        }
    }

    private void RequireStatementSeparatorOrClosing(GesTokenKind closingKind)
    {
        if (Is(closingKind)) return;

        if (Match(Semicolon, NewLine))
        {
            SkipStatementSeparators();
            return;
        }

        var token = Current;
        throw new GameEventScriptParseException(
            $"Expected statement separator or '{closingKind}' but found {token.Kind}",
            token);
    }

    private void RequireHandlerSeparatorOrEndOfFile()
    {
        if (Is(EndOfFile)) return;

        if (Match(Semicolon, NewLine))
        {
            SkipStatementSeparators();
            return;
        }

        var token = Current;
        throw new GameEventScriptParseException(
            $"Expected event handler separator but found {token.Kind}",
            token);
    }

    private GesToken Advance()
    {
        var token = _reader.Advance();
        if (token.Kind is LeftParen or LeftBracket or LeftBrace) _delimiterDepth++;
        else if (token.Kind is RightParen or RightBracket or RightBrace) _delimiterDepth--;
        return token;
    }

    private GesToken Current => _reader.Current;

    private GesToken Previous => _reader.Previous;

    private void AddParseError(GameEventScriptParseException exception)
    {
        _errors.Add(new GesDiagnostic(
            GesDiagnosticPhase.Parse,
            GesDiagnosticCodes.ParseSyntax,
            exception.Message,
            SourceLocation: new GesSourceLocation(SourceName, exception.Line, exception.Column, exception.EndLine, exception.EndColumn, ModuleName, _sourceId),
            ProgramName: ModuleName));
    }

    private void SynchronizeTopLevel()
    {
        while (!Is(EndOfFile))
        {
            if (Is(Module) || Is(Constant) || Is(Record) || Is(Predicate) || Is(Function) || Is(On)) return;

            Advance();
        }
    }

    private bool SynchronizeStatement(GesTokenKind closingKind)
    {
        while (!Is(EndOfFile))
        {
            if (Is(closingKind)) return false;

            if (Match(Semicolon, NewLine))
            {
                SkipStatementSeparators();
                return true;
            }

            Advance();
        }

        return false;
    }
}
