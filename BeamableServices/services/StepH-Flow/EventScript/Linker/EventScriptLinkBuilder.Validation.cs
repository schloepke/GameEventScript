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

    private static void ValidateModule(
        EventScriptModule eventScriptModule,
        IReadOnlyDictionary<string, LinkedCallableDefinition> callables,
        List<EventScriptLinkageError> errors)
    {
        foreach (var typeDefinition in eventScriptModule.TypeDefinitions)
        {
            foreach (var field in typeDefinition.Fields)
            {
                ValidateIdentifierCase(
                    eventScriptModule,
                    field.Name,
                    field.Name,
                    EventScriptSymbolKind.Variable,
                    "Field names must use identifier casing (start lowercase and contain only letters or digits)",
                    errors);

                if (field.MinimumExpression is not null)
                {
                    ValidateExpressionReferences(eventScriptModule, field.MinimumExpression, callables, errors);
                }

                if (field.MaximumExpression is not null)
                {
                    ValidateExpressionReferences(eventScriptModule, field.MaximumExpression, callables, errors);
                }

                if (field.ComputedExpression is not null)
                {
                    ValidateExpressionReferences(eventScriptModule, field.ComputedExpression, callables, errors);
                }
            }
        }

        foreach (var ruleDefinition in eventScriptModule.RuleDefinitions)
        {
            ValidateIdentifierCase(
                eventScriptModule,
                ruleDefinition.Name,
                ruleDefinition.Name,
                EventScriptSymbolKind.Rule,
                "Rule names must use identifier casing (start lowercase and contain only letters or digits)",
                errors);

            foreach (var parameter in ruleDefinition.Parameters)
            {
                ValidateIdentifierCase(
                    eventScriptModule,
                    parameter,
                    ruleDefinition.Name,
                    EventScriptSymbolKind.Rule,
                    $"Rule '{ruleDefinition.Name}' declares an invalid parameter name '{parameter}'",
                    errors);
            }

            var duplicateParameters = ruleDefinition.Parameters
                .GroupBy(parameter => parameter, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key);

            foreach (var duplicateParameter in duplicateParameters)
            {
                errors.Add(CreateError(
                    eventScriptModule,
                    $"Rule '{ruleDefinition.Name}' declares parameter '{duplicateParameter}' more than once",
                    ruleDefinition.Name,
                    EventScriptSymbolKind.Rule,
                    EventScriptLinkageErrorKind.DuplicateDefinitionParameter));
            }

            ValidateExpressionReferences(eventScriptModule, ruleDefinition.Expression, callables, errors);
        }

        foreach (var selectDefinition in eventScriptModule.SelectDefinitions)
        {
            ValidateIdentifierCase(
                eventScriptModule,
                selectDefinition.Name,
                selectDefinition.Name,
                EventScriptSymbolKind.Select,
                "Select names must use identifier casing (start lowercase and contain only letters or digits)",
                errors);

            foreach (var parameter in selectDefinition.Parameters)
            {
                ValidateIdentifierCase(
                    eventScriptModule,
                    parameter,
                    selectDefinition.Name,
                    EventScriptSymbolKind.Select,
                    $"Select '{selectDefinition.Name}' declares an invalid parameter name '{parameter}'",
                    errors);
            }

            var duplicateParameters = selectDefinition.Parameters
                .GroupBy(parameter => parameter, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key);

            foreach (var duplicateParameter in duplicateParameters)
            {
                errors.Add(CreateError(
                    eventScriptModule,
                    $"Select '{selectDefinition.Name}' declares parameter '{duplicateParameter}' more than once",
                    selectDefinition.Name,
                    EventScriptSymbolKind.Select,
                    EventScriptLinkageErrorKind.DuplicateDefinitionParameter));
            }

            ValidateExpressionReferences(eventScriptModule, selectDefinition.Expression, callables, errors);
        }

        foreach (var handler in eventScriptModule.Handlers)
        {
            ValidateMessageCase(
                eventScriptModule,
                handler.Message,
                handler.Message,
                EventScriptSymbolKind.Handler,
                "Handler message names must use message casing (start uppercase and contain only letters or digits)",
                errors);

            foreach (var parameter in handler.Parameters)
            {
                ValidateIdentifierCase(
                    eventScriptModule,
                    parameter,
                    handler.Message,
                    EventScriptSymbolKind.Handler,
                    $"Handler '{handler.Message}' declares an invalid parameter name '{parameter}'",
                    errors);
            }

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
                ValidateStatementReferences(eventScriptModule, statement, callables, errors, handlerScope);
            }
        }
    }

    private static void ValidateStatementReferences(
        EventScriptModule moduleContext,
        StatementNode statement,
        IReadOnlyDictionary<string, LinkedCallableDefinition> callables,
        List<EventScriptLinkageError> errors,
        ValidationScope scope)
    {
        switch (statement)
        {
            case PublishStatementNode publish:
                ValidateExpressionReferences(moduleContext, publish.MessageExpression, callables, errors);
                return;

            case LetStatementNode let:
                ValidateExpressionReferences(moduleContext, let.Expression, callables, errors);
                ValidateIdentifierCase(
                    moduleContext,
                    let.Identifier,
                    let.Identifier,
                    EventScriptSymbolKind.Variable,
                    $"Variable '{let.Identifier}' must use identifier casing (start lowercase and contain only letters or digits)",
                    errors);
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
                ValidateExpressionReferences(moduleContext, ifStatement.Condition, callables, errors);
                ValidateStatementBodyReferences(moduleContext, ifStatement.ThenBody, callables, errors, scope);

                if (ifStatement.ElseBody is null)
                {
                    return;
                }

                ValidateStatementBodyReferences(moduleContext, ifStatement.ElseBody, callables, errors, scope);

                return;

            case ForStatementNode forStatement:
                ValidateIterationSourceReferences(moduleContext, forStatement.Source, callables, errors);
                ValidateIdentifierCase(
                    moduleContext,
                    forStatement.Identifier,
                    forStatement.Identifier,
                    EventScriptSymbolKind.Variable,
                    $"Loop variable '{forStatement.Identifier}' must use identifier casing (start lowercase and contain only letters or digits)",
                    errors);
                var loopScope = scope.CreateChild();
                loopScope.Declare(forStatement.Identifier);
                ValidateStatementBodyReferences(moduleContext, forStatement.Body, callables, errors, loopScope);

                return;

            case SeededRandomStatementNode seededRandom:
                ValidateExpressionReferences(moduleContext, seededRandom.SeedExpression, callables, errors);
                ValidateStatementBodyReferences(moduleContext, seededRandom.Body, callables, errors, scope);

                return;

            case ExpressionStatementNode expressionStatement:
                ValidateExpressionReferences(moduleContext, expressionStatement.Expression, callables, errors);
                return;
        }
    }

    private static void ValidateStatementBodyReferences(
        EventScriptModule moduleContext,
        StatementBodyNode body,
        IReadOnlyDictionary<string, LinkedCallableDefinition> callables,
        List<EventScriptLinkageError> errors,
        ValidationScope parentScope)
    {
        var bodyScope = body.IsBlock ? parentScope.CreateChild() : parentScope;
        foreach (var nested in body.Statements)
        {
            ValidateStatementReferences(moduleContext, nested, callables, errors, bodyScope);
        }
    }

    private static void ValidateExpressionReferences(
        EventScriptModule moduleContext,
        ExpressionNode expression,
        IReadOnlyDictionary<string, LinkedCallableDefinition> callables,
        List<EventScriptLinkageError> errors)
    {
        while (true)
        {
            switch (expression)
            {
                case IdentifierExpressionNode identifierExpression:
                    ValidateIdentifierCase(
                        moduleContext,
                        identifierExpression.Name,
                        identifierExpression.Name,
                        EventScriptSymbolKind.Variable,
                        $"Identifier '{identifierExpression.Name}' must use identifier casing (start lowercase and contain only letters or digits)",
                        errors);
                    return;

                case CallExpressionNode call:
                    ValidateIdentifierCase(
                        moduleContext,
                        call.Name,
                        call.Name,
                        EventScriptSymbolKind.GlobalDefinition,
                        $"Call target '{call.Name}' must use identifier casing (rule/select names start lowercase)",
                        errors);
                    ValidateCallExpression(moduleContext, call, callables, errors);
                    foreach (var argument in call.Arguments)
                    {
                        ValidateExpressionReferences(moduleContext, argument, callables, errors);
                    }

                    return;

                case HandlerLiteralExpressionNode handlerLiteral:
                    ValidateMessageCase(
                        moduleContext,
                        handlerLiteral.Message,
                        handlerLiteral.Message,
                        EventScriptSymbolKind.Handler,
                        $"Handler literal '{handlerLiteral.Message}' must use message casing (start uppercase and contain only letters or digits)",
                        errors);
                    foreach (var parameter in handlerLiteral.Parameters)
                    {
                        ValidateIdentifierCase(
                            moduleContext,
                            parameter,
                            handlerLiteral.Message,
                            EventScriptSymbolKind.Handler,
                            $"Handler literal '{handlerLiteral.Message}' declares invalid parameter '{parameter}'",
                            errors);
                    }
                    ValidateDuplicateHandlerLiteralParameters(moduleContext, handlerLiteral, errors);
                    return;

                case MessageLiteralExpressionNode messageLiteral:
                    ValidateMessageCase(
                        moduleContext,
                        messageLiteral.Message,
                        messageLiteral.Message,
                        EventScriptSymbolKind.Message,
                        $"Message literal '{messageLiteral.Message}' must use message casing (start uppercase and contain only letters or digits)",
                        errors);
                    ValidateDuplicateNamedArguments(moduleContext, messageLiteral.Message, messageLiteral.Arguments, errors);
                    foreach (var argument in messageLiteral.Arguments)
                    {
                        ValidateIdentifierCase(
                            moduleContext,
                            argument.Name,
                            messageLiteral.Message,
                            EventScriptSymbolKind.Message,
                            $"Message literal '{messageLiteral.Message}' declares invalid argument name '{argument.Name}'",
                            errors);
                        ValidateExpressionReferences(moduleContext, argument.Expression, callables, errors);
                    }

                    return;

                case HandlerBindExpressionNode handlerBind:
                    ValidateDuplicateNamedArguments(moduleContext, "handler bind", handlerBind.Arguments, errors);
                    ValidateExpressionReferences(moduleContext, handlerBind.CalleeExpression, callables, errors);
                    foreach (var argument in handlerBind.Arguments)
                    {
                        ValidateIdentifierCase(
                            moduleContext,
                            argument.Name,
                            "handler bind",
                            EventScriptSymbolKind.Handler,
                            $"Handler binding declares invalid argument name '{argument.Name}'",
                            errors);
                        ValidateExpressionReferences(moduleContext, argument.Expression, callables, errors);
                    }

                    return;

                case RulePredicateExpressionNode rulePredicate:
                    ValidateIdentifierCase(
                        moduleContext,
                        rulePredicate.RuleName,
                        rulePredicate.RuleName,
                        EventScriptSymbolKind.Rule,
                        $"Rule predicate target '{rulePredicate.RuleName}' must use identifier casing (start lowercase)",
                        errors);
                    if (!callables.TryGetValue(rulePredicate.RuleName, out var callableDefinition) ||
                        callableDefinition.Kind != LinkedCallableKind.Rule ||
                        callableDefinition.Parameters.Count != 1)
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
                        ValidateExpressionReferences(moduleContext, argument, callables, errors);
                    }

                    return;

                case ClampExpressionNode clamp:
                    ValidateExpressionReferences(moduleContext, clamp.Value, callables, errors);
                    ValidateExpressionReferences(moduleContext, clamp.Minimum, callables, errors);
                    expression = clamp.Maximum;
                    continue;

                case RangeExpressionNode rangeExpression:
                    ValidateExpressionReferences(moduleContext, rangeExpression.FromExpression, callables, errors);
                    ValidateExpressionReferences(moduleContext, rangeExpression.ToExpression, callables, errors);
                    if (rangeExpression.StepExpression is not null)
                    {
                        expression = rangeExpression.StepExpression;
                        continue;
                    }

                    return;

                case RandomExpressionNode random:
                    ValidateExpressionReferences(moduleContext, random.FromExpression, callables, errors);
                    expression = random.ToExpression;
                    continue;

                case SeededRandomExpressionNode seededRandom:
                    ValidateExpressionReferences(moduleContext, seededRandom.SeedExpression, callables, errors);
                    expression = seededRandom.BodyExpression;
                    continue;

                case GeneratedCollectionExpressionNode generatedCollection:
                    ValidateIdentifierCase(
                        moduleContext,
                        generatedCollection.Identifier,
                        generatedCollection.Identifier,
                        EventScriptSymbolKind.Variable,
                        $"Generated collection identifier '{generatedCollection.Identifier}' must use identifier casing (start lowercase)",
                        errors);
                    ValidateIterationSourceReferences(moduleContext, generatedCollection.Source, callables, errors);
                    if (generatedCollection.Predicate is not null)
                    {
                        ValidateExpressionReferences(moduleContext, generatedCollection.Predicate, callables, errors);
                    }

                    expression = generatedCollection.Projection;
                    continue;

                case GuardedChoiceExpressionNode guardedChoice:
                    foreach (var branch in guardedChoice.Branches)
                    {
                        ValidateExpressionReferences(moduleContext, branch.ValueExpression, callables, errors);
                        ValidateExpressionReferences(moduleContext, branch.ConditionExpression, callables, errors);
                    }

                    expression = guardedChoice.OtherwiseExpression;
                    continue;

                case BinaryExpressionNode binary:
                    ValidateExpressionReferences(moduleContext, binary.Left, callables, errors);
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
                    ValidateExpressionReferences(moduleContext, collectionAccess.Target, callables, errors);
                    ValidateCollectionSelectorReferences(moduleContext, collectionAccess.Selector, callables, errors);
                    return;

                case ListLiteralExpressionNode list:
                    foreach (var item in list.Items)
                    {
                        ValidateExpressionReferences(moduleContext, item, callables, errors);
                    }

                    return;

                case SetLiteralExpressionNode set:
                    foreach (var item in set.Items)
                    {
                        ValidateExpressionReferences(moduleContext, item, callables, errors);
                    }

                    return;

                case DictionaryLiteralExpressionNode dictionary:
                    foreach (var entry in dictionary.Entries)
                    {
                        ValidateExpressionReferences(moduleContext, entry.Value, callables, errors);
                    }

                    return;
            }

            break;
        }
    }

    private static void ValidateIterationSourceReferences(
        EventScriptModule moduleContext,
        IterationSourceNode source,
        IReadOnlyDictionary<string, LinkedCallableDefinition> callables,
        List<EventScriptLinkageError> errors)
    {
        switch (source)
        {
            case CollectionIterationSourceNode collectionSource:
                ValidateExpressionReferences(moduleContext, collectionSource.Expression, callables, errors);
                return;
            case RangeIterationSourceNode rangeSource:
                ValidateExpressionReferences(moduleContext, rangeSource.RangeExpression, callables, errors);
                return;
        }
    }

    private static void ValidateCollectionSelectorReferences(
        EventScriptModule moduleContext,
        CollectionSelectorNode selector,
        IReadOnlyDictionary<string, LinkedCallableDefinition> callables,
        List<EventScriptLinkageError> errors)
    {
        switch (selector)
        {
            case ExpressionSelectorNode expressionSelector:
                ValidateExpressionReferences(moduleContext, expressionSelector.Expression, callables, errors);
                return;
            case PredicateSelectorNode predicateSelector:
                ValidateIdentifierCase(
                    moduleContext,
                    predicateSelector.Identifier,
                    predicateSelector.Identifier,
                    EventScriptSymbolKind.Variable,
                    $"Selector identifier '{predicateSelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(moduleContext, predicateSelector.Predicate, callables, errors);
                return;
            case CountSelectorNode countSelector:
                ValidateIdentifierCase(
                    moduleContext,
                    countSelector.Identifier,
                    countSelector.Identifier,
                    EventScriptSymbolKind.Variable,
                    $"Selector identifier '{countSelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(moduleContext, countSelector.Predicate, callables, errors);
                return;
            case ChooseSelectorNode chooseSelector:
                if (chooseSelector.Identifier is not null)
                {
                    ValidateIdentifierCase(
                        moduleContext,
                        chooseSelector.Identifier,
                        chooseSelector.Identifier,
                        EventScriptSymbolKind.Variable,
                        $"Selector identifier '{chooseSelector.Identifier}' must use identifier casing (start lowercase)",
                        errors);
                }

                if (chooseSelector.Predicate is not null)
                {
                    ValidateExpressionReferences(moduleContext, chooseSelector.Predicate, callables, errors);
                }

                if (chooseSelector.WeightIdentifier is not null)
                {
                    ValidateIdentifierCase(
                        moduleContext,
                        chooseSelector.WeightIdentifier,
                        chooseSelector.WeightIdentifier,
                        EventScriptSymbolKind.Variable,
                        $"Selector weight identifier '{chooseSelector.WeightIdentifier}' must use identifier casing (start lowercase)",
                        errors);
                }

                if (chooseSelector.WeightExpression is not null)
                {
                    ValidateExpressionReferences(moduleContext, chooseSelector.WeightExpression, callables, errors);
                }

                return;
            case EdgeSelectorNode { Predicate: not null } edgeSelector:
                if (edgeSelector.Identifier is not null)
                {
                    ValidateIdentifierCase(
                        moduleContext,
                        edgeSelector.Identifier,
                        edgeSelector.Identifier,
                        EventScriptSymbolKind.Variable,
                        $"Selector identifier '{edgeSelector.Identifier}' must use identifier casing (start lowercase)",
                        errors);
                }
                ValidateExpressionReferences(moduleContext, edgeSelector.Predicate, callables, errors);
                return;
            case FilterSelectorNode filterSelector:
                ValidateIdentifierCase(
                    moduleContext,
                    filterSelector.Identifier,
                    filterSelector.Identifier,
                    EventScriptSymbolKind.Variable,
                    $"Selector identifier '{filterSelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(moduleContext, filterSelector.Predicate, callables, errors);
                return;
            case SumSelectorNode sumSelector:
                ValidateIdentifierCase(
                    moduleContext,
                    sumSelector.Identifier,
                    sumSelector.Identifier,
                    EventScriptSymbolKind.Variable,
                    $"Selector identifier '{sumSelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(moduleContext, sumSelector.Projection, callables, errors);
                return;
            case AverageSelectorNode averageSelector:
                ValidateIdentifierCase(
                    moduleContext,
                    averageSelector.Identifier,
                    averageSelector.Identifier,
                    EventScriptSymbolKind.Variable,
                    $"Selector identifier '{averageSelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(moduleContext, averageSelector.Projection, callables, errors);
                return;
            case SelectSelectorNode selectSelector:
                ValidateIdentifierCase(
                    moduleContext,
                    selectSelector.Identifier,
                    selectSelector.Identifier,
                    EventScriptSymbolKind.Variable,
                    $"Selector identifier '{selectSelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(moduleContext, selectSelector.Projection, callables, errors);
                return;
            case DictionarySelectorNode dictionarySelector:
                ValidateIdentifierCase(
                    moduleContext,
                    dictionarySelector.Identifier,
                    dictionarySelector.Identifier,
                    EventScriptSymbolKind.Variable,
                    $"Selector identifier '{dictionarySelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(moduleContext, dictionarySelector.KeyProjection, callables, errors);
                if (dictionarySelector.ValueProjection is not null)
                {
                    ValidateExpressionReferences(moduleContext, dictionarySelector.ValueProjection, callables, errors);
                }

                return;
            case MinSelectorNode minSelector:
                ValidateIdentifierCase(
                    moduleContext,
                    minSelector.Identifier,
                    minSelector.Identifier,
                    EventScriptSymbolKind.Variable,
                    $"Selector identifier '{minSelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(moduleContext, minSelector.Projection, callables, errors);
                return;
            case MaxSelectorNode maxSelector:
                ValidateIdentifierCase(
                    moduleContext,
                    maxSelector.Identifier,
                    maxSelector.Identifier,
                    EventScriptSymbolKind.Variable,
                    $"Selector identifier '{maxSelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(moduleContext, maxSelector.Projection, callables, errors);
                return;
            case ContainsSelectorNode containsSelector:
                ValidateExpressionReferences(moduleContext, containsSelector.ValueExpression, callables, errors);
                return;
            case DistinctSelectorNode { Projection: not null } distinctSelector:
                if (distinctSelector.Identifier is not null)
                {
                    ValidateIdentifierCase(
                        moduleContext,
                        distinctSelector.Identifier,
                        distinctSelector.Identifier,
                        EventScriptSymbolKind.Variable,
                        $"Selector identifier '{distinctSelector.Identifier}' must use identifier casing (start lowercase)",
                        errors);
                }
                ValidateExpressionReferences(moduleContext, distinctSelector.Projection, callables, errors);
                return;
            case GroupBySelectorNode groupBySelector:
                ValidateIdentifierCase(
                    moduleContext,
                    groupBySelector.Identifier,
                    groupBySelector.Identifier,
                    EventScriptSymbolKind.Variable,
                    $"Selector identifier '{groupBySelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(moduleContext, groupBySelector.Projection, callables, errors);
                return;
            case OrderBySelectorNode orderBySelector:
                ValidateIdentifierCase(
                    moduleContext,
                    orderBySelector.Identifier,
                    orderBySelector.Identifier,
                    EventScriptSymbolKind.Variable,
                    $"Selector identifier '{orderBySelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(moduleContext, orderBySelector.Projection, callables, errors);
                return;
        }
    }

    private static void ValidateCallExpression(
        EventScriptModule moduleContext,
        CallExpressionNode call,
        IReadOnlyDictionary<string, LinkedCallableDefinition> callables,
        List<EventScriptLinkageError> errors)
    {
        if (!callables.TryGetValue(call.Name, out var callable))
        {
            errors.Add(CreateError(
                moduleContext,
                $"No rule or select named '{call.Name}' exists",
                call.Name,
                EventScriptSymbolKind.GlobalDefinition,
                EventScriptLinkageErrorKind.MissingRuleOrSelect));
            return;
        }

        ValidateCallArity(moduleContext, callable.Kind, call.Name, callable.Parameters.Count, call.Arguments.Count, errors);
    }

    private static void ValidateCallArity(
        EventScriptModule moduleContext,
        LinkedCallableKind kind,
        string name,
        int expectedCount,
        int actualCount,
        List<EventScriptLinkageError> errors)
    {
        if (expectedCount != actualCount)
        {
            errors.Add(CreateError(
                moduleContext,
                $"{kind} '{name}' expects {expectedCount} argument(s) but received {actualCount}",
                name,
                kind == LinkedCallableKind.Rule ? EventScriptSymbolKind.Rule : EventScriptSymbolKind.Select,
                kind == LinkedCallableKind.Rule ? EventScriptLinkageErrorKind.WrongRuleArity : EventScriptLinkageErrorKind.WrongSelectArity));
        }
    }

    private static void ValidateDuplicateNamedArguments(
        EventScriptModule moduleContext,
        string symbolName,
        IReadOnlyList<NamedArgumentNode> arguments,
        List<EventScriptLinkageError> errors)
    {
        var duplicateArguments = arguments
            .GroupBy(argument => argument.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        foreach (var duplicateArgument in duplicateArguments)
        {
            errors.Add(CreateError(
                moduleContext,
                $"Named argument '{duplicateArgument}' is declared more than once",
                symbolName,
                EventScriptSymbolKind.Handler,
                EventScriptLinkageErrorKind.DuplicatePublishArgument));
        }
    }

    private static void ValidateDuplicateHandlerLiteralParameters(
        EventScriptModule moduleContext,
        HandlerLiteralExpressionNode handlerLiteral,
        List<EventScriptLinkageError> errors)
    {
        var duplicateParameters = handlerLiteral.Parameters
            .GroupBy(parameter => parameter, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        foreach (var duplicateParameter in duplicateParameters)
        {
            errors.Add(CreateError(
                moduleContext,
                $"Handler literal '{handlerLiteral.Message}' declares parameter '{duplicateParameter}' more than once",
                handlerLiteral.Message,
                EventScriptSymbolKind.Handler,
                EventScriptLinkageErrorKind.DuplicateHandlerParameter));
        }
    }

    private static void ValidateIdentifierCase(
        EventScriptModule moduleContext,
        string name,
        string symbol,
        EventScriptSymbolKind symbolKind,
        string message,
        List<EventScriptLinkageError> errors)
    {
        if (IsIdentifierCase(name))
        {
            return;
        }

        errors.Add(CreateError(
            moduleContext,
            message,
            symbol,
            symbolKind,
            EventScriptLinkageErrorKind.InvalidIdentifierCase));
    }

    private static void ValidateMessageCase(
        EventScriptModule moduleContext,
        string name,
        string symbol,
        EventScriptSymbolKind symbolKind,
        string message,
        List<EventScriptLinkageError> errors)
    {
        if (IsMessageCase(name))
        {
            return;
        }

        errors.Add(CreateError(
            moduleContext,
            message,
            symbol,
            symbolKind,
            EventScriptLinkageErrorKind.InvalidMessageCase));
    }

    private static bool IsIdentifierCase(string name)
    {
        if (string.IsNullOrEmpty(name) || !char.IsLower(name[0]))
        {
            return false;
        }

        for (var i = 1; i < name.Length; i++)
        {
            if (!char.IsLetterOrDigit(name[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsMessageCase(string name)
    {
        if (string.IsNullOrEmpty(name) || !char.IsUpper(name[0]))
        {
            return false;
        }

        for (var i = 1; i < name.Length; i++)
        {
            if (!char.IsLetterOrDigit(name[i]))
            {
                return false;
            }
        }

        return true;
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

    private static Dictionary<string, LinkedCallableDefinition> BuildCallableDefinitionMap(
        IReadOnlyDictionary<string, RuleDefinitionNode> ruleDefinitions,
        IReadOnlyDictionary<string, SelectDefinitionNode> selectDefinitions)
    {
        var map = new Dictionary<string, LinkedCallableDefinition>(StringComparer.Ordinal);

        foreach (var pair in ruleDefinitions)
        {
            map[pair.Key] = new LinkedCallableDefinition(
                pair.Key,
                pair.Value.Parameters.ToArray(),
                pair.Value.Expression,
                LinkedCallableKind.Rule);
        }

        foreach (var pair in selectDefinitions)
        {
            map[pair.Key] = new LinkedCallableDefinition(
                pair.Key,
                pair.Value.Parameters.ToArray(),
                pair.Value.Expression,
                LinkedCallableKind.Select);
        }

        return map;
    }
}
