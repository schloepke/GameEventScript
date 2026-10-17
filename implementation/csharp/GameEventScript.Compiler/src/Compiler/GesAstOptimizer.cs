// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using GameEventScript.Api;
using GameEventScript.Runtime;
using GameEventScript.Runtime.Values;
using GameEventScript.Runtime.Vm;

namespace GameEventScript.Compiler;

internal sealed class GesAstOptimizer(IReadOnlyDictionary<string, GesExpressionNode> constants)
{
    public static GesSyntaxTreeModule Optimize(GesSyntaxTreeModule module) => new GesAstOptimizer(module.Constants).OptimizeModule(module);

    private GesSyntaxTreeModule OptimizeModule(GesSyntaxTreeModule module)
    {
        var knownTypeNames = new HashSet<string>(module.TypeDefinitions.Keys, StringComparer.Ordinal);
        for (var externalTypeIndex = 0; externalTypeIndex < module.ExternalTypeDefinitions.Types.Count; externalTypeIndex++) knownTypeNames.Add(module.ExternalTypeDefinitions.Types[externalTypeIndex].Name);

        var optimizedTypes = new Dictionary<string, GesTypeDefinitionNode>(module.TypeDefinitions.Count, StringComparer.Ordinal);
        foreach (var pair in module.TypeDefinitions) optimizedTypes.Add(pair.Key, OptimizeTypeDefinition(pair.Value, knownTypeNames));

        var optimizedCallables = new Dictionary<string, GesCallableDefinition>(module.Callables.Count, StringComparer.Ordinal);
        foreach (var pair in module.Callables) optimizedCallables.Add(pair.Key, OptimizeCallableDefinition(pair.Value, knownTypeNames));

        var optimizedHandlers = new Dictionary<string, IReadOnlyList<GesEventHandlerNode>>(module.Handlers.Count, StringComparer.Ordinal);
        foreach (var pair in module.Handlers)
        {
            var handlers = new GesEventHandlerNode[pair.Value.Count];
            for (var index = 0; index < handlers.Length; index++) handlers[index] = OptimizeHandler(pair.Value[index], knownTypeNames);

            optimizedHandlers.Add(pair.Key, handlers);
        }

        return new GesSyntaxTreeModule(module.ModuleName, module.Constants, optimizedTypes, optimizedCallables, optimizedHandlers, module.ExternalTypeDefinitions, module.Sources);
    }

    private GesTypeDefinitionNode OptimizeTypeDefinition(GesTypeDefinitionNode definition, ISet<string> knownTypeNames)
        => definition with
        {
            Fields = OptimizeTypeFields(definition.Fields, knownTypeNames)
        };

    private GesCallableDefinition OptimizeCallableDefinition(GesCallableDefinition definition, ISet<string> knownTypeNames)
    {
        var optimized = OptimizeExpression(definition.Expression, knownTypeNames);
        return new GesCallableDefinition(definition.Name, definition.ParameterList, optimized, definition.Kind, definition.SourceRange);
    }

    private GesEventHandlerNode OptimizeHandler(GesEventHandlerNode handler, ISet<string> knownTypeNames) => handler with { Statements = OptimizeStatements(handler.Statements, knownTypeNames) };

    private IReadOnlyList<GesIfConditionNode> OptimizeConditions(IReadOnlyList<GesIfConditionNode> conditions, ISet<string> knownTypeNames)
    {
        // Keep condition iteration outside OptimizeStatement so other statements need no captured closure.
        var optimized = new GesIfConditionNode[conditions.Count];
        for (var index = 0; index < optimized.Length; index++)
        {
            var condition = conditions[index];
            optimized[index] = condition with { Expression = OptimizeExpression(condition.Expression, knownTypeNames) };
        }

        return optimized;
    }

    private GesStatementNode OptimizeStatement(GesStatementNode statement, ISet<string> knownTypeNames)
        => statement switch
        {
            GesPublishStatementNode publish => publish with
            {
                MessageExpression = OptimizeExpression(publish.MessageExpression, knownTypeNames),
                TagExpressions = OptimizeExpressions(publish.TagExpressions, knownTypeNames)
            },
            GesLetStatementNode let => let with
            {
                Expression = OptimizeExpression(let.Expression, knownTypeNames)
            },
            GesIfStatementNode ifStatement => ifStatement with
            {
                Conditions = OptimizeConditions(ifStatement.Conditions, knownTypeNames),
                ThenBody = OptimizeBody(ifStatement.ThenBody, knownTypeNames),
                ElseBody = ifStatement.ElseBody is null ? null : OptimizeBody(ifStatement.ElseBody, knownTypeNames)
            },
            GesForStatementNode forStatement => forStatement with
            {
                Source = OptimizeIterationSource(forStatement.Source, knownTypeNames),
                Body = OptimizeBody(forStatement.Body, knownTypeNames)
            },
            GesSeededRandomStatementNode seededRandom => seededRandom with
            {
                SeedExpression = OptimizeExpression(seededRandom.SeedExpression, knownTypeNames),
                Body = OptimizeBody(seededRandom.Body, knownTypeNames)
            },
            GesExpressionStatementNode expressionStatement => expressionStatement with
            {
                Expression = OptimizeExpression(expressionStatement.Expression, knownTypeNames)
            },
            _ => statement
        };

    private GesStatementBodyNode OptimizeBody(GesStatementBodyNode body, ISet<string> knownTypeNames) => body with { Statements = OptimizeStatements(body.Statements, knownTypeNames) };

