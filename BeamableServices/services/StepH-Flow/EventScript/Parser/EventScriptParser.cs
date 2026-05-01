using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using static StepH.Flow.EventScript.Parser.EventScriptTokenKind;

namespace StepH.Flow.EventScript.Parser;


/// <summary>
/// A utility class responsible for parsing Event Script source code into a structured syntax tree.
/// This class serves as the entry point for transforming raw script strings
/// into an abstract representation of the script for further processing or compilation.
/// </summary>
public sealed class EventScriptParser
{
    /// <summary>
    /// Parses the provided Event Script string into an EventScriptModule, which represents the root node of the syntax tree.
    /// </summary>
    /// <param name="script">The Event Script source code to be parsed.</param>
    /// <param name="sourceName">An optional source name used for diagnostics.</param>
    /// <returns>An <see cref="EventScriptModule"/> representing the parsed syntax tree structure.</returns>
    public static EventScriptModule Parse(string script, string? sourceName = null)
    {
        _ = script ?? throw new ArgumentNullException(nameof(script));

        var normalizedScript = script.Replace("\r\n", "\n").Replace('\r', '\n');
        var hash = ComputeShortHash(normalizedScript);
        var rawTokens = new EventScriptLexer(normalizedScript).Tokenize().ToList();
        var moduleName = TryResolveModuleName(rawTokens) ?? $"AnonymousModule_{hash}";
        var resolvedSourceName = string.IsNullOrWhiteSpace(sourceName) ? $"UnknownSource_{hash}" : sourceName!;
        var tokens = new List<EventScriptToken>();
        var initialErrors = new List<EventScriptSyntaxError>();
        foreach (var token in rawTokens)
        {
            if (token.Kind == Illegal)
            {
                initialErrors.Add(new EventScriptSyntaxError(
                    $"Illegal token '{token.Text}'",
                    moduleName,
                    EventScriptSyntaxErrorKind.Lexer,
                    new EventScriptSourceLocation(resolvedSourceName, token.Line, token.Column, token.EndLine, token.EndColumn, moduleName)));
                continue;
            }

            tokens.Add(token);
        }

        return new EventScriptParser(tokens, moduleName, resolvedSourceName, initialErrors)
            .ParseEventScript();
    }

    private static readonly HashSet<string> CollectionCombineOperators = new(StringComparer.Ordinal)
    {
        "intersect",
        "combine",
        "merge",
        "except",
        "zip"
    };

    private sealed class EventScriptParseException(string message, int line, int column) : Exception($"{message} (line {line}, col {column})")
    {
        public int Line { get; } = line;
        public int Column { get; } = column;
        public int EndLine { get; } = line;
        public int EndColumn { get; } = column;

        public EventScriptParseException(string message, EventScriptToken token)
            : this(message, token.Line, token.Column, token.EndLine, token.EndColumn)
        {
        }

        public EventScriptParseException(string message, int line, int column, int endLine, int endColumn)
            : this(message, line, column)
        {
            EndLine = endLine;
            EndColumn = endColumn;
        }
    }

    private readonly IReadOnlyList<EventScriptToken> _tokens;
    private readonly string _moduleName;
    private readonly string _sourceName;
    private readonly List<EventScriptSyntaxError> _errors;
    private int _index;

    private EventScriptParser(IReadOnlyList<EventScriptToken> tokens, string moduleName, string sourceName, IReadOnlyList<EventScriptSyntaxError> initialErrors)
    {
        _tokens = tokens;
        _moduleName = moduleName;
        _sourceName = sourceName;
        _errors = initialErrors?.ToList() ?? [];
    }

    private EventScriptSourceLocation CreateRange(EventScriptToken token)
        => new(_sourceName, token.Line, token.Column, token.EndLine, token.EndColumn, _moduleName);

    private EventScriptSourceLocation CreateRange(EventScriptToken start, EventScriptToken end)
        => new(_sourceName, start.Line, start.Column, end.EndLine, end.EndColumn, _moduleName);

