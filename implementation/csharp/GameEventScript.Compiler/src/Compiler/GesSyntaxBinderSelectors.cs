// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using GameEventScript.Syntax;
using static GameEventScript.Syntax.GesTokenKind;
using NodeKind = GameEventScript.Syntax.GesSyntaxNodeKind;

namespace GameEventScript.Compiler;

internal sealed partial class GesSyntaxBinder
{
    private static bool IsCombinedSelector(ref Cursor c)
        => c.IsOwnToken && c.Current.Text is ":cartesian" or ":lockstep" or ":zip" or ":union" or ":intersect" or ":difference";

    // Each pipeline step is one selector node that extends the expression of the steps before it.
    private GesExpressionNode PipelineSteps(ref Cursor c, GesExpressionNode? source)
    {
        do
        {
            var s = new Cursor(this, c.TakeNode(NodeKind.CollectionSelector));
            var start = s.Current;
            if (s.MatchSelector(":split"))
            {
                s.Expect(On);
                var whitespace = s.MatchWord("whitespace");
                GesExpressionNode delimiter = whitespace ? new GesNothingLiteralExpressionNode() : Expression(ref s);
                var arguments = new GesArgumentListNode(whitespace ? [new GesArgumentNode(null, source!)] : [new GesArgumentNode(null, source!), new GesArgumentNode(null, delimiter)]);
                source = Merge(new GesTypeConstructorExpressionNode(whitespace ? "__splitWhitespace" : "__split", arguments), source);
            }
            else if (IsCombinedSelector(ref s))
            {
                var operation = s.Advance().Text[1..];
                var sources = new List<GesExpressionNode>();
                if (source is not null) sources.Add(source);
                s.SkipNewLines();
                sources.Add(Expression(ref s));
                while (s.Match(Comma))
                {
                    s.SkipNewLines();
                    sources.Add(Expression(ref s));
                }

                source = Set(new GesCombinedCollectionExpressionNode(operation == "zip" ? "lockstep" : operation, sources), start, s.Previous);
            }
            else
            {
                var selector = BindSelector(ref s);
                source = Merge(new GesCollectionAccessExpressionNode(source!, selector), source, selector);
            }

            c.SkipNewLines();
        }
        while (c.IsNode(NodeKind.CollectionSelector));

        return source!;
    }

