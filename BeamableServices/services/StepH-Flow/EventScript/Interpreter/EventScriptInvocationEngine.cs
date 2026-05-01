#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StepH.Flow.EventScript.Parser;
using StepH.Flow.EventScript.Runtime;
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
        private readonly IReadOnlyDictionary<string, IReadOnlyList<CompiledEventScriptHandler>> _dispatchIndex;
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
            _dispatchIndex = compiledScript.DispatchIndex;
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
            args = args is EventScriptNamedArguments ? args : EventScriptNamedArguments.Create(args);
            var context = new ExecutionContext(_context, _diagnosticsEnabled);
            ExecuteHandler(context, handler, args);
        }

        private IReadOnlyList<CompiledEventScriptHandler> GetMatchingHandlers(EventScriptMessage message)
            => EventScriptInvocationKernel.GetMatchingHandlers(_dispatchIndex, message);

        private void ExecuteHandler(ExecutionContext context, CompiledEventScriptHandler handler, IReadOnlyDictionary<string, EventScriptValue> args)
        {
            context.PushScope();
            try
            {
                for (var parameterIndex = 0; parameterIndex < handler.Parameters.Count; parameterIndex++)
                {
                    var parameter = handler.Parameters[parameterIndex];
                    var parameterValue = TryGetArgumentValue(args, parameter, parameterIndex, out var value)
                        ? value
                        : EventScriptValue.Nothing;

                    if (handler.DiagnosticsEnabled)
                    {
                        context.RecordDiagnostic(
                            EventScriptDiagnosticEventKind.ParameterBound,
                            parameter,
                            new Dictionary<string, EventScriptValue>(StringComparer.Ordinal) { [parameter] = parameterValue },
                            $"Bound '{parameter}'");
                    }

                    context.Define(parameter, parameterValue);
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

        private static bool TryGetArgumentValue(IReadOnlyDictionary<string, EventScriptValue> args, string parameter, int parameterIndex, out EventScriptValue value)
        {
            if (args is EventScriptNamedArguments namedArguments && parameterIndex >= 0 && parameterIndex < namedArguments.Count)
            {
                value = namedArguments[parameterIndex];
                return true;
            }

            return args.TryGetValue(parameter, out value!);
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
            if (!context.TryConsumeExecutionStep("Statement execution budget exhausted."))
            {
                return;
            }

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
                        letValue = ConvertToDeclaredType(context, letValue, let.DeclaredType!);
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
                        if (!context.TryConsumeLoopIteration("Loop iteration budget exhausted."))
                        {
                            break;
                        }

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
            if (!context.TryConsumeExecutionStep("Expression evaluation budget exhausted."))
            {
                return EventScriptValue.Nothing;
            }

            switch (expression)
            {
                case IntegerLiteralExpressionNode integer:
                    return EventScriptValueFactory.Integer(integer.Value);
                case DecimalLiteralExpressionNode number:
                    return EventScriptValueFactory.Decimal(number.Value);
                case PercentageLiteralExpressionNode percentage:
                    return EventScriptValueFactory.Percentage(percentage.PercentValue / 100m);
                case UnitDecimalLiteralExpressionNode unitDecimal:
                    return EventScriptDecimalUnits.TryParseTypeName(unitDecimal.UnitName, out var unit)
                        ? EventScriptValueFactory.Decimal(unitDecimal.Value, unit)
                        : EventScriptValueFactory.DecimalNaN();

                case TextLiteralExpressionNode text:
                    return EventScriptValueFactory.Text(text.Value);

                case TagLiteralExpressionNode tag:
                    return EventScriptValueFactory.Tag(tag.Name);

                case ListLiteralExpressionNode listLiteral:
                    return EventScriptValueFactory.List(listLiteral.Items.Select(item => EvaluateExpression(context, item)));

                case SetLiteralExpressionNode setLiteral:
                    return EventScriptValueFactory.Set(setLiteral.Items.Select(item => EvaluateExpression(context, item)));

                case DictionaryLiteralExpressionNode dictionaryLiteral:
                    return EvaluateDictionaryLiteral(context, dictionaryLiteral);

                case BooleanLiteralExpressionNode boolean:
                    return EventScriptValueFactory.Boolean(boolean.Value);

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

                case ExtensionCallExpressionNode extensionCall:
                    return EvaluateExtensionCallExpression(context, extensionCall);

                case TypeConstructorExpressionNode typeConstructor:
                    return EvaluateTypeConstructorExpression(context, typeConstructor);

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

                case ExtensionPredicateExpressionNode extensionPredicate:
                    return EvaluateExtensionPredicateExpression(context, extensionPredicate);

                case TypeCheckExpressionNode typeCheck:
                    return EventScriptValueFactory.Boolean(IsValueOfType(EvaluateExpression(context, typeCheck.Value), typeCheck.TypeName));
                case TypeCastExpressionNode typeCast:
                    return ConvertToDeclaredType(context, EvaluateExpression(context, typeCast.Value), typeCast.TypeName);

                case MemberAccessExpressionNode memberAccess:
                    return EvaluateMemberAccess(context, memberAccess);

                case CollectionAccessExpressionNode collectionAccess:
                    return EvaluateCollectionAccess(context, collectionAccess);

                case SequenceLiteralExpressionNode sequence:
                    return EventScriptValueFactory.Sequence(sequence.Items.Select(item => EvaluateExpression(context, item)));

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
                unwrappedFrom.Kind == EventScriptValueKind.Integer &&
                unwrappedTo.Kind == EventScriptValueKind.Integer)
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

                return EventScriptValueFactory.Integer(next);
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
                return EventScriptValueFactory.Decimal(lower);
            }

            return TryNextInclusiveDecimal(lower, upper, out var nextDecimal)
                ? EventScriptValueFactory.Decimal(nextDecimal)
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
                return EventScriptValueFactory.Dice(EventScriptDiceValue.Empty);
            }

            if (!context.TryCheckDice(diceExpression))
            {
                return EventScriptValueFactory.Dice(EventScriptDiceValue.Empty);
            }

            var rolls = new int[diceExpression.DiceCount];
            for (var i = 0; i < rolls.Length; i++)
            {
                if (!TryNextInclusiveInt(1, diceExpression.SideCount, out var roll))
                {
                    return EventScriptValueFactory.Dice(EventScriptDiceValue.Empty);
                }

                rolls[i] = roll;
            }

            var dice = EventScriptDiceValue.EventScriptDice(rolls);
            return EventScriptValueFactory.Dice(dice);
        }

        private EventScriptValue EvaluateDictionaryLiteral(ExecutionContext context, DictionaryLiteralExpressionNode dictionaryLiteral)
        {
            var map = new Dictionary<string, EventScriptValue>(StringComparer.Ordinal);
            foreach (var entry in dictionaryLiteral.Entries)
            {
                map[entry.Key] = EvaluateExpression(context, entry.Value);
            }

            return EventScriptValueFactory.Dictionary(map);
        }

        private EventScriptValue EvaluateGeneratedCollectionExpression(ExecutionContext context, GeneratedCollectionExpressionNode generatedCollection)
        {
            var values = new List<EventScriptValue>();
            foreach (var item in EnumerateIterationSource(context, generatedCollection.Source))
            {
                if (!context.TryConsumeLoopIteration("Generated collection iteration budget exhausted."))
                {
                    break;
                }

                if (!TryProjectGeneratedItem(context, generatedCollection, item, values))
                {
                    break;
                }
            }

            return generatedCollection.CollectionType == "set"
                ? EventScriptValueFactory.Set(values)
                : EventScriptValueFactory.List(values);
        }

        private EventScriptValue EvaluateCallExpression(ExecutionContext context, CallExpressionNode call)
        {
            var arguments = call.ArgumentList.Arguments.Select(argument => EvaluateExpression(context, argument.Expression)).ToArray();

            if (_callables.TryGetValue(call.Name, out var callable))
            {
                if (!ArgumentsMatch(call.ArgumentList.Arguments, callable.SignatureLabels))
                {
                    return EventScriptValue.Nothing;
                }

                var result = EvaluateCallableDefinition(context, callable, arguments);
                return callable.Kind == CallableKind.Rule
                    ? EventScriptValueFactory.Boolean(AsBool(result))
                    : result;
            }

            var handlerValue = context.Resolve(call.Name);
            if (!handlerValue.IsNothing())
            {
                var namedArguments = EvaluateArgumentList(context, call.ArgumentList);
                if (EventScriptMessageValueCodec.TryBindHandlerValue(handlerValue, namedArguments, out var message))
                {
                    return EventScriptMessageValueCodec.CreateMessageValue(message);
                }
            }

            return EventScriptValue.Nothing;
        }

        private static EventScriptValue EvaluateHandlerLiteralExpression(HandlerLiteralExpressionNode handlerLiteral)
            => EventScriptMessageValueCodec.CreateHandlerValue(new EventScriptMessageSignature(handlerLiteral.Message, handlerLiteral.SignatureLabels));

        private EventScriptValue EvaluateMessageLiteralExpression(ExecutionContext context, MessageLiteralExpressionNode messageLiteral)
        {
            var message = new EventScriptMessage(messageLiteral.Message, EvaluateArgumentList(context, messageLiteral.ArgumentList));
            return EventScriptMessageValueCodec.CreateMessageValue(message);
        }

        private EventScriptValue EvaluateHandlerBindExpression(ExecutionContext context, HandlerBindExpressionNode handlerBind)
        {
            var handlerValue = EvaluateExpression(context, handlerBind.CalleeExpression);
            var arguments = EvaluateArgumentList(context, handlerBind.ArgumentList);

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
            return EventScriptValueFactory.Boolean(AsBool(result));
        }

        private EventScriptValue EvaluateExtensionCallExpression(ExecutionContext context, ExtensionCallExpressionNode extensionCall)
        {
            var arguments = extensionCall.Arguments
                .Select(argument => EventScriptFastValue.FromEventScriptValue(EvaluateExpression(context, argument.Expression)))
                .ToArray();
            var reference = new EventScriptExtensionReference(
                extensionCall.ExtensionName,
                extensionCall.FunctionName,
                extensionCall.Arguments.Select(argument => argument.Name).ToArray());
            return _context.ExtensionRegistry.TryResolve(reference, out var function)
                ? function.Invoke(new EventScriptExtensionContext(_context), arguments).ToEventScriptValue()
                : EventScriptValue.Nothing;
        }

        private EventScriptValue EvaluateExtensionPredicateExpression(ExecutionContext context, ExtensionPredicateExpressionNode extensionPredicate)
        {
            var reference = new EventScriptExtensionReference(
                extensionPredicate.ExtensionName,
                extensionPredicate.FunctionName,
                [EventScriptMessageSignature.UnlabeledParameterName]);
            if (!_context.ExtensionRegistry.TryResolve(reference, out var function))
            {
                return EventScriptValue.Nothing;
            }

            var input = EventScriptFastValue.FromEventScriptValue(EvaluateExpression(context, extensionPredicate.Value));
            return EventScriptValueFactory.Boolean(function.Invoke(new EventScriptExtensionContext(_context), [input]).ToEventScriptValue().AsBoolean());
        }

        private EventScriptNamedArguments EvaluateArgumentList(ExecutionContext context, ArgumentListNode argumentList)
        {
            if (argumentList.Count == 0)
            {
                return EventScriptNamedArguments.Empty;
            }

            var pairs = new KeyValuePair<string, EventScriptValue>[argumentList.Count];
            var labels = new string[argumentList.Count];
            for (var index = 0; index < argumentList.Count; index++)
            {
                var argument = argumentList.Arguments[index];
                labels[index] = argument.Name;
                pairs[index] = new KeyValuePair<string, EventScriptValue>(
                    argument.Name,
                    EvaluateExpression(context, argument.Expression));
            }

            return EventScriptNamedArguments.CreateOrdered(pairs, labels);
        }

        private static bool ArgumentsMatch(IReadOnlyList<ArgumentNode> arguments, IReadOnlyList<string> signatureLabels)
        {
            if (arguments.Count != signatureLabels.Count)
            {
                return false;
            }

            for (var index = 0; index < arguments.Count; index++)
            {
                if (!string.Equals(arguments[index].Name, signatureLabels[index], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private EventScriptValue EvaluateCallableDefinition(ExecutionContext context, CompiledCallableDefinition definition, IReadOnlyList<EventScriptValue> arguments)
        {
            if (!context.TryEnterCall($"Callable '{definition.Name}' exceeded the configured call depth."))
            {
                return EventScriptValue.Nothing;
            }

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
                context.ExitCall();
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

                if (!context.TryCheckGeneratedCollectionItemCount(values.Count + 1, "Generated collection item count exceeds the configured limit."))
                {
                    return false;
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
                    var collection = EvaluateExpression(context, collectionSource.Expression);
                    if (!context.TryCheckMaterializedValue(collection, "Iteration source would enumerate more range items than allowed."))
                    {
                        yield break;
                    }

                    foreach (var item in collection.AsEnumerable())
                    {
                        yield return item;
                    }

                    yield break;

                case RangeIterationSourceNode rangeSource:
                    if (!TryEvaluateRangeExpression(context, rangeSource.RangeExpression, out var rangeValue))
                    {
                        yield break;
                    }

                    if (!context.TryCheckRangeLength(rangeValue, "Range item count exceeds the configured limit."))
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
                ? EventScriptValueFactory.Integer(1)
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

            range = EventScriptValueFactory.Range(
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
                "has value" => EventScriptValueFactory.Boolean(operand.HasSemanticValue()),
                "empty" => EventScriptValueFactory.Boolean(operand.IsSemanticallyEmpty()),
                "len" => EvaluateLenUnary(context, operand),
                "chance" => EvaluateChanceUnary(operand),
                "keys" => EventScriptValueFactory.Keys(operand),
                "values" => EventScriptValueFactory.Values(operand),
                "entries" => EventScriptValueFactory.Entries(operand),
                "abs" => EvaluateAbsUnary(operand),
                "floor" => EvaluateRoundingUnary(operand, "floor"),
                "ceil" => EvaluateRoundingUnary(operand, "ceil"),
                "round" => EvaluateRoundingUnary(operand, "round"),
                "rounddown" => EvaluateRoundingUnary(operand, "rounddown"),
                "roundup" => EvaluateRoundingUnary(operand, "roundup"),
                "roundeven" => EvaluateRoundingUnary(operand, "roundeven"),
                "wrapDegree" => EventScriptValueAlu.EvaluateWrapDegree(operand),
                _ => EventScriptValue.Nothing
            };
        }

        private static EventScriptValue EvaluateNegateUnary(EventScriptValue operand)
        {
            if (operand.IsNothing())
            {
                return EventScriptValue.Nothing;
            }

            if (!TryUnwrapOptionalForOperation(operand, out var unwrapped))
            {
                return EventScriptValueFactory.OptionalNone();
            }

            if (unwrapped.IsPercentage())
            {
                return EventScriptValueFactory.Percentage(-unwrapped.AsNumber());
            }

            if (EventScriptValueAlu.TryEvaluateVectorUnary(unwrapped, "-", out var vectorNegation))
            {
                return vectorNegation;
            }

            if (EventScriptValue.TryGetDecimalUnit(unwrapped, out var unit))
            {
                return EventScriptValueFactory.Decimal(-unwrapped.AsNumber(), unit);
            }

            if (!TryCoerceNumericForOperation(unwrapped, out var number))
            {
                return EventScriptValue.Nothing;
            }

            return number.Kind switch
            {
                NumericKind.Finite => EventScriptValueFactory.Decimal(-number.Value),
                NumericKind.NaN => EventScriptValueFactory.DecimalNaN(),
                NumericKind.PositiveInfinity => EventScriptValueFactory.DecimalNegativeInfinity(),
                NumericKind.NegativeInfinity => EventScriptValueFactory.DecimalInfinity(),
                _ => EventScriptValueFactory.DecimalNaN()
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

            if (!EventScriptValueAlu.HaveCompatibleNumericUnits(raw, minimum) ||
                !EventScriptValueAlu.HaveCompatibleNumericUnits(raw, maximum) ||
                !EventScriptValueAlu.HaveCompatibleNumericUnits(minimum, maximum))
            {
                return EventScriptValueFactory.DecimalNaN();
            }

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
            EventScriptValue.TryGetDecimalUnit(raw, out var unit);
            return EventScriptValueFactory.Decimal(Math.Min(Math.Max(rawNumber.Value, lower), upper), raw.HasDecimalUnit() ? unit : null);
        }

        private EventScriptValue EvaluateNotUnary(EventScriptValue operand)
        {
            if (operand.IsNothing())
            {
                return EventScriptValue.Nothing;
            }

            if (!TryUnwrapOptionalForOperation(operand, out var unwrapped))
            {
                return EventScriptValueFactory.OptionalNone();
            }

            return EventScriptValueFactory.Boolean(!AsBool(unwrapped));
        }

        private EventScriptValue EvaluateLenUnary(ExecutionContext context, EventScriptValue operand)
        {
            if (operand.IsNothing())
            {
                return EventScriptValueFactory.Integer(0);
            }

            return operand.Kind switch
            {
                EventScriptValueKind.Text => EventScriptValueFactory.Integer(operand.AsText().Length),
                EventScriptValueKind.Sequence => CountEnumerableWithBudget(context, operand.AsEnumerable(), "Sequence length evaluation budget exhausted."),
                EventScriptValueKind.Range => EvaluateRangeLength(context, operand),
                EventScriptValueKind.List => EventScriptValueFactory.Integer(operand.AsList().Count),
                EventScriptValueKind.Dictionary => EventScriptValueFactory.Integer(operand.AsDictionary().Count),
                EventScriptValueKind.Set => EventScriptValueFactory.Integer(operand.AsSet().Count),
                EventScriptValueKind.Dice => EventScriptValueFactory.Integer(operand.AsDice().Rolls.Count),
                EventScriptValueKind.Optional => EventScriptValueFactory.Integer(operand.AsOptional().HasValue ? 1 : 0),
                _ => EventScriptValue.Nothing
            };
        }

        private EventScriptValue EvaluateRangeLength(ExecutionContext context, EventScriptValue operand)
        {
            if (!EventScriptRuntimeLimitUtilities.TryGetRangeLength(operand, out var length))
            {
                return EventScriptValue.Nothing;
            }

            return context.TryCheckRangeLength(operand, "Range length exceeds the configured limit.")
                ? EventScriptValueFactory.Integer(length)
                : EventScriptValue.Nothing;
        }

        private EventScriptValue CountEnumerableWithBudget(ExecutionContext context, IEnumerable<EventScriptValue> values, string detail)
        {
            long count = 0;
            foreach (var _ in values)
            {
                if (!context.TryConsumeLoopIteration(detail))
                {
                    return EventScriptValue.Nothing;
                }

                count++;
            }

            return EventScriptValueFactory.Integer(count);
        }

        private EventScriptValue EvaluateChanceUnary(EventScriptValue operand)
        {
            var percentage = ConvertToPercentage(operand);
            if (!percentage.IsPercentage())
            {
                return EventScriptValueFactory.Boolean(false);
            }

            var ratio = percentage.AsNumber();
            if (ratio <= 0m)
            {
                return EventScriptValueFactory.Boolean(false);
            }

            if (ratio >= 1m)
            {
                return EventScriptValueFactory.Boolean(true);
            }

            return TryNextInclusiveDecimal(0m, 1m, out var randomValue)
                ? EventScriptValueFactory.Boolean(randomValue < ratio)
                : EventScriptValueFactory.Boolean(false);
        }

        private static EventScriptValue EvaluateRoundingUnary(EventScriptValue operand, string operation)
        {
            if (operand.IsNothing())
            {
                return EventScriptValue.Nothing;
            }

            if (EventScriptValueAlu.TryEvaluateUnitRounding(operand, operation, out var degree))
            {
                return degree;
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
                return EventScriptValueFactory.Integer(long.MaxValue);
            }

            if (number.IsNegativeInfinity)
            {
                return EventScriptValueFactory.Integer(long.MinValue);
            }

            return operation switch
            {
                "floor" or "rounddown" => EventScriptValueFactory.Integer(ToIntegerSaturated(Math.Floor(number.Value))),
                "ceil" or "roundup" => EventScriptValueFactory.Integer(ToIntegerSaturated(Math.Ceiling(number.Value))),
                "round" or "roundeven" => EventScriptValueFactory.Integer(ToIntegerSaturated(Math.Round(number.Value, 0, MidpointRounding.ToEven))),
                _ => EventScriptValue.Nothing
            };
        }

        private static EventScriptValue EvaluateAbsUnary(EventScriptValue operand)
        {
            if (operand.IsNothing())
            {
                return EventScriptValue.Nothing;
            }

            if (EventScriptValueAlu.TryEvaluateVectorUnary(operand, "abs", out var vectorLength))
            {
                return vectorLength;
            }

            if (!TryCoerceNumericForOperation(operand, out var number) || !number.IsFinite)
            {
                return EventScriptValue.Nothing;
            }

            return EventScriptValueFactory.Decimal(Math.Abs(number.Value));
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

        private static bool IsPatternSequence(EventScriptValue value) => value.Kind is EventScriptValueKind.List or EventScriptValueKind.Dice;

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
                if (!leftRaw.HasSemanticValue())
                {
                    return rightRaw;
                }

                if (leftRaw.IsOptional())
                {
                    var optional = leftRaw.AsOptional();
                    return optional.HasValue ? optional.Value : rightRaw;
                }

                return leftRaw;
            }

            if (leftRaw.IsNothing() || rightRaw.IsNothing())
            {
                return EventScriptValue.Nothing;
            }

            if (!TryUnwrapOptionalForOperation(leftRaw, out var left) || !TryUnwrapOptionalForOperation(rightRaw, out var right))
            {
                return EventScriptValueFactory.OptionalNone();
            }

            switch (binary.Operator)
            {
                case "|":
                    return EventScriptValueFactory.Boolean(AsBool(left) || AsBool(right));
                case "^":
                    return EventScriptValueFactory.Boolean(AsBool(left) ^ AsBool(right));
                case "&":
                    return EventScriptValueFactory.Boolean(AsBool(left) && AsBool(right));
                case "default":
                    return EventScriptValue.Nothing;
                case "=":
                    return EventScriptValueFactory.Boolean(AreEqual(left, right));
                case "<>":
                    return EventScriptValueFactory.Boolean(!AreEqual(left, right));
                case "in":
                    return EventScriptValueFactory.Boolean(right.Contains(left));
                case "value in":
                    return EventScriptValueFactory.Boolean(right.ContainsValue(left));
                case "starts with":
                    return EventScriptValueFactory.Boolean(left.StartsWith(right));
                case "ends with":
                    return EventScriptValueFactory.Boolean(left.EndsWith(right));
                case "<":
                    if (!TryCompareNumericValues(left, right, out var lessComparison))
                    {
                        return EventScriptValueFactory.Boolean(false);
                    }

                    return EventScriptValueFactory.Boolean(lessComparison < 0);
                case ">":
                    if (!TryCompareNumericValues(left, right, out var greaterComparison))
                    {
                        return EventScriptValueFactory.Boolean(false);
                    }

                    return EventScriptValueFactory.Boolean(greaterComparison > 0);
                case "<=":
                    if (!TryCompareNumericValues(left, right, out var lessOrEqualComparison))
                    {
                        return EventScriptValueFactory.Boolean(false);
                    }

                    return EventScriptValueFactory.Boolean(lessOrEqualComparison <= 0);
                case ">=":
                    if (!TryCompareNumericValues(left, right, out var greaterOrEqualComparison))
                    {
                        return EventScriptValueFactory.Boolean(false);
                    }

                    return EventScriptValueFactory.Boolean(greaterOrEqualComparison >= 0);
                case "+":
                {
                    if (EventScriptValueAlu.TryEvaluateVectorBinary(left, "+", right, out var vectorSum))
                    {
                        return vectorSum;
                    }

                    if (EventScriptValueAlu.TryEvaluatePercentageBinary(left, "+", right, out var percentage))
                    {
                        return percentage;
                    }

                    if (EventScriptValueAlu.TryEvaluateUnitBinary(left, "+", right, out var degree))
                    {
                        return degree;
                    }

                    if (TryCoerceNumericForOperation(left, out var leftNumeric) &&
                        TryCoerceNumericForOperation(right, out var rightNumeric))
                    {
                        return EventScriptValueAlu.ToEventScriptNumericResult(left, "+", right, AddNumeric(leftNumeric, rightNumeric));
                    }

                    if (TryCombineWithPlus(left, right, out var combined))
                    {
                        return combined;
                    }

                    if (left.IsText() || right.IsText())
                    {
                        return EventScriptValueFactory.Text($"{ToText(left)}{ToText(right)}");
                    }

                    return EventScriptValueFactory.DecimalNaN();
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
                    if (EventScriptValueAlu.TryEvaluateVectorBinary(left, "-", right, out var vectorDifference))
                    {
                        return vectorDifference;
                    }

                    if (EventScriptValueAlu.TryEvaluatePercentageBinary(left, "-", right, out var percentageDifference))
                    {
                        return percentageDifference;
                    }

                    if (EventScriptValueAlu.TryEvaluateUnitBinary(left, "-", right, out var degreeDifference))
                    {
                        return degreeDifference;
                    }

                    if (!TryCoerceNumericForOperation(left, out var leftMinus) ||
                        !TryCoerceNumericForOperation(right, out var rightMinus))
                    {
                        return EventScriptValueFactory.DecimalNaN();
                    }

                    return EventScriptValueAlu.ToEventScriptNumericResult(left, "-", right, SubtractNumeric(leftMinus, rightMinus));
                case "*":
                    if (EventScriptValueAlu.TryEvaluateVectorBinary(left, "*", right, out var vectorProduct))
                    {
                        return vectorProduct;
                    }

                    if (EventScriptValueAlu.TryEvaluatePercentageBinary(left, "*", right, out var percentageProduct))
                    {
                        return percentageProduct;
                    }

                    if (EventScriptValueAlu.TryEvaluateUnitBinary(left, "*", right, out var degreeProduct))
                    {
                        return degreeProduct;
                    }

                    if (!TryCoerceNumericForOperation(left, out var leftMultiply) ||
                        !TryCoerceNumericForOperation(right, out var rightMultiply))
                    {
                        return EventScriptValueFactory.DecimalNaN();
                    }

                    return EventScriptValueAlu.ToEventScriptNumericResult(left, "*", right, MultiplyNumeric(leftMultiply, rightMultiply));
                case "/":
                    if (EventScriptValueAlu.TryEvaluateVectorBinary(left, "/", right, out var vectorQuotient))
                    {
                        return vectorQuotient;
                    }

                    if (EventScriptValueAlu.TryEvaluatePercentageBinary(left, "/", right, out var percentageQuotient))
                    {
                        return percentageQuotient;
                    }

                    if (EventScriptValueAlu.TryEvaluateUnitBinary(left, "/", right, out var degreeQuotient))
                    {
                        return degreeQuotient;
                    }

                    if (!TryCoerceNumericForOperation(left, out var leftDivide) ||
                        !TryCoerceNumericForOperation(right, out var rightDivide))
                    {
                        return EventScriptValueFactory.DecimalNaN();
                    }

                    return ToEventScriptDecimal(DivideNumeric(leftDivide, rightDivide));
                case "mod":
                    if (EventScriptValueAlu.TryEvaluateVectorBinary(left, "mod", right, out var vectorModulo))
                    {
                        return vectorModulo;
                    }

                    if (EventScriptValueAlu.TryEvaluateUnitBinary(left, "mod", right, out var degreeModulo))
                    {
                        return degreeModulo;
                    }

                    if (!TryCoerceNumericForOperation(left, out var leftModulo) ||
                        !TryCoerceNumericForOperation(right, out var rightModulo))
                    {
                        return EventScriptValueFactory.DecimalNaN();
                    }

                    return EventScriptValueAlu.ToEventScriptNumericResult(left, "mod", right, ModuloNumeric(leftModulo, rightModulo));
                case "div":
                    if (EventScriptValueAlu.TryEvaluateVectorBinary(left, "div", right, out var vectorIntegerDivide))
                    {
                        return vectorIntegerDivide;
                    }

                    if (EventScriptValueAlu.TryEvaluateUnitBinary(left, "div", right, out var unitIntegerDivide))
                    {
                        return unitIntegerDivide;
                    }

                    if (!TryCoerceNumericForOperation(left, out var leftIntegerDivide) ||
                        !TryCoerceNumericForOperation(right, out var rightIntegerDivide))
                    {
                        return EventScriptValueFactory.DecimalNaN();
                    }

                    return EventScriptValueAlu.ToEventScriptNumericResult(left, "div", right, IntegerDivideNumeric(leftIntegerDivide, rightIntegerDivide));
                case "rem":
                    if (EventScriptValueAlu.TryEvaluateVectorBinary(left, "rem", right, out var vectorRemainder))
                    {
                        return vectorRemainder;
                    }

                    if (EventScriptValueAlu.TryEvaluateUnitBinary(left, "rem", right, out var unitRemainder))
                    {
                        return unitRemainder;
                    }

                    if (!TryCoerceNumericForOperation(left, out var leftRemainder) ||
                        !TryCoerceNumericForOperation(right, out var rightRemainder))
                    {
                        return EventScriptValueFactory.DecimalNaN();
                    }

                    return EventScriptValueAlu.ToEventScriptNumericResult(left, "rem", right, RemainderNumeric(leftRemainder, rightRemainder));
                default:
                    return EventScriptValue.Nothing;
            }
        }

        private EventScriptValue EvaluateMemberAccess(ExecutionContext context, MemberAccessExpressionNode memberAccess)
        {
            var target = EvaluateExpression(context, memberAccess.Target);
            if (target.IsNothing())
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
            if (TryEvaluatePipelinedCollectionAccess(context, collectionAccess, out var pipelinedValue))
            {
                return pipelinedValue;
            }

            var target = EvaluateExpression(context, collectionAccess.Target);
            if (target.IsNothing())
            {
                return EventScriptValue.Nothing;
            }

            switch (collectionAccess.Selector)
            {
                case ExpressionSelectorNode expressionSelector:
                    return EvaluateIndexedCollectionAccess(context, target, expressionSelector.Expression);

                case PatternSelectorNode patternSelector:
                    return EventScriptValueFactory.Boolean(EvaluateSequencePattern(context, target, patternSelector.Pattern));

                case ObjectMatchSelectorNode objectMatchSelector:
                    return EventScriptValueFactory.Boolean(EvaluateObjectMatchSelector(context, target, objectMatchSelector.Pattern));

                case TakePatternSelectorNode takePatternSelector:
                    return EvaluateTakePattern(context, target, takePatternSelector.Pattern);

                case SequenceSliceSelectorNode sliceSelector:
                    if (!TryMaterializeCollectionAccessTarget(context, target, out var sliceItems))
                    {
                        return EventScriptValue.Nothing;
                    }

                    var items = sliceItems;
                    return EvaluateSequenceSliceSelector(target, items, sliceSelector);

                case PredicateSelectorNode predicateSelector:
                    return TryEnumerateCollectionAccessTarget(context, target, out var predicateItems)
                        ? EvaluatePredicateSelector(context, predicateItems, predicateSelector)
                        : EventScriptValue.Nothing;

                case CountSelectorNode countSelector:
                    return TryEnumerateCollectionAccessTarget(context, target, out var countItems)
                        ? EvaluateCountSelector(context, countItems, countSelector)
                        : EventScriptValue.Nothing;

                case ChooseSelectorNode chooseSelector:
                    return TryMaterializeCollectionAccessTarget(context, target, out var chooseItems)
                        ? EvaluateChooseSelector(context, chooseItems, chooseSelector)
                        : EventScriptValue.Nothing;

                case DrawSelectorNode drawSelector:
                    return TryMaterializeCollectionAccessTarget(context, target, out var drawItems)
                        ? EvaluateDrawSelector(target, drawItems, drawSelector)
                        : EventScriptValue.Nothing;

                case ShuffleSelectorNode:
                    return TryMaterializeCollectionAccessTarget(context, target, out var shuffleItems)
                        ? EvaluateShuffleSelector(target, shuffleItems)
                        : EventScriptValue.Nothing;

                case ReverseSelectorNode:
                    return TryMaterializeCollectionAccessTarget(context, target, out var reverseItems)
                        ? EvaluateReverseSelector(target, reverseItems)
                        : EventScriptValue.Nothing;

                case EdgeSelectorNode edgeSelector:
                    return TryEnumerateCollectionAccessTarget(context, target, out var edgeItems)
                        ? EvaluateEdgeSelector(context, edgeItems, edgeSelector)
                        : EventScriptValue.Nothing;

                case FilterSelectorNode filterSelector:
                    return TryEnumerateCollectionAccessTarget(context, target, out var filterItems)
                        ? EvaluateFilterSelector(context, filterItems, filterSelector)
                        : EventScriptValue.Nothing;

                case SumSelectorNode sumSelector:
                    return TryEnumerateCollectionAccessTarget(context, target, out var sumItems)
                        ? EvaluateSumSelector(context, sumItems, sumSelector)
                        : EventScriptValue.Nothing;

                case AverageSelectorNode averageSelector:
                    return TryEnumerateCollectionAccessTarget(context, target, out var averageItems)
                        ? EvaluateAverageSelector(context, averageItems, averageSelector)
                        : EventScriptValue.Nothing;

                case MinSelectorNode minSelector:
                    return TryEnumerateCollectionAccessTarget(context, target, out var minItems)
                        ? EvaluateExtremaSelector(context, minItems, minSelector.Identifier, minSelector.Projection, isMax: false)
                        : EventScriptValue.Nothing;

                case MaxSelectorNode maxSelector:
                    return TryEnumerateCollectionAccessTarget(context, target, out var maxItems)
                        ? EvaluateExtremaSelector(context, maxItems, maxSelector.Identifier, maxSelector.Projection, isMax: true)
                        : EventScriptValue.Nothing;

                case SelectSelectorNode selectSelector:
                    return TryEnumerateCollectionAccessTarget(context, target, out var selectItems)
                        ? EvaluateSelectSelector(context, selectItems, selectSelector)
                        : EventScriptValue.Nothing;

                case DictionarySelectorNode dictionarySelector:
                    return TryEnumerateCollectionAccessTarget(context, target, out var dictionaryItems)
                        ? EvaluateDictionarySelector(context, dictionaryItems, dictionarySelector)
                        : EventScriptValue.Nothing;

                case ContainsSelectorNode containsSelector:
                    return EvaluateContainsSelector(context, target, containsSelector);

                case SortSelectorNode sortSelector:
                    return TryEnumerateCollectionAccessTarget(context, target, out var sortItems)
                        ? EvaluateSortSelector(context, target, sortItems, sortSelector)
                        : EventScriptValue.Nothing;

                case DistinctSelectorNode distinctSelector:
                    return TryEnumerateCollectionAccessTarget(context, target, out var distinctItems)
                        ? EvaluateDistinctSelector(context, target, distinctItems, distinctSelector)
                        : EventScriptValue.Nothing;

                case GroupBySelectorNode groupBySelector:
                    return TryEnumerateCollectionAccessTarget(context, target, out var groupItems)
                        ? EvaluateGroupBySelector(context, groupItems, groupBySelector)
                        : EventScriptValue.Nothing;

                case OrderBySelectorNode orderBySelector:
                    return TryEnumerateCollectionAccessTarget(context, target, out var orderItems)
                        ? EvaluateOrderBySelector(context, target, orderItems, orderBySelector)
                        : EventScriptValue.Nothing;

                default:
                    return EventScriptValue.Nothing;
            }
        }

        private bool TryEvaluatePipelinedCollectionAccess(ExecutionContext context, CollectionAccessExpressionNode collectionAccess, out EventScriptValue value)
        {
            if (!EventScriptCollectionPipeline.TryCreate(collectionAccess, out var pipeline))
            {
                value = EventScriptValue.Nothing;
                return false;
            }

            var sourceTarget = EvaluateExpression(context, pipeline.SourceExpression);
            if (sourceTarget.IsNothing())
            {
                value = EventScriptValue.Nothing;
                return true;
            }

            if (!TryEnumerateCollectionAccessTarget(context, sourceTarget, out var sourceItems))
            {
                value = EventScriptValue.Nothing;
                return true;
            }

            var projection = CreateProjectionEvaluator(context);
            var items = pipeline.ApplyPrefix(sourceItems, projection);
            value = EvaluatePipelinedTerminalSelector(context, items, pipeline.TerminalSelector, projection);
            return true;
        }

        private EventScriptValue EvaluatePipelinedTerminalSelector(
            ExecutionContext context,
            IEnumerable<EventScriptValue> items,
            CollectionSelectorNode selector,
            EventScriptProjectionEvaluator projection)
        {
            var listTarget = EventScriptListValue.Empty;
            switch (selector)
            {
                case PredicateSelectorNode predicateSelector:
                    return EvaluatePredicateSelector(context, items, predicateSelector);
                case CountSelectorNode countSelector:
                    return EvaluateCountSelector(context, items, countSelector);
                case EdgeSelectorNode edgeSelector:
                    return EvaluateEdgeSelector(context, items, edgeSelector);
                case FilterSelectorNode:
                case SelectSelectorNode:
                    return EventScriptValueFactory.List(EventScriptCollectionPipeline.ApplyComposableSelector(items, selector, projection));
                case SumSelectorNode sumSelector:
                    return EvaluateSumSelector(context, items, sumSelector);
                case AverageSelectorNode averageSelector:
                    return EvaluateAverageSelector(context, items, averageSelector);
                case MinSelectorNode minSelector:
                    return EvaluateExtremaSelector(context, items, minSelector.Identifier, minSelector.Projection, isMax: false);
                case MaxSelectorNode maxSelector:
                    return EvaluateExtremaSelector(context, items, maxSelector.Identifier, maxSelector.Projection, isMax: true);
                case DictionarySelectorNode dictionarySelector:
                    return EvaluateDictionarySelector(context, items, dictionarySelector);
                case SortSelectorNode sortSelector:
                    return EvaluateSortSelector(context, listTarget, items, sortSelector);
                case DistinctSelectorNode distinctSelector:
                    return EvaluateDistinctSelector(context, listTarget, items, distinctSelector);
                case GroupBySelectorNode groupBySelector:
                    return EvaluateGroupBySelector(context, items, groupBySelector);
                case OrderBySelectorNode orderBySelector:
                    return EvaluateOrderBySelector(context, listTarget, items, orderBySelector);
                case ChooseSelectorNode chooseSelector:
                    return EvaluateChooseSelector(context, MaterializePipelineItems(items), chooseSelector);
                case DrawSelectorNode drawSelector:
                    return EvaluateDrawSelector(listTarget, MaterializePipelineItems(items), drawSelector);
                case ShuffleSelectorNode:
                    return EvaluateShuffleSelector(listTarget, MaterializePipelineItems(items));
                case ReverseSelectorNode:
                    return EvaluateReverseSelector(listTarget, MaterializePipelineItems(items));
                case SequenceSliceSelectorNode sliceSelector:
                    return EvaluateSequenceSliceSelector(listTarget, MaterializePipelineItems(items), sliceSelector);
                default:
                    return EventScriptValue.Nothing;
            }
        }

        private static IReadOnlyList<EventScriptValue> MaterializePipelineItems(IEnumerable<EventScriptValue> items)
            => items as IReadOnlyList<EventScriptValue> ?? items.ToList();

        private static bool TryMaterializeCollectionAccessTarget(ExecutionContext context, EventScriptValue target, out IReadOnlyList<EventScriptValue> items)
        {
            if (!context.TryCheckMaterializedValue(target, "Collection access would materialize more range items than allowed."))
            {
                items = Array.Empty<EventScriptValue>();
                return false;
            }

            items = target.AsList();
            return true;
        }

        private static bool TryEnumerateCollectionAccessTarget(ExecutionContext context, EventScriptValue target, out IEnumerable<EventScriptValue> items)
        {
            if (!context.TryCheckMaterializedValue(target, "Collection access would enumerate more range items than allowed."))
            {
                items = Array.Empty<EventScriptValue>();
                return false;
            }

            items = EnumerateListLikeValue(target);
            return true;
        }

        private static IEnumerable<EventScriptValue> EnumerateListLikeValue(EventScriptValue value)
        {
            if (value.Kind == EventScriptValueKind.Optional)
            {
                var optional = value.AsOptional();
                return optional.HasValue
                    ? EnumerateListLikeValue(optional.Value)
                    : Array.Empty<EventScriptValue>();
            }

            return value.Kind is EventScriptValueKind.Range or EventScriptValueKind.Sequence
                ? value.AsEnumerable()
                : value.AsList();
        }

        private EventScriptProjectionEvaluator CreateProjectionEvaluator(ExecutionContext context)
            => new(
                context.PushScope,
                context.PopScope,
                context.Define,
                expression => EvaluateExpression(context, expression));

        private EventScriptValue EvaluateEdgeSelector(ExecutionContext context, IEnumerable<EventScriptValue> items, EdgeSelectorNode selector)
        {
            IEnumerable<EventScriptValue> candidates = items;
            if (!string.IsNullOrEmpty(selector.Identifier) && selector.Predicate is not null)
            {
                var projection = CreateProjectionEvaluator(context);
                candidates = items.Where(item => projection.EvaluateBoolean(selector.Identifier!, selector.Predicate, item));
            }

            EventScriptValue? first = null;
            EventScriptValue? last = null;
            var count = 0;
            foreach (var candidate in candidates)
            {
                first ??= candidate;
                last = candidate;
                count++;
                if (selector.Mode == "first")
                {
                    return candidate;
                }

                if (selector.Mode == "single" && count > 1)
                {
                    return EventScriptValue.Nothing;
                }
            }

            return selector.Mode switch
            {
                "last" => last ?? EventScriptValue.Nothing,
                "single" => count == 1 ? first! : EventScriptValue.Nothing,
                _ => EventScriptValue.Nothing
            };
        }

        private EventScriptValue EvaluateIndexedCollectionAccess(ExecutionContext context, EventScriptValue target, ExpressionNode selectorExpression)
        {
            var selector = EvaluateExpression(context, selectorExpression);
            if (selector.IsNothing())
            {
                return EventScriptValue.Nothing;
            }

            return target.Lookup(selector);
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

            return target.Kind == EventScriptValueKind.Dice
                ? EventScriptValueFactory.Dice(EventScriptDiceValue.EventScriptDice(takenItems.Select(item => (int)item.AsInteger())))
                : EventScriptValueFactory.List(takenItems);
        }

        private bool EvaluateObjectMatchSelector(ExecutionContext context, EventScriptValue target, ObjectMatchPatternNode pattern)
        {
            if (target.Kind is not (EventScriptValueKind.List or EventScriptValueKind.Set or EventScriptValueKind.Dice))
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
                return target.Kind == EventScriptValueKind.Dice
                    ? EventScriptValueFactory.Dice(EventScriptDiceValue.Empty)
                    : EventScriptValueFactory.List(Array.Empty<EventScriptValue>());
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

            return target.Kind switch
            {
                EventScriptValueKind.Dice => EventScriptValueFactory.Dice(EventScriptDiceValue.EventScriptDice(selectedItems.Select(item => (int)item.AsInteger()))),
                EventScriptValueKind.List => EventScriptValueFactory.List(selectedItems),
                EventScriptValueKind.Set => EventScriptValueFactory.List(selectedItems),
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

        private EventScriptValue EvaluatePredicateSelector(ExecutionContext context, IEnumerable<EventScriptValue> items, PredicateSelectorNode selector)
        {
            var isAny = string.Equals(selector.Operator, "any", StringComparison.Ordinal);
            if (!isAny && !string.Equals(selector.Operator, "all", StringComparison.Ordinal))
            {
                return EventScriptValueFactory.Boolean(false);
            }

            var projection = CreateProjectionEvaluator(context);
            foreach (var item in items)
            {
                var predicateResult = projection.EvaluateBoolean(selector.Identifier, selector.Predicate, item);
                if (isAny && predicateResult)
                {
                    return EventScriptValueFactory.Boolean(true);
                }

                if (!isAny && !predicateResult)
                {
                    return EventScriptValueFactory.Boolean(false);
                }
            }

            return EventScriptValueFactory.Boolean(!isAny);
        }

        private EventScriptValue EvaluateCountSelector(ExecutionContext context, IEnumerable<EventScriptValue> items, CountSelectorNode selector)
        {
            var count = 0L;
            var projection = CreateProjectionEvaluator(context);
            foreach (var item in items)
            {
                if (projection.EvaluateBoolean(selector.Identifier, selector.Predicate, item))
                {
                    count++;
                }
            }

            return EventScriptValueFactory.Integer(count);
        }

        private EventScriptValue EvaluateChooseSelector(ExecutionContext context, IReadOnlyList<EventScriptValue> items, ChooseSelectorNode selector)
        {
            var projection = CreateProjectionEvaluator(context);
            var candidates = selector.Predicate is null || string.IsNullOrEmpty(selector.Identifier)
                ? items.ToList()
                : FilterItems(projection, items, selector.Identifier!, selector.Predicate);

            IReadOnlyList<EventScriptValue> chosen;
            if (selector.WeightExpression is not null && !string.IsNullOrEmpty(selector.WeightIdentifier))
            {
                chosen = ChooseWeightedItems(projection, candidates, selector.Count, selector.WeightIdentifier!, selector.WeightExpression);
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

            return EventScriptValueFactory.List(chosen);
        }

        private static EventScriptValue EvaluateDrawSelector(EventScriptValue target, IReadOnlyList<EventScriptValue> items, DrawSelectorNode selector)
        {
            if (target.Kind is not (EventScriptValueKind.List or EventScriptValueKind.Dice))
            {
                return EventScriptValue.Nothing;
            }

            var drawn = items.Take(selector.Count).ToArray();
            if (selector.Count == 1)
            {
                return drawn.Length == 0 ? EventScriptValue.Nothing : drawn[0];
            }

            return target.Kind == EventScriptValueKind.Dice
                ? EventScriptValueFactory.Dice(EventScriptDiceValue.EventScriptDice(drawn.Select(item => (int)item.AsInteger())))
                : EventScriptValueFactory.List(drawn);
        }

        private EventScriptValue EvaluateShuffleSelector(EventScriptValue target, IReadOnlyList<EventScriptValue> items)
        {
            if (target.Kind is not (EventScriptValueKind.List or EventScriptValueKind.Dice))
            {
                return EventScriptValue.Nothing;
            }

            var shuffled = items.ToArray();
            for (var i = shuffled.Length - 1; i > 0; i--)
            {
                if (!TryNextInclusiveInt(0, i, out var swapIndex))
                {
                    return EventScriptValueFactory.List(shuffled);
                }

                (shuffled[i], shuffled[swapIndex]) = (shuffled[swapIndex], shuffled[i]);
            }

            return EventScriptValueFactory.List(shuffled);
        }

        private static EventScriptValue EvaluateReverseSelector(EventScriptValue target, IReadOnlyList<EventScriptValue> items)
        {
            if (target.Kind is not (EventScriptValueKind.List or EventScriptValueKind.Dice))
            {
                return EventScriptValue.Nothing;
            }

            return EventScriptValueFactory.List(items.Reverse().ToArray());
        }

        private static List<EventScriptValue> FilterItems(EventScriptProjectionEvaluator projection, IEnumerable<EventScriptValue> items, string identifier, ExpressionNode predicate)
        {
            var result = new List<EventScriptValue>();
            foreach (var item in items)
            {
                if (projection.EvaluateBoolean(identifier, predicate, item))
                {
                    result.Add(item);
                }
            }

            return result;
        }

        private IReadOnlyList<EventScriptValue> ChooseWeightedItems(EventScriptProjectionEvaluator projection, IReadOnlyList<EventScriptValue> candidates, int count, string identifier, ExpressionNode weightExpression)
        {
            var remaining = candidates.ToList();
            var chosen = new List<EventScriptValue>();

            while (chosen.Count < count && remaining.Count > 0)
            {
                var weightedItems = new List<(EventScriptValue Item, decimal Weight)>();
                decimal totalWeight = 0m;

                foreach (var candidate in remaining)
                {
                    var weight = projection.EvaluatePositiveWeight(identifier, weightExpression, candidate);
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

        private EventScriptValue EvaluateFilterSelector(ExecutionContext context, IEnumerable<EventScriptValue> items, FilterSelectorNode selector)
        {
            var result = new List<EventScriptValue>();
            var projection = CreateProjectionEvaluator(context);
            foreach (var item in items)
            {
                if (projection.EvaluateBoolean(selector.Identifier, selector.Predicate, item))
                {
                    result.Add(item);
                }
            }

            return EventScriptValueFactory.List(result);
        }

        private EventScriptValue EvaluateSumSelector(ExecutionContext context, IEnumerable<EventScriptValue> items, SumSelectorNode selector)
        {
            EventScriptValue? sumValue = null;
            var projection = CreateProjectionEvaluator(context);
            foreach (var item in items)
            {
                var projected = projection.Evaluate(selector.Identifier, selector.Projection, item);
                if (!TryCoerceNumericForOperation(projected, out _))
                {
                    return EventScriptValueFactory.DecimalNaN();
                }

                if (sumValue is null)
                {
                    sumValue = projected;
                    continue;
                }

                if (EventScriptValueAlu.TryEvaluateUnitBinary(sumValue, "+", projected, out var unitSum))
                {
                    sumValue = unitSum;
                    continue;
                }

                TryCoerceNumericForOperation(sumValue, out var left);
                TryCoerceNumericForOperation(projected, out var right);
                sumValue = EventScriptValueAlu.ToEventScriptNumericResult(sumValue, "+", projected, AddNumeric(left, right));
            }

            return sumValue ?? EventScriptValueFactory.Decimal(0m);
        }

        private EventScriptValue EvaluateAverageSelector(ExecutionContext context, IEnumerable<EventScriptValue> items, AverageSelectorNode selector)
        {
            EventScriptValue? sumValue = null;
            var count = 0;
            var projection = CreateProjectionEvaluator(context);
            foreach (var item in items)
            {
                var projected = projection.Evaluate(selector.Identifier, selector.Projection, item);
                if (!TryCoerceNumericForOperation(projected, out var number) || !number.IsFinite)
                {
                    return EventScriptValue.Nothing;
                }

                if (sumValue is null)
                {
                    sumValue = projected;
                }
                else if (EventScriptValueAlu.TryEvaluateUnitBinary(sumValue, "+", projected, out var unitSum))
                {
                    sumValue = unitSum;
                }
                else
                {
                    TryCoerceNumericForOperation(sumValue, out var left);
                    sumValue = EventScriptValueAlu.ToEventScriptNumericResult(sumValue, "+", projected, AddNumeric(left, number));
                }

                count++;
            }

            if (count == 0 || sumValue is null || !TryCoerceNumericForOperation(sumValue, out var sum) || !sum.IsFinite)
            {
                return EventScriptValue.Nothing;
            }

            return EventScriptValueAlu.TryEvaluateUnitBinary(sumValue, "/", EventScriptValueFactory.Integer(count), out var average)
                ? average
                : EventScriptValueFactory.Decimal(sum.Value / count);
        }

        private EventScriptValue EvaluateSelectSelector(ExecutionContext context, IEnumerable<EventScriptValue> items, SelectSelectorNode selector)
        {
            var result = new List<EventScriptValue>();
            var projection = CreateProjectionEvaluator(context);
            foreach (var item in items)
            {
                result.Add(projection.Evaluate(selector.Identifier, selector.Projection, item));
            }

            return EventScriptValueFactory.List(result);
        }

        private EventScriptValue EvaluateDictionarySelector(ExecutionContext context, IEnumerable<EventScriptValue> items, DictionarySelectorNode selector)
        {
            var result = new Dictionary<string, EventScriptValue>(StringComparer.Ordinal);
            var projection = CreateProjectionEvaluator(context);
            foreach (var item in items)
            {
                var key = projection.Evaluate(selector.Identifier, selector.KeyProjection, item).AsText();
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                var value = selector.ValueProjection is null
                    ? item
                    : projection.Evaluate(selector.Identifier, selector.ValueProjection, item);
                result[key] = value;
            }

            return EventScriptValueFactory.Dictionary(result);
        }

        private EventScriptValue EvaluateContainsSelector(ExecutionContext context, EventScriptValue target, ContainsSelectorNode selector)
        {
            var value = EvaluateExpression(context, selector.ValueExpression);
            return selector.Mode switch
            {
                "single" => EventScriptValueFactory.Boolean(target.Contains(value)),
                "all" => EventScriptValueFactory.Boolean(EnumerateListLikeValue(value).All(target.Contains)),
                "any" => EventScriptValueFactory.Boolean(EnumerateListLikeValue(value).Any(target.Contains)),
                _ => EventScriptValueFactory.Boolean(false)
            };
        }

        private EventScriptValue EvaluateExtremaSelector(ExecutionContext context, IEnumerable<EventScriptValue> items, string identifier, ExpressionNode projection, bool isMax)
        {
            EventScriptValue? bestItem = null;
            EventScriptValue? bestProjection = null;
            var projectionEvaluator = CreateProjectionEvaluator(context);

            foreach (var item in items)
            {
                var candidateProjection = projectionEvaluator.Evaluate(identifier, projection, item);
                if (bestProjection is null)
                {
                    bestProjection = candidateProjection;
                    bestItem = item;
                    continue;
                }

                int comparison;
                if (TryCoerceNumericForOperation(candidateProjection, out _) && TryCoerceNumericForOperation(bestProjection, out _))
                {
                    if (!TryCompareNumericValues(candidateProjection, bestProjection, out comparison))
                    {
                        return EventScriptValue.Nothing;
                    }
                }
                else
                {
                    comparison = EventScriptValue.StableComparer.Compare(candidateProjection, bestProjection);
                }

                if ((isMax && comparison > 0) || (!isMax && comparison < 0))
                {
                    bestProjection = candidateProjection;
                    bestItem = item;
                }
            }

            return bestItem ?? EventScriptValue.Nothing;
        }

        private EventScriptValue EvaluateSortSelector(ExecutionContext context, EventScriptValue target, IEnumerable<EventScriptValue> items, SortSelectorNode selector)
        {
            _ = context;
            return EventScriptCollectionOperators.Sort(target, items, selector.Direction);
        }

        private EventScriptValue EvaluateOrderBySelector(ExecutionContext context, EventScriptValue target, IEnumerable<EventScriptValue> items, OrderBySelectorNode selector)
        {
            var projection = CreateProjectionEvaluator(context);
            return EventScriptCollectionOperators.OrderBy(
                target,
                items,
                selector.Direction,
                item => projection.Evaluate(selector.Identifier, selector.Projection, item));
        }

        private EventScriptValue EvaluateDistinctSelector(ExecutionContext context, EventScriptValue target, IEnumerable<EventScriptValue> items, DistinctSelectorNode selector)
        {
            if (selector.Projection is null || string.IsNullOrEmpty(selector.Identifier))
            {
                return EventScriptCollectionOperators.Distinct(target, items);
            }

            var projection = CreateProjectionEvaluator(context);
            return EventScriptCollectionOperators.DistinctBy(
                target,
                items,
                item => projection.Evaluate(selector.Identifier!, selector.Projection!, item));
        }

        private EventScriptValue EvaluateGroupBySelector(ExecutionContext context, IEnumerable<EventScriptValue> items, GroupBySelectorNode selector)
        {
            var projection = CreateProjectionEvaluator(context);
            return EventScriptCollectionOperators.GroupBy(
                items,
                item => projection.Evaluate(selector.Identifier, selector.Projection, item));
        }

        private bool MatchesObjectPattern(ExecutionContext context, EventScriptValue value, ObjectMatchPatternNode pattern)
        {
            if (value.Kind != EventScriptValueKind.Dictionary)
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

        private static bool TryCompareNumericValues(EventScriptValue left, EventScriptValue right, out int comparison)
            => EventScriptValueAlu.TryCompareNumericValues(left, right, out comparison);

        private static NumericValue AddNumeric(NumericValue left, NumericValue right) => EventScriptValueAlu.AddNumeric(left, right);

        private static NumericValue SubtractNumeric(NumericValue left, NumericValue right) => EventScriptValueAlu.SubtractNumeric(left, right);

        private static NumericValue MultiplyNumeric(NumericValue left, NumericValue right) => EventScriptValueAlu.MultiplyNumeric(left, right);

        private static NumericValue DivideNumeric(NumericValue left, NumericValue right) => EventScriptValueAlu.DivideNumeric(left, right);

        private static NumericValue IntegerDivideNumeric(NumericValue left, NumericValue right) => EventScriptValueAlu.IntegerDivideNumeric(left, right);

        private static NumericValue ModuloNumeric(NumericValue left, NumericValue right) => EventScriptValueAlu.ModuloNumeric(left, right);

        private static NumericValue RemainderNumeric(NumericValue left, NumericValue right) => EventScriptValueAlu.RemainderNumeric(left, right);

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

        private EventScriptValue ConvertToDeclaredType(ExecutionContext context, EventScriptValue value, string declaredType)
        {
            switch (declaredType)
            {
                case "nothing":
                    return EventScriptValue.Nothing;
                case "tag":
                    return EventScriptValueFactory.Tag(value.AsText());
                case "text":
                    return EventScriptValueFactory.Text(value.AsText());
                case "percentage":
                    return ConvertToPercentage(value);
                case "degree":
                    return ConvertToDecimalUnit(value, EventScriptDecimalUnit.Degree);
                case "meter":
                    return ConvertToDecimalUnit(value, EventScriptDecimalUnit.Meter);
                case "second":
                    return ConvertToDecimalUnit(value, EventScriptDecimalUnit.Second);
                case "vector2":
                    return ConvertToVector2(value);
                case "vector3":
                    return ConvertToVector3(value);
                case "boolean":
                    return EventScriptValueFactory.Boolean(value.AsBoolean());
                case "integer":
                    return EventScriptValueFactory.Integer(value.AsInteger());
                case "decimal":
                {
                    if (!TryUnwrapOptionalForOperation(value, out var unwrappedNumber))
                    {
                        return EventScriptValueFactory.DecimalNaN();
                    }

                    if (TryCoerceNumericForOperation(unwrappedNumber, out var number))
                    {
                        return ToEventScriptDecimal(number);
                    }

                    return EventScriptValueFactory.DecimalNaN();
                }
                case "list":
                    if (!context.TryCheckMaterializedValue(value, "List conversion would materialize more range items than allowed."))
                    {
                        return EventScriptValue.Nothing;
                    }

                    return EventScriptValueFactory.List(value.AsList());
                case "sequence":
                    return value.IsSequence() ? value : EventScriptValueFactory.Sequence(value.AsEnumerable());
                case "range":
                    return value.IsRange() ? value : EventScriptValue.Nothing;
                case "message":
                    return value.Kind == EventScriptValueKind.Message
                        ? value
                        : EventScriptMessageValueCodec.TryReadMessageValue(value, out var messageValue)
                            ? EventScriptMessageValueCodec.CreateMessageValue(messageValue)
                            : EventScriptValue.Nothing;
                case "handler":
                    return value.Kind == EventScriptValueKind.Handler
                        ? value
                        : EventScriptMessageValueCodec.TryReadHandlerValue(value, out var handlerValue)
                            ? EventScriptMessageValueCodec.CreateHandlerValue(handlerValue)
                            : EventScriptValue.Nothing;
                case "dictionary":
                    return EventScriptValueFactory.Dictionary(value.AsDictionary());
                case "set":
                    if (!context.TryCheckMaterializedValue(value, "Set conversion would materialize more range items than allowed."))
                    {
                        return EventScriptValue.Nothing;
                    }

                    return EventScriptValueFactory.Set(value.AsSet());
                case "dice":
                    return EventScriptValueFactory.Dice(value.AsDice());
                case "optional":
                    if (value.IsOptional()) return value;
                    return value.IsNothing() ? EventScriptValueFactory.OptionalNone() : EventScriptValueFactory.OptionalSome(value);
                default:
                    return _typeDefinitions.TryGetValue(declaredType, out var typeDefinition)
                        ? ConvertToCustomType(context, value, typeDefinition)
                        : value;
            }
        }

        private EventScriptValue EvaluateTypeConstructorExpression(ExecutionContext context, TypeConstructorExpressionNode constructor)
        {
            if (constructor.TypeName is "vector2" or "vector3")
            {
                return EvaluateVectorConstructorExpression(context, constructor);
            }

            if (_typeDefinitions.TryGetValue(constructor.TypeName, out var typeDefinition))
            {
                var values = new Dictionary<string, EventScriptValue>(StringComparer.Ordinal);
                foreach (var argument in constructor.Arguments)
                {
                    if (argument.Label is null)
                    {
                        return EventScriptValue.Nothing;
                    }

                    values[argument.Label] = EvaluateExpression(context, argument.Expression);
                }

                return ConvertToCustomType(context, EventScriptValueFactory.Dictionary(values), typeDefinition);
            }

            if (constructor.Arguments.Count != 1 || constructor.Arguments[0].Label is not null)
            {
                return EventScriptValue.Nothing;
            }

            return ConvertToDeclaredType(context, EvaluateExpression(context, constructor.Arguments[0].Expression), constructor.TypeName);
        }

        private EventScriptValue EvaluateVectorConstructorExpression(ExecutionContext context, TypeConstructorExpressionNode constructor)
        {
            var expectedCount = constructor.TypeName == "vector2" ? 2 : 3;
            if (constructor.Arguments.Count != expectedCount)
            {
                return EventScriptValue.Nothing;
            }

            var components = constructor.Arguments
                .Select(argument => EvaluateExpression(context, argument.Expression))
                .ToArray();

            return expectedCount == 2
                ? EventScriptValueAlu.TryCreateVector2(components[0], components[1], out var vector2) ? vector2 : EventScriptValue.Nothing
                : EventScriptValueAlu.TryCreateVector3(components[0], components[1], components[2], out var vector3) ? vector3 : EventScriptValue.Nothing;
        }

        private EventScriptValue ConvertToPercentage(EventScriptValue value)
        {
            if (!TryUnwrapOptionalForOperation(value, out var unwrapped))
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

            if (TryCoerceNumericForOperation(unwrapped, out var number))
            {
                if (!number.IsFinite)
                {
                    return EventScriptValueFactory.DecimalNaN();
                }

                var ratio = unwrapped.Kind == EventScriptValueKind.Integer
                    ? number.Value / 100m
                    : number.Value > 1m || number.Value < -1m
                        ? number.Value / 100m
                        : number.Value;
                return EventScriptValueFactory.Percentage(ratio);
            }

            return EventScriptValueFactory.DecimalNaN();
        }

        private EventScriptValue ConvertToDecimalUnit(EventScriptValue value, EventScriptDecimalUnit unit)
        {
            if (!TryUnwrapOptionalForOperation(value, out var unwrapped))
            {
                return EventScriptValueFactory.DecimalNaN();
            }

            if (unwrapped.Kind is EventScriptValueKind.Decimal or EventScriptValueKind.Integer &&
                TryCoerceNumericForOperation(unwrapped, out var number) &&
                number.IsFinite)
            {
                return EventScriptValueFactory.Decimal(number.Value, unit);
            }

            return EventScriptValueFactory.DecimalNaN();
        }

        private EventScriptValue ConvertToVector2(EventScriptValue value)
        {
            if (!TryUnwrapOptionalForOperation(value, out var unwrapped))
            {
                return EventScriptValue.Nothing;
            }

            if (unwrapped is EventScriptVector2Value vector2)
            {
                return vector2;
            }

            if (unwrapped is EventScriptVector3Value vector3)
            {
                return EventScriptValueFactory.Vector2(vector3.X, vector3.Y, vector3.Unit);
            }

            if (unwrapped.TryGetDictionaryMember("x", out var x) &&
                unwrapped.TryGetDictionaryMember("y", out var y) &&
                EventScriptValueAlu.TryCreateVector2(x, y, out var vectorFromMembers))
            {
                return vectorFromMembers;
            }

            var items = unwrapped.AsList();
            if (items.Count >= 2 &&
                EventScriptValueAlu.TryCreateVector2(items[0], items[1], out var vectorFromItems))
            {
                return vectorFromItems;
            }

            return EventScriptValue.Nothing;
        }

        private EventScriptValue ConvertToVector3(EventScriptValue value)
        {
            if (!TryUnwrapOptionalForOperation(value, out var unwrapped))
            {
                return EventScriptValue.Nothing;
            }

            if (unwrapped is EventScriptVector3Value vector3)
            {
                return vector3;
            }

            if (unwrapped is EventScriptVector2Value vector2)
            {
                return EventScriptValueFactory.Vector3(vector2.X, vector2.Y, 0m, vector2.Unit);
            }

            if (unwrapped.TryGetDictionaryMember("x", out var x) &&
                unwrapped.TryGetDictionaryMember("y", out var y))
            {
                if (unwrapped.TryGetDictionaryMember("z", out var z))
                {
                    return EventScriptValueAlu.TryCreateVector3(x, y, z, out var vectorFromMembers)
                        ? vectorFromMembers
                        : EventScriptValue.Nothing;
                }

                return EventScriptValueAlu.TryCreateVector2(x, y, out var xyVector) && xyVector is EventScriptVector2Value xy
                    ? EventScriptValueFactory.Vector3(xy.X, xy.Y, 0m, xy.Unit)
                    : xyVector;
            }

            var items = unwrapped.AsList();
            if (items.Count >= 3 &&
                EventScriptValueAlu.TryCreateVector3(items[0], items[1], items[2], out var vectorFromItems))
            {
                return vectorFromItems;
            }

            if (items.Count >= 2 &&
                EventScriptValueAlu.TryCreateVector2(items[0], items[1], out var xyVectorFromItems) &&
                xyVectorFromItems is EventScriptVector2Value xyFromItems)
            {
                return EventScriptValueFactory.Vector3(xyFromItems.X, xyFromItems.Y, 0m, xyFromItems.Unit);
            }

            return EventScriptValue.Nothing;
        }

        private EventScriptValue ConvertToCustomType(ExecutionContext context, EventScriptValue value, CompiledTypeDefinition typeDefinition)
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

                var fieldValue = ConvertToDeclaredType(context, rawValue, field.TypeName);
                fieldValue = ApplyFieldClamp(typeDefinition, field, fieldValue, sourceValues, materializedValues);
                fieldValue = ConvertToDeclaredType(context, fieldValue, field.TypeName);
                materializedValues[field.Name] = fieldValue;
            }

            foreach (var field in typeDefinition.Fields.Where(field => field.ComputedExpression is not null))
            {
                var computedValue = EvaluateCustomTypeExpression(typeDefinition, field.ComputedExpression!, sourceValues, materializedValues);
                materializedValues[field.Name] = ConvertToDeclaredType(context, computedValue, field.TypeName);
            }

            return EventScriptValueFactory.CustomType(typeDefinition.Name, materializedValues);
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
            if (!EventScriptValueAlu.HaveCompatibleNumericUnits(fieldValue, minimum) ||
                !EventScriptValueAlu.HaveCompatibleNumericUnits(fieldValue, maximum) ||
                !EventScriptValueAlu.HaveCompatibleNumericUnits(minimum, maximum))
            {
                return EventScriptValueFactory.DecimalNaN();
            }

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
                    return EventScriptValueFactory.Decimal(Math.Max(valueNumber.Value, minimumNumber.Value));
                }

                return fieldValue;
            }

            var lower = Math.Min(minimumNumber.Value, maximumNumber.Value);
            var upper = Math.Max(minimumNumber.Value, maximumNumber.Value);
            EventScriptValue.TryGetDecimalUnit(fieldValue, out var unit);
            return EventScriptValueFactory.Decimal(Math.Min(Math.Max(valueNumber.Value, lower), upper), fieldValue.HasDecimalUnit() ? unit : null);
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
                "nothing" => value.IsNothing(),
                "tag" => value.IsTag(),
                "text" => value.IsText(),
                "percentage" => value.IsPercentage(),
                "degree" => value.IsDecimalUnit(EventScriptDecimalUnit.Degree),
                "meter" => value.IsDecimalUnit(EventScriptDecimalUnit.Meter),
                "second" => value.IsDecimalUnit(EventScriptDecimalUnit.Second),
                "vector2" => value.IsVector2(),
                "vector3" => value.IsVector3(),
                "decimal" => value.IsNumber(),
                "integer" => value.IsInteger(),
                "boolean" => value.Kind == EventScriptValueKind.Boolean,
                "optional" => value.IsOptional(),
                "sequence" => value.IsSequence(),
                "list" => value.IsList(),
                "range" => value.IsRange(),
                "message" => value.Kind == EventScriptValueKind.Message || EventScriptMessageValueCodec.TryReadMessageValue(value, out _),
                "handler" => value.Kind == EventScriptValueKind.Handler || EventScriptMessageValueCodec.TryReadHandlerValue(value, out _),
                "dictionary" => value.IsDictionary(),
                "set" => value.IsSet(),
                "dice" => value.IsDice(),
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
            return value.Kind switch
            {
                EventScriptValueKind.Nothing => "nothing",
                EventScriptValueKind.Tag => $"tag:{value.AsText()}",
                EventScriptValueKind.Text => $"text:{value.AsText()}",
                EventScriptValueKind.Percentage => $"percentage:{value.AsNumber().ToString(CultureInfo.InvariantCulture)}",
                EventScriptValueKind.Vector2 => BuildVector2StableSeedText((EventScriptVector2Value)value),
                EventScriptValueKind.Vector3 => BuildVector3StableSeedText((EventScriptVector3Value)value),
                EventScriptValueKind.Decimal => value.IsNaN()
                    ? "decimal:nan"
                    : value.IsNegativeInfinity()
                        ? "decimal:-infinity"
                        : value.IsInfinity()
                            ? "decimal:infinity"
                            : value is EventScriptDecimalValue { Unit: { } unit }
                                ? $"decimal:{value.AsNumber().ToString(CultureInfo.InvariantCulture)}:{EventScriptDecimalUnits.ToTypeName(unit)}"
                                : $"decimal:{value.AsNumber().ToString(CultureInfo.InvariantCulture)}",
                EventScriptValueKind.Integer => $"integer:{value.AsInteger().ToString(CultureInfo.InvariantCulture)}",
                EventScriptValueKind.Boolean => $"boolean:{(value.AsBoolean() ? "true" : "false")}",
                EventScriptValueKind.Optional => value.AsOptional().HasValue
                    ? $"optional:{BuildStableSeedText(value.AsOptional().Value)}"
                    : "optional:none",
                EventScriptValueKind.Sequence => $"sequence:[{string.Join("|", value.AsEnumerable().Select(BuildStableSeedText))}]",
                EventScriptValueKind.Range => $"range:{((EventScriptRangeValue)value).From}:{((EventScriptRangeValue)value).To}:{((EventScriptRangeValue)value).Step}",
                EventScriptValueKind.Message =>
                    $"message:{((EventScriptMessageValue)value).Value.SignatureId}:[{string.Join("|", ((EventScriptMessageValue)value).Value.Arguments.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={BuildStableSeedText(pair.Value)}"))}]",
                EventScriptValueKind.Handler => $"handler:{((EventScriptHandlerValue)value).Signature.SignatureId}",
                EventScriptValueKind.List => $"list:[{string.Join("|", value.AsList().Select(BuildStableSeedText))}]",
                EventScriptValueKind.Dictionary =>
                    $"dict:[{string.Join("|", value.AsDictionary().OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={BuildStableSeedText(pair.Value)}"))}]",
                EventScriptValueKind.Set => $"set:[{string.Join("|", value.AsSet().OrderBy(item => item, EventScriptValue.StableComparer).Select(BuildStableSeedText))}]",
                EventScriptValueKind.Dice => $"dice:[{string.Join("|", value.AsDice().Rolls)}]",
                _ => value.ToString()
            };
        }

        private static string BuildVector2StableSeedText(EventScriptVector2Value value)
            => value.Unit.HasValue
                ? $"vector2:{value.X.ToString(CultureInfo.InvariantCulture)}:{value.Y.ToString(CultureInfo.InvariantCulture)}:{EventScriptDecimalUnits.ToTypeName(value.Unit.Value)}"
                : $"vector2:{value.X.ToString(CultureInfo.InvariantCulture)}:{value.Y.ToString(CultureInfo.InvariantCulture)}";

        private static string BuildVector3StableSeedText(EventScriptVector3Value value)
            => value.Unit.HasValue
                ? $"vector3:{value.X.ToString(CultureInfo.InvariantCulture)}:{value.Y.ToString(CultureInfo.InvariantCulture)}:{value.Z.ToString(CultureInfo.InvariantCulture)}:{EventScriptDecimalUnits.ToTypeName(value.Unit.Value)}"
                : $"vector3:{value.X.ToString(CultureInfo.InvariantCulture)}:{value.Y.ToString(CultureInfo.InvariantCulture)}:{value.Z.ToString(CultureInfo.InvariantCulture)}";

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

            public bool TryConsumeExecutionStep(string detail)
                => _context.RuntimeBudget.TryConsumeExecutionStep(detail);

            public bool TryConsumeLoopIteration(string detail)
                => _context.RuntimeBudget.TryConsumeLoopIteration(detail);

            public bool TryEnterCall(string detail)
                => _context.RuntimeBudget.TryEnterCall(detail);

            public void ExitCall()
                => _context.RuntimeBudget.ExitCall();

            public bool TryCheckRangeLength(EventScriptValue range, string detail)
            {
                if (!EventScriptRuntimeLimitUtilities.TryGetRangeLength(range, out var length))
                {
                    return true;
                }

                return _context.RuntimeBudget.TryCheckRangeLength(length, detail);
            }

            public bool TryCheckMaterializedValue(EventScriptValue value, string detail)
                => TryCheckRangeLength(value, detail);

            public bool TryCheckGeneratedCollectionItemCount(int count, string detail)
                => _context.RuntimeBudget.TryCheckGeneratedCollectionItemCount(count, detail);

            public bool TryCheckDice(DiceExpressionNode diceExpression)
                => _context.RuntimeBudget.TryCheckDice(diceExpression);

            public void Publish(string message, IReadOnlyDictionary<string, EventScriptValue> arguments)
            {
                var publishedMessage = new EventScriptMessage(message, arguments);
                _context.Publish(publishedMessage);
                if (_context.PublishCallbackRecordsDiagnostics)
                {
                    return;
                }

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
