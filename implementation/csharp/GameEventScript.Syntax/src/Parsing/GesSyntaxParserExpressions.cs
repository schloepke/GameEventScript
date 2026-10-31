// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using static GameEventScript.Syntax.GesTokenKind;

namespace GameEventScript.Syntax;

internal sealed partial class GesSyntaxParser
{
    // An assembly block in expression position assigns a definition or binding, so it cannot export bindings.
    private Expression ParseAssemblyOrExpression()
    {
        if (!IsWord("asm")) return ParseExpression();
        _tree.StartNode(GesSyntaxNodeKind.AssemblyBlock);
        var start = CurrentIndex;
        Advance();
        ParseAssemblyBlock(standalone: false);
        return FinishNode(Node(0, start));
    }

    private void ParseAssemblyBlock(bool standalone)
    {
        SkipNewLines();
        Expect(LeftBrace);
        SkipStatementSeparators();
        var lines = 0;
        while (!Is(RightBrace))
        {
            var token = Current;
            var exported = Match(Let);
            if (exported || Match(Dot))
            {
                if (!exported) ExpectWord("register");
                if (lines != 0 || (exported && !standalone))
                    throw new ParseException("Assembly declarations must precede instructions; only standalone blocks export bindings.", token);
                do
                {
                    ExpectIdentifier();
                    Role(GesSyntaxRole.AssemblyDeclaration);
                }
                while (Match(Comma));
            }
            else if (Match(Emit) || Match(Publish))
            {
                ParsePublishMessageExpression();
                ParseOptionalTagExpressions();
                lines++;
            }
            else if (Is(Identifier))
            {
                Advance();
                Expect(Colon);
                lines++;
            }
            else
            {
                var name = Expect(Message).Text;
                if (!Is(NewLine) && !Is(Semicolon) && !Is(RightBrace))
                {
                    var operands = 0;
                    do ParseAssemblyOperand(AssemblyMessageOperand(name, operands++)); while (Match(Comma));
                }

                lines++;
            }

            RequireStatementSeparatorOrClosing(RightBrace);
            SkipStatementSeparators();
        }

        Expect(RightBrace);
    }

    private static bool AssemblyMessageOperand(string opcode, int index)
        => opcode switch
        {
            "LoadMessage" or "EmitInstant" or "PublishInstant" => index == 1,
            "EmitAfter" or "PublishAfter" => index == 2,
            "EmitMessage" or "EmitMessageWithTags" or "PublishMessage" or "PublishMessageWithTags" => index == 0,
            _ => false
        };

    private void ParseAssemblyOperand(bool message)
    {
        if (message && Is(Message))
        {
            ParseMessageLiteralExpressionCore();
            return;
        }

        if (Is(OperatorMinus) && _reader.Peek(1).Kind == GesTokenKind.Float && _reader.Peek(1).Text == "9223372036854775808")
        {
            Advance();
            Advance();
            return;
        }

        if (Is(TypeName) && _reader.Peek(1).Kind != LeftParen)
        {
            Advance();
            return;
        }

        // Literal decoding and compound-expression rejection belong to the Compiler's assembly validation.
        ParseExpression();
    }

    private Expression ParseExpression() => ParseGuardedChoiceExpression();

    private Expression ParseGuardedChoiceExpression()
    {
        SkipNewLines();
        if (Is(When))
        {
            _tree.StartNode(GesSyntaxNodeKind.GuardedChoiceExpression);
            Advance();
            return FinishNode(ParseConditionFirstChoiceExpression());
        }

        var start = _tree.Mark();
        var expression = ParseImplicationExpression();
        if (!Is(When)) return expression;

        _tree.StartNodeAt(start, GesSyntaxNodeKind.GuardedChoiceExpression);
        _tree.StartNodeAt(start, GesSyntaxNodeKind.ChoiceBranch);
        Advance();
        var depth = Deepest(expression.Depth, ParseImplicationExpression().Depth);
        _tree.FinishNode();

        while (Match(Comma))
        {
            SkipNewLines();
            if (Is(Otherwise)) break;

            Match(OperatorOr);
            SkipNewLines();

            _tree.StartNode(GesSyntaxNodeKind.ChoiceBranch);
            depth = Deepest(depth, ParseImplicationExpression().Depth);
            SkipNewLines();
            Expect(When);
            SkipNewLines();
            depth = Deepest(depth, ParseImplicationExpression().Depth);
            _tree.FinishNode();
        }

        SkipNewLines();
        Expect(Otherwise);
        SkipNewLines();
        var otherwise = ParseImplicationExpression();
        return FinishNode(Merged(Deepest(depth, otherwise.Depth), expression, otherwise));
    }

