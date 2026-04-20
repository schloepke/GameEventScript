#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StepH.Flow.EventScript.Linker;
using StepH.Flow.EventScript.Parser;
using StepH.Flow.EventScript.Semantics;
using StepH.Flow.EventScript.Types;

namespace StepH.Flow.EventScript.Interpreter;

internal sealed class EventScriptInvocationEngine
{
    private const int RandomUnitMax = 1_000_000;
    private const decimal RandomUnitScale = RandomUnitMax;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<CompiledEventScriptHandler>> _handlers;
    private readonly IReadOnlyDictionary<string, TypeDefinitionNode> _typeDefinitions;
    private readonly IReadOnlyDictionary<string, RuleDefinitionNode> _ruleDefinitions;
    private readonly IReadOnlyDictionary<string, SelectDefinitionNode> _selectDefinitions;
    private readonly IEventScriptRandom _random;

    private EventScriptInvocationEngine(CompiledEventScript compiledScript, IEventScriptRandom? random = null)
    {
        _ = compiledScript ?? throw new ArgumentNullException(nameof(compiledScript));
        _random = random ?? new DefaultEventScriptRandom();
        _typeDefinitions = compiledScript.LinkedModule.TypeDefinitions;
        _ruleDefinitions = compiledScript.LinkedModule.RuleDefinitions;
        _selectDefinitions = compiledScript.LinkedModule.SelectDefinitions;
        foreach (var ruleName in _ruleDefinitions.Keys)
        {
            if (_selectDefinitions.ContainsKey(ruleName))
            {
                throw new EventScriptCompilationException($"Global definition '{ruleName}' is defined as both rule and select");
            }
        }

        _handlers = compiledScript.Handlers;
    }

    public static EventScriptInvocationEngine Compile(string script, IEventScriptRandom? random = null) => Compile(EventScriptLinkBuilder.LinkScripts(script), random);

    public static EventScriptInvocationEngine Compile(EventScriptModule eventScriptModule, IEventScriptRandom? random = null)
    {
        _ = eventScriptModule ?? throw new ArgumentNullException(nameof(eventScriptModule));
        return Compile(new EventScriptLinkBuilder().AddModule(eventScriptModule).Link(), random);
    }

    public static EventScriptInvocationEngine Compile(LinkedEventScriptModule linkedModule, IEventScriptRandom? random = null)
    {
        _ = linkedModule ?? throw new ArgumentNullException(nameof(linkedModule));
        return Compile(new CompiledEventScript(linkedModule), random);
    }

    public static EventScriptInvocationEngine Compile(CompiledEventScript compiledScript, IEventScriptRandom? random = null)
    {
        _ = compiledScript ?? throw new ArgumentNullException(nameof(compiledScript));
        return new EventScriptInvocationEngine(compiledScript, random);
    }

