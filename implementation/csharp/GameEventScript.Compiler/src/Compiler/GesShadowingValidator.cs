// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Api;
using GameEventScript.Syntax;

namespace GameEventScript.Compiler;

internal static class GesShadowingValidator
{
    private sealed class Scope(Scope? parent = null)
    {
        private readonly HashSet<string> _names = new(StringComparer.Ordinal);

        public Scope? Parent { get; } = parent;

        public void Declare(string name) => _names.Add(name);

        public bool ContainsVisible(string name)
        {
            for (Scope? scope = this; scope is not null; scope = scope.Parent)
                if (scope._names.Contains(name)) return true;
            return false;
        }

        public bool ContainsInAncestor(string name) => Parent?.ContainsVisible(name) == true;
    }

    public static void Validate(GesParsedScript script, GesValidationErrors errors)
    {
        for (var typeIndex = 0; typeIndex < script.TypeDefinitions.Count; typeIndex++)
        {
            var type = script.TypeDefinitions[typeIndex];
            var scope = new Scope();
            for (var fieldIndex = 0; fieldIndex < type.Fields.Count; fieldIndex++) scope.Declare(type.Fields[fieldIndex].Name);
            for (var fieldIndex = 0; fieldIndex < type.Fields.Count; fieldIndex++)
            {
                var field = type.Fields[fieldIndex];
                VisitOptional(field.MinimumExpression, scope, script, errors);
                VisitOptional(field.MaximumExpression, scope, script, errors);
                VisitOptional(field.ComputedExpression, scope, script, errors);
            }
        }

        for (var index = 0; index < script.PredicateDefinitions.Count; index++)
        {
            var definition = script.PredicateDefinitions[index];
            VisitExpression(definition.Expression, CreateParameterScope(definition.ParameterList), script, errors);
        }

        for (var index = 0; index < script.FunctionDefinitions.Count; index++)
        {
            var definition = script.FunctionDefinitions[index];
            VisitExpression(definition.Expression, CreateParameterScope(definition.ParameterList), script, errors);
        }

        for (var index = 0; index < script.Handlers.Count; index++)
        {
            var handler = script.Handlers[index];
            VisitStatements(handler.Statements, CreateParameterScope(handler.ParameterList), script, errors);
        }
    }

    private static Scope CreateParameterScope(IReadOnlyList<GesParameterNode> parameters)
    {
        var scope = new Scope();
        for (var index = 0; index < parameters.Count; index++) scope.Declare(parameters[index].LocalName);
        return scope;
    }

    private static void VisitStatements(IReadOnlyList<GesStatementNode> statements, Scope scope, GesParsedScript script, GesValidationErrors errors)
    {
        for (var index = 0; index < statements.Count; index++)
        {
            switch (statements[index])
            {
                case GesPublishStatementNode publish:
                    VisitExpression(publish.MessageExpression, scope, script, errors);
                    VisitExpressions(publish.TagExpressions, scope, script, errors);
                    break;
                case GesAssemblyStatementNode assembly:
                    foreach (var declaration in assembly.Block.Declarations)
                    {
                        if (scope.ContainsVisible(declaration.Name)) AddShadowError(script, errors, declaration.Name, declaration);
                        if (declaration.Export) scope.Declare(declaration.Name);
                    }
                    break;
                case GesLetStatementNode let:
                    VisitExpression(let.Expression, scope, script, errors);
                    if (scope.ContainsInAncestor(let.Identifier)) AddShadowError(script, errors, let.Identifier, let);
                    scope.Declare(let.Identifier);
                    break;
                case GesIfStatementNode ifStatement:
                    var conditionScope = new Scope(scope);
                    foreach (var condition in ifStatement.Conditions)
                    {
                        VisitExpression(condition.Expression, conditionScope, script, errors);
                        if (condition.Binding is { } binding)
                        {
                            if (conditionScope.ContainsInAncestor(binding)) AddShadowError(script, errors, binding, condition);
                            conditionScope.Declare(binding);
                        }
                    }
                    VisitStatements(ifStatement.ThenBody.Statements, conditionScope, script, errors);
                    if (ifStatement.ElseBody is not null) VisitStatements(ifStatement.ElseBody.Statements, new Scope(scope), script, errors);
                    break;
                case GesForStatementNode forStatement:
                    VisitIterationSource(forStatement.Source, scope, script, errors);
                    var loopScope = CreateBinderScope(scope, forStatement.Identifier, forStatement, script, errors);
                    VisitBody(forStatement.Body, loopScope, script, errors);
                    break;
                case GesSeededRandomStatementNode seededRandom:
                    VisitExpression(seededRandom.SeedExpression, scope, script, errors);
                    VisitStatements(seededRandom.Body.Statements, new Scope(scope), script, errors);
                    break;
                case GesExpressionStatementNode expressionStatement: VisitExpression(expressionStatement.Expression, scope, script, errors); break;
            }
        }
    }

