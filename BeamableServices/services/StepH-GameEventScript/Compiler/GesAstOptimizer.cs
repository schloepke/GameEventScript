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

        if (FoldConstantExpression(optimized) is { } constantFolded)
        {
            return constantFolded with { SourceRange = optimized.SourceRange };
        }

        if (optimized is ExtensionCallExpressionNode extensionCallExpression &&
            FoldConstantStandardExtension(extensionCallExpression) is { } extensionFolded)
        {
            return extensionFolded with { SourceRange = optimized.SourceRange };
        }

        return optimized;
    }

    private static ExpressionNode? FoldConstantExpression(ExpressionNode expression)
    {
        if (expression is FloatLiteralExpressionNode or UnitFloatLiteralExpressionNode)
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

    private static ExpressionNode? FoldConstantStandardExtension(ExtensionCallExpressionNode extensionCall)
    {
        var reference = new GameEventScriptExtensionReference(
            extensionCall.ExtensionName,
            extensionCall.FunctionName,
            extensionCall.Arguments.Select(argument => argument.Name).ToArray());
        if (!GesStandardExtensions.IsStandardReference(reference))
        {
            return null;
        }

        var arguments = new GameEventScriptValue[extensionCall.Arguments.Count];
        for (var index = 0; index < extensionCall.Arguments.Count; index++)
        {
            if (EvaluateConstantValue(extensionCall.Arguments[index].Expression) is not { } argument)
            {
                return null;
            }

            arguments[index] = argument;
        }

        var value = GesStandardExtensions.Invoke(reference, arguments);
        if (value is null)
        {
            return null;
        }

        return ConvertValueToLiteral(value);
    }

    private static GameEventScriptValue? EvaluateConstantValue(ExpressionNode expression)
    {
        switch (expression)
        {
            case BooleanLiteralExpressionNode booleanLiteral:
                return GameEventScriptValueFactory.GesBoolean(booleanLiteral.Value);
            case NothingLiteralExpressionNode:
                return GameEventScriptValueFactory.GesNothing();
            case IntegerLiteralExpressionNode integerLiteral:
                return GameEventScriptValueFactory.GesInteger(integerLiteral.Value);
            case UnitIntegerLiteralExpressionNode unitIntegerLiteral:
                return GameEventScriptBytecodeInstructionUnits.ParseTypeName(unitIntegerLiteral.UnitName) is { } integerUnit
                    ? GameEventScriptValueFactory.GesInteger(unitIntegerLiteral.Value, integerUnit)
                    : GameEventScriptValueFactory.GesNothing();
            case FloatLiteralExpressionNode floatLiteral:
                return GameEventScriptValueFactory.GesFloat(floatLiteral.Value);
            case UnitFloatLiteralExpressionNode unitFloatLiteral:
                return GameEventScriptBytecodeInstructionUnits.ParseTypeName(unitFloatLiteral.UnitName) is { } unit
                    ? GameEventScriptValueFactory.GesFloat(unitFloatLiteral.Value, unit)
                    : GameEventScriptValueFactory.GesNothing();
            case PercentageLiteralExpressionNode percentageLiteral:
                return GameEventScriptValueFactory.GesPercentage(percentageLiteral.PercentValue / 100d);
            case TextLiteralExpressionNode textLiteral:
                return GameEventScriptValueFactory.GesText(textLiteral.Value);
            case TagLiteralExpressionNode tagLiteral:
                return GameEventScriptValueFactory.GesTag(tagLiteral.Name);
            case ListLiteralExpressionNode listLiteral:
            {
                var items = new GameEventScriptValue[listLiteral.Items.Count];
                for (var index = 0; index < listLiteral.Items.Count; index++)
                {
                    if (EvaluateConstantValue(listLiteral.Items[index]) is not { } item)
                    {
                        return null;
                    }

                    items[index] = item;
                }

                return GameEventScriptValueFactory.GesList(items);
            }
            case MapLiteralExpressionNode mapLiteral:
            {
                var entries = new KeyValuePair<string, GameEventScriptValue>[mapLiteral.Entries.Count];
                for (var index = 0; index < mapLiteral.Entries.Count; index++)
                {
                    var entry = mapLiteral.Entries[index];
                    if (EvaluateConstantValue(entry.Value) is not { } entryValue)
                    {
                        return null;
                    }

                    entries[index] = new KeyValuePair<string, GameEventScriptValue>(entry.Key, entryValue);
                }

                return GameEventScriptValueFactory.GesMap(entries);
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

    private static GameEventScriptValue? EvaluateConstantTypeCastValue(TypeCastExpressionNode typeCast)
    {
        if (EvaluateConstantValue(typeCast.Value) is not { } source)
        {
            return null;
        }

        switch (typeCast.TypeName)
        {
            case "number":
            case "numeric":
                if (ReadNumberForCast(source) is { } number)
                {
                    return double.IsFinite(number.Number)
                        ? CreateNumber(number.Number, number.Unit, Math.Truncate(number.Number) == number.Number)
                        : GameEventScriptValueFactory.GesFloat(number.Number, number.Unit);
                }

                return GameEventScriptValueFactory.GesNothing();
            default:
                return null;
        }
    }

    private static GameEventScriptValue? EvaluateConstantUnaryValue(UnaryExpressionNode unary)
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
                    ? GameEventScriptValueFactory.GesNothing()
                    : GameEventScriptValueFactory.GesBoolean(!operand.Boolean);
            case GesUnaryOperator.HasValue:
                return GameEventScriptValueFactory.GesBoolean(operand.HasValue);
            case GesUnaryOperator.Empty:
                return GameEventScriptValueFactory.GesBoolean(!operand.HasValue);
            case GesUnaryOperator.Negate
                when operand.Kind == GameEventScriptBytecodeTypeKind.Integer &&
                     operand.Integer != long.MinValue:
                return GameEventScriptValueFactory.GesInteger(-operand.Integer, operand.Unit);
            case GesUnaryOperator.Abs when operand.Kind is GameEventScriptBytecodeTypeKind.Integer:
                return operand.Integer == long.MinValue
                    ? GameEventScriptValueFactory.GesFloat(-(double)operand.Integer, operand.Unit)
                    : GameEventScriptValueFactory.GesInteger(Math.Abs(operand.Integer), operand.Unit);
            case GesUnaryOperator.Abs when operand.Kind is GameEventScriptBytecodeTypeKind.Float or GameEventScriptBytecodeTypeKind.Percentage:
                return operand.Kind == GameEventScriptBytecodeTypeKind.Percentage
                    ? GameEventScriptValueFactory.GesPercentage(Math.Abs(operand.Number))
                    : GameEventScriptValueFactory.GesFloat(Math.Abs(operand.Number), operand.Unit);
            case GesUnaryOperator.NaturalLog:
                return !operand.Unit.IsNumericUnit() && operand.Kind is GameEventScriptBytecodeTypeKind.Integer or GameEventScriptBytecodeTypeKind.Float or GameEventScriptBytecodeTypeKind.Percentage or GameEventScriptBytecodeTypeKind.Boolean
                    ? GameEventScriptValueFactory.GesFloat(Math.Log(operand.Number))
                    : GameEventScriptValueFactory.GesNothing();
            case GesUnaryOperator.Exp:
                return !operand.Unit.IsNumericUnit() && operand.Kind is GameEventScriptBytecodeTypeKind.Integer or GameEventScriptBytecodeTypeKind.Float or GameEventScriptBytecodeTypeKind.Percentage or GameEventScriptBytecodeTypeKind.Boolean
                    ? GameEventScriptValueFactory.GesFloat(Math.Exp(operand.Number))
                    : GameEventScriptValueFactory.GesNothing();
            case GesUnaryOperator.Floor:
            case GesUnaryOperator.Ceil:
            case GesUnaryOperator.Truncate:
            case GesUnaryOperator.RoundHalfEven:
            case GesUnaryOperator.RoundHalfUp:
            case GesUnaryOperator.RoundHalfDown:
                if (operand.Kind is not (GameEventScriptBytecodeTypeKind.Integer or GameEventScriptBytecodeTypeKind.Float or GameEventScriptBytecodeTypeKind.Percentage or GameEventScriptBytecodeTypeKind.Boolean))
                {
                    return GameEventScriptValueFactory.GesNothing();
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
                return GameEventScriptValueFactory.GesInteger(ToIntegerSaturated(rounded));
            case GesUnaryOperator.DegreeToRadians:
                if ((operand.Unit.IsNumericUnit() && operand.Unit != GameEventScriptBytecodeInstructionUnit.UnitDegree) ||
                    operand.Kind is not (GameEventScriptBytecodeTypeKind.Integer or GameEventScriptBytecodeTypeKind.Float or GameEventScriptBytecodeTypeKind.Percentage or GameEventScriptBytecodeTypeKind.Boolean) ||
                    !double.IsFinite(operand.Number))
                {
                    return GameEventScriptValueFactory.GesNothing();
                }

                return GameEventScriptValueFactory.GesFloat(operand.Number / 180d * GameEventScriptMathConstants.Pi);
            case GesUnaryOperator.DegreeFromRadians:
                if (operand.Unit.IsNumericUnit() ||
                    operand.Kind is not (GameEventScriptBytecodeTypeKind.Integer or GameEventScriptBytecodeTypeKind.Float or GameEventScriptBytecodeTypeKind.Percentage or GameEventScriptBytecodeTypeKind.Boolean) ||
                    !double.IsFinite(operand.Number))
                {
                    return GameEventScriptValueFactory.GesNothing();
                }

                return GameEventScriptValueFactory.GesFloat(operand.Number / GameEventScriptMathConstants.Pi * 180d, GameEventScriptBytecodeInstructionUnit.UnitDegree);
            case GesUnaryOperator.WrapDegree:
                if ((operand.Unit.IsNumericUnit() && operand.Unit != GameEventScriptBytecodeInstructionUnit.UnitDegree) ||
                    operand.Kind is not (GameEventScriptBytecodeTypeKind.Integer or GameEventScriptBytecodeTypeKind.Float or GameEventScriptBytecodeTypeKind.Percentage or GameEventScriptBytecodeTypeKind.Boolean) ||
                    !double.IsFinite(operand.Number))
                {
                    return GameEventScriptValueFactory.GesNothing();
                }

                var wrapped = operand.Number % 360d;
                if (wrapped < 0d) wrapped += 360d;
                return GameEventScriptValueFactory.GesFloat(wrapped == 360d ? 0d : wrapped, GameEventScriptBytecodeInstructionUnit.UnitDegree);
            default:
                return null;
        }
    }

    private static GameEventScriptValue? EvaluateConstantBinaryValue(BinaryExpressionNode binary)
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
                    return GameEventScriptValueFactory.GesNothing();
                }

                return GameEventScriptValueFactory.GesBoolean(left.Boolean ^ right.Boolean);
            case GesBinaryOperator.Implies:
                return EvaluateConstantImplies(left, right);
        }

        if (left.IsNothing || right.IsNothing)
        {
            return GameEventScriptValueFactory.GesNothing();
        }

        if (ReadFiniteNumber(left) is not { } leftNumber ||
            ReadFiniteNumber(right) is not { } rightNumber)
        {
            return null;
        }

        var sameUnit = left.Unit == right.Unit;
        var bothIntegers = left.Kind == GameEventScriptBytecodeTypeKind.Integer &&
                           right.Kind == GameEventScriptBytecodeTypeKind.Integer;
        switch (binary.Operator)
        {
            case GesBinaryOperator.Equal:
                if (!bothIntegers || !sameUnit) goto default;
                return GameEventScriptValueFactory.GesBoolean(left.Integer == right.Integer);
            case GesBinaryOperator.NotEqual:
                if (!bothIntegers || !sameUnit) goto default;
                return GameEventScriptValueFactory.GesBoolean(left.Integer != right.Integer);
            case GesBinaryOperator.Less:
                if (!bothIntegers || !sameUnit) goto default;
                return GameEventScriptValueFactory.GesBoolean(left.Integer < right.Integer);
            case GesBinaryOperator.Greater:
                if (!bothIntegers || !sameUnit) goto default;
                return GameEventScriptValueFactory.GesBoolean(left.Integer > right.Integer);
            case GesBinaryOperator.LessOrEqual:
                if (!bothIntegers || !sameUnit) goto default;
                return GameEventScriptValueFactory.GesBoolean(left.Integer <= right.Integer);
            case GesBinaryOperator.GreaterOrEqual:
                if (!bothIntegers || !sameUnit) goto default;
                return GameEventScriptValueFactory.GesBoolean(left.Integer >= right.Integer);
            case GesBinaryOperator.Add:
                return FoldAdd(left, right, leftNumber, rightNumber);
            case GesBinaryOperator.Subtract:
                return FoldSubtract(left, right, leftNumber, rightNumber);
            case GesBinaryOperator.Multiply:
                return FoldMultiply(left, right, leftNumber, rightNumber);
            case GesBinaryOperator.Divide when rightNumber != 0d && sameUnit:
                if (left.Kind == GameEventScriptBytecodeTypeKind.Percentage && right.Kind is GameEventScriptBytecodeTypeKind.Integer or GameEventScriptBytecodeTypeKind.Float)
                {
                    return GameEventScriptValueFactory.GesPercentage(leftNumber / rightNumber);
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
            case GesBinaryOperator.Power when sameUnit:
                return CreateNumber(Math.Pow(leftNumber, rightNumber), left.Unit, false);
            default:
                return null;
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

    private static GameEventScriptValue? CreateCheckedInteger(
        long left,
        long right,
        GameEventScriptBytecodeInstructionUnit unit,
        Func<long, long, long> operation)
    {
        try
        {
            return GameEventScriptValueFactory.GesInteger(operation(left, right), unit);
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    private static GameEventScriptValue? FoldAdd(
        GameEventScriptValue left,
        GameEventScriptValue right,
        double leftNumber,
        double rightNumber)
    {
        if (left.Kind == GameEventScriptBytecodeTypeKind.Integer &&
            right.Kind == GameEventScriptBytecodeTypeKind.Integer &&
            left.Unit == right.Unit)
        {
            return CreateCheckedInteger(left.Integer, right.Integer, left.Unit, static (leftValue, rightValue) => checked(leftValue + rightValue));
        }

        if (left.Unit == right.Unit &&
            left.Kind != GameEventScriptBytecodeTypeKind.Percentage &&
            right.Kind != GameEventScriptBytecodeTypeKind.Percentage)
        {
            return CreateNumber(leftNumber + rightNumber, left.Unit, false);
        }

        if (left.Kind == GameEventScriptBytecodeTypeKind.Percentage && right.Kind == GameEventScriptBytecodeTypeKind.Percentage)
        {
            return GameEventScriptValueFactory.GesPercentage(leftNumber + rightNumber);
        }

        if (right.Kind == GameEventScriptBytecodeTypeKind.Percentage && left.Unit.IsNumericUnit())
        {
            return CreateNumber(leftNumber + leftNumber * rightNumber, left.Unit, false);
        }

        return null;
    }

    private static GameEventScriptValue? FoldSubtract(
        GameEventScriptValue left,
        GameEventScriptValue right,
        double leftNumber,
        double rightNumber)
    {
        if (left.Kind == GameEventScriptBytecodeTypeKind.Integer &&
            right.Kind == GameEventScriptBytecodeTypeKind.Integer &&
            left.Unit == right.Unit)
        {
            return CreateCheckedInteger(left.Integer, right.Integer, left.Unit, static (leftValue, rightValue) => checked(leftValue - rightValue));
        }

        if (left.Unit == right.Unit &&
            left.Kind != GameEventScriptBytecodeTypeKind.Percentage &&
            right.Kind != GameEventScriptBytecodeTypeKind.Percentage)
        {
            return CreateNumber(leftNumber - rightNumber, left.Unit, false);
        }

        if (left.Kind == GameEventScriptBytecodeTypeKind.Percentage && right.Kind == GameEventScriptBytecodeTypeKind.Percentage)
        {
            return GameEventScriptValueFactory.GesPercentage(leftNumber - rightNumber);
        }

        if (right.Kind == GameEventScriptBytecodeTypeKind.Percentage && left.Unit.IsNumericUnit())
        {
            return CreateNumber(leftNumber - leftNumber * rightNumber, left.Unit, false);
        }

        return null;
    }

    private static GameEventScriptValue? FoldMultiply(
        GameEventScriptValue left,
        GameEventScriptValue right,
        double leftNumber,
        double rightNumber)
    {
        if (left.Kind == GameEventScriptBytecodeTypeKind.Integer &&
            right.Kind == GameEventScriptBytecodeTypeKind.Integer)
        {
            if (left.Unit == GameEventScriptBytecodeInstructionUnit.UnitNone && right.Unit == GameEventScriptBytecodeInstructionUnit.UnitNone)
            {
                return CreateCheckedInteger(left.Integer, right.Integer, GameEventScriptBytecodeInstructionUnit.UnitNone, static (leftValue, rightValue) => checked(leftValue * rightValue));
            }

            if (ProductUnit(left.Unit, right.Unit) is { } productUnit)
            {
                return CreateNumber((double)left.Integer * right.Integer, productUnit, false);
            }

            return null;
        }

        if (left.Kind == GameEventScriptBytecodeTypeKind.Percentage && right.Kind == GameEventScriptBytecodeTypeKind.Percentage)
        {
            return GameEventScriptValueFactory.GesPercentage(leftNumber * rightNumber);
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

    private static (double Number, GameEventScriptBytecodeInstructionUnit Unit)? ReadNumberForCast(GameEventScriptValue value)
    {
        switch (value.Kind)
        {
            case GameEventScriptBytecodeTypeKind.Integer:
            case GameEventScriptBytecodeTypeKind.Float:
            case GameEventScriptBytecodeTypeKind.Percentage:
                return double.IsNaN(value.Number) ? null : (value.Number, value.Unit);
            case GameEventScriptBytecodeTypeKind.Boolean:
                return (value.Boolean ? 1d : 0d, value.Unit);
            case GameEventScriptBytecodeTypeKind.Text:
                return double.TryParse(value.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) &&
                       double.IsFinite(number)
                    ? (number, value.Unit)
                    : null;
            default:
                return null;
        }
    }

    private static double? ReadFiniteNumber(GameEventScriptValue value)
    {
        switch (value.Kind)
        {
            case GameEventScriptBytecodeTypeKind.Integer:
            case GameEventScriptBytecodeTypeKind.Float:
            case GameEventScriptBytecodeTypeKind.Percentage:
                return double.IsFinite(value.Number) ? value.Number : null;
            case GameEventScriptBytecodeTypeKind.Boolean:
                return value.Boolean ? 1d : 0d;
            default:
                return null;
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

    private static GameEventScriptBytecodeInstructionUnit? ProductUnit(
        GameEventScriptBytecodeInstructionUnit left,
        GameEventScriptBytecodeInstructionUnit right)
    {
        if (left.IsNumericUnit() && right.IsNumericUnit())
        {
            return null;
        }

        return left is GameEventScriptBytecodeInstructionUnit.UnitNone ? right : left;
    }

    private static GameEventScriptBytecodeInstructionUnit DivideResultUnit(GameEventScriptBytecodeInstructionUnit unit)
        => unit.IsNumericUnit() ? GameEventScriptBytecodeInstructionUnit.UnitNone : unit;

    private static ExpressionNode? ConvertValueToLiteral(GameEventScriptValue value)
    {
        switch (value.Kind)
        {
            case GameEventScriptBytecodeTypeKind.Nothing:
                return new NothingLiteralExpressionNode();
            case GameEventScriptBytecodeTypeKind.Boolean:
                return new BooleanLiteralExpressionNode(value.Boolean);
            case GameEventScriptBytecodeTypeKind.Integer:
                return value.Unit.IsNumericUnit()
                    ? new UnitIntegerLiteralExpressionNode(value.Integer, value.Unit.ToTypeName())
                    : new IntegerLiteralExpressionNode(value.Integer);
            case GameEventScriptBytecodeTypeKind.Float:
                if (double.IsNaN(value.Number) || double.IsInfinity(value.Number))
                {
                    return null;
                }

                return value.Unit.IsNumericUnit()
                    ? new UnitFloatLiteralExpressionNode(value.Number, value.Unit.ToTypeName())
                    : new FloatLiteralExpressionNode(value.Number);
            case GameEventScriptBytecodeTypeKind.Percentage:
                return new PercentageLiteralExpressionNode(value.Number * 100d);
            case GameEventScriptBytecodeTypeKind.Text:
                return new TextLiteralExpressionNode(value.Text);
            case GameEventScriptBytecodeTypeKind.Tag:
                return new TagLiteralExpressionNode(value.Text);
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
                var items = new List<ExpressionNode>();
                foreach (var item in value.AsList())
                {
                    if (ConvertValueToLiteral(item) is not { } itemLiteral)
                    {
                        return null;
                    }

                    items.Add(itemLiteral);
                }

                return new ListLiteralExpressionNode(items);
            }
            case GameEventScriptBytecodeTypeKind.Map:
            {
                var entries = new List<MapEntryNode>();
                foreach (var entry in value.AsMap())
                {
                    if (ConvertValueToLiteral(entry.Value) is not { } itemLiteral)
                    {
                        return null;
                    }

                    entries.Add(new MapEntryNode(entry.Key, itemLiteral));
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
