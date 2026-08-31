using System;
using System.Collections.Generic;
using System.Globalization;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using static StepH.GameEventScript.Compiler.GesTokenKind;

namespace StepH.GameEventScript.Compiler;

internal sealed class GesParser
{
    private const double SquareRootExponent = 0.5d;
    private const double CubeRootExponent = 0.3333333333333333333333333333d;

    public static ParsedScript Parse(string script, string? sourceName = null, uint? sourceId = null)
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
    private readonly List<GameEventScriptCompileError> _errors;
    private string? _moduleName;
    private string? _sourceName;

    private GesParser(GesTokenReader reader, string? sourceName, uint? sourceId)
    {
        _reader = reader;
        _requestedSourceName = sourceName;
        _sourceId = sourceId;
        _errors = [];
    }

    private string ModuleName => _moduleName ?? string.Empty;

    private string SourceName => _sourceName ??= string.IsNullOrWhiteSpace(_requestedSourceName) ? "UnknownSource" : _requestedSourceName;

    private GameEventScriptSourceLocation CreateRange(GesToken token)
        => new(SourceName, token.Line, token.Column, token.EndLine, token.EndColumn, ModuleName, _sourceId);

    private GameEventScriptSourceLocation CreateRange(GesToken start, GesToken end)
        => new(SourceName, start.Line, start.Column, end.EndLine, end.EndColumn, ModuleName, _sourceId);

    private GameEventScriptSourceLocation? MergeRanges(ScriptNode? first, ScriptNode? last = null)
    {
        var start = first?.SourceRange;
        var end = (last ?? first)?.SourceRange;
        if (start is null || end is null || start.Line is null || start.Column is null)
        {
            return null;
        }

        var endLine = end.EndLine ?? end.Line;
        var endColumn = end.EndColumn ?? end.Column;
        return endLine is null || endColumn is null
            ? null
            : new GameEventScriptSourceLocation(SourceName, start.Line, start.Column, endLine, endColumn, ModuleName, _sourceId);
    }

    private T WithRange<T>(T node, GesToken startToken) where T : ScriptNode
        => node with { SourceRange = CreateRange(startToken, Previous) };

    private T WithRange<T>(T node, GesToken startToken, GesToken endToken) where T : ScriptNode
        => node with { SourceRange = CreateRange(startToken, endToken) };

    private T WithRange<T>(T node, ScriptNode? first, ScriptNode? last = null) where T : ScriptNode
        => node with { SourceRange = MergeRanges(first, last) };

    private ExpressionNode ApplyIsNegation(ExpressionNode expression, bool negated)
        => negated
            ? WithRange(new UnaryExpressionNode(GesUnaryOperator.Not, expression), expression)
            : expression;

    private ParsedScript ParseScript()
    {
        var typeDefinitions = new List<TypeDefinitionNode>();
        var predicateDefinitions = new List<PredicateDefinitionNode>();
        var functionDefinitions = new List<FunctionDefinitionNode>();
        var handlers = new List<EventHandlerNode>();
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

                if (Match(Record))
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

        if (_errors.Count > 0)
        {
            throw new GameEventScriptCompileException(_errors);
        }

        var module = new ParsedScript(ModuleName, SourceName, typeDefinitions, predicateDefinitions, functionDefinitions, handlers);
        var firstToken = _reader.FirstToken;
        if (firstToken.Kind == EndOfFile)
        {
            return module with { SourceRange = CreateRange(firstToken) };
        }

        return module with { SourceRange = CreateRange(firstToken, _reader.LastNonEofToken) };
    }

    private void ParseOptionalModuleDeclaration()
    {
        if (!Match(Module))
        {
            return;
        }

        SkipNewLines();
        ThrowIfIllegalToken();
        if (Current.Kind is not (Identifier or Message))
        {
            throw new GameEventScriptParseException($"Expected module name but found {Current.Kind}", Current.Line, Current.Column);
        }

        _moduleName = Advance().Text;
    }

    private TypeDefinitionNode ParseTypeDefinition()
    {
        var startToken = Previous;
        var name = ParseTypeName();
        Expect(As);
        SkipNewLines();
        Expect(LeftBrace);
        SkipStatementSeparators();

        var fields = new List<TypeFieldDefinitionNode>();
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
        return WithRange(new TypeDefinitionNode(name, fields), startToken);
    }

    private TypeFieldDefinitionNode ParseTypeFieldDefinition()
    {
        var startToken = Current;
        var isUnlabeledConstructorParameter = Match(Underscore);
        if (isUnlabeledConstructorParameter)
        {
            SkipNewLines();
        }

        var name = ExpectIdentifier();
        Expect(Colon);
        SkipNewLines();
        var typeName = ParseTypeName();
        SkipNewLines();

        ExpressionNode? minimumExpression = null;
        ExpressionNode? maximumExpression = null;
        ExpressionNode? computedExpression = null;

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
            computedExpression = ParseExpression();
        }

        if (isUnlabeledConstructorParameter && computedExpression is not null)
        {
            throw new GameEventScriptParseException("Computed record fields cannot be constructor parameters.", startToken);
        }

        var constructorLabel = computedExpression is null
            ? isUnlabeledConstructorParameter
                ? GameEventScriptMessageSignature.UnlabeledParameterName
                : name
            : null;

