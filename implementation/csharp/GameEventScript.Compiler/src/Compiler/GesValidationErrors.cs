// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using GameEventScript.Api;

namespace GameEventScript.Compiler;

internal sealed class GesValidationErrors
{
    private readonly List<GesDiagnostic> _errors = [];

    public int Count => _errors.Count;

    public void Add(GesParsedScript? module, string message, string symbol, GesDiagnosticSymbolKind symbolKind, string code, GesScriptNode? sourceNode = null)
    {
        var moduleName = string.IsNullOrWhiteSpace(module?.ModuleName) ? "UnknownModule" : module.ModuleName;
        var sourceName = string.IsNullOrWhiteSpace(module?.SourceName) ? "UnknownSource" : module.SourceName;
        var resolvedSourceNode = sourceNode ?? FindSourceNode(module, symbol, symbolKind);
        var sourceLocation = resolvedSourceNode?.SourceRange ??
                             module?.SourceRange ??
                             new GesSourceLocation(sourceName, ModuleName: moduleName);

        if (string.IsNullOrWhiteSpace(sourceLocation.ModuleName) ||
            string.Equals(sourceLocation.ModuleName, "UnknownModule", StringComparison.Ordinal))
        {
            sourceLocation = sourceLocation with { ModuleName = moduleName };
        }

        if (string.IsNullOrWhiteSpace(sourceLocation.SourceName) ||
            string.Equals(sourceLocation.SourceName, "UnknownSource", StringComparison.Ordinal))
        {
            sourceLocation = sourceLocation with { SourceName = sourceName };
        }

        _errors.Add(new GesDiagnostic(
            GesDiagnosticPhase.Validate,
            code,
            message,
            string.IsNullOrEmpty(symbol) ? null : symbol,
            symbolKind,
            sourceLocation,
            moduleName));
    }

    public void ThrowIfAny()
    {
        if (_errors.Count > 0)
        {
            throw new GesCompileException(_errors);
        }
    }

    private static GesScriptNode? FindSourceNode(GesParsedScript? module, string symbol, GesDiagnosticSymbolKind symbolKind)
    {
        if (module is null)
        {
            return null;
        }

        return symbolKind switch
        {
            GesDiagnosticSymbolKind.Type => FindTypeDefinition(module.TypeDefinitions, symbol),
            GesDiagnosticSymbolKind.Predicate => FindPredicateDefinition(module.PredicateDefinitions, symbol),
            GesDiagnosticSymbolKind.Function => FindFunctionDefinition(module.FunctionDefinitions, symbol),
            GesDiagnosticSymbolKind.Handler => FindHandler(module.Handlers, symbol) ??
                                                 FindNodeInModule(module, symbol),
            GesDiagnosticSymbolKind.Message => FindHandler(module.Handlers, symbol) ??
                                                 FindNodeInModule(module, symbol),
            GesDiagnosticSymbolKind.Variable => FindVariableNode(module, symbol),
            GesDiagnosticSymbolKind.GlobalDefinition => FindPredicateDefinition(module.PredicateDefinitions, symbol) ??
                                                          FindFunctionDefinition(module.FunctionDefinitions, symbol) ??
                                                          FindConstantDefinition(module.ConstantDefinitions, symbol) ??
                                                          FindNodeInModule(module, symbol),
            _ => FindNodeInModule(module, symbol)
        };
    }

    private static GesConstantDefinitionNode? FindConstantDefinition(IReadOnlyList<GesConstantDefinitionNode> definitions, string symbol)
    {
        for (var index = 0; index < definitions.Count; index++)
            if (string.Equals(definitions[index].Name, symbol, StringComparison.Ordinal)) return definitions[index];
        return null;
    }

    private static GesScriptNode? FindVariableNode(GesParsedScript parsedScript, string symbol)
    {
        foreach (var type in parsedScript.TypeDefinitions)
        {
            var field = FindField(type.Fields, symbol);
            if (field is not null)
            {
                return field;
            }
        }

        foreach (var rule in parsedScript.PredicateDefinitions)
        {
            var parameter = FindParameter(rule.ParameterList, symbol);
            if (parameter is not null)
            {
                return parameter;
            }

            var expressionNode = FindNodeInExpression(rule.Expression, symbol);
            if (expressionNode is not null)
            {
                return expressionNode;
            }
        }

        foreach (var select in parsedScript.FunctionDefinitions)
        {
            var parameter = FindParameter(select.ParameterList, symbol);
            if (parameter is not null)
            {
                return parameter;
            }

            var expressionNode = FindNodeInExpression(select.Expression, symbol);
            if (expressionNode is not null)
            {
                return expressionNode;
            }
        }

        foreach (var handler in parsedScript.Handlers)
        {
            var parameter = FindParameter(handler.ParameterList, symbol);
            if (parameter is not null)
            {
                return parameter;
            }

            var statementNode = FindNodeInStatements(handler.Statements, symbol);
            if (statementNode is not null)
            {
                return statementNode;
            }
        }

        return null;
    }

