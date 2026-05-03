#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Compiler;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.BytecodeVM;

internal static class BytecodeVmExecutionPlanBuilder
{
    public static BytecodeVmExecutionPlan CompileHandlerPlan(
        string messageName,
        int declarationOrder,
        IReadOnlyList<string> parameters,
        IReadOnlyList<StatementNode> statements,
        IReadOnlyDictionary<string, GameEventScriptCallableDefinition> callables,
        IReadOnlyDictionary<string, TypeDefinitionNode>? typeDefinitions = null,
        Func<GameEventScriptExtensionReference, int>? externalReferenceResolver = null)
    {
        if (!TryValidateStatements(statements, callables, out var failureReason))
        {
            throw new GameEventScriptCompilationException(
                $"BytecodeVM execution planner does not support handler '{messageName}' #{declarationOrder}: {failureReason}");
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

        var programCompiler = new ProgramCompiler(slotCollector.Slots, callables, externalReferenceResolver);
        foreach (var statement in statements)
        {
            programCompiler.CompileStatementPrograms(statement);
        }

        return BytecodeVmExecutionPlan.Create(
            slotCollector.Slots,
            programCompiler.ExpressionPrograms,
            programCompiler.PublishLayouts,
            programCompiler.MaxStackDepth);
    }

    private static bool TryValidateStatements(
        IReadOnlyList<StatementNode> statements,
        IReadOnlyDictionary<string, GameEventScriptCallableDefinition> callables,
        out string failureReason)
    {
        for (var statementIndex = 0; statementIndex < statements.Count; statementIndex++)
        {
            if (!TryValidateStatement(statements[statementIndex], callables, out failureReason))
            {
                failureReason = $"Statement {statementIndex}: {failureReason}";
                return false;
            }
        }

        failureReason = string.Empty;
        return true;
    }

    private static bool TryValidateStatement(
        StatementNode statement,
        IReadOnlyDictionary<string, GameEventScriptCallableDefinition> callables,
        out string failureReason)
    {
        switch (statement)
        {
            case LetStatementNode let:
                if (!string.IsNullOrEmpty(let.DeclaredType) &&
                    !IsKnownDeclaredType(let.DeclaredType!))
                {
                    failureReason = $"Typed let '{let.Identifier}' with type '{let.DeclaredType}' is not currently supported.";
                    return false;
                }

                if (!TryValidateExpression(let.Expression, callables, out failureReason))
                {
                    failureReason = $"Let '{let.Identifier}' expression: {failureReason}";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            case PublishStatementNode publish:
                if (!TryValidateExpression(publish.MessageExpression, callables, out failureReason))
                {
                    failureReason = publish.MessageExpression is MessageLiteralExpressionNode
                        ? $"Publish message expression: {failureReason}"
                        : $"Publish expression: {failureReason}";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            case IfStatementNode ifStatement:
                if (!TryValidateExpression(ifStatement.Condition, callables, out failureReason))
                {
                    failureReason = $"If condition: {failureReason}";
                    return false;
                }

                if (!TryValidateStatements(ifStatement.ThenBody.Statements, callables, out failureReason))
                {
                    failureReason = $"If then body: {failureReason}";
                    return false;
                }

                if (ifStatement.ElseBody is not null &&
                    !TryValidateStatements(ifStatement.ElseBody.Statements, callables, out failureReason))
                {
                    failureReason = $"If else body: {failureReason}";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            case ForStatementNode { Source: RangeIterationSourceNode range } forStatement:
                if (!TryValidateRange(range.RangeExpression, callables, out failureReason))
                {
                    failureReason = $"For range: {failureReason}";
                    return false;
                }

                if (!TryValidateStatements(forStatement.Body.Statements, callables, out failureReason))
                {
                    failureReason = $"For body: {failureReason}";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            case ForStatementNode { Source: CollectionIterationSourceNode collection } forStatement:
                if (!TryValidateExpression(collection.Expression, callables, out failureReason))
                {
                    failureReason = $"For collection source: {failureReason}";
                    return false;
                }

                if (!TryValidateStatements(forStatement.Body.Statements, callables, out failureReason))
                {
                    failureReason = $"For body: {failureReason}";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            case ForStatementNode forStatement:
                failureReason = $"For source '{forStatement.Source.GetType().Name}' is not currently supported.";
                return false;

            case ExpressionStatementNode expressionStatement:
                if (!TryValidateExpression(expressionStatement.Expression, callables, out failureReason))
                {
                    failureReason = $"Expression statement: {failureReason}";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            case SeededRandomStatementNode seededRandom:
                if (!TryValidateExpression(seededRandom.SeedExpression, callables, out failureReason))
                {
                    failureReason = $"Seeded random seed: {failureReason}";
                    return false;
                }

                if (!TryValidateStatements(seededRandom.Body.Statements, callables, out failureReason))
                {
                    failureReason = $"Seeded random body: {failureReason}";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            default:
                failureReason = $"Statement '{statement.GetType().Name}' is not currently supported.";
                return false;
        }
    }

    private static bool TryValidateRange(
        RangeExpressionNode range,
        IReadOnlyDictionary<string, GameEventScriptCallableDefinition> callables,
        out string failureReason)
    {
        if (!TryValidateExpression(range.FromExpression, callables, out failureReason))
        {
            failureReason = $"Range start: {failureReason}";
            return false;
        }

        if (!TryValidateExpression(range.ToExpression, callables, out failureReason))
        {
            failureReason = $"Range end: {failureReason}";
            return false;
        }

        if (range.StepExpression is not null &&
            !TryValidateExpression(range.StepExpression, callables, out failureReason))
        {
            failureReason = $"Range step: {failureReason}";
            return false;
        }

        failureReason = string.Empty;
        return true;
    }

    private static bool TryValidateExpression(
        ExpressionNode expression,
        IReadOnlyDictionary<string, GameEventScriptCallableDefinition> callables,
        out string failureReason)
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
                failureReason = string.Empty;
                return true;

            case MessageLiteralExpressionNode message:
                for (var argumentIndex = 0; argumentIndex < message.Arguments.Count; argumentIndex++)
                {
                    var argument = message.Arguments[argumentIndex];
                    if (!TryValidateExpression(argument.Expression, callables, out failureReason))
                    {
                        failureReason = $"Message argument '{argument.Name}': {failureReason}";
                        return false;
                    }
                }

                failureReason = string.Empty;
                return true;

            case HandlerLiteralExpressionNode:
                failureReason = string.Empty;
                return true;

            case HandlerBindExpressionNode handlerBind:
                if (!TryValidateExpression(handlerBind.CalleeExpression, callables, out failureReason))
                {
                    failureReason = $"Handler bind callee: {failureReason}";
                    return false;
                }

                for (var argumentIndex = 0; argumentIndex < handlerBind.Arguments.Count; argumentIndex++)
                {
                    var argument = handlerBind.Arguments[argumentIndex];
                    if (!TryValidateExpression(argument.Expression, callables, out failureReason))
                    {
                        failureReason = $"Handler bind argument '{argument.Name}': {failureReason}";
                        return false;
                    }
                }

                failureReason = string.Empty;
                return true;

            case ExtensionCallExpressionNode extensionCall:
                for (var argumentIndex = 0; argumentIndex < extensionCall.Arguments.Count; argumentIndex++)
                {
                    if (!TryValidateExpression(extensionCall.Arguments[argumentIndex].Expression, callables, out failureReason))
                    {
                        failureReason = $"Extension argument {argumentIndex}: {failureReason}";
                        return false;
                    }
                }

                failureReason = string.Empty;
                return true;

            case TypeConstructorExpressionNode typeConstructor:
                for (var argumentIndex = 0; argumentIndex < typeConstructor.Arguments.Count; argumentIndex++)
                {
                    if (!TryValidateExpression(typeConstructor.Arguments[argumentIndex].Expression, callables, out failureReason))
                    {
                        failureReason = $"Type constructor argument {argumentIndex}: {failureReason}";
                        return false;
                    }
                }

                failureReason = string.Empty;
                return true;

            case ListLiteralExpressionNode list:
                for (var itemIndex = 0; itemIndex < list.Items.Count; itemIndex++)
                {
                    if (!TryValidateExpression(list.Items[itemIndex], callables, out failureReason))
                    {
                        failureReason = $"List item {itemIndex}: {failureReason}";
                        return false;
                    }
                }

                failureReason = string.Empty;
                return true;

            case SequenceLiteralExpressionNode sequence:
                for (var itemIndex = 0; itemIndex < sequence.Items.Count; itemIndex++)
                {
                    if (!TryValidateExpression(sequence.Items[itemIndex], callables, out failureReason))
                    {
                        failureReason = $"Sequence item {itemIndex}: {failureReason}";
                        return false;
                    }
                }

                failureReason = string.Empty;
                return true;

            case SetLiteralExpressionNode set:
                for (var itemIndex = 0; itemIndex < set.Items.Count; itemIndex++)
                {
                    if (!TryValidateExpression(set.Items[itemIndex], callables, out failureReason))
                    {
                        failureReason = $"Set item {itemIndex}: {failureReason}";
                        return false;
                    }
                }

                failureReason = string.Empty;
                return true;

            case DictionaryLiteralExpressionNode dictionary:
                for (var entryIndex = 0; entryIndex < dictionary.Entries.Count; entryIndex++)
                {
                    var entry = dictionary.Entries[entryIndex];
                    if (!TryValidateExpression(entry.Value, callables, out failureReason))
                    {
                        failureReason = $"Dictionary entry '{entry.Key}': {failureReason}";
                        return false;
                    }
                }

                failureReason = string.Empty;
                return true;

            case UnaryExpressionNode unary:
                if (!IsKnownUnaryOperator(unary.Operator))
                {
                    failureReason = $"Unary operator '{unary.Operator}' is not currently supported.";
                    return false;
                }

                if (!TryValidateExpression(unary.Operand, callables, out failureReason))
                {
                    failureReason = $"Unary operand: {failureReason}";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            case VariadicTaggedExpressionNode variadic:
                if (!IsKnownVariadicTaggedOperator(variadic.Operator))
                {
                    failureReason = $"Variadic operator '{variadic.Operator}' is not currently supported.";
                    return false;
                }

                for (var argumentIndex = 0; argumentIndex < variadic.Arguments.Count; argumentIndex++)
                {
                    if (!TryValidateExpression(variadic.Arguments[argumentIndex], callables, out failureReason))
                    {
                        failureReason = $"Variadic argument {argumentIndex}: {failureReason}";
                        return false;
                    }
                }

                failureReason = string.Empty;
                return true;

            case ClampExpressionNode clamp:
                if (!TryValidateExpression(clamp.Value, callables, out failureReason))
                {
                    failureReason = $"Clamp value: {failureReason}";
                    return false;
                }

                if (!TryValidateExpression(clamp.Minimum, callables, out failureReason))
                {
                    failureReason = $"Clamp minimum: {failureReason}";
                    return false;
                }

                if (!TryValidateExpression(clamp.Maximum, callables, out failureReason))
                {
                    failureReason = $"Clamp maximum: {failureReason}";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            case RandomExpressionNode random:
                if (!TryValidateExpression(random.FromExpression, callables, out failureReason))
                {
                    failureReason = $"Random start: {failureReason}";
                    return false;
                }

                if (!TryValidateExpression(random.ToExpression, callables, out failureReason))
                {
                    failureReason = $"Random end: {failureReason}";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            case RangeExpressionNode range:
                return TryValidateRange(range, callables, out failureReason);

            case DiceExpressionNode:
                failureReason = string.Empty;
                return true;

            case SeededRandomExpressionNode seededRandom:
                if (!TryValidateExpression(seededRandom.SeedExpression, callables, out failureReason))
                {
                    failureReason = $"Seeded random seed: {failureReason}";
                    return false;
                }

                if (!TryValidateExpression(seededRandom.BodyExpression, callables, out failureReason))
                {
                    failureReason = $"Seeded random body: {failureReason}";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            case GeneratedCollectionExpressionNode generatedCollection:
                if (!TryValidateIterationSource(generatedCollection.Source, callables, out failureReason))
                {
                    failureReason = $"Generated collection source: {failureReason}";
                    return false;
                }

                if (generatedCollection.Predicate is not null &&
                    !TryValidateExpression(generatedCollection.Predicate, callables, out failureReason))
                {
                    failureReason = $"Generated collection predicate: {failureReason}";
                    return false;
                }

                if (!TryValidateExpression(generatedCollection.Projection, callables, out failureReason))
                {
                    failureReason = $"Generated collection projection: {failureReason}";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            case GuardedChoiceExpressionNode guardedChoice:
                for (var branchIndex = 0; branchIndex < guardedChoice.Branches.Count; branchIndex++)
                {
                    var branch = guardedChoice.Branches[branchIndex];
                    if (!TryValidateExpression(branch.ConditionExpression, callables, out failureReason))
                    {
                        failureReason = $"Guarded choice branch {branchIndex} condition: {failureReason}";
                        return false;
                    }

                    if (!TryValidateExpression(branch.ValueExpression, callables, out failureReason))
                    {
                        failureReason = $"Guarded choice branch {branchIndex} value: {failureReason}";
                        return false;
                    }
                }

                if (!TryValidateExpression(guardedChoice.OtherwiseExpression, callables, out failureReason))
                {
                    failureReason = $"Guarded choice otherwise: {failureReason}";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            case BinaryExpressionNode binary:
                if (!IsKnownBinaryOperator(binary.Operator))
                {
                    failureReason = $"Binary operator '{binary.Operator}' is not currently supported.";
                    return false;
                }

                if (!TryValidateExpression(binary.Left, callables, out failureReason))
                {
                    failureReason = $"Binary left operand: {failureReason}";
                    return false;
                }

                if (!TryValidateExpression(binary.Right, callables, out failureReason))
                {
                    failureReason = $"Binary right operand: {failureReason}";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            case RulePredicateExpressionNode rulePredicate:
                if (!TryValidateExpression(rulePredicate.Value, callables, out failureReason))
                {
                    failureReason = $"Rule predicate value: {failureReason}";
                    return false;
                }

                if (!callables.TryGetValue(rulePredicate.RuleName, out var callable))
                {
                    failureReason = $"Rule '{rulePredicate.RuleName}' was not found.";
                    return false;
                }

                if (callable.Kind != GameEventScriptCallableKind.Rule)
                {
                    failureReason = $"Callable '{rulePredicate.RuleName}' is a {callable.Kind}, not a rule.";
                    return false;
                }

                if (callable.Parameters.Count != 1)
                {
                    failureReason = $"Rule '{rulePredicate.RuleName}' has {callable.Parameters.Count} parameters; only unary rule predicates are supported.";
                    return false;
                }

                if (!TryValidateExpression(callable.Expression, callables, out failureReason))
                {
                    failureReason = $"Rule '{rulePredicate.RuleName}' expression: {failureReason}";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            case ExtensionPredicateExpressionNode extensionPredicate:
                if (!TryValidateExpression(extensionPredicate.Value, callables, out failureReason))
                {
                    failureReason = $"Extension predicate value: {failureReason}";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            case CallExpressionNode call:
                if (!callables.TryGetValue(call.Name, out var called))
                {
                    foreach (var argument in call.ArgumentList.Arguments)
                    {
                        if (!TryValidateExpression(argument.Expression, callables, out failureReason))
                        {
                            failureReason = $"Handler bind argument: {failureReason}";
                            return false;
                        }
                    }

                    failureReason = string.Empty;
                    return true;
                }

                if (called.Parameters.Count != call.Arguments.Count)
                {
                    failureReason = $"Callable '{call.Name}' expects {called.Parameters.Count} arguments but received {call.Arguments.Count}.";
                    return false;
                }

                for (var argumentIndex = 0; argumentIndex < call.Arguments.Count; argumentIndex++)
                {
                    if (!TryValidateExpression(call.Arguments[argumentIndex], callables, out failureReason))
                    {
                        failureReason = $"Call argument {argumentIndex}: {failureReason}";
                        return false;
                    }
                }

                if (!TryValidateExpression(called.Expression, callables, out failureReason))
                {
                    failureReason = $"Callable '{call.Name}' expression: {failureReason}";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            case TypeCastExpressionNode typeCast:
                if (!TryValidateExpression(typeCast.Value, callables, out failureReason))
                {
                    failureReason = $"Type cast value: {failureReason}";
                    return false;
                }

                if (!IsKnownTypeCast(typeCast.TypeName))
                {
                    failureReason = $"Type cast '{typeCast.TypeName}' is not currently supported.";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            case TypeCheckExpressionNode typeCheck:
                if (!TryValidateExpression(typeCheck.Value, callables, out failureReason))
                {
                    failureReason = $"Type check value: {failureReason}";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            case MemberAccessExpressionNode memberAccess:
                if (!TryValidateExpression(memberAccess.Target, callables, out failureReason))
                {
                    failureReason = $"Member access target: {failureReason}";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            case CollectionAccessExpressionNode collectionAccess:
                if (TryValidatePipelinedCollection(collectionAccess, callables, out failureReason) ||
                    TryValidateIndexedCollectionAccess(collectionAccess, callables, out failureReason))
                {
                    failureReason = string.Empty;
                    return true;
                }

                return false;

            default:
                failureReason = $"Expression '{expression.GetType().Name}' is not currently supported.";
                return false;
        }
    }

    private static bool IsKnownBinaryOperator(string operation)
        => operation is "+" or "-" or "*" or "/" or "div" or "mod" or "rem" or
            "=" or "==" or "<>" or "<" or ">" or "<=" or ">=" or
            "&" or "|" or "^" or "default" or "in" or "value in" or
            "starts with" or "ends with" or
            "intersect" or "combine" or "merge" or "except" or "zip";

    private static bool IsKnownUnaryOperator(string operation)
        => operation is "-" or "!" or "has value" or "empty" or
            "len" or "chance" or "keys" or "values" or "entries" or
            "abs";

    private static bool IsKnownVariadicTaggedOperator(string operation)
        => operation is "min" or "max";

    private static bool IsKnownTypeCast(string typeName)
        => typeName is "boolean" or "integer" or "decimal" or "number" or "percentage" or "degree" or "meter" or "second" or "sequence";

    private static bool IsKnownDeclaredType(string typeName)
        => typeName is "nothing" or "tag" or "text" or
            "percentage" or "degree" or "meter" or "second" or
            "vector2" or "vector3" or
            "boolean" or "integer" or "decimal" or "number" or
            "sequence" or "list" or "range" or "message" or "handler" or
            "dictionary" or "set" or "dice" or "optional" ||
            !string.IsNullOrWhiteSpace(typeName);

    private static bool TryValidateIterationSource(
        IterationSourceNode source,
        IReadOnlyDictionary<string, GameEventScriptCallableDefinition> callables,
        out string failureReason)
    {
        switch (source)
        {
            case CollectionIterationSourceNode collection:
                if (!TryValidateExpression(collection.Expression, callables, out failureReason))
                {
                    failureReason = $"Collection source expression: {failureReason}";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            case RangeIterationSourceNode range:
                return TryValidateRange(range.RangeExpression, callables, out failureReason);

            default:
                failureReason = $"Iteration source '{source.GetType().Name}' is not supported.";
                return false;
        }
    }

    private static bool TryValidatePipelinedCollection(
        CollectionAccessExpressionNode expression,
        IReadOnlyDictionary<string, GameEventScriptCallableDefinition> callables,
        out string failureReason)
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
            failureReason = "Collection access did not contain a selector.";
            return false;
        }

        if (!TryValidateExpression(source, callables, out failureReason))
        {
            failureReason = $"Collection source: {failureReason}";
            return false;
        }

        for (var i = 0; i < selectors.Count; i++)
        {
            var isTerminal = i == selectors.Count - 1;
            if (!TryValidateSelector(selectors[i], isTerminal, callables, out failureReason))
            {
                failureReason = $"Collection selector {i}: {failureReason}";
                return false;
            }
        }

        failureReason = string.Empty;
        return true;
    }

    private static bool TryValidateIndexedCollectionAccess(
        CollectionAccessExpressionNode expression,
        IReadOnlyDictionary<string, GameEventScriptCallableDefinition> callables,
        out string failureReason)
    {
        if (expression.Selector is not ExpressionSelectorNode selector)
        {
            failureReason = $"Collection selector '{expression.Selector.GetType().Name}' is not a direct index/key expression.";
            return false;
        }

        if (!TryValidateExpression(expression.Target, callables, out failureReason))
        {
            failureReason = $"Collection access target: {failureReason}";
            return false;
        }

        if (!TryValidateExpression(selector.Expression, callables, out failureReason))
        {
            failureReason = $"Collection access selector: {failureReason}";
            return false;
        }

        failureReason = string.Empty;
        return true;
    }

    private static bool TryValidateSelector(
        CollectionSelectorNode selector,
        bool isTerminal,
        IReadOnlyDictionary<string, GameEventScriptCallableDefinition> callables,
        out string failureReason)
    {
        switch (selector)
        {
            case FilterSelectorNode filter:
                if (!TryValidateExpression(filter.Predicate, callables, out failureReason))
                {
                    failureReason = $"Filter predicate: {failureReason}";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            case SelectSelectorNode select:
                if (!TryValidateExpression(select.Projection, callables, out failureReason))
                {
                    failureReason = $"Select projection: {failureReason}";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            case PredicateSelectorNode predicate when isTerminal:
                if (!TryValidateExpression(predicate.Predicate, callables, out failureReason))
                {
                    failureReason = $"Predicate selector predicate: {failureReason}";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            case SumSelectorNode sum when isTerminal:
                if (!TryValidateExpression(sum.Projection, callables, out failureReason))
                {
                    failureReason = $"Sum projection: {failureReason}";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            case AverageSelectorNode average when isTerminal:
                if (!TryValidateExpression(average.Projection, callables, out failureReason))
                {
                    failureReason = $"Average projection: {failureReason}";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            case CountSelectorNode count when isTerminal:
                if (!TryValidateExpression(count.Predicate, callables, out failureReason))
                {
                    failureReason = $"Count predicate: {failureReason}";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            case EdgeSelectorNode edge when isTerminal:
                if (edge.Predicate is not null &&
                    !TryValidateExpression(edge.Predicate, callables, out failureReason))
                {
                    failureReason = $"Edge predicate: {failureReason}";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            case PatternSelectorNode pattern when isTerminal:
                if (!TryValidateDicePattern(pattern.Pattern, callables, out failureReason))
                {
                    failureReason = $"Pattern selector: {failureReason}";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            case ObjectMatchSelectorNode objectMatch when isTerminal:
                if (!TryValidateObjectMatchPattern(objectMatch.Pattern, callables, out failureReason))
                {
                    failureReason = $"Object match selector: {failureReason}";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            case TakePatternSelectorNode takePattern when isTerminal:
                if (!TryValidateDicePattern(takePattern.Pattern, callables, out failureReason))
                {
                    failureReason = $"Take pattern selector: {failureReason}";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            case MinSelectorNode min when isTerminal:
                if (!TryValidateExpression(min.Projection, callables, out failureReason))
                {
                    failureReason = $"Min projection: {failureReason}";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            case MaxSelectorNode max when isTerminal:
                if (!TryValidateExpression(max.Projection, callables, out failureReason))
                {
                    failureReason = $"Max projection: {failureReason}";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            case DictionarySelectorNode dictionary when isTerminal:
                if (!TryValidateExpression(dictionary.KeyProjection, callables, out failureReason))
                {
                    failureReason = $"Dictionary key projection: {failureReason}";
                    return false;
                }

                if (dictionary.ValueProjection is not null &&
                    !TryValidateExpression(dictionary.ValueProjection, callables, out failureReason))
                {
                    failureReason = $"Dictionary value projection: {failureReason}";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            case ContainsSelectorNode contains when isTerminal:
                if (!TryValidateExpression(contains.ValueExpression, callables, out failureReason))
                {
                    failureReason = $"Contains value: {failureReason}";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            case ChooseSelectorNode choose when isTerminal:
                if (choose.Predicate is not null &&
                    !TryValidateExpression(choose.Predicate, callables, out failureReason))
                {
                    failureReason = $"Choose predicate: {failureReason}";
                    return false;
                }

                if (choose.WeightExpression is not null &&
                    !TryValidateExpression(choose.WeightExpression, callables, out failureReason))
                {
                    failureReason = $"Choose weight: {failureReason}";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            case DrawSelectorNode when isTerminal:
            case ShuffleSelectorNode when isTerminal:
            case SortSelectorNode when isTerminal:
            case ReverseSelectorNode when isTerminal:
            case SequenceSliceSelectorNode when isTerminal:
                failureReason = string.Empty;
                return true;

            case DistinctSelectorNode distinct when isTerminal:
                if (distinct.Projection is not null &&
                    !TryValidateExpression(distinct.Projection, callables, out failureReason))
                {
                    failureReason = $"Distinct projection: {failureReason}";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            case GroupBySelectorNode groupBy when isTerminal:
                if (!TryValidateExpression(groupBy.Projection, callables, out failureReason))
                {
                    failureReason = $"Group projection: {failureReason}";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            case OrderBySelectorNode orderBy when isTerminal:
                if (!TryValidateExpression(orderBy.Projection, callables, out failureReason))
                {
                    failureReason = $"Order projection: {failureReason}";
                    return false;
                }

                failureReason = string.Empty;
                return true;

            default:
                failureReason = $"Selector '{selector.GetType().Name}' is not supported as a {(isTerminal ? "terminal" : "prefix")} BytecodeVM selector.";
                return false;
        }
    }

    private static bool TryValidateDicePattern(
        DicePatternNode pattern,
        IReadOnlyDictionary<string, GameEventScriptCallableDefinition> callables,
        out string failureReason)
    {
        if (pattern is DiceCountPatternNode { Face: { } face } &&
            !TryValidateExpression(face, callables, out failureReason))
        {
            failureReason = $"Dice count face: {failureReason}";
            return false;
        }

        failureReason = string.Empty;
        return true;
    }

    private static bool TryValidateObjectMatchPattern(
        ObjectMatchPatternNode pattern,
        IReadOnlyDictionary<string, GameEventScriptCallableDefinition> callables,
        out string failureReason)
    {
        foreach (var entry in pattern.Entries)
        {
            switch (entry.Value)
            {
                case ObjectMatchExpressionValueNode expressionValue:
                    if (!TryValidateExpression(expressionValue.Expression, callables, out failureReason))
                    {
                        failureReason = $"Object field '{entry.Key}': {failureReason}";
                        return false;
                    }

                    break;

                case ObjectMatchNestedValueNode nestedValue:
                    if (!TryValidateObjectMatchPattern(nestedValue.Pattern, callables, out failureReason))
                    {
                        failureReason = $"Object field '{entry.Key}': {failureReason}";
                        return false;
                    }

                    break;
            }
        }

        failureReason = string.Empty;
        return true;
    }

    private sealed class SlotCollector(
        IReadOnlyDictionary<string, GameEventScriptCallableDefinition> callables,
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

                case TypeConstructorExpressionNode typeConstructor:
                    foreach (var argument in typeConstructor.Arguments)
                    {
                        CollectExpression(argument.Expression);
                    }

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
        IReadOnlyDictionary<string, GameEventScriptCallableDefinition> callables,
        Func<GameEventScriptExtensionReference, int>? externalReferenceResolver)
    {
        private readonly IReadOnlyDictionary<string, GameEventScriptCallableDefinition> _callables = callables;
        private readonly Func<GameEventScriptExtensionReference, int>? _externalReferenceResolver = externalReferenceResolver;
        private readonly Dictionary<ExpressionNode, BytecodeVmExpressionProgram> _expressionPrograms = new(ReferenceEqualityComparer<ExpressionNode>.Instance);
        private readonly Dictionary<PublishStatementNode, BytecodeVmPublishLayout> _publishLayouts = new(ReferenceEqualityComparer<PublishStatementNode>.Instance);

        public IReadOnlyDictionary<ExpressionNode, BytecodeVmExpressionProgram> ExpressionPrograms => _expressionPrograms;

        public IReadOnlyDictionary<PublishStatementNode, BytecodeVmPublishLayout> PublishLayouts => _publishLayouts;

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

        private BytecodeVmExpressionProgram CompileExpression(ExpressionNode expression)
        {
            if (_expressionPrograms.TryGetValue(expression, out var program))
            {
                return program;
            }

            var instructions = new List<BytecodeVmProgramInstruction>();
            var builder = new ExpressionBuilder(this, instructions);
            builder.EmitExpression(expression);
            program = new BytecodeVmExpressionProgram(instructions.ToArray(), Math.Max(1, builder.MaxStackDepth));
            _expressionPrograms[expression] = program;
            MaxStackDepth = Math.Max(MaxStackDepth, program.MaxStackDepth + 8);
            return program;
        }

        private BytecodeVmPublishLayout CompilePublishLayout(PublishStatementNode publish)
        {
            if (_publishLayouts.TryGetValue(publish, out var layout))
            {
                return layout;
            }

            if (publish.MessageExpression is not MessageLiteralExpressionNode message)
            {
                throw new GameEventScriptCompilationException("BytecodeVM execution planner does not support this publish expression.");
            }

            var argumentNames = new string[message.Arguments.Count];
            var argumentPrograms = new BytecodeVmExpressionProgram[message.Arguments.Count];
            for (var argumentIndex = 0; argumentIndex < message.Arguments.Count; argumentIndex++)
            {
                var argument = message.Arguments[argumentIndex];
                argumentNames[argumentIndex] = argument.Name;
                argumentPrograms[argumentIndex] = CompileExpression(argument.Expression);
            }

            layout = new BytecodeVmPublishLayout(
                GameEventScriptMessageSignature.NormalizeMessageName(message.Message),
                GameEventScriptMessageSignature.CreateSignatureId(message.Message, argumentNames),
                argumentNames,
                argumentPrograms);
            _publishLayouts[publish] = layout;
            return layout;
        }

        private bool TryGetSlot(string name, out int slot)
            => slots.TryGetValue(name, out slot);

        private bool TryGetCastKind(string typeName, out BytecodeVmCastKind kind)
        {
            kind = typeName switch
            {
                "boolean" => BytecodeVmCastKind.Boolean,
                "integer" => BytecodeVmCastKind.Integer,
                "decimal" => BytecodeVmCastKind.Decimal,
                "number" => BytecodeVmCastKind.Number,
                "percentage" => BytecodeVmCastKind.Percentage,
                "degree" => BytecodeVmCastKind.Degree,
                "meter" => BytecodeVmCastKind.Meter,
                "second" => BytecodeVmCastKind.Second,
                "sequence" => BytecodeVmCastKind.Sequence,
                _ => default
            };

            return IsKnownTypeCast(typeName);
        }

        private sealed class ExpressionBuilder(ProgramCompiler compiler, List<BytecodeVmProgramInstruction> instructions)
        {
            private int _stackDepth;

            public int MaxStackDepth { get; private set; }

            public void EmitExpression(ExpressionNode expression)
            {
                switch (expression)
                {
                    case BooleanLiteralExpressionNode boolean:
                        EmitLoadConstant(BytecodeVmValue.Boolean(boolean.Value));
                        return;

                    case IntegerLiteralExpressionNode integer:
                        EmitLoadConstant(BytecodeVmValue.Integer(integer.Value));
                        return;

                    case DecimalLiteralExpressionNode decimalLiteral:
                        EmitLoadConstant(BytecodeVmValue.Decimal(decimalLiteral.Value));
                        return;

                    case PercentageLiteralExpressionNode percentage:
                        EmitLoadConstant(BytecodeVmValue.Percentage(percentage.PercentValue / 100m));
                        return;

                    case UnitDecimalLiteralExpressionNode unitDecimal:
                        EmitLoadConstant(GameEventScriptDecimalUnits.TryParseTypeName(unitDecimal.UnitName, out var unit)
                            ? BytecodeVmValue.Decimal(unitDecimal.Value, unit)
                            : BytecodeVmValue.NaN());
                        return;

                    case TextLiteralExpressionNode text:
                        EmitLoadConstant(BytecodeVmValue.Reference(GameEventScriptValueFactory.GesText(text.Value)));
                        return;

                    case TagLiteralExpressionNode tag:
                        EmitLoadConstant(BytecodeVmValue.Reference(GameEventScriptValueFactory.GesTag(tag.Name)));
                        return;

                    case HandlerLiteralExpressionNode handler:
                    {
                        var parameterNames = handler.SignatureLabels.ToArray();
                        EmitLoadConstant(BytecodeVmValue.Reference(GameEventScriptValueFactory.GesHandler(GameEventScriptMessageSignature.Create(handler.Message, parameterNames))));
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

                        instructions.Add(new BytecodeVmProgramInstruction(
                            BytecodeVmProgramOpCode.BuildMessage,
                            A: message.Arguments.Count,
                            DiagnosticName: GameEventScriptMessageSignature.NormalizeMessageName(message.Message),
                            DiagnosticArgumentName: GameEventScriptMessageSignature.CreateSignatureId(message.Message, argumentNames),
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

                        instructions.Add(new BytecodeVmProgramInstruction(
                            BytecodeVmProgramOpCode.BindHandler,
                            A: handlerBind.Arguments.Count,
                            Names: bindArgumentNames));
                        CollapseValuesToSingle(handlerBind.Arguments.Count + 1);
                        return;

                    case ExtensionCallExpressionNode extensionCall:
                        var extensionArgumentNames = new string[extensionCall.Arguments.Count];
                        for (var argumentIndex = 0; argumentIndex < extensionCall.Arguments.Count; argumentIndex++)
                        {
                            var argument = extensionCall.Arguments[argumentIndex];
                            extensionArgumentNames[argumentIndex] = argument.Name;
                            EmitExpression(argument.Expression);
                        }

                        var extensionReferenceIndex = ResolveExternalReference(
                            extensionCall.ExtensionName,
                            extensionCall.FunctionName,
                            extensionArgumentNames);
                        instructions.Add(new BytecodeVmProgramInstruction(
                            BytecodeVmProgramOpCode.CallExtension,
                            A: extensionCall.Arguments.Count,
                            B: extensionReferenceIndex,
                            DiagnosticName: extensionCall.ExtensionName,
                            DiagnosticArgumentName: extensionCall.FunctionName,
                            Names: extensionArgumentNames));
                        CollapseValuesToSingle(extensionCall.Arguments.Count);
                        return;

                    case ListLiteralExpressionNode list:
                        foreach (var item in list.Items)
                        {
                            EmitExpression(item);
                        }

                        instructions.Add(new BytecodeVmProgramInstruction(BytecodeVmProgramOpCode.BuildList, A: list.Items.Count));
                        CollapseValuesToSingle(list.Items.Count);
                        return;

                    case SequenceLiteralExpressionNode sequence:
                        foreach (var item in sequence.Items)
                        {
                            EmitExpression(item);
                        }

                        instructions.Add(new BytecodeVmProgramInstruction(BytecodeVmProgramOpCode.BuildSequence, A: sequence.Items.Count));
                        CollapseValuesToSingle(sequence.Items.Count);
                        return;

                    case SetLiteralExpressionNode set:
                        foreach (var item in set.Items)
                        {
                            EmitExpression(item);
                        }

                        instructions.Add(new BytecodeVmProgramInstruction(BytecodeVmProgramOpCode.BuildSet, A: set.Items.Count));
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

                        instructions.Add(new BytecodeVmProgramInstruction(
                            BytecodeVmProgramOpCode.BuildDictionary,
                            A: dictionary.Entries.Count,
                            Names: names));
                        CollapseValuesToSingle(dictionary.Entries.Count);
                        return;

                    case IdentifierExpressionNode identifier:
                        if (!compiler.TryGetSlot(identifier.Name, out var slot))
                        {
                            throw new InvalidOperationException($"Missing BytecodeVM local slot '{identifier.Name}'.");
                        }

                        instructions.Add(new BytecodeVmProgramInstruction(BytecodeVmProgramOpCode.LoadSlot, slot));
                        Push();
                        return;

                    case UnaryExpressionNode unary:
                        EmitExpression(unary.Operand);
                        instructions.Add(new BytecodeVmProgramInstruction(
                            BytecodeVmProgramOpCode.Unary,
                            DiagnosticName: unary.Operator));
                        return;

                    case VariadicTaggedExpressionNode variadic:
                        foreach (var argument in variadic.Arguments)
                        {
                            EmitExpression(argument);
                        }

                        instructions.Add(new BytecodeVmProgramInstruction(
                            BytecodeVmProgramOpCode.Variadic,
                            A: variadic.Arguments.Count,
                            DiagnosticName: variadic.Operator));
                        CollapseValuesToSingle(variadic.Arguments.Count);
                        return;

                    case ClampExpressionNode clamp:
                        EmitExpression(clamp.Value);
                        EmitExpression(clamp.Minimum);
                        EmitExpression(clamp.Maximum);
                        instructions.Add(new BytecodeVmProgramInstruction(BytecodeVmProgramOpCode.Clamp));
                        CollapseValuesToSingle(3);
                        return;

                    case RandomExpressionNode random:
                        EmitExpression(random.FromExpression);
                        EmitExpression(random.ToExpression);
                        instructions.Add(new BytecodeVmProgramInstruction(BytecodeVmProgramOpCode.Random));
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

                        instructions.Add(new BytecodeVmProgramInstruction(BytecodeVmProgramOpCode.Range, A: rangeValueCount));
                        CollapseValuesToSingle(rangeValueCount);
                        return;

                    case DiceExpressionNode dice:
                        instructions.Add(new BytecodeVmProgramInstruction(
                            BytecodeVmProgramOpCode.Dice,
                            A: dice.DiceCount,
                            B: dice.SideCount));
                        Push();
                        return;

                    case SeededRandomExpressionNode seededRandom:
                        EmitExpression(seededRandom.SeedExpression);
                        var seededBodyProgram = compiler.CompileExpression(seededRandom.BodyExpression);
                        AccountNestedProgram(argumentCount: 1, seededBodyProgram);
                        instructions.Add(new BytecodeVmProgramInstruction(
                            BytecodeVmProgramOpCode.SeededRandom,
                            ExpressionProgram: seededBodyProgram));
                        return;

                    case GeneratedCollectionExpressionNode generatedCollection:
                        instructions.Add(new BytecodeVmProgramInstruction(
                            BytecodeVmProgramOpCode.GeneratedCollection,
                            GeneratedCollectionProgram: CompileGeneratedCollection(generatedCollection)));
                        Push();
                        return;

                    case GuardedChoiceExpressionNode guardedChoice:
                        instructions.Add(new BytecodeVmProgramInstruction(
                            BytecodeVmProgramOpCode.GuardedChoice,
                            GuardedChoiceProgram: CompileGuardedChoice(guardedChoice)));
                        Push();
                        return;

                    case BinaryExpressionNode binary:
                        EmitExpression(binary.Left);
                        EmitExpression(binary.Right);
                        instructions.Add(new BytecodeVmProgramInstruction(ToBinaryOpCode(binary.Operator)));
                        Pop();
                        return;

                    case RulePredicateExpressionNode rulePredicate:
                        if (!compiler._callables.TryGetValue(rulePredicate.RuleName, out var callable) ||
                            callable.Parameters.Count != 1)
                        {
                            throw new GameEventScriptCompilationException($"BytecodeVM execution planner does not support rule predicate '{rulePredicate.RuleName}'.");
                        }

                        if (!compiler.TryGetSlot(callable.Parameters[0], out var parameterSlot))
                        {
                            throw new GameEventScriptCompilationException($"BytecodeVM execution planner does not support rule predicate '{rulePredicate.RuleName}'.");
                        }

                        EmitExpression(rulePredicate.Value);
                        var rulePredicateProgram = compiler.CompileExpression(callable.Expression);
                        AccountNestedProgram(argumentCount: 1, rulePredicateProgram);
                        instructions.Add(new BytecodeVmProgramInstruction(
                            BytecodeVmProgramOpCode.RulePredicate,
                            parameterSlot,
                            ExpressionProgram: rulePredicateProgram,
                            DiagnosticName: callable.Name,
                            DiagnosticArgumentName: callable.Parameters[0]));
                        return;

                    case ExtensionPredicateExpressionNode extensionPredicate:
                        EmitExpression(extensionPredicate.Value);
                        var predicateExtensionReferenceIndex = ResolveExternalReference(
                            extensionPredicate.ExtensionName,
                            extensionPredicate.FunctionName,
                            [GameEventScriptMessageSignature.UnlabeledParameterName]);
                        instructions.Add(new BytecodeVmProgramInstruction(
                            BytecodeVmProgramOpCode.CallExtension,
                            A: 1,
                            B: predicateExtensionReferenceIndex,
                            DiagnosticName: extensionPredicate.ExtensionName,
                            DiagnosticArgumentName: extensionPredicate.FunctionName,
                            Names: [GameEventScriptMessageSignature.UnlabeledParameterName]));
                        return;

                    case CallExpressionNode call:
                        if (!compiler._callables.TryGetValue(call.Name, out var called))
                        {
                            if (!compiler.TryGetSlot(call.Name, out var handlerSlot))
                            {
                                throw new GameEventScriptCompilationException($"BytecodeVM execution planner does not support callable '{call.Name}'.");
                            }

                            var dynamicBindArgumentNames = new string[call.ArgumentList.Count];
                            instructions.Add(new BytecodeVmProgramInstruction(BytecodeVmProgramOpCode.LoadSlot, A: handlerSlot));
                            Push();
                            for (var argumentIndex = 0; argumentIndex < call.ArgumentList.Count; argumentIndex++)
                            {
                                var argument = call.ArgumentList.Arguments[argumentIndex];
                                dynamicBindArgumentNames[argumentIndex] = argument.Name;
                                EmitExpression(argument.Expression);
                            }

                            instructions.Add(new BytecodeVmProgramInstruction(
                                BytecodeVmProgramOpCode.BindHandler,
                                A: call.ArgumentList.Count,
                                Names: dynamicBindArgumentNames));
                            CollapseValuesToSingle(call.ArgumentList.Count + 1);
                            return;
                        }

                        var parameterSlots = new int[called.Parameters.Count];
                        for (var parameterIndex = 0; parameterIndex < called.Parameters.Count; parameterIndex++)
                        {
                            if (!compiler.TryGetSlot(called.Parameters[parameterIndex], out parameterSlots[parameterIndex]))
                            {
                                throw new InvalidOperationException($"Missing BytecodeVM callable parameter slot '{called.Parameters[parameterIndex]}'.");
                            }
                        }

                        foreach (var argument in call.Arguments)
                        {
                            EmitExpression(argument);
                        }

                        var callableProgram = compiler.CompileExpression(called.Expression);
                        AccountNestedProgram(call.Arguments.Count, callableProgram);
                        instructions.Add(new BytecodeVmProgramInstruction(
                            BytecodeVmProgramOpCode.Call,
                            A: call.Arguments.Count,
                            CallableKind: called.Kind == GameEventScriptCallableKind.Rule
                                ? BytecodeVmCallableKind.Rule
                                : BytecodeVmCallableKind.Select,
                            ExpressionProgram: callableProgram,
                            DiagnosticName: called.Name,
                            Names: called.Parameters.ToArray(),
                            Slots: parameterSlots));
                        CollapseValuesToSingle(call.Arguments.Count);
                        return;

                    case TypeCastExpressionNode typeCast:
                        if (!compiler.TryGetCastKind(typeCast.TypeName, out var castKind))
                        {
                            throw new GameEventScriptCompilationException($"BytecodeVM execution planner does not support type cast '{typeCast.TypeName}'.");
                        }

                        EmitExpression(typeCast.Value);
                        instructions.Add(new BytecodeVmProgramInstruction(BytecodeVmProgramOpCode.Cast, CastKind: castKind));
                        return;

                    case TypeConstructorExpressionNode typeConstructor:
                        if (typeConstructor.Arguments.Count == 1 &&
                            typeConstructor.Arguments[0].Label is null &&
                            compiler.TryGetCastKind(typeConstructor.TypeName, out var constructorCastKind))
                        {
                            EmitExpression(typeConstructor.Arguments[0].Expression);
                            instructions.Add(new BytecodeVmProgramInstruction(BytecodeVmProgramOpCode.Cast, CastKind: constructorCastKind));
                            return;
                        }

                        var constructorArgumentNames = new string[typeConstructor.Arguments.Count];
                        for (var argumentIndex = 0; argumentIndex < typeConstructor.Arguments.Count; argumentIndex++)
                        {
                            var argument = typeConstructor.Arguments[argumentIndex];
                            constructorArgumentNames[argumentIndex] = argument.Name;
                            EmitExpression(argument.Expression);
                        }

                        instructions.Add(new BytecodeVmProgramInstruction(
                            BytecodeVmProgramOpCode.TypeConstructor,
                            A: typeConstructor.Arguments.Count,
                            DiagnosticName: typeConstructor.TypeName,
                            Names: constructorArgumentNames));
                        CollapseValuesToSingle(typeConstructor.Arguments.Count);
                        return;

                    case TypeCheckExpressionNode typeCheck:
                        EmitExpression(typeCheck.Value);
                        instructions.Add(new BytecodeVmProgramInstruction(
                            BytecodeVmProgramOpCode.TypeCheck,
                            DiagnosticName: typeCheck.TypeName));
                        return;

                    case MemberAccessExpressionNode memberAccess:
                        EmitExpression(memberAccess.Target);
                        instructions.Add(new BytecodeVmProgramInstruction(
                            BytecodeVmProgramOpCode.MemberAccess,
                            DiagnosticName: memberAccess.Member));
                        return;

                    case CollectionAccessExpressionNode collectionAccess:
                        if (collectionAccess.Selector is ExpressionSelectorNode selector)
                        {
                            EmitExpression(collectionAccess.Target);
                            EmitExpression(selector.Expression);
                            instructions.Add(new BytecodeVmProgramInstruction(BytecodeVmProgramOpCode.IndexedAccess));
                            Pop();
                            return;
                        }

                        instructions.Add(new BytecodeVmProgramInstruction(
                            BytecodeVmProgramOpCode.Pipeline,
                            PipelineProgram: CompilePipeline(collectionAccess)));
                        Push();
                        return;

                    default:
                        throw new GameEventScriptCompilationException($"BytecodeVM execution planner does not support expression node '{expression.GetType().Name}'.");
                }
            }

            private BytecodeVmGeneratedCollectionProgram CompileGeneratedCollection(GeneratedCollectionExpressionNode generatedCollection)
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
                return new BytecodeVmGeneratedCollectionProgram(
                    generatedCollection.CollectionType,
                    RequireSlot(generatedCollection.Identifier),
                    generatedCollection.Source,
                    predicateProgram,
                    projectionProgram);
            }

            private BytecodeVmGuardedChoiceProgram CompileGuardedChoice(GuardedChoiceExpressionNode guardedChoice)
            {
                var valuePrograms = new BytecodeVmExpressionProgram[guardedChoice.Branches.Count];
                var conditionPrograms = new BytecodeVmExpressionProgram[guardedChoice.Branches.Count];
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
                return new BytecodeVmGuardedChoiceProgram(valuePrograms, conditionPrograms, otherwiseProgram);
            }

            private BytecodeVmPipelineProgram CompilePipeline(CollectionAccessExpressionNode expression)
            {
                var selectors = new List<CollectionSelectorNode>();
                ExpressionNode source = expression;
                while (source is CollectionAccessExpressionNode collectionAccess)
                {
                    selectors.Add(collectionAccess.Selector);
                    source = collectionAccess.Target;
                }

                selectors.Reverse();
                var prefixSelectors = new List<BytecodeVmSelectorProgram>(Math.Max(0, selectors.Count - 1));
                for (var i = 0; i < selectors.Count - 1; i++)
                {
                    prefixSelectors.Add(CompileSelector(selectors[i], isTerminal: false));
                }

                return new BytecodeVmPipelineProgram(
                    compiler.CompileExpression(source),
                    prefixSelectors.ToArray(),
                    CompileSelector(selectors[^1], isTerminal: true));
            }

            private BytecodeVmSelectorProgram CompileSelector(CollectionSelectorNode selector, bool isTerminal)
            {
                switch (selector)
                {
                    case FilterSelectorNode filter:
                        return new BytecodeVmSelectorProgram(
                            BytecodeVmSelectorKind.Filter,
                            RequireSlot(filter.Identifier),
                            compiler.CompileExpression(filter.Predicate));

                    case SelectSelectorNode select:
                        return new BytecodeVmSelectorProgram(
                            BytecodeVmSelectorKind.Select,
                            RequireSlot(select.Identifier),
                            compiler.CompileExpression(select.Projection));

                    case PredicateSelectorNode predicate when isTerminal:
                        return new BytecodeVmSelectorProgram(
                            BytecodeVmSelectorKind.Predicate,
                            RequireSlot(predicate.Identifier),
                            compiler.CompileExpression(predicate.Predicate),
                            predicate.Operator);

                    case SumSelectorNode sum when isTerminal:
                        return new BytecodeVmSelectorProgram(
                            BytecodeVmSelectorKind.Sum,
                            RequireSlot(sum.Identifier),
                            compiler.CompileExpression(sum.Projection));

                    case AverageSelectorNode average when isTerminal:
                        return new BytecodeVmSelectorProgram(
                            BytecodeVmSelectorKind.Average,
                            RequireSlot(average.Identifier),
                            compiler.CompileExpression(average.Projection));

                    case CountSelectorNode count when isTerminal:
                        return new BytecodeVmSelectorProgram(
                            BytecodeVmSelectorKind.Count,
                            RequireSlot(count.Identifier),
                            compiler.CompileExpression(count.Predicate));

                    case EdgeSelectorNode edge when isTerminal:
                        return new BytecodeVmSelectorProgram(
                            BytecodeVmSelectorKind.Edge,
                            string.IsNullOrEmpty(edge.Identifier) ? -1 : RequireSlot(edge.Identifier!),
                            edge.Predicate is null ? null : compiler.CompileExpression(edge.Predicate),
                            edge.Mode);

                    case PatternSelectorNode pattern when isTerminal:
                        return new BytecodeVmSelectorProgram(
                            BytecodeVmSelectorKind.Pattern,
                            -1,
                            null,
                            dicePattern: pattern.Pattern);

                    case ObjectMatchSelectorNode objectMatch when isTerminal:
                        return new BytecodeVmSelectorProgram(
                            BytecodeVmSelectorKind.ObjectMatch,
                            -1,
                            null,
                            objectPattern: objectMatch.Pattern);

                    case TakePatternSelectorNode takePattern when isTerminal:
                        return new BytecodeVmSelectorProgram(
                            BytecodeVmSelectorKind.TakePattern,
                            -1,
                            null,
                            dicePattern: takePattern.Pattern);

                    case MinSelectorNode min when isTerminal:
                        return new BytecodeVmSelectorProgram(
                            BytecodeVmSelectorKind.Min,
                            RequireSlot(min.Identifier),
                            compiler.CompileExpression(min.Projection));

                    case MaxSelectorNode max when isTerminal:
                        return new BytecodeVmSelectorProgram(
                            BytecodeVmSelectorKind.Max,
                            RequireSlot(max.Identifier),
                            compiler.CompileExpression(max.Projection));

                    case DictionarySelectorNode dictionary when isTerminal:
                        return new BytecodeVmSelectorProgram(
                            BytecodeVmSelectorKind.Dictionary,
                            RequireSlot(dictionary.Identifier),
                            compiler.CompileExpression(dictionary.KeyProjection),
                            secondaryExpressionProgram: dictionary.ValueProjection is null
                                ? null
                                : compiler.CompileExpression(dictionary.ValueProjection));

                    case ContainsSelectorNode contains when isTerminal:
                        return new BytecodeVmSelectorProgram(
                            BytecodeVmSelectorKind.Contains,
                            -1,
                            compiler.CompileExpression(contains.ValueExpression),
                            contains.Mode);

                    case ChooseSelectorNode choose when isTerminal:
                        return new BytecodeVmSelectorProgram(
                            BytecodeVmSelectorKind.Choose,
                            string.IsNullOrEmpty(choose.Identifier) ? -1 : RequireSlot(choose.Identifier!),
                            choose.Predicate is null ? null : compiler.CompileExpression(choose.Predicate),
                            count: choose.Count,
                            secondaryExpressionProgram: choose.WeightExpression is null
                                ? null
                                : compiler.CompileExpression(choose.WeightExpression),
                            secondaryIdentifierSlot: string.IsNullOrEmpty(choose.WeightIdentifier) ? -1 : RequireSlot(choose.WeightIdentifier!),
                            flag: choose.AtRandom);

                    case DrawSelectorNode draw when isTerminal:
                        return new BytecodeVmSelectorProgram(
                            BytecodeVmSelectorKind.Draw,
                            -1,
                            null,
                            count: draw.Count);

                    case ShuffleSelectorNode when isTerminal:
                        return new BytecodeVmSelectorProgram(
                            BytecodeVmSelectorKind.Shuffle,
                            -1,
                            null);

                    case SortSelectorNode sort when isTerminal:
                        return new BytecodeVmSelectorProgram(
                            BytecodeVmSelectorKind.Sort,
                            -1,
                            null,
                            sort.Direction);

                    case DistinctSelectorNode distinct when isTerminal:
                        return new BytecodeVmSelectorProgram(
                            BytecodeVmSelectorKind.Distinct,
                            string.IsNullOrEmpty(distinct.Identifier) ? -1 : RequireSlot(distinct.Identifier!),
                            distinct.Projection is null ? null : compiler.CompileExpression(distinct.Projection));

                    case GroupBySelectorNode groupBy when isTerminal:
                        return new BytecodeVmSelectorProgram(
                            BytecodeVmSelectorKind.GroupBy,
                            RequireSlot(groupBy.Identifier),
                            compiler.CompileExpression(groupBy.Projection));

                    case OrderBySelectorNode orderBy when isTerminal:
                        return new BytecodeVmSelectorProgram(
                            BytecodeVmSelectorKind.OrderBy,
                            RequireSlot(orderBy.Identifier),
                            compiler.CompileExpression(orderBy.Projection),
                            orderBy.Direction);

                    case ReverseSelectorNode when isTerminal:
                        return new BytecodeVmSelectorProgram(
                            BytecodeVmSelectorKind.Reverse,
                            -1,
                            null);

                    case SequenceSliceSelectorNode slice when isTerminal:
                        return new BytecodeVmSelectorProgram(
                            BytecodeVmSelectorKind.SequenceSlice,
                            -1,
                            null,
                            slice.Operation,
                            secondaryMode: slice.Scope,
                            count: slice.Count);

                    default:
                        throw new GameEventScriptCompilationException($"BytecodeVM execution planner does not support selector node '{selector.GetType().Name}'.");
                }
            }

            private int RequireSlot(string name)
            {
                if (!compiler.TryGetSlot(name, out var slot))
                {
                    throw new InvalidOperationException($"Missing BytecodeVM local slot '{name}'.");
                }

                return slot;
            }

            private void EmitLoadConstant(BytecodeVmValue value)
            {
                instructions.Add(new BytecodeVmProgramInstruction(BytecodeVmProgramOpCode.LoadConstant, Constant: value));
                Push();
            }

            private void Push()
            {
                _stackDepth++;
                MaxStackDepth = Math.Max(MaxStackDepth, _stackDepth);
            }

            private void Pop() => _stackDepth = Math.Max(0, _stackDepth - 1);

            private void AccountNestedProgram(int argumentCount, BytecodeVmExpressionProgram program)
            {
                var nestedStackBaseDepth = Math.Max(0, _stackDepth - argumentCount);
                MaxStackDepth = Math.Max(MaxStackDepth, nestedStackBaseDepth + program.MaxStackDepth);
            }

            private int ResolveExternalReference(string extensionName, string functionName, IReadOnlyList<string> argumentLabels)
                => compiler._externalReferenceResolver?.Invoke(new GameEventScriptExtensionReference(extensionName, functionName, argumentLabels)) ?? -1;

            private void CollapseValuesToSingle(int valueCount)
            {
                if (valueCount == 0)
                {
                    Push();
                    return;
                }

                _stackDepth = Math.Max(1, _stackDepth - valueCount + 1);
            }

            private static BytecodeVmProgramOpCode ToBinaryOpCode(string operation)
                => operation switch
                {
                    "|" => BytecodeVmProgramOpCode.Or,
                    "^" => BytecodeVmProgramOpCode.Xor,
                    "&" => BytecodeVmProgramOpCode.And,
                    "=" or "==" => BytecodeVmProgramOpCode.Equal,
                    "<>" => BytecodeVmProgramOpCode.NotEqual,
                    "<" => BytecodeVmProgramOpCode.Less,
                    ">" => BytecodeVmProgramOpCode.Greater,
                    "<=" => BytecodeVmProgramOpCode.LessOrEqual,
                    ">=" => BytecodeVmProgramOpCode.GreaterOrEqual,
                    "+" => BytecodeVmProgramOpCode.Add,
                    "-" => BytecodeVmProgramOpCode.Subtract,
                    "*" => BytecodeVmProgramOpCode.Multiply,
                    "/" => BytecodeVmProgramOpCode.Divide,
                    "div" => BytecodeVmProgramOpCode.IntegerDivide,
                    "mod" => BytecodeVmProgramOpCode.Modulo,
                    "rem" => BytecodeVmProgramOpCode.Remainder,
                    "default" => BytecodeVmProgramOpCode.Default,
                    "in" => BytecodeVmProgramOpCode.Contains,
                    "value in" => BytecodeVmProgramOpCode.ContainsValue,
                    "starts with" => BytecodeVmProgramOpCode.StartsWith,
                    "ends with" => BytecodeVmProgramOpCode.EndsWith,
                    "intersect" => BytecodeVmProgramOpCode.Intersect,
                    "combine" or "merge" => BytecodeVmProgramOpCode.Combine,
                    "except" => BytecodeVmProgramOpCode.Except,
                    "zip" => BytecodeVmProgramOpCode.Zip,
                    _ => throw new GameEventScriptCompilationException($"BytecodeVM execution planner does not support binary operator '{operation}'.")
                };
        }
    }
}

internal sealed class BytecodeVmExecutionPlan
{
    private readonly IReadOnlyDictionary<string, int> _slots;
    private readonly IReadOnlyDictionary<ExpressionNode, BytecodeVmExpressionProgram> _expressionPrograms;
    private readonly IReadOnlyDictionary<PublishStatementNode, BytecodeVmPublishLayout> _publishLayouts;

    private BytecodeVmExecutionPlan(
        IReadOnlyDictionary<string, int> slots,
        IReadOnlyDictionary<ExpressionNode, BytecodeVmExpressionProgram> expressionPrograms,
        IReadOnlyDictionary<PublishStatementNode, BytecodeVmPublishLayout> publishLayouts,
        int maxStackDepth)
    {
        _slots = slots;
        _expressionPrograms = expressionPrograms;
        _publishLayouts = publishLayouts;
        SlotCount = slots.Count;
        MaxStackDepth = maxStackDepth;
    }

    public int SlotCount { get; }

    public int MaxStackDepth { get; }

    public static BytecodeVmExecutionPlan Create(
        IReadOnlyDictionary<string, int> slots,
        IReadOnlyDictionary<ExpressionNode, BytecodeVmExpressionProgram> expressionPrograms,
        IReadOnlyDictionary<PublishStatementNode, BytecodeVmPublishLayout> publishLayouts,
        int maxStackDepth)
        => new(
            new Dictionary<string, int>(slots, StringComparer.Ordinal),
            new Dictionary<ExpressionNode, BytecodeVmExpressionProgram>(expressionPrograms, ReferenceEqualityComparer<ExpressionNode>.Instance),
            new Dictionary<PublishStatementNode, BytecodeVmPublishLayout>(publishLayouts, ReferenceEqualityComparer<PublishStatementNode>.Instance),
            maxStackDepth);

    public bool TryGetSlot(string name, out int slot)
        => _slots.TryGetValue(name, out slot);

    public bool TryGetExpressionProgram(ExpressionNode expression, out BytecodeVmExpressionProgram program)
        => _expressionPrograms.TryGetValue(expression, out program!);

    public bool TryGetPublishLayout(PublishStatementNode publish, out BytecodeVmPublishLayout layout)
        => _publishLayouts.TryGetValue(publish, out layout!);
}

internal sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
    where T : class
{
    public static ReferenceEqualityComparer<T> Instance { get; } = new();

    public bool Equals(T? x, T? y) => ReferenceEquals(x, y);

    public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
}
