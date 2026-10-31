// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using GameEventScript.Api;
using GameEventScript.Runtime;
using GameEventScript.Syntax;
using static GameEventScript.Syntax.GesTokenKind;
using NodeKind = GameEventScript.Syntax.GesSyntaxNodeKind;

namespace GameEventScript.Compiler;

internal sealed partial class GesSyntaxBinder
{
    private const double SquareRootExponent = 0.5d;
    private const double CubeRootExponent = 0.3333333333333333333333333333d;

    private GesExpressionNode Expression(ref Cursor c) => Operand(ref c);

    // Every expression is one element: a child node, or a single token for a literal or a name.
    private GesExpressionNode Operand(ref Cursor c)
    {
        c.SkipNewLines();
        if (c.Node is { } node)
        {
            c.TakeNode();
            return BindExpression(node);
        }

        return BindPrimary(c.Advance());
    }

    private GesExpressionNode BindPrimary(GesToken token)
    {
        GesExpressionNode node = token.Kind switch
        {
            GesTokenKind.Float => token.GetIntegerValue() is { } integer
                ? new GesIntegerLiteralExpressionNode(integer, token.Text.IndexOf('.') >= 0)
                : new GesFloatLiteralExpressionNode(token.FloatValue),
            Percentage => double.IsFinite(token.FloatValue) ? new GesPercentageLiteralExpressionNode(token.FloatValue) : new GesNothingLiteralExpressionNode(),
            UnitNumber => token.GetIntegerValue() is { } integer
                ? new GesUnitIntegerLiteralExpressionNode(integer, token.UnitName, token.Text.IndexOf('.') >= 0)
                : new GesUnitFloatLiteralExpressionNode(token.FloatValue, token.UnitName),
            Text => new GesTextLiteralExpressionNode(token.Text),
            True => new GesBooleanLiteralExpressionNode(true),
            False => new GesBooleanLiteralExpressionNode(false),
            Nothing => new GesNothingLiteralExpressionNode(),
            Tag => new GesTagLiteralExpressionNode(token.Text[1..]),
            ConstantReference => new GesConstantReferenceExpressionNode(token.Text[1..]),
            MathConstantPi => new GesFloatLiteralExpressionNode(GesMathConstants.Pi),
            MathConstantE => new GesFloatLiteralExpressionNode(GesMathConstants.E),
            MathConstantTau => new GesFloatLiteralExpressionNode(GesMathConstants.Tau),
            MathConstantInfinity => new GesFloatLiteralExpressionNode(GesMathConstants.Infinity),
            Identifier => new GesIdentifierExpressionNode(token.Text),
            _ => throw Mismatch(token.Start)
        };
        return Set(node, Range(token));
    }

    private GesExpressionNode BindExpression(GesSyntaxNode node) => node.Kind switch
    {
        NodeKind.GuardedChoiceExpression => BindGuardedChoice(node),
        NodeKind.BinaryExpression => BindBinary(node),
        NodeKind.MembershipExpression => BindMembership(node),
        NodeKind.TypeOperationExpression => BindTypeOperation(node),
        NodeKind.UnaryExpression => BindUnary(node),
        NodeKind.IntrinsicCallExpression => BindIntrinsicCall(node),
        NodeKind.SeriesExpression => BindSeries(node),
        NodeKind.ClampExpression => BindClamp(node),
        NodeKind.VariadicExpression => BindVariadic(node),
        NodeKind.ExtensionCallExpression => BindExtensionCall(node),
        NodeKind.MemberAccessExpression => BindMemberAccess(node),
        NodeKind.CollectionAccessExpression => BindCollectionAccess(node),
        NodeKind.TypeConstructorExpression => BindTypeConstructor(node),
        NodeKind.DiceLiteralExpression => BindDiceLiteral(node),
        NodeKind.RangeExpression => BindRange(node),
        NodeKind.RandomExpression => BindRandom(node),
        NodeKind.SeededRandomExpression => BindSeededRandom(node),
        NodeKind.DiceExpression => BindDice(node),
        NodeKind.CollectionFactoryExpression => BindCollectionFactory(node),
        NodeKind.ListLiteralExpression => BindListLiteral(node),
        NodeKind.MapLiteralExpression => BindMapLiteral(node),
        NodeKind.MessageExpression => BindMessageLiteral(node),
        NodeKind.CallExpression => BindCall(node),
        NodeKind.HandlerLiteralExpression => BindHandlerLiteral(node),
        NodeKind.ParenthesizedExpression => BindParenthesized(node),
        NodeKind.SendExpression => BindSend(node, null, default),
        _ => throw Mismatch(node.FirstToken)
    };

    private GesExpressionNode Binary(GesExpressionNode left, GesBinaryOperator operation, GesExpressionNode right)
        => Merge(new GesBinaryExpressionNode(left, operation, right), left, right);

