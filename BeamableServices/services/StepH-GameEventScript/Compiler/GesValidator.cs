using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.Compiler;

internal static class GesValidator
{
    private sealed class ValidationScope(IEnumerable<string>? names = null)
    {
        private readonly HashSet<string> _variables = names is null ? new HashSet<string>(StringComparer.Ordinal) : new HashSet<string>(names, StringComparer.Ordinal);

        public static ValidationScope CreateChild() => new();

        public bool ContainsInCurrentScope(string name) => _variables.Contains(name);

        public void Declare(string name) => _variables.Add(name);
    }

    internal static void ValidateModule(ParsedModule eventScriptModule, IReadOnlyDictionary<string, GseCallableDefinition> callables,
        IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions, GesValidationErrors errors)
    {
        foreach (var typeDefinition in eventScriptModule.TypeDefinitions)
        {
            foreach (var field in typeDefinition.Fields)
            {
                ValidateIdentifierCase(
                    eventScriptModule,
                    field.Name,
                    field.Name,
                    GameEventScriptSymbolKind.Variable,
                    "Field names must use identifier casing (start lowercase and contain only letters or digits)",
                    errors);

                if (field.MinimumExpression is not null)
                {
                    ValidateExpressionReferences(eventScriptModule, field.MinimumExpression, callables, typeDefinitions, errors);
                }

                if (field.MaximumExpression is not null)
                {
                    ValidateExpressionReferences(eventScriptModule, field.MaximumExpression, callables, typeDefinitions, errors);
                }

                if (field.ComputedExpression is not null)
                {
                    ValidateExpressionReferences(eventScriptModule, field.ComputedExpression, callables, typeDefinitions, errors);
                }
            }
        }

        foreach (var ruleDefinition in eventScriptModule.RuleDefinitions)
        {
            ValidateIdentifierCase(
                eventScriptModule,
                ruleDefinition.Name,
                ruleDefinition.Name,
                GameEventScriptSymbolKind.Rule,
                "Rule names must use identifier casing (start lowercase and contain only letters or digits)",
                errors);

            foreach (var parameter in ruleDefinition.Parameters)
            {
                ValidateIdentifierCase(
                    eventScriptModule,
                    parameter,
                    ruleDefinition.Name,
                    GameEventScriptSymbolKind.Rule,
                    $"Rule '{ruleDefinition.Name}' declares an invalid parameter name '{parameter}'",
                    errors);
            }

            var duplicateParameters = ruleDefinition.Parameters
                .GroupBy(parameter => parameter, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key);

            foreach (var duplicateParameter in duplicateParameters)
            {
                var duplicateParameterNode = ruleDefinition.ParameterList.Last(parameter => string.Equals(parameter.LocalName, duplicateParameter, StringComparison.Ordinal));
                errors.Add(
                    eventScriptModule,
                    $"Rule '{ruleDefinition.Name}' declares parameter '{duplicateParameter}' more than once",
                    ruleDefinition.Name,
                    GameEventScriptSymbolKind.Rule,
                    GameEventScriptModuleBuildErrorKind.DuplicateDefinitionParameter,
                    duplicateParameterNode);
            }

            ValidateExpressionReferences(eventScriptModule, ruleDefinition.Expression, callables, typeDefinitions, errors);
        }

        foreach (var selectDefinition in eventScriptModule.SelectDefinitions)
        {
            ValidateIdentifierCase(
                eventScriptModule,
                selectDefinition.Name,
                selectDefinition.Name,
                GameEventScriptSymbolKind.Select,
                "Select names must use identifier casing (start lowercase and contain only letters or digits)",
                errors);

            foreach (var parameter in selectDefinition.Parameters)
            {
                ValidateIdentifierCase(
                    eventScriptModule,
                    parameter,
                    selectDefinition.Name,
                    GameEventScriptSymbolKind.Select,
                    $"Select '{selectDefinition.Name}' declares an invalid parameter name '{parameter}'",
                    errors);
            }

            var duplicateParameters = selectDefinition.Parameters
                .GroupBy(parameter => parameter, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key);

            foreach (var duplicateParameter in duplicateParameters)
            {
                var duplicateParameterNode = selectDefinition.ParameterList.Last(parameter => string.Equals(parameter.LocalName, duplicateParameter, StringComparison.Ordinal));
                errors.Add(
                    eventScriptModule,
                    $"Select '{selectDefinition.Name}' declares parameter '{duplicateParameter}' more than once",
                    selectDefinition.Name,
                    GameEventScriptSymbolKind.Select,
                    GameEventScriptModuleBuildErrorKind.DuplicateDefinitionParameter,
                    duplicateParameterNode);
            }

            ValidateExpressionReferences(eventScriptModule, selectDefinition.Expression, callables, typeDefinitions, errors);
        }

        foreach (var handler in eventScriptModule.Handlers)
        {
            ValidateMessageCase(
                eventScriptModule,
                handler.Message,
                handler.Message,
                GameEventScriptSymbolKind.Handler,
                "Handler message names must use message casing (start uppercase and contain only letters or digits)",
                errors);

            foreach (var parameter in handler.Parameters)
            {
                ValidateIdentifierCase(
                    eventScriptModule,
                    parameter,
                    handler.Message,
                    GameEventScriptSymbolKind.Handler,
                    $"Handler '{handler.Message}' declares an invalid parameter name '{parameter}'",
                    errors);
            }

            var duplicateParameters = handler.Parameters
                .GroupBy(parameter => parameter, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key);

            foreach (var duplicateParameter in duplicateParameters)
            {
                var duplicateParameterNode = handler.ParameterList.Last(parameter => string.Equals(parameter.LocalName, duplicateParameter, StringComparison.Ordinal));
                errors.Add(
                    eventScriptModule,
                    $"Handler '{handler.Message}' declares parameter '{duplicateParameter}' more than once",
                    handler.Message,
                    GameEventScriptSymbolKind.Handler,
                    GameEventScriptModuleBuildErrorKind.DuplicateHandlerParameter,
                    duplicateParameterNode);
            }

            var handlerScope = new ValidationScope(handler.Parameters);
            foreach (var statement in handler.Statements)
            {
                ValidateStatementReferences(eventScriptModule, statement, callables, typeDefinitions, errors, handlerScope);
            }
        }
    }

