#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StepH.Flow.EventScript.Parser;
using StepH.Flow.EventScript.Runtime;
using StepH.Flow.EventScript.Semantics;
using StepH.Flow.EventScript.Types;
using NumericKind = StepH.Flow.EventScript.Runtime.EventScriptValueAlu.NumericKind;
using NumericValue = StepH.Flow.EventScript.Runtime.EventScriptValueAlu.NumericValue;

namespace StepH.Flow.EventScript.Interpreter;

internal static class EventScriptInvocationEngine
{
    public static void InvokeMessage(CompiledEventScript compiledScript, EventScriptContext context, EventScriptMessage message)
        => new InvocationSession(compiledScript, context).InvokeMessage(message);

    public static void InvokeHandler(
        CompiledEventScript compiledScript,
        EventScriptContext context,
        CompiledEventScriptHandler handler,
        IReadOnlyDictionary<string, EventScriptValue> args)
        => new InvocationSession(compiledScript, context).InvokeHandler(handler, args);

    private sealed class InvocationSession
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyList<CompiledEventScriptHandler>> _handlers;
        private readonly IReadOnlyDictionary<string, CompiledTypeDefinition> _typeDefinitions;
        private readonly IReadOnlyDictionary<string, CompiledCallableDefinition> _callables;
        private readonly EventScriptContext _context;
        private readonly Stack<EventScriptRandomGenerator> _randomScopes = new();
        private readonly bool _diagnosticsEnabled;

        internal InvocationSession(CompiledEventScript compiledScript, EventScriptContext context)
        {
            _ = compiledScript ?? throw new ArgumentNullException(nameof(compiledScript));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            var randomGenerator = _context.Random;
            _typeDefinitions = compiledScript.TypeDefinitions;
            _callables = compiledScript.Callables;
            _handlers = compiledScript.Handlers;
            _diagnosticsEnabled = compiledScript.Options.EnableDiagnostics;
            _randomScopes.Push(randomGenerator);
        }

        public void InvokeMessage(EventScriptMessage message)
        {
            if (string.IsNullOrWhiteSpace(message.Name)) return;
            foreach (var handler in GetMatchingHandlers(message))
            {
                var executionContext = new ExecutionContext(_context, _diagnosticsEnabled);
                ExecuteHandler(executionContext, handler, message.Arguments);
            }
        }

        public void InvokeHandler(CompiledEventScriptHandler handler, IReadOnlyDictionary<string, EventScriptValue> args)
        {
            _ = handler ?? throw new ArgumentNullException(nameof(handler));
            args = EventScriptNamedArguments.Normalize(args);
            var context = new ExecutionContext(_context, _diagnosticsEnabled);
            ExecuteHandler(context, handler, args);
        }

        private IReadOnlyList<CompiledEventScriptHandler> GetMatchingHandlers(EventScriptMessage message)
            => EventScriptInvocationKernel.GetMatchingHandlers(_handlers, message, handler => handler.SignatureId, handler => handler.DeclarationOrder);