    private GesExpressionNode BindGuardedChoice(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        var branches = new List<GesGuardedChoiceBranchNode>();
        if (c.Is(When))
        {
            var start = c.Advance();
            while (true)
            {
                c.SkipNewLines();
                var b = new Cursor(this, c.TakeNode(NodeKind.ChoiceBranch));
                var condition = Expression(ref b);
                b.SkipNewLines();
                b.ExpectWord("then");
                b.SkipNewLines();
                branches.Add(new GesGuardedChoiceBranchNode(Expression(ref b), condition));
                if (!c.Match(Comma)) break;
                c.SkipNewLines();
                if (c.Is(Otherwise)) break;
                c.Match(OperatorOr);
                c.SkipNewLines();
                c.Expect(When);
            }

            c.SkipNewLines();
            c.Expect(Otherwise);
            c.SkipNewLines();
            var fallback = Expression(ref c);
            return Set(new GesGuardedChoiceExpressionNode(branches, fallback), start, c.Previous);
        }

        var first = new Cursor(this, c.TakeNode(NodeKind.ChoiceBranch));
        var expression = Expression(ref first);
        first.Expect(When);
        branches.Add(new GesGuardedChoiceBranchNode(expression, Expression(ref first)));
        while (c.Match(Comma))
        {
            c.SkipNewLines();
            if (c.Is(Otherwise)) break;
            c.Match(OperatorOr);
            c.SkipNewLines();
            var b = new Cursor(this, c.TakeNode(NodeKind.ChoiceBranch));
            var value = Expression(ref b);
            b.SkipNewLines();
            b.Expect(When);
            b.SkipNewLines();
            branches.Add(new GesGuardedChoiceBranchNode(value, Expression(ref b)));
        }

        c.SkipNewLines();
        c.Expect(Otherwise);
        c.SkipNewLines();
        var otherwise = Expression(ref c);
        return Merge(new GesGuardedChoiceExpressionNode(branches, otherwise), expression, otherwise);
    }

    private static GesBinaryOperator? BinaryOperator(GesTokenKind kind) => kind switch
    {
        Default => GesBinaryOperator.Default,
        OperatorCollectionUnion => GesBinaryOperator.Union,
        OperatorCollectionIntersect => GesBinaryOperator.Intersect,
        Zip => GesBinaryOperator.Zip,
        OperatorOr => GesBinaryOperator.Or,
        OperatorXor => GesBinaryOperator.Xor,
        OperatorAnd => GesBinaryOperator.And,
        OperatorEqual => GesBinaryOperator.Equal,
        OperatorNotEqual => GesBinaryOperator.NotEqual,
        OperatorLess => GesBinaryOperator.Less,
        OperatorGreater => GesBinaryOperator.Greater,
        OperatorLessOrEqual => GesBinaryOperator.LessOrEqual,
        OperatorGreaterOrEqual => GesBinaryOperator.GreaterOrEqual,
        OperatorPlus => GesBinaryOperator.Add,
        OperatorMinus => GesBinaryOperator.Subtract,
        OperatorMultiply => GesBinaryOperator.Multiply,
        OperatorDivide => GesBinaryOperator.Divide,
        OperatorIntegerDivide => GesBinaryOperator.IntegerDivide,
        OperatorModulo => GesBinaryOperator.Modulo,
        OperatorRemainder => GesBinaryOperator.Remainder,
        OperatorImplication => GesBinaryOperator.Implies,
        OperatorPower => GesBinaryOperator.Power,
        _ => null,
    };

    private GesExpressionNode BindBinary(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        var left = Operand(ref c);
        if (c.Is(SuperscriptInteger))
        {
            var exponentToken = c.Advance();
            var exponent = Set(new GesIntegerLiteralExpressionNode(long.Parse(exponentToken.Text, CultureInfo.InvariantCulture)), Range(exponentToken));
            return Binary(left, GesBinaryOperator.Power, exponent);
        }

        // Implicit multiplication, such as `2pi`, has no operator token.
        if (!c.IsOwnToken || BinaryOperator(c.Current.Kind) is not { } operation) return Binary(left, GesBinaryOperator.Multiply, Operand(ref c));
        c.Advance();
        c.SkipNewLines();
        return Binary(left, operation, Operand(ref c));
    }

    private GesExpressionNode BindMembership(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        var left = Operand(ref c);
        var operatorKind = c.Advance().Kind;
        c.SkipNewLines();
        switch (operatorKind)
        {
            case Has:
                c.ExpectWord("value");
                return Merge(new GesUnaryExpressionNode(GesUnaryOperator.HasValue, left), left);
            case In when c.IsWord("values") && c.PeekSignificant(1).Kind == Of:
                c.Advance();
                c.SkipNewLines();
                c.Expect(Of);
                c.SkipNewLines();
                return Binary(left, GesBinaryOperator.ContainsValue, Operand(ref c));
            case In:
                return Binary(left, GesBinaryOperator.Contains, Operand(ref c));
            case NotIn:
                var membership = Binary(left, GesBinaryOperator.Contains, Operand(ref c));
                return Merge(new GesUnaryExpressionNode(GesUnaryOperator.Not, membership), membership);
            default:
                c.Expect(With);
                c.SkipNewLines();
                return Binary(left, operatorKind == Starts ? GesBinaryOperator.StartsWith : GesBinaryOperator.EndsWith, Operand(ref c));
        }
    }

