// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace StepH.GameEventScript.Compiler;

internal static class GesSourceNesting
{
    internal const int MaximumExpressionDepth = 32;
    internal const int MaximumStatementDepth = 32;

    // Children have already been checked and annotated. This calculation never
    // walks an expression recursively, including for left-associative chains.
    internal static int Measure(ScriptNode node)
    {
        var children = node switch
        {
            UnaryExpressionNode value => Depth(value.Operand),
            BinaryExpressionNode value => Math.Max(Depth(value.Left), Depth(value.Right)),
            MessageLiteralExpressionNode value => Arguments(value.ArgumentList),
            CallExpressionNode value => Arguments(value.ArgumentList),
            ExtensionCallExpressionNode value => Arguments(value.ArgumentList),
            TypeConstructorExpressionNode value => Arguments(value.ArgumentList),
            ListLiteralExpressionNode value => Maximum(value.Items),
            MapLiteralExpressionNode value => Maximum(value.Entries),
            MapEntryNode value => Depth(value.Value),
            IntrinsicCallExpressionNode value => Maximum(value.Arguments),
            VariadicTaggedExpressionNode value => Maximum(value.Arguments),
            ClampExpressionNode value => Math.Max(Depth(value.Value), Math.Max(Depth(value.Minimum), Depth(value.Maximum))),
            RangeExpressionNode value => Math.Max(Depth(value.FromExpression), Math.Max(Depth(value.ToExpression), Depth(value.StepExpression))),
            RandomExpressionNode value => Math.Max(Depth(value.FromExpression), Depth(value.ToExpression)),
            SeededRandomExpressionNode value => Math.Max(Depth(value.SeedExpression), Depth(value.BodyExpression)),
            GeneratedCollectionExpressionNode value => Math.Max(Depth(value.Source), Math.Max(Depth(value.Predicate), Depth(value.Projection))),
            GuardedChoiceExpressionNode value => Math.Max(Branches(value.Branches), Depth(value.OtherwiseExpression)),
            PredicateCallExpressionNode value => Depth(value.Value),
            ExtensionPredicateExpressionNode value => Depth(value.Value),
            TypeCheckExpressionNode value => Depth(value.Value),
            NothingCheckExpressionNode value => Depth(value.Value),
            TypeCastExpressionNode value => Depth(value.Value),
            MemberAccessExpressionNode value => Depth(value.Target),
            CollectionAccessExpressionNode value => Math.Max(Depth(value.Target), Depth(value.Selector)),
            ExpressionSelectorNode value => Depth(value.Expression),
            PatternSelectorNode value => Depth(value.Pattern),
            ObjectMatchSelectorNode value => Depth(value.Pattern),
            TakePatternSelectorNode value => Depth(value.Pattern),
            SeriesTermSelectorNode value => Depth(value.IndexExpression),
            PredicateSelectorNode value => Depth(value.Predicate),
            CountSelectorNode value => Depth(value.Predicate),
            ChooseSelectorNode value => Math.Max(Depth(value.Predicate), Depth(value.WeightExpression)),
            EdgeSelectorNode value => Depth(value.Predicate),
            FilterSelectorNode value => Depth(value.Predicate),
            SumSelectorNode value => Depth(value.Projection),
            AverageSelectorNode value => Depth(value.Projection),
            SelectSelectorNode value => Depth(value.Projection),
            MapSelectorNode value => Math.Max(Depth(value.KeyProjection), Depth(value.ValueProjection)),
            MinSelectorNode value => Depth(value.Projection),
            MaxSelectorNode value => Depth(value.Projection),
            ContainsSelectorNode value => Depth(value.ValueExpression),
            DistinctSelectorNode value => Depth(value.Projection),
            GroupBySelectorNode value => Depth(value.Projection),
            OrderBySelectorNode value => Depth(value.Projection),
            DiceCountPatternNode value => Depth(value.Face),
            ObjectMatchPatternNode value => Maximum(value.Entries),
            ObjectMatchEntryNode { Value: ObjectMatchExpressionValueNode value } => Depth(value.Expression),
            ObjectMatchEntryNode { Value: ObjectMatchNestedValueNode value } => Depth(value.Pattern),
            CollectionIterationSourceNode value => Depth(value.Expression),
            RangeIterationSourceNode value => Depth(value.RangeExpression),
            _ => 0
        };
        return Math.Max(node.SourceNesting, children + (node is ExpressionNode or ObjectMatchPatternNode ? 1 : 0));
    }

    private static int Depth(ScriptNode? node) => node?.SourceNesting ?? 0;

    private static int Maximum<T>(IReadOnlyList<T> nodes) where T : ScriptNode
    {
        var depth = 0;
        for (var index = 0; index < nodes.Count; index++) depth = Math.Max(depth, Depth(nodes[index]));
        return depth;
    }

    private static int Arguments(ArgumentListNode arguments)
    {
        var depth = 0;
        for (var index = 0; index < arguments.Count; index++) depth = Math.Max(depth, Depth(arguments.Arguments[index].Expression));
        return depth;
    }

    private static int Branches(IReadOnlyList<GuardedChoiceBranchNode> branches)
    {
        var depth = 0;
        for (var index = 0; index < branches.Count; index++)
            depth = Math.Max(depth, Math.Max(Depth(branches[index].ValueExpression), Depth(branches[index].ConditionExpression)));
        return depth;
    }
}
