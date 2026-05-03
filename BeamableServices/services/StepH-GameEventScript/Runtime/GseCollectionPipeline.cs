#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Compiler;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.Runtime;

internal sealed class GseCollectionPipeline
{
    private GseCollectionPipeline(ExpressionNode sourceExpression, IReadOnlyList<CollectionSelectorNode> prefixSelectors, CollectionSelectorNode terminalSelector)
    {
        SourceExpression = sourceExpression;
        PrefixSelectors = prefixSelectors;
        TerminalSelector = terminalSelector;
    }

    public ExpressionNode SourceExpression { get; }

    public IReadOnlyList<CollectionSelectorNode> PrefixSelectors { get; }

    public CollectionSelectorNode TerminalSelector { get; }

    public static bool TryCreate(CollectionAccessExpressionNode expression, out GseCollectionPipeline pipeline)
    {
        var selectors = new List<CollectionSelectorNode>();
        ExpressionNode source = expression;
        while (source is CollectionAccessExpressionNode collectionAccess)
        {
            selectors.Add(collectionAccess.Selector);
            source = collectionAccess.Target;
        }

        selectors.Reverse();
        if (selectors.Count < 2)
        {
            pipeline = null!;
            return false;
        }

        var prefixSelectors = selectors.Take(selectors.Count - 1).ToArray();
        var terminalSelector = selectors[^1];
        if (prefixSelectors.Length == 0 ||
            prefixSelectors.Any(selector => !IsComposableStreamingSelector(selector)) ||
            !IsSupportedTerminalSelector(terminalSelector))
        {
            pipeline = null!;
            return false;
        }

        pipeline = new GseCollectionPipeline(source, prefixSelectors, terminalSelector);
        return true;
    }

    public IEnumerable<GseValue> ApplyPrefix(IEnumerable<GseValue> items, GseProjectionEvaluator projection)
    {
        var current = items ?? throw new ArgumentNullException(nameof(items));
        foreach (var selector in PrefixSelectors)
        {
            current = ApplyComposableSelector(current, selector, projection);
        }

        return current;
    }

    public static IEnumerable<GseValue> ApplyComposableSelector(
        IEnumerable<GseValue> items,
        CollectionSelectorNode selector,
        GseProjectionEvaluator projection)
    {
        return selector switch
        {
            FilterSelectorNode filter => Filter(items, projection, filter.Identifier, filter.Predicate),
            SelectSelectorNode select => Select(items, projection, select.Identifier, select.Projection),
            _ => items
        };
    }

    private static bool IsComposableStreamingSelector(CollectionSelectorNode selector)
        => selector is FilterSelectorNode or SelectSelectorNode;

    private static bool IsSupportedTerminalSelector(CollectionSelectorNode selector)
        => selector is
            PredicateSelectorNode or
            CountSelectorNode or
            EdgeSelectorNode or
            FilterSelectorNode or
            SumSelectorNode or
            AverageSelectorNode or
            SelectSelectorNode or
            DictionarySelectorNode or
            MinSelectorNode or
            MaxSelectorNode or
            SortSelectorNode or
            DistinctSelectorNode or
            GroupBySelectorNode or
            OrderBySelectorNode or
            ChooseSelectorNode or
            DrawSelectorNode or
            ShuffleSelectorNode or
            ReverseSelectorNode or
            SequenceSliceSelectorNode;

    private static IEnumerable<GseValue> Filter(
        IEnumerable<GseValue> items,
        GseProjectionEvaluator projection,
        string identifier,
        ExpressionNode predicate)
    {
        foreach (var item in items)
        {
            if (projection.EvaluateBoolean(identifier, predicate, item))
            {
                yield return item;
            }
        }
    }

    private static IEnumerable<GseValue> Select(
        IEnumerable<GseValue> items,
        GseProjectionEvaluator projection,
        string identifier,
        ExpressionNode projectionExpression)
    {
        foreach (var item in items)
        {
            yield return projection.Evaluate(identifier, projectionExpression, item);
        }
    }
}
