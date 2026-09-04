// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.Compiler;

internal sealed class GesValidationErrors
{
    private readonly List<GameEventScriptDiagnostic> _errors = [];

    public int Count => _errors.Count;

    public void Add(ParsedScript? module, string message, string symbol, GameEventScriptSymbolKind symbolKind, string code, ScriptNode? sourceNode = null)
    {
        var moduleName = string.IsNullOrWhiteSpace(module?.ModuleName) ? "UnknownModule" : module.ModuleName;
        var sourceName = string.IsNullOrWhiteSpace(module?.SourceName) ? "UnknownSource" : module.SourceName;
        var resolvedSourceNode = sourceNode ?? FindSourceNode(module, symbol, symbolKind);
        var sourceLocation = resolvedSourceNode?.SourceRange ??
                             module?.SourceRange ??
                             new GameEventScriptSourceLocation(sourceName, ModuleName: moduleName);

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

        _errors.Add(new GameEventScriptDiagnostic(
            GameEventScriptDiagnosticPhase.Validate,
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
            throw new GameEventScriptCompileException(_errors);
        }
    }

    private static ScriptNode? FindSourceNode(ParsedScript? module, string symbol, GameEventScriptSymbolKind symbolKind)
    {
        if (module is null)
        {
            return null;
        }

        return symbolKind switch
        {
            GameEventScriptSymbolKind.Type => FindTypeDefinition(module.TypeDefinitions, symbol),
            GameEventScriptSymbolKind.Predicate => FindPredicateDefinition(module.PredicateDefinitions, symbol),
            GameEventScriptSymbolKind.Function => FindFunctionDefinition(module.FunctionDefinitions, symbol),
            GameEventScriptSymbolKind.Handler => FindHandler(module.Handlers, symbol) ??
                                                 FindNodeInModule(module, symbol),
            GameEventScriptSymbolKind.Message => FindHandler(module.Handlers, symbol) ??
                                                 FindNodeInModule(module, symbol),
            GameEventScriptSymbolKind.Variable => FindVariableNode(module, symbol),
            GameEventScriptSymbolKind.GlobalDefinition => FindPredicateDefinition(module.PredicateDefinitions, symbol) ??
                                                          FindFunctionDefinition(module.FunctionDefinitions, symbol) ??
                                                          FindNodeInModule(module, symbol),
            _ => FindNodeInModule(module, symbol)
        };
    }

    private static ScriptNode? FindVariableNode(ParsedScript parsedScript, string symbol)
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

    private static ScriptNode? FindNodeInModule(ParsedScript parsedScript, string symbol)
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

    private static ScriptNode? FindNodeInStatements(IEnumerable<StatementNode> statements, string symbol)
    {
        foreach (var statement in statements)
        {
            var node = statement switch
            {
                LetStatementNode let => string.Equals(let.Identifier, symbol, StringComparison.Ordinal)
                    ? let
                    : FindNodeInExpression(let.Expression, symbol),
                ForStatementNode forStatement => string.Equals(forStatement.Identifier, symbol, StringComparison.Ordinal)
                    ? forStatement
                    : FindNodeInIterationSource(forStatement.Source, symbol) ??
                      FindNodeInStatements(forStatement.Body.Statements, symbol),
                IfStatementNode ifStatement => FindNodeInExpression(ifStatement.Condition, symbol) ??
                                               FindNodeInStatements(ifStatement.ThenBody.Statements, symbol) ??
                                               (ifStatement.ElseBody is null ? null : FindNodeInStatements(ifStatement.ElseBody.Statements, symbol)),
                PublishStatementNode publish => FindNodeInExpression(publish.MessageExpression, symbol),
                ExpressionStatementNode expressionStatement => FindNodeInExpression(expressionStatement.Expression, symbol),
                SeededRandomStatementNode seededRandom => FindNodeInExpression(seededRandom.SeedExpression, symbol) ??
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

    private static ScriptNode? FindNodeInIterationSource(IterationSourceNode source, string symbol)
        => source switch
        {
            CollectionIterationSourceNode collection => FindNodeInExpression(collection.Expression, symbol),
            RangeIterationSourceNode range => FindNodeInExpression(range.RangeExpression, symbol),
            _ => null
        };

    private static ScriptNode? FindNodeInExpression(ExpressionNode expression, string symbol)
    {
        switch (expression)
        {
            case IdentifierExpressionNode identifier when string.Equals(identifier.Name, symbol, StringComparison.Ordinal):
                return identifier;
            case CallExpressionNode call when string.Equals(call.Name, symbol, StringComparison.Ordinal):
                return call;
            case PredicateCallExpressionNode predicateCall when string.Equals(predicateCall.PredicateName, symbol, StringComparison.Ordinal):
                return predicateCall;
            case TypeConstructorExpressionNode constructor when string.Equals(constructor.TypeName, symbol, StringComparison.Ordinal):
                return constructor;
            case MessageLiteralExpressionNode message when string.Equals(message.Message, symbol, StringComparison.Ordinal):
                return message;
            case HandlerLiteralExpressionNode handler when string.Equals(handler.Message, symbol, StringComparison.Ordinal):
                return handler;
        }

        return expression switch
        {
            UnaryExpressionNode unary => FindNodeInExpression(unary.Operand, symbol),
            BinaryExpressionNode binary => FindNodeInExpression(binary.Left, symbol) ?? FindNodeInExpression(binary.Right, symbol),
            TypeCastExpressionNode cast => FindNodeInExpression(cast.Value, symbol),
            TypeCheckExpressionNode check => FindNodeInExpression(check.Value, symbol),
            PredicateCallExpressionNode predicateCall => FindNodeInExpression(predicateCall.Value, symbol),
            ExtensionPredicateExpressionNode extensionPredicate => FindNodeInExpression(extensionPredicate.Value, symbol),
            MemberAccessExpressionNode member => FindNodeInExpression(member.Target, symbol),
            CollectionAccessExpressionNode collection => FindNodeInExpression(collection.Target, symbol) ?? FindNodeInSelector(collection.Selector, symbol),
            RangeExpressionNode range => FindNodeInExpression(range.FromExpression, symbol) ??
                                         FindNodeInExpression(range.ToExpression, symbol) ??
                                         (range.StepExpression is null ? null : FindNodeInExpression(range.StepExpression, symbol)),
            RandomExpressionNode random => FindNodeInExpression(random.FromExpression, symbol) ?? FindNodeInExpression(random.ToExpression, symbol),
            SeededRandomExpressionNode seeded => FindNodeInExpression(seeded.SeedExpression, symbol) ?? FindNodeInExpression(seeded.BodyExpression, symbol),
            VariadicTaggedExpressionNode variadic => FindNodeInExpressions(variadic.Arguments, symbol),
            ClampExpressionNode clamp => FindNodeInExpression(clamp.Value, symbol) ??
                                         FindNodeInExpression(clamp.Minimum, symbol) ??
                                         FindNodeInExpression(clamp.Maximum, symbol),
            GeneratedCollectionExpressionNode generated => FindNodeInIterationSource(generated.Source, symbol) ??
                                                           (generated.Predicate is null ? null : FindNodeInExpression(generated.Predicate, symbol)) ??
                                                           FindNodeInExpression(generated.Projection, symbol),
            GuardedChoiceExpressionNode guarded => FindNodeInGuardedChoice(guarded, symbol),
            ListLiteralExpressionNode list => FindNodeInExpressions(list.Items, symbol),
            MapLiteralExpressionNode dictionary => FindNodeInMapEntries(dictionary.Entries, symbol),
            MessageLiteralExpressionNode message => FindNodeInArguments(message.Arguments, symbol),
            CallExpressionNode call => FindNodeInArguments(call.ArgumentList.Arguments, symbol),
            ExtensionCallExpressionNode extension => FindNodeInArguments(extension.Arguments, symbol),
            TypeConstructorExpressionNode constructor => FindNodeInArguments(constructor.Arguments, symbol),
            _ => null
        };
    }

    private static ScriptNode? FindNodeInExpressions(IEnumerable<ExpressionNode> expressions, string symbol)
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

    private static ScriptNode? FindNodeInArguments(IEnumerable<ArgumentNode> arguments, string symbol)
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

    private static ScriptNode? FindNodeInSelector(CollectionSelectorNode selector, string symbol)
        => selector switch
        {
            ExpressionSelectorNode expressionSelector => FindNodeInExpression(expressionSelector.Expression, symbol),
            PredicateSelectorNode predicateSelector => FindNodeInExpression(predicateSelector.Predicate, symbol),
            CountSelectorNode countSelector => FindNodeInExpression(countSelector.Predicate, symbol),
            SeriesTermSelectorNode seriesTermSelector => FindNodeInExpression(seriesTermSelector.IndexExpression, symbol),
            FilterSelectorNode filterSelector => FindNodeInExpression(filterSelector.Predicate, symbol),
            SumSelectorNode sumSelector => FindNodeInExpression(sumSelector.Projection, symbol),
            AverageSelectorNode averageSelector => FindNodeInExpression(averageSelector.Projection, symbol),
            SelectSelectorNode selectSelector => FindNodeInExpression(selectSelector.Projection, symbol),
            MapSelectorNode dictionarySelector => FindNodeInExpression(dictionarySelector.KeyProjection, symbol) ??
                                                         (dictionarySelector.ValueProjection is null ? null : FindNodeInExpression(dictionarySelector.ValueProjection, symbol)),
            MinSelectorNode minSelector => FindNodeInExpression(minSelector.Projection, symbol),
            MaxSelectorNode maxSelector => FindNodeInExpression(maxSelector.Projection, symbol),
            ContainsSelectorNode containsSelector => FindNodeInExpression(containsSelector.ValueExpression, symbol),
            DistinctSelectorNode distinctSelector => distinctSelector.Projection is null ? null : FindNodeInExpression(distinctSelector.Projection, symbol),
            GroupBySelectorNode groupBySelector => FindNodeInExpression(groupBySelector.Projection, symbol),
            OrderBySelectorNode orderBySelector => FindNodeInExpression(orderBySelector.Projection, symbol),
            ChooseSelectorNode chooseSelector => (chooseSelector.Predicate is null ? null : FindNodeInExpression(chooseSelector.Predicate, symbol)) ??
                                                 (chooseSelector.WeightExpression is null ? null : FindNodeInExpression(chooseSelector.WeightExpression, symbol)),
            _ => null
        };

    private static TypeDefinitionNode? FindTypeDefinition(IReadOnlyList<TypeDefinitionNode> definitions, string symbol)
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

    private static PredicateDefinitionNode? FindPredicateDefinition(IReadOnlyList<PredicateDefinitionNode> definitions, string symbol)
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

    private static FunctionDefinitionNode? FindFunctionDefinition(IReadOnlyList<FunctionDefinitionNode> definitions, string symbol)
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

    private static EventHandlerNode? FindHandler(IReadOnlyList<EventHandlerNode> handlers, string symbol)
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

    private static TypeFieldDefinitionNode? FindField(IReadOnlyList<TypeFieldDefinitionNode> fields, string symbol)
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

    private static ParameterNode? FindParameter(IReadOnlyList<ParameterNode> parameters, string symbol)
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

    private static ScriptNode? FindNodeInGuardedChoice(GuardedChoiceExpressionNode guarded, string symbol)
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

    private static ScriptNode? FindNodeInMapEntries(IReadOnlyList<MapEntryNode> entries, string symbol)
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
