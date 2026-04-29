#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StepH.Flow.EventScript.Interpreter;
using StepH.Flow.EventScript.Linker;
using StepH.Flow.EventScript.Parser;
using StepH.Flow.EventScript.Runtime;
using StepH.Flow.EventScript.Types;
using static StepH.Flow.EventScript.Types.EventScriptValueFactory;

namespace StepH.Flow.EventScript.RegisterVM;

internal sealed class RegisterVmFastExecutionSession
{
    private const string CallableCallDepthExceededDetail = "Callable exceeded the configured call depth.";

    private readonly RegisterCompiledEventScript _compiledScript;
    private readonly EventScriptContext _context;
    private readonly RegisterVmFastPathPlan _plan;
    private readonly RegisterFastValue[] _locals;
    private readonly bool[] _assignedSlots;
    private readonly RegisterFastValue[] _evaluationStack;
    private readonly bool _diagnosticsEnabled;
    private readonly List<LocalChange> _changes = [];
    private readonly List<int> _scopeMarks = [];
    private readonly Stack<EventScriptRandomGenerator> _randomScopes = new();
    private bool _halted;

    private RegisterVmFastExecutionSession(
        RegisterCompiledEventScript compiledScript,
        EventScriptContext context,
        RegisterVmFastPathPlan plan,
        bool diagnosticsEnabled)
    {
        _compiledScript = compiledScript;
        _context = context;
        _plan = plan;
        _diagnosticsEnabled = diagnosticsEnabled;
        _locals = new RegisterFastValue[plan.SlotCount];
        _assignedSlots = new bool[plan.SlotCount];
        _evaluationStack = new RegisterFastValue[Math.Max(16, plan.MaxStackDepth + 16)];
        _randomScopes.Push(context.Random);
    }

    public static bool TryInvokeHandler(
        RegisterCompiledEventScript compiledScript,
        EventScriptContext context,
        RegisterCompiledEventScriptHandler handler,
        IReadOnlyDictionary<string, EventScriptValue> args)
    {
        if (!handler.FastPathPlan.IsSupported)
        {
            return false;
        }

        var session = new RegisterVmFastExecutionSession(compiledScript, context, handler.FastPathPlan, handler.DiagnosticsEnabled);
        return session.TryInvoke(handler, args);
    }

    private bool TryInvoke(RegisterCompiledEventScriptHandler handler, IReadOnlyDictionary<string, EventScriptValue> args)
    {
        EnterScope();
        try
        {
            var parameters = handler.Parameters;
            for (var parameterIndex = 0; parameterIndex < parameters.Count; parameterIndex++)
            {
                var parameter = parameters[parameterIndex];
                if (!args.TryGetValue(parameter, out var value))
                {
                    return false;
                }

                RecordParameterBound(parameter, value);

                if (!Define(parameter, RegisterFastValue.FromEventScriptValue(value)))
                {
                    return false;
                }
            }

            RecordHandlerInvoked(handler.Message, args);

            return TryExecuteStatements(handler.Statements);
        }
        finally
        {
            ExitScope();
        }
    }

    private bool TryExecuteStatements(IReadOnlyList<StatementNode> statements)
    {
        for (var statementIndex = 0; statementIndex < statements.Count; statementIndex++)
        {
            var statement = statements[statementIndex];
            if (!TryConsumeExecutionStep("Statement execution budget exhausted."))
            {
                _halted = true;
                return true;
            }

            if (!TryExecuteStatement(statement))
            {
                return false;
            }

            if (_halted)
            {
                return true;
            }
        }

        return true;
    }

    private bool TryExecuteStatement(StatementNode statement)
    {
        switch (statement)
        {
            case LetStatementNode let:
                if (!TryEvaluate(let.Expression, out var letValue))
                {
                    return false;
                }

                if (!string.IsNullOrEmpty(let.DeclaredType) &&
                    !TryConvertDeclaredType(let.DeclaredType!, letValue, out letValue))
                {
                    return false;
                }

                RecordLetEvaluated(let.Identifier, letValue);
                RecordLetExpressionEvaluatedToNothing(let.Identifier, letValue);
                return Define(let.Identifier, letValue);

            case PublishStatementNode publish:
                return _plan.TryGetPublishLayout(publish, out var layout)
                    ? TryPublish(layout)
                    : TryPublish(publish.MessageExpression);

            case IfStatementNode ifStatement:
                return TryExecuteIf(ifStatement);

            case ForStatementNode { Source: RangeIterationSourceNode range } forStatement:
                return TryExecuteRangeFor(forStatement, range.RangeExpression);

            case ForStatementNode { Source: CollectionIterationSourceNode collection } forStatement:
                return TryExecuteCollectionFor(forStatement, collection.Expression);

            case ExpressionStatementNode expressionStatement:
                if (!TryEvaluate(expressionStatement.Expression, out var expressionValue))
                {
                    return false;
                }

                RecordExpressionStatementEvaluatedToNothing(expressionStatement.Expression, expressionValue);
                return true;

            case SeededRandomStatementNode seededRandom:
                return TryExecuteSeededRandomStatement(seededRandom);

            default:
                return false;
        }
    }

    private bool TryExecuteSeededRandomStatement(SeededRandomStatementNode seededRandom)
    {
        if (!TryEvaluate(seededRandom.SeedExpression, out var seed))
        {
            return false;
        }

        PushSeededRandomScope(seed.ToEventScriptValue());
        try
        {
            return TryExecuteStatementBody(seededRandom.Body);
        }
        finally
        {
            PopSeededRandomScope();
        }
    }

    private bool TryExecuteIf(IfStatementNode ifStatement)
    {
        if (!TryEvaluate(ifStatement.Condition, out var condition))
        {
            return false;
        }

        if (condition.AsBoolean())
        {
            return TryExecuteStatementBody(ifStatement.ThenBody);
        }

        return ifStatement.ElseBody is null ||
               TryExecuteStatementBody(ifStatement.ElseBody);
    }

    private bool TryExecuteStatementBody(StatementBodyNode body)
    {
        if (!body.IsBlock)
        {
            return TryExecuteStatements(body.Statements);
        }

        EnterScope();
        try
        {
            return TryExecuteStatements(body.Statements);
        }
        finally
        {
            ExitScope();
        }
    }

    private bool TryExecuteRangeFor(ForStatementNode forStatement, RangeExpressionNode range)
    {
        if (!TryEvaluate(range.FromExpression, out var fromValue) ||
            !TryEvaluate(range.ToExpression, out var toValue) ||
            !fromValue.TryGetFiniteNumber(out var fromNumber) ||
            !toValue.TryGetFiniteNumber(out var toNumber))
        {
            return false;
        }

        var stepNumber = 1m;
        if (range.StepExpression is not null &&
            (!TryEvaluate(range.StepExpression, out var stepValue) || !stepValue.TryGetFiniteNumber(out stepNumber)))
        {
            return false;
        }

        var from = ToLongSaturated(fromNumber);
        var to = ToLongSaturated(toNumber);
        var step = ToLongSaturated(stepNumber);
        if (step == 0)
        {
            return true;
        }

        var length = EventScriptRuntimeLimitUtilities.GetRangeLength(from, to, step);
        if (!_context.RuntimeBudget.TryCheckRangeLength(length, "For loop range would enumerate more range items than allowed."))
        {
            return true;
        }

        if (step > 0)
        {
            for (var item = from; item <= to; item += step)
            {
                if (!TryExecuteLoopIteration(forStatement, item))
                {
                    return false;
                }

                if (_halted)
                {
                    break;
                }

                if (long.MaxValue - item < step)
                {
                    break;
                }
            }
        }
        else
        {
            for (var item = from; item >= to; item += step)
            {
                if (!TryExecuteLoopIteration(forStatement, item))
                {
                    return false;
                }

                if (_halted)
                {
                    break;
                }

                if (long.MinValue - item > step)
                {
                    break;
                }
            }
        }

        return true;
    }

    private bool TryExecuteCollectionFor(ForStatementNode forStatement, ExpressionNode sourceExpression)
    {
        if (!TryEvaluate(sourceExpression, out var sourceFastValue))
        {
            return false;
        }

        var sourceValue = sourceFastValue.ToEventScriptValue();
        if (EventScriptRuntimeLimitUtilities.TryGetRangeLength(sourceValue, out var length) &&
            !_context.RuntimeBudget.TryCheckRangeLength(length, "Iteration source would enumerate more range items than allowed."))
        {
            return true;
        }

        foreach (var item in sourceValue.AsEnumerable())
        {
            if (!TryExecuteLoopIteration(forStatement, RegisterFastValue.FromEventScriptValue(item)))
            {
                return false;
            }

            if (_halted)
            {
                break;
            }
        }

        return true;
    }

    private bool TryExecuteLoopIteration(ForStatementNode forStatement, long item)
        => TryExecuteLoopIteration(forStatement, RegisterFastValue.Integer(item));

    private bool TryExecuteLoopIteration(ForStatementNode forStatement, RegisterFastValue item)
    {
        if (!_context.RuntimeBudget.TryConsumeLoopIteration("Loop iteration budget exhausted."))
        {
            _halted = true;
            return true;
        }

        EnterScope();
        try
        {
            return Define(forStatement.Identifier, item) &&
                   TryExecuteStatementBody(forStatement.Body);
        }
        finally
        {
            ExitScope();
        }
    }

    private bool TryEvaluateMessageArguments(MessageLiteralExpressionNode message, out IReadOnlyDictionary<string, EventScriptValue> arguments)
    {
        if (message.Arguments.Count == 0)
        {
            arguments = EventScriptNamedArguments.Empty;
            return true;
        }

        var pairs = new KeyValuePair<string, EventScriptValue>[message.Arguments.Count];
        for (var argumentIndex = 0; argumentIndex < message.Arguments.Count; argumentIndex++)
        {
            var argument = message.Arguments[argumentIndex];
            if (!TryEvaluate(argument.Expression, out var value))
            {
                arguments = EventScriptNamedArguments.Empty;
                return false;
            }

            pairs[argumentIndex] = new KeyValuePair<string, EventScriptValue>(argument.Name, value.ToEventScriptValue());
        }

        arguments = EventScriptNamedArguments.CreateOrdered(pairs);
        return true;
    }

    private bool TryPublish(RegisterFastPublishLayout layout)
    {
        var argumentNames = layout.ArgumentNames;
        var argumentPrograms = layout.ArgumentPrograms;
        if (argumentNames.Length == 0)
        {
            _context.Publish(EventScriptMessage.CreatePrecomputed(
                layout.MessageName,
                EventScriptNamedArguments.Empty,
                layout.SignatureId));
            return true;
        }

        var pairs = new KeyValuePair<string, EventScriptValue>[argumentNames.Length];
        for (var argumentIndex = 0; argumentIndex < argumentNames.Length; argumentIndex++)
        {
            if (!TryExecuteExpressionProgram(argumentPrograms[argumentIndex], 0, out var value))
            {
                return false;
            }

            RecordPublishArgumentEvaluatedToNothing(argumentNames[argumentIndex], value);

            pairs[argumentIndex] = new KeyValuePair<string, EventScriptValue>(
                argumentNames[argumentIndex],
                value.ToEventScriptValue());
        }

        _context.Publish(EventScriptMessage.CreatePrecomputed(
            layout.MessageName,
            EventScriptNamedArguments.CreateOrdered(pairs),
            layout.SignatureId));
        return true;
    }

    private bool TryPublish(ExpressionNode messageExpression)
    {
        if (!TryEvaluate(messageExpression, out var publishValue))
        {
            return false;
        }

        var boxed = publishValue.ToEventScriptValue();
        if (EventScriptMessageValueCodec.TryReadMessageValue(boxed, out var message))
        {
            _context.Publish(message);
        }

        return true;
    }

    private bool TryEvaluate(ExpressionNode expression, out RegisterFastValue value)
    {
        if (_plan.TryGetExpressionProgram(expression, out var program))
        {
            return TryExecuteExpressionProgram(program, 0, out value);
        }

        if (!TryConsumeExecutionStep("Expression evaluation budget exhausted."))
        {
            value = RegisterFastValue.Nothing;
            return true;
        }

        switch (expression)
        {
            case BooleanLiteralExpressionNode boolean:
                value = RegisterFastValue.Boolean(boolean.Value);
                return true;
            case IntegerLiteralExpressionNode integer:
                value = RegisterFastValue.Integer(integer.Value);
                return true;
            case DecimalLiteralExpressionNode decimalLiteral:
                value = RegisterFastValue.Decimal(decimalLiteral.Value);
                return true;
            case PercentageLiteralExpressionNode percentage:
                value = RegisterFastValue.Percentage(percentage.PercentValue / 100m);
                return true;
            case UnitDecimalLiteralExpressionNode unitDecimal:
                value = EventScriptDecimalUnits.TryParseTypeName(unitDecimal.UnitName, out var unit)
                    ? RegisterFastValue.Decimal(unitDecimal.Value, unit)
                    : RegisterFastValue.NaN();
                return true;
            case TextLiteralExpressionNode text:
                value = RegisterFastValue.Reference(Text(text.Value));
                return true;
            case TagLiteralExpressionNode tag:
                value = RegisterFastValue.Reference(Tag(tag.Name));
                return true;
            case HandlerLiteralExpressionNode handler:
                value = RegisterFastValue.Reference(EventScriptValueFactory.Handler(
                    new EventScriptMessageSignature(handler.Message, handler.Parameters)));
                return true;
            case HandlerBindExpressionNode handlerBind:
                return TryEvaluateHandlerBind(handlerBind, out value);
            case ListLiteralExpressionNode list:
                return TryEvaluateListLiteral(list, out value);
            case SetLiteralExpressionNode set:
                return TryEvaluateSetLiteral(set, out value);
            case DictionaryLiteralExpressionNode dictionary:
                return TryEvaluateDictionaryLiteral(dictionary, out value);
            case IdentifierExpressionNode identifier:
                value = Resolve(identifier.Name);
                return true;
            case UnaryExpressionNode unary:
                return TryEvaluateUnary(unary, out value);
            case VariadicTaggedExpressionNode variadic:
                return TryEvaluateVariadic(variadic, out value);
            case ClampExpressionNode clamp:
                return TryEvaluateClamp(clamp, out value);
            case RandomExpressionNode random:
                return TryEvaluateRandom(random, out value);
            case RangeExpressionNode range:
                return TryEvaluateRange(range, out value);
            case DiceExpressionNode dice:
                value = EvaluateDiceExpression(dice.DiceCount, dice.SideCount);
                return true;
            case SeededRandomExpressionNode seededRandom:
                return TryEvaluateSeededRandomExpression(seededRandom, out value);
            case BinaryExpressionNode binary:
                return TryEvaluateBinary(binary, out value);
            case RulePredicateExpressionNode rulePredicate:
                return TryEvaluateRulePredicate(rulePredicate, out value);
            case CallExpressionNode call:
                return TryEvaluateCall(call, out value);
            case TypeCastExpressionNode typeCast:
                return TryEvaluateTypeCast(typeCast, out value);
            case TypeCheckExpressionNode typeCheck:
                return TryEvaluateTypeCheck(typeCheck, out value);
            case MemberAccessExpressionNode memberAccess:
                return TryEvaluateMemberAccess(memberAccess, out value);
            case CollectionAccessExpressionNode collectionAccess:
                return TryEvaluateCollectionAccess(collectionAccess, out value);
            case MessageLiteralExpressionNode message:
                if (!TryEvaluateMessageArguments(message, out var arguments))
                {
                    value = RegisterFastValue.Nothing;
                    return false;
                }

                value = RegisterFastValue.Reference(Message(new EventScriptMessage(message.Message, arguments)));
                return true;
            default:
                value = RegisterFastValue.Nothing;
                return false;
        }
    }