    private IReadOnlyList<GesTypeFieldDefinitionNode> OptimizeTypeFields(IReadOnlyList<GesTypeFieldDefinitionNode> fields, ISet<string> knownTypeNames)
    {
        if (fields.Count == 0) return [];

        var optimized = new GesTypeFieldDefinitionNode[fields.Count];
        for (var index = 0; index < optimized.Length; index++)
        {
            var field = fields[index];
            var minimum = field.MinimumExpression is null ? null : OptimizeExpression(field.MinimumExpression, knownTypeNames);
            var maximum = field.MaximumExpression is null ? null : OptimizeExpression(field.MaximumExpression, knownTypeNames);
            var computed = field.ComputedExpression is null ? null : OptimizeExpression(field.ComputedExpression, knownTypeNames);
            optimized[index] = ReferenceEquals(minimum, field.MinimumExpression) && ReferenceEquals(maximum, field.MaximumExpression) && ReferenceEquals(computed, field.ComputedExpression)
                ? field
                : field with { MinimumExpression = minimum, MaximumExpression = maximum, ComputedExpression = computed };
        }

        return optimized;
    }

    private IReadOnlyList<GesStatementNode> OptimizeStatements(IReadOnlyList<GesStatementNode> statements, ISet<string> knownTypeNames)
    {
        if (statements.Count == 0) return [];

        var optimized = new GesStatementNode[statements.Count];
        for (var index = 0; index < optimized.Length; index++) optimized[index] = OptimizeStatement(statements[index], knownTypeNames);

        return optimized;
    }

    private IReadOnlyList<GesExpressionNode> OptimizeExpressions(IReadOnlyList<GesExpressionNode> expressions, ISet<string> knownTypeNames)
    {
        if (expressions.Count == 0) return [];

        var optimized = new GesExpressionNode[expressions.Count];
        for (var index = 0; index < optimized.Length; index++) optimized[index] = OptimizeExpression(expressions[index], knownTypeNames);

        return optimized;
    }

    private IReadOnlyList<GesGuardedChoiceBranchNode> OptimizeGuardedChoiceBranches(IReadOnlyList<GesGuardedChoiceBranchNode> branches, ISet<string> knownTypeNames)
    {
        if (branches.Count == 0) return [];

        var optimized = new GesGuardedChoiceBranchNode[branches.Count];
        for (var index = 0; index < optimized.Length; index++)
        {
            var branch = branches[index];
            optimized[index] = branch with
            {
                ValueExpression = OptimizeExpression(branch.ValueExpression, knownTypeNames),
                ConditionExpression = OptimizeExpression(branch.ConditionExpression, knownTypeNames)
            };
        }

        return optimized;
    }

    private GesArgumentListNode OptimizeArgumentList(GesArgumentListNode argumentList, ISet<string> knownTypeNames) => new(OptimizeArguments(argumentList.Arguments, knownTypeNames));

    private IReadOnlyList<GesArgumentNode> OptimizeArguments(IReadOnlyList<GesArgumentNode> arguments, ISet<string> knownTypeNames)
    {
        if (arguments.Count == 0) return [];

        var optimized = new GesArgumentNode[arguments.Count];
        for (var index = 0; index < optimized.Length; index++)
        {
            var argument = arguments[index];
            optimized[index] = argument with
            {
                Expression = OptimizeExpression(argument.Expression, knownTypeNames)
            };
        }

        return optimized;
    }

    private IReadOnlyList<GesMapEntryNode> OptimizeMapEntries(IReadOnlyList<GesMapEntryNode> entries, ISet<string> knownTypeNames)
    {
        if (entries.Count == 0) return [];

        var optimized = new GesMapEntryNode[entries.Count];
        for (var index = 0; index < optimized.Length; index++)
        {
            var entry = entries[index];
            optimized[index] = entry with
            {
                Value = OptimizeExpression(entry.Value, knownTypeNames)
            };
        }

        return optimized;
    }

    private GesIterationSourceNode OptimizeIterationSource(GesIterationSourceNode source, ISet<string> knownTypeNames)
        => source switch
        {
            GesCollectionIterationSourceNode collectionSource => collectionSource with
            {
                Expression = OptimizeExpression(collectionSource.Expression, knownTypeNames)
            },
            GesRangeIterationSourceNode rangeSource => rangeSource with
            {
                RangeExpression = (GesRangeExpressionNode)OptimizeExpression(rangeSource.RangeExpression, knownTypeNames)
            },
            _ => source
        };

