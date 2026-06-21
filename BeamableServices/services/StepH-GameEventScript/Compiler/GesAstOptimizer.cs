using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;
using StepH.GameEventScript.VirtualMachine;

namespace StepH.GameEventScript.Compiler;

internal static class GesAstOptimizer
{
    private static readonly ISet<string> EmptyTypeNames = new HashSet<string>(StringComparer.Ordinal);

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

        if (optimized is TypeCastExpressionNode typeCastExpression &&
            TryFoldConstantTypeCast(typeCastExpression, knownTypeNames, out var folded))
        {
            return folded with { SourceRange = optimized.SourceRange };
        }

        if (optimized is TypeConstructorExpressionNode typeConstructorExpression &&
            TryFoldConstantTypeConstructor(typeConstructorExpression, knownTypeNames, out var constructed))
        {
            return constructed with { SourceRange = optimized.SourceRange };
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

        if (!TryEvaluateConstant(expression, out var value))
        {
            return false;
        }

        return TryConvertValueToLiteral(value, out folded);
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

    private static bool TryFoldConstantTypeCast(TypeCastExpressionNode typeCast, ISet<string> knownTypeNames, out ExpressionNode folded)
    {
        folded = typeCast;
        if (!TryEvaluateConstant(typeCast.Value, out var source))
        {
            return false;
        }

        if (!TryConvertConstantType(source, typeCast.TypeName, knownTypeNames, out var converted))
        {
            return false;
        }

        return TryConvertValueToLiteral(converted, out folded);
    }

    private static bool TryFoldConstantTypeConstructor(TypeConstructorExpressionNode constructor, ISet<string> knownTypeNames, out ExpressionNode folded)
    {
        folded = constructor;
        if (!TryEvaluateConstantTypeConstructor(constructor, knownTypeNames, out var value))
        {
            return false;
        }

        return TryConvertValueToLiteral(value, out folded);
    }

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

        var arguments = new GameEventScriptBoxedValue[extensionCall.Arguments.Count];
        for (var index = 0; index < extensionCall.Arguments.Count; index++)
        {
            if (!TryEvaluateConstantBoxed(extensionCall.Arguments[index].Expression, out var argument))
            {
                return false;
            }

            arguments[index] = argument;
        }

        if (!GesStandardExtensions.TryInvoke(reference, arguments, out var value))
        {
            return false;
        }

        return TryConvertBoxedValueToLiteral(value, out folded);
    }

    private static bool TryEvaluateConstantBoxed(ExpressionNode expression, out GameEventScriptBoxedValue value)
    {
        switch (expression)
        {
            case BooleanLiteralExpressionNode booleanLiteral:
                value = GameEventScriptBoxedValue.FromBoolean(booleanLiteral.Value);
                return true;
            case NothingLiteralExpressionNode:
                value = GameEventScriptBoxedValue.Nothing();
                return true;
            case IntegerLiteralExpressionNode integerLiteral:
                value = GameEventScriptBoxedValue.FromInteger(integerLiteral.Value);
                return true;
            case UnitIntegerLiteralExpressionNode unitIntegerLiteral:
                value = GameEventScriptBytecodeInstructionUnits.TryParseTypeName(unitIntegerLiteral.UnitName, out var integerUnit)
                    ? GameEventScriptBoxedValue.FromInteger(unitIntegerLiteral.Value, integerUnit)
                    : GameEventScriptBoxedValue.Nothing();
                return true;
            case FloatLiteralExpressionNode floatLiteral:
                value = GameEventScriptBoxedValue.FromFloat(floatLiteral.Value);
                return true;
            case UnitFloatLiteralExpressionNode unitFloatLiteral:
                value = GameEventScriptBytecodeInstructionUnits.TryParseTypeName(unitFloatLiteral.UnitName, out var unit)
                    ? GameEventScriptBoxedValue.FromFloat(unitFloatLiteral.Value, unit)
                    : GameEventScriptBoxedValue.Nothing();
                return true;
            case PercentageLiteralExpressionNode percentageLiteral:
                value = GameEventScriptBoxedValue.FromPercentage(percentageLiteral.PercentValue / 100d);
                return true;
            case TextLiteralExpressionNode textLiteral:
                value = GameEventScriptBoxedValue.FromText(textLiteral.Value);
                return true;
            case TagLiteralExpressionNode tagLiteral:
                value = GameEventScriptBoxedValue.FromTag(tagLiteral.Name);
                return true;
            case ListLiteralExpressionNode listLiteral:
            {
                var items = new GameEventScriptBoxedValue[listLiteral.Items.Count];
                for (var index = 0; index < listLiteral.Items.Count; index++)
                {
                    if (!TryEvaluateConstantBoxed(listLiteral.Items[index], out items[index]))
                    {
                        value = GameEventScriptBoxedValue.Nothing();
                        return false;
                    }
                }

                value = GameEventScriptBoxedValue.FromList(items);
                return true;
            }
            case MapLiteralExpressionNode mapLiteral:
            {
                var entries = new KeyValuePair<string, GameEventScriptBoxedValue>[mapLiteral.Entries.Count];
                for (var index = 0; index < mapLiteral.Entries.Count; index++)
                {
                    var entry = mapLiteral.Entries[index];
                    if (!TryEvaluateConstantBoxed(entry.Value, out var entryValue))
                    {
                        value = GameEventScriptBoxedValue.Nothing();
                        return false;
                    }

                    entries[index] = new KeyValuePair<string, GameEventScriptBoxedValue>(entry.Key, entryValue);
                }

                value = GameEventScriptBoxedValue.FromMap(entries);
                return true;
            }
            default:
                value = GameEventScriptBoxedValue.Nothing();
                return false;
        }
    }