        return WithRange(new TypeFieldDefinitionNode(name, typeName, minimumExpression, maximumExpression, computedExpression, constructorLabel), startToken);
    }

    private PredicateDefinitionNode ParsePredicateDefinition()
    {
        var startToken = Previous;
        var name = ExpectIdentifier();
        var parameters = ParseDefinitionParameters();
        Expect(Be);
        SkipNewLines();
        var expression = ParseExpression();
        return WithRange(new PredicateDefinitionNode(name, parameters, expression), startToken);
    }

    private FunctionDefinitionNode ParseFunctionDefinition()
    {
        var startToken = Previous;
        var name = ExpectIdentifier();
        var parameters = ParseDefinitionParameters();
        Expect(Be);
        SkipNewLines();
        var expression = ParseExpression();
        return WithRange(new FunctionDefinitionNode(name, parameters, expression), startToken);
    }

    private IReadOnlyList<ParameterNode> ParseDefinitionParameters()
    {
        var parameters = new List<ParameterNode>();
        Expect(LeftParen);
        if (!Is(RightParen))
        {
            parameters.Add(ParseParameter());
            while (Match(Comma))
            {
                parameters.Add(ParseParameter());
            }
        }

        Expect(RightParen);
        return parameters;
    }

    private ParameterNode ParseParameter(bool allowDeclaredType = true)
    {
        var startToken = Current;
        if (Match(Underscore))
        {
            SkipNewLines();
            var localName = ExpectIdentifier();
            var declaredType = allowDeclaredType ? ParseOptionalParameterDeclaredType() : null;
            return WithRange(new ParameterNode(null, localName, declaredType), startToken);
        }

        var name = ExpectIdentifier();
        var typeName = allowDeclaredType ? ParseOptionalParameterDeclaredType() : null;
        return WithRange(new ParameterNode(name, name, typeName), startToken);
    }

    private string? ParseOptionalParameterDeclaredType()
    {
        SkipNewLines();
        if (!Match(As))
        {
            return null;
        }

        SkipNewLines();
        return ParseTypeName();
    }

    private EventHandlerNode ParseEventHandler()
    {
        var startToken = Current;
        Expect(On);
        SkipNewLines();
        var message = ParseEventHandlerMessageName();

        var dispatchKind = EventHandlerDispatchKind.ExactSignature;
        var parameters = new List<ParameterNode>();
        if (Match(As))
        {
            SkipNewLines();
            var messageToken = Current;
            var messageLocalName = ExpectIdentifier();
            dispatchKind = EventHandlerDispatchKind.MessageName;
            parameters.Add(WithRange(new ParameterNode(
                GameEventScriptSystemEndpoints.MessageArgumentName,
                messageLocalName,
                "message"), messageToken));
        }
        else if (Match(LeftParen))
        {
            if (!Is(RightParen))
            {
                parameters.Add(ParseParameter());
                while (Match(Comma))
                {
                    parameters.Add(ParseParameter());
                }
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

        return WithRange(new EventHandlerNode(message, dispatchKind, parameters, statements, requiredTags, excludedTags), startToken);
    }

    private string ParseEventHandlerMessageName()
    {
        ThrowIfIllegalToken();

        if (Is(Message))
        {
            return Advance().Text;
        }

        if (Current.Kind == Identifier &&
            GameEventScriptSystemEndpoints.IsSystemEndpointName(Current.Text))
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
            if (!tagToken.Text.StartsWith("#", StringComparison.Ordinal))
            {
                throw new GameEventScriptParseException($"Expected tag literal but found {tagToken.Text}", tagToken);
            }

            var tag = tagToken.Text[1..];
            var normalized = GameEventScriptMessage.NormalizeTagName(tag);
            if (normalized.Length > 0 && !ContainsTag(tags, normalized))
            {
                tags.Add(normalized);
            }

            SkipNewLines();
        }
        while (Match(Comma));
    }

    private static bool ContainsTag(IReadOnlyList<string> tags, string normalized)
    {
        for (var index = 0; index < tags.Count; index++)
        {
            if (string.Equals(tags[index], normalized, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private IReadOnlyList<StatementNode> ParseStatementsUntil(GesTokenKind closingKind)
    {
        var statements = new List<StatementNode>();
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
                if (!SynchronizeStatement(closingKind))
                {
                    return statements;
                }
            }
        }

        return statements;
    }

    private StatementNode ParseStatement()
    {
        if (IsSeededRandomStatementStart())
        {
            return ParseSeededRandomStatement();
        }

        if (Match(Emit))
        {
            return ParsePublishStatement(PublishStatementKind.Emit);
        }

        if (Match(Publish))
        {
            return ParsePublishStatement(PublishStatementKind.Publish);
        }

        if (Match(Let))
        {
            return ParseLetStatement();
        }

        if (Match(If))
        {
            return ParseIfStatement();
        }

        if (Match(For))
        {
            return ParseForStatement();
        }

        var expression = ParseExpression();
        return WithRange(new ExpressionStatementNode(expression), expression);
    }

    private PublishStatementNode ParsePublishStatement(PublishStatementKind kind)
    {
        var startToken = Previous;
        SkipNewLines();
        var messageExpression = ParsePublishMessageExpression();
        var tagExpressions = ParseOptionalTagExpressions();
        return WithRange(new PublishStatementNode(kind, messageExpression, tagExpressions), startToken);
    }

    private ExpressionNode ParsePublishMessageExpression()
    {
        if (Current.Kind == Message)
        {
            return ParseMessageLiteralExpressionCore();
        }

        return ParseExpression();
    }

    private IReadOnlyList<ExpressionNode> ParseOptionalTagExpressions()
    {
        if (!Match(With))
        {
            return [];
        }

        var tags = new List<ExpressionNode>();
        do
        {
            SkipNewLines();
            tags.Add(ParseExpression());
        }
        while (Match(Comma));

        return tags;
    }

    private ArgumentNode ParseArgument()
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
                return WithRange(new ArgumentNode(label, expression), startToken);
            }
        }

        var value = ParseExpression();
        return WithRange(new ArgumentNode(null, value), startToken);
    }

    private ArgumentListNode ParseArgumentListAfterLeftParen()
    {
        SkipNewLines();
        if (Match(RightParen))
        {
            return ArgumentListNode.Empty;
        }

        var arguments = ParseArgumentListAfterFirstArgument(ParseArgument(), RightParen);
        Expect(RightParen);
        return arguments;
    }

    private ArgumentListNode ParseArgumentListAfterFirstArgument(ArgumentNode firstArgument, GesTokenKind closingKind)
    {
        var arguments = new List<ArgumentNode> { firstArgument };
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

            return new ArgumentListNode(arguments);
        }
    }

    private ArgumentListNode ParseUngroupedLabeledArgumentList()
    {
        if (!IsArgumentLabelStart())
        {
            return ArgumentListNode.Empty;
        }

        var arguments = new List<ArgumentNode>();
        do
        {
            arguments.Add(ParseArgument());
        }
        while (IsArgumentLabelStart());

        return new ArgumentListNode(arguments);
    }

    private ArgumentListNode ParseOfArgumentList()
    {
        var arguments = new List<ArgumentNode>();
        ExpectWord("of");
        SkipNewLines();
        arguments.Add(new ArgumentNode(null, ParseEqualityExpression()));
        while (Match(OperatorAnd))
        {
            SkipNewLines();
            arguments.Add(new ArgumentNode(null, ParseEqualityExpression()));
        }

        return new ArgumentListNode(arguments);
    }

    private LetStatementNode ParseLetStatement()
    {
        var startToken = Previous;
        var identifier = ExpectIdentifier();
        string? declaredType = null;
        if (Match(As))
        {
            SkipNewLines();
            declaredType = ParseTypeName();
        }

        if (Match(Be))
        {
            var expression = ParseExpression();
            return WithRange(new LetStatementNode(identifier, declaredType, expression), startToken);
        }
        var token = Current;
        throw new GameEventScriptParseException($"Expected {Be} but found {token.Kind}", token.Line, token.Column);
    }

    private IfStatementNode ParseIfStatement()
    {
        var startToken = Previous;
        var condition = ParseExpression();
        var thenBody = ParseStatementBody();

        StatementBodyNode? elseBody = null;
        if (IsElseClauseStart())
        {
            SkipNewLines();
            Expect(Else);
            elseBody = ParseStatementBody();
        }

        return WithRange(new IfStatementNode(condition, thenBody, elseBody), startToken);
    }

    private ForStatementNode ParseForStatement()
    {
        var startToken = Previous;
        var identifier = ExpectIdentifier();
        SkipNewLines();
        IterationSourceNode source;
        if (Match(In))
        {
            SkipNewLines();
            if (Current.Kind == Identifier && string.Equals(Current.Text, "from", StringComparison.Ordinal))
            {
                throw new GameEventScriptParseException("Direct ranges are not allowed after 'in'; use 'for item from ... to ...' or iterate a range variable", Current.Line, Current.Column);
            }

            var sourceStart = Current;
            source = WithRange(new CollectionIterationSourceNode(ParseExpression()), sourceStart);
        }
        else if (MatchWord("from"))
        {
            var fromToken = Previous;
            source = WithRange(new RangeIterationSourceNode(ParseRangeExpressionCore()), fromToken);
        }
        else
        {
            var token = Current;
            throw new GameEventScriptParseException($"Expected {In} or 'from' but found {token.Text}", token.Line, token.Column);
        }

        var body = ParseStatementBody();
        return WithRange(new ForStatementNode(identifier, source, body), startToken);
    }

    private SeededRandomStatementNode ParseSeededRandomStatement()
    {
        var startToken = Current;
        Expect(KeywordRandom);
        SkipNewLines();
        Expect(With);
        SkipNewLines();
        var seedExpression = ParseExpression();
        var body = ParseStatementBody();
        return WithRange(new SeededRandomStatementNode(seedExpression, body), startToken);
    }

    private StatementBodyNode ParseStatementBody()
    {
        var startToken = Current;
        SkipNewLines();
        if (Match(LeftBrace))
        {
            var statements = ParseStatementsUntil(RightBrace);
            Expect(RightBrace);
            return WithRange(new StatementBodyNode(true, statements), startToken);
        }

        return WithRange(new StatementBodyNode(false, [ParseStatement()]), startToken);
    }

    private ExpressionNode ParseExpression() => ParseGuardedChoiceExpression();

    private ExpressionNode ParseGuardedChoiceExpression()
    {
        var expression = ParseImplicationExpression();
        if (Match(When))
        {
            var branches = new List<GuardedChoiceBranchNode>();
            var conditionExpression = ParseImplicationExpression();
            branches.Add(new GuardedChoiceBranchNode(expression, conditionExpression));

            while (Match(Comma))
            {
                SkipNewLines();
                if (Is(Otherwise))
                {
                    break;
                }

                Match(OperatorOr);
                SkipNewLines();

                var branchValue = ParseImplicationExpression();
                SkipNewLines();
                Expect(When);
                SkipNewLines();
                var branchCondition = ParseImplicationExpression();
                branches.Add(new GuardedChoiceBranchNode(branchValue, branchCondition));
            }

            SkipNewLines();
            Expect(Otherwise);
            SkipNewLines();
            var otherwiseExpression = ParseImplicationExpression();
            expression = WithRange(new GuardedChoiceExpressionNode(branches, otherwiseExpression), expression, otherwiseExpression);
        }

        return expression;
    }

    private ExpressionNode ParseImplicationExpression()
    {
        var expression = ParseDefaultExpression();
        if (!Match(OperatorImplication))
        {
            return expression;
        }

        SkipNewLines();
        var right = ParseImplicationExpression();
        return WithRange(new BinaryExpressionNode(expression, GesBinaryOperator.Implies, right), expression, right);
    }

    private ExpressionNode ParseDefaultExpression()
    {
        var expression = ParseCollectionUnionExpression();

        while (Match(Default))
        {
            SkipNewLines();
            var right = ParseCollectionUnionExpression();
            expression = WithRange(new BinaryExpressionNode(expression, GesBinaryOperator.Default, right), expression, right);
        }

        return expression;
    }

    private ExpressionNode ParseCollectionUnionExpression()
    {
        var expression = ParseCollectionIntersectExpression();

        while (Match(OperatorCollectionUnion))
        {
            SkipNewLines();
            var right = ParseCollectionIntersectExpression();
            expression = WithRange(new BinaryExpressionNode(expression, GesBinaryOperator.Union, right), expression, right);
        }

        return expression;
    }

    private ExpressionNode ParseCollectionIntersectExpression()
    {
        var expression = ParseCollectionZipExpression();

        while (Match(OperatorCollectionIntersect))
        {
            SkipNewLines();
            var right = ParseCollectionZipExpression();
            expression = WithRange(new BinaryExpressionNode(expression, GesBinaryOperator.Intersect, right), expression, right);
        }

        return expression;
    }

    private ExpressionNode ParseCollectionZipExpression()
    {
        var expression = ParseOrExpression();

        while (Current.Kind == Zip)
        {
            Advance();
            SkipNewLines();
            var right = ParseOrExpression();
            expression = WithRange(new BinaryExpressionNode(expression, GesBinaryOperator.Zip, right), expression, right);
        }

        return expression;
    }

    private ExpressionNode ParseOrExpression()
    {
        var expression = ParseXorExpression();

        while (Match(OperatorOr))
        {
            SkipNewLines();
            var right = ParseXorExpression();
            expression = WithRange(new BinaryExpressionNode(expression, GesBinaryOperator.Or, right), expression, right);
        }

        return expression;
    }

    private ExpressionNode ParseXorExpression()
    {
        var expression = ParseAndExpression();

        while (Match(OperatorXor))
        {
            SkipNewLines();
            var right = ParseAndExpression();
            expression = WithRange(new BinaryExpressionNode(expression, GesBinaryOperator.Xor, right), expression, right);
        }

        return expression;
    }

    private ExpressionNode ParseAndExpression()
    {
        var expression = ParseEqualityExpression();

        while (Match(OperatorAnd))
        {
            SkipNewLines();
            var right = ParseEqualityExpression();
            expression = WithRange(new BinaryExpressionNode(expression, GesBinaryOperator.And, right), expression, right);
        }

        return expression;
    }

    private ExpressionNode ParseEqualityExpression()
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
            expression = WithRange(new BinaryExpressionNode(expression, op, right), expression, right);
        }

        return expression;
    }

    private ExpressionNode ParseMembershipExpression()
    {
        var expression = ParseTypeOperationExpression();

        while (true)
        {
            if (Match(Has))
            {
                SkipNewLines();
                ExpectValueWord();
                expression = WithRange(new UnaryExpressionNode(GesUnaryOperator.HasValue, expression), expression);
                continue;
            }

            if (Match(In))
            {
                SkipNewLines();
                if (IsValuesOfOperator())
                {
                    ExpectWord("values");
                    SkipNewLines();
                    ExpectWord("of");
                    SkipNewLines();
                    var container = ParseTypeOperationExpression();
                    expression = WithRange(new BinaryExpressionNode(expression, GesBinaryOperator.ContainsValue, container), expression, container);
                    continue;
                }

                var right = ParseTypeOperationExpression();
                expression = WithRange(new BinaryExpressionNode(expression, GesBinaryOperator.Contains, right), expression, right);
                continue;
            }

            if (Match(NotIn))
            {
                SkipNewLines();
                var right = ParseTypeOperationExpression();
                var membership = WithRange(new BinaryExpressionNode(expression, GesBinaryOperator.Contains, right), expression, right);
                expression = WithRange(new UnaryExpressionNode(GesUnaryOperator.Not, membership), membership);
                continue;
            }

            if (Match(Starts))
            {
                SkipNewLines();
                Expect(With);
                SkipNewLines();
                var right = ParseTypeOperationExpression();
                expression = WithRange(new BinaryExpressionNode(expression, GesBinaryOperator.StartsWith, right), expression, right);
                continue;
            }

            if (Match(Ends))
            {
                SkipNewLines();
                Expect(With);
                SkipNewLines();
                var right = ParseTypeOperationExpression();
                expression = WithRange(new BinaryExpressionNode(expression, GesBinaryOperator.EndsWith, right), expression, right);
                continue;
            }

            break;
        }

        return expression;
    }

    private bool IsValuesOfOperator()
    {
        var first = _reader.PeekSignificant(0);
        if (first.Kind != Identifier || !string.Equals(first.Text, "values", StringComparison.Ordinal))
        {
            return false;
        }

        var second = _reader.PeekSignificant(1);
        return second.Kind == Identifier && string.Equals(second.Text, "of", StringComparison.Ordinal);
    }

    private DicePatternNode ParseDicePattern()
    {
        if (MatchWord("pair"))
        {
            var startToken = Previous;
            SkipNewLines();
            if (MatchWord("of"))
            {
                SkipNewLines();
                return WithRange(new DiceCountPatternNode(2, ParsePatternFace()), startToken);
            }

            return WithRange(new DiceCountPatternNode(2, null), startToken);
        }

        if (MatchWord("three"))
        {
            return ParseOfPattern(3);
        }

        if (MatchWord("four"))
        {
            return ParseOfPattern(4);
        }

        if (MatchWord("five"))
        {
            return ParseOfPattern(5);
        }

        if (MatchWord("six"))
        {
            return ParseOfPattern(6);
        }

        if (MatchWord("seven"))
        {
            return ParseOfPattern(7);
        }

        if (MatchWord("full"))
        {
            var startToken = Previous;
            SkipNewLines();
            ExpectWord("house");
            return WithRange(new DiceFullHousePatternNode(), startToken);
        }

        if (MatchWord("straight"))
        {
            return WithRange(new DiceStraightPatternNode(), Previous);
        }

        var token = Current;
        throw new GameEventScriptParseException($"Expected dice pattern but found {token.Text}", token.Line, token.Column);
    }

    private DicePatternNode ParseOfPattern(int count)
    {
        var startToken = Previous;
        SkipNewLines();
        ExpectWord("of");
        SkipNewLines();

        if (MatchWord("a"))
        {
            SkipNewLines();
            ExpectWord("kind");
            return WithRange(new DiceCountPatternNode(count, null), startToken);
        }

        return WithRange(new DiceCountPatternNode(count, ParsePatternFace()), startToken);
    }

    private ExpressionNode ParsePatternFace() => ParseUnaryExpression();

    private ExpressionNode ParseTypeOperationExpression()
    {
        var expression = ParseRelationalExpression();

        while (true)
        {
            if (Match(GesTokenKind.Is))
            {
                SkipNewLines();
                var negated = Match(OperatorNot);
                if (negated)
                {
                    SkipNewLines();
                }

                if (IsExtensionCallStart())
                {
                    var (extensionName, functionName, _) = ParseExtensionSymbol();
                    expression = ApplyIsNegation(WithRange(new ExtensionPredicateExpressionNode(expression, extensionName, functionName), expression), negated);
                    continue;
                }

                if (Match(Numeric))
                {
                    expression = ApplyIsNegation(WithRange(new TypeCheckExpressionNode(expression, "numeric"), expression), negated);
                    continue;
                }

                if (MatchWord("integer"))
                {
                    expression = ApplyIsNegation(WithRange(new TypeCheckExpressionNode(expression, "numeric:integer"), expression), negated);
                    continue;
                }

                if (MatchWord("fractional"))
                {
                    expression = ApplyIsNegation(WithRange(new TypeCheckExpressionNode(expression, "numeric:fractional"), expression), negated);
                    continue;
                }

                if (Is(Nothing) || Is(Tag))
                {
                    var typeName = ParseTypeName();
                    expression = ApplyIsNegation(WithRange(new TypeCheckExpressionNode(expression, typeName), expression), negated);
                    continue;
                }

                if (Match(Empty))
                {
                    expression = ApplyIsNegation(WithRange(new UnaryExpressionNode(GesUnaryOperator.Empty, expression), expression), negated);
                    continue;
                }

                if (MatchWord("at"))
                {
                    SkipNewLines();
                    if (MatchWord("least"))
                    {
                        SkipNewLines();
                        var right = ParseRelationalComparisonOperand();
                        expression = ApplyIsNegation(WithRange(new BinaryExpressionNode(expression, GesBinaryOperator.GreaterOrEqual, right), expression, right), negated);
                        continue;
                    }

                    if (MatchWord("most"))
                    {
                        SkipNewLines();
                        var right = ParseRelationalComparisonOperand();
                        expression = ApplyIsNegation(WithRange(new BinaryExpressionNode(expression, GesBinaryOperator.LessOrEqual, right), expression, right), negated);
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
                    expression = ApplyIsNegation(WithRange(new BinaryExpressionNode(expression, GesBinaryOperator.Less, right), expression, right), negated);
                    continue;
                }

                if (MatchWord("more"))
                {
                    SkipNewLines();
                    ExpectWord("than");
                    SkipNewLines();
                    var right = ParseRelationalComparisonOperand();
                    expression = ApplyIsNegation(WithRange(new BinaryExpressionNode(expression, GesBinaryOperator.Greater, right), expression, right), negated);
                    continue;
                }

                if (Current.Kind == Identifier)
                {
                    var predicateName = Advance().Text;
                    expression = ApplyIsNegation(WithRange(new PredicateCallExpressionNode(expression, predicateName), expression), negated);
                    continue;
                }

                var threshold = ParseRelationalComparisonOperand();
                SkipNewLines();
                if (Match(OperatorOr))
                {
                    SkipNewLines();
                    if (MatchWord("less"))
                    {
                        expression = ApplyIsNegation(WithRange(new BinaryExpressionNode(expression, GesBinaryOperator.LessOrEqual, threshold), expression, threshold), negated);
                        continue;
                    }

                    if (MatchWord("more"))
                    {
                        expression = ApplyIsNegation(WithRange(new BinaryExpressionNode(expression, GesBinaryOperator.GreaterOrEqual, threshold), expression, threshold), negated);
                        continue;
                    }

                    var token = Current;
                    throw new GameEventScriptParseException($"Expected less or more but found {token.Text}", token.Line, token.Column);
                }

                expression = ApplyIsNegation(WithRange(new BinaryExpressionNode(expression, GesBinaryOperator.Equal, threshold), expression, threshold), negated);
                continue;
            }

            if (Match(As))
            {
                SkipNewLines();
                var typeName = ParseTypeName();
                expression = WithRange(new TypeCastExpressionNode(expression, typeName), expression);
                continue;
            }

            break;
        }

        return expression;
    }

    private ExpressionNode ParseRelationalComparisonOperand()
        => ParseAdditiveExpression();

    private ExpressionNode ParseRelationalExpression()
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
            expression = WithRange(new BinaryExpressionNode(expression, op, right), expression, right);
        }

        return expression;
    }

    private ExpressionNode ParseAdditiveExpression()
    {
        var expression = ParseMultiplicativeExpression();

        while (Match(OperatorPlus, OperatorMinus))
        {
            var op = Previous.Kind == OperatorPlus
                ? GesBinaryOperator.Add
                : GesBinaryOperator.Subtract;
            SkipNewLines();
            var right = ParseMultiplicativeExpression();
            expression = WithRange(new BinaryExpressionNode(expression, op, right), expression, right);
        }

        return expression;
    }

    private ExpressionNode ParseMultiplicativeExpression()
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
                expression = WithRange(new BinaryExpressionNode(expression, op, right), expression, right);
                continue;
            }

            if (IsImplicitMultiplicationLeftExpression(expression) &&
                IsImplicitMultiplicationRightStart() &&
                AreAdjacent(Previous, Current))
            {
                var right = ParseUnaryExpression();
                expression = WithRange(new BinaryExpressionNode(expression, GesBinaryOperator.Multiply, right), expression, right);
                continue;
            }

            break;
        }

        return expression;
    }

    private ExpressionNode ParsePowerExpression()
    {
        var expression = ParsePowerBaseExpression();

        if (Match(OperatorPower))
        {
            SkipNewLines();
            var right = ParseUnaryExpression();
            expression = WithRange(new BinaryExpressionNode(expression, GesBinaryOperator.Power, right), expression, right);
        }
        else if (Match(SuperscriptInteger))
        {
            var exponentToken = Previous;
            var exponent = long.Parse(exponentToken.Text, CultureInfo.InvariantCulture);
            var right = WithRange(new IntegerLiteralExpressionNode(exponent), exponentToken);
            expression = WithRange(new BinaryExpressionNode(expression, GesBinaryOperator.Power, right), expression, right);
        }

        return expression;
    }

    private static bool IsImplicitMultiplicationLeftExpression(ExpressionNode expression)
        => expression is IntegerLiteralExpressionNode or FloatLiteralExpressionNode;

    private bool IsImplicitMultiplicationRightStart()
        => Current.Kind is Identifier or MathConstantPi or MathConstantE or MathConstantTau or MathConstantInfinity;

    private static bool AreAdjacent(GesToken left, GesToken right)
        => left.EndLine == right.Line &&
           left.EndColumn == right.Column;

    private ExpressionNode ParseUnaryExpression()
    {
        SkipNewLines();
        if (Match(OperatorMinus))
        {
            var opToken = Previous;
            SkipNewLines();
            var negativeOperand = ParseUnaryExpression();
            return WithRange(new UnaryExpressionNode(GesUnaryOperator.Negate, negativeOperand), opToken, Previous);
        }

        if (Match(Empty))
        {
            var opToken = Previous;
            SkipNewLines();
            var emptyOperand = ParseUnaryExpression();
            return WithRange(new UnaryExpressionNode(GesUnaryOperator.Empty, emptyOperand), opToken, Previous);
        }

        if (!Match(OperatorNot))
        {
            if (ParseIntrinsicCallExpression() is { } intrinsicCall)
            {
                return intrinsicCall;
            }

            if (ParseKeywordUnaryOperator() is { } keywordOperator)
            {
                var keywordOperand = ParseUnaryIntrinsicOperand();
                if (CreateRootPowerExpression(keywordOperator.RootExponent, keywordOperand, keywordOperator.StartToken) is { } rootExpression)
                {
                    return rootExpression;
                }

                return WithRange(new UnaryExpressionNode(keywordOperator.Operator, keywordOperand), keywordOperator.StartToken, Previous);
            }

            return ParsePowerExpression();
        }

        var startToken = Previous;
        SkipNewLines();
        var operand = ParseUnaryExpression();
        return WithRange(new UnaryExpressionNode(GesUnaryOperator.Not, operand), startToken, Previous);
    }

    private ExpressionNode ParsePowerBaseExpression()
    {
        if (Match(Series))
        {
            return ParseSeriesExpression(Previous);
        }

        if (IsExtensionCallStart())
        {
            return ParseExtensionCallExpression();
        }

        if (Match(Clamp))
        {
            return ParseClampExpression();
        }

        if (Is(Min) && IsNextSignificantWord("of"))
        {
            Advance();
            return ParseVariadicTaggedExpression("min");
        }

        if (Is(Max) && IsNextSignificantWord("of"))
        {
            Advance();
            return ParseVariadicTaggedExpression("max");
        }

        return ParsePostfixExpression();
    }

    private ExpressionNode ParseSeriesExpression(GesToken startToken)
    {
        SkipNewLines();
        if (Match(Fibonacci))
        {
            return WithRange(new SeriesExpressionNode(GameEventScriptBytecodeSeriesKind.Fibonacci), startToken, Previous);
        }

        if (Match(Factorial))
        {
            return WithRange(new SeriesExpressionNode(GameEventScriptBytecodeSeriesKind.Factorial), startToken, Previous);
        }

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

    private ExpressionNode? ParseIntrinsicCallExpression()
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
        return WithRange(new IntrinsicCallExpressionNode(function, arguments), startToken, Previous);
    }

    private IReadOnlyList<ExpressionNode> ParseIntrinsicArgumentExpressions(GesIntrinsicFunction function)
    {
        Expect(LeftParen);
        SkipNewLines();

        if (Match(RightParen))
        {
            return [];
        }

        var arguments = new List<ExpressionNode>();
        while (true)
        {
            arguments.Add(ParseExpression());
            SkipNewLines();

            if (Match(RightParen))
            {
                return arguments;
            }

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
            case MathConstantPi:
                return GameEventScriptMathConstants.Pi;
            case MathConstantE:
                return GameEventScriptMathConstants.E;
            case MathConstantTau:
                return GameEventScriptMathConstants.Tau;
            case MathConstantInfinity:
                return GameEventScriptMathConstants.Infinity;
            default:
                return null;
        }
    }

    private ExpressionNode ParseUnaryIntrinsicOperand()
    {
        SkipNewLines();
        if (!Match(LeftParen))
        {
            return ParseUnaryExpression();
        }

        SkipNewLines();
        var operand = ParseExpression();
        SkipNewLines();
        Expect(RightParen);
        return operand;
    }

    private ExpressionNode? CreateRootPowerExpression(
        double? rootExponent,
        ExpressionNode operand,
        GesToken startToken)
    {
        if (!rootExponent.HasValue)
        {
            return null;
        }

        var exponentNode = new FloatLiteralExpressionNode(rootExponent.Value)
        {
            SourceRange = CreateRange(startToken, Previous)
        };
        return WithRange(new BinaryExpressionNode(operand, GesBinaryOperator.Power, exponentNode), startToken, Previous);
    }

    private ExpressionNode ParsePostfixExpression()
    {
        var expression = ParsePrimaryExpression();

        while (true)
        {
            if (Match(Dot))
            {
                SkipNewLines();
                var member = ExpectIdentifier();
                expression = WithRange(new MemberAccessExpressionNode(expression, member), expression);
                continue;
            }

            if (Match(LeftBracket))
            {
                SkipNewLines();
                var selector = ParseCollectionSelector();
                SkipNewLines();
                Expect(RightBracket);
                expression = WithRange(new CollectionAccessExpressionNode(expression, selector), expression, selector);
                continue;
            }

            return expression;
        }
    }

    private CollectionSelectorNode ParseCollectionSelector()
    {
        var startToken = Current;
        SkipNewLines();
        if (Match(SelectorAny, SelectorAll))
        {
            var op = Previous.Kind == SelectorAny ? "any" : "all";
            var identifier = ExpectIdentifier();
            ExpectWord("where");
            var predicate = ParseExpression();
            return WithRange(new PredicateSelectorNode(op, identifier, predicate), startToken);
        }

        if (Match(SelectorHas))
        {
            SkipNewLines();
            if (Is(LeftBracket))
            {
                return new ObjectMatchSelectorNode(ParseObjectMatchPattern());
            }

            return WithRange(new PatternSelectorNode(ParseDicePattern()), startToken);
        }

        if (Match(SelectorTake))
        {
            return ParseTakeSelector();
        }

        if (Match(SelectorDrop))
        {
            return ParseDropSelector();
        }

        if (MatchTag(":term"))
        {
            return ParseSeriesTermSelector();
        }

        if (Match(SelectorCount))
        {
            SkipNewLines();
            if (Is(RightBracket))
            {
                return WithRange(new CountSelectorNode("value", new BooleanLiteralExpressionNode(true)), startToken);
            }

            var identifier = ExpectIdentifier();
            ExpectWord("where");
            var predicate = ParseExpression();
            return WithRange(new CountSelectorNode(identifier, predicate), startToken);
        }

        if (Match(SelectorChoose))
        {
            return ParseChooseSelector();
        }

        if (Match(SelectorDraw))
        {
            SkipNewLines();
            var countToken = Expect(GesTokenKind.Float);
            return WithRange(new DrawSelectorNode(ParsePositiveInteger(countToken, "draw count")), startToken);
        }

        if (Match(SelectorShuffle))
        {
            return WithRange(new ShuffleSelectorNode(), startToken);
        }

        if (Match(SelectorReverse))
        {
            return WithRange(new ReverseSelectorNode(), startToken);
        }

        if (MatchTag(":first"))
        {
            return ParseEdgeSelector("first");
        }

        if (MatchTag(":last"))
        {
            return ParseEdgeSelector("last");
        }

        if (MatchTag(":single"))
        {
            return ParseEdgeSelector("single");
        }

        if (Match(SelectorFilter))
        {
            var identifier = ExpectIdentifier();
            ExpectWord("where");
            var predicate = ParseExpression();
            return WithRange(new FilterSelectorNode(identifier, predicate), startToken);
        }

        if (Match(SelectorSum))
        {
            SkipNewLines();
            if (Is(RightBracket))
            {
                return WithRange(new SumSelectorNode("value", new IdentifierExpressionNode("value")), startToken);
            }

            var identifier = ExpectIdentifier();
            Expect(ProjectionArrow);
            var projection = ParseExpression();
            return WithRange(new SumSelectorNode(identifier, projection), startToken);
        }

        if (Match(SelectorAverage))
        {
            SkipNewLines();
            if (Is(RightBracket))
            {
                return WithRange(new AverageSelectorNode("value", new IdentifierExpressionNode("value")), startToken);
            }

            var identifier = ExpectIdentifier();
            Expect(ProjectionArrow);
            var projection = ParseExpression();
            return WithRange(new AverageSelectorNode(identifier, projection), startToken);
        }

        if (MatchTag(":min"))
        {
            return ParseProjectionSelector("min");
        }

        if (MatchTag(":max"))
        {
            return ParseProjectionSelector("max");
        }

        if (MatchTag(":highest"))
        {
            return ParseProjectionSelector("highest");
        }

        if (MatchTag(":lowest"))
        {
            return ParseProjectionSelector("lowest");
        }

        if (Match(SelectorSelect))
        {
            var identifier = ExpectIdentifier();
            Expect(ProjectionArrow);
            var projection = ParseExpression();
            return WithRange(new SelectSelectorNode(identifier, projection), startToken);
        }

        if (MatchTag(":map"))
        {
            SkipNewLines();
            var identifier = ExpectIdentifier();
            SkipNewLines();
            ExpectWord("by");
            SkipNewLines();
            var keyProjection = ParseExpression();
            SkipNewLines();
            ExpressionNode? valueProjection = null;
            if (Match(ProjectionArrow))
            {
                SkipNewLines();
                valueProjection = ParseExpression();
            }

            return WithRange(new MapSelectorNode(identifier, keyProjection, valueProjection), startToken);
        }

        if (Match(SelectorContains))
        {
            return ParseContainsSelector();
        }

        if (MatchTag(":distinct"))
        {
            return ParseDistinctSelector();
        }

        if (MatchTag(":group"))
        {
            return ParseGroupBySelector();
        }

        if (Match(SelectorSort))
        {
            return ParseSortSelector();
        }

        if (MatchTag(":order"))
        {
            return ParseOrderBySelector();
        }

        if (Current.Kind == Tag && Current.Text.StartsWith(":", StringComparison.Ordinal))
        {
            var tagToken = Advance();
            var tag = WithRange(new TagLiteralExpressionNode(tagToken.Text[1..]), tagToken);
            return WithRange(new ExpressionSelectorNode(tag), startToken);
        }

        return WithRange(new ExpressionSelectorNode(ParseExpression()), startToken);
    }

    private string ParseSortDirection()
    {
        SkipNewLines();
        if (MatchWord("ascending"))
        {
            return "ascending";
        }

        if (MatchWord("descending"))
        {
            return "descending";
        }

        var token = Current;
        throw new GameEventScriptParseException($"Expected sort direction but found {token.Text}", token.Line, token.Column);
    }

    private CollectionSelectorNode ParseSortSelector()
    {
        var startToken = Previous;
        SkipNewLines();
        return WithRange(new SortSelectorNode(ParseSortDirection()), startToken);
    }

    private CollectionSelectorNode ParseEdgeSelector(string mode)
    {
        var startToken = Previous;
        SkipNewLines();
        if (Current.Kind != Identifier)
        {
            return WithRange(new EdgeSelectorNode(mode, null, null), startToken);
        }

        var identifier = ExpectIdentifier();
        ExpectWord("where");
        var predicate = ParseExpression();
        return WithRange(new EdgeSelectorNode(mode, identifier, predicate), startToken);
    }

    private CollectionSelectorNode ParseOrderBySelector()
    {
        var startToken = Previous;
        SkipNewLines();
        ExpectWord("by");
        SkipNewLines();
        var identifier = ExpectIdentifier();
        Expect(ProjectionArrow);
        var projection = ParseExpression();
        var direction = ParseSortDirection();
        return WithRange(new OrderBySelectorNode(direction, identifier, projection), startToken);
    }

    private CollectionSelectorNode ParseProjectionSelector(string op)
    {
        var startToken = Previous;
        SkipNewLines();
        var identifier = ExpectIdentifier();
        Expect(ProjectionArrow);
        var projection = ParseExpression();

        return op switch
        {
            "min" or "lowest" => WithRange(new MinSelectorNode(identifier, projection), startToken),
            "max" or "highest" => WithRange(new MaxSelectorNode(identifier, projection), startToken),
            _ => throw new InvalidOperationException($"Unknown projection selector '{op}'")
        };
    }

    private CollectionSelectorNode ParseContainsSelector()
    {
        var startToken = Previous;
        SkipNewLines();
        if (MatchWord("all"))
        {
            SkipNewLines();
            return WithRange(new ContainsSelectorNode("all", ParseExpression()), startToken);
        }

        if (MatchWord("any"))
        {
            SkipNewLines();
            return WithRange(new ContainsSelectorNode("any", ParseExpression()), startToken);
        }

        return WithRange(new ContainsSelectorNode("single", ParseExpression()), startToken);
    }

    private CollectionSelectorNode ParseDistinctSelector()
    {
        var startToken = Previous;
        SkipNewLines();
        if (!MatchWord("by"))
        {
            return WithRange(new DistinctSelectorNode(null, null), startToken);
        }

        SkipNewLines();
        var identifier = ExpectIdentifier();
        Expect(ProjectionArrow);
        var projection = ParseExpression();
        return WithRange(new DistinctSelectorNode(identifier, projection), startToken);
    }

    private CollectionSelectorNode ParseGroupBySelector()
    {
        var startToken = Previous;
        SkipNewLines();
        ExpectWord("by");
        SkipNewLines();
        var identifier = ExpectIdentifier();
        Expect(ProjectionArrow);
        var projection = ParseExpression();
        return WithRange(new GroupBySelectorNode(identifier, projection), startToken);
    }

    private ObjectMatchPatternNode ParseObjectMatchPattern()
    {
        var startToken = Current;
        Expect(LeftBracket);
        SkipNewLines();
        var entries = new List<ObjectMatchEntryNode>();
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
        return WithRange(new ObjectMatchPatternNode(entries), startToken);
    }

    private ObjectMatchEntryNode ParseObjectMatchEntry()
    {
        var startToken = Current;
        var key = ExpectIdentifier();
        Expect(Colon);
        SkipNewLines();
        ObjectMatchValueNode value = Is(LeftBracket)
            ? new ObjectMatchNestedValueNode(ParseObjectMatchPattern())
            : new ObjectMatchExpressionValueNode(ParseExpression());
        return WithRange(new ObjectMatchEntryNode(key, value), startToken);
    }

    private ExpressionNode ParsePrimaryExpression()
    {
        ThrowIfIllegalToken();

        if (IsTypeConstructorStart())
        {
            return ParseTypeConstructorExpression();
        }

        if (MatchWord("of"))
        {
            return ParseListLiteralExpressionCore(Previous);
        }

        if (MatchWord("from"))
        {
            return ParseRangeExpressionCore();
        }

        if (Match(KeywordRandom))
        {
            SkipNewLines();
            if (Match(With))
            {
                return ParseSeededRandomExpression();
            }

            return ParseRandomExpression();
        }

        if (Is(Roll) && IsNextSignificantWord("dice"))
        {
            Advance();
            SkipNewLines();
            ExpectWord("dice");
            return ParseDiceExpression();
        }

        if (MatchTag(":list"))
        {
            return ParseCollectionFactoryExpression("list");
        }

        if (Current.Kind == Tag &&
            string.Equals(Current.Text, ":set", StringComparison.Ordinal) &&
            IsNextSignificantToken(LeftBracket))
        {
            var setToken = Current;
            throw new GameEventScriptParseException("Type ':set' has been removed; use lists or key-only maps.", setToken);
        }

        if (Match(GesTokenKind.Float))
        {
            return Previous.GetIntegerValue() is { } integerValue
                ? WithRange(new IntegerLiteralExpressionNode(integerValue), Previous)
                : WithRange(new FloatLiteralExpressionNode(Previous.FloatValue), Previous);
        }

        if (Match(Percentage))
        {
            return WithRange(new PercentageLiteralExpressionNode(Previous.FloatValue), Previous);
        }

        if (Match(GesTokenKind.UnitNumber))
        {
            return Previous.GetIntegerValue() is { } integerValue
                ? WithRange(new UnitIntegerLiteralExpressionNode(integerValue, Previous.UnitName), Previous)
                : WithRange(new UnitFloatLiteralExpressionNode(Previous.FloatValue, Previous.UnitName), Previous);
        }

        if (Match(Text))
        {
            return WithRange(new TextLiteralExpressionNode(Previous.Text), Previous);
        }

        if (Match(LeftBracket))
        {
            return ParseBracketLiteralExpression();
        }

        if (Match(True))
        {
            return WithRange(new BooleanLiteralExpressionNode(true), Previous);
        }

        if (Match(False))
        {
            return WithRange(new BooleanLiteralExpressionNode(false), Previous);
        }

        if (Match(Nothing))
        {
            return WithRange(new NothingLiteralExpressionNode(), Previous);
        }

        if (Current.Kind == Tag && Current.Text.StartsWith("#", StringComparison.Ordinal))
        {
            var tagToken = Advance();
            return WithRange(new TagLiteralExpressionNode(tagToken.Text[1..]), tagToken);
        }

        if (GetMathConstant(Current.Kind) is { } numericConstant)
        {
            var constantToken = Current;
            Advance();
            return WithRange(new FloatLiteralExpressionNode(numericConstant), constantToken);
        }

        if ((Current.Kind == Identifier || Current.Kind == Message) && IsCallExpressionStart())
        {
            return ParseCallOrHandlerBindExpression();
        }

        if (Current.Kind == Identifier)
        {
            var identifierToken = Advance();
            return WithRange(new IdentifierExpressionNode(identifierToken.Text), identifierToken);
        }

        if (Match(LeftParen))
        {
            SkipNewLines();
            var expression = ParseExpression();
            SkipNewLines();
            Expect(RightParen);
            return expression;
        }

        var token = Current;
        throw new GameEventScriptParseException($"Unexpected token '{token.Text}'", token.Line, token.Column);
    }

    private ExpressionNode ParseBracketLiteralExpression()
    {
        var startToken = Previous;
        SkipNewLines();
        if (Match(Colon))
        {
            SkipNewLines();
            Expect(RightBracket);
            return WithRange(new MapLiteralExpressionNode(Array.Empty<MapEntryNode>()), startToken);
        }

        if (Is(RightBracket))
        {
            Expect(RightBracket);
            return WithRange(new ListLiteralExpressionNode(Array.Empty<ExpressionNode>()), startToken);
        }

        return IsMapLiteralEntryStart()
            ? ParseDictionaryLiteralExpression()
            : ParseListLiteralExpression();
    }

    private MessageLiteralExpressionNode ParseMessageLiteralExpressionCore()
    {
        SkipNewLines();
        var startToken = Current;
        var message = Expect(Message).Text;
        var arguments = ArgumentListNode.Empty;
        if (Match(LeftParen))
        {
            arguments = ParseArgumentListAfterLeftParen();
        }

        return WithRange(new MessageLiteralExpressionNode(message, arguments), startToken);
    }

    private ExpressionNode ParseCallOrHandlerBindExpression()
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
                ? WithRange(new HandlerLiteralExpressionNode(name, Array.Empty<ParameterNode>()), startToken)
                : WithRange(new CallExpressionNode(name, ArgumentListNode.Empty), startToken);
        }

        if (IsArgumentLabelStart())
        {
            var labeledArguments = ParseArgumentListAfterFirstArgument(ParseArgument(), RightParen);
            Expect(RightParen);
            return isMessageCallee
                ? WithRange(new MessageLiteralExpressionNode(name, labeledArguments), startToken)
                : WithRange(new CallExpressionNode(name, labeledArguments), startToken);
        }

        if (isMessageCallee)
        {
            var parameters = new List<ParameterNode> { ParseParameter(allowDeclaredType: false) };
            while (Match(Comma))
            {
                SkipNewLines();
                parameters.Add(ParseParameter(allowDeclaredType: false));
            }

            SkipNewLines();
            Expect(RightParen);
            return WithRange(new HandlerLiteralExpressionNode(name, parameters), startToken);
        }

        var arguments = new List<ArgumentNode> { new(null, ParseExpression()) };
        while (Match(Comma))
        {
            SkipNewLines();
            arguments.Add(new ArgumentNode(null, ParseExpression()));
        }

        SkipNewLines();
        Expect(RightParen);
        return WithRange(new CallExpressionNode(name, new ArgumentListNode(arguments)), startToken);
    }

    private ListLiteralExpressionNode ParseListLiteralExpression()
    {
        var startToken = Previous;
        var items = new List<ExpressionNode>();
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
        return WithRange(new ListLiteralExpressionNode(items), startToken);
    }

    private ExpressionNode ParseCollectionFactoryExpression(string collectionType)
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

    private GeneratedCollectionExpressionNode ParseGeneratedCollectionExpression(string collectionType)
    {
        var startToken = Previous;
        SkipNewLines();
        var identifier = ExpectIdentifier();
        SkipNewLines();
        IterationSourceNode source;
        if (MatchWord("from"))
        {
            var fromToken = Previous;
            source = WithRange(new RangeIterationSourceNode(ParseRangeExpressionCore()), fromToken);
        }
        else if (Match(In))
        {
            SkipNewLines();
            if (Current.Kind == Identifier && string.Equals(Current.Text, "from", StringComparison.Ordinal))
            {
                throw new GameEventScriptParseException("Direct ranges are not allowed after 'in'; use ':select item from ... to ...' or iterate a range value", Current.Line, Current.Column);
            }

            var sourceStart = Current;
            source = WithRange(new CollectionIterationSourceNode(ParseExpression()), sourceStart);
        }
        else
        {
            var token = Current;
            throw new GameEventScriptParseException($"Expected 'from' or {In} but found {token.Text}", token.Line, token.Column);
        }

        ExpressionNode? predicate = null;
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
        return WithRange(new GeneratedCollectionExpressionNode(collectionType, identifier, source, predicate, projection), startToken);
    }

    private MapLiteralExpressionNode ParseDictionaryLiteralExpression()
    {
        var startToken = Previous;
        var entries = new List<MapEntryNode>();
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
        return WithRange(new MapLiteralExpressionNode(entries), startToken);
    }

    private MapEntryNode ParseDictionaryEntry()
    {
        var startToken = Current;
        var key = ExpectIdentifier();
        Expect(Colon);
        SkipNewLines();
        var value = Is(Comma) || Is(RightBracket)
            ? WithRange(new BooleanLiteralExpressionNode(true), startToken)
            : ParseExpression();
        return WithRange(new MapEntryNode(key, value), startToken);
    }

    private bool IsMapLiteralEntryStart()
    {
        if (Current.Kind != Identifier)
        {
            return false;
        }

        return _reader.PeekSignificant(1).Kind == Colon;
    }

    private RandomExpressionNode ParseRandomExpression()
    {
        var startToken = Previous;
        MatchWord("from");
        SkipNewLines();
        var fromExpression = ParseRandomBoundExpression();
        SkipNewLines();
        Expect(To);
        SkipNewLines();
        var toExpression = ParseRandomBoundExpression();

        return WithRange(new RandomExpressionNode(fromExpression, toExpression), startToken);
    }

    private RangeExpressionNode ParseRangeExpressionCore()
    {
        var startToken = Previous;
        SkipNewLines();
        var fromExpression = ParseRangeBoundExpression();
        SkipNewLines();
        Expect(To);
        SkipNewLines();
        var toExpression = ParseRangeBoundExpression();

        ExpressionNode? stepExpression = null;
        if (MatchWord("step"))
        {
            SkipNewLines();
            stepExpression = ParseExpression();
        }

        return WithRange(new RangeExpressionNode(fromExpression, toExpression, stepExpression), startToken);
    }

    private SeededRandomExpressionNode ParseSeededRandomExpression()
    {
        var startToken = Previous;
        SkipNewLines();
        var seedExpression = ParseExpression();
        SkipNewLines();
        var bodyExpression = ParseExpression();
        return WithRange(new SeededRandomExpressionNode(seedExpression, bodyExpression), startToken);
    }

    private ExpressionNode ParseRandomBoundExpression()
    {
        SkipNewLines();
        return ParseAdditiveExpression();
    }

    private ExpressionNode ParseRangeBoundExpression()
    {
        SkipNewLines();
        return ParseAdditiveExpression();
    }

    private DiceExpressionNode ParseDiceExpression()
    {
        var startToken = Previous;
        var diceCountToken = Expect(GesTokenKind.Float);
        var sideCountToken = ParseDiceSideCountToken();

        var diceCount = ParsePositiveInteger(diceCountToken, "dice count");
        var sideCount = ParsePositiveInteger(sideCountToken, "side count");

        return WithRange(new DiceExpressionNode(diceCount, sideCount), startToken);
    }

    private CollectionSelectorNode ParseTakeSelector()
    {
        var startToken = Previous;
        SkipNewLines();
        if (ParseSliceScope() is { } scope)
        {
            SkipNewLines();
            var countToken = Expect(GesTokenKind.Float);
            return WithRange(new SequenceSliceSelectorNode("take", scope, ParsePositiveInteger(countToken, "take count")), startToken);
        }

        return WithRange(new TakePatternSelectorNode(ParseDicePattern()), startToken);
    }

    private CollectionSelectorNode ParseDropSelector()
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
        return WithRange(new SequenceSliceSelectorNode("drop", scope, ParsePositiveInteger(countToken, "drop count")), startToken);
    }

    private CollectionSelectorNode ParseSeriesTermSelector()
    {
        var startToken = Previous;
        SkipNewLines();
        return WithRange(new SeriesTermSelectorNode(ParseExpression()), startToken);
    }

    private string? ParseSliceScope()
    {
        if (MatchWord("first"))
        {
            return "first";
        }

        if (MatchWord("last"))
        {
            return "last";
        }

        if (MatchWord("highest"))
        {
            return "highest";
        }

        if (MatchWord("lowest"))
        {
            return "lowest";
        }

        return null;
    }

    private CollectionSelectorNode ParseChooseSelector()
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
        ExpressionNode? predicate = null;
        string? weightIdentifier = null;
        ExpressionNode? weightExpression = null;
        if (Current.Kind == Identifier)
        {
            var lookahead = _reader.PeekSignificant(1);
            if (lookahead.Kind == Identifier && string.Equals(lookahead.Text, "where", StringComparison.Ordinal))
            {
                identifier = ExpectIdentifier();
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
            weightIdentifier = ExpectIdentifier();
            SkipNewLines();
            Expect(ProjectionArrow);
            SkipNewLines();
            weightExpression = ParseExpression();
        }

        return WithRange(new ChooseSelectorNode(count, atRandom, identifier, predicate, weightIdentifier, weightExpression), startToken);
    }

    private ExtensionCallExpressionNode ParseExtensionCallExpression()
    {
        var startToken = Current;
        var (extensionName, functionName, _) = ParseExtensionSymbol();
        if (extensionName is "integer" or "degree")
        {
            throw new GameEventScriptParseException($"Standard extension namespace ':{extensionName}' has been removed; use direct math intrinsics instead.", startToken);
        }

        SkipNewLines();

        ArgumentListNode arguments;
        if (Match(LeftParen))
        {
            arguments = ParseArgumentListAfterLeftParen();
        }
        else if (Current.Kind == Identifier && string.Equals(Current.Text, "of", StringComparison.Ordinal))
        {
            arguments = ParseOfArgumentList();
        }
        else if (IsArgumentLabelStart())
        {
            arguments = ParseUngroupedLabeledArgumentList();
        }
        else if (IsExtensionUnaryArgumentStart())
        {
            arguments = new ArgumentListNode([new ArgumentNode(null, ParseUnaryExpression())]);
        }
        else
        {
            arguments = ArgumentListNode.Empty;
        }

        return WithRange(new ExtensionCallExpressionNode(extensionName, functionName, arguments), startToken);
    }

    private TypeConstructorExpressionNode ParseTypeConstructorExpression()
    {
        var startToken = Current;
        var typeName = ParseTypeName();
        SkipNewLines();
        Expect(LeftParen);
        var arguments = ParseArgumentListAfterLeftParen();
        return WithRange(new TypeConstructorExpressionNode(typeName, arguments), startToken);
    }

    private ListLiteralExpressionNode ParseListLiteralExpressionCore(GesToken startToken)
    {
        SkipNewLines();
        var items = new List<ExpressionNode> { ParseEqualityExpression() };
        while (Match(OperatorAnd))
        {
            SkipNewLines();
            items.Add(ParseEqualityExpression());
        }

        return WithRange(new ListLiteralExpressionNode(items), startToken);
    }

    private ExpressionNode ParseClampExpression()
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
        return WithRange(new ClampExpressionNode(value, minimum, maximum), startToken);
    }

    private ExpressionNode ParseVariadicTaggedExpression(string op)
    {
        var startToken = Previous;
        SkipNewLines();
        ExpectWord("of");
        SkipNewLines();
        var arguments = new List<ExpressionNode> { ParseEqualityExpression() };
        while (Match(OperatorAnd))
        {
            SkipNewLines();
            arguments.Add(ParseEqualityExpression());
        }

        return WithRange(new VariadicTaggedExpressionNode(op, arguments), startToken);
    }

    private GesToken ParseDiceSideCountToken()
    {
        if (Match(DiceSeparator))
        {
            return Expect(GesTokenKind.Float);
        }

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
        if (text.Length <= 1 || text[0] != 'd')
        {
            return false;
        }

        for (var i = 1; i < text.Length; i++)
        {
            if (!char.IsDigit(text[i]))
            {
                return false;
            }
        }

        return true;
    }

    private int ParsePositiveInteger(GesToken numberToken, string name)
    {
        if (!double.TryParse(numberToken.NormalizedNumericText, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ||
            value <= 0 ||
            value != Math.Truncate(value))
        {
            throw new GameEventScriptParseException($"Expected positive integer for {name}", numberToken.Line, numberToken.Column);
        }

        if (value > int.MaxValue)
        {
            throw new GameEventScriptParseException($"{name} is too large", numberToken.Line, numberToken.Column);
        }

        return (int)value;
    }

    private GesToken Expect(GesTokenKind kind)
    {
        ThrowIfIllegalToken();

        if (Is(kind))
        {
            return Advance();
        }

        var token = Current;
        throw new GameEventScriptParseException($"Expected {kind} but found {token.Kind}", token);
    }

    private string ExpectIdentifier()
    {
        ThrowIfIllegalToken();

        if (Is(Identifier))
        {
            return Advance().Text;
        }

        var token = Current;
        throw new GameEventScriptParseException($"Expected Identifier but found {token.Kind}", token);
    }

    private bool Match(GesTokenKind kind)
    {
        if (!Is(kind)) return false;
        Advance();
        return true;
    }

    private bool Match(GesTokenKind first, GesTokenKind second)
    {
        if (!Is(first) && !Is(second)) return false;
        Advance();
        return true;
    }

    private bool Match(GesTokenKind first, GesTokenKind second, GesTokenKind third)
    {
        if (!Is(first) && !Is(second) && !Is(third)) return false;
        Advance();
        return true;
    }

    private bool Match(GesTokenKind first, GesTokenKind second, GesTokenKind third, GesTokenKind fourth)
    {
        if (!Is(first) && !Is(second) && !Is(third) && !Is(fourth)) return false;
        Advance();
        return true;
    }

    private bool Match(GesTokenKind first, GesTokenKind second, GesTokenKind third, GesTokenKind fourth, GesTokenKind fifth)
    {
        if (!Is(first) && !Is(second) && !Is(third) && !Is(fourth) && !Is(fifth)) return false;
        Advance();
        return true;
    }

    private bool MatchTag(string tagText)
    {
        if (Current.Kind == Tag && Current.Text.StartsWith(":", StringComparison.Ordinal) && string.Equals(Current.Text, tagText, StringComparison.Ordinal))
        {
            Advance();
            return true;
        }

        return false;
    }

    private bool Is(GesTokenKind kind) => Current.Kind == kind;

    private bool IsNextSignificantToken(GesTokenKind kind)
        => _reader.PeekSignificant(1).Kind == kind;

    private bool IsSeededRandomStatementStart()
    {
        if (Current.Kind != KeywordRandom)
        {
            return false;
        }

        return _reader.PeekSignificant(1).Kind == With;
    }

    private bool IsElseClauseStart()
    {
        return _reader.PeekSignificant(0).Kind == Else;
    }

    private bool IsCallExpressionStart()
    {
        if (Current.Kind is not (Identifier or Message))
        {
            return false;
        }

        return _reader.PeekSignificant(1).Kind == LeftParen;
    }

    private bool IsExtensionCallStart()
    {
        if (Current.Kind != Tag || !Current.Text.StartsWith(":", StringComparison.Ordinal))
        {
            return false;
        }

        if (_reader.PeekSignificant(1).Kind != Dot)
        {
            return false;
        }

        if (!IsExtensionFunctionNameKind(_reader.PeekSignificant(2).Kind))
        {
            return false;
        }

        return true;
    }

    private bool IsTypeConstructorStart()
    {
        if (Current.Kind != Tag)
        {
            return false;
        }

        return _reader.PeekSignificant(1).Kind == LeftParen;
    }

    private (string ExtensionName, string FunctionName, GesToken EndToken) ParseExtensionSymbol()
    {
        var extensionToken = Expect(Tag);
        SkipNewLines();
        Expect(Dot);
        SkipNewLines();
        var functionToken = ExpectExtensionFunctionName();
        return (extensionToken.Text[1..], functionToken.Text, functionToken);
    }

    private bool IsExtensionUnaryArgumentStart()
        => Current.Kind is Identifier or Message or Tag or GesTokenKind.Float or Percentage or UnitNumber or Text or True or False or Nothing or
            MathConstantPi or MathConstantE or MathConstantTau or MathConstantInfinity or
            IntrinsicAbs or IntrinsicLn or IntrinsicExp or IntrinsicSqrt or IntrinsicCbrt or IntrinsicChance or
            IntrinsicFloor or IntrinsicCeil or IntrinsicTruncate or IntrinsicRad or IntrinsicDeg or IntrinsicWrap or IntrinsicRound or
            IntrinsicSin or IntrinsicCos or IntrinsicTan or IntrinsicAsin or IntrinsicAcos or IntrinsicAtan or IntrinsicAtan2 or
            IntrinsicHypot or IntrinsicDistance or IntrinsicSquared or IntrinsicLength or IntrinsicNormalize or IntrinsicDot or IntrinsicCross or IntrinsicAngle or
            KeywordRandom or Series or Clamp or Min or Max or Roll or
            LeftBracket or LeftParen or OperatorMinus or Has or Empty or OperatorNot;

    private bool IsArgumentLabelStart()
    {
        if (!IsArgumentLabelKind(Current.Kind))
        {
            return false;
        }

        return _reader.PeekSignificant(1).Kind == Colon;
    }

    private string ExpectArgumentLabel()
    {
        ThrowIfIllegalToken();

        if (IsArgumentLabelKind(Current.Kind))
        {
            return Advance().Text;
        }

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

        if (IsExtensionFunctionNameKind(Current.Kind))
        {
            return Advance();
        }

        var token = Current;
        throw new GameEventScriptParseException($"Expected extension function name but found {token.Kind}", token);
    }

    private string ParseTypeName()
    {
        ThrowIfIllegalToken();

        if (Match(Nothing))
        {
            return "nothing";
        }

        if (!Is(Tag))
        {
            var token = Current;
            throw new GameEventScriptParseException($"Expected type name but found {token.Kind}", token);
        }

        var typeToken = Advance();
        var typeName = typeToken.Text[1..];
        if (string.Equals(typeName, "nothing", StringComparison.Ordinal))
        {
            throw new GameEventScriptParseException("The absence type is a keyword; use nothing without ':'.", typeToken);
        }

        if (string.Equals(typeName, "numeric", StringComparison.Ordinal))
        {
            throw new GameEventScriptParseException("The numeric type helper is a keyword; use numeric without ':'.", typeToken);
        }

        if (string.Equals(typeName, "quantity", StringComparison.Ordinal) &&
            Is(LeftParen) &&
            IsQuantityTypeSpecifierAhead())
        {
            SkipNewLines();
            Expect(LeftParen);
            SkipNewLines();
            var unitToken = Expect(Identifier);
            SkipNewLines();
            Expect(RightParen);
            typeName = $"quantity:{unitToken.Text}";
        }

        return typeName;
    }

    private bool IsQuantityTypeSpecifierAhead()
    {
        if (_reader.PeekSignificant(0).Kind != LeftParen)
        {
            return false;
        }

        if (_reader.PeekSignificant(1).Kind != Identifier)
        {
            return false;
        }

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

    private bool IsWord(string word)
        => Current.Kind == Identifier && string.Equals(Current.Text, word, StringComparison.Ordinal);

    private bool IsNextSignificantWord(string word)
    {
        var lookahead = _reader.PeekSignificant(1);
        return lookahead.Kind == Identifier && string.Equals(lookahead.Text, word, StringComparison.Ordinal);
    }

    private void ExpectWord(string word)
    {
        ThrowIfIllegalToken();

        if (MatchWord(word))
        {
            return;
        }

        throw new GameEventScriptParseException(
            $"Expected {word} but found {Current.Text}",
            Current);
    }

    private void ThrowIfIllegalToken()
    {
        if (Current.Kind == Illegal)
        {
            throw new GameEventScriptParseException($"Illegal token '{Current.Text}'", Current);
        }
    }

    private void SkipNewLines()
    {
        while (Is(NewLine))
        {
            Advance();
        }
    }

    private void SkipStatementSeparators()
    {
        while (Match(Semicolon, NewLine))
        {
        }
    }

    private void RequireStatementSeparatorOrClosing(GesTokenKind closingKind)
    {
        if (Is(closingKind))
        {
            return;
        }

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
        if (Is(EndOfFile))
        {
            return;
        }

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
        => _reader.Advance();

    private GesToken Current => _reader.Current;

    private GesToken Previous => _reader.Previous;

    private void AddParseError(GameEventScriptParseException exception)
    {
        _errors.Add(new GameEventScriptCompileError(
            exception.Message,
            ModuleName,
            string.Empty,
            GameEventScriptSymbolKind.Unknown,
            GameEventScriptCompileErrorKind.Syntax,
            new GameEventScriptSourceLocation(SourceName, exception.Line, exception.Column, exception.EndLine, exception.EndColumn, ModuleName, _sourceId)));
    }

    private void SynchronizeTopLevel()
    {
        while (!Is(EndOfFile))
        {
            if (Is(Module) || Is(Record) || Is(Predicate) || Is(Function) || Is(On))
            {
                return;
            }

            Advance();
        }
    }

    private bool SynchronizeStatement(GesTokenKind closingKind)
    {
        while (!Is(EndOfFile))
        {
            if (Is(closingKind))
            {
                return false;
            }

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