    private GesExpressionNode OptimizeExpression(GesExpressionNode expression, ISet<string> knownTypeNames)
    {
        var optimized = expression switch
        {
            GesConstantReferenceExpressionNode constant when constants.TryGetValue(constant.Name, out var value) => value with { SourceRange = constant.SourceRange },
            GesSendExpressionNode send => send with
            {
                Message = OptimizeExpression(send.Message, knownTypeNames),
                Tags = OptimizeExpressions(send.Tags, knownTypeNames),
                Delay = send.Delay is null ? null : OptimizeExpression(send.Delay, knownTypeNames)
            },
            GesUnaryExpressionNode unary => unary with
            {
                Operand = OptimizeExpression(unary.Operand, knownTypeNames)
            },
            GesBinaryExpressionNode binary => binary with
            {
                Left = OptimizeExpression(binary.Left, knownTypeNames),
                Right = OptimizeExpression(binary.Right, knownTypeNames)
            },
            GesVariadicTaggedExpressionNode variadic => variadic with
            {
                Arguments = OptimizeExpressions(variadic.Arguments, knownTypeNames)
            },
            GesIntrinsicCallExpressionNode intrinsic => intrinsic with
            {
                Arguments = OptimizeExpressions(intrinsic.Arguments, knownTypeNames)
            },
            GesBetweenExpressionNode between => between with
            {
                Value = OptimizeExpression(between.Value, knownTypeNames),
                Minimum = OptimizeExpression(between.Minimum, knownTypeNames),
                Maximum = OptimizeExpression(between.Maximum, knownTypeNames)
            },
            GesClampExpressionNode clamp => clamp with
            {
                Value = OptimizeExpression(clamp.Value, knownTypeNames),
                Minimum = OptimizeExpression(clamp.Minimum, knownTypeNames),
                Maximum = OptimizeExpression(clamp.Maximum, knownTypeNames)
            },
            GesRangeExpressionNode range => range with
            {
                FromExpression = OptimizeExpression(range.FromExpression, knownTypeNames),
                ToExpression = OptimizeExpression(range.ToExpression, knownTypeNames),
                StepExpression = range.StepExpression is null ? null : OptimizeExpression(range.StepExpression, knownTypeNames)
            },
            GesRandomExpressionNode random => random with
            {
                FromExpression = OptimizeExpression(random.FromExpression, knownTypeNames),
                ToExpression = OptimizeExpression(random.ToExpression, knownTypeNames)
            },
            GesSeededRandomExpressionNode seededRandom => seededRandom with
            {
                SeedExpression = OptimizeExpression(seededRandom.SeedExpression, knownTypeNames),
                BodyExpression = OptimizeExpression(seededRandom.BodyExpression, knownTypeNames)
            },
            GesGeneratedCollectionExpressionNode generatedCollection => generatedCollection with
            {
                Source = OptimizeIterationSource(generatedCollection.Source, knownTypeNames),
                Predicate = generatedCollection.Predicate is null ? null : OptimizeExpression(generatedCollection.Predicate, knownTypeNames),
                Projection = OptimizeExpression(generatedCollection.Projection, knownTypeNames)
            },
            GesGuardedChoiceExpressionNode guardedChoice => guardedChoice with
            {
                Branches = OptimizeGuardedChoiceBranches(guardedChoice.Branches, knownTypeNames),
                OtherwiseExpression = OptimizeExpression(guardedChoice.OtherwiseExpression, knownTypeNames)
            },
            GesPredicateCallExpressionNode predicateCall => predicateCall with
            {
                Value = predicateCall.Value is null ? null : OptimizeExpression(predicateCall.Value, knownTypeNames)
            },
            GesExtensionPredicateExpressionNode extensionPredicate => extensionPredicate with
            {
                Value = OptimizeExpression(extensionPredicate.Value, knownTypeNames)
            },
            GesTypeCheckExpressionNode typeCheck => typeCheck with
            {
                Value = OptimizeExpression(typeCheck.Value, knownTypeNames)
            },
            GesNothingCheckExpressionNode nothingCheck => nothingCheck with
            {
                Value = OptimizeExpression(nothingCheck.Value, knownTypeNames)
            },
            GesTypeCastExpressionNode typeCast => typeCast with
            {
                Value = OptimizeExpression(typeCast.Value, knownTypeNames)
            },
            GesTypeConstructorExpressionNode typeConstructor => typeConstructor with
            {
                ArgumentList = OptimizeArgumentList(typeConstructor.ArgumentList, knownTypeNames)
            },
            GesMemberAccessExpressionNode member => member with
            {
                Target = OptimizeExpression(member.Target, knownTypeNames)
            },
            GesCombinedCollectionExpressionNode combined => combined with { Sources = OptimizeExpressions(combined.Sources, knownTypeNames) },
            GesCollectionAccessExpressionNode access => access with
            {
                Target = OptimizeExpression(access.Target, knownTypeNames),
                Selector = OptimizeSelector(access.Selector, knownTypeNames)
            },
            GesListLiteralExpressionNode list => list with
            {
                Items = OptimizeExpressions(list.Items, knownTypeNames)
            },
            GesMapLiteralExpressionNode dictionary => dictionary with
            {
                Entries = OptimizeMapEntries(dictionary.Entries, knownTypeNames)
            },
            GesMessageLiteralExpressionNode message => message with
            {
                ArgumentList = OptimizeArgumentList(message.ArgumentList, knownTypeNames)
            },
            GesCallExpressionNode call => call with
            {
                ArgumentList = OptimizeArgumentList(call.ArgumentList, knownTypeNames)
            },
            GesExtensionCallExpressionNode extensionCall => extensionCall with
            {
                ArgumentList = OptimizeArgumentList(extensionCall.ArgumentList, knownTypeNames)
            },
            _ => expression
        };

        if (FoldConstantExpression(optimized) is { } constantFolded) return constantFolded with { SourceRange = optimized.SourceRange };

        return optimized;
    }

    private static GesExpressionNode? FoldConstantExpression(GesExpressionNode expression)
    {
        // Preserve decimal source spelling metadata used to select continuous random bounds.
        if (expression is GesFloatLiteralExpressionNode or GesUnitFloatLiteralExpressionNode or GesIntegerLiteralExpressionNode or GesUnitIntegerLiteralExpressionNode) return null;

        return EvaluateConstantValue(expression) is { } value
            ? ConvertValueToLiteral(value)
            : null;
    }

