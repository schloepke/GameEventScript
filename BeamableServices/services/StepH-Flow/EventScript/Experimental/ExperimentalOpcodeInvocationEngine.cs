#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StepH.Flow.EventScript.Interpreter;
using StepH.Flow.EventScript.Parser;
using StepH.Flow.EventScript.Runtime;
using StepH.Flow.EventScript.Types;
using NumericKind = StepH.Flow.EventScript.Runtime.EventScriptValueAlu.NumericKind;
using NumericValue = StepH.Flow.EventScript.Runtime.EventScriptValueAlu.NumericValue;

namespace StepH.Flow.EventScript.Experimental;

internal static class ExperimentalOpcodeInvocationEngine
{
    public static void InvokeMessage(ExperimentalCompiledEventScript compiledScript, EventScriptContext context, EventScriptMessage message)
        => new InvocationSession(compiledScript, context).InvokeMessage(message);

    public static void InvokeHandler(
        ExperimentalCompiledEventScript compiledScript,
        EventScriptContext context,
        ExperimentalCompiledEventScriptHandler? handler,
        IReadOnlyDictionary<string, EventScriptValue> args)
        => new InvocationSession(compiledScript, context).InvokeHandler(handler, args);

    private sealed class InvocationSession
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyList<ExperimentalCompiledEventScriptHandler>> _dispatchIndex;
        private readonly IReadOnlyDictionary<string, ExperimentalCompiledTypeDefinition> _typeDefinitions;
        private readonly IReadOnlyDictionary<string, ExperimentalCompiledCallableDefinition> _callables;
        private readonly EventScriptContext _context;
        private readonly EventScriptRandomGenerator _randomGenerator;
        private readonly Stack<EventScriptRandomGenerator> _randomScopes = new();
        private readonly bool _diagnosticsEnabled;
        private readonly ExperimentalCompiledEventScript? _compiledScript;

        internal InvocationSession(ExperimentalCompiledEventScript compiledScript, EventScriptContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _randomGenerator = _context.Random;
            _typeDefinitions = compiledScript?.TypeDefinitions ?? new Dictionary<string, ExperimentalCompiledTypeDefinition>(StringComparer.Ordinal);
            _callables = compiledScript?.Callables ?? new Dictionary<string, ExperimentalCompiledCallableDefinition>(StringComparer.Ordinal);
            _dispatchIndex = compiledScript?.DispatchIndex ?? new Dictionary<string, IReadOnlyList<ExperimentalCompiledEventScriptHandler>>(StringComparer.Ordinal);
            _compiledScript = compiledScript;
            _diagnosticsEnabled = compiledScript?.Options.EnableDiagnostics ?? false;
            _randomScopes.Push(_randomGenerator);
        }

        public void InvokeMessage(string message, IReadOnlyDictionary<string, EventScriptValue> args)
            => InvokeMessage(new EventScriptMessage(message, args));

