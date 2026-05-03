using System;
using System.Collections.Generic;
using System.Linq;

namespace StepH.GameEventScript.Compiler;

internal sealed class GesValidationErrors
{
    private readonly List<GameEventScriptModuleBuildError> _errors = [];

    public int Count => _errors.Count;

    public void Add(
        ParsedModule? module,
        string message,
        string symbol,
        GameEventScriptSymbolKind symbolKind,
        GameEventScriptModuleBuildErrorKind kind,
        ScriptNode? sourceNode = null)
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

        _errors.Add(new GameEventScriptModuleBuildError(message, moduleName, symbol, symbolKind, kind, sourceLocation));
    }

    public void ThrowIfAny()
    {
        if (_errors.Count > 0)
        {
            throw new GameEventScriptModuleBuildException(_errors);
        }
    }

    private static ScriptNode? FindSourceNode(ParsedModule? module, string symbol, GameEventScriptSymbolKind symbolKind)
    {
        if (module is null)
        {
            return null;
        }

        return symbolKind switch
        {
            GameEventScriptSymbolKind.Type => module.TypeDefinitions.FirstOrDefault(type => string.Equals(type.Name, symbol, StringComparison.Ordinal)),
            GameEventScriptSymbolKind.Rule => module.RuleDefinitions.FirstOrDefault(rule => string.Equals(rule.Name, symbol, StringComparison.Ordinal)),
            GameEventScriptSymbolKind.Select => module.SelectDefinitions.FirstOrDefault(select => string.Equals(select.Name, symbol, StringComparison.Ordinal)),
            GameEventScriptSymbolKind.Handler => module.Handlers.FirstOrDefault(handler => string.Equals(handler.Message, symbol, StringComparison.Ordinal)) ??
                                                 FindNodeInModule(module, symbol),
            GameEventScriptSymbolKind.Message => module.Handlers.FirstOrDefault(handler => string.Equals(handler.Message, symbol, StringComparison.Ordinal)) ??
                                                 FindNodeInModule(module, symbol),
            GameEventScriptSymbolKind.Variable => FindVariableNode(module, symbol),
            GameEventScriptSymbolKind.GlobalDefinition => module.RuleDefinitions.FirstOrDefault(rule => string.Equals(rule.Name, symbol, StringComparison.Ordinal)) ??
                                                          module.SelectDefinitions.FirstOrDefault(select => string.Equals(select.Name, symbol, StringComparison.Ordinal)) ??
                                                          FindNodeInModule(module, symbol),
            _ => FindNodeInModule(module, symbol)
        };
    }

    private static ScriptNode? FindVariableNode(ParsedModule module, string symbol)
    {
        foreach (var type in module.TypeDefinitions)
        {
            var field = type.Fields.FirstOrDefault(candidate => string.Equals(candidate.Name, symbol, StringComparison.Ordinal));
            if (field is not null)
            {
                return field;
            }
        }

        foreach (var rule in module.RuleDefinitions)
        {
            var parameter = rule.ParameterList.FirstOrDefault(candidate => string.Equals(candidate.LocalName, symbol, StringComparison.Ordinal));
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

        foreach (var select in module.SelectDefinitions)
        {
            var parameter = select.ParameterList.FirstOrDefault(candidate => string.Equals(candidate.LocalName, symbol, StringComparison.Ordinal));
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

        foreach (var handler in module.Handlers)
        {
            var parameter = handler.ParameterList.FirstOrDefault(candidate => string.Equals(candidate.LocalName, symbol, StringComparison.Ordinal));
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

    private static ScriptNode? FindNodeInModule(ParsedModule module, string symbol)
    {
        foreach (var handler in module.Handlers)
        {
            var statementNode = FindNodeInStatements(handler.Statements, symbol);
            if (statementNode is not null)
            {
                return statementNode;
            }
        }

        foreach (var rule in module.RuleDefinitions)
        {
            var expressionNode = FindNodeInExpression(rule.Expression, symbol);
            if (expressionNode is not null)
            {
                return expressionNode;
            }
        }

        foreach (var select in module.SelectDefinitions)
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
            case RulePredicateExpressionNode rulePredicate when string.Equals(rulePredicate.RuleName, symbol, StringComparison.Ordinal):
                return rulePredicate;
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
            RulePredicateExpressionNode rulePredicate => FindNodeInExpression(rulePredicate.Value, symbol),
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
            GuardedChoiceExpressionNode guarded => guarded.Branches
                                                       .Select(branch => FindNodeInExpression(branch.ValueExpression, symbol) ??
                                                                         FindNodeInExpression(branch.ConditionExpression, symbol))
                                                       .FirstOrDefault(node => node is not null) ??
                                                   FindNodeInExpression(guarded.OtherwiseExpression, symbol),
            ListLiteralExpressionNode list => FindNodeInExpressions(list.Items, symbol),
            SetLiteralExpressionNode set => FindNodeInExpressions(set.Items, symbol),
            SequenceLiteralExpressionNode sequence => FindNodeInExpressions(sequence.Items, symbol),
            DictionaryLiteralExpressionNode dictionary => dictionary.Entries
                .Select(entry => FindNodeInExpression(entry.Value, symbol))
                .FirstOrDefault(node => node is not null),
            MessageLiteralExpressionNode message => FindNodeInArguments(message.Arguments, symbol),
            HandlerBindExpressionNode bind => FindNodeInExpression(bind.CalleeExpression, symbol) ?? FindNodeInArguments(bind.Arguments, symbol),
            CallExpressionNode call => FindNodeInArguments(call.ArgumentList.Arguments, symbol),
            ExtensionCallExpressionNode extension => FindNodeInArguments(extension.Arguments, symbol),
            TypeConstructorExpressionNode constructor => FindNodeInArguments(constructor.Arguments, symbol),
            _ => null
        };
    }

    private static ScriptNode? FindNodeInExpressions(IEnumerable<ExpressionNode> expressions, string symbol)
        => expressions.Select(expression => FindNodeInExpression(expression, symbol)).FirstOrDefault(node => node is not null);

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
            FilterSelectorNode filterSelector => FindNodeInExpression(filterSelector.Predicate, symbol),
            SumSelectorNode sumSelector => FindNodeInExpression(sumSelector.Projection, symbol),
            AverageSelectorNode averageSelector => FindNodeInExpression(averageSelector.Projection, symbol),
            SelectSelectorNode selectSelector => FindNodeInExpression(selectSelector.Projection, symbol),
            DictionarySelectorNode dictionarySelector => FindNodeInExpression(dictionarySelector.KeyProjection, symbol) ??
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
}