    private GesCollectionSelectorNode OptimizeSelector(GesCollectionSelectorNode selector, ISet<string> knownTypeNames)
        => selector switch
        {
            GesExpressionSelectorNode expressionSelector => expressionSelector with
            {
                Expression = OptimizeExpression(expressionSelector.Expression, knownTypeNames)
            },
            GesPredicateSelectorNode predicateSelector => predicateSelector with
            {
                Predicate = OptimizeExpression(predicateSelector.Predicate, knownTypeNames)
            },
            GesCountSelectorNode countSelector => countSelector with
            {
                Predicate = OptimizeExpression(countSelector.Predicate, knownTypeNames)
            },
            GesChooseSelectorNode chooseSelector => chooseSelector with
            {
                Predicate = chooseSelector.Predicate is null ? null : OptimizeExpression(chooseSelector.Predicate, knownTypeNames),
                WeightExpression = chooseSelector.WeightExpression is null ? null : OptimizeExpression(chooseSelector.WeightExpression, knownTypeNames)
            },
            GesEdgeSelectorNode edgeSelector => edgeSelector with
            {
                Predicate = edgeSelector.Predicate is null ? null : OptimizeExpression(edgeSelector.Predicate, knownTypeNames)
            },
            GesSeriesTermSelectorNode seriesTermSelector => seriesTermSelector with
            {
                IndexExpression = OptimizeExpression(seriesTermSelector.IndexExpression, knownTypeNames)
            },
            GesForeachSelectorNode each => each with { Expression = OptimizeExpression(each.Expression, knownTypeNames) },
            GesFilterSelectorNode filterSelector => filterSelector with
            {
                Predicate = OptimizeExpression(filterSelector.Predicate, knownTypeNames)
            },
            GesSumSelectorNode sumSelector => sumSelector with
            {
                Projection = OptimizeExpression(sumSelector.Projection, knownTypeNames)
            },
            GesAverageSelectorNode averageSelector => averageSelector with
            {
                Projection = OptimizeExpression(averageSelector.Projection, knownTypeNames)
            },
            GesFoldSelectorNode fold => fold with
            {
                Seed = fold.Seed is null ? null : OptimizeExpression(fold.Seed, knownTypeNames),
                Projection = OptimizeExpression(fold.Projection, knownTypeNames)
            },
            GesSelectSelectorNode selectSelector => selectSelector with
            {
                Projection = OptimizeExpression(selectSelector.Projection, knownTypeNames)
            },
            GesMapSelectorNode dictionarySelector => dictionarySelector with
            {
                KeyProjection = OptimizeExpression(dictionarySelector.KeyProjection, knownTypeNames),
                ValueProjection = dictionarySelector.ValueProjection is null ? null : OptimizeExpression(dictionarySelector.ValueProjection, knownTypeNames)
            },
            GesMinSelectorNode minSelector => minSelector with
            {
                Projection = OptimizeExpression(minSelector.Projection, knownTypeNames)
            },
            GesMaxSelectorNode maxSelector => maxSelector with
            {
                Projection = OptimizeExpression(maxSelector.Projection, knownTypeNames)
            },
            GesContainsSelectorNode containsSelector => containsSelector with
            {
                ValueExpression = OptimizeExpression(containsSelector.ValueExpression, knownTypeNames)
            },
            GesDistinctSelectorNode distinctSelector => distinctSelector with
            {
                Projection = distinctSelector.Projection is null ? null : OptimizeExpression(distinctSelector.Projection, knownTypeNames)
            },
            GesGroupBySelectorNode groupBySelector => groupBySelector with
            {
                Projection = OptimizeExpression(groupBySelector.Projection, knownTypeNames)
            },
            GesOrderBySelectorNode orderBySelector => orderBySelector with
            {
                Projection = OptimizeExpression(orderBySelector.Projection, knownTypeNames)
            },
            GesObjectMatchSelectorNode objectMatchSelector => objectMatchSelector with
            {
                Pattern = OptimizeObjectMatchPattern(objectMatchSelector.Pattern, knownTypeNames)
            },
            _ => selector
        };

    private GesObjectMatchPatternNode OptimizeObjectMatchPattern(GesObjectMatchPatternNode pattern, ISet<string> knownTypeNames)
        => pattern with
        {
            Entries = OptimizeObjectMatchEntries(pattern.Entries, knownTypeNames)
        };

    private IReadOnlyList<GesObjectMatchEntryNode> OptimizeObjectMatchEntries(IReadOnlyList<GesObjectMatchEntryNode> entries, ISet<string> knownTypeNames)
    {
        if (entries.Count == 0) return [];

        var optimized = new GesObjectMatchEntryNode[entries.Count];
        for (var index = 0; index < optimized.Length; index++)
        {
            var entry = entries[index];
            optimized[index] = entry with
            {
                Value = entry.Value switch
                {
                    GesObjectMatchExpressionValueNode expressionValue => expressionValue with
                    {
                        Expression = OptimizeExpression(expressionValue.Expression, knownTypeNames)
                    },
                    GesObjectMatchNestedValueNode nestedValue => nestedValue with
                    {
                        Pattern = OptimizeObjectMatchPattern(nestedValue.Pattern, knownTypeNames)
                    },
                    _ => entry.Value
                }
            };
        }

        return optimized;
    }

    private static GesValue? EvaluateConstantValue(GesExpressionNode expression)
    {
        switch (expression)
        {
            case GesBooleanLiteralExpressionNode booleanLiteral: return GesValue.GesBoolean(booleanLiteral.Value);
            case GesNothingLiteralExpressionNode: return GesValue.GesNothing();
            case GesIntegerLiteralExpressionNode integerLiteral: return GesValue.GesInteger(integerLiteral.Value);
            case GesUnitIntegerLiteralExpressionNode unitIntegerLiteral:
                return GesQuantityUnits.ParseTypeName(unitIntegerLiteral.UnitName) is { } integerUnit
                    ? GesValue.GesInteger(unitIntegerLiteral.Value, integerUnit)
                    : GesValue.GesNothing();
            case GesFloatLiteralExpressionNode floatLiteral: return GesValue.GesFloat(floatLiteral.Value);
            case GesUnitFloatLiteralExpressionNode unitFloatLiteral:
                return GesQuantityUnits.ParseTypeName(unitFloatLiteral.UnitName) is { } unit
                    ? GesValue.GesFloat(unitFloatLiteral.Value, unit)
                    : GesValue.GesNothing();
            case GesPercentageLiteralExpressionNode percentageLiteral: return GesValue.GesPercentage(percentageLiteral.RatioValue);
            case GesTextLiteralExpressionNode textLiteral: return GesValue.GesText(textLiteral.Value);
            case GesTagLiteralExpressionNode tagLiteral: return GesValue.GesTag(tagLiteral.Name);
            case GesListLiteralExpressionNode listLiteral:
            {
                var items = new GesValue[listLiteral.Items.Count];
                for (var index = 0; index < listLiteral.Items.Count; index++)
                {
                    if (EvaluateConstantValue(listLiteral.Items[index]) is not { } item) return null;

                    items[index] = item;
                }

                return GesValue.GesList(items);
            }
            case GesMapLiteralExpressionNode mapLiteral:
            {
                var keys = new string[mapLiteral.Entries.Count];
                var values = new GesValue[mapLiteral.Entries.Count];
                for (var index = 0; index < mapLiteral.Entries.Count; index++)
                {
                    var entry = mapLiteral.Entries[index];
                    if (EvaluateConstantValue(entry.Value) is not { } entryValue) return null;

                    keys[index] = entry.Key;
                    values[index] = entryValue;
                }

                return GesValue.GesMap(keys, values);
            }
            case GesTypeCastExpressionNode typeCastExpression: return EvaluateConstantTypeCastValue(typeCastExpression);
            case GesUnaryExpressionNode unaryExpression: return EvaluateConstantUnaryValue(unaryExpression);
            case GesBinaryExpressionNode binaryExpression: return EvaluateConstantBinaryValue(binaryExpression);
            default: return null;
        }
    }