        public void InvokeMessage(EventScriptMessage message)
        {
            try
            {
                message ??= new EventScriptMessage(string.Empty);
                if (string.IsNullOrWhiteSpace(message.Name))
                {
                    return;
                }

                foreach (var handler in GetMatchingHandlers(message))
                {
                    try
                    {
                        var executionContext = new ExperimentalCompiledExecutionContext(_context, _diagnosticsEnabled);
                        ExecuteHandler(executionContext, handler, message.Arguments);
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
                // Runtime has to be lenient and should not throw.
            }
        }

        public void InvokeHandler(ExperimentalCompiledEventScriptHandler? handler, IReadOnlyDictionary<string, EventScriptValue> args)
        {
            if (handler is null)
            {
                return;
            }

            try
            {
                args = EventScriptNamedArguments.Normalize(args);
                var context = new ExperimentalCompiledExecutionContext(_context, _diagnosticsEnabled);
                ExecuteHandler(context, handler, args);
            }
            catch
            {
                // Runtime has to be lenient and should not throw.
            }
        }

    private IReadOnlyList<ExperimentalCompiledEventScriptHandler> GetMatchingHandlers(EventScriptMessage message)
        => EventScriptInvocationKernel.GetMatchingHandlers(_dispatchIndex, message);

    private void ExecuteHandler(ExperimentalCompiledExecutionContext context, ExperimentalCompiledEventScriptHandler handler, IReadOnlyDictionary<string, EventScriptValue> args)
    {
        context.PushScope();
        try
        {
            foreach (var parameter in handler.Parameters)
            {
                if (handler.DiagnosticsEnabled)
                {
                    args.TryGetValue(parameter, out var parameterValue);
                    context.RecordDiagnostic(
                        EventScriptDiagnosticEventKind.ParameterBound,
                        parameter,
                        new Dictionary<string, EventScriptValue>(StringComparer.Ordinal) { [parameter] = parameterValue ?? EventScriptValue.Nothing },
                        $"Bound '{parameter}'");
                }

                context.Define(parameter, args.TryGetValue(parameter, out var parameterArgument) ? parameterArgument : EventScriptValue.Nothing);
            }

            if (handler.DiagnosticsEnabled)
            {
                context.RecordDiagnostic(
                    EventScriptDiagnosticEventKind.HandlerInvoked,
                    handler.Message,
                    args,
                    $"Handler '{handler.Message}' invoked");
            }

            ExecuteProgram(context, handler.ProgramIndex);
        }
        finally
        {
            context.PopScope();
        }
    }

    private void ExecuteProgram(ExperimentalCompiledExecutionContext context, int programIndex)
    {
        if (_compiledScript is null || programIndex < 0 || programIndex >= _compiledScript.Programs.Count)
        {
            return;
        }

        var program = _compiledScript.Programs[programIndex];
        foreach (var instruction in program.Instructions)
        {
            ExecuteInstruction(context, instruction);
        }
    }

    private void ExecuteInstruction(ExperimentalCompiledExecutionContext context, ExperimentalInstruction instruction)
    {
        try
        {
            if (!context.TryConsumeExecutionStep("Instruction execution budget exhausted."))
            {
                return;
            }

            if (_compiledScript is null)
            {
                return;
            }

            switch (instruction.OpCode)
            {
                case ExperimentalOpCode.Let:
                {
                    var identifier = ResolveString(instruction.A);
                    var value = EvaluateCompiledExpression(context, instruction.C);
                    var declaredType = ResolveStringNullable(instruction.B);
                    if (!string.IsNullOrEmpty(declaredType))
                    {
                        value = ConvertToDeclaredType(context, value, declaredType!);
                    }

                    context.Define(identifier, value);
                    return;
                }
                case ExperimentalOpCode.Publish:
                {
                    var publishValue = EvaluateCompiledExpression(context, instruction.A);
                    if (EventScriptMessageValueCodec.TryReadMessageValue(publishValue, out var message))
                    {
                        context.Publish(message.Name, message.Arguments);
                    }

                    return;
                }
                case ExperimentalOpCode.If:
                {
                    if (AsBool(EvaluateCompiledExpression(context, instruction.A)))
                    {
                        ExecuteProgram(context, instruction.B);
                    }
                    else if (instruction.C >= 0)
                    {
                        ExecuteProgram(context, instruction.C);
                    }

                    return;
                }
                case ExperimentalOpCode.ForEach:
                {
                    var identifier = ResolveString(instruction.A);
                    foreach (var item in EnumerateIterationSource(context, instruction.B))
                    {
                        if (!context.TryConsumeLoopIteration("Loop iteration budget exhausted."))
                        {
                            break;
                        }

                        context.PushScope();
                        try
                        {
                            context.Define(identifier, item);
                            ExecuteProgram(context, instruction.C);
                        }
                        finally
                        {
                            context.PopScope();
                        }
                    }

                    return;
                }
                case ExperimentalOpCode.SeededRandom:
                {
                    var seedValue = EvaluateCompiledExpression(context, instruction.A);
                    PushSeededRandomScope(seedValue);
                    try
                    {
                        ExecuteProgram(context, instruction.B);
                    }
                    finally
                    {
                        PopSeededRandomScope();
                    }

                    return;
                }
                case ExperimentalOpCode.EvaluateExpression:
                    EvaluateCompiledExpression(context, instruction.A);
                    return;
                default:
                    return;
            }
        }
        catch
        {
            return;
        }
    }

    private EventScriptValue EvaluateExpression(ExperimentalCompiledExecutionContext context, ExpressionNode expression)
    {
        try
        {
            return EvaluateExpressionCore(context, expression);
        }
        catch
        {
            return EventScriptValue.Nothing;
        }
    }

    private EventScriptValue EvaluateCompiledExpression(ExperimentalCompiledExecutionContext context, int expressionIndex)
    {
        if (_compiledScript is null || expressionIndex < 0 || expressionIndex >= _compiledScript.ExpressionPool.Count)
        {
            return EventScriptValue.Nothing;
        }

        var expression = _compiledScript.ExpressionPool[expressionIndex];
        if (expression.IsConstant && expression.ConstantValue is not null)
        {
            return expression.ConstantValue;
        }

        return EvaluateExpression(context, expression.ExpressionNode);
    }

    private EventScriptValue EvaluateCompiledExpression(ExperimentalCompiledExecutionContext context, ExperimentalCompiledExpression? expression)
    {
        if (expression is null)
        {
            return EventScriptValue.Nothing;
        }

        if (expression.IsConstant && expression.ConstantValue is not null)
        {
            return expression.ConstantValue;
        }

        return EvaluateExpression(context, expression.ExpressionNode);
    }

    private IReadOnlyDictionary<string, EventScriptValue> EvaluateNamedArguments(ExperimentalCompiledExecutionContext context, int namedArgumentListIndex)
    {
        if (_compiledScript is null || namedArgumentListIndex < 0 || namedArgumentListIndex >= _compiledScript.NamedArgumentLists.Count)
        {
            return EventScriptNamedArguments.Empty;
        }

        var map = new Dictionary<string, EventScriptValue>(StringComparer.Ordinal);
        foreach (var argument in _compiledScript.NamedArgumentLists[namedArgumentListIndex].Arguments)
        {
            map[argument.Name] = EvaluateCompiledExpression(context, argument.Expression);
        }

        return map;
    }

    private string ResolveString(int stringIndex)
    {
        if (_compiledScript is null || stringIndex < 0 || stringIndex >= _compiledScript.StringPool.Count)
        {
            return string.Empty;
        }

        return _compiledScript.StringPool[stringIndex];
    }

    private string? ResolveStringNullable(int stringIndex)
        => stringIndex < 0 ? null : ResolveString(stringIndex);

    private EventScriptValue EvaluateExpressionCore(ExperimentalCompiledExecutionContext context, ExpressionNode expression)
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
            case DegreeLiteralExpressionNode degree:
                return EventScriptValueFactory.Degree(degree.Degrees);

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
                return EventScriptValueFactory.Boolean(IsValueOfType(EvaluateExpression(context, typeCheck.Value), typeCheck.TypeName));
            case TypeCastExpressionNode typeCast:
                return ConvertToDeclaredType(context, EvaluateExpression(context, typeCast.Value), typeCast.TypeName);

            case MemberAccessExpressionNode memberAccess:
                return EvaluateMemberAccess(context, memberAccess);

            case CollectionAccessExpressionNode collectionAccess:
                return EvaluateCollectionAccess(context, collectionAccess);

            default:
                return EventScriptValue.Nothing;
        }
    }