    private static void VisitBody(GesStatementBodyNode body, Scope parent, GesParsedScript script, GesValidationErrors errors) => VisitStatements(body.Statements, body.IsBlock ? new Scope(parent) : parent, script, errors);

    private static void VisitExpression(GesExpressionNode expression, Scope scope, GesParsedScript script, GesValidationErrors errors)
    {
        switch (expression)
        {
            case GesMessageLiteralExpressionNode message: VisitArguments(message.ArgumentList.Arguments, scope, script, errors); break;
            case GesCallExpressionNode call: VisitArguments(call.ArgumentList.Arguments, scope, script, errors); break;
            case GesExtensionCallExpressionNode extension: VisitArguments(extension.ArgumentList.Arguments, scope, script, errors); break;
            case GesTypeConstructorExpressionNode constructor: VisitArguments(constructor.ArgumentList.Arguments, scope, script, errors); break;
            case GesCombinedCollectionExpressionNode combined: VisitExpressions(combined.Sources, scope, script, errors); break;
            case GesListLiteralExpressionNode list: VisitExpressions(list.Items, scope, script, errors); break;
            case GesMapLiteralExpressionNode map:
                for (var index = 0; index < map.Entries.Count; index++) VisitExpression(map.Entries[index].Value, scope, script, errors);
                break;
            case GesSendExpressionNode send:
                VisitOptional(send.Delay, scope, script, errors);
                VisitExpression(send.Message, scope, script, errors);
                VisitExpressions(send.Tags, scope, script, errors);
                break;
            case GesUnaryExpressionNode unary: VisitExpression(unary.Operand, scope, script, errors); break;
            case GesIntrinsicCallExpressionNode intrinsic: VisitExpressions(intrinsic.Arguments, scope, script, errors); break;
            case GesVariadicTaggedExpressionNode variadic: VisitExpressions(variadic.Arguments, scope, script, errors); break;
            case GesBetweenExpressionNode between:
                VisitExpression(between.Value, scope, script, errors);
                VisitExpression(between.Minimum, scope, script, errors);
                VisitExpression(between.Maximum, scope, script, errors);
                break;
            case GesClampExpressionNode clamp:
                VisitExpression(clamp.Value, scope, script, errors);
                VisitExpression(clamp.Minimum, scope, script, errors);
                VisitExpression(clamp.Maximum, scope, script, errors);
                break;
            case GesRangeExpressionNode range:
                VisitExpression(range.FromExpression, scope, script, errors);
                VisitExpression(range.ToExpression, scope, script, errors);
                VisitOptional(range.StepExpression, scope, script, errors);
                break;
            case GesRandomExpressionNode random:
                VisitExpression(random.FromExpression, scope, script, errors);
                VisitExpression(random.ToExpression, scope, script, errors);
                break;
            case GesSeededRandomExpressionNode seededRandom:
                VisitExpression(seededRandom.SeedExpression, scope, script, errors);
                VisitExpression(seededRandom.BodyExpression, new Scope(scope), script, errors);
                break;
            case GesGeneratedCollectionExpressionNode generated:
                VisitIterationSource(generated.Source, scope, script, errors);
                var generatedScope = CreateBinderScope(scope, generated.Identifier, generated, script, errors);
                VisitOptional(generated.Predicate, generatedScope, script, errors);
                VisitExpression(generated.Projection, generatedScope, script, errors);
                break;
            case GesGuardedChoiceExpressionNode choice:
                for (var index = 0; index < choice.Branches.Count; index++)
                {
                    VisitExpression(choice.Branches[index].ValueExpression, scope, script, errors);
                    VisitExpression(choice.Branches[index].ConditionExpression, scope, script, errors);
                }
                VisitExpression(choice.OtherwiseExpression, scope, script, errors);
                break;
            case GesBinaryExpressionNode binary:
                VisitExpression(binary.Left, scope, script, errors);
                VisitExpression(binary.Right, scope, script, errors);
                break;
            case GesPredicateCallExpressionNode predicate:
                if (predicate.Value is not null) VisitExpression(predicate.Value, scope, script, errors);
                break;
            case GesExtensionPredicateExpressionNode predicate: VisitExpression(predicate.Value, scope, script, errors); break;
            case GesTypeCheckExpressionNode check: VisitExpression(check.Value, scope, script, errors); break;
            case GesNothingCheckExpressionNode check: VisitExpression(check.Value, scope, script, errors); break;
            case GesTypeCastExpressionNode cast: VisitExpression(cast.Value, scope, script, errors); break;
            case GesMemberAccessExpressionNode member: VisitExpression(member.Target, scope, script, errors); break;
            case GesCollectionAccessExpressionNode access:
                VisitExpression(access.Target, scope, script, errors);
                VisitSelector(access.Selector, scope, script, errors);
                break;
        }
    }