    private static GesValue? EvaluateConstantTypeCastValue(GesTypeCastExpressionNode typeCast)
    {
        if (EvaluateConstantValue(typeCast.Value) is not { } source) return null;

        switch (typeCast.TypeName)
        {
            case "number":
            case "numeric": return GesNumber.Cast(in source);
            default: return null;
        }
    }

    private static GesValue? EvaluateConstantUnaryValue(GesUnaryExpressionNode unary)
    {
        if (EvaluateConstantValue(unary.Operand) is not { } operand) return null;

        switch (unary.Operator)
        {
            case GesUnaryOperator.Not:
                return operand.Kind is GesBytecodeTypeKind.Nothing or
                    GesBytecodeTypeKind.List or
                    GesBytecodeTypeKind.Map or
                    GesBytecodeTypeKind.Dice
                    ? GesValue.GesNothing()
                    : GesValue.GesBoolean(!operand.AsBoolean());
            case GesUnaryOperator.HasValue: return GesValue.GesBoolean(operand.HasValue);
            case GesUnaryOperator.Empty: return GesValue.GesBoolean(!operand.HasValue);
            case GesUnaryOperator.Negate
                when operand.Kind == GesBytecodeTypeKind.Integer &&
                     operand.IntegerValue != long.MinValue:
                return GesValue.GesInteger(-operand.IntegerValue, operand.Unit);
            case GesUnaryOperator.Abs when operand.Kind is GesBytecodeTypeKind.Integer:
                return operand.IntegerValue == long.MinValue
                    ? GesValue.GesFloat(-(double)operand.IntegerValue, operand.Unit)
                    : GesValue.GesInteger(Math.Abs(operand.IntegerValue), operand.Unit);
            case GesUnaryOperator.Abs when operand.Kind is GesBytecodeTypeKind.Float or GesBytecodeTypeKind.Percentage:
                return operand.Kind == GesBytecodeTypeKind.Percentage
                    ? GesValue.GesPercentage(Math.Abs(operand.AsNumber()))
                    : GesValue.GesFloat(Math.Abs(operand.AsNumber()), operand.Unit);
            case GesUnaryOperator.NaturalLog:
                return !operand.Unit.IsNumericUnit() && operand.Kind is GesBytecodeTypeKind.Integer or GesBytecodeTypeKind.Float or GesBytecodeTypeKind.Percentage or GesBytecodeTypeKind.Boolean
                    ? GesValue.GesFloat(Math.Log(operand.AsNumber()))
                    : GesValue.GesNothing();
            case GesUnaryOperator.Exp:
                return !operand.Unit.IsNumericUnit() && operand.Kind is GesBytecodeTypeKind.Integer or GesBytecodeTypeKind.Float or GesBytecodeTypeKind.Percentage or GesBytecodeTypeKind.Boolean
                    ? GesValue.GesFloat(Math.Exp(operand.AsNumber()))
                    : GesValue.GesNothing();
            case GesUnaryOperator.Floor:
            case GesUnaryOperator.Ceil:
            case GesUnaryOperator.Truncate:
            case GesUnaryOperator.RoundHalfEven:
            case GesUnaryOperator.RoundHalfUp:
            case GesUnaryOperator.RoundHalfDown:
                if (operand.Kind is not (GesBytecodeTypeKind.Integer or GesBytecodeTypeKind.Float or GesBytecodeTypeKind.Percentage or GesBytecodeTypeKind.Boolean)) return GesValue.GesNothing();

                var rounded = unary.Operator switch
                {
                    GesUnaryOperator.Floor => Math.Floor(operand.AsNumber()),
                    GesUnaryOperator.Ceil => Math.Ceiling(operand.AsNumber()),
                    GesUnaryOperator.Truncate => Math.Truncate(operand.AsNumber()),
                    GesUnaryOperator.RoundHalfEven => Math.Round(operand.AsNumber(), 0, MidpointRounding.ToEven),
                    GesUnaryOperator.RoundHalfUp => Math.Round(operand.AsNumber(), 0, MidpointRounding.AwayFromZero),
                    GesUnaryOperator.RoundHalfDown => RoundHalfTowardZero(operand.AsNumber()),
                    _ => 0d
                };
                return GesValue.GesInteger(ToIntegerSaturated(rounded));
            case GesUnaryOperator.DegreeToRadians:
                if ((operand.Unit.IsNumericUnit() && operand.Unit != GesQuantityUnit.UnitDegree) ||
                    operand.Kind is not (GesBytecodeTypeKind.Integer or GesBytecodeTypeKind.Float or GesBytecodeTypeKind.Percentage or GesBytecodeTypeKind.Boolean) ||
                    !double.IsFinite(operand.AsNumber()))
                {
                    return GesValue.GesNothing();
                }

                return GesValue.GesFloat(operand.AsNumber() / 180d * GesMathConstants.Pi);
            case GesUnaryOperator.DegreeFromRadians:
                if (operand.Unit.IsNumericUnit() ||
                    operand.Kind is not (GesBytecodeTypeKind.Integer or GesBytecodeTypeKind.Float or GesBytecodeTypeKind.Percentage or GesBytecodeTypeKind.Boolean) ||
                    !double.IsFinite(operand.AsNumber()))
                {
                    return GesValue.GesNothing();
                }

                return GesValue.GesFloat(operand.AsNumber() / GesMathConstants.Pi * 180d, GesQuantityUnit.UnitDegree);
            case GesUnaryOperator.WrapDegree:
                if ((operand.Unit.IsNumericUnit() && operand.Unit != GesQuantityUnit.UnitDegree) ||
                    operand.Kind is not (GesBytecodeTypeKind.Integer or GesBytecodeTypeKind.Float or GesBytecodeTypeKind.Percentage or GesBytecodeTypeKind.Boolean) ||
                    !double.IsFinite(operand.AsNumber()))
                {
                    return GesValue.GesNothing();
                }

                var wrapped = operand.AsNumber() % 360d;
                if (wrapped < 0d) wrapped += 360d;
                return GesValue.GesFloat(wrapped == 360d ? 0d : wrapped, GesQuantityUnit.UnitDegree);
            default: return null;
        }
    }