    private static bool TryEvaluateConstant(ExpressionNode expression, out GameEventScriptValue value)
    {
        switch (expression)
        {
            case BooleanLiteralExpressionNode booleanLiteral:
                value = GameEventScriptValueFactory.GesBoolean(booleanLiteral.Value);
                return true;
            case NothingLiteralExpressionNode:
                value = GameEventScriptNothingValue.Instance;
                return true;
            case IntegerLiteralExpressionNode integerLiteral:
                value = GameEventScriptValueFactory.GesInteger(integerLiteral.Value);
                return true;
            case UnitIntegerLiteralExpressionNode unitIntegerLiteral:
                value = GameEventScriptBytecodeInstructionUnits.TryParseTypeName(unitIntegerLiteral.UnitName, out var integerUnit)
                    ? GameEventScriptValueFactory.GesInteger(unitIntegerLiteral.Value, integerUnit)
                    : GameEventScriptValueFactory.GesFloatNaN();
                return true;
            case FloatLiteralExpressionNode floatLiteral:
                value = GameEventScriptValueFactory.GesFloat(floatLiteral.Value);
                return true;
            case PercentageLiteralExpressionNode percentageLiteral:
                value = GameEventScriptValueFactory.GesPercentage(percentageLiteral.PercentValue / 100d);
                return true;
            case UnitFloatLiteralExpressionNode unitFloatLiteral:
                value = GameEventScriptBytecodeInstructionUnits.TryParseTypeName(unitFloatLiteral.UnitName, out var unit)
                    ? GameEventScriptValueFactory.GesFloat(unitFloatLiteral.Value, unit)
                    : GameEventScriptValueFactory.GesFloatNaN();
                return true;
            case TextLiteralExpressionNode textLiteral:
                value = GameEventScriptValueFactory.GesText(textLiteral.Value);
                return true;
            case TagLiteralExpressionNode tagLiteral:
                value = GameEventScriptValueFactory.GesTag(tagLiteral.Name);
                return true;
            case ListLiteralExpressionNode listLiteral:
            {
                var items = new List<GameEventScriptValue>(listLiteral.Items.Count);
                foreach (var itemExpression in listLiteral.Items)
                {
                    if (!TryEvaluateConstant(itemExpression, out var item))
                    {
                        value = GameEventScriptNothingValue.Instance;
                        return false;
                    }

                    items.Add(item);
                }

                value = GameEventScriptValueFactory.GesList(items);
                return true;
            }
            case MapLiteralExpressionNode dictionaryLiteral:
            {
                var items = new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal);
                foreach (var entry in dictionaryLiteral.Entries)
                {
                    if (!TryEvaluateConstant(entry.Value, out var item))
                    {
                        value = GameEventScriptNothingValue.Instance;
                        return false;
                    }

                    items[entry.Key] = item;
                }

                value = GameEventScriptValueFactory.GesMap(items);
                return true;
            }
            case TypeCastExpressionNode castExpression:
            {
                if (!TryEvaluateConstant(castExpression.Value, out var source))
                {
                    value = GameEventScriptNothingValue.Instance;
                    return false;
                }

                if (!TryConvertConstantType(source, castExpression.TypeName, EmptyTypeNames, out value))
                {
                    return false;
                }

                return true;
            }
            case TypeConstructorExpressionNode constructorExpression:
                return TryEvaluateConstantTypeConstructor(constructorExpression, EmptyTypeNames, out value);
            case UnaryExpressionNode unaryExpression:
                return TryEvaluateConstantUnary(unaryExpression, out value);
            case BinaryExpressionNode binaryExpression:
                return TryEvaluateConstantBinary(binaryExpression, out value);
            default:
                value = GameEventScriptNothingValue.Instance;
                return false;
        }
    }

    private static bool TryEvaluateConstantTypeConstructor(TypeConstructorExpressionNode constructor, ISet<string> knownTypeNames, out GameEventScriptValue value)
    {
        value = GameEventScriptNothingValue.Instance;
        if (constructor.TypeName is "vector" or "point")
        {
            return TryEvaluateConstantSpatialConstructor(constructor, out value);
        }

        if (knownTypeNames.Contains(constructor.TypeName))
        {
            return false;
        }

        if (constructor.Arguments.Count != 1 ||
            constructor.Arguments[0].Label is not null ||
            !TryEvaluateConstant(constructor.Arguments[0].Expression, out var source))
        {
            return false;
        }

        return TryConvertConstantType(source, constructor.TypeName, knownTypeNames, out value);
    }

    private static bool TryEvaluateConstantSpatialConstructor(TypeConstructorExpressionNode constructor, out GameEventScriptValue value)
    {
        value = GameEventScriptNothingValue.Instance;
        if (constructor.Arguments.Count == 1 &&
            constructor.Arguments[0].Label is null &&
            TryEvaluateConstant(constructor.Arguments[0].Expression, out var source))
        {
            value = constructor.TypeName switch
            {
                "vector" => ConvertToVector(source),
                "point" => ConvertToPoint(source),
                _ => GameEventScriptNothingValue.Instance
            };
            return !value.IsNothing();
        }

        if (constructor.Arguments.Count == 2 &&
            constructor.Arguments.All(argument => argument.Label is null) &&
            TryEvaluateConstant(constructor.Arguments[0].Expression, out var xy) &&
            TryEvaluateConstant(constructor.Arguments[1].Expression, out var z) &&
            TryCreateSpatialLift(constructor.TypeName, xy, z, out value))
        {
            return !value.IsNothing();
        }

        if (constructor.Arguments.Count == 0 || constructor.Arguments.All(argument => argument.Label is not null))
        {
            var labeledComponents = new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal);
            foreach (var argument in constructor.Arguments)
            {
                if (!TryEvaluateConstant(argument.Expression, out var component))
                {
                    return false;
                }

                labeledComponents[argument.Label!] = component;
            }

            return TryCreateSpatialFromLabeledComponents(constructor.TypeName, labeledComponents, out value) &&
                   !value.IsNothing();
        }

        if (constructor.Arguments.Count is < 1 or > 3)
        {
            return false;
        }

        var components = new GameEventScriptValue[constructor.Arguments.Count];
        for (var index = 0; index < components.Length; index++)
        {
            if (!TryEvaluateConstant(constructor.Arguments[index].Expression, out components[index]))
            {
                return false;
            }
        }

        return TryCreateSpatialFromComponents(constructor.TypeName, components, out value);
    }

    private static bool TryCreateSpatialFromComponents(string typeName, IReadOnlyList<GameEventScriptValue> components, out GameEventScriptValue value)
        => typeName == "point"
            ? GesValueOperations.TryCreatePointFromComponents(components, out value)
            : GesValueOperations.TryCreateVectorFromComponents(components, out value);

    private static bool TryCreateSpatialLift(string typeName, GameEventScriptValue xy, GameEventScriptValue z, out GameEventScriptValue value)
        => typeName == "point"
            ? GesValueOperations.TryCreatePoint(xy, z, out value)
            : GesValueOperations.TryCreateVector(xy, z, out value);

    private static bool TryCreateSpatialFromLabeledComponents(string typeName, IReadOnlyDictionary<string, GameEventScriptValue> components, out GameEventScriptValue value)
        => typeName == "point"
            ? GesValueOperations.TryCreatePointFromLabeledComponents(typeName, components, out value)
            : GesValueOperations.TryCreateVectorFromLabeledComponents(typeName, components, out value);

    private static bool TryEvaluateConstantUnary(UnaryExpressionNode unary, out GameEventScriptValue value)
    {
        if (!TryEvaluateConstant(unary.Operand, out var operand))
        {
            value = GameEventScriptNothingValue.Instance;
            return false;
        }

        switch (unary.Operator)
        {
            case GesUnaryOperator.Negate:
                if (operand.IsNothing())
                {
                    value = GameEventScriptNothingValue.Instance;
                    return true;
                }

                var unwrapped = operand;
                if (unwrapped.IsPercentage())
                {
                    value = GameEventScriptValueFactory.GesPercentage(-unwrapped.AsNumber());
                    return true;
                }

                if (GesValueOperations.TryEvaluatePointUnary(unwrapped, "-", out value))
                {
                    return true;
                }

                if (GesValueOperations.TryEvaluateVectorUnary(unwrapped, "-", out value))
                {
                    return true;
                }

                if (unwrapped is GameEventScriptNumberValue { IsIntegerValue: true, IntegerValue: not long.MinValue } integer)
                {
                    value = GameEventScriptValueFactory.GesInteger(-integer.IntegerValue, integer.Unit);
                    return true;
                }

                if (GameEventScriptValue.TryGetNumericUnit(unwrapped, out var unit))
                {
                    value = GameEventScriptValueFactory.GesFloat(-unwrapped.AsNumber(), unit);
                    return true;
                }

                if (!TryCoerceNumericForOperation(unwrapped, out var numeric))
                {
                    value = GameEventScriptNothingValue.Instance;
                    return true;
                }

                value = ToGameEventScriptNumber(NegateNumeric(numeric));
                return true;
            case GesUnaryOperator.Not:
                if (IsTruthIndeterminate(operand))
                {
                    value = GameEventScriptNothingValue.Instance;
                    return true;
                }

                value = GameEventScriptValueFactory.GesBoolean(!operand.AsBoolean());
                return true;
            case GesUnaryOperator.HasValue:
                value = GameEventScriptValueFactory.GesBoolean(operand.HasSemanticValue());
                return true;
            case GesUnaryOperator.Empty:
                value = GameEventScriptValueFactory.GesBoolean(operand.IsSemanticallyEmpty());
                return true;
            case GesUnaryOperator.Abs:
                if (GesValueOperations.TryEvaluatePointUnary(operand, "abs", out value))
                {
                    return true;
                }

                if (GesValueOperations.TryEvaluateVectorUnary(operand, "abs", out value))
                {
                    return true;
                }

                value = GameEventScriptNothingValue.Instance;
                return false;
            case GesUnaryOperator.NaturalLog:
                value = EvaluateNaturalLogUnary(operand);
                return true;
            default:
                value = GameEventScriptNothingValue.Instance;
                return false;
        }
    }

    private static GameEventScriptValue EvaluateNaturalLogUnary(GameEventScriptValue operand)
    {
        if (operand.IsNothing())
        {
            return GameEventScriptNothingValue.Instance;
        }

        var unwrapped = operand;
        if (GameEventScriptValue.TryGetNumericUnit(unwrapped, out _))
        {
            return GameEventScriptValueFactory.GesFloatNaN();
        }

        if (!TryCoerceNumericForOperation(unwrapped, out var number))
        {
            return GameEventScriptValueFactory.GesFloatNaN();
        }

        if (number.IsNaN || number.IsNegativeInfinity)
        {
            return GameEventScriptValueFactory.GesFloatNaN();
        }

        if (number.IsPositiveInfinity)
        {
            return GameEventScriptValueFactory.GesFloatInfinity();
        }

        if (number.Value < 0d)
        {
            return GameEventScriptValueFactory.GesFloatNaN();
        }

        if (number.Value == 0d)
        {
            return GameEventScriptValueFactory.GesFloatNegativeInfinity();
        }

        var result = Math.Log((double)number.Value);
        if (double.IsNaN(result))
        {
            return GameEventScriptValueFactory.GesFloatNaN();
        }

        if (double.IsPositiveInfinity(result))
        {
            return GameEventScriptValueFactory.GesFloatInfinity();
        }

        if (double.IsNegativeInfinity(result))
        {
            return GameEventScriptValueFactory.GesFloatNegativeInfinity();
        }

        try
        {
            return GameEventScriptValueFactory.GesFloat((double)result);
        }
        catch (OverflowException)
        {
            return result < 0d
                ? GameEventScriptValueFactory.GesFloatNegativeInfinity()
                : GameEventScriptValueFactory.GesFloatInfinity();
        }
    }

    private static bool TryEvaluateConstantBinary(BinaryExpressionNode binary, out GameEventScriptValue value)
    {
        if (!TryEvaluateConstant(binary.Left, out var leftRaw) || !TryEvaluateConstant(binary.Right, out var rightRaw))
        {
            value = GameEventScriptNothingValue.Instance;
            return false;
        }

        if (binary.Operator == GesBinaryOperator.Default)
        {
            if (!leftRaw.HasSemanticValue())
            {
                value = rightRaw;
                return true;
            }

            value = leftRaw;
            return true;
        }

        switch (binary.Operator)
        {
            case GesBinaryOperator.Or:
                value = EvaluateLogicalOr(leftRaw, rightRaw);
                return true;
            case GesBinaryOperator.Xor:
                value = EvaluateLogicalXor(leftRaw, rightRaw);
                return true;
            case GesBinaryOperator.And:
                value = EvaluateLogicalAnd(leftRaw, rightRaw);
                return true;
            case GesBinaryOperator.Implies:
                value = EvaluateLogicalImplies(leftRaw, rightRaw);
                return true;
        }

        if (leftRaw.IsNothing() || rightRaw.IsNothing())
        {
            value = GameEventScriptNothingValue.Instance;
            return true;
        }

        var left = leftRaw;
        var right = rightRaw;

        switch (binary.Operator)
        {
            case GesBinaryOperator.Equal:
                value = GameEventScriptValueFactory.GesBoolean(GesValueOperations.AreEqual(left, right));
                return true;
            case GesBinaryOperator.NotEqual:
                value = GameEventScriptValueFactory.GesBoolean(!GesValueOperations.AreEqual(left, right));
                return true;
            case GesBinaryOperator.Less:
                value = EvaluateNumericComparison(left, right, comparison => comparison < 0);
                return true;
            case GesBinaryOperator.Greater:
                value = EvaluateNumericComparison(left, right, comparison => comparison > 0);
                return true;
            case GesBinaryOperator.LessOrEqual:
                value = EvaluateNumericComparison(left, right, comparison => comparison <= 0);
                return true;
            case GesBinaryOperator.GreaterOrEqual:
                value = EvaluateNumericComparison(left, right, comparison => comparison >= 0);
                return true;
            case GesBinaryOperator.Add:
                if ((left.IsText() || right.IsText()) &&
                    GesValueOperations.TryCombineWithPlus(left, right, out value))
                {
                    return true;
                }

                if (GesValueOperations.TryEvaluatePointBinary(left, "+", right, out value))
                {
                    return true;
                }

                if (GesValueOperations.TryEvaluateVectorBinary(left, "+", right, out value))
                {
                    return true;
                }

                if (GesValueOperations.TryEvaluateIntegerBinary(left, "+", right, out value))
                {
                    return true;
                }

                if (GesValueOperations.TryEvaluatePercentageBinary(left, "+", right, out value))
                {
                    return true;
                }

                if (GesValueOperations.TryEvaluateUnitBinary(left, "+", right, out value))
                {
                    return true;
                }

                if (GesValueOperations.TryCombineWithPlus(left, right, out value))
                {
                    return true;
                }

                if (!TryCoerceNumericForOperation(left, out var leftNumeric) ||
                    !TryCoerceNumericForOperation(right, out var rightNumeric))
                {
                    value = GameEventScriptNothingValue.Instance;
                    return false;
                }

                value = ToGameEventScriptNumericResult(left, "+", right, AddNumeric(leftNumeric, rightNumeric));
                return true;
            case GesBinaryOperator.Subtract:
                if (GesValueOperations.TryEvaluatePointBinary(left, "-", right, out value))
                {
                    return true;
                }

                if (GesValueOperations.TryEvaluateVectorBinary(left, "-", right, out value))
                {
                    return true;
                }

                if (GesValueOperations.TryEvaluateIntegerBinary(left, "-", right, out value))
                {
                    return true;
                }

                if (GesValueOperations.TryEvaluatePercentageBinary(left, "-", right, out value))
                {
                    return true;
                }

                if (GesValueOperations.TryEvaluateUnitBinary(left, "-", right, out value))
                {
                    return true;
                }

                if (!TryCoerceNumericForOperation(left, out var leftMinus) ||
                    !TryCoerceNumericForOperation(right, out var rightMinus))
                {
                    value = GameEventScriptNothingValue.Instance;
                    return false;
                }

                value = ToGameEventScriptNumericResult(left, "-", right, SubtractNumeric(leftMinus, rightMinus));
                return true;
            case GesBinaryOperator.Multiply:
                if (GesValueOperations.TryEvaluatePointBinary(left, "*", right, out value))
                {
                    return true;
                }

                if (GesValueOperations.TryEvaluateVectorBinary(left, "*", right, out value))
                {
                    return true;
                }

                if (GesValueOperations.TryEvaluateIntegerBinary(left, "*", right, out value))
                {
                    return true;
                }

                if (GesValueOperations.TryEvaluatePercentageBinary(left, "*", right, out value))
                {
                    return true;
                }

                if (GesValueOperations.TryEvaluateUnitBinary(left, "*", right, out value))
                {
                    return true;
                }

                if (!TryCoerceNumericForOperation(left, out var leftMultiply) ||
                    !TryCoerceNumericForOperation(right, out var rightMultiply))
                {
                    value = GameEventScriptNothingValue.Instance;
                    return false;
                }

                value = ToGameEventScriptNumericResult(left, "*", right, MultiplyNumeric(leftMultiply, rightMultiply));
                return true;
            case GesBinaryOperator.Divide:
                if (GesValueOperations.TryEvaluatePointBinary(left, "/", right, out value))
                {
                    return true;
                }

                if (GesValueOperations.TryEvaluateVectorBinary(left, "/", right, out value))
                {
                    return true;
                }

                if (GesValueOperations.TryEvaluatePercentageBinary(left, "/", right, out value))
                {
                    return true;
                }

                if (GesValueOperations.TryEvaluateUnitBinary(left, "/", right, out value))
                {
                    return true;
                }

                if (!TryCoerceNumericForOperation(left, out var leftDivide) ||
                    !TryCoerceNumericForOperation(right, out var rightDivide))
                {
                    value = GameEventScriptNothingValue.Instance;
                    return false;
                }

                value = ToGameEventScriptNumber(DivideNumeric(leftDivide, rightDivide));
                return true;
            case GesBinaryOperator.Modulo:
                if (GesValueOperations.TryEvaluatePointBinary(left, "mod", right, out value))
                {
                    return true;
                }

                if (GesValueOperations.TryEvaluateVectorBinary(left, "mod", right, out value))
                {
                    return true;
                }

                if (GesValueOperations.TryEvaluateIntegerBinary(left, "mod", right, out value))
                {
                    return true;
                }

                if (GesValueOperations.TryEvaluateUnitBinary(left, "mod", right, out value))
                {
                    return true;
                }

                if (!TryCoerceNumericForOperation(left, out var leftModulo) ||
                    !TryCoerceNumericForOperation(right, out var rightModulo))
                {
                    value = GameEventScriptNothingValue.Instance;
                    return false;
                }

                value = ToGameEventScriptNumericResult(left, "mod", right, ModuloNumeric(leftModulo, rightModulo));
                return true;
            case GesBinaryOperator.IntegerDivide:
                if (GesValueOperations.TryEvaluatePointBinary(left, "div", right, out value))
                {
                    return true;
                }

                if (GesValueOperations.TryEvaluateVectorBinary(left, "div", right, out value))
                {
                    return true;
                }

                if (GesValueOperations.TryEvaluateIntegerBinary(left, "div", right, out value))
                {
                    return true;
                }

                if (GesValueOperations.TryEvaluateUnitBinary(left, "div", right, out value))
                {
                    return true;
                }

                if (!TryCoerceNumericForOperation(left, out var leftIntegerDivide) ||
                    !TryCoerceNumericForOperation(right, out var rightIntegerDivide))
                {
                    value = GameEventScriptNothingValue.Instance;
                    return false;
                }

                value = ToGameEventScriptNumericResult(left, "div", right, IntegerDivideNumeric(leftIntegerDivide, rightIntegerDivide));
                return true;
            case GesBinaryOperator.Remainder:
                if (GesValueOperations.TryEvaluatePointBinary(left, "rem", right, out value))
                {
                    return true;
                }

                if (GesValueOperations.TryEvaluateVectorBinary(left, "rem", right, out value))
                {
                    return true;
                }

                if (GesValueOperations.TryEvaluateIntegerBinary(left, "rem", right, out value))
                {
                    return true;
                }

                if (GesValueOperations.TryEvaluateUnitBinary(left, "rem", right, out value))
                {
                    return true;
                }

                if (!TryCoerceNumericForOperation(left, out var leftRemainder) ||
                    !TryCoerceNumericForOperation(right, out var rightRemainder))
                {
                    value = GameEventScriptNothingValue.Instance;
                    return false;
                }

                value = ToGameEventScriptNumericResult(left, "rem", right, RemainderNumeric(leftRemainder, rightRemainder));
                return true;
            case GesBinaryOperator.Power:
                if (GesValueOperations.TryEvaluateUnitBinary(left, "^", right, out value))
                {
                    return true;
                }

                if (!GesValueOperations.TryCoerceNumericForOperation(left, out var leftPower) ||
                    !GesValueOperations.TryCoerceNumericForOperation(right, out var rightPower))
                {
                    value = GameEventScriptNothingValue.Instance;
                    return false;
                }

                value = GesValueOperations.ToGameEventScriptNumericResult(left, "^", right, GesValueOperations.PowerNumeric(leftPower, rightPower));
                return true;
            default:
                value = GameEventScriptNothingValue.Instance;
                return false;
        }
    }

    private static GameEventScriptValue EvaluateLogicalAnd(GameEventScriptValue left, GameEventScriptValue right)
    {
        if (IsFalse(left) || IsFalse(right))
        {
            return GameEventScriptValueFactory.GesBoolean(false);
        }

        return IsTruthIndeterminate(left) || IsTruthIndeterminate(right)
            ? GameEventScriptNothingValue.Instance
            : GameEventScriptValueFactory.GesBoolean(true);
    }

    private static GameEventScriptValue EvaluateLogicalOr(GameEventScriptValue left, GameEventScriptValue right)
    {
        if (IsTrue(left) || IsTrue(right))
        {
            return GameEventScriptValueFactory.GesBoolean(true);
        }

        return IsTruthIndeterminate(left) || IsTruthIndeterminate(right)
            ? GameEventScriptNothingValue.Instance
            : GameEventScriptValueFactory.GesBoolean(false);
    }

    private static GameEventScriptValue EvaluateLogicalXor(GameEventScriptValue left, GameEventScriptValue right)
        => IsTruthIndeterminate(left) || IsTruthIndeterminate(right)
            ? GameEventScriptNothingValue.Instance
            : GameEventScriptValueFactory.GesBoolean(IsTrue(left) ^ IsTrue(right));

    private static GameEventScriptValue EvaluateLogicalImplies(GameEventScriptValue left, GameEventScriptValue right)
    {
        if (IsFalse(left) || IsTrue(right))
        {
            return GameEventScriptValueFactory.GesBoolean(true);
        }

        if (IsTruthIndeterminate(left) || IsTruthIndeterminate(right))
        {
            return GameEventScriptNothingValue.Instance;
        }

        return GameEventScriptValueFactory.GesBoolean(false);
    }

    private static bool IsTrue(GameEventScriptValue value)
        => !IsTruthIndeterminate(value) && value.AsBoolean();

    private static bool IsFalse(GameEventScriptValue value)
        => !IsTruthIndeterminate(value) && !value.AsBoolean();

    private static bool IsTruthIndeterminate(GameEventScriptValue value)
        => value.Kind is GameEventScriptBytecodeTypeKind.Nothing or
            GameEventScriptBytecodeTypeKind.List or
            GameEventScriptBytecodeTypeKind.Map or
            GameEventScriptBytecodeTypeKind.Dice;

    private static GameEventScriptValue EvaluateNumericComparison(GameEventScriptValue left, GameEventScriptValue right, Func<int, bool> predicate)
    {
        if (!GesValueOperations.TryCompareNumericValues(left, right, out var comparison))
        {
            return GameEventScriptValueFactory.GesBoolean(false);
        }

        return GameEventScriptValueFactory.GesBoolean(predicate(comparison));
    }

    private static bool TryConvertConstantType(GameEventScriptValue value, string declaredType, ISet<string> knownTypeNames, out GameEventScriptValue converted)
    {
        if (GameEventScriptBytecodeInstructionUnits.TryParseQuantityTypeName(declaredType, out var quantityUnit))
        {
            converted = ConvertToNumericUnit(value, quantityUnit);
            return true;
        }

        if (GameEventScriptBytecodeInstructionUnits.IsQuantityTypeName(declaredType))
        {
            converted = value;
            return false;
        }

        switch (declaredType)
        {
            case "nothing":
                converted = GameEventScriptNothingValue.Instance;
                return true;
            case "tag":
                converted = ConvertToTag(value);
                return true;
            case "text":
                converted = GameEventScriptValueFactory.GesText(value.AsText());
                return true;
            case "percentage":
                converted = ConvertToPercentage(value);
                return true;
            case "number":
            case "numeric":
                converted = ConvertToNumber(value);
                return true;
            case "vector":
                converted = ConvertToVector(value);
                return true;
            case "point":
                converted = ConvertToPoint(value);
                return true;
            case "boolean":
                converted = GameEventScriptValueFactory.GesBoolean(value.AsBoolean());
                return true;
            case "list":
                converted = GameEventScriptValueFactory.GesList(value.AsList());
                return true;
            case "range":
                converted = value.IsRange() ? value : GameEventScriptNothingValue.Instance;
                return true;
            case "message":
                converted = GameEventScriptNothingValue.Instance;
                return true;
            case "handler":
                converted = value.Kind == GameEventScriptBytecodeTypeKind.Handler ? value : GameEventScriptNothingValue.Instance;
                return true;
            case "map":
                converted = GameEventScriptValueFactory.GesMap(value.AsMap());
                return true;
            case "dice":
                converted = GameEventScriptValueFactory.GesDice(value.AsDice());
                return true;
            default:
                if (knownTypeNames.Contains(declaredType))
                {
                    converted = GameEventScriptNothingValue.Instance;
                    return false;
                }

                converted = value;
                return true;
        }
    }

    private static GameEventScriptValue ConvertToFloat(GameEventScriptValue value)
    {
        var unwrapped = value;

        if (GesValueOperations.TryEraseVectorUnit(unwrapped, out var vectorWithoutUnit))
        {
            return vectorWithoutUnit;
        }

        if (unwrapped is GameEventScriptTagValue && unwrapped.TryConvertToNumber(out var convertedTag))
        {
            return ConvertToFloat(convertedTag);
        }

        if (!TryCoerceNumeric(unwrapped, out var number, out var isFinite))
        {
            return GameEventScriptValueFactory.GesFloatNaN();
        }

        if (isFinite)
        {
            return GameEventScriptValueFactory.GesFloat(number);
        }

        if (unwrapped.IsNaN())
        {
            return GameEventScriptValueFactory.GesFloatNaN();
        }

        return unwrapped.IsNegativeInfinity()
            ? GameEventScriptValueFactory.GesFloatNegativeInfinity()
            : GameEventScriptValueFactory.GesFloatInfinity();
    }

    private static GameEventScriptValue ConvertToNumber(GameEventScriptValue value)
    {
        if (value.IsInteger())
        {
            return value;
        }

        if (value.IsSeries() && value.TryConvertToNumber(out var convertedSeries))
        {
            return ConvertToNumber(convertedSeries);
        }

        var unit = GameEventScriptValue.TryGetNumericUnit(value, out var numericUnit)
            ? numericUnit
            : (GameEventScriptBytecodeInstructionUnit?)null;

        if (value.IsText())
        {
            return double.TryParse(value.AsText(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedText)
                ? ToNumberValue(parsedText, unit)
                : GameEventScriptNothingValue.Instance;
        }

        if (!GesValueOperations.TryCoerceNumericForOperation(value, out var number))
        {
            return GameEventScriptNothingValue.Instance;
        }

        if (number.IsNaN)
        {
            return GameEventScriptValueFactory.GesFloatNaN();
        }

        if (number.IsPositiveInfinity)
        {
            return GameEventScriptValueFactory.GesFloatInfinity();
        }

        if (number.IsNegativeInfinity)
        {
            return GameEventScriptValueFactory.GesFloatNegativeInfinity();
        }

        if (number.Value >= long.MinValue &&
            number.Value <= long.MaxValue &&
            number.Value == Math.Truncate(number.Value))
        {
            return GameEventScriptValueFactory.GesInteger((long)number.Value, unit);
        }

        return GameEventScriptValueFactory.GesFloat(number.Value, unit);
    }

    private static GameEventScriptValue ConvertToTag(GameEventScriptValue value)
    {
        if (value.Kind == GameEventScriptBytecodeTypeKind.Boolean)
        {
            return GameEventScriptValueFactory.GesTag(value.AsBoolean() ? "true" : "false");
        }

        var text = value.AsText();
        if (value.Kind == GameEventScriptBytecodeTypeKind.Text)
        {
            return GameEventScriptTagValue.TryNormalizeTextCast(text, out var normalized)
                ? GameEventScriptValueFactory.GesTag(normalized)
                : GameEventScriptNothingValue.Instance;
        }

        return GameEventScriptTagValue.IsValidTagName(text)
            ? GameEventScriptValueFactory.GesTag(text)
            : GameEventScriptNothingValue.Instance;
    }

    private static GameEventScriptValue ToNumberValue(double number, GameEventScriptBytecodeInstructionUnit? unit)
    {
        if (number >= long.MinValue &&
            number <= long.MaxValue &&
            number == Math.Truncate(number))
        {
            return GameEventScriptValueFactory.GesInteger((long)number, unit);
        }

        return GameEventScriptValueFactory.GesFloat(number, unit);
    }

    private static GameEventScriptValue ConvertToPercentage(GameEventScriptValue value)
    {
        var unwrapped = value;

        if (unwrapped.IsPercentage())
        {
            return unwrapped;
        }

        if (unwrapped.HasNumericUnit())
        {
            return GameEventScriptValueFactory.GesFloatNaN();
        }

        if (!TryCoerceNumeric(unwrapped, out var number, out var isFinite) || !isFinite)
        {
            return GameEventScriptValueFactory.GesFloatNaN();
        }

        var ratio = unwrapped.IsInteger()
            ? number / 100d
            : number > 1d || number < -1d
                ? number / 100d
                : number;
        return GameEventScriptValueFactory.GesPercentage(ratio);
    }

    private static GameEventScriptValue ConvertToNumericUnit(GameEventScriptValue value, GameEventScriptBytecodeInstructionUnit unit)
    {
        var unwrapped = value;

        if (GesValueOperations.TryApplyVectorUnit(unwrapped, unit, out var vectorWithUnit))
        {
            return vectorWithUnit;
        }

        if (GameEventScriptValue.TryGetNumericUnit(unwrapped, out var existingUnit) && existingUnit != unit)
        {
            return GameEventScriptValueFactory.GesFloatNaN();
        }

        if (unwrapped is GameEventScriptNumberValue { IsIntegerValue: true } &&
            TryCoerceNumeric(unwrapped, out var integerNumber, out var integerIsFinite) &&
            integerIsFinite)
        {
            return GameEventScriptValueFactory.GesInteger(GameEventScriptValue.ToIntegerSaturated(integerNumber), unit);
        }

        if (unwrapped.Kind is GameEventScriptBytecodeTypeKind.Integer or GameEventScriptBytecodeTypeKind.Float &&
            TryCoerceNumeric(unwrapped, out var number, out var isFinite) &&
            isFinite)
        {
            return GameEventScriptValueFactory.GesFloat(number, unit);
        }

        return GameEventScriptValueFactory.GesFloatNaN();
    }

    private static GameEventScriptValue ConvertToVector(GameEventScriptValue value)
    {
        var unwrapped = value;

        if (unwrapped is GameEventScriptVectorValue vector)
        {
            return vector;
        }

        if (TryCreateSpatialFromMembers("vector", unwrapped, out var vectorFromMembers))
        {
            return vectorFromMembers;
        }

        var items = unwrapped.AsList();
        if (items.Count is >= 1 and <= 3 &&
            TryCreateSpatialFromComponents("vector", items, out var vectorFromItems))
        {
            return vectorFromItems;
        }

        return TryCreateSpatialFromComponents("vector", [unwrapped], out var vectorFromScalar)
            ? vectorFromScalar
            : GameEventScriptNothingValue.Instance;
    }

    private static GameEventScriptValue ConvertToPoint(GameEventScriptValue value)
    {
        var unwrapped = value;

        if (unwrapped is GameEventScriptPointValue point)
        {
            return point;
        }

        if (TryCreateSpatialFromMembers("point", unwrapped, out var pointFromMembers))
        {
            return pointFromMembers;
        }

        var items = unwrapped.AsList();
        if (items.Count is >= 1 and <= 3 &&
            TryCreateSpatialFromComponents("point", items, out var pointFromItems))
        {
            return pointFromItems;
        }

        return TryCreateSpatialFromComponents("point", [unwrapped], out var pointFromScalar)
            ? pointFromScalar
            : GameEventScriptNothingValue.Instance;
    }

    private static bool TryCreateSpatialFromMembers(string typeName, GameEventScriptValue value, out GameEventScriptValue spatial)
    {
        var hasX = value.TryGetMapMember("x", out var x);
        var hasY = value.TryGetMapMember("y", out var y);
        var hasZ = value.TryGetMapMember("z", out var z);

        if (!hasX && !hasY && !hasZ)
        {
            spatial = GameEventScriptNothingValue.Instance;
            return false;
        }

        var components = new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal);
        if (hasX) components["x"] = x;
        if (hasY) components["y"] = y;
        if (hasZ) components["z"] = z;
        return TryCreateSpatialFromLabeledComponents(typeName, components, out spatial);
    }

    private static bool TryCoerceNumeric(GameEventScriptValue value, out double number, out bool isFinite)
    {
        number = default;
        isFinite = false;

        if (value.IsNaN())
        {
            return true;
        }

        if (value.IsInfinity() || value.IsNegativeInfinity())
        {
            return true;
        }

        switch (value.Kind)
        {
            case GameEventScriptBytecodeTypeKind.Integer:
            case GameEventScriptBytecodeTypeKind.Float when value.IsInteger():
                number = value.AsInteger();
                isFinite = true;
                return true;
            case GameEventScriptBytecodeTypeKind.Float:
                if (!value.IsNaN() && !value.IsInfinity())
                {
                    number = value.AsNumber();
                    isFinite = true;
                    return true;
                }

                return true;
            case GameEventScriptBytecodeTypeKind.Percentage:
                number = value.AsNumber();
                isFinite = true;
                return true;
            case GameEventScriptBytecodeTypeKind.Tag:
                if (double.TryParse(value.AsText(), out var parsed))
                {
                    number = parsed;
                    isFinite = true;
                }
                return true;
            case GameEventScriptBytecodeTypeKind.Dice:
                number = value.AsDice().Sum();
                isFinite = true;
                return true;
            case GameEventScriptBytecodeTypeKind.Boolean:
                number = value.AsBoolean() ? 1d : 0d;
                isFinite = true;
                return true;
            default:
                return false;
        }
    }

    private enum NumericKind
    {
        Finite,
        NaN,
        PositiveInfinity,
        NegativeInfinity
    }

    private readonly record struct NumericValue(NumericKind Kind, double Value)
    {
        public bool IsFinite => Kind == NumericKind.Finite;
        public bool IsNaN => Kind == NumericKind.NaN;
        public bool IsPositiveInfinity => Kind == NumericKind.PositiveInfinity;
        public bool IsNegativeInfinity => Kind == NumericKind.NegativeInfinity;
        public bool IsInfinity => IsPositiveInfinity || IsNegativeInfinity;

        public static NumericValue Finite(double value) => new(NumericKind.Finite, value);
        public static NumericValue NaN() => new(NumericKind.NaN, 0d);
        public static NumericValue PositiveInfinity() => new(NumericKind.PositiveInfinity, 0d);
        public static NumericValue NegativeInfinity() => new(NumericKind.NegativeInfinity, 0d);
    }

    private static bool TryCoerceNumericForOperation(GameEventScriptValue value, out NumericValue number)
    {
        if (value.IsNothing())
        {
            number = default;
            return false;
        }

        if (value is GameEventScriptTagValue && value.TryConvertToNumber(out var convertedTag))
        {
            return TryCoerceNumericForOperation(convertedTag, out number);
        }

        if (value.Kind is GameEventScriptBytecodeTypeKind.Integer or GameEventScriptBytecodeTypeKind.Float)
        {
            if (value.IsNaN())
            {
                number = NumericValue.NaN();
                return true;
            }

            if (value.IsInfinity())
            {
                number = value.IsNegativeInfinity()
                    ? NumericValue.NegativeInfinity()
                    : NumericValue.PositiveInfinity();
                return true;
            }

            number = NumericValue.Finite(value.AsNumber());
            return true;
        }

        if (value.Kind == GameEventScriptBytecodeTypeKind.Percentage)
        {
            number = NumericValue.Finite(value.AsNumber());
            return true;
        }

        if (value.Kind == GameEventScriptBytecodeTypeKind.Dice)
        {
            number = NumericValue.Finite(value.AsDice().Sum());
            return true;
        }

        if (value.Kind == GameEventScriptBytecodeTypeKind.Boolean)
        {
            number = NumericValue.Finite(value.AsBoolean() ? 1d : 0d);
            return true;
        }

        number = default;
        return false;
    }

    private static GameEventScriptValue ToGameEventScriptFloat(NumericValue number)
        => number.Kind switch
        {
            NumericKind.Finite => GameEventScriptValueFactory.GesFloat(number.Value),
            NumericKind.NaN => GameEventScriptValueFactory.GesFloatNaN(),
            NumericKind.PositiveInfinity => GameEventScriptValueFactory.GesFloatInfinity(),
            NumericKind.NegativeInfinity => GameEventScriptValueFactory.GesFloatNegativeInfinity(),
            _ => GameEventScriptValueFactory.GesFloatNaN()
        };

    private static GameEventScriptValue ToGameEventScriptNumber(NumericValue number)
        => TryToInteger(number, out var integer)
            ? GameEventScriptValueFactory.GesInteger(integer)
            : ToGameEventScriptFloat(number);

    private static GameEventScriptValue ToGameEventScriptNumericResult(
        GameEventScriptValue left,
        string operation,
        GameEventScriptValue right,
        NumericValue number)
    {
        return ToGameEventScriptNumber(number);
    }

    private static bool TryCompareNumeric(NumericValue left, NumericValue right, out int comparison)
    {
        if (left.IsNaN || right.IsNaN)
        {
            comparison = default;
            return false;
        }

        if (left.IsPositiveInfinity)
        {
            comparison = right.IsPositiveInfinity ? 0 : 1;
            return true;
        }

        if (left.IsNegativeInfinity)
        {
            comparison = right.IsNegativeInfinity ? 0 : -1;
            return true;
        }

        if (right.IsPositiveInfinity)
        {
            comparison = -1;
            return true;
        }

        if (right.IsNegativeInfinity)
        {
            comparison = 1;
            return true;
        }

        comparison = left.Value.CompareTo(right.Value);
        return true;
    }

    private static NumericValue AddNumeric(NumericValue left, NumericValue right)
    {
        if (left.IsNaN || right.IsNaN) return NumericValue.NaN();

        if (left.IsInfinity || right.IsInfinity)
        {
            if (left.IsPositiveInfinity && right.IsNegativeInfinity) return NumericValue.NaN();
            if (left.IsNegativeInfinity && right.IsPositiveInfinity) return NumericValue.NaN();
            if (left.IsPositiveInfinity || right.IsPositiveInfinity) return NumericValue.PositiveInfinity();
            return NumericValue.NegativeInfinity();
        }

        if (TryAddFinite(left.Value, right.Value, out var sum))
        {
            return NumericValue.Finite(sum);
        }

        if (left.Value > 0d && right.Value > 0d) return NumericValue.PositiveInfinity();
        if (left.Value < 0d && right.Value < 0d) return NumericValue.NegativeInfinity();
        return NumericValue.NaN();
    }

    private static NumericValue SubtractNumeric(NumericValue left, NumericValue right)
        => AddNumeric(left, NegateNumeric(right));

    private static NumericValue MultiplyNumeric(NumericValue left, NumericValue right)
    {
        if (left.IsNaN || right.IsNaN) return NumericValue.NaN();

        if ((left.IsInfinity && IsZero(right)) || (right.IsInfinity && IsZero(left)))
        {
            return NumericValue.NaN();
        }

        if (left.IsInfinity || right.IsInfinity)
        {
            return SignOf(left) * SignOf(right) >= 0
                ? NumericValue.PositiveInfinity()
                : NumericValue.NegativeInfinity();
        }

        if (TryMultiplyFinite(left.Value, right.Value, out var product))
        {
            return NumericValue.Finite(product);
        }

        return SignOf(left) * SignOf(right) >= 0
            ? NumericValue.PositiveInfinity()
            : NumericValue.NegativeInfinity();
    }

    private static NumericValue DivideNumeric(NumericValue left, NumericValue right)
    {
        if (left.IsNaN || right.IsNaN) return NumericValue.NaN();

        if (right.IsFinite && right.Value == 0d)
        {
            if (left.IsFinite && left.Value == 0d) return NumericValue.NaN();
            return SignOf(left) >= 0 ? NumericValue.PositiveInfinity() : NumericValue.NegativeInfinity();
        }

        if (left.IsInfinity && right.IsInfinity) return NumericValue.NaN();

        if (left.IsInfinity)
        {
            return SignOf(left) * SignOf(right) >= 0
                ? NumericValue.PositiveInfinity()
                : NumericValue.NegativeInfinity();
        }

        if (right.IsInfinity)
        {
            return NumericValue.Finite(0d);
        }

        if (TryDivideFinite(left.Value, right.Value, out var quotient))
        {
            return NumericValue.Finite(quotient);
        }

        return SignOf(left) * SignOf(right) >= 0
            ? NumericValue.PositiveInfinity()
            : NumericValue.NegativeInfinity();
    }

    private static NumericValue IntegerDivideNumeric(NumericValue left, NumericValue right)
    {
        var quotient = DivideNumeric(left, right);
        if (!quotient.IsFinite)
        {
            return quotient;
        }

        return NumericValue.Finite(Math.Floor(quotient.Value));
    }

    private static NumericValue ModuloNumeric(NumericValue left, NumericValue right)
    {
        if (left.IsNaN || right.IsNaN) return NumericValue.NaN();
        if (left.IsInfinity) return NumericValue.NaN();
        if (right.IsInfinity) return left.IsFinite ? NumericValue.Finite(left.Value) : NumericValue.NaN();
        if (right.Value == 0d) return NumericValue.NaN();

        if (TryModuloFinite(left.Value, right.Value, out var modulo))
        {
            return NumericValue.Finite(modulo);
        }

        return NumericValue.NaN();
    }

    private static NumericValue RemainderNumeric(NumericValue left, NumericValue right)
    {
        if (left.IsNaN || right.IsNaN) return NumericValue.NaN();
        if (left.IsInfinity) return NumericValue.NaN();
        if (right.IsInfinity) return left.IsFinite ? NumericValue.Finite(left.Value) : NumericValue.NaN();
        if (right.Value == 0d) return NumericValue.NaN();

        if (TryRemainderFinite(left.Value, right.Value, out var remainder))
        {
            return NumericValue.Finite(remainder);
        }

        return NumericValue.NaN();
    }


    private static NumericValue NegateNumeric(NumericValue value)
    {
        if (value.IsNaN) return NumericValue.NaN();
        if (value.IsPositiveInfinity) return NumericValue.NegativeInfinity();
        if (value.IsNegativeInfinity) return NumericValue.PositiveInfinity();

        if (TryNegateFinite(value.Value, out var negated))
        {
            return NumericValue.Finite(negated);
        }

        return value.Value < 0d ? NumericValue.PositiveInfinity() : NumericValue.NegativeInfinity();
    }

    private static int SignOf(NumericValue value)
    {
        if (value.IsPositiveInfinity) return 1;
        if (value.IsNegativeInfinity) return -1;
        if (!value.IsFinite) return 0;
        return value.Value.CompareTo(0d);
    }

    private static bool IsZero(NumericValue value) => value.IsFinite && value.Value == 0d;

    private static bool TryToInteger(NumericValue number, out long integer)
    {
        if (!number.IsFinite ||
            number.Value != Math.Truncate(number.Value) ||
            number.Value > long.MaxValue ||
            number.Value < long.MinValue)
        {
            integer = default;
            return false;
        }

        integer = (long)number.Value;
        return true;
    }

    private static bool TryAddFinite(double left, double right, out double value)
    {
        try
        {
            value = left + right;
            return true;
        }
        catch (OverflowException)
        {
            value = default;
            return false;
        }
    }

    private static bool TryMultiplyFinite(double left, double right, out double value)
    {
        try
        {
            value = left * right;
            return true;
        }
        catch (OverflowException)
        {
            value = default;
            return false;
        }
    }

    private static bool TryDivideFinite(double left, double right, out double value)
    {
        try
        {
            value = left / right;
            return true;
        }
        catch (OverflowException)
        {
            value = default;
            return false;
        }
    }

    private static bool TryModuloFinite(double left, double right, out double value)
    {
        try
        {
            var remainder = left % right;
            if (remainder != 0d &&
                (remainder < 0d && right > 0d || remainder > 0d && right < 0d))
            {
                remainder += right;
            }

            value = remainder;
            return true;
        }
        catch (Exception exception) when (exception is OverflowException or DivideByZeroException)
        {
            value = default;
            return false;
        }
    }

    private static bool TryNegateFinite(double input, out double value)
    {
        try
        {
            value = -input;
            return true;
        }
        catch (OverflowException)
        {
            value = default;
            return false;
        }
    }

    private static bool TryConvertValueToLiteral(GameEventScriptValue value, out ExpressionNode expression)
    {
        switch (value.Kind)
        {
            case GameEventScriptBytecodeTypeKind.Nothing:
                expression = new NothingLiteralExpressionNode();
                return true;
            case GameEventScriptBytecodeTypeKind.Boolean:
                expression = new BooleanLiteralExpressionNode(value.AsBoolean());
                return true;
            case GameEventScriptBytecodeTypeKind.Integer:
            case GameEventScriptBytecodeTypeKind.Float when value.IsInteger():
                expression = value is GameEventScriptNumberValue { Unit: var integerUnit } && integerUnit.IsNumericUnit()
                    ? new UnitIntegerLiteralExpressionNode(value.AsInteger(), integerUnit.ToTypeName())
                    : new IntegerLiteralExpressionNode(value.AsInteger());
                return true;
            case GameEventScriptBytecodeTypeKind.Float:
                if (value.IsNaN() || value.IsInfinity())
                {
                    expression = default!;
                    return false;
                }

                expression = value is GameEventScriptNumberValue { Unit: var unit } && unit.IsNumericUnit()
                    ? new UnitFloatLiteralExpressionNode(value.AsNumber(), unit.ToTypeName())
                    : new FloatLiteralExpressionNode(value.AsNumber());
                return true;
            case GameEventScriptBytecodeTypeKind.Percentage:
                expression = new PercentageLiteralExpressionNode(value.AsNumber() * 100d);
                return true;
            case GameEventScriptBytecodeTypeKind.Text:
                expression = new TextLiteralExpressionNode(value.AsText());
                return true;
            case GameEventScriptBytecodeTypeKind.Tag:
                expression = new TagLiteralExpressionNode(value.AsText());
                return true;
            case GameEventScriptBytecodeTypeKind.Vector:
            {
                var vector = (GameEventScriptVectorValue)value;
                expression = new TypeConstructorExpressionNode(
                    "vector",
                    new ArgumentListNode([
                        new ArgumentNode(null, CreateFloatLiteral(vector.X, vector.Unit)),
                        new ArgumentNode(null, CreateFloatLiteral(vector.Y, vector.Unit)),
                        new ArgumentNode(null, CreateFloatLiteral(vector.Z, vector.Unit))
                    ]));
                return true;
            }
            case GameEventScriptBytecodeTypeKind.Point:
            {
                var point = (GameEventScriptPointValue)value;
                expression = new TypeConstructorExpressionNode(
                    "point",
                    new ArgumentListNode([
                        new ArgumentNode(null, CreateFloatLiteral(point.X, point.Unit)),
                        new ArgumentNode(null, CreateFloatLiteral(point.Y, point.Unit)),
                        new ArgumentNode(null, CreateFloatLiteral(point.Z, point.Unit))
                    ]));
                return true;
            }
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

    private static bool TryConvertBoxedValueToLiteral(GameEventScriptBoxedValue value, out ExpressionNode expression)
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
                    if (!TryConvertBoxedValueToLiteral(item, out var itemLiteral))
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
                    if (!TryConvertBoxedValueToLiteral(entry.Value, out var itemLiteral))
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

    private static bool TryRemainderFinite(double left, double right, out double value)
    {
        try
        {
            value = left % right;
            return true;
        }
        catch (Exception exception) when (exception is OverflowException or DivideByZeroException)
        {
            value = default;
            return false;
        }
    }
}