    private GesExpressionNode IsNegation(GesExpressionNode expression, bool negated)
        => negated ? Merge(new GesUnaryExpressionNode(GesUnaryOperator.Not, expression), expression) : expression;

    private GesExpressionNode BindTypeOperation(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        if (c.Is(GesTokenKind.Is))
        {
            // `is name` without a subject tests the handler's message.
            var start = c.Advance();
            c.SkipNewLines();
            var negatedCall = c.Match(OperatorNot);
            c.SkipNewLines();
            var name = c.Expect(Identifier).Text;
            return IsNegation(Set(new GesPredicateCallExpressionNode(null, name), start, c.Previous), negatedCall);
        }

        var subject = Operand(ref c);
        if (c.Match(As))
        {
            c.SkipNewLines();
            return Merge(new GesTypeCastExpressionNode(subject, BindTypeName(c.TakeNode(NodeKind.TypeReference))), subject);
        }

        c.Expect(GesTokenKind.Is);
        c.SkipNewLines();
        var negated = c.Match(OperatorNot);
        if (negated) c.SkipNewLines();
        return IsNegation(BindIsTest(ref c, subject), negated);
    }

    private GesExpressionNode BindIsTest(ref Cursor c, GesExpressionNode subject)
    {
        if (IsExtensionSymbolStart(ref c))
        {
            var (extensionName, functionName) = ExtensionSymbol(ref c);
            return Merge(new GesExtensionPredicateExpressionNode(subject, extensionName, functionName), subject);
        }

        if (c.Match(Numeric)) return Merge(new GesTypeCheckExpressionNode(subject, "numeric"), subject);
        if (c.MatchWord("integer")) return Merge(new GesTypeCheckExpressionNode(subject, "numeric:integer"), subject);
        if (c.MatchWord("fractional")) return Merge(new GesTypeCheckExpressionNode(subject, "numeric:fractional"), subject);
        if (c.Match(Nothing)) return Merge(new GesNothingCheckExpressionNode(subject), subject);
        if (c.IsNode(NodeKind.TypeReference)) return Merge(new GesTypeCheckExpressionNode(subject, BindTypeName(c.TakeNode())), subject);
        if (c.Match(Empty)) return Merge(new GesUnaryExpressionNode(GesUnaryOperator.Empty, subject), subject);
        if (c.MatchWord("between"))
        {
            c.SkipNewLines();
            var minimum = Operand(ref c);
            c.SkipNewLines();
            c.Expect(OperatorAnd);
            c.SkipNewLines();
            var maximum = Operand(ref c);
            return Merge(new GesBetweenExpressionNode(subject, minimum, maximum), subject, maximum);
        }

        if (c.MatchWord("at"))
        {
            c.SkipNewLines();
            if (c.MatchWord("least")) return IsComparison(ref c, subject, GesBinaryOperator.GreaterOrEqual);
            c.ExpectWord("most");
            return IsComparison(ref c, subject, GesBinaryOperator.LessOrEqual);
        }

        if (c.IsWord("less") || c.IsWord("more"))
        {
            var operation = c.Advance().Text == "less" ? GesBinaryOperator.Less : GesBinaryOperator.Greater;
            c.SkipNewLines();
            c.ExpectWord("than");
            return IsComparison(ref c, subject, operation);
        }

        if (c.Is(Identifier)) return Merge(new GesPredicateCallExpressionNode(subject, c.Advance().Text), subject);

        // `is value`, `is value or less` and `is value or more`.
        var threshold = Operand(ref c);
        c.SkipNewLines();
        if (!c.Match(OperatorOr)) return Binary(subject, GesBinaryOperator.Equal, threshold);
        c.SkipNewLines();
        if (c.MatchWord("less")) return Binary(subject, GesBinaryOperator.LessOrEqual, threshold);
        c.ExpectWord("more");
        return Binary(subject, GesBinaryOperator.GreaterOrEqual, threshold);
    }

    private GesExpressionNode IsComparison(ref Cursor c, GesExpressionNode subject, GesBinaryOperator operation)
    {
        c.SkipNewLines();
        return Binary(subject, operation, Operand(ref c));
    }

    private static bool IsExtensionSymbolStart(ref Cursor c)
        => c.IsOwnToken && c.Current.Kind != TypeName && c.Current.Text.StartsWith(":", StringComparison.Ordinal) && c.PeekSignificant(1).Kind == Dot;