    private EventScriptValue EvaluateRandomExpression(ExperimentalCompiledExecutionContext context, RandomExpressionNode randomExpression)
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

    private EventScriptValue EvaluateRangeExpression(ExperimentalCompiledExecutionContext context, RangeExpressionNode rangeExpression)
    {
        return TryEvaluateRangeExpression(context, rangeExpression, out var range)
            ? range
            : EventScriptValue.Nothing;
    }

    private EventScriptValue EvaluateSeededRandomExpression(ExperimentalCompiledExecutionContext context, SeededRandomExpressionNode seededRandomExpression)
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

    private EventScriptValue EvaluateDiceExpression(ExperimentalCompiledExecutionContext context, DiceExpressionNode diceExpression)
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

    private EventScriptValue EvaluateDictionaryLiteral(ExperimentalCompiledExecutionContext context, DictionaryLiteralExpressionNode dictionaryLiteral)
    {
        var map = new Dictionary<string, EventScriptValue>(StringComparer.Ordinal);
        foreach (var entry in dictionaryLiteral.Entries)
        {
            map[entry.Key] = EvaluateExpression(context, entry.Value);
        }

        return EventScriptValueFactory.Dictionary(map);
    }

    private EventScriptValue EvaluateGeneratedCollectionExpression(ExperimentalCompiledExecutionContext context, GeneratedCollectionExpressionNode generatedCollection)
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

    private EventScriptValue EvaluateCallExpression(ExperimentalCompiledExecutionContext context, CallExpressionNode call)
    {
        var arguments = call.Arguments.Select(argument => EvaluateExpression(context, argument)).ToArray();

        if (_callables.TryGetValue(call.Name, out var callable))
        {
            var result = EvaluateCallableDefinition(context, callable, arguments);
            return callable.Kind == ExperimentalCallableKind.Rule
                ? EventScriptValueFactory.Boolean(AsBool(result))
                : result;
        }

        return EventScriptValue.Nothing;
    }

    private static EventScriptValue EvaluateHandlerLiteralExpression(HandlerLiteralExpressionNode handlerLiteral)
        => EventScriptMessageValueCodec.CreateHandlerValue(new EventScriptMessageSignature(handlerLiteral.Message, handlerLiteral.Parameters));

    private EventScriptValue EvaluateMessageLiteralExpression(ExperimentalCompiledExecutionContext context, MessageLiteralExpressionNode messageLiteral)
    {
        var arguments = new Dictionary<string, EventScriptValue>(StringComparer.Ordinal);
        foreach (var argument in messageLiteral.Arguments)
        {
            arguments[argument.Name] = EvaluateExpression(context, argument.Expression);
        }

        var message = new EventScriptMessage(messageLiteral.Message, arguments);
        return EventScriptMessageValueCodec.CreateMessageValue(message);
    }

    private EventScriptValue EvaluateHandlerBindExpression(ExperimentalCompiledExecutionContext context, HandlerBindExpressionNode handlerBind)
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

