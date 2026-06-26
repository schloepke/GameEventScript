using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Runtime.VM;

namespace StepH.GameEventScript.Compiler;

internal static class GesAstOptimizer
{
    public static GesSyntaxTreeModule Optimize(GesSyntaxTreeModule module, GameEventScriptCompileOptions? options = null)
    {
        _ = options ?? new GameEventScriptCompileOptions();
        var knownTypeNames = new HashSet<string>(module.TypeDefinitions.Keys, StringComparer.Ordinal);
        foreach (var typeName in module.ExternalTypeDefinitions.Keys)
        {
            knownTypeNames.Add(typeName);
        }

        var optimizedTypes = module.TypeDefinitions.ToDictionary(
            pair => pair.Key,
            pair => OptimizeTypeDefinition(pair.Value, knownTypeNames),
            StringComparer.Ordinal);

        var optimizedCallables = module.Callables.ToDictionary(
            pair => pair.Key,
            pair => OptimizeCallableDefinition(pair.Value, knownTypeNames),
            StringComparer.Ordinal);

        var optimizedHandlers = module.Handlers.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<EventHandlerNode>)pair.Value
                .Select(handler => OptimizeHandler(handler, knownTypeNames))
                .ToArray(),
            StringComparer.Ordinal);

        return new GesSyntaxTreeModule(
            module.ModuleName,
            optimizedTypes,
            optimizedCallables,
            optimizedHandlers,
            module.ExternalTypeDefinitions);
    }

    private static TypeDefinitionNode OptimizeTypeDefinition(TypeDefinitionNode definition, ISet<string> knownTypeNames)
        => definition with
        {
            Fields = definition.Fields.Select(field => field with
            {
                MinimumExpression = field.MinimumExpression is null ? null : OptimizeExpression(field.MinimumExpression, knownTypeNames),
                MaximumExpression = field.MaximumExpression is null ? null : OptimizeExpression(field.MaximumExpression, knownTypeNames),
                ComputedExpression = field.ComputedExpression is null ? null : OptimizeExpression(field.ComputedExpression, knownTypeNames)
            }).ToArray()
        };

    private static GesCallableDefinition OptimizeCallableDefinition(GesCallableDefinition definition, ISet<string> knownTypeNames)
    {
        var optimized = OptimizeExpression(definition.Expression, knownTypeNames);
        return new GesCallableDefinition(definition.Name, definition.ParameterList, optimized, definition.Kind, definition.SourceRange);
    }

    private static EventHandlerNode OptimizeHandler(EventHandlerNode handler, ISet<string> knownTypeNames)
        => handler with { Statements = handler.Statements.Select(statement => OptimizeStatement(statement, knownTypeNames)).ToArray() };

    private static StatementNode OptimizeStatement(StatementNode statement, ISet<string> knownTypeNames)
        => statement switch
        {
            PublishStatementNode publish => publish with
            {
                MessageExpression = OptimizeExpression(publish.MessageExpression, knownTypeNames),
                TagExpressions = publish.TagExpressions.Select(expression => OptimizeExpression(expression, knownTypeNames)).ToArray()
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
        => body with { Statements = body.Statements.Select(statement => OptimizeStatement(statement, knownTypeNames)).ToArray() };

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
                Arguments = variadic.Arguments.Select(argument => OptimizeExpression(argument, knownTypeNames)).ToArray()
            },
            IntrinsicCallExpressionNode intrinsic => intrinsic with
            {
                Arguments = intrinsic.Arguments.Select(argument => OptimizeExpression(argument, knownTypeNames)).ToArray()
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
                Branches = guardedChoice.Branches.Select(branch => branch with
                {
                    ValueExpression = OptimizeExpression(branch.ValueExpression, knownTypeNames),
                    ConditionExpression = OptimizeExpression(branch.ConditionExpression, knownTypeNames)
                }).ToArray(),
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
            TypeCastExpressionNode typeCast => typeCast with
            {
                Value = OptimizeExpression(typeCast.Value, knownTypeNames)
            },
            TypeConstructorExpressionNode typeConstructor => typeConstructor with
            {
                ArgumentList = new ArgumentListNode(typeConstructor.Arguments.Select(argument => argument with
                {
                    Expression = OptimizeExpression(argument.Expression, knownTypeNames)
                }).ToArray())
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
                Items = list.Items.Select(item => OptimizeExpression(item, knownTypeNames)).ToArray()
            },
            MapLiteralExpressionNode dictionary => dictionary with
            {
                Entries = dictionary.Entries.Select(entry => entry with
                {
                    Value = OptimizeExpression(entry.Value, knownTypeNames)
                }).ToArray()
            },
            MessageLiteralExpressionNode message => message with
            {
                ArgumentList = new ArgumentListNode(message.Arguments.Select(argument => argument with
                {
                    Expression = OptimizeExpression(argument.Expression, knownTypeNames)
                }).ToArray())
            },
            CallExpressionNode call => call with
            {
                ArgumentList = new ArgumentListNode(call.ArgumentList.Arguments.Select(argument => argument with
                {
                    Expression = OptimizeExpression(argument.Expression, knownTypeNames)
                }).ToArray())
            },
            ExtensionCallExpressionNode extensionCall => extensionCall with
            {
                ArgumentList = new ArgumentListNode(extensionCall.Arguments.Select(argument => argument with
                {
                    Expression = OptimizeExpression(argument.Expression, knownTypeNames)
                }).ToArray())
            },
            _ => expression
        };

        if (TryFoldConstantExpression(optimized, out var constantFolded))
        {
            return constantFolded with { SourceRange = optimized.SourceRange };
        }

        if (optimized is ExtensionCallExpressionNode extensionCallExpression &&
            TryFoldConstantStandardExtension(extensionCallExpression, out var extensionFolded))
        {
            return extensionFolded with { SourceRange = optimized.SourceRange };
        }

        return optimized;
    }

    private static bool TryFoldConstantExpression(ExpressionNode expression, out ExpressionNode folded)
    {
        folded = expression;
        if (expression is FloatLiteralExpressionNode or UnitFloatLiteralExpressionNode)
        {
            return false;
        }

        return TryEvaluateConstantValue(expression, out var value) &&
               TryConvertValueToLiteral(value, out folded);
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
            Entries = pattern.Entries.Select(entry => entry with
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
            }).ToArray()
        };

    private static bool TryFoldConstantStandardExtension(ExtensionCallExpressionNode extensionCall, out ExpressionNode folded)
    {
        folded = extensionCall;
        var reference = new GameEventScriptExtensionReference(
            extensionCall.ExtensionName,
            extensionCall.FunctionName,
            extensionCall.Arguments.Select(argument => argument.Name).ToArray());
        if (!GesStandardExtensions.IsStandardReference(reference))
        {
            return false;
        }

        var arguments = new GameEventScriptValue[extensionCall.Arguments.Count];
        for (var index = 0; index < extensionCall.Arguments.Count; index++)
        {
            if (!TryEvaluateConstantValue(extensionCall.Arguments[index].Expression, out var argument))
            {
                return false;
            }

            arguments[index] = argument;
        }

        if (!GesStandardExtensions.TryInvoke(reference, arguments, out var value))
        {
            return false;
        }

        return TryConvertValueToLiteral(value, out folded);
    }

    private static bool TryEvaluateConstantValue(ExpressionNode expression, out GameEventScriptValue value)
    {
        switch (expression)
        {
            case BooleanLiteralExpressionNode booleanLiteral:
                value = GameEventScriptValueFactory.GesBoolean(booleanLiteral.Value);
                return true;
            case NothingLiteralExpressionNode:
                value = GameEventScriptValueFactory.GesNothing();
                return true;
            case IntegerLiteralExpressionNode integerLiteral:
                value = GameEventScriptValueFactory.GesInteger(integerLiteral.Value);
                return true;
            case UnitIntegerLiteralExpressionNode unitIntegerLiteral:
                value = GameEventScriptBytecodeInstructionUnits.TryParseTypeName(unitIntegerLiteral.UnitName, out var integerUnit)
                    ? GameEventScriptValueFactory.GesInteger(unitIntegerLiteral.Value, integerUnit)
                    : GameEventScriptValueFactory.GesNothing();
                return true;
            case FloatLiteralExpressionNode floatLiteral:
                value = GameEventScriptValueFactory.GesFloat(floatLiteral.Value);
                return true;
            case UnitFloatLiteralExpressionNode unitFloatLiteral:
                value = GameEventScriptBytecodeInstructionUnits.TryParseTypeName(unitFloatLiteral.UnitName, out var unit)
                    ? GameEventScriptValueFactory.GesFloat(unitFloatLiteral.Value, unit)
                    : GameEventScriptValueFactory.GesNothing();
                return true;
            case PercentageLiteralExpressionNode percentageLiteral:
                value = GameEventScriptValueFactory.GesPercentage(percentageLiteral.PercentValue / 100d);
                return true;
            case TextLiteralExpressionNode textLiteral:
                value = GameEventScriptValueFactory.GesText(textLiteral.Value);
                return true;
            case TagLiteralExpressionNode tagLiteral:
                value = GameEventScriptValueFactory.GesTag(tagLiteral.Name);
                return true;
            case ListLiteralExpressionNode listLiteral:
            {
                var items = new GameEventScriptValue[listLiteral.Items.Count];
                for (var index = 0; index < listLiteral.Items.Count; index++)
                {
                    if (!TryEvaluateConstantValue(listLiteral.Items[index], out items[index]))
                    {
                        value = GameEventScriptValueFactory.GesNothing();
                        return false;
                    }
                }

                value = GameEventScriptValueFactory.GesList(items);
                return true;
            }
            case MapLiteralExpressionNode mapLiteral:
            {
                var entries = new KeyValuePair<string, GameEventScriptValue>[mapLiteral.Entries.Count];
                for (var index = 0; index < mapLiteral.Entries.Count; index++)
                {
                    var entry = mapLiteral.Entries[index];
                    if (!TryEvaluateConstantValue(entry.Value, out var entryValue))
                    {
                        value = GameEventScriptValueFactory.GesNothing();
                        return false;
                    }

                    entries[index] = new KeyValuePair<string, GameEventScriptValue>(entry.Key, entryValue);
                }

                value = GameEventScriptValueFactory.GesMap(entries);
                return true;
            }
            case TypeCastExpressionNode typeCastExpression:
                return TryEvaluateConstantTypeCastValue(typeCastExpression, out value);
            case UnaryExpressionNode unaryExpression:
                return TryEvaluateConstantUnaryValue(unaryExpression, out value);
            case BinaryExpressionNode binaryExpression:
                return TryEvaluateConstantBinaryValue(binaryExpression, out value);
            default:
                value = GameEventScriptValueFactory.GesNothing();
                return false;
        }
    }

    private static bool TryEvaluateConstantTypeCastValue(TypeCastExpressionNode typeCast, out GameEventScriptValue value)
    {
        if (!TryEvaluateConstantValue(typeCast.Value, out var source))
        {
            value = GameEventScriptValueFactory.GesNothing();
            return false;
        }

        switch (typeCast.TypeName)
        {
            case "number":
            case "numeric":
                if (TryReadNumberForCast(source, out var number, out var unit))
                {
                    value = double.IsFinite(number)
                        ? CreateNumber(number, unit, Math.Truncate(number) == number)
                        : GameEventScriptValueFactory.GesFloat(number, unit);
                    return true;
                }

                value = GameEventScriptValueFactory.GesNothing();
                return true;
            default:
                value = GameEventScriptValueFactory.GesNothing();
                return false;
        }
    }

    private static bool TryEvaluateConstantUnaryValue(UnaryExpressionNode unary, out GameEventScriptValue value)
    {
        if (!TryEvaluateConstantValue(unary.Operand, out var operand))
        {
            value = GameEventScriptValueFactory.GesNothing();
            return false;
        }

        switch (unary.Operator)
        {
            case GesUnaryOperator.Not:
                value = operand.Kind is GameEventScriptBytecodeTypeKind.Nothing or
                    GameEventScriptBytecodeTypeKind.List or
                    GameEventScriptBytecodeTypeKind.Map or
                    GameEventScriptBytecodeTypeKind.Dice
                    ? GameEventScriptValueFactory.GesNothing()
                    : GameEventScriptValueFactory.GesBoolean(!operand.Boolean);
                return true;
            case GesUnaryOperator.HasValue:
                value = GameEventScriptValueFactory.GesBoolean(operand.HasValue);
                return true;
            case GesUnaryOperator.Empty:
                value = GameEventScriptValueFactory.GesBoolean(!operand.HasValue);
                return true;
            case GesUnaryOperator.Negate
                when operand.Kind == GameEventScriptBytecodeTypeKind.Integer &&
                     operand.Integer != long.MinValue:
                value = GameEventScriptValueFactory.GesInteger(-operand.Integer, operand.Unit);
                return true;
            case GesUnaryOperator.Abs when operand.Kind is GameEventScriptBytecodeTypeKind.Integer:
                value = operand.Integer == long.MinValue
                    ? GameEventScriptValueFactory.GesFloat(-(double)operand.Integer, operand.Unit)
                    : GameEventScriptValueFactory.GesInteger(Math.Abs(operand.Integer), operand.Unit);
                return true;
            case GesUnaryOperator.Abs when operand.Kind is GameEventScriptBytecodeTypeKind.Float or GameEventScriptBytecodeTypeKind.Percentage:
                value = operand.Kind == GameEventScriptBytecodeTypeKind.Percentage
                    ? GameEventScriptValueFactory.GesPercentage(Math.Abs(operand.Number))
                    : GameEventScriptValueFactory.GesFloat(Math.Abs(operand.Number), operand.Unit);
                return true;
            case GesUnaryOperator.NaturalLog:
                value = !operand.Unit.IsNumericUnit() && operand.Kind is GameEventScriptBytecodeTypeKind.Integer or GameEventScriptBytecodeTypeKind.Float or GameEventScriptBytecodeTypeKind.Percentage or GameEventScriptBytecodeTypeKind.Boolean
                    ? GameEventScriptValueFactory.GesFloat(Math.Log(operand.Number))
                    : GameEventScriptValueFactory.GesNothing();
                return true;
            case GesUnaryOperator.Exp:
                value = !operand.Unit.IsNumericUnit() && operand.Kind is GameEventScriptBytecodeTypeKind.Integer or GameEventScriptBytecodeTypeKind.Float or GameEventScriptBytecodeTypeKind.Percentage or GameEventScriptBytecodeTypeKind.Boolean
                    ? GameEventScriptValueFactory.GesFloat(Math.Exp(operand.Number))
                    : GameEventScriptValueFactory.GesNothing();
                return true;
            case GesUnaryOperator.Floor:
            case GesUnaryOperator.Ceil:
            case GesUnaryOperator.Truncate:
            case GesUnaryOperator.RoundHalfEven:
            case GesUnaryOperator.RoundHalfUp:
            case GesUnaryOperator.RoundHalfDown:
                if (operand.Kind is not (GameEventScriptBytecodeTypeKind.Integer or GameEventScriptBytecodeTypeKind.Float or GameEventScriptBytecodeTypeKind.Percentage or GameEventScriptBytecodeTypeKind.Boolean))
                {
                    value = GameEventScriptValueFactory.GesNothing();
                    return true;
                }

                var rounded = unary.Operator switch
                {
                    GesUnaryOperator.Floor => Math.Floor(operand.Number),
                    GesUnaryOperator.Ceil => Math.Ceiling(operand.Number),
                    GesUnaryOperator.Truncate => Math.Truncate(operand.Number),
                    GesUnaryOperator.RoundHalfEven => Math.Round(operand.Number, 0, MidpointRounding.ToEven),
                    GesUnaryOperator.RoundHalfUp => Math.Round(operand.Number, 0, MidpointRounding.AwayFromZero),
                    GesUnaryOperator.RoundHalfDown => RoundHalfTowardZero(operand.Number),
                    _ => 0d
                };
                value = GameEventScriptValueFactory.GesInteger(ToIntegerSaturated(rounded));
                return true;
            case GesUnaryOperator.DegreeToRadians:
                if ((operand.Unit.IsNumericUnit() && operand.Unit != GameEventScriptBytecodeInstructionUnit.UnitDegree) ||
                    operand.Kind is not (GameEventScriptBytecodeTypeKind.Integer or GameEventScriptBytecodeTypeKind.Float or GameEventScriptBytecodeTypeKind.Percentage or GameEventScriptBytecodeTypeKind.Boolean) ||
                    !double.IsFinite(operand.Number))
                {
                    value = GameEventScriptValueFactory.GesNothing();
                    return true;
                }

                value = GameEventScriptValueFactory.GesFloat(operand.Number / 180d * GameEventScriptMathConstants.Pi);
                return true;
            case GesUnaryOperator.DegreeFromRadians:
                if (operand.Unit.IsNumericUnit() ||
                    operand.Kind is not (GameEventScriptBytecodeTypeKind.Integer or GameEventScriptBytecodeTypeKind.Float or GameEventScriptBytecodeTypeKind.Percentage or GameEventScriptBytecodeTypeKind.Boolean) ||
                    !double.IsFinite(operand.Number))
                {
                    value = GameEventScriptValueFactory.GesNothing();
                    return true;
                }

                value = GameEventScriptValueFactory.GesFloat(operand.Number / GameEventScriptMathConstants.Pi * 180d, GameEventScriptBytecodeInstructionUnit.UnitDegree);
                return true;
            case GesUnaryOperator.WrapDegree:
                if ((operand.Unit.IsNumericUnit() && operand.Unit != GameEventScriptBytecodeInstructionUnit.UnitDegree) ||
                    operand.Kind is not (GameEventScriptBytecodeTypeKind.Integer or GameEventScriptBytecodeTypeKind.Float or GameEventScriptBytecodeTypeKind.Percentage or GameEventScriptBytecodeTypeKind.Boolean) ||
                    !double.IsFinite(operand.Number))
                {
                    value = GameEventScriptValueFactory.GesNothing();
                    return true;
                }

                var wrapped = operand.Number % 360d;
                if (wrapped < 0d) wrapped += 360d;
                value = GameEventScriptValueFactory.GesFloat(wrapped == 360d ? 0d : wrapped, GameEventScriptBytecodeInstructionUnit.UnitDegree);
                return true;
            default:
                value = GameEventScriptValueFactory.GesNothing();
                return false;
        }
    }

    private static bool TryEvaluateConstantBinaryValue(BinaryExpressionNode binary, out GameEventScriptValue value)
    {
        if (!TryEvaluateConstantValue(binary.Left, out var left) ||
            !TryEvaluateConstantValue(binary.Right, out var right))
        {
            value = GameEventScriptValueFactory.GesNothing();
            return false;
        }

        switch (binary.Operator)
        {
            case GesBinaryOperator.Default:
                value = left.HasValue ? left : right;
                return true;
            case GesBinaryOperator.Or:
                value = EvaluateConstantOr(left, right);
                return true;
            case GesBinaryOperator.And:
                value = EvaluateConstantAnd(left, right);
                return true;
            case GesBinaryOperator.Xor:
                if (IsTruthIndeterminate(left) || IsTruthIndeterminate(right))
                {
                    value = GameEventScriptValueFactory.GesNothing();
                    return true;
                }

                value = GameEventScriptValueFactory.GesBoolean(left.Boolean ^ right.Boolean);
                return true;
            case GesBinaryOperator.Implies:
                value = EvaluateConstantImplies(left, right);
                return true;
        }

        if (left.IsNothing || right.IsNothing)
        {
            value = GameEventScriptValueFactory.GesNothing();
            return true;
        }

        if (!TryReadFiniteNumber(left, out var leftNumber) ||
            !TryReadFiniteNumber(right, out var rightNumber))
        {
            value = GameEventScriptValueFactory.GesNothing();
            return false;
        }

        var sameUnit = left.Unit == right.Unit;
        var bothIntegers = left.Kind == GameEventScriptBytecodeTypeKind.Integer &&
                           right.Kind == GameEventScriptBytecodeTypeKind.Integer;
        switch (binary.Operator)
        {
            case GesBinaryOperator.Equal:
                if (!bothIntegers || !sameUnit) goto default;
                value = GameEventScriptValueFactory.GesBoolean(left.Integer == right.Integer);
                return true;
            case GesBinaryOperator.NotEqual:
                if (!bothIntegers || !sameUnit) goto default;
                value = GameEventScriptValueFactory.GesBoolean(left.Integer != right.Integer);
                return true;
            case GesBinaryOperator.Less:
                if (!bothIntegers || !sameUnit) goto default;
                value = GameEventScriptValueFactory.GesBoolean(left.Integer < right.Integer);
                return true;
            case GesBinaryOperator.Greater:
                if (!bothIntegers || !sameUnit) goto default;
                value = GameEventScriptValueFactory.GesBoolean(left.Integer > right.Integer);
                return true;
            case GesBinaryOperator.LessOrEqual:
                if (!bothIntegers || !sameUnit) goto default;
                value = GameEventScriptValueFactory.GesBoolean(left.Integer <= right.Integer);
                return true;
            case GesBinaryOperator.GreaterOrEqual:
                if (!bothIntegers || !sameUnit) goto default;
                value = GameEventScriptValueFactory.GesBoolean(left.Integer >= right.Integer);
                return true;
            case GesBinaryOperator.Add:
                return TryFoldAdd(left, right, leftNumber, rightNumber, out value);
            case GesBinaryOperator.Subtract:
                return TryFoldSubtract(left, right, leftNumber, rightNumber, out value);
            case GesBinaryOperator.Multiply:
                return TryFoldMultiply(left, right, leftNumber, rightNumber, out value);
            case GesBinaryOperator.Divide when rightNumber != 0d && sameUnit:
                if (left.Kind == GameEventScriptBytecodeTypeKind.Percentage && right.Kind is GameEventScriptBytecodeTypeKind.Integer or GameEventScriptBytecodeTypeKind.Float)
                {
                    value = GameEventScriptValueFactory.GesPercentage(leftNumber / rightNumber);
                    return true;
                }

                value = CreateNumber(leftNumber / rightNumber, DivideResultUnit(left.Unit), false);
                return true;
            case GesBinaryOperator.IntegerDivide when rightNumber != 0d && sameUnit:
                value = CreateNumber(Math.Floor(leftNumber / rightNumber), DivideResultUnit(left.Unit), true);
                return true;
            case GesBinaryOperator.Modulo when rightNumber != 0d && sameUnit:
            {
                var remainder = leftNumber % rightNumber;
                if (remainder != 0d &&
                    (remainder < 0d && rightNumber > 0d || remainder > 0d && rightNumber < 0d))
                {
                    remainder += rightNumber;
                }

                value = CreateNumber(remainder, left.Unit, bothIntegers);
                return true;
            }
            case GesBinaryOperator.Remainder when rightNumber != 0d && sameUnit:
            {
                value = CreateNumber(leftNumber % rightNumber, left.Unit, bothIntegers);
                return true;
            }
            case GesBinaryOperator.Power when sameUnit:
                value = CreateNumber(Math.Pow(leftNumber, rightNumber), left.Unit, false);
                return true;
            default:
                value = GameEventScriptValueFactory.GesNothing();
                return false;
        }
    }

    private static GameEventScriptValue EvaluateConstantOr(GameEventScriptValue left, GameEventScriptValue right)
    {
        if (!IsTruthIndeterminate(left) && left.Boolean || !IsTruthIndeterminate(right) && right.Boolean)
        {
            return GameEventScriptValueFactory.GesBoolean(true);
        }

        return IsTruthIndeterminate(left) || IsTruthIndeterminate(right)
            ? GameEventScriptValueFactory.GesNothing()
            : GameEventScriptValueFactory.GesBoolean(false);
    }

    private static GameEventScriptValue EvaluateConstantAnd(GameEventScriptValue left, GameEventScriptValue right)
    {
        if (!IsTruthIndeterminate(left) && !left.Boolean || !IsTruthIndeterminate(right) && !right.Boolean)
        {
            return GameEventScriptValueFactory.GesBoolean(false);
        }

        return IsTruthIndeterminate(left) || IsTruthIndeterminate(right)
            ? GameEventScriptValueFactory.GesNothing()
            : GameEventScriptValueFactory.GesBoolean(true);
    }

    private static GameEventScriptValue EvaluateConstantImplies(GameEventScriptValue left, GameEventScriptValue right)
    {
        if (!IsTruthIndeterminate(left) && !left.Boolean || !IsTruthIndeterminate(right) && right.Boolean)
        {
            return GameEventScriptValueFactory.GesBoolean(true);
        }

        return IsTruthIndeterminate(left) || IsTruthIndeterminate(right)
            ? GameEventScriptValueFactory.GesNothing()
            : GameEventScriptValueFactory.GesBoolean(false);
    }

    private static bool IsTruthIndeterminate(GameEventScriptValue value)
        => value.Kind is GameEventScriptBytecodeTypeKind.Nothing or
            GameEventScriptBytecodeTypeKind.List or
            GameEventScriptBytecodeTypeKind.Map or
            GameEventScriptBytecodeTypeKind.Dice;

    private static bool TryCreateCheckedInteger(
        long left,
        long right,
        GameEventScriptBytecodeInstructionUnit unit,
        Func<long, long, long> operation,
        out GameEventScriptValue value)
    {
        try
        {
            value = GameEventScriptValueFactory.GesInteger(operation(left, right), unit);
            return true;
        }
        catch (OverflowException)
        {
            value = GameEventScriptValueFactory.GesNothing();
            return false;
        }
    }

    private static bool TryFoldAdd(
        GameEventScriptValue left,
        GameEventScriptValue right,
        double leftNumber,
        double rightNumber,
        out GameEventScriptValue value)
    {
        if (left.Kind == GameEventScriptBytecodeTypeKind.Integer &&
            right.Kind == GameEventScriptBytecodeTypeKind.Integer &&
            left.Unit == right.Unit)
        {
            return TryCreateCheckedInteger(left.Integer, right.Integer, left.Unit, static (leftValue, rightValue) => checked(leftValue + rightValue), out value);
        }

        if (left.Unit == right.Unit &&
            left.Kind != GameEventScriptBytecodeTypeKind.Percentage &&
            right.Kind != GameEventScriptBytecodeTypeKind.Percentage)
        {
            value = CreateNumber(leftNumber + rightNumber, left.Unit, false);
            return true;
        }

        if (left.Kind == GameEventScriptBytecodeTypeKind.Percentage && right.Kind == GameEventScriptBytecodeTypeKind.Percentage)
        {
            value = GameEventScriptValueFactory.GesPercentage(leftNumber + rightNumber);
            return true;
        }

        if (right.Kind == GameEventScriptBytecodeTypeKind.Percentage && left.Unit.IsNumericUnit())
        {
            value = CreateNumber(leftNumber + leftNumber * rightNumber, left.Unit, false);
            return true;
        }

        value = GameEventScriptValueFactory.GesNothing();
        return false;
    }

    private static bool TryFoldSubtract(
        GameEventScriptValue left,
        GameEventScriptValue right,
        double leftNumber,
        double rightNumber,
        out GameEventScriptValue value)
    {
        if (left.Kind == GameEventScriptBytecodeTypeKind.Integer &&
            right.Kind == GameEventScriptBytecodeTypeKind.Integer &&
            left.Unit == right.Unit)
        {
            return TryCreateCheckedInteger(left.Integer, right.Integer, left.Unit, static (leftValue, rightValue) => checked(leftValue - rightValue), out value);
        }

        if (left.Unit == right.Unit &&
            left.Kind != GameEventScriptBytecodeTypeKind.Percentage &&
            right.Kind != GameEventScriptBytecodeTypeKind.Percentage)
        {
            value = CreateNumber(leftNumber - rightNumber, left.Unit, false);
            return true;
        }

        if (left.Kind == GameEventScriptBytecodeTypeKind.Percentage && right.Kind == GameEventScriptBytecodeTypeKind.Percentage)
        {
            value = GameEventScriptValueFactory.GesPercentage(leftNumber - rightNumber);
            return true;
        }

        if (right.Kind == GameEventScriptBytecodeTypeKind.Percentage && left.Unit.IsNumericUnit())
        {
            value = CreateNumber(leftNumber - leftNumber * rightNumber, left.Unit, false);
            return true;
        }

        value = GameEventScriptValueFactory.GesNothing();
        return false;
    }

    private static bool TryFoldMultiply(
        GameEventScriptValue left,
        GameEventScriptValue right,
        double leftNumber,
        double rightNumber,
        out GameEventScriptValue value)
    {
        if (left.Kind == GameEventScriptBytecodeTypeKind.Integer &&
            right.Kind == GameEventScriptBytecodeTypeKind.Integer)
        {
            if (left.Unit == GameEventScriptBytecodeInstructionUnit.UnitNone && right.Unit == GameEventScriptBytecodeInstructionUnit.UnitNone)
            {
                return TryCreateCheckedInteger(left.Integer, right.Integer, GameEventScriptBytecodeInstructionUnit.UnitNone, static (leftValue, rightValue) => checked(leftValue * rightValue), out value);
            }

            if (TryProductUnit(left.Unit, right.Unit, out var productUnit))
            {
                value = CreateNumber((double)left.Integer * right.Integer, productUnit, false);
                return true;
            }

            value = GameEventScriptValueFactory.GesNothing();
            return false;
        }

        if (left.Kind == GameEventScriptBytecodeTypeKind.Percentage && right.Kind == GameEventScriptBytecodeTypeKind.Percentage)
        {
            value = GameEventScriptValueFactory.GesPercentage(leftNumber * rightNumber);
            return true;
        }

        if (left.Kind == GameEventScriptBytecodeTypeKind.Percentage)
        {
            value = CreateNumber(leftNumber * rightNumber, right.Unit, false);
            return true;
        }

        if (right.Kind == GameEventScriptBytecodeTypeKind.Percentage)
        {
            value = CreateNumber(leftNumber * rightNumber, left.Unit, false);
            return true;
        }

        if (TryProductUnit(left.Unit, right.Unit, out var unit))
        {
            value = CreateNumber(leftNumber * rightNumber, unit, false);
            return true;
        }

        value = GameEventScriptValueFactory.GesNothing();
        return false;
    }

    private static bool TryReadNumberForCast(GameEventScriptValue value, out double number, out GameEventScriptBytecodeInstructionUnit unit)
    {
        unit = value.Unit;
        switch (value.Kind)
        {
            case GameEventScriptBytecodeTypeKind.Integer:
            case GameEventScriptBytecodeTypeKind.Float:
            case GameEventScriptBytecodeTypeKind.Percentage:
                number = value.Number;
                return !double.IsNaN(number);
            case GameEventScriptBytecodeTypeKind.Boolean:
                number = value.Boolean ? 1d : 0d;
                return true;
            case GameEventScriptBytecodeTypeKind.Text:
                return double.TryParse(value.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out number) &&
                       double.IsFinite(number);
            default:
                number = default;
                return false;
        }
    }

    private static bool TryReadFiniteNumber(GameEventScriptValue value, out double number)
    {
        switch (value.Kind)
        {
            case GameEventScriptBytecodeTypeKind.Integer:
            case GameEventScriptBytecodeTypeKind.Float:
            case GameEventScriptBytecodeTypeKind.Percentage:
                number = value.Number;
                return double.IsFinite(number);
            case GameEventScriptBytecodeTypeKind.Boolean:
                number = value.Boolean ? 1d : 0d;
                return true;
            default:
                number = default;
                return false;
        }
    }

    private static GameEventScriptValue CreateNumber(double number, GameEventScriptBytecodeInstructionUnit unit, bool preferInteger)
    {
        if (!double.IsFinite(number))
        {
            return GameEventScriptValueFactory.GesNothing();
        }

        if (preferInteger &&
            number >= long.MinValue &&
            number <= long.MaxValue &&
            Math.Truncate(number) == number)
        {
            return GameEventScriptValueFactory.GesInteger((long)number, unit);
        }

        return GameEventScriptValueFactory.GesFloat(number, unit);
    }

    private static bool TryProductUnit(
        GameEventScriptBytecodeInstructionUnit left,
        GameEventScriptBytecodeInstructionUnit right,
        out GameEventScriptBytecodeInstructionUnit unit)
    {
        if (left.IsNumericUnit() && right.IsNumericUnit())
        {
            unit = GameEventScriptBytecodeInstructionUnit.UnitNone;
            return false;
        }

        unit = left is GameEventScriptBytecodeInstructionUnit.UnitNone ? right : left;
        return true;
    }

    private static GameEventScriptBytecodeInstructionUnit DivideResultUnit(GameEventScriptBytecodeInstructionUnit unit)
        => unit.IsNumericUnit() ? GameEventScriptBytecodeInstructionUnit.UnitNone : unit;

    private static bool TryConvertValueToLiteral(GameEventScriptValue value, out ExpressionNode expression)
    {
        switch (value.Kind)
        {
            case GameEventScriptBytecodeTypeKind.Nothing:
                expression = new NothingLiteralExpressionNode();
                return true;
            case GameEventScriptBytecodeTypeKind.Boolean:
                expression = new BooleanLiteralExpressionNode(value.Boolean);
                return true;
            case GameEventScriptBytecodeTypeKind.Integer:
                expression = value.Unit.IsNumericUnit()
                    ? new UnitIntegerLiteralExpressionNode(value.Integer, value.Unit.ToTypeName())
                    : new IntegerLiteralExpressionNode(value.Integer);
                return true;
            case GameEventScriptBytecodeTypeKind.Float:
                if (double.IsNaN(value.Number) || double.IsInfinity(value.Number))
                {
                    expression = default!;
                    return false;
                }

                expression = value.Unit.IsNumericUnit()
                    ? new UnitFloatLiteralExpressionNode(value.Number, value.Unit.ToTypeName())
                    : new FloatLiteralExpressionNode(value.Number);
                return true;
            case GameEventScriptBytecodeTypeKind.Percentage:
                expression = new PercentageLiteralExpressionNode(value.Number * 100d);
                return true;
            case GameEventScriptBytecodeTypeKind.Text:
                expression = new TextLiteralExpressionNode(value.Text);
                return true;
            case GameEventScriptBytecodeTypeKind.Tag:
                expression = new TagLiteralExpressionNode(value.Text);
                return true;
            case GameEventScriptBytecodeTypeKind.Vector:
                expression = new TypeConstructorExpressionNode(
                    "vector",
                    new ArgumentListNode([
                        new ArgumentNode(null, CreateFloatLiteral(value.X, value.Unit)),
                        new ArgumentNode(null, CreateFloatLiteral(value.Y, value.Unit)),
                        new ArgumentNode(null, CreateFloatLiteral(value.Z, value.Unit))
                    ]));
                return true;
            case GameEventScriptBytecodeTypeKind.Point:
                expression = new TypeConstructorExpressionNode(
                    "point",
                    new ArgumentListNode([
                        new ArgumentNode(null, CreateFloatLiteral(value.X, value.Unit)),
                        new ArgumentNode(null, CreateFloatLiteral(value.Y, value.Unit)),
                        new ArgumentNode(null, CreateFloatLiteral(value.Z, value.Unit))
                    ]));
                return true;
            case GameEventScriptBytecodeTypeKind.List:
            {
                var items = new List<ExpressionNode>();
                foreach (var item in value.AsList())
                {
                    if (!TryConvertValueToLiteral(item, out var itemLiteral))
                    {
                        expression = default!;
                        return false;
                    }

                    items.Add(itemLiteral);
                }

                expression = new ListLiteralExpressionNode(items);
                return true;
            }
            case GameEventScriptBytecodeTypeKind.Map:
            {
                var entries = new List<MapEntryNode>();
                foreach (var entry in value.AsMap())
                {
                    if (!TryConvertValueToLiteral(entry.Value, out var itemLiteral))
                    {
                        expression = default!;
                        return false;
                    }

                    entries.Add(new MapEntryNode(entry.Key, itemLiteral));
                }

                expression = new MapLiteralExpressionNode(entries);
                return true;
            }
            default:
                expression = default!;
                return false;
        }
    }

    private static ExpressionNode CreateFloatLiteral(double value, GameEventScriptBytecodeInstructionUnit? unit)
        => unit is { } valueUnit && valueUnit.IsNumericUnit()
            ? new UnitFloatLiteralExpressionNode(value, valueUnit.ToTypeName())
            : new FloatLiteralExpressionNode(value);

    private static long ToIntegerSaturated(double number)
    {
        if (double.IsNaN(number)) return 0;
        if (double.IsPositiveInfinity(number) || number > long.MaxValue) return long.MaxValue;
        if (double.IsNegativeInfinity(number) || number < long.MinValue) return long.MinValue;
        return (long)Math.Truncate(number);
    }

    private static double RoundHalfTowardZero(double value)
    {
        var sign = Math.Sign(value);
        var absolute = Math.Abs(value);
        var floor = Math.Floor(absolute);
        var fraction = absolute - floor;
        var roundedAbsolute = fraction > 0.5d ? floor + 1d : floor;
        return sign < 0 ? -roundedAbsolute : roundedAbsolute;
    }
}