    private static (string ExtensionName, string FunctionName) ExtensionSymbol(ref Cursor c)
    {
        var extension = c.Advance();
        c.SkipNewLines();
        c.Expect(Dot);
        c.SkipNewLines();
        return (extension.Text[1..], c.Advance().Text);
    }

    private GesExpressionNode BindUnary(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        var start = c.Current;
        if (start.Kind is OperatorMinus or KeywordParse or Empty or OperatorNot)
        {
            c.Advance();
            var operation = start.Kind switch { OperatorMinus => GesUnaryOperator.Negate, KeywordParse => GesUnaryOperator.Parse, Empty => GesUnaryOperator.Empty, _ => GesUnaryOperator.Not };
            if (operation == GesUnaryOperator.Parse && c.MatchWord("strict")) operation = GesUnaryOperator.ParseStrict;
            if (operation is not (GesUnaryOperator.Parse or GesUnaryOperator.ParseStrict)) c.SkipNewLines();
            var operand = Operand(ref c);
            return Set(new GesUnaryExpressionNode(operation, operand), start, c.Previous);
        }

        var (keywordOperator, rootExponent) = KeywordOperator(ref c);
        var keywordOperand = IntrinsicOperand(ref c);
        if (rootExponent is { } exponent)
        {
            // Roots lower to powers; the exponent spans the root keyword and its operand.
            var exponentNode = new GesFloatLiteralExpressionNode(exponent) { SourceRange = Range(start, c.Previous) };
            return Set(new GesBinaryExpressionNode(keywordOperand, GesBinaryOperator.Power, exponentNode), start, c.Previous);
        }

        return Set(new GesUnaryExpressionNode(keywordOperator, keywordOperand), start, c.Previous);
    }

    private static (GesUnaryOperator Operator, double? RootExponent) KeywordOperator(ref Cursor c)
    {
        GesUnaryOperator? simple = c.Current.Kind switch
        {
            IntrinsicAbs => GesUnaryOperator.Abs,
            IntrinsicLn => GesUnaryOperator.NaturalLog,
            IntrinsicExp => GesUnaryOperator.Exp,
            IntrinsicChance => GesUnaryOperator.Chance,
            IntrinsicFloor => GesUnaryOperator.Floor,
            IntrinsicCeil => GesUnaryOperator.Ceil,
            IntrinsicTruncate => GesUnaryOperator.Truncate,
            IntrinsicRad => GesUnaryOperator.DegreeToRadians,
            IntrinsicDeg => GesUnaryOperator.DegreeFromRadians,
            IntrinsicSin => GesUnaryOperator.Sin,
            IntrinsicCos => GesUnaryOperator.Cos,
            IntrinsicTan => GesUnaryOperator.Tan,
            IntrinsicAsin => GesUnaryOperator.Asin,
            IntrinsicAcos => GesUnaryOperator.Acos,
            IntrinsicAtan => GesUnaryOperator.Atan,
            _ => null,
        };
        var token = c.Advance();
        if (simple is { } operation) return (operation, null);
        switch (token.Kind)
        {
            case IntrinsicSqrt: return (default, SquareRootExponent);
            case IntrinsicCbrt: return (default, CubeRootExponent);
            case IntrinsicWrap:
                c.SkipNewLines();
                c.ExpectWord("degree");
                return (GesUnaryOperator.WrapDegree, null);
        }

        c.SkipNewLines();
        c.ExpectWord("half");
        c.SkipNewLines();
        if (c.MatchWord("even")) return (GesUnaryOperator.RoundHalfEven, null);
        if (c.MatchWord("up")) return (GesUnaryOperator.RoundHalfUp, null);
        c.ExpectWord("down");
        return (GesUnaryOperator.RoundHalfDown, null);
    }

    private GesExpressionNode IntrinsicOperand(ref Cursor c)
    {
        c.SkipNewLines();
        if (!c.Match(LeftParen)) return Operand(ref c);
        c.SkipNewLines();
        var operand = Expression(ref c);
        c.SkipNewLines();
        c.Expect(RightParen);
        return operand;
    }

    private GesExpressionNode BindIntrinsicCall(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        var start = c.Advance();
        GesIntrinsicFunction function;
        switch (start.Kind)
        {
            case IntrinsicAtan2: function = GesIntrinsicFunction.Atan2; break;
            case IntrinsicHypot: function = GesIntrinsicFunction.Hypot; break;
            case IntrinsicNormalize: function = GesIntrinsicFunction.Normalize; break;
            case IntrinsicDot: function = GesIntrinsicFunction.Dot; break;
            case IntrinsicCross: function = GesIntrinsicFunction.Cross; break;
            case IntrinsicDistance:
                c.SkipNewLines();
                function = c.Match(IntrinsicSquared) ? GesIntrinsicFunction.DistanceSquared : GesIntrinsicFunction.Distance;
                break;
            case IntrinsicLength:
                c.SkipNewLines();
                c.Expect(IntrinsicSquared);
                function = GesIntrinsicFunction.LengthSquared;
                break;
            default:
                c.SkipNewLines();
                c.ExpectWord("between");
                function = GesIntrinsicFunction.AngleBetween;
                break;
        }

        c.SkipNewLines();
        c.Expect(LeftParen);
        c.SkipNewLines();
        IReadOnlyList<GesExpressionNode> arguments = [];
        if (!c.Match(RightParen))
        {
            var list = new List<GesExpressionNode>();
            while (true)
            {
                list.Add(Expression(ref c));
                c.SkipNewLines();
                if (c.Match(RightParen)) break;
                c.Expect(Comma);
                c.SkipNewLines();
            }

            arguments = list;
        }

        return Set(new GesIntrinsicCallExpressionNode(function, arguments), start, c.Previous);
    }

