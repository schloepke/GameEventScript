#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.Flow.EventScript;
using StepH.Flow.EventScript.Linker;
using StepH.Flow.EventScript.Parser;
using StepH.Flow.EventScript.Types;

namespace StepH.Flow.EventScript.RegisterVM;

internal static class RegisterVmFastPathAnalyzer
{
    public static RegisterVmFastPathPlan CreateHandlerPlan(
        IReadOnlyList<string> parameters,
        IReadOnlyList<StatementNode> statements,
        IReadOnlyDictionary<string, LinkedCallableDefinition> callables,
        IReadOnlyDictionary<string, TypeDefinitionNode>? typeDefinitions = null)
    {
        if (!SupportsHandler(statements, callables, out var unsupportedReason))
        {
            return RegisterVmFastPathPlan.Unsupported(unsupportedReason);
        }

        var slotCollector = new SlotCollector(callables, typeDefinitions ?? new Dictionary<string, TypeDefinitionNode>(StringComparer.Ordinal));
        foreach (var parameter in parameters)
        {
            slotCollector.AddSlot(parameter);
        }

        slotCollector.CollectTypeDefinitions();

        foreach (var statement in statements)
        {
            slotCollector.CollectStatement(statement);
        }

        var programCompiler = new ProgramCompiler(slotCollector.Slots, callables);
        foreach (var statement in statements)
        {
            programCompiler.CompileStatementPrograms(statement);
        }

        return RegisterVmFastPathPlan.Create(
            slotCollector.Slots,
            programCompiler.ExpressionPrograms,
            programCompiler.PublishLayouts,
            programCompiler.MaxStackDepth);
    }

    public static bool SupportsHandler(
        IReadOnlyList<StatementNode> statements,
        IReadOnlyDictionary<string, LinkedCallableDefinition> callables)
        => SupportsHandler(statements, callables, out _);

    private static bool SupportsHandler(
        IReadOnlyList<StatementNode> statements,
        IReadOnlyDictionary<string, LinkedCallableDefinition> callables,
        out string unsupportedReason)
    {
        for (var statementIndex = 0; statementIndex < statements.Count; statementIndex++)
        {
            if (!SupportsStatement(statements[statementIndex], callables, out unsupportedReason))
            {
                unsupportedReason = $"Statement {statementIndex}: {unsupportedReason}";
                return false;
            }
        }

        unsupportedReason = string.Empty;
        return true;
    }

