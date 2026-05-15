#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.Compiler;

internal static class GesBytecodeLowerer
{
    public static IReadOnlyDictionary<string, int> CollectHandlerSlots(
        string messageName,
        int declarationOrder,
        IReadOnlyList<string> parameters,
        IReadOnlyList<StatementNode> statements,
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, TypeDefinitionNode>? typeDefinitions = null)
    {
        if (!TryValidateStatements(statements, callables, out var failureReason))
        {
            throw new GameEventScriptCompileException(
                $"GameEventScript bytecode lowerer does not support handler '{messageName}' #{declarationOrder}: {failureReason}");
        }

        var slotCollector = new SlotCollector(typeDefinitions ?? new Dictionary<string, TypeDefinitionNode>(StringComparer.Ordinal));
        foreach (var parameter in parameters)
        {
            slotCollector.AddSlot(parameter);
        }

        slotCollector.CollectTypeDefinitions();

        foreach (var statement in statements)
        {
            slotCollector.CollectStatement(statement);
        }

        return new Dictionary<string, int>(slotCollector.Slots, StringComparer.Ordinal);
    }

    public static IReadOnlyDictionary<string, GameEventScriptBytecodeTypeDefinition> CompileTypeDefinitions(
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions)
    {
        var result = new Dictionary<string, GameEventScriptBytecodeTypeDefinition>(StringComparer.Ordinal);
        foreach (var typeDefinition in typeDefinitions.Values)
        {
            var fields = new GameEventScriptBytecodeTypeFieldDefinition[typeDefinition.Fields.Count];
            for (var fieldIndex = 0; fieldIndex < typeDefinition.Fields.Count; fieldIndex++)
            {
                var field = typeDefinition.Fields[fieldIndex];
                fields[fieldIndex] = new GameEventScriptBytecodeTypeFieldDefinition(
                    field.Name,
                    field.TypeName);
            }

            result[typeDefinition.Name] = new GameEventScriptBytecodeTypeDefinition(typeDefinition.Name, fields);
        }

        return result;
    }

    public static IReadOnlyDictionary<string, GameEventScriptBytecodeCallable> CompileCallableDefinitions(
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions)
    {
        var result = new Dictionary<string, GameEventScriptBytecodeCallable>(StringComparer.Ordinal);
        foreach (var callable in callables.Values.OrderBy(callable => callable.Name, StringComparer.Ordinal))
        {
            var slotCollector = new SlotCollector(typeDefinitions);
            foreach (var parameter in callable.Parameters)
            {
                slotCollector.AddSlot(parameter);
            }

            slotCollector.CollectTypeDefinitions();
            slotCollector.CollectExpression(callable.Expression);
            result[callable.Name] = new GameEventScriptBytecodeCallable(
                callable.Name,
                callable.Kind == GameEventScriptCallableKind.PredicateCall ? GameEventScriptBytecodeCallableKind.Predicate : GameEventScriptBytecodeCallableKind.Function,
                callable.Parameters,
                callable.SignatureLabels,
                parameterTypes: callable.ParameterList.Select(parameter => parameter.DeclaredType).ToArray());
        }

        return result;
    }

    public static IReadOnlyDictionary<string, int> CollectCallableSlots(
        GesCallableDefinition callable,
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions)
    {
        var slotCollector = new SlotCollector(typeDefinitions);
        foreach (var parameter in callable.Parameters)
        {
            slotCollector.AddSlot(parameter);
        }

        slotCollector.CollectTypeDefinitions();
        slotCollector.CollectExpression(callable.Expression);
        return new Dictionary<string, int>(slotCollector.Slots, StringComparer.Ordinal);
    }

    public static IReadOnlyDictionary<string, int> CollectTypeDefinitionSlots(
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions)
    {
        var slotCollector = new SlotCollector(typeDefinitions);
        slotCollector.CollectTypeDefinitions();
        return new Dictionary<string, int>(slotCollector.Slots, StringComparer.Ordinal);
    }