    private static GesScriptNode? FindNodeInModule(GesParsedScript parsedScript, string symbol)
    {
        foreach (var handler in parsedScript.Handlers)
        {
            var statementNode = FindNodeInStatements(handler.Statements, symbol);
            if (statementNode is not null)
            {
                return statementNode;
            }
        }

        foreach (var rule in parsedScript.PredicateDefinitions)
        {
            var expressionNode = FindNodeInExpression(rule.Expression, symbol);
            if (expressionNode is not null)
            {
                return expressionNode;
            }
        }

        foreach (var select in parsedScript.FunctionDefinitions)
        {
            var expressionNode = FindNodeInExpression(select.Expression, symbol);
            if (expressionNode is not null)
            {
                return expressionNode;
            }
        }

        return null;
    }

    private static GesScriptNode? FindNodeInStatements(IEnumerable<GesStatementNode> statements, string symbol)
    {
        foreach (var statement in statements)
        {
            var node = statement switch
            {
                GesLetStatementNode let => string.Equals(let.Identifier, symbol, StringComparison.Ordinal)
                    ? let
                    : FindNodeInExpression(let.Expression, symbol),
                GesForStatementNode forStatement => string.Equals(forStatement.Identifier, symbol, StringComparison.Ordinal)
                    ? forStatement
                    : FindNodeInIterationSource(forStatement.Source, symbol) ??
                      FindNodeInStatements(forStatement.Body.Statements, symbol),
                GesIfStatementNode ifStatement => ifStatement.Conditions.Select(condition => condition.Binding == symbol ? condition : FindNodeInExpression(condition.Expression, symbol)).FirstOrDefault(node => node is not null) ??
                                               FindNodeInStatements(ifStatement.ThenBody.Statements, symbol) ??
                                               (ifStatement.ElseBody is null ? null : FindNodeInStatements(ifStatement.ElseBody.Statements, symbol)),
                GesPublishStatementNode publish => FindNodeInExpression(publish.MessageExpression, symbol),
                GesExpressionStatementNode expressionStatement => FindNodeInExpression(expressionStatement.Expression, symbol),
                GesSeededRandomStatementNode seededRandom => FindNodeInExpression(seededRandom.SeedExpression, symbol) ??
                                                          FindNodeInStatements(seededRandom.Body.Statements, symbol),
                _ => null
            };

            if (node is not null)
            {
                return node;
            }
        }

        return null;
    }

    private static GesScriptNode? FindNodeInIterationSource(GesIterationSourceNode source, string symbol)
        => source switch
        {
            GesCollectionIterationSourceNode collection => FindNodeInExpression(collection.Expression, symbol),
            GesRangeIterationSourceNode range => FindNodeInExpression(range.RangeExpression, symbol),
            _ => null
        };