    private GesCollectionSelectorNode BindSelector(ref Cursor c)
    {
        var start = c.Current;
        if (c.MatchSelector(":foreach"))
        {
            var (identifier, names, projection) = BoundProjection(ref c);
            return Set(new GesForeachSelectorNode(identifier, projection) { BindingNames = names }, start, c.Previous);
        }

        if (c.Is(SelectorAny) || c.Is(SelectorAll))
        {
            var op = c.Advance().Kind == SelectorAny ? "any" : "all";
            var (identifier, names, predicate) = BoundWhere(ref c);
            return Set(new GesPredicateSelectorNode(op, identifier, predicate) { BindingNames = names }, start, c.Previous);
        }

        if (c.Match(SelectorHas))
        {
            c.SkipNewLines();
            if (c.IsNode(NodeKind.ObjectPattern)) return Set(new GesObjectMatchSelectorNode(BindObjectPattern(c.TakeNode())), null);
            return Set(new GesPatternSelectorNode(BindDicePattern(c.TakeNode(NodeKind.DicePattern))), start, c.Previous);
        }

        if (c.Match(SelectorTake))
        {
            c.SkipNewLines();
            if (SliceScope(ref c) is { } scope)
            {
                c.SkipNewLines();
                return Set(new GesSequenceSliceSelectorNode("take", scope, PositiveInteger(c.Expect(GesTokenKind.Float).FloatValue)), start, c.Previous);
            }

            return Set(new GesTakePatternSelectorNode(BindDicePattern(c.TakeNode(NodeKind.DicePattern))), start, c.Previous);
        }

        if (c.Match(SelectorDrop))
        {
            c.SkipNewLines();
            var scope = SliceScope(ref c)!;
            c.SkipNewLines();
            return Set(new GesSequenceSliceSelectorNode("drop", scope, PositiveInteger(c.Expect(GesTokenKind.Float).FloatValue)), start, c.Previous);
        }

        if (c.MatchSelector(":term"))
        {
            c.SkipNewLines();
            return Set(new GesSeriesTermSelectorNode(Expression(ref c)), start, c.Previous);
        }

        if (c.Match(SelectorCount))
        {
            c.SkipNewLines();
            if (c.AtEnd) return Set(new GesCountSelectorNode("value", new GesBooleanLiteralExpressionNode(true)), start, c.Previous);
            var (identifier, names, predicate) = BoundWhere(ref c);
            return Set(new GesCountSelectorNode(identifier, predicate) { BindingNames = names }, start, c.Previous);
        }

        if (c.Match(SelectorChoose)) return BindChoose(ref c, start);

        if (c.Match(SelectorDraw))
        {
            c.SkipNewLines();
            return Set(new GesDrawSelectorNode(PositiveInteger(c.Expect(GesTokenKind.Float).FloatValue)), start, c.Previous);
        }

        if (c.Match(SelectorShuffle)) return Set(new GesShuffleSelectorNode(), start, c.Previous);
        if (c.Match(SelectorReverse)) return Set(new GesReverseSelectorNode(), start, c.Previous);

        if (c.IsSelector(":first") || c.IsSelector(":last") || c.IsSelector(":single"))
        {
            var mode = c.Advance().Text[1..];
            c.SkipNewLines();
            if (!c.Is(Identifier)) return Set(new GesEdgeSelectorNode(mode, null, null), start, c.Previous);
            var (identifier, names, predicate) = BoundWhere(ref c);
            return Set(new GesEdgeSelectorNode(mode, identifier, predicate) { BindingNames = names }, start, c.Previous);
        }

        if (c.Match(SelectorFilter))
        {
            var (identifier, names, predicate) = BoundWhere(ref c);
            return Set(new GesFilterSelectorNode(identifier, predicate) { BindingNames = names }, start, c.Previous);
        }

        if (c.Match(SelectorSum))
        {
            c.SkipNewLines();
            if (c.AtEnd) return Set(new GesSumSelectorNode("value", new GesIdentifierExpressionNode("value")), start, c.Previous);
            var (identifier, names, projection) = BoundProjection(ref c);
            return Set(new GesSumSelectorNode(identifier, projection) { BindingNames = names }, start, c.Previous);
        }

        if (c.Match(SelectorAverage))
        {
            c.SkipNewLines();
            if (c.AtEnd) return Set(new GesAverageSelectorNode("value", new GesIdentifierExpressionNode("value")), start, c.Previous);
            var (identifier, names, projection) = BoundProjection(ref c);
            return Set(new GesAverageSelectorNode(identifier, projection) { BindingNames = names }, start, c.Previous);
        }

        if (c.IsSelector(":min") || c.IsSelector(":max") || c.IsSelector(":highest") || c.IsSelector(":lowest"))
        {
            var op = c.Advance().Text;
            c.SkipNewLines();
            var (identifier, names, projection) = BoundProjection(ref c);
            return op is ":min" or ":lowest"
                ? Set(new GesMinSelectorNode(identifier, projection) { BindingNames = names }, start, c.Previous)
                : Set(new GesMaxSelectorNode(identifier, projection) { BindingNames = names }, start, c.Previous);
        }

        if (c.IsSelector(":fold") || c.IsSelector(":reduce"))
        {
            var reduce = c.Advance().Text == ":reduce";
            var accumulator = c.Expect(Identifier).Text;
            GesExpressionNode? seed = null;
            if (!reduce)
            {
                c.Expect(Be);
                seed = Expression(ref c);
            }

            c.Expect(Comma);
            c.SkipNewLines();
            var (identifier, names) = BindingNames(ref c);
            c.Expect(ProjectionArrow);
            c.SkipNewLines();
            return Set(new GesFoldSelectorNode(accumulator, identifier, seed, Expression(ref c)) { BindingNames = names }, start, c.Previous);
        }

        if (c.Match(SelectorSelect))
        {
            var (identifier, names, projection) = BoundProjection(ref c);
            return Set(new GesSelectSelectorNode(identifier, projection) { BindingNames = names }, start, c.Previous);
        }

        if (c.MatchSelector(":map"))
        {
            c.SkipNewLines();
            var (identifier, names) = BindingNames(ref c);
            c.SkipNewLines();
            c.ExpectWord("by");
            c.SkipNewLines();
            var key = Expression(ref c);
            c.SkipNewLines();
            GesExpressionNode? value = null;
            if (c.Match(ProjectionArrow))
            {
                c.SkipNewLines();
                value = Expression(ref c);
            }

            return Set(new GesMapSelectorNode(identifier, key, value) { BindingNames = names }, start, c.Previous);
        }

        if (c.Match(SelectorContains))
        {
            c.SkipNewLines();
            var mode = c.MatchWord("all") ? "all" : c.MatchWord("any") ? "any" : "single";
            if (mode != "single") c.SkipNewLines();
            return Set(new GesContainsSelectorNode(mode, Expression(ref c)), start, c.Previous);
        }

        if (c.MatchSelector(":distinct"))
        {
            c.SkipNewLines();
            if (!c.MatchWord("by")) return Set(new GesDistinctSelectorNode(null, null), start, c.Previous);
            c.SkipNewLines();
            var (identifier, names, projection) = BoundProjection(ref c);
            return Set(new GesDistinctSelectorNode(identifier, projection) { BindingNames = names }, start, c.Previous);
        }

        if (c.MatchSelector(":group"))
        {
            c.SkipNewLines();
            c.ExpectWord("by");
            c.SkipNewLines();
            var (identifier, names, projection) = BoundProjection(ref c);
            return Set(new GesGroupBySelectorNode(identifier, projection) { BindingNames = names }, start, c.Previous);
        }

        if (c.Match(SelectorSort))
        {
            c.SkipNewLines();
            return Set(new GesSortSelectorNode(SortDirection(ref c)), start, c.Previous);
        }

        if (c.MatchSelector(":order"))
        {
            c.SkipNewLines();
            c.ExpectWord("by");
            c.SkipNewLines();
            var (identifier, names, projection) = BoundProjection(ref c);
            var direction = SortDirection(ref c);
            return Set(new GesOrderBySelectorNode(direction, identifier, projection) { BindingNames = names }, start, c.Previous);
        }

        if (c.IsSelector(":keys") || c.IsSelector(":values") || c.IsSelector(":entries"))
        {
            var selectorToken = c.Advance();
            var tag = Set(new GesTagLiteralExpressionNode(selectorToken.Text[1..]), Range(selectorToken));
            return Set(new GesExpressionSelectorNode(tag), start, c.Previous);
        }

        return Set(new GesExpressionSelectorNode(Expression(ref c)), start, c.Previous);
    }