    private bool TryExecuteExpressionProgram(RegisterFastExpressionProgram program, int stackBase, out RegisterFastValue value)
    {
        if (stackBase + program.MaxStackDepth > _evaluationStack.Length)
        {
            value = RegisterFastValue.Nothing;
            return false;
        }

        var top = stackBase;
        var instructions = program.Instructions;
        if (!TryConsumeExecutionSteps(instructions.Length, "Expression evaluation budget exhausted."))
        {
            value = RegisterFastValue.Nothing;
            return true;
        }

        for (var instructionIndex = 0; instructionIndex < instructions.Length; instructionIndex++)
        {
            var instruction = instructions[instructionIndex];
            switch (instruction.OpCode)
            {
                case RegisterFastOpCode.LoadConstant:
                    _evaluationStack[top++] = instruction.Constant;
                    break;

                case RegisterFastOpCode.LoadSlot:
                    _evaluationStack[top++] = ResolveSlot(instruction.A);
                    break;

                case RegisterFastOpCode.Or:
                case RegisterFastOpCode.Xor:
                case RegisterFastOpCode.And:
                case RegisterFastOpCode.Equal:
                case RegisterFastOpCode.NotEqual:
                case RegisterFastOpCode.Less:
                case RegisterFastOpCode.Greater:
                case RegisterFastOpCode.LessOrEqual:
                case RegisterFastOpCode.GreaterOrEqual:
                case RegisterFastOpCode.Add:
                case RegisterFastOpCode.Subtract:
                case RegisterFastOpCode.Multiply:
                case RegisterFastOpCode.Divide:
                case RegisterFastOpCode.Modulo:
                case RegisterFastOpCode.Default:
                case RegisterFastOpCode.Contains:
                case RegisterFastOpCode.ContainsValue:
                case RegisterFastOpCode.StartsWith:
                case RegisterFastOpCode.EndsWith:
                case RegisterFastOpCode.Intersect:
                case RegisterFastOpCode.Combine:
                case RegisterFastOpCode.Except:
                case RegisterFastOpCode.Zip:
                    var right = _evaluationStack[--top];
                    var left = _evaluationStack[--top];
                    _evaluationStack[top++] = EvaluateProgramBinary(instruction.OpCode, left, right);
                    break;

                case RegisterFastOpCode.Unary:
                    if (!TryEvaluateUnaryOperation(instruction.DiagnosticName, _evaluationStack[top - 1], out var unaryValue))
                    {
                        value = RegisterFastValue.Nothing;
                        return false;
                    }

                    _evaluationStack[top - 1] = unaryValue;
                    break;

                case RegisterFastOpCode.Variadic:
                    top -= instruction.A;
                    if (!TryEvaluateVariadicOperation(instruction.DiagnosticName, _evaluationStack, top, instruction.A, out var variadicValue))
                    {
                        value = RegisterFastValue.Nothing;
                        return false;
                    }

                    _evaluationStack[top++] = variadicValue;
                    break;

                case RegisterFastOpCode.Clamp:
                    top -= 3;
                    _evaluationStack[top] = EvaluateClamp(
                        _evaluationStack[top],
                        _evaluationStack[top + 1],
                        _evaluationStack[top + 2]);
                    top++;
                    break;

                case RegisterFastOpCode.Random:
                    var to = _evaluationStack[--top];
                    var from = _evaluationStack[--top];
                    _evaluationStack[top++] = EvaluateRandomExpression(from, to);
                    break;

                case RegisterFastOpCode.Range:
                    top -= instruction.A;
                    _evaluationStack[top] = EvaluateRangeExpression(
                        _evaluationStack[top],
                        _evaluationStack[top + 1],
                        instruction.A == 3 ? _evaluationStack[top + 2] : RegisterFastValue.Integer(1));
                    top++;
                    break;

                case RegisterFastOpCode.Dice:
                    _evaluationStack[top++] = EvaluateDiceExpression(instruction.A, instruction.B);
                    break;

                case RegisterFastOpCode.SeededRandom:
                    var seed = _evaluationStack[--top];
                    if (instruction.ExpressionProgram is null ||
                        !TryEvaluateSeededRandomExpression(seed, instruction.ExpressionProgram, top, out var seededValue))
                    {
                        value = RegisterFastValue.Nothing;
                        return false;
                    }

                    _evaluationStack[top++] = seededValue;
                    break;

                case RegisterFastOpCode.Cast:
                    _evaluationStack[top - 1] = EvaluateProgramCast(instruction.CastKind, _evaluationStack[top - 1]);
                    break;

                case RegisterFastOpCode.TypeCheck:
                    _evaluationStack[top - 1] = RegisterFastValue.Boolean(IsValueOfType(
                        _evaluationStack[top - 1],
                        instruction.DiagnosticName));
                    break;

                case RegisterFastOpCode.RulePredicate:
                    var input = _evaluationStack[--top];
                    if (!TryEvaluateRulePredicate(instruction, input, top, out var predicateValue))
                    {
                        value = RegisterFastValue.Nothing;
                        return false;
                    }

                    _evaluationStack[top++] = predicateValue;
                    break;

                case RegisterFastOpCode.Call:
                    top -= instruction.A;
                    if (!TryEvaluateCallable(instruction, _evaluationStack, top, instruction.A, out var callValue))
                    {
                        value = RegisterFastValue.Nothing;
                        return false;
                    }

                    _evaluationStack[top++] = callValue;
                    break;

                case RegisterFastOpCode.MemberAccess:
                    _evaluationStack[top - 1] = EvaluateMemberAccess(_evaluationStack[top - 1], instruction.DiagnosticName);
                    break;

                case RegisterFastOpCode.IndexedAccess:
                    var selector = _evaluationStack[--top];
                    var target = _evaluationStack[--top];
                    _evaluationStack[top++] = EvaluateIndexedAccess(target, selector);
                    break;

                case RegisterFastOpCode.BuildList:
                    top -= instruction.A;
                    _evaluationStack[top] = BuildListValue(_evaluationStack, top, instruction.A);
                    top++;
                    break;

                case RegisterFastOpCode.BuildSet:
                    top -= instruction.A;
                    _evaluationStack[top] = BuildSetValue(_evaluationStack, top, instruction.A);
                    top++;
                    break;

                case RegisterFastOpCode.BuildDictionary:
                    top -= instruction.A;
                    _evaluationStack[top] = BuildDictionaryValue(_evaluationStack, top, instruction.A, instruction.Names);
                    top++;
                    break;

                case RegisterFastOpCode.BuildMessage:
                    top -= instruction.A;
                    _evaluationStack[top] = BuildMessageValue(
                        _evaluationStack,
                        top,
                        instruction.A,
                        instruction.Names,
                        instruction.DiagnosticName,
                        instruction.DiagnosticArgumentName);
                    top++;
                    break;

                case RegisterFastOpCode.BindHandler:
                    top -= instruction.A + 1;
                    _evaluationStack[top] = BindHandlerValue(
                        _evaluationStack[top],
                        _evaluationStack,
                        top + 1,
                        instruction.A,
                        instruction.Names);
                    top++;
                    break;

                case RegisterFastOpCode.Pipeline:
                    if (instruction.PipelineProgram is null ||
                        !TryExecutePipelineProgram(instruction.PipelineProgram, out var pipelineValue))
                    {
                        value = RegisterFastValue.Nothing;
                        return false;
                    }

                    _evaluationStack[top++] = pipelineValue;
                    break;

                default:
                    value = RegisterFastValue.Nothing;
                    return false;
            }
        }

        value = top > stackBase ? _evaluationStack[top - 1] : RegisterFastValue.Nothing;
        return true;
    }

    private bool TryEvaluateHandlerBind(HandlerBindExpressionNode handlerBind, out RegisterFastValue value)
    {
        if (!TryEvaluate(handlerBind.CalleeExpression, out var callee))
        {
            value = RegisterFastValue.Nothing;
            return false;
        }

        var arguments = new Dictionary<string, EventScriptValue>(handlerBind.Arguments.Count, StringComparer.Ordinal);
        foreach (var argument in handlerBind.Arguments)
        {
            if (!TryEvaluate(argument.Expression, out var argumentValue))
            {
                value = RegisterFastValue.Nothing;
                return false;
            }

            arguments[argument.Name] = argumentValue.ToEventScriptValue();
        }

        value = EventScriptMessageValueCodec.TryBindHandlerValue(callee.ToEventScriptValue(), arguments, out var message)
            ? RegisterFastValue.Reference(EventScriptMessageValueCodec.CreateMessageValue(message))
            : RegisterFastValue.Nothing;
        return true;
    }

    private bool TryEvaluateTypeCheck(TypeCheckExpressionNode typeCheck, out RegisterFastValue value)
    {
        if (!TryEvaluate(typeCheck.Value, out var input))
        {
            value = RegisterFastValue.Nothing;
            return false;
        }

        value = RegisterFastValue.Boolean(IsValueOfType(input, typeCheck.TypeName));
        return true;
    }

    private bool TryEvaluateListLiteral(ListLiteralExpressionNode list, out RegisterFastValue value)
    {
        var items = new EventScriptValue[list.Items.Count];
        for (var itemIndex = 0; itemIndex < list.Items.Count; itemIndex++)
        {
            if (!TryEvaluate(list.Items[itemIndex], out var item))
            {
                value = RegisterFastValue.Nothing;
                return false;
            }

            items[itemIndex] = item.ToEventScriptValue();
        }

        value = RegisterFastValue.Reference(EventScriptValueFactory.List(items));
        return true;
    }

    private bool TryEvaluateSetLiteral(SetLiteralExpressionNode set, out RegisterFastValue value)
    {
        var items = new EventScriptValue[set.Items.Count];
        for (var itemIndex = 0; itemIndex < set.Items.Count; itemIndex++)
        {
            if (!TryEvaluate(set.Items[itemIndex], out var item))
            {
                value = RegisterFastValue.Nothing;
                return false;
            }

            items[itemIndex] = item.ToEventScriptValue();
        }

        value = RegisterFastValue.Reference(EventScriptValueFactory.Set(items));
        return true;
    }

    private bool TryEvaluateDictionaryLiteral(DictionaryLiteralExpressionNode dictionary, out RegisterFastValue value)
    {
        var map = new Dictionary<string, EventScriptValue>(StringComparer.Ordinal);
        foreach (var entry in dictionary.Entries)
        {
            if (!TryEvaluate(entry.Value, out var entryValue))
            {
                value = RegisterFastValue.Nothing;
                return false;
            }

            map[entry.Key] = entryValue.ToEventScriptValue();
        }

        value = RegisterFastValue.Reference(EventScriptValueFactory.Dictionary(map));
        return true;
    }

    private bool TryEvaluateMemberAccess(MemberAccessExpressionNode memberAccess, out RegisterFastValue value)
    {
        if (!TryEvaluate(memberAccess.Target, out var target))
        {
            value = RegisterFastValue.Nothing;
            return false;
        }

        value = EvaluateMemberAccess(target, memberAccess.Member);
        return true;
    }

    private static RegisterFastValue EvaluateMemberAccess(RegisterFastValue target, string? member)
    {
        if (string.IsNullOrEmpty(member))
        {
            return RegisterFastValue.Nothing;
        }

        var targetValue = target.ToEventScriptValue();
        if (targetValue.IsNothing())
        {
            return RegisterFastValue.Nothing;
        }

        return targetValue.TryGetDictionaryMember(member, out var value)
            ? RegisterFastValue.FromEventScriptValue(value)
            : RegisterFastValue.Nothing;
    }

    private static RegisterFastValue EvaluateIndexedAccess(RegisterFastValue target, RegisterFastValue selector)
    {
        var selectorValue = selector.ToEventScriptValue();
        if (selectorValue.IsNothing())
        {
            return RegisterFastValue.Nothing;
        }

        return RegisterFastValue.FromEventScriptValue(target.ToEventScriptValue().Lookup(selectorValue));
    }

    private static RegisterFastValue BuildListValue(RegisterFastValue[] stack, int start, int count)
    {
        var items = new EventScriptValue[count];
        for (var itemIndex = 0; itemIndex < count; itemIndex++)
        {
            items[itemIndex] = stack[start + itemIndex].ToEventScriptValue();
        }

        return RegisterFastValue.Reference(EventScriptValueFactory.List(items));
    }

    private static RegisterFastValue BuildSetValue(RegisterFastValue[] stack, int start, int count)
    {
        var items = new EventScriptValue[count];
        for (var itemIndex = 0; itemIndex < count; itemIndex++)
        {
            items[itemIndex] = stack[start + itemIndex].ToEventScriptValue();
        }

        return RegisterFastValue.Reference(EventScriptValueFactory.Set(items));
    }

    private static RegisterFastValue BuildDictionaryValue(RegisterFastValue[] stack, int start, int count, string[]? names)
    {
        if (names is null || names.Length != count)
        {
            return RegisterFastValue.Nothing;
        }

        var map = new Dictionary<string, EventScriptValue>(count, StringComparer.Ordinal);
        for (var entryIndex = 0; entryIndex < count; entryIndex++)
        {
            map[names[entryIndex]] = stack[start + entryIndex].ToEventScriptValue();
        }

        return RegisterFastValue.Reference(EventScriptValueFactory.Dictionary(map));
    }

    private static RegisterFastValue BuildMessageValue(
        RegisterFastValue[] stack,
        int start,
        int count,
        string[]? names,
        string? messageName,
        string? signatureId)
    {
        if (string.IsNullOrEmpty(messageName) ||
            string.IsNullOrEmpty(signatureId) ||
            names is null ||
            names.Length != count)
        {
            return RegisterFastValue.Nothing;
        }

        if (count == 0)
        {
            return RegisterFastValue.Reference(Message(EventScriptMessage.CreatePrecomputed(
                messageName,
                EventScriptNamedArguments.Empty,
                signatureId)));
        }

        var pairs = new KeyValuePair<string, EventScriptValue>[count];
        for (var argumentIndex = 0; argumentIndex < count; argumentIndex++)
        {
            pairs[argumentIndex] = new KeyValuePair<string, EventScriptValue>(
                names[argumentIndex],
                stack[start + argumentIndex].ToEventScriptValue());
        }

        return RegisterFastValue.Reference(Message(EventScriptMessage.CreatePrecomputed(
            messageName,
            EventScriptNamedArguments.CreateOrdered(pairs),
            signatureId)));
    }