    private static GesValue? EvaluateConstantBinaryValue(GesBinaryExpressionNode binary)
    {
        if (EvaluateConstantValue(binary.Left) is not { } left ||
            EvaluateConstantValue(binary.Right) is not { } right)
        {
            return null;
        }

        switch (binary.Operator)
        {
            case GesBinaryOperator.Default: return left.HasValue ? left : right;
            case GesBinaryOperator.Or: return EvaluateConstantOr(left, right);
            case GesBinaryOperator.And: return EvaluateConstantAnd(left, right);
            case GesBinaryOperator.Xor:
                if (IsTruthIndeterminate(left) || IsTruthIndeterminate(right)) return GesValue.GesNothing();

                return GesValue.GesBoolean(left.AsBoolean() ^ right.AsBoolean());
            case GesBinaryOperator.Implies: return EvaluateConstantImplies(left, right);
            case GesBinaryOperator.Power: return GesNumber.Power(in left, in right);
        }

        if (left.IsNothing || right.IsNothing)
        {
            // List subtraction treats Nothing as an element to remove, not scalar propagation.
            if (binary.Operator == GesBinaryOperator.Subtract && left.Kind == GesBytecodeTypeKind.List) return null;
            return GesValue.GesNothing();
        }

        var sameUnit = left.Unit == right.Unit;
        var bothIntegers = left.Kind == GesBytecodeTypeKind.Integer &&
                           right.Kind == GesBytecodeTypeKind.Integer;
        if (bothIntegers && sameUnit)
        {
            switch (binary.Operator)
            {
                case GesBinaryOperator.IntegerDivide:
                    return GesNumber.FloorDivideExact(left.IntegerValue, right.IntegerValue) is { } quotient
                        ? GesValue.GesInteger(quotient, DivideResultUnit(left.Unit))
                        : GesValue.GesFloat(Math.Floor((double)left.IntegerValue / right.IntegerValue), DivideResultUnit(left.Unit));
                case GesBinaryOperator.Modulo: return right.IntegerValue == 0 ? GesValue.GesNothing() : GesValue.GesInteger(GesNumber.Modulo(left.IntegerValue, right.IntegerValue), left.Unit);
                case GesBinaryOperator.Remainder: return right.IntegerValue == 0 ? GesValue.GesNothing() : GesValue.GesInteger(GesNumber.Remainder(left.IntegerValue, right.IntegerValue), left.Unit);
            }
        }

        if (ReadFiniteNumber(left) is not { } leftNumber ||
            ReadFiniteNumber(right) is not { } rightNumber)
        {
            return null;
        }

        switch (binary.Operator)
        {
            case GesBinaryOperator.Equal:
                if (!bothIntegers || !sameUnit) goto default;
                return GesValue.GesBoolean(left.IntegerValue == right.IntegerValue);
            case GesBinaryOperator.NotEqual:
                if (!bothIntegers || !sameUnit) goto default;
                return GesValue.GesBoolean(left.IntegerValue != right.IntegerValue);
            case GesBinaryOperator.Less:
                if (!bothIntegers || !sameUnit) goto default;
                return GesValue.GesBoolean(left.IntegerValue < right.IntegerValue);
            case GesBinaryOperator.Greater:
                if (!bothIntegers || !sameUnit) goto default;
                return GesValue.GesBoolean(left.IntegerValue > right.IntegerValue);
            case GesBinaryOperator.LessOrEqual:
                if (!bothIntegers || !sameUnit) goto default;
                return GesValue.GesBoolean(left.IntegerValue <= right.IntegerValue);
            case GesBinaryOperator.GreaterOrEqual:
                if (!bothIntegers || !sameUnit) goto default;
                return GesValue.GesBoolean(left.IntegerValue >= right.IntegerValue);
            case GesBinaryOperator.Add: return FoldAdd(left, right, leftNumber, rightNumber);
            case GesBinaryOperator.Subtract: return FoldSubtract(left, right, leftNumber, rightNumber);
            case GesBinaryOperator.Multiply: return FoldMultiply(left, right, leftNumber, rightNumber);
            case GesBinaryOperator.Divide when rightNumber != 0d && sameUnit:
                if (left.Kind == GesBytecodeTypeKind.Percentage && right.Kind is GesBytecodeTypeKind.Integer or GesBytecodeTypeKind.Float) return GesValue.GesPercentage(leftNumber / rightNumber);

                return CreateNumber(leftNumber / rightNumber, DivideResultUnit(left.Unit), false);
            case GesBinaryOperator.IntegerDivide when rightNumber != 0d && sameUnit: return CreateNumber(Math.Floor(leftNumber / rightNumber), DivideResultUnit(left.Unit), true);
            case GesBinaryOperator.Modulo when rightNumber != 0d && sameUnit:
            {
                var remainder = leftNumber % rightNumber;
                if (remainder != 0d &&
                    (remainder < 0d && rightNumber > 0d || remainder > 0d && rightNumber < 0d))
                {
                    remainder += rightNumber;
                }

                return CreateNumber(remainder, left.Unit, bothIntegers);
            }
            case GesBinaryOperator.Remainder when rightNumber != 0d && sameUnit:
            {
                return CreateNumber(leftNumber % rightNumber, left.Unit, bothIntegers);
            }
            default: return null;
        }
    }