    private EventScriptValue EvaluateRulePredicateExpression(ExperimentalCompiledExecutionContext context, RulePredicateExpressionNode rulePredicate)
    {
        if (!_callables.TryGetValue(rulePredicate.RuleName, out var callable) ||
            callable.Kind != ExperimentalCallableKind.Rule ||
            callable.Parameters.Count != 1)
        {
            return EventScriptValue.Nothing;
        }

        var value = EvaluateExpression(context, rulePredicate.Value);
        var result = EvaluateCallableDefinition(context, callable, new[] { value });
        return EventScriptValueFactory.Boolean(AsBool(result));
    }

    private EventScriptValue EvaluateCallableDefinition(ExperimentalCompiledExecutionContext context, ExperimentalCompiledCallableDefinition definition, IReadOnlyList<EventScriptValue> arguments)
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
                    definition.Kind == ExperimentalCallableKind.Rule ? EventScriptDiagnosticEventKind.RuleCalled : EventScriptDiagnosticEventKind.SelectCalled,
                    definition.Name,
                    BuildOrderedArgumentMap(definition.Parameters, arguments),
                    $"{definition.Kind.ToString().ToLowerInvariant()} '{definition.Name}' called");
            }

            for (var i = 0; i < definition.Parameters.Count; i++)
            {
                context.Define(definition.Parameters[i], i < arguments.Count ? arguments[i] : EventScriptValue.Nothing);
            }

            return EvaluateCompiledExpression(context, definition.Expression);
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

    private bool TryProjectGeneratedItem(ExperimentalCompiledExecutionContext context, GeneratedCollectionExpressionNode generatedCollection, EventScriptValue item, List<EventScriptValue> values)
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

    private IEnumerable<EventScriptValue> EnumerateIterationSource(ExperimentalCompiledExecutionContext context, int sourceIndex)
    {
        if (_compiledScript is null || sourceIndex < 0 || sourceIndex >= _compiledScript.IterationSources.Count)
        {
            yield break;
        }

        var source = _compiledScript.IterationSources[sourceIndex];
        switch (source.Kind)
        {
            case ExperimentalIterationSourceKind.Collection:
                var compiledCollection = EvaluateCompiledExpression(context, source.CollectionExpression);
                if (!context.TryCheckMaterializedValue(compiledCollection, "Iteration source would enumerate more range items than allowed."))
                {
                    yield break;
                }

                foreach (var item in compiledCollection.AsEnumerable())
                {
                    yield return item;
                }

                yield break;
            case ExperimentalIterationSourceKind.Range:
            {
                var fromValue = EvaluateCompiledExpression(context, source.FromExpression);
                var toValue = EvaluateCompiledExpression(context, source.ToExpression);
                var stepValue = source.StepExpression is null
                    ? EventScriptValueFactory.Integer(1)
                    : EvaluateCompiledExpression(context, source.StepExpression);

                if (!TryCoerceNumericForOperation(fromValue, out var fromNumber) ||
                    !TryCoerceNumericForOperation(toValue, out var toNumber) ||
                    !TryCoerceNumericForOperation(stepValue, out var stepNumber) ||
                    !fromNumber.IsFinite ||
                    !toNumber.IsFinite ||
                    !stepNumber.IsFinite)
                {
                    yield break;
                }

                var range = EventScriptValueFactory.Range(
                    ToIntegerSaturated(fromNumber.Value),
                    ToIntegerSaturated(toNumber.Value),
                    ToIntegerSaturated(stepNumber.Value));
                if (!context.TryCheckRangeLength(range, "Range item count exceeds the configured limit."))
                {
                    yield break;
                }

                foreach (var item in range.AsEnumerable())
                {
                    yield return item;
                }

                yield break;
            }
        }
    }

    private IEnumerable<EventScriptValue> EnumerateIterationSource(ExperimentalCompiledExecutionContext context, IterationSourceNode source)
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

    private bool TryEvaluateRangeExpression(ExperimentalCompiledExecutionContext context, RangeExpressionNode rangeExpression, out EventScriptValue range)
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

    private EventScriptValue EvaluateUnaryExpression(ExperimentalCompiledExecutionContext context, UnaryExpressionNode unary)
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

        if (unwrapped.IsDegree())
        {
            return EventScriptValueFactory.Degree(-unwrapped.AsNumber());
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

    private EventScriptValue EvaluateVariadicTaggedExpression(ExperimentalCompiledExecutionContext context, VariadicTaggedExpressionNode variadic)
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

    private EventScriptValue EvaluateClampExpression(ExperimentalCompiledExecutionContext context, ClampExpressionNode clamp)
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
        return EventScriptValueFactory.Decimal(Math.Min(Math.Max(rawNumber.Value, lower), upper));
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

    private EventScriptValue EvaluateLenUnary(ExperimentalCompiledExecutionContext context, EventScriptValue operand)
    {
        if (operand.IsNothing())
        {
            return EventScriptValueFactory.Integer(0);
        }

        return operand.Kind switch
        {
            EventScriptValueKind.Text => EventScriptValueFactory.Integer(operand.AsText().Length),
            EventScriptValueKind.Iterator => CountEnumerableWithBudget(context, operand.AsEnumerable(), "Iterator length evaluation budget exhausted."),
            EventScriptValueKind.Range => EvaluateRangeLength(context, operand),
            EventScriptValueKind.List => EventScriptValueFactory.Integer(operand.AsList().Count),
            EventScriptValueKind.Dictionary => EventScriptValueFactory.Integer(operand.AsDictionary().Count),
            EventScriptValueKind.Set => EventScriptValueFactory.Integer(operand.AsSet().Count),
            EventScriptValueKind.Dice => EventScriptValueFactory.Integer(operand.AsDice().Rolls.Count),
            EventScriptValueKind.Optional => EventScriptValueFactory.Integer(operand.AsOptional().HasValue ? 1 : 0),
            _ => EventScriptValue.Nothing
        };
    }

    private EventScriptValue EvaluateRangeLength(ExperimentalCompiledExecutionContext context, EventScriptValue operand)
    {
        if (!EventScriptRuntimeLimitUtilities.TryGetRangeLength(operand, out var length))
        {
            return EventScriptValue.Nothing;
        }

        return context.TryCheckRangeLength(operand, "Range length exceeds the configured limit.")
            ? EventScriptValueFactory.Integer(length)
            : EventScriptValue.Nothing;
    }

    private EventScriptValue CountEnumerableWithBudget(ExperimentalCompiledExecutionContext context, IEnumerable<EventScriptValue> values, string detail)
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

        if (EventScriptValueAlu.TryEvaluateDegreeRounding(operand, operation, out var degree))
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

        if (!TryCoerceNumericForOperation(operand, out var number) || !number.IsFinite)
        {
            return EventScriptValue.Nothing;
        }

        return EventScriptValueFactory.Decimal(Math.Abs(number.Value));
    }

    private static EventScriptValue EvaluateMinMax(IReadOnlyList<EventScriptValue> values, bool isMax)
        => EventScriptValueAlu.EvaluateMinMax(values, isMax);

    private bool EvaluateSequencePattern(ExperimentalCompiledExecutionContext context, EventScriptValue target, DicePatternNode pattern)
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

    private bool MatchDiceCountPattern(ExperimentalCompiledExecutionContext context, IReadOnlyDictionary<EventScriptValue, int> counts, DiceCountPatternNode pattern)
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

    private EventScriptValue EvaluateBinaryExpression(ExperimentalCompiledExecutionContext context, BinaryExpressionNode binary)
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
                if (!TryCompareDegreeAware(left, right, out var lessComparison))
                {
                    return EventScriptValueFactory.Boolean(false);
                }

                return EventScriptValueFactory.Boolean(lessComparison < 0);
            case ">":
                if (!TryCompareDegreeAware(left, right, out var greaterComparison))
                {
                    return EventScriptValueFactory.Boolean(false);
                }

                return EventScriptValueFactory.Boolean(greaterComparison > 0);
            case "<=":
                if (!TryCompareDegreeAware(left, right, out var lessOrEqualComparison))
                {
                    return EventScriptValueFactory.Boolean(false);
                }

                return EventScriptValueFactory.Boolean(lessOrEqualComparison <= 0);
            case ">=":
                if (!TryCompareDegreeAware(left, right, out var greaterOrEqualComparison))
                {
                    return EventScriptValueFactory.Boolean(false);
                }

                return EventScriptValueFactory.Boolean(greaterOrEqualComparison >= 0);
            case "+":
            {
                if (EventScriptValueAlu.TryEvaluateDegreeBinary(left, "+", right, out var degree))
                {
                    return degree;
                }

                if (TryCoerceNumericForOperation(left, out var leftNumeric) &&
                    TryCoerceNumericForOperation(right, out var rightNumeric))
                {
                    return ToEventScriptDecimal(AddNumeric(leftNumeric, rightNumeric));
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
                if (EventScriptValueAlu.TryEvaluateDegreeBinary(left, "-", right, out var degreeDifference))
                {
                    return degreeDifference;
                }

                if (!TryCoerceNumericForOperation(left, out var leftMinus) ||
                    !TryCoerceNumericForOperation(right, out var rightMinus))
                {
                    return EventScriptValueFactory.DecimalNaN();
                }

                return ToEventScriptDecimal(SubtractNumeric(leftMinus, rightMinus));
            case "*":
                if (EventScriptValueAlu.TryEvaluateDegreeBinary(left, "*", right, out var degreeProduct))
                {
                    return degreeProduct;
                }

                if (!TryCoerceNumericForOperation(left, out var leftMultiply) ||
                    !TryCoerceNumericForOperation(right, out var rightMultiply))
                {
                    return EventScriptValueFactory.DecimalNaN();
                }

                return ToEventScriptDecimal(MultiplyNumeric(leftMultiply, rightMultiply));
            case "/":
                if (EventScriptValueAlu.TryEvaluateDegreeBinary(left, "/", right, out var degreeQuotient))
                {
                    return degreeQuotient;
                }

                if (!TryCoerceNumericForOperation(left, out var leftDivide) ||
                    !TryCoerceNumericForOperation(right, out var rightDivide))
                {
                    return EventScriptValueFactory.DecimalNaN();
                }

                return ToEventScriptDecimal(DivideNumeric(leftDivide, rightDivide));
            case "%":
                if (EventScriptValueAlu.TryEvaluateDegreeBinary(left, "%", right, out var degreeModulo))
                {
                    return degreeModulo;
                }

                if (!TryCoerceNumericForOperation(left, out var leftModulo) ||
                    !TryCoerceNumericForOperation(right, out var rightModulo))
                {
                    return EventScriptValueFactory.DecimalNaN();
                }

                return ToEventScriptDecimal(ModuloNumeric(leftModulo, rightModulo));
            default:
                return EventScriptValue.Nothing;
        }
    }

    private EventScriptValue EvaluateMemberAccess(ExperimentalCompiledExecutionContext context, MemberAccessExpressionNode memberAccess)
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

    private EventScriptValue EvaluateCollectionAccess(ExperimentalCompiledExecutionContext context, CollectionAccessExpressionNode collectionAccess)
    {
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

    private static bool TryMaterializeCollectionAccessTarget(ExperimentalCompiledExecutionContext context, EventScriptValue target, out IReadOnlyList<EventScriptValue> items)
    {
        if (!context.TryCheckMaterializedValue(target, "Collection access would materialize more range items than allowed."))
        {
            items = Array.Empty<EventScriptValue>();
            return false;
        }

        items = target.AsList();
        return true;
    }

    private static bool TryEnumerateCollectionAccessTarget(ExperimentalCompiledExecutionContext context, EventScriptValue target, out IEnumerable<EventScriptValue> items)
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

        return value.Kind is EventScriptValueKind.Range or EventScriptValueKind.Iterator
            ? value.AsEnumerable()
            : value.AsList();
    }

    private EventScriptProjectionEvaluator CreateProjectionEvaluator(ExperimentalCompiledExecutionContext context)
        => new(
            context.PushScope,
            context.PopScope,
            context.Define,
            expression => EvaluateExpression(context, expression));

    private EventScriptValue EvaluateEdgeSelector(ExperimentalCompiledExecutionContext context, IEnumerable<EventScriptValue> items, EdgeSelectorNode selector)
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

    private EventScriptValue EvaluateIndexedCollectionAccess(ExperimentalCompiledExecutionContext context, EventScriptValue target, ExpressionNode selectorExpression)
    {
        var selector = EvaluateExpression(context, selectorExpression);
        if (selector.IsNothing())
        {
            return EventScriptValue.Nothing;
        }

        return target.Lookup(selector);
    }

    private EventScriptValue EvaluateTakePattern(ExperimentalCompiledExecutionContext context, EventScriptValue target, DicePatternNode pattern)
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

    private bool EvaluateObjectMatchSelector(ExperimentalCompiledExecutionContext context, EventScriptValue target, ObjectMatchPatternNode pattern)
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

    private bool TryTakeSequencePattern(ExperimentalCompiledExecutionContext context, IReadOnlyList<EventScriptValue> items, DicePatternNode pattern, out IReadOnlyList<EventScriptValue> takenItems)
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

    private bool TryTakeCountPattern(ExperimentalCompiledExecutionContext context, IReadOnlyList<EventScriptValue> items, IReadOnlyDictionary<EventScriptValue, int> counts, DiceCountPatternNode pattern, out IReadOnlyList<EventScriptValue> takenItems)
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

    private EventScriptValue EvaluatePredicateSelector(ExperimentalCompiledExecutionContext context, IEnumerable<EventScriptValue> items, PredicateSelectorNode selector)
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

    private EventScriptValue EvaluateCountSelector(ExperimentalCompiledExecutionContext context, IEnumerable<EventScriptValue> items, CountSelectorNode selector)
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

    private EventScriptValue EvaluateChooseSelector(ExperimentalCompiledExecutionContext context, IReadOnlyList<EventScriptValue> items, ChooseSelectorNode selector)
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

    private EventScriptValue EvaluateFilterSelector(ExperimentalCompiledExecutionContext context, IEnumerable<EventScriptValue> items, FilterSelectorNode selector)
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

    private EventScriptValue EvaluateSumSelector(ExperimentalCompiledExecutionContext context, IEnumerable<EventScriptValue> items, SumSelectorNode selector)
    {
        var sum = NumericValue.Finite(0m);
        var projection = CreateProjectionEvaluator(context);
        foreach (var item in items)
        {
            if (!TryCoerceNumericForOperation(projection.Evaluate(selector.Identifier, selector.Projection, item), out var number))
            {
                return EventScriptValueFactory.DecimalNaN();
            }

            sum = AddNumeric(sum, number);
        }

        return ToEventScriptDecimal(sum);
    }

    private EventScriptValue EvaluateAverageSelector(ExperimentalCompiledExecutionContext context, IEnumerable<EventScriptValue> items, AverageSelectorNode selector)
    {
        var sum = NumericValue.Finite(0m);
        var count = 0;
        var projection = CreateProjectionEvaluator(context);
        foreach (var item in items)
        {
            if (!TryCoerceNumericForOperation(projection.Evaluate(selector.Identifier, selector.Projection, item), out var number) || !number.IsFinite)
            {
                return EventScriptValue.Nothing;
            }

            sum = AddNumeric(sum, number);
            count++;
        }

        return count == 0 || !sum.IsFinite
            ? EventScriptValue.Nothing
            : EventScriptValueFactory.Decimal(sum.Value / count);
    }

    private EventScriptValue EvaluateSelectSelector(ExperimentalCompiledExecutionContext context, IEnumerable<EventScriptValue> items, SelectSelectorNode selector)
    {
        var result = new List<EventScriptValue>();
        var projection = CreateProjectionEvaluator(context);
        foreach (var item in items)
        {
            result.Add(projection.Evaluate(selector.Identifier, selector.Projection, item));
        }

        return EventScriptValueFactory.List(result);
    }

    private EventScriptValue EvaluateDictionarySelector(ExperimentalCompiledExecutionContext context, IEnumerable<EventScriptValue> items, DictionarySelectorNode selector)
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

    private EventScriptValue EvaluateContainsSelector(ExperimentalCompiledExecutionContext context, EventScriptValue target, ContainsSelectorNode selector)
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

    private EventScriptValue EvaluateExtremaSelector(ExperimentalCompiledExecutionContext context, IEnumerable<EventScriptValue> items, string identifier, ExpressionNode projection, bool isMax)
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

            var comparison = EventScriptValue.StableComparer.Compare(candidateProjection, bestProjection);
            if ((isMax && comparison > 0) || (!isMax && comparison < 0))
            {
                bestProjection = candidateProjection;
                bestItem = item;
            }
        }

        return bestItem ?? EventScriptValue.Nothing;
    }

    private EventScriptValue EvaluateSortSelector(ExperimentalCompiledExecutionContext context, EventScriptValue target, IEnumerable<EventScriptValue> items, SortSelectorNode selector)
    {
        _ = context;
        return EventScriptCollectionOperators.Sort(target, items, selector.Direction);
    }

    private EventScriptValue EvaluateOrderBySelector(ExperimentalCompiledExecutionContext context, EventScriptValue target, IEnumerable<EventScriptValue> items, OrderBySelectorNode selector)
    {
        var projection = CreateProjectionEvaluator(context);
        return EventScriptCollectionOperators.OrderBy(
            target,
            items,
            selector.Direction,
            item => projection.Evaluate(selector.Identifier, selector.Projection, item));
    }

    private EventScriptValue EvaluateDistinctSelector(ExperimentalCompiledExecutionContext context, EventScriptValue target, IEnumerable<EventScriptValue> items, DistinctSelectorNode selector)
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

    private EventScriptValue EvaluateGroupBySelector(ExperimentalCompiledExecutionContext context, IEnumerable<EventScriptValue> items, GroupBySelectorNode selector)
    {
        var projection = CreateProjectionEvaluator(context);
        return EventScriptCollectionOperators.GroupBy(
            items,
            item => projection.Evaluate(selector.Identifier, selector.Projection, item));
    }

    private bool MatchesObjectPattern(ExperimentalCompiledExecutionContext context, EventScriptValue value, ObjectMatchPatternNode pattern)
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

    private static bool TryCompareDegreeAware(EventScriptValue left, EventScriptValue right, out int comparison)
        => EventScriptValueAlu.TryCompareDegreeAware(left, right, out comparison);

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

    private EventScriptValue ConvertToDeclaredType(ExperimentalCompiledExecutionContext context, EventScriptValue value, string declaredType)
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
                return ConvertToDegree(value);
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

                if (unwrappedNumber.IsDegree())
                {
                    return EventScriptValueFactory.Decimal(unwrappedNumber.AsNumber());
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

    private EventScriptValue ConvertToDegree(EventScriptValue value)
    {
        if (!TryUnwrapOptionalForOperation(value, out var unwrapped))
        {
            return EventScriptValueFactory.DecimalNaN();
        }

        if (unwrapped.IsDegree())
        {
            return unwrapped;
        }

        if (unwrapped.Kind is EventScriptValueKind.Decimal or EventScriptValueKind.Integer or EventScriptValueKind.Percentage &&
            TryCoerceNumericForOperation(unwrapped, out var number) &&
            number.IsFinite)
        {
            return EventScriptValueFactory.Degree(number.Value);
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
        if (!TryUnwrapOptionalForOperation(component, out var unwrapped) ||
            !TryCoerceNumericForOperation(unwrapped, out var number) ||
            !number.IsFinite)
        {
            value = default;
            return false;
        }

        value = number.Value;
        return true;
    }

    private EventScriptValue ConvertToCustomType(ExperimentalCompiledExecutionContext context, EventScriptValue value, ExperimentalCompiledTypeDefinition typeDefinition)
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

    private EventScriptValue ApplyFieldClamp(ExperimentalCompiledTypeDefinition typeDefinition, ExperimentalCompiledTypeFieldDefinition field, EventScriptValue fieldValue, IReadOnlyDictionary<string, EventScriptValue> sourceValues, IReadOnlyDictionary<string, EventScriptValue> materializedValues)
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
                return EventScriptValueFactory.Decimal(Math.Max(valueNumber.Value, minimumNumber.Value));
            }

            return fieldValue;
        }

        var lower = Math.Min(minimumNumber.Value, maximumNumber.Value);
        var upper = Math.Max(minimumNumber.Value, maximumNumber.Value);
        return EventScriptValueFactory.Decimal(Math.Min(Math.Max(valueNumber.Value, lower), upper));
    }

    private EventScriptValue EvaluateCustomTypeExpression(ExperimentalCompiledTypeDefinition typeDefinition, ExperimentalCompiledExpression expression, IReadOnlyDictionary<string, EventScriptValue> sourceValues, IReadOnlyDictionary<string, EventScriptValue> materializedValues)
    {
        var context = new ExperimentalCompiledExecutionContext(_context, _diagnosticsEnabled);
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

            return EvaluateCompiledExpression(context, expression);
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
            "degree" => value.IsDegree(),
            "vector2" => value.IsVector2(),
            "vector3" => value.IsVector3(),
            "decimal" => value.IsNumber(),
            "integer" => value.IsInteger(),
            "boolean" => value.Kind == EventScriptValueKind.Boolean,
            "optional" => value.IsOptional(),
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
            EventScriptValueKind.Degree => $"degree:{value.AsNumber().ToString(CultureInfo.InvariantCulture)}",
            EventScriptValueKind.Vector2 => $"vector2:{((EventScriptVector2Value)value).X.ToString(CultureInfo.InvariantCulture)}:{((EventScriptVector2Value)value).Y.ToString(CultureInfo.InvariantCulture)}",
            EventScriptValueKind.Vector3 => $"vector3:{((EventScriptVector3Value)value).X.ToString(CultureInfo.InvariantCulture)}:{((EventScriptVector3Value)value).Y.ToString(CultureInfo.InvariantCulture)}:{((EventScriptVector3Value)value).Z.ToString(CultureInfo.InvariantCulture)}",
            EventScriptValueKind.Decimal => value.IsNaN()
                ? "decimal:nan"
                : value.IsNegativeInfinity()
                    ? "decimal:-infinity"
                    : value.IsInfinity()
                        ? "decimal:infinity"
                        : $"decimal:{value.AsNumber().ToString(CultureInfo.InvariantCulture)}",
            EventScriptValueKind.Integer => $"integer:{value.AsInteger().ToString(CultureInfo.InvariantCulture)}",
            EventScriptValueKind.Boolean => $"boolean:{(value.AsBoolean() ? "true" : "false")}",
            EventScriptValueKind.Optional => value.AsOptional().HasValue
                ? $"optional:{BuildStableSeedText(value.AsOptional().Value)}"
                : "optional:none",
            EventScriptValueKind.Iterator => $"iterator:[{string.Join("|", value.AsEnumerable().Select(BuildStableSeedText))}]",
            EventScriptValueKind.Range => $"range:{((EventScriptRangeValue)value).From}:{((EventScriptRangeValue)value).To}:{((EventScriptRangeValue)value).Step}",
            EventScriptValueKind.Message => $"message:{((EventScriptMessageValue)value).Value.SignatureId}:[{string.Join("|", ((EventScriptMessageValue)value).Value.Arguments.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={BuildStableSeedText(pair.Value)}"))}]",
            EventScriptValueKind.Handler => $"handler:{((EventScriptHandlerValue)value).Signature.SignatureId}",
            EventScriptValueKind.List => $"list:[{string.Join("|", value.AsList().Select(BuildStableSeedText))}]",
            EventScriptValueKind.Dictionary => $"dict:[{string.Join("|", value.AsDictionary().OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={BuildStableSeedText(pair.Value)}"))}]",
            EventScriptValueKind.Set => $"set:[{string.Join("|", value.AsSet().OrderBy(item => item, EventScriptValue.StableComparer).Select(BuildStableSeedText))}]",
            EventScriptValueKind.Dice => $"dice:[{string.Join("|", value.AsDice().Rolls)}]",
            _ => value.ToString()
        };
    }

}
}
