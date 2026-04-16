#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace StepH.Flow.EventScript;

public sealed record EventScriptEmittedEvent(string Message, IReadOnlyList<object?> Arguments);

public sealed record EventScriptExecutionResult(string Message, IReadOnlyList<EventScriptEmittedEvent> EmittedEvents, IReadOnlyDictionary<string, object?> Variables);

public interface IEventScriptRandom
{
    int NextInclusive(int minInclusive, int maxInclusive);
}

public sealed class DefaultEventScriptRandom(Random? random = null) : IEventScriptRandom
{
    private readonly Random _random = random ?? new Random();

    public int NextInclusive(int minInclusive, int maxInclusive)
    {
        if (minInclusive > maxInclusive) throw new ArgumentOutOfRangeException(nameof(minInclusive), "minInclusive must be <= maxInclusive");
        if (maxInclusive != int.MaxValue) return _random.Next(minInclusive, maxInclusive + 1);
        var sample = _random.NextDouble();
        return minInclusive + (int)Math.Floor(sample * ((long)maxInclusive - minInclusive + 1));
    }
}

public sealed class EventScriptRuntimeException(string message) : Exception(message);

public sealed class EventScriptInterpreter
{
    private readonly Dictionary<string, List<EventHandlerNode>> _handlers;
    private readonly Dictionary<string, EventHandlerNode> _externalHandlers;
    private readonly Dictionary<string, EventScriptExternalMessageBinding> _externalBindings;
    private readonly IEventScriptRandom _random;
    private readonly int _maxEmitDepth;
    private readonly Dictionary<(Type Type, string Member), Func<object, object?>> _memberAccessCache = new();

    private static readonly IReadOnlyDictionary<string, object?> EmptyVariables = new Dictionary<string, object?>(StringComparer.Ordinal);

    private EventScriptInterpreter(EventScriptProgram eventScriptProgram, IEventScriptRandom? random = null, EventScriptCompilationContext? context = null)
    {
        _ = eventScriptProgram ?? throw new ArgumentNullException(nameof(eventScriptProgram));
        _random = random ?? new DefaultEventScriptRandom();
        _handlers = BuildHandlerMap(eventScriptProgram);
        _externalHandlers = BuildExternalHandlerMap(eventScriptProgram);
        _externalBindings = new Dictionary<string, EventScriptExternalMessageBinding>(context?.ExternalBindings ?? EmptyExternalBindings, StringComparer.Ordinal);
        _maxEmitDepth = context?.MaxEmitDepth ?? 64;
        if (_maxEmitDepth <= 0) throw new EventScriptCompilationException("Max emit depth must be > 0");
        ValidateEndpointConfiguration(_handlers, _externalHandlers, _externalBindings);
    }

    public static EventScriptInterpreter Compile(string script, IEventScriptRandom? random = null, EventScriptCompilationContext? context = null)
        => new(EventScriptParser.Parse(script), random, context);

    public EventScriptExecutionResult Emit(string message, params object[] args)
    {
        var dispatchState = new DispatchState(_maxEmitDepth);
        var variables = DispatchInternalMessage(message, args, dispatchState, captureVariables: true);
        return new EventScriptExecutionResult(message, dispatchState.EmittedEvents, variables);
    }

    private IReadOnlyDictionary<string, object?> DispatchInternalMessage(string message, IReadOnlyList<object?> args, DispatchState state, bool captureVariables)
    {
        if (!_handlers.TryGetValue(message, out var handlers)) throw new EventScriptRuntimeException($"No handler found for message '{message}'");
        state.Enter(message);
        try
        {
            var variables = captureVariables ? new Dictionary<string, object?>(StringComparer.Ordinal) : null;
            foreach (var handler in handlers)
            {
                var context = new ExecutionContext(state.EmittedEvents);
                var handlerVariables = ExecuteHandler(context, state, handler, args);
                if (variables == null) continue;
                foreach (var pair in handlerVariables)
                {
                    variables[pair.Key] = pair.Value;
                }
            }

            return variables ?? EmptyVariables;
        }
        finally
        {
            state.Exit(message);
        }
    }