    private EventScriptSourceLocation? MergeRanges(EventScriptNode? first, EventScriptNode? last = null)
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
            : new EventScriptSourceLocation(_sourceName, start.Line, start.Column, endLine, endColumn, _moduleName);
    }

    private T WithRange<T>(T node, EventScriptToken startToken) where T : EventScriptNode
        => node with { SourceRange = CreateRange(startToken, Previous) };

    private T WithRange<T>(T node, EventScriptToken startToken, EventScriptToken endToken) where T : EventScriptNode
        => node with { SourceRange = CreateRange(startToken, endToken) };

    private T WithRange<T>(T node, EventScriptNode? first, EventScriptNode? last = null) where T : EventScriptNode
        => node with { SourceRange = MergeRanges(first, last) };

    private EventScriptModule ParseEventScript()
    {
        var typeDefinitions = new List<TypeDefinitionNode>();
        var ruleDefinitions = new List<RuleDefinitionNode>();
        var selectDefinitions = new List<SelectDefinitionNode>();
        var handlers = new List<EventHandlerNode>();
        SkipStatementSeparators();
        ParseOptionalModuleDeclaration();
        SkipStatementSeparators();
        while (!Is(EndOfFile))
        {
            try
            {
                if (Match(Record))
                {
                    typeDefinitions.Add(ParseTypeDefinition());
                }
                else if (Match(Rule))
                {
                    ruleDefinitions.Add(ParseRuleDefinition());
                }
                else if (Match(Select))
                {
                    selectDefinitions.Add(ParseSelectDefinition());
                }
                else
                {
                    handlers.Add(ParseEventHandler());
                }

                RequireHandlerSeparatorOrEndOfFile();
                SkipStatementSeparators();
            }
            catch (EventScriptParseException ex)
            {
                AddParseError(ex);
                SynchronizeTopLevel();
            }
        }

        if (_errors.Count > 0)
        {
            throw new EventScriptSyntaxException(_errors);
        }

        var module = new EventScriptModule(_moduleName, _sourceName, typeDefinitions, ruleDefinitions, selectDefinitions, handlers);
        var firstToken = _tokens.FirstOrDefault();
        if (firstToken.Kind == EndOfFile)
        {
            return module with { SourceRange = CreateRange(firstToken) };
        }

        var lastToken = _tokens.LastOrDefault(token => token.Kind != EndOfFile);
        return module with { SourceRange = CreateRange(firstToken, lastToken) };
    }

    private static string ComputeShortHash(string text)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(text));
        return BitConverter.ToString(bytes, 0, 4).Replace("-", string.Empty, StringComparison.Ordinal);
    }

    private static string? TryResolveModuleName(IReadOnlyList<EventScriptToken> tokens)
    {
        var index = 0;
        while (index < tokens.Count && tokens[index].Kind == NewLine)
        {
            index++;
        }

        if (index >= tokens.Count || tokens[index].Kind != Module)
        {
            return null;
        }

        index++;
        while (index < tokens.Count && tokens[index].Kind == NewLine)
        {
            index++;
        }

        if (index >= tokens.Count)
        {
            return null;
        }

        return tokens[index].Kind switch
        {
            Identifier or Message => tokens[index].Text,
            _ => null
        };
    }

    private void ParseOptionalModuleDeclaration()
    {
        if (!Match(Module))
        {
            return;
        }

        SkipNewLines();
        if (Current.Kind is not (Identifier or Message))
        {
            throw new EventScriptParseException($"Expected module name but found {Current.Kind}", Current.Line, Current.Column);
        }

        Advance();
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
            Expect(And);
            maximumExpression = ParseEqualityExpression();
            SkipNewLines();
        }

        if (MatchWord("computed"))
        {
            ExpectWord("by");
            computedExpression = ParseExpression();
        }

        return WithRange(new TypeFieldDefinitionNode(name, typeName, minimumExpression, maximumExpression, computedExpression), startToken);
    }

    private RuleDefinitionNode ParseRuleDefinition()
    {
        var startToken = Previous;
        var name = ExpectIdentifier();
        var parameters = ParseDefinitionParameters();
        Expect(Means);
        SkipNewLines();
        var expression = ParseExpression();
        return WithRange(new RuleDefinitionNode(name, parameters, expression), startToken);
    }

    private SelectDefinitionNode ParseSelectDefinition()
    {
        var startToken = Previous;
        var name = ExpectIdentifier();
        var parameters = ParseDefinitionParameters();
        Expect(Means);
        SkipNewLines();
        var expression = ParseExpression();
        return WithRange(new SelectDefinitionNode(name, parameters, expression), startToken);
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

    private ParameterNode ParseParameter()
    {
        var startToken = Current;
        if (Match(Underscore))
        {
            SkipNewLines();
            return WithRange(new ParameterNode(null, ExpectIdentifier()), startToken);
        }

        var name = ExpectIdentifier();
        return WithRange(new ParameterNode(name, name), startToken);
    }

    private EventHandlerNode ParseEventHandler()
    {
        var startToken = Current;
        Expect(On);
        SkipNewLines();
        var message = Expect(Message).Text;

        var parameters = new List<ParameterNode>();
        if (Match(LeftParen))
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

        SkipNewLines();
        Expect(LeftBrace);
        var statements = ParseStatementsUntil(RightBrace);
        Expect(RightBrace);

        return WithRange(new EventHandlerNode(message, parameters, statements), startToken);
    }

    private IReadOnlyList<StatementNode> ParseStatementsUntil(EventScriptTokenKind closingKind)
    {
        var statements = new List<StatementNode>();
        SkipStatementSeparators();

        while (!Is(closingKind))
        {
            if (Is(EndOfFile))
            {
                var token = Current;
                AddParseError(new EventScriptParseException($"Expected '{closingKind}' before end of input", token.Line, token.Column));
                return statements;
            }

            try
            {
                statements.Add(ParseStatement());
                RequireStatementSeparatorOrClosing(closingKind);
            }
            catch (EventScriptParseException ex)
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

        if (Match(Publish))
        {
            return ParsePublishStatement();
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

    private PublishStatementNode ParsePublishStatement()
    {
        var startToken = Previous;
        SkipNewLines();
        var messageExpression = ParsePublishMessageExpression();
        return WithRange(new PublishStatementNode(messageExpression), startToken);
    }

    private ExpressionNode ParsePublishMessageExpression()
    {
        if (Current.Kind == Message)
        {
            return ParseMessageLiteralExpressionCore();
        }

        return ParseExpression();
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

    private ArgumentListNode ParseArgumentListAfterFirstArgument(ArgumentNode firstArgument, EventScriptTokenKind closingKind)
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
        while (Match(And))
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
        throw new EventScriptParseException($"Expected {Be} but found {token.Kind}", token.Line, token.Column);
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
                throw new EventScriptParseException("Direct ranges are not allowed after 'in'; use 'for item from ... to ...' or iterate a range variable", Current.Line, Current.Column);
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
            throw new EventScriptParseException($"Expected {In} or 'from' but found {token.Text}", token.Line, token.Column);
        }

        var body = ParseStatementBody();
        return WithRange(new ForStatementNode(identifier, source, body), startToken);
    }

    private SeededRandomStatementNode ParseSeededRandomStatement()
    {
        var startToken = Current;
        Expect(Tag);
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

        return WithRange(new StatementBodyNode(false, new[] { ParseStatement() }), startToken);
    }

    private ExpressionNode ParseExpression() => ParseGuardedChoiceExpression();

    private ExpressionNode ParseGuardedChoiceExpression()
    {
        var expression = ParseDefaultExpression();
        if (Match(When))
        {
            var branches = new List<GuardedChoiceBranchNode>();
            var conditionExpression = ParseDefaultExpression();
            branches.Add(new GuardedChoiceBranchNode(expression, conditionExpression));

            while (Match(Comma))
            {
                SkipNewLines();
                if (Is(Otherwise))
                {
                    break;
                }

                Match(Or);
                SkipNewLines();

                var branchValue = ParseDefaultExpression();
                SkipNewLines();
                Expect(When);
                SkipNewLines();
                var branchCondition = ParseDefaultExpression();
                branches.Add(new GuardedChoiceBranchNode(branchValue, branchCondition));
            }

            SkipNewLines();
            Expect(Otherwise);
            SkipNewLines();
            var otherwiseExpression = ParseDefaultExpression();
            expression = WithRange(new GuardedChoiceExpressionNode(branches, otherwiseExpression), expression, otherwiseExpression);
        }

        return expression;
    }

    private ExpressionNode ParseDefaultExpression()
    {
        var expression = ParseCollectionCombineExpression();

        while (Current.Kind == Tag && string.Equals(Current.Text, ":default", StringComparison.Ordinal))
        {
            Advance();
            var op = "default";
            SkipNewLines();
            var right = ParseCollectionCombineExpression();
            expression = WithRange(new BinaryExpressionNode(expression, op, right), expression, right);
        }

        return expression;
    }

    private ExpressionNode ParseCollectionCombineExpression()
    {
        var expression = ParseOrExpression();

        while (Current.Kind == Tag &&
               CollectionCombineOperators.Contains(Current.Text[1..]))
        {
            var op = Current.Text[1..];
            Advance();
            SkipNewLines();
            var right = ParseOrExpression();
            expression = WithRange(new BinaryExpressionNode(expression, op, right), expression, right);
        }

        return expression;
    }

    private ExpressionNode ParseOrExpression()
    {
        var expression = ParseXorExpression();

        while (Match(Or))
        {
            var op = "|";
            SkipNewLines();
            var right = ParseXorExpression();
            expression = WithRange(new BinaryExpressionNode(expression, op, right), expression, right);
        }

        return expression;
    }

    private ExpressionNode ParseXorExpression()
    {
        var expression = ParseAndExpression();

        while (Match(Xor))
        {
            var op = "^";
            SkipNewLines();
            var right = ParseAndExpression();
            expression = WithRange(new BinaryExpressionNode(expression, op, right), expression, right);
        }

        return expression;
    }

    private ExpressionNode ParseAndExpression()
    {
        var expression = ParseEqualityExpression();

        while (Match(And))
        {
            var op = "&";
            SkipNewLines();
            var right = ParseEqualityExpression();
            expression = WithRange(new BinaryExpressionNode(expression, op, right), expression, right);
        }

        return expression;
    }

    private ExpressionNode ParseEqualityExpression()
    {
        var expression = ParseMembershipExpression();

        while (Match(Equal, NotEqual))
        {
            var op = Previous.Text;
            SkipNewLines();
            var right = ParseMembershipExpression();
            expression = new BinaryExpressionNode(expression, op, right);
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
                expression = WithRange(new UnaryExpressionNode("has value", expression), expression);
                continue;
            }

            if (Match(In))
            {
                var op = Previous.Text;
                SkipNewLines();
                var right = ParseTypeOperationExpression();
                expression = WithRange(new BinaryExpressionNode(expression, op, right), expression, right);
                continue;
            }

            if (IsValueInOperator())
            {
                Advance();
                SkipNewLines();
                Expect(In);
                var op = "value in";
                SkipNewLines();
                var right = ParseTypeOperationExpression();
                expression = WithRange(new BinaryExpressionNode(expression, op, right), expression, right);
                continue;
            }

            if (Match(Starts))
            {
                SkipNewLines();
                Expect(With);
                var op = "starts with";
                SkipNewLines();
                var right = ParseTypeOperationExpression();
                expression = WithRange(new BinaryExpressionNode(expression, op, right), expression, right);
                continue;
            }

            if (Match(Ends))
            {
                SkipNewLines();
                Expect(With);
                var op = "ends with";
                SkipNewLines();
                var right = ParseTypeOperationExpression();
                expression = WithRange(new BinaryExpressionNode(expression, op, right), expression, right);
                continue;
            }

            break;
        }

        return expression;
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
        throw new EventScriptParseException($"Expected dice pattern but found {token.Text}", token.Line, token.Column);
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
            if (Match(EventScriptTokenKind.Is))
            {
                SkipNewLines();
                if (IsExtensionCallStart())
                {
                    var (extensionName, functionName, _) = ParseExtensionSymbol();
                    expression = WithRange(new ExtensionPredicateExpressionNode(expression, extensionName, functionName), expression);
                    continue;
                }

                if (Is(Tag))
                {
                    var typeName = ParseTypeName();
                    expression = WithRange(new TypeCheckExpressionNode(expression, typeName), expression);
                    continue;
                }

                if (Match(Empty))
                {
                    expression = WithRange(new UnaryExpressionNode("empty", expression), expression);
                    continue;
                }

                if (MatchWord("at"))
                {
                    SkipNewLines();
                    if (MatchWord("least"))
                    {
                        SkipNewLines();
                        var right = ParseRelationalComparisonOperand();
                        expression = WithRange(new BinaryExpressionNode(expression, ">=", right), expression, right);
                        continue;
                    }

                    if (MatchWord("most"))
                    {
                        SkipNewLines();
                        var right = ParseRelationalComparisonOperand();
                        expression = WithRange(new BinaryExpressionNode(expression, "<=", right), expression, right);
                        continue;
                    }

                    var token = Current;
                    throw new EventScriptParseException($"Expected least or most but found {token.Text}", token.Line, token.Column);
                }

                if (Current.Kind == Identifier)
                {
                    var ruleName = Advance().Text;
                    expression = WithRange(new RulePredicateExpressionNode(expression, ruleName), expression);
                    continue;
                }

                var threshold = ParseRelationalComparisonOperand();
                SkipNewLines();
                if (Match(Or))
                {
                    SkipNewLines();
                    if (MatchWord("less"))
                    {
                        expression = WithRange(new BinaryExpressionNode(expression, "<=", threshold), expression, threshold);
                        continue;
                    }

                    if (MatchWord("more") || MatchWord("greater"))
                    {
                        expression = WithRange(new BinaryExpressionNode(expression, ">=", threshold), expression, threshold);
                        continue;
                    }

                    var token = Current;
                    throw new EventScriptParseException($"Expected less, more or greater but found {token.Text}", token.Line, token.Column);
                }

                expression = WithRange(new BinaryExpressionNode(expression, "=", threshold), expression, threshold);
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

        while (Match(Less, Greater, LessOrEqual, GreaterOrEqual))
        {
            var op = Previous.Text;
            SkipNewLines();
            var right = ParseAdditiveExpression();
            expression = WithRange(new BinaryExpressionNode(expression, op, right), expression, right);
        }

        return expression;
    }

    private ExpressionNode ParseAdditiveExpression()
    {
        var expression = ParseMultiplicativeExpression();

        while (Match(Plus, Minus))
        {
            var op = Previous.Text;
            SkipNewLines();
            var right = ParseMultiplicativeExpression();
            expression = WithRange(new BinaryExpressionNode(expression, op, right), expression, right);
        }

        return expression;
    }

    private ExpressionNode ParseMultiplicativeExpression()
    {
        var expression = ParseUnaryExpression();

        while (Match(Multiply, Divide, IntegerDivide, Modulo, Remainder))
        {
            var op = Previous.Text;
            SkipNewLines();
            var right = ParseUnaryExpression();
            expression = WithRange(new BinaryExpressionNode(expression, op, right), expression, right);
        }

        return expression;
    }

    private ExpressionNode ParseUnaryExpression()
    {
        SkipNewLines();
        if (Match(Minus))
        {
            var opToken = Previous;
            SkipNewLines();
            var negativeOperand = ParseUnaryExpression();
            return WithRange(new UnaryExpressionNode("-", negativeOperand), opToken, Previous);
        }

        if (Match(Has))
        {
            var opToken = Previous;
            SkipNewLines();
            ExpectValueWord();
            SkipNewLines();
            var hasValueOperand = ParseUnaryExpression();
            return WithRange(new UnaryExpressionNode("has value", hasValueOperand), opToken, Previous);
        }

        if (Match(Empty))
        {
            var opToken = Previous;
            SkipNewLines();
            var emptyOperand = ParseUnaryExpression();
            return WithRange(new UnaryExpressionNode("empty", emptyOperand), opToken, Previous);
        }

        if (!Match(Not))
        {
            if (!TryParseTaggedUnaryOperator(out var taggedOperator))
            {
                if (IsExtensionCallStart())
                {
                    return ParseExtensionCallExpression();
                }

                if (MatchTag(":clamp"))
                {
                    return ParseClampExpression();
                }

                if (MatchTag(":min"))
                {
                    return ParseVariadicTaggedExpression("min");
                }

                if (MatchTag(":max"))
                {
                    return ParseVariadicTaggedExpression("max");
                }

                return ParsePostfixExpression();
            }

            SkipNewLines();
            var taggedOperand = ParseUnaryExpression();
            return WithRange(new UnaryExpressionNode(taggedOperator, taggedOperand), taggedOperand);
        }

        var startToken = Previous;
        var op = Previous.Text;
        if (string.Equals(op, "not", StringComparison.Ordinal) ||
            string.Equals(op, "~", StringComparison.Ordinal))
        {
            op = "!";
        }

        SkipNewLines();
        var operand = ParseUnaryExpression();
        return WithRange(new UnaryExpressionNode(op, operand), startToken, Previous);
    }

    private bool TryParseTaggedUnaryOperator(out string op)
    {
        op = string.Empty;
        if (Current.Kind != Tag)
        {
            return false;
        }

        op = Current.Text switch
        {
            ":len" => "len",
            ":chance" => "chance",
            ":keys" => "keys",
            ":values" => "values",
            ":entries" => "entries",
            ":abs" => "abs",
            ":floor" => "floor",
            ":ceil" => "ceil",
            ":round" => "round",
            ":rounddown" => "rounddown",
            ":roundup" => "roundup",
            ":roundeven" => "roundeven",
            ":wrapDegree" => "wrapDegree",
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(op))
        {
            return false;
        }

        Advance();
        return true;
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

        if (Match(SelectorCount))
        {
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
            var countToken = Expect(EventScriptTokenKind.Decimal);
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
            var identifier = ExpectIdentifier();
            Expect(Arrow);
            var projection = ParseExpression();
            return WithRange(new SumSelectorNode(identifier, projection), startToken);
        }

        if (Match(SelectorAverage))
        {
            var identifier = ExpectIdentifier();
            Expect(Arrow);
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
            Expect(Arrow);
            var projection = ParseExpression();
            return WithRange(new SelectSelectorNode(identifier, projection), startToken);
        }

        if (MatchTag(":dictionary"))
        {
            SkipNewLines();
            var identifier = ExpectIdentifier();
            SkipNewLines();
            ExpectWord("by");
            SkipNewLines();
            var keyProjection = ParseExpression();
            SkipNewLines();
            ExpressionNode? valueProjection = null;
            if (Match(Arrow))
            {
                SkipNewLines();
                valueProjection = ParseExpression();
            }

            return WithRange(new DictionarySelectorNode(identifier, keyProjection, valueProjection), startToken);
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
        throw new EventScriptParseException($"Expected sort direction but found {token.Text}", token.Line, token.Column);
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
        Expect(Arrow);
        var projection = ParseExpression();
        var direction = ParseSortDirection();
        return WithRange(new OrderBySelectorNode(direction, identifier, projection), startToken);
    }

    private CollectionSelectorNode ParseProjectionSelector(string op)
    {
        var startToken = Previous;
        SkipNewLines();
        var identifier = ExpectIdentifier();
        Expect(Arrow);
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
        Expect(Arrow);
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
        Expect(Arrow);
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
        if (IsTypeConstructorStart())
        {
            return ParseTypeConstructorExpression();
        }

        if (MatchWord("of"))
        {
            return ParseSequenceLiteralExpressionCore(Previous);
        }

        if (MatchWord("from"))
        {
            return ParseRangeExpressionCore();
        }

        if (MatchTag(":random"))
        {
            SkipNewLines();
            if (Match(With))
            {
                return ParseSeededRandomExpression();
            }

            return ParseRandomExpression();
        }

        if (MatchTag(":dice"))
        {
            return ParseDiceExpression();
        }

        if (MatchTag(":list"))
        {
            return ParseCollectionFactoryExpression("list");
        }

        if (MatchTag(":set"))
        {
            return ParseCollectionFactoryExpression("set");
        }

        if (Match(EventScriptTokenKind.Decimal))
        {
            return Previous.TryGetIntegerValue(out var integerValue)
                ? WithRange(new IntegerLiteralExpressionNode(integerValue), Previous)
                : WithRange(new DecimalLiteralExpressionNode(Previous.DecimalValue), Previous);
        }

        if (Match(Percentage))
        {
            return WithRange(new PercentageLiteralExpressionNode(Previous.DecimalValue), Previous);
        }

        if (Match(EventScriptTokenKind.UnitDecimal))
        {
            return WithRange(new UnitDecimalLiteralExpressionNode(Previous.DecimalValue, Previous.UnitName), Previous);
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

        if (Current.Kind == Tag)
        {
            var tagToken = Advance();
            return WithRange(new TagLiteralExpressionNode(tagToken.Text[1..]), tagToken);
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
        throw new EventScriptParseException($"Unexpected token '{token.Text}'", token.Line, token.Column);
    }

    private ExpressionNode ParseBracketLiteralExpression()
    {
        var startToken = Previous;
        SkipNewLines();
        if (Match(Colon))
        {
            SkipNewLines();
            Expect(RightBracket);
            return WithRange(new DictionaryLiteralExpressionNode(Array.Empty<DictionaryEntryNode>()), startToken);
        }

        if (Is(RightBracket))
        {
            Expect(RightBracket);
            return WithRange(new ListLiteralExpressionNode(Array.Empty<ExpressionNode>()), startToken);
        }

        return IsDictionaryLiteralEntryStart()
            ? ParseDictionaryLiteralExpression()
            : ParseListLiteralExpression();
    }

    private HandlerLiteralExpressionNode ParseHandlerLiteralExpression()
    {
        SkipNewLines();
        var startToken = Current;
        var message = Expect(Message).Text;
        var parameters = new List<ParameterNode>();

        if (Match(LeftParen))
        {
            SkipNewLines();
            if (!Is(RightParen))
            {
                parameters.Add(ParseParameter());
                while (Match(Comma))
                {
                    SkipNewLines();
                    parameters.Add(ParseParameter());
                }
            }

            SkipNewLines();
            Expect(RightParen);
        }

        return WithRange(new HandlerLiteralExpressionNode(message, parameters), startToken);
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
            var parameters = new List<ParameterNode> { ParseParameter() };
            while (Match(Comma))
            {
                SkipNewLines();
                parameters.Add(ParseParameter());
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

    private SetLiteralExpressionNode ParseSetLiteralExpression()
    {
        var startToken = Current;
        Expect(LeftBracket);
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
        return WithRange(new SetLiteralExpressionNode(items), startToken);
    }

    private ExpressionNode ParseCollectionFactoryExpression(string collectionType)
    {
        Expect(LeftBracket);
        SkipNewLines();

        if (Match(SelectorSelect))
        {
            var expression = ParseGeneratedCollectionExpression(collectionType);
            SkipNewLines();
            Expect(RightBracket);
            return expression;
        }

        if (collectionType == "set")
        {
            var items = new List<ExpressionNode>();
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
            return new SetLiteralExpressionNode(items);
        }

        var token = Current;
        throw new EventScriptParseException($"Expected :select but found {token.Text}", token.Line, token.Column);
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
                throw new EventScriptParseException("Direct ranges are not allowed after 'in'; use ':select item from ... to ...' or iterate a range value", Current.Line, Current.Column);
            }

            var sourceStart = Current;
            source = WithRange(new CollectionIterationSourceNode(ParseExpression()), sourceStart);
        }
        else
        {
            var token = Current;
            throw new EventScriptParseException($"Expected 'from' or {In} but found {token.Text}", token.Line, token.Column);
        }

        ExpressionNode? predicate = null;
        SkipNewLines();
        if (MatchWord("where"))
        {
            SkipNewLines();
            predicate = ParseExpression();
        }

        SkipNewLines();
        Expect(Arrow);
        SkipNewLines();
        var projection = ParseExpression();
        return WithRange(new GeneratedCollectionExpressionNode(collectionType, identifier, source, predicate, projection), startToken);
    }

    private DictionaryLiteralExpressionNode ParseDictionaryLiteralExpression()
    {
        var startToken = Previous;
        var entries = new List<DictionaryEntryNode>();
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
        return WithRange(new DictionaryLiteralExpressionNode(entries), startToken);
    }

    private DictionaryEntryNode ParseDictionaryEntry()
    {
        var startToken = Current;
        var key = ExpectIdentifier();
        Expect(Colon);
        var value = ParseExpression();
        return WithRange(new DictionaryEntryNode(key, value), startToken);
    }

    private bool IsDictionaryLiteralEntryStart()
    {
        if (Current.Kind != Identifier)
        {
            return false;
        }

        var lookahead = _index + 1;
        while (lookahead < _tokens.Count && _tokens[lookahead].Kind == NewLine)
        {
            lookahead++;
        }

        return lookahead < _tokens.Count && _tokens[lookahead].Kind == Colon;
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
        var diceCountToken = Expect(EventScriptTokenKind.Decimal);
        var sideCountToken = ParseDiceSideCountToken();

        var diceCount = ParsePositiveInteger(diceCountToken, "dice count");
        var sideCount = ParsePositiveInteger(sideCountToken, "side count");

        return WithRange(new DiceExpressionNode(diceCount, sideCount), startToken);
    }

    private CollectionSelectorNode ParseTakeSelector()
    {
        var startToken = Previous;
        SkipNewLines();
        if (TryParseSliceScope(out var scope))
        {
            SkipNewLines();
            var countToken = Expect(EventScriptTokenKind.Decimal);
            return WithRange(new SequenceSliceSelectorNode("take", scope, ParsePositiveInteger(countToken, "take count")), startToken);
        }

        return WithRange(new TakePatternSelectorNode(ParseDicePattern()), startToken);
    }

    private CollectionSelectorNode ParseDropSelector()
    {
        var startToken = Previous;
        SkipNewLines();
        if (!TryParseSliceScope(out var scope))
        {
            var token = Current;
            throw new EventScriptParseException($"Expected drop scope but found {token.Text}", token.Line, token.Column);
        }

        SkipNewLines();
        var countToken = Expect(EventScriptTokenKind.Decimal);
        return WithRange(new SequenceSliceSelectorNode("drop", scope, ParsePositiveInteger(countToken, "drop count")), startToken);
    }

    private bool TryParseSliceScope(out string scope)
    {
        scope = string.Empty;
        if (MatchWord("first"))
        {
            scope = "first";
            return true;
        }

        if (MatchWord("last"))
        {
            scope = "last";
            return true;
        }

        if (MatchWord("highest"))
        {
            scope = "highest";
            return true;
        }

        if (MatchWord("lowest"))
        {
            scope = "lowest";
            return true;
        }

        return false;
    }

    private CollectionSelectorNode ParseChooseSelector()
    {
        var startToken = Previous;
        SkipNewLines();
        var countToken = Expect(EventScriptTokenKind.Decimal);
        var count = ParsePositiveInteger(countToken, "choose count");
        SkipNewLines();

        var atRandom = false;
        if (MatchWord("at"))
        {
            SkipNewLines();
            ExpectWord("random");
            atRandom = true;
            SkipNewLines();
        }

        string? identifier = null;
        ExpressionNode? predicate = null;
        string? weightIdentifier = null;
        ExpressionNode? weightExpression = null;
        if (Current.Kind == Identifier)
        {
            var lookahead = _index + 1;
            while (lookahead < _tokens.Count && _tokens[lookahead].Kind == NewLine)
            {
                lookahead++;
            }

            if (lookahead < _tokens.Count &&
                _tokens[lookahead].Kind == Identifier &&
                string.Equals(_tokens[lookahead].Text, "where", StringComparison.Ordinal))
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
            Expect(Arrow);
            SkipNewLines();
            weightExpression = ParseExpression();
        }

        return WithRange(new ChooseSelectorNode(count, atRandom, identifier, predicate, weightIdentifier, weightExpression), startToken);
    }

    private ExtensionCallExpressionNode ParseExtensionCallExpression()
    {
        var startToken = Current;
        var (extensionName, functionName, _) = ParseExtensionSymbol();
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

    private SequenceLiteralExpressionNode ParseSequenceLiteralExpressionCore(EventScriptToken startToken)
    {
        SkipNewLines();
        var items = new List<ExpressionNode> { ParseEqualityExpression() };
        while (Match(And))
        {
            SkipNewLines();
            items.Add(ParseEqualityExpression());
        }

        return WithRange(new SequenceLiteralExpressionNode(items), startToken);
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
        Expect(And);
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
        while (Match(And))
        {
            SkipNewLines();
            arguments.Add(ParseEqualityExpression());
        }

        return WithRange(new VariadicTaggedExpressionNode(op, arguments), startToken);
    }

    private EventScriptToken ParseDiceSideCountToken()
    {
        if (Match(DiceSeparator))
        {
            return Expect(EventScriptTokenKind.Decimal);
        }

        if (Is(Identifier))
        {
            var token = Current;
            if (string.Equals(token.Text, "d", StringComparison.Ordinal))
            {
                Advance();
                return Expect(EventScriptTokenKind.Decimal);
            }

            if (IsCompactDiceToken(token.Text))
            {
                Advance();
                return new EventScriptToken(
                    EventScriptTokenKind.Decimal,
                    token.Text[1..],
                    token.Line,
                    token.Column + 1,
                    token.EndLine,
                    token.EndColumn);
            }
        }

        var current = Current;
        throw new EventScriptParseException($"Expected dice separator but found {current.Kind}", current.Line, current.Column);
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

    private int ParsePositiveInteger(EventScriptToken numberToken, string name)
    {
        if (!decimal.TryParse(numberToken.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ||
            value <= 0 ||
            value != decimal.Truncate(value))
        {
            throw new EventScriptParseException($"Expected positive integer for {name}", numberToken.Line, numberToken.Column);
        }

        if (value > int.MaxValue)
        {
            throw new EventScriptParseException($"{name} is too large", numberToken.Line, numberToken.Column);
        }

        return (int)value;
    }

    private EventScriptToken Expect(EventScriptTokenKind kind)
    {
        if (Is(kind))
        {
            return Advance();
        }

        var token = Current;
        throw new EventScriptParseException($"Expected {kind} but found {token.Kind}", token);
    }

    private string ExpectIdentifier()
    {
        if (Is(Identifier))
        {
            return Advance().Text;
        }

        var token = Current;
        throw new EventScriptParseException($"Expected Identifier but found {token.Kind}", token);
    }

    private bool Match(params EventScriptTokenKind[] kinds)
    {
        if (!kinds.Any(Is)) return false;
        Advance();
        return true;
    }

    private bool MatchTag(string tagText)
    {
        if (Current.Kind == Tag && string.Equals(Current.Text, tagText, StringComparison.Ordinal))
        {
            Advance();
            return true;
        }

        return false;
    }

    private bool Is(EventScriptTokenKind kind) => Current.Kind == kind;

    private bool IsSeededRandomStatementStart()
    {
        if (Current.Kind != Tag || !string.Equals(Current.Text, ":random", StringComparison.Ordinal))
        {
            return false;
        }

        var lookahead = _index + 1;
        while (lookahead < _tokens.Count && _tokens[lookahead].Kind == NewLine)
        {
            lookahead++;
        }

        return lookahead < _tokens.Count && _tokens[lookahead].Kind == With;
    }

    private bool IsElseClauseStart()
    {
        var lookahead = _index;
        while (lookahead < _tokens.Count && _tokens[lookahead].Kind == NewLine)
        {
            lookahead++;
        }

        return lookahead < _tokens.Count && _tokens[lookahead].Kind == Else;
    }

    private bool IsCallExpressionStart()
    {
        if (Current.Kind is not (Identifier or Message))
        {
            return false;
        }

        var lookahead = _index + 1;
        while (lookahead < _tokens.Count && _tokens[lookahead].Kind == NewLine)
        {
            lookahead++;
        }

        return lookahead < _tokens.Count && _tokens[lookahead].Kind == LeftParen;
    }

    private bool IsExtensionCallStart()
    {
        if (Current.Kind != Tag)
        {
            return false;
        }

        var lookahead = _index + 1;
        while (lookahead < _tokens.Count && _tokens[lookahead].Kind == NewLine)
        {
            lookahead++;
        }

        if (lookahead >= _tokens.Count || _tokens[lookahead].Kind != Dot)
        {
            return false;
        }

        lookahead++;
        while (lookahead < _tokens.Count && _tokens[lookahead].Kind == NewLine)
        {
            lookahead++;
        }

        return lookahead < _tokens.Count && _tokens[lookahead].Kind == Identifier;
    }

    private bool IsTypeConstructorStart()
    {
        if (Current.Kind != Tag)
        {
            return false;
        }

        var lookahead = _index + 1;
        while (lookahead < _tokens.Count && _tokens[lookahead].Kind == NewLine)
        {
            lookahead++;
        }

        return lookahead < _tokens.Count && _tokens[lookahead].Kind == LeftParen;
    }

    private (string ExtensionName, string FunctionName, EventScriptToken EndToken) ParseExtensionSymbol()
    {
        var extensionToken = Expect(Tag);
        SkipNewLines();
        Expect(Dot);
        SkipNewLines();
        var functionToken = Expect(Identifier);
        return (extensionToken.Text[1..], functionToken.Text, functionToken);
    }

    private bool IsExtensionUnaryArgumentStart()
        => Current.Kind is Identifier or Message or Tag or EventScriptTokenKind.Decimal or Percentage or UnitDecimal or Text or True or False or LeftBracket or LeftParen or Minus or Has or Empty or Not;

    private bool IsArgumentLabelStart()
    {
        if (Current.Kind is not (Identifier or To))
        {
            return false;
        }

        var lookahead = _index + 1;
        while (lookahead < _tokens.Count && _tokens[lookahead].Kind == NewLine)
        {
            lookahead++;
        }

        return lookahead < _tokens.Count && _tokens[lookahead].Kind == Colon;
    }

    private string ExpectArgumentLabel()
    {
        if (Current.Kind is Identifier or To)
        {
            return Advance().Text;
        }

        var token = Current;
        throw new EventScriptParseException($"Expected argument label but found {token.Kind}", token);
    }

    private string ParseTypeName()
    {
        if (!Is(Tag))
        {
            var token = Current;
            throw new EventScriptParseException($"Expected type name but found {token.Kind}", token);
        }

        var typeToken = Advance();
        var typeName = typeToken.Text[1..];
        return typeName;
    }

    private void ExpectValueWord()
    {
        if (Current.Kind == Identifier && string.Equals(Current.Text, "value", StringComparison.Ordinal))
        {
            Advance();
            return;
        }

        throw new EventScriptParseException(
            $"Expected value but found {Current.Text}",
            Current);
    }

    private bool MatchWord(string word)
    {
        if (Current.Kind == Identifier && string.Equals(Current.Text, word, StringComparison.Ordinal))
        {
            Advance();
            return true;
        }

        return false;
    }

    private void ExpectWord(string word)
    {
        if (MatchWord(word))
        {
            return;
        }

        throw new EventScriptParseException(
            $"Expected {word} but found {Current.Text}",
            Current);
    }

    private bool IsValueInOperator()
        => Current.Kind == Identifier &&
           string.Equals(Current.Text, "value", StringComparison.Ordinal) &&
           _index + 1 < _tokens.Count &&
           _tokens[_index + 1].Kind == In;

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

    private void RequireStatementSeparatorOrClosing(EventScriptTokenKind closingKind)
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
        throw new EventScriptParseException(
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
        throw new EventScriptParseException(
            $"Expected event handler separator but found {token.Kind}",
            token);
    }

    private EventScriptToken Advance()
    {
        if (!Is(EndOfFile))
        {
            _index++;
        }

        return Previous;
    }

    private EventScriptToken Current => _tokens[_index];

    private EventScriptToken Previous => _tokens[_index - 1];

    private void AddParseError(EventScriptParseException exception)
    {
        _errors.Add(new EventScriptSyntaxError(
            exception.Message,
            _moduleName,
            EventScriptSyntaxErrorKind.Parser,
            new EventScriptSourceLocation(_sourceName, exception.Line, exception.Column, exception.EndLine, exception.EndColumn, _moduleName)));
    }

    private void SynchronizeTopLevel()
    {
        while (!Is(EndOfFile))
        {
            if (Is(Module) || Is(Record) || Is(Rule) || Is(Select) || Is(On))
            {
                return;
            }

            Advance();
        }
    }

    private bool SynchronizeStatement(EventScriptTokenKind closingKind)
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