    private static string SortDirection(ref Cursor c)
    {
        c.SkipNewLines();
        if (c.MatchWord("ascending")) return "ascending";
        c.ExpectWord("descending");
        return "descending";
    }

    private static string? SliceScope(ref Cursor c)
    {
        foreach (var scope in (string[])["first", "last", "highest", "lowest"])
            if (c.MatchWord(scope)) return scope;
        return null;
    }

    private (string Identifier, IReadOnlyList<string> Names, GesExpressionNode Predicate) BoundWhere(ref Cursor c)
    {
        var (identifier, names) = BindingNames(ref c);
        c.ExpectWord("where");
        return (identifier, names, Expression(ref c));
    }

    private (string Identifier, IReadOnlyList<string> Names, GesExpressionNode Projection) BoundProjection(ref Cursor c)
    {
        var (identifier, names) = BindingNames(ref c);
        c.Expect(ProjectionArrow);
        return (identifier, names, Expression(ref c));
    }

    private GesCollectionSelectorNode BindChoose(ref Cursor c, GesToken start)
    {
        c.SkipNewLines();
        var count = PositiveInteger(c.Expect(GesTokenKind.Float).FloatValue);
        c.SkipNewLines();
        var atRandom = false;
        if (c.MatchWord("at"))
        {
            c.SkipNewLines();
            c.Expect(KeywordRandom);
            atRandom = true;
            c.SkipNewLines();
        }

        string? identifier = null;
        GesExpressionNode? predicate = null;
        IReadOnlyList<string> names = [];
        if (c.Is(Identifier))
        {
            var lookahead = c.PeekSignificant(1);
            if (lookahead.Kind == Comma || lookahead.Kind == Identifier && lookahead.Text == "where")
            {
                (identifier, names) = BindingNames(ref c);
                c.ExpectWord("where");
                predicate = Expression(ref c);
            }
        }

        string? weightIdentifier = null;
        IReadOnlyList<string> weightNames = [];
        GesExpressionNode? weight = null;
        c.SkipNewLines();
        if (c.MatchWord("weighted"))
        {
            c.SkipNewLines();
            c.ExpectWord("by");
            c.SkipNewLines();
            (weightIdentifier, weightNames) = BindingNames(ref c);
            c.SkipNewLines();
            c.Expect(ProjectionArrow);
            c.SkipNewLines();
            weight = Expression(ref c);
        }

        return Set(new GesChooseSelectorNode(count, atRandom, identifier, predicate, weightIdentifier, weight) { BindingNames = names, WeightBindingNames = weightNames }, start, c.Previous);
    }