    private IReadOnlyDictionary<string, object?> ExecuteHandler(ExecutionContext context, DispatchState state, EventHandlerNode handler, IReadOnlyList<object?> args)
    {
        if (handler.Parameters.Count != args.Count) throw new EventScriptRuntimeException($"Handler '{handler.Message}' expects {handler.Parameters.Count} arguments but got {args.Count}");

        context.PushScope();
        try
        {
            for (var i = 0; i < handler.Parameters.Count; i++)
            {
                context.Define(handler.Parameters[i], args[i]);
            }

            ExecuteStatements(context, state, handler.Statements);
            return context.SnapshotTopScope();
        }
        finally
        {
            context.PopScope();
        }
    }

    private void ExecuteStatements(ExecutionContext context, DispatchState state, IReadOnlyList<StatementNode> statements)
    {
        foreach (var statement in statements)
        {
            ExecuteStatement(context, state, statement);
        }
    }

    private void ExecuteStatement(ExecutionContext context, DispatchState state, StatementNode statement)
    {
        switch (statement)
        {
            case EmitStatementNode emit:
                var args = emit.Arguments.Select(argument => EvaluateExpression(context, argument)).ToArray();
                EmitMessage(context, state, emit.Message, args);
                return;

            case LetStatementNode let:
                context.Define(let.Identifier, EvaluateExpression(context, let.Expression));
                return;

            case IfStatementNode ifStatement:
                ExecuteStatements(context, state, AsBool(EvaluateExpression(context, ifStatement.Condition)) ? ifStatement.ThenStatements : ifStatement.ElseStatements);
                return;

            case ForStatementNode forStatement:
                foreach (var item in AsEnumerable(EvaluateExpression(context, forStatement.Source)))
                {
                    context.PushScope();
                    try
                    {
                        context.Define(forStatement.Identifier, item);
                        ExecuteStatements(context, state, forStatement.Statements);
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
                throw new EventScriptRuntimeException($"Unsupported statement type: {statement.GetType().Name}");
        }
    }

    private void EmitMessage(ExecutionContext context, DispatchState state, string message, IReadOnlyList<object?> args)
    {
        ValidateEmitArguments(message, args.Count);
        context.Emit(new EventScriptEmittedEvent(message, args.ToArray()));

        if (_handlers.ContainsKey(message))
        {
            DispatchInternalMessage(message, args, state, captureVariables: false);
            return;
        }

        if (_externalBindings.TryGetValue(message, out var binding))
        {
            binding.Handler(args);
        }
    }

    private object? EvaluateExpression(ExecutionContext context, ExpressionNode expression)
    {
        switch (expression)
        {
            case NumberLiteralExpressionNode number:
                return number.Value;

            case StringLiteralExpressionNode text:
                return text.Value;

            case BooleanLiteralExpressionNode boolean:
                return boolean.Value;

            case IdentifierExpressionNode identifier:
                return context.Resolve(identifier.Name);

            case UnaryExpressionNode unary:
                if (unary.Operator == "!")
                {
                    return !AsBool(EvaluateExpression(context, unary.Operand));
                }

                throw new EventScriptRuntimeException($"Unsupported unary operator '{unary.Operator}'");

            case RandomExpressionNode randomExpression:
                return EvaluateRandomExpression(context, randomExpression);

            case DiceExpressionNode diceExpression:
                return EvaluateDiceExpression(diceExpression);

            case BinaryExpressionNode binary:
                return EvaluateBinaryExpression(context, binary);

            case MemberAccessExpressionNode memberAccess:
                return EvaluateMemberAccess(context, memberAccess);

            case CollectionAccessExpressionNode collectionAccess:
                return EvaluateCollectionAccess(context, collectionAccess);

            default:
                throw new EventScriptRuntimeException($"Unsupported expression type: {expression.GetType().Name}");
        }
    }

    private object EvaluateRandomExpression(ExecutionContext context, RandomExpressionNode randomExpression)
    {
        var from = AsInt(EvaluateExpression(context, randomExpression.FromExpression));
        var to = AsInt(EvaluateExpression(context, randomExpression.ToExpression));
        return from > to ? throw new EventScriptRuntimeException($"Invalid random range: {from}..{to}") : _random.NextInclusive(from, to);
    }

    private object EvaluateDiceExpression(DiceExpressionNode diceExpression)
    {
        if (diceExpression.DiceCount <= 0) throw new EventScriptRuntimeException("Dice count must be > 0");
        if (diceExpression.SideCount <= 0) throw new EventScriptRuntimeException("Dice side count must be > 0");

        var rolls = new int[diceExpression.DiceCount];
        var total = 0;
        for (var i = 0; i < rolls.Length; i++)
        {
            rolls[i] = _random.NextInclusive(1, diceExpression.SideCount);
            total += rolls[i];
        }

        if (diceExpression.Modifier == null) return total;

        switch (diceExpression.Modifier)
        {
            case KeepHighestModifierNode keepHighest:
                if (keepHighest.Count > rolls.Length) throw new EventScriptRuntimeException("Cannot keep more dice than exist");

                Array.Sort(rolls);
                var keepTotal = 0;
                for (var i = rolls.Length - keepHighest.Count; i < rolls.Length; i++)
                {
                    keepTotal += rolls[i];
                }

                return keepTotal;

            case DropLowestModifierNode dropLowest:
                if (dropLowest.Count > rolls.Length) throw new EventScriptRuntimeException("Cannot drop more dice than exist");

                Array.Sort(rolls);
                var dropTotal = 0;
                for (var i = dropLowest.Count; i < rolls.Length; i++)
                {
                    dropTotal += rolls[i];
                }

                return dropTotal;

            default:
                throw new EventScriptRuntimeException($"Unsupported dice modifier: {diceExpression.Modifier.GetType().Name}");
        }
    }

    private object? EvaluateBinaryExpression(ExecutionContext context, BinaryExpressionNode binary)
    {
        switch (binary.Operator)
        {
            case "||":
            {
                var left = AsBool(EvaluateExpression(context, binary.Left));
                return left || AsBool(EvaluateExpression(context, binary.Right));
            }
            case "&&":
            {
                var left = AsBool(EvaluateExpression(context, binary.Left));
                return left && AsBool(EvaluateExpression(context, binary.Right));
            }
            case "==":
                return AreEqual(EvaluateExpression(context, binary.Left), EvaluateExpression(context, binary.Right));
            case "!=":
                return !AreEqual(EvaluateExpression(context, binary.Left), EvaluateExpression(context, binary.Right));
            case "<":
                return AsDecimal(EvaluateExpression(context, binary.Left)) < AsDecimal(EvaluateExpression(context, binary.Right));
            case ">":
                return AsDecimal(EvaluateExpression(context, binary.Left)) > AsDecimal(EvaluateExpression(context, binary.Right));
            case "<=":
                return AsDecimal(EvaluateExpression(context, binary.Left)) <= AsDecimal(EvaluateExpression(context, binary.Right));
            case ">=":
                return AsDecimal(EvaluateExpression(context, binary.Left)) >= AsDecimal(EvaluateExpression(context, binary.Right));
            case "+":
            {
                var left = EvaluateExpression(context, binary.Left);
                var right = EvaluateExpression(context, binary.Right);
                if (left is string || right is string)
                {
                    return $"{left}{right}";
                }

                return AsDecimal(left) + AsDecimal(right);
            }
            case "-":
                return AsDecimal(EvaluateExpression(context, binary.Left)) - AsDecimal(EvaluateExpression(context, binary.Right));
            case "*":
                return AsDecimal(EvaluateExpression(context, binary.Left)) * AsDecimal(EvaluateExpression(context, binary.Right));
            case "/":
            {
                var left = AsDecimal(EvaluateExpression(context, binary.Left));
                var divisor = AsDecimal(EvaluateExpression(context, binary.Right));
                if (divisor == 0)
                {
                    throw new EventScriptRuntimeException("Division by zero");
                }

                return left / divisor;
            }
            case "%":
            {
                var left = AsDecimal(EvaluateExpression(context, binary.Left));
                var divisor = AsDecimal(EvaluateExpression(context, binary.Right));
                if (divisor == 0)
                {
                    throw new EventScriptRuntimeException("Modulo by zero");
                }

                return left % divisor;
            }
            default:
                throw new EventScriptRuntimeException($"Unsupported binary operator '{binary.Operator}'");
        }
    }

    private object? EvaluateMemberAccess(ExecutionContext context, MemberAccessExpressionNode memberAccess)
    {
        var target = EvaluateExpression(context, memberAccess.Target);
        if (target == null)
        {
            throw new EventScriptRuntimeException($"Cannot access member '{memberAccess.Member}' on null");
        }

        if (TryGetStringDictionaryValue(target, memberAccess.Member, out var isDictionary, out var value))
        {
            return value;
        }

        if (isDictionary)
        {
            throw new EventScriptRuntimeException($"Member '{memberAccess.Member}' not found");
        }

        return GetMemberAccessor(target.GetType(), memberAccess.Member)(target);
    }

    private object? EvaluateCollectionAccess(ExecutionContext context, CollectionAccessExpressionNode collectionAccess)
    {
        var target = EvaluateExpression(context, collectionAccess.Target);
        var items = AsList(target);

        switch (collectionAccess.Selector)
        {
            case CountSelectorNode:
                return items.Count;

            case ExpressionSelectorNode expressionSelector:
                var index = AsInt(EvaluateExpression(context, expressionSelector.Expression));
                if (index < 0 || index >= items.Count)
                {
                    throw new EventScriptRuntimeException($"Collection index out of range: {index}");
                }

                return items[index];

            case PredicateSelectorNode predicateSelector:
                return EvaluatePredicateSelector(context, items, predicateSelector);

            case FilterSelectorNode filterSelector:
                return EvaluateFilterSelector(context, items, filterSelector);

            case SumSelectorNode sumSelector:
                return EvaluateSumSelector(context, items, sumSelector);

            case SelectSelectorNode selectSelector:
                return EvaluateSelectSelector(context, items, selectSelector);

            default:
                throw new EventScriptRuntimeException($"Unsupported collection selector: {collectionAccess.Selector.GetType().Name}");
        }
    }

    private object EvaluatePredicateSelector(
        ExecutionContext context,
        IReadOnlyList<object?> items,
        PredicateSelectorNode selector)
    {
        var isAny = string.Equals(selector.Operator, "any", StringComparison.Ordinal);
        if (!isAny && !string.Equals(selector.Operator, "all", StringComparison.Ordinal))
        {
            throw new EventScriptRuntimeException($"Unsupported collection predicate operator '{selector.Operator}'");
        }

        if (!isAny && items.Count == 0)
        {
            return true;
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
                    return true;
                }

                if (!isAny && !predicateResult)
                {
                    return false;
                }
            }
            finally
            {
                context.PopScope();
            }
        }

        return !isAny;
    }

