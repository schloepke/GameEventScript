#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.Flow.EventScript.Parser;
using StepH.Flow.EventScript.Runtime;
using StepH.Flow.EventScript.Types;

namespace StepH.Flow.EventScript.Linker;

internal static class EventScriptLinkOptimizer
{
    private static readonly ISet<string> EmptyTypeNames = new HashSet<string>(StringComparer.Ordinal);

    public static LinkedEventScriptModule Optimize(LinkedEventScriptModule module)
    {
        var knownTypeNames = new HashSet<string>(module.TypeDefinitions.Keys, StringComparer.Ordinal);

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

        return new LinkedEventScriptModule(
            optimizedTypes,
            optimizedCallables,
            optimizedHandlers,
            module.SourceCount);
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

    private static LinkedCallableDefinition OptimizeCallableDefinition(LinkedCallableDefinition definition, ISet<string> knownTypeNames)
    {
        var optimized = OptimizeExpression(definition.Expression, knownTypeNames);
        if (definition.Kind == LinkedCallableKind.Rule)
        {
            optimized = EnsureBooleanRuleExpression(optimized);
        }

        return new LinkedCallableDefinition(definition.Name, definition.ParameterList, optimized, definition.Kind, definition.SourceRange);
    }

    private static EventHandlerNode OptimizeHandler(EventHandlerNode handler, ISet<string> knownTypeNames)
        => handler with { Statements = handler.Statements.Select(statement => OptimizeStatement(statement, knownTypeNames)).ToArray() };

    private static StatementNode OptimizeStatement(StatementNode statement, ISet<string> knownTypeNames)
        => statement switch
        {
            PublishStatementNode publish => publish with
            {
                MessageExpression = OptimizeExpression(publish.MessageExpression, knownTypeNames)
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

    private static ExpressionNode EnsureBooleanRuleExpression(ExpressionNode expression)
    {
        if (expression is TypeCastExpressionNode { TypeName: "boolean" })
        {
            return expression;
        }

        return new TypeCastExpressionNode(expression, "boolean")
        {
            SourceRange = expression.SourceRange
        };
    }

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
            RulePredicateExpressionNode rulePredicate => rulePredicate with
            {
                Value = OptimizeExpression(rulePredicate.Value, knownTypeNames)
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
            SetLiteralExpressionNode set => set with
            {
                Items = set.Items.Select(item => OptimizeExpression(item, knownTypeNames)).ToArray()
            },
            DictionaryLiteralExpressionNode dictionary => dictionary with
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
            HandlerBindExpressionNode bind => bind with
            {
                CalleeExpression = OptimizeExpression(bind.CalleeExpression, knownTypeNames),
                ArgumentList = new ArgumentListNode(bind.Arguments.Select(argument => argument with
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
            SequenceLiteralExpressionNode sequence => sequence with
            {
                Items = sequence.Items.Select(item => OptimizeExpression(item, knownTypeNames)).ToArray()
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

        return optimized;
    }

    private static bool TryFoldConstantExpression(ExpressionNode expression, out ExpressionNode folded)
    {
        folded = expression;
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
            DictionarySelectorNode dictionarySelector => dictionarySelector with
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

    private static bool TryEvaluateConstant(ExpressionNode expression, out EventScriptValue value)
    {
        switch (expression)
        {
            case BooleanLiteralExpressionNode booleanLiteral:
                value = EventScriptValueFactory.Boolean(booleanLiteral.Value);
                return true;
            case IntegerLiteralExpressionNode integerLiteral:
                value = EventScriptValueFactory.Integer(integerLiteral.Value);
                return true;
            case DecimalLiteralExpressionNode decimalLiteral:
                value = EventScriptValueFactory.Decimal(decimalLiteral.Value);
                return true;
            case PercentageLiteralExpressionNode percentageLiteral:
                value = EventScriptValueFactory.Percentage(percentageLiteral.PercentValue / 100m);
                return true;
            case UnitDecimalLiteralExpressionNode unitDecimalLiteral:
                value = EventScriptDecimalUnits.TryParseTypeName(unitDecimalLiteral.UnitName, out var unit)
                    ? EventScriptValueFactory.Decimal(unitDecimalLiteral.Value, unit)
                    : EventScriptValueFactory.DecimalNaN();
                return true;
            case TextLiteralExpressionNode textLiteral:
                value = EventScriptValueFactory.Text(textLiteral.Value);
                return true;
            case TagLiteralExpressionNode tagLiteral:
                value = EventScriptValueFactory.Tag(tagLiteral.Name);
                return true;
            case ListLiteralExpressionNode listLiteral:
            {
                var items = new List<EventScriptValue>(listLiteral.Items.Count);
                foreach (var itemExpression in listLiteral.Items)
                {
                    if (!TryEvaluateConstant(itemExpression, out var item))
                    {
                        value = EventScriptValue.Nothing;
                        return false;
                    }

                    items.Add(item);
                }

                value = EventScriptValueFactory.List(items);
                return true;
            }
            case SetLiteralExpressionNode setLiteral:
            {
                var items = new List<EventScriptValue>(setLiteral.Items.Count);
                foreach (var itemExpression in setLiteral.Items)
                {
                    if (!TryEvaluateConstant(itemExpression, out var item))
                    {
                        value = EventScriptValue.Nothing;
                        return false;
                    }

                    items.Add(item);
                }

                value = EventScriptValueFactory.Set(items);
                return true;
            }
            case DictionaryLiteralExpressionNode dictionaryLiteral:
            {
                var items = new Dictionary<string, EventScriptValue>(StringComparer.Ordinal);
                foreach (var entry in dictionaryLiteral.Entries)
                {
                    if (!TryEvaluateConstant(entry.Value, out var item))
                    {
                        value = EventScriptValue.Nothing;
                        return false;
                    }

                    items[entry.Key] = item;
                }

                value = EventScriptValueFactory.Dictionary(items);
                return true;
            }
            case TypeCastExpressionNode castExpression:
            {
                if (!TryEvaluateConstant(castExpression.Value, out var source))
                {
                    value = EventScriptValue.Nothing;
                    return false;
                }

                if (!TryConvertConstantType(source, castExpression.TypeName, EmptyTypeNames, out value))
                {
                    return false;
                }

                return true;
            }
            case UnaryExpressionNode unaryExpression:
                return TryEvaluateConstantUnary(unaryExpression, out value);
            case BinaryExpressionNode binaryExpression:
                return TryEvaluateConstantBinary(binaryExpression, out value);
            default:
                value = EventScriptValue.Nothing;
                return false;
        }
    }

    private static bool TryEvaluateConstantUnary(UnaryExpressionNode unary, out EventScriptValue value)
    {
        if (!TryEvaluateConstant(unary.Operand, out var operand))
        {
            value = EventScriptValue.Nothing;
            return false;
        }

        switch (unary.Operator)
        {
            case "-":
                if (operand.IsNothing())
                {
                    value = EventScriptValue.Nothing;
                    return true;
                }

                if (!TryUnwrapOptional(operand, out var unwrapped))
                {
                    value = EventScriptValueFactory.OptionalNone();
                    return true;
                }

                if (unwrapped.IsPercentage())
                {
                    value = EventScriptValueFactory.Percentage(-unwrapped.AsNumber());
                    return true;
                }

                if (EventScriptValue.TryGetDecimalUnit(unwrapped, out var unit))
                {
                    value = EventScriptValueFactory.Decimal(-unwrapped.AsNumber(), unit);
                    return true;
                }

                if (!TryCoerceNumericForOperation(unwrapped, out var numeric))
                {
                    value = EventScriptValue.Nothing;
                    return true;
                }

                value = ToEventScriptDecimal(NegateNumeric(numeric));
                return true;
            case "!":
                if (operand.IsNothing())
                {
                    value = EventScriptValue.Nothing;
                    return true;
                }

                if (!TryUnwrapOptional(operand, out var unwrappedBool))
                {
                    value = EventScriptValueFactory.OptionalNone();
                    return true;
                }

                value = EventScriptValueFactory.Boolean(!unwrappedBool.AsBoolean());
                return true;
            case "has value":
                value = EventScriptValueFactory.Boolean(operand.HasSemanticValue());
                return true;
            case "empty":
                value = EventScriptValueFactory.Boolean(operand.IsSemanticallyEmpty());
                return true;
            case "wrapDegree":
                value = EventScriptValueAlu.EvaluateWrapDegree(operand);
                return true;
            default:
                value = EventScriptValue.Nothing;
                return false;
        }
    }

    private static bool TryEvaluateConstantBinary(BinaryExpressionNode binary, out EventScriptValue value)
    {
        if (!TryEvaluateConstant(binary.Left, out var leftRaw) || !TryEvaluateConstant(binary.Right, out var rightRaw))
        {
            value = EventScriptValue.Nothing;
            return false;
        }

        if (binary.Operator == "default")
        {
            if (!leftRaw.HasSemanticValue())
            {
                value = rightRaw;
                return true;
            }

            if (leftRaw.IsOptional())
            {
                var optional = leftRaw.AsOptional();
                value = optional.HasValue ? optional.Value : rightRaw;
                return true;
            }

            value = leftRaw;
            return true;
        }

        if (leftRaw.IsNothing() || rightRaw.IsNothing())
        {
            value = EventScriptValue.Nothing;
            return true;
        }

        if (!TryUnwrapOptional(leftRaw, out var left) || !TryUnwrapOptional(rightRaw, out var right))
        {
            value = EventScriptValueFactory.OptionalNone();
            return true;
        }

        switch (binary.Operator)
        {
            case "|":
                value = EventScriptValueFactory.Boolean(left.AsBoolean() || right.AsBoolean());
                return true;
            case "^":
                value = EventScriptValueFactory.Boolean(left.AsBoolean() ^ right.AsBoolean());
                return true;
            case "&":
                value = EventScriptValueFactory.Boolean(left.AsBoolean() && right.AsBoolean());
                return true;
            case "=":
                value = EventScriptValueFactory.Boolean(left.Equals(right));
                return true;
            case "<>":
                value = EventScriptValueFactory.Boolean(!left.Equals(right));
                return true;
            case "<":
                value = EvaluateNumericComparison(left, right, comparison => comparison < 0);
                return true;
            case ">":
                value = EvaluateNumericComparison(left, right, comparison => comparison > 0);
                return true;
            case "<=":
                value = EvaluateNumericComparison(left, right, comparison => comparison <= 0);
                return true;
            case ">=":
                value = EvaluateNumericComparison(left, right, comparison => comparison >= 0);
                return true;
            case "+":
                if (EventScriptValueAlu.TryEvaluatePercentageBinary(left, "+", right, out value))
                {
                    return true;
                }

                if (EventScriptValueAlu.TryEvaluateUnitBinary(left, "+", right, out value))
                {
                    return true;
                }

                if (!TryCoerceNumericForOperation(left, out var leftNumeric) ||
                    !TryCoerceNumericForOperation(right, out var rightNumeric))
                {
                    value = EventScriptValue.Nothing;
                    return false;
                }

                value = ToEventScriptNumericResult(left, "+", right, AddNumeric(leftNumeric, rightNumeric));
                return true;
            case "-":
                if (EventScriptValueAlu.TryEvaluatePercentageBinary(left, "-", right, out value))
                {
                    return true;
                }

                if (EventScriptValueAlu.TryEvaluateUnitBinary(left, "-", right, out value))
                {
                    return true;
                }

                if (!TryCoerceNumericForOperation(left, out var leftMinus) ||
                    !TryCoerceNumericForOperation(right, out var rightMinus))
                {
                    value = EventScriptValue.Nothing;
                    return false;
                }

                value = ToEventScriptNumericResult(left, "-", right, SubtractNumeric(leftMinus, rightMinus));
                return true;
            case "*":
                if (EventScriptValueAlu.TryEvaluatePercentageBinary(left, "*", right, out value))
                {
                    return true;
                }

                if (EventScriptValueAlu.TryEvaluateUnitBinary(left, "*", right, out value))
                {
                    return true;
                }

                if (!TryCoerceNumericForOperation(left, out var leftMultiply) ||
                    !TryCoerceNumericForOperation(right, out var rightMultiply))
                {
                    value = EventScriptValue.Nothing;
                    return false;
                }

                value = ToEventScriptNumericResult(left, "*", right, MultiplyNumeric(leftMultiply, rightMultiply));
                return true;
            case "/":
                if (EventScriptValueAlu.TryEvaluatePercentageBinary(left, "/", right, out value))
                {
                    return true;
                }

                if (EventScriptValueAlu.TryEvaluateUnitBinary(left, "/", right, out value))
                {
                    return true;
                }

                if (!TryCoerceNumericForOperation(left, out var leftDivide) ||
                    !TryCoerceNumericForOperation(right, out var rightDivide))
                {
                    value = EventScriptValue.Nothing;
                    return false;
                }

                value = ToEventScriptDecimal(DivideNumeric(leftDivide, rightDivide));
                return true;
            case "mod":
                if (EventScriptValueAlu.TryEvaluateUnitBinary(left, "mod", right, out value))
                {
                    return true;
                }

                if (!TryCoerceNumericForOperation(left, out var leftModulo) ||
                    !TryCoerceNumericForOperation(right, out var rightModulo))
                {
                    value = EventScriptValue.Nothing;
                    return false;
                }

                value = ToEventScriptNumericResult(left, "mod", right, ModuloNumeric(leftModulo, rightModulo));
                return true;
            case "div":
                if (EventScriptValueAlu.TryEvaluateUnitBinary(left, "div", right, out value))
                {
                    return true;
                }

                if (!TryCoerceNumericForOperation(left, out var leftIntegerDivide) ||
                    !TryCoerceNumericForOperation(right, out var rightIntegerDivide))
                {
                    value = EventScriptValue.Nothing;
                    return false;
                }

                value = ToEventScriptNumericResult(left, "div", right, IntegerDivideNumeric(leftIntegerDivide, rightIntegerDivide));
                return true;
            case "rem":
                if (EventScriptValueAlu.TryEvaluateUnitBinary(left, "rem", right, out value))
                {
                    return true;
                }

                if (!TryCoerceNumericForOperation(left, out var leftRemainder) ||
                    !TryCoerceNumericForOperation(right, out var rightRemainder))
                {
                    value = EventScriptValue.Nothing;
                    return false;
                }

                value = ToEventScriptNumericResult(left, "rem", right, RemainderNumeric(leftRemainder, rightRemainder));
                return true;
            default:
                value = EventScriptValue.Nothing;
                return false;
        }
    }

    private static EventScriptValue EvaluateNumericComparison(EventScriptValue left, EventScriptValue right, Func<int, bool> predicate)
    {
        if (!EventScriptValueAlu.TryCompareNumericValues(left, right, out var comparison))
        {
            return EventScriptValueFactory.Boolean(false);
        }

        return EventScriptValueFactory.Boolean(predicate(comparison));
    }

    private static bool TryConvertConstantType(EventScriptValue value, string declaredType, ISet<string> knownTypeNames, out EventScriptValue converted)
    {
        switch (declaredType)
        {
            case "nothing":
                converted = EventScriptValue.Nothing;
                return true;
            case "tag":
                converted = EventScriptValueFactory.Tag(value.AsText());
                return true;
            case "text":
                converted = EventScriptValueFactory.Text(value.AsText());
                return true;
            case "percentage":
                converted = ConvertToPercentage(value);
                return true;
            case "degree":
                converted = ConvertToDecimalUnit(value, EventScriptDecimalUnit.Degree);
                return true;
            case "meter":
                converted = ConvertToDecimalUnit(value, EventScriptDecimalUnit.Meter);
                return true;
            case "second":
                converted = ConvertToDecimalUnit(value, EventScriptDecimalUnit.Second);
                return true;
            case "vector2":
                converted = ConvertToVector2(value);
                return true;
            case "vector3":
                converted = ConvertToVector3(value);
                return true;
            case "boolean":
                converted = EventScriptValueFactory.Boolean(value.AsBoolean());
                return true;
            case "integer":
                converted = EventScriptValueFactory.Integer(value.AsInteger());
                return true;
            case "decimal":
                converted = ConvertToDecimal(value);
                return true;
            case "list":
                converted = EventScriptValueFactory.List(value.AsList());
                return true;
            case "range":
                converted = value.IsRange() ? value : EventScriptValue.Nothing;
                return true;
            case "message":
                converted = value.Kind == EventScriptValueKind.Message ? value : EventScriptValue.Nothing;
                return true;
            case "handler":
                converted = value.Kind == EventScriptValueKind.Handler ? value : EventScriptValue.Nothing;
                return true;
            case "dictionary":
                converted = EventScriptValueFactory.Dictionary(value.AsDictionary());
                return true;
            case "set":
                converted = EventScriptValueFactory.Set(value.AsSet());
                return true;
            case "dice":
                converted = EventScriptValueFactory.Dice(value.AsDice());
                return true;
            case "optional":
                converted = value.IsOptional()
                    ? value
                    : value.IsNothing() ? EventScriptValueFactory.OptionalNone() : EventScriptValueFactory.OptionalSome(value);
                return true;
            default:
                if (knownTypeNames.Contains(declaredType))
                {
                    converted = EventScriptValue.Nothing;
                    return false;
                }

                converted = value;
                return true;
        }
    }

    private static EventScriptValue ConvertToDecimal(EventScriptValue value)
    {
        if (!TryUnwrapOptional(value, out var unwrapped))
        {
            return EventScriptValueFactory.DecimalNaN();
        }

        if (!TryCoerceNumeric(unwrapped, out var number, out var isFinite))
        {
            return EventScriptValueFactory.DecimalNaN();
        }

        if (isFinite)
        {
            return EventScriptValueFactory.Decimal(number);
        }

        if (unwrapped.IsNaN())
        {
            return EventScriptValueFactory.DecimalNaN();
        }

        return unwrapped.IsNegativeInfinity()
            ? EventScriptValueFactory.DecimalNegativeInfinity()
            : EventScriptValueFactory.DecimalInfinity();
    }

    private static EventScriptValue ConvertToPercentage(EventScriptValue value)
    {
        if (!TryUnwrapOptional(value, out var unwrapped))
        {
            return EventScriptValueFactory.DecimalNaN();
        }

        if (unwrapped.IsPercentage())
        {
            return unwrapped;
        }

        if (unwrapped.HasDecimalUnit())
        {
            return EventScriptValueFactory.DecimalNaN();
        }

        if (!TryCoerceNumeric(unwrapped, out var number, out var isFinite) || !isFinite)
        {
            return EventScriptValueFactory.DecimalNaN();
        }

        var ratio = unwrapped.Kind == EventScriptValueKind.Integer
            ? number / 100m
            : number > 1m || number < -1m
                ? number / 100m
                : number;
        return EventScriptValueFactory.Percentage(ratio);
    }

    private static EventScriptValue ConvertToDecimalUnit(EventScriptValue value, EventScriptDecimalUnit unit)
    {
        if (!TryUnwrapOptional(value, out var unwrapped))
        {
            return EventScriptValueFactory.DecimalNaN();
        }

        if (unwrapped.Kind is EventScriptValueKind.Decimal or EventScriptValueKind.Integer &&
            TryCoerceNumeric(unwrapped, out var number, out var isFinite) &&
            isFinite)
        {
            return EventScriptValueFactory.Decimal(number, unit);
        }

        return EventScriptValueFactory.DecimalNaN();
    }

    private static EventScriptValue ConvertToVector2(EventScriptValue value)
    {
        if (!TryUnwrapOptional(value, out var unwrapped))
        {
            return EventScriptValue.Nothing;
        }

        if (unwrapped is EventScriptVector2Value vector2)
        {
            return vector2;
        }

        if (unwrapped is EventScriptVector3Value vector3)
        {
            return EventScriptValueFactory.Vector2(vector3.X, vector3.Y);
        }

        if (TryReadVectorComponent(unwrapped, "x", out var x) &&
            TryReadVectorComponent(unwrapped, "y", out var y))
        {
            return EventScriptValueFactory.Vector2(x, y);
        }

        var items = unwrapped.AsList();
        if (items.Count >= 2 &&
            TryReadVectorComponent(items[0], out x) &&
            TryReadVectorComponent(items[1], out y))
        {
            return EventScriptValueFactory.Vector2(x, y);
        }

        return EventScriptValue.Nothing;
    }

    private static EventScriptValue ConvertToVector3(EventScriptValue value)
    {
        if (!TryUnwrapOptional(value, out var unwrapped))
        {
            return EventScriptValue.Nothing;
        }

        if (unwrapped is EventScriptVector3Value vector3)
        {
            return vector3;
        }

        if (unwrapped is EventScriptVector2Value vector2)
        {
            return EventScriptValueFactory.Vector3(vector2.X, vector2.Y, 0m);
        }

        if (TryReadVectorComponent(unwrapped, "x", out var x) &&
            TryReadVectorComponent(unwrapped, "y", out var y))
        {
            var z = TryReadVectorComponent(unwrapped, "z", out var zValue) ? zValue : 0m;
            return EventScriptValueFactory.Vector3(x, y, z);
        }

        var items = unwrapped.AsList();
        if (items.Count >= 2 &&
            TryReadVectorComponent(items[0], out x) &&
            TryReadVectorComponent(items[1], out y))
        {
            var z = items.Count >= 3 && TryReadVectorComponent(items[2], out var zValue) ? zValue : 0m;
            return EventScriptValueFactory.Vector3(x, y, z);
        }

        return EventScriptValue.Nothing;
    }

    private static bool TryReadVectorComponent(EventScriptValue source, string key, out decimal value)
    {
        if (source.TryGetDictionaryMember(key, out var component) &&
            TryReadVectorComponent(component, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryReadVectorComponent(EventScriptValue component, out decimal value)
    {
        if (!TryUnwrapOptional(component, out var unwrapped) ||
            !TryCoerceNumeric(unwrapped, out var number, out var isFinite) ||
            !isFinite)
        {
            value = default;
            return false;
        }

        value = number;
        return true;
    }

    private static bool TryUnwrapOptional(EventScriptValue value, out EventScriptValue unwrapped)
    {
        if (!value.IsOptional())
        {
            unwrapped = value;
            return true;
        }

        var optional = value.AsOptional();
        if (!optional.HasValue)
        {
            unwrapped = default!;
            return false;
        }

        unwrapped = optional.Value;
        return true;
    }

    private static bool TryCoerceNumeric(EventScriptValue value, out decimal number, out bool isFinite)
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
            case EventScriptValueKind.Integer:
                number = value.AsInteger();
                isFinite = true;
                return true;
            case EventScriptValueKind.Decimal:
                if (!value.IsNaN() && !value.IsInfinity())
                {
                    number = value.AsNumber();
                    isFinite = true;
                    return true;
                }

                return true;
            case EventScriptValueKind.Percentage:
                number = value.AsNumber();
                isFinite = true;
                return true;
            case EventScriptValueKind.Text:
            case EventScriptValueKind.Tag:
                if (decimal.TryParse(value.AsText(), out var parsed))
                {
                    number = parsed;
                    isFinite = true;
                }
                return true;
            case EventScriptValueKind.Boolean:
                number = value.AsBoolean() ? 1m : 0m;
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

    private readonly record struct NumericValue(NumericKind Kind, decimal Value)
    {
        public bool IsFinite => Kind == NumericKind.Finite;
        public bool IsNaN => Kind == NumericKind.NaN;
        public bool IsPositiveInfinity => Kind == NumericKind.PositiveInfinity;
        public bool IsNegativeInfinity => Kind == NumericKind.NegativeInfinity;
        public bool IsInfinity => IsPositiveInfinity || IsNegativeInfinity;

        public static NumericValue Finite(decimal value) => new(NumericKind.Finite, value);
        public static NumericValue NaN() => new(NumericKind.NaN, 0m);
        public static NumericValue PositiveInfinity() => new(NumericKind.PositiveInfinity, 0m);
        public static NumericValue NegativeInfinity() => new(NumericKind.NegativeInfinity, 0m);
    }

    private static bool TryCoerceNumericForOperation(EventScriptValue value, out NumericValue number)
    {
        if (value.IsNothing())
        {
            number = default;
            return false;
        }

        if (value.Kind == EventScriptValueKind.Decimal)
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

        if (value.Kind == EventScriptValueKind.Integer)
        {
            number = NumericValue.Finite(value.AsInteger());
            return true;
        }

        if (value.Kind == EventScriptValueKind.Percentage)
        {
            number = NumericValue.Finite(value.AsNumber());
            return true;
        }

        if (value.IsText())
        {
            if (decimal.TryParse(value.AsText(), out var parsed))
            {
                number = NumericValue.Finite(parsed);
                return true;
            }

            number = default;
            return false;
        }

        if (value.Kind == EventScriptValueKind.Boolean)
        {
            number = NumericValue.Finite(value.AsBoolean() ? 1m : 0m);
            return true;
        }

        number = default;
        return false;
    }

    private static EventScriptValue ToEventScriptDecimal(NumericValue number)
        => number.Kind switch
        {
            NumericKind.Finite => EventScriptValueFactory.Decimal(number.Value),
            NumericKind.NaN => EventScriptValueFactory.DecimalNaN(),
            NumericKind.PositiveInfinity => EventScriptValueFactory.DecimalInfinity(),
            NumericKind.NegativeInfinity => EventScriptValueFactory.DecimalNegativeInfinity(),
            _ => EventScriptValueFactory.DecimalNaN()
        };

    private static EventScriptValue ToEventScriptNumericResult(
        EventScriptValue left,
        string operation,
        EventScriptValue right,
        NumericValue number)
    {
        if (operation == "div" &&
            TryToInteger(number, out var quotient))
        {
            return EventScriptValueFactory.Integer(quotient);
        }

        if (operation is "+" or "-" or "*" or "mod" or "rem" &&
            left.Kind == EventScriptValueKind.Integer &&
            right.Kind == EventScriptValueKind.Integer &&
            TryToInteger(number, out var integer))
        {
            return EventScriptValueFactory.Integer(integer);
        }

        return ToEventScriptDecimal(number);
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

        if (left.Value > 0m && right.Value > 0m) return NumericValue.PositiveInfinity();
        if (left.Value < 0m && right.Value < 0m) return NumericValue.NegativeInfinity();
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

        if (right.IsFinite && right.Value == 0m)
        {
            if (left.IsFinite && left.Value == 0m) return NumericValue.NaN();
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
            return NumericValue.Finite(0m);
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
        if (right.Value == 0m) return NumericValue.NaN();

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
        if (right.Value == 0m) return NumericValue.NaN();

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

        return value.Value < 0m ? NumericValue.PositiveInfinity() : NumericValue.NegativeInfinity();
    }

    private static int SignOf(NumericValue value)
    {
        if (value.IsPositiveInfinity) return 1;
        if (value.IsNegativeInfinity) return -1;
        if (!value.IsFinite) return 0;
        return value.Value.CompareTo(0m);
    }

    private static bool IsZero(NumericValue value) => value.IsFinite && value.Value == 0m;

    private static bool TryToInteger(NumericValue number, out long integer)
    {
        if (!number.IsFinite ||
            number.Value != decimal.Truncate(number.Value) ||
            number.Value > long.MaxValue ||
            number.Value < long.MinValue)
        {
            integer = default;
            return false;
        }

        integer = (long)number.Value;
        return true;
    }

    private static bool TryAddFinite(decimal left, decimal right, out decimal value)
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

    private static bool TryMultiplyFinite(decimal left, decimal right, out decimal value)
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

    private static bool TryDivideFinite(decimal left, decimal right, out decimal value)
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

    private static bool TryModuloFinite(decimal left, decimal right, out decimal value)
    {
        try
        {
            var remainder = left % right;
            if (remainder != 0m &&
                (remainder < 0m && right > 0m || remainder > 0m && right < 0m))
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

    private static bool TryNegateFinite(decimal input, out decimal value)
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

    private static bool TryConvertValueToLiteral(EventScriptValue value, out ExpressionNode expression)
    {
        switch (value.Kind)
        {
            case EventScriptValueKind.Boolean:
                expression = new BooleanLiteralExpressionNode(value.AsBoolean());
                return true;
            case EventScriptValueKind.Integer:
                expression = new IntegerLiteralExpressionNode(value.AsInteger());
                return true;
            case EventScriptValueKind.Decimal:
                if (value.IsNaN() || value.IsInfinity())
                {
                    expression = default!;
                    return false;
                }

                expression = value is EventScriptDecimalValue { Unit: { } unit }
                    ? new UnitDecimalLiteralExpressionNode(value.AsNumber(), EventScriptDecimalUnits.ToTypeName(unit))
                    : new DecimalLiteralExpressionNode(value.AsNumber());
                return true;
            case EventScriptValueKind.Percentage:
                expression = new PercentageLiteralExpressionNode(value.AsNumber() * 100m);
                return true;
            case EventScriptValueKind.Text:
                expression = new TextLiteralExpressionNode(value.AsText());
                return true;
            case EventScriptValueKind.Tag:
                expression = new TagLiteralExpressionNode(value.AsText());
                return true;
            case EventScriptValueKind.List:
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
            case EventScriptValueKind.Set:
            {
                var items = new List<ExpressionNode>();
                foreach (var item in value.AsSet())
                {
                    if (!TryConvertValueToLiteral(item, out var itemLiteral))
                    {
                        expression = default!;
                        return false;
                    }

                    items.Add(itemLiteral);
                }

                expression = new SetLiteralExpressionNode(items);
                return true;
            }
            case EventScriptValueKind.Dictionary:
            {
                var entries = new List<DictionaryEntryNode>();
                foreach (var entry in value.AsDictionary())
                {
                    if (!TryConvertValueToLiteral(entry.Value, out var itemLiteral))
                    {
                        expression = default!;
                        return false;
                    }

                    entries.Add(new DictionaryEntryNode(entry.Key, itemLiteral));
                }

                expression = new DictionaryLiteralExpressionNode(entries);
                return true;
            }
            default:
                expression = default!;
            return false;
        }
    }

    private static bool TryRemainderFinite(decimal left, decimal right, out decimal value)
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