    private static bool SupportsStatement(
        StatementNode statement,
        IReadOnlyDictionary<string, LinkedCallableDefinition> callables,
        out string unsupportedReason)
    {
        switch (statement)
        {
            case LetStatementNode let:
                if (!string.IsNullOrEmpty(let.DeclaredType) &&
                    !SupportsDeclaredType(let.DeclaredType!))
                {
                    unsupportedReason = $"Typed let '{let.Identifier}' with type '{let.DeclaredType}' is not supported by the RegisterVM fast path yet.";
                    return false;
                }

                if (!SupportsExpression(let.Expression, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Let '{let.Identifier}' expression: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case PublishStatementNode publish:
                if (!SupportsExpression(publish.MessageExpression, callables, out unsupportedReason))
                {
                    unsupportedReason = publish.MessageExpression is MessageLiteralExpressionNode
                        ? $"Publish message expression: {unsupportedReason}"
                        : $"Publish expression: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case IfStatementNode ifStatement:
                if (!SupportsExpression(ifStatement.Condition, callables, out unsupportedReason))
                {
                    unsupportedReason = $"If condition: {unsupportedReason}";
                    return false;
                }

                if (!SupportsHandler(ifStatement.ThenBody.Statements, callables, out unsupportedReason))
                {
                    unsupportedReason = $"If then body: {unsupportedReason}";
                    return false;
                }

                if (ifStatement.ElseBody is not null &&
                    !SupportsHandler(ifStatement.ElseBody.Statements, callables, out unsupportedReason))
                {
                    unsupportedReason = $"If else body: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case ForStatementNode { Source: RangeIterationSourceNode range } forStatement:
                if (!SupportsRange(range.RangeExpression, callables, out unsupportedReason))
                {
                    unsupportedReason = $"For range: {unsupportedReason}";
                    return false;
                }

                if (!SupportsHandler(forStatement.Body.Statements, callables, out unsupportedReason))
                {
                    unsupportedReason = $"For body: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case ForStatementNode { Source: CollectionIterationSourceNode collection } forStatement:
                if (!SupportsExpression(collection.Expression, callables, out unsupportedReason))
                {
                    unsupportedReason = $"For collection source: {unsupportedReason}";
                    return false;
                }

                if (!SupportsHandler(forStatement.Body.Statements, callables, out unsupportedReason))
                {
                    unsupportedReason = $"For body: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case ForStatementNode forStatement:
                unsupportedReason = $"For source '{forStatement.Source.GetType().Name}' is not supported by the RegisterVM fast path.";
                return false;

            case ExpressionStatementNode expressionStatement:
                if (!SupportsExpression(expressionStatement.Expression, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Expression statement: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case SeededRandomStatementNode seededRandom:
                if (!SupportsExpression(seededRandom.SeedExpression, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Seeded random seed: {unsupportedReason}";
                    return false;
                }

                if (!SupportsHandler(seededRandom.Body.Statements, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Seeded random body: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            default:
                unsupportedReason = $"Statement '{statement.GetType().Name}' is not supported by the RegisterVM fast path.";
                return false;
        }
    }

    private static bool SupportsRange(
        RangeExpressionNode range,
        IReadOnlyDictionary<string, LinkedCallableDefinition> callables,
        out string unsupportedReason)
    {
        if (!SupportsExpression(range.FromExpression, callables, out unsupportedReason))
        {
            unsupportedReason = $"Range start: {unsupportedReason}";
            return false;
        }

        if (!SupportsExpression(range.ToExpression, callables, out unsupportedReason))
        {
            unsupportedReason = $"Range end: {unsupportedReason}";
            return false;
        }

        if (range.StepExpression is not null &&
            !SupportsExpression(range.StepExpression, callables, out unsupportedReason))
        {
            unsupportedReason = $"Range step: {unsupportedReason}";
            return false;
        }

        unsupportedReason = string.Empty;
        return true;
    }

    private static bool SupportsExpression(
        ExpressionNode expression,
        IReadOnlyDictionary<string, LinkedCallableDefinition> callables,
        out string unsupportedReason)
    {
        switch (expression)
        {
            case BooleanLiteralExpressionNode:
            case IntegerLiteralExpressionNode:
            case DecimalLiteralExpressionNode:
            case PercentageLiteralExpressionNode:
            case UnitDecimalLiteralExpressionNode:
            case TextLiteralExpressionNode:
            case TagLiteralExpressionNode:
            case IdentifierExpressionNode:
                unsupportedReason = string.Empty;
                return true;

            case MessageLiteralExpressionNode message:
                for (var argumentIndex = 0; argumentIndex < message.Arguments.Count; argumentIndex++)
                {
                    var argument = message.Arguments[argumentIndex];
                    if (!SupportsExpression(argument.Expression, callables, out unsupportedReason))
                    {
                        unsupportedReason = $"Message argument '{argument.Name}': {unsupportedReason}";
                        return false;
                    }
                }

                unsupportedReason = string.Empty;
                return true;

            case HandlerLiteralExpressionNode:
                unsupportedReason = string.Empty;
                return true;

            case HandlerBindExpressionNode handlerBind:
                if (!SupportsExpression(handlerBind.CalleeExpression, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Handler bind callee: {unsupportedReason}";
                    return false;
                }

                for (var argumentIndex = 0; argumentIndex < handlerBind.Arguments.Count; argumentIndex++)
                {
                    var argument = handlerBind.Arguments[argumentIndex];
                    if (!SupportsExpression(argument.Expression, callables, out unsupportedReason))
                    {
                        unsupportedReason = $"Handler bind argument '{argument.Name}': {unsupportedReason}";
                        return false;
                    }
                }

                unsupportedReason = string.Empty;
                return true;

            case ListLiteralExpressionNode list:
                for (var itemIndex = 0; itemIndex < list.Items.Count; itemIndex++)
                {
                    if (!SupportsExpression(list.Items[itemIndex], callables, out unsupportedReason))
                    {
                        unsupportedReason = $"List item {itemIndex}: {unsupportedReason}";
                        return false;
                    }
                }

                unsupportedReason = string.Empty;
                return true;

            case SetLiteralExpressionNode set:
                for (var itemIndex = 0; itemIndex < set.Items.Count; itemIndex++)
                {
                    if (!SupportsExpression(set.Items[itemIndex], callables, out unsupportedReason))
                    {
                        unsupportedReason = $"Set item {itemIndex}: {unsupportedReason}";
                        return false;
                    }
                }

                unsupportedReason = string.Empty;
                return true;

            case DictionaryLiteralExpressionNode dictionary:
                for (var entryIndex = 0; entryIndex < dictionary.Entries.Count; entryIndex++)
                {
                    var entry = dictionary.Entries[entryIndex];
                    if (!SupportsExpression(entry.Value, callables, out unsupportedReason))
                    {
                        unsupportedReason = $"Dictionary entry '{entry.Key}': {unsupportedReason}";
                        return false;
                    }
                }

                unsupportedReason = string.Empty;
                return true;

            case UnaryExpressionNode unary:
                if (!SupportsUnaryOperator(unary.Operator))
                {
                    unsupportedReason = $"Unary operator '{unary.Operator}' is not supported by the RegisterVM fast path.";
                    return false;
                }

                if (!SupportsExpression(unary.Operand, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Unary operand: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case VariadicTaggedExpressionNode variadic:
                if (!SupportsVariadicTaggedOperator(variadic.Operator))
                {
                    unsupportedReason = $"Variadic operator '{variadic.Operator}' is not supported by the RegisterVM fast path.";
                    return false;
                }

                for (var argumentIndex = 0; argumentIndex < variadic.Arguments.Count; argumentIndex++)
                {
                    if (!SupportsExpression(variadic.Arguments[argumentIndex], callables, out unsupportedReason))
                    {
                        unsupportedReason = $"Variadic argument {argumentIndex}: {unsupportedReason}";
                        return false;
                    }
                }

                unsupportedReason = string.Empty;
                return true;

            case ClampExpressionNode clamp:
                if (!SupportsExpression(clamp.Value, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Clamp value: {unsupportedReason}";
                    return false;
                }

                if (!SupportsExpression(clamp.Minimum, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Clamp minimum: {unsupportedReason}";
                    return false;
                }

                if (!SupportsExpression(clamp.Maximum, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Clamp maximum: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case RandomExpressionNode random:
                if (!SupportsExpression(random.FromExpression, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Random start: {unsupportedReason}";
                    return false;
                }

                if (!SupportsExpression(random.ToExpression, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Random end: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case RangeExpressionNode range:
                return SupportsRange(range, callables, out unsupportedReason);

            case DiceExpressionNode:
                unsupportedReason = string.Empty;
                return true;

            case SeededRandomExpressionNode seededRandom:
                if (!SupportsExpression(seededRandom.SeedExpression, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Seeded random seed: {unsupportedReason}";
                    return false;
                }

                if (!SupportsExpression(seededRandom.BodyExpression, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Seeded random body: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case GeneratedCollectionExpressionNode generatedCollection:
                if (!SupportsIterationSource(generatedCollection.Source, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Generated collection source: {unsupportedReason}";
                    return false;
                }

                if (generatedCollection.Predicate is not null &&
                    !SupportsExpression(generatedCollection.Predicate, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Generated collection predicate: {unsupportedReason}";
                    return false;
                }

                if (!SupportsExpression(generatedCollection.Projection, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Generated collection projection: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case GuardedChoiceExpressionNode guardedChoice:
                for (var branchIndex = 0; branchIndex < guardedChoice.Branches.Count; branchIndex++)
                {
                    var branch = guardedChoice.Branches[branchIndex];
                    if (!SupportsExpression(branch.ConditionExpression, callables, out unsupportedReason))
                    {
                        unsupportedReason = $"Guarded choice branch {branchIndex} condition: {unsupportedReason}";
                        return false;
                    }

                    if (!SupportsExpression(branch.ValueExpression, callables, out unsupportedReason))
                    {
                        unsupportedReason = $"Guarded choice branch {branchIndex} value: {unsupportedReason}";
                        return false;
                    }
                }

                if (!SupportsExpression(guardedChoice.OtherwiseExpression, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Guarded choice otherwise: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case BinaryExpressionNode binary:
                if (!SupportsBinaryOperator(binary.Operator))
                {
                    unsupportedReason = $"Binary operator '{binary.Operator}' is not supported by the RegisterVM fast path.";
                    return false;
                }

                if (!SupportsExpression(binary.Left, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Binary left operand: {unsupportedReason}";
                    return false;
                }

                if (!SupportsExpression(binary.Right, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Binary right operand: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case RulePredicateExpressionNode rulePredicate:
                if (!SupportsExpression(rulePredicate.Value, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Rule predicate value: {unsupportedReason}";
                    return false;
                }

                if (!callables.TryGetValue(rulePredicate.RuleName, out var callable))
                {
                    unsupportedReason = $"Rule '{rulePredicate.RuleName}' was not found.";
                    return false;
                }

                if (callable.Kind != LinkedCallableKind.Rule)
                {
                    unsupportedReason = $"Callable '{rulePredicate.RuleName}' is a {callable.Kind}, not a rule.";
                    return false;
                }

                if (callable.Parameters.Count != 1)
                {
                    unsupportedReason = $"Rule '{rulePredicate.RuleName}' has {callable.Parameters.Count} parameters; only unary rule predicates are supported.";
                    return false;
                }

                if (!SupportsExpression(callable.Expression, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Rule '{rulePredicate.RuleName}' expression: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case CallExpressionNode call:
                if (!callables.TryGetValue(call.Name, out var called))
                {
                    unsupportedReason = $"Callable '{call.Name}' was not found.";
                    return false;
                }

                if (called.Parameters.Count != call.Arguments.Count)
                {
                    unsupportedReason = $"Callable '{call.Name}' expects {called.Parameters.Count} arguments but received {call.Arguments.Count}.";
                    return false;
                }

                for (var argumentIndex = 0; argumentIndex < call.Arguments.Count; argumentIndex++)
                {
                    if (!SupportsExpression(call.Arguments[argumentIndex], callables, out unsupportedReason))
                    {
                        unsupportedReason = $"Call argument {argumentIndex}: {unsupportedReason}";
                        return false;
                    }
                }

                if (!SupportsExpression(called.Expression, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Callable '{call.Name}' expression: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case TypeCastExpressionNode typeCast:
                if (!SupportsExpression(typeCast.Value, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Type cast value: {unsupportedReason}";
                    return false;
                }

                if (!SupportsTypeCast(typeCast.TypeName))
                {
                    unsupportedReason = $"Type cast '{typeCast.TypeName}' is not supported by the RegisterVM fast path.";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case TypeCheckExpressionNode typeCheck:
                if (!SupportsExpression(typeCheck.Value, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Type check value: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case MemberAccessExpressionNode memberAccess:
                if (!SupportsExpression(memberAccess.Target, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Member access target: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case CollectionAccessExpressionNode collectionAccess:
                if (SupportsPipelinedCollection(collectionAccess, callables, out unsupportedReason) ||
                    SupportsIndexedCollectionAccess(collectionAccess, callables, out unsupportedReason))
                {
                    unsupportedReason = string.Empty;
                    return true;
                }

                return false;

            default:
                unsupportedReason = $"Expression '{expression.GetType().Name}' is not supported by the RegisterVM fast path.";
                return false;
        }
    }

    private static bool SupportsBinaryOperator(string operation)
        => operation is "+" or "-" or "*" or "/" or "div" or "mod" or "rem" or
            "=" or "==" or "<>" or "<" or ">" or "<=" or ">=" or
            "&" or "|" or "^" or "default" or "in" or "value in" or
            "starts with" or "ends with" or
            "intersect" or "combine" or "merge" or "except" or "zip";

    private static bool SupportsUnaryOperator(string operation)
        => operation is "-" or "!" or "has value" or "empty" or
            "len" or "chance" or "keys" or "values" or "entries" or
            "abs" or "floor" or "ceil" or "round" or "rounddown" or
            "roundup" or "roundeven" or "wrapDegree";

    private static bool SupportsVariadicTaggedOperator(string operation)
        => operation is "min" or "max";

    private static bool SupportsTypeCast(string typeName)
        => typeName is "boolean" or "integer" or "decimal" or "number" or "percentage" or "degree" or "meter" or "second";

    private static bool SupportsDeclaredType(string typeName)
        => typeName is "nothing" or "tag" or "text" or
            "percentage" or "degree" or "meter" or "second" or
            "vector2" or "vector3" or
            "boolean" or "integer" or "decimal" or "number" or
            "list" or "range" or "message" or "handler" or
            "dictionary" or "set" or "dice" or "optional" ||
            !string.IsNullOrWhiteSpace(typeName);

    private static bool SupportsIterationSource(
        IterationSourceNode source,
        IReadOnlyDictionary<string, LinkedCallableDefinition> callables,
        out string unsupportedReason)
    {
        switch (source)
        {
            case CollectionIterationSourceNode collection:
                if (!SupportsExpression(collection.Expression, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Collection source expression: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case RangeIterationSourceNode range:
                return SupportsRange(range.RangeExpression, callables, out unsupportedReason);

            default:
                unsupportedReason = $"Iteration source '{source.GetType().Name}' is not supported.";
                return false;
        }
    }

    private static bool SupportsPipelinedCollection(
        CollectionAccessExpressionNode expression,
        IReadOnlyDictionary<string, LinkedCallableDefinition> callables,
        out string unsupportedReason)
    {
        var selectors = new List<CollectionSelectorNode>();
        ExpressionNode source = expression;
        while (source is CollectionAccessExpressionNode collectionAccess)
        {
            selectors.Add(collectionAccess.Selector);
            source = collectionAccess.Target;
        }

        selectors.Reverse();
        if (selectors.Count == 0)
        {
            unsupportedReason = "Collection access did not contain a selector.";
            return false;
        }

        if (!SupportsExpression(source, callables, out unsupportedReason))
        {
            unsupportedReason = $"Collection source: {unsupportedReason}";
            return false;
        }

        for (var i = 0; i < selectors.Count; i++)
        {
            var isTerminal = i == selectors.Count - 1;
            if (!SupportsSelector(selectors[i], isTerminal, callables, out unsupportedReason))
            {
                unsupportedReason = $"Collection selector {i}: {unsupportedReason}";
                return false;
            }
        }

        unsupportedReason = string.Empty;
        return true;
    }

    private static bool SupportsIndexedCollectionAccess(
        CollectionAccessExpressionNode expression,
        IReadOnlyDictionary<string, LinkedCallableDefinition> callables,
        out string unsupportedReason)
    {
        if (expression.Selector is not ExpressionSelectorNode selector)
        {
            unsupportedReason = $"Collection selector '{expression.Selector.GetType().Name}' is not a direct index/key expression.";
            return false;
        }

        if (!SupportsExpression(expression.Target, callables, out unsupportedReason))
        {
            unsupportedReason = $"Collection access target: {unsupportedReason}";
            return false;
        }

        if (!SupportsExpression(selector.Expression, callables, out unsupportedReason))
        {
            unsupportedReason = $"Collection access selector: {unsupportedReason}";
            return false;
        }

        unsupportedReason = string.Empty;
        return true;
    }

    private static bool SupportsSelector(
        CollectionSelectorNode selector,
        bool isTerminal,
        IReadOnlyDictionary<string, LinkedCallableDefinition> callables,
        out string unsupportedReason)
    {
        switch (selector)
        {
            case FilterSelectorNode filter:
                if (!SupportsExpression(filter.Predicate, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Filter predicate: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case SelectSelectorNode select:
                if (!SupportsExpression(select.Projection, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Select projection: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case PredicateSelectorNode predicate when isTerminal:
                if (!SupportsExpression(predicate.Predicate, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Predicate selector predicate: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case SumSelectorNode sum when isTerminal:
                if (!SupportsExpression(sum.Projection, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Sum projection: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case AverageSelectorNode average when isTerminal:
                if (!SupportsExpression(average.Projection, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Average projection: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case CountSelectorNode count when isTerminal:
                if (!SupportsExpression(count.Predicate, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Count predicate: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case EdgeSelectorNode edge when isTerminal:
                if (edge.Predicate is not null &&
                    !SupportsExpression(edge.Predicate, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Edge predicate: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case PatternSelectorNode pattern when isTerminal:
                if (!SupportsDicePattern(pattern.Pattern, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Pattern selector: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case ObjectMatchSelectorNode objectMatch when isTerminal:
                if (!SupportsObjectMatchPattern(objectMatch.Pattern, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Object match selector: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case TakePatternSelectorNode takePattern when isTerminal:
                if (!SupportsDicePattern(takePattern.Pattern, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Take pattern selector: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case MinSelectorNode min when isTerminal:
                if (!SupportsExpression(min.Projection, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Min projection: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case MaxSelectorNode max when isTerminal:
                if (!SupportsExpression(max.Projection, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Max projection: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case DictionarySelectorNode dictionary when isTerminal:
                if (!SupportsExpression(dictionary.KeyProjection, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Dictionary key projection: {unsupportedReason}";
                    return false;
                }

                if (dictionary.ValueProjection is not null &&
                    !SupportsExpression(dictionary.ValueProjection, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Dictionary value projection: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case ContainsSelectorNode contains when isTerminal:
                if (!SupportsExpression(contains.ValueExpression, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Contains value: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case ChooseSelectorNode choose when isTerminal:
                if (choose.Predicate is not null &&
                    !SupportsExpression(choose.Predicate, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Choose predicate: {unsupportedReason}";
                    return false;
                }

                if (choose.WeightExpression is not null &&
                    !SupportsExpression(choose.WeightExpression, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Choose weight: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case DrawSelectorNode when isTerminal:
            case ShuffleSelectorNode when isTerminal:
            case SortSelectorNode when isTerminal:
            case ReverseSelectorNode when isTerminal:
            case SequenceSliceSelectorNode when isTerminal:
                unsupportedReason = string.Empty;
                return true;

            case DistinctSelectorNode distinct when isTerminal:
                if (distinct.Projection is not null &&
                    !SupportsExpression(distinct.Projection, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Distinct projection: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case GroupBySelectorNode groupBy when isTerminal:
                if (!SupportsExpression(groupBy.Projection, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Group projection: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            case OrderBySelectorNode orderBy when isTerminal:
                if (!SupportsExpression(orderBy.Projection, callables, out unsupportedReason))
                {
                    unsupportedReason = $"Order projection: {unsupportedReason}";
                    return false;
                }

                unsupportedReason = string.Empty;
                return true;

            default:
                unsupportedReason = $"Selector '{selector.GetType().Name}' is not supported as a {(isTerminal ? "terminal" : "prefix")} RegisterVM fast-path selector.";
                return false;
        }
    }

    private static bool SupportsDicePattern(
        DicePatternNode pattern,
        IReadOnlyDictionary<string, LinkedCallableDefinition> callables,
        out string unsupportedReason)
    {
        if (pattern is DiceCountPatternNode { Face: { } face } &&
            !SupportsExpression(face, callables, out unsupportedReason))
        {
            unsupportedReason = $"Dice count face: {unsupportedReason}";
            return false;
        }

        unsupportedReason = string.Empty;
        return true;
    }

    private static bool SupportsObjectMatchPattern(
        ObjectMatchPatternNode pattern,
        IReadOnlyDictionary<string, LinkedCallableDefinition> callables,
        out string unsupportedReason)
    {
        foreach (var entry in pattern.Entries)
        {
            switch (entry.Value)
            {
                case ObjectMatchExpressionValueNode expressionValue:
                    if (!SupportsExpression(expressionValue.Expression, callables, out unsupportedReason))
                    {
                        unsupportedReason = $"Object field '{entry.Key}': {unsupportedReason}";
                        return false;
                    }

                    break;

                case ObjectMatchNestedValueNode nestedValue:
                    if (!SupportsObjectMatchPattern(nestedValue.Pattern, callables, out unsupportedReason))
                    {
                        unsupportedReason = $"Object field '{entry.Key}': {unsupportedReason}";
                        return false;
                    }

                    break;
            }
        }

        unsupportedReason = string.Empty;
        return true;
    }

    private sealed class SlotCollector(
        IReadOnlyDictionary<string, LinkedCallableDefinition> callables,
        IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions)
    {
        private readonly Dictionary<string, int> _slots = new(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> Slots => _slots;

        public void CollectTypeDefinitions()
        {
            foreach (var typeDefinition in typeDefinitions.Values)
            {
                foreach (var field in typeDefinition.Fields)
                {
                    AddSlot(field.Name);
                    if (field.MinimumExpression is not null)
                    {
                        CollectExpression(field.MinimumExpression);
                    }

                    if (field.MaximumExpression is not null)
                    {
                        CollectExpression(field.MaximumExpression);
                    }

                    if (field.ComputedExpression is not null)
                    {
                        CollectExpression(field.ComputedExpression);
                    }
                }
            }
        }

        public void AddSlot(string name)
        {
            if (!_slots.ContainsKey(name))
            {
                _slots[name] = _slots.Count;
            }
        }

        public void CollectStatement(StatementNode statement)
        {
            switch (statement)
            {
                case LetStatementNode let:
                    CollectExpression(let.Expression);
                    AddSlot(let.Identifier);
                    break;

                case IfStatementNode ifStatement:
                    CollectExpression(ifStatement.Condition);
                    foreach (var nested in ifStatement.ThenBody.Statements)
                    {
                        CollectStatement(nested);
                    }

                    if (ifStatement.ElseBody is not null)
                    {
                        foreach (var nested in ifStatement.ElseBody.Statements)
                        {
                            CollectStatement(nested);
                        }
                    }

                    break;

                case PublishStatementNode publish:
                    CollectExpression(publish.MessageExpression);
                    break;

                case ForStatementNode { Source: RangeIterationSourceNode range } forStatement:
                    CollectRange(range.RangeExpression);
                    AddSlot(forStatement.Identifier);
                    foreach (var nested in forStatement.Body.Statements)
                    {
                        CollectStatement(nested);
                    }

                    break;

                case ForStatementNode { Source: CollectionIterationSourceNode collection } forStatement:
                    CollectExpression(collection.Expression);
                    AddSlot(forStatement.Identifier);
                    foreach (var nested in forStatement.Body.Statements)
                    {
                        CollectStatement(nested);
                    }

                    break;

                case ExpressionStatementNode expressionStatement:
                    CollectExpression(expressionStatement.Expression);
                    break;

                case SeededRandomStatementNode seededRandom:
                    CollectExpression(seededRandom.SeedExpression);
                    foreach (var nested in seededRandom.Body.Statements)
                    {
                        CollectStatement(nested);
                    }

                    break;
            }
        }

        private void CollectRange(RangeExpressionNode range)
        {
            CollectExpression(range.FromExpression);
            CollectExpression(range.ToExpression);
            if (range.StepExpression is not null)
            {
                CollectExpression(range.StepExpression);
            }
        }

        private void CollectExpression(ExpressionNode expression)
        {
            switch (expression)
            {
                case IdentifierExpressionNode identifier:
                    AddSlot(identifier.Name);
                    break;

                case MessageLiteralExpressionNode message:
                    foreach (var argument in message.Arguments)
                    {
                        CollectExpression(argument.Expression);
                    }

                    break;

                case HandlerBindExpressionNode handlerBind:
                    CollectExpression(handlerBind.CalleeExpression);
                    foreach (var argument in handlerBind.Arguments)
                    {
                        CollectExpression(argument.Expression);
                    }

                    break;

                case ListLiteralExpressionNode list:
                    foreach (var item in list.Items)
                    {
                        CollectExpression(item);
                    }

                    break;

                case SetLiteralExpressionNode set:
                    foreach (var item in set.Items)
                    {
                        CollectExpression(item);
                    }

                    break;

                case DictionaryLiteralExpressionNode dictionary:
                    foreach (var entry in dictionary.Entries)
                    {
                        CollectExpression(entry.Value);
                    }

                    break;

                case UnaryExpressionNode unary:
                    CollectExpression(unary.Operand);
                    break;

                case VariadicTaggedExpressionNode variadic:
                    foreach (var argument in variadic.Arguments)
                    {
                        CollectExpression(argument);
                    }

                    break;

                case ClampExpressionNode clamp:
                    CollectExpression(clamp.Value);
                    CollectExpression(clamp.Minimum);
                    CollectExpression(clamp.Maximum);
                    break;

                case RandomExpressionNode random:
                    CollectExpression(random.FromExpression);
                    CollectExpression(random.ToExpression);
                    break;

                case RangeExpressionNode range:
                    CollectRange(range);
                    break;

                case SeededRandomExpressionNode seededRandom:
                    CollectExpression(seededRandom.SeedExpression);
                    CollectExpression(seededRandom.BodyExpression);
                    break;

                case GeneratedCollectionExpressionNode generatedCollection:
                    AddSlot(generatedCollection.Identifier);
                    CollectIterationSource(generatedCollection.Source);
                    if (generatedCollection.Predicate is not null)
                    {
                        CollectExpression(generatedCollection.Predicate);
                    }

                    CollectExpression(generatedCollection.Projection);
                    break;

                case GuardedChoiceExpressionNode guardedChoice:
                    foreach (var branch in guardedChoice.Branches)
                    {
                        CollectExpression(branch.ConditionExpression);
                        CollectExpression(branch.ValueExpression);
                    }

                    CollectExpression(guardedChoice.OtherwiseExpression);
                    break;

                case BinaryExpressionNode binary:
                    CollectExpression(binary.Left);
                    CollectExpression(binary.Right);
                    break;

                case RulePredicateExpressionNode rulePredicate:
                    CollectExpression(rulePredicate.Value);
                    if (callables.TryGetValue(rulePredicate.RuleName, out var callable))
                    {
                        foreach (var parameter in callable.Parameters)
                        {
                            AddSlot(parameter);
                        }

                        CollectExpression(callable.Expression);
                    }

                    break;

                case CallExpressionNode call:
                    foreach (var argument in call.Arguments)
                    {
                        CollectExpression(argument);
                    }

                    if (callables.TryGetValue(call.Name, out var called))
                    {
                        foreach (var parameter in called.Parameters)
                        {
                            AddSlot(parameter);
                        }

                        CollectExpression(called.Expression);
                    }

                    break;

                case TypeCastExpressionNode typeCast:
                    CollectExpression(typeCast.Value);
                    break;

                case TypeCheckExpressionNode typeCheck:
                    CollectExpression(typeCheck.Value);
                    break;

                case MemberAccessExpressionNode memberAccess:
                    CollectExpression(memberAccess.Target);
                    break;

                case CollectionAccessExpressionNode collectionAccess:
                    CollectCollectionAccess(collectionAccess);
                    break;
            }
        }

        private void CollectIterationSource(IterationSourceNode source)
        {
            switch (source)
            {
                case CollectionIterationSourceNode collection:
                    CollectExpression(collection.Expression);
                    break;

                case RangeIterationSourceNode range:
                    CollectRange(range.RangeExpression);
                    break;
            }
        }

        private void CollectCollectionAccess(CollectionAccessExpressionNode expression)
        {
            if (expression.Selector is ExpressionSelectorNode selector)
            {
                CollectExpression(expression.Target);
                CollectExpression(selector.Expression);
                return;
            }

            var selectors = new List<CollectionSelectorNode>();
            ExpressionNode source = expression;
            while (source is CollectionAccessExpressionNode collectionAccess)
            {
                selectors.Add(collectionAccess.Selector);
                source = collectionAccess.Target;
            }

            CollectExpression(source);
            selectors.Reverse();
            foreach (var pipelineSelector in selectors)
            {
                CollectSelector(pipelineSelector);
            }
        }

        private void CollectSelector(CollectionSelectorNode selector)
        {
            switch (selector)
            {
                case FilterSelectorNode filter:
                    AddSlot(filter.Identifier);
                    CollectExpression(filter.Predicate);
                    break;

                case SelectSelectorNode select:
                    AddSlot(select.Identifier);
                    CollectExpression(select.Projection);
                    break;

                case SumSelectorNode sum:
                    AddSlot(sum.Identifier);
                    CollectExpression(sum.Projection);
                    break;

                case AverageSelectorNode average:
                    AddSlot(average.Identifier);
                    CollectExpression(average.Projection);
                    break;

                case CountSelectorNode count:
                    AddSlot(count.Identifier);
                    CollectExpression(count.Predicate);
                    break;

                case PredicateSelectorNode predicate:
                    AddSlot(predicate.Identifier);
                    CollectExpression(predicate.Predicate);
                    break;

                case EdgeSelectorNode edge:
                    if (!string.IsNullOrEmpty(edge.Identifier))
                    {
                        AddSlot(edge.Identifier!);
                    }

                    if (edge.Predicate is not null)
                    {
                        CollectExpression(edge.Predicate);
                    }

                    break;

                case PatternSelectorNode pattern:
                    CollectDicePattern(pattern.Pattern);
                    break;

                case ObjectMatchSelectorNode objectMatch:
                    CollectObjectMatchPattern(objectMatch.Pattern);
                    break;

                case TakePatternSelectorNode takePattern:
                    CollectDicePattern(takePattern.Pattern);
                    break;

                case MinSelectorNode min:
                    AddSlot(min.Identifier);
                    CollectExpression(min.Projection);
                    break;

                case MaxSelectorNode max:
                    AddSlot(max.Identifier);
                    CollectExpression(max.Projection);
                    break;

                case DictionarySelectorNode dictionary:
                    AddSlot(dictionary.Identifier);
                    CollectExpression(dictionary.KeyProjection);
                    if (dictionary.ValueProjection is not null)
                    {
                        CollectExpression(dictionary.ValueProjection);
                    }

                    break;

                case ContainsSelectorNode contains:
                    CollectExpression(contains.ValueExpression);
                    break;

                case ChooseSelectorNode choose:
                    if (!string.IsNullOrEmpty(choose.Identifier))
                    {
                        AddSlot(choose.Identifier!);
                    }

                    if (choose.Predicate is not null)
                    {
                        CollectExpression(choose.Predicate);
                    }

                    if (!string.IsNullOrEmpty(choose.WeightIdentifier))
                    {
                        AddSlot(choose.WeightIdentifier!);
                    }

                    if (choose.WeightExpression is not null)
                    {
                        CollectExpression(choose.WeightExpression);
                    }

                    break;

                case DistinctSelectorNode distinct:
                    if (!string.IsNullOrEmpty(distinct.Identifier))
                    {
                        AddSlot(distinct.Identifier!);
                    }

                    if (distinct.Projection is not null)
                    {
                        CollectExpression(distinct.Projection);
                    }

                    break;

                case GroupBySelectorNode groupBy:
                    AddSlot(groupBy.Identifier);
                    CollectExpression(groupBy.Projection);
                    break;

                case OrderBySelectorNode orderBy:
                    AddSlot(orderBy.Identifier);
                    CollectExpression(orderBy.Projection);
                    break;
            }
        }

        private void CollectDicePattern(DicePatternNode pattern)
        {
            if (pattern is DiceCountPatternNode { Face: { } face })
            {
                CollectExpression(face);
            }
        }

        private void CollectObjectMatchPattern(ObjectMatchPatternNode pattern)
        {
            foreach (var entry in pattern.Entries)
            {
                switch (entry.Value)
                {
                    case ObjectMatchExpressionValueNode expressionValue:
                        CollectExpression(expressionValue.Expression);
                        break;

                    case ObjectMatchNestedValueNode nestedValue:
                        CollectObjectMatchPattern(nestedValue.Pattern);
                        break;
                }
            }
        }
    }

    private sealed class ProgramCompiler(
        IReadOnlyDictionary<string, int> slots,
        IReadOnlyDictionary<string, LinkedCallableDefinition> callables)
    {
        private readonly IReadOnlyDictionary<string, LinkedCallableDefinition> _callables = callables;
        private readonly Dictionary<ExpressionNode, RegisterFastExpressionProgram> _expressionPrograms = new(ReferenceEqualityComparer<ExpressionNode>.Instance);
        private readonly Dictionary<PublishStatementNode, RegisterFastPublishLayout> _publishLayouts = new(ReferenceEqualityComparer<PublishStatementNode>.Instance);

        public IReadOnlyDictionary<ExpressionNode, RegisterFastExpressionProgram> ExpressionPrograms => _expressionPrograms;

        public IReadOnlyDictionary<PublishStatementNode, RegisterFastPublishLayout> PublishLayouts => _publishLayouts;

        public int MaxStackDepth { get; private set; } = 1;

        public void CompileStatementPrograms(StatementNode statement)
        {
            switch (statement)
            {
                case LetStatementNode let:
                    CompileExpression(let.Expression);
                    break;

                case PublishStatementNode { MessageExpression: MessageLiteralExpressionNode } publish:
                    CompilePublishLayout(publish);
                    break;

                case PublishStatementNode publish:
                    CompileExpression(publish.MessageExpression);
                    break;

                case IfStatementNode ifStatement:
                    CompileExpression(ifStatement.Condition);
                    foreach (var nested in ifStatement.ThenBody.Statements)
                    {
                        CompileStatementPrograms(nested);
                    }

                    if (ifStatement.ElseBody is not null)
                    {
                        foreach (var nested in ifStatement.ElseBody.Statements)
                        {
                            CompileStatementPrograms(nested);
                        }
                    }

                    break;

                case ForStatementNode { Source: RangeIterationSourceNode range } forStatement:
                    CompileExpression(range.RangeExpression.FromExpression);
                    CompileExpression(range.RangeExpression.ToExpression);
                    if (range.RangeExpression.StepExpression is not null)
                    {
                        CompileExpression(range.RangeExpression.StepExpression);
                    }

                    foreach (var nested in forStatement.Body.Statements)
                    {
                        CompileStatementPrograms(nested);
                    }

                    break;

                case ForStatementNode { Source: CollectionIterationSourceNode collection } forStatement:
                    CompileExpression(collection.Expression);

                    foreach (var nested in forStatement.Body.Statements)
                    {
                        CompileStatementPrograms(nested);
                    }

                    break;

                case ExpressionStatementNode expressionStatement:
                    CompileExpression(expressionStatement.Expression);
                    break;

                case SeededRandomStatementNode seededRandom:
                    CompileExpression(seededRandom.SeedExpression);
                    foreach (var nested in seededRandom.Body.Statements)
                    {
                        CompileStatementPrograms(nested);
                    }

                    break;
            }
        }

        private RegisterFastExpressionProgram CompileExpression(ExpressionNode expression)
        {
            if (_expressionPrograms.TryGetValue(expression, out var program))
            {
                return program;
            }

            var instructions = new List<RegisterFastInstruction>();
            var builder = new ExpressionBuilder(this, instructions);
            builder.EmitExpression(expression);
            program = new RegisterFastExpressionProgram(instructions.ToArray(), Math.Max(1, builder.MaxStackDepth));
            _expressionPrograms[expression] = program;
            MaxStackDepth = Math.Max(MaxStackDepth, program.MaxStackDepth + 8);
            return program;
        }

        private RegisterFastPublishLayout CompilePublishLayout(PublishStatementNode publish)
        {
            if (_publishLayouts.TryGetValue(publish, out var layout))
            {
                return layout;
            }

            if (publish.MessageExpression is not MessageLiteralExpressionNode message)
            {
                throw new InvalidOperationException("Unsupported RegisterVM publish expression.");
            }

            var argumentNames = new string[message.Arguments.Count];
            var argumentPrograms = new RegisterFastExpressionProgram[message.Arguments.Count];
            for (var argumentIndex = 0; argumentIndex < message.Arguments.Count; argumentIndex++)
            {
                var argument = message.Arguments[argumentIndex];
                argumentNames[argumentIndex] = argument.Name;
                argumentPrograms[argumentIndex] = CompileExpression(argument.Expression);
            }

            layout = new RegisterFastPublishLayout(
                EventScriptMessageSignature.NormalizeMessageName(message.Message),
                EventScriptMessageSignature.CreateSignatureId(message.Message, argumentNames),
                argumentNames,
                argumentPrograms);
            _publishLayouts[publish] = layout;
            return layout;
        }

        private bool TryGetSlot(string name, out int slot)
            => slots.TryGetValue(name, out slot);

        private bool TryGetCastKind(string typeName, out RegisterFastCastKind kind)
        {
            kind = typeName switch
            {
                "boolean" => RegisterFastCastKind.Boolean,
                "integer" => RegisterFastCastKind.Integer,
                "decimal" => RegisterFastCastKind.Decimal,
                "number" => RegisterFastCastKind.Number,
                "percentage" => RegisterFastCastKind.Percentage,
                "degree" => RegisterFastCastKind.Degree,
                "meter" => RegisterFastCastKind.Meter,
                "second" => RegisterFastCastKind.Second,
                _ => default
            };

            return SupportsTypeCast(typeName);
        }

        private sealed class ExpressionBuilder(ProgramCompiler compiler, List<RegisterFastInstruction> instructions)
        {
            private int _stackDepth;

            public int MaxStackDepth { get; private set; }

            public void EmitExpression(ExpressionNode expression)
            {
                switch (expression)
                {
                    case BooleanLiteralExpressionNode boolean:
                        EmitLoadConstant(RegisterFastValue.Boolean(boolean.Value));
                        return;

                    case IntegerLiteralExpressionNode integer:
                        EmitLoadConstant(RegisterFastValue.Integer(integer.Value));
                        return;

                    case DecimalLiteralExpressionNode decimalLiteral:
                        EmitLoadConstant(RegisterFastValue.Decimal(decimalLiteral.Value));
                        return;

                    case PercentageLiteralExpressionNode percentage:
                        EmitLoadConstant(RegisterFastValue.Percentage(percentage.PercentValue / 100m));
                        return;

                    case UnitDecimalLiteralExpressionNode unitDecimal:
                        EmitLoadConstant(EventScriptDecimalUnits.TryParseTypeName(unitDecimal.UnitName, out var unit)
                            ? RegisterFastValue.Decimal(unitDecimal.Value, unit)
                            : RegisterFastValue.NaN());
                        return;

                    case TextLiteralExpressionNode text:
                        EmitLoadConstant(RegisterFastValue.Reference(EventScriptValueFactory.Text(text.Value)));
                        return;

                    case TagLiteralExpressionNode tag:
                        EmitLoadConstant(RegisterFastValue.Reference(EventScriptValueFactory.Tag(tag.Name)));
                        return;

                    case HandlerLiteralExpressionNode handler:
                    {
                        var parameterNames = handler.Parameters.ToArray();
                        EmitLoadConstant(RegisterFastValue.Reference(EventScriptValueFactory.Handler(
                            new EventScriptMessageSignature(handler.Message, parameterNames))));
                        return;
                    }

                    case MessageLiteralExpressionNode message:
                        var argumentNames = new string[message.Arguments.Count];
                        for (var argumentIndex = 0; argumentIndex < message.Arguments.Count; argumentIndex++)
                        {
                            var argument = message.Arguments[argumentIndex];
                            argumentNames[argumentIndex] = argument.Name;
                            EmitExpression(argument.Expression);
                        }

                        instructions.Add(new RegisterFastInstruction(
                            RegisterFastOpCode.BuildMessage,
                            A: message.Arguments.Count,
                            DiagnosticName: EventScriptMessageSignature.NormalizeMessageName(message.Message),
                            DiagnosticArgumentName: EventScriptMessageSignature.CreateSignatureId(message.Message, argumentNames),
                            Names: argumentNames));
                        CollapseValuesToSingle(message.Arguments.Count);
                        return;

                    case HandlerBindExpressionNode handlerBind:
                        var bindArgumentNames = new string[handlerBind.Arguments.Count];
                        EmitExpression(handlerBind.CalleeExpression);
                        for (var argumentIndex = 0; argumentIndex < handlerBind.Arguments.Count; argumentIndex++)
                        {
                            var argument = handlerBind.Arguments[argumentIndex];
                            bindArgumentNames[argumentIndex] = argument.Name;
                            EmitExpression(argument.Expression);
                        }

                        instructions.Add(new RegisterFastInstruction(
                            RegisterFastOpCode.BindHandler,
                            A: handlerBind.Arguments.Count,
                            Names: bindArgumentNames));
                        CollapseValuesToSingle(handlerBind.Arguments.Count + 1);
                        return;

                    case ListLiteralExpressionNode list:
                        foreach (var item in list.Items)
                        {
                            EmitExpression(item);
                        }

                        instructions.Add(new RegisterFastInstruction(RegisterFastOpCode.BuildList, A: list.Items.Count));
                        CollapseValuesToSingle(list.Items.Count);
                        return;

                    case SetLiteralExpressionNode set:
                        foreach (var item in set.Items)
                        {
                            EmitExpression(item);
                        }

                        instructions.Add(new RegisterFastInstruction(RegisterFastOpCode.BuildSet, A: set.Items.Count));
                        CollapseValuesToSingle(set.Items.Count);
                        return;

                    case DictionaryLiteralExpressionNode dictionary:
                        var names = new string[dictionary.Entries.Count];
                        for (var entryIndex = 0; entryIndex < dictionary.Entries.Count; entryIndex++)
                        {
                            var entry = dictionary.Entries[entryIndex];
                            names[entryIndex] = entry.Key;
                            EmitExpression(entry.Value);
                        }

                        instructions.Add(new RegisterFastInstruction(
                            RegisterFastOpCode.BuildDictionary,
                            A: dictionary.Entries.Count,
                            Names: names));
                        CollapseValuesToSingle(dictionary.Entries.Count);
                        return;

                    case IdentifierExpressionNode identifier:
                        if (!compiler.TryGetSlot(identifier.Name, out var slot))
                        {
                            throw new InvalidOperationException($"Missing RegisterVM local slot '{identifier.Name}'.");
                        }

                        instructions.Add(new RegisterFastInstruction(RegisterFastOpCode.LoadSlot, slot));
                        Push();
                        return;

                    case UnaryExpressionNode unary:
                        EmitExpression(unary.Operand);
                        instructions.Add(new RegisterFastInstruction(
                            RegisterFastOpCode.Unary,
                            DiagnosticName: unary.Operator));
                        return;

                    case VariadicTaggedExpressionNode variadic:
                        foreach (var argument in variadic.Arguments)
                        {
                            EmitExpression(argument);
                        }

                        instructions.Add(new RegisterFastInstruction(
                            RegisterFastOpCode.Variadic,
                            A: variadic.Arguments.Count,
                            DiagnosticName: variadic.Operator));
                        CollapseValuesToSingle(variadic.Arguments.Count);
                        return;

                    case ClampExpressionNode clamp:
                        EmitExpression(clamp.Value);
                        EmitExpression(clamp.Minimum);
                        EmitExpression(clamp.Maximum);
                        instructions.Add(new RegisterFastInstruction(RegisterFastOpCode.Clamp));
                        CollapseValuesToSingle(3);
                        return;

                    case RandomExpressionNode random:
                        EmitExpression(random.FromExpression);
                        EmitExpression(random.ToExpression);
                        instructions.Add(new RegisterFastInstruction(RegisterFastOpCode.Random));
                        Pop();
                        return;

                    case RangeExpressionNode range:
                        EmitExpression(range.FromExpression);
                        EmitExpression(range.ToExpression);
                        var rangeValueCount = 2;
                        if (range.StepExpression is not null)
                        {
                            EmitExpression(range.StepExpression);
                            rangeValueCount = 3;
                        }

                        instructions.Add(new RegisterFastInstruction(RegisterFastOpCode.Range, A: rangeValueCount));
                        CollapseValuesToSingle(rangeValueCount);
                        return;

                    case DiceExpressionNode dice:
                        instructions.Add(new RegisterFastInstruction(
                            RegisterFastOpCode.Dice,
                            A: dice.DiceCount,
                            B: dice.SideCount));
                        Push();
                        return;

                    case SeededRandomExpressionNode seededRandom:
                        EmitExpression(seededRandom.SeedExpression);
                        var seededBodyProgram = compiler.CompileExpression(seededRandom.BodyExpression);
                        AccountNestedProgram(argumentCount: 1, seededBodyProgram);
                        instructions.Add(new RegisterFastInstruction(
                            RegisterFastOpCode.SeededRandom,
                            ExpressionProgram: seededBodyProgram));
                        return;

                    case GeneratedCollectionExpressionNode generatedCollection:
                        instructions.Add(new RegisterFastInstruction(
                            RegisterFastOpCode.GeneratedCollection,
                            GeneratedCollectionProgram: CompileGeneratedCollection(generatedCollection)));
                        Push();
                        return;

                    case GuardedChoiceExpressionNode guardedChoice:
                        instructions.Add(new RegisterFastInstruction(
                            RegisterFastOpCode.GuardedChoice,
                            GuardedChoiceProgram: CompileGuardedChoice(guardedChoice)));
                        Push();
                        return;

                    case BinaryExpressionNode binary:
                        EmitExpression(binary.Left);
                        EmitExpression(binary.Right);
                        instructions.Add(new RegisterFastInstruction(ToBinaryOpCode(binary.Operator)));
                        Pop();
                        return;

                    case RulePredicateExpressionNode rulePredicate:
                        if (!compiler._callables.TryGetValue(rulePredicate.RuleName, out var callable) ||
                            callable.Parameters.Count != 1)
                        {
                            throw new InvalidOperationException($"Unsupported RegisterVM rule predicate '{rulePredicate.RuleName}'.");
                        }

                        if (!compiler.TryGetSlot(callable.Parameters[0], out var parameterSlot))
                        {
                            throw new InvalidOperationException($"Unsupported RegisterVM rule predicate '{rulePredicate.RuleName}'.");
                        }

                        EmitExpression(rulePredicate.Value);
                        var rulePredicateProgram = compiler.CompileExpression(callable.Expression);
                        AccountNestedProgram(argumentCount: 1, rulePredicateProgram);
                        instructions.Add(new RegisterFastInstruction(
                            RegisterFastOpCode.RulePredicate,
                            parameterSlot,
                            ExpressionProgram: rulePredicateProgram,
                            DiagnosticName: callable.Name,
                            DiagnosticArgumentName: callable.Parameters[0]));
                        return;

                    case CallExpressionNode call:
                        if (!compiler._callables.TryGetValue(call.Name, out var called))
                        {
                            throw new InvalidOperationException($"Unsupported RegisterVM callable '{call.Name}'.");
                        }

                        var parameterSlots = new int[called.Parameters.Count];
                        for (var parameterIndex = 0; parameterIndex < called.Parameters.Count; parameterIndex++)
                        {
                            if (!compiler.TryGetSlot(called.Parameters[parameterIndex], out parameterSlots[parameterIndex]))
                            {
                                throw new InvalidOperationException($"Missing RegisterVM callable parameter slot '{called.Parameters[parameterIndex]}'.");
                            }
                        }

                        foreach (var argument in call.Arguments)
                        {
                            EmitExpression(argument);
                        }

                        var callableProgram = compiler.CompileExpression(called.Expression);
                        AccountNestedProgram(call.Arguments.Count, callableProgram);
                        instructions.Add(new RegisterFastInstruction(
                            RegisterFastOpCode.Call,
                            A: call.Arguments.Count,
                            CallableKind: called.Kind == LinkedCallableKind.Rule
                                ? RegisterFastCallableKind.Rule
                                : RegisterFastCallableKind.Select,
                            ExpressionProgram: callableProgram,
                            DiagnosticName: called.Name,
                            Names: called.Parameters.ToArray(),
                            Slots: parameterSlots));
                        CollapseValuesToSingle(call.Arguments.Count);
                        return;

                    case TypeCastExpressionNode typeCast:
                        if (!compiler.TryGetCastKind(typeCast.TypeName, out var castKind))
                        {
                            throw new InvalidOperationException($"Unsupported RegisterVM type cast '{typeCast.TypeName}'.");
                        }

                        EmitExpression(typeCast.Value);
                        instructions.Add(new RegisterFastInstruction(RegisterFastOpCode.Cast, CastKind: castKind));
                        return;

                    case TypeCheckExpressionNode typeCheck:
                        EmitExpression(typeCheck.Value);
                        instructions.Add(new RegisterFastInstruction(
                            RegisterFastOpCode.TypeCheck,
                            DiagnosticName: typeCheck.TypeName));
                        return;

                    case MemberAccessExpressionNode memberAccess:
                        EmitExpression(memberAccess.Target);
                        instructions.Add(new RegisterFastInstruction(
                            RegisterFastOpCode.MemberAccess,
                            DiagnosticName: memberAccess.Member));
                        return;

                    case CollectionAccessExpressionNode collectionAccess:
                        if (collectionAccess.Selector is ExpressionSelectorNode selector)
                        {
                            EmitExpression(collectionAccess.Target);
                            EmitExpression(selector.Expression);
                            instructions.Add(new RegisterFastInstruction(RegisterFastOpCode.IndexedAccess));
                            Pop();
                            return;
                        }

                        instructions.Add(new RegisterFastInstruction(
                            RegisterFastOpCode.Pipeline,
                            PipelineProgram: CompilePipeline(collectionAccess)));
                        Push();
                        return;

                    default:
                        throw new InvalidOperationException($"Unsupported RegisterVM expression program node '{expression.GetType().Name}'.");
                }
            }

            private RegisterFastGeneratedCollectionProgram CompileGeneratedCollection(GeneratedCollectionExpressionNode generatedCollection)
            {
                var predicateProgram = generatedCollection.Predicate is null
                    ? null
                    : compiler.CompileExpression(generatedCollection.Predicate);
                var projectionProgram = compiler.CompileExpression(generatedCollection.Projection);
                if (predicateProgram is not null)
                {
                    AccountNestedProgram(0, predicateProgram);
                }

                AccountNestedProgram(0, projectionProgram);
                return new RegisterFastGeneratedCollectionProgram(
                    generatedCollection.CollectionType,
                    RequireSlot(generatedCollection.Identifier),
                    generatedCollection.Source,
                    predicateProgram,
                    projectionProgram);
            }

            private RegisterFastGuardedChoiceProgram CompileGuardedChoice(GuardedChoiceExpressionNode guardedChoice)
            {
                var valuePrograms = new RegisterFastExpressionProgram[guardedChoice.Branches.Count];
                var conditionPrograms = new RegisterFastExpressionProgram[guardedChoice.Branches.Count];
                for (var branchIndex = 0; branchIndex < guardedChoice.Branches.Count; branchIndex++)
                {
                    var branch = guardedChoice.Branches[branchIndex];
                    conditionPrograms[branchIndex] = compiler.CompileExpression(branch.ConditionExpression);
                    valuePrograms[branchIndex] = compiler.CompileExpression(branch.ValueExpression);
                    AccountNestedProgram(0, conditionPrograms[branchIndex]);
                    AccountNestedProgram(0, valuePrograms[branchIndex]);
                }

                var otherwiseProgram = compiler.CompileExpression(guardedChoice.OtherwiseExpression);
                AccountNestedProgram(0, otherwiseProgram);
                return new RegisterFastGuardedChoiceProgram(valuePrograms, conditionPrograms, otherwiseProgram);
            }

            private RegisterFastPipelineProgram CompilePipeline(CollectionAccessExpressionNode expression)
            {
                var selectors = new List<CollectionSelectorNode>();
                ExpressionNode source = expression;
                while (source is CollectionAccessExpressionNode collectionAccess)
                {
                    selectors.Add(collectionAccess.Selector);
                    source = collectionAccess.Target;
                }

                selectors.Reverse();
                var prefixSelectors = new List<RegisterFastSelectorProgram>(Math.Max(0, selectors.Count - 1));
                for (var i = 0; i < selectors.Count - 1; i++)
                {
                    prefixSelectors.Add(CompileSelector(selectors[i], isTerminal: false));
                }

                return new RegisterFastPipelineProgram(
                    compiler.CompileExpression(source),
                    prefixSelectors.ToArray(),
                    CompileSelector(selectors[^1], isTerminal: true));
            }

            private RegisterFastSelectorProgram CompileSelector(CollectionSelectorNode selector, bool isTerminal)
            {
                switch (selector)
                {
                    case FilterSelectorNode filter:
                        return new RegisterFastSelectorProgram(
                            RegisterFastSelectorKind.Filter,
                            RequireSlot(filter.Identifier),
                            compiler.CompileExpression(filter.Predicate));

                    case SelectSelectorNode select:
                        return new RegisterFastSelectorProgram(
                            RegisterFastSelectorKind.Select,
                            RequireSlot(select.Identifier),
                            compiler.CompileExpression(select.Projection));

                    case PredicateSelectorNode predicate when isTerminal:
                        return new RegisterFastSelectorProgram(
                            RegisterFastSelectorKind.Predicate,
                            RequireSlot(predicate.Identifier),
                            compiler.CompileExpression(predicate.Predicate),
                            predicate.Operator);

                    case SumSelectorNode sum when isTerminal:
                        return new RegisterFastSelectorProgram(
                            RegisterFastSelectorKind.Sum,
                            RequireSlot(sum.Identifier),
                            compiler.CompileExpression(sum.Projection));

                    case AverageSelectorNode average when isTerminal:
                        return new RegisterFastSelectorProgram(
                            RegisterFastSelectorKind.Average,
                            RequireSlot(average.Identifier),
                            compiler.CompileExpression(average.Projection));

                    case CountSelectorNode count when isTerminal:
                        return new RegisterFastSelectorProgram(
                            RegisterFastSelectorKind.Count,
                            RequireSlot(count.Identifier),
                            compiler.CompileExpression(count.Predicate));

                    case EdgeSelectorNode edge when isTerminal:
                        return new RegisterFastSelectorProgram(
                            RegisterFastSelectorKind.Edge,
                            string.IsNullOrEmpty(edge.Identifier) ? -1 : RequireSlot(edge.Identifier!),
                            edge.Predicate is null ? null : compiler.CompileExpression(edge.Predicate),
                            edge.Mode);

                    case PatternSelectorNode pattern when isTerminal:
                        return new RegisterFastSelectorProgram(
                            RegisterFastSelectorKind.Pattern,
                            -1,
                            null,
                            dicePattern: pattern.Pattern);

                    case ObjectMatchSelectorNode objectMatch when isTerminal:
                        return new RegisterFastSelectorProgram(
                            RegisterFastSelectorKind.ObjectMatch,
                            -1,
                            null,
                            objectPattern: objectMatch.Pattern);

                    case TakePatternSelectorNode takePattern when isTerminal:
                        return new RegisterFastSelectorProgram(
                            RegisterFastSelectorKind.TakePattern,
                            -1,
                            null,
                            dicePattern: takePattern.Pattern);

                    case MinSelectorNode min when isTerminal:
                        return new RegisterFastSelectorProgram(
                            RegisterFastSelectorKind.Min,
                            RequireSlot(min.Identifier),
                            compiler.CompileExpression(min.Projection));

                    case MaxSelectorNode max when isTerminal:
                        return new RegisterFastSelectorProgram(
                            RegisterFastSelectorKind.Max,
                            RequireSlot(max.Identifier),
                            compiler.CompileExpression(max.Projection));

                    case DictionarySelectorNode dictionary when isTerminal:
                        return new RegisterFastSelectorProgram(
                            RegisterFastSelectorKind.Dictionary,
                            RequireSlot(dictionary.Identifier),
                            compiler.CompileExpression(dictionary.KeyProjection),
                            secondaryExpressionProgram: dictionary.ValueProjection is null
                                ? null
                                : compiler.CompileExpression(dictionary.ValueProjection));

                    case ContainsSelectorNode contains when isTerminal:
                        return new RegisterFastSelectorProgram(
                            RegisterFastSelectorKind.Contains,
                            -1,
                            compiler.CompileExpression(contains.ValueExpression),
                            contains.Mode);

                    case ChooseSelectorNode choose when isTerminal:
                        return new RegisterFastSelectorProgram(
                            RegisterFastSelectorKind.Choose,
                            string.IsNullOrEmpty(choose.Identifier) ? -1 : RequireSlot(choose.Identifier!),
                            choose.Predicate is null ? null : compiler.CompileExpression(choose.Predicate),
                            count: choose.Count,
                            secondaryExpressionProgram: choose.WeightExpression is null
                                ? null
                                : compiler.CompileExpression(choose.WeightExpression),
                            secondaryIdentifierSlot: string.IsNullOrEmpty(choose.WeightIdentifier) ? -1 : RequireSlot(choose.WeightIdentifier!),
                            flag: choose.AtRandom);

                    case DrawSelectorNode draw when isTerminal:
                        return new RegisterFastSelectorProgram(
                            RegisterFastSelectorKind.Draw,
                            -1,
                            null,
                            count: draw.Count);

                    case ShuffleSelectorNode when isTerminal:
                        return new RegisterFastSelectorProgram(
                            RegisterFastSelectorKind.Shuffle,
                            -1,
                            null);

                    case SortSelectorNode sort when isTerminal:
                        return new RegisterFastSelectorProgram(
                            RegisterFastSelectorKind.Sort,
                            -1,
                            null,
                            sort.Direction);

                    case DistinctSelectorNode distinct when isTerminal:
                        return new RegisterFastSelectorProgram(
                            RegisterFastSelectorKind.Distinct,
                            string.IsNullOrEmpty(distinct.Identifier) ? -1 : RequireSlot(distinct.Identifier!),
                            distinct.Projection is null ? null : compiler.CompileExpression(distinct.Projection));

                    case GroupBySelectorNode groupBy when isTerminal:
                        return new RegisterFastSelectorProgram(
                            RegisterFastSelectorKind.GroupBy,
                            RequireSlot(groupBy.Identifier),
                            compiler.CompileExpression(groupBy.Projection));

                    case OrderBySelectorNode orderBy when isTerminal:
                        return new RegisterFastSelectorProgram(
                            RegisterFastSelectorKind.OrderBy,
                            RequireSlot(orderBy.Identifier),
                            compiler.CompileExpression(orderBy.Projection),
                            orderBy.Direction);

                    case ReverseSelectorNode when isTerminal:
                        return new RegisterFastSelectorProgram(
                            RegisterFastSelectorKind.Reverse,
                            -1,
                            null);

                    case SequenceSliceSelectorNode slice when isTerminal:
                        return new RegisterFastSelectorProgram(
                            RegisterFastSelectorKind.SequenceSlice,
                            -1,
                            null,
                            slice.Operation,
                            secondaryMode: slice.Scope,
                            count: slice.Count);

                    default:
                        throw new InvalidOperationException($"Unsupported RegisterVM selector program node '{selector.GetType().Name}'.");
                }
            }

            private int RequireSlot(string name)
            {
                if (!compiler.TryGetSlot(name, out var slot))
                {
                    throw new InvalidOperationException($"Missing RegisterVM local slot '{name}'.");
                }

                return slot;
            }

            private void EmitLoadConstant(RegisterFastValue value)
            {
                instructions.Add(new RegisterFastInstruction(RegisterFastOpCode.LoadConstant, Constant: value));
                Push();
            }

            private void Push()
            {
                _stackDepth++;
                MaxStackDepth = Math.Max(MaxStackDepth, _stackDepth);
            }

            private void Pop() => _stackDepth = Math.Max(0, _stackDepth - 1);

            private void AccountNestedProgram(int argumentCount, RegisterFastExpressionProgram program)
            {
                var nestedStackBaseDepth = Math.Max(0, _stackDepth - argumentCount);
                MaxStackDepth = Math.Max(MaxStackDepth, nestedStackBaseDepth + program.MaxStackDepth);
            }

            private void CollapseValuesToSingle(int valueCount)
            {
                if (valueCount == 0)
                {
                    Push();
                    return;
                }

                _stackDepth = Math.Max(1, _stackDepth - valueCount + 1);
            }

            private static RegisterFastOpCode ToBinaryOpCode(string operation)
                => operation switch
                {
                    "|" => RegisterFastOpCode.Or,
                    "^" => RegisterFastOpCode.Xor,
                    "&" => RegisterFastOpCode.And,
                    "=" or "==" => RegisterFastOpCode.Equal,
                    "<>" => RegisterFastOpCode.NotEqual,
                    "<" => RegisterFastOpCode.Less,
                    ">" => RegisterFastOpCode.Greater,
                    "<=" => RegisterFastOpCode.LessOrEqual,
                    ">=" => RegisterFastOpCode.GreaterOrEqual,
                    "+" => RegisterFastOpCode.Add,
                    "-" => RegisterFastOpCode.Subtract,
                    "*" => RegisterFastOpCode.Multiply,
                    "/" => RegisterFastOpCode.Divide,
                    "div" => RegisterFastOpCode.IntegerDivide,
                    "mod" => RegisterFastOpCode.Modulo,
                    "rem" => RegisterFastOpCode.Remainder,
                    "default" => RegisterFastOpCode.Default,
                    "in" => RegisterFastOpCode.Contains,
                    "value in" => RegisterFastOpCode.ContainsValue,
                    "starts with" => RegisterFastOpCode.StartsWith,
                    "ends with" => RegisterFastOpCode.EndsWith,
                    "intersect" => RegisterFastOpCode.Intersect,
                    "combine" or "merge" => RegisterFastOpCode.Combine,
                    "except" => RegisterFastOpCode.Except,
                    "zip" => RegisterFastOpCode.Zip,
                    _ => throw new InvalidOperationException($"Unsupported RegisterVM binary operator '{operation}'.")
                };
        }
    }
}

internal sealed class RegisterVmFastPathPlan
{
    private readonly IReadOnlyDictionary<string, int> _slots;
    private readonly IReadOnlyDictionary<ExpressionNode, RegisterFastExpressionProgram> _expressionPrograms;
    private readonly IReadOnlyDictionary<PublishStatementNode, RegisterFastPublishLayout> _publishLayouts;

    private RegisterVmFastPathPlan(
        bool isSupported,
        IReadOnlyDictionary<string, int> slots,
        IReadOnlyDictionary<ExpressionNode, RegisterFastExpressionProgram> expressionPrograms,
        IReadOnlyDictionary<PublishStatementNode, RegisterFastPublishLayout> publishLayouts,
        int maxStackDepth,
        string? unsupportedReason)
    {
        IsSupported = isSupported;
        _slots = slots;
        _expressionPrograms = expressionPrograms;
        _publishLayouts = publishLayouts;
        SlotCount = slots.Count;
        MaxStackDepth = maxStackDepth;
        UnsupportedReason = unsupportedReason;
    }

    public bool IsSupported { get; }

    public string? UnsupportedReason { get; }

    public int SlotCount { get; }

    public int MaxStackDepth { get; }

    public static RegisterVmFastPathPlan Unsupported(string unsupportedReason)
        => new(
            false,
            new Dictionary<string, int>(StringComparer.Ordinal),
            new Dictionary<ExpressionNode, RegisterFastExpressionProgram>(ReferenceEqualityComparer<ExpressionNode>.Instance),
            new Dictionary<PublishStatementNode, RegisterFastPublishLayout>(ReferenceEqualityComparer<PublishStatementNode>.Instance),
            1,
            string.IsNullOrWhiteSpace(unsupportedReason)
                ? "Handler is not supported by the RegisterVM fast path."
                : unsupportedReason);

    public static RegisterVmFastPathPlan Create(
        IReadOnlyDictionary<string, int> slots,
        IReadOnlyDictionary<ExpressionNode, RegisterFastExpressionProgram> expressionPrograms,
        IReadOnlyDictionary<PublishStatementNode, RegisterFastPublishLayout> publishLayouts,
        int maxStackDepth)
        => new(
            true,
            new Dictionary<string, int>(slots, StringComparer.Ordinal),
            new Dictionary<ExpressionNode, RegisterFastExpressionProgram>(expressionPrograms, ReferenceEqualityComparer<ExpressionNode>.Instance),
            new Dictionary<PublishStatementNode, RegisterFastPublishLayout>(publishLayouts, ReferenceEqualityComparer<PublishStatementNode>.Instance),
            maxStackDepth,
            null);

    public bool TryGetSlot(string name, out int slot)
        => _slots.TryGetValue(name, out slot);

    public bool TryGetExpressionProgram(ExpressionNode expression, out RegisterFastExpressionProgram program)
        => _expressionPrograms.TryGetValue(expression, out program!);

    public bool TryGetPublishLayout(PublishStatementNode publish, out RegisterFastPublishLayout layout)
        => _publishLayouts.TryGetValue(publish, out layout!);
}

internal sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
    where T : class
{
    public static ReferenceEqualityComparer<T> Instance { get; } = new();

    public bool Equals(T? x, T? y) => ReferenceEquals(x, y);

    public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
}