    private static void VisitSelector(GesCollectionSelectorNode selector, Scope scope, GesParsedScript script, GesValidationErrors errors)
    {
        switch (selector)
        {
            case GesExpressionSelectorNode expression: VisitExpression(expression.Expression, scope, script, errors); break;
            case GesSeriesTermSelectorNode term: VisitExpression(term.IndexExpression, scope, script, errors); break;
            case GesPredicateSelectorNode predicate: VisitBoundExpression(predicate.Identifier, predicate.Predicate, predicate, scope, script, errors); break;
            case GesCountSelectorNode { Predicate: GesBooleanLiteralExpressionNode { Value: true } }: break;
            case GesCountSelectorNode count: VisitBoundExpression(count.Identifier, count.Predicate, count, scope, script, errors); break;
            case GesChooseSelectorNode choose:
                if (choose.Identifier is not null && choose.Predicate is not null) VisitBoundExpression(choose.Identifier, choose.Predicate, choose, scope, script, errors);
                if (choose.WeightIdentifier is not null && choose.WeightExpression is not null) VisitBoundExpression(choose.WeightIdentifier, choose.WeightExpression, choose with { BindingNames = choose.WeightBindingNames }, scope, script, errors);
                break;
            case GesEdgeSelectorNode edge when edge.Identifier is not null && edge.Predicate is not null: VisitBoundExpression(edge.Identifier, edge.Predicate, edge, scope, script, errors); break;
            case GesForeachSelectorNode each: VisitBoundExpression(each.Identifier, each.Expression, each, scope, script, errors); break;
            case GesFilterSelectorNode filter: VisitBoundExpression(filter.Identifier, filter.Predicate, filter, scope, script, errors); break;
            case GesSumSelectorNode sum: VisitBoundExpression(sum.Identifier, sum.Projection, sum, scope, script, errors); break;
            case GesAverageSelectorNode average: VisitBoundExpression(average.Identifier, average.Projection, average, scope, script, errors); break;
            case GesFoldSelectorNode fold:
                VisitOptional(fold.Seed, scope, script, errors);
                var foldScope = CreateBinderScope(scope, fold.Accumulator, fold, script, errors);
                if (fold.Accumulator == fold.Identifier)
                    errors.Add(script, "Fold bindings must be distinct", fold.Identifier, GesDiagnosticSymbolKind.Variable, GesDiagnosticCodes.ValidateDuplicateVariable, fold);
                else foldScope = CreateBinderScope(foldScope, fold.Identifier, fold, script, errors);
                VisitExpression(fold.Projection, foldScope, script, errors);
                break;
            case GesSelectSelectorNode select: VisitBoundExpression(select.Identifier, select.Projection, select, scope, script, errors); break;
            case GesMapSelectorNode map:
                var mapScope = CreateBinderScope(scope, map.Identifier, map, script, errors);
                VisitExpression(map.KeyProjection, mapScope, script, errors);
                VisitOptional(map.ValueProjection, mapScope, script, errors);
                break;
            case GesMinSelectorNode min: VisitBoundExpression(min.Identifier, min.Projection, min, scope, script, errors); break;
            case GesMaxSelectorNode max: VisitBoundExpression(max.Identifier, max.Projection, max, scope, script, errors); break;
            case GesContainsSelectorNode contains: VisitExpression(contains.ValueExpression, scope, script, errors); break;
            case GesDistinctSelectorNode distinct when distinct.Identifier is not null && distinct.Projection is not null: VisitBoundExpression(distinct.Identifier, distinct.Projection, distinct, scope, script, errors); break;
            case GesGroupBySelectorNode group: VisitBoundExpression(group.Identifier, group.Projection, group, scope, script, errors); break;
            case GesOrderBySelectorNode order: VisitBoundExpression(order.Identifier, order.Projection, order, scope, script, errors); break;
            case GesPatternSelectorNode pattern: VisitDicePattern(pattern.Pattern, scope, script, errors); break;
            case GesTakePatternSelectorNode pattern: VisitDicePattern(pattern.Pattern, scope, script, errors); break;
            case GesObjectMatchSelectorNode objectMatch: VisitObjectPattern(objectMatch.Pattern, scope, script, errors); break;
        }
    }

