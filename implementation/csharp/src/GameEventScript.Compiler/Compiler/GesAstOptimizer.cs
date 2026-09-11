// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Api;
using GameEventScript.Runtime;
using GameEventScript.Runtime.Values;
using GameEventScript.Runtime.VM;

namespace GameEventScript.Compiler;

internal static class GesAstOptimizer
{
    public static GesSyntaxTreeModule Optimize(GesSyntaxTreeModule module)
    {
        var knownTypeNames = new HashSet<string>(module.TypeDefinitions.Keys, StringComparer.Ordinal);
        for (var externalTypeIndex = 0; externalTypeIndex < module.ExternalTypeDefinitions.Types.Count; externalTypeIndex++)
        {
            knownTypeNames.Add(module.ExternalTypeDefinitions.Types[externalTypeIndex].Name);
        }

        var optimizedTypes = new Dictionary<string, TypeDefinitionNode>(module.TypeDefinitions.Count, StringComparer.Ordinal);
        foreach (var pair in module.TypeDefinitions)
        {
            optimizedTypes.Add(pair.Key, OptimizeTypeDefinition(pair.Value, knownTypeNames));
        }

        var optimizedCallables = new Dictionary<string, GesCallableDefinition>(module.Callables.Count, StringComparer.Ordinal);
        foreach (var pair in module.Callables)
        {
            optimizedCallables.Add(pair.Key, OptimizeCallableDefinition(pair.Value, knownTypeNames));
        }

        var optimizedHandlers = new Dictionary<string, IReadOnlyList<EventHandlerNode>>(module.Handlers.Count, StringComparer.Ordinal);
        foreach (var pair in module.Handlers)
        {
            var handlers = new EventHandlerNode[pair.Value.Count];
            for (var index = 0; index < handlers.Length; index++)
            {
                handlers[index] = OptimizeHandler(pair.Value[index], knownTypeNames);
            }

            optimizedHandlers.Add(pair.Key, handlers);
        }

        return new GesSyntaxTreeModule(
            module.ModuleName,
            module.Constants,
            optimizedTypes,
            optimizedCallables,
            optimizedHandlers,
            module.ExternalTypeDefinitions,
            module.Sources);
    }

    private static TypeDefinitionNode OptimizeTypeDefinition(TypeDefinitionNode definition, ISet<string> knownTypeNames)
        => definition with
        {
            Fields = OptimizeTypeFields(definition.Fields, knownTypeNames)
        };

    private static GesCallableDefinition OptimizeCallableDefinition(GesCallableDefinition definition, ISet<string> knownTypeNames)
    {
        var optimized = OptimizeExpression(definition.Expression, knownTypeNames);
        return new GesCallableDefinition(definition.Name, definition.ParameterList, optimized, definition.Kind, definition.SourceRange);
    }

    private static EventHandlerNode OptimizeHandler(EventHandlerNode handler, ISet<string> knownTypeNames)
        => handler with { Statements = OptimizeStatements(handler.Statements, knownTypeNames) };

    private static StatementNode OptimizeStatement(StatementNode statement, ISet<string> knownTypeNames)
        => statement switch
        {
            PublishStatementNode publish => publish with
            {
                MessageExpression = OptimizeExpression(publish.MessageExpression, knownTypeNames),
                TagExpressions = OptimizeExpressions(publish.TagExpressions, knownTypeNames)
            },
            LetStatementNode let => let with
            {
                Expression = OptimizeExpression(let.Expression, knownTypeNames)
            },
            IfStatementNode ifStatement => ifStatement with
            {
                Condition = OptimizeExpression(ifStatement.Condition, knownTypeNames),
                ThenBody = OptimizeBody(ifStatement.ThenBody, knownTypeNames),
                ElseBody = ifStatement.ElseBody is null ? null : OptimizeBody(ifStatement.ElseBody, knownTypeNames)
            },
            ForStatementNode forStatement => forStatement with
            {
                Source = OptimizeIterationSource(forStatement.Source, knownTypeNames),
                Body = OptimizeBody(forStatement.Body, knownTypeNames)
            },
            SeededRandomStatementNode seededRandom => seededRandom with
            {
                SeedExpression = OptimizeExpression(seededRandom.SeedExpression, knownTypeNames),
                Body = OptimizeBody(seededRandom.Body, knownTypeNames)
            },
            ExpressionStatementNode expressionStatement => expressionStatement with
            {
                Expression = OptimizeExpression(expressionStatement.Expression, knownTypeNames)
            },
            _ => statement
        };

    private static StatementBodyNode OptimizeBody(StatementBodyNode body, ISet<string> knownTypeNames)
        => body with { Statements = OptimizeStatements(body.Statements, knownTypeNames) };

    private static IReadOnlyList<TypeFieldDefinitionNode> OptimizeTypeFields(IReadOnlyList<TypeFieldDefinitionNode> fields, ISet<string> knownTypeNames)
    {
        if (fields.Count == 0)
        {
            return [];
        }

        var optimized = new TypeFieldDefinitionNode[fields.Count];
        for (var index = 0; index < optimized.Length; index++)
        {
            var field = fields[index];
            optimized[index] = field with
            {
                MinimumExpression = field.MinimumExpression is null ? null : OptimizeExpression(field.MinimumExpression, knownTypeNames),
                MaximumExpression = field.MaximumExpression is null ? null : OptimizeExpression(field.MaximumExpression, knownTypeNames),
                ComputedExpression = field.ComputedExpression is null ? null : OptimizeExpression(field.ComputedExpression, knownTypeNames)
            };
        }

        return optimized;
    }

