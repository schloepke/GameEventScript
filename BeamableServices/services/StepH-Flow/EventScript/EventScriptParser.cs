#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using static StepH.Flow.EventScript.EventScriptTokenKind;

namespace StepH.Flow.EventScript;

public sealed class EventScriptParser
{
    private static readonly HashSet<string> KnownTypeNames = new(StringComparer.Ordinal)
    {
        "tag",
        "text",
        "percentage",
        "decimal",
        "integer",
        "boolean",
        "optional",
        "list",
        "dictionary",
        "set",
        "dice",
        "nothing"
    };

    private static readonly HashSet<string> CollectionCombineOperators = new(StringComparer.Ordinal)
    {
        "intersect",
        "combine",
        "merge",
        "except",
        "zip"
    };

    private readonly IReadOnlyList<EventScriptToken> _tokens;
    private int _index;

    private EventScriptParser(IReadOnlyList<EventScriptToken> tokens)
    {
        _tokens = tokens;
    }

    public static EventScriptProgram Parse(string script)
    {
        var lexer = new EventScriptLexer(script);
        var tokens = lexer.Tokenize();
        return new EventScriptParser(tokens).ParseProgram();
    }

    private EventScriptProgram ParseProgram()
    {
        var typeDefinitions = new List<TypeDefinitionNode>();
        var ruleDefinitions = new List<RuleDefinitionNode>();
        var selectDefinitions = new List<SelectDefinitionNode>();
        var handlers = new List<EventHandlerNode>();
        SkipStatementSeparators();
        while (!Is(EndOfFile))
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

        return new EventScriptProgram(typeDefinitions, ruleDefinitions, selectDefinitions, handlers);
    }

    private TypeDefinitionNode ParseTypeDefinition()
    {
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
        return new TypeDefinitionNode(name, fields);
    }

    private TypeFieldDefinitionNode ParseTypeFieldDefinition()
    {
        var name = ExpectIdentifierLike();
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

        return new TypeFieldDefinitionNode(name, typeName, minimumExpression, maximumExpression, computedExpression);
    }

    private RuleDefinitionNode ParseRuleDefinition()
    {
        var name = ExpectIdentifierLike();
        var parameters = ParseDefinitionParameters();
        Expect(Means);
        SkipNewLines();
        var expression = ParseExpression();
        return new RuleDefinitionNode(name, parameters, expression);
    }

    private SelectDefinitionNode ParseSelectDefinition()
    {
        var name = ExpectIdentifierLike();
        var parameters = ParseDefinitionParameters();
        Expect(Means);
        SkipNewLines();
        var expression = ParseExpression();
        return new SelectDefinitionNode(name, parameters, expression);
    }

    private IReadOnlyList<string> ParseDefinitionParameters()
    {
        var parameters = new List<string>();
        Expect(LeftParen);
        if (!Is(RightParen))
        {
            parameters.Add(ExpectIdentifierLike());
            while (Match(Comma))
            {
                parameters.Add(ExpectIdentifierLike());
            }
        }

        Expect(RightParen);
        return parameters;
    }

    private EventHandlerNode ParseEventHandler()
    {
        Expect(On);
        var message = Expect(Message).Text;

        var parameters = new List<string>();
        if (Match(LeftParen))
        {
            if (!Is(RightParen))
            {
                parameters.Add(ExpectIdentifierLike());
                while (Match(Comma))
                {
                    parameters.Add(ExpectIdentifierLike());
                }
            }

            Expect(RightParen);
        }

        SkipNewLines();
        Expect(LeftBrace);
        var statements = ParseStatementsUntil(RightBrace);
        Expect(RightBrace);

        return new EventHandlerNode(message, parameters, statements);
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
                throw new EventScriptParseException($"Expected '{closingKind}' before end of input", token.Line, token.Column);
            }