    private static void VisitBoundExpression(string identifier, GesExpressionNode expression, GesScriptNode node, Scope parent, GesParsedScript script, GesValidationErrors errors)
        => VisitExpression(expression, CreateBinderScope(parent, identifier, node, script, errors), script, errors);

    private static Scope CreateBinderScope(Scope parent, string identifier, GesScriptNode node, GesParsedScript script, GesValidationErrors errors)
    {
        var child = new Scope(parent);
        var names = node switch
        {
            GesCollectionSelectorNode selector => selector.BindingNames,
            GesForStatementNode loop => loop.BindingNames,
            _ => Array.Empty<string>()
        };
        if (names.Count == 0 || names[0] != identifier)
        {
            if (parent.ContainsVisible(identifier)) AddShadowError(script, errors, identifier, node);
            child.Declare(identifier);
            return child;
        }
        for (var index = 0; index < names.Count; index++)
        {
            var name = names[index];
            if (name != identifier && !GameEventScript.Syntax.GesText.IsVariableName(name))
                errors.Add(script, "Invalid component binding name", name, GesDiagnosticSymbolKind.Variable, GesDiagnosticCodes.ValidateInvalidIdentifierCase, node);
            if (parent.ContainsVisible(name)) AddShadowError(script, errors, name, node);
            if (child.ContainsVisible(name) && !parent.ContainsVisible(name))
                errors.Add(script, "Bindings must be distinct", name, GesDiagnosticSymbolKind.Variable, GesDiagnosticCodes.ValidateDuplicateVariable, node);
            child.Declare(name);
        }
        return child;
    }

    private static void VisitIterationSource(GesIterationSourceNode source, Scope scope, GesParsedScript script, GesValidationErrors errors)
    {
        if (source is GesCollectionIterationSourceNode collection) VisitExpression(collection.Expression, scope, script, errors);
        else if (source is GesRangeIterationSourceNode range) VisitExpression(range.RangeExpression, scope, script, errors);
    }

    private static void VisitDicePattern(GesDicePatternNode pattern, Scope scope, GesParsedScript script, GesValidationErrors errors)
    {
        if (pattern is GesDiceCountPatternNode { Face: not null } count) VisitExpression(count.Face, scope, script, errors);
    }

    private static void VisitObjectPattern(GesObjectMatchPatternNode pattern, Scope scope, GesParsedScript script, GesValidationErrors errors)
    {
        for (var index = 0; index < pattern.Entries.Count; index++)
        {
            var value = pattern.Entries[index].Value;
            if (value is GesObjectMatchExpressionValueNode expression) VisitExpression(expression.Expression, scope, script, errors);
            else if (value is GesObjectMatchNestedValueNode nested) VisitObjectPattern(nested.Pattern, scope, script, errors);
        }
    }

    private static void VisitArguments(IReadOnlyList<GesArgumentNode> arguments, Scope scope, GesParsedScript script, GesValidationErrors errors)
    {
        for (var index = 0; index < arguments.Count; index++) VisitExpression(arguments[index].Expression, scope, script, errors);
    }

    private static void VisitExpressions(IReadOnlyList<GesExpressionNode> expressions, Scope scope, GesParsedScript script, GesValidationErrors errors)
    {
        for (var index = 0; index < expressions.Count; index++) VisitExpression(expressions[index], scope, script, errors);
    }

    private static void VisitOptional(GesExpressionNode? expression, Scope scope, GesParsedScript script, GesValidationErrors errors)
    {
        if (expression is not null) VisitExpression(expression, scope, script, errors);
    }

    private static void AddShadowError(GesParsedScript script, GesValidationErrors errors, string name, GesScriptNode node)
        => errors.Add(script, $"Binding '{name}' shadows a binding from an enclosing scope", name, GesDiagnosticSymbolKind.Variable, GesDiagnosticCodes.ValidateShadowedVariable, node);
}