    private static bool TryValidateStatements(
        IReadOnlyList<StatementNode> statements,
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
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
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
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

                foreach (var tagExpression in publish.TagExpressions)
                {
                    if (!TryValidateExpression(tagExpression, callables, out failureReason))
                    {
                        failureReason = $"Publish tag expression: {failureReason}";
                        return false;
                    }
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
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
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
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
        out string failureReason)
    {
        switch (expression)
        {
            case BooleanLiteralExpressionNode:
            case IntegerLiteralExpressionNode:
            case UnitIntegerLiteralExpressionNode:
            case FloatLiteralExpressionNode:
            case PercentageLiteralExpressionNode:
            case UnitFloatLiteralExpressionNode:
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

            case MapLiteralExpressionNode dictionary:
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
                    failureReason = $"Unary operator '{unary.Operator.ToSourceText()}' is not currently supported.";
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
                    failureReason = $"Binary operator '{binary.Operator.ToSourceText()}' is not currently supported.";
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

            case PredicateCallExpressionNode predicateCall:
                if (!TryValidateExpression(predicateCall.Value, callables, out failureReason))
                {
                    failureReason = $"Predicate test value: {failureReason}";
                    return false;
                }

                if (!callables.TryGetValue(predicateCall.PredicateName, out var callable))
                {
                    failureReason = $"Predicate '{predicateCall.PredicateName}' was not found.";
                    return false;
                }

                if (callable.Kind != GameEventScriptCallableKind.PredicateCall)
                {
                    failureReason = $"Callable '{predicateCall.PredicateName}' is a {callable.Kind}, not a predicate.";
                    return false;
                }

                if (callable.Parameters.Count != 1)
                {
                    failureReason = $"Predicate '{predicateCall.PredicateName}' has {callable.Parameters.Count} parameters; only unary predicate tests are supported.";
                    return false;
                }

                if (!TryValidateExpression(callable.Expression, callables, out failureReason))
                {
                    failureReason = $"Predicate '{predicateCall.PredicateName}' expression: {failureReason}";
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

    private static bool IsKnownBinaryOperator(GesBinaryOperator operation)
        => Enum.IsDefined(typeof(GesBinaryOperator), operation);

    private static bool IsKnownUnaryOperator(GesUnaryOperator operation)
        => Enum.IsDefined(typeof(GesUnaryOperator), operation);

    private static bool IsKnownVariadicTaggedOperator(string operation)
        => operation is "min" or "max";

    private static bool IsKnownTypeCast(string typeName)
        => typeName is "boolean" or "integer" or "float" or "number" or "percentage" or "degree" or "meter" or "second" or "vector" or "point" or "uuid" or "series" or "envelope" or "ref";

    private static bool IsKnownDeclaredType(string typeName)
        => typeName is "nothing" or "tag" or "text" or
            "percentage" or "degree" or "meter" or "second" or
            "vector" or "point" or
            "boolean" or "integer" or "float" or "number" or
            "uuid" or "series" or "envelope" or "list" or "range" or "message" or "handler" or
            "map" or "set" or "dice" ||
            !string.IsNullOrWhiteSpace(typeName);

    private static bool TryValidateIterationSource(
        IterationSourceNode source,
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
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
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
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
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
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
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
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

            case SeriesTermSelectorNode seriesTerm when isTerminal:
                if (!TryValidateExpression(seriesTerm.IndexExpression, callables, out failureReason))
                {
                    failureReason = $"Series term index: {failureReason}";
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

            case MapSelectorNode dictionary when isTerminal:
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
                failureReason = $"Selector '{selector.GetType().Name}' is not supported as a {(isTerminal ? "terminal" : "prefix")} GameEventScript bytecode selector.";
                return false;
        }
    }

    private static bool TryValidateDicePattern(
        DicePatternNode pattern,
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
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
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
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

    private sealed class SlotCollector(IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions)
    {
        private readonly Dictionary<string, int> _slots = new(StringComparer.Ordinal);
        private readonly Stack<HashSet<string>> _scopedLocals = new();

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

        private void EnterScope()
            => _scopedLocals.Push(new HashSet<string>(StringComparer.Ordinal));

        private void ExitScope()
            => _scopedLocals.Pop();

        private void DeclareLocal(string name)
        {
            if (_scopedLocals.Count == 0)
            {
                AddSlot(name);
                return;
            }

            _scopedLocals.Peek().Add(name);
        }

        private bool IsScopedLocal(string name)
            => _scopedLocals.Any(scope => scope.Contains(name));

        private void CollectStatements(IReadOnlyList<StatementNode> statements, bool createsScope)
        {
            if (createsScope)
            {
                EnterScope();
            }

            foreach (var nested in statements)
            {
                CollectStatement(nested);
            }

            if (createsScope)
            {
                ExitScope();
            }
        }

        public void CollectStatement(StatementNode statement)
        {
            switch (statement)
            {
                case LetStatementNode let:
                    CollectExpression(let.Expression);
                    DeclareLocal(let.Identifier);
                    break;

                case IfStatementNode ifStatement:
                    CollectExpression(ifStatement.Condition);
                    CollectStatements(ifStatement.ThenBody.Statements, ifStatement.ThenBody.IsBlock);

                    if (ifStatement.ElseBody is not null)
                    {
                        CollectStatements(ifStatement.ElseBody.Statements, ifStatement.ElseBody.IsBlock);
                    }

                    break;

                case PublishStatementNode publish:
                    CollectExpression(publish.MessageExpression);
                    foreach (var tagExpression in publish.TagExpressions)
                    {
                        CollectExpression(tagExpression);
                    }

                    break;

                case ForStatementNode { Source: RangeIterationSourceNode range } forStatement:
                    CollectRange(range.RangeExpression);
                    EnterScope();
                    DeclareLocal(forStatement.Identifier);
                    CollectStatements(forStatement.Body.Statements, forStatement.Body.IsBlock);
                    ExitScope();

                    break;

                case ForStatementNode { Source: CollectionIterationSourceNode collection } forStatement:
                    CollectExpression(collection.Expression);
                    EnterScope();
                    DeclareLocal(forStatement.Identifier);
                    CollectStatements(forStatement.Body.Statements, forStatement.Body.IsBlock);
                    ExitScope();

                    break;

                case ExpressionStatementNode expressionStatement:
                    CollectExpression(expressionStatement.Expression);
                    break;

                case SeededRandomStatementNode seededRandom:
                    CollectExpression(seededRandom.SeedExpression);
                    CollectStatements(seededRandom.Body.Statements, seededRandom.Body.IsBlock);

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

        public void CollectExpression(ExpressionNode expression)
        {
            switch (expression)
            {
                case IdentifierExpressionNode identifier:
                    if (!IsScopedLocal(identifier.Name))
                    {
                        AddSlot(identifier.Name);
                    }

                    break;

                case MessageLiteralExpressionNode message:
                    foreach (var argument in message.Arguments)
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

                case MapLiteralExpressionNode dictionary:
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
                    CollectIterationSource(generatedCollection.Source);
                    EnterScope();
                    DeclareLocal(generatedCollection.Identifier);
                    try
                    {
                        if (generatedCollection.Predicate is not null)
                        {
                            CollectExpression(generatedCollection.Predicate);
                        }

                        CollectExpression(generatedCollection.Projection);
                    }
                    finally
                    {
                        ExitScope();
                    }

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

                case PredicateCallExpressionNode predicateCall:
                    CollectExpression(predicateCall.Value);
                    break;

                case CallExpressionNode call:
                    foreach (var argument in call.Arguments)
                    {
                        CollectExpression(argument);
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

                case MapSelectorNode dictionary:
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

}

internal sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
    where T : class
{
    public static ReferenceEqualityComparer<T> Instance { get; } = new();

    public bool Equals(T? x, T? y) => ReferenceEquals(x, y);

    public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
}