    private Expression ParseConditionFirstChoiceExpression()
    {
        var startToken = PreviousIndex;
        var depth = 0;
        while (true)
        {
            SkipNewLines();
            _tree.StartNode(GesSyntaxNodeKind.ChoiceBranch);
            var savedConditionDepth = _choiceConditionDepth;
            _choiceConditionDepth = _delimiterDepth;
            try { depth = Deepest(depth, ParseImplicationExpression().Depth); }
            finally { _choiceConditionDepth = savedConditionDepth; }
            SkipNewLines();
            ExpectWord("then");
            SkipNewLines();
            depth = Deepest(depth, ParseImplicationExpression().Depth);
            _tree.FinishNode();
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
        return Node(Deepest(depth, fallback.Depth), startToken);
    }

    private Expression Binary(Expression left, Expression right) => Merged(Deepest(left.Depth, right.Depth), left, right);

    private Expression ParseImplicationExpression()
    {
        var start = _tree.Mark();
        var expression = ParseDefaultExpression();
        if (!Is(OperatorImplication)) return expression;

        _tree.StartNodeAt(start, GesSyntaxNodeKind.BinaryExpression);
        Advance();
        SkipNewLines();
        if (_expressionNesting >= MaximumExpressionDepth) ThrowNestingLimitAtCurrent();
        _expressionNesting++;
        Expression right;
        try { right = ParseImplicationExpression(); }
        finally { _expressionNesting--; }
        return FinishNode(Binary(expression, right));
    }

    // Left-associative binary levels, loosest first. A null level ends a chain at its tighter, non-table form:
    // membership after equality, multiplication after addition.
    private static readonly GesTokenKind[]?[] BinaryLevels =
    [
        [Default], [OperatorCollectionUnion], [OperatorCollectionIntersect], [Zip], [OperatorOr], [OperatorXor], [OperatorAnd], [OperatorEqual, OperatorNotEqual], null,
        [OperatorLess, OperatorGreater, OperatorLessOrEqual, OperatorGreaterOrEqual], [OperatorPlus, OperatorMinus], null,
    ];

    private const int AndLevel = 6;
    private const int EqualityLevel = 7;
    private const int RelationalLevel = 9;
    private const int AdditiveLevel = 10;

    private Expression ParseDefaultExpression() => ParseBinaryLevel(0);

    private Expression ParseEqualityExpression() => ParseBinaryLevel(EqualityLevel);

    private Expression ParseRelationalExpression() => ParseBinaryLevel(RelationalLevel);

    private Expression ParseAdditiveExpression() => ParseBinaryLevel(AdditiveLevel);

    private Expression ParseBinaryLevel(int level)
    {
        if (BinaryLevels[level] is not { } operators) return level < RelationalLevel ? ParseMembershipExpression() : ParseMultiplicativeExpression();
        var start = _tree.Mark();
        var expression = ParseBinaryLevel(level + 1);
        while ((level != AndLevel || !IsForClauseStart()) && Match(operators))
        {
            _tree.StartNodeAt(start, GesSyntaxNodeKind.BinaryExpression);
            SkipNewLines();
            var right = ParseBinaryLevel(level + 1);
            expression = FinishNode(Binary(expression, right));
        }

        return expression;
    }

    private Expression ParseMembershipExpression()
    {
        var start = _tree.Mark();
        var expression = ParseTypeOperationExpression();
        while (Match(Has, In, NotIn, Starts, Ends))
        {
            _tree.StartNodeAt(start, GesSyntaxNodeKind.MembershipExpression);
            var operatorKind = Previous.Kind;
            SkipNewLines();
            switch (operatorKind)
            {
                case Has:
                    ExpectValueWord();
                    expression = Merged(expression.Depth, expression);
                    break;
                case In when IsValuesOfOperator():
                    ExpectWord("values");
                    SkipNewLines();
                    Expect(Of);
                    SkipNewLines();
                    expression = Binary(expression, ParseTypeOperationExpression());
                    break;
                case In: expression = Binary(expression, ParseTypeOperationExpression()); break;
                case NotIn:
                    // `not in` counts the membership and its negation.
                    var membership = Binary(expression, ParseTypeOperationExpression());
                    expression = Merged(membership.Depth, membership);
                    break;
                default:
                    Expect(With);
                    SkipNewLines();
                    expression = Binary(expression, ParseTypeOperationExpression());
                    break;
            }
            FinishNode(expression);
        }

        return expression;
    }

    private bool IsValuesOfOperator()
    {
        var first = _reader.PeekSignificant(0);
        if (first.Kind != Identifier || !string.Equals(first.Text, "values", StringComparison.Ordinal)) return false;

        return _reader.PeekSignificant(1).Kind == Of;
    }

    // A dice pattern; its depth is that of its face, since a pattern itself adds no level.
    private int ParseDicePattern()
    {
        _tree.StartNode(GesSyntaxNodeKind.DicePattern);
        return FinishNode(ParseDicePatternCore());
    }

    private int ParseDicePatternCore()
    {
        if (MatchWord("pair"))
        {
            SkipNewLines();
            if (!Match(Of)) return 0;
            SkipNewLines();
            return ParseUnaryExpression().Depth;
        }

        if (MatchWord("three") || MatchWord("four") || MatchWord("five") || MatchWord("six") || MatchWord("seven"))
        {
            SkipNewLines();
            Expect(Of);
            SkipNewLines();
            if (!MatchWord("a")) return ParseUnaryExpression().Depth;
            SkipNewLines();
            ExpectWord("kind");
            return 0;
        }

        if (MatchWord("full"))
        {
            SkipNewLines();
            ExpectWord("house");
            return 0;
        }

        if (MatchWord("straight")) return 0;

        var token = Current;
        throw new ParseException($"Expected dice pattern but found {token.Text}", token.Line, token.Column);
    }

    private Expression ParseTypeOperationExpression()
    {
        var start = _tree.Mark();
        var expression = ParseRelationalExpression();
        while (true)
        {
            if (Is(GesTokenKind.Is))
            {
                _tree.StartNodeAt(start, GesSyntaxNodeKind.TypeOperationExpression);
                Advance();
                SkipNewLines();
                var negated = Match(OperatorNot);
                if (negated) SkipNewLines();
                expression = FinishNode(ApplyIsNegation(ParseIsTest(expression), negated));
            }
            else if (Is(As))
            {
                _tree.StartNodeAt(start, GesSyntaxNodeKind.TypeOperationExpression);
                Advance();
                SkipNewLines();
                ParseTypeName();
                expression = FinishNode(Merged(expression.Depth, expression));
            }
            else return expression;
        }
    }

    // `is not` counts the test and its negation.
    private Expression ApplyIsNegation(Expression expression, bool negated) => negated ? Merged(expression.Depth, expression) : expression;

    // The test after `is` or `is not`, without the negation.
    private Expression ParseIsTest(Expression subject)
    {
        if (IsExtensionCallStart())
        {
            ParseExtensionSymbol();
            return Merged(subject.Depth, subject);
        }

        if (Match(Numeric) || MatchWord("integer") || MatchWord("fractional") || Match(Nothing)) return Merged(subject.Depth, subject);
        if (Is(TypeName))
        {
            ParseTypeName();
            return Merged(subject.Depth, subject);
        }

        if (Match(Empty)) return Merged(subject.Depth, subject);
        if (MatchWord("between"))
        {
            SkipNewLines();
            var minimum = ParseAdditiveExpression();
            SkipNewLines();
            Expect(OperatorAnd);
            SkipNewLines();
            var maximum = ParseAdditiveExpression();
            return Merged(Deepest(subject.Depth, minimum.Depth, maximum.Depth), subject, maximum);
        }

        if (MatchWord("at"))
        {
            SkipNewLines();
            if (MatchWord("least") || MatchWord("most")) return ParseIsComparison(subject);
            throw new ParseException($"Expected least or most but found {Current.Text}", Current.Line, Current.Column);
        }

        if (IsWord("less") || IsWord("more"))
        {
            Advance();
            SkipNewLines();
            ExpectWord("than");
            return ParseIsComparison(subject);
        }

        if (Current.Kind == Identifier)
        {
            Advance();
            Role(GesSyntaxRole.PredicateTarget);
            return Merged(subject.Depth, subject);
        }

        // `is value`, `is value or less` and `is value or more`.
        var threshold = ParseAdditiveExpression();
        SkipNewLines();
        if (!Match(OperatorOr)) return Binary(subject, threshold);
        SkipNewLines();
        if (MatchWord("less") || MatchWord("more")) return Binary(subject, threshold);
        throw new ParseException($"Expected less or more but found {Current.Text}", Current.Line, Current.Column);
    }

    // The right operand of an `is` comparison after its operator words.
    private Expression ParseIsComparison(Expression subject)
    {
        SkipNewLines();
        return Binary(subject, ParseAdditiveExpression());
    }

    private Expression ParseMultiplicativeExpression()
    {
        var start = _tree.Mark();
        var expression = ParseUnaryExpression();

        while (true)
        {
            if (Match(OperatorMultiply, OperatorDivide, OperatorIntegerDivide, OperatorModulo, OperatorRemainder))
            {
                _tree.StartNodeAt(start, GesSyntaxNodeKind.BinaryExpression);
                SkipNewLines();
                var right = ParseUnaryExpression();
                expression = FinishNode(Binary(expression, right));
                continue;
            }

            // Implicit multiplication, such as `2pi`, has no operator token.
            if (expression.Shape == Shape.Number && IsImplicitMultiplicationRightStart() && AreAdjacent(Previous, Current))
            {
                _tree.StartNodeAt(start, GesSyntaxNodeKind.BinaryExpression);
                var right = ParseUnaryExpression();
                expression = FinishNode(Binary(expression, right));
                continue;
            }

            break;
        }

        return expression;
    }

    private Expression ParsePowerExpression()
    {
        var start = _tree.Mark();
        var expression = ParsePowerBaseExpression();

        if (Match(OperatorPower))
        {
            _tree.StartNodeAt(start, GesSyntaxNodeKind.BinaryExpression);
            SkipNewLines();
            var right = ParseUnaryExpression();
            expression = FinishNode(Binary(expression, right));
        }
        else if (Match(SuperscriptInteger))
        {
            _tree.StartNodeAt(start, GesSyntaxNodeKind.BinaryExpression);
            expression = FinishNode(Binary(expression, Leaf(PreviousIndex)));
        }

        return expression;
    }

    private bool IsImplicitMultiplicationRightStart() => Current.Kind is Identifier or MathConstantPi or MathConstantE or MathConstantTau or MathConstantInfinity;

    private static bool AreAdjacent(GesToken left, GesToken right) => left.EndLine == right.Line && left.EndColumn == right.Column;

    private Expression ParseUnaryExpression()
    {
        if (_expressionNesting >= MaximumExpressionDepth) ThrowNestingLimitAtCurrent();
        _expressionNesting++;
        try { return ParseUnaryExpressionCore(); }
        finally { _expressionNesting--; }
    }

    private Expression ParseUnaryExpressionCore()
    {
        SkipNewLines();
        if (Current.Kind is OperatorMinus or KeywordParse or Empty or OperatorNot)
        {
            _tree.StartNode(GesSyntaxNodeKind.UnaryExpression);
            var operation = Advance().Kind;
            var opToken = PreviousIndex;
            // `strict` directly after `parse` on the same line is always the mode word, never an operand.
            if (operation == KeywordParse && Current.Kind == Identifier && Current.Text == "strict") Advance();
            if (operation != KeywordParse) SkipNewLines();
            var operand = ParseUnaryExpression();
            var shape = operation == OperatorMinus && operand.Shape is Shape.Number or Shape.Numeric ? Shape.NegatedNumeric : Shape.None;
            return FinishNode(Node(operand.Depth, opToken, shape));
        }

        if (ParseIntrinsicCallExpression() is { } intrinsicCall) return intrinsicCall;
        var start = _tree.Mark();
        var startToken = CurrentIndex;
        if (!ParseKeywordUnaryOperator()) return ParsePowerExpression();
        _tree.StartNodeAt(start, GesSyntaxNodeKind.UnaryExpression);
        // A root lowers to a power whose exponent has no depth of its own, so it measures as the unary operators do.
        var keywordOperand = ParseUnaryIntrinsicOperand();
        return FinishNode(Node(keywordOperand.Depth, startToken));
    }

    private Expression ParsePowerBaseExpression()
    {
        if (Is(Series))
        {
            _tree.StartNode(GesSyntaxNodeKind.SeriesExpression);
            Advance();
            var startToken = PreviousIndex;
            SkipNewLines();
            if (!Match(Fibonacci) && !Match(Factorial)) throw new ParseException("Expected series kind 'fibonacci' or 'factorial'.", Current);
            return FinishNode(Node(0, startToken));
        }

        if (IsExtensionCallStart()) return ParseExtensionCallExpression();

        if (Is(Clamp))
        {
            _tree.StartNode(GesSyntaxNodeKind.ClampExpression);
            Advance();
            return FinishNode(ParseClampExpression());
        }

        if ((Is(Min) || Is(Max)) && IsNextSignificantToken(Of))
        {
            _tree.StartNode(GesSyntaxNodeKind.VariadicExpression);
            Advance();
            return FinishNode(ParseVariadicTaggedExpression());
        }

        var start = _tree.Mark();
        return ParsePostfixSuffixes(ParsePrimaryExpression(), start);
    }

    private bool ParseKeywordUnaryOperator()
    {
        if (Current.Kind is IntrinsicAbs or IntrinsicLn or IntrinsicExp or IntrinsicChance or IntrinsicFloor or IntrinsicCeil or IntrinsicTruncate or IntrinsicRad or IntrinsicDeg or
            IntrinsicSin or IntrinsicCos or IntrinsicTan or IntrinsicAsin or IntrinsicAcos or IntrinsicAtan or IntrinsicSqrt or IntrinsicCbrt)
        {
            Advance();
            return true;
        }

        if (Match(IntrinsicWrap))
        {
            SkipNewLines();
            ExpectWord("degree");
            return true;
        }

        if (!Match(IntrinsicRound)) return false;
        SkipNewLines();
        ExpectWord("half");
        SkipNewLines();
        if (MatchWord("even") || MatchWord("up") || MatchWord("down")) return true;
        throw new ParseException("Expected round mode 'even', 'up', or 'down'.", Current);
    }

    private Expression? ParseIntrinsicCallExpression()
    {
        var start = _tree.Mark();
        var startToken = CurrentIndex;
        string name;
        if (Current.Kind is IntrinsicAtan2 or IntrinsicHypot or IntrinsicNormalize or IntrinsicDot or IntrinsicCross) name = Advance().Text;
        else if (Match(IntrinsicDistance))
        {
            SkipNewLines();
            name = Match(IntrinsicSquared) ? "distance squared" : "distance";
        }
        else if (Match(IntrinsicLength))
        {
            SkipNewLines();
            Expect(IntrinsicSquared);
            name = "length squared";
        }
        else if (Match(IntrinsicAngle))
        {
            SkipNewLines();
            ExpectWord("between");
            name = "angle between";
        }
        else return null;

        _tree.StartNodeAt(start, GesSyntaxNodeKind.IntrinsicCallExpression);
        SkipNewLines();
        var depth = ParseIntrinsicArgumentExpressions(name);
        return FinishNode(Node(depth, startToken));
    }

    private int ParseIntrinsicArgumentExpressions(string name)
    {
        Expect(LeftParen);
        SkipNewLines();

        if (Match(RightParen)) return 0;

        var depth = 0;
        while (true)
        {
            depth = Deepest(depth, ParseExpression().Depth);
            SkipNewLines();

            if (Match(RightParen)) return depth;

            if (!Match(Comma))
            {
                var token = Current;
                throw new ParseException($"Expected ',' or ')' in {name} argument list.", token);
            }

            SkipNewLines();
        }
    }

    private Expression ParseUnaryIntrinsicOperand()
    {
        SkipNewLines();
        if (!Match(LeftParen)) return ParseUnaryExpression();

        SkipNewLines();
        var operand = ParseExpression();
        SkipNewLines();
        Expect(RightParen);
        return operand;
    }

    private Expression ParsePostfixSuffixes(Expression expression, GesSyntaxTreeBuilder.Checkpoint start)
    {
        while (true)
        {
            if (Match(Dot))
            {
                _tree.StartNodeAt(start, GesSyntaxNodeKind.MemberAccessExpression);
                SkipNewLines();
                ExpectIdentifier();
                Role(GesSyntaxRole.Member);
                expression = FinishNode(Merged(expression.Depth, expression));
                continue;
            }

            if (Match(LeftBracket))
            {
                _tree.StartNodeAt(start, GesSyntaxNodeKind.CollectionAccessExpression);
                SkipNewLines();
                expression = ParsePipelineSteps(expression);
                Expect(RightBracket);
                FinishNode(expression);
                continue;
            }

            return expression;
        }
    }

    // `names where predicate`: a selector's binders and the predicate they scope.
    private int ParseBoundWhere()
    {
        ParseBindingNames(GesSyntaxRole.SelectorBinding);
        ExpectWord("where");
        return ParseExpression().Depth;
    }

    // `names => projection`: a selector's binders and the projection they scope.
    private int ParseBoundProjection()
    {
        ParseBindingNames(GesSyntaxRole.SelectorBinding);
        Expect(ProjectionArrow);
        return ParseExpression().Depth;
    }

    private void ParseBindingNames(GesSyntaxRole role)
    {
        ExpectIdentifier();
        Role(role);
        while (Match(Comma))
        {
            SkipNewLines();
            ExpectIdentifier();
            Role(role);
        }
    }

    private bool IsCombinedSelector() => Current.Text is ":cartesian" or ":lockstep" or ":zip" or ":union" or ":intersect" or ":difference";

    private bool IsPipelineSelectorStart()
        => !IsExtensionCallStart() && (IsCombinedSelector() || Current.Text is
            ":entries" or ":keys" or ":values" or ":filter" or ":select" or ":foreach" or ":any" or ":all" or ":count" or
            ":sum" or ":average" or ":min" or ":max" or ":highest" or ":lowest" or ":fold" or ":reduce" or ":map" or
            ":first" or ":last" or ":single" or ":distinct" or ":order" or ":group" or ":sort" or ":reverse" or ":shuffle" or
            ":split" or ":choose" or ":draw" or ":take" or ":drop" or ":contains" or ":has" or ":term");

    // Each step extends the expression of the steps before it by one collection access.
    private Expression ParsePipelineSteps(Expression? source)
    {
        do
        {
            _tree.StartNode(GesSyntaxNodeKind.CollectionSelector);
            var start = Current;
            var startToken = CurrentIndex;
            if (MatchSelector(":split"))
            {
                if (source is not { } input) throw new ParseException("Split requires a source.", start);
                Expect(On);
                var delimiter = MatchWord("whitespace") ? 0 : ParseExpression().Depth;
                source = Merged(Deepest(input.Depth, delimiter), input);
            }
            else if (IsCombinedSelector())
            {
                Advance();
                var sources = source is null ? 0 : 1;
                var depth = source?.Depth ?? 0;
                SkipNewLines();
                depth = Deepest(depth, ParseExpression().Depth);
                sources++;
                while (Match(Comma))
                {
                    SkipNewLines();
                    depth = Deepest(depth, ParseExpression().Depth);
                    sources++;
                }

                if (sources < 2) throw new ParseException("Combined selectors require at least two sources.", start);
                source = Node(depth, startToken);
            }
            else
            {
                if (source is not { } input) throw new ParseException("Selector requires a source.", start);
                var selector = ParseCollectionSelector();
                source = Merged(Deepest(input.Depth, selector.Depth), input, selector);
            }
            _tree.FinishNode();
            SkipNewLines();
        } while (IsPipelineSelectorStart());
        return source!.Value;
    }

    // A selector adds no level of its own; it ranges from its keyword to its last clause.
    private Expression Selected(int depth, int start) => new(depth, start, PreviousIndex, Shape.None);

    private Expression ParseCollectionSelector()
    {
        var start = CurrentIndex;
        SkipNewLines();
        if (MatchSelector(":foreach") || Match(SelectorAny, SelectorAll) || Match(SelectorFilter) || Match(SelectorSelect)) return Selected(Previous.Kind == SelectorSelect || Previous.Text == ":foreach" ? ParseBoundProjection() : ParseBoundWhere(), start);

        if (Match(SelectorHas))
        {
            SkipNewLines();
            // An object-match selector has no range.
            if (Is(LeftBracket)) return new Expression(ParseObjectMatchPattern().Depth, -1, -1, Shape.None);

            return Selected(ParseDicePattern(), start);
        }

        if (Match(SelectorTake))
        {
            SkipNewLines();
            if (MatchSliceScope())
            {
                SkipNewLines();
                ParsePositiveInteger(Expect(GesTokenKind.Float), "take count");
                return Selected(0, start);
            }

            return Selected(ParseDicePattern(), start);
        }

        if (Match(SelectorDrop))
        {
            SkipNewLines();
            if (!MatchSliceScope())
            {
                var token = Current;
                throw new ParseException($"Expected drop scope but found {token.Text}", token.Line, token.Column);
            }

            SkipNewLines();
            ParsePositiveInteger(Expect(GesTokenKind.Float), "drop count");
            return Selected(0, start);
        }

        if (MatchSelector(":term"))
        {
            SkipNewLines();
            return Selected(ParseExpression().Depth, start);
        }

        if (Match(SelectorCount))
        {
            SkipNewLines();
            if (Is(RightBracket) || IsPipelineSelectorStart()) return Selected(0, start);
            return Selected(ParseBoundWhere(), start);
        }

        if (Match(SelectorChoose)) return ParseChooseSelector(start);

        if (Match(SelectorDraw))
        {
            SkipNewLines();
            ParsePositiveInteger(Expect(GesTokenKind.Float), "draw count");
            return Selected(0, start);
        }

        if (Match(SelectorShuffle) || Match(SelectorReverse)) return Selected(0, start);

        if (MatchSelector(":first") || MatchSelector(":last") || MatchSelector(":single"))
        {
            SkipNewLines();
            return Selected(Current.Kind != Identifier ? 0 : ParseBoundWhere(), start);
        }

        if (Match(SelectorSum) || Match(SelectorAverage))
        {
            SkipNewLines();
            if (Is(RightBracket) || IsPipelineSelectorStart()) return Selected(0, start);
            return Selected(ParseBoundProjection(), start);
        }

        if (MatchSelector(":min") || MatchSelector(":max") || MatchSelector(":highest") || MatchSelector(":lowest"))
        {
            SkipNewLines();
            return Selected(ParseBoundProjection(), start);
        }

        if (Current.Kind == Selector && Current.Text is ":fold" or ":reduce")
        {
            var reduce = MatchSelector(":reduce");
            if (!reduce) MatchSelector(":fold");
            ExpectIdentifier();
            Role(GesSyntaxRole.FoldAccumulator);
            var depth = 0;
            if (!reduce)
            {
                Expect(Be);
                depth = ParseExpression().Depth;
            }

            Expect(Comma);
            SkipNewLines();
            ParseBindingNames(GesSyntaxRole.SelectorBinding);
            Expect(ProjectionArrow);
            SkipNewLines();
            return Selected(Deepest(depth, ParseExpression().Depth), start);
        }

        if (MatchSelector(":map"))
        {
            SkipNewLines();
            ParseBindingNames(GesSyntaxRole.SelectorBinding);
            SkipNewLines();
            ExpectWord("by");
            SkipNewLines();
            var depth = ParseExpression().Depth;
            SkipNewLines();
            if (Match(ProjectionArrow))
            {
                SkipNewLines();
                depth = Deepest(depth, ParseExpression().Depth);
            }

            return Selected(depth, start);
        }

        if (Match(SelectorContains))
        {
            SkipNewLines();
            if (MatchWord("all") || MatchWord("any")) SkipNewLines();
            return Selected(ParseExpression().Depth, start);
        }

        if (MatchSelector(":distinct"))
        {
            SkipNewLines();
            if (!MatchWord("by")) return Selected(0, start);
            SkipNewLines();
            return Selected(ParseBoundProjection(), start);
        }

        if (MatchSelector(":group"))
        {
            SkipNewLines();
            ExpectWord("by");
            SkipNewLines();
            return Selected(ParseBoundProjection(), start);
        }

        if (Match(SelectorSort))
        {
            SkipNewLines();
            ParseSortDirection();
            return Selected(0, start);
        }

        if (MatchSelector(":order"))
        {
            SkipNewLines();
            ExpectWord("by");
            SkipNewLines();
            var depth = ParseBoundProjection();
            ParseSortDirection();
            return Selected(depth, start);
        }

        // `:keys`, `:values` and `:entries` select by a tag literal.
        if (Current.Kind == Selector && Current.Text is ":keys" or ":values" or ":entries")
        {
            Advance();
            return Selected(1, start);
        }

        return Selected(ParseExpression().Depth, start);
    }

    private void ParseSortDirection()
    {
        SkipNewLines();
        if (MatchWord("ascending") || MatchWord("descending")) return;

        var token = Current;
        throw new ParseException($"Expected sort direction but found {token.Text}", token.Line, token.Column);
    }

    private bool MatchSliceScope() => MatchWord("first") || MatchWord("last") || MatchWord("highest") || MatchWord("lowest");

    private Expression ParseChooseSelector(int start)
    {
        SkipNewLines();
        ParsePositiveInteger(Expect(GesTokenKind.Float), "choose count");
        SkipNewLines();

        if (MatchWord("at"))
        {
            SkipNewLines();
            Expect(KeywordRandom);
            SkipNewLines();
        }

        var depth = 0;
        if (Current.Kind == Identifier)
        {
            var lookahead = _reader.PeekSignificant(1);
            if (lookahead.Kind == Comma || lookahead.Kind == Identifier && string.Equals(lookahead.Text, "where", StringComparison.Ordinal)) depth = ParseBoundWhere();
        }

        SkipNewLines();
        if (MatchWord("weighted"))
        {
            SkipNewLines();
            ExpectWord("by");
            SkipNewLines();
            ParseBindingNames(GesSyntaxRole.WeightBinding);
            SkipNewLines();
            Expect(ProjectionArrow);
            SkipNewLines();
            depth = Deepest(depth, ParseExpression().Depth);
        }

        return Selected(depth, start);
    }

    private Expression ParseObjectMatchPattern()
    {
        if (_expressionNesting >= MaximumExpressionDepth) ThrowNestingLimitAtCurrent();
        _expressionNesting++;
        try { return ParseObjectMatchPatternCore(); }
        finally { _expressionNesting--; }
    }

    // An object pattern adds one level to the greatest depth of its entry values.
    private Expression ParseObjectMatchPatternCore()
    {
        _tree.StartNode(GesSyntaxNodeKind.ObjectPattern);
        var startToken = CurrentIndex;
        Expect(LeftBracket);
        SkipNewLines();
        var depth = 0;
        if (!Is(RightBracket))
        {
            depth = ParseObjectMatchEntry();
            while (Match(Comma))
            {
                SkipNewLines();
                depth = Deepest(depth, ParseObjectMatchEntry());
            }
        }

        SkipNewLines();
        Expect(RightBracket);
        return FinishNode(Node(depth, startToken));
    }

    private int ParseObjectMatchEntry()
    {
        _tree.StartNode(GesSyntaxNodeKind.ObjectPatternEntry);
        ExpectIdentifier();
        Role(GesSyntaxRole.MapKey);
        Expect(Colon);
        SkipNewLines();
        return FinishNode(Is(LeftBracket) ? ParseObjectMatchPattern().Depth : ParseExpression().Depth);
    }

    private Expression ParsePrimaryExpression()
    {
        if (Is(GesTokenKind.Is))
        {
            _tree.StartNode(GesSyntaxNodeKind.TypeOperationExpression);
            Advance();
            var startToken = PreviousIndex;
            SkipNewLines();
            var negated = Match(OperatorNot);
            SkipNewLines();
            Expect(Identifier);
            Role(GesSyntaxRole.PredicateTarget);
            return FinishNode(ApplyIsNegation(Node(0, startToken), negated));
        }

        // The send node of an expression includes its emit or publish keyword.
        var sendStart = _tree.Mark();
        if (Match(Emit) || Match(Publish)) return ParseSendExpression(PreviousIndex, sendStart);
        ThrowIfIllegalToken();

        if (IsTypeConstructorStart())
        {
            _tree.StartNode(GesSyntaxNodeKind.TypeConstructorExpression);
            return FinishNode(ParseTypeConstructorExpressionCore());
        }

        if (Current.Kind == TypeName && string.Equals(Current.Text, ":Dice", StringComparison.Ordinal) && IsNextSignificantToken(LeftBracket)) return ParseDiceLiteralExpression();

        if (IsWord("from"))
        {
            _tree.StartNode(GesSyntaxNodeKind.RangeExpression);
            Advance();
            return FinishNode(ParseRangeExpressionCore());
        }

        if (Is(KeywordRandom))
        {
            var randomStart = _tree.Mark();
            Advance();
            SkipNewLines();
            if (Match(With)) return WrapNode(randomStart, GesSyntaxNodeKind.SeededRandomExpression, ParseSeededRandomExpression());

            return WrapNode(randomStart, GesSyntaxNodeKind.RandomExpression, ParseRandomExpression());
        }

        if (Is(Roll) && IsNextSignificantWord("dice"))
        {
            _tree.StartNode(GesSyntaxNodeKind.DiceExpression);
            Advance();
            SkipNewLines();
            ExpectWord("dice");
            return FinishNode(ParseDiceExpression());
        }

        if (Current.Kind == TypeName && string.Equals(Current.Text, ":List", StringComparison.Ordinal) && IsNextSignificantToken(LeftBracket))
        {
            _tree.StartNode(GesSyntaxNodeKind.CollectionFactoryExpression);
            Advance();
            return FinishNode(ParseCollectionFactoryExpression());
        }

        if (Current.Kind == Selector && string.Equals(Current.Text, ":set", StringComparison.Ordinal) && IsNextSignificantToken(LeftBracket))
            throw new ParseException("Type ':set' is not supported; use lists or key-only maps.", Current);

        switch (Current.Kind)
        {
            case GesTokenKind.Float:
            case MathConstantPi or MathConstantE or MathConstantTau or MathConstantInfinity:
                Advance();
                return Leaf(PreviousIndex, Shape.Number);
            case Percentage:
                // A percentage beyond the binary64 range reads as nothing, which a negation cannot take.
                Advance();
                return Leaf(PreviousIndex, GesNumberText.IsFinitePercentage(Previous.Text) ? Shape.Numeric : Shape.Scalar);
            case UnitNumber:
                Advance();
                return Leaf(PreviousIndex, Shape.Numeric);
            case Text or True or False or Nothing or Tag:
                Advance();
                return Leaf(PreviousIndex, Shape.Scalar);
            case ConstantReference:
                Advance();
                return Leaf(PreviousIndex);
            case LeftBracket:
                var bracketStart = _tree.Mark();
                Advance();
                return ParseBracketLiteralExpression(bracketStart);
        }

        if ((Current.Kind == Identifier || Current.Kind == Message) && IsCallExpressionStart()) return ParseCallOrHandlerBindExpression();

        if (Current.Kind == Identifier)
        {
            Advance();
            Role(GesSyntaxRole.Variable);
            return Leaf(PreviousIndex);
        }

        if (Is(LeftParen))
        {
            // Parentheses add one level and keep the range and shape of the enclosed expression.
            _tree.StartNode(GesSyntaxNodeKind.ParenthesizedExpression);
            Advance();
            SkipNewLines();
            var expression = ParseExpression();
            SkipNewLines();
            Expect(RightParen);
            return FinishNode(Checked(expression.Nested()));
        }

        var token = Current;
        throw new ParseException($"Unexpected token '{token.Text}'", token.Line, token.Column);
    }

    private Expression ParseBracketLiteralExpression(GesSyntaxTreeBuilder.Checkpoint start)
    {
        var startToken = PreviousIndex;
        SkipNewLines();
        if (IsCombinedSelector())
        {
            // A combined selector without a source, such as `[:cartesian a, b]`.
            var combined = ParsePipelineSteps(null);
            Expect(RightBracket);
            return WrapNode(start, GesSyntaxNodeKind.CollectionAccessExpression, combined);
        }

        if (Match(Colon))
        {
            SkipNewLines();
            Expect(RightBracket);
            return WrapNode(start, GesSyntaxNodeKind.MapLiteralExpression, Node(0, startToken));
        }

        if (Is(RightBracket))
        {
            Expect(RightBracket);
            return WrapNode(start, GesSyntaxNodeKind.ListLiteralExpression, Node(0, startToken));
        }

        return IsMapLiteralEntryStart()
            ? WrapNode(start, GesSyntaxNodeKind.MapLiteralExpression, ParseDictionaryLiteralExpression())
            : WrapNode(start, GesSyntaxNodeKind.ListLiteralExpression, ParseListLiteralExpression());
    }

    private Expression ParseMessageLiteralExpressionCore()
    {
        SkipNewLines();
        _tree.StartNode(GesSyntaxNodeKind.MessageExpression);
        var startToken = CurrentIndex;
        Expect(Message);
        Role(GesSyntaxRole.MessageName);
        var depth = Match(LeftParen) ? ParseArgumentListAfterLeftParen() : 0;
        return FinishNode(Node(depth, startToken));
    }

    private Expression ParseCallOrHandlerBindExpression()
    {
        var start = _tree.Mark();
        var startToken = CurrentIndex;
        var isMessageCallee = Current.Kind == Message;
        if (isMessageCallee) Expect(Message);
        else ExpectIdentifier();
        Role(isMessageCallee ? GesSyntaxRole.MessageName : GesSyntaxRole.CallTarget);
        Expect(LeftParen);
        SkipNewLines();
        if (Is(RightParen))
        {
            Expect(RightParen);
            return WrapNode(start, isMessageCallee ? GesSyntaxNodeKind.HandlerLiteralExpression : GesSyntaxNodeKind.CallExpression, Node(0, startToken));
        }

        if (IsArgumentLabelStart())
        {
            var labeled = ParseArgumentListAfterFirstArgument(ParseArgument(), RightParen);
            Expect(RightParen);
            return WrapNode(start, isMessageCallee ? GesSyntaxNodeKind.MessageExpression : GesSyntaxNodeKind.CallExpression, Node(labeled, startToken));
        }

        if (isMessageCallee)
        {
            ParseParameter(allowDeclaredType: false);
            while (Match(Comma))
            {
                SkipNewLines();
                ParseParameter(allowDeclaredType: false);
            }

            SkipNewLines();
            Expect(RightParen);
            return WrapNode(start, GesSyntaxNodeKind.HandlerLiteralExpression, Node(0, startToken));
        }

        var argument = _tree.Mark();
        var depth = WrapNode(argument, GesSyntaxNodeKind.Argument, ParseExpression().Depth);
        while (Match(Comma))
        {
            SkipNewLines();
            argument = _tree.Mark();
            depth = Deepest(depth, WrapNode(argument, GesSyntaxNodeKind.Argument, ParseExpression().Depth));
        }

        SkipNewLines();
        Expect(RightParen);
        return WrapNode(start, GesSyntaxNodeKind.CallExpression, Node(depth, startToken));
    }

    // A non-empty list ranges from the last token before its first item.
    private Expression ParseListLiteralExpression()
    {
        var startToken = PreviousIndex;
        var depth = 0;
        SkipNewLines();
        if (!Is(RightBracket))
        {
            depth = ParseExpression().Depth;
            while (Match(Comma))
            {
                SkipNewLines();
                depth = Deepest(depth, ParseExpression().Depth);
            }
        }

        SkipNewLines();
        Expect(RightBracket);
        return Node(depth, startToken);
    }

    // A Dice literal reads as a constructor over a list of its values.
    private Expression ParseDiceLiteralExpression()
    {
        _tree.StartNode(GesSyntaxNodeKind.DiceLiteralExpression);
        Advance();
        var startToken = PreviousIndex;
        SkipNewLines();
        Expect(LeftBracket);
        var bracketToken = PreviousIndex;
        var depth = 0;
        SkipNewLines();
        if (!Is(RightBracket))
        {
            while (true)
            {
                var token = Expect(GesTokenKind.Float);
                if (!GesNumberText.IsPositiveInt32(token.Text)) throw new ParseException("Dice literals require positive Int32 numeric literals.", token);
                depth = 1;
                SkipNewLines();
                if (!Match(Comma)) break;
                SkipNewLines();
            }
        }

        Expect(RightBracket);
        var values = Node(depth, bracketToken);
        return FinishNode(Node(values.Depth, startToken));
    }

    private Expression ParseCollectionFactoryExpression()
    {
        Expect(LeftBracket);
        SkipNewLines();

        if (Match(SelectorSelect))
        {
            var expression = ParseGeneratedCollectionExpression();
            SkipNewLines();
            Expect(RightBracket);
            return expression;
        }

        var token = Current;
        throw new ParseException($"Expected :select but found {token.Text}", token.Line, token.Column);
    }

    private Expression ParseGeneratedCollectionExpression()
    {
        var startToken = PreviousIndex;
        SkipNewLines();
        ExpectIdentifier();
        Role(GesSyntaxRole.GeneratedBinding);
        SkipNewLines();
        int depth;
        if (IsWord("from"))
        {
            _tree.StartNode(GesSyntaxNodeKind.RangeExpression);
            Advance();
            depth = FinishNode(ParseRangeExpressionCore()).Depth;
        }
        else if (Match(In))
        {
            SkipNewLines();
            if (Current.Kind == Identifier && string.Equals(Current.Text, "from", StringComparison.Ordinal))
            {
                throw new ParseException("Direct ranges are not allowed after 'in'; use ':select item from ... to ...' or iterate a range value", Current.Line, Current.Column);
            }

            depth = ParseExpression().Depth;
        }
        else
        {
            var token = Current;
            throw new ParseException($"Expected 'from' or {In} but found {token.Text}", token.Line, token.Column);
        }

        SkipNewLines();
        if (MatchWord("where"))
        {
            SkipNewLines();
            depth = Deepest(depth, ParseExpression().Depth);
        }

        SkipNewLines();
        Expect(ProjectionArrow);
        SkipNewLines();
        return Node(Deepest(depth, ParseExpression().Depth), startToken);
    }

    // A non-empty map ranges from the last token before its first entry.
    private Expression ParseDictionaryLiteralExpression()
    {
        var startToken = PreviousIndex;
        var depth = 0;
        SkipNewLines();
        if (!Is(RightBracket))
        {
            depth = ParseDictionaryEntry();
            while (Match(Comma))
            {
                SkipNewLines();
                depth = Deepest(depth, ParseDictionaryEntry());
            }
        }

        SkipNewLines();
        Expect(RightBracket);
        return Node(depth, startToken);
    }

    // An entry adds no level; a key without a value, such as `a:`, holds the literal true.
    private int ParseDictionaryEntry()
    {
        _tree.StartNode(GesSyntaxNodeKind.MapEntry);
        ExpectIdentifier();
        Role(GesSyntaxRole.MapKey);
        Expect(Colon);
        SkipNewLines();
        return FinishNode(Is(Comma) || Is(RightBracket) ? 1 : ParseExpression().Depth);
    }

    private bool IsMapLiteralEntryStart() => Current.Kind == Identifier && _reader.PeekSignificant(1).Kind == Colon;

    private Expression ParseRandomExpression()
    {
        var startToken = PreviousIndex;
        MatchWord("from");
        SkipNewLines();
        var from = ParseRangeBoundExpression();
        SkipNewLines();
        Expect(To);
        SkipNewLines();
        var to = ParseRangeBoundExpression();
        return Node(Deepest(from.Depth, to.Depth), startToken);
    }

    private Expression ParseRangeExpressionCore()
    {
        var startToken = PreviousIndex;
        SkipNewLines();
        var from = ParseRangeBoundExpression();
        SkipNewLines();
        Expect(To);
        SkipNewLines();
        var to = ParseRangeBoundExpression();

        var step = 0;
        if (MatchWord("step"))
        {
            SkipNewLines();
            step = ParseExpression().Depth;
        }

        return Node(Deepest(from.Depth, to.Depth, step), startToken);
    }

    private Expression ParseSeededRandomExpression()
    {
        var startToken = PreviousIndex;
        SkipNewLines();
        var seed = ParseExpression();
        SkipNewLines();
        var body = ParseExpression();
        return Node(Deepest(seed.Depth, body.Depth), startToken);
    }

    private Expression ParseRangeBoundExpression()
    {
        SkipNewLines();
        return ParseAdditiveExpression();
    }

    private Expression ParseDiceExpression()
    {
        var startToken = PreviousIndex;
        var diceCountToken = Expect(GesTokenKind.Float);
        var sideCountToken = ParseDiceSideCountToken();

        ParsePositiveInteger(diceCountToken, "dice count");
        ParsePositiveInteger(sideCountToken, "side count");
        return Node(0, startToken);
    }

    private Expression ParseExtensionCallExpression()
    {
        var start = _tree.Mark();
        var startToken = CurrentIndex;
        var extensionToken = Current;
        var extensionName = ParseExtensionSymbol().Text[1..];
        if (extensionName is "integer" or "degree") throw new ParseException($"Standard extension namespace ':{extensionName}' is not supported; use direct math intrinsics.", extensionToken);

        SkipNewLines();

        var depth = 0;
        var parenthesized = Match(LeftParen);
        if (parenthesized) depth = ParseArgumentListAfterLeftParen();
        else if (Current.Kind == Of) depth = ParseOfArgumentList();
        else if (IsArgumentLabelStart()) depth = ParseUngroupedLabeledArgumentList();
        else if (IsExtensionUnaryArgumentStart() && !(_choiceConditionDepth == _delimiterDepth && Current.Kind == Identifier && Current.Text == "then")) depth = ParseUnaryExpression().Depth;

        var expression = WrapNode(start, GesSyntaxNodeKind.ExtensionCallExpression, Node(depth, startToken));
        return parenthesized ? ParsePostfixSuffixes(expression, start) : expression;
    }

    private Expression ParseTypeConstructorExpressionCore()
    {
        var startToken = CurrentIndex;
        var typeName = GesText.CanonicalTypeName(ParseTypeName());
        SkipNewLines();
        Expect(LeftParen);
        if (typeName == "handler" && Current.Kind == Message)
        {
            // The handler literal of this data form adds no level of its own.
            Advance();
            Role(GesSyntaxRole.MessageName);
            Expect(LeftParen);
            SkipNewLines();
            if (!Is(RightParen))
            {
                do
                {
                    SkipNewLines();
                    if (!Match(Underscore))
                    {
                        ExpectIdentifier();
                        Role(GesSyntaxRole.Parameter);
                    }

                    SkipNewLines();
                } while (Match(Comma));
            }

            Expect(RightParen);
            SkipNewLines();
            Expect(RightParen);
            return Node(0, startToken);
        }

        if (typeName == "message" && Current.Kind == Message)
        {
            // Its tag list adds no level, as the message is its only measured argument.
            var message = ParseMessageLiteralExpressionCore();
            ParseOptionalTagExpressions();
            Expect(RightParen);
            return Node(message.Depth, startToken);
        }

        if (typeName == "series" && Current.Kind is Fibonacci or Factorial)
        {
            Advance();
            var depth = 0;
            if (Match(Comma)) depth = ParseArgumentListAfterLeftParen();
            else Expect(RightParen);
            return Node(depth, startToken);
        }

        return Node(ParseArgumentListAfterLeftParen(), startToken);
    }

    private Expression ParseClampExpression()
    {
        var startToken = PreviousIndex;
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
        return Node(Deepest(value.Depth, minimum.Depth, maximum.Depth), startToken);
    }

    private Expression ParseVariadicTaggedExpression()
    {
        var startToken = PreviousIndex;
        SkipNewLines();
        Expect(Of);
        SkipNewLines();
        var depth = ParseEqualityExpression().Depth;
        while (!IsForClauseStart() && Match(OperatorAnd))
        {
            SkipNewLines();
            depth = Deepest(depth, ParseEqualityExpression().Depth);
        }

        return Node(depth, startToken);
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
                // The rest of a compact dice token such as `d6` is its side count.
                return token with { Kind = GesTokenKind.Float, Text = token.Text[1..], Column = token.Column + 1, FullStart = token.Start + 1, Start = token.Start + 1 };
            }
        }

        var current = Current;
        throw new ParseException($"Expected dice separator but found {current.Kind}", current.Line, current.Column);
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

    private static void ParsePositiveInteger(GesToken numberToken, string name)
    {
        var value = GesNumberText.Value(numberToken.Text);
        if (!double.IsFinite(value) || value <= 0 || value != Math.Truncate(value))
        {
            throw new ParseException($"Expected positive integer for {name}", numberToken.Line, numberToken.Column);
        }

        if (value > int.MaxValue) throw new ParseException($"{name} is too large", numberToken.Line, numberToken.Column);
    }
}