    private static IReadOnlyList<StatementNode> OptimizeStatements(IReadOnlyList<StatementNode> statements, ISet<string> knownTypeNames)
    {
        if (statements.Count == 0)
        {
            return [];
        }

        var optimized = new StatementNode[statements.Count];
        for (var index = 0; index < optimized.Length; index++)
        {
            optimized[index] = OptimizeStatement(statements[index], knownTypeNames);
        }

        return optimized;
    }

    private static IReadOnlyList<ExpressionNode> OptimizeExpressions(IReadOnlyList<ExpressionNode> expressions, ISet<string> knownTypeNames)
    {
        if (expressions.Count == 0)
        {
            return [];
        }

        var optimized = new ExpressionNode[expressions.Count];
        for (var index = 0; index < optimized.Length; index++)
        {
            optimized[index] = OptimizeExpression(expressions[index], knownTypeNames);
        }

        return optimized;
    }

    private static IReadOnlyList<GuardedChoiceBranchNode> OptimizeGuardedChoiceBranches(IReadOnlyList<GuardedChoiceBranchNode> branches, ISet<string> knownTypeNames)
    {
        if (branches.Count == 0)
        {
            return [];
        }

        var optimized = new GuardedChoiceBranchNode[branches.Count];
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

    private static ArgumentListNode OptimizeArgumentList(ArgumentListNode argumentList, ISet<string> knownTypeNames)
        => new(OptimizeArguments(argumentList.Arguments, knownTypeNames));

    private static IReadOnlyList<ArgumentNode> OptimizeArguments(IReadOnlyList<ArgumentNode> arguments, ISet<string> knownTypeNames)
    {
        if (arguments.Count == 0)
        {
            return [];
        }

        var optimized = new ArgumentNode[arguments.Count];
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

    private static IReadOnlyList<MapEntryNode> OptimizeMapEntries(IReadOnlyList<MapEntryNode> entries, ISet<string> knownTypeNames)
    {
        if (entries.Count == 0)
        {
            return [];
        }

        var optimized = new MapEntryNode[entries.Count];
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

    private static IterationSourceNode OptimizeIterationSource(IterationSourceNode source, ISet<string> knownTypeNames)
        => source switch
        {
            CollectionIterationSourceNode collectionSource => collectionSource with
            {
                Expression = OptimizeExpression(collectionSource.Expression, knownTypeNames)
            },
            RangeIterationSourceNode rangeSource => rangeSource with
            {
                RangeExpression = (RangeExpressionNode)OptimizeExpression(rangeSource.RangeExpression, knownTypeNames)
            },
            _ => source
        };

    private static ExpressionNode OptimizeExpression(ExpressionNode expression, ISet<string> knownTypeNames)
    {
        var optimized = expression switch
        {
            UnaryExpressionNode unary => unary with
            {
                Operand = OptimizeExpression(unary.Operand, knownTypeNames)
            },
            BinaryExpressionNode binary => binary with
            {
                Left = OptimizeExpression(binary.Left, knownTypeNames),
                Right = OptimizeExpression(binary.Right, knownTypeNames)
            },
            VariadicTaggedExpressionNode variadic => variadic with
            {
                Arguments = OptimizeExpressions(variadic.Arguments, knownTypeNames)
            },
            IntrinsicCallExpressionNode intrinsic => intrinsic with
            {
                Arguments = OptimizeExpressions(intrinsic.Arguments, knownTypeNames)
            },
            ClampExpressionNode clamp => clamp with
            {
                Value = OptimizeExpression(clamp.Value, knownTypeNames),
                Minimum = OptimizeExpression(clamp.Minimum, knownTypeNames),
                Maximum = OptimizeExpression(clamp.Maximum, knownTypeNames)
            },
            RangeExpressionNode range => range with
            {
                FromExpression = OptimizeExpression(range.FromExpression, knownTypeNames),
                ToExpression = OptimizeExpression(range.ToExpression, knownTypeNames),
                StepExpression = range.StepExpression is null ? null : OptimizeExpression(range.StepExpression, knownTypeNames)
            },
            RandomExpressionNode random => random with
            {
                FromExpression = OptimizeExpression(random.FromExpression, knownTypeNames),
                ToExpression = OptimizeExpression(random.ToExpression, knownTypeNames)
            },
            SeededRandomExpressionNode seededRandom => seededRandom with
            {
                SeedExpression = OptimizeExpression(seededRandom.SeedExpression, knownTypeNames),
                BodyExpression = OptimizeExpression(seededRandom.BodyExpression, knownTypeNames)
            },
            GeneratedCollectionExpressionNode generatedCollection => generatedCollection with
            {
                Source = OptimizeIterationSource(generatedCollection.Source, knownTypeNames),
                Predicate = generatedCollection.Predicate is null ? null : OptimizeExpression(generatedCollection.Predicate, knownTypeNames),
                Projection = OptimizeExpression(generatedCollection.Projection, knownTypeNames)
            },
            GuardedChoiceExpressionNode guardedChoice => guardedChoice with
            {
                Branches = OptimizeGuardedChoiceBranches(guardedChoice.Branches, knownTypeNames),
                OtherwiseExpression = OptimizeExpression(guardedChoice.OtherwiseExpression, knownTypeNames)
            },
            PredicateCallExpressionNode predicateCall => predicateCall with
            {
                Value = OptimizeExpression(predicateCall.Value, knownTypeNames)
            },
            ExtensionPredicateExpressionNode extensionPredicate => extensionPredicate with
            {
                Value = OptimizeExpression(extensionPredicate.Value, knownTypeNames)
            },
            TypeCheckExpressionNode typeCheck => typeCheck with
            {
                Value = OptimizeExpression(typeCheck.Value, knownTypeNames)
            },
            NothingCheckExpressionNode nothingCheck => nothingCheck with
            {
                Value = OptimizeExpression(nothingCheck.Value, knownTypeNames)
            },
            TypeCastExpressionNode typeCast => typeCast with
            {
                Value = OptimizeExpression(typeCast.Value, knownTypeNames)
            },
            TypeConstructorExpressionNode typeConstructor => typeConstructor with
            {
                ArgumentList = OptimizeArgumentList(typeConstructor.ArgumentList, knownTypeNames)
            },
            MemberAccessExpressionNode member => member with
            {
                Target = OptimizeExpression(member.Target, knownTypeNames)
            },
            CollectionAccessExpressionNode access => access with
            {
                Target = OptimizeExpression(access.Target, knownTypeNames),
                Selector = OptimizeSelector(access.Selector, knownTypeNames)
            },
            ListLiteralExpressionNode list => list with
            {
                Items = OptimizeExpressions(list.Items, knownTypeNames)
            },
            MapLiteralExpressionNode dictionary => dictionary with
            {
                Entries = OptimizeMapEntries(dictionary.Entries, knownTypeNames)
            },
            MessageLiteralExpressionNode message => message with
            {
                ArgumentList = OptimizeArgumentList(message.ArgumentList, knownTypeNames)
            },
            CallExpressionNode call => call with
            {
                ArgumentList = OptimizeArgumentList(call.ArgumentList, knownTypeNames)
            },
            ExtensionCallExpressionNode extensionCall => extensionCall with
            {
                ArgumentList = OptimizeArgumentList(extensionCall.ArgumentList, knownTypeNames)
            },
            _ => expression
        };

        if (FoldConstantExpression(optimized) is { } constantFolded)
        {
            return constantFolded with { SourceRange = optimized.SourceRange };
        }

        return optimized;
    }

    private static ExpressionNode? FoldConstantExpression(ExpressionNode expression)
    {
        // Preserve decimal source spelling metadata used to select continuous random bounds.
        if (expression is FloatLiteralExpressionNode or UnitFloatLiteralExpressionNode or IntegerLiteralExpressionNode or UnitIntegerLiteralExpressionNode)
        {
            return null;
        }

        return EvaluateConstantValue(expression) is { } value
            ? ConvertValueToLiteral(value)
            : null;
    }

    private static CollectionSelectorNode OptimizeSelector(CollectionSelectorNode selector, ISet<string> knownTypeNames)
        => selector switch
        {
            ExpressionSelectorNode expressionSelector => expressionSelector with
            {
                Expression = OptimizeExpression(expressionSelector.Expression, knownTypeNames)
            },
            PredicateSelectorNode predicateSelector => predicateSelector with
            {
                Predicate = OptimizeExpression(predicateSelector.Predicate, knownTypeNames)
            },
            CountSelectorNode countSelector => countSelector with
            {
                Predicate = OptimizeExpression(countSelector.Predicate, knownTypeNames)
            },
            ChooseSelectorNode chooseSelector => chooseSelector with
            {
                Predicate = chooseSelector.Predicate is null ? null : OptimizeExpression(chooseSelector.Predicate, knownTypeNames),
                WeightExpression = chooseSelector.WeightExpression is null ? null : OptimizeExpression(chooseSelector.WeightExpression, knownTypeNames)
            },
            EdgeSelectorNode edgeSelector => edgeSelector with
            {
                Predicate = edgeSelector.Predicate is null ? null : OptimizeExpression(edgeSelector.Predicate, knownTypeNames)
            },
            SeriesTermSelectorNode seriesTermSelector => seriesTermSelector with
            {
                IndexExpression = OptimizeExpression(seriesTermSelector.IndexExpression, knownTypeNames)
            },
            FilterSelectorNode filterSelector => filterSelector with
            {
                Predicate = OptimizeExpression(filterSelector.Predicate, knownTypeNames)
            },
            SumSelectorNode sumSelector => sumSelector with
            {
                Projection = OptimizeExpression(sumSelector.Projection, knownTypeNames)
            },
            AverageSelectorNode averageSelector => averageSelector with
            {
                Projection = OptimizeExpression(averageSelector.Projection, knownTypeNames)
            },
            SelectSelectorNode selectSelector => selectSelector with
            {
                Projection = OptimizeExpression(selectSelector.Projection, knownTypeNames)
            },
            MapSelectorNode dictionarySelector => dictionarySelector with
            {
                KeyProjection = OptimizeExpression(dictionarySelector.KeyProjection, knownTypeNames),
                ValueProjection = dictionarySelector.ValueProjection is null ? null : OptimizeExpression(dictionarySelector.ValueProjection, knownTypeNames)
            },
            MinSelectorNode minSelector => minSelector with
            {
                Projection = OptimizeExpression(minSelector.Projection, knownTypeNames)
            },
            MaxSelectorNode maxSelector => maxSelector with
            {
                Projection = OptimizeExpression(maxSelector.Projection, knownTypeNames)
            },
            ContainsSelectorNode containsSelector => containsSelector with
            {
                ValueExpression = OptimizeExpression(containsSelector.ValueExpression, knownTypeNames)
            },
            DistinctSelectorNode distinctSelector => distinctSelector with
            {
                Projection = distinctSelector.Projection is null ? null : OptimizeExpression(distinctSelector.Projection, knownTypeNames)
            },
            GroupBySelectorNode groupBySelector => groupBySelector with
            {
                Projection = OptimizeExpression(groupBySelector.Projection, knownTypeNames)
            },
            OrderBySelectorNode orderBySelector => orderBySelector with
            {
                Projection = OptimizeExpression(orderBySelector.Projection, knownTypeNames)
            },
            ObjectMatchSelectorNode objectMatchSelector => objectMatchSelector with
            {
                Pattern = OptimizeObjectMatchPattern(objectMatchSelector.Pattern, knownTypeNames)
            },
            _ => selector
        };

    private static ObjectMatchPatternNode OptimizeObjectMatchPattern(ObjectMatchPatternNode pattern, ISet<string> knownTypeNames)
        => pattern with
        {
            Entries = OptimizeObjectMatchEntries(pattern.Entries, knownTypeNames)
        };

    private static IReadOnlyList<ObjectMatchEntryNode> OptimizeObjectMatchEntries(IReadOnlyList<ObjectMatchEntryNode> entries, ISet<string> knownTypeNames)
    {
        if (entries.Count == 0)
        {
            return [];
        }

        var optimized = new ObjectMatchEntryNode[entries.Count];
        for (var index = 0; index < optimized.Length; index++)
        {
            var entry = entries[index];
            optimized[index] = entry with
            {
                Value = entry.Value switch
                {
                    ObjectMatchExpressionValueNode expressionValue => expressionValue with
                    {
                        Expression = OptimizeExpression(expressionValue.Expression, knownTypeNames)
                    },
                    ObjectMatchNestedValueNode nestedValue => nestedValue with
                    {
                        Pattern = OptimizeObjectMatchPattern(nestedValue.Pattern, knownTypeNames)
                    },
                    _ => entry.Value
                }
            };
        }

        return optimized;
    }

    private static GesValue? EvaluateConstantValue(ExpressionNode expression)
    {
        switch (expression)
        {
            case BooleanLiteralExpressionNode booleanLiteral:
                return GesValue.GesBoolean(booleanLiteral.Value);
            case NothingLiteralExpressionNode:
                return GesValue.GesNothing();
            case IntegerLiteralExpressionNode integerLiteral:
                return GesValue.GesInteger(integerLiteral.Value);
            case UnitIntegerLiteralExpressionNode unitIntegerLiteral:
                return GameEventScriptBytecodeInstructionUnits.ParseTypeName(unitIntegerLiteral.UnitName) is { } integerUnit
                    ? GesValue.GesInteger(unitIntegerLiteral.Value, integerUnit)
                    : GesValue.GesNothing();
            case FloatLiteralExpressionNode floatLiteral:
                return GesValue.GesFloat(floatLiteral.Value);
            case UnitFloatLiteralExpressionNode unitFloatLiteral:
                return GameEventScriptBytecodeInstructionUnits.ParseTypeName(unitFloatLiteral.UnitName) is { } unit
                    ? GesValue.GesFloat(unitFloatLiteral.Value, unit)
                    : GesValue.GesNothing();
            case PercentageLiteralExpressionNode percentageLiteral:
                return GesValue.GesPercentage(percentageLiteral.RatioValue);
            case TextLiteralExpressionNode textLiteral:
                return GesValue.GesText(textLiteral.Value);
            case TagLiteralExpressionNode tagLiteral:
                return GesValue.GesTag(tagLiteral.Name);
            case ListLiteralExpressionNode listLiteral:
            {
                var items = new GesValue[listLiteral.Items.Count];
                for (var index = 0; index < listLiteral.Items.Count; index++)
                {
                    if (EvaluateConstantValue(listLiteral.Items[index]) is not { } item)
                    {
                        return null;
                    }

                    items[index] = item;
                }

                return GesValue.GesList(items);
            }
            case MapLiteralExpressionNode mapLiteral:
            {
                var keys = new string[mapLiteral.Entries.Count];
                var values = new GesValue[mapLiteral.Entries.Count];
                for (var index = 0; index < mapLiteral.Entries.Count; index++)
                {
                    var entry = mapLiteral.Entries[index];
                    if (EvaluateConstantValue(entry.Value) is not { } entryValue)
                    {
                        return null;
                    }

                    keys[index] = entry.Key;
                    values[index] = entryValue;
                }

                return GesValue.GesMap(keys, values);
            }
            case TypeCastExpressionNode typeCastExpression:
                return EvaluateConstantTypeCastValue(typeCastExpression);
            case UnaryExpressionNode unaryExpression:
                return EvaluateConstantUnaryValue(unaryExpression);
            case BinaryExpressionNode binaryExpression:
                return EvaluateConstantBinaryValue(binaryExpression);
            default:
                return null;
        }
    }

    private static GesValue? EvaluateConstantTypeCastValue(TypeCastExpressionNode typeCast)
    {
        if (EvaluateConstantValue(typeCast.Value) is not { } source)
        {
            return null;
        }

        switch (typeCast.TypeName)
        {
            case "number":
            case "numeric":
                return GameEventScriptNumber.Cast(in source);
            default:
                return null;
        }
    }

    private static GesValue? EvaluateConstantUnaryValue(UnaryExpressionNode unary)
    {
        if (EvaluateConstantValue(unary.Operand) is not { } operand)
        {
            return null;
        }

        switch (unary.Operator)
        {
            case GesUnaryOperator.Not:
                return operand.Kind is GameEventScriptBytecodeTypeKind.Nothing or
                    GameEventScriptBytecodeTypeKind.List or
                    GameEventScriptBytecodeTypeKind.Map or
                    GameEventScriptBytecodeTypeKind.Dice
                    ? GesValue.GesNothing()
                    : GesValue.GesBoolean(!operand.AsBoolean());
            case GesUnaryOperator.HasValue:
                return GesValue.GesBoolean(operand.HasValue);
            case GesUnaryOperator.Empty:
                return GesValue.GesBoolean(!operand.HasValue);
            case GesUnaryOperator.Negate
                when operand.Kind == GameEventScriptBytecodeTypeKind.Integer &&
                     operand.IntegerValue != long.MinValue:
                return GesValue.GesInteger(-operand.IntegerValue, operand.Unit);
            case GesUnaryOperator.Abs when operand.Kind is GameEventScriptBytecodeTypeKind.Integer:
                return operand.IntegerValue == long.MinValue
                    ? GesValue.GesFloat(-(double)operand.IntegerValue, operand.Unit)
                    : GesValue.GesInteger(Math.Abs(operand.IntegerValue), operand.Unit);
            case GesUnaryOperator.Abs when operand.Kind is GameEventScriptBytecodeTypeKind.Float or GameEventScriptBytecodeTypeKind.Percentage:
                return operand.Kind == GameEventScriptBytecodeTypeKind.Percentage
                    ? GesValue.GesPercentage(Math.Abs(operand.AsNumber()))
                    : GesValue.GesFloat(Math.Abs(operand.AsNumber()), operand.Unit);
            case GesUnaryOperator.NaturalLog:
                return !operand.Unit.IsNumericUnit() && operand.Kind is GameEventScriptBytecodeTypeKind.Integer or GameEventScriptBytecodeTypeKind.Float or GameEventScriptBytecodeTypeKind.Percentage or GameEventScriptBytecodeTypeKind.Boolean
                    ? GesValue.GesFloat(Math.Log(operand.AsNumber()))
                    : GesValue.GesNothing();
            case GesUnaryOperator.Exp:
                return !operand.Unit.IsNumericUnit() && operand.Kind is GameEventScriptBytecodeTypeKind.Integer or GameEventScriptBytecodeTypeKind.Float or GameEventScriptBytecodeTypeKind.Percentage or GameEventScriptBytecodeTypeKind.Boolean
                    ? GesValue.GesFloat(Math.Exp(operand.AsNumber()))
                    : GesValue.GesNothing();
            case GesUnaryOperator.Floor:
            case GesUnaryOperator.Ceil:
            case GesUnaryOperator.Truncate:
            case GesUnaryOperator.RoundHalfEven:
            case GesUnaryOperator.RoundHalfUp:
            case GesUnaryOperator.RoundHalfDown:
                if (operand.Kind is not (GameEventScriptBytecodeTypeKind.Integer or GameEventScriptBytecodeTypeKind.Float or GameEventScriptBytecodeTypeKind.Percentage or GameEventScriptBytecodeTypeKind.Boolean))
                {
                    return GesValue.GesNothing();
                }

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
                if ((operand.Unit.IsNumericUnit() && operand.Unit != GameEventScriptBytecodeInstructionUnit.UnitDegree) ||
                    operand.Kind is not (GameEventScriptBytecodeTypeKind.Integer or GameEventScriptBytecodeTypeKind.Float or GameEventScriptBytecodeTypeKind.Percentage or GameEventScriptBytecodeTypeKind.Boolean) ||
                    !double.IsFinite(operand.AsNumber()))
                {
                    return GesValue.GesNothing();
                }

                return GesValue.GesFloat(operand.AsNumber() / 180d * GameEventScriptMathConstants.Pi);
            case GesUnaryOperator.DegreeFromRadians:
                if (operand.Unit.IsNumericUnit() ||
                    operand.Kind is not (GameEventScriptBytecodeTypeKind.Integer or GameEventScriptBytecodeTypeKind.Float or GameEventScriptBytecodeTypeKind.Percentage or GameEventScriptBytecodeTypeKind.Boolean) ||
                    !double.IsFinite(operand.AsNumber()))
                {
                    return GesValue.GesNothing();
                }

                return GesValue.GesFloat(operand.AsNumber() / GameEventScriptMathConstants.Pi * 180d, GameEventScriptBytecodeInstructionUnit.UnitDegree);
            case GesUnaryOperator.WrapDegree:
                if ((operand.Unit.IsNumericUnit() && operand.Unit != GameEventScriptBytecodeInstructionUnit.UnitDegree) ||
                    operand.Kind is not (GameEventScriptBytecodeTypeKind.Integer or GameEventScriptBytecodeTypeKind.Float or GameEventScriptBytecodeTypeKind.Percentage or GameEventScriptBytecodeTypeKind.Boolean) ||
                    !double.IsFinite(operand.AsNumber()))
                {
                    return GesValue.GesNothing();
                }

                var wrapped = operand.AsNumber() % 360d;
                if (wrapped < 0d) wrapped += 360d;
                return GesValue.GesFloat(wrapped == 360d ? 0d : wrapped, GameEventScriptBytecodeInstructionUnit.UnitDegree);
            default:
                return null;
        }
    }

    private static GesValue? EvaluateConstantBinaryValue(BinaryExpressionNode binary)
    {
        if (EvaluateConstantValue(binary.Left) is not { } left ||
            EvaluateConstantValue(binary.Right) is not { } right)
        {
            return null;
        }

        switch (binary.Operator)
        {
            case GesBinaryOperator.Default:
                return left.HasValue ? left : right;
            case GesBinaryOperator.Or:
                return EvaluateConstantOr(left, right);
            case GesBinaryOperator.And:
                return EvaluateConstantAnd(left, right);
            case GesBinaryOperator.Xor:
                if (IsTruthIndeterminate(left) || IsTruthIndeterminate(right))
                {
                    return GesValue.GesNothing();
                }

                return GesValue.GesBoolean(left.AsBoolean() ^ right.AsBoolean());
            case GesBinaryOperator.Implies:
                return EvaluateConstantImplies(left, right);
            case GesBinaryOperator.Power:
                return GameEventScriptNumber.Power(in left, in right);
        }

        if (left.IsNothing || right.IsNothing)
        {
            return GesValue.GesNothing();
        }

        var sameUnit = left.Unit == right.Unit;
        var bothIntegers = left.Kind == GameEventScriptBytecodeTypeKind.Integer &&
                           right.Kind == GameEventScriptBytecodeTypeKind.Integer;
        if (bothIntegers && sameUnit)
        {
            switch (binary.Operator)
            {
                case GesBinaryOperator.IntegerDivide:
                    return GameEventScriptNumber.FloorDivideExact(left.IntegerValue, right.IntegerValue) is { } quotient
                        ? GesValue.GesInteger(quotient, DivideResultUnit(left.Unit))
                        : GesValue.GesFloat(Math.Floor((double)left.IntegerValue / right.IntegerValue), DivideResultUnit(left.Unit));
                case GesBinaryOperator.Modulo:
                    return right.IntegerValue == 0 ? GesValue.GesNothing() : GesValue.GesInteger(GameEventScriptNumber.Modulo(left.IntegerValue, right.IntegerValue), left.Unit);
                case GesBinaryOperator.Remainder:
                    return right.IntegerValue == 0 ? GesValue.GesNothing() : GesValue.GesInteger(GameEventScriptNumber.Remainder(left.IntegerValue, right.IntegerValue), left.Unit);
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
            case GesBinaryOperator.Add:
                return FoldAdd(left, right, leftNumber, rightNumber);
            case GesBinaryOperator.Subtract:
                return FoldSubtract(left, right, leftNumber, rightNumber);
            case GesBinaryOperator.Multiply:
                return FoldMultiply(left, right, leftNumber, rightNumber);
            case GesBinaryOperator.Divide when rightNumber != 0d && sameUnit:
                if (left.Kind == GameEventScriptBytecodeTypeKind.Percentage && right.Kind is GameEventScriptBytecodeTypeKind.Integer or GameEventScriptBytecodeTypeKind.Float)
                {
                    return GesValue.GesPercentage(leftNumber / rightNumber);
                }

                return CreateNumber(leftNumber / rightNumber, DivideResultUnit(left.Unit), false);
            case GesBinaryOperator.IntegerDivide when rightNumber != 0d && sameUnit:
                return CreateNumber(Math.Floor(leftNumber / rightNumber), DivideResultUnit(left.Unit), true);
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
            default:
                return null;
        }
    }

    private static GesValue EvaluateConstantOr(GesValue left, GesValue right)
    {
        if (!IsTruthIndeterminate(left) && left.AsBoolean() || !IsTruthIndeterminate(right) && right.AsBoolean())
        {
            return GesValue.GesBoolean(true);
        }

        return IsTruthIndeterminate(left) || IsTruthIndeterminate(right)
            ? GesValue.GesNothing()
            : GesValue.GesBoolean(false);
    }

    private static GesValue EvaluateConstantAnd(GesValue left, GesValue right)
    {
        if (!IsTruthIndeterminate(left) && !left.AsBoolean() || !IsTruthIndeterminate(right) && !right.AsBoolean())
        {
            return GesValue.GesBoolean(false);
        }

        return IsTruthIndeterminate(left) || IsTruthIndeterminate(right)
            ? GesValue.GesNothing()
            : GesValue.GesBoolean(true);
    }

    private static GesValue EvaluateConstantImplies(GesValue left, GesValue right)
    {
        if (!IsTruthIndeterminate(left) && !left.AsBoolean() || !IsTruthIndeterminate(right) && right.AsBoolean())
        {
            return GesValue.GesBoolean(true);
        }

        return IsTruthIndeterminate(left) || IsTruthIndeterminate(right)
            ? GesValue.GesNothing()
            : GesValue.GesBoolean(false);
    }

    private static bool IsTruthIndeterminate(GesValue value)
        => value.Kind is GameEventScriptBytecodeTypeKind.Nothing or
            GameEventScriptBytecodeTypeKind.List or
            GameEventScriptBytecodeTypeKind.Map or
            GameEventScriptBytecodeTypeKind.Dice;

    private static GesValue? FoldAdd(GesValue left, GesValue right, double leftNumber, double rightNumber)
    {
        if (left.Kind == GameEventScriptBytecodeTypeKind.Integer &&
            right.Kind == GameEventScriptBytecodeTypeKind.Integer &&
            left.Unit == right.Unit)
        {
            return GameEventScriptNumber.AddExact(left.IntegerValue, right.IntegerValue) is { } integerResult
                ? GesValue.GesInteger(integerResult, left.Unit)
                : GesValue.GesFloat((double)left.IntegerValue + right.IntegerValue, left.Unit);
        }

        if (left.Unit == right.Unit &&
            left.Kind != GameEventScriptBytecodeTypeKind.Percentage &&
            right.Kind != GameEventScriptBytecodeTypeKind.Percentage)
        {
            return CreateNumber(leftNumber + rightNumber, left.Unit, false);
        }

        if (left.Kind == GameEventScriptBytecodeTypeKind.Percentage && right.Kind == GameEventScriptBytecodeTypeKind.Percentage)
        {
            return GesValue.GesPercentage(leftNumber + rightNumber);
        }

        if (right.Kind == GameEventScriptBytecodeTypeKind.Percentage && left.Unit.IsNumericUnit())
        {
            return CreateNumber(leftNumber + leftNumber * rightNumber, left.Unit, false);
        }

        return null;
    }

    private static GesValue? FoldSubtract(GesValue left, GesValue right, double leftNumber, double rightNumber)
    {
        if (left.Kind == GameEventScriptBytecodeTypeKind.Integer &&
            right.Kind == GameEventScriptBytecodeTypeKind.Integer &&
            left.Unit == right.Unit)
        {
            return GameEventScriptNumber.SubtractExact(left.IntegerValue, right.IntegerValue) is { } integerResult
                ? GesValue.GesInteger(integerResult, left.Unit)
                : GesValue.GesFloat((double)left.IntegerValue - right.IntegerValue, left.Unit);
        }

        if (left.Unit == right.Unit &&
            left.Kind != GameEventScriptBytecodeTypeKind.Percentage &&
            right.Kind != GameEventScriptBytecodeTypeKind.Percentage)
        {
            return CreateNumber(leftNumber - rightNumber, left.Unit, false);
        }

        if (left.Kind == GameEventScriptBytecodeTypeKind.Percentage && right.Kind == GameEventScriptBytecodeTypeKind.Percentage)
        {
            return GesValue.GesPercentage(leftNumber - rightNumber);
        }

        if (right.Kind == GameEventScriptBytecodeTypeKind.Percentage && left.Unit.IsNumericUnit())
        {
            return CreateNumber(leftNumber - leftNumber * rightNumber, left.Unit, false);
        }

        return null;
    }

    private static GesValue? FoldMultiply(GesValue left, GesValue right, double leftNumber, double rightNumber)
    {
        if (left.Kind == GameEventScriptBytecodeTypeKind.Integer &&
            right.Kind == GameEventScriptBytecodeTypeKind.Integer)
        {
            if (left.Unit == GameEventScriptBytecodeInstructionUnit.UnitNone && right.Unit == GameEventScriptBytecodeInstructionUnit.UnitNone)
            {
                return GameEventScriptNumber.MultiplyExact(left.IntegerValue, right.IntegerValue) is { } integerResult
                    ? GesValue.GesInteger(integerResult)
                    : GesValue.GesFloat((double)left.IntegerValue * right.IntegerValue);
            }

            if (ProductUnit(left.Unit, right.Unit) is { } productUnit)
            {
                return CreateNumber((double)left.IntegerValue * right.IntegerValue, productUnit, false);
            }

            return null;
        }

        if (left.Kind == GameEventScriptBytecodeTypeKind.Percentage && right.Kind == GameEventScriptBytecodeTypeKind.Percentage)
        {
            return GesValue.GesPercentage(leftNumber * rightNumber);
        }

        if (left.Kind == GameEventScriptBytecodeTypeKind.Percentage)
        {
            return CreateNumber(leftNumber * rightNumber, right.Unit, false);
        }

        if (right.Kind == GameEventScriptBytecodeTypeKind.Percentage)
        {
            return CreateNumber(leftNumber * rightNumber, left.Unit, false);
        }

        if (ProductUnit(left.Unit, right.Unit) is { } unit)
        {
            return CreateNumber(leftNumber * rightNumber, unit, false);
        }

        return null;
    }

    private static double? ReadFiniteNumber(GesValue value)
    {
        switch (value.Kind)
        {
            case GameEventScriptBytecodeTypeKind.Integer:
            case GameEventScriptBytecodeTypeKind.Float:
            case GameEventScriptBytecodeTypeKind.Percentage:
                return double.IsFinite(value.AsNumber()) ? value.AsNumber() : null;
            case GameEventScriptBytecodeTypeKind.Boolean:
                return value.AsBoolean() ? 1d : 0d;
            default:
                return null;
        }
    }

    private static GesValue CreateNumber(double number, GameEventScriptBytecodeInstructionUnit unit, bool preferInteger)
    {
        if (!double.IsFinite(number))
        {
            return GesValue.GesNothing();
        }

        if (preferInteger &&
            GameEventScriptNumber.CanRepresentAsInteger(number))
        {
            return GesValue.GesInteger((long)number, unit);
        }

        return GesValue.GesFloat(number, unit);
    }

    private static GameEventScriptBytecodeInstructionUnit? ProductUnit(GameEventScriptBytecodeInstructionUnit left, GameEventScriptBytecodeInstructionUnit right)
    {
        if (left.IsNumericUnit() && right.IsNumericUnit())
        {
            return null;
        }

        return left is GameEventScriptBytecodeInstructionUnit.UnitNone ? right : left;
    }

    private static GameEventScriptBytecodeInstructionUnit DivideResultUnit(GameEventScriptBytecodeInstructionUnit unit)
        => unit.IsNumericUnit() ? GameEventScriptBytecodeInstructionUnit.UnitNone : unit;

    private static ExpressionNode? ConvertValueToLiteral(GesValue value)
    {
        switch (value.Kind)
        {
            case GameEventScriptBytecodeTypeKind.Nothing:
                return new NothingLiteralExpressionNode();
            case GameEventScriptBytecodeTypeKind.Boolean:
                return new BooleanLiteralExpressionNode(value.AsBoolean());
            case GameEventScriptBytecodeTypeKind.Integer:
                return value.Unit.IsNumericUnit()
                    ? new UnitIntegerLiteralExpressionNode(value.IntegerValue, value.Unit.ToTypeName())
                    : new IntegerLiteralExpressionNode(value.IntegerValue);
            case GameEventScriptBytecodeTypeKind.Float:
                if (double.IsNaN(value.AsNumber()) || double.IsInfinity(value.AsNumber()))
                {
                    return null;
                }

                return value.Unit.IsNumericUnit()
                    ? new UnitFloatLiteralExpressionNode(value.AsNumber(), value.Unit.ToTypeName())
                    : new FloatLiteralExpressionNode(value.AsNumber());
            case GameEventScriptBytecodeTypeKind.Percentage:
                return new PercentageLiteralExpressionNode(value.AsNumber());
            case GameEventScriptBytecodeTypeKind.Text:
                return new TextLiteralExpressionNode(value.TextValue);
            case GameEventScriptBytecodeTypeKind.Tag:
                return new TagLiteralExpressionNode(value.TextValue);
            case GameEventScriptBytecodeTypeKind.Vector:
                return new TypeConstructorExpressionNode(
                    "vector",
                    new ArgumentListNode([
                        new ArgumentNode(null, CreateFloatLiteral(value.X, value.Unit)),
                        new ArgumentNode(null, CreateFloatLiteral(value.Y, value.Unit)),
                        new ArgumentNode(null, CreateFloatLiteral(value.Z, value.Unit))
                    ]));
            case GameEventScriptBytecodeTypeKind.Point:
                return new TypeConstructorExpressionNode(
                    "point",
                    new ArgumentListNode([
                        new ArgumentNode(null, CreateFloatLiteral(value.X, value.Unit)),
                        new ArgumentNode(null, CreateFloatLiteral(value.Y, value.Unit)),
                        new ArgumentNode(null, CreateFloatLiteral(value.Z, value.Unit))
                    ]));
            case GameEventScriptBytecodeTypeKind.List:
            {
                var source = value.AsList();
                var items = new ExpressionNode[source.Length];
                for (var index = 0; index < items.Length; index++)
                {
                    if (ConvertValueToLiteral(source[index]) is not { } itemLiteral)
                    {
                        return null;
                    }

                    items[index] = itemLiteral;
                }

                return new ListLiteralExpressionNode(items);
            }
            case GameEventScriptBytecodeTypeKind.Map:
            {
                if (value.AsMap() is not { } map)
                {
                    return null;
                }

                var entries = new MapEntryNode[map.Length];
                for (var index = 0; index < map.StorageLength; index++)
                {
                    var mapValue = map.ValueAt(index);
                    if (ConvertValueToLiteral(mapValue) is not { } itemLiteral)
                    {
                        return null;
                    }

                    entries[index] = new MapEntryNode(map.KeyAt(index), itemLiteral);
                }

                return new MapLiteralExpressionNode(entries);
            }
            default:
                return null;
        }
    }

    private static ExpressionNode CreateFloatLiteral(double value, GameEventScriptBytecodeInstructionUnit? unit)
        => unit is { } valueUnit && valueUnit.IsNumericUnit()
            ? new UnitFloatLiteralExpressionNode(value, valueUnit.ToTypeName())
            : new FloatLiteralExpressionNode(value);

    private static long ToIntegerSaturated(double number) => GameEventScriptNumber.ToIntegerSaturated(number);

    private static double RoundHalfTowardZero(double value) => GameEventScriptNumber.RoundHalfTowardZero(value);
}
