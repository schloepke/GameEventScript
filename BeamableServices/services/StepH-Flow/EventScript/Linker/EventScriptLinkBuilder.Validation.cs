#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.Flow.EventScript.Parser;

namespace StepH.Flow.EventScript.Linker;

public sealed partial class EventScriptLinkBuilder
{
    private sealed class ValidationScope
    {
        private readonly HashSet<string> _variables;

        public ValidationScope(IEnumerable<string>? names = null)
        {
            _variables = names is null
                ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(names, StringComparer.Ordinal);
        }

        public ValidationScope CreateChild() => new();

        public bool ContainsInCurrentScope(string name) => _variables.Contains(name);

        public void Declare(string name) => _variables.Add(name);
    }

    private static void ValidateModule(EventScriptModule eventScriptModule, IReadOnlyDictionary<string, RuleDefinitionNode> ruleDefinitions, IReadOnlyDictionary<string, SelectDefinitionNode> selectDefinitions, List<EventScriptLinkageError> errors)
    {
        foreach (var typeDefinition in eventScriptModule.TypeDefinitions)
        {
            foreach (var field in typeDefinition.Fields)
            {
                if (field.MinimumExpression is not null)
                {
                    ValidateExpressionReferences(eventScriptModule, field.MinimumExpression, ruleDefinitions, selectDefinitions, errors);
                }

                if (field.MaximumExpression is not null)
                {
                    ValidateExpressionReferences(eventScriptModule, field.MaximumExpression, ruleDefinitions, selectDefinitions, errors);
                }

                if (field.ComputedExpression is not null)
                {
                    ValidateExpressionReferences(eventScriptModule, field.ComputedExpression, ruleDefinitions, selectDefinitions, errors);
                }
            }
        }

        foreach (var ruleDefinition in eventScriptModule.RuleDefinitions)
        {
            ValidateExpressionReferences(eventScriptModule, ruleDefinition.Expression, ruleDefinitions, selectDefinitions, errors);
        }

        foreach (var selectDefinition in eventScriptModule.SelectDefinitions)
        {
            ValidateExpressionReferences(eventScriptModule, selectDefinition.Expression, ruleDefinitions, selectDefinitions, errors);
        }

        foreach (var handler in eventScriptModule.Handlers)
        {
            var duplicateParameters = handler.Parameters
                .GroupBy(parameter => parameter, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key);

            foreach (var duplicateParameter in duplicateParameters)
            {
                errors.Add(CreateError(
                    eventScriptModule,
                    $"Handler '{handler.Message}' declares parameter '{duplicateParameter}' more than once",
                    handler.Message,
                    EventScriptSymbolKind.Handler,
                    EventScriptLinkageErrorKind.DuplicateHandlerParameter));
            }

            var handlerScope = new ValidationScope(handler.Parameters);
            foreach (var statement in handler.Statements)
            {
                ValidateStatementReferences(eventScriptModule, statement, ruleDefinitions, selectDefinitions, errors, handlerScope);
            }
        }
    }