            statements.Add(ParseStatement());
            RequireStatementSeparatorOrClosing(closingKind);
        }

        return statements;
    }

    private StatementNode ParseStatement()
    {
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
        return new ExpressionStatementNode(expression);
    }

    private PublishStatementNode ParsePublishStatement()
    {
        var message = Expect(Message).Text;
        var arguments = new List<ExpressionNode>();

        if (Match(LeftParen))
        {
            if (!Is(RightParen))
            {
                arguments.Add(ParseExpression());
                while (Match(Comma))
                {
                    arguments.Add(ParseExpression());
                }
            }

            Expect(RightParen);
        }

        return new PublishStatementNode(message, arguments);
    }

    private LetStatementNode ParseLetStatement()
    {
        var identifier = ExpectIdentifierLike();
        string? declaredType = null;
        if (Match(As))
        {
            SkipNewLines();
            declaredType = ParseTypeName();
        }

        if (Match(Be)) return new LetStatementNode(identifier, declaredType, ParseExpression());
        var token = Current;
        throw new EventScriptParseException($"Expected {Be} but found {token.Kind}", token.Line, token.Column);
    }

    private IfStatementNode ParseIfStatement()
    {
        var condition = ParseExpression();
        SkipNewLines();
        Expect(LeftBrace);
        var thenStatements = ParseStatementsUntil(RightBrace);
        Expect(RightBrace);

        var elseStatements = new List<StatementNode>();
        SkipNewLines();
        if (Match(Else))
        {
            SkipNewLines();
            Expect(LeftBrace);
            elseStatements.AddRange(ParseStatementsUntil(RightBrace));
            Expect(RightBrace);
        }

        return new IfStatementNode(condition, thenStatements, elseStatements);
    }

    private ForStatementNode ParseForStatement()
    {
        var identifier = ExpectIdentifierLike();
        Expect(In);
        var source = ParseExpression();
        SkipNewLines();
        Expect(LeftBrace);
        var statements = ParseStatementsUntil(RightBrace);
        Expect(RightBrace);
        return new ForStatementNode(identifier, source, statements);
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
            expression = new GuardedChoiceExpressionNode(branches, otherwiseExpression);
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
            expression = new BinaryExpressionNode(expression, op, right);
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
            expression = new BinaryExpressionNode(expression, op, right);
        }

        return expression;
    }

    private ExpressionNode ParseOrExpression()
    {
        var expression = ParseAndExpression();

        while (Match(Or))
        {
            var op = "||";
            SkipNewLines();
            var right = ParseAndExpression();
            expression = new BinaryExpressionNode(expression, op, right);
        }

        return expression;
    }

    private ExpressionNode ParseAndExpression()
    {
        var expression = ParseEqualityExpression();

        while (Match(And))
        {
            var op = "&&";
            SkipNewLines();
            var right = ParseEqualityExpression();
            expression = new BinaryExpressionNode(expression, op, right);
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
                expression = new UnaryExpressionNode("has value", expression);
                continue;
            }

            if (Match(In))
            {
                var op = Previous.Text;
                SkipNewLines();
                var right = ParseTypeOperationExpression();
                expression = new BinaryExpressionNode(expression, op, right);
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
                expression = new BinaryExpressionNode(expression, op, right);
                continue;
            }

            if (Match(Starts))
            {
                SkipNewLines();
                Expect(With);
                var op = "starts with";
                SkipNewLines();
                var right = ParseTypeOperationExpression();
                expression = new BinaryExpressionNode(expression, op, right);
                continue;
            }

            if (Match(Ends))
            {
                SkipNewLines();
                Expect(With);
                var op = "ends with";
                SkipNewLines();
                var right = ParseTypeOperationExpression();
                expression = new BinaryExpressionNode(expression, op, right);
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
            SkipNewLines();
            if (MatchWord("of"))
            {
                SkipNewLines();
                return new DiceCountPatternNode(2, ParsePatternFace());
            }

            return new DiceCountPatternNode(2, null);
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
            SkipNewLines();
            ExpectWord("house");
            return new DiceFullHousePatternNode();
        }

        if (MatchWord("straight"))
        {
            return new DiceStraightPatternNode();
        }

        var token = Current;
        throw new EventScriptParseException($"Expected dice pattern but found {token.Text}", token.Line, token.Column);
    }

    private DicePatternNode ParseOfPattern(int count)
    {
        SkipNewLines();
        ExpectWord("of");
        SkipNewLines();

        if (MatchWord("a"))
        {
            SkipNewLines();
            ExpectWord("kind");
            return new DiceCountPatternNode(count, null);
        }

        return new DiceCountPatternNode(count, ParsePatternFace());
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
                if (Is(Tag))
                {
                    var typeName = ParseTypeName();
                    expression = new TypeCheckExpressionNode(expression, typeName);
                    continue;
                }

                if (Match(Empty))
                {
                    expression = new UnaryExpressionNode("empty", expression);
                    continue;
                }

                if (MatchWord("at"))
                {
                    SkipNewLines();
                    if (MatchWord("least"))
                    {
                        SkipNewLines();
                        var right = ParseRelationalComparisonOperand();
                        expression = new BinaryExpressionNode(expression, ">=", right);
                        continue;
                    }

                    if (MatchWord("most"))
                    {
                        SkipNewLines();
                        var right = ParseRelationalComparisonOperand();
                        expression = new BinaryExpressionNode(expression, "<=", right);
                        continue;
                    }

                    var token = Current;
                    throw new EventScriptParseException($"Expected least or most but found {token.Text}", token.Line, token.Column);
                }

                if (Current.Kind == Identifier)
                {
                    var ruleName = Advance().Text;
                    expression = new RulePredicateExpressionNode(expression, ruleName);
                    continue;
                }

                var threshold = ParseRelationalComparisonOperand();
                SkipNewLines();
                if (Match(Or))
                {
                    SkipNewLines();
                    if (MatchWord("less"))
                    {
                        expression = new BinaryExpressionNode(expression, "<=", threshold);
                        continue;
                    }

                    if (MatchWord("more") || MatchWord("greater"))
                    {
                        expression = new BinaryExpressionNode(expression, ">=", threshold);
                        continue;
                    }

                    var token = Current;
                    throw new EventScriptParseException($"Expected less, more or greater but found {token.Text}", token.Line, token.Column);
                }

                expression = new BinaryExpressionNode(expression, "=", threshold);
                continue;
            }

            if (Match(As))
            {
                SkipNewLines();
                var typeName = ParseTypeName();
                expression = new TypeCastExpressionNode(expression, typeName);
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
            expression = new BinaryExpressionNode(expression, op, right);
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
            expression = new BinaryExpressionNode(expression, op, right);
        }

        return expression;
    }

    private ExpressionNode ParseMultiplicativeExpression()
    {
        var expression = ParseUnaryExpression();

        while (Match(Multiply, Divide, Modulo))
        {
            var op = Previous.Text;
            SkipNewLines();
            var right = ParseUnaryExpression();
            expression = new BinaryExpressionNode(expression, op, right);
        }

        return expression;
    }

    private ExpressionNode ParseUnaryExpression()
    {
        SkipNewLines();
        if (Match(Has))
        {
            SkipNewLines();
            ExpectValueWord();
            SkipNewLines();
            var hasValueOperand = ParseUnaryExpression();
            return new UnaryExpressionNode("has value", hasValueOperand);
        }

        if (Match(Empty))
        {
            SkipNewLines();
            var emptyOperand = ParseUnaryExpression();
            return new UnaryExpressionNode("empty", emptyOperand);
        }

        if (!Match(Not))
        {
            if (!TryParseTaggedUnaryOperator(out var taggedOperator))
            {
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
            return new UnaryExpressionNode(taggedOperator, taggedOperand);
        }

        var op = Previous.Text;
        if (string.Equals(op, "not", StringComparison.Ordinal))
        {
            op = "!";
        }

        SkipNewLines();
        var operand = ParseUnaryExpression();
        return new UnaryExpressionNode(op, operand);
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
                var member = ExpectIdentifierLike();
                expression = new MemberAccessExpressionNode(expression, member);
                continue;
            }

            if (Match(LeftBracket))
            {
                SkipNewLines();
                var selector = ParseCollectionSelector();
                SkipNewLines();
                Expect(RightBracket);
                expression = new CollectionAccessExpressionNode(expression, selector);
                continue;
            }

            return expression;
        }
    }

    private CollectionSelectorNode ParseCollectionSelector()
    {
        SkipNewLines();
        if (Match(SelectorAny, SelectorAll))
        {
            var op = Previous.Kind == SelectorAny ? "any" : "all";
            var identifier = ExpectIdentifierLike();
            ExpectWord("where");
            var predicate = ParseExpression();
            return new PredicateSelectorNode(op, identifier, predicate);
        }

        if (Match(SelectorHas))
        {
            SkipNewLines();
            if (Is(LeftBracket))
            {
                return new ObjectMatchSelectorNode(ParseObjectMatchPattern());
            }

            return new PatternSelectorNode(ParseDicePattern());
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
            var identifier = ExpectIdentifierLike();
            ExpectWord("where");
            var predicate = ParseExpression();
            return new CountSelectorNode(identifier, predicate);
        }

        if (Match(SelectorChoose))
        {
            return ParseChooseSelector();
        }

        if (Match(SelectorDraw))
        {
            SkipNewLines();
            var countToken = Expect(Number);
            return new DrawSelectorNode(ParsePositiveInteger(countToken, "draw count"));
        }

        if (Match(SelectorShuffle))
        {
            return new ShuffleSelectorNode();
        }

        if (Match(SelectorReverse))
        {
            return new ReverseSelectorNode();
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
            var identifier = ExpectIdentifierLike();
            ExpectWord("where");
            var predicate = ParseExpression();
            return new FilterSelectorNode(identifier, predicate);
        }

        if (Match(SelectorSum))
        {
            var identifier = ExpectIdentifierLike();
            Expect(Arrow);
            var projection = ParseExpression();
            return new SumSelectorNode(identifier, projection);
        }

        if (Match(SelectorAverage))
        {
            var identifier = ExpectIdentifierLike();
            Expect(Arrow);
            var projection = ParseExpression();
            return new AverageSelectorNode(identifier, projection);
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
            var identifier = ExpectIdentifierLike();
            Expect(Arrow);
            var projection = ParseExpression();
            return new SelectSelectorNode(identifier, projection);
        }

        if (MatchTag(":dictionary"))
        {
            SkipNewLines();
            var identifier = ExpectIdentifierLike();
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

            return new DictionarySelectorNode(identifier, keyProjection, valueProjection);
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

        return new ExpressionSelectorNode(ParseExpression());
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
        SkipNewLines();
        return new SortSelectorNode(ParseSortDirection(), null, null);
    }

    private CollectionSelectorNode ParseEdgeSelector(string mode)
    {
        SkipNewLines();
        if (Current.Kind != Identifier)
        {
            return new EdgeSelectorNode(mode, null, null);
        }

        var identifier = ExpectIdentifierLike();
        ExpectWord("where");
        var predicate = ParseExpression();
        return new EdgeSelectorNode(mode, identifier, predicate);
    }

    private CollectionSelectorNode ParseOrderBySelector()
    {
        SkipNewLines();
        ExpectWord("by");
        SkipNewLines();
        var identifier = ExpectIdentifierLike();
        Expect(Arrow);
        var projection = ParseExpression();
        var direction = ParseSortDirection();
        return new OrderBySelectorNode(direction, identifier, projection);
    }

    private CollectionSelectorNode ParseProjectionSelector(string op)
    {
        SkipNewLines();
        var identifier = ExpectIdentifierLike();
        Expect(Arrow);
        var projection = ParseExpression();

        return op switch
        {
            "min" or "lowest" => new MinSelectorNode(identifier, projection),
            "max" or "highest" => new MaxSelectorNode(identifier, projection),
            _ => throw new InvalidOperationException($"Unknown projection selector '{op}'")
        };
    }

    private CollectionSelectorNode ParseContainsSelector()
    {
        SkipNewLines();
        if (MatchWord("all"))
        {
            SkipNewLines();
            return new ContainsSelectorNode("all", ParseExpression());
        }

        if (MatchWord("any"))
        {
            SkipNewLines();
            return new ContainsSelectorNode("any", ParseExpression());
        }

        return new ContainsSelectorNode("single", ParseExpression());
    }

    private CollectionSelectorNode ParseDistinctSelector()
    {
        SkipNewLines();
        if (!MatchWord("by"))
        {
            return new DistinctSelectorNode(null, null);
        }

        SkipNewLines();
        var identifier = ExpectIdentifierLike();
        Expect(Arrow);
        var projection = ParseExpression();
        return new DistinctSelectorNode(identifier, projection);
    }

    private CollectionSelectorNode ParseGroupBySelector()
    {
        SkipNewLines();
        ExpectWord("by");
        SkipNewLines();
        var identifier = ExpectIdentifierLike();
        Expect(Arrow);
        var projection = ParseExpression();
        return new GroupBySelectorNode(identifier, projection);
    }

    private ObjectMatchPatternNode ParseObjectMatchPattern()
    {
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
        return new ObjectMatchPatternNode(entries);
    }

    private ObjectMatchEntryNode ParseObjectMatchEntry()
    {
        var key = ExpectIdentifierLike();
        Expect(Colon);
        SkipNewLines();
        ObjectMatchValueNode value = Is(LeftBracket)
            ? new ObjectMatchNestedValueNode(ParseObjectMatchPattern())
            : new ObjectMatchExpressionValueNode(ParseExpression());
        return new ObjectMatchEntryNode(key, value);
    }

    private ExpressionNode ParsePrimaryExpression()
    {
        if (MatchTag(":random"))
        {
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

        if (Match(Number))
        {
            return new NumberLiteralExpressionNode(Previous.NumberValue, Previous.Text);
        }

        if (Match(Percentage))
        {
            return new PercentageLiteralExpressionNode(Previous.NumberValue);
        }

        if (Match(Text))
        {
            return new TextLiteralExpressionNode(Previous.Text);
        }

        if (Match(LeftBracket))
        {
            return ParseBracketLiteralExpression();
        }

        if (Match(True))
        {
            return new BooleanLiteralExpressionNode(true);
        }

        if (Match(False))
        {
            return new BooleanLiteralExpressionNode(false);
        }

        if (Current.Kind == Tag)
        {
            var tagToken = Advance();
            return new TagLiteralExpressionNode(tagToken.Text[1..]);
        }

        if (Current.Kind == Identifier && IsCallExpressionStart())
        {
            return ParseCallExpression();
        }

        if (IsIdentifierLike(Current.Kind))
        {
            var identifierToken = Advance();
            return new IdentifierExpressionNode(identifierToken.Text);
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
        SkipNewLines();
        if (Match(Colon))
        {
            SkipNewLines();
            Expect(RightBracket);
            return new DictionaryLiteralExpressionNode(Array.Empty<DictionaryEntryNode>());
        }

        if (Is(RightBracket))
        {
            Expect(RightBracket);
            return new ListLiteralExpressionNode(Array.Empty<ExpressionNode>());
        }

        return IsDictionaryLiteralEntryStart()
            ? ParseDictionaryLiteralExpression()
            : ParseListLiteralExpression();
    }

    private CallExpressionNode ParseCallExpression()
    {
        var name = ExpectIdentifierLike();
        Expect(LeftParen);
        var arguments = new List<ExpressionNode>();
        SkipNewLines();
        if (!Is(RightParen))
        {
            arguments.Add(ParseExpression());
            while (Match(Comma))
            {
                SkipNewLines();
                arguments.Add(ParseExpression());
            }
        }

        SkipNewLines();
        Expect(RightParen);
        return new CallExpressionNode(name, arguments);
    }

    private ListLiteralExpressionNode ParseListLiteralExpression()
    {
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
        return new ListLiteralExpressionNode(items);
    }

    private SetLiteralExpressionNode ParseSetLiteralExpression()
    {
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
        return new SetLiteralExpressionNode(items);
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
        SkipNewLines();
        var identifier = ExpectIdentifierLike();
        SkipNewLines();
        ExpectWord("from");
        SkipNewLines();
        var fromExpression = ParseExpression();
        SkipNewLines();
        Expect(To);
        SkipNewLines();
        var toExpression = ParseExpression();

        ExpressionNode? stepExpression = null;
        SkipNewLines();
        if (MatchWord("step"))
        {
            SkipNewLines();
            stepExpression = ParseExpression();
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
        return new GeneratedCollectionExpressionNode(collectionType, identifier, fromExpression, toExpression, stepExpression, predicate, projection);
    }

    private DictionaryLiteralExpressionNode ParseDictionaryLiteralExpression()
    {
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
        return new DictionaryLiteralExpressionNode(entries);
    }

    private DictionaryEntryNode ParseDictionaryEntry()
    {
        var key = ExpectIdentifierLike();
        Expect(Colon);
        var value = ParseExpression();
        return new DictionaryEntryNode(key, value);
    }

    private bool IsDictionaryLiteralEntryStart()
    {
        if (!IsIdentifierLike(Current.Kind))
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
        var fromExpression = ParseRandomBoundExpression();
        SkipNewLines();
        Expect(To);
        SkipNewLines();
        var toExpression = ParseRandomBoundExpression();

        return new RandomExpressionNode(fromExpression, toExpression);
    }

    private ExpressionNode ParseRandomBoundExpression()
    {
        SkipNewLines();
        return ParseAdditiveExpression();
    }

    private DiceExpressionNode ParseDiceExpression()
    {
        var diceCountToken = Expect(Number);
        var sideCountToken = ParseDiceSideCountToken();

        var diceCount = ParsePositiveInteger(diceCountToken, "dice count");
        var sideCount = ParsePositiveInteger(sideCountToken, "side count");

        return new DiceExpressionNode(diceCount, sideCount);
    }

    private CollectionSelectorNode ParseTakeSelector()
    {
        SkipNewLines();
        if (TryParseSliceScope(out var scope))
        {
            SkipNewLines();
            var countToken = Expect(Number);
            return new SequenceSliceSelectorNode("take", scope, ParsePositiveInteger(countToken, "take count"));
        }

        return new TakePatternSelectorNode(ParseDicePattern());
    }

    private CollectionSelectorNode ParseDropSelector()
    {
        SkipNewLines();
        if (!TryParseSliceScope(out var scope))
        {
            var token = Current;
            throw new EventScriptParseException($"Expected drop scope but found {token.Text}", token.Line, token.Column);
        }

        SkipNewLines();
        var countToken = Expect(Number);
        return new SequenceSliceSelectorNode("drop", scope, ParsePositiveInteger(countToken, "drop count"));
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
        SkipNewLines();
        var countToken = Expect(Number);
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
                identifier = ExpectIdentifierLike();
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
            weightIdentifier = ExpectIdentifierLike();
            SkipNewLines();
            Expect(Arrow);
            SkipNewLines();
            weightExpression = ParseExpression();
        }

        return new ChooseSelectorNode(count, atRandom, identifier, predicate, weightIdentifier, weightExpression);
    }

    private ExpressionNode ParseClampExpression()
    {
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
        return new ClampExpressionNode(value, minimum, maximum);
    }

    private ExpressionNode ParseVariadicTaggedExpression(string op)
    {
        SkipNewLines();
        ExpectWord("of");
        SkipNewLines();
        var arguments = new List<ExpressionNode> { ParseEqualityExpression() };
        SkipNewLines();
        while (Match(And))
        {
            SkipNewLines();
            arguments.Add(ParseEqualityExpression());
            SkipNewLines();
        }

        return new VariadicTaggedExpressionNode(op, arguments);
    }

    private EventScriptToken ParseDiceSideCountToken()
    {
        if (Match(DiceSeparator))
        {
            return Expect(Number);
        }

        if (Is(Identifier))
        {
            var token = Current;
            if (string.Equals(token.Text, "d", StringComparison.Ordinal))
            {
                Advance();
                return Expect(Number);
            }

            if (IsCompactDiceToken(token.Text))
            {
                Advance();
                return new EventScriptToken(
                    Number,
                    token.Text[1..],
                    token.Line,
                    token.Column + 1);
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
        throw new EventScriptParseException($"Expected {kind} but found {token.Kind}", token.Line, token.Column);
    }

    private string ExpectIdentifierLike()
    {
        if (IsIdentifierLike(Current.Kind))
        {
            return Advance().Text;
        }

        var token = Current;
        throw new EventScriptParseException($"Expected Identifier but found {token.Kind}", token.Line, token.Column);
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

    private static bool IsIdentifierLike(EventScriptTokenKind kind)
    {
        return kind is
            Identifier or
            On or
            Publish or
            Let or
            As or
            Be or
            When or
            Otherwise or
            Has or
            If or
            Else or
            For or
            In or
            EventScriptTokenKind.Is or
            Record;
    }

    private bool IsCallExpressionStart()
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

        return lookahead < _tokens.Count && _tokens[lookahead].Kind == LeftParen;
    }

    private string ParseTypeName()
    {
        if (!Is(Tag))
        {
            var token = Current;
            throw new EventScriptParseException($"Expected type name but found {token.Kind}", token.Line, token.Column);
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
            Current.Line,
            Current.Column);
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
            Current.Line,
            Current.Column);
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
            token.Line,
            token.Column);
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
            token.Line,
            token.Column);
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
}
