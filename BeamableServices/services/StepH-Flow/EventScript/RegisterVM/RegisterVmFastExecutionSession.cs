#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
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
    private readonly RegisterCompiledEventScript _compiledScript;
    private readonly EventScriptContext _context;
    private readonly RegisterVmFastPathPlan _plan;
    private readonly RegisterFastValue[] _locals;
    private readonly bool[] _assignedSlots;
    private readonly RegisterFastValue[] _evaluationStack;
    private readonly bool _diagnosticsEnabled;
    private readonly List<LocalChange> _changes = [];
    private readonly List<int> _scopeMarks = [];

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
            if (!TryConsumeExecutionStep("Statement execution budget exhausted.") ||
                !TryExecuteStatement(statement))
            {
                return false;
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
                return _plan.TryGetPublishLayout(publish, out var layout) &&
                       TryPublish(layout);

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

            default:
                return false;
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
        }

        return true;
    }

    private bool TryExecuteLoopIteration(ForStatementNode forStatement, long item)
        => TryExecuteLoopIteration(forStatement, RegisterFastValue.Integer(item));

    private bool TryExecuteLoopIteration(ForStatementNode forStatement, RegisterFastValue item)
    {
        if (!_context.RuntimeBudget.TryConsumeLoopIteration("Loop iteration budget exhausted."))
        {
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
            case BinaryExpressionNode binary:
                return TryEvaluateBinary(binary, out value);
            case RulePredicateExpressionNode rulePredicate:
                return TryEvaluateRulePredicate(rulePredicate, out value);
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
        for (var instructionIndex = 0; instructionIndex < instructions.Length; instructionIndex++)
        {
            var instruction = instructions[instructionIndex];
            if (!TryConsumeExecutionStep("Expression evaluation budget exhausted."))
            {
                value = RegisterFastValue.Nothing;
                return true;
            }

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
                case RegisterFastOpCode.Multiply:
                case RegisterFastOpCode.Modulo:
                    var right = _evaluationStack[--top];
                    var left = _evaluationStack[--top];
                    _evaluationStack[top++] = EvaluateProgramBinary(instruction.OpCode, left, right);
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
                    RecordRuleCalled(instruction, input);
                    EnterScope();
                    try
                    {
                        if (!DefineSlot(instruction.A, input) ||
                            instruction.ExpressionProgram is null ||
                            !TryExecuteExpressionProgram(instruction.ExpressionProgram, top, out var predicateValue))
                        {
                            value = RegisterFastValue.Nothing;
                            return false;
                        }

                        _evaluationStack[top++] = predicateValue;
                    }
                    finally
                    {
                        ExitScope();
                    }

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
        => opCode switch
        {
            RegisterFastOpCode.Or => RegisterFastValue.Boolean(left.AsBoolean() || right.AsBoolean()),
            RegisterFastOpCode.Xor => RegisterFastValue.Boolean(left.AsBoolean() ^ right.AsBoolean()),
            RegisterFastOpCode.And => RegisterFastValue.Boolean(left.AsBoolean() && right.AsBoolean()),
            RegisterFastOpCode.Equal => RegisterFastValue.Boolean(RegisterFastValue.AreEqual(left, right)),
            RegisterFastOpCode.NotEqual => RegisterFastValue.Boolean(!RegisterFastValue.AreEqual(left, right)),
            RegisterFastOpCode.Less => RegisterFastValue.Boolean(RegisterFastValue.CompareNumeric(left, right) < 0),
            RegisterFastOpCode.Greater => RegisterFastValue.Boolean(RegisterFastValue.CompareNumeric(left, right) > 0),
            RegisterFastOpCode.LessOrEqual => RegisterFastValue.Boolean(RegisterFastValue.CompareNumeric(left, right) <= 0),
            RegisterFastOpCode.GreaterOrEqual => RegisterFastValue.Boolean(RegisterFastValue.CompareNumeric(left, right) >= 0),
            RegisterFastOpCode.Add => RegisterFastValue.Add(left, right),
            RegisterFastOpCode.Multiply => RegisterFastValue.Multiply(left, right),
            RegisterFastOpCode.Modulo => RegisterFastValue.Modulo(left, right),
            _ => RegisterFastValue.Unsupported()
        };

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

        value = binary.Operator switch
        {
            "|" => RegisterFastValue.Boolean(left.AsBoolean() || right.AsBoolean()),
            "^" => RegisterFastValue.Boolean(left.AsBoolean() ^ right.AsBoolean()),
            "&" => RegisterFastValue.Boolean(left.AsBoolean() && right.AsBoolean()),
            "=" or "==" => RegisterFastValue.Boolean(RegisterFastValue.AreEqual(left, right)),
            "<>" => RegisterFastValue.Boolean(!RegisterFastValue.AreEqual(left, right)),
            "<" => RegisterFastValue.Boolean(RegisterFastValue.CompareNumeric(left, right) < 0),
            ">" => RegisterFastValue.Boolean(RegisterFastValue.CompareNumeric(left, right) > 0),
            "<=" => RegisterFastValue.Boolean(RegisterFastValue.CompareNumeric(left, right) <= 0),
            ">=" => RegisterFastValue.Boolean(RegisterFastValue.CompareNumeric(left, right) >= 0),
            "+" => RegisterFastValue.Add(left, right),
            "*" => RegisterFastValue.Multiply(left, right),
            "mod" => RegisterFastValue.Modulo(left, right),
            _ => RegisterFastValue.Nothing
        };

        return value.Kind != RegisterFastValueKind.Unsupported;
    }

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

        EnterScope();
        try
        {
            if (!Define(callable.Parameters[0], input))
            {
                value = RegisterFastValue.Nothing;
                return false;
            }

            return TryEvaluate(callable.Expression, out value);
        }
        finally
        {
            ExitScope();
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
    {
        EnterScope();
        try
        {
            if (!DefineSlot(identifierSlot, item))
            {
                value = RegisterFastValue.Nothing;
                return false;
            }

            return TryExecuteExpressionProgram(expressionProgram, 0, out value);
        }
        finally
        {
            ExitScope();
        }
    }

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
        EnterScope();
        try
        {
            if (!Define(identifier, item))
            {
                value = RegisterFastValue.Nothing;
                return false;
            }

            return TryEvaluate(expression, out value);
        }
        finally
        {
            ExitScope();
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
        => _context.RuntimeBudget.TryConsumeExecutionStep(detail);

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
        value = Kind switch
        {
            RegisterFastValueKind.Boolean => BooleanValue ? 1m : 0m,
            RegisterFastValueKind.Integer => IntegerValue,
            RegisterFastValueKind.Decimal => Number,
            RegisterFastValueKind.Percentage => Number,
            RegisterFastValueKind.Reference => ReferenceValue is { } reference && reference.IsNumber() ? reference.AsNumber() : 0m,
            _ => 0m
        };

        return Kind is RegisterFastValueKind.Boolean or RegisterFastValueKind.Integer or RegisterFastValueKind.Decimal or RegisterFastValueKind.Percentage ||
               ReferenceValue is { } referenceValue && referenceValue.IsNumber() && referenceValue.HasSemanticValue();
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
        if (left.TryGetFiniteNumber(out var leftNumber) && right.TryGetFiniteNumber(out var rightNumber))
        {
            return left.Unit == right.Unit && leftNumber == rightNumber;
        }

        return left.ToEventScriptValue().Equals(right.ToEventScriptValue());
    }

    public static int CompareNumeric(RegisterFastValue left, RegisterFastValue right)
    {
        if (!left.TryGetFiniteNumber(out var leftNumber) || !right.TryGetFiniteNumber(out var rightNumber))
        {
            return 0;
        }

        return leftNumber.CompareTo(rightNumber);
    }

    public static RegisterFastValue Add(RegisterFastValue left, RegisterFastValue right)
    {
        if (left.Kind == RegisterFastValueKind.Percentage || right.Kind == RegisterFastValueKind.Percentage)
        {
            return AddPercentage(left, right);
        }

        if (!left.TryGetFiniteNumber(out var leftNumber) || !right.TryGetFiniteNumber(out var rightNumber))
        {
            return NaN();
        }

        if (left.Unit != right.Unit)
        {
            return NaN();
        }

        return Decimal(leftNumber + rightNumber, left.Unit);
    }

    public static RegisterFastValue Multiply(RegisterFastValue left, RegisterFastValue right)
    {
        if (left.Kind == RegisterFastValueKind.Percentage || right.Kind == RegisterFastValueKind.Percentage)
        {
            return MultiplyPercentage(left, right);
        }

        if (!left.TryGetFiniteNumber(out var leftNumber) || !right.TryGetFiniteNumber(out var rightNumber))
        {
            return NaN();
        }

        return Decimal(leftNumber * rightNumber, left.Unit ?? right.Unit);
    }

    public static RegisterFastValue Modulo(RegisterFastValue left, RegisterFastValue right)
    {
        if (!left.TryGetFiniteNumber(out var leftNumber) ||
            !right.TryGetFiniteNumber(out var rightNumber) ||
            rightNumber == 0m)
        {
            return NaN();
        }

        return Decimal(leftNumber % rightNumber, left.Unit);
    }

    private static RegisterFastValue AddPercentage(RegisterFastValue left, RegisterFastValue right)
    {
        if (!left.TryGetFiniteNumber(out var leftNumber) || !right.TryGetFiniteNumber(out var rightNumber))
        {
            return NaN();
        }

        if (left.Kind == RegisterFastValueKind.Percentage && right.Kind == RegisterFastValueKind.Percentage)
        {
            return Percentage(leftNumber + rightNumber);
        }

        if (left.Kind == RegisterFastValueKind.Percentage)
        {
            return NaN();
        }

        return Decimal(leftNumber + leftNumber * rightNumber, left.Unit);
    }

    private static RegisterFastValue MultiplyPercentage(RegisterFastValue left, RegisterFastValue right)
    {
        if (!left.TryGetFiniteNumber(out var leftNumber) || !right.TryGetFiniteNumber(out var rightNumber))
        {
            return NaN();
        }

        var result = leftNumber * rightNumber;
        if (left.Kind == RegisterFastValueKind.Percentage && right.Kind == RegisterFastValueKind.Percentage)
        {
            return Percentage(result);
        }

        if (left.Kind == RegisterFastValueKind.Percentage)
        {
            return right.Unit is { } unit ? Decimal(result, unit) : Percentage(result);
        }

        return Decimal(result, left.Unit);
    }

    private static long ToLongSaturated(decimal value)
    {
        if (value > long.MaxValue) return long.MaxValue;
        if (value < long.MinValue) return long.MinValue;
        return (long)value;
    }
}