    private GesObjectMatchPatternNode BindObjectPattern(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        var start = c.Expect(LeftBracket);
        c.SkipNewLines();
        var entries = new List<GesObjectMatchEntryNode>();
        if (c.IsNode(NodeKind.ObjectPatternEntry))
        {
            entries.Add(BindObjectPatternEntry(c.TakeNode()));
            while (c.Match(Comma))
            {
                c.SkipNewLines();
                entries.Add(BindObjectPatternEntry(c.TakeNode(NodeKind.ObjectPatternEntry)));
            }
        }

        c.SkipNewLines();
        c.Expect(RightBracket);
        return Set(new GesObjectMatchPatternNode(entries), start, c.Previous);
    }

    private GesObjectMatchEntryNode BindObjectPatternEntry(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        var start = c.Current;
        var key = c.Expect(Identifier).Text;
        c.Expect(Colon);
        c.SkipNewLines();
        GesObjectMatchValueNode value = c.IsNode(NodeKind.ObjectPattern)
            ? new GesObjectMatchNestedValueNode(BindObjectPattern(c.TakeNode()))
            : new GesObjectMatchExpressionValueNode(Expression(ref c));
        return Set(new GesObjectMatchEntryNode(key, value), start, c.Previous);
    }

    private GesDicePatternNode BindDicePattern(GesSyntaxNode node)
    {
        var c = new Cursor(this, node);
        var start = c.Advance();
        switch (start.Text)
        {
            case "pair":
                c.SkipNewLines();
                if (!c.Match(Of)) return Set(new GesDiceCountPatternNode(2, null), start, c.Previous);
                c.SkipNewLines();
                return Set(new GesDiceCountPatternNode(2, Operand(ref c)), start, c.Previous);
            case "full":
                c.SkipNewLines();
                c.ExpectWord("house");
                return Set(new GesDiceFullHousePatternNode(), start, c.Previous);
            case "straight":
                return Set(new GesDiceStraightPatternNode(), start, c.Previous);
        }

        var count = start.Text switch { "three" => 3, "four" => 4, "five" => 5, "six" => 6, _ => 7 };
        c.SkipNewLines();
        c.Expect(Of);
        c.SkipNewLines();
        if (c.MatchWord("a"))
        {
            c.SkipNewLines();
            c.ExpectWord("kind");
            return Set(new GesDiceCountPatternNode(count, null), start, c.Previous);
        }

        return Set(new GesDiceCountPatternNode(count, Operand(ref c)), start, c.Previous);
    }

    private GesAssemblyBlockNode BindAssemblyBlock(ref Cursor c, string? output)
    {
        var start = c.Previous;
        c.SkipNewLines();
        c.Expect(LeftBrace);
        c.SkipSeparators();
        var declarations = new List<GesAssemblyDeclaration>();
        var lines = new List<GesAssemblyLine>();
        while (!c.Is(RightBrace))
        {
            var token = c.Current;
            var exported = c.Match(Let);
            if (exported || c.Match(Dot))
            {
                if (!exported) c.ExpectWord("register");
                do declarations.Add(Set(new GesAssemblyDeclaration(c.Expect(Identifier).Text, exported), token, c.Previous)); while (c.Match(Comma));
            }
            else if (c.Is(Emit) || c.Is(Publish))
            {
                var name = c.Advance().Kind == Emit ? "emit" : "publish";
                var operands = new List<GesExpressionNode> { NormalizeAssemblyOperand(Expression(ref c)) };
                operands.AddRange(OptionalTags(ref c).Select(NormalizeAssemblyOperand));
                lines.Add(Set(new GesAssemblyLine(name, false, operands), token, c.Previous));
            }
            else if (c.Is(Identifier))
            {
                var name = c.Advance().Text;
                c.Expect(Colon);
                lines.Add(Set(new GesAssemblyLine(name, true, []), token, c.Previous));
            }
            else
            {
                var name = c.Expect(Message).Text;
                var operands = new List<GesExpressionNode>();
                if (!c.Is(NewLine) && !c.Is(Semicolon) && !c.Is(RightBrace))
                {
                    do operands.Add(BindAssemblyOperand(ref c)); while (c.Match(Comma));
                }

                lines.Add(Set(new GesAssemblyLine(name, false, operands), token, c.Previous));
            }

            c.SkipSeparators();
        }

        c.Expect(RightBrace);
        return Set(new GesAssemblyBlockNode(output, declarations, lines, AllowSend: output is null), start, c.Previous);
    }

