// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace GameEventScript.Compiler;

internal static class GesSourceNesting
{
    internal const int MaximumExpressionDepth = 32;
    internal const int MaximumStatementDepth = 32;

    // Children have already been checked and annotated. This calculation never
    // walks an expression recursively, including for left-associative chains.
    internal static int Measure(GesScriptNode node)
    {
        var children = node switch
        {
            GesSendExpressionNode value => Math.Max(Depth(value.Message), Math.Max(Depth(value.Delay), Maximum(value.Tags))),
            GesUnaryExpressionNode value => Depth(value.Operand),
            GesBinaryExpressionNode value => Math.Max(Depth(value.Left), Depth(value.Right)),
            GesMessageLiteralExpressionNode value => Arguments(value.ArgumentList),
            GesCallExpressionNode value => Arguments(value.ArgumentList),
            GesExtensionCallExpressionNode value => Arguments(value.ArgumentList),
            GesTypeConstructorExpressionNode value => Arguments(value.ArgumentList),
            GesListLiteralExpressionNode value => Maximum(value.Items),
            GesCombinedCollectionExpressionNode value => Maximum(value.Sources),
            GesForeachSelectorNode value => Depth(value.Expression),
            GesMapLiteralExpressionNode value => Maximum(value.Entries),
            GesMapEntryNode value => Depth(value.Value),
            GesIntrinsicCallExpressionNode value => Maximum(value.Arguments),
            GesVariadicTaggedExpressionNode value => Maximum(value.Arguments),
            GesBetweenExpressionNode value => Math.Max(Depth(value.Value), Math.Max(Depth(value.Minimum), Depth(value.Maximum))),
            GesClampExpressionNode value => Math.Max(Depth(value.Value), Math.Max(Depth(value.Minimum), Depth(value.Maximum))),
            GesRangeExpressionNode value => Math.Max(Depth(value.FromExpression), Math.Max(Depth(value.ToExpression), Depth(value.StepExpression))),
            GesRandomExpressionNode value => Math.Max(Depth(value.FromExpression), Depth(value.ToExpression)),
            GesSeededRandomExpressionNode value => Math.Max(Depth(value.SeedExpression), Depth(value.BodyExpression)),
            GesGeneratedCollectionExpressionNode value => Math.Max(Depth(value.Source), Math.Max(Depth(value.Predicate), Depth(value.Projection))),
            GesGuardedChoiceExpressionNode value => Math.Max(Branches(value.Branches), Depth(value.OtherwiseExpression)),
            GesPredicateCallExpressionNode value => Depth(value.Value),
            GesExtensionPredicateExpressionNode value => Depth(value.Value),
            GesTypeCheckExpressionNode value => Depth(value.Value),
            GesNothingCheckExpressionNode value => Depth(value.Value),
            GesTypeCastExpressionNode value => Depth(value.Value),
            GesMemberAccessExpressionNode value => Depth(value.Target),
            GesCollectionAccessExpressionNode value => Math.Max(Depth(value.Target), Depth(value.Selector)),
            GesExpressionSelectorNode value => Depth(value.Expression),
            GesPatternSelectorNode value => Depth(value.Pattern),
            GesObjectMatchSelectorNode value => Depth(value.Pattern),
            GesTakePatternSelectorNode value => Depth(value.Pattern),
            GesSeriesTermSelectorNode value => Depth(value.IndexExpression),
            GesPredicateSelectorNode value => Depth(value.Predicate),
            GesCountSelectorNode value => Depth(value.Predicate),
            GesChooseSelectorNode value => Math.Max(Depth(value.Predicate), Depth(value.WeightExpression)),
            GesEdgeSelectorNode value => Depth(value.Predicate),
            GesFilterSelectorNode value => Depth(value.Predicate),
            GesSumSelectorNode value => Depth(value.Projection),
            GesAverageSelectorNode value => Depth(value.Projection),
            GesFoldSelectorNode value => Math.Max(Depth(value.Seed), Depth(value.Projection)),
            GesSelectSelectorNode value => Depth(value.Projection),
            GesMapSelectorNode value => Math.Max(Depth(value.KeyProjection), Depth(value.ValueProjection)),
            GesMinSelectorNode value => Depth(value.Projection),
            GesMaxSelectorNode value => Depth(value.Projection),
            GesContainsSelectorNode value => Depth(value.ValueExpression),
            GesDistinctSelectorNode value => Depth(value.Projection),
            GesGroupBySelectorNode value => Depth(value.Projection),
            GesOrderBySelectorNode value => Depth(value.Projection),
            GesDiceCountPatternNode value => Depth(value.Face),
            GesObjectMatchPatternNode value => Maximum(value.Entries),
            GesObjectMatchEntryNode { Value: GesObjectMatchExpressionValueNode value } => Depth(value.Expression),
            GesObjectMatchEntryNode { Value: GesObjectMatchNestedValueNode value } => Depth(value.Pattern),
            GesCollectionIterationSourceNode value => Depth(value.Expression),
            GesRangeIterationSourceNode value => Depth(value.RangeExpression),
            _ => 0
        };
        return Math.Max(node.SourceNesting, children + (node is GesExpressionNode or GesObjectMatchPatternNode ? 1 : 0));
    }

    private static int Depth(GesScriptNode? node) => node?.SourceNesting ?? 0;

    private static int Maximum<T>(IReadOnlyList<T> nodes) where T : GesScriptNode
    {
        var depth = 0;
        for (var index = 0; index < nodes.Count; index++) depth = Math.Max(depth, Depth(nodes[index]));
        return depth;
    }

    private static int Arguments(GesArgumentListNode arguments)
    {
        var depth = 0;
        for (var index = 0; index < arguments.Count; index++) depth = Math.Max(depth, Depth(arguments.Arguments[index].Expression));
        return depth;
    }

    private static int Branches(IReadOnlyList<GesGuardedChoiceBranchNode> branches)
    {
        var depth = 0;
        for (var index = 0; index < branches.Count; index++)
            depth = Math.Max(depth, Math.Max(Depth(branches[index].ValueExpression), Depth(branches[index].ConditionExpression)));
        return depth;
    }
}
