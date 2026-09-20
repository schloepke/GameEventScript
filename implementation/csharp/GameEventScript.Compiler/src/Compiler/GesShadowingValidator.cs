// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Api;

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

    public static void Validate(ParsedScript script, GesValidationErrors errors)
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

    private static Scope CreateParameterScope(IReadOnlyList<ParameterNode> parameters)
    {
        var scope = new Scope();
        for (var index = 0; index < parameters.Count; index++) scope.Declare(parameters[index].LocalName);
        return scope;
    }

    private static void VisitStatements(IReadOnlyList<StatementNode> statements, Scope scope, ParsedScript script, GesValidationErrors errors)
    {
        for (var index = 0; index < statements.Count; index++)
        {
            switch (statements[index])
            {
                case PublishStatementNode publish:
                    VisitExpression(publish.MessageExpression, scope, script, errors);
                    VisitExpressions(publish.TagExpressions, scope, script, errors);
                    break;
                case LetStatementNode let:
                    VisitExpression(let.Expression, scope, script, errors);
                    if (scope.ContainsInAncestor(let.Identifier)) AddShadowError(script, errors, let.Identifier, let);
                    scope.Declare(let.Identifier);
                    break;
                case IfStatementNode ifStatement:
                    VisitExpression(ifStatement.Condition, scope, script, errors);
                    VisitBody(ifStatement.ThenBody, scope, script, errors);
                    if (ifStatement.ElseBody is not null) VisitBody(ifStatement.ElseBody, scope, script, errors);
                    break;
                case ForStatementNode forStatement:
                    VisitIterationSource(forStatement.Source, scope, script, errors);
                    var loopScope = CreateBinderScope(scope, forStatement.Identifier, forStatement, script, errors);
                    VisitBody(forStatement.Body, loopScope, script, errors);
                    break;
                case SeededRandomStatementNode seededRandom:
                    VisitExpression(seededRandom.SeedExpression, scope, script, errors);
                    VisitStatements(seededRandom.Body.Statements, new Scope(scope), script, errors);
                    break;
                case ExpressionStatementNode expressionStatement:
                    VisitExpression(expressionStatement.Expression, scope, script, errors);
                    break;
            }
        }
    }

    private static void VisitBody(StatementBodyNode body, Scope parent, ParsedScript script, GesValidationErrors errors)
        => VisitStatements(body.Statements, body.IsBlock ? new Scope(parent) : parent, script, errors);

    private static void VisitExpression(ExpressionNode expression, Scope scope, ParsedScript script, GesValidationErrors errors)
    {
        switch (expression)
        {
            case MessageLiteralExpressionNode message:
                VisitArguments(message.ArgumentList.Arguments, scope, script, errors);
                break;
            case CallExpressionNode call:
                VisitArguments(call.ArgumentList.Arguments, scope, script, errors);
                break;
            case ExtensionCallExpressionNode extension:
                VisitArguments(extension.ArgumentList.Arguments, scope, script, errors);
                break;
            case TypeConstructorExpressionNode constructor:
                VisitArguments(constructor.ArgumentList.Arguments, scope, script, errors);
                break;
            case ListLiteralExpressionNode list:
                VisitExpressions(list.Items, scope, script, errors);
                break;
            case MapLiteralExpressionNode map:
                for (var index = 0; index < map.Entries.Count; index++) VisitExpression(map.Entries[index].Value, scope, script, errors);
                break;
            case UnaryExpressionNode unary:
                VisitExpression(unary.Operand, scope, script, errors);
                break;
            case IntrinsicCallExpressionNode intrinsic:
                VisitExpressions(intrinsic.Arguments, scope, script, errors);
                break;
            case VariadicTaggedExpressionNode variadic:
                VisitExpressions(variadic.Arguments, scope, script, errors);
                break;
            case ClampExpressionNode clamp:
                VisitExpression(clamp.Value, scope, script, errors);
                VisitExpression(clamp.Minimum, scope, script, errors);
                VisitExpression(clamp.Maximum, scope, script, errors);
                break;
            case RangeExpressionNode range:
                VisitExpression(range.FromExpression, scope, script, errors);
                VisitExpression(range.ToExpression, scope, script, errors);
                VisitOptional(range.StepExpression, scope, script, errors);
                break;
            case RandomExpressionNode random:
                VisitExpression(random.FromExpression, scope, script, errors);
                VisitExpression(random.ToExpression, scope, script, errors);
                break;
            case SeededRandomExpressionNode seededRandom:
                VisitExpression(seededRandom.SeedExpression, scope, script, errors);
                VisitExpression(seededRandom.BodyExpression, new Scope(scope), script, errors);
                break;
            case GeneratedCollectionExpressionNode generated:
                VisitIterationSource(generated.Source, scope, script, errors);
                var generatedScope = CreateBinderScope(scope, generated.Identifier, generated, script, errors);
                VisitOptional(generated.Predicate, generatedScope, script, errors);
                VisitExpression(generated.Projection, generatedScope, script, errors);
                break;
            case GuardedChoiceExpressionNode choice:
                for (var index = 0; index < choice.Branches.Count; index++)
                {
                    VisitExpression(choice.Branches[index].ValueExpression, scope, script, errors);
                    VisitExpression(choice.Branches[index].ConditionExpression, scope, script, errors);
                }
                VisitExpression(choice.OtherwiseExpression, scope, script, errors);
                break;
            case BinaryExpressionNode binary:
                VisitExpression(binary.Left, scope, script, errors);
                VisitExpression(binary.Right, scope, script, errors);
                break;
            case PredicateCallExpressionNode predicate:
                VisitExpression(predicate.Value, scope, script, errors);
                break;
            case ExtensionPredicateExpressionNode predicate:
                VisitExpression(predicate.Value, scope, script, errors);
                break;
            case TypeCheckExpressionNode check:
                VisitExpression(check.Value, scope, script, errors);
                break;
            case NothingCheckExpressionNode check:
                VisitExpression(check.Value, scope, script, errors);
                break;
            case TypeCastExpressionNode cast:
                VisitExpression(cast.Value, scope, script, errors);
                break;
            case MemberAccessExpressionNode member:
                VisitExpression(member.Target, scope, script, errors);
                break;
            case CollectionAccessExpressionNode access:
                VisitExpression(access.Target, scope, script, errors);
                VisitSelector(access.Selector, scope, script, errors);
                break;
        }
    }

    private static void VisitSelector(CollectionSelectorNode selector, Scope scope, ParsedScript script, GesValidationErrors errors)
    {
        switch (selector)
        {
            case ExpressionSelectorNode expression: VisitExpression(expression.Expression, scope, script, errors); break;
            case SeriesTermSelectorNode term: VisitExpression(term.IndexExpression, scope, script, errors); break;
            case PredicateSelectorNode predicate: VisitBoundExpression(predicate.Identifier, predicate.Predicate, predicate, scope, script, errors); break;
            case CountSelectorNode { Predicate: BooleanLiteralExpressionNode { Value: true } }:
                break;
            case CountSelectorNode count: VisitBoundExpression(count.Identifier, count.Predicate, count, scope, script, errors); break;
            case ChooseSelectorNode choose:
                if (choose.Identifier is not null && choose.Predicate is not null) VisitBoundExpression(choose.Identifier, choose.Predicate, choose, scope, script, errors);
                if (choose.WeightIdentifier is not null && choose.WeightExpression is not null) VisitBoundExpression(choose.WeightIdentifier, choose.WeightExpression, choose, scope, script, errors);
                break;
            case EdgeSelectorNode edge when edge.Identifier is not null && edge.Predicate is not null: VisitBoundExpression(edge.Identifier, edge.Predicate, edge, scope, script, errors); break;
            case FilterSelectorNode filter: VisitBoundExpression(filter.Identifier, filter.Predicate, filter, scope, script, errors); break;
            case SumSelectorNode sum: VisitBoundExpression(sum.Identifier, sum.Projection, sum, scope, script, errors); break;
            case AverageSelectorNode average: VisitBoundExpression(average.Identifier, average.Projection, average, scope, script, errors); break;
            case SelectSelectorNode select: VisitBoundExpression(select.Identifier, select.Projection, select, scope, script, errors); break;
            case MapSelectorNode map:
                var mapScope = CreateBinderScope(scope, map.Identifier, map, script, errors);
                VisitExpression(map.KeyProjection, mapScope, script, errors);
                VisitOptional(map.ValueProjection, mapScope, script, errors);
                break;
            case MinSelectorNode min: VisitBoundExpression(min.Identifier, min.Projection, min, scope, script, errors); break;
            case MaxSelectorNode max: VisitBoundExpression(max.Identifier, max.Projection, max, scope, script, errors); break;
            case ContainsSelectorNode contains: VisitExpression(contains.ValueExpression, scope, script, errors); break;
            case DistinctSelectorNode distinct when distinct.Identifier is not null && distinct.Projection is not null: VisitBoundExpression(distinct.Identifier, distinct.Projection, distinct, scope, script, errors); break;
            case GroupBySelectorNode group: VisitBoundExpression(group.Identifier, group.Projection, group, scope, script, errors); break;
            case OrderBySelectorNode order: VisitBoundExpression(order.Identifier, order.Projection, order, scope, script, errors); break;
            case PatternSelectorNode pattern: VisitDicePattern(pattern.Pattern, scope, script, errors); break;
            case TakePatternSelectorNode pattern: VisitDicePattern(pattern.Pattern, scope, script, errors); break;
            case ObjectMatchSelectorNode objectMatch: VisitObjectPattern(objectMatch.Pattern, scope, script, errors); break;
        }
    }

    private static void VisitBoundExpression(string identifier, ExpressionNode expression, ScriptNode node, Scope parent, ParsedScript script, GesValidationErrors errors)
        => VisitExpression(expression, CreateBinderScope(parent, identifier, node, script, errors), script, errors);

    private static Scope CreateBinderScope(Scope parent, string identifier, ScriptNode node, ParsedScript script, GesValidationErrors errors)
    {
        if (parent.ContainsVisible(identifier)) AddShadowError(script, errors, identifier, node);
        var child = new Scope(parent);
        child.Declare(identifier);
        return child;
    }

    private static void VisitIterationSource(IterationSourceNode source, Scope scope, ParsedScript script, GesValidationErrors errors)
    {
        if (source is CollectionIterationSourceNode collection) VisitExpression(collection.Expression, scope, script, errors);
        else if (source is RangeIterationSourceNode range) VisitExpression(range.RangeExpression, scope, script, errors);
    }

    private static void VisitDicePattern(DicePatternNode pattern, Scope scope, ParsedScript script, GesValidationErrors errors)
    {
        if (pattern is DiceCountPatternNode { Face: not null } count) VisitExpression(count.Face, scope, script, errors);
    }

    private static void VisitObjectPattern(ObjectMatchPatternNode pattern, Scope scope, ParsedScript script, GesValidationErrors errors)
    {
        for (var index = 0; index < pattern.Entries.Count; index++)
        {
            var value = pattern.Entries[index].Value;
            if (value is ObjectMatchExpressionValueNode expression) VisitExpression(expression.Expression, scope, script, errors);
            else if (value is ObjectMatchNestedValueNode nested) VisitObjectPattern(nested.Pattern, scope, script, errors);
        }
    }

    private static void VisitArguments(IReadOnlyList<ArgumentNode> arguments, Scope scope, ParsedScript script, GesValidationErrors errors)
    {
        for (var index = 0; index < arguments.Count; index++) VisitExpression(arguments[index].Expression, scope, script, errors);
    }

    private static void VisitExpressions(IReadOnlyList<ExpressionNode> expressions, Scope scope, ParsedScript script, GesValidationErrors errors)
    {
        for (var index = 0; index < expressions.Count; index++) VisitExpression(expressions[index], scope, script, errors);
    }

    private static void VisitOptional(ExpressionNode? expression, Scope scope, ParsedScript script, GesValidationErrors errors)
    {
        if (expression is not null) VisitExpression(expression, scope, script, errors);
    }

    private static void AddShadowError(ParsedScript script, GesValidationErrors errors, string name, ScriptNode node)
        => errors.Add(script, $"Binding '{name}' shadows a binding from an enclosing scope", name, GameEventScriptSymbolKind.Variable, GameEventScriptDiagnosticCodes.ValidateShadowedVariable, node);
}