    private static void ValidateStatementReferences(
        EventScriptModule moduleContext,
        StatementNode statement,
        IReadOnlyDictionary<string, RuleDefinitionNode> ruleDefinitions,
        IReadOnlyDictionary<string, SelectDefinitionNode> selectDefinitions,
        List<EventScriptLinkageError> errors,
        ValidationScope scope)
    {
        switch (statement)
        {
            case PublishStatementNode publish:
                var duplicateArguments = publish.Arguments
                    .GroupBy(argument => argument.Name, StringComparer.Ordinal)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key);

                foreach (var duplicateArgument in duplicateArguments)
                {
                    errors.Add(CreateError(
                        moduleContext,
                        $"Publish '{publish.Message}' declares argument '{duplicateArgument}' more than once",
                        publish.Message,
                        EventScriptSymbolKind.Handler,
                        EventScriptLinkageErrorKind.DuplicatePublishArgument));
                }

                foreach (var argument in publish.Arguments)
                {
                    ValidateExpressionReferences(moduleContext, argument.Expression, ruleDefinitions, selectDefinitions, errors);
                }

                return;

            case LetStatementNode let:
                ValidateExpressionReferences(moduleContext, let.Expression, ruleDefinitions, selectDefinitions, errors);
                if (scope.ContainsInCurrentScope(let.Identifier))
                {
                    errors.Add(CreateError(
                        moduleContext,
                        $"Variable '{let.Identifier}' is already declared in the current scope",
                        let.Identifier,
                        EventScriptSymbolKind.Variable,
                        EventScriptLinkageErrorKind.DuplicateVariable));
                    return;
                }

                scope.Declare(let.Identifier);
                return;

            case IfStatementNode ifStatement:
                ValidateExpressionReferences(moduleContext, ifStatement.Condition, ruleDefinitions, selectDefinitions, errors);
                ValidateStatementBodyReferences(moduleContext, ifStatement.ThenBody, ruleDefinitions, selectDefinitions, errors, scope);

                if (ifStatement.ElseBody is null)
                {
                    return;
                }

                ValidateStatementBodyReferences(moduleContext, ifStatement.ElseBody, ruleDefinitions, selectDefinitions, errors, scope);

                return;

            case ForStatementNode forStatement:
                ValidateExpressionReferences(moduleContext, forStatement.Source, ruleDefinitions, selectDefinitions, errors);
                var loopScope = scope.CreateChild();
                loopScope.Declare(forStatement.Identifier);
                ValidateStatementBodyReferences(moduleContext, forStatement.Body, ruleDefinitions, selectDefinitions, errors, loopScope);

                return;

            case SeededRandomStatementNode seededRandom:
                ValidateExpressionReferences(moduleContext, seededRandom.SeedExpression, ruleDefinitions, selectDefinitions, errors);
                ValidateStatementBodyReferences(moduleContext, seededRandom.Body, ruleDefinitions, selectDefinitions, errors, scope);

                return;

            case ExpressionStatementNode expressionStatement:
                ValidateExpressionReferences(moduleContext, expressionStatement.Expression, ruleDefinitions, selectDefinitions, errors);
                return;
        }
    }

    private static void ValidateStatementBodyReferences(
        EventScriptModule moduleContext,
        StatementBodyNode body,
        IReadOnlyDictionary<string, RuleDefinitionNode> ruleDefinitions,
        IReadOnlyDictionary<string, SelectDefinitionNode> selectDefinitions,
        List<EventScriptLinkageError> errors,
        ValidationScope parentScope)
    {
        var bodyScope = body.IsBlock ? parentScope.CreateChild() : parentScope;
        foreach (var nested in body.Statements)
        {
            ValidateStatementReferences(moduleContext, nested, ruleDefinitions, selectDefinitions, errors, bodyScope);
        }
    }

    private static void ValidateExpressionReferences(
        EventScriptModule moduleContext,
        ExpressionNode expression,
        IReadOnlyDictionary<string, RuleDefinitionNode> ruleDefinitions,
        IReadOnlyDictionary<string, SelectDefinitionNode> selectDefinitions,
        List<EventScriptLinkageError> errors)
    {
        while (true)
        {
            switch (expression)
            {
                case CallExpressionNode call:
                    ValidateCallExpression(moduleContext, call, ruleDefinitions, selectDefinitions, errors);
                    foreach (var argument in call.Arguments)
                    {
                        ValidateExpressionReferences(moduleContext, argument, ruleDefinitions, selectDefinitions, errors);
                    }

                    return;

                case RulePredicateExpressionNode rulePredicate:
                    if (!ruleDefinitions.TryGetValue(rulePredicate.RuleName, out var ruleDefinition) || ruleDefinition.Parameters.Count != 1)
                    {
                        errors.Add(CreateError(
                            moduleContext,
                            $"Rule '{rulePredicate.RuleName}' must exist and declare exactly one parameter to be used with 'is'",
                            rulePredicate.RuleName,
                            EventScriptSymbolKind.Rule,
                            EventScriptLinkageErrorKind.InvalidRulePredicate));
                    }

                    expression = rulePredicate.Value;
                    continue;

                case UnaryExpressionNode unary:
                    expression = unary.Operand;
                    continue;

                case VariadicTaggedExpressionNode variadic:
                    foreach (var argument in variadic.Arguments)
                    {
                        ValidateExpressionReferences(moduleContext, argument, ruleDefinitions, selectDefinitions, errors);
                    }

                    return;

                case ClampExpressionNode clamp:
                    ValidateExpressionReferences(moduleContext, clamp.Value, ruleDefinitions, selectDefinitions, errors);
                    ValidateExpressionReferences(moduleContext, clamp.Minimum, ruleDefinitions, selectDefinitions, errors);
                    expression = clamp.Maximum;
                    continue;

                case RandomExpressionNode random:
                    ValidateExpressionReferences(moduleContext, random.FromExpression, ruleDefinitions, selectDefinitions, errors);
                    expression = random.ToExpression;
                    continue;

                case SeededRandomExpressionNode seededRandom:
                    ValidateExpressionReferences(moduleContext, seededRandom.SeedExpression, ruleDefinitions, selectDefinitions, errors);
                    expression = seededRandom.BodyExpression;
                    continue;

                case GeneratedCollectionExpressionNode generatedCollection:
                    ValidateExpressionReferences(moduleContext, generatedCollection.FromExpression, ruleDefinitions, selectDefinitions, errors);
                    ValidateExpressionReferences(moduleContext, generatedCollection.ToExpression, ruleDefinitions, selectDefinitions, errors);
                    if (generatedCollection.StepExpression is not null)
                    {
                        ValidateExpressionReferences(moduleContext, generatedCollection.StepExpression, ruleDefinitions, selectDefinitions, errors);
                    }

                    if (generatedCollection.Predicate is not null)
                    {
                        ValidateExpressionReferences(moduleContext, generatedCollection.Predicate, ruleDefinitions, selectDefinitions, errors);
                    }

                    expression = generatedCollection.Projection;
                    continue;

                case GuardedChoiceExpressionNode guardedChoice:
                    foreach (var branch in guardedChoice.Branches)
                    {
                        ValidateExpressionReferences(moduleContext, branch.ValueExpression, ruleDefinitions, selectDefinitions, errors);
                        ValidateExpressionReferences(moduleContext, branch.ConditionExpression, ruleDefinitions, selectDefinitions, errors);
                    }

                    expression = guardedChoice.OtherwiseExpression;
                    continue;

                case BinaryExpressionNode binary:
                    ValidateExpressionReferences(moduleContext, binary.Left, ruleDefinitions, selectDefinitions, errors);
                    expression = binary.Right;
                    continue;

                case TypeCheckExpressionNode typeCheck:
                    expression = typeCheck.Value;
                    continue;

                case TypeCastExpressionNode typeCast:
                    expression = typeCast.Value;
                    continue;

                case MemberAccessExpressionNode memberAccess:
                    expression = memberAccess.Target;
                    continue;

                case CollectionAccessExpressionNode collectionAccess:
                    ValidateExpressionReferences(moduleContext, collectionAccess.Target, ruleDefinitions, selectDefinitions, errors);
                    ValidateCollectionSelectorReferences(moduleContext, collectionAccess.Selector, ruleDefinitions, selectDefinitions, errors);
                    return;

                case ListLiteralExpressionNode list:
                    foreach (var item in list.Items)
                    {
                        ValidateExpressionReferences(moduleContext, item, ruleDefinitions, selectDefinitions, errors);
                    }

                    return;

                case SetLiteralExpressionNode set:
                    foreach (var item in set.Items)
                    {
                        ValidateExpressionReferences(moduleContext, item, ruleDefinitions, selectDefinitions, errors);
                    }

                    return;

                case DictionaryLiteralExpressionNode dictionary:
                    foreach (var entry in dictionary.Entries)
                    {
                        ValidateExpressionReferences(moduleContext, entry.Value, ruleDefinitions, selectDefinitions, errors);
                    }

                    return;
            }

            break;
        }
    }

    private static void ValidateCollectionSelectorReferences(
        EventScriptModule moduleContext,
        CollectionSelectorNode selector,
        IReadOnlyDictionary<string, RuleDefinitionNode> ruleDefinitions,
        IReadOnlyDictionary<string, SelectDefinitionNode> selectDefinitions,
        List<EventScriptLinkageError> errors)
    {
        switch (selector)
        {
            case ExpressionSelectorNode expressionSelector:
                ValidateExpressionReferences(moduleContext, expressionSelector.Expression, ruleDefinitions, selectDefinitions, errors);
                return;
            case PredicateSelectorNode predicateSelector:
                ValidateExpressionReferences(moduleContext, predicateSelector.Predicate, ruleDefinitions, selectDefinitions, errors);
                return;
            case CountSelectorNode countSelector:
                ValidateExpressionReferences(moduleContext, countSelector.Predicate, ruleDefinitions, selectDefinitions, errors);
                return;
            case ChooseSelectorNode chooseSelector:
                if (chooseSelector.Predicate is not null)
                {
                    ValidateExpressionReferences(moduleContext, chooseSelector.Predicate, ruleDefinitions, selectDefinitions, errors);
                }

                if (chooseSelector.WeightExpression is not null)
                {
                    ValidateExpressionReferences(moduleContext, chooseSelector.WeightExpression, ruleDefinitions, selectDefinitions, errors);
                }

                return;
            case EdgeSelectorNode { Predicate: not null } edgeSelector:
                ValidateExpressionReferences(moduleContext, edgeSelector.Predicate, ruleDefinitions, selectDefinitions, errors);
                return;
            case FilterSelectorNode filterSelector:
                ValidateExpressionReferences(moduleContext, filterSelector.Predicate, ruleDefinitions, selectDefinitions, errors);
                return;
            case SumSelectorNode sumSelector:
                ValidateExpressionReferences(moduleContext, sumSelector.Projection, ruleDefinitions, selectDefinitions, errors);
                return;
            case AverageSelectorNode averageSelector:
                ValidateExpressionReferences(moduleContext, averageSelector.Projection, ruleDefinitions, selectDefinitions, errors);
                return;
            case SelectSelectorNode selectSelector:
                ValidateExpressionReferences(moduleContext, selectSelector.Projection, ruleDefinitions, selectDefinitions, errors);
                return;
            case DictionarySelectorNode dictionarySelector:
                ValidateExpressionReferences(moduleContext, dictionarySelector.KeyProjection, ruleDefinitions, selectDefinitions, errors);
                if (dictionarySelector.ValueProjection is not null)
                {
                    ValidateExpressionReferences(moduleContext, dictionarySelector.ValueProjection, ruleDefinitions, selectDefinitions, errors);
                }

                return;
            case MinSelectorNode minSelector:
                ValidateExpressionReferences(moduleContext, minSelector.Projection, ruleDefinitions, selectDefinitions, errors);
                return;
            case MaxSelectorNode maxSelector:
                ValidateExpressionReferences(moduleContext, maxSelector.Projection, ruleDefinitions, selectDefinitions, errors);
                return;
            case ContainsSelectorNode containsSelector:
                ValidateExpressionReferences(moduleContext, containsSelector.ValueExpression, ruleDefinitions, selectDefinitions, errors);
                return;
            case DistinctSelectorNode { Projection: not null } distinctSelector:
                ValidateExpressionReferences(moduleContext, distinctSelector.Projection, ruleDefinitions, selectDefinitions, errors);
                return;
            case GroupBySelectorNode groupBySelector:
                ValidateExpressionReferences(moduleContext, groupBySelector.Projection, ruleDefinitions, selectDefinitions, errors);
                return;
            case OrderBySelectorNode orderBySelector:
                ValidateExpressionReferences(moduleContext, orderBySelector.Projection, ruleDefinitions, selectDefinitions, errors);
                return;
        }
    }

    private static void ValidateCallExpression(
        EventScriptModule moduleContext,
        CallExpressionNode call,
        IReadOnlyDictionary<string, RuleDefinitionNode> ruleDefinitions,
        IReadOnlyDictionary<string, SelectDefinitionNode> selectDefinitions,
        List<EventScriptLinkageError> errors)
    {
        if (ruleDefinitions.TryGetValue(call.Name, out var ruleDefinition))
        {
            ValidateCallArity(moduleContext, "Rule", call.Name, ruleDefinition.Parameters.Count, call.Arguments.Count, errors);
            return;
        }

        if (!selectDefinitions.TryGetValue(call.Name, out var selectDefinition))
        {
            errors.Add(CreateError(
                moduleContext,
                $"No rule or select named '{call.Name}' exists",
                call.Name,
                EventScriptSymbolKind.GlobalDefinition,
                EventScriptLinkageErrorKind.MissingRuleOrSelect));
            return;
        }

        ValidateCallArity(moduleContext, "Select", call.Name, selectDefinition.Parameters.Count, call.Arguments.Count, errors);
    }

    private static void ValidateCallArity(EventScriptModule moduleContext, string kind, string name, int expectedCount, int actualCount, List<EventScriptLinkageError> errors)
    {
        if (expectedCount != actualCount)
        {
            errors.Add(CreateError(
                moduleContext,
                $"{kind} '{name}' expects {expectedCount} argument(s) but received {actualCount}",
                name,
                kind == "Rule" ? EventScriptSymbolKind.Rule : EventScriptSymbolKind.Select,
                kind == "Rule" ? EventScriptLinkageErrorKind.WrongRuleArity : EventScriptLinkageErrorKind.WrongSelectArity));
        }
    }

    private static Dictionary<string, List<EventHandlerNode>> BuildHandlerMap(IReadOnlyList<EventScriptModule> modules, List<EventScriptLinkageError> errors)
    {
        var map = new Dictionary<string, List<EventHandlerNode>>(StringComparer.Ordinal);

        foreach (var module in modules)
        {
            foreach (var handler in module.Handlers)
            {
                if (!map.TryGetValue(handler.Message, out var handlers))
                {
                    handlers = [];
                    map[handler.Message] = handlers;
                }

                handlers.Add(handler);
            }
        }

        return map;
    }

    private static Dictionary<string, TypeDefinitionNode> BuildTypeDefinitionMap(IReadOnlyList<EventScriptModule> modules, List<EventScriptLinkageError> errors)
    {
        var map = new Dictionary<string, TypeDefinitionNode>(StringComparer.Ordinal);
        foreach (var module in modules)
        {
            foreach (var typeDefinition in module.TypeDefinitions)
            {
                if (!map.TryAdd(typeDefinition.Name, typeDefinition))
                {
                    errors.Add(CreateError(
                        module,
                        $"Type '{typeDefinition.Name}' is defined more than once",
                        typeDefinition.Name,
                        EventScriptSymbolKind.Type,
                        EventScriptLinkageErrorKind.DuplicateType));
                }
            }
        }

        return map;
    }

    private static Dictionary<string, RuleDefinitionNode> BuildRuleDefinitionMap(IReadOnlyList<EventScriptModule> modules, List<EventScriptLinkageError> errors)
    {
        var map = new Dictionary<string, RuleDefinitionNode>(StringComparer.Ordinal);
        foreach (var module in modules)
        {
            foreach (var ruleDefinition in module.RuleDefinitions)
            {
                if (!map.TryAdd(ruleDefinition.Name, ruleDefinition))
                {
                    errors.Add(CreateError(
                        module,
                        $"Rule '{ruleDefinition.Name}' is defined more than once",
                        ruleDefinition.Name,
                        EventScriptSymbolKind.Rule,
                        EventScriptLinkageErrorKind.DuplicateRule));
                }
            }
        }

        return map;
    }

    private static Dictionary<string, SelectDefinitionNode> BuildSelectDefinitionMap(IReadOnlyList<EventScriptModule> modules, List<EventScriptLinkageError> errors)
    {
        var map = new Dictionary<string, SelectDefinitionNode>(StringComparer.Ordinal);
        foreach (var module in modules)
        {
            foreach (var selectDefinition in module.SelectDefinitions)
            {
                if (!map.TryAdd(selectDefinition.Name, selectDefinition))
                {
                    errors.Add(CreateError(
                        module,
                        $"Select '{selectDefinition.Name}' is defined more than once",
                        selectDefinition.Name,
                        EventScriptSymbolKind.Select,
                        EventScriptLinkageErrorKind.DuplicateSelect));
                }
            }
        }

        return map;
    }
}