    private object EvaluateFilterSelector(
        ExecutionContext context,
        IReadOnlyList<object?> items,
        FilterSelectorNode selector)
    {
        var result = new List<object?>();
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

        return result;
    }

    private object EvaluateSumSelector(
        ExecutionContext context,
        IReadOnlyList<object?> items,
        SumSelectorNode selector)
    {
        decimal sum = 0;
        foreach (var item in items)
        {
            context.PushScope();
            try
            {
                context.Define(selector.Identifier, item);
                sum += AsDecimal(EvaluateExpression(context, selector.Projection));
            }
            finally
            {
                context.PopScope();
            }
        }

        return sum;
    }

    private object EvaluateSelectSelector(
        ExecutionContext context,
        IReadOnlyList<object?> items,
        SelectSelectorNode selector)
    {
        var result = new List<object?>(items.Count);
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

        return result;
    }

    private Func<object, object?> GetMemberAccessor(Type type, string member)
    {
        if (_memberAccessCache.TryGetValue((type, member), out var accessor))
        {
            return accessor;
        }

        var property = type.GetProperty(member, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (property != null)
        {
            accessor = instance => property.GetValue(instance);
            _memberAccessCache[(type, member)] = accessor;
            return accessor;
        }

        var field = type.GetField(member, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (field != null)
        {
            accessor = instance => field.GetValue(instance);
            _memberAccessCache[(type, member)] = accessor;
            return accessor;
        }

        throw new EventScriptRuntimeException($"Member '{member}' not found on type {type.Name}");
    }

    private static Dictionary<string, List<EventHandlerNode>> BuildHandlerMap(EventScriptProgram eventScriptProgram)
    {
        var map = new Dictionary<string, List<EventHandlerNode>>(StringComparer.Ordinal);
        foreach (var handler in eventScriptProgram.Handlers)
        {
            if (handler.IsExternal)
            {
                continue;
            }

            if (!map.TryGetValue(handler.Message, out var handlers))
            {
                handlers = new List<EventHandlerNode>();
                map[handler.Message] = handlers;
            }

            handlers.Add(handler);
        }

        foreach (var pair in map)
        {
            var expectedParameterCount = pair.Value[0].Parameters.Count;
            if (pair.Value.Any(handler => handler.Parameters.Count != expectedParameterCount))
            {
                throw new EventScriptCompilationException(
                    $"All handlers for message '{pair.Key}' must declare the same parameter count");
            }
        }

        return map;
    }

    private static Dictionary<string, EventHandlerNode> BuildExternalHandlerMap(EventScriptProgram eventScriptProgram)
    {
        var map = new Dictionary<string, EventHandlerNode>(StringComparer.Ordinal);
        foreach (var handler in eventScriptProgram.Handlers)
        {
            if (!handler.IsExternal)
            {
                continue;
            }

            if (!map.TryAdd(handler.Message, handler))
            {
                throw new EventScriptCompilationException(
                    $"External message '{handler.Message}' is declared more than once");
            }
        }

        return map;
    }

    private static void ValidateEndpointConfiguration(
        IReadOnlyDictionary<string, List<EventHandlerNode>> handlers,
        IReadOnlyDictionary<string, EventHandlerNode> externalHandlers,
        IReadOnlyDictionary<string, EventScriptExternalMessageBinding> externalBindings)
    {
        foreach (var message in externalHandlers.Keys)
        {
            if (handlers.ContainsKey(message))
            {
                throw new EventScriptCompilationException(
                    $"Message '{message}' cannot be both internal and external");
            }
        }

        foreach (var binding in externalBindings.Values)
        {
            if (handlers.ContainsKey(binding.Message))
            {
                throw new EventScriptCompilationException(
                    $"External binding '{binding.Message}' conflicts with an internal handler");
            }

            if (externalHandlers.TryGetValue(binding.Message, out var externalHandler) &&
                binding.ParameterCount.HasValue &&
                binding.ParameterCount.Value != externalHandler.Parameters.Count)
            {
                throw new EventScriptCompilationException(
                    $"External binding '{binding.Message}' expects {binding.ParameterCount.Value} arguments but the script declares {externalHandler.Parameters.Count}");
            }
        }
    }

    private static bool AreEqual(object? left, object? right)
    {
        if (left == null || right == null)
        {
            return left == right;
        }

        if (IsNumeric(left) && IsNumeric(right))
        {
            return AsDecimal(left) == AsDecimal(right);
        }

        return Equals(left, right);
    }

    private static bool IsNumeric(object value)
    {
        return value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;
    }

    private static bool AsBool(object? value)
    {
        if (value is bool result)
        {
            return result;
        }

        throw new EventScriptRuntimeException($"Expected bool value, got {DescribeType(value)}");
    }

    private static int AsInt(object? value)
    {
        if (value is int intValue)
        {
            return intValue;
        }

        var decimalValue = AsDecimal(value);
        if (decimalValue != decimal.Truncate(decimalValue))
        {
            throw new EventScriptRuntimeException($"Expected integer value, got {decimalValue.ToString(CultureInfo.InvariantCulture)}");
        }

        if (decimalValue < int.MinValue || decimalValue > int.MaxValue)
        {
            throw new EventScriptRuntimeException($"Integer value out of range: {decimalValue.ToString(CultureInfo.InvariantCulture)}");
        }

        return (int)decimalValue;
    }

    private static decimal AsDecimal(object? value)
    {
        switch (value)
        {
            case decimal d:
                return d;
            case byte b:
                return b;
            case sbyte sb:
                return sb;
            case short s:
                return s;
            case ushort us:
                return us;
            case int i:
                return i;
            case uint ui:
                return ui;
            case long l:
                return l;
            case ulong ul:
                return ul;
            case float f:
                return (decimal)f;
            case double db:
                return (decimal)db;
            default:
                throw new EventScriptRuntimeException($"Expected numeric value, got {DescribeType(value)}");
        }
    }

    private static IReadOnlyList<object?> AsList(object? value)
    {
        if (value == null)
        {
            throw new EventScriptRuntimeException("Expected collection, got null");
        }

        if (value is IReadOnlyList<object?> readOnlyList)
        {
            return readOnlyList;
        }

        if (value is IList<object?> list)
        {
            return list.ToArray();
        }

        if (value is string text)
        {
            return text.Select(ch => (object?)ch.ToString()).ToArray();
        }

        if (value is IEnumerable enumerable)
        {
            var result = new List<object?>();
            foreach (var item in enumerable)
            {
                result.Add(item);
            }

            return result;
        }

        throw new EventScriptRuntimeException($"Expected collection, got {DescribeType(value)}");
    }

    private static IEnumerable<object?> AsEnumerable(object? value)
    {
        if (value == null)
        {
            throw new EventScriptRuntimeException("Expected enumerable, got null");
        }

        if (value is string text)
        {
            foreach (var item in text)
            {
                yield return item.ToString();
            }

            yield break;
        }

        if (value is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                yield return item;
            }

            yield break;
        }

        throw new EventScriptRuntimeException($"Expected enumerable, got {DescribeType(value)}");
    }

    private static string DescribeType(object? value) => value?.GetType().Name ?? "null";

    private void ValidateEmitArguments(string message, int argumentCount)
    {
        if (_handlers.TryGetValue(message, out var handlers))
        {
            var expected = handlers[0].Parameters.Count;
            if (expected != argumentCount)
            {
                throw new EventScriptRuntimeException(
                    $"Message '{message}' expects {expected} arguments but got {argumentCount}");
            }

            return;
        }

        if (_externalHandlers.TryGetValue(message, out var externalHandler) &&
            externalHandler.Parameters.Count != argumentCount)
        {
            throw new EventScriptRuntimeException(
                $"External message '{message}' expects {externalHandler.Parameters.Count} arguments but got {argumentCount}");
        }

        if (_externalBindings.TryGetValue(message, out var binding) &&
            binding.ParameterCount.HasValue &&
            binding.ParameterCount.Value != argumentCount)
        {
            throw new EventScriptRuntimeException(
                $"External binding '{message}' expects {binding.ParameterCount.Value} arguments but got {argumentCount}");
        }
    }

    private static bool TryGetStringDictionaryValue(object target, string key, out bool isDictionary, out object? value)
    {
        switch (target)
        {
            case IReadOnlyDictionary<string, object?> readOnlyDictionary:
                isDictionary = true;
                return readOnlyDictionary.TryGetValue(key, out value);
            case IDictionary<string, object?> dictionary:
                isDictionary = true;
                return dictionary.TryGetValue(key, out value);
            case IDictionary nonGenericDictionary:
            {
                isDictionary = true;
                if (nonGenericDictionary.Contains(key))
                {
                    value = nonGenericDictionary[key];
                    return true;
                }

                value = null;
                return false;
            }
        }

        foreach (var interfaceType in target.GetType().GetInterfaces())
        {
            if (!interfaceType.IsGenericType) continue;
            var genericDefinition = interfaceType.GetGenericTypeDefinition();
            if (genericDefinition != typeof(IDictionary<,>) && genericDefinition != typeof(IReadOnlyDictionary<,>)) continue;
            if (interfaceType.GetGenericArguments()[0] != typeof(string)) continue;
            var tryGetValue = interfaceType.GetMethod("TryGetValue");
            if (tryGetValue == null) continue;
            var parameters = new object?[] { key, null };
            isDictionary = true;
            var found = (bool)tryGetValue.Invoke(target, parameters)!;
            value = parameters[1];
            return found;
        }

        isDictionary = false;
        value = null;
        return false;
    }

    private sealed class ExecutionContext
    {
        private readonly Stack<Dictionary<string, object?>> _scopes = new();

        public ExecutionContext(List<EventScriptEmittedEvent> emittedEvents)
        {
            _scopes.Push(new Dictionary<string, object?>(StringComparer.Ordinal));
            EmittedEvents = emittedEvents;
        }

        public List<EventScriptEmittedEvent> EmittedEvents { get; }

        public void PushScope() => _scopes.Push(new Dictionary<string, object?>(StringComparer.Ordinal));

        public void PopScope()
        {
            if (_scopes.Count == 1)
            {
                throw new InvalidOperationException("Cannot pop root scope");
            }

            _scopes.Pop();
        }

        public void Define(string name, object? value)
        {
            _scopes.Peek()[name] = value;
        }

        public object? Resolve(string name)
        {
            foreach (var scope in _scopes)
            {
                if (scope.TryGetValue(name, out var value))
                {
                    return value;
                }
            }

            throw new EventScriptRuntimeException($"Unknown identifier '{name}'");
        }

        public void Emit(EventScriptEmittedEvent emittedEvent)
        {
            EmittedEvents.Add(emittedEvent);
        }

        public IReadOnlyDictionary<string, object?> SnapshotTopScope()
        {
            return new Dictionary<string, object?>(_scopes.Peek(), StringComparer.Ordinal);
        }
    }

    private sealed class DispatchState
    {
        private readonly List<string> _stack = new();
        private readonly int _maxEmitDepth;

        public DispatchState(int maxEmitDepth)
        {
            _maxEmitDepth = maxEmitDepth;
        }

        public List<EventScriptEmittedEvent> EmittedEvents { get; } = new();

        public void Enter(string message)
        {
            if (_stack.Contains(message))
            {
                var path = string.Join(" -> ", _stack.Concat(new[] { message }));
                throw new EventScriptRuntimeException($"Emit recursion detected: {path}");
            }

            if (_stack.Count >= _maxEmitDepth)
            {
                throw new EventScriptRuntimeException($"Maximum emit depth of {_maxEmitDepth} exceeded");
            }

            _stack.Add(message);
        }

        public void Exit(string message)
        {
            if (_stack.Count == 0 || !string.Equals(_stack[^1], message, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Cannot exit message '{message}' because it is not active");
            }

            _stack.RemoveAt(_stack.Count - 1);
        }
    }

    private static readonly IReadOnlyDictionary<string, EventScriptExternalMessageBinding> EmptyExternalBindings =
        new Dictionary<string, EventScriptExternalMessageBinding>(StringComparer.Ordinal);
}