        private void ExecuteHandler(ExecutionContext context, CompiledEventScriptHandler handler, IReadOnlyDictionary<string, EventScriptValue> args)
        {
            context.PushScope();
            try
            {
                foreach (var parameter in handler.Parameters)
                {
                    if (handler.DiagnosticsEnabled)
                    {
                        context.RecordDiagnostic(
                            EventScriptDiagnosticEventKind.ParameterBound,
                            parameter,
                            new Dictionary<string, EventScriptValue>(StringComparer.Ordinal) { [parameter] = args[parameter] },
                            $"Bound '{parameter}'");
                    }

                    context.Define(parameter, args[parameter]);
                }

                if (handler.DiagnosticsEnabled)
                {
                    context.RecordDiagnostic(
                        EventScriptDiagnosticEventKind.HandlerInvoked,
                        handler.Message,
                        args,
                        $"Handler '{handler.Message}' invoked");
                }

                ExecuteStatements(context, handler.Statements);
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
            switch (statement)
            {
                case PublishStatementNode publish:
                    var publishValue = EvaluateExpression(context, publish.MessageExpression);
                    if (EventScriptMessageValueCodec.TryReadMessageValue(publishValue, out var message))
                    {
                        EmitMessage(context, message);
                    }

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
                    if (AsBool(EvaluateExpression(context, ifStatement.Condition)))
                    {
                        ExecuteStatementBody(context, ifStatement.ThenBody);
                    }
                    else if (ifStatement.ElseBody is not null)
                    {
                        ExecuteStatementBody(context, ifStatement.ElseBody);
                    }

                    return;

                case ForStatementNode forStatement:
                    foreach (var item in EnumerateIterationSource(context, forStatement.Source))
                    {
                        context.PushScope();
                        try
                        {
                            context.Define(forStatement.Identifier, item);
                            ExecuteStatementBody(context, forStatement.Body);
                        }
                        finally
                        {
                            context.PopScope();
                        }
                    }

                    return;

                case SeededRandomStatementNode seededRandom:
                    ExecuteSeededRandomStatement(context, seededRandom);
                    return;

                case ExpressionStatementNode expressionStatement:
                    EvaluateExpression(context, expressionStatement.Expression);
                    return;

                default:
                    return;
            }
        }

        private static void EmitMessage(ExecutionContext context, EventScriptMessage message)
        {
            context.Publish(message.Name, message.Arguments);
        }

        private void ExecuteStatementBody(ExecutionContext context, StatementBodyNode body)
        {
            if (body.IsBlock)
            {
                context.PushScope();
                try
                {
                    ExecuteStatements(context, body.Statements);
                }
                finally
                {
                    context.PopScope();
                }

                return;
            }

            ExecuteStatements(context, body.Statements);
        }

        private EventScriptValue EvaluateExpression(ExecutionContext context, ExpressionNode expression) => EvaluateExpressionCore(context, expression);

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

                case HandlerLiteralExpressionNode handlerLiteral:
                    return EvaluateHandlerLiteralExpression(handlerLiteral);

                case MessageLiteralExpressionNode messageLiteral:
                    return EvaluateMessageLiteralExpression(context, messageLiteral);

                case HandlerBindExpressionNode handlerBind:
                    return EvaluateHandlerBindExpression(context, handlerBind);

                case CallExpressionNode call:
                    return EvaluateCallExpression(context, call);

                case UnaryExpressionNode unary:
                    return EvaluateUnaryExpression(context, unary);

                case VariadicTaggedExpressionNode variadic:
                    return EvaluateVariadicTaggedExpression(context, variadic);

                case ClampExpressionNode clamp:
                    return EvaluateClampExpression(context, clamp);

                case RangeExpressionNode rangeExpression:
                    return EvaluateRangeExpression(context, rangeExpression);

                case RandomExpressionNode randomExpression:
                    return EvaluateRandomExpression(context, randomExpression);

                case SeededRandomExpressionNode seededRandomExpression:
                    return EvaluateSeededRandomExpression(context, seededRandomExpression);

                case DiceExpressionNode diceExpression:
                    return EvaluateDiceExpression(context, diceExpression);

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

                if (!TryNextInclusiveInt(from, to, out var next))
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

            return TryNextInclusiveDecimal(lower, upper, out var nextDecimal)
                ? EventScriptValue.Decimal(nextDecimal)
                : EventScriptValue.Nothing;
        }

        private EventScriptValue EvaluateRangeExpression(ExecutionContext context, RangeExpressionNode rangeExpression)
        {
            return TryEvaluateRangeExpression(context, rangeExpression, out var range)
                ? range
                : EventScriptValue.Nothing;
        }