    private static GesValue EvaluateConstantOr(GesValue left, GesValue right)
    {
        if (!IsTruthIndeterminate(left) && left.AsBoolean() || !IsTruthIndeterminate(right) && right.AsBoolean()) return GesValue.GesBoolean(true);

        return IsTruthIndeterminate(left) || IsTruthIndeterminate(right)
            ? GesValue.GesNothing()
            : GesValue.GesBoolean(false);
    }

    private static GesValue EvaluateConstantAnd(GesValue left, GesValue right)
    {
        if (!IsTruthIndeterminate(left) && !left.AsBoolean() || !IsTruthIndeterminate(right) && !right.AsBoolean()) return GesValue.GesBoolean(false);

        return IsTruthIndeterminate(left) || IsTruthIndeterminate(right)
            ? GesValue.GesNothing()
            : GesValue.GesBoolean(true);
    }

    private static GesValue EvaluateConstantImplies(GesValue left, GesValue right)
    {
        if (!IsTruthIndeterminate(left) && !left.AsBoolean() || !IsTruthIndeterminate(right) && right.AsBoolean()) return GesValue.GesBoolean(true);

        return IsTruthIndeterminate(left) || IsTruthIndeterminate(right)
            ? GesValue.GesNothing()
            : GesValue.GesBoolean(false);
    }

    private static bool IsTruthIndeterminate(GesValue value)
        => value.Kind is GesBytecodeTypeKind.Nothing or
            GesBytecodeTypeKind.List or
            GesBytecodeTypeKind.Map or
            GesBytecodeTypeKind.Dice;

    private static GesValue? FoldAdd(GesValue left, GesValue right, double leftNumber, double rightNumber)
    {
        if (left.Kind == GesBytecodeTypeKind.Integer &&
            right.Kind == GesBytecodeTypeKind.Integer &&
            left.Unit == right.Unit)
        {
            return GesNumber.AddExact(left.IntegerValue, right.IntegerValue) is { } integerResult
                ? GesValue.GesInteger(integerResult, left.Unit)
                : GesValue.GesFloat((double)left.IntegerValue + right.IntegerValue, left.Unit);
        }

        if (left.Unit == right.Unit &&
            left.Kind != GesBytecodeTypeKind.Percentage &&
            right.Kind != GesBytecodeTypeKind.Percentage)
        {
            return CreateNumber(leftNumber + rightNumber, left.Unit, false);
        }

        if (left.Kind == GesBytecodeTypeKind.Percentage && right.Kind == GesBytecodeTypeKind.Percentage) return GesValue.GesPercentage(leftNumber + rightNumber);

        if (right.Kind == GesBytecodeTypeKind.Percentage && left.Unit.IsNumericUnit()) return CreateNumber(leftNumber + leftNumber * rightNumber, left.Unit, false);

        return null;
    }

    private static GesValue? FoldSubtract(GesValue left, GesValue right, double leftNumber, double rightNumber)
    {
        if (left.Kind == GesBytecodeTypeKind.Integer &&
            right.Kind == GesBytecodeTypeKind.Integer &&
            left.Unit == right.Unit)
        {
            return GesNumber.SubtractExact(left.IntegerValue, right.IntegerValue) is { } integerResult
                ? GesValue.GesInteger(integerResult, left.Unit)
                : GesValue.GesFloat((double)left.IntegerValue - right.IntegerValue, left.Unit);
        }

        if (left.Unit == right.Unit &&
            left.Kind != GesBytecodeTypeKind.Percentage &&
            right.Kind != GesBytecodeTypeKind.Percentage)
        {
            return CreateNumber(leftNumber - rightNumber, left.Unit, false);
        }

        if (left.Kind == GesBytecodeTypeKind.Percentage && right.Kind == GesBytecodeTypeKind.Percentage) return GesValue.GesPercentage(leftNumber - rightNumber);

        if (right.Kind == GesBytecodeTypeKind.Percentage && left.Unit.IsNumericUnit()) return CreateNumber(leftNumber - leftNumber * rightNumber, left.Unit, false);

        return null;
    }