    private static RegisterFastValue BindHandlerValue(
        RegisterFastValue callee,
        RegisterFastValue[] stack,
        int start,
        int count,
        string[]? names)
    {
        if (names is null || names.Length != count)
        {
            return RegisterFastValue.Nothing;
        }

        var arguments = new Dictionary<string, EventScriptValue>(count, StringComparer.Ordinal);
        for (var argumentIndex = 0; argumentIndex < count; argumentIndex++)
        {
            arguments[names[argumentIndex]] = stack[start + argumentIndex].ToEventScriptValue();
        }

        return EventScriptMessageValueCodec.TryBindHandlerValue(callee.ToEventScriptValue(), arguments, out var message)
            ? RegisterFastValue.Reference(EventScriptMessageValueCodec.CreateMessageValue(message))
            : RegisterFastValue.Nothing;
    }

    private RegisterFastValue EvaluateProgramBinary(RegisterFastOpCode opCode, RegisterFastValue left, RegisterFastValue right)
    {
        if (opCode != RegisterFastOpCode.Default &&
            (left.IsNothingLike() || right.IsNothingLike()))
        {
            return RegisterFastValue.Nothing;
        }

        switch (opCode)
        {
            case RegisterFastOpCode.Or:
                return RegisterFastValue.Boolean(left.AsBoolean() || right.AsBoolean());
            case RegisterFastOpCode.Xor:
                return RegisterFastValue.Boolean(left.AsBoolean() ^ right.AsBoolean());
            case RegisterFastOpCode.And:
                return RegisterFastValue.Boolean(left.AsBoolean() && right.AsBoolean());
            case RegisterFastOpCode.Equal:
                return RegisterFastValue.Boolean(RegisterFastValue.AreEqual(left, right));
            case RegisterFastOpCode.NotEqual:
                return RegisterFastValue.Boolean(!RegisterFastValue.AreEqual(left, right));
            case RegisterFastOpCode.Less:
                return RegisterFastValue.Boolean(RegisterFastValue.TryCompareNumeric(left, right, out var lessComparison) && lessComparison < 0);
            case RegisterFastOpCode.Greater:
                return RegisterFastValue.Boolean(RegisterFastValue.TryCompareNumeric(left, right, out var greaterComparison) && greaterComparison > 0);
            case RegisterFastOpCode.LessOrEqual:
                return RegisterFastValue.Boolean(RegisterFastValue.TryCompareNumeric(left, right, out var lessOrEqualComparison) && lessOrEqualComparison <= 0);
            case RegisterFastOpCode.GreaterOrEqual:
                return RegisterFastValue.Boolean(RegisterFastValue.TryCompareNumeric(left, right, out var greaterOrEqualComparison) && greaterOrEqualComparison >= 0);
            case RegisterFastOpCode.Add:
                return RegisterFastValue.Add(left, right);
            case RegisterFastOpCode.Subtract:
                return RegisterFastValue.Subtract(left, right);
            case RegisterFastOpCode.Multiply:
                return RegisterFastValue.Multiply(left, right);
            case RegisterFastOpCode.Divide:
                return RegisterFastValue.Divide(left, right);
            case RegisterFastOpCode.Modulo:
                return RegisterFastValue.Modulo(left, right);
            default:
                return TryEvaluateBinaryOperation(GetBinaryOperator(opCode), left, right, out var value)
                    ? value
                    : RegisterFastValue.Unsupported();
        }
    }

    private RegisterFastValue EvaluateProgramCast(RegisterFastCastKind castKind, RegisterFastValue input)
        => TryConvertDeclaredType(GetCastTypeName(castKind), input, out var value)
            ? value
            : RegisterFastValue.Unsupported();