        private EventScriptValue EvaluateSeededRandomExpression(ExecutionContext context, SeededRandomExpressionNode seededRandomExpression)
        {
            var seedValue = EvaluateExpression(context, seededRandomExpression.SeedExpression);
            PushSeededRandomScope(seedValue);
            try
            {
                return EvaluateExpression(context, seededRandomExpression.BodyExpression);
            }
            finally
            {
                PopSeededRandomScope();
            }
        }

        private void ExecuteSeededRandomStatement(ExecutionContext context, SeededRandomStatementNode seededRandomStatement)
        {
            var seedValue = EvaluateExpression(context, seededRandomStatement.SeedExpression);
            PushSeededRandomScope(seedValue);
            try
            {
                ExecuteStatementBody(context, seededRandomStatement.Body);
            }
            finally
            {
                PopSeededRandomScope();
            }
        }

        private EventScriptValue EvaluateDiceExpression(ExecutionContext context, DiceExpressionNode diceExpression)
        {
            if (diceExpression.DiceCount <= 0 || diceExpression.SideCount <= 0)
            {
                return EventScriptValue.Dice(EventScriptDiceValue.Create(Array.Empty<int>()));
            }

            var rolls = new int[diceExpression.DiceCount];
            for (var i = 0; i < rolls.Length; i++)
            {
                if (!TryNextInclusiveInt(1, diceExpression.SideCount, out var roll))
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
            var values = new List<EventScriptValue>();
            foreach (var item in EnumerateIterationSource(context, generatedCollection.Source))
            {
                if (!TryProjectGeneratedItem(context, generatedCollection, item, values))
                {
                    break;
                }
            }

            return generatedCollection.CollectionType == "set"
                ? EventScriptValue.Set(values)
                : EventScriptValue.List(values);
        }

        private EventScriptValue EvaluateCallExpression(ExecutionContext context, CallExpressionNode call)
        {
            var arguments = call.Arguments.Select(argument => EvaluateExpression(context, argument)).ToArray();

            if (_callables.TryGetValue(call.Name, out var callable))
            {
                var result = EvaluateCallableDefinition(context, callable, arguments);
                return callable.Kind == CallableKind.Rule
                    ? EventScriptValue.Boolean(AsBool(result))
                    : result;
            }

            return EventScriptValue.Nothing;
        }

        private static EventScriptValue EvaluateHandlerLiteralExpression(HandlerLiteralExpressionNode handlerLiteral)
            => EventScriptMessageValueCodec.CreateHandlerValue(new EventScriptMessageSignature(handlerLiteral.Message, handlerLiteral.Parameters));

        private EventScriptValue EvaluateMessageLiteralExpression(ExecutionContext context, MessageLiteralExpressionNode messageLiteral)
        {
            var arguments = new Dictionary<string, EventScriptValue>(StringComparer.Ordinal);
            foreach (var argument in messageLiteral.Arguments)
            {
                arguments[argument.Name] = EvaluateExpression(context, argument.Expression);
            }

            var message = new EventScriptMessage(messageLiteral.Message, arguments);
            return EventScriptMessageValueCodec.CreateMessageValue(message);
        }

        private EventScriptValue EvaluateHandlerBindExpression(ExecutionContext context, HandlerBindExpressionNode handlerBind)
        {
            var handlerValue = EvaluateExpression(context, handlerBind.CalleeExpression);
            var arguments = new Dictionary<string, EventScriptValue>(StringComparer.Ordinal);
            foreach (var argument in handlerBind.Arguments)
            {
                arguments[argument.Name] = EvaluateExpression(context, argument.Expression);
            }

            if (!EventScriptMessageValueCodec.TryBindHandlerValue(handlerValue, arguments, out var message))
            {
                return EventScriptValue.Nothing;
            }

            return EventScriptMessageValueCodec.CreateMessageValue(message);
        }

        private EventScriptValue EvaluateRulePredicateExpression(ExecutionContext context, RulePredicateExpressionNode rulePredicate)
        {
            if (!_callables.TryGetValue(rulePredicate.RuleName, out var callable) ||
                callable.Kind != CallableKind.Rule ||
                callable.Parameters.Count != 1)
            {
                return EventScriptValue.Nothing;
            }

            var value = EvaluateExpression(context, rulePredicate.Value);
            var result = EvaluateCallableDefinition(context, callable, new[] { value });
            return EventScriptValue.Boolean(AsBool(result));
        }

        private EventScriptValue EvaluateCallableDefinition(ExecutionContext context, CompiledCallableDefinition definition, IReadOnlyList<EventScriptValue> arguments)
        {
            context.PushScope();
            try
            {
                if (definition.DiagnosticsEnabled)
                {
                    context.RecordDiagnostic(
                        definition.Kind == CallableKind.Rule ? EventScriptDiagnosticEventKind.RuleCalled : EventScriptDiagnosticEventKind.SelectCalled,
                        definition.Name,
                        BuildOrderedArgumentMap(definition.Parameters, arguments),
                        $"{definition.Kind.ToString().ToLowerInvariant()} '{definition.Name}' called");
                }

                for (var i = 0; i < definition.Parameters.Count; i++)
                {
                    context.Define(definition.Parameters[i], i < arguments.Count ? arguments[i] : EventScriptValue.Nothing);
                }

                return EvaluateExpression(context, definition.Expression);
            }
            finally
            {
                context.PopScope();
            }
        }

        private static IReadOnlyDictionary<string, EventScriptValue> BuildOrderedArgumentMap(IReadOnlyList<string> parameterNames, IReadOnlyList<EventScriptValue> arguments)
        {
            var map = new Dictionary<string, EventScriptValue>(StringComparer.Ordinal);
            for (var i = 0; i < parameterNames.Count; i++)
            {
                map[parameterNames[i]] = i < arguments.Count ? arguments[i] : EventScriptValue.Nothing;
            }

            return map;
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

        private IEnumerable<EventScriptValue> EnumerateIterationSource(ExecutionContext context, IterationSourceNode source)
        {
            switch (source)
            {
                case CollectionIterationSourceNode collectionSource:
                    foreach (var item in EvaluateExpression(context, collectionSource.Expression).AsEnumerable())
                    {
                        yield return item;
                    }

                    yield break;

                case RangeIterationSourceNode rangeSource:
                    if (!TryEvaluateRangeExpression(context, rangeSource.RangeExpression, out var rangeValue))
                    {
                        yield break;
                    }

                    foreach (var item in rangeValue.AsEnumerable())
                    {
                        yield return item;
                    }

                    yield break;
            }
        }

        private bool TryEvaluateRangeExpression(ExecutionContext context, RangeExpressionNode rangeExpression, out EventScriptValue range)
        {
            var fromValue = EvaluateExpression(context, rangeExpression.FromExpression);
            var toValue = EvaluateExpression(context, rangeExpression.ToExpression);
            var stepValue = rangeExpression.StepExpression is null
                ? EventScriptValue.Integer(1)
                : EvaluateExpression(context, rangeExpression.StepExpression);

            if (!TryCoerceNumericForOperation(fromValue, out var fromNumber) ||
                !TryCoerceNumericForOperation(toValue, out var toNumber) ||
                !TryCoerceNumericForOperation(stepValue, out var stepNumber) ||
                !fromNumber.IsFinite ||
                !toNumber.IsFinite ||
                !stepNumber.IsFinite)
            {
                range = EventScriptValue.Nothing;
                return false;
            }

            range = EventScriptValue.Range(
                ToIntegerSaturated(fromNumber.Value),
                ToIntegerSaturated(toNumber.Value),
                ToIntegerSaturated(stepNumber.Value));
            return true;
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
                EventScriptValueType.Range => EventScriptValue.Integer(operand.AsEnumerable().LongCount()),
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

            return TryNextInclusiveDecimal(0m, 1m, out var randomValue)
                ? EventScriptValue.Boolean(randomValue < ratio)
                : EventScriptValue.Boolean(false);
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
            => EventScriptValueAlu.EvaluateMinMax(values, isMax);

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
            => EventScriptValueAlu.TryCombineWithPlus(left, right, out value);

        private static EventScriptValue EvaluateCollectionCombine(EventScriptValue left, EventScriptValue right)
            => EventScriptValueAlu.EvaluateCollectionCombine(left, right);

        private static EventScriptValue EvaluateCollectionIntersect(EventScriptValue left, EventScriptValue right)
            => EventScriptValueAlu.EvaluateCollectionIntersect(left, right);

        private static EventScriptValue EvaluateCollectionExcept(EventScriptValue left, EventScriptValue right)
            => EventScriptValueAlu.EvaluateCollectionExcept(left, right);

        private static EventScriptValue EvaluateCollectionZip(EventScriptValue left, EventScriptValue right)
            => EventScriptValueAlu.EvaluateCollectionZip(left, right);

        private static EventScriptValue EvaluateDictionaryCombine(EventScriptValue left, EventScriptValue right)
            => EventScriptValueAlu.EvaluateDictionaryCombine(left, right);

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

            if (target.TryGetDictionaryMember(memberAccess.Member, out var value))
            {
                return value;
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

        private bool TryTakeCountPattern(ExecutionContext context, IReadOnlyList<EventScriptValue> items, IReadOnlyDictionary<EventScriptValue, int> counts, DiceCountPatternNode pattern,
            out IReadOnlyList<EventScriptValue> takenItems)
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
                if (!TryNextInclusiveInt(0, i, out var swapIndex))
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

                if (!TryNextInclusiveDecimal(0m, totalWeight, out var threshold))
                {
                    break;
                }

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
                if (!TryNextInclusiveInt(0, pool.Count - 1, out var index))
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

        private static bool AreEqual(EventScriptValue left, EventScriptValue right) => EventScriptValueAlu.AreEqual(left, right);

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
            => EventScriptValueAlu.TryUnwrapOptionalForOperation(value, out unwrapped);

        private static bool TryCoerceNumericForOperation(EventScriptValue value, out NumericValue number)
            => EventScriptValueAlu.TryCoerceNumericForOperation(value, out number);

        private static EventScriptValue ToEventScriptDecimal(NumericValue number)
            => EventScriptValueAlu.ToEventScriptDecimal(number);

        private static bool TryCompareNumeric(NumericValue left, NumericValue right, out int comparison)
            => EventScriptValueAlu.TryCompareNumeric(left, right, out comparison);

        private static NumericValue AddNumeric(NumericValue left, NumericValue right) => EventScriptValueAlu.AddNumeric(left, right);

        private static NumericValue SubtractNumeric(NumericValue left, NumericValue right) => EventScriptValueAlu.SubtractNumeric(left, right);

        private static NumericValue MultiplyNumeric(NumericValue left, NumericValue right) => EventScriptValueAlu.MultiplyNumeric(left, right);

        private static NumericValue DivideNumeric(NumericValue left, NumericValue right) => EventScriptValueAlu.DivideNumeric(left, right);

        private static NumericValue ModuloNumeric(NumericValue left, NumericValue right) => EventScriptValueAlu.ModuloNumeric(left, right);

        private static NumericValue NegateNumeric(NumericValue value) => EventScriptValueAlu.NegateNumeric(value);

        private static int SignOf(NumericValue value) => EventScriptValueAlu.SignOf(value);

        private static bool IsZero(NumericValue value) => EventScriptValueAlu.IsZero(value);

        private static bool TryAddFinite(decimal left, decimal right, out decimal value)
            => EventScriptValueAlu.TryAddFinite(left, right, out value);

        private static bool TryMultiplyFinite(decimal left, decimal right, out decimal value)
            => EventScriptValueAlu.TryMultiplyFinite(left, right, out value);

        private static bool TryDivideFinite(decimal left, decimal right, out decimal value)
            => EventScriptValueAlu.TryDivideFinite(left, right, out value);

        private static bool TryModuloFinite(decimal left, decimal right, out decimal value)
            => EventScriptValueAlu.TryModuloFinite(left, right, out value);

        private static bool TryNegateFinite(decimal input, out decimal value)
            => EventScriptValueAlu.TryNegateFinite(input, out value);

        private static string ToText(EventScriptValue value)
            => EventScriptValueAlu.ToText(value);

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
                case "range":
                    return value.isRange() ? value : EventScriptValue.Nothing;
                case "message":
                    return value.Type == EventScriptValueType.Message
                        ? value
                        : EventScriptMessageValueCodec.TryReadMessageValue(value, out var messageValue)
                            ? EventScriptMessageValueCodec.CreateMessageValue(messageValue)
                            : EventScriptValue.Nothing;
                case "handler":
                    return value.Type == EventScriptValueType.Handler
                        ? value
                        : EventScriptMessageValueCodec.TryReadHandlerValue(value, out var handlerValue)
                            ? EventScriptMessageValueCodec.CreateHandlerValue(handlerValue)
                            : EventScriptValue.Nothing;
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

        private EventScriptValue ConvertToCustomType(EventScriptValue value, CompiledTypeDefinition typeDefinition)
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

        private EventScriptValue ApplyFieldClamp(CompiledTypeDefinition typeDefinition, CompiledTypeFieldDefinition field, EventScriptValue fieldValue,
            IReadOnlyDictionary<string, EventScriptValue> sourceValues, IReadOnlyDictionary<string, EventScriptValue> materializedValues)
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

        private EventScriptValue EvaluateCustomTypeExpression(CompiledTypeDefinition typeDefinition, ExpressionNode expression, IReadOnlyDictionary<string, EventScriptValue> sourceValues,
            IReadOnlyDictionary<string, EventScriptValue> materializedValues)
        {
            var context = new ExecutionContext(_context, _diagnosticsEnabled);
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
                "range" => value.isRange(),
                "message" => value.Type == EventScriptValueType.Message || EventScriptMessageValueCodec.TryReadMessageValue(value, out _),
                "handler" => value.Type == EventScriptValueType.Handler || EventScriptMessageValueCodec.TryReadHandlerValue(value, out _),
                "dictionary" => value.isDictionary(),
                "set" => value.isSet(),
                "dice" => value.isDice(),
                _ => value.TryGetCustomTypeName(out var customTypeName) && string.Equals(customTypeName, typeName, StringComparison.Ordinal)
            };
        }

        private static long ToIntegerSaturated(decimal number)
            => EventScriptValueAlu.ToIntegerSaturated(number);

        private bool TryNextInclusiveInt(int minInclusive, int maxInclusive, out int value)
        {
            try
            {
                value = _randomScopes.Peek().NextInclusiveInt(minInclusive, maxInclusive);
                return true;
            }
            catch
            {
                value = default;
                return false;
            }
        }

        private bool TryNextInclusiveDecimal(decimal minInclusive, decimal maxInclusive, out decimal value)
        {
            try
            {
                value = _randomScopes.Peek().NextInclusiveDecimal(minInclusive, maxInclusive);
                return true;
            }
            catch
            {
                value = default;
                return false;
            }
        }

        private void PushSeededRandomScope(EventScriptValue seedValue)
            => _randomScopes.Push(EventScriptRandomGenerator.FromSeed(DeriveStableSeed(seedValue)));

        private void PopSeededRandomScope()
        {
            if (_randomScopes.Count > 1)
            {
                _randomScopes.Pop();
            }
        }

        private static int DeriveStableSeed(EventScriptValue value)
        {
            var canonical = BuildStableSeedText(value);
            unchecked
            {
                uint hash = 2166136261;
                foreach (var ch in canonical)
                {
                    hash ^= ch;
                    hash *= 16777619;
                }

                return (int)hash;
            }
        }

        private static string BuildStableSeedText(EventScriptValue value)
        {
            return value.Type switch
            {
                EventScriptValueType.Nothing => "nothing",
                EventScriptValueType.Tag => $"tag:{value.AsText()}",
                EventScriptValueType.Text => $"text:{value.AsText()}",
                EventScriptValueType.Percentage => $"percentage:{value.AsNumber().ToString(CultureInfo.InvariantCulture)}",
                EventScriptValueType.Decimal => value.IsNaN()
                    ? "decimal:nan"
                    : value.IsNegativeInfinity()
                        ? "decimal:-infinity"
                        : value.IsInfinity()
                            ? "decimal:infinity"
                            : $"decimal:{value.AsNumber().ToString(CultureInfo.InvariantCulture)}",
                EventScriptValueType.Integer => $"integer:{value.AsInteger().ToString(CultureInfo.InvariantCulture)}",
                EventScriptValueType.Boolean => $"boolean:{(value.AsBoolean() ? "true" : "false")}",
                EventScriptValueType.Optional => value.AsOptional().HasValue
                    ? $"optional:{BuildStableSeedText(value.AsOptional().Value)}"
                    : "optional:none",
                EventScriptValueType.Iterator => $"iterator:[{string.Join("|", value.AsEnumerable().Select(BuildStableSeedText))}]",
                EventScriptValueType.Range => $"range:{((EventScriptRangeValue)value).From}:{((EventScriptRangeValue)value).To}:{((EventScriptRangeValue)value).Step}",
                EventScriptValueType.Message =>
                    $"message:{((EventScriptMessageValue)value).Value.SignatureId}:[{string.Join("|", ((EventScriptMessageValue)value).Value.Arguments.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={BuildStableSeedText(pair.Value)}"))}]",
                EventScriptValueType.Handler => $"handler:{((EventScriptHandlerValue)value).Signature.SignatureId}",
                EventScriptValueType.List => $"list:[{string.Join("|", value.AsList().Select(BuildStableSeedText))}]",
                EventScriptValueType.Dictionary =>
                    $"dict:[{string.Join("|", value.AsDictionary().OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={BuildStableSeedText(pair.Value)}"))}]",
                EventScriptValueType.Set => $"set:[{string.Join("|", value.AsSet().OrderBy(item => item, EventScriptValue.StableComparer).Select(BuildStableSeedText))}]",
                EventScriptValueType.Dice => $"dice:[{string.Join("|", value.AsDice().Rolls)}]",
                _ => value.ToString()
            };
        }

        private sealed class ExecutionContext
        {
            private readonly Stack<Dictionary<string, EventScriptValue>> _scopes = new();
            private readonly EventScriptContext _context;
            private readonly bool _diagnosticsEnabled;

            public ExecutionContext(EventScriptContext context, bool diagnosticsEnabled)
            {
                _context = context;
                _diagnosticsEnabled = diagnosticsEnabled;
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
                => _scopes.Peek()[name] = value;

            public EventScriptValue Resolve(string name)
            {
                foreach (var scope in _scopes)
                {
                    if (scope.TryGetValue(name, out var value))
                    {
                        return value;
                    }
                }

                return EventScriptValue.Nothing;
            }

            public void Publish(string message, IReadOnlyDictionary<string, EventScriptValue> arguments)
            {
                var publishedMessage = new EventScriptMessage(message, arguments);
                _context.Publish(publishedMessage);
                EventScriptInvocationKernel.RecordDiagnostic(
                    _context,
                    _diagnosticsEnabled,
                    EventScriptDiagnosticEventKind.EventPublished,
                    publishedMessage.Name,
                    publishedMessage.Arguments,
                    $"Published '{publishedMessage.Name}'");
            }

            public void RecordDiagnostic(EventScriptDiagnosticEventKind kind, string name, IReadOnlyDictionary<string, EventScriptValue> arguments, string? detail = null)
                => EventScriptInvocationKernel.RecordDiagnostic(_context, _diagnosticsEnabled, kind, name, arguments, detail);
        }
    }
}