    private static GesScriptNode? FindNodeInExpression(GesExpressionNode expression, string symbol)
    {
        switch (expression)
        {
            case GesConstantReferenceExpressionNode constant when string.Equals(constant.Name, symbol, StringComparison.Ordinal):
                return constant;
            case GesIdentifierExpressionNode identifier when string.Equals(identifier.Name, symbol, StringComparison.Ordinal):
                return identifier;
            case GesCallExpressionNode call when string.Equals(call.Name, symbol, StringComparison.Ordinal):
                return call;
            case GesPredicateCallExpressionNode predicateCall when string.Equals(predicateCall.PredicateName, symbol, StringComparison.Ordinal):
                return predicateCall;
            case GesTypeConstructorExpressionNode constructor when string.Equals(constructor.TypeName, symbol, StringComparison.Ordinal):
                return constructor;
            case GesMessageLiteralExpressionNode message when string.Equals(message.Message, symbol, StringComparison.Ordinal):
                return message;
            case GesHandlerLiteralExpressionNode handler when string.Equals(handler.Message, symbol, StringComparison.Ordinal):
                return handler;
        }

        return expression switch
        {
            GesSendExpressionNode send => (send.Delay is null ? null : FindNodeInExpression(send.Delay, symbol)) ?? FindNodeInExpression(send.Message, symbol),
            GesUnaryExpressionNode unary => FindNodeInExpression(unary.Operand, symbol),
            GesBinaryExpressionNode binary => FindNodeInExpression(binary.Left, symbol) ?? FindNodeInExpression(binary.Right, symbol),
            GesTypeCastExpressionNode cast => FindNodeInExpression(cast.Value, symbol),
            GesTypeCheckExpressionNode check => FindNodeInExpression(check.Value, symbol),
            GesNothingCheckExpressionNode check => FindNodeInExpression(check.Value, symbol),
            GesPredicateCallExpressionNode predicateCall => predicateCall.Value is null ? null : FindNodeInExpression(predicateCall.Value, symbol),
            GesExtensionPredicateExpressionNode extensionPredicate => FindNodeInExpression(extensionPredicate.Value, symbol),
            GesMemberAccessExpressionNode member => FindNodeInExpression(member.Target, symbol),
            GesCombinedCollectionExpressionNode combined => FindNodeInExpressions(combined.Sources, symbol),
            GesCollectionAccessExpressionNode collection => FindNodeInExpression(collection.Target, symbol) ?? FindNodeInSelector(collection.Selector, symbol),
            GesRangeExpressionNode range => FindNodeInExpression(range.FromExpression, symbol) ??
                                         FindNodeInExpression(range.ToExpression, symbol) ??
                                         (range.StepExpression is null ? null : FindNodeInExpression(range.StepExpression, symbol)),
            GesRandomExpressionNode random => FindNodeInExpression(random.FromExpression, symbol) ?? FindNodeInExpression(random.ToExpression, symbol),
            GesSeededRandomExpressionNode seeded => FindNodeInExpression(seeded.SeedExpression, symbol) ?? FindNodeInExpression(seeded.BodyExpression, symbol),
            GesVariadicTaggedExpressionNode variadic => FindNodeInExpressions(variadic.Arguments, symbol),
            GesBetweenExpressionNode between => FindNodeInExpression(between.Value, symbol) ??
                                         FindNodeInExpression(between.Minimum, symbol) ??
                                         FindNodeInExpression(between.Maximum, symbol),
            GesClampExpressionNode clamp => FindNodeInExpression(clamp.Value, symbol) ??
                                         FindNodeInExpression(clamp.Minimum, symbol) ??
                                         FindNodeInExpression(clamp.Maximum, symbol),
            GesGeneratedCollectionExpressionNode generated => FindNodeInIterationSource(generated.Source, symbol) ??
                                                           (generated.Predicate is null ? null : FindNodeInExpression(generated.Predicate, symbol)) ??
                                                           FindNodeInExpression(generated.Projection, symbol),
            GesGuardedChoiceExpressionNode guarded => FindNodeInGuardedChoice(guarded, symbol),
            GesListLiteralExpressionNode list => FindNodeInExpressions(list.Items, symbol),
            GesMapLiteralExpressionNode dictionary => FindNodeInMapEntries(dictionary.Entries, symbol),
            GesMessageLiteralExpressionNode message => FindNodeInArguments(message.Arguments, symbol),
            GesCallExpressionNode call => FindNodeInArguments(call.ArgumentList.Arguments, symbol),
            GesExtensionCallExpressionNode extension => FindNodeInArguments(extension.Arguments, symbol),
            GesTypeConstructorExpressionNode constructor => FindNodeInArguments(constructor.Arguments, symbol),
            _ => null
        };
    }

    private static GesScriptNode? FindNodeInExpressions(IEnumerable<GesExpressionNode> expressions, string symbol)
    {
        foreach (var expression in expressions)
        {
            var node = FindNodeInExpression(expression, symbol);
            if (node is not null)
            {
                return node;
            }
        }

        return null;
    }

    private static GesScriptNode? FindNodeInArguments(IEnumerable<GesArgumentNode> arguments, string symbol)
    {
        foreach (var argument in arguments)
        {
            if (string.Equals(argument.Label, symbol, StringComparison.Ordinal))
            {
                return argument;
            }

            var expressionNode = FindNodeInExpression(argument.Expression, symbol);
            if (expressionNode is not null)
            {
                return expressionNode;
            }
        }

        return null;
    }