    private static GesValue? FoldMultiply(GesValue left, GesValue right, double leftNumber, double rightNumber)
    {
        if (left.Kind == GesBytecodeTypeKind.Integer && right.Kind == GesBytecodeTypeKind.Integer)
        {
            // As the VM computes it: an exact product stays int64 with the product unit; only an overflow becomes binary64.
            if (ProductUnit(left.Unit, right.Unit) is not { } productUnit) return null;
            return GesNumber.MultiplyExact(left.IntegerValue, right.IntegerValue) is { } integerResult
                ? GesValue.GesInteger(integerResult, productUnit)
                : GesValue.GesFloat((double)left.IntegerValue * right.IntegerValue, productUnit);
        }

        if (left.Kind == GesBytecodeTypeKind.Percentage && right.Kind == GesBytecodeTypeKind.Percentage) return GesValue.GesPercentage(leftNumber * rightNumber);

        if (left.Kind == GesBytecodeTypeKind.Percentage) return CreateNumber(leftNumber * rightNumber, right.Unit, false);

        if (right.Kind == GesBytecodeTypeKind.Percentage) return CreateNumber(leftNumber * rightNumber, left.Unit, false);

        if (ProductUnit(left.Unit, right.Unit) is { } unit) return CreateNumber(leftNumber * rightNumber, unit, false);

        return null;
    }

    private static double? ReadFiniteNumber(GesValue value)
    {
        switch (value.Kind)
        {
            case GesBytecodeTypeKind.Integer:
            case GesBytecodeTypeKind.Float:
            case GesBytecodeTypeKind.Percentage: return double.IsFinite(value.AsNumber()) ? value.AsNumber() : null;
            case GesBytecodeTypeKind.Boolean: return value.AsBoolean() ? 1d : 0d;
            default: return null;
        }
    }

    private static GesValue CreateNumber(double number, GesQuantityUnit unit, bool preferInteger)
    {
        if (!double.IsFinite(number)) return GesValue.GesNothing();

        if (preferInteger &&
            GesNumber.CanRepresentAsInteger(number))
        {
            return GesValue.GesInteger((long)number, unit);
        }

        return GesValue.GesFloat(number, unit);
    }

    private static GesQuantityUnit? ProductUnit(GesQuantityUnit left, GesQuantityUnit right)
    {
        if (left.IsNumericUnit() && right.IsNumericUnit()) return null;

        return left is GesQuantityUnit.UnitNone ? right : left;
    }

    private static GesQuantityUnit DivideResultUnit(GesQuantityUnit unit) => unit.IsNumericUnit() ? GesQuantityUnit.UnitNone : unit;

    private static GesExpressionNode? ConvertValueToLiteral(GesValue value)
    {
        switch (value.Kind)
        {
            case GesBytecodeTypeKind.Nothing: return new GesNothingLiteralExpressionNode();
            case GesBytecodeTypeKind.Boolean: return new GesBooleanLiteralExpressionNode(value.AsBoolean());
            case GesBytecodeTypeKind.Integer:
                return value.Unit.IsNumericUnit()
                    ? new GesUnitIntegerLiteralExpressionNode(value.IntegerValue, value.Unit.ToTypeName())
                    : new GesIntegerLiteralExpressionNode(value.IntegerValue);
            case GesBytecodeTypeKind.Float:
                if (double.IsNaN(value.AsNumber()) || double.IsInfinity(value.AsNumber())) return null;

                return value.Unit.IsNumericUnit()
                    ? new GesUnitFloatLiteralExpressionNode(value.AsNumber(), value.Unit.ToTypeName())
                    : new GesFloatLiteralExpressionNode(value.AsNumber());
            case GesBytecodeTypeKind.Percentage: return new GesPercentageLiteralExpressionNode(value.AsNumber());
            case GesBytecodeTypeKind.Text: return new GesTextLiteralExpressionNode(value.TextValue);
            case GesBytecodeTypeKind.Tag: return new GesTagLiteralExpressionNode(value.TextValue);
            case GesBytecodeTypeKind.Vector:
                return new GesTypeConstructorExpressionNode(
                    "vector",
                    new GesArgumentListNode([
                        new GesArgumentNode(null, CreateFloatLiteral(value.X, value.Unit)),
                        new GesArgumentNode(null, CreateFloatLiteral(value.Y, value.Unit)),
                        new GesArgumentNode(null, CreateFloatLiteral(value.Z, value.Unit))
                    ]));
            case GesBytecodeTypeKind.Point:
                return new GesTypeConstructorExpressionNode(
                    "point",
                    new GesArgumentListNode([
                        new GesArgumentNode(null, CreateFloatLiteral(value.X, value.Unit)),
                        new GesArgumentNode(null, CreateFloatLiteral(value.Y, value.Unit)),
                        new GesArgumentNode(null, CreateFloatLiteral(value.Z, value.Unit))
                    ]));
            case GesBytecodeTypeKind.List:
            {
                var source = value.AsList();
                var items = new GesExpressionNode[source.Length];
                for (var index = 0; index < items.Length; index++)
                {
                    if (ConvertValueToLiteral(source[index]) is not { } itemLiteral) return null;

                    items[index] = itemLiteral;
                }

                return new GesListLiteralExpressionNode(items);
            }
            case GesBytecodeTypeKind.Map:
            {
                if (value.AsMap() is not { } map) return null;

                var entries = new GesMapEntryNode[map.Length];
                for (var index = 0; index < map.StorageLength; index++)
                {
                    var mapValue = map.ValueAt(index);
                    if (ConvertValueToLiteral(mapValue) is not { } itemLiteral) return null;

                    entries[index] = new GesMapEntryNode(map.KeyAt(index), itemLiteral);
                }

                return new GesMapLiteralExpressionNode(entries);
            }
            default: return null;
        }
    }

    private static GesExpressionNode CreateFloatLiteral(double value, GesQuantityUnit? unit)
        => unit is { } valueUnit && valueUnit.IsNumericUnit()
            ? new GesUnitFloatLiteralExpressionNode(value, valueUnit.ToTypeName())
            : new GesFloatLiteralExpressionNode(value);

    private static long ToIntegerSaturated(double number) => GesNumber.ToIntegerSaturated(number);

    private static double RoundHalfTowardZero(double value) => GesNumber.RoundHalfTowardZero(value);
}