    private GesExpressionNode BindSeries(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        var start = c.Expect(Series);
        c.SkipNewLines();
        var kind = c.Advance().Kind == Fibonacci ? GesBytecodeSeriesKind.Fibonacci : GesBytecodeSeriesKind.Factorial;
        return Set(new GesSeriesExpressionNode(kind), start, c.Previous);
    }

    private GesExpressionNode BindClamp(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        var start = c.Expect(Clamp);
        c.SkipNewLines();
        var value = Operand(ref c);
        c.SkipNewLines();
        c.ExpectWord("between");
        c.SkipNewLines();
        var minimum = Operand(ref c);
        c.SkipNewLines();
        c.Expect(OperatorAnd);
        c.SkipNewLines();
        var maximum = Operand(ref c);
        return Set(new GesClampExpressionNode(value, minimum, maximum), start, c.Previous);
    }

    private GesExpressionNode BindVariadic(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        var start = c.Advance();
        c.SkipNewLines();
        c.Expect(Of);
        c.SkipNewLines();
        var arguments = new List<GesExpressionNode> { Operand(ref c) };
        while (c.Match(OperatorAnd))
        {
            c.SkipNewLines();
            arguments.Add(Operand(ref c));
        }

        return Set(new GesVariadicTaggedExpressionNode(start.Kind == Min ? "min" : "max", arguments), start, c.Previous);
    }

    private GesExpressionNode BindExtensionCall(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        var start = c.Current;
        var (extensionName, functionName) = ExtensionSymbol(ref c);
        c.SkipNewLines();
        GesArgumentListNode arguments;
        if (c.Match(LeftParen)) arguments = ArgumentsAfterLeftParen(ref c);
        else if (c.Match(Of))
        {
            // `of a and b` arguments carry no source range.
            c.SkipNewLines();
            var list = new List<GesArgumentNode> { new(null, Operand(ref c)) };
            while (c.Match(OperatorAnd))
            {
                c.SkipNewLines();
                list.Add(new GesArgumentNode(null, Operand(ref c)));
            }

            arguments = new GesArgumentListNode(list);
        }
        else if (c.IsNode(NodeKind.Argument))
        {
            var list = new List<GesArgumentNode>();
            do list.Add(BindArgument(c.TakeNode())); while (c.IsNode(NodeKind.Argument));
            arguments = new GesArgumentListNode(list);
        }
        else if (!c.AtEnd) arguments = new GesArgumentListNode([new GesArgumentNode(null, Operand(ref c))]);
        else arguments = GesArgumentListNode.Empty;

        return Set(new GesExtensionCallExpressionNode(extensionName, functionName, arguments), start, c.Previous);
    }

    private GesArgumentListNode ArgumentsAfterLeftParen(ref Cursor c)
    {
        c.SkipNewLines();
        if (c.Match(RightParen)) return GesArgumentListNode.Empty;
        var arguments = ArgumentsAfterFirst(ref c, BindArgument(c.TakeNode(NodeKind.Argument)));
        c.Expect(RightParen);
        return arguments;
    }

    private GesArgumentListNode ArgumentsAfterFirst(ref Cursor c, GesArgumentNode first)
    {
        var arguments = new List<GesArgumentNode> { first };
        while (true)
        {
            c.SkipNewLines();
            if (c.Match(Comma))
            {
                c.SkipNewLines();
                arguments.Add(BindArgument(c.TakeNode(NodeKind.Argument)));
                continue;
            }

            if (c.IsNode(NodeKind.Argument))
            {
                arguments.Add(BindArgument(c.TakeNode()));
                continue;
            }

            return new GesArgumentListNode(arguments);
        }
    }