    private GesExpressionNode BindAssemblyOperand(ref Cursor c)
    {
        // A minus of its own token is the Int64 minimum, which has no positive literal.
        if (c.Is(OperatorMinus))
        {
            var start = c.Advance();
            c.Advance();
            return Set(new GesIntegerLiteralExpressionNode(long.MinValue), start, c.Previous);
        }

        if (c.Is(TypeName))
        {
            var token = c.Advance();
            return Set(new GesTextLiteralExpressionNode(token.Text.TrimStart(':')), Range(token));
        }

        return NormalizeAssemblyOperand(Expression(ref c));
    }

    private GesExpressionNode NormalizeAssemblyOperand(GesExpressionNode operand)
    {
        GesArgumentListNode Arguments(GesArgumentListNode arguments)
            => Merge(new GesArgumentListNode(arguments.Arguments.Select(argument => argument with { Expression = NormalizeAssemblyOperand(argument.Expression) }).ToArray()), arguments);

        return operand switch
        {
            GesUnaryExpressionNode { Operator: GesUnaryOperator.Negate, Operand: GesIntegerLiteralExpressionNode integer } when integer.Value != long.MinValue => Merge(new GesIntegerLiteralExpressionNode(-integer.Value), operand),
            GesUnaryExpressionNode { Operator: GesUnaryOperator.Negate, Operand: GesFloatLiteralExpressionNode number } => Merge(new GesFloatLiteralExpressionNode(-number.Value), operand),
            GesUnaryExpressionNode { Operator: GesUnaryOperator.Negate, Operand: GesUnitIntegerLiteralExpressionNode integer } when integer.Value != long.MinValue
                => Merge(new GesUnitIntegerLiteralExpressionNode(-integer.Value, integer.UnitName), operand),
            GesUnaryExpressionNode { Operator: GesUnaryOperator.Negate, Operand: GesUnitFloatLiteralExpressionNode number }
                => Merge(new GesUnitFloatLiteralExpressionNode(-number.Value, number.UnitName), operand),
            GesUnaryExpressionNode { Operator: GesUnaryOperator.Negate, Operand: GesPercentageLiteralExpressionNode percentage }
                => Merge(new GesPercentageLiteralExpressionNode(-percentage.RatioValue), operand),
            GesCallExpressionNode call => Merge(new GesCallExpressionNode(call.Name, Arguments(call.ArgumentList)), operand),
            GesExtensionCallExpressionNode call => Merge(new GesExtensionCallExpressionNode(call.ExtensionName, call.FunctionName, Arguments(call.ArgumentList)), operand),
            GesTypeConstructorExpressionNode constructor => Merge(new GesTypeConstructorExpressionNode(constructor.TypeName, Arguments(constructor.ArgumentList)), operand),
            GesMessageLiteralExpressionNode message => Merge(new GesMessageLiteralExpressionNode(message.Message, Arguments(message.ArgumentList)), operand),
            GesListLiteralExpressionNode list => Merge(new GesListLiteralExpressionNode(list.Items.Select(NormalizeAssemblyOperand).ToArray()), operand),
            GesMapLiteralExpressionNode map => Merge(new GesMapLiteralExpressionNode(map.Entries.Select(entry => entry with { Value = NormalizeAssemblyOperand(entry.Value) }).ToArray()), operand),
            _ => operand
        };
    }
}