    public EventScriptDiagnosticInvocationResult Invoke(string message, IReadOnlyDictionary<string, EventScriptValue> args)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message must not be null or whitespace", nameof(message));
        }

        args = EventScriptArgumentMap.Normalize(args);
        var state = new RunState(diagnosticsEnabled: true);
        state.RecordDiagnostic(EventScriptDiagnosticStepKind.InvocationStarted, message, args, $"Invoke '{message}'");

        foreach (var handler in GetMatchingHandlers(message, args))
        {
            state.RecordDiagnostic(
                EventScriptDiagnosticStepKind.HandlerMatched,
                handler.Message,
                args,
                $"Handler '{handler.Message}' with parameters ({string.Join(", ", handler.Parameters)})");
            var context = new ExecutionContext(state);
            var handlerVariables = ExecuteHandler(context, handler.Syntax, args);
            state.CaptureVariables(handlerVariables);
        }

        state.RecordDiagnostic(
            EventScriptDiagnosticStepKind.InvocationCompleted,
            message,
            args,
            $"Completed with {state.EmittedEvents.Count} published event(s)");

        return new EventScriptDiagnosticInvocationResult(
            message,
            EventScriptNamedArguments.Create(args),
            state.EmittedEvents,
            state.Variables,
            state.Steps);
    }

    public EventScriptExecutionResult InvokeMessage(string message, IReadOnlyDictionary<string, EventScriptValue> args)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message must not be null or whitespace", nameof(message));
        }

        args = EventScriptArgumentMap.Normalize(args);
        var state = new RunState();
        foreach (var handler in GetMatchingHandlers(message, args))
        {
            var context = new ExecutionContext(state);
            var handlerVariables = ExecuteHandler(context, handler.Syntax, args);
            state.CaptureVariables(handlerVariables);
        }

        return new EventScriptExecutionResult(message, state.EmittedEvents, state.Variables);
    }

    public EventScriptExecutionResult InvokeHandler(CompiledEventScriptHandler handler, IReadOnlyDictionary<string, EventScriptValue> args)
    {
        _ = handler ?? throw new ArgumentNullException(nameof(handler));
        args = EventScriptArgumentMap.Normalize(args);
        var state = new RunState();
        var context = new ExecutionContext(state);
        var variables = ExecuteHandler(context, handler.Syntax, args);
        state.CaptureVariables(variables);
        return new EventScriptExecutionResult(handler.Message, state.EmittedEvents, state.Variables);
    }

    private IReadOnlyList<CompiledEventScriptHandler> GetMatchingHandlers(string message, IReadOnlyDictionary<string, EventScriptValue> args)
    {
        if (!_handlers.TryGetValue(message, out var handlers))
        {
            return [];
        }

        var signatureKey = EventScriptArgumentMap.CreateSignatureKey(args.Keys);
        return handlers
            .Where(handler => handler.SignatureKey == signatureKey)
            .OrderBy(handler => handler.DeclarationOrder)
            .ToArray();
    }

    private IReadOnlyDictionary<string, EventScriptValue> ExecuteHandler(ExecutionContext context, EventHandlerNode handler, IReadOnlyDictionary<string, EventScriptValue> args)
    {
        context.PushScope();
        try
        {
            foreach (var parameter in handler.Parameters)
            {
                context.Define(parameter, args[parameter]);
            }

            ExecuteStatements(context, handler.Statements);
            return context.SnapshotTopScope();
        }
        finally
        {
            context.PopScope();
        }
    }

    private void ExecuteStatements(ExecutionContext context, IReadOnlyList<StatementNode> statements)
    {
        foreach (var statement in statements)
        {
            ExecuteStatement(context, statement);
        }
    }

    private void ExecuteStatement(ExecutionContext context, StatementNode statement)
    {
        context.RecordStatement(statement);

        switch (statement)
        {
            case PublishStatementNode publish:
                var args = publish.Arguments.ToDictionary(
                    argument => argument.Name,
                    argument => EvaluateExpression(context, argument.Expression),
                    StringComparer.Ordinal);
                EmitMessage(context, publish.Message, args);
                return;

            case LetStatementNode let:
                var letValue = EvaluateExpression(context, let.Expression);
                if (!string.IsNullOrEmpty(let.DeclaredType))
                {
                    letValue = ConvertToDeclaredType(letValue, let.DeclaredType!);
                }

                context.Define(let.Identifier, letValue);
                return;

            case IfStatementNode ifStatement:
                ExecuteStatements(context, AsBool(EvaluateExpression(context, ifStatement.Condition)) ? ifStatement.ThenStatements : ifStatement.ElseStatements);
                return;

            case ForStatementNode forStatement:
                foreach (var item in EvaluateExpression(context, forStatement.Source).AsEnumerable())
                {
                    context.PushScope();
                    try
                    {
                        context.Define(forStatement.Identifier, item);
                        ExecuteStatements(context, forStatement.Statements);
                    }
                    finally
                    {
                        context.PopScope();
                    }
                }

                return;

            case ExpressionStatementNode expressionStatement:
                EvaluateExpression(context, expressionStatement.Expression);
                return;

            default:
                return;
        }
    }

    private void EmitMessage(ExecutionContext context, string message, IReadOnlyDictionary<string, EventScriptValue> args)
    {
        ValidateEmitArguments(message, args.Keys);
        context.Publish(message, args);
    }

    private EventScriptValue EvaluateExpression(ExecutionContext context, ExpressionNode expression)
    {
        var value = EvaluateExpressionCore(context, expression);
        context.RecordExpression(expression, value);
        return value;
    }

    private EventScriptValue EvaluateExpressionCore(ExecutionContext context, ExpressionNode expression)
    {
        switch (expression)
        {
            case IntegerLiteralExpressionNode integer:
                return EventScriptValue.Integer(integer.Value);
            case DecimalLiteralExpressionNode number:
                return EventScriptValue.Decimal(number.Value);
            case PercentageLiteralExpressionNode percentage:
                return EventScriptValue.Percentage(percentage.PercentValue / 100m);

            case TextLiteralExpressionNode text:
                return EventScriptValue.Text(text.Value);

            case TagLiteralExpressionNode tag:
                return EventScriptValue.Tag(tag.Name);

            case ListLiteralExpressionNode listLiteral:
                return EventScriptValue.List(listLiteral.Items.Select(item => EvaluateExpression(context, item)));

            case SetLiteralExpressionNode setLiteral:
                return EventScriptValue.Set(setLiteral.Items.Select(item => EvaluateExpression(context, item)));

            case DictionaryLiteralExpressionNode dictionaryLiteral:
                return EvaluateDictionaryLiteral(context, dictionaryLiteral);

            case BooleanLiteralExpressionNode boolean:
                return EventScriptValue.Boolean(boolean.Value);

            case IdentifierExpressionNode identifier:
                return context.Resolve(identifier.Name);

            case CallExpressionNode call:
                return EvaluateCallExpression(context, call);

            case UnaryExpressionNode unary:
                return EvaluateUnaryExpression(context, unary);

            case VariadicTaggedExpressionNode variadic:
                return EvaluateVariadicTaggedExpression(context, variadic);

            case ClampExpressionNode clamp:
                return EvaluateClampExpression(context, clamp);

            case RandomExpressionNode randomExpression:
                return EvaluateRandomExpression(context, randomExpression);

            case DiceExpressionNode diceExpression:
                return EvaluateDiceExpression(diceExpression);

            case GeneratedCollectionExpressionNode generatedCollection:
                return EvaluateGeneratedCollectionExpression(context, generatedCollection);

            case GuardedChoiceExpressionNode guardedChoice:
                foreach (var branch in guardedChoice.Branches)
                {
                    if (AsBool(EvaluateExpression(context, branch.ConditionExpression)))
                    {
                        return EvaluateExpression(context, branch.ValueExpression);
                    }
                }

                return EvaluateExpression(context, guardedChoice.OtherwiseExpression);

            case BinaryExpressionNode binary:
                return EvaluateBinaryExpression(context, binary);

            case RulePredicateExpressionNode rulePredicate:
                return EvaluateRulePredicateExpression(context, rulePredicate);

            case TypeCheckExpressionNode typeCheck:
                return EventScriptValue.Boolean(IsValueOfType(EvaluateExpression(context, typeCheck.Value), typeCheck.TypeName));
            case TypeCastExpressionNode typeCast:
                return ConvertToDeclaredType(EvaluateExpression(context, typeCast.Value), typeCast.TypeName);

            case MemberAccessExpressionNode memberAccess:
                return EvaluateMemberAccess(context, memberAccess);

            case CollectionAccessExpressionNode collectionAccess:
                return EvaluateCollectionAccess(context, collectionAccess);

            default:
                return EventScriptValue.Nothing;
        }
    }

    private EventScriptValue EvaluateRandomExpression(ExecutionContext context, RandomExpressionNode randomExpression)
    {
        var fromValue = EvaluateExpression(context, randomExpression.FromExpression);
        var toValue = EvaluateExpression(context, randomExpression.ToExpression);

        if (TryUnwrapOptionalForOperation(fromValue, out var unwrappedFrom) &&
            TryUnwrapOptionalForOperation(toValue, out var unwrappedTo) &&
            unwrappedFrom.Type == EventScriptValueType.Integer &&
            unwrappedTo.Type == EventScriptValueType.Integer)
        {
            var from = AsInt(unwrappedFrom);
            var to = AsInt(unwrappedTo);
            if (from > to)
            {
                (from, to) = (to, from);
            }

            if (!TryNextInclusive(from, to, out var next))
            {
                return EventScriptValue.Nothing;
            }

            return EventScriptValue.Integer(next);
        }

        if (!TryCoerceNumericForOperation(fromValue, out var fromNumber) ||
            !TryCoerceNumericForOperation(toValue, out var toNumber) ||
            !fromNumber.IsFinite ||
            !toNumber.IsFinite)
        {
            return EventScriptValue.Nothing;
        }

        var lower = Math.Min(fromNumber.Value, toNumber.Value);
        var upper = Math.Max(fromNumber.Value, toNumber.Value);
        if (lower == upper)
        {
            return EventScriptValue.Decimal(lower);
        }

        return EventScriptValue.Decimal(lower + (upper - lower) * NextRandomUnit());
    }

    private EventScriptValue EvaluateDiceExpression(DiceExpressionNode diceExpression)
    {
        if (diceExpression.DiceCount <= 0 || diceExpression.SideCount <= 0)
        {
            return EventScriptValue.Dice(EventScriptDiceValue.Create(Array.Empty<int>()));
        }

        var rolls = new int[diceExpression.DiceCount];
        for (var i = 0; i < rolls.Length; i++)
        {
            if (!TryNextInclusive(1, diceExpression.SideCount, out var roll))
            {
                return EventScriptValue.Dice(EventScriptDiceValue.Create(Array.Empty<int>()));
            }

            rolls[i] = roll;
        }

        var dice = EventScriptDiceValue.Create(rolls);
        return EventScriptValue.Dice(dice);
    }

    private EventScriptValue EvaluateDictionaryLiteral(ExecutionContext context, DictionaryLiteralExpressionNode dictionaryLiteral)
    {
        var map = new Dictionary<string, EventScriptValue>(StringComparer.Ordinal);
        foreach (var entry in dictionaryLiteral.Entries)
        {
            map[entry.Key] = EvaluateExpression(context, entry.Value);
        }

        return EventScriptValue.Dictionary(map);
    }

    private EventScriptValue EvaluateGeneratedCollectionExpression(ExecutionContext context, GeneratedCollectionExpressionNode generatedCollection)
    {
        var fromValue = EvaluateExpression(context, generatedCollection.FromExpression);
        var toValue = EvaluateExpression(context, generatedCollection.ToExpression);
        var stepValue = generatedCollection.StepExpression is null
            ? EventScriptValue.Integer(1)
            : EvaluateExpression(context, generatedCollection.StepExpression);

        if (!TryCoerceNumericForOperation(fromValue, out var fromNumber) ||
            !TryCoerceNumericForOperation(toValue, out var toNumber) ||
            !TryCoerceNumericForOperation(stepValue, out var stepNumber) ||
            !fromNumber.IsFinite ||
            !toNumber.IsFinite ||
            !stepNumber.IsFinite)
        {
            return EventScriptValue.Nothing;
        }

        var from = ToIntegerSaturated(fromNumber.Value);
        var to = ToIntegerSaturated(toNumber.Value);
        var step = ToIntegerSaturated(stepNumber.Value);

        if (step == 0)
        {
            return generatedCollection.CollectionType == "set"
                ? EventScriptValue.Set(Array.Empty<EventScriptValue>())
                : EventScriptValue.List(Array.Empty<EventScriptValue>());
        }

        var values = new List<EventScriptValue>();
        if (step > 0)
        {
            for (var current = from; current <= to; current += step)
            {
                if (!TryProjectGeneratedItem(context, generatedCollection, EventScriptValue.Integer(current), values))
                {
                    break;
                }
            }
        }
        else
        {
            for (var current = from; current >= to; current += step)
            {
                if (!TryProjectGeneratedItem(context, generatedCollection, EventScriptValue.Integer(current), values))
                {
                    break;
                }
            }
        }

        return generatedCollection.CollectionType == "set"
            ? EventScriptValue.Set(values)
            : EventScriptValue.List(values);
    }

    private EventScriptValue EvaluateCallExpression(ExecutionContext context, CallExpressionNode call)
    {
        var arguments = call.Arguments.Select(argument => EvaluateExpression(context, argument)).ToArray();

        if (_ruleDefinitions.TryGetValue(call.Name, out var ruleDefinition))
        {
            return EvaluateGlobalDefinition(context, ruleDefinition.Parameters, ruleDefinition.Expression, arguments);
        }

        if (_selectDefinitions.TryGetValue(call.Name, out var selectDefinition))
        {
            return EvaluateGlobalDefinition(context, selectDefinition.Parameters, selectDefinition.Expression, arguments);
        }

        return EventScriptValue.Nothing;
    }

    private EventScriptValue EvaluateRulePredicateExpression(ExecutionContext context, RulePredicateExpressionNode rulePredicate)
    {
        if (!_ruleDefinitions.TryGetValue(rulePredicate.RuleName, out var ruleDefinition) ||
            ruleDefinition.Parameters.Count != 1)
        {
            return EventScriptValue.Nothing;
        }

        var value = EvaluateExpression(context, rulePredicate.Value);
        return EvaluateGlobalDefinition(context, ruleDefinition.Parameters, ruleDefinition.Expression, new[] { value });
    }

    private EventScriptValue EvaluateGlobalDefinition(ExecutionContext context, IReadOnlyList<string> parameters, ExpressionNode expression, IReadOnlyList<EventScriptValue> arguments)
    {
        context.PushScope();
        try
        {
            for (var i = 0; i < parameters.Count; i++)
            {
                context.Define(parameters[i], i < arguments.Count ? arguments[i] : EventScriptValue.Nothing);
            }

            return EvaluateExpression(context, expression);
        }
        finally
        {
            context.PopScope();
        }
    }

    private bool TryProjectGeneratedItem(ExecutionContext context, GeneratedCollectionExpressionNode generatedCollection, EventScriptValue item, List<EventScriptValue> values)
    {
        context.PushScope();
        try
        {
            context.Define(generatedCollection.Identifier, item);
            if (generatedCollection.Predicate is not null &&
                !AsBool(EvaluateExpression(context, generatedCollection.Predicate)))
            {
                return true;
            }

            values.Add(EvaluateExpression(context, generatedCollection.Projection));
            return true;
        }
        finally
        {
            context.PopScope();
        }
    }

    private EventScriptValue EvaluateUnaryExpression(ExecutionContext context, UnaryExpressionNode unary)
    {
        var operand = EvaluateExpression(context, unary.Operand);

        return unary.Operator switch
        {
            "-" => EvaluateNegateUnary(operand),
            "!" => EvaluateNotUnary(operand),
            "has value" => EventScriptValue.Boolean(EventScriptValueSemantics.HasValue(operand)),
            "empty" => EventScriptValue.Boolean(EventScriptValueSemantics.IsEmpty(operand)),
            "len" => EvaluateLenUnary(operand),
            "chance" => EvaluateChanceUnary(operand),
            "keys" => EventScriptValue.Keys(operand),
            "values" => EventScriptValue.Values(operand),
            "entries" => EventScriptValue.Entries(operand),
            "abs" => EvaluateAbsUnary(operand),
            "floor" => EvaluateRoundingUnary(operand, "floor"),
            "ceil" => EvaluateRoundingUnary(operand, "ceil"),
            "round" => EvaluateRoundingUnary(operand, "round"),
            "rounddown" => EvaluateRoundingUnary(operand, "rounddown"),
            "roundup" => EvaluateRoundingUnary(operand, "roundup"),
            "roundeven" => EvaluateRoundingUnary(operand, "roundeven"),
            _ => EventScriptValue.Nothing
        };
    }

    private static EventScriptValue EvaluateNegateUnary(EventScriptValue operand)
    {
        if (operand.isNothing())
        {
            return EventScriptValue.Nothing;
        }

        if (!TryUnwrapOptionalForOperation(operand, out var unwrapped))
        {
            return EventScriptValue.OptionalNone();
        }

        if (unwrapped.isPercentage())
        {
            return EventScriptValue.Percentage(-unwrapped.AsNumber());
        }

        if (!TryCoerceNumericForOperation(unwrapped, out var number))
        {
            return EventScriptValue.Nothing;
        }

        return number.Kind switch
        {
            NumericKind.Finite => EventScriptValue.Decimal(-number.Value),
            NumericKind.NaN => EventScriptValue.DecimalNaN(),
            NumericKind.PositiveInfinity => EventScriptValue.DecimalNegativeInfinity(),
            NumericKind.NegativeInfinity => EventScriptValue.DecimalInfinity(),
            _ => EventScriptValue.DecimalNaN()
        };
    }

    private EventScriptValue EvaluateVariadicTaggedExpression(ExecutionContext context, VariadicTaggedExpressionNode variadic)
    {
        var values = variadic.Arguments.Select(argument => EvaluateExpression(context, argument)).ToArray();
        if (values.Length == 0)
        {
            return EventScriptValue.Nothing;
        }

        return variadic.Operator switch
        {
            "min" => EvaluateMinMax(values, isMax: false),
            "max" => EvaluateMinMax(values, isMax: true),
            _ => EventScriptValue.Nothing
        };
    }

    private EventScriptValue EvaluateClampExpression(ExecutionContext context, ClampExpressionNode clamp)
    {
        var raw = EvaluateExpression(context, clamp.Value);
        var minimum = EvaluateExpression(context, clamp.Minimum);
        var maximum = EvaluateExpression(context, clamp.Maximum);

        if (!TryCoerceNumericForOperation(raw, out var rawNumber) ||
            !TryCoerceNumericForOperation(minimum, out var minimumNumber) ||
            !TryCoerceNumericForOperation(maximum, out var maximumNumber))
        {
            return EventScriptValue.Nothing;
        }

        if (!rawNumber.IsFinite || !minimumNumber.IsFinite || !maximumNumber.IsFinite)
        {
            return EventScriptValue.Nothing;
        }

        var lower = Math.Min(minimumNumber.Value, maximumNumber.Value);
        var upper = Math.Max(minimumNumber.Value, maximumNumber.Value);
        return EventScriptValue.Decimal(Math.Min(Math.Max(rawNumber.Value, lower), upper));
    }

    private EventScriptValue EvaluateNotUnary(EventScriptValue operand)
    {
        if (operand.isNothing())
        {
            return EventScriptValue.Nothing;
        }

        if (!TryUnwrapOptionalForOperation(operand, out var unwrapped))
        {
            return EventScriptValue.OptionalNone();
        }

        return EventScriptValue.Boolean(!AsBool(unwrapped));
    }

    private static EventScriptValue EvaluateLenUnary(EventScriptValue operand)
    {
        if (operand.isNothing())
        {
            return EventScriptValue.Integer(0);
        }

        return operand.Type switch
        {
            EventScriptValueType.Text => EventScriptValue.Integer(operand.AsText().Length),
            EventScriptValueType.Iterator => EventScriptValue.Integer(operand.AsEnumerable().LongCount()),
            EventScriptValueType.List => EventScriptValue.Integer(operand.AsList().Count),
            EventScriptValueType.Dictionary => EventScriptValue.Integer(operand.AsDictionary().Count),
            EventScriptValueType.Set => EventScriptValue.Integer(operand.AsSet().Count),
            EventScriptValueType.Dice => EventScriptValue.Integer(operand.AsDice().Rolls.Count),
            EventScriptValueType.Optional => EventScriptValue.Integer(operand.AsOptional().HasValue ? 1 : 0),
            _ => EventScriptValue.Nothing
        };
    }

    private EventScriptValue EvaluateChanceUnary(EventScriptValue operand)
    {
        var percentage = ConvertToPercentage(operand);
        if (!percentage.isPercentage())
        {
            return EventScriptValue.Boolean(false);
        }

        var ratio = percentage.AsNumber();
        if (ratio <= 0m)
        {
            return EventScriptValue.Boolean(false);
        }

        if (ratio >= 1m)
        {
            return EventScriptValue.Boolean(true);
        }

        var threshold = ratio * RandomUnitScale;
        return EventScriptValue.Boolean(NextRandomUnit() < threshold);
    }

    private static EventScriptValue EvaluateRoundingUnary(EventScriptValue operand, string operation)
    {
        if (operand.isNothing())
        {
            return EventScriptValue.Nothing;
        }

        if (!TryCoerceNumericForOperation(operand, out var number))
        {
            return EventScriptValue.Nothing;
        }

        if (number.IsNaN)
        {
            return EventScriptValue.Nothing;
        }

        if (number.IsPositiveInfinity)
        {
            return EventScriptValue.Integer(long.MaxValue);
        }

        if (number.IsNegativeInfinity)
        {
            return EventScriptValue.Integer(long.MinValue);
        }

        return operation switch
        {
            "floor" or "rounddown" => EventScriptValue.Integer(ToIntegerSaturated(Math.Floor(number.Value))),
            "ceil" or "roundup" => EventScriptValue.Integer(ToIntegerSaturated(Math.Ceiling(number.Value))),
            "round" or "roundeven" => EventScriptValue.Integer(ToIntegerSaturated(Math.Round(number.Value, 0, MidpointRounding.ToEven))),
            _ => EventScriptValue.Nothing
        };
    }

    private static EventScriptValue EvaluateAbsUnary(EventScriptValue operand)
    {
        if (operand.isNothing())
        {
            return EventScriptValue.Nothing;
        }

        if (!TryCoerceNumericForOperation(operand, out var number) || !number.IsFinite)
        {
            return EventScriptValue.Nothing;
        }

        return EventScriptValue.Decimal(Math.Abs(number.Value));
    }

    private static EventScriptValue EvaluateMinMax(IReadOnlyList<EventScriptValue> values, bool isMax)
    {
        var best = values[0];
        for (var i = 1; i < values.Count; i++)
        {
            var comparison = EventScriptValue.StableComparer.Compare(values[i], best);
            if ((isMax && comparison > 0) || (!isMax && comparison < 0))
            {
                best = values[i];
            }
        }

        return best;
    }

    private bool EvaluateSequencePattern(ExecutionContext context, EventScriptValue target, DicePatternNode pattern)
    {
        if (!IsPatternSequence(target))
        {
            return false;
        }

        var items = target.AsList();
        var counts = items
            .GroupBy(item => item)
            .ToDictionary(group => group.Key, group => group.Count());

        return pattern switch
        {
            DiceCountPatternNode countPattern => MatchDiceCountPattern(context, counts, countPattern),
            DiceFullHousePatternNode => counts.Count == 2 && counts.Values.OrderByDescending(x => x).SequenceEqual(new[] { 3, 2 }),
            DiceStraightPatternNode => MatchStraight(items),
            _ => false
        };
    }

    private bool MatchDiceCountPattern(ExecutionContext context, IReadOnlyDictionary<EventScriptValue, int> counts, DiceCountPatternNode pattern)
    {
        if (pattern.Face is not null)
        {
            return counts.TryGetValue(EvaluateExpression(context, pattern.Face), out var count) && count >= pattern.Count;
        }

        return counts.Values.Any(count => count >= pattern.Count);
    }

    private static bool MatchStraight(IReadOnlyList<EventScriptValue> items)
    {
        var unique = items
            .Select(item => item.AsInteger())
            .Distinct()
            .OrderByDescending(x => x)
            .ToArray();
        if (unique.Length < 2)
        {
            return false;
        }

        for (var i = 0; i < unique.Length - 1; i++)
        {
            if (unique[i] - 1 != unique[i + 1])
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPatternSequence(EventScriptValue value) => value.Type is EventScriptValueType.List or EventScriptValueType.Dice;

    private static bool TryCombineWithPlus(EventScriptValue left, EventScriptValue right, out EventScriptValue value)
    {
        if (left.Type == EventScriptValueType.Dictionary && right.Type == EventScriptValueType.Dictionary)
        {
            value = EvaluateDictionaryCombine(left, right);
            return true;
        }

        if (left.Type == EventScriptValueType.List && right.Type == EventScriptValueType.List)
        {
            value = EventScriptValue.List(left.AsList().Concat(right.AsList()));
            return true;
        }

        if (left.Type == EventScriptValueType.List)
        {
            value = EventScriptValue.List(left.AsList().Append(right));
            return true;
        }

        if (right.Type == EventScriptValueType.List)
        {
            value = EventScriptValue.List(new[] { left }.Concat(right.AsList()));
            return true;
        }

        value = EventScriptValue.Nothing;
        return false;
    }

    private static EventScriptValue EvaluateCollectionCombine(EventScriptValue left, EventScriptValue right)
    {
        if (left.Type == EventScriptValueType.Dictionary && right.Type == EventScriptValueType.Dictionary)
        {
            return EvaluateDictionaryCombine(left, right);
        }

        if (left.Type == EventScriptValueType.Set && right.Type == EventScriptValueType.Set)
        {
            return EventScriptValue.Set(left.AsSet().Concat(right.AsSet()));
        }

        if (left.Type is EventScriptValueType.List or EventScriptValueType.Dice &&
            right.Type is EventScriptValueType.List or EventScriptValueType.Dice)
        {
            return EventScriptValue.List(left.AsList().Concat(right.AsList()));
        }

        return EventScriptValue.Nothing;
    }

    private static EventScriptValue EvaluateCollectionIntersect(EventScriptValue left, EventScriptValue right)
    {
        if (left.Type == EventScriptValueType.Dictionary && right.Type == EventScriptValueType.Dictionary)
        {
            var rightKeys = new HashSet<string>(right.AsDictionary().Keys, StringComparer.Ordinal);
            var map = left.AsDictionary()
                .Where(pair => rightKeys.Contains(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            return EventScriptValue.Dictionary(map);
        }

        if (left.Type == EventScriptValueType.Set && right.Type == EventScriptValueType.Set)
        {
            var rightSet = right.AsSet();
            return EventScriptValue.Set(left.AsSet().Where(item => rightSet.Contains(item)));
        }

        if (left.Type is EventScriptValueType.List or EventScriptValueType.Dice &&
            right.Type is EventScriptValueType.List or EventScriptValueType.Dice)
        {
            var remaining = right.AsList().ToList();
            var result = new List<EventScriptValue>();
            foreach (var item in left.AsList())
            {
                var index = remaining.FindIndex(candidate => AreEqual(candidate, item));
                if (index < 0)
                {
                    continue;
                }

                result.Add(item);
                remaining.RemoveAt(index);
            }

            return EventScriptValue.List(result);
        }

        return EventScriptValue.Nothing;
    }

    private static EventScriptValue EvaluateCollectionExcept(EventScriptValue left, EventScriptValue right)
    {
        if (left.Type == EventScriptValueType.Dictionary && right.Type == EventScriptValueType.Dictionary)
        {
            var rightKeys = new HashSet<string>(right.AsDictionary().Keys, StringComparer.Ordinal);
            var map = left.AsDictionary()
                .Where(pair => !rightKeys.Contains(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            return EventScriptValue.Dictionary(map);
        }

        if (left.Type == EventScriptValueType.Set && right.Type == EventScriptValueType.Set)
        {
            var rightSet = right.AsSet();
            return EventScriptValue.Set(left.AsSet().Where(item => !rightSet.Contains(item)));
        }

        if (left.Type is EventScriptValueType.List or EventScriptValueType.Dice &&
            right.Type is EventScriptValueType.List or EventScriptValueType.Dice)
        {
            var remaining = right.AsList().ToList();
            var result = new List<EventScriptValue>();
            foreach (var item in left.AsList())
            {
                var index = remaining.FindIndex(candidate => AreEqual(candidate, item));
                if (index >= 0)
                {
                    remaining.RemoveAt(index);
                    continue;
                }

                result.Add(item);
            }

            return EventScriptValue.List(result);
        }

        return EventScriptValue.Nothing;
    }

    private static EventScriptValue EvaluateCollectionZip(EventScriptValue left, EventScriptValue right)
    {
        if (left.Type is not (EventScriptValueType.List or EventScriptValueType.Dice) ||
            right.Type is not (EventScriptValueType.List or EventScriptValueType.Dice))
        {
            return EventScriptValue.Nothing;
        }

        var leftItems = left.AsList();
        var rightItems = right.AsList();
        var count = Math.Min(leftItems.Count, rightItems.Count);
        var zipped = new List<EventScriptValue>(count);
        for (var i = 0; i < count; i++)
        {
            zipped.Add(EventScriptValue.Dictionary(new Dictionary<string, EventScriptValue>(StringComparer.Ordinal)
            {
                ["left"] = leftItems[i],
                ["right"] = rightItems[i]
            }));
        }

        return EventScriptValue.List(zipped);
    }

    private static EventScriptValue EvaluateDictionaryCombine(EventScriptValue left, EventScriptValue right)
    {
        var map = new Dictionary<string, EventScriptValue>(left.AsDictionary(), StringComparer.Ordinal);
        foreach (var pair in right.AsDictionary())
        {
            map[pair.Key] = pair.Value;
        }

        return EventScriptValue.Dictionary(map);
    }

    private EventScriptValue EvaluateBinaryExpression(ExecutionContext context, BinaryExpressionNode binary)
    {
        var leftRaw = EvaluateExpression(context, binary.Left);
        var rightRaw = EvaluateExpression(context, binary.Right);

        if (binary.Operator == "default")
        {
            if (!EventScriptValueSemantics.HasValue(leftRaw))
            {
                return rightRaw;
            }

            if (leftRaw.isOptional())
            {
                var optional = leftRaw.AsOptional();
                return optional.HasValue ? optional.Value : rightRaw;
            }

            return leftRaw;
        }

        if (leftRaw.isNothing() || rightRaw.isNothing())
        {
            return EventScriptValue.Nothing;
        }

        if (!TryUnwrapOptionalForOperation(leftRaw, out var left) || !TryUnwrapOptionalForOperation(rightRaw, out var right))
        {
            return EventScriptValue.OptionalNone();
        }

        switch (binary.Operator)
        {
            case "|":
                return EventScriptValue.Boolean(AsBool(left) || AsBool(right));
            case "^":
                return EventScriptValue.Boolean(AsBool(left) ^ AsBool(right));
            case "&":
                return EventScriptValue.Boolean(AsBool(left) && AsBool(right));
            case "default":
                return EventScriptValue.Nothing;
            case "=":
                return EventScriptValue.Boolean(AreEqual(left, right));
            case "<>":
                return EventScriptValue.Boolean(!AreEqual(left, right));
            case "in":
                return EventScriptValue.Boolean(EventScriptValueSemantics.Contains(right, left));
            case "value in":
                return EventScriptValue.Boolean(EventScriptValueSemantics.ContainsValue(right, left));
            case "starts with":
                return EventScriptValue.Boolean(EventScriptValueSemantics.StartsWith(left, right));
            case "ends with":
                return EventScriptValue.Boolean(EventScriptValueSemantics.EndsWith(left, right));
            case "<":
                if (!TryCoerceNumericForOperation(left, out var leftLess) ||
                    !TryCoerceNumericForOperation(right, out var rightLess) ||
                    !TryCompareNumeric(leftLess, rightLess, out var lessComparison))
                {
                    return EventScriptValue.Boolean(false);
                }

                return EventScriptValue.Boolean(lessComparison < 0);
            case ">":
                if (!TryCoerceNumericForOperation(left, out var leftGreater) ||
                    !TryCoerceNumericForOperation(right, out var rightGreater) ||
                    !TryCompareNumeric(leftGreater, rightGreater, out var greaterComparison))
                {
                    return EventScriptValue.Boolean(false);
                }

                return EventScriptValue.Boolean(greaterComparison > 0);
            case "<=":
                if (!TryCoerceNumericForOperation(left, out var leftLessOrEqual) ||
                    !TryCoerceNumericForOperation(right, out var rightLessOrEqual) ||
                    !TryCompareNumeric(leftLessOrEqual, rightLessOrEqual, out var lessOrEqualComparison))
                {
                    return EventScriptValue.Boolean(false);
                }

                return EventScriptValue.Boolean(lessOrEqualComparison <= 0);
            case ">=":
                if (!TryCoerceNumericForOperation(left, out var leftGreaterOrEqual) ||
                    !TryCoerceNumericForOperation(right, out var rightGreaterOrEqual) ||
                    !TryCompareNumeric(leftGreaterOrEqual, rightGreaterOrEqual, out var greaterOrEqualComparison))
                {
                    return EventScriptValue.Boolean(false);
                }

                return EventScriptValue.Boolean(greaterOrEqualComparison >= 0);
            case "+":
            {
                if (TryCoerceNumericForOperation(left, out var leftNumeric) &&
                    TryCoerceNumericForOperation(right, out var rightNumeric))
                {
                    return ToEventScriptDecimal(AddNumeric(leftNumeric, rightNumeric));
                }

                if (TryCombineWithPlus(left, right, out var combined))
                {
                    return combined;
                }

                if (left.isText() || right.isText())
                {
                    return EventScriptValue.Text($"{ToText(left)}{ToText(right)}");
                }

                return EventScriptValue.DecimalNaN();
            }
            case "intersect":
                return EvaluateCollectionIntersect(left, right);
            case "combine":
                return EvaluateCollectionCombine(left, right);
            case "merge":
                return EvaluateCollectionCombine(left, right);
            case "except":
                return EvaluateCollectionExcept(left, right);
            case "zip":
                return EvaluateCollectionZip(left, right);
            case "-":
                if (!TryCoerceNumericForOperation(left, out var leftMinus) ||
                    !TryCoerceNumericForOperation(right, out var rightMinus))
                {
                    return EventScriptValue.DecimalNaN();
                }

                return ToEventScriptDecimal(SubtractNumeric(leftMinus, rightMinus));
            case "*":
                if (!TryCoerceNumericForOperation(left, out var leftMultiply) ||
                    !TryCoerceNumericForOperation(right, out var rightMultiply))
                {
                    return EventScriptValue.DecimalNaN();
                }

                return ToEventScriptDecimal(MultiplyNumeric(leftMultiply, rightMultiply));
            case "/":
                if (!TryCoerceNumericForOperation(left, out var leftDivide) ||
                    !TryCoerceNumericForOperation(right, out var rightDivide))
                {
                    return EventScriptValue.DecimalNaN();
                }

                return ToEventScriptDecimal(DivideNumeric(leftDivide, rightDivide));
            case "%":
                if (!TryCoerceNumericForOperation(left, out var leftModulo) ||
                    !TryCoerceNumericForOperation(right, out var rightModulo))
                {
                    return EventScriptValue.DecimalNaN();
                }

                return ToEventScriptDecimal(ModuloNumeric(leftModulo, rightModulo));
            default:
                return EventScriptValue.Nothing;
        }
    }

    private EventScriptValue EvaluateMemberAccess(ExecutionContext context, MemberAccessExpressionNode memberAccess)
    {
        var target = EvaluateExpression(context, memberAccess.Target);
        if (target.isNothing())
        {
            return EventScriptValue.Nothing;
        }

        if (target.Type == EventScriptValueType.Dictionary)
        {
            if (target.TryGetDictionaryMember(memberAccess.Member, out var value))
            {
                return value;
            }

            return EventScriptValue.Nothing;
        }

        return EventScriptValue.Nothing;
    }

    private EventScriptValue EvaluateCollectionAccess(ExecutionContext context, CollectionAccessExpressionNode collectionAccess)
    {
        var target = EvaluateExpression(context, collectionAccess.Target);
        if (target.isNothing())
        {
            return EventScriptValue.Nothing;
        }

        var items = target.AsList();

        switch (collectionAccess.Selector)
        {
            case ExpressionSelectorNode expressionSelector:
                return EvaluateIndexedCollectionAccess(context, target, expressionSelector.Expression);

            case PatternSelectorNode patternSelector:
                return EventScriptValue.Boolean(EvaluateSequencePattern(context, target, patternSelector.Pattern));

            case ObjectMatchSelectorNode objectMatchSelector:
                return EventScriptValue.Boolean(EvaluateObjectMatchSelector(context, target, objectMatchSelector.Pattern));

            case TakePatternSelectorNode takePatternSelector:
                return EvaluateTakePattern(context, target, takePatternSelector.Pattern);

            case SequenceSliceSelectorNode sliceSelector:
                return EvaluateSequenceSliceSelector(target, items, sliceSelector);

            case PredicateSelectorNode predicateSelector:
                return EvaluatePredicateSelector(context, items, predicateSelector);

            case CountSelectorNode countSelector:
                return EvaluateCountSelector(context, items, countSelector);

            case ChooseSelectorNode chooseSelector:
                return EvaluateChooseSelector(context, items, chooseSelector);

            case DrawSelectorNode drawSelector:
                return EvaluateDrawSelector(target, items, drawSelector);

            case ShuffleSelectorNode:
                return EvaluateShuffleSelector(target, items);

            case ReverseSelectorNode:
                return EvaluateReverseSelector(target, items);

            case EdgeSelectorNode edgeSelector:
                return EvaluateEdgeSelector(context, items, edgeSelector);

            case FilterSelectorNode filterSelector:
                return EvaluateFilterSelector(context, items, filterSelector);

            case SumSelectorNode sumSelector:
                return EvaluateSumSelector(context, items, sumSelector);

            case AverageSelectorNode averageSelector:
                return EvaluateAverageSelector(context, items, averageSelector);

            case MinSelectorNode minSelector:
                return EvaluateExtremaSelector(context, items, minSelector.Identifier, minSelector.Projection, isMax: false);

            case MaxSelectorNode maxSelector:
                return EvaluateExtremaSelector(context, items, maxSelector.Identifier, maxSelector.Projection, isMax: true);

            case SelectSelectorNode selectSelector:
                return EvaluateSelectSelector(context, items, selectSelector);

            case DictionarySelectorNode dictionarySelector:
                return EvaluateDictionarySelector(context, items, dictionarySelector);

            case ContainsSelectorNode containsSelector:
                return EvaluateContainsSelector(context, target, items, containsSelector);

            case SortSelectorNode sortSelector:
                return EvaluateSortSelector(context, target, items, sortSelector);

            case DistinctSelectorNode distinctSelector:
                return EvaluateDistinctSelector(context, target, items, distinctSelector);

            case GroupBySelectorNode groupBySelector:
                return EvaluateGroupBySelector(context, items, groupBySelector);

            case OrderBySelectorNode orderBySelector:
                return EvaluateOrderBySelector(context, target, items, orderBySelector);

            default:
                return EventScriptValue.Nothing;
        }
    }

    private EventScriptValue EvaluateEdgeSelector(ExecutionContext context, IReadOnlyList<EventScriptValue> items, EdgeSelectorNode selector)
    {
        IReadOnlyList<EventScriptValue> candidates = items;
        if (!string.IsNullOrEmpty(selector.Identifier) && selector.Predicate is not null)
        {
            candidates = items
                .Where(item => EvaluatePredicateItem(context, item, selector.Identifier!, selector.Predicate))
                .ToArray();
        }

        if (candidates.Count == 0)
        {
            return EventScriptValue.Nothing;
        }

        return selector.Mode switch
        {
            "first" => candidates[0],
            "last" => candidates[^1],
            "single" => candidates.Count == 1 ? candidates[0] : EventScriptValue.Nothing,
            _ => EventScriptValue.Nothing
        };
    }

    private bool EvaluatePredicateItem(ExecutionContext context, EventScriptValue item, string identifier, ExpressionNode predicate)
    {
        context.PushScope();
        try
        {
            context.Define(identifier, item);
            return EvaluateExpression(context, predicate).AsBoolean();
        }
        finally
        {
            context.PopScope();
        }
    }

    private EventScriptValue EvaluateIndexedCollectionAccess(ExecutionContext context, EventScriptValue target, ExpressionNode selectorExpression)
    {
        var selector = EvaluateExpression(context, selectorExpression);
        if (selector.isNothing())
        {
            return EventScriptValue.Nothing;
        }

        return EventScriptValueSemantics.Lookup(target, selector);
    }

    private EventScriptValue EvaluateTakePattern(ExecutionContext context, EventScriptValue target, DicePatternNode pattern)
    {
        if (!IsPatternSequence(target))
        {
            return EventScriptValue.Nothing;
        }

        var items = target.AsList();
        if (!TryTakeSequencePattern(context, items, pattern, out var takenItems))
        {
            return EventScriptValue.Nothing;
        }

        return target.Type == EventScriptValueType.Dice
            ? EventScriptValue.Dice(EventScriptDiceValue.Create(takenItems.Select(item => (int)item.AsInteger())))
            : EventScriptValue.List(takenItems);
    }

    private bool EvaluateObjectMatchSelector(ExecutionContext context, EventScriptValue target, ObjectMatchPatternNode pattern)
    {
        if (target.Type is not (EventScriptValueType.List or EventScriptValueType.Set or EventScriptValueType.Dice))
        {
            return false;
        }

        foreach (var item in target.AsList())
        {
            if (MatchesObjectPattern(context, item, pattern))
            {
                return true;
            }
        }

        return false;
    }

    private static EventScriptValue EvaluateSequenceSliceSelector(EventScriptValue target, IReadOnlyList<EventScriptValue> items, SequenceSliceSelectorNode selector)
    {
        if (selector.Count <= 0)
        {
            return target.Type == EventScriptValueType.Dice
                ? EventScriptValue.Dice(EventScriptDiceValue.Create(Array.Empty<int>()))
                : EventScriptValue.List(Array.Empty<EventScriptValue>());
        }

        var selectedItems = selector.Scope switch
        {
            "first" => TakeFirst(items, selector.Count),
            "last" => TakeLast(items, selector.Count),
            "highest" => TakeHighest(items, selector.Count),
            "lowest" => TakeLowest(items, selector.Count),
            _ => Array.Empty<EventScriptValue>()
        };

        if (string.Equals(selector.Operation, "drop", StringComparison.Ordinal))
        {
            selectedItems = DropSelection(items, selectedItems);
        }

        return target.Type switch
        {
            EventScriptValueType.Dice => EventScriptValue.Dice(EventScriptDiceValue.Create(selectedItems.Select(item => (int)item.AsInteger()))),
            EventScriptValueType.List => EventScriptValue.List(selectedItems),
            EventScriptValueType.Set => EventScriptValue.List(selectedItems),
            _ => EventScriptValue.Nothing
        };
    }

    private static EventScriptValue[] TakeFirst(IReadOnlyList<EventScriptValue> items, int count) => items.Take(count).ToArray();

    private static EventScriptValue[] TakeLast(IReadOnlyList<EventScriptValue> items, int count) => items.Skip(Math.Max(0, items.Count - count)).ToArray();

    private static EventScriptValue[] TakeHighest(IReadOnlyList<EventScriptValue> items, int count) => items
            .OrderByDescending(item => item, EventScriptValue.StableComparer)
            .Take(count)
            .ToArray();

    private static EventScriptValue[] TakeLowest(IReadOnlyList<EventScriptValue> items, int count) => items
            .OrderBy(item => item, EventScriptValue.StableComparer)
            .Take(count)
            .ToArray();

    private static EventScriptValue[] DropSelection(IReadOnlyList<EventScriptValue> items, IReadOnlyList<EventScriptValue> selection)
    {
        if (selection.Count == 0)
        {
            return items.ToArray();
        }

        var remainingSelections = selection
            .GroupBy(item => item)
            .ToDictionary(group => group.Key, group => group.Count());
        var result = new List<EventScriptValue>(items.Count);

        foreach (var item in items)
        {
            if (remainingSelections.TryGetValue(item, out var remainingCount) && remainingCount > 0)
            {
                remainingSelections[item] = remainingCount - 1;
                continue;
            }

            result.Add(item);
        }

        return result.ToArray();
    }

    private bool TryTakeSequencePattern(ExecutionContext context, IReadOnlyList<EventScriptValue> items, DicePatternNode pattern, out IReadOnlyList<EventScriptValue> takenItems)
    {
        var counts = items
            .GroupBy(item => item)
            .ToDictionary(group => group.Key, group => group.Count());

        switch (pattern)
        {
            case DiceCountPatternNode countPattern:
                return TryTakeCountPattern(context, items, counts, countPattern, out takenItems);

            case DiceFullHousePatternNode:
                return TryTakeFullHouse(items, counts, out takenItems);

            case DiceStraightPatternNode:
                return TryTakeStraight(items, out takenItems);

            default:
                takenItems = Array.Empty<EventScriptValue>();
                return false;
        }
    }

    private bool TryTakeCountPattern(ExecutionContext context, IReadOnlyList<EventScriptValue> items, IReadOnlyDictionary<EventScriptValue, int> counts, DiceCountPatternNode pattern, out IReadOnlyList<EventScriptValue> takenItems)
    {
        if (pattern.Face is not null)
        {
            var face = EvaluateExpression(context, pattern.Face);
            if (counts.TryGetValue(face, out var faceCount) && faceCount >= pattern.Count)
            {
                takenItems = TakeItemsByCounts(items, new Dictionary<EventScriptValue, int> { [face] = pattern.Count });
                return true;
            }

            takenItems = Array.Empty<EventScriptValue>();
            return false;
        }

        foreach (var candidate in EnumerateDistinctInSourceOrder(items))
        {
            if (counts.TryGetValue(candidate, out var candidateCount) && candidateCount >= pattern.Count)
            {
                takenItems = TakeItemsByCounts(items, new Dictionary<EventScriptValue, int> { [candidate] = pattern.Count });
                return true;
            }
        }

        takenItems = Array.Empty<EventScriptValue>();
        return false;
    }

    private static bool TryTakeFullHouse(IReadOnlyList<EventScriptValue> items, IReadOnlyDictionary<EventScriptValue, int> counts, out IReadOnlyList<EventScriptValue> takenItems)
    {
        foreach (var tripleCandidate in EnumerateDistinctInSourceOrder(items))
        {
            if (!counts.TryGetValue(tripleCandidate, out var tripleCount) || tripleCount < 3)
            {
                continue;
            }

            foreach (var pairCandidate in EnumerateDistinctInSourceOrder(items))
            {
                if (AreEqual(pairCandidate, tripleCandidate))
                {
                    continue;
                }

                if (counts.TryGetValue(pairCandidate, out var pairCount) && pairCount >= 2)
                {
                    takenItems = TakeItemsByCounts(items, new Dictionary<EventScriptValue, int>
                    {
                        [tripleCandidate] = 3,
                        [pairCandidate] = 2
                    });
                    return true;
                }
            }
        }

        takenItems = Array.Empty<EventScriptValue>();
        return false;
    }

    private static bool TryTakeStraight(IReadOnlyList<EventScriptValue> items, out IReadOnlyList<EventScriptValue> takenItems)
    {
        var distinctValues = new List<EventScriptValue>();
        var seenIntegers = new HashSet<long>();
        foreach (var item in items)
        {
            var value = item.AsInteger();
            if (seenIntegers.Add(value))
            {
                distinctValues.Add(item);
            }
        }

        var uniqueIntegers = distinctValues
            .Select(item => item.AsInteger())
            .OrderByDescending(value => value)
            .ToArray();
        if (uniqueIntegers.Length < 2)
        {
            takenItems = Array.Empty<EventScriptValue>();
            return false;
        }

        for (var i = 0; i < uniqueIntegers.Length - 1; i++)
        {
            if (uniqueIntegers[i] - 1 != uniqueIntegers[i + 1])
            {
                takenItems = Array.Empty<EventScriptValue>();
                return false;
            }
        }

        takenItems = distinctValues;
        return true;
    }

    private static IReadOnlyList<EventScriptValue> TakeItemsByCounts(IReadOnlyList<EventScriptValue> items, IReadOnlyDictionary<EventScriptValue, int> requiredCounts)
    {
        var remaining = requiredCounts.ToDictionary(pair => pair.Key, pair => pair.Value);
        var takenItems = new List<EventScriptValue>();

        foreach (var item in items)
        {
            if (!remaining.TryGetValue(item, out var remainingCount) || remainingCount <= 0)
            {
                continue;
            }

            takenItems.Add(item);
            remaining[item] = remainingCount - 1;
        }

        return takenItems;
    }

    private static IEnumerable<EventScriptValue> EnumerateDistinctInSourceOrder(IReadOnlyList<EventScriptValue> items)
    {
        var seen = new HashSet<EventScriptValue>();
        foreach (var item in items)
        {
            if (seen.Add(item))
            {
                yield return item;
            }
        }
    }

    private EventScriptValue EvaluatePredicateSelector(ExecutionContext context, IReadOnlyList<EventScriptValue> items, PredicateSelectorNode selector)
    {
        var isAny = string.Equals(selector.Operator, "any", StringComparison.Ordinal);
        if (!isAny && !string.Equals(selector.Operator, "all", StringComparison.Ordinal))
        {
            return EventScriptValue.Boolean(false);
        }

        if (!isAny && items.Count == 0)
        {
            return EventScriptValue.Boolean(true);
        }

        foreach (var item in items)
        {
            context.PushScope();
            try
            {
                context.Define(selector.Identifier, item);
                var predicateResult = AsBool(EvaluateExpression(context, selector.Predicate));
                if (isAny && predicateResult)
                {
                    return EventScriptValue.Boolean(true);
                }

                if (!isAny && !predicateResult)
                {
                    return EventScriptValue.Boolean(false);
                }
            }
            finally
            {
                context.PopScope();
            }
        }

        return EventScriptValue.Boolean(!isAny);
    }

    private EventScriptValue EvaluateCountSelector(ExecutionContext context, IReadOnlyList<EventScriptValue> items, CountSelectorNode selector)
    {
        var count = 0;
        foreach (var item in items)
        {
            context.PushScope();
            try
            {
                context.Define(selector.Identifier, item);
                if (AsBool(EvaluateExpression(context, selector.Predicate)))
                {
                    count++;
                }
            }
            finally
            {
                context.PopScope();
            }
        }

        return EventScriptValue.Integer(count);
    }

    private EventScriptValue EvaluateChooseSelector(ExecutionContext context, IReadOnlyList<EventScriptValue> items, ChooseSelectorNode selector)
    {
        var candidates = selector.Predicate is null || string.IsNullOrEmpty(selector.Identifier)
            ? items.ToList()
            : FilterItems(context, items, selector.Identifier!, selector.Predicate);

        IReadOnlyList<EventScriptValue> chosen;
        if (selector.WeightExpression is not null && !string.IsNullOrEmpty(selector.WeightIdentifier))
        {
            chosen = ChooseWeightedItems(context, candidates, selector.Count, selector.WeightIdentifier!, selector.WeightExpression);
        }
        else if (selector.AtRandom)
        {
            chosen = ChooseRandomItems(candidates, selector.Count);
        }
        else
        {
            chosen = candidates.Take(selector.Count).ToArray();
        }

        if (selector.Count == 1)
        {
            return chosen.Count == 0 ? EventScriptValue.Nothing : chosen[0];
        }

        return EventScriptValue.List(chosen);
    }

    private static EventScriptValue EvaluateDrawSelector(EventScriptValue target, IReadOnlyList<EventScriptValue> items, DrawSelectorNode selector)
    {
        if (target.Type is not (EventScriptValueType.List or EventScriptValueType.Dice))
        {
            return EventScriptValue.Nothing;
        }

        var drawn = items.Take(selector.Count).ToArray();
        if (selector.Count == 1)
        {
            return drawn.Length == 0 ? EventScriptValue.Nothing : drawn[0];
        }

        return target.Type == EventScriptValueType.Dice
            ? EventScriptValue.Dice(EventScriptDiceValue.Create(drawn.Select(item => (int)item.AsInteger())))
            : EventScriptValue.List(drawn);
    }

    private EventScriptValue EvaluateShuffleSelector(EventScriptValue target, IReadOnlyList<EventScriptValue> items)
    {
        if (target.Type is not (EventScriptValueType.List or EventScriptValueType.Dice))
        {
            return EventScriptValue.Nothing;
        }

        var shuffled = items.ToArray();
        for (var i = shuffled.Length - 1; i > 0; i--)
        {
            if (!TryNextInclusive(0, i, out var swapIndex))
            {
                return EventScriptValue.List(shuffled);
            }

            (shuffled[i], shuffled[swapIndex]) = (shuffled[swapIndex], shuffled[i]);
        }

        return EventScriptValue.List(shuffled);
    }

    private static EventScriptValue EvaluateReverseSelector(EventScriptValue target, IReadOnlyList<EventScriptValue> items)
    {
        if (target.Type is not (EventScriptValueType.List or EventScriptValueType.Dice))
        {
            return EventScriptValue.Nothing;
        }

        return EventScriptValue.List(items.Reverse().ToArray());
    }

    private List<EventScriptValue> FilterItems(ExecutionContext context, IReadOnlyList<EventScriptValue> items, string identifier, ExpressionNode predicate)
    {
        var result = new List<EventScriptValue>();
        foreach (var item in items)
        {
            context.PushScope();
            try
            {
                context.Define(identifier, item);
                if (AsBool(EvaluateExpression(context, predicate)))
                {
                    result.Add(item);
                }
            }
            finally
            {
                context.PopScope();
            }
        }

        return result;
    }

    private IReadOnlyList<EventScriptValue> ChooseWeightedItems(ExecutionContext context, IReadOnlyList<EventScriptValue> candidates, int count, string identifier, ExpressionNode weightExpression)
    {
        var remaining = candidates.ToList();
        var chosen = new List<EventScriptValue>();

        while (chosen.Count < count && remaining.Count > 0)
        {
            var weightedItems = new List<(EventScriptValue Item, decimal Weight)>();
            decimal totalWeight = 0m;

            foreach (var candidate in remaining)
            {
                var weight = EvaluateWeight(context, candidate, identifier, weightExpression);
                if (weight <= 0m)
                {
                    continue;
                }

                weightedItems.Add((candidate, weight));
                totalWeight += weight;
            }

            if (weightedItems.Count == 0 || totalWeight <= 0m)
            {
                break;
            }

            var threshold = NextRandomUnit() * totalWeight;
            decimal cumulative = 0m;
            var selected = weightedItems[^1].Item;
            foreach (var weightedItem in weightedItems)
            {
                cumulative += weightedItem.Weight;
                if (threshold < cumulative)
                {
                    selected = weightedItem.Item;
                    break;
                }
            }

            chosen.Add(selected);
            remaining.Remove(selected);
        }

        return chosen;
    }

    private decimal EvaluateWeight(ExecutionContext context, EventScriptValue item, string identifier, ExpressionNode weightExpression)
    {
        context.PushScope();
        try
        {
            context.Define(identifier, item);
            var weightValue = EvaluateExpression(context, weightExpression);
            if (!TryCoerceNumericForOperation(weightValue, out var weight) || !weight.IsFinite)
            {
                return 0m;
            }

            return weight.Value > 0m ? weight.Value : 0m;
        }
        finally
        {
            context.PopScope();
        }
    }

    private IReadOnlyList<EventScriptValue> ChooseRandomItems(IReadOnlyList<EventScriptValue> items, int count)
    {
        var pool = items.ToList();
        var result = new List<EventScriptValue>(Math.Min(count, pool.Count));
        for (var i = 0; i < count && pool.Count > 0; i++)
        {
            if (!TryNextInclusive(0, pool.Count - 1, out var index))
            {
                break;
            }

            result.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return result;
    }

    private EventScriptValue EvaluateFilterSelector(ExecutionContext context, IReadOnlyList<EventScriptValue> items, FilterSelectorNode selector)
    {
        var result = new List<EventScriptValue>();
        foreach (var item in items)
        {
            context.PushScope();
            try
            {
                context.Define(selector.Identifier, item);
                if (AsBool(EvaluateExpression(context, selector.Predicate)))
                {
                    result.Add(item);
                }
            }
            finally
            {
                context.PopScope();
            }
        }

        return EventScriptValue.List(result);
    }

    private EventScriptValue EvaluateSumSelector(ExecutionContext context, IReadOnlyList<EventScriptValue> items, SumSelectorNode selector)
    {
        var sum = NumericValue.Finite(0m);
        foreach (var item in items)
        {
            context.PushScope();
            try
            {
                context.Define(selector.Identifier, item);
                if (!TryCoerceNumericForOperation(EvaluateExpression(context, selector.Projection), out var number))
                {
                    return EventScriptValue.DecimalNaN();
                }

                sum = AddNumeric(sum, number);
            }
            finally
            {
                context.PopScope();
            }
        }

        return ToEventScriptDecimal(sum);
    }

    private EventScriptValue EvaluateAverageSelector(ExecutionContext context, IReadOnlyList<EventScriptValue> items, AverageSelectorNode selector)
    {
        if (items.Count == 0)
        {
            return EventScriptValue.Nothing;
        }

        var sum = NumericValue.Finite(0m);
        var count = 0;
        foreach (var item in items)
        {
            context.PushScope();
            try
            {
                context.Define(selector.Identifier, item);
                if (!TryCoerceNumericForOperation(EvaluateExpression(context, selector.Projection), out var number) || !number.IsFinite)
                {
                    return EventScriptValue.Nothing;
                }

                sum = AddNumeric(sum, number);
                count++;
            }
            finally
            {
                context.PopScope();
            }
        }

        return count == 0 || !sum.IsFinite
            ? EventScriptValue.Nothing
            : EventScriptValue.Decimal(sum.Value / count);
    }

    private EventScriptValue EvaluateSelectSelector(ExecutionContext context, IReadOnlyList<EventScriptValue> items, SelectSelectorNode selector)
    {
        var result = new List<EventScriptValue>(items.Count);
        foreach (var item in items)
        {
            context.PushScope();
            try
            {
                context.Define(selector.Identifier, item);
                result.Add(EvaluateExpression(context, selector.Projection));
            }
            finally
            {
                context.PopScope();
            }
        }

        return EventScriptValue.List(result);
    }

    private EventScriptValue EvaluateDictionarySelector(ExecutionContext context, IReadOnlyList<EventScriptValue> items, DictionarySelectorNode selector)
    {
        var result = new Dictionary<string, EventScriptValue>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            context.PushScope();
            try
            {
                context.Define(selector.Identifier, item);
                var key = EvaluateExpression(context, selector.KeyProjection).AsText();
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                var value = selector.ValueProjection is null
                    ? item
                    : EvaluateExpression(context, selector.ValueProjection);
                result[key] = value;
            }
            finally
            {
                context.PopScope();
            }
        }

        return EventScriptValue.Dictionary(result);
    }

    private EventScriptValue EvaluateContainsSelector(ExecutionContext context, EventScriptValue target, IReadOnlyList<EventScriptValue> items, ContainsSelectorNode selector)
    {
        var value = EvaluateExpression(context, selector.ValueExpression);
        return selector.Mode switch
        {
            "single" => EventScriptValue.Boolean(EventScriptCollectionSemantics.ContainsSingle(target, items, value)),
            "all" => EventScriptValue.Boolean(EventScriptCollectionSemantics.ContainsAll(target, items, value)),
            "any" => EventScriptValue.Boolean(EventScriptCollectionSemantics.ContainsAny(target, items, value)),
            _ => EventScriptValue.Boolean(false)
        };
    }

    private EventScriptValue EvaluateExtremaSelector(ExecutionContext context, IReadOnlyList<EventScriptValue> items, string identifier, ExpressionNode projection, bool isMax)
    {
        if (items.Count == 0)
        {
            return EventScriptValue.Nothing;
        }

        var bestItem = items[0];
        EventScriptValue? bestProjection = null;

        foreach (var item in items)
        {
            context.PushScope();
            try
            {
                context.Define(identifier, item);
                var candidateProjection = EvaluateExpression(context, projection);
                if (bestProjection is null)
                {
                    bestProjection = candidateProjection;
                    bestItem = item;
                    continue;
                }

                var comparison = EventScriptValue.StableComparer.Compare(candidateProjection, bestProjection);
                if ((isMax && comparison > 0) || (!isMax && comparison < 0))
                {
                    bestProjection = candidateProjection;
                    bestItem = item;
                }
            }
            finally
            {
                context.PopScope();
            }
        }

        return bestItem;
    }

    private EventScriptValue EvaluateSortSelector(ExecutionContext context, EventScriptValue target, IReadOnlyList<EventScriptValue> items, SortSelectorNode selector)
    {
        _ = context;
        return EventScriptCollectionSemantics.Sort(target, items, selector.Direction);
    }

    private EventScriptValue EvaluateOrderBySelector(ExecutionContext context, EventScriptValue target, IReadOnlyList<EventScriptValue> items, OrderBySelectorNode selector)
    {
        return EventScriptCollectionSemantics.OrderBy(
            target,
            items,
            selector.Direction,
            item => EvaluateSortProjection(context, item, selector.Identifier, selector.Projection));
    }

    private EventScriptValue EvaluateSortProjection(ExecutionContext context, EventScriptValue item, string identifier, ExpressionNode projection)
    {
        context.PushScope();
        try
        {
            context.Define(identifier, item);
            return EvaluateExpression(context, projection);
        }
        finally
        {
            context.PopScope();
        }
    }

    private EventScriptValue EvaluateDistinctSelector(ExecutionContext context, EventScriptValue target, IReadOnlyList<EventScriptValue> items, DistinctSelectorNode selector)
    {
        if (selector.Projection is null || string.IsNullOrEmpty(selector.Identifier))
        {
            return EventScriptCollectionSemantics.Distinct(target, items);
        }

        return EventScriptCollectionSemantics.DistinctBy(
            target,
            items,
            item => EvaluateSortProjection(context, item, selector.Identifier!, selector.Projection!));
    }

    private EventScriptValue EvaluateGroupBySelector(ExecutionContext context, IReadOnlyList<EventScriptValue> items, GroupBySelectorNode selector) => EventScriptCollectionSemantics.GroupBy(
        items, item => EvaluateSortProjection(context, item, selector.Identifier, selector.Projection));

    private bool MatchesObjectPattern(ExecutionContext context, EventScriptValue value, ObjectMatchPatternNode pattern)
    {
        if (value.Type != EventScriptValueType.Dictionary)
        {
            return false;
        }

        var dictionary = value.AsDictionary();
        foreach (var entry in pattern.Entries)
        {
            if (!dictionary.TryGetValue(entry.Key, out var actual))
            {
                return false;
            }

            switch (entry.Value)
            {
                case ObjectMatchExpressionValueNode expressionValue:
                    if (!AreEqual(actual, EvaluateExpression(context, expressionValue.Expression)))
                    {
                        return false;
                    }

                    break;

                case ObjectMatchNestedValueNode nestedValue:
                    if (!MatchesObjectPattern(context, actual, nestedValue.Pattern))
                    {
                        return false;
                    }

                    break;
            }
        }

        return true;
    }

    private static bool AreEqual(EventScriptValue left, EventScriptValue right) => left.Equals(right);

    private void ValidateDefinitionReferences(EventScriptModule eventScriptModule)
    {
        foreach (var typeDefinition in eventScriptModule.TypeDefinitions)
        {
            foreach (var field in typeDefinition.Fields)
            {
                if (field.MinimumExpression is not null)
                {
                    ValidateExpressionReferences(field.MinimumExpression);
                }

                if (field.MaximumExpression is not null)
                {
                    ValidateExpressionReferences(field.MaximumExpression);
                }

                if (field.ComputedExpression is not null)
                {
                    ValidateExpressionReferences(field.ComputedExpression);
                }
            }
        }

        foreach (var ruleDefinition in eventScriptModule.RuleDefinitions)
        {
            ValidateExpressionReferences(ruleDefinition.Expression);
        }

        foreach (var selectDefinition in eventScriptModule.SelectDefinitions)
        {
            ValidateExpressionReferences(selectDefinition.Expression);
        }

        foreach (var handler in eventScriptModule.Handlers)
        {
            foreach (var statement in handler.Statements)
            {
                ValidateStatementReferences(statement);
            }
        }
    }

    private void ValidateStatementReferences(StatementNode statement)
    {
        switch (statement)
        {
            case PublishStatementNode publish:
                foreach (var argument in publish.Arguments)
                {
                    ValidateExpressionReferences(argument.Expression);
                }

                return;

            case LetStatementNode let:
                ValidateExpressionReferences(let.Expression);
                return;

            case IfStatementNode ifStatement:
                ValidateExpressionReferences(ifStatement.Condition);
                foreach (var nested in ifStatement.ThenStatements)
                {
                    ValidateStatementReferences(nested);
                }

                foreach (var nested in ifStatement.ElseStatements)
                {
                    ValidateStatementReferences(nested);
                }

                return;

            case ForStatementNode forStatement:
                ValidateExpressionReferences(forStatement.Source);
                foreach (var nested in forStatement.Statements)
                {
                    ValidateStatementReferences(nested);
                }

                return;

            case ExpressionStatementNode expressionStatement:
                ValidateExpressionReferences(expressionStatement.Expression);
                return;
        }
    }

    private void ValidateExpressionReferences(ExpressionNode expression)
    {
        switch (expression)
        {
            case CallExpressionNode call:
                ValidateCallExpression(call);
                foreach (var argument in call.Arguments)
                {
                    ValidateExpressionReferences(argument);
                }

                return;

            case RulePredicateExpressionNode rulePredicate:
                if (!_ruleDefinitions.TryGetValue(rulePredicate.RuleName, out var ruleDefinition) ||
                    ruleDefinition.Parameters.Count != 1)
                {
                    throw new EventScriptCompilationException(
                        $"Rule '{rulePredicate.RuleName}' must exist and declare exactly one parameter to be used with 'is'");
                }

                ValidateExpressionReferences(rulePredicate.Value);
                return;

            case UnaryExpressionNode unary:
                ValidateExpressionReferences(unary.Operand);
                return;

            case VariadicTaggedExpressionNode variadic:
                foreach (var argument in variadic.Arguments)
                {
                    ValidateExpressionReferences(argument);
                }

                return;

            case ClampExpressionNode clamp:
                ValidateExpressionReferences(clamp.Value);
                ValidateExpressionReferences(clamp.Minimum);
                ValidateExpressionReferences(clamp.Maximum);
                return;

            case RandomExpressionNode random:
                ValidateExpressionReferences(random.FromExpression);
                ValidateExpressionReferences(random.ToExpression);
                return;

            case GeneratedCollectionExpressionNode generatedCollection:
                ValidateExpressionReferences(generatedCollection.FromExpression);
                ValidateExpressionReferences(generatedCollection.ToExpression);
                if (generatedCollection.StepExpression is not null)
                {
                    ValidateExpressionReferences(generatedCollection.StepExpression);
                }

                if (generatedCollection.Predicate is not null)
                {
                    ValidateExpressionReferences(generatedCollection.Predicate);
                }

                ValidateExpressionReferences(generatedCollection.Projection);
                return;

            case GuardedChoiceExpressionNode guardedChoice:
                foreach (var branch in guardedChoice.Branches)
                {
                    ValidateExpressionReferences(branch.ValueExpression);
                    ValidateExpressionReferences(branch.ConditionExpression);
                }

                ValidateExpressionReferences(guardedChoice.OtherwiseExpression);
                return;

            case BinaryExpressionNode binary:
                ValidateExpressionReferences(binary.Left);
                ValidateExpressionReferences(binary.Right);
                return;

            case TypeCheckExpressionNode typeCheck:
                ValidateExpressionReferences(typeCheck.Value);
                return;

            case TypeCastExpressionNode typeCast:
                ValidateExpressionReferences(typeCast.Value);
                return;

            case MemberAccessExpressionNode memberAccess:
                ValidateExpressionReferences(memberAccess.Target);
                return;

            case CollectionAccessExpressionNode collectionAccess:
                ValidateExpressionReferences(collectionAccess.Target);
                ValidateCollectionSelectorReferences(collectionAccess.Selector);
                return;

            case ListLiteralExpressionNode list:
                foreach (var item in list.Items)
                {
                    ValidateExpressionReferences(item);
                }

                return;

            case SetLiteralExpressionNode set:
                foreach (var item in set.Items)
                {
                    ValidateExpressionReferences(item);
                }

                return;

            case DictionaryLiteralExpressionNode dictionary:
                foreach (var entry in dictionary.Entries)
                {
                    ValidateExpressionReferences(entry.Value);
                }

                return;
        }
    }

    private void ValidateCollectionSelectorReferences(CollectionSelectorNode selector)
    {
        switch (selector)
        {
            case ExpressionSelectorNode expressionSelector:
                ValidateExpressionReferences(expressionSelector.Expression);
                return;
            case PredicateSelectorNode predicateSelector:
                ValidateExpressionReferences(predicateSelector.Predicate);
                return;
            case CountSelectorNode countSelector:
                ValidateExpressionReferences(countSelector.Predicate);
                return;
            case ChooseSelectorNode chooseSelector:
                if (chooseSelector.Predicate is not null)
                {
                    ValidateExpressionReferences(chooseSelector.Predicate);
                }

                if (chooseSelector.WeightExpression is not null)
                {
                    ValidateExpressionReferences(chooseSelector.WeightExpression);
                }

                return;
            case EdgeSelectorNode edgeSelector when edgeSelector.Predicate is not null:
                ValidateExpressionReferences(edgeSelector.Predicate);
                return;
            case FilterSelectorNode filterSelector:
                ValidateExpressionReferences(filterSelector.Predicate);
                return;
            case SumSelectorNode sumSelector:
                ValidateExpressionReferences(sumSelector.Projection);
                return;
            case AverageSelectorNode averageSelector:
                ValidateExpressionReferences(averageSelector.Projection);
                return;
            case SelectSelectorNode selectSelector:
                ValidateExpressionReferences(selectSelector.Projection);
                return;
            case DictionarySelectorNode dictionarySelector:
                ValidateExpressionReferences(dictionarySelector.KeyProjection);
                if (dictionarySelector.ValueProjection is not null)
                {
                    ValidateExpressionReferences(dictionarySelector.ValueProjection);
                }

                return;
            case MinSelectorNode minSelector:
                ValidateExpressionReferences(minSelector.Projection);
                return;
            case MaxSelectorNode maxSelector:
                ValidateExpressionReferences(maxSelector.Projection);
                return;
            case ContainsSelectorNode containsSelector:
                ValidateExpressionReferences(containsSelector.ValueExpression);
                return;
            case DistinctSelectorNode distinctSelector when distinctSelector.Projection is not null:
                ValidateExpressionReferences(distinctSelector.Projection);
                return;
            case GroupBySelectorNode groupBySelector:
                ValidateExpressionReferences(groupBySelector.Projection);
                return;
            case OrderBySelectorNode orderBySelector:
                ValidateExpressionReferences(orderBySelector.Projection);
                return;
        }
    }

    private void ValidateCallExpression(CallExpressionNode call)
    {
        if (_ruleDefinitions.TryGetValue(call.Name, out var ruleDefinition))
        {
            ValidateCallArity("Rule", call.Name, ruleDefinition.Parameters.Count, call.Arguments.Count);
            return;
        }

        if (_selectDefinitions.TryGetValue(call.Name, out var selectDefinition))
        {
            ValidateCallArity("Select", call.Name, selectDefinition.Parameters.Count, call.Arguments.Count);
            return;
        }

        throw new EventScriptCompilationException($"No rule or select named '{call.Name}' exists");
    }

    private static void ValidateCallArity(string kind, string name, int expectedCount, int actualCount)
    {
        if (expectedCount != actualCount)
        {
            throw new EventScriptCompilationException(
                $"{kind} '{name}' expects {expectedCount} argument(s) but received {actualCount}");
        }
    }

    private static bool AsBool(EventScriptValue value) => value.AsBoolean();

    private static int AsInt(EventScriptValue value)
    {
        var integer = value.AsInteger();
        if (integer < int.MinValue || integer > int.MaxValue)
        {
            return integer < 0 ? int.MinValue : int.MaxValue;
        }

        return (int)integer;
    }

    private static bool TryUnwrapOptionalForOperation(EventScriptValue value, out EventScriptValue unwrapped)
    {
        if (!value.isOptional())
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
        if (value.isNothing())
        {
            number = default;
            return false;
        }

        if (value.Type == EventScriptValueType.Decimal)
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

        if (value.Type == EventScriptValueType.Integer)
        {
            number = NumericValue.Finite(value.AsInteger());
            return true;
        }

        if (value.Type == EventScriptValueType.Percentage)
        {
            number = NumericValue.Finite(value.AsNumber());
            return true;
        }

        if (value.Type == EventScriptValueType.Dice)
        {
            number = NumericValue.Finite(value.AsDice().Sum());
            return true;
        }

        if (value.isText())
        {
            if (decimal.TryParse(value.AsText(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            {
                number = NumericValue.Finite(parsed);
                return true;
            }

            number = default;
            return false;
        }

        if (value.Type == EventScriptValueType.Boolean)
        {
            number = NumericValue.Finite(value.AsBoolean() ? 1m : 0m);
            return true;
        }

        number = default;
        return false;
    }

    private static EventScriptValue ToEventScriptDecimal(NumericValue number)
    {
        return number.Kind switch
        {
            NumericKind.Finite => EventScriptValue.Decimal(number.Value),
            NumericKind.NaN => EventScriptValue.DecimalNaN(),
            NumericKind.PositiveInfinity => EventScriptValue.DecimalInfinity(),
            NumericKind.NegativeInfinity => EventScriptValue.DecimalNegativeInfinity(),
            _ => EventScriptValue.DecimalNaN()
        };
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

    private static NumericValue SubtractNumeric(NumericValue left, NumericValue right) => AddNumeric(left, NegateNumeric(right));

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
            value = left % right;
            return true;
        }
        catch (OverflowException)
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

    private static string ToText(EventScriptValue value)
    {
        if (value.isNothing())
        {
            return string.Empty;
        }

        return value.Type switch
        {
            EventScriptValueType.Text => value.AsText(),
            EventScriptValueType.Decimal => value.ToString(),
            EventScriptValueType.Integer => value.AsInteger().ToString(CultureInfo.InvariantCulture),
            EventScriptValueType.Boolean => value.AsBoolean().ToString(),
            _ => value.ToString()
        };
    }

    private EventScriptValue ConvertToDeclaredType(EventScriptValue value, string declaredType)
    {
        switch (declaredType)
        {
            case "nothing":
                return EventScriptValue.Nothing;
            case "tag":
                return EventScriptValue.Tag(value.AsText());
            case "text":
                return EventScriptValue.Text(value.AsText());
            case "percentage":
                return ConvertToPercentage(value);
            case "boolean":
                return EventScriptValue.Boolean(value.AsBoolean());
            case "integer":
                return EventScriptValue.Integer(value.AsInteger());
            case "decimal":
            {
                if (!TryUnwrapOptionalForOperation(value, out var unwrappedNumber))
                {
                    return EventScriptValue.DecimalNaN();
                }

                if (TryCoerceNumericForOperation(unwrappedNumber, out var number))
                {
                    return ToEventScriptDecimal(number);
                }

                return EventScriptValue.DecimalNaN();
            }
            case "list":
                return EventScriptValue.List(value.AsList());
            case "dictionary":
                return EventScriptValue.Dictionary(value.AsDictionary());
            case "set":
                return EventScriptValue.Set(value.AsSet());
            case "dice":
                return EventScriptValue.Dice(value.AsDice());
            case "optional":
                if (value.isOptional()) return value;
                return value.isNothing() ? EventScriptValue.OptionalNone() : EventScriptValue.OptionalSome(value);
            default:
                return _typeDefinitions.TryGetValue(declaredType, out var typeDefinition)
                    ? ConvertToCustomType(value, typeDefinition)
                    : value;
        }
    }

    private EventScriptValue ConvertToPercentage(EventScriptValue value)
    {
        if (!TryUnwrapOptionalForOperation(value, out var unwrapped))
        {
            return EventScriptValue.DecimalNaN();
        }

        if (unwrapped.isPercentage())
        {
            return unwrapped;
        }

        if (TryCoerceNumericForOperation(unwrapped, out var number))
        {
            if (!number.IsFinite)
            {
                return EventScriptValue.DecimalNaN();
            }

            var ratio = unwrapped.Type == EventScriptValueType.Integer
                ? number.Value / 100m
                : number.Value > 1m || number.Value < -1m
                    ? number.Value / 100m
                    : number.Value;
            return EventScriptValue.Percentage(ratio);
        }

        return EventScriptValue.DecimalNaN();
    }

    private EventScriptValue ConvertToCustomType(EventScriptValue value, TypeDefinitionNode typeDefinition)
    {
        if (value.TryGetCustomTypeName(out var existingTypeName) &&
            string.Equals(existingTypeName, typeDefinition.Name, StringComparison.Ordinal))
        {
            return value;
        }

        var sourceValues = value.AsDictionary().ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var materializedValues = new Dictionary<string, EventScriptValue>(StringComparer.Ordinal);

        foreach (var field in typeDefinition.Fields.Where(field => field.ComputedExpression is null))
        {
            sourceValues.TryGetValue(field.Name, out var rawValue);
            rawValue ??= EventScriptValue.Nothing;

            var fieldValue = ConvertToDeclaredType(rawValue, field.TypeName);
            fieldValue = ApplyFieldClamp(typeDefinition, field, fieldValue, sourceValues, materializedValues);
            fieldValue = ConvertToDeclaredType(fieldValue, field.TypeName);
            materializedValues[field.Name] = fieldValue;
        }

        foreach (var field in typeDefinition.Fields.Where(field => field.ComputedExpression is not null))
        {
            var computedValue = EvaluateCustomTypeExpression(typeDefinition, field.ComputedExpression!, sourceValues, materializedValues);
            materializedValues[field.Name] = ConvertToDeclaredType(computedValue, field.TypeName);
        }

        return EventScriptValue.CustomType(typeDefinition.Name, materializedValues);
    }

    private EventScriptValue ApplyFieldClamp(TypeDefinitionNode typeDefinition, TypeFieldDefinitionNode field, EventScriptValue fieldValue, IReadOnlyDictionary<string, EventScriptValue> sourceValues, IReadOnlyDictionary<string, EventScriptValue> materializedValues)
    {
        if (field.MinimumExpression is null || field.MaximumExpression is null)
        {
            return fieldValue;
        }

        var minimum = EvaluateCustomTypeExpression(typeDefinition, field.MinimumExpression, sourceValues, materializedValues);
        var maximum = EvaluateCustomTypeExpression(typeDefinition, field.MaximumExpression, sourceValues, materializedValues);
        if (!TryCoerceNumericForOperation(fieldValue, out var valueNumber) ||
            !TryCoerceNumericForOperation(minimum, out var minimumNumber) ||
            !TryCoerceNumericForOperation(maximum, out var maximumNumber))
        {
            return fieldValue;
        }

        if (!valueNumber.IsFinite || !minimumNumber.IsFinite || !maximumNumber.IsFinite)
        {
            if (maximumNumber.IsPositiveInfinity && minimumNumber.IsFinite && valueNumber.IsFinite)
            {
                return EventScriptValue.Decimal(Math.Max(valueNumber.Value, minimumNumber.Value));
            }

            return fieldValue;
        }

        var lower = Math.Min(minimumNumber.Value, maximumNumber.Value);
        var upper = Math.Max(minimumNumber.Value, maximumNumber.Value);
        return EventScriptValue.Decimal(Math.Min(Math.Max(valueNumber.Value, lower), upper));
    }

    private EventScriptValue EvaluateCustomTypeExpression(TypeDefinitionNode typeDefinition, ExpressionNode expression, IReadOnlyDictionary<string, EventScriptValue> sourceValues, IReadOnlyDictionary<string, EventScriptValue> materializedValues)
    {
        var state = new RunState();
        var context = new ExecutionContext(state);
        context.PushScope();
        try
        {
            foreach (var pair in sourceValues)
            {
                context.Define(pair.Key, pair.Value);
            }

            foreach (var pair in materializedValues)
            {
                context.Define(pair.Key, pair.Value);
            }

            return EvaluateExpression(context, expression);
        }
        finally
        {
            context.PopScope();
        }
    }

    private bool IsValueOfType(EventScriptValue value, string typeName)
    {
        return typeName switch
        {
            "nothing" => value.isNothing(),
            "tag" => value.isTag(),
            "text" => value.isText(),
            "percentage" => value.isPercentage(),
            "decimal" => value.isNumber(),
            "integer" => value.isInteger(),
            "boolean" => value.Type == EventScriptValueType.Boolean,
            "optional" => value.isOptional(),
            "list" => value.isList(),
            "dictionary" => value.isDictionary(),
            "set" => value.isSet(),
            "dice" => value.isDice(),
            _ => value.TryGetCustomTypeName(out var customTypeName) && string.Equals(customTypeName, typeName, StringComparison.Ordinal)
        };
    }

    private static long ToIntegerSaturated(decimal number)
    {
        var truncated = decimal.Truncate(number);
        if (truncated > long.MaxValue) return long.MaxValue;
        if (truncated < long.MinValue) return long.MinValue;
        return (long)truncated;
    }

    private bool TryNextInclusive(int minInclusive, int maxInclusive, out int value)
    {
        try
        {
            value = _random.NextInclusive(minInclusive, maxInclusive);
            return true;
        }
        catch
        {
            value = default;
            return false;
        }
    }

    private decimal NextRandomUnit()
    {
        return TryNextInclusive(0, RandomUnitMax - 1, out var value)
            ? value / RandomUnitScale
            : 0m;
    }

    private void ValidateEmitArguments(string message, IEnumerable<string> argumentNames)
    {
        _ = message;
        _ = argumentNames;
    }

    private sealed class ExecutionContext
    {
        private readonly Stack<Dictionary<string, EventScriptValue>> _scopes = new();
        private readonly RunState _state;

        public ExecutionContext(RunState state)
        {
            _state = state;
            _scopes.Push(new Dictionary<string, EventScriptValue>(StringComparer.Ordinal));
        }

        public void PushScope() => _scopes.Push(new Dictionary<string, EventScriptValue>(StringComparer.Ordinal));

        public void PopScope()
        {
            if (_scopes.Count == 1)
            {
                return;
            }

            _scopes.Pop();
        }

        public void Define(string name, EventScriptValue value)
        {
            _scopes.Peek()[name] = value;
            _state.RecordDiagnostic(
                EventScriptDiagnosticStepKind.VariableAssigned,
                name,
                SingleArgument(value),
                $"Assigned '{name}' = {value}");
        }

        public EventScriptValue Resolve(string name)
        {
            foreach (var scope in _scopes)
            {
                if (scope.TryGetValue(name, out var value))
                {
                    _state.RecordDiagnostic(
                        EventScriptDiagnosticStepKind.VariableResolved,
                        name,
                        SingleArgument(value),
                        $"Resolved '{name}'");
                    return value;
                }
            }

            _state.RecordDiagnostic(
                EventScriptDiagnosticStepKind.VariableResolved,
                name,
                SingleArgument(EventScriptValue.Nothing),
                $"Resolved '{name}' to nothing");
            return EventScriptValue.Nothing;
        }

        public void Publish(string message, IReadOnlyDictionary<string, EventScriptValue> arguments)
        {
            var emittedEvent = new EventScriptEmittedEvent(message, EventScriptNamedArguments.Create(arguments));
            _state.RecordPublishedEvent(emittedEvent);
            _state.RecordDiagnostic(
                EventScriptDiagnosticStepKind.EventPublished,
                message,
                emittedEvent.Arguments,
                $"Published '{message}'");
        }

        public void RecordStatement(StatementNode statement)
            => _state.RecordDiagnostic(
                EventScriptDiagnosticStepKind.StatementExecuting,
                statement.GetType().Name,
                EventScriptArgumentMap.Empty,
                $"Executing {statement.GetType().Name}");

        public void RecordExpression(ExpressionNode expression, EventScriptValue value)
            => _state.RecordDiagnostic(
                EventScriptDiagnosticStepKind.ExpressionEvaluated,
                expression.GetType().Name,
                SingleArgument(value),
                value.isNothing()
                    ? $"Expression {expression.GetType().Name} evaluated to nothing"
                    : $"Expression {expression.GetType().Name} evaluated");

        private static IReadOnlyDictionary<string, EventScriptValue> SingleArgument(EventScriptValue value)
            => new Dictionary<string, EventScriptValue>(StringComparer.Ordinal) { ["value"] = value };

        public IReadOnlyDictionary<string, EventScriptValue> SnapshotTopScope()
            => new Dictionary<string, EventScriptValue>(_scopes.Peek(), StringComparer.Ordinal);
    }

    private sealed class RunState
    {
        private readonly Dictionary<string, EventScriptValue> _variables = new(StringComparer.Ordinal);
        private readonly bool _diagnosticsEnabled;
        private int _diagnosticSequence;

        public RunState(bool diagnosticsEnabled = false)
        {
            _diagnosticsEnabled = diagnosticsEnabled;
        }

        public IReadOnlyDictionary<string, EventScriptValue> Variables => _variables;

        public List<EventScriptEmittedEvent> EmittedEvents { get; } = new();

        public List<EventScriptDiagnosticStep> Steps { get; } = new();

        public void RecordPublishedEvent(EventScriptEmittedEvent emittedEvent)
        {
            EmittedEvents.Add(emittedEvent);
        }

        public void RecordDiagnostic(EventScriptDiagnosticStepKind kind, string message, IReadOnlyDictionary<string, EventScriptValue> arguments, string? detail = null)
        {
            if (!_diagnosticsEnabled)
            {
                return;
            }

            Steps.Add(new EventScriptDiagnosticStep(++_diagnosticSequence, kind, message, EventScriptNamedArguments.Create(arguments), detail));
        }

        public void CaptureVariables(IReadOnlyDictionary<string, EventScriptValue> variables)
        {
            foreach (var pair in variables)
            {
                _variables[pair.Key] = pair.Value;
            }
        }
    }
}