    private static GesScriptNode? FindNodeInSelector(GesCollectionSelectorNode selector, string symbol)
        => selector switch
        {
            GesExpressionSelectorNode expressionSelector => FindNodeInExpression(expressionSelector.Expression, symbol),
            GesPredicateSelectorNode predicateSelector => FindNodeInExpression(predicateSelector.Predicate, symbol),
            GesCountSelectorNode countSelector => FindNodeInExpression(countSelector.Predicate, symbol),
            GesSeriesTermSelectorNode seriesTermSelector => FindNodeInExpression(seriesTermSelector.IndexExpression, symbol),
            GesFilterSelectorNode filterSelector => FindNodeInExpression(filterSelector.Predicate, symbol),
            GesSumSelectorNode sumSelector => FindNodeInExpression(sumSelector.Projection, symbol),
            GesAverageSelectorNode averageSelector => FindNodeInExpression(averageSelector.Projection, symbol),
            GesFoldSelectorNode fold => (fold.Seed is null ? null : FindNodeInExpression(fold.Seed, symbol)) ?? FindNodeInExpression(fold.Projection, symbol),
            GesForeachSelectorNode each => FindNodeInExpression(each.Expression, symbol),
            GesSelectSelectorNode selectSelector => FindNodeInExpression(selectSelector.Projection, symbol),
            GesMapSelectorNode dictionarySelector => FindNodeInExpression(dictionarySelector.KeyProjection, symbol) ??
                                                         (dictionarySelector.ValueProjection is null ? null : FindNodeInExpression(dictionarySelector.ValueProjection, symbol)),
            GesMinSelectorNode minSelector => FindNodeInExpression(minSelector.Projection, symbol),
            GesMaxSelectorNode maxSelector => FindNodeInExpression(maxSelector.Projection, symbol),
            GesContainsSelectorNode containsSelector => FindNodeInExpression(containsSelector.ValueExpression, symbol),
            GesDistinctSelectorNode distinctSelector => distinctSelector.Projection is null ? null : FindNodeInExpression(distinctSelector.Projection, symbol),
            GesGroupBySelectorNode groupBySelector => FindNodeInExpression(groupBySelector.Projection, symbol),
            GesOrderBySelectorNode orderBySelector => FindNodeInExpression(orderBySelector.Projection, symbol),
            GesChooseSelectorNode chooseSelector => (chooseSelector.Predicate is null ? null : FindNodeInExpression(chooseSelector.Predicate, symbol)) ??
                                                 (chooseSelector.WeightExpression is null ? null : FindNodeInExpression(chooseSelector.WeightExpression, symbol)),
            _ => null
        };

    private static GesTypeDefinitionNode? FindTypeDefinition(IReadOnlyList<GesTypeDefinitionNode> definitions, string symbol)
    {
        for (var index = 0; index < definitions.Count; index++)
        {
            var definition = definitions[index];
            if (string.Equals(definition.Name, symbol, StringComparison.Ordinal))
            {
                return definition;
            }
        }

        return null;
    }

    private static GesPredicateDefinitionNode? FindPredicateDefinition(IReadOnlyList<GesPredicateDefinitionNode> definitions, string symbol)
    {
        for (var index = 0; index < definitions.Count; index++)
        {
            var definition = definitions[index];
            if (string.Equals(definition.Name, symbol, StringComparison.Ordinal))
            {
                return definition;
            }
        }

        return null;
    }

    private static GesFunctionDefinitionNode? FindFunctionDefinition(IReadOnlyList<GesFunctionDefinitionNode> definitions, string symbol)
    {
        for (var index = 0; index < definitions.Count; index++)
        {
            var definition = definitions[index];
            if (string.Equals(definition.Name, symbol, StringComparison.Ordinal))
            {
                return definition;
            }
        }

        return null;
    }

    private static GesEventHandlerNode? FindHandler(IReadOnlyList<GesEventHandlerNode> handlers, string symbol)
    {
        for (var index = 0; index < handlers.Count; index++)
        {
            var handler = handlers[index];
            if (string.Equals(handler.Message, symbol, StringComparison.Ordinal))
            {
                return handler;
            }
        }

        return null;
    }

    private static GesTypeFieldDefinitionNode? FindField(IReadOnlyList<GesTypeFieldDefinitionNode> fields, string symbol)
    {
        for (var index = 0; index < fields.Count; index++)
        {
            var field = fields[index];
            if (string.Equals(field.Name, symbol, StringComparison.Ordinal))
            {
                return field;
            }
        }

        return null;
    }

    private static GesParameterNode? FindParameter(IReadOnlyList<GesParameterNode> parameters, string symbol)
    {
        for (var index = 0; index < parameters.Count; index++)
        {
            var parameter = parameters[index];
            if (string.Equals(parameter.LocalName, symbol, StringComparison.Ordinal))
            {
                return parameter;
            }
        }

        return null;
    }

    private static GesScriptNode? FindNodeInGuardedChoice(GesGuardedChoiceExpressionNode guarded, string symbol)
    {
        for (var index = 0; index < guarded.Branches.Count; index++)
        {
            var branch = guarded.Branches[index];
            var node = FindNodeInExpression(branch.ValueExpression, symbol) ??
                       FindNodeInExpression(branch.ConditionExpression, symbol);
            if (node is not null)
            {
                return node;
            }
        }

        return FindNodeInExpression(guarded.OtherwiseExpression, symbol);
    }

    private static GesScriptNode? FindNodeInMapEntries(IReadOnlyList<GesMapEntryNode> entries, string symbol)
    {
        for (var index = 0; index < entries.Count; index++)
        {
            var node = FindNodeInExpression(entries[index].Value, symbol);
            if (node is not null)
            {
                return node;
            }
        }

        return null;
    }
}