    private static bool IsArgumentLabelKind(GesTokenKind kind)
        => kind is Identifier or To or Nothing or MathConstantPi or MathConstantE or MathConstantTau or MathConstantInfinity or
            IntrinsicAbs or IntrinsicLn or IntrinsicExp or IntrinsicSqrt or IntrinsicCbrt or IntrinsicChance or
            IntrinsicFloor or IntrinsicCeil or IntrinsicTruncate or IntrinsicRad or IntrinsicDeg or IntrinsicWrap or IntrinsicRound or
            IntrinsicSin or IntrinsicCos or IntrinsicTan or IntrinsicAsin or IntrinsicAcos or IntrinsicAtan or IntrinsicAtan2 or
            IntrinsicHypot or IntrinsicDistance or IntrinsicSquared or IntrinsicLength or IntrinsicNormalize or IntrinsicDot or IntrinsicCross or IntrinsicAngle;

    private static bool IsLabelStart(ref Cursor c) => c.IsOwnToken && IsArgumentLabelKind(c.Current.Kind) && c.PeekSignificant(1).Kind == Colon;

    private GesArgumentNode BindArgument(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        var start = c.Current;
        string? label = null;
        if (IsLabelStart(ref c))
        {
            label = c.Advance().Text;
            c.SkipNewLines();
            c.Expect(Colon);
            c.SkipNewLines();
        }

        return Set(new GesArgumentNode(label, Expression(ref c)), start, c.Previous);
    }

    private GesExpressionNode BindMemberAccess(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        var target = Operand(ref c);
        c.Expect(Dot);
        c.SkipNewLines();
        return Merge(new GesMemberAccessExpressionNode(target, c.Expect(Identifier).Text), target);
    }

    private GesExpressionNode BindCollectionAccess(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        // A combined selector without a source, such as `[:cartesian a, b]`, starts with its bracket.
        var target = c.Is(LeftBracket) ? null : Operand(ref c);
        c.Expect(LeftBracket);
        c.SkipNewLines();
        var expression = PipelineSteps(ref c, target);
        c.Expect(RightBracket);
        return expression;
    }

    private GesTypeConstructorExpressionNode BindTypeConstructor(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        var start = c.Current;
        var typeName = BindTypeName(c.TakeNode(NodeKind.TypeReference));
        c.SkipNewLines();
        c.Expect(LeftParen);
        if (typeName == "handler" && c.Is(Message))
        {
            var name = c.Advance().Text;
            c.Expect(LeftParen);
            c.SkipNewLines();
            var parameters = new List<GesParameterNode>();
            if (!c.Is(RightParen))
            {
                do
                {
                    c.SkipNewLines();
                    if (c.Match(Underscore)) parameters.Add(new GesParameterNode(null, "literalArgument" + parameters.Count.ToString(CultureInfo.InvariantCulture)));
                    else
                    {
                        var label = c.Expect(Identifier).Text;
                        parameters.Add(new GesParameterNode(label, label));
                    }

                    c.SkipNewLines();
                }
                while (c.Match(Comma));
            }

            var usedNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var parameter in parameters)
                if (parameter.ExternalLabel is not null) usedNames.Add(parameter.LocalName);
            for (var index = 0; index < parameters.Count; index++)
            {
                if (parameters[index].ExternalLabel is not null) continue;
                var local = parameters[index].LocalName;
                while (!usedNames.Add(local)) local += "x";
                parameters[index] = parameters[index] with { LocalName = local };
            }

            c.Expect(RightParen);
            c.SkipNewLines();
            c.Expect(RightParen);
            return Set(new GesTypeConstructorExpressionNode(typeName, new GesArgumentListNode([new GesArgumentNode(null, new GesHandlerLiteralExpressionNode(name, parameters))])), start, c.Previous);
        }

        if (typeName == "message" && c.IsNode(NodeKind.MessageExpression))
        {
            var message = BindMessageLiteral(c.TakeNode());
            var tags = OptionalTags(ref c);
            c.Expect(RightParen);
            var items = new List<GesArgumentNode> { new(null, message) };
            if (tags.Count > 0) items.Add(new GesArgumentNode("tags", new GesListLiteralExpressionNode(tags)));
            return Set(new GesTypeConstructorExpressionNode(typeName, new GesArgumentListNode(items)), start, c.Previous);
        }

        if (typeName == "series" && (c.Is(Fibonacci) || c.Is(Factorial)))
        {
            var items = new List<GesArgumentNode> { new(null, new GesTextLiteralExpressionNode(c.Advance().Text)) };
            if (c.Match(Comma)) items.AddRange(ArgumentsAfterLeftParen(ref c).Arguments);
            else c.Expect(RightParen);
            return Set(new GesTypeConstructorExpressionNode(typeName, new GesArgumentListNode(items)), start, c.Previous);
        }