    private static void ValidateStatementReferences(
        ParsedModule moduleContext,
        StatementNode statement,
        IReadOnlyDictionary<string, GseCallableDefinition> callables,
        IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions,
        GesValidationErrors errors,
        ValidationScope scope)
    {
        switch (statement)
        {
            case PublishStatementNode publish:
                ValidateExpressionReferences(moduleContext, publish.MessageExpression, callables, typeDefinitions, errors);
                return;

            case LetStatementNode let:
                ValidateExpressionReferences(moduleContext, let.Expression, callables, typeDefinitions, errors);
                ValidateIdentifierCase(
                    moduleContext,
                    let.Identifier,
                    let.Identifier,
                    GameEventScriptSymbolKind.Variable,
                    $"Variable '{let.Identifier}' must use identifier casing (start lowercase and contain only letters or digits)",
                    errors);
                if (scope.ContainsInCurrentScope(let.Identifier))
                {
                    errors.Add(
                        moduleContext,
                        $"Variable '{let.Identifier}' is already declared in the current scope",
                        let.Identifier,
                        GameEventScriptSymbolKind.Variable,
                        GameEventScriptModuleBuildErrorKind.DuplicateVariable,
                        let);
                    return;
                }

                scope.Declare(let.Identifier);
                return;

            case IfStatementNode ifStatement:
                ValidateExpressionReferences(moduleContext, ifStatement.Condition, callables, typeDefinitions, errors);
                ValidateStatementBodyReferences(moduleContext, ifStatement.ThenBody, callables, typeDefinitions, errors, scope);

                if (ifStatement.ElseBody is null)
                {
                    return;
                }

                ValidateStatementBodyReferences(moduleContext, ifStatement.ElseBody, callables, typeDefinitions, errors, scope);

                return;

            case ForStatementNode forStatement:
                ValidateIterationSourceReferences(moduleContext, forStatement.Source, callables, typeDefinitions, errors);
                ValidateIdentifierCase(
                    moduleContext,
                    forStatement.Identifier,
                    forStatement.Identifier,
                    GameEventScriptSymbolKind.Variable,
                    $"Loop variable '{forStatement.Identifier}' must use identifier casing (start lowercase and contain only letters or digits)",
                    errors);
                var loopScope = ValidationScope.CreateChild();
                loopScope.Declare(forStatement.Identifier);
                ValidateStatementBodyReferences(moduleContext, forStatement.Body, callables, typeDefinitions, errors, loopScope);

                return;

            case SeededRandomStatementNode seededRandom:
                ValidateExpressionReferences(moduleContext, seededRandom.SeedExpression, callables, typeDefinitions, errors);
                ValidateStatementBodyReferences(moduleContext, seededRandom.Body, callables, typeDefinitions, errors, scope);

                return;

            case ExpressionStatementNode expressionStatement:
                ValidateExpressionReferences(moduleContext, expressionStatement.Expression, callables, typeDefinitions, errors);
                return;
        }
    }

    private static void ValidateStatementBodyReferences(
        ParsedModule moduleContext,
        StatementBodyNode body,
        IReadOnlyDictionary<string, GseCallableDefinition> callables,
        IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions,
        GesValidationErrors errors,
        ValidationScope parentScope)
    {
        var bodyScope = body.IsBlock ? ValidationScope.CreateChild() : parentScope;
        foreach (var nested in body.Statements)
        {
            ValidateStatementReferences(moduleContext, nested, callables, typeDefinitions, errors, bodyScope);
        }
    }