    private static bool IsValueOfType(RegisterFastValue value, string? typeName)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            return false;
        }

        return typeName switch
        {
            "nothing" => value.Kind == RegisterFastValueKind.Nothing || (value.ReferenceValue?.IsNothing() ?? false),
            "tag" => value.ReferenceValue?.IsTag() ?? false,
            "text" => value.ReferenceValue?.IsText() ?? false,
            "percentage" => value.Kind == RegisterFastValueKind.Percentage || (value.ReferenceValue?.IsPercentage() ?? false),
            "degree" => value.Kind == RegisterFastValueKind.Decimal && value.Unit == EventScriptDecimalUnit.Degree ||
                        (value.ReferenceValue?.IsDecimalUnit(EventScriptDecimalUnit.Degree) ?? false),
            "meter" => value.Kind == RegisterFastValueKind.Decimal && value.Unit == EventScriptDecimalUnit.Meter ||
                       (value.ReferenceValue?.IsDecimalUnit(EventScriptDecimalUnit.Meter) ?? false),
            "second" => value.Kind == RegisterFastValueKind.Decimal && value.Unit == EventScriptDecimalUnit.Second ||
                        (value.ReferenceValue?.IsDecimalUnit(EventScriptDecimalUnit.Second) ?? false),
            "vector2" => value.ReferenceValue?.IsVector2() ?? false,
            "vector3" => value.ReferenceValue?.IsVector3() ?? false,
            "decimal" => value.Kind is RegisterFastValueKind.Integer or RegisterFastValueKind.Decimal or RegisterFastValueKind.Percentage ||
                         (value.ReferenceValue?.IsNumber() ?? false),
            "integer" => value.Kind == RegisterFastValueKind.Integer || (value.ReferenceValue?.IsInteger() ?? false),
            "boolean" => value.Kind == RegisterFastValueKind.Boolean || value.ReferenceValue?.Kind == EventScriptValueKind.Boolean,
            "optional" => value.ReferenceValue?.IsOptional() ?? false,
            "list" => value.ReferenceValue?.IsList() ?? false,
            "range" => value.ReferenceValue?.IsRange() ?? false,
            "message" => value.ReferenceValue is { } messageValue &&
                         (messageValue.Kind == EventScriptValueKind.Message || EventScriptMessageValueCodec.TryReadMessageValue(messageValue, out _)),
            "handler" => value.ReferenceValue is { } handlerValue &&
                         (handlerValue.Kind == EventScriptValueKind.Handler || EventScriptMessageValueCodec.TryReadHandlerValue(handlerValue, out _)),
            "dictionary" => value.ReferenceValue?.IsDictionary() ?? false,
            "set" => value.ReferenceValue?.IsSet() ?? false,
            "dice" => value.ReferenceValue?.IsDice() ?? false,
            _ => value.ReferenceValue is { } customValue &&
                 customValue.TryGetCustomTypeName(out var customTypeName) &&
                 string.Equals(customTypeName, typeName, StringComparison.Ordinal)
        };
    }

    private bool TryEvaluateBinary(BinaryExpressionNode binary, out RegisterFastValue value)
    {
        if (!TryEvaluate(binary.Left, out var left) ||
            !TryEvaluate(binary.Right, out var right))
        {
            value = RegisterFastValue.Nothing;
            return false;
        }

        return TryEvaluateBinaryOperation(binary.Operator, left, right, out value);
    }

    private bool TryEvaluateUnary(UnaryExpressionNode unary, out RegisterFastValue value)
    {
        if (!TryEvaluate(unary.Operand, out var operand))
        {
            value = RegisterFastValue.Nothing;
            return false;
        }

        return TryEvaluateUnaryOperation(unary.Operator, operand, out value);
    }

    private bool TryEvaluateVariadic(VariadicTaggedExpressionNode variadic, out RegisterFastValue value)
    {
        var values = new RegisterFastValue[variadic.Arguments.Count];
        for (var argumentIndex = 0; argumentIndex < variadic.Arguments.Count; argumentIndex++)
        {
            if (!TryEvaluate(variadic.Arguments[argumentIndex], out values[argumentIndex]))
            {
                value = RegisterFastValue.Nothing;
                return false;
            }
        }

        return TryEvaluateVariadicOperation(variadic.Operator, values, 0, values.Length, out value);
    }

    private bool TryEvaluateClamp(ClampExpressionNode clamp, out RegisterFastValue value)
    {
        if (!TryEvaluate(clamp.Value, out var raw) ||
            !TryEvaluate(clamp.Minimum, out var minimum) ||
            !TryEvaluate(clamp.Maximum, out var maximum))
        {
            value = RegisterFastValue.Nothing;
            return false;
        }

        value = EvaluateClamp(raw, minimum, maximum);
        return true;
    }

    private bool TryEvaluateRandom(RandomExpressionNode random, out RegisterFastValue value)
    {
        if (!TryEvaluate(random.FromExpression, out var from) ||
            !TryEvaluate(random.ToExpression, out var to))
        {
            value = RegisterFastValue.Nothing;
            return false;
        }

        value = EvaluateRandomExpression(from, to);
        return true;
    }

    private bool TryEvaluateRange(RangeExpressionNode range, out RegisterFastValue value)
    {
        if (!TryEvaluate(range.FromExpression, out var from) ||
            !TryEvaluate(range.ToExpression, out var to))
        {
            value = RegisterFastValue.Nothing;
            return false;
        }

        if (range.StepExpression is not null)
        {
            if (!TryEvaluate(range.StepExpression, out var step))
            {
                value = RegisterFastValue.Nothing;
                return false;
            }

            value = EvaluateRangeExpression(from, to, step);
            return true;
        }

        value = EvaluateRangeExpression(from, to, RegisterFastValue.Integer(1));
        return true;
    }

    private bool TryEvaluateSeededRandomExpression(SeededRandomExpressionNode seededRandom, out RegisterFastValue value)
    {
        if (!TryEvaluate(seededRandom.SeedExpression, out var seed) ||
            !_plan.TryGetExpressionProgram(seededRandom.BodyExpression, out var bodyProgram))
        {
            value = RegisterFastValue.Nothing;
            return false;
        }

        return TryEvaluateSeededRandomExpression(seed, bodyProgram, 0, out value);
    }

    private bool TryEvaluateSeededRandomExpression(
        RegisterFastValue seed,
        RegisterFastExpressionProgram bodyProgram,
        int stackBase,
        out RegisterFastValue value)
    {
        PushSeededRandomScope(seed.ToEventScriptValue());
        try
        {
            return TryExecuteExpressionProgram(bodyProgram, stackBase, out value);
        }
        finally
        {
            PopSeededRandomScope();
        }
    }

    private RegisterFastValue EvaluateRandomExpression(RegisterFastValue fromValue, RegisterFastValue toValue)
    {
        var fromRaw = fromValue.ToEventScriptValue();
        var toRaw = toValue.ToEventScriptValue();

        if (EventScriptValueAlu.TryUnwrapOptionalForOperation(fromRaw, out var unwrappedFrom) &&
            EventScriptValueAlu.TryUnwrapOptionalForOperation(toRaw, out var unwrappedTo) &&
            unwrappedFrom.Kind == EventScriptValueKind.Integer &&
            unwrappedTo.Kind == EventScriptValueKind.Integer)
        {
            var from = ToIntSaturated(unwrappedFrom.AsInteger());
            var to = ToIntSaturated(unwrappedTo.AsInteger());
            if (from > to)
            {
                (from, to) = (to, from);
            }

            return TryNextInclusiveInt(from, to, out var next)
                ? RegisterFastValue.Integer(next)
                : RegisterFastValue.Nothing;
        }

        if (!EventScriptValueAlu.TryCoerceNumericForOperation(fromRaw, out var fromNumber) ||
            !EventScriptValueAlu.TryCoerceNumericForOperation(toRaw, out var toNumber) ||
            !fromNumber.IsFinite ||
            !toNumber.IsFinite)
        {
            return RegisterFastValue.Nothing;
        }

        var lower = Math.Min(fromNumber.Value, toNumber.Value);
        var upper = Math.Max(fromNumber.Value, toNumber.Value);
        if (lower == upper)
        {
            return RegisterFastValue.Decimal(lower);
        }

        return TryNextInclusiveDecimal(lower, upper, out var nextDecimal)
            ? RegisterFastValue.Decimal(nextDecimal)
            : RegisterFastValue.Nothing;
    }

    private static RegisterFastValue EvaluateRangeExpression(RegisterFastValue fromValue, RegisterFastValue toValue, RegisterFastValue stepValue)
    {
        if (!EventScriptValueAlu.TryCoerceNumericForOperation(fromValue.ToEventScriptValue(), out var fromNumber) ||
            !EventScriptValueAlu.TryCoerceNumericForOperation(toValue.ToEventScriptValue(), out var toNumber) ||
            !EventScriptValueAlu.TryCoerceNumericForOperation(stepValue.ToEventScriptValue(), out var stepNumber) ||
            !fromNumber.IsFinite ||
            !toNumber.IsFinite ||
            !stepNumber.IsFinite)
        {
            return RegisterFastValue.Nothing;
        }

        return RegisterFastValue.Reference(Range(
            EventScriptValueAlu.ToIntegerSaturated(fromNumber.Value),
            EventScriptValueAlu.ToIntegerSaturated(toNumber.Value),
            EventScriptValueAlu.ToIntegerSaturated(stepNumber.Value)));
    }

    private RegisterFastValue EvaluateDiceExpression(int diceCount, int sideCount)
    {
        if (diceCount <= 0 || sideCount <= 0)
        {
            return RegisterFastValue.Reference(Dice(EventScriptDiceValue.Empty));
        }

        if (!_context.RuntimeBudget.TryCheckDice(new DiceExpressionNode(diceCount, sideCount)))
        {
            return RegisterFastValue.Reference(Dice(EventScriptDiceValue.Empty));
        }

        var rolls = new int[diceCount];
        for (var i = 0; i < rolls.Length; i++)
        {
            if (!TryNextInclusiveInt(1, sideCount, out var roll))
            {
                return RegisterFastValue.Reference(Dice(EventScriptDiceValue.Empty));
            }

            rolls[i] = roll;
        }

        return RegisterFastValue.Reference(Dice(EventScriptDiceValue.EventScriptDice(rolls)));
    }

    private bool TryEvaluateUnaryOperation(string? operation, RegisterFastValue operand, out RegisterFastValue value)
    {
        var boxed = operand.ToEventScriptValue();
        value = operation switch
        {
            "-" => RegisterFastValue.FromEventScriptValue(EvaluateNegateUnary(boxed)),
            "!" => RegisterFastValue.FromEventScriptValue(EvaluateNotUnary(boxed)),
            "has value" => RegisterFastValue.Boolean(boxed.HasSemanticValue()),
            "empty" => RegisterFastValue.Boolean(boxed.IsSemanticallyEmpty()),
            "len" => RegisterFastValue.FromEventScriptValue(EvaluateLenUnary(boxed)),
            "chance" => RegisterFastValue.FromEventScriptValue(EvaluateChanceUnary(boxed)),
            "keys" => RegisterFastValue.Reference(Keys(boxed)),
            "values" => RegisterFastValue.Reference(Values(boxed)),
            "entries" => RegisterFastValue.Reference(Entries(boxed)),
            "abs" => RegisterFastValue.FromEventScriptValue(EvaluateAbsUnary(boxed)),
            "floor" or "ceil" or "round" or "rounddown" or "roundup" or "roundeven" => RegisterFastValue.FromEventScriptValue(EvaluateRoundingUnary(boxed, operation!)),
            "wrapDegree" => RegisterFastValue.FromEventScriptValue(EventScriptValueAlu.EvaluateWrapDegree(boxed)),
            _ => RegisterFastValue.Unsupported()
        };

        return value.Kind != RegisterFastValueKind.Unsupported;
    }

    private bool TryEvaluateVariadicOperation(
        string? operation,
        RegisterFastValue[] stack,
        int start,
        int count,
        out RegisterFastValue value)
    {
        if (count == 0)
        {
            value = RegisterFastValue.Nothing;
            return true;
        }

        var boxedValues = new EventScriptValue[count];
        for (var i = 0; i < count; i++)
        {
            boxedValues[i] = stack[start + i].ToEventScriptValue();
        }

        value = operation switch
        {
            "min" => RegisterFastValue.FromEventScriptValue(EventScriptValueAlu.EvaluateMinMax(boxedValues, isMax: false)),
            "max" => RegisterFastValue.FromEventScriptValue(EventScriptValueAlu.EvaluateMinMax(boxedValues, isMax: true)),
            _ => RegisterFastValue.Unsupported()
        };

        return value.Kind != RegisterFastValueKind.Unsupported;
    }

    private bool TryEvaluateBinaryOperation(string operation, RegisterFastValue leftRawFast, RegisterFastValue rightRawFast, out RegisterFastValue value)
    {
        var leftRaw = leftRawFast.ToEventScriptValue();
        var rightRaw = rightRawFast.ToEventScriptValue();

        if (operation == "default")
        {
            value = RegisterFastValue.FromEventScriptValue(EvaluateDefaultBinary(leftRaw, rightRaw));
            return true;
        }

        if (leftRaw.IsNothing() || rightRaw.IsNothing())
        {
            value = RegisterFastValue.Nothing;
            return true;
        }

        if (!EventScriptValueAlu.TryUnwrapOptionalForOperation(leftRaw, out var left) ||
            !EventScriptValueAlu.TryUnwrapOptionalForOperation(rightRaw, out var right))
        {
            value = RegisterFastValue.Reference(OptionalNone());
            return true;
        }

        value = operation switch
        {
            "|" => RegisterFastValue.Boolean(left.AsBoolean() || right.AsBoolean()),
            "^" => RegisterFastValue.Boolean(left.AsBoolean() ^ right.AsBoolean()),
            "&" => RegisterFastValue.Boolean(left.AsBoolean() && right.AsBoolean()),
            "=" or "==" => RegisterFastValue.Boolean(EventScriptValueAlu.AreEqual(left, right)),
            "<>" => RegisterFastValue.Boolean(!EventScriptValueAlu.AreEqual(left, right)),
            "in" => RegisterFastValue.Boolean(right.Contains(left)),
            "value in" => RegisterFastValue.Boolean(right.ContainsValue(left)),
            "starts with" => RegisterFastValue.Boolean(left.StartsWith(right)),
            "ends with" => RegisterFastValue.Boolean(left.EndsWith(right)),
            "<" => RegisterFastValue.Boolean(TryCompare(left, right, static comparison => comparison < 0)),
            ">" => RegisterFastValue.Boolean(TryCompare(left, right, static comparison => comparison > 0)),
            "<=" => RegisterFastValue.Boolean(TryCompare(left, right, static comparison => comparison <= 0)),
            ">=" => RegisterFastValue.Boolean(TryCompare(left, right, static comparison => comparison >= 0)),
            "+" => RegisterFastValue.FromEventScriptValue(EvaluateAddBinary(left, right)),
            "-" => RegisterFastValue.FromEventScriptValue(EvaluateNumericBinary(left, "-", right)),
            "*" => RegisterFastValue.FromEventScriptValue(EvaluateNumericBinary(left, "*", right)),
            "/" => RegisterFastValue.FromEventScriptValue(EvaluateNumericBinary(left, "/", right)),
            "mod" => RegisterFastValue.FromEventScriptValue(EvaluateNumericBinary(left, "mod", right)),
            "intersect" => RegisterFastValue.Reference(EventScriptValueAlu.EvaluateCollectionIntersect(left, right)),
            "combine" or "merge" => RegisterFastValue.Reference(EventScriptValueAlu.EvaluateCollectionCombine(left, right)),
            "except" => RegisterFastValue.Reference(EventScriptValueAlu.EvaluateCollectionExcept(left, right)),
            "zip" => RegisterFastValue.Reference(EventScriptValueAlu.EvaluateCollectionZip(left, right)),
            _ => RegisterFastValue.Unsupported()
        };

        return value.Kind != RegisterFastValueKind.Unsupported;
    }

    private static string GetBinaryOperator(RegisterFastOpCode opCode)
        => opCode switch
        {
            RegisterFastOpCode.Or => "|",
            RegisterFastOpCode.Xor => "^",
            RegisterFastOpCode.And => "&",
            RegisterFastOpCode.Equal => "=",
            RegisterFastOpCode.NotEqual => "<>",
            RegisterFastOpCode.Less => "<",
            RegisterFastOpCode.Greater => ">",
            RegisterFastOpCode.LessOrEqual => "<=",
            RegisterFastOpCode.GreaterOrEqual => ">=",
            RegisterFastOpCode.Add => "+",
            RegisterFastOpCode.Subtract => "-",
            RegisterFastOpCode.Multiply => "*",
            RegisterFastOpCode.Divide => "/",
            RegisterFastOpCode.Modulo => "mod",
            RegisterFastOpCode.Default => "default",
            RegisterFastOpCode.Contains => "in",
            RegisterFastOpCode.ContainsValue => "value in",
            RegisterFastOpCode.StartsWith => "starts with",
            RegisterFastOpCode.EndsWith => "ends with",
            RegisterFastOpCode.Intersect => "intersect",
            RegisterFastOpCode.Combine => "combine",
            RegisterFastOpCode.Except => "except",
            RegisterFastOpCode.Zip => "zip",
            _ => string.Empty
        };

    private static EventScriptValue EvaluateDefaultBinary(EventScriptValue leftRaw, EventScriptValue rightRaw)
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

    private static bool TryCompare(EventScriptValue left, EventScriptValue right, Func<int, bool> predicate)
        => EventScriptValueAlu.TryCompareNumericValues(left, right, out var comparison) &&
           predicate(comparison);

    private static EventScriptValue EvaluateAddBinary(EventScriptValue left, EventScriptValue right)
    {
        if (EventScriptValueAlu.TryEvaluatePercentageBinary(left, "+", right, out var percentage))
        {
            return percentage;
        }

        if (EventScriptValueAlu.TryEvaluateUnitBinary(left, "+", right, out var unit))
        {
            return unit;
        }

        if (EventScriptValueAlu.TryCoerceNumericForOperation(left, out var leftNumeric) &&
            EventScriptValueAlu.TryCoerceNumericForOperation(right, out var rightNumeric))
        {
            return EventScriptValueAlu.ToEventScriptDecimal(EventScriptValueAlu.AddNumeric(leftNumeric, rightNumeric));
        }

        if (EventScriptValueAlu.TryCombineWithPlus(left, right, out var combined))
        {
            return combined;
        }

        return left.IsText() || right.IsText()
            ? Text($"{EventScriptValueAlu.ToText(left)}{EventScriptValueAlu.ToText(right)}")
            : DecimalNaN();
    }

    private static EventScriptValue EvaluateNumericBinary(EventScriptValue left, string operation, EventScriptValue right)
    {
        if (EventScriptValueAlu.TryEvaluatePercentageBinary(left, operation, right, out var percentage))
        {
            return percentage;
        }

        if (EventScriptValueAlu.TryEvaluateUnitBinary(left, operation, right, out var unit))
        {
            return unit;
        }

        if (!EventScriptValueAlu.TryCoerceNumericForOperation(left, out var leftNumeric) ||
            !EventScriptValueAlu.TryCoerceNumericForOperation(right, out var rightNumeric))
        {
            return DecimalNaN();
        }

        var result = operation switch
        {
            "-" => EventScriptValueAlu.SubtractNumeric(leftNumeric, rightNumeric),
            "*" => EventScriptValueAlu.MultiplyNumeric(leftNumeric, rightNumeric),
            "/" => EventScriptValueAlu.DivideNumeric(leftNumeric, rightNumeric),
            "mod" => EventScriptValueAlu.ModuloNumeric(leftNumeric, rightNumeric),
            _ => EventScriptValueAlu.NumericValue.NaN()
        };
        return EventScriptValueAlu.ToEventScriptDecimal(result);
    }

    private static EventScriptValue EvaluateNegateUnary(EventScriptValue operand)
    {
        if (operand.IsNothing())
        {
            return EventScriptValue.Nothing;
        }

        if (!EventScriptValueAlu.TryUnwrapOptionalForOperation(operand, out var unwrapped))
        {
            return OptionalNone();
        }

        if (unwrapped.IsPercentage())
        {
            return Percentage(-unwrapped.AsNumber());
        }

        if (EventScriptValue.TryGetDecimalUnit(unwrapped, out var unit))
        {
            return Decimal(-unwrapped.AsNumber(), unit);
        }

        return EventScriptValueAlu.TryCoerceNumericForOperation(unwrapped, out var number)
            ? EventScriptValueAlu.ToEventScriptDecimal(EventScriptValueAlu.NegateNumeric(number))
            : EventScriptValue.Nothing;
    }

    private static EventScriptValue EvaluateNotUnary(EventScriptValue operand)
    {
        if (operand.IsNothing())
        {
            return EventScriptValue.Nothing;
        }

        return EventScriptValueAlu.TryUnwrapOptionalForOperation(operand, out var unwrapped)
            ? Boolean(!unwrapped.AsBoolean())
            : OptionalNone();
    }

    private EventScriptValue EvaluateLenUnary(EventScriptValue operand)
    {
        if (operand.IsNothing())
        {
            return Integer(0);
        }

        return operand.Kind switch
        {
            EventScriptValueKind.Text => Integer(operand.AsText().Length),
            EventScriptValueKind.Iterator => CountEnumerableWithBudget(operand.AsEnumerable(), "Iterator length evaluation budget exhausted."),
            EventScriptValueKind.Range => EvaluateRangeLength(operand),
            EventScriptValueKind.List => Integer(operand.AsList().Count),
            EventScriptValueKind.Dictionary => Integer(operand.AsDictionary().Count),
            EventScriptValueKind.Set => Integer(operand.AsSet().Count),
            EventScriptValueKind.Dice => Integer(operand.AsDice().Rolls.Count),
            EventScriptValueKind.Optional => Integer(operand.AsOptional().HasValue ? 1 : 0),
            _ => EventScriptValue.Nothing
        };
    }

    private EventScriptValue EvaluateRangeLength(EventScriptValue operand)
    {
        if (!EventScriptRuntimeLimitUtilities.TryGetRangeLength(operand, out var length))
        {
            return EventScriptValue.Nothing;
        }

        return _context.RuntimeBudget.TryCheckRangeLength(length, "Range length exceeds the configured limit.")
            ? Integer(length)
            : EventScriptValue.Nothing;
    }

    private EventScriptValue CountEnumerableWithBudget(IEnumerable<EventScriptValue> values, string detail)
    {
        long count = 0;
        foreach (var _ in values)
        {
            if (!_context.RuntimeBudget.TryConsumeLoopIteration(detail))
            {
                return EventScriptValue.Nothing;
            }

            count++;
        }

        return Integer(count);
    }

    private EventScriptValue EvaluateChanceUnary(EventScriptValue operand)
    {
        var percentage = ConvertToPercentage(operand);
        if (!percentage.IsPercentage())
        {
            return Boolean(false);
        }

        var ratio = percentage.AsNumber();
        if (ratio <= 0m)
        {
            return Boolean(false);
        }

        if (ratio >= 1m)
        {
            return Boolean(true);
        }

        return TryNextInclusiveDecimal(0m, 1m, out var randomValue)
            ? Boolean(randomValue < ratio)
            : Boolean(false);
    }

    private static EventScriptValue EvaluateRoundingUnary(EventScriptValue operand, string operation)
    {
        if (operand.IsNothing())
        {
            return EventScriptValue.Nothing;
        }

        if (EventScriptValueAlu.TryEvaluateUnitRounding(operand, operation, out var unitRounded))
        {
            return unitRounded;
        }

        if (!EventScriptValueAlu.TryCoerceNumericForOperation(operand, out var number) || number.IsNaN)
        {
            return EventScriptValue.Nothing;
        }

        if (number.IsPositiveInfinity)
        {
            return Integer(long.MaxValue);
        }

        if (number.IsNegativeInfinity)
        {
            return Integer(long.MinValue);
        }

        return operation switch
        {
            "floor" or "rounddown" => Integer(EventScriptValueAlu.ToIntegerSaturated(Math.Floor(number.Value))),
            "ceil" or "roundup" => Integer(EventScriptValueAlu.ToIntegerSaturated(Math.Ceiling(number.Value))),
            "round" or "roundeven" => Integer(EventScriptValueAlu.ToIntegerSaturated(Math.Round(number.Value, 0, MidpointRounding.ToEven))),
            _ => EventScriptValue.Nothing
        };
    }

    private static EventScriptValue EvaluateAbsUnary(EventScriptValue operand)
    {
        if (operand.IsNothing())
        {
            return EventScriptValue.Nothing;
        }

        return EventScriptValueAlu.TryCoerceNumericForOperation(operand, out var number) && number.IsFinite
            ? Decimal(Math.Abs(number.Value))
            : EventScriptValue.Nothing;
    }

    private static RegisterFastValue EvaluateClamp(RegisterFastValue rawValue, RegisterFastValue minimumValue, RegisterFastValue maximumValue)
    {
        var raw = rawValue.ToEventScriptValue();
        var minimum = minimumValue.ToEventScriptValue();
        var maximum = maximumValue.ToEventScriptValue();

        if (!EventScriptValueAlu.HaveCompatibleNumericUnits(raw, minimum) ||
            !EventScriptValueAlu.HaveCompatibleNumericUnits(raw, maximum) ||
            !EventScriptValueAlu.HaveCompatibleNumericUnits(minimum, maximum))
        {
            return RegisterFastValue.NaN();
        }

        if (!EventScriptValueAlu.TryCoerceNumericForOperation(raw, out var rawNumber) ||
            !EventScriptValueAlu.TryCoerceNumericForOperation(minimum, out var minimumNumber) ||
            !EventScriptValueAlu.TryCoerceNumericForOperation(maximum, out var maximumNumber) ||
            !rawNumber.IsFinite ||
            !minimumNumber.IsFinite ||
            !maximumNumber.IsFinite)
        {
            return RegisterFastValue.Nothing;
        }

        var lower = Math.Min(minimumNumber.Value, maximumNumber.Value);
        var upper = Math.Max(minimumNumber.Value, maximumNumber.Value);
        EventScriptValue.TryGetDecimalUnit(raw, out var unit);
        return RegisterFastValue.FromEventScriptValue(Decimal(
            Math.Min(Math.Max(rawNumber.Value, lower), upper),
            raw.HasDecimalUnit() ? unit : null));
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

    private void PushSeededRandomScope(EventScriptValue seedValue)
        => _randomScopes.Push(EventScriptRandomGenerator.FromSeed(DeriveStableSeed(seedValue)));

    private void PopSeededRandomScope()
    {
        if (_randomScopes.Count > 1)
        {
            _randomScopes.Pop();
        }
    }

    private static int ToIntSaturated(long value)
    {
        if (value > int.MaxValue) return int.MaxValue;
        if (value < int.MinValue) return int.MinValue;
        return (int)value;
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
        => value.Kind switch
        {
            EventScriptValueKind.Nothing => "nothing",
            EventScriptValueKind.Tag => $"tag:{value.AsText()}",
            EventScriptValueKind.Text => $"text:{value.AsText()}",
            EventScriptValueKind.Percentage => $"percentage:{value.AsNumber().ToString(CultureInfo.InvariantCulture)}",
            EventScriptValueKind.Vector2 => $"vector2:{((EventScriptVector2Value)value).X.ToString(CultureInfo.InvariantCulture)}:{((EventScriptVector2Value)value).Y.ToString(CultureInfo.InvariantCulture)}",
            EventScriptValueKind.Vector3 => $"vector3:{((EventScriptVector3Value)value).X.ToString(CultureInfo.InvariantCulture)}:{((EventScriptVector3Value)value).Y.ToString(CultureInfo.InvariantCulture)}:{((EventScriptVector3Value)value).Z.ToString(CultureInfo.InvariantCulture)}",
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
            EventScriptValueKind.Iterator => $"iterator:[{string.Join("|", value.AsEnumerable().Select(BuildStableSeedText))}]",
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

    private bool TryEvaluateRulePredicate(RulePredicateExpressionNode rulePredicate, out RegisterFastValue value)
    {
        if (!_compiledScript.Callables.TryGetValue(rulePredicate.RuleName, out var callable) ||
            callable.Kind != LinkedCallableKind.Rule ||
            callable.Parameters.Count != 1 ||
            !TryEvaluate(rulePredicate.Value, out var input))
        {
            value = RegisterFastValue.Nothing;
            return false;
        }

        var instruction = new RegisterFastInstruction(
            RegisterFastOpCode.RulePredicate,
            A: -1,
            ExpressionProgram: _plan.TryGetExpressionProgram(callable.Expression, out var expressionProgram)
                ? expressionProgram
                : null,
            DiagnosticName: callable.Name,
            DiagnosticArgumentName: callable.Parameters[0]);

        if (!TryEvaluateRulePredicate(instruction, input, stackBase: 0, out value, callable.Parameters[0]))
        {
            return false;
        }

        return true;
    }

    private bool TryEvaluateCall(CallExpressionNode call, out RegisterFastValue value)
    {
        if (!_compiledScript.Callables.TryGetValue(call.Name, out var callable) ||
            callable.Parameters.Count != call.Arguments.Count)
        {
            value = RegisterFastValue.Nothing;
            return false;
        }

        var arguments = new RegisterFastValue[call.Arguments.Count];
        for (var argumentIndex = 0; argumentIndex < call.Arguments.Count; argumentIndex++)
        {
            if (!TryEvaluate(call.Arguments[argumentIndex], out arguments[argumentIndex]))
            {
                value = RegisterFastValue.Nothing;
                return false;
            }
        }

        var slots = new int[callable.Parameters.Count];
        for (var parameterIndex = 0; parameterIndex < callable.Parameters.Count; parameterIndex++)
        {
            if (!_plan.TryGetSlot(callable.Parameters[parameterIndex], out slots[parameterIndex]))
            {
                value = RegisterFastValue.Nothing;
                return false;
            }
        }

        var instruction = new RegisterFastInstruction(
            RegisterFastOpCode.Call,
            A: call.Arguments.Count,
            CallableKind: callable.Kind == LinkedCallableKind.Rule
                ? RegisterFastCallableKind.Rule
                : RegisterFastCallableKind.Select,
            ExpressionProgram: _plan.TryGetExpressionProgram(callable.Expression, out var expressionProgram)
                ? expressionProgram
                : null,
            DiagnosticName: callable.Name,
            Names: callable.Parameters.ToArray(),
            Slots: slots);

        return TryEvaluateCallable(instruction, arguments, 0, arguments.Length, out value);
    }

    private bool TryEvaluateRulePredicate(
        RegisterFastInstruction instruction,
        RegisterFastValue input,
        int stackBase,
        out RegisterFastValue value,
        string? parameterName = null)
    {
        value = RegisterFastValue.Nothing;
        if (instruction.ExpressionProgram is null)
        {
            return false;
        }

        if (!_context.RuntimeBudget.TryEnterCall(CallableCallDepthExceededDetail))
        {
            return true;
        }

        if (instruction.A >= 0)
        {
            try
            {
                RecordRuleCalled(instruction, input);
                if (!TryExecuteExpressionProgramWithTemporarySlot(instruction.A, input, instruction.ExpressionProgram, stackBase, out value))
                {
                    value = RegisterFastValue.Nothing;
                    return false;
                }

                value = RegisterFastValue.Boolean(value.AsBoolean());
                return true;
            }
            finally
            {
                _context.RuntimeBudget.ExitCall();
            }
        }

        EnterScope();
        try
        {
            RecordRuleCalled(instruction, input);
            var parameterDefined = !string.IsNullOrEmpty(parameterName) && Define(parameterName!, input);
            if (!parameterDefined ||
                !TryExecuteExpressionProgram(instruction.ExpressionProgram, stackBase, out value))
            {
                value = RegisterFastValue.Nothing;
                return false;
            }

            value = RegisterFastValue.Boolean(value.AsBoolean());
            return true;
        }
        finally
        {
            ExitScope();
            _context.RuntimeBudget.ExitCall();
        }
    }

    private bool TryEvaluateCallable(
        RegisterFastInstruction instruction,
        RegisterFastValue[] stack,
        int start,
        int count,
        out RegisterFastValue value)
    {
        value = RegisterFastValue.Nothing;
        if (instruction.ExpressionProgram is null ||
            instruction.Slots is null ||
            instruction.Names is null ||
            instruction.Slots.Length != count ||
            instruction.Names.Length != count)
        {
            return false;
        }

        var callableName = instruction.DiagnosticName ?? string.Empty;
        if (!_context.RuntimeBudget.TryEnterCall(CallableCallDepthExceededDetail))
        {
            return true;
        }

        EnterScope();
        try
        {
            RecordCallableCalled(instruction, stack, start, count);
            for (var argumentIndex = 0; argumentIndex < count; argumentIndex++)
            {
                if (!DefineSlot(instruction.Slots[argumentIndex], stack[start + argumentIndex]))
                {
                    value = RegisterFastValue.Nothing;
                    return false;
                }
            }

            if (!TryExecuteExpressionProgram(instruction.ExpressionProgram, start, out value))
            {
                return false;
            }

            if (instruction.CallableKind == RegisterFastCallableKind.Rule)
            {
                value = RegisterFastValue.Boolean(value.AsBoolean());
            }

            return true;
        }
        finally
        {
            ExitScope();
            _context.RuntimeBudget.ExitCall();
        }
    }

    private bool TryEvaluateTypeCast(TypeCastExpressionNode typeCast, out RegisterFastValue value)
    {
        if (!TryEvaluate(typeCast.Value, out var input))
        {
            value = RegisterFastValue.Nothing;
            return false;
        }

        if (!TryConvertDeclaredType(typeCast.TypeName, input, out value))
        {
            value = RegisterFastValue.Unsupported();
        }

        return value.Kind != RegisterFastValueKind.Unsupported;
    }

    private bool TryConvertDeclaredType(string declaredType, RegisterFastValue input, out RegisterFastValue value)
    {
        var boxed = input.ToEventScriptValue();
        value = declaredType switch
        {
            "nothing" => RegisterFastValue.Nothing,
            "tag" => RegisterFastValue.Reference(Tag(boxed.AsText())),
            "text" => RegisterFastValue.Reference(Text(EventScriptValueAlu.ToText(boxed))),
            "percentage" => RegisterFastValue.FromEventScriptValue(ConvertToPercentage(boxed)),
            "degree" => RegisterFastValue.FromEventScriptValue(ConvertToDecimalUnit(boxed, EventScriptDecimalUnit.Degree)),
            "meter" => RegisterFastValue.FromEventScriptValue(ConvertToDecimalUnit(boxed, EventScriptDecimalUnit.Meter)),
            "second" => RegisterFastValue.FromEventScriptValue(ConvertToDecimalUnit(boxed, EventScriptDecimalUnit.Second)),
            "vector2" => RegisterFastValue.Reference(ConvertToVector2(boxed)),
            "vector3" => RegisterFastValue.Reference(ConvertToVector3(boxed)),
            "boolean" => RegisterFastValue.Boolean(boxed.AsBoolean()),
            "integer" => RegisterFastValue.Integer(boxed.AsInteger()),
            "decimal" or "number" => RegisterFastValue.FromEventScriptValue(ConvertToDecimal(boxed)),
            "list" => TryCheckMaterializedValue(boxed, "List conversion would materialize more range items than allowed.")
                ? RegisterFastValue.Reference(List(boxed.AsList()))
                : RegisterFastValue.Nothing,
            "range" => RegisterFastValue.Reference(boxed.IsRange() ? boxed : EventScriptValue.Nothing),
            "message" => boxed.Kind == EventScriptValueKind.Message
                ? input
                : EventScriptMessageValueCodec.TryReadMessageValue(boxed, out var message)
                    ? RegisterFastValue.Reference(EventScriptMessageValueCodec.CreateMessageValue(message))
                    : RegisterFastValue.Nothing,
            "handler" => boxed.Kind == EventScriptValueKind.Handler
                ? input
                : EventScriptMessageValueCodec.TryReadHandlerValue(boxed, out var handler)
                    ? RegisterFastValue.Reference(EventScriptMessageValueCodec.CreateHandlerValue(handler))
                    : RegisterFastValue.Nothing,
            "dictionary" => RegisterFastValue.Reference(Dictionary(boxed.AsDictionary())),
            "set" => TryCheckMaterializedValue(boxed, "Set conversion would materialize more range items than allowed.")
                ? RegisterFastValue.Reference(Set(boxed.AsSet()))
                : RegisterFastValue.Nothing,
            "dice" => RegisterFastValue.Reference(Dice(boxed.AsDice())),
            "optional" => boxed.IsOptional()
                ? input
                : boxed.IsNothing()
                    ? RegisterFastValue.Reference(OptionalNone())
                    : RegisterFastValue.Reference(OptionalSome(boxed)),
            _ => RegisterFastValue.Unsupported()
        };

        return value.Kind != RegisterFastValueKind.Unsupported;
    }

    private bool TryCheckMaterializedValue(EventScriptValue value, string detail)
        => !EventScriptRuntimeLimitUtilities.TryGetRangeLength(value, out var length) ||
           _context.RuntimeBudget.TryCheckRangeLength(length, detail);

    private static string GetCastTypeName(RegisterFastCastKind castKind)
        => castKind switch
        {
            RegisterFastCastKind.Boolean => "boolean",
            RegisterFastCastKind.Integer => "integer",
            RegisterFastCastKind.Decimal => "decimal",
            RegisterFastCastKind.Number => "number",
            RegisterFastCastKind.Percentage => "percentage",
            RegisterFastCastKind.Degree => "degree",
            RegisterFastCastKind.Meter => "meter",
            RegisterFastCastKind.Second => "second",
            _ => string.Empty
        };

    private static EventScriptValue ConvertToDecimal(EventScriptValue value)
    {
        if (!EventScriptValueAlu.TryUnwrapOptionalForOperation(value, out var unwrappedNumber))
        {
            return DecimalNaN();
        }

        return EventScriptValueAlu.TryCoerceNumericForOperation(unwrappedNumber, out var number)
            ? EventScriptValueAlu.ToEventScriptDecimal(number)
            : DecimalNaN();
    }

    private static EventScriptValue ConvertToPercentage(EventScriptValue value)
    {
        if (!EventScriptValueAlu.TryUnwrapOptionalForOperation(value, out var unwrapped))
        {
            return DecimalNaN();
        }

        if (unwrapped.IsPercentage())
        {
            return unwrapped;
        }

        if (unwrapped.HasDecimalUnit())
        {
            return DecimalNaN();
        }

        if (!EventScriptValueAlu.TryCoerceNumericForOperation(unwrapped, out var number) ||
            !number.IsFinite)
        {
            return DecimalNaN();
        }

        var ratio = unwrapped.Kind == EventScriptValueKind.Integer
            ? number.Value / 100m
            : number.Value > 1m || number.Value < -1m
                ? number.Value / 100m
                : number.Value;
        return Percentage(ratio);
    }

    private static EventScriptValue ConvertToDecimalUnit(EventScriptValue value, EventScriptDecimalUnit unit)
    {
        if (!EventScriptValueAlu.TryUnwrapOptionalForOperation(value, out var unwrapped))
        {
            return DecimalNaN();
        }

        if (unwrapped.Kind is EventScriptValueKind.Decimal or EventScriptValueKind.Integer &&
            EventScriptValueAlu.TryCoerceNumericForOperation(unwrapped, out var number) &&
            number.IsFinite)
        {
            return Decimal(number.Value, unit);
        }

        return DecimalNaN();
    }

    private static EventScriptValue ConvertToVector2(EventScriptValue value)
    {
        if (!EventScriptValueAlu.TryUnwrapOptionalForOperation(value, out var unwrapped))
        {
            return EventScriptValue.Nothing;
        }

        if (unwrapped is EventScriptVector2Value vector2)
        {
            return vector2;
        }

        if (unwrapped is EventScriptVector3Value vector3)
        {
            return Vector2(vector3.X, vector3.Y);
        }

        if (TryReadVectorComponent(unwrapped, "x", out var x) &&
            TryReadVectorComponent(unwrapped, "y", out var y))
        {
            return Vector2(x, y);
        }

        var items = unwrapped.AsList();
        if (items.Count >= 2 &&
            TryReadVectorComponent(items[0], out x) &&
            TryReadVectorComponent(items[1], out y))
        {
            return Vector2(x, y);
        }

        return EventScriptValue.Nothing;
    }

    private static EventScriptValue ConvertToVector3(EventScriptValue value)
    {
        if (!EventScriptValueAlu.TryUnwrapOptionalForOperation(value, out var unwrapped))
        {
            return EventScriptValue.Nothing;
        }

        if (unwrapped is EventScriptVector3Value vector3)
        {
            return vector3;
        }

        if (unwrapped is EventScriptVector2Value vector2)
        {
            return Vector3(vector2.X, vector2.Y, 0m);
        }

        if (TryReadVectorComponent(unwrapped, "x", out var x) &&
            TryReadVectorComponent(unwrapped, "y", out var y))
        {
            var z = TryReadVectorComponent(unwrapped, "z", out var zValue) ? zValue : 0m;
            return Vector3(x, y, z);
        }

        var items = unwrapped.AsList();
        if (items.Count >= 2 &&
            TryReadVectorComponent(items[0], out x) &&
            TryReadVectorComponent(items[1], out y))
        {
            var z = items.Count >= 3 && TryReadVectorComponent(items[2], out var zValue) ? zValue : 0m;
            return Vector3(x, y, z);
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
        if (!EventScriptValueAlu.TryUnwrapOptionalForOperation(component, out var unwrapped) ||
            !EventScriptValueAlu.TryCoerceNumericForOperation(unwrapped, out var number) ||
            !number.IsFinite)
        {
            value = default;
            return false;
        }

        value = number.Value;
        return true;
    }

    private bool TryExecutePipelineProgram(RegisterFastPipelineProgram pipeline, out RegisterFastValue value)
    {
        if (!TryExecuteExpressionProgram(pipeline.SourceProgram, 0, out var sourceValue))
        {
            value = RegisterFastValue.Nothing;
            return false;
        }

        var sourceItems = sourceValue.ToEventScriptValue().AsList();
        return pipeline.TerminalSelector.Kind switch
        {
            RegisterFastSelectorKind.Sum => TryExecuteProgramSum(sourceItems, pipeline, out value),
            RegisterFastSelectorKind.Average => TryExecuteProgramAverage(sourceItems, pipeline, out value),
            RegisterFastSelectorKind.Count => TryExecuteProgramCount(sourceItems, pipeline, out value),
            RegisterFastSelectorKind.Edge => TryExecuteProgramEdge(sourceItems, pipeline, out value),
            _ => Fail(out value)
        };
    }

    private bool TryExecuteProgramSum(
        IReadOnlyList<EventScriptValue> sourceItems,
        RegisterFastPipelineProgram pipeline,
        out RegisterFastValue value)
    {
        var terminal = pipeline.TerminalSelector;
        if (terminal.ExpressionProgram is null)
        {
            value = RegisterFastValue.Nothing;
            return false;
        }

        var hasValue = false;
        var sum = RegisterFastValue.Decimal(0m);
        var prefixSelectors = pipeline.PrefixSelectors;
        for (var itemIndex = 0; itemIndex < sourceItems.Count; itemIndex++)
        {
            if (!TryApplyProgramPipelinePrefix(
                    RegisterFastValue.FromEventScriptValue(sourceItems[itemIndex]),
                    prefixSelectors,
                    out var item,
                    out var include))
            {
                value = RegisterFastValue.Nothing;
                return false;
            }

            if (!include)
            {
                continue;
            }

            if (!TryEvaluateProgramProjection(terminal.IdentifierSlot, terminal.ExpressionProgram, item, out var projected))
            {
                value = RegisterFastValue.Nothing;
                return false;
            }

            sum = hasValue ? RegisterFastValue.Add(sum, projected) : projected;
            hasValue = true;
        }

        value = hasValue ? sum : RegisterFastValue.Decimal(0m);
        return true;
    }

    private bool TryExecuteProgramAverage(
        IReadOnlyList<EventScriptValue> sourceItems,
        RegisterFastPipelineProgram pipeline,
        out RegisterFastValue value)
    {
        var terminal = pipeline.TerminalSelector;
        if (terminal.ExpressionProgram is null)
        {
            value = RegisterFastValue.Nothing;
            return false;
        }

        var count = 0L;
        var sum = RegisterFastValue.Decimal(0m);
        var prefixSelectors = pipeline.PrefixSelectors;
        for (var itemIndex = 0; itemIndex < sourceItems.Count; itemIndex++)
        {
            if (!TryApplyProgramPipelinePrefix(
                    RegisterFastValue.FromEventScriptValue(sourceItems[itemIndex]),
                    prefixSelectors,
                    out var item,
                    out var include))
            {
                value = RegisterFastValue.Nothing;
                return false;
            }

            if (!include)
            {
                continue;
            }

            if (!TryEvaluateProgramProjection(terminal.IdentifierSlot, terminal.ExpressionProgram, item, out var projected))
            {
                value = RegisterFastValue.Nothing;
                return false;
            }

            sum = count == 0 ? projected : RegisterFastValue.Add(sum, projected);
            count++;
        }

        value = count > 0 && sum.TryGetFiniteNumber(out var number)
            ? RegisterFastValue.Decimal(number / count, sum.Unit)
            : RegisterFastValue.Nothing;
        return true;
    }

    private bool TryExecuteProgramCount(
        IReadOnlyList<EventScriptValue> sourceItems,
        RegisterFastPipelineProgram pipeline,
        out RegisterFastValue value)
    {
        var terminal = pipeline.TerminalSelector;
        if (terminal.ExpressionProgram is null)
        {
            value = RegisterFastValue.Nothing;
            return false;
        }

        var count = 0L;
        var prefixSelectors = pipeline.PrefixSelectors;
        for (var itemIndex = 0; itemIndex < sourceItems.Count; itemIndex++)
        {
            if (!TryApplyProgramPipelinePrefix(
                    RegisterFastValue.FromEventScriptValue(sourceItems[itemIndex]),
                    prefixSelectors,
                    out var item,
                    out var include))
            {
                value = RegisterFastValue.Nothing;
                return false;
            }

            if (!include)
            {
                continue;
            }

            if (!TryEvaluateProgramProjection(terminal.IdentifierSlot, terminal.ExpressionProgram, item, out var predicate))
            {
                value = RegisterFastValue.Nothing;
                return false;
            }

            if (predicate.AsBoolean())
            {
                count++;
            }
        }

        value = RegisterFastValue.Integer(count);
        return true;
    }

    private bool TryExecuteProgramEdge(
        IReadOnlyList<EventScriptValue> sourceItems,
        RegisterFastPipelineProgram pipeline,
        out RegisterFastValue value)
    {
        var terminal = pipeline.TerminalSelector;
        RegisterFastValue first = RegisterFastValue.Nothing;
        RegisterFastValue last = RegisterFastValue.Nothing;
        var count = 0;
        var prefixSelectors = pipeline.PrefixSelectors;
        for (var itemIndex = 0; itemIndex < sourceItems.Count; itemIndex++)
        {
            if (!TryApplyProgramPipelinePrefix(
                    RegisterFastValue.FromEventScriptValue(sourceItems[itemIndex]),
                    prefixSelectors,
                    out var item,
                    out var include))
            {
                value = RegisterFastValue.Nothing;
                return false;
            }

            if (!include)
            {
                continue;
            }

            if (terminal.ExpressionProgram is not null &&
                (!TryEvaluateProgramProjection(terminal.IdentifierSlot, terminal.ExpressionProgram, item, out var predicate) || !predicate.AsBoolean()))
            {
                continue;
            }

            first = count == 0 ? item : first;
            last = item;
            count++;
        }

        value = terminal.EdgeMode switch
        {
            "first" => count > 0 ? first : RegisterFastValue.Nothing,
            "last" => count > 0 ? last : RegisterFastValue.Nothing,
            "single" => count == 1 ? first : RegisterFastValue.Nothing,
            _ => RegisterFastValue.Nothing
        };
        return true;
    }

    private bool TryApplyProgramPipelinePrefix(
        RegisterFastValue item,
        RegisterFastSelectorProgram[] prefixSelectors,
        out RegisterFastValue value,
        out bool include)
    {
        value = item;
        include = true;

        for (var selectorIndex = 0; selectorIndex < prefixSelectors.Length; selectorIndex++)
        {
            var selector = prefixSelectors[selectorIndex];
            if (selector.ExpressionProgram is null)
            {
                return false;
            }

            switch (selector.Kind)
            {
                case RegisterFastSelectorKind.Filter:
                    if (!TryEvaluateProgramProjection(selector.IdentifierSlot, selector.ExpressionProgram, value, out var predicate))
                    {
                        return false;
                    }

                    if (!predicate.AsBoolean())
                    {
                        include = false;
                        return true;
                    }

                    break;

                case RegisterFastSelectorKind.Select:
                    if (!TryEvaluateProgramProjection(selector.IdentifierSlot, selector.ExpressionProgram, value, out var selected))
                    {
                        return false;
                    }

                    value = selected;
                    break;

                default:
                    return false;
            }
        }

        return true;
    }

    private bool TryEvaluateProgramProjection(
        int identifierSlot,
        RegisterFastExpressionProgram expressionProgram,
        RegisterFastValue item,
        out RegisterFastValue value)
        => TryExecuteExpressionProgramWithTemporarySlot(identifierSlot, item, expressionProgram, 0, out value);

    private bool TryEvaluateCollectionAccess(CollectionAccessExpressionNode expression, out RegisterFastValue value)
    {
        if (expression.Selector is ExpressionSelectorNode expressionSelector)
        {
            if (!TryEvaluate(expression.Target, out var target) ||
                !TryEvaluate(expressionSelector.Expression, out var selector))
            {
                value = RegisterFastValue.Nothing;
                return false;
            }

            value = EvaluateIndexedAccess(target, selector);
            return true;
        }

        var selectors = new List<CollectionSelectorNode>();
        ExpressionNode source = expression;
        while (source is CollectionAccessExpressionNode collectionAccess)
        {
            selectors.Add(collectionAccess.Selector);
            source = collectionAccess.Target;
        }

        selectors.Reverse();
        if (selectors.Count == 0 || !TryEvaluate(source, out var sourceValue))
        {
            value = RegisterFastValue.Nothing;
            return false;
        }

        var sourceItems = sourceValue.ToEventScriptValue().AsList();
        var prefixCount = Math.Max(0, selectors.Count - 1);
        var terminal = selectors[^1];
        return terminal switch
        {
            SumSelectorNode sum => TryEvaluateSum(sourceItems, selectors, prefixCount, sum, out value),
            AverageSelectorNode average => TryEvaluateAverage(sourceItems, selectors, prefixCount, average, out value),
            CountSelectorNode count => TryEvaluateCount(sourceItems, selectors, prefixCount, count, out value),
            EdgeSelectorNode edge => TryEvaluateEdge(sourceItems, selectors, prefixCount, edge, out value),
            _ => Fail(out value)
        };
    }

    private bool TryEvaluateSum(
        IReadOnlyList<EventScriptValue> sourceItems,
        IReadOnlyList<CollectionSelectorNode> selectors,
        int prefixCount,
        SumSelectorNode selector,
        out RegisterFastValue value)
    {
        var hasValue = false;
        var sum = RegisterFastValue.Decimal(0m);
        var ok = TryForEachPipelineItem(sourceItems, selectors, prefixCount, item =>
        {
            if (!TryEvaluateProjection(selector.Identifier, selector.Projection, item, out var projected))
            {
                return false;
            }

            sum = hasValue ? RegisterFastValue.Add(sum, projected) : projected;
            hasValue = true;
            return true;
        });

        value = hasValue ? sum : RegisterFastValue.Decimal(0m);
        return ok;
    }

    private bool TryEvaluateAverage(
        IReadOnlyList<EventScriptValue> sourceItems,
        IReadOnlyList<CollectionSelectorNode> selectors,
        int prefixCount,
        AverageSelectorNode selector,
        out RegisterFastValue value)
    {
        var count = 0L;
        var sum = RegisterFastValue.Decimal(0m);
        var ok = TryForEachPipelineItem(sourceItems, selectors, prefixCount, item =>
        {
            if (!TryEvaluateProjection(selector.Identifier, selector.Projection, item, out var projected))
            {
                return false;
            }

            sum = count == 0 ? projected : RegisterFastValue.Add(sum, projected);
            count++;
            return true;
        });

        value = ok && count > 0 && sum.TryGetFiniteNumber(out var number)
            ? RegisterFastValue.Decimal(number / count, sum.Unit)
            : RegisterFastValue.Nothing;
        return ok;
    }

    private bool TryEvaluateCount(
        IReadOnlyList<EventScriptValue> sourceItems,
        IReadOnlyList<CollectionSelectorNode> selectors,
        int prefixCount,
        CountSelectorNode selector,
        out RegisterFastValue value)
    {
        var count = 0L;
        var ok = TryForEachPipelineItem(sourceItems, selectors, prefixCount, item =>
        {
            if (!TryEvaluateProjection(selector.Identifier, selector.Predicate, item, out var predicate))
            {
                return false;
            }

            if (predicate.AsBoolean())
            {
                count++;
            }

            return true;
        });

        value = RegisterFastValue.Integer(count);
        return ok;
    }

    private bool TryEvaluateEdge(
        IReadOnlyList<EventScriptValue> sourceItems,
        IReadOnlyList<CollectionSelectorNode> selectors,
        int prefixCount,
        EdgeSelectorNode selector,
        out RegisterFastValue value)
    {
        RegisterFastValue first = RegisterFastValue.Nothing;
        RegisterFastValue last = RegisterFastValue.Nothing;
        var count = 0;
        var ok = TryForEachPipelineItem(sourceItems, selectors, prefixCount, item =>
        {
            if (!string.IsNullOrEmpty(selector.Identifier) &&
                selector.Predicate is not null &&
                (!TryEvaluateProjection(selector.Identifier!, selector.Predicate, item, out var predicate) || !predicate.AsBoolean()))
            {
                return true;
            }

            first = count == 0 ? item : first;
            last = item;
            count++;
            return true;
        });

        value = selector.Mode switch
        {
            "first" => count > 0 ? first : RegisterFastValue.Nothing,
            "last" => count > 0 ? last : RegisterFastValue.Nothing,
            "single" => count == 1 ? first : RegisterFastValue.Nothing,
            _ => RegisterFastValue.Nothing
        };
        return ok;
    }

    private bool TryForEachPipelineItem(
        IReadOnlyList<EventScriptValue> sourceItems,
        IReadOnlyList<CollectionSelectorNode> selectors,
        int prefixCount,
        Func<RegisterFastValue, bool> action)
    {
        for (var index = 0; index < sourceItems.Count; index++)
        {
            if (!TryApplyPipelinePrefix(RegisterFastValue.FromEventScriptValue(sourceItems[index]), selectors, 0, prefixCount, action))
            {
                return false;
            }
        }

        return true;
    }

    private bool TryApplyPipelinePrefix(
        RegisterFastValue item,
        IReadOnlyList<CollectionSelectorNode> selectors,
        int index,
        int prefixCount,
        Func<RegisterFastValue, bool> action)
    {
        if (index >= prefixCount)
        {
            return action(item);
        }

        switch (selectors[index])
        {
            case FilterSelectorNode filter:
                if (!TryEvaluateProjection(filter.Identifier, filter.Predicate, item, out var predicate))
                {
                    return false;
                }

                return !predicate.AsBoolean() ||
                       TryApplyPipelinePrefix(item, selectors, index + 1, prefixCount, action);

            case SelectSelectorNode select:
                return TryEvaluateProjection(select.Identifier, select.Projection, item, out var selected) &&
                       TryApplyPipelinePrefix(selected, selectors, index + 1, prefixCount, action);

            default:
                return false;
        }
    }

    private bool TryEvaluateProjection(string identifier, ExpressionNode expression, RegisterFastValue item, out RegisterFastValue value)
    {
        if (!_plan.TryGetSlot(identifier, out var slot))
        {
            value = RegisterFastValue.Nothing;
            return false;
        }

        if ((uint)slot >= (uint)_locals.Length)
        {
            value = RegisterFastValue.Nothing;
            return false;
        }

        var hadValue = _assignedSlots[slot];
        var previous = _locals[slot];
        _locals[slot] = item;
        _assignedSlots[slot] = true;
        try
        {
            return TryEvaluate(expression, out value);
        }
        finally
        {
            RestoreSlot(slot, hadValue, previous);
        }
    }

    private bool TryExecuteExpressionProgramWithTemporarySlot(
        int slot,
        RegisterFastValue slotValue,
        RegisterFastExpressionProgram expressionProgram,
        int stackBase,
        out RegisterFastValue value)
    {
        if ((uint)slot >= (uint)_locals.Length)
        {
            value = RegisterFastValue.Nothing;
            return false;
        }

        var hadValue = _assignedSlots[slot];
        var previous = _locals[slot];
        _locals[slot] = slotValue;
        _assignedSlots[slot] = true;
        try
        {
            return TryExecuteExpressionProgram(expressionProgram, stackBase, out value);
        }
        finally
        {
            RestoreSlot(slot, hadValue, previous);
        }
    }

    private void RestoreSlot(int slot, bool hadValue, RegisterFastValue previous)
    {
        if (hadValue)
        {
            _locals[slot] = previous;
            _assignedSlots[slot] = true;
        }
        else
        {
            _locals[slot] = RegisterFastValue.Nothing;
            _assignedSlots[slot] = false;
        }
    }

    private void EnterScope() => _scopeMarks.Add(_changes.Count);

    private void ExitScope()
    {
        var markIndex = _scopeMarks.Count - 1;
        var mark = _scopeMarks[markIndex];
        _scopeMarks.RemoveAt(markIndex);
        for (var i = _changes.Count - 1; i >= mark; i--)
        {
            var change = _changes[i];
            if (change.HadValue)
            {
                _locals[change.Slot] = change.PreviousValue;
                _assignedSlots[change.Slot] = true;
            }
            else
            {
                _locals[change.Slot] = RegisterFastValue.Nothing;
                _assignedSlots[change.Slot] = false;
            }
        }

        _changes.RemoveRange(mark, _changes.Count - mark);
    }

    private bool Define(string name, RegisterFastValue value)
    {
        if (!_plan.TryGetSlot(name, out var slot))
        {
            return false;
        }

        return DefineSlot(slot, value);
    }

    private bool DefineSlot(int slot, RegisterFastValue value)
    {
        if ((uint)slot >= (uint)_locals.Length)
        {
            return false;
        }

        var hadValue = _assignedSlots[slot];
        var previous = _locals[slot];
        _changes.Add(new LocalChange(slot, hadValue, previous));
        _locals[slot] = value;
        _assignedSlots[slot] = true;
        return true;
    }

    private RegisterFastValue Resolve(string name)
        => _plan.TryGetSlot(name, out var slot) && _assignedSlots[slot]
            ? _locals[slot]
            : RegisterFastValue.Nothing;

    private RegisterFastValue ResolveSlot(int slot)
        => (uint)slot < (uint)_locals.Length && _assignedSlots[slot]
            ? _locals[slot]
            : RegisterFastValue.Nothing;

    private bool TryConsumeExecutionStep(string detail)
    {
        if (_context.RuntimeBudget.TryConsumeExecutionStep(detail))
        {
            return true;
        }

        _halted = true;
        return false;
    }

    private bool TryConsumeExecutionSteps(int count, string detail)
    {
        if (_context.RuntimeBudget.TryConsumeExecutionSteps(count, detail))
        {
            return true;
        }

        _halted = true;
        return false;
    }

    private void RecordParameterBound(string parameter, EventScriptValue value)
    {
        if (!_diagnosticsEnabled)
        {
            return;
        }

        RecordDiagnostic(
            EventScriptDiagnosticEventKind.ParameterBound,
            parameter,
            CreateSingleArgument(parameter, value),
            $"Bound '{parameter}'");
    }

    private void RecordLetEvaluated(string identifier, RegisterFastValue value)
    {
        if (!_diagnosticsEnabled)
        {
            return;
        }

        RecordDiagnostic(
            EventScriptDiagnosticEventKind.LetEvaluated,
            identifier,
            CreateSingleArgument(identifier, value.ToEventScriptValue()),
            $"Let '{identifier}' evaluated");
    }

    private void RecordHandlerInvoked(string message, IReadOnlyDictionary<string, EventScriptValue> args)
    {
        if (!_diagnosticsEnabled)
        {
            return;
        }

        RecordDiagnostic(
            EventScriptDiagnosticEventKind.HandlerInvoked,
            message,
            args,
            $"Handler '{message}' invoked");
    }

    private void RecordRuleCalled(RegisterFastInstruction instruction, RegisterFastValue input)
    {
        if (!_diagnosticsEnabled)
        {
            return;
        }

        var ruleName = instruction.DiagnosticName ?? string.Empty;
        var argumentName = instruction.DiagnosticArgumentName ?? "value";
        RecordDiagnostic(
            EventScriptDiagnosticEventKind.RuleCalled,
            ruleName,
            CreateSingleArgument(argumentName, input.ToEventScriptValue()),
            $"rule '{ruleName}' called");
    }

    private void RecordCallableCalled(RegisterFastInstruction instruction, RegisterFastValue[] stack, int start, int count)
    {
        if (!_diagnosticsEnabled)
        {
            return;
        }

        var callableName = instruction.DiagnosticName ?? string.Empty;
        var parameters = instruction.Names ?? [];
        var pairs = new KeyValuePair<string, EventScriptValue>[Math.Min(parameters.Length, count)];
        for (var argumentIndex = 0; argumentIndex < pairs.Length; argumentIndex++)
        {
            pairs[argumentIndex] = new KeyValuePair<string, EventScriptValue>(
                parameters[argumentIndex],
                stack[start + argumentIndex].ToEventScriptValue());
        }

        var kind = instruction.CallableKind == RegisterFastCallableKind.Rule
            ? EventScriptDiagnosticEventKind.RuleCalled
            : EventScriptDiagnosticEventKind.SelectCalled;
        var kindText = instruction.CallableKind == RegisterFastCallableKind.Rule ? "rule" : "select";
        RecordDiagnostic(
            kind,
            callableName,
            EventScriptNamedArguments.CreateOrdered(pairs),
            $"{kindText} '{callableName}' called");
    }

    private void RecordLetExpressionEvaluatedToNothing(string identifier, RegisterFastValue value)
    {
        if (!_diagnosticsEnabled || value.Kind != RegisterFastValueKind.Nothing)
        {
            return;
        }

        RecordDiagnostic(
            EventScriptDiagnosticEventKind.ExpressionEvaluatedToNothing,
            identifier,
            CreateSingleArgument(identifier, EventScriptValue.Nothing),
            $"Let '{identifier}' expression evaluated to Nothing.");
    }

    private void RecordPublishArgumentEvaluatedToNothing(string argumentName, RegisterFastValue value)
    {
        if (!_diagnosticsEnabled || value.Kind != RegisterFastValueKind.Nothing)
        {
            return;
        }

        RecordDiagnostic(
            EventScriptDiagnosticEventKind.ExpressionEvaluatedToNothing,
            argumentName,
            CreateSingleArgument(argumentName, EventScriptValue.Nothing),
            $"Publish argument '{argumentName}' evaluated to Nothing.");
    }

    private void RecordExpressionStatementEvaluatedToNothing(ExpressionNode expression, RegisterFastValue value)
    {
        if (!_diagnosticsEnabled || value.Kind != RegisterFastValueKind.Nothing)
        {
            return;
        }

        var name = expression.GetType().Name;
        RecordDiagnostic(
            EventScriptDiagnosticEventKind.ExpressionEvaluatedToNothing,
            name,
            CreateSingleArgument(name, EventScriptValue.Nothing),
            "Expression statement evaluated to Nothing.");
    }

    private void RecordDiagnostic(
        EventScriptDiagnosticEventKind kind,
        string name,
        IReadOnlyDictionary<string, EventScriptValue> arguments,
        string? detail = null)
        => EventScriptInvocationKernel.RecordDiagnostic(_context, _diagnosticsEnabled, kind, name, arguments, detail);

    private static EventScriptNamedArguments CreateSingleArgument(string name, EventScriptValue value)
        => EventScriptNamedArguments.CreateOrdered(
        [
            new KeyValuePair<string, EventScriptValue>(name, value)
        ]);

    private static bool Fail(out RegisterFastValue value)
    {
        value = RegisterFastValue.Nothing;
        return false;
    }

    private static long ToLongSaturated(decimal value)
    {
        if (value > long.MaxValue) return long.MaxValue;
        if (value < long.MinValue) return long.MinValue;
        return (long)value;
    }

    private readonly record struct LocalChange(int Slot, bool HadValue, RegisterFastValue PreviousValue);
}

internal enum RegisterFastValueKind
{
    Nothing,
    Boolean,
    Integer,
    Decimal,
    Percentage,
    Reference,
    Unsupported
}

internal readonly record struct RegisterFastValue(
    RegisterFastValueKind Kind,
    decimal Number,
    long IntegerValue,
    bool BooleanValue,
    EventScriptDecimalUnit? Unit,
    EventScriptValue? ReferenceValue)
{
    public static RegisterFastValue Nothing { get; } = new(RegisterFastValueKind.Nothing, 0m, 0, false, null, null);

    public static RegisterFastValue Boolean(bool value)
        => new(RegisterFastValueKind.Boolean, value ? 1m : 0m, value ? 1 : 0, value, null, null);

    public static RegisterFastValue Integer(long value)
        => new(RegisterFastValueKind.Integer, value, value, value != 0, null, null);

    public static RegisterFastValue Decimal(decimal value, EventScriptDecimalUnit? unit = null)
        => new(RegisterFastValueKind.Decimal, value, ToLongSaturated(value), value != 0m, unit, null);

    public static RegisterFastValue Percentage(decimal ratio)
        => new(RegisterFastValueKind.Percentage, ratio, ToLongSaturated(ratio * 100m), ratio != 0m, null, null);

    public static RegisterFastValue Reference(EventScriptValue value)
        => new(RegisterFastValueKind.Reference, 0m, 0, value.AsBoolean(), null, value);

    public static RegisterFastValue Unsupported()
        => new(RegisterFastValueKind.Unsupported, 0m, 0, false, null, null);

    public static RegisterFastValue NaN()
        => Reference(DecimalNaN());

    public static RegisterFastValue FromEventScriptValue(EventScriptValue value)
        => value switch
        {
            EventScriptBooleanValue boolean => Boolean(boolean.Value),
            EventScriptIntegerValue integer => Integer(integer.Value),
            EventScriptDecimalValue decimalValue when decimalValue.HasSemanticValue() => Decimal(decimalValue.Value, decimalValue.Unit),
            EventScriptPercentageValue percentage => Percentage(percentage.Ratio),
            _ => Reference(value)
        };

    public bool AsBoolean()
        => Kind switch
        {
            RegisterFastValueKind.Boolean => BooleanValue,
            RegisterFastValueKind.Integer => IntegerValue != 0,
            RegisterFastValueKind.Decimal => Number != 0m,
            RegisterFastValueKind.Percentage => Number != 0m,
            RegisterFastValueKind.Reference => ReferenceValue?.AsBoolean() ?? false,
            _ => false
        };

    public bool TryGetFiniteNumber(out decimal value)
    {
        if (TryGetPrimitiveFiniteNumber(out value) ||
            TryGetNumeric(out var number, out _, out _) && number.IsFinite && SetNumber(number.Value, out value))
        {
            return true;
        }

        value = 0m;
        return false;
    }

    public EventScriptValue ToEventScriptValue()
        => Kind switch
        {
            RegisterFastValueKind.Nothing => EventScriptValue.Nothing,
            RegisterFastValueKind.Boolean => EventScriptValueFactory.Boolean(BooleanValue),
            RegisterFastValueKind.Integer => EventScriptValueFactory.Integer(IntegerValue),
            RegisterFastValueKind.Decimal => EventScriptValueFactory.Decimal(Number, Unit),
            RegisterFastValueKind.Percentage => EventScriptValueFactory.Percentage(Number),
            RegisterFastValueKind.Reference => ReferenceValue ?? EventScriptValue.Nothing,
            _ => EventScriptValue.Nothing
        };

    public static bool AreEqual(RegisterFastValue left, RegisterFastValue right)
    {
        if (left.TryGetNumeric(out var leftNumber, out var leftUnit, out _) &&
            right.TryGetNumeric(out var rightNumber, out var rightUnit, out _))
        {
            if (leftUnit != rightUnit)
            {
                return false;
            }

            if (leftNumber.IsNaN || rightNumber.IsNaN)
            {
                return leftNumber.IsNaN && rightNumber.IsNaN;
            }

            if (leftNumber.IsInfinity || rightNumber.IsInfinity)
            {
                return leftNumber.Kind == rightNumber.Kind;
            }

            return leftNumber.Value == rightNumber.Value;
        }

        return left.ToEventScriptValue().Equals(right.ToEventScriptValue());
    }

    public static int CompareNumeric(RegisterFastValue left, RegisterFastValue right)
    {
        return TryCompareNumeric(left, right, out var comparison) ? comparison : 0;
    }

    public static bool TryCompareNumeric(RegisterFastValue left, RegisterFastValue right, out int comparison)
    {
        if (left.TryGetPrimitiveFiniteNumber(out var leftPrimitive) &&
            right.TryGetPrimitiveFiniteNumber(out var rightPrimitive))
        {
            if (left.Unit != right.Unit)
            {
                comparison = default;
                return false;
            }

            comparison = leftPrimitive.CompareTo(rightPrimitive);
            return true;
        }

        if (!left.TryGetNumeric(out var leftNumber, out var leftUnit, out _) ||
            !right.TryGetNumeric(out var rightNumber, out var rightUnit, out _) ||
            leftUnit != rightUnit)
        {
            comparison = default;
            return false;
        }

        return EventScriptValueAlu.TryCompareNumeric(leftNumber, rightNumber, out comparison);
    }

    public static RegisterFastValue Add(RegisterFastValue left, RegisterFastValue right)
    {
        if (left.IsPercentageLike() || right.IsPercentageLike())
        {
            return AddPercentage(left, right);
        }

        if (left.TryGetPrimitiveFiniteNumber(out var leftPrimitive) &&
            right.TryGetPrimitiveFiniteNumber(out var rightPrimitive))
        {
            if (left.Unit != right.Unit)
            {
                return NaN();
            }

            return EventScriptValueAlu.TryAddFinite(leftPrimitive, rightPrimitive, out var sum)
                ? Decimal(sum, left.Unit)
                : FromDecimalNumeric(
                    EventScriptValueAlu.AddNumeric(
                        EventScriptValueAlu.NumericValue.Finite(leftPrimitive),
                        EventScriptValueAlu.NumericValue.Finite(rightPrimitive)),
                    left.Unit);
        }

        if (!left.TryGetNumeric(out var leftNumber, out var leftUnit, out _) ||
            !right.TryGetNumeric(out var rightNumber, out var rightUnit, out _))
        {
            var leftValue = left.ToEventScriptValue();
            var rightValue = right.ToEventScriptValue();
            if (EventScriptValueAlu.TryCombineWithPlus(leftValue, rightValue, out var combined))
            {
                return FromEventScriptValue(combined);
            }

            return leftValue.IsText() || rightValue.IsText()
                ? Reference(Text($"{EventScriptValueAlu.ToText(leftValue)}{EventScriptValueAlu.ToText(rightValue)}"))
                : NaN();
        }

        if (leftUnit != rightUnit)
        {
            return NaN();
        }

        return FromDecimalNumeric(EventScriptValueAlu.AddNumeric(leftNumber, rightNumber), leftUnit);
    }

    public static RegisterFastValue Subtract(RegisterFastValue left, RegisterFastValue right)
    {
        if (left.IsPercentageLike() || right.IsPercentageLike())
        {
            return SubtractPercentage(left, right);
        }

        if (left.TryGetPrimitiveFiniteNumber(out var leftPrimitive) &&
            right.TryGetPrimitiveFiniteNumber(out var rightPrimitive))
        {
            if (left.Unit != right.Unit)
            {
                return NaN();
            }

            return EventScriptValueAlu.TryNegateFinite(rightPrimitive, out var negatedRight) &&
                   EventScriptValueAlu.TryAddFinite(leftPrimitive, negatedRight, out var difference)
                ? Decimal(difference, left.Unit)
                : FromDecimalNumeric(
                    EventScriptValueAlu.SubtractNumeric(
                        EventScriptValueAlu.NumericValue.Finite(leftPrimitive),
                        EventScriptValueAlu.NumericValue.Finite(rightPrimitive)),
                    left.Unit);
        }

        if (!left.TryGetNumeric(out var leftNumber, out var leftUnit, out _) ||
            !right.TryGetNumeric(out var rightNumber, out var rightUnit, out _))
        {
            return NaN();
        }

        if (leftUnit != rightUnit)
        {
            return NaN();
        }

        return FromDecimalNumeric(EventScriptValueAlu.SubtractNumeric(leftNumber, rightNumber), leftUnit);
    }

    public static RegisterFastValue Multiply(RegisterFastValue left, RegisterFastValue right)
    {
        if (left.IsPercentageLike() || right.IsPercentageLike())
        {
            return MultiplyPercentage(left, right);
        }

        if (left.TryGetPrimitiveFiniteNumber(out var leftPrimitive) &&
            right.TryGetPrimitiveFiniteNumber(out var rightPrimitive))
        {
            if (left.Unit.HasValue && right.Unit.HasValue)
            {
                return NaN();
            }

            return EventScriptValueAlu.TryMultiplyFinite(leftPrimitive, rightPrimitive, out var product)
                ? Decimal(product, left.Unit ?? right.Unit)
                : FromDecimalNumeric(
                    EventScriptValueAlu.MultiplyNumeric(
                        EventScriptValueAlu.NumericValue.Finite(leftPrimitive),
                        EventScriptValueAlu.NumericValue.Finite(rightPrimitive)),
                    left.Unit ?? right.Unit);
        }

        if (!left.TryGetNumeric(out var leftNumber, out var leftUnit, out _) ||
            !right.TryGetNumeric(out var rightNumber, out var rightUnit, out _))
        {
            return NaN();
        }

        if (leftUnit.HasValue && rightUnit.HasValue)
        {
            return NaN();
        }

        return FromDecimalNumeric(EventScriptValueAlu.MultiplyNumeric(leftNumber, rightNumber), leftUnit ?? rightUnit);
    }

    public static RegisterFastValue Divide(RegisterFastValue left, RegisterFastValue right)
    {
        if (left.IsPercentageLike() || right.IsPercentageLike())
        {
            return DividePercentage(left, right);
        }

        if (left.TryGetPrimitiveFiniteNumber(out var leftPrimitive) &&
            right.TryGetPrimitiveFiniteNumber(out var rightPrimitive))
        {
            if (!TryGetDivideResultUnit(left.Unit, right.Unit, out var primitiveResultUnit))
            {
                return NaN();
            }

            return rightPrimitive != 0m && EventScriptValueAlu.TryDivideFinite(leftPrimitive, rightPrimitive, out var quotient)
                ? Decimal(quotient, primitiveResultUnit)
                : FromDecimalNumeric(
                    EventScriptValueAlu.DivideNumeric(
                        EventScriptValueAlu.NumericValue.Finite(leftPrimitive),
                        EventScriptValueAlu.NumericValue.Finite(rightPrimitive)),
                    primitiveResultUnit);
        }

        if (!left.TryGetNumeric(out var leftNumber, out var leftUnit, out _) ||
            !right.TryGetNumeric(out var rightNumber, out var rightUnit, out _))
        {
            return NaN();
        }

        if (!TryGetDivideResultUnit(leftUnit, rightUnit, out var resultUnit))
        {
            return NaN();
        }

        return FromDecimalNumeric(EventScriptValueAlu.DivideNumeric(leftNumber, rightNumber), resultUnit);
    }

    public static RegisterFastValue Modulo(RegisterFastValue left, RegisterFastValue right)
    {
        if (left.TryGetPrimitiveFiniteNumber(out var leftPrimitive) &&
            right.TryGetPrimitiveFiniteNumber(out var rightPrimitive))
        {
            if (left.Unit.HasValue != right.Unit.HasValue ||
                left.Unit.HasValue && right.Unit.HasValue && left.Unit != right.Unit)
            {
                return NaN();
            }

            return rightPrimitive != 0m && EventScriptValueAlu.TryModuloFinite(leftPrimitive, rightPrimitive, out var modulo)
                ? Decimal(modulo, left.Unit)
                : FromDecimalNumeric(
                    EventScriptValueAlu.ModuloNumeric(
                        EventScriptValueAlu.NumericValue.Finite(leftPrimitive),
                        EventScriptValueAlu.NumericValue.Finite(rightPrimitive)),
                    left.Unit);
        }

        if (!left.TryGetNumeric(out var leftNumber, out var leftUnit, out _) ||
            !right.TryGetNumeric(out var rightNumber, out var rightUnit, out _))
        {
            return NaN();
        }

        if (leftUnit.HasValue != rightUnit.HasValue ||
            leftUnit.HasValue && rightUnit.HasValue && leftUnit != rightUnit)
        {
            return NaN();
        }

        return FromDecimalNumeric(EventScriptValueAlu.ModuloNumeric(leftNumber, rightNumber), leftUnit);
    }

    private static bool TryGetDivideResultUnit(
        EventScriptDecimalUnit? leftUnit,
        EventScriptDecimalUnit? rightUnit,
        out EventScriptDecimalUnit? resultUnit)
    {
        if (!leftUnit.HasValue && !rightUnit.HasValue)
        {
            resultUnit = null;
            return true;
        }

        if (leftUnit.HasValue && !rightUnit.HasValue)
        {
            resultUnit = leftUnit;
            return true;
        }

        if (leftUnit.HasValue && rightUnit.HasValue && leftUnit == rightUnit)
        {
            resultUnit = null;
            return true;
        }

        resultUnit = null;
        return false;
    }

    private static RegisterFastValue AddPercentage(RegisterFastValue left, RegisterFastValue right)
    {
        if (!left.TryGetNumeric(out var leftNumber, out var leftUnit, out var leftIsPercentage) ||
            !right.TryGetNumeric(out var rightNumber, out _, out var rightIsPercentage))
        {
            return NaN();
        }

        if (leftIsPercentage && rightIsPercentage)
        {
            return FromPercentageNumeric(EventScriptValueAlu.AddNumeric(leftNumber, rightNumber));
        }

        if (leftIsPercentage)
        {
            return NaN();
        }

        var delta = EventScriptValueAlu.MultiplyNumeric(leftNumber, rightNumber);
        return FromDecimalNumeric(EventScriptValueAlu.AddNumeric(leftNumber, delta), leftUnit);
    }

    private static RegisterFastValue SubtractPercentage(RegisterFastValue left, RegisterFastValue right)
    {
        if (!left.TryGetNumeric(out var leftNumber, out var leftUnit, out var leftIsPercentage) ||
            !right.TryGetNumeric(out var rightNumber, out _, out var rightIsPercentage))
        {
            return NaN();
        }

        if (leftIsPercentage && rightIsPercentage)
        {
            return FromPercentageNumeric(EventScriptValueAlu.SubtractNumeric(leftNumber, rightNumber));
        }

        if (leftIsPercentage)
        {
            return NaN();
        }

        var delta = EventScriptValueAlu.MultiplyNumeric(leftNumber, rightNumber);
        return FromDecimalNumeric(EventScriptValueAlu.SubtractNumeric(leftNumber, delta), leftUnit);
    }

    private static RegisterFastValue MultiplyPercentage(RegisterFastValue left, RegisterFastValue right)
    {
        if (!left.TryGetNumeric(out var leftNumber, out var leftUnit, out var leftIsPercentage) ||
            !right.TryGetNumeric(out var rightNumber, out var rightUnit, out var rightIsPercentage))
        {
            return NaN();
        }

        var result = EventScriptValueAlu.MultiplyNumeric(leftNumber, rightNumber);
        if (leftIsPercentage && rightIsPercentage)
        {
            return FromPercentageNumeric(result);
        }

        if (leftIsPercentage)
        {
            return rightUnit is { } unit
                ? FromDecimalNumeric(result, unit)
                : FromPercentageNumeric(result);
        }

        return FromDecimalNumeric(result, leftUnit);
    }

    private static RegisterFastValue DividePercentage(RegisterFastValue left, RegisterFastValue right)
    {
        if (!left.TryGetNumeric(out var leftNumber, out var leftUnit, out var leftIsPercentage) ||
            !right.TryGetNumeric(out var rightNumber, out var rightUnit, out var rightIsPercentage))
        {
            return NaN();
        }

        if (leftIsPercentage && rightUnit.HasValue)
        {
            return NaN();
        }

        var result = EventScriptValueAlu.DivideNumeric(leftNumber, rightNumber);
        if (leftIsPercentage && rightIsPercentage)
        {
            return FromDecimalNumeric(result);
        }

        if (leftIsPercentage)
        {
            return FromPercentageNumeric(result);
        }

        return FromDecimalNumeric(result, leftUnit);
    }

    private bool TryGetNumeric(
        out EventScriptValueAlu.NumericValue number,
        out EventScriptDecimalUnit? unit,
        out bool isPercentage)
    {
        switch (Kind)
        {
            case RegisterFastValueKind.Boolean:
                number = EventScriptValueAlu.NumericValue.Finite(BooleanValue ? 1m : 0m);
                unit = null;
                isPercentage = false;
                return true;
            case RegisterFastValueKind.Integer:
                number = EventScriptValueAlu.NumericValue.Finite(IntegerValue);
                unit = null;
                isPercentage = false;
                return true;
            case RegisterFastValueKind.Decimal:
                number = EventScriptValueAlu.NumericValue.Finite(Number);
                unit = Unit;
                isPercentage = false;
                return true;
            case RegisterFastValueKind.Percentage:
                number = EventScriptValueAlu.NumericValue.Finite(Number);
                unit = null;
                isPercentage = true;
                return true;
            case RegisterFastValueKind.Reference when ReferenceValue is { } reference &&
                                                       EventScriptValueAlu.TryCoerceNumericForOperation(reference, out var referenceNumber):
                number = referenceNumber;
                unit = EventScriptValue.TryGetDecimalUnit(reference, out var referenceUnit) ? referenceUnit : null;
                isPercentage = reference.IsPercentage();
                return true;
            default:
                number = default;
                unit = null;
                isPercentage = false;
                return false;
        }
    }

    private bool TryGetPrimitiveFiniteNumber(out decimal number)
    {
        switch (Kind)
        {
            case RegisterFastValueKind.Boolean:
                number = BooleanValue ? 1m : 0m;
                return true;
            case RegisterFastValueKind.Integer:
                number = IntegerValue;
                return true;
            case RegisterFastValueKind.Decimal:
            case RegisterFastValueKind.Percentage:
                number = Number;
                return true;
            default:
                number = default;
                return false;
        }
    }

    private bool IsPercentageLike()
        => Kind == RegisterFastValueKind.Percentage ||
           ReferenceValue is { } reference && reference.IsPercentage();

    public bool IsNothingLike()
        => Kind == RegisterFastValueKind.Nothing ||
           ReferenceValue is { } reference && reference.IsNothing();

    private static RegisterFastValue FromDecimalNumeric(EventScriptValueAlu.NumericValue number, EventScriptDecimalUnit? unit = null)
        => number.IsFinite
            ? Decimal(number.Value, unit)
            : Reference(EventScriptValueAlu.ToEventScriptDecimal(number));

    private static RegisterFastValue FromPercentageNumeric(EventScriptValueAlu.NumericValue number)
        => number.IsFinite
            ? Percentage(number.Value)
            : FromDecimalNumeric(number);

    private static bool SetNumber(decimal input, out decimal output)
    {
        output = input;
        return true;
    }

    private static long ToLongSaturated(decimal value)
    {
        if (value > long.MaxValue) return long.MaxValue;
        if (value < long.MinValue) return long.MinValue;
        return (long)value;
    }
}