        var arguments = ArgumentsAfterLeftParen(ref c);
        return Set(new GesTypeConstructorExpressionNode(typeName, arguments), start, c.Previous);
    }

    private GesExpressionNode BindDiceLiteral(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        var start = c.Expect(TypeName);
        c.SkipNewLines();
        var bracket = c.Expect(LeftBracket);
        var items = new List<GesExpressionNode>();
        c.SkipNewLines();
        if (!c.Is(RightBracket))
        {
            while (true)
            {
                var token = c.Expect(GesTokenKind.Float);
                items.Add(Set(new GesIntegerLiteralExpressionNode(token.GetIntegerValue()!.Value), Range(token)));
                c.SkipNewLines();
                if (!c.Match(Comma)) break;
                c.SkipNewLines();
            }
        }

        c.Expect(RightBracket);
        var values = Set(new GesListLiteralExpressionNode(items), bracket, c.Previous);
        // Reuse deterministic list-to-Dice conversion; CreateDice is a random roll.
        return Set(new GesTypeConstructorExpressionNode("dice", new GesArgumentListNode([new GesArgumentNode(null, values)])), start, c.Previous);
    }

    private GesExpressionNode BindRange(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        c.ExpectWord("from");
        return BindRangeCore(ref c);
    }

    private GesRangeExpressionNode BindRangeCore(ref Cursor c)
    {
        var start = c.Previous;
        c.SkipNewLines();
        var from = Operand(ref c);
        c.SkipNewLines();
        c.Expect(To);
        c.SkipNewLines();
        var to = Operand(ref c);
        GesExpressionNode? step = null;
        if (c.MatchWord("step"))
        {
            c.SkipNewLines();
            step = Expression(ref c);
        }

        return Set(new GesRangeExpressionNode(from, to, step), start, c.Previous);
    }

    private GesExpressionNode BindRandom(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        c.Expect(KeywordRandom);
        c.SkipNewLines();
        var start = c.Previous;
        c.MatchWord("from");
        c.SkipNewLines();
        var from = Operand(ref c);
        c.SkipNewLines();
        c.Expect(To);
        c.SkipNewLines();
        var to = Operand(ref c);
        return Set(new GesRandomExpressionNode(from, to), start, c.Previous);
    }

    private GesExpressionNode BindSeededRandom(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        c.Expect(KeywordRandom);
        c.SkipNewLines();
        var start = c.Expect(With);
        c.SkipNewLines();
        var seed = Expression(ref c);
        c.SkipNewLines();
        var body = Expression(ref c);
        return Set(new GesSeededRandomExpressionNode(seed, body), start, c.Previous);
    }

    private GesExpressionNode BindDice(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        c.Expect(Roll);
        c.SkipNewLines();
        c.ExpectWord("dice");
        var start = c.Previous;
        var diceCount = PositiveInteger(c.Expect(GesTokenKind.Float).FloatValue);
        double sides;
        if (c.MatchWord("d")) sides = c.Expect(GesTokenKind.Float).FloatValue;
        else
        {
            // The rest of a compact dice token such as `d6` is its side count.
            var compact = c.Expect(Identifier);
            sides = (compact with { Kind = GesTokenKind.Float, Text = compact.Text[1..] }).FloatValue;
        }

        return Set(new GesDiceExpressionNode(diceCount, PositiveInteger(sides)), start, c.Previous);
    }

    private static int PositiveInteger(double value) => (int)value;

    private GesExpressionNode BindCollectionFactory(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        c.Expect(TypeName);
        c.Expect(LeftBracket);
        c.SkipNewLines();
        c.Expect(SelectorSelect);
        var start = c.Previous;
        c.SkipNewLines();
        var identifier = c.Expect(Identifier).Text;
        c.SkipNewLines();
        GesIterationSourceNode source;
        if (c.IsNode(NodeKind.RangeExpression)) source = BindRangeSource(c.TakeNode());
        else
        {
            c.Expect(In);
            c.SkipNewLines();
            var sourceStart = c.Current;
            source = Set(new GesCollectionIterationSourceNode(Expression(ref c)), sourceStart, c.Previous);
        }

        GesExpressionNode? predicate = null;
        c.SkipNewLines();
        if (c.MatchWord("where"))
        {
            c.SkipNewLines();
            predicate = Expression(ref c);
        }

        c.SkipNewLines();
        c.Expect(ProjectionArrow);
        c.SkipNewLines();
        var projection = Expression(ref c);
        var expression = Set(new GesGeneratedCollectionExpressionNode("list", identifier, source, predicate, projection), start, c.Previous);
        c.SkipNewLines();
        c.Expect(RightBracket);
        return expression;
    }

    private GesExpressionNode BindListLiteral(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        var bracket = c.Expect(LeftBracket);
        c.SkipNewLines();
        if (c.Match(RightBracket)) return Set(new GesListLiteralExpressionNode(Array.Empty<GesExpressionNode>()), bracket, c.Previous);

        // The range of a list starts at the last token before its first item, as in the parser.
        var start = c.Previous;
        var items = new List<GesExpressionNode> { Expression(ref c) };
        while (c.Match(Comma))
        {
            c.SkipNewLines();
            items.Add(Expression(ref c));
        }

        c.SkipNewLines();
        c.Expect(RightBracket);
        return Set(new GesListLiteralExpressionNode(items), start, c.Previous);
    }

    private GesExpressionNode BindMapLiteral(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        var bracket = c.Expect(LeftBracket);
        c.SkipNewLines();
        if (c.Match(Colon))
        {
            c.SkipNewLines();
            c.Expect(RightBracket);
            return Set(new GesMapLiteralExpressionNode(Array.Empty<GesMapEntryNode>()), bracket, c.Previous);
        }

        var start = c.Previous;
        var entries = new List<GesMapEntryNode> { BindMapEntry(c.TakeNode(NodeKind.MapEntry)) };
        while (c.Match(Comma))
        {
            c.SkipNewLines();
            entries.Add(BindMapEntry(c.TakeNode(NodeKind.MapEntry)));
        }

        c.SkipNewLines();
        c.Expect(RightBracket);
        return Set(new GesMapLiteralExpressionNode(entries), start, c.Previous);
    }

    private GesMapEntryNode BindMapEntry(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        var start = c.Current;
        var key = c.Expect(Identifier).Text;
        c.Expect(Colon);
        c.SkipNewLines();
        // A key without a value, such as in `[a:, b:]`, is true.
        var value = c.AtEnd ? Set(new GesBooleanLiteralExpressionNode(true), start, c.Previous) : Expression(ref c);
        return Set(new GesMapEntryNode(key, value), start, c.Previous);
    }

    private GesMessageLiteralExpressionNode BindMessageLiteral(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        var start = c.Expect(Message);
        var arguments = c.Match(LeftParen) ? ArgumentsAfterLeftParen(ref c) : GesArgumentListNode.Empty;
        return Set(new GesMessageLiteralExpressionNode(start.Text, arguments), start, c.Previous);
    }

    private GesExpressionNode BindCall(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        var start = c.Expect(Identifier);
        c.Expect(LeftParen);
        c.SkipNewLines();
        if (c.Match(RightParen)) return Set(new GesCallExpressionNode(start.Text, GesArgumentListNode.Empty), start, c.Previous);

        var first = c.TakeNode(NodeKind.Argument);
        var probe = new Cursor(this, first);
        if (IsLabelStart(ref probe))
        {
            var labeled = ArgumentsAfterFirst(ref c, BindArgument(first));
            c.Expect(RightParen);
            return Set(new GesCallExpressionNode(start.Text, labeled), start, c.Previous);
        }

        // Unlabeled call arguments carry no source range.
        var arguments = new List<GesArgumentNode> { BindUnrangedArgument(first) };
        while (c.Match(Comma))
        {
            c.SkipNewLines();
            arguments.Add(BindUnrangedArgument(c.TakeNode(NodeKind.Argument)));
        }

        c.SkipNewLines();
        c.Expect(RightParen);
        return Set(new GesCallExpressionNode(start.Text, new GesArgumentListNode(arguments)), start, c.Previous);
    }

    private GesArgumentNode BindUnrangedArgument(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        return new GesArgumentNode(null, Expression(ref c));
    }

    private GesExpressionNode BindHandlerLiteral(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        var start = c.Expect(Message);
        c.Expect(LeftParen);
        c.SkipNewLines();
        if (c.Match(RightParen)) return Set(new GesHandlerLiteralExpressionNode(start.Text, Array.Empty<GesParameterNode>()), start, c.Previous);

        var parameters = new List<GesParameterNode> { BindParameter(c.TakeNode(NodeKind.Parameter), allowDeclaredType: false) };
        while (c.Match(Comma))
        {
            c.SkipNewLines();
            parameters.Add(BindParameter(c.TakeNode(NodeKind.Parameter), allowDeclaredType: false));
        }

        c.SkipNewLines();
        c.Expect(RightParen);
        return Set(new GesHandlerLiteralExpressionNode(start.Text, parameters), start, c.Previous);
    }

    // Parentheses leave no semantic node; they count toward the source nesting depth.
    private GesExpressionNode BindParenthesized(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        c.Expect(LeftParen);
        c.SkipNewLines();
        var expression = Expression(ref c);
        c.SkipNewLines();
        c.Expect(RightParen);
        var nested = expression with { SourceNesting = expression.SourceNesting + 1 };
        return Set(nested, nested.SourceRange);
    }

    // A send statement owns the emit or publish keyword of its delayed send; a send expression starts with it.
    private GesExpressionNode BindSend(GesSyntaxNode node, GesPublishStatementKind? statementKind, GesToken statementStart)
    {
        var c = new Cursor(this, node);
        var start = statementKind is null ? c.Advance() : statementStart;
        var kind = statementKind ?? (start.Kind == Emit ? GesPublishStatementKind.Emit : GesPublishStatementKind.Publish);
        c.SkipNewLines();
        GesExpressionNode? delay = null;
        if (c.MatchWord("after")) delay = Expression(ref c);
        var message = Operand(ref c);
        var tags = OptionalTags(ref c);
        return Set(new GesSendExpressionNode(kind, message, tags, delay), start, c.Previous);
    }
}