    private static void ValidateExpressionReferences(
        ParsedModule moduleContext,
        ExpressionNode expression,
        IReadOnlyDictionary<string, GseCallableDefinition> callables,
        IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions,
        GesValidationErrors errors)
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
                        GameEventScriptSymbolKind.Variable,
                        $"Identifier '{identifierExpression.Name}' must use identifier casing (start lowercase and contain only letters or digits)",
                        errors);
                    return;

                case CallExpressionNode call:
                    ValidateIdentifierCase(
                        moduleContext,
                        call.Name,
                        call.Name,
                        GameEventScriptSymbolKind.GlobalDefinition,
                        $"Call target '{call.Name}' must use identifier casing (rule/select names start lowercase)",
                        errors);
                    ValidateDuplicateNamedArguments(moduleContext, call.Name, call.ArgumentList.Arguments, errors);
                    ValidateCallExpression(moduleContext, call, callables, typeDefinitions, errors);
                    foreach (var argument in call.ArgumentList.Arguments)
                    {
                        if (argument.Label is not null)
                        {
                            ValidateIdentifierCase(
                                moduleContext,
                                argument.Label,
                                call.Name,
                                GameEventScriptSymbolKind.GlobalDefinition,
                                $"Call argument label '{argument.Label}' must use identifier casing",
                                errors);
                        }

                        ValidateExpressionReferences(moduleContext, argument.Expression, callables, typeDefinitions, errors);
                    }

                    return;

                case HandlerLiteralExpressionNode handlerLiteral:
                    ValidateMessageCase(
                        moduleContext,
                        handlerLiteral.Message,
                        handlerLiteral.Message,
                        GameEventScriptSymbolKind.Handler,
                        $"Handler literal '{handlerLiteral.Message}' must use message casing (start uppercase and contain only letters or digits)",
                        errors);
                    foreach (var parameter in handlerLiteral.Parameters)
                    {
                        ValidateIdentifierCase(
                            moduleContext,
                            parameter,
                            handlerLiteral.Message,
                            GameEventScriptSymbolKind.Handler,
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
                        GameEventScriptSymbolKind.Message,
                        $"Message literal '{messageLiteral.Message}' must use message casing (start uppercase and contain only letters or digits)",
                        errors);
                    ValidateDuplicateNamedArguments(moduleContext, messageLiteral.Message, messageLiteral.Arguments, errors);
                    foreach (var argument in messageLiteral.Arguments)
                    {
                        if (argument.Label is not null)
                        {
                            ValidateIdentifierCase(
                                moduleContext,
                                argument.Label,
                                messageLiteral.Message,
                                GameEventScriptSymbolKind.Message,
                                $"Message literal '{messageLiteral.Message}' declares invalid argument name '{argument.Label}'",
                                errors);
                        }

                        ValidateExpressionReferences(moduleContext, argument.Expression, callables, typeDefinitions, errors);
                    }

                    return;

                case HandlerBindExpressionNode handlerBind:
                    ValidateDuplicateNamedArguments(moduleContext, "handler bind", handlerBind.Arguments, errors);
                    ValidateExpressionReferences(moduleContext, handlerBind.CalleeExpression, callables, typeDefinitions, errors);
                    foreach (var argument in handlerBind.Arguments)
                    {
                        if (argument.Label is not null)
                        {
                            ValidateIdentifierCase(
                                moduleContext,
                                argument.Label,
                                "handler bind",
                                GameEventScriptSymbolKind.Handler,
                                $"Handler binding declares invalid argument name '{argument.Label}'",
                                errors);
                        }

                        ValidateExpressionReferences(moduleContext, argument.Expression, callables, typeDefinitions, errors);
                    }

                    return;

                case ExtensionCallExpressionNode extensionCall:
                    foreach (var argument in extensionCall.Arguments)
                    {
                        ValidateExpressionReferences(moduleContext, argument.Expression, callables, typeDefinitions, errors);
                    }

                    return;

                case TypeConstructorExpressionNode typeConstructor:
                    ValidateTypeConstructorExpression(moduleContext, typeConstructor, callables, typeDefinitions, errors);
                    return;

                case RulePredicateExpressionNode rulePredicate:
                    ValidateIdentifierCase(
                        moduleContext,
                        rulePredicate.RuleName,
                        rulePredicate.RuleName,
                        GameEventScriptSymbolKind.Rule,
                        $"Rule predicate target '{rulePredicate.RuleName}' must use identifier casing (start lowercase)",
                        errors);
                    if (!callables.TryGetValue(rulePredicate.RuleName, out var callableDefinition) ||
                        callableDefinition.Kind != GameEventScriptCallableKind.Rule ||
                        callableDefinition.Parameters.Count != 1)
                    {
                        errors.Add(
                            moduleContext,
                            $"Rule '{rulePredicate.RuleName}' must exist and declare exactly one parameter to be used with 'is'",
                            rulePredicate.RuleName,
                            GameEventScriptSymbolKind.Rule,
                            GameEventScriptModuleBuildErrorKind.InvalidRulePredicate);
                    }

                    expression = rulePredicate.Value;
                    continue;

                case ExtensionPredicateExpressionNode extensionPredicate:
                    expression = extensionPredicate.Value;
                    continue;

                case UnaryExpressionNode unary:
                    expression = unary.Operand;
                    continue;

                case VariadicTaggedExpressionNode variadic:
                    foreach (var argument in variadic.Arguments)
                    {
                        ValidateExpressionReferences(moduleContext, argument, callables, typeDefinitions, errors);
                    }

                    return;

                case ClampExpressionNode clamp:
                    ValidateExpressionReferences(moduleContext, clamp.Value, callables, typeDefinitions, errors);
                    ValidateExpressionReferences(moduleContext, clamp.Minimum, callables, typeDefinitions, errors);
                    expression = clamp.Maximum;
                    continue;

                case RangeExpressionNode rangeExpression:
                    ValidateExpressionReferences(moduleContext, rangeExpression.FromExpression, callables, typeDefinitions, errors);
                    ValidateExpressionReferences(moduleContext, rangeExpression.ToExpression, callables, typeDefinitions, errors);
                    if (rangeExpression.StepExpression is not null)
                    {
                        expression = rangeExpression.StepExpression;
                        continue;
                    }

                    return;

                case RandomExpressionNode random:
                    ValidateExpressionReferences(moduleContext, random.FromExpression, callables, typeDefinitions, errors);
                    expression = random.ToExpression;
                    continue;

                case SeededRandomExpressionNode seededRandom:
                    ValidateExpressionReferences(moduleContext, seededRandom.SeedExpression, callables, typeDefinitions, errors);
                    expression = seededRandom.BodyExpression;
                    continue;

                case GeneratedCollectionExpressionNode generatedCollection:
                    ValidateIdentifierCase(
                        moduleContext,
                        generatedCollection.Identifier,
                        generatedCollection.Identifier,
                        GameEventScriptSymbolKind.Variable,
                        $"Generated collection identifier '{generatedCollection.Identifier}' must use identifier casing (start lowercase)",
                        errors);
                    ValidateIterationSourceReferences(moduleContext, generatedCollection.Source, callables, typeDefinitions, errors);
                    if (generatedCollection.Predicate is not null)
                    {
                        ValidateExpressionReferences(moduleContext, generatedCollection.Predicate, callables, typeDefinitions, errors);
                    }

                    expression = generatedCollection.Projection;
                    continue;

                case GuardedChoiceExpressionNode guardedChoice:
                    foreach (var branch in guardedChoice.Branches)
                    {
                        ValidateExpressionReferences(moduleContext, branch.ValueExpression, callables, typeDefinitions, errors);
                        ValidateExpressionReferences(moduleContext, branch.ConditionExpression, callables, typeDefinitions, errors);
                    }

                    expression = guardedChoice.OtherwiseExpression;
                    continue;

                case BinaryExpressionNode binary:
                    ValidateExpressionReferences(moduleContext, binary.Left, callables, typeDefinitions, errors);
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
                    ValidateExpressionReferences(moduleContext, collectionAccess.Target, callables, typeDefinitions, errors);
                    ValidateCollectionSelectorReferences(moduleContext, collectionAccess.Selector, callables, typeDefinitions, errors);
                    return;

                case ListLiteralExpressionNode list:
                    foreach (var item in list.Items)
                    {
                        ValidateExpressionReferences(moduleContext, item, callables, typeDefinitions, errors);
                    }

                    return;

                case SetLiteralExpressionNode set:
                    foreach (var item in set.Items)
                    {
                        ValidateExpressionReferences(moduleContext, item, callables, typeDefinitions, errors);
                    }

                    return;

                case SequenceLiteralExpressionNode sequence:
                    foreach (var item in sequence.Items)
                    {
                        ValidateExpressionReferences(moduleContext, item, callables, typeDefinitions, errors);
                    }

                    return;

                case DictionaryLiteralExpressionNode dictionary:
                    foreach (var entry in dictionary.Entries)
                    {
                        ValidateExpressionReferences(moduleContext, entry.Value, callables, typeDefinitions, errors);
                    }

                    return;
            }

            break;
        }
    }

    private static void ValidateIterationSourceReferences(
        ParsedModule moduleContext,
        IterationSourceNode source,
        IReadOnlyDictionary<string, GseCallableDefinition> callables,
        IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions,
        GesValidationErrors errors)
    {
        switch (source)
        {
            case CollectionIterationSourceNode collectionSource:
                ValidateExpressionReferences(moduleContext, collectionSource.Expression, callables, typeDefinitions, errors);
                return;
            case RangeIterationSourceNode rangeSource:
                ValidateExpressionReferences(moduleContext, rangeSource.RangeExpression, callables, typeDefinitions, errors);
                return;
        }
    }

    private static void ValidateCollectionSelectorReferences(
        ParsedModule moduleContext,
        CollectionSelectorNode selector,
        IReadOnlyDictionary<string, GseCallableDefinition> callables,
        IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions,
        GesValidationErrors errors)
    {
        switch (selector)
        {
            case ExpressionSelectorNode expressionSelector:
                ValidateExpressionReferences(moduleContext, expressionSelector.Expression, callables, typeDefinitions, errors);
                return;
            case PredicateSelectorNode predicateSelector:
                ValidateIdentifierCase(
                    moduleContext,
                    predicateSelector.Identifier,
                    predicateSelector.Identifier,
                    GameEventScriptSymbolKind.Variable,
                    $"Selector identifier '{predicateSelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(moduleContext, predicateSelector.Predicate, callables, typeDefinitions, errors);
                return;
            case CountSelectorNode countSelector:
                ValidateIdentifierCase(
                    moduleContext,
                    countSelector.Identifier,
                    countSelector.Identifier,
                    GameEventScriptSymbolKind.Variable,
                    $"Selector identifier '{countSelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(moduleContext, countSelector.Predicate, callables, typeDefinitions, errors);
                return;
            case ChooseSelectorNode chooseSelector:
                if (chooseSelector.Identifier is not null)
                {
                    ValidateIdentifierCase(
                        moduleContext,
                        chooseSelector.Identifier,
                        chooseSelector.Identifier,
                        GameEventScriptSymbolKind.Variable,
                        $"Selector identifier '{chooseSelector.Identifier}' must use identifier casing (start lowercase)",
                        errors);
                }

                if (chooseSelector.Predicate is not null)
                {
                    ValidateExpressionReferences(moduleContext, chooseSelector.Predicate, callables, typeDefinitions, errors);
                }

                if (chooseSelector.WeightIdentifier is not null)
                {
                    ValidateIdentifierCase(
                        moduleContext,
                        chooseSelector.WeightIdentifier,
                        chooseSelector.WeightIdentifier,
                        GameEventScriptSymbolKind.Variable,
                        $"Selector weight identifier '{chooseSelector.WeightIdentifier}' must use identifier casing (start lowercase)",
                        errors);
                }

                if (chooseSelector.WeightExpression is not null)
                {
                    ValidateExpressionReferences(moduleContext, chooseSelector.WeightExpression, callables, typeDefinitions, errors);
                }

                return;
            case EdgeSelectorNode { Predicate: not null } edgeSelector:
                if (edgeSelector.Identifier is not null)
                {
                    ValidateIdentifierCase(
                        moduleContext,
                        edgeSelector.Identifier,
                        edgeSelector.Identifier,
                        GameEventScriptSymbolKind.Variable,
                        $"Selector identifier '{edgeSelector.Identifier}' must use identifier casing (start lowercase)",
                        errors);
                }

                ValidateExpressionReferences(moduleContext, edgeSelector.Predicate, callables, typeDefinitions, errors);
                return;
            case FilterSelectorNode filterSelector:
                ValidateIdentifierCase(
                    moduleContext,
                    filterSelector.Identifier,
                    filterSelector.Identifier,
                    GameEventScriptSymbolKind.Variable,
                    $"Selector identifier '{filterSelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(moduleContext, filterSelector.Predicate, callables, typeDefinitions, errors);
                return;
            case SumSelectorNode sumSelector:
                ValidateIdentifierCase(
                    moduleContext,
                    sumSelector.Identifier,
                    sumSelector.Identifier,
                    GameEventScriptSymbolKind.Variable,
                    $"Selector identifier '{sumSelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(moduleContext, sumSelector.Projection, callables, typeDefinitions, errors);
                return;
            case AverageSelectorNode averageSelector:
                ValidateIdentifierCase(
                    moduleContext,
                    averageSelector.Identifier,
                    averageSelector.Identifier,
                    GameEventScriptSymbolKind.Variable,
                    $"Selector identifier '{averageSelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(moduleContext, averageSelector.Projection, callables, typeDefinitions, errors);
                return;
            case SelectSelectorNode selectSelector:
                ValidateIdentifierCase(
                    moduleContext,
                    selectSelector.Identifier,
                    selectSelector.Identifier,
                    GameEventScriptSymbolKind.Variable,
                    $"Selector identifier '{selectSelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(moduleContext, selectSelector.Projection, callables, typeDefinitions, errors);
                return;
            case DictionarySelectorNode dictionarySelector:
                ValidateIdentifierCase(
                    moduleContext,
                    dictionarySelector.Identifier,
                    dictionarySelector.Identifier,
                    GameEventScriptSymbolKind.Variable,
                    $"Selector identifier '{dictionarySelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(moduleContext, dictionarySelector.KeyProjection, callables, typeDefinitions, errors);
                if (dictionarySelector.ValueProjection is not null)
                {
                    ValidateExpressionReferences(moduleContext, dictionarySelector.ValueProjection, callables, typeDefinitions, errors);
                }

                return;
            case MinSelectorNode minSelector:
                ValidateIdentifierCase(
                    moduleContext,
                    minSelector.Identifier,
                    minSelector.Identifier,
                    GameEventScriptSymbolKind.Variable,
                    $"Selector identifier '{minSelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(moduleContext, minSelector.Projection, callables, typeDefinitions, errors);
                return;
            case MaxSelectorNode maxSelector:
                ValidateIdentifierCase(
                    moduleContext,
                    maxSelector.Identifier,
                    maxSelector.Identifier,
                    GameEventScriptSymbolKind.Variable,
                    $"Selector identifier '{maxSelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(moduleContext, maxSelector.Projection, callables, typeDefinitions, errors);
                return;
            case ContainsSelectorNode containsSelector:
                ValidateExpressionReferences(moduleContext, containsSelector.ValueExpression, callables, typeDefinitions, errors);
                return;
            case DistinctSelectorNode { Projection: not null } distinctSelector:
                if (distinctSelector.Identifier is not null)
                {
                    ValidateIdentifierCase(
                        moduleContext,
                        distinctSelector.Identifier,
                        distinctSelector.Identifier,
                        GameEventScriptSymbolKind.Variable,
                        $"Selector identifier '{distinctSelector.Identifier}' must use identifier casing (start lowercase)",
                        errors);
                }

                ValidateExpressionReferences(moduleContext, distinctSelector.Projection, callables, typeDefinitions, errors);
                return;
            case GroupBySelectorNode groupBySelector:
                ValidateIdentifierCase(
                    moduleContext,
                    groupBySelector.Identifier,
                    groupBySelector.Identifier,
                    GameEventScriptSymbolKind.Variable,
                    $"Selector identifier '{groupBySelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(moduleContext, groupBySelector.Projection, callables, typeDefinitions, errors);
                return;
            case OrderBySelectorNode orderBySelector:
                ValidateIdentifierCase(
                    moduleContext,
                    orderBySelector.Identifier,
                    orderBySelector.Identifier,
                    GameEventScriptSymbolKind.Variable,
                    $"Selector identifier '{orderBySelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(moduleContext, orderBySelector.Projection, callables, typeDefinitions, errors);
                return;
        }
    }

    private static void ValidateCallExpression(
        ParsedModule moduleContext,
        CallExpressionNode call,
        IReadOnlyDictionary<string, GseCallableDefinition> callables,
        IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions,
        GesValidationErrors errors)
    {
        if (!callables.TryGetValue(call.Name, out var callable))
        {
            if (call.ArgumentList.Arguments.All(argument => argument.Label is null))
            {
                errors.Add(
                    moduleContext,
                    $"No rule or select named '{call.Name}' exists",
                    call.Name,
                    GameEventScriptSymbolKind.GlobalDefinition,
                    GameEventScriptModuleBuildErrorKind.MissingRuleOrSelect);
            }

            return;
        }

        ValidateCallArity(moduleContext, callable.Kind, call.Name, callable.Parameters.Count, call.Arguments.Count, errors);
        ValidateCallLabels(moduleContext, callable.Kind, call.Name, callable.SignatureLabels, call.ArgumentList.Arguments, errors);
    }

    private static void ValidateCallLabels(
        ParsedModule moduleContext,
        GameEventScriptCallableKind kind,
        string name,
        IReadOnlyList<string> expectedLabels,
        IReadOnlyList<ArgumentNode> arguments,
        GesValidationErrors errors)
    {
        var count = Math.Min(expectedLabels.Count, arguments.Count);
        for (var index = 0; index < count; index++)
        {
            var expected = expectedLabels[index];
            var actual = arguments[index].Name;
            if (string.Equals(expected, actual, StringComparison.Ordinal))
            {
                continue;
            }

            errors.Add(
                moduleContext,
                $"{kind} '{name}' argument {index + 1} expects label '{expected}' but received '{actual}'",
                name,
                kind == GameEventScriptCallableKind.Rule ? GameEventScriptSymbolKind.Rule : GameEventScriptSymbolKind.Select,
                kind == GameEventScriptCallableKind.Rule ? GameEventScriptModuleBuildErrorKind.WrongRuleArity : GameEventScriptModuleBuildErrorKind.WrongSelectArity);
        }
    }

    private static void ValidateTypeConstructorExpression(
        ParsedModule moduleContext,
        TypeConstructorExpressionNode constructor,
        IReadOnlyDictionary<string, GseCallableDefinition> callables,
        IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions,
        GesValidationErrors errors)
    {
        ValidateDuplicateNamedArguments(moduleContext, constructor.TypeName, constructor.Arguments, errors);
        foreach (var argument in constructor.Arguments)
        {
            if (argument.Label is not null)
            {
                ValidateIdentifierCase(
                    moduleContext,
                    argument.Label,
                    constructor.TypeName,
                    GameEventScriptSymbolKind.Type,
                    $"Type constructor ':{constructor.TypeName}' declares invalid argument label '{argument.Label}'",
                    errors);
            }

            ValidateExpressionReferences(moduleContext, argument.Expression, callables, typeDefinitions, errors);
        }

        if (IsBuiltinConstructorType(constructor.TypeName))
        {
            ValidateBuiltinTypeConstructor(moduleContext, constructor, errors);
            return;
        }

        if (!typeDefinitions.TryGetValue(constructor.TypeName, out var typeDefinition))
        {
            AddTypeConstructorError(moduleContext, constructor.TypeName, $"Unknown type constructor ':{constructor.TypeName}'", errors);
            return;
        }

        if (constructor.Arguments.Any(argument => argument.Label is null))
        {
            AddTypeConstructorError(moduleContext, constructor.TypeName, $"Custom type constructor ':{constructor.TypeName}' requires labeled field arguments", errors);
        }

        var fieldNames = new HashSet<string>(typeDefinition.Fields.Select(field => field.Name), StringComparer.Ordinal);
        foreach (var argument in constructor.Arguments.Where(argument => argument.Label is not null))
        {
            if (!fieldNames.Contains(argument.Label!))
            {
                AddTypeConstructorError(
                    moduleContext,
                    constructor.TypeName,
                    $"Custom type constructor ':{constructor.TypeName}' has unknown field '{argument.Label}'",
                    errors);
            }
        }
    }

    private static void ValidateBuiltinTypeConstructor(
        ParsedModule moduleContext,
        TypeConstructorExpressionNode constructor,
        GesValidationErrors errors)
    {
        switch (constructor.TypeName)
        {
            case "vector2":
                ValidateVectorConstructor(moduleContext, constructor, ["x", "y"], errors);
                return;
            case "vector3":
                ValidateVectorConstructor(moduleContext, constructor, ["x", "y", "z"], errors);
                return;
            default:
                if (constructor.Arguments.Count != 1 || constructor.Arguments[0].Label is not null)
                {
                    AddTypeConstructorError(
                        moduleContext,
                        constructor.TypeName,
                        $"Type conversion ':{constructor.TypeName}' expects exactly one unlabeled argument",
                        errors);
                }

                return;
        }
    }

    private static void ValidateVectorConstructor(
        ParsedModule moduleContext,
        TypeConstructorExpressionNode constructor,
        IReadOnlyList<string> labels,
        GesValidationErrors errors)
    {
        if (constructor.Arguments.Count == 1 && constructor.Arguments[0].Label is null)
        {
            return;
        }

        if (constructor.TypeName == "vector3" &&
            constructor.Arguments.Count == 2 &&
            constructor.Arguments.All(argument => argument.Label is null))
        {
            return;
        }

        var labeledCount = constructor.Arguments.Count(argument => argument.Label is not null);
        if (labeledCount is not 0 && labeledCount != constructor.Arguments.Count)
        {
            AddTypeConstructorError(
                moduleContext,
                constructor.TypeName,
                $"Type constructor ':{constructor.TypeName}' cannot mix labeled and unlabeled component arguments",
                errors);
            return;
        }

        if (labeledCount > 0 || constructor.Arguments.Count == 0)
        {
            ValidateLabeledVectorConstructor(moduleContext, constructor, labels, errors);
            return;
        }

        if (constructor.Arguments.Count != labels.Count)
        {
            AddTypeConstructorError(
                moduleContext,
                constructor.TypeName,
                $"Type constructor ':{constructor.TypeName}' expects {labels.Count} component arguments",
                errors);
        }
    }

    private static void ValidateLabeledVectorConstructor(
        ParsedModule moduleContext,
        TypeConstructorExpressionNode constructor,
        IReadOnlyList<string> labels,
        GesValidationErrors errors)
    {
        var previousIndex = -1;
        for (var argumentIndex = 0; argumentIndex < constructor.Arguments.Count; argumentIndex++)
        {
            var label = constructor.Arguments[argumentIndex].Label;
            var componentIndex = IndexOf(labels, label);
            if (componentIndex < 0)
            {
                AddTypeConstructorError(
                    moduleContext,
                    constructor.TypeName,
                    $"Type constructor ':{constructor.TypeName}' has unknown component label '{label}'",
                    errors);
                return;
            }

            if (componentIndex <= previousIndex)
            {
                AddTypeConstructorError(
                    moduleContext,
                    constructor.TypeName,
                    $"Type constructor ':{constructor.TypeName}' component labels must follow x, y, z order",
                    errors);
                return;
            }

            previousIndex = componentIndex;
        }
    }

    private static int IndexOf(IReadOnlyList<string> values, string? value)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (string.Equals(values[index], value, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsBuiltinConstructorType(string typeName)
        => typeName is "nothing" or "tag" or "text" or "percentage" or "degree" or "meter" or "second" or
            "vector2" or "vector3" or "boolean" or "integer" or "decimal" or "number" or "sequence" or
            "list" or "range" or "message" or "handler" or "dictionary" or "set" or "dice" or "optional";

    private static void AddTypeConstructorError(
        ParsedModule moduleContext,
        string typeName,
        string message,
        GesValidationErrors errors)
        => errors.Add(
            moduleContext,
            message,
            typeName,
            GameEventScriptSymbolKind.Type,
            GameEventScriptModuleBuildErrorKind.InvalidTypeConstructor);

    private static void ValidateCallArity(
        ParsedModule moduleContext,
        GameEventScriptCallableKind kind,
        string name,
        int expectedCount,
        int actualCount,
        GesValidationErrors errors)
    {
        if (expectedCount != actualCount)
        {
            errors.Add(
                moduleContext,
                $"{kind} '{name}' expects {expectedCount} argument(s) but received {actualCount}",
                name,
                kind == GameEventScriptCallableKind.Rule ? GameEventScriptSymbolKind.Rule : GameEventScriptSymbolKind.Select,
                kind == GameEventScriptCallableKind.Rule ? GameEventScriptModuleBuildErrorKind.WrongRuleArity : GameEventScriptModuleBuildErrorKind.WrongSelectArity);
        }
    }

    private static void ValidateDuplicateNamedArguments(
        ParsedModule moduleContext,
        string symbolName,
        IReadOnlyList<ArgumentNode> arguments,
        GesValidationErrors errors)
    {
        var duplicateArguments = arguments
            .Where(argument => argument.Label is not null)
            .GroupBy(argument => argument.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        foreach (var duplicateArgument in duplicateArguments)
        {
            var duplicateArgumentNode = arguments.Last(argument => string.Equals(argument.Name, duplicateArgument, StringComparison.Ordinal));
            errors.Add(
                moduleContext,
                $"Named argument '{duplicateArgument}' is declared more than once",
                symbolName,
                GameEventScriptSymbolKind.Handler,
                GameEventScriptModuleBuildErrorKind.DuplicatePublishArgument,
                duplicateArgumentNode);
        }
    }

    private static void ValidateDuplicateHandlerLiteralParameters(
        ParsedModule moduleContext,
        HandlerLiteralExpressionNode handlerLiteral,
        GesValidationErrors errors)
    {
        var duplicateParameters = handlerLiteral.Parameters
            .GroupBy(parameter => parameter, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        foreach (var duplicateParameter in duplicateParameters)
        {
            var duplicateParameterNode = handlerLiteral.ParameterList.Last(parameter => string.Equals(parameter.LocalName, duplicateParameter, StringComparison.Ordinal));
            errors.Add(
                moduleContext,
                $"Handler literal '{handlerLiteral.Message}' declares parameter '{duplicateParameter}' more than once",
                handlerLiteral.Message,
                GameEventScriptSymbolKind.Handler,
                GameEventScriptModuleBuildErrorKind.DuplicateHandlerParameter,
                duplicateParameterNode);
        }
    }

    private static void ValidateIdentifierCase(
        ParsedModule moduleContext,
        string name,
        string symbol,
        GameEventScriptSymbolKind symbolKind,
        string message,
        GesValidationErrors errors)
    {
        if (IsIdentifierCase(name))
        {
            return;
        }

        errors.Add(
            moduleContext,
            message,
            symbol,
            symbolKind,
            GameEventScriptModuleBuildErrorKind.InvalidIdentifierCase);
    }

    private static void ValidateMessageCase(
        ParsedModule moduleContext,
        string name,
        string symbol,
        GameEventScriptSymbolKind symbolKind,
        string message,
        GesValidationErrors errors)
    {
        if (IsMessageCase(name))
        {
            return;
        }

        errors.Add(
            moduleContext,
            message,
            symbol,
            symbolKind,
            GameEventScriptModuleBuildErrorKind.InvalidMessageCase);
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
}
