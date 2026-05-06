using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptValueFactory;

namespace StepH.GameEventScript.BytecodeVM;

internal sealed partial class GesBytecodeVmExecutionSession
{
    private const string CallableCallDepthExceededDetail = "Callable exceeded the configured call depth.";

    private readonly GesBytecodeVmExecutable _compiledScript;
    private readonly GameEventScriptContext _context;
    private readonly GesRuntimeBudget _runtimeBudget;
    private readonly GameEventScriptBytecodeExecutionPlan _plan;
    private readonly BytecodeVmValue[] _locals;
    private readonly bool[] _assignedSlots;
    private readonly BytecodeVmValue[] _evaluationStack;
    private readonly bool _diagnosticsEnabled;
    private readonly List<LocalChange> _changes = [];
    private readonly List<int> _scopeMarks = [];
    private readonly Stack<GameEventScriptRandomGenerator> _randomScopes = new();
    private bool _halted;

    private GesBytecodeVmExecutionSession(
        GesBytecodeVmExecutable compiledScript,
        GameEventScriptContext context,
        GameEventScriptBytecodeExecutionPlan plan,
        bool diagnosticsEnabled)
    {
        _compiledScript = compiledScript;
        _context = context;
        _runtimeBudget = context.RuntimeBudget;
        _plan = plan;
        _diagnosticsEnabled = diagnosticsEnabled;
        _locals = new BytecodeVmValue[plan.SlotCount];
        _assignedSlots = new bool[plan.SlotCount];
        _evaluationStack = new BytecodeVmValue[Math.Max(16, Math.Max(plan.MaxStackDepth, compiledScript.BytecodeModule.MaxStackDepth) + 16)];
        _randomScopes.Push(context.Random);
    }

    public static void InvokeHandler(
        GesBytecodeVmExecutable compiledScript,
        GameEventScriptContext context,
        GesBytecodeVmCompiledHandler handler,
        IReadOnlyDictionary<string, GameEventScriptValue> args)
    {
        var session = new GesBytecodeVmExecutionSession(compiledScript, context, handler.ExecutionPlan, handler.DiagnosticsEnabled);
        if (!session.TryInvoke(handler, args))
        {
            throw new InvalidOperationException(
                $"BytecodeVM invariant failed: handler '{handler.SignatureId}' #{handler.DeclarationOrder} could not be executed by its compiled execution plan.");
        }
    }

    private bool TryInvoke(GesBytecodeVmCompiledHandler handler, IReadOnlyDictionary<string, GameEventScriptValue> args)
    {
        EnterScope();
        try
        {
            var parameters = handler.Parameters;
            for (var parameterIndex = 0; parameterIndex < parameters.Count; parameterIndex++)
            {
                var parameter = parameters[parameterIndex];
                if (!TryGetArgumentValue(args, parameter, parameterIndex, out var value))
                {
                    return false;
                }

                var parameterValue = BytecodeVmValue.FromGameEventScriptValue(value);
                var hasParameterType = HasParameterType(handler.ParameterTypes, parameterIndex);
                if (!TryConvertParameterType(handler.ParameterTypes, parameterIndex, parameterValue, out parameterValue))
                {
                    return false;
                }

                if (_diagnosticsEnabled)
                {
                    RecordParameterBound(parameter, hasParameterType ? parameterValue.ToGameEventScriptValue() : value);
                }

                if (!Define(parameter, parameterValue))
                {
                    return false;
                }
            }

            RecordHandlerInvoked(handler.Message, args);

            return TryExecuteStatementProgram(handler.ExecutionPlan.StatementProgram);
        }
        finally
        {
            ExitScope();
        }
    }

    private static bool TryGetArgumentValue(IReadOnlyDictionary<string, GameEventScriptValue> args, string parameter, int parameterIndex, out GameEventScriptValue value)
    {
        if (args is GameEventScriptNamedArguments namedArguments && parameterIndex >= 0 && parameterIndex < namedArguments.Count)
        {
            value = namedArguments[parameterIndex];
            return true;
        }

        return args.TryGetValue(parameter, out value!);
    }

    private bool TryExecuteStatementProgram(GameEventScriptBytecodeStatementProgram program)
    {
        if (!program.CreatesScope)
        {
            return TryExecuteStatements(program.Statements);
        }

        EnterScope();
        try
        {
            return TryExecuteStatements(program.Statements);
        }
        finally
        {
            ExitScope();
        }
    }

    private bool TryExecuteStatements(IReadOnlyList<GameEventScriptBytecodeStatement> statements)
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

    private bool TryExecuteStatement(GameEventScriptBytecodeStatement statement)
    {
        switch (statement.Kind)
        {
            case GameEventScriptBytecodeStatementKind.Let:
                if (statement.ExpressionProgram is null ||
                    string.IsNullOrEmpty(statement.Name) ||
                    !TryExecuteExpressionProgram(statement.ExpressionProgram, 0, out var letValue))
                {
                    return false;
                }

                if (!string.IsNullOrEmpty(statement.DeclaredType) &&
                    !TryConvertDeclaredType(statement.DeclaredType!, letValue, out letValue))
                {
                    return false;
                }

                RecordLetEvaluated(statement.Name!, letValue);
                RecordLetExpressionEvaluatedToNothing(statement.Name!, letValue);
                return Define(statement.Name!, letValue);

            case GameEventScriptBytecodeStatementKind.Publish:
                return TryPublish(statement);

            case GameEventScriptBytecodeStatementKind.If:
                return TryExecuteIf(statement);

            case GameEventScriptBytecodeStatementKind.ForRange:
                return TryExecuteRangeFor(statement);

            case GameEventScriptBytecodeStatementKind.ForCollection:
                return TryExecuteCollectionFor(statement);

            case GameEventScriptBytecodeStatementKind.Expression:
                if (statement.ExpressionProgram is null ||
                    !TryExecuteExpressionProgram(statement.ExpressionProgram, 0, out var expressionValue))
                {
                    return false;
                }

                RecordExpressionStatementEvaluatedToNothing(statement.DiagnosticName, expressionValue);
                return true;

            case GameEventScriptBytecodeStatementKind.SeededRandom:
                return TryExecuteSeededRandomStatement(statement);

            default:
                return false;
        }
    }

    private bool TryExecuteSeededRandomStatement(GameEventScriptBytecodeStatement statement)
    {
        if (statement.ExpressionProgram is null ||
            statement.BodyProgram is null ||
            !TryExecuteExpressionProgram(statement.ExpressionProgram, 0, out var seed))
        {
            return false;
        }

        PushSeededRandomScope(seed.ToGameEventScriptValue());
        try
        {
            return TryExecuteStatementProgram(statement.BodyProgram);
        }
        finally
        {
            PopSeededRandomScope();
        }
    }

    private bool TryExecuteIf(GameEventScriptBytecodeStatement statement)
    {
        if (statement.ExpressionProgram is null ||
            statement.ThenProgram is null ||
            !TryExecuteExpressionProgram(statement.ExpressionProgram, 0, out var condition))
        {
            return false;
        }

        if (condition.AsBoolean())
        {
            return TryExecuteStatementProgram(statement.ThenProgram);
        }

        return statement.ElseProgram is null ||
               TryExecuteStatementProgram(statement.ElseProgram);
    }

    private bool TryExecuteRangeFor(GameEventScriptBytecodeStatement statement)
    {
        var source = statement.IterationSource;
        if (source is null ||
            source.RangeFromProgram is null ||
            source.RangeToProgram is null ||
            !TryExecuteExpressionProgram(source.RangeFromProgram, 0, out var fromValue) ||
            !TryExecuteExpressionProgram(source.RangeToProgram, 0, out var toValue) ||
            !fromValue.TryGetFiniteNumber(out var fromNumber) ||
            !toValue.TryGetFiniteNumber(out var toNumber))
        {
            return false;
        }

        var stepNumber = 1d;
        if (source.RangeStepProgram is not null &&
            (!TryExecuteExpressionProgram(source.RangeStepProgram, 0, out var stepValue) || !stepValue.TryGetFiniteNumber(out stepNumber)))
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

        var length = GesRuntimeLimitUtilities.GetRangeLength(from, to, step);
        if (!_runtimeBudget.TryCheckRangeLength(length, "For loop range would enumerate more range items than allowed."))
        {
            return true;
        }

        if (step > 0)
        {
            for (var item = from; item <= to; item += step)
            {
                if (!TryExecuteLoopIteration(statement, item))
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
                if (!TryExecuteLoopIteration(statement, item))
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

    private bool TryExecuteCollectionFor(GameEventScriptBytecodeStatement statement)
    {
        var source = statement.IterationSource;
        if (source?.CollectionProgram is null ||
            !TryExecuteExpressionProgram(source.CollectionProgram, 0, out var sourceVmValue))
        {
            return false;
        }

        var sourceValue = sourceVmValue.ToGameEventScriptValue();
        if (GesRuntimeLimitUtilities.TryGetRangeLength(sourceValue, out var length) &&
            !_runtimeBudget.TryCheckRangeLength(length, "Iteration source would enumerate more range items than allowed."))
        {
            return true;
        }

        foreach (var item in sourceValue.AsEnumerable())
        {
            if (!TryExecuteLoopIteration(statement, BytecodeVmValue.FromGameEventScriptValue(item)))
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

    private bool TryExecuteLoopIteration(GameEventScriptBytecodeStatement statement, long item)
        => TryExecuteLoopIteration(statement, BytecodeVmValue.Integer(item));

    private bool TryExecuteLoopIteration(GameEventScriptBytecodeStatement statement, BytecodeVmValue item)
    {
        if (!_runtimeBudget.TryConsumeLoopIteration("Loop iteration budget exhausted."))
        {
            _halted = true;
            return true;
        }

        EnterScope();
        try
        {
            return !string.IsNullOrEmpty(statement.Name) &&
                   statement.BodyProgram is not null &&
                   Define(statement.Name!, item) &&
                   TryExecuteStatementProgram(statement.BodyProgram);
        }
        finally
        {
            ExitScope();
        }
    }

    private bool TryPublish(GameEventScriptBytecodeStatement statement)
    {
        GameEventScriptMessage? message;
        if (statement.PublishLayout is not null)
        {
            if (!TryCreateMessage(statement.PublishLayout, out message))
            {
                return false;
            }
        }
        else if (statement.ExpressionProgram is not null)
        {
            if (!TryCreateMessage(statement.ExpressionProgram, out message))
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        if (message is null)
        {
            return true;
        }

        if (!TryEvaluateTags(statement.TagPrograms, out var tags))
        {
            return false;
        }

        PublishMessage(statement.PublishKind, tags.Count == 0 ? message : message.WithTags(tags));
        return true;
    }

    private bool TryCreateMessage(GameEventScriptBytecodePublishLayout layout, out GameEventScriptMessage message)
    {
        var argumentNames = layout.ArgumentNames;
        var argumentPrograms = layout.ArgumentPrograms;
        if (argumentNames.Length == 0)
        {
            message = GameEventScriptMessage.CreatePrecomputed(
                layout.MessageName,
                GameEventScriptNamedArguments.Empty,
                layout.SignatureId);
            return true;
        }

        var pairs = new KeyValuePair<string, GameEventScriptValue>[argumentNames.Length];
        for (var argumentIndex = 0; argumentIndex < argumentNames.Length; argumentIndex++)
        {
            if (!TryExecuteExpressionProgram(argumentPrograms[argumentIndex], 0, out var value))
            {
                message = GameEventScriptMessage.Empty;
                return false;
            }

            RecordPublishArgumentEvaluatedToNothing(argumentNames[argumentIndex], value);

            pairs[argumentIndex] = new KeyValuePair<string, GameEventScriptValue>(
                argumentNames[argumentIndex],
                value.ToGameEventScriptValue());
        }

        message = GameEventScriptMessage.CreatePrecomputed(
            layout.MessageName,
            GameEventScriptNamedArguments.CreateOrdered(pairs),
            layout.SignatureId);
        return true;
    }

    private bool TryCreateMessage(GameEventScriptBytecodeExpressionProgram messageExpression, out GameEventScriptMessage? message)
    {
        if (!TryExecuteExpressionProgram(messageExpression, 0, out var publishValue))
        {
            message = null;
            return false;
        }

        var boxed = publishValue.ToGameEventScriptValue();
        message = GesMessageValueCodec.TryReadMessageValue(boxed, out var boxedMessage)
            ? boxedMessage
            : null;

        return true;
    }

    private bool TryEvaluateTags(IReadOnlyList<GameEventScriptBytecodeExpressionProgram> tagPrograms, out IReadOnlyList<string> tags)
    {
        if (tagPrograms.Count == 0)
        {
            tags = [];
            return true;
        }

        var builder = new MessageTagBuilder();
        foreach (var tagProgram in tagPrograms)
        {
            if (!TryExecuteExpressionProgram(tagProgram, 0, out var tagValue))
            {
                tags = [];
                return false;
            }

            AddTags(builder, tagValue.ToGameEventScriptValue());
        }

        tags = builder.ToArray();
        return true;
    }

    internal void PublishMessage(GameEventScriptBytecodePublishKind publishKind, GameEventScriptMessage message)
    {
        if (publishKind == GameEventScriptBytecodePublishKind.Publish)
        {
            _context.Publish(message);
        }
        else
        {
            _context.Emit(message);
        }
    }

    internal static void AddTags(MessageTagBuilder builder, GameEventScriptValue value)
    {
        if (value.IsNothing())
        {
            return;
        }

        if (value.IsList() || value.IsSet() || value.IsSequence())
        {
            foreach (var item in value.AsEnumerable())
            {
                AddTags(builder, item);
            }

            return;
        }

        builder.Add(value.AsText());
    }

    private bool TryExecuteExpressionProgram(GameEventScriptBytecodeExpressionProgram program, int stackBase, out BytecodeVmValue value)
    {
        if (stackBase + program.MaxStackDepth > _evaluationStack.Length)
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        var top = stackBase;
        var instructions = program.Instructions;
        var isStepping = _runtimeBudget.IsStepping;
        if (!isStepping && !TryConsumeExecutionSteps(instructions.Length, "Expression evaluation budget exhausted."))
        {
            value = BytecodeVmValue.Nothing;
            return true;
        }

        for (var instructionIndex = 0; instructionIndex < instructions.Length; instructionIndex++)
        {
            if (isStepping && !TryConsumeExecutionStep("Expression evaluation budget exhausted."))
            {
                value = BytecodeVmValue.Nothing;
                return true;
            }

            ref readonly var instruction = ref instructions[instructionIndex];
            switch (instruction.OpCode)
            {
                case GameEventScriptBytecodeOpCode.LoadConstant:
                    _evaluationStack[top++] = LoadConstant(instruction.ConstantIndex);
                    break;

                case GameEventScriptBytecodeOpCode.LoadSlot:
                    _evaluationStack[top++] = ResolveSlot(instruction.A);
                    break;

                case GameEventScriptBytecodeOpCode.Or:
                case GameEventScriptBytecodeOpCode.Xor:
                case GameEventScriptBytecodeOpCode.And:
                case GameEventScriptBytecodeOpCode.Power:
                case GameEventScriptBytecodeOpCode.Equal:
                case GameEventScriptBytecodeOpCode.NotEqual:
                case GameEventScriptBytecodeOpCode.ApproxEqual:
                case GameEventScriptBytecodeOpCode.Less:
                case GameEventScriptBytecodeOpCode.Greater:
                case GameEventScriptBytecodeOpCode.LessOrEqual:
                case GameEventScriptBytecodeOpCode.GreaterOrEqual:
                case GameEventScriptBytecodeOpCode.Add:
                case GameEventScriptBytecodeOpCode.Subtract:
                case GameEventScriptBytecodeOpCode.Multiply:
                case GameEventScriptBytecodeOpCode.Divide:
                case GameEventScriptBytecodeOpCode.IntegerDivide:
                case GameEventScriptBytecodeOpCode.Modulo:
                case GameEventScriptBytecodeOpCode.Remainder:
                case GameEventScriptBytecodeOpCode.PrimitiveIntegerEqual:
                case GameEventScriptBytecodeOpCode.PrimitiveIntegerNotEqual:
                case GameEventScriptBytecodeOpCode.PrimitiveIntegerLess:
                case GameEventScriptBytecodeOpCode.PrimitiveIntegerGreater:
                case GameEventScriptBytecodeOpCode.PrimitiveIntegerLessOrEqual:
                case GameEventScriptBytecodeOpCode.PrimitiveIntegerGreaterOrEqual:
                case GameEventScriptBytecodeOpCode.PrimitiveIntegerAdd:
                case GameEventScriptBytecodeOpCode.PrimitiveIntegerSubtract:
                case GameEventScriptBytecodeOpCode.PrimitiveIntegerMultiply:
                case GameEventScriptBytecodeOpCode.PrimitiveIntegerDivide:
                case GameEventScriptBytecodeOpCode.PrimitiveIntegerFloorDivide:
                case GameEventScriptBytecodeOpCode.PrimitiveIntegerModulo:
                case GameEventScriptBytecodeOpCode.PrimitiveIntegerRemainder:
                case GameEventScriptBytecodeOpCode.Default:
                case GameEventScriptBytecodeOpCode.Contains:
                case GameEventScriptBytecodeOpCode.ContainsValue:
                case GameEventScriptBytecodeOpCode.StartsWith:
                case GameEventScriptBytecodeOpCode.EndsWith:
                case GameEventScriptBytecodeOpCode.Intersect:
                case GameEventScriptBytecodeOpCode.Combine:
                case GameEventScriptBytecodeOpCode.Except:
                case GameEventScriptBytecodeOpCode.Zip:
                    var right = _evaluationStack[--top];
                    var left = _evaluationStack[--top];
                    _evaluationStack[top++] = EvaluateProgramBinary(instruction.OpCode, left, right);
                    break;

                case GameEventScriptBytecodeOpCode.Unary:
                    if (!TryEvaluateUnaryOperation(instruction.DiagnosticName, _evaluationStack[top - 1], out var unaryValue))
                    {
                        value = BytecodeVmValue.Nothing;
                        return false;
                    }

                    _evaluationStack[top - 1] = unaryValue;
                    break;

                case GameEventScriptBytecodeOpCode.Variadic:
                    top -= instruction.A;
                    if (!TryEvaluateVariadicOperation(instruction.DiagnosticName, _evaluationStack, top, instruction.A, out var variadicValue))
                    {
                        value = BytecodeVmValue.Nothing;
                        return false;
                    }

                    _evaluationStack[top++] = variadicValue;
                    break;

                case GameEventScriptBytecodeOpCode.Clamp:
                    top -= 3;
                    _evaluationStack[top] = EvaluateClamp(
                        _evaluationStack[top],
                        _evaluationStack[top + 1],
                        _evaluationStack[top + 2]);
                    top++;
                    break;

                case GameEventScriptBytecodeOpCode.Random:
                    var to = _evaluationStack[--top];
                    var from = _evaluationStack[--top];
                    _evaluationStack[top++] = EvaluateRandomExpression(from, to);
                    break;

                case GameEventScriptBytecodeOpCode.Range:
                    top -= instruction.A;
                    _evaluationStack[top] = EvaluateRangeExpression(
                        _evaluationStack[top],
                        _evaluationStack[top + 1],
                        instruction.A == 3 ? _evaluationStack[top + 2] : BytecodeVmValue.Integer(1));
                    top++;
                    break;

                case GameEventScriptBytecodeOpCode.Dice:
                    _evaluationStack[top++] = EvaluateDiceExpression(instruction.A, instruction.B);
                    break;

                case GameEventScriptBytecodeOpCode.SeededRandom:
                    var seed = _evaluationStack[--top];
                    if (instruction.ExpressionProgram is null ||
                        !TryEvaluateSeededRandomExpression(seed, instruction.ExpressionProgram, top, out var seededValue))
                    {
                        value = BytecodeVmValue.Nothing;
                        return false;
                    }

                    _evaluationStack[top++] = seededValue;
                    break;

                case GameEventScriptBytecodeOpCode.Cast:
                    _evaluationStack[top - 1] = EvaluateProgramCast(instruction.CastKind, _evaluationStack[top - 1]);
                    break;

                case GameEventScriptBytecodeOpCode.TypeConstructor:
                    top -= instruction.A;
                    _evaluationStack[top] = EvaluateTypeConstructor(
                        instruction.DiagnosticName,
                        instruction.Names,
                        _evaluationStack,
                        top,
                        instruction.A);
                    top++;
                    break;

                case GameEventScriptBytecodeOpCode.TypeCheck:
                    _evaluationStack[top - 1] = BytecodeVmValue.Boolean(IsValueOfType(
                        _evaluationStack[top - 1],
                        instruction.DiagnosticName));
                    break;

                case GameEventScriptBytecodeOpCode.RulePredicate:
                    var input = _evaluationStack[--top];
                    if (!TryEvaluateRulePredicate(instruction, input, top, out var predicateValue))
                    {
                        value = BytecodeVmValue.Nothing;
                        return false;
                    }

                    _evaluationStack[top++] = predicateValue;
                    break;

                case GameEventScriptBytecodeOpCode.Call:
                    top -= instruction.A;
                    if (!TryEvaluateCallable(instruction, _evaluationStack, top, instruction.A, out var callValue))
                    {
                        value = BytecodeVmValue.Nothing;
                        return false;
                    }

                    _evaluationStack[top++] = callValue;
                    break;

                case GameEventScriptBytecodeOpCode.MemberAccess:
                    _evaluationStack[top - 1] = EvaluateMemberAccess(_evaluationStack[top - 1], instruction.DiagnosticName);
                    break;

                case GameEventScriptBytecodeOpCode.IndexedAccess:
                    var selector = _evaluationStack[--top];
                    var target = _evaluationStack[--top];
                    _evaluationStack[top++] = EvaluateIndexedAccess(target, selector);
                    break;

                case GameEventScriptBytecodeOpCode.BuildList:
                    top -= instruction.A;
                    _evaluationStack[top] = BuildListValue(_evaluationStack, top, instruction.A);
                    top++;
                    break;

                case GameEventScriptBytecodeOpCode.BuildSequence:
                    top -= instruction.A;
                    _evaluationStack[top] = BuildSequenceValue(_evaluationStack, top, instruction.A);
                    top++;
                    break;

                case GameEventScriptBytecodeOpCode.BuildSet:
                    top -= instruction.A;
                    _evaluationStack[top] = BuildSetValue(_evaluationStack, top, instruction.A);
                    top++;
                    break;

                case GameEventScriptBytecodeOpCode.BuildDictionary:
                    top -= instruction.A;
                    _evaluationStack[top] = BuildDictionaryValue(_evaluationStack, top, instruction.A, instruction.Names);
                    top++;
                    break;

                case GameEventScriptBytecodeOpCode.BuildMessage:
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

                case GameEventScriptBytecodeOpCode.BindHandler:
                    top -= instruction.A + 1;
                    _evaluationStack[top] = BindHandlerValue(
                        _evaluationStack[top],
                        _evaluationStack,
                        top + 1,
                        instruction.A,
                        instruction.Names);
                    top++;
                    break;

                case GameEventScriptBytecodeOpCode.CallExtension:
                    top -= instruction.A;
                    if (!TryCallExtension(
                            instruction.DiagnosticName,
                            instruction.DiagnosticArgumentName,
                            instruction.Names,
                            instruction.B,
                            _evaluationStack,
                            top,
                            instruction.A,
                            out var extensionValue))
                    {
                        value = BytecodeVmValue.Nothing;
                        return false;
                    }

                    _evaluationStack[top++] = extensionValue;
                    break;

                case GameEventScriptBytecodeOpCode.Pipeline:
                    if (instruction.PipelineProgram is null ||
                        !TryExecutePipelineProgram(instruction.PipelineProgram, out var pipelineValue))
                    {
                        value = BytecodeVmValue.Nothing;
                        return false;
                    }

                    _evaluationStack[top++] = pipelineValue;
                    break;

                case GameEventScriptBytecodeOpCode.GeneratedCollection:
                    if (instruction.GeneratedCollectionProgram is null ||
                        !TryExecuteGeneratedCollectionProgram(instruction.GeneratedCollectionProgram, out var generatedValue))
                    {
                        value = BytecodeVmValue.Nothing;
                        return false;
                    }

                    _evaluationStack[top++] = generatedValue;
                    break;

                case GameEventScriptBytecodeOpCode.GuardedChoice:
                    if (instruction.GuardedChoiceProgram is null ||
                        !TryExecuteGuardedChoiceProgram(instruction.GuardedChoiceProgram, out var guardedValue))
                    {
                        value = BytecodeVmValue.Nothing;
                        return false;
                    }

                    _evaluationStack[top++] = guardedValue;
                    break;

                default:
                    value = BytecodeVmValue.Nothing;
                    return false;
            }
        }

        value = top > stackBase ? _evaluationStack[top - 1] : BytecodeVmValue.Nothing;
        return true;
    }

    private BytecodeVmValue LoadConstant(int index)
        => (uint)index < (uint)_compiledScript.Constants.Count
            ? _compiledScript.Constants[index]
            : BytecodeVmValue.Nothing;

    private static BytecodeVmValue EvaluateMemberAccess(BytecodeVmValue target, string? member)
    {
        if (string.IsNullOrEmpty(member))
        {
            return BytecodeVmValue.Nothing;
        }

        var targetValue = target.ToGameEventScriptValue();
        if (targetValue.IsNothing())
        {
            return BytecodeVmValue.Nothing;
        }

        return targetValue.TryGetDictionaryMember(member, out var value)
            ? BytecodeVmValue.FromGameEventScriptValue(value)
            : BytecodeVmValue.Nothing;
    }

    private static BytecodeVmValue EvaluateIndexedAccess(BytecodeVmValue target, BytecodeVmValue selector)
    {
        var selectorValue = selector.ToGameEventScriptValue();
        if (selectorValue.IsNothing())
        {
            return BytecodeVmValue.Nothing;
        }

        return BytecodeVmValue.FromGameEventScriptValue(target.ToGameEventScriptValue().Lookup(selectorValue));
    }

    private static BytecodeVmValue BuildListValue(BytecodeVmValue[] stack, int start, int count)
    {
        var items = new GameEventScriptValue[count];
        for (var itemIndex = 0; itemIndex < count; itemIndex++)
        {
            items[itemIndex] = stack[start + itemIndex].ToGameEventScriptValue();
        }

        return BytecodeVmValue.Reference(GameEventScriptValueFactory.GesList(items));
    }

    private static BytecodeVmValue BuildSequenceValue(BytecodeVmValue[] stack, int start, int count)
    {
        var items = new GameEventScriptValue[count];
        for (var itemIndex = 0; itemIndex < count; itemIndex++)
        {
            items[itemIndex] = stack[start + itemIndex].ToGameEventScriptValue();
        }

        return BytecodeVmValue.Reference(GameEventScriptValueFactory.GesSequence(items));
    }

    private static BytecodeVmValue BuildSetValue(BytecodeVmValue[] stack, int start, int count)
    {
        var items = new GameEventScriptValue[count];
        for (var itemIndex = 0; itemIndex < count; itemIndex++)
        {
            items[itemIndex] = stack[start + itemIndex].ToGameEventScriptValue();
        }

        return BytecodeVmValue.Reference(GameEventScriptValueFactory.GesSet(items));
    }

    private static BytecodeVmValue BuildDictionaryValue(BytecodeVmValue[] stack, int start, int count, string[]? names)
    {
        if (names is null || names.Length != count)
        {
            return BytecodeVmValue.Nothing;
        }

        var map = new Dictionary<string, GameEventScriptValue>(count, StringComparer.Ordinal);
        for (var entryIndex = 0; entryIndex < count; entryIndex++)
        {
            map[names[entryIndex]] = stack[start + entryIndex].ToGameEventScriptValue();
        }

        return BytecodeVmValue.Reference(GameEventScriptValueFactory.GesDictionary(map));
    }

    private static BytecodeVmValue BuildMessageValue(
        BytecodeVmValue[] stack,
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
            return BytecodeVmValue.Nothing;
        }

        if (count == 0)
        {
            return BytecodeVmValue.Reference(GesMessage(GameEventScriptMessage.CreatePrecomputed(
                messageName,
                GameEventScriptNamedArguments.Empty,
                signatureId)));
        }

        var pairs = new KeyValuePair<string, GameEventScriptValue>[count];
        for (var argumentIndex = 0; argumentIndex < count; argumentIndex++)
        {
            pairs[argumentIndex] = new KeyValuePair<string, GameEventScriptValue>(
                names[argumentIndex],
                stack[start + argumentIndex].ToGameEventScriptValue());
        }

        return BytecodeVmValue.Reference(GesMessage(GameEventScriptMessage.CreatePrecomputed(
            messageName,
            GameEventScriptNamedArguments.CreateOrdered(pairs),
            signatureId)));
    }

    private static BytecodeVmValue BindHandlerValue(
        BytecodeVmValue callee,
        BytecodeVmValue[] stack,
        int start,
        int count,
        string[]? names)
    {
        if (names is null || names.Length != count)
        {
            return BytecodeVmValue.Nothing;
        }

        var pairs = new KeyValuePair<string, GameEventScriptValue>[count];
        for (var argumentIndex = 0; argumentIndex < count; argumentIndex++)
        {
            pairs[argumentIndex] = new KeyValuePair<string, GameEventScriptValue>(
                names[argumentIndex],
                stack[start + argumentIndex].ToGameEventScriptValue());
        }

        var arguments = GameEventScriptNamedArguments.CreateOrdered(pairs, names);
        return GesMessageValueCodec.TryBindHandlerValue(callee.ToGameEventScriptValue(), arguments, out var message)
            ? BytecodeVmValue.Reference(GesMessageValueCodec.CreateMessageValue(message))
            : BytecodeVmValue.Nothing;
    }

    private bool TryCallExtension(
        string? extensionName,
        string? functionName,
        string[]? labels,
        int referenceIndex,
        BytecodeVmValue[] stack,
        int start,
        int count,
        out BytecodeVmValue value)
    {
        value = BytecodeVmValue.Nothing;
        if (string.IsNullOrWhiteSpace(extensionName) ||
            string.IsNullOrWhiteSpace(functionName))
        {
            return true;
        }

        var argumentLabels = labels is { Length: var labelCount } && labelCount == count
            ? labels
            : Enumerable.Repeat(GameEventScriptMessageSignature.UnlabeledParameterName, count).ToArray();
        var reference = new GameEventScriptExtensionReference(extensionName, functionName, argumentLabels);
        var arguments = new GameEventScriptFastValue[count];
        for (var argumentIndex = 0; argumentIndex < count; argumentIndex++)
        {
            arguments[argumentIndex] = ToGameEventScriptFastValue(stack[start + argumentIndex]);
        }

        if (GesStandardExtensions.TryInvoke(reference, arguments, out var standardValue))
        {
            value = BytecodeVmValue.FromGameEventScriptFastValue(standardValue);
            return true;
        }

        IGameEventScriptExtensionFunction function;
        if (referenceIndex >= 0)
        {
            if (!_compiledScript.TryGetBoundExtension(referenceIndex, out function))
            {
                throw new GameEventScriptDynamicLinkException($"GameEventScript extension '{reference.SignatureId}' was not dynamically bound to reference slot '{referenceIndex}'.");
            }
        }
        else
        {
            if (!_compiledScript.TryGetBoundExtension(reference, out function))
            {
                throw new GameEventScriptDynamicLinkException($"GameEventScript extension '{reference.SignatureId}' was not dynamically bound.");
            }
        }

        value = BytecodeVmValue.FromGameEventScriptFastValue(function.Invoke(new GameEventScriptExtensionContext(_context), arguments));
        return true;
    }

    private static GameEventScriptFastValue ToGameEventScriptFastValue(BytecodeVmValue value)
        => value.Kind switch
        {
            BytecodeVmValueKind.Nothing => GameEventScriptFastValue.Nothing,
            BytecodeVmValueKind.Boolean => GameEventScriptFastValue.FromBoolean(value.BooleanValue),
            BytecodeVmValueKind.Integer => GameEventScriptFastValue.FromInteger(value.IntegerValue, value.Unit),
            BytecodeVmValueKind.Float => GameEventScriptFastValue.FromFloat(value.Number, value.Unit),
            BytecodeVmValueKind.Percentage => GameEventScriptFastValue.FromPercentage(value.Number),
            BytecodeVmValueKind.Reference => GameEventScriptFastValue.FromGameEventScriptValue(value.ReferenceValue ?? GameEventScriptNothingValue.Instance),
            _ => GameEventScriptFastValue.Nothing
        };

    private BytecodeVmValue EvaluateProgramBinary(GameEventScriptBytecodeOpCode opCode, in BytecodeVmValue left, in BytecodeVmValue right)
    {
        if (opCode != GameEventScriptBytecodeOpCode.Default &&
            (left.IsNothingLike() || right.IsNothingLike()))
        {
            return BytecodeVmValue.Nothing;
        }

        switch (opCode)
        {
            case GameEventScriptBytecodeOpCode.Or:
                return BytecodeVmValue.Boolean(left.AsBoolean() || right.AsBoolean());
            case GameEventScriptBytecodeOpCode.Xor:
                return BytecodeVmValue.Boolean(left.AsBoolean() ^ right.AsBoolean());
            case GameEventScriptBytecodeOpCode.And:
                return BytecodeVmValue.Boolean(left.AsBoolean() && right.AsBoolean());
            case GameEventScriptBytecodeOpCode.Power:
                return BytecodeVmValue.Power(left, right);
            case GameEventScriptBytecodeOpCode.Equal:
                return BytecodeVmValue.Boolean(BytecodeVmValue.AreEqual(left, right));
            case GameEventScriptBytecodeOpCode.NotEqual:
                return BytecodeVmValue.Boolean(!BytecodeVmValue.AreEqual(left, right));
            case GameEventScriptBytecodeOpCode.ApproxEqual:
                return BytecodeVmValue.Boolean(BytecodeVmValue.AreApproximatelyEqual(left, right));
            case GameEventScriptBytecodeOpCode.Less:
                return BytecodeVmValue.Boolean(BytecodeVmValue.TryCompareNumeric(left, right, out var lessComparison) && lessComparison < 0);
            case GameEventScriptBytecodeOpCode.Greater:
                return BytecodeVmValue.Boolean(BytecodeVmValue.TryCompareNumeric(left, right, out var greaterComparison) && greaterComparison > 0);
            case GameEventScriptBytecodeOpCode.LessOrEqual:
                return BytecodeVmValue.Boolean(BytecodeVmValue.TryCompareNumeric(left, right, out var lessOrEqualComparison) && lessOrEqualComparison <= 0);
            case GameEventScriptBytecodeOpCode.GreaterOrEqual:
                return BytecodeVmValue.Boolean(BytecodeVmValue.TryCompareNumeric(left, right, out var greaterOrEqualComparison) && greaterOrEqualComparison >= 0);
            case GameEventScriptBytecodeOpCode.Add:
                return BytecodeVmValue.Add(left, right);
            case GameEventScriptBytecodeOpCode.Subtract:
                return BytecodeVmValue.Subtract(left, right);
            case GameEventScriptBytecodeOpCode.Multiply:
                return BytecodeVmValue.Multiply(left, right);
            case GameEventScriptBytecodeOpCode.Divide:
                return BytecodeVmValue.Divide(left, right);
            case GameEventScriptBytecodeOpCode.IntegerDivide:
                return BytecodeVmValue.IntegerDivide(left, right);
            case GameEventScriptBytecodeOpCode.Modulo:
                return BytecodeVmValue.Modulo(left, right);
            case GameEventScriptBytecodeOpCode.Remainder:
                return BytecodeVmValue.Remainder(left, right);
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerEqual:
                return BytecodeVmValue.TryComparePrimitiveIntegers(left, right, out var equalComparison)
                    ? BytecodeVmValue.Boolean(equalComparison == 0)
                    : BytecodeVmValue.Boolean(BytecodeVmValue.AreEqual(left, right));
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerNotEqual:
                return BytecodeVmValue.TryComparePrimitiveIntegers(left, right, out var notEqualComparison)
                    ? BytecodeVmValue.Boolean(notEqualComparison != 0)
                    : BytecodeVmValue.Boolean(!BytecodeVmValue.AreEqual(left, right));
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerLess:
                return BytecodeVmValue.TryComparePrimitiveIntegers(left, right, out var integerLessComparison)
                    ? BytecodeVmValue.Boolean(integerLessComparison < 0)
                    : BytecodeVmValue.Boolean(BytecodeVmValue.TryCompareNumeric(left, right, out var primitiveLessComparison) && primitiveLessComparison < 0);
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerGreater:
                return BytecodeVmValue.TryComparePrimitiveIntegers(left, right, out var integerGreaterComparison)
                    ? BytecodeVmValue.Boolean(integerGreaterComparison > 0)
                    : BytecodeVmValue.Boolean(BytecodeVmValue.TryCompareNumeric(left, right, out var primitiveGreaterComparison) && primitiveGreaterComparison > 0);
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerLessOrEqual:
                return BytecodeVmValue.TryComparePrimitiveIntegers(left, right, out var integerLessOrEqualComparison)
                    ? BytecodeVmValue.Boolean(integerLessOrEqualComparison <= 0)
                    : BytecodeVmValue.Boolean(BytecodeVmValue.TryCompareNumeric(left, right, out var primitiveLessOrEqualComparison) && primitiveLessOrEqualComparison <= 0);
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerGreaterOrEqual:
                return BytecodeVmValue.TryComparePrimitiveIntegers(left, right, out var integerGreaterOrEqualComparison)
                    ? BytecodeVmValue.Boolean(integerGreaterOrEqualComparison >= 0)
                    : BytecodeVmValue.Boolean(BytecodeVmValue.TryCompareNumeric(left, right, out var primitiveGreaterOrEqualComparison) && primitiveGreaterOrEqualComparison >= 0);
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerAdd:
                return BytecodeVmValue.TryPrimitiveIntegerAdd(left, right, out var integerAdd) ? integerAdd : BytecodeVmValue.Add(left, right);
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerSubtract:
                return BytecodeVmValue.TryPrimitiveIntegerSubtract(left, right, out var integerSubtract) ? integerSubtract : BytecodeVmValue.Subtract(left, right);
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerMultiply:
                return BytecodeVmValue.TryPrimitiveIntegerMultiply(left, right, out var integerMultiply) ? integerMultiply : BytecodeVmValue.Multiply(left, right);
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerDivide:
                return BytecodeVmValue.TryPrimitiveIntegerDivide(left, right, out var integerDivide) ? integerDivide : BytecodeVmValue.Divide(left, right);
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerFloorDivide:
                return BytecodeVmValue.TryPrimitiveIntegerFloorDivide(left, right, out var integerFloorDivide) ? integerFloorDivide : BytecodeVmValue.IntegerDivide(left, right);
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerModulo:
                return BytecodeVmValue.TryPrimitiveIntegerModulo(left, right, out var integerModulo) ? integerModulo : BytecodeVmValue.Modulo(left, right);
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerRemainder:
                return BytecodeVmValue.TryPrimitiveIntegerRemainder(left, right, out var integerRemainder) ? integerRemainder : BytecodeVmValue.Remainder(left, right);
            default:
                return TryEvaluateBinaryOperation(GetBinaryOperator(opCode), left, right, out var value)
                    ? value
                    : throw new InvalidOperationException($"BytecodeVM invariant failed: binary opcode '{opCode}' could not be evaluated.");
        }
    }

    private BytecodeVmValue EvaluateProgramCast(GameEventScriptBytecodeCastKind castKind, BytecodeVmValue input)
        => TryConvertDeclaredType(GetCastTypeName(castKind), input, out var value)
            ? value
            : throw new InvalidOperationException($"BytecodeVM invariant failed: cast '{castKind}' could not be evaluated.");

    private static bool IsValueOfType(BytecodeVmValue value, string? typeName)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            return false;
        }

        return typeName switch
        {
            "nothing" => value.Kind == BytecodeVmValueKind.Nothing || (value.ReferenceValue?.IsNothing() ?? false),
            "tag" => value.ReferenceValue?.IsTag() ?? false,
            "text" => value.ReferenceValue?.IsText() ?? false,
            "percentage" => value.Kind == BytecodeVmValueKind.Percentage || (value.ReferenceValue?.IsPercentage() ?? false),
            "degree" => (value.Kind is BytecodeVmValueKind.Integer or BytecodeVmValueKind.Float) && value.Unit == GameEventScriptNumericUnit.Degree ||
                        (value.ReferenceValue?.IsNumericUnit(GameEventScriptNumericUnit.Degree) ?? false),
            "meter" => (value.Kind is BytecodeVmValueKind.Integer or BytecodeVmValueKind.Float) && value.Unit == GameEventScriptNumericUnit.Meter ||
                       (value.ReferenceValue?.IsNumericUnit(GameEventScriptNumericUnit.Meter) ?? false),
            "second" => (value.Kind is BytecodeVmValueKind.Integer or BytecodeVmValueKind.Float) && value.Unit == GameEventScriptNumericUnit.Second ||
                        (value.ReferenceValue?.IsNumericUnit(GameEventScriptNumericUnit.Second) ?? false),
            "vector" => value.ReferenceValue?.IsVector() ?? false,
            "point" => value.ReferenceValue?.IsPoint() ?? false,
            "float" => value.Kind is BytecodeVmValueKind.Integer or BytecodeVmValueKind.Float or BytecodeVmValueKind.Percentage ||
                         (value.ReferenceValue?.IsNumber() ?? false),
            "integer" => value.Kind == BytecodeVmValueKind.Integer || (value.ReferenceValue?.IsInteger() ?? false),
            "boolean" => value.Kind == BytecodeVmValueKind.Boolean || value.ReferenceValue?.Kind == GameEventScriptValueKind.Boolean,
            "optional" => value.ReferenceValue?.IsOptional() ?? false,
            "sequence" => value.ReferenceValue?.IsSequence() ?? false,
            "list" => value.ReferenceValue?.IsList() ?? false,
            "range" => value.ReferenceValue?.IsRange() ?? false,
            "message" => value.ReferenceValue is { } messageValue &&
                         (messageValue.Kind == GameEventScriptValueKind.Message || GesMessageValueCodec.TryReadMessageValue(messageValue, out _)),
            "handler" => value.ReferenceValue is { } handlerValue &&
                         (handlerValue.Kind == GameEventScriptValueKind.Handler || GesMessageValueCodec.TryReadHandlerValue(handlerValue, out _)),
            "dictionary" => value.ReferenceValue?.IsDictionary() ?? false,
            "set" => value.ReferenceValue?.IsSet() ?? false,
            "dice" => value.ReferenceValue?.IsDice() ?? false,
            _ => value.ReferenceValue is { } customValue &&
                 customValue.TryGetCustomTypeName(out var customTypeName) &&
                 string.Equals(customTypeName, typeName, StringComparison.Ordinal)
        };
    }

    private bool TryEvaluateSeededRandomExpression(
        BytecodeVmValue seed,
        GameEventScriptBytecodeExpressionProgram bodyProgram,
        int stackBase,
        out BytecodeVmValue value)
    {
        PushSeededRandomScope(seed.ToGameEventScriptValue());
        try
        {
            return TryExecuteExpressionProgram(bodyProgram, stackBase, out value);
        }
        finally
        {
            PopSeededRandomScope();
        }
    }

    private bool TryExecuteGeneratedCollectionProgram(GameEventScriptBytecodeGeneratedCollectionProgram program, out BytecodeVmValue value)
    {
        if (!TryMaterializeIterationSource(program.Source, out var sourceItems))
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        var values = new List<GameEventScriptValue>();
        foreach (var item in sourceItems)
        {
            if (!_runtimeBudget.TryConsumeLoopIteration("Generated collection iteration budget exhausted."))
            {
                break;
            }

            var fastItem = BytecodeVmValue.FromGameEventScriptValue(item);
            if (program.PredicateProgram is not null)
            {
                if (!TryExecuteExpressionProgramWithTemporarySlot(program.IdentifierSlot, fastItem, program.PredicateProgram, 0, out var predicate))
                {
                    value = BytecodeVmValue.Nothing;
                    return false;
                }

                if (!predicate.AsBoolean())
                {
                    continue;
                }
            }

            if (!_runtimeBudget.TryCheckGeneratedCollectionItemCount(values.Count + 1, "Generated collection item count exceeds the configured limit."))
            {
                break;
            }

            if (!TryExecuteExpressionProgramWithTemporarySlot(program.IdentifierSlot, fastItem, program.ProjectionProgram, 0, out var projected))
            {
                value = BytecodeVmValue.Nothing;
                return false;
            }

            values.Add(projected.ToGameEventScriptValue());
        }

        value = BytecodeVmValue.Reference(program.CollectionType == "set"
            ? GameEventScriptValueFactory.GesSet(values)
            : GameEventScriptValueFactory.GesList(values));
        return true;
    }

    private bool TryMaterializeIterationSource(GameEventScriptBytecodeIterationSourceProgram source, out GameEventScriptValue[] items)
    {
        switch (source.Kind)
        {
            case GameEventScriptBytecodeIterationSourceKind.Collection:
                if (source.CollectionProgram is null ||
                    !TryExecuteExpressionProgram(source.CollectionProgram, 0, out var collectionValue))
                {
                    items = [];
                    return false;
                }

                var boxedCollection = collectionValue.ToGameEventScriptValue();
                if (!TryCheckMaterializedValue(boxedCollection, "Iteration source would enumerate more range items than allowed."))
                {
                    items = [];
                    return true;
                }

                items = boxedCollection.AsEnumerable().ToArray();
                return true;

            case GameEventScriptBytecodeIterationSourceKind.Range:
                if (source.RangeFromProgram is null ||
                    source.RangeToProgram is null ||
                    !TryExecuteExpressionProgram(source.RangeFromProgram, 0, out var from) ||
                    !TryExecuteExpressionProgram(source.RangeToProgram, 0, out var to))
                {
                    items = [];
                    return false;
                }

                var step = BytecodeVmValue.Integer(1);
                if (source.RangeStepProgram is not null &&
                    !TryExecuteExpressionProgram(source.RangeStepProgram, 0, out step))
                {
                    items = [];
                    return false;
                }

                var rangeValue = EvaluateRangeExpression(from, to, step);
                var boxedRange = rangeValue.ToGameEventScriptValue();
                if (!TryCheckMaterializedValue(boxedRange, "Range item count exceeds the configured limit."))
                {
                    items = [];
                    return true;
                }

                items = boxedRange.AsEnumerable().ToArray();
                return true;

            default:
                items = [];
                return false;
        }
    }

    private bool TryExecuteGuardedChoiceProgram(GameEventScriptBytecodeGuardedChoiceProgram program, out BytecodeVmValue value)
    {
        var conditions = program.ConditionPrograms;
        var values = program.ValuePrograms;
        if (conditions.Length != values.Length)
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        for (var branchIndex = 0; branchIndex < conditions.Length; branchIndex++)
        {
            if (!TryExecuteExpressionProgram(conditions[branchIndex], 0, out var condition))
            {
                value = BytecodeVmValue.Nothing;
                return false;
            }

            if (condition.AsBoolean())
            {
                return TryExecuteExpressionProgram(values[branchIndex], 0, out value);
            }
        }

        return TryExecuteExpressionProgram(program.OtherwiseProgram, 0, out value);
    }

    private BytecodeVmValue EvaluateRandomExpression(BytecodeVmValue fromValue, BytecodeVmValue toValue)
    {
        var fromRaw = fromValue.ToGameEventScriptValue();
        var toRaw = toValue.ToGameEventScriptValue();

        if (GesValueOperations.TryUnwrapOptionalForOperation(fromRaw, out var unwrappedFrom) &&
            GesValueOperations.TryUnwrapOptionalForOperation(toRaw, out var unwrappedTo) &&
            unwrappedFrom.Kind == GameEventScriptValueKind.Integer &&
            unwrappedTo.Kind == GameEventScriptValueKind.Integer)
        {
            var from = ToIntSaturated(unwrappedFrom.AsInteger());
            var to = ToIntSaturated(unwrappedTo.AsInteger());
            if (from > to)
            {
                (from, to) = (to, from);
            }

            return TryNextInclusiveInt(from, to, out var next)
                ? BytecodeVmValue.Integer(next)
                : BytecodeVmValue.Nothing;
        }

        if (!GesValueOperations.TryCoerceNumericForOperation(fromRaw, out var fromNumber) ||
            !GesValueOperations.TryCoerceNumericForOperation(toRaw, out var toNumber) ||
            !fromNumber.IsFinite ||
            !toNumber.IsFinite)
        {
            return BytecodeVmValue.Nothing;
        }

        var lower = Math.Min(fromNumber.Value, toNumber.Value);
        var upper = Math.Max(fromNumber.Value, toNumber.Value);
        if (lower == upper)
        {
            return BytecodeVmValue.Float(lower);
        }

        return TryNextInclusiveFloat(lower, upper, out var nextFloat)
            ? BytecodeVmValue.Float(nextFloat)
            : BytecodeVmValue.Nothing;
    }

    private static BytecodeVmValue EvaluateRangeExpression(BytecodeVmValue fromValue, BytecodeVmValue toValue, BytecodeVmValue stepValue)
    {
        if (!GesValueOperations.TryCoerceNumericForOperation(fromValue.ToGameEventScriptValue(), out var fromNumber) ||
            !GesValueOperations.TryCoerceNumericForOperation(toValue.ToGameEventScriptValue(), out var toNumber) ||
            !GesValueOperations.TryCoerceNumericForOperation(stepValue.ToGameEventScriptValue(), out var stepNumber) ||
            !fromNumber.IsFinite ||
            !toNumber.IsFinite ||
            !stepNumber.IsFinite)
        {
            return BytecodeVmValue.Nothing;
        }

        return BytecodeVmValue.Reference(GesRange(
            GesValueOperations.ToIntegerSaturated(fromNumber.Value),
            GesValueOperations.ToIntegerSaturated(toNumber.Value),
            GesValueOperations.ToIntegerSaturated(stepNumber.Value)));
    }

    private BytecodeVmValue EvaluateDiceExpression(int diceCount, int sideCount)
    {
        if (diceCount <= 0 || sideCount <= 0)
        {
            return BytecodeVmValue.Reference(GesDice(GameEventScriptDiceValue.Empty));
        }

        if (!_runtimeBudget.TryCheckDice(diceCount, sideCount))
        {
            return BytecodeVmValue.Reference(GesDice(GameEventScriptDiceValue.Empty));
        }

        var rolls = new int[diceCount];
        for (var i = 0; i < rolls.Length; i++)
        {
            if (!TryNextInclusiveInt(1, sideCount, out var roll))
            {
                return BytecodeVmValue.Reference(GesDice(GameEventScriptDiceValue.Empty));
            }

            rolls[i] = roll;
        }

        return BytecodeVmValue.Reference(GesDice(GameEventScriptDiceValue.Create(rolls)));
    }

    private bool TryEvaluateUnaryOperation(string? operation, BytecodeVmValue operand, out BytecodeVmValue value)
    {
        var boxed = operand.ToGameEventScriptValue();
        value = operation switch
        {
            "-" => BytecodeVmValue.FromGameEventScriptValue(EvaluateNegateUnary(boxed)),
            "!" => BytecodeVmValue.FromGameEventScriptValue(EvaluateNotUnary(boxed)),
            "has value" => BytecodeVmValue.Boolean(boxed.HasSemanticValue()),
            "empty" => BytecodeVmValue.Boolean(boxed.IsSemanticallyEmpty()),
            "len" => BytecodeVmValue.FromGameEventScriptValue(EvaluateLenUnary(boxed)),
            "chance" => BytecodeVmValue.FromGameEventScriptValue(EvaluateChanceUnary(boxed)),
            "keys" => BytecodeVmValue.Reference(GesKeys(boxed)),
            "values" => BytecodeVmValue.Reference(GesValues(boxed)),
            "entries" => BytecodeVmValue.Reference(GesEntries(boxed)),
            "abs" => BytecodeVmValue.FromGameEventScriptValue(EvaluateAbsUnary(boxed)),
            "ln" => BytecodeVmValue.FromGameEventScriptValue(EvaluateNaturalLogUnary(boxed)),
            _ => throw new InvalidOperationException($"BytecodeVM invariant failed: unknown unary operator '{operation}'.")
        };

        return true;
    }

    private bool TryEvaluateVariadicOperation(
        string? operation,
        BytecodeVmValue[] stack,
        int start,
        int count,
        out BytecodeVmValue value)
    {
        if (count == 0)
        {
            value = BytecodeVmValue.Nothing;
            return true;
        }

        var boxedValues = new GameEventScriptValue[count];
        for (var i = 0; i < count; i++)
        {
            boxedValues[i] = stack[start + i].ToGameEventScriptValue();
        }

        value = operation switch
        {
            "min" => BytecodeVmValue.FromGameEventScriptValue(GesValueOperations.EvaluateMinMax(boxedValues, isMax: false)),
            "max" => BytecodeVmValue.FromGameEventScriptValue(GesValueOperations.EvaluateMinMax(boxedValues, isMax: true)),
            _ => throw new InvalidOperationException($"BytecodeVM invariant failed: unknown variadic operator '{operation}'.")
        };

        return true;
    }

    private bool TryEvaluateBinaryOperation(string operation, BytecodeVmValue leftRawValue, BytecodeVmValue rightRawValue, out BytecodeVmValue value)
    {
        var leftRaw = leftRawValue.ToGameEventScriptValue();
        var rightRaw = rightRawValue.ToGameEventScriptValue();

        if (operation == "default")
        {
            value = BytecodeVmValue.FromGameEventScriptValue(EvaluateDefaultBinary(leftRaw, rightRaw));
            return true;
        }

        if (leftRaw.IsNothing() || rightRaw.IsNothing())
        {
            value = BytecodeVmValue.Nothing;
            return true;
        }

        if (!GesValueOperations.TryUnwrapOptionalForOperation(leftRaw, out var left) ||
            !GesValueOperations.TryUnwrapOptionalForOperation(rightRaw, out var right))
        {
            value = BytecodeVmValue.Reference(GesOptionalNone());
            return true;
        }

        value = operation switch
        {
            "|" => BytecodeVmValue.Boolean(left.AsBoolean() || right.AsBoolean()),
            "xor" => BytecodeVmValue.Boolean(left.AsBoolean() ^ right.AsBoolean()),
            "&" => BytecodeVmValue.Boolean(left.AsBoolean() && right.AsBoolean()),
            "=" or "==" => BytecodeVmValue.Boolean(GesValueOperations.AreEqual(left, right)),
            "<>" => BytecodeVmValue.Boolean(!GesValueOperations.AreEqual(left, right)),
            "=~" => BytecodeVmValue.Boolean(GesValueOperations.AreApproximatelyEqual(left, right)),
            "in" => BytecodeVmValue.Boolean(right.Contains(left)),
            "value in" => BytecodeVmValue.Boolean(right.ContainsValue(left)),
            "starts with" => BytecodeVmValue.Boolean(left.StartsWith(right)),
            "ends with" => BytecodeVmValue.Boolean(left.EndsWith(right)),
            "<" => BytecodeVmValue.Boolean(TryCompare(left, right, static comparison => comparison < 0)),
            ">" => BytecodeVmValue.Boolean(TryCompare(left, right, static comparison => comparison > 0)),
            "<=" => BytecodeVmValue.Boolean(TryCompare(left, right, static comparison => comparison <= 0)),
            ">=" => BytecodeVmValue.Boolean(TryCompare(left, right, static comparison => comparison >= 0)),
            "+" => BytecodeVmValue.FromGameEventScriptValue(EvaluateAddBinary(left, right)),
            "-" => BytecodeVmValue.FromGameEventScriptValue(EvaluateNumericBinary(left, "-", right)),
            "*" => BytecodeVmValue.FromGameEventScriptValue(EvaluateNumericBinary(left, "*", right)),
            "/" => BytecodeVmValue.FromGameEventScriptValue(EvaluateNumericBinary(left, "/", right)),
            "div" => BytecodeVmValue.FromGameEventScriptValue(EvaluateNumericBinary(left, "div", right)),
            "mod" => BytecodeVmValue.FromGameEventScriptValue(EvaluateNumericBinary(left, "mod", right)),
            "rem" => BytecodeVmValue.FromGameEventScriptValue(EvaluateNumericBinary(left, "rem", right)),
            "^" => BytecodeVmValue.FromGameEventScriptValue(EvaluateNumericBinary(left, "^", right)),
            "intersect" => BytecodeVmValue.Reference(GesValueOperations.EvaluateCollectionIntersect(left, right)),
            "combine" or "merge" => BytecodeVmValue.Reference(GesValueOperations.EvaluateCollectionCombine(left, right)),
            "except" => BytecodeVmValue.Reference(GesValueOperations.EvaluateCollectionExcept(left, right)),
            "zip" => BytecodeVmValue.Reference(GesValueOperations.EvaluateCollectionZip(left, right)),
            _ => throw new InvalidOperationException($"BytecodeVM invariant failed: unknown binary operator '{operation}'.")
        };

        return true;
    }

    private static string GetBinaryOperator(GameEventScriptBytecodeOpCode opCode)
        => opCode switch
        {
            GameEventScriptBytecodeOpCode.Or => "|",
            GameEventScriptBytecodeOpCode.Xor => "xor",
            GameEventScriptBytecodeOpCode.And => "&",
            GameEventScriptBytecodeOpCode.Power => "^",
            GameEventScriptBytecodeOpCode.Equal => "=",
            GameEventScriptBytecodeOpCode.NotEqual => "<>",
            GameEventScriptBytecodeOpCode.ApproxEqual => "=~",
            GameEventScriptBytecodeOpCode.Less => "<",
            GameEventScriptBytecodeOpCode.Greater => ">",
            GameEventScriptBytecodeOpCode.LessOrEqual => "<=",
            GameEventScriptBytecodeOpCode.GreaterOrEqual => ">=",
            GameEventScriptBytecodeOpCode.Add => "+",
            GameEventScriptBytecodeOpCode.Subtract => "-",
            GameEventScriptBytecodeOpCode.Multiply => "*",
            GameEventScriptBytecodeOpCode.Divide => "/",
            GameEventScriptBytecodeOpCode.IntegerDivide => "div",
            GameEventScriptBytecodeOpCode.Modulo => "mod",
            GameEventScriptBytecodeOpCode.Remainder => "rem",
            GameEventScriptBytecodeOpCode.Default => "default",
            GameEventScriptBytecodeOpCode.Contains => "in",
            GameEventScriptBytecodeOpCode.ContainsValue => "value in",
            GameEventScriptBytecodeOpCode.StartsWith => "starts with",
            GameEventScriptBytecodeOpCode.EndsWith => "ends with",
            GameEventScriptBytecodeOpCode.Intersect => "intersect",
            GameEventScriptBytecodeOpCode.Combine => "combine",
            GameEventScriptBytecodeOpCode.Except => "except",
            GameEventScriptBytecodeOpCode.Zip => "zip",
            _ => string.Empty
        };

    private static GameEventScriptValue EvaluateDefaultBinary(GameEventScriptValue leftRaw, GameEventScriptValue rightRaw)
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

    private static bool TryCompare(GameEventScriptValue left, GameEventScriptValue right, Func<int, bool> predicate)
        => GesValueOperations.TryCompareNumericValues(left, right, out var comparison) &&
           predicate(comparison);

    private static GameEventScriptValue EvaluateAddBinary(GameEventScriptValue left, GameEventScriptValue right)
    {
        if (GesValueOperations.TryEvaluatePointBinary(left, "+", right, out var point))
        {
            return point;
        }

        if (GesValueOperations.TryEvaluateVectorBinary(left, "+", right, out var vector))
        {
            return vector;
        }

        if (GesValueOperations.TryEvaluatePercentageBinary(left, "+", right, out var percentage))
        {
            return percentage;
        }

        if (GesValueOperations.TryEvaluateUnitBinary(left, "+", right, out var unit))
        {
            return unit;
        }

        if (GesValueOperations.TryCoerceNumericForOperation(left, out var leftNumeric) &&
            GesValueOperations.TryCoerceNumericForOperation(right, out var rightNumeric))
        {
            return GesValueOperations.ToGameEventScriptNumericResult(left, "+", right, GesValueOperations.AddNumeric(leftNumeric, rightNumeric));
        }

        if (GesValueOperations.TryCombineWithPlus(left, right, out var combined))
        {
            return combined;
        }

        return left.IsText() || right.IsText()
            ? GesText($"{GesValueOperations.ToText(left)}{GesValueOperations.ToText(right)}")
            : GesFloatNaN();
    }

    private static GameEventScriptValue EvaluateNumericBinary(GameEventScriptValue left, string operation, GameEventScriptValue right)
    {
        if (GesValueOperations.TryEvaluatePointBinary(left, operation, right, out var point))
        {
            return point;
        }

        if (GesValueOperations.TryEvaluateVectorBinary(left, operation, right, out var vector))
        {
            return vector;
        }

        if (GesValueOperations.TryEvaluatePercentageBinary(left, operation, right, out var percentage))
        {
            return percentage;
        }

        if (GesValueOperations.TryEvaluateUnitBinary(left, operation, right, out var unit))
        {
            return unit;
        }

        if (!GesValueOperations.TryCoerceNumericForOperation(left, out var leftNumeric) ||
            !GesValueOperations.TryCoerceNumericForOperation(right, out var rightNumeric))
        {
            return GesFloatNaN();
        }

        var result = operation switch
        {
            "-" => GesValueOperations.SubtractNumeric(leftNumeric, rightNumeric),
            "*" => GesValueOperations.MultiplyNumeric(leftNumeric, rightNumeric),
            "/" => GesValueOperations.DivideNumeric(leftNumeric, rightNumeric),
            "div" => GesValueOperations.IntegerDivideNumeric(leftNumeric, rightNumeric),
            "mod" => GesValueOperations.ModuloNumeric(leftNumeric, rightNumeric),
            "rem" => GesValueOperations.RemainderNumeric(leftNumeric, rightNumeric),
            "^" => GesValueOperations.PowerNumeric(leftNumeric, rightNumeric),
            _ => GesValueOperations.NumericValue.NaN()
        };
        return GesValueOperations.ToGameEventScriptNumericResult(left, operation, right, result);
    }

    private static GameEventScriptValue EvaluateNegateUnary(GameEventScriptValue operand)
    {
        if (operand.IsNothing())
        {
            return GameEventScriptNothingValue.Instance;
        }

        if (!GesValueOperations.TryUnwrapOptionalForOperation(operand, out var unwrapped))
        {
            return GesOptionalNone();
        }

        if (unwrapped.IsPercentage())
        {
            return GesPercentage(-unwrapped.AsNumber());
        }

        if (GesValueOperations.TryEvaluatePointUnary(unwrapped, "-", out var pointNegation))
        {
            return pointNegation;
        }

        if (GesValueOperations.TryEvaluateVectorUnary(unwrapped, "-", out var vectorNegation))
        {
            return vectorNegation;
        }

        if (unwrapped is GameEventScriptIntegerValue { Value: not long.MinValue } integer)
        {
            return GesInteger(-integer.Value, integer.Unit);
        }

        if (GameEventScriptValue.TryGetNumericUnit(unwrapped, out var unit))
        {
            return GesFloat(-unwrapped.AsNumber(), unit);
        }

        return GesValueOperations.TryCoerceNumericForOperation(unwrapped, out var number)
            ? GesValueOperations.ToGameEventScriptFloat(GesValueOperations.NegateNumeric(number))
            : GameEventScriptNothingValue.Instance;
    }

    private static GameEventScriptValue EvaluateNotUnary(GameEventScriptValue operand)
    {
        if (operand.IsNothing())
        {
            return GameEventScriptNothingValue.Instance;
        }

        return GesValueOperations.TryUnwrapOptionalForOperation(operand, out var unwrapped)
            ? GesBoolean(!unwrapped.AsBoolean())
            : GesOptionalNone();
    }

    private GameEventScriptValue EvaluateLenUnary(GameEventScriptValue operand)
    {
        if (operand.IsNothing())
        {
            return GesInteger(0);
        }

        return operand.Kind switch
        {
            GameEventScriptValueKind.Text => GesInteger(operand.AsText().Length),
            GameEventScriptValueKind.Sequence => CountEnumerableWithBudget(operand.AsEnumerable(), "Sequence length evaluation budget exhausted."),
            GameEventScriptValueKind.Range => EvaluateRangeLength(operand),
            GameEventScriptValueKind.List => GesInteger(operand.AsList().Count),
            GameEventScriptValueKind.Dictionary => GesInteger(operand.AsDictionary().Count),
            GameEventScriptValueKind.Set => GesInteger(operand.AsSet().Count),
            GameEventScriptValueKind.Dice => GesInteger(operand.AsDice().Rolls.Count),
            GameEventScriptValueKind.Optional => GesInteger(operand.AsOptional().HasValue ? 1 : 0),
            _ => GameEventScriptNothingValue.Instance
        };
    }

    private GameEventScriptValue EvaluateRangeLength(GameEventScriptValue operand)
    {
        if (!GesRuntimeLimitUtilities.TryGetRangeLength(operand, out var length))
        {
            return GameEventScriptNothingValue.Instance;
        }

        return _runtimeBudget.TryCheckRangeLength(length, "Range length exceeds the configured limit.")
            ? GesInteger(length)
            : GameEventScriptNothingValue.Instance;
    }

    private GameEventScriptValue CountEnumerableWithBudget(IEnumerable<GameEventScriptValue> values, string detail)
    {
        long count = 0;
        foreach (var _ in values)
        {
            if (!_runtimeBudget.TryConsumeLoopIteration(detail))
            {
                return GameEventScriptNothingValue.Instance;
            }

            count++;
        }

        return GesInteger(count);
    }

    private GameEventScriptValue EvaluateChanceUnary(GameEventScriptValue operand)
    {
        var percentage = ConvertToPercentage(operand);
        if (!percentage.IsPercentage())
        {
            return GesBoolean(false);
        }

        var ratio = percentage.AsNumber();
        if (ratio <= 0d)
        {
            return GesBoolean(false);
        }

        if (ratio >= 1d)
        {
            return GesBoolean(true);
        }

        return TryNextInclusiveFloat(0d, 1d, out var randomValue)
            ? GesBoolean(randomValue < ratio)
            : GesBoolean(false);
    }

    private static GameEventScriptValue EvaluateAbsUnary(GameEventScriptValue operand)
    {
        if (operand.IsNothing())
        {
            return GameEventScriptNothingValue.Instance;
        }

        if (GesValueOperations.TryEvaluatePointUnary(operand, "abs", out var pointAbs))
        {
            return pointAbs;
        }

        if (GesValueOperations.TryEvaluateVectorUnary(operand, "abs", out var vectorLength))
        {
            return vectorLength;
        }

        return GesValueOperations.TryCoerceNumericForOperation(operand, out var number) && number.IsFinite
            ? GesFloat(Math.Abs(number.Value))
            : GameEventScriptNothingValue.Instance;
    }

    private static GameEventScriptValue EvaluateNaturalLogUnary(GameEventScriptValue operand)
    {
        if (operand.IsNothing())
        {
            return GameEventScriptNothingValue.Instance;
        }

        if (!GesValueOperations.TryUnwrapOptionalForOperation(operand, out var unwrapped))
        {
            return GesOptionalNone();
        }

        if (GameEventScriptValue.TryGetNumericUnit(unwrapped, out _))
        {
            return GesFloatNaN();
        }

        if (!GesValueOperations.TryCoerceNumericForOperation(unwrapped, out var number))
        {
            return GesFloatNaN();
        }

        if (number.IsNaN || number.IsNegativeInfinity)
        {
            return GesFloatNaN();
        }

        if (number.IsPositiveInfinity)
        {
            return GesFloatInfinity();
        }

        if (number.Value < 0d)
        {
            return GesFloatNaN();
        }

        if (number.Value == 0d)
        {
            return GesFloatNegativeInfinity();
        }

        var result = Math.Log((double)number.Value);
        if (double.IsNaN(result))
        {
            return GesFloatNaN();
        }

        if (double.IsPositiveInfinity(result))
        {
            return GesFloatInfinity();
        }

        if (double.IsNegativeInfinity(result))
        {
            return GesFloatNegativeInfinity();
        }

        try
        {
            return GesFloat((double)result);
        }
        catch (OverflowException)
        {
            return result < 0d ? GesFloatNegativeInfinity() : GesFloatInfinity();
        }
    }

    private static BytecodeVmValue EvaluateClamp(BytecodeVmValue rawValue, BytecodeVmValue minimumValue, BytecodeVmValue maximumValue)
    {
        var raw = rawValue.ToGameEventScriptValue();
        var minimum = minimumValue.ToGameEventScriptValue();
        var maximum = maximumValue.ToGameEventScriptValue();

        if (!GesValueOperations.HaveCompatibleNumericUnits(raw, minimum) ||
            !GesValueOperations.HaveCompatibleNumericUnits(raw, maximum) ||
            !GesValueOperations.HaveCompatibleNumericUnits(minimum, maximum))
        {
            return BytecodeVmValue.NaN();
        }

        if (!GesValueOperations.TryCoerceNumericForOperation(raw, out var rawNumber) ||
            !GesValueOperations.TryCoerceNumericForOperation(minimum, out var minimumNumber) ||
            !GesValueOperations.TryCoerceNumericForOperation(maximum, out var maximumNumber) ||
            !rawNumber.IsFinite ||
            !minimumNumber.IsFinite ||
            !maximumNumber.IsFinite)
        {
            return BytecodeVmValue.Nothing;
        }

        var lower = Math.Min(minimumNumber.Value, maximumNumber.Value);
        var upper = Math.Max(minimumNumber.Value, maximumNumber.Value);
        GameEventScriptValue.TryGetNumericUnit(raw, out var unit);
        return BytecodeVmValue.FromGameEventScriptValue(GesFloat(
            Math.Min(Math.Max(rawNumber.Value, lower), upper),
            raw.HasNumericUnit() ? unit : null));
    }

    private bool TryNextInclusiveFloat(double minInclusive, double maxInclusive, out double value)
    {
        try
        {
            value = _randomScopes.Peek().NextInclusiveFloat(minInclusive, maxInclusive);
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

    private void PushSeededRandomScope(GameEventScriptValue seedValue)
        => _randomScopes.Push(GameEventScriptRandomGenerator.FromSeed(DeriveStableSeed(seedValue)));

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

    private static int DeriveStableSeed(GameEventScriptValue value)
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

    private static string BuildStableSeedText(GameEventScriptValue value)
        => value.Kind switch
        {
            GameEventScriptValueKind.Nothing => "nothing",
            GameEventScriptValueKind.Tag => $"tag:{value.AsText()}",
            GameEventScriptValueKind.Text => $"text:{value.AsText()}",
            GameEventScriptValueKind.Percentage => $"percentage:{value.AsNumber().ToString(CultureInfo.InvariantCulture)}",
            GameEventScriptValueKind.Vector => BuildVectorStableSeedText((GameEventScriptVectorValue)value),
            GameEventScriptValueKind.Point => BuildPointStableSeedText((GameEventScriptPointValue)value),
            GameEventScriptValueKind.Float => value.IsNaN()
                ? "float:nan"
                : value.IsNegativeInfinity()
                    ? "float:-infinity"
                    : value.IsInfinity()
                        ? "float:infinity"
                        : value is GameEventScriptFloatValue { Unit: { } unit }
                            ? $"float:{value.AsNumber().ToString(CultureInfo.InvariantCulture)}:{unit.ToTypeName()}"
                            : $"float:{value.AsNumber().ToString(CultureInfo.InvariantCulture)}",
            GameEventScriptValueKind.Integer => $"integer:{value.AsInteger().ToString(CultureInfo.InvariantCulture)}",
            GameEventScriptValueKind.Boolean => $"boolean:{(value.AsBoolean() ? "true" : "false")}",
            GameEventScriptValueKind.Optional => value.AsOptional().HasValue
                ? $"optional:{BuildStableSeedText(value.AsOptional().Value)}"
                : "optional:none",
            GameEventScriptValueKind.Sequence => $"sequence:[{string.Join("|", value.AsEnumerable().Select(BuildStableSeedText))}]",
            GameEventScriptValueKind.Range => $"range:{((GameEventScriptRangeValue)value).From}:{((GameEventScriptRangeValue)value).To}:{((GameEventScriptRangeValue)value).Step}",
            GameEventScriptValueKind.Message =>
                $"message:{((GameEventScriptMessageValue)value).Value.SignatureId}:[{string.Join("|", ((GameEventScriptMessageValue)value).Value.Arguments.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={BuildStableSeedText(pair.Value)}"))}]",
            GameEventScriptValueKind.Handler => $"handler:{((GameEventScriptHandlerValue)value).Signature.SignatureId}",
            GameEventScriptValueKind.List => $"list:[{string.Join("|", value.AsList().Select(BuildStableSeedText))}]",
            GameEventScriptValueKind.Dictionary =>
                $"dict:[{string.Join("|", value.AsDictionary().OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={BuildStableSeedText(pair.Value)}"))}]",
            GameEventScriptValueKind.Set => $"set:[{string.Join("|", value.AsSet().OrderBy(item => item, GameEventScriptValue.StableComparer).Select(BuildStableSeedText))}]",
            GameEventScriptValueKind.Dice => $"dice:[{string.Join("|", value.AsDice().Rolls)}]",
            _ => value.ToString()
        };

    private static string BuildVectorStableSeedText(GameEventScriptVectorValue value)
        => value.Unit.HasValue
            ? $"vector:{value.X.ToString(CultureInfo.InvariantCulture)}:{value.Y.ToString(CultureInfo.InvariantCulture)}:{value.Z.ToString(CultureInfo.InvariantCulture)}:{value.Unit.Value.ToTypeName()}"
            : $"vector:{value.X.ToString(CultureInfo.InvariantCulture)}:{value.Y.ToString(CultureInfo.InvariantCulture)}:{value.Z.ToString(CultureInfo.InvariantCulture)}";

    private static string BuildPointStableSeedText(GameEventScriptPointValue value)
        => value.Unit.HasValue
            ? $"point:{value.X.ToString(CultureInfo.InvariantCulture)}:{value.Y.ToString(CultureInfo.InvariantCulture)}:{value.Z.ToString(CultureInfo.InvariantCulture)}:{value.Unit.Value.ToTypeName()}"
            : $"point:{value.X.ToString(CultureInfo.InvariantCulture)}:{value.Y.ToString(CultureInfo.InvariantCulture)}:{value.Z.ToString(CultureInfo.InvariantCulture)}";

    private bool TryEvaluateRulePredicate(
        in GameEventScriptBytecodeInstruction instruction,
        BytecodeVmValue input,
        int stackBase,
        out BytecodeVmValue value,
        string? parameterName = null)
    {
        value = BytecodeVmValue.Nothing;
        if (instruction.ExpressionProgram is null)
        {
            return false;
        }

        if (!_runtimeBudget.TryEnterCall(CallableCallDepthExceededDetail))
        {
            return true;
        }

        if (instruction.A >= 0)
        {
            try
            {
                if (!TryConvertParameterType(instruction.DeclaredTypes, 0, input, out var ruleInput))
                {
                    return false;
                }

                RecordRuleCalled(instruction, ruleInput);
                if (!_diagnosticsEnabled &&
                    TryEvaluateSimpleRulePredicate(instruction.ExpressionProgram, instruction.A, ruleInput, out value))
                {
                    return true;
                }

                if (!TryExecuteExpressionProgramWithTemporarySlot(instruction.A, ruleInput, instruction.ExpressionProgram, stackBase, out value))
                {
                    value = BytecodeVmValue.Nothing;
                    return false;
                }

                value = BytecodeVmValue.Boolean(value.AsBoolean());
                return true;
            }
            finally
            {
                _runtimeBudget.ExitCall();
            }
        }

        EnterScope();
        try
        {
            if (!TryConvertParameterType(instruction.DeclaredTypes, 0, input, out var ruleInput))
            {
                return false;
            }

            RecordRuleCalled(instruction, ruleInput);
            var parameterDefined = !string.IsNullOrEmpty(parameterName) && Define(parameterName!, ruleInput);
            if (!parameterDefined ||
                !TryExecuteExpressionProgram(instruction.ExpressionProgram, stackBase, out value))
            {
                value = BytecodeVmValue.Nothing;
                return false;
            }

            value = BytecodeVmValue.Boolean(value.AsBoolean());
            return true;
        }
        finally
        {
            ExitScope();
            _runtimeBudget.ExitCall();
        }
    }

    private bool TryEvaluateSimpleRulePredicate(
        GameEventScriptBytecodeExpressionProgram program,
        int parameterSlot,
        BytecodeVmValue input,
        out BytecodeVmValue value)
    {
        value = BytecodeVmValue.Nothing;
        var instructions = program.Instructions;
        if (instructions.Length != 4 ||
            instructions[0].OpCode != GameEventScriptBytecodeOpCode.LoadSlot ||
            instructions[0].A != parameterSlot ||
            instructions[1].OpCode != GameEventScriptBytecodeOpCode.LoadConstant ||
            instructions[3].OpCode != GameEventScriptBytecodeOpCode.Cast ||
            instructions[3].CastKind != GameEventScriptBytecodeCastKind.Boolean)
        {
            return false;
        }

        var opCode = instructions[2].OpCode;
        if (opCode is not (GameEventScriptBytecodeOpCode.Equal or
                           GameEventScriptBytecodeOpCode.NotEqual or
                           GameEventScriptBytecodeOpCode.ApproxEqual or
                           GameEventScriptBytecodeOpCode.Less or
                           GameEventScriptBytecodeOpCode.Greater or
                           GameEventScriptBytecodeOpCode.LessOrEqual or
                           GameEventScriptBytecodeOpCode.GreaterOrEqual))
        {
            return false;
        }

        if (!TryConsumeExecutionSteps(instructions.Length, "Expression evaluation budget exhausted."))
        {
            return true;
        }

        var right = LoadConstant(instructions[1].ConstantIndex);
        if (input.IsNothingLike() || right.IsNothingLike())
        {
            value = BytecodeVmValue.Boolean(false);
            return true;
        }

        value = opCode switch
        {
            GameEventScriptBytecodeOpCode.Equal => BytecodeVmValue.Boolean(BytecodeVmValue.AreEqual(input, right)),
            GameEventScriptBytecodeOpCode.NotEqual => BytecodeVmValue.Boolean(!BytecodeVmValue.AreEqual(input, right)),
            GameEventScriptBytecodeOpCode.ApproxEqual => BytecodeVmValue.Boolean(BytecodeVmValue.AreApproximatelyEqual(input, right)),
            GameEventScriptBytecodeOpCode.Less => BytecodeVmValue.Boolean(BytecodeVmValue.TryCompareNumeric(input, right, out var comparison) && comparison < 0),
            GameEventScriptBytecodeOpCode.Greater => BytecodeVmValue.Boolean(BytecodeVmValue.TryCompareNumeric(input, right, out var comparison) && comparison > 0),
            GameEventScriptBytecodeOpCode.LessOrEqual => BytecodeVmValue.Boolean(BytecodeVmValue.TryCompareNumeric(input, right, out var comparison) && comparison <= 0),
            GameEventScriptBytecodeOpCode.GreaterOrEqual => BytecodeVmValue.Boolean(BytecodeVmValue.TryCompareNumeric(input, right, out var comparison) && comparison >= 0),
            _ => BytecodeVmValue.Nothing
        };
        return true;
    }

    private bool TryEvaluateCallable(
        in GameEventScriptBytecodeInstruction instruction,
        BytecodeVmValue[] stack,
        int start,
        int count,
        out BytecodeVmValue value)
    {
        value = BytecodeVmValue.Nothing;
        if (instruction.ExpressionProgram is null ||
            instruction.Slots is null ||
            instruction.Names is null ||
            instruction.Slots.Length != count ||
            instruction.Names.Length != count)
        {
            return false;
        }

        var callableName = instruction.DiagnosticName ?? string.Empty;
        if (!_runtimeBudget.TryEnterCall(CallableCallDepthExceededDetail))
        {
            return true;
        }

        EnterScope();
        try
        {
            RecordCallableCalled(instruction, stack, start, count);
            for (var argumentIndex = 0; argumentIndex < count; argumentIndex++)
            {
                var argumentValue = stack[start + argumentIndex];
                if (!TryConvertParameterType(instruction.DeclaredTypes, argumentIndex, argumentValue, out argumentValue))
                {
                    value = BytecodeVmValue.Nothing;
                    return false;
                }

                if (!DefineSlot(instruction.Slots[argumentIndex], argumentValue))
                {
                    value = BytecodeVmValue.Nothing;
                    return false;
                }
            }

            if (!TryExecuteExpressionProgram(instruction.ExpressionProgram, start, out value))
            {
                return false;
            }

            if (instruction.CallableKind == GameEventScriptBytecodeCallableKind.Rule)
            {
                value = BytecodeVmValue.Boolean(value.AsBoolean());
            }

            return true;
        }
        finally
        {
            ExitScope();
            _runtimeBudget.ExitCall();
        }
    }

    private BytecodeVmValue EvaluateTypeConstructor(
        string? typeName,
        string[]? labels,
        BytecodeVmValue[] stack,
        int start,
        int count)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            return BytecodeVmValue.Nothing;
        }

        if (typeName is "vector" or "point")
        {
            return EvaluateSpatialConstructor(typeName, labels, stack, start, count);
        }

        if (TryEvaluateExternalTypeConstructor(typeName, labels, stack, start, count, out var externalValue))
        {
            return externalValue;
        }

        if (_compiledScript.TypeDefinitions.TryGetValue(typeName, out var typeDefinition))
        {
            var values = new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal);
            for (var index = 0; index < count; index++)
            {
                var label = labels is { Length: var labelCount } && index < labelCount
                    ? labels[index]
                    : GameEventScriptMessageSignature.UnlabeledParameterName;
                if (string.Equals(label, GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal))
                {
                    return BytecodeVmValue.Nothing;
                }

                values[label] = stack[start + index].ToGameEventScriptValue();
            }

            return BytecodeVmValue.FromGameEventScriptValue(ConvertToCustomType(GesDictionary(values), typeDefinition));
        }

        if (count != 1 || labels is not { Length: > 0 } || !string.Equals(labels[0], GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal))
        {
            return BytecodeVmValue.Nothing;
        }

        return TryConvertDeclaredType(typeName, stack[start], out var converted)
            ? converted
            : BytecodeVmValue.Nothing;
    }

    private bool TryEvaluateExternalTypeConstructor(
        string typeName,
        string[]? labels,
        BytecodeVmValue[] stack,
        int start,
        int count,
        out BytecodeVmValue value)
    {
        value = BytecodeVmValue.Nothing;
        if (labels is null || labels.Length < count || HasUnlabeledConstructorArgument(labels, count) ||
            !_compiledScript.TryGetBoundExternalTypeConstructor(typeName, labels, out var constructor))
        {
            return false;
        }

        if (constructor.Definition.Parameters.Count != count)
        {
            return true;
        }

        var arguments = new GameEventScriptValue[count];
        for (var parameterIndex = 0; parameterIndex < constructor.Definition.Parameters.Count; parameterIndex++)
        {
            var parameter = constructor.Definition.Parameters[parameterIndex];
            var argumentIndex = IndexOf(labels, parameter.Name, count);
            if (argumentIndex < 0 ||
                string.Equals(labels[argumentIndex], GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal) ||
                !TryConvertDeclaredType(parameter.TypeName, stack[start + argumentIndex], out var converted))
            {
                return true;
            }

            arguments[parameterIndex] = GameEventScriptExternalTypeValueConverter.CoerceToDeclaredType(
                converted.ToGameEventScriptValue(),
                parameter);
        }

        value = BytecodeVmValue.FromGameEventScriptValue(constructor.Invoke(arguments));
        return true;
    }

    private static bool HasUnlabeledConstructorArgument(IReadOnlyList<string> labels, int count)
    {
        for (var index = 0; index < count; index++)
        {
            if (string.Equals(labels[index], GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static int IndexOf(IReadOnlyList<string> labels, string label, int count)
    {
        for (var index = 0; index < count; index++)
        {
            if (string.Equals(labels[index], label, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private BytecodeVmValue EvaluateSpatialConstructor(
        string typeName,
        string[]? labels,
        BytecodeVmValue[] stack,
        int start,
        int count)
    {
        if (count == 1 &&
            labels is { Length: > 0 } &&
            string.Equals(labels[0], GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal))
        {
            return TryConvertDeclaredType(typeName, stack[start], out var converted)
                ? converted
                : BytecodeVmValue.Nothing;
        }

        if (count == 2 &&
            labels is { Length: >= 2 } &&
            string.Equals(labels[0], GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal) &&
            string.Equals(labels[1], GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal) &&
            TryCreateSpatialLift(
                typeName,
                stack[start].ToGameEventScriptValue(),
                stack[start + 1].ToGameEventScriptValue(),
                out var lifted))
        {
            return BytecodeVmValue.FromGameEventScriptValue(lifted);
        }

        if (count == 0 || labels is { Length: var labelCount } &&
            labelCount >= count &&
            Enumerable.Range(0, count).All(index => !string.Equals(labels[index], GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal)))
        {
            var labeledComponents = new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal);
            for (var index = 0; index < count; index++)
            {
                labeledComponents[labels![index]] = stack[start + index].ToGameEventScriptValue();
            }

            return TryCreateSpatialFromLabeledComponents(typeName, labeledComponents, out var spatialValue)
                ? BytecodeVmValue.FromGameEventScriptValue(spatialValue)
                : BytecodeVmValue.Nothing;
        }

        if (count is < 1 or > 3)
        {
            return BytecodeVmValue.Nothing;
        }

        var components = new GameEventScriptValue[count];
        for (var index = 0; index < components.Length; index++)
        {
            components[index] = stack[start + index].ToGameEventScriptValue();
        }

        return TryCreateSpatialFromComponents(typeName, components, out var spatial)
            ? BytecodeVmValue.FromGameEventScriptValue(spatial)
            : BytecodeVmValue.Nothing;
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

    private bool TryConvertDeclaredType(string declaredType, BytecodeVmValue input, out BytecodeVmValue value)
    {
        if (TryConvertPrimitiveDeclaredType(declaredType, input, out value))
        {
            return true;
        }

        var boxed = input.ToGameEventScriptValue();
        value = declaredType switch
        {
            "tag" => BytecodeVmValue.Reference(GesTag(boxed.AsText())),
            "text" => BytecodeVmValue.Reference(GesText(GesValueOperations.ToText(boxed))),
            "percentage" => BytecodeVmValue.FromGameEventScriptValue(ConvertToPercentage(boxed)),
            "degree" => BytecodeVmValue.FromGameEventScriptValue(ConvertToNumericUnit(boxed, GameEventScriptNumericUnit.Degree)),
            "meter" => BytecodeVmValue.FromGameEventScriptValue(ConvertToNumericUnit(boxed, GameEventScriptNumericUnit.Meter)),
            "second" => BytecodeVmValue.FromGameEventScriptValue(ConvertToNumericUnit(boxed, GameEventScriptNumericUnit.Second)),
            "vector" => BytecodeVmValue.Reference(ConvertToVector(boxed)),
            "point" => BytecodeVmValue.Reference(ConvertToPoint(boxed)),
            "integer" => BytecodeVmValue.Integer(boxed.AsInteger()),
            "float" or "number" => BytecodeVmValue.FromGameEventScriptValue(ConvertToFloat(boxed)),
            "sequence" => BytecodeVmValue.Reference(boxed.IsSequence() ? boxed : GameEventScriptValueFactory.GesSequence(boxed.AsEnumerable())),
            "list" => TryCheckMaterializedValue(boxed, "List conversion would materialize more range items than allowed.")
                ? BytecodeVmValue.Reference(GesList(boxed.AsList()))
                : BytecodeVmValue.Nothing,
            "range" => BytecodeVmValue.Reference(boxed.IsRange() ? boxed : GameEventScriptNothingValue.Instance),
            "message" => boxed.Kind == GameEventScriptValueKind.Message
                ? input
                : GesMessageValueCodec.TryReadMessageValue(boxed, out var message)
                    ? BytecodeVmValue.Reference(GesMessageValueCodec.CreateMessageValue(message))
                    : BytecodeVmValue.Nothing,
            "handler" => boxed.Kind == GameEventScriptValueKind.Handler
                ? input
                : GesMessageValueCodec.TryReadHandlerValue(boxed, out var handler)
                    ? BytecodeVmValue.Reference(GesMessageValueCodec.CreateHandlerValue(handler))
                    : BytecodeVmValue.Nothing,
            "dictionary" => BytecodeVmValue.Reference(GesDictionary(boxed.AsDictionary())),
            "set" => TryCheckMaterializedValue(boxed, "Set conversion would materialize more range items than allowed.")
                ? BytecodeVmValue.Reference(GesSet(boxed.AsSet()))
                : BytecodeVmValue.Nothing,
            "dice" => BytecodeVmValue.Reference(GesDice(boxed.AsDice())),
            "optional" => boxed.IsOptional()
                ? input
                : boxed.IsNothing()
                    ? BytecodeVmValue.Reference(GesOptionalNone())
                    : BytecodeVmValue.Reference(GesOptionalSome(boxed)),
            _ => _compiledScript.TypeDefinitions.TryGetValue(declaredType, out var typeDefinition)
                ? BytecodeVmValue.FromGameEventScriptValue(ConvertToCustomType(boxed, typeDefinition))
                : boxed.TryGetCustomTypeName(out var customTypeName) &&
                  string.Equals(customTypeName, declaredType, StringComparison.Ordinal)
                    ? input
                    : BytecodeVmValue.Nothing
        };

        return true;
    }

    private static bool TryConvertPrimitiveDeclaredType(string declaredType, BytecodeVmValue input, out BytecodeVmValue value)
    {
        switch (declaredType)
        {
            case "nothing":
                value = BytecodeVmValue.Nothing;
                return true;
            case "boolean":
                value = BytecodeVmValue.Boolean(input.AsBoolean());
                return true;
            case "integer" when input.Kind == BytecodeVmValueKind.Integer:
                value = input.Unit.HasValue ? BytecodeVmValue.Integer(input.IntegerValue) : input;
                return true;
            case "integer" when input.Kind == BytecodeVmValueKind.Boolean:
                value = BytecodeVmValue.Integer(input.BooleanValue ? 1 : 0);
                return true;
            case "integer" when input.Kind == BytecodeVmValueKind.Float:
                value = BytecodeVmValue.Integer(ToLongSaturated(input.Number));
                return true;
            case "integer" when input.Kind == BytecodeVmValueKind.Percentage:
                value = BytecodeVmValue.Integer(GameEventScriptValue.ToIntegerPercentage(input.Number));
                return true;
            case "float":
            case "number":
                return TryConvertPrimitiveToFloat(input, out value);
            case "percentage":
                return TryConvertPrimitiveToPercentage(input, out value);
            case "degree":
                return TryConvertPrimitiveToNumericUnit(input, GameEventScriptNumericUnit.Degree, out value);
            case "meter":
                return TryConvertPrimitiveToNumericUnit(input, GameEventScriptNumericUnit.Meter, out value);
            case "second":
                return TryConvertPrimitiveToNumericUnit(input, GameEventScriptNumericUnit.Second, out value);
            default:
                value = input;
                return false;
        }
    }

    private static bool TryConvertPrimitiveToFloat(BytecodeVmValue input, out BytecodeVmValue value)
    {
        value = input.Kind switch
        {
            BytecodeVmValueKind.Integer => BytecodeVmValue.Float(input.IntegerValue),
            BytecodeVmValueKind.Boolean => BytecodeVmValue.Float(input.BooleanValue ? 1d : 0d),
            BytecodeVmValueKind.Float => input.Unit.HasValue ? BytecodeVmValue.Float(input.Number) : input,
            BytecodeVmValueKind.Percentage => BytecodeVmValue.Float(input.Number),
            _ => input
        };

        return input.Kind is BytecodeVmValueKind.Integer
            or BytecodeVmValueKind.Boolean
            or BytecodeVmValueKind.Float
            or BytecodeVmValueKind.Percentage;
    }

    private static bool TryConvertPrimitiveToPercentage(BytecodeVmValue input, out BytecodeVmValue value)
    {
        switch (input.Kind)
        {
            case BytecodeVmValueKind.Percentage:
                value = input;
                return true;
            case BytecodeVmValueKind.Integer:
                if (input.Unit.HasValue)
                {
                    value = BytecodeVmValue.NaN();
                    return true;
                }

                value = BytecodeVmValue.Percentage(input.IntegerValue / 100d);
                return true;
            case BytecodeVmValueKind.Boolean:
                value = BytecodeVmValue.Percentage(input.BooleanValue ? 1d : 0d);
                return true;
            case BytecodeVmValueKind.Float when input.Unit.HasValue:
                value = BytecodeVmValue.NaN();
                return true;
            case BytecodeVmValueKind.Float:
                var number = input.Number;
                value = BytecodeVmValue.Percentage(number > 1d || number < -1d ? number / 100d : number);
                return true;
            default:
                value = input;
                return false;
        }
    }

    private static bool TryConvertPrimitiveToNumericUnit(BytecodeVmValue input, GameEventScriptNumericUnit unit, out BytecodeVmValue value)
    {
        switch (input.Kind)
        {
            case BytecodeVmValueKind.Integer:
                value = input.Unit.HasValue && input.Unit != unit
                    ? BytecodeVmValue.NaN()
                    : BytecodeVmValue.Integer(input.IntegerValue, unit);
                return true;
            case BytecodeVmValueKind.Float when input.Unit.HasValue && input.Unit != unit:
                value = BytecodeVmValue.NaN();
                return true;
            case BytecodeVmValueKind.Float:
                value = BytecodeVmValue.Float(input.Number, unit);
                return true;
            default:
                value = input;
                return false;
        }
    }

    private bool TryConvertParameterType(IReadOnlyList<string?>? declaredTypes, int index, BytecodeVmValue input, out BytecodeVmValue value)
    {
        value = input;
        if (!TryGetParameterType(declaredTypes, index, out var declaredType))
        {
            return true;
        }

        return TryConvertDeclaredType(declaredType, input, out value);
    }

    private static bool HasParameterType(IReadOnlyList<string?>? declaredTypes, int index)
        => TryGetParameterType(declaredTypes, index, out _);

    private static bool TryGetParameterType(IReadOnlyList<string?>? declaredTypes, int index, out string declaredType)
    {
        declaredType = string.Empty;
        if (declaredTypes is null ||
            index < 0 ||
            index >= declaredTypes.Count ||
            string.IsNullOrEmpty(declaredTypes[index]))
        {
            return false;
        }

        declaredType = declaredTypes[index]!;
        return true;
    }

    private bool TryCheckMaterializedValue(GameEventScriptValue value, string detail)
        => !GesRuntimeLimitUtilities.TryGetRangeLength(value, out var length) ||
           _runtimeBudget.TryCheckRangeLength(length, detail);

    private static string GetCastTypeName(GameEventScriptBytecodeCastKind castKind)
        => castKind switch
        {
            GameEventScriptBytecodeCastKind.Boolean => "boolean",
            GameEventScriptBytecodeCastKind.Integer => "integer",
            GameEventScriptBytecodeCastKind.Float => "float",
            GameEventScriptBytecodeCastKind.Number => "number",
            GameEventScriptBytecodeCastKind.Percentage => "percentage",
            GameEventScriptBytecodeCastKind.Degree => "degree",
            GameEventScriptBytecodeCastKind.Meter => "meter",
            GameEventScriptBytecodeCastKind.Second => "second",
            GameEventScriptBytecodeCastKind.Vector => "vector",
            GameEventScriptBytecodeCastKind.Point => "point",
            GameEventScriptBytecodeCastKind.Sequence => "sequence",
            _ => string.Empty
        };

    private static GameEventScriptValue ConvertToFloat(GameEventScriptValue value)
    {
        if (!GesValueOperations.TryUnwrapOptionalForOperation(value, out var unwrappedNumber))
        {
            return GesFloatNaN();
        }

        if (GesValueOperations.TryEraseVectorUnit(unwrappedNumber, out var vectorWithoutUnit))
        {
            return vectorWithoutUnit;
        }

        if (unwrappedNumber is GameEventScriptTagValue && unwrappedNumber.TryConvertToNumber(out var convertedTag))
        {
            return ConvertToFloat(convertedTag);
        }

        return GesValueOperations.TryCoerceNumericForOperation(unwrappedNumber, out var number)
            ? GesValueOperations.ToGameEventScriptFloat(number)
            : GesFloatNaN();
    }

    private static GameEventScriptValue ConvertToPercentage(GameEventScriptValue value)
    {
        if (!GesValueOperations.TryUnwrapOptionalForOperation(value, out var unwrapped))
        {
            return GesFloatNaN();
        }

        if (unwrapped.IsPercentage())
        {
            return unwrapped;
        }

        if (unwrapped.HasNumericUnit())
        {
            return GesFloatNaN();
        }

        if (!GesValueOperations.TryCoerceNumericForOperation(unwrapped, out var number) ||
            !number.IsFinite)
        {
            return GesFloatNaN();
        }

        var ratio = unwrapped.Kind == GameEventScriptValueKind.Integer
            ? number.Value / 100d
            : number.Value > 1d || number.Value < -1d
                ? number.Value / 100d
                : number.Value;
        return GesPercentage(ratio);
    }

    private static GameEventScriptValue ConvertToNumericUnit(GameEventScriptValue value, GameEventScriptNumericUnit unit)
    {
        if (!GesValueOperations.TryUnwrapOptionalForOperation(value, out var unwrapped))
        {
            return GesFloatNaN();
        }

        if (GesValueOperations.TryApplyVectorUnit(unwrapped, unit, out var vectorWithUnit))
        {
            return vectorWithUnit;
        }

        if (GameEventScriptValue.TryGetNumericUnit(unwrapped, out var existingUnit) && existingUnit != unit)
        {
            return GesFloatNaN();
        }

        if (unwrapped.Kind == GameEventScriptValueKind.Integer &&
            GesValueOperations.TryCoerceNumericForOperation(unwrapped, out var integerNumber) &&
            integerNumber.IsFinite)
        {
            return GesInteger(GesValueOperations.ToIntegerSaturated(integerNumber.Value), unit);
        }

        if (unwrapped.Kind == GameEventScriptValueKind.Float &&
            GesValueOperations.TryCoerceNumericForOperation(unwrapped, out var number) &&
            number.IsFinite)
        {
            return GesFloat(number.Value, unit);
        }

        return GesFloatNaN();
    }

    private static GameEventScriptValue ConvertToVector(GameEventScriptValue value)
    {
        if (!GesValueOperations.TryUnwrapOptionalForOperation(value, out var unwrapped))
        {
            return GameEventScriptNothingValue.Instance;
        }

        if (unwrapped is GameEventScriptVectorValue vector)
        {
            return vector;
        }

        if (unwrapped is GameEventScriptPointValue)
        {
            return GameEventScriptNothingValue.Instance;
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
        if (!GesValueOperations.TryUnwrapOptionalForOperation(value, out var unwrapped))
        {
            return GameEventScriptNothingValue.Instance;
        }

        if (unwrapped is GameEventScriptPointValue point)
        {
            return point;
        }

        if (unwrapped is GameEventScriptVectorValue)
        {
            return GameEventScriptNothingValue.Instance;
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
        var hasX = value.TryGetDictionaryMember("x", out var x);
        var hasY = value.TryGetDictionaryMember("y", out var y);
        var hasZ = value.TryGetDictionaryMember("z", out var z);

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

    private GameEventScriptValue ConvertToCustomType(GameEventScriptValue value, GameEventScriptBytecodeTypeDefinition typeDefinition)
    {
        if (value.TryGetCustomTypeName(out var existingTypeName) &&
            string.Equals(existingTypeName, typeDefinition.Name, StringComparison.Ordinal))
        {
            return value;
        }

        var sourceValues = value.AsDictionary().ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var materializedValues = new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal);

        foreach (var field in typeDefinition.Fields.Where(field => field.ComputedProgram is null))
        {
            sourceValues.TryGetValue(field.Name, out var rawValue);
            rawValue ??= GameEventScriptNothingValue.Instance;

            var fieldValue = ConvertValueToDeclaredType(rawValue, field.TypeName);
            fieldValue = ApplyFieldClamp(typeDefinition, field, fieldValue, sourceValues, materializedValues);
            fieldValue = ConvertValueToDeclaredType(fieldValue, field.TypeName);
            materializedValues[field.Name] = fieldValue;
        }

        foreach (var field in typeDefinition.Fields.Where(field => field.ComputedProgram is not null))
        {
            var computedValue = EvaluateCustomTypeExpression(field.ComputedProgram!, sourceValues, materializedValues);
            materializedValues[field.Name] = ConvertValueToDeclaredType(computedValue, field.TypeName);
        }

        return GameEventScriptValueFactory.GesCustomType(typeDefinition.Name, materializedValues);
    }

    private GameEventScriptValue ConvertValueToDeclaredType(GameEventScriptValue value, string declaredType)
        => TryConvertDeclaredType(declaredType, BytecodeVmValue.FromGameEventScriptValue(value), out var converted)
            ? converted.ToGameEventScriptValue()
            : GameEventScriptNothingValue.Instance;

    private GameEventScriptValue ApplyFieldClamp(
        GameEventScriptBytecodeTypeDefinition typeDefinition,
        GameEventScriptBytecodeTypeFieldDefinition field,
        GameEventScriptValue fieldValue,
        IReadOnlyDictionary<string, GameEventScriptValue> sourceValues,
        IReadOnlyDictionary<string, GameEventScriptValue> materializedValues)
    {
        _ = typeDefinition;
        if (field.MinimumProgram is null || field.MaximumProgram is null)
        {
            return fieldValue;
        }

        var minimum = EvaluateCustomTypeExpression(field.MinimumProgram, sourceValues, materializedValues);
        var maximum = EvaluateCustomTypeExpression(field.MaximumProgram, sourceValues, materializedValues);
        if (!GesValueOperations.HaveCompatibleNumericUnits(fieldValue, minimum) ||
            !GesValueOperations.HaveCompatibleNumericUnits(fieldValue, maximum) ||
            !GesValueOperations.HaveCompatibleNumericUnits(minimum, maximum))
        {
            return GameEventScriptValueFactory.GesFloatNaN();
        }

        if (!GesValueOperations.TryCoerceNumericForOperation(fieldValue, out var valueNumber) ||
            !GesValueOperations.TryCoerceNumericForOperation(minimum, out var minimumNumber) ||
            !GesValueOperations.TryCoerceNumericForOperation(maximum, out var maximumNumber))
        {
            return fieldValue;
        }

        if (!valueNumber.IsFinite || !minimumNumber.IsFinite || !maximumNumber.IsFinite)
        {
            if (maximumNumber.IsPositiveInfinity && minimumNumber.IsFinite && valueNumber.IsFinite)
            {
                return GameEventScriptValueFactory.GesFloat(Math.Max(valueNumber.Value, minimumNumber.Value));
            }

            return fieldValue;
        }

        var lower = Math.Min(minimumNumber.Value, maximumNumber.Value);
        var upper = Math.Max(minimumNumber.Value, maximumNumber.Value);
        GameEventScriptValue.TryGetNumericUnit(fieldValue, out var unit);
        return GameEventScriptValueFactory.GesFloat(Math.Min(Math.Max(valueNumber.Value, lower), upper), fieldValue.HasNumericUnit() ? unit : null);
    }

    private GameEventScriptValue EvaluateCustomTypeExpression(
        GameEventScriptBytecodeExpressionProgram expressionProgram,
        IReadOnlyDictionary<string, GameEventScriptValue> sourceValues,
        IReadOnlyDictionary<string, GameEventScriptValue> materializedValues)
    {
        EnterScope();
        try
        {
            foreach (var pair in sourceValues)
            {
                if (!Define(pair.Key, BytecodeVmValue.FromGameEventScriptValue(pair.Value)))
                {
                    return GameEventScriptNothingValue.Instance;
                }
            }

            foreach (var pair in materializedValues)
            {
                if (!Define(pair.Key, BytecodeVmValue.FromGameEventScriptValue(pair.Value)))
                {
                    return GameEventScriptNothingValue.Instance;
                }
            }

            return TryExecuteExpressionProgram(expressionProgram, 0, out var value)
                ? value.ToGameEventScriptValue()
                : GameEventScriptNothingValue.Instance;
        }
        finally
        {
            ExitScope();
        }
    }

    private bool TryExecutePipelineProgram(GameEventScriptBytecodePipelineProgram pipeline, out BytecodeVmValue value)
    {
        if (!TryExecuteExpressionProgram(pipeline.SourceProgram, 0, out var sourceValue))
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        var sourceTarget = sourceValue.ToGameEventScriptValue();
        if (!TryCheckMaterializedValue(sourceTarget, "Collection access would enumerate more range items than allowed."))
        {
            value = BytecodeVmValue.Nothing;
            return true;
        }

        var sourceItems = MaterializeListLikeValue(sourceTarget);
        var terminalTarget = pipeline.PrefixSelectors.Length == 0
            ? sourceTarget
            : GameEventScriptListValue.Empty;
        return pipeline.TerminalSelector.Kind switch
        {
            GameEventScriptBytecodeSelectorKind.Filter => TryExecuteProgramFilter(sourceItems, pipeline, out value),
            GameEventScriptBytecodeSelectorKind.Select => TryExecuteProgramSelect(sourceItems, pipeline, out value),
            GameEventScriptBytecodeSelectorKind.Predicate => TryExecuteProgramPredicate(sourceItems, pipeline, out value),
            GameEventScriptBytecodeSelectorKind.Sum => TryExecuteProgramSum(sourceItems, pipeline, out value),
            GameEventScriptBytecodeSelectorKind.Average => TryExecuteProgramAverage(sourceItems, pipeline, out value),
            GameEventScriptBytecodeSelectorKind.Count => TryExecuteProgramCount(sourceItems, pipeline, out value),
            GameEventScriptBytecodeSelectorKind.Edge => TryExecuteProgramEdge(sourceItems, pipeline, out value),
            GameEventScriptBytecodeSelectorKind.Pattern => TryMaterializePipelineItems(sourceItems, pipeline, out var patternItems, out value)
                ? TryExecuteProgramPattern(terminalTarget, patternItems, pipeline.TerminalSelector, out value)
                : false,
            GameEventScriptBytecodeSelectorKind.ObjectMatch => TryMaterializePipelineItems(sourceItems, pipeline, out var objectItems, out value)
                ? TryExecuteProgramObjectMatch(terminalTarget, objectItems, pipeline.TerminalSelector, out value)
                : false,
            GameEventScriptBytecodeSelectorKind.TakePattern => TryMaterializePipelineItems(sourceItems, pipeline, out var takePatternItems, out value)
                ? TryExecuteProgramTakePattern(terminalTarget, takePatternItems, pipeline.TerminalSelector, out value)
                : false,
            GameEventScriptBytecodeSelectorKind.Min => TryExecuteProgramExtrema(sourceItems, pipeline, isMax: false, out value),
            GameEventScriptBytecodeSelectorKind.Max => TryExecuteProgramExtrema(sourceItems, pipeline, isMax: true, out value),
            GameEventScriptBytecodeSelectorKind.Dictionary => TryExecuteProgramDictionary(sourceItems, pipeline, out value),
            GameEventScriptBytecodeSelectorKind.Contains => TryExecuteProgramContains(sourceItems, terminalTarget, pipeline, out value),
            GameEventScriptBytecodeSelectorKind.Choose => TryMaterializePipelineItems(sourceItems, pipeline, out var chooseItems, out value)
                ? TryExecuteProgramChoose(chooseItems, pipeline.TerminalSelector, out value)
                : false,
            GameEventScriptBytecodeSelectorKind.Draw => TryMaterializePipelineItems(sourceItems, pipeline, out var drawItems, out value)
                ? SetValue(BytecodeVmValue.FromGameEventScriptValue(EvaluateDrawSelector(terminalTarget, drawItems, pipeline.TerminalSelector.Count)), out value)
                : false,
            GameEventScriptBytecodeSelectorKind.Shuffle => TryMaterializePipelineItems(sourceItems, pipeline, out var shuffleItems, out value)
                ? SetValue(BytecodeVmValue.FromGameEventScriptValue(EvaluateShuffleSelector(terminalTarget, shuffleItems)), out value)
                : false,
            GameEventScriptBytecodeSelectorKind.Sort => TryMaterializePipelineItems(sourceItems, pipeline, out var sortItems, out value)
                ? SetValue(BytecodeVmValue.FromGameEventScriptValue(GesCollectionOperations.Sort(terminalTarget, sortItems, pipeline.TerminalSelector.EdgeMode ?? "ascending")), out value)
                : false,
            GameEventScriptBytecodeSelectorKind.Distinct => TryExecuteProgramDistinct(sourceItems, terminalTarget, pipeline, out value),
            GameEventScriptBytecodeSelectorKind.GroupBy => TryExecuteProgramGroupBy(sourceItems, pipeline, out value),
            GameEventScriptBytecodeSelectorKind.OrderBy => TryExecuteProgramOrderBy(sourceItems, terminalTarget, pipeline, out value),
            GameEventScriptBytecodeSelectorKind.Reverse => TryMaterializePipelineItems(sourceItems, pipeline, out var reverseItems, out value)
                ? SetValue(BytecodeVmValue.FromGameEventScriptValue(EvaluateReverseSelector(terminalTarget, reverseItems)), out value)
                : false,
            GameEventScriptBytecodeSelectorKind.SequenceSlice => TryMaterializePipelineItems(sourceItems, pipeline, out var sliceItems, out value)
                ? SetValue(BytecodeVmValue.FromGameEventScriptValue(EvaluateSequenceSliceSelector(terminalTarget, sliceItems, pipeline.TerminalSelector)), out value)
                : false,
            _ => Fail(out value)
        };
    }

    private bool TryExecuteProgramFilter(
        IReadOnlyList<GameEventScriptValue> sourceItems,
        GameEventScriptBytecodePipelineProgram pipeline,
        out BytecodeVmValue value)
    {
        var terminal = pipeline.TerminalSelector;
        if (terminal.ExpressionProgram is null)
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        var result = new List<GameEventScriptValue>();
        if (!TryForEachIncludedPipelineItem(sourceItems, pipeline.PrefixSelectors, item =>
            {
                if (!TryEvaluateProgramProjection(terminal.IdentifierSlot, terminal.ExpressionProgram, item, out var predicate))
                {
                    return false;
                }

                if (predicate.AsBoolean())
                {
                    result.Add(item.ToGameEventScriptValue());
                }

                return true;
            }))
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        value = BytecodeVmValue.Reference(GameEventScriptValueFactory.GesList(result));
        return true;
    }

    private bool TryExecuteProgramSelect(
        IReadOnlyList<GameEventScriptValue> sourceItems,
        GameEventScriptBytecodePipelineProgram pipeline,
        out BytecodeVmValue value)
    {
        var terminal = pipeline.TerminalSelector;
        if (terminal.ExpressionProgram is null)
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        var result = new List<GameEventScriptValue>();
        if (!TryForEachIncludedPipelineItem(sourceItems, pipeline.PrefixSelectors, item =>
            {
                if (!TryEvaluateProgramProjection(terminal.IdentifierSlot, terminal.ExpressionProgram, item, out var selected))
                {
                    return false;
                }

                result.Add(selected.ToGameEventScriptValue());
                return true;
            }))
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        value = BytecodeVmValue.Reference(GameEventScriptValueFactory.GesList(result));
        return true;
    }

    private bool TryExecuteProgramPredicate(
        IReadOnlyList<GameEventScriptValue> sourceItems,
        GameEventScriptBytecodePipelineProgram pipeline,
        out BytecodeVmValue value)
    {
        var terminal = pipeline.TerminalSelector;
        if (terminal.ExpressionProgram is null)
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        var isAny = string.Equals(terminal.EdgeMode, "any", StringComparison.Ordinal);
        if (!isAny && !string.Equals(terminal.EdgeMode, "all", StringComparison.Ordinal))
        {
            value = BytecodeVmValue.Boolean(false);
            return true;
        }

        var result = !isAny;
        if (!TryForEachIncludedPipelineItem(sourceItems, pipeline.PrefixSelectors, item =>
            {
                if (!TryEvaluateProgramProjection(terminal.IdentifierSlot, terminal.ExpressionProgram, item, out var predicate))
                {
                    return false;
                }

                if (isAny && predicate.AsBoolean())
                {
                    result = true;
                    return true;
                }

                if (!isAny && !predicate.AsBoolean())
                {
                    result = false;
                    return true;
                }

                return true;
            }, stopWhen: () => isAny ? result : !result))
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        value = BytecodeVmValue.Boolean(result);
        return true;
    }

    private bool TryExecuteProgramSum(
        IReadOnlyList<GameEventScriptValue> sourceItems,
        GameEventScriptBytecodePipelineProgram pipeline,
        out BytecodeVmValue value)
    {
        var terminal = pipeline.TerminalSelector;
        if (terminal.ExpressionProgram is null)
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        var hasValue = false;
        var sum = BytecodeVmValue.Float(0d);
        var prefixSelectors = pipeline.PrefixSelectors;
        var terminalIsIdentity = IsIdentityProjection(terminal.IdentifierSlot, terminal.ExpressionProgram);
        for (var itemIndex = 0; itemIndex < sourceItems.Count; itemIndex++)
        {
            var item = BytecodeVmValue.FromGameEventScriptValue(sourceItems[itemIndex]);
            var include = true;
            for (var selectorIndex = 0; selectorIndex < prefixSelectors.Length; selectorIndex++)
            {
                var selector = prefixSelectors[selectorIndex];
                if (selector.ExpressionProgram is null)
                {
                    value = BytecodeVmValue.Nothing;
                    return false;
                }

                if (selector.Kind == GameEventScriptBytecodeSelectorKind.Filter)
                {
                    if (!TryEvaluateProgramProjection(selector.IdentifierSlot, selector.ExpressionProgram, item, out var prefixPredicate))
                    {
                        value = BytecodeVmValue.Nothing;
                        return false;
                    }

                    if (!prefixPredicate.AsBoolean())
                    {
                        include = false;
                        break;
                    }

                    continue;
                }

                if (selector.Kind == GameEventScriptBytecodeSelectorKind.Select)
                {
                    if (!TryEvaluateProgramProjection(selector.IdentifierSlot, selector.ExpressionProgram, item, out item))
                    {
                        value = BytecodeVmValue.Nothing;
                        return false;
                    }

                    continue;
                }

                value = BytecodeVmValue.Nothing;
                return false;
            }

            if (!include)
            {
                continue;
            }

            BytecodeVmValue projected;
            if (terminalIsIdentity)
            {
                if (!TryConsumeExecutionStep("Expression evaluation budget exhausted."))
                {
                    value = BytecodeVmValue.Nothing;
                    return true;
                }

                projected = item;
            }
            else if (!TryEvaluatePipelineTerminalProjection(terminal.IdentifierSlot, terminal.ExpressionProgram, item, out projected))
            {
                value = BytecodeVmValue.Nothing;
                return false;
            }

            sum = hasValue ? BytecodeVmValue.Add(sum, projected) : projected;
            hasValue = true;
        }

        value = hasValue ? sum : BytecodeVmValue.Float(0d);
        return true;
    }

    private bool TryExecuteProgramAverage(
        IReadOnlyList<GameEventScriptValue> sourceItems,
        GameEventScriptBytecodePipelineProgram pipeline,
        out BytecodeVmValue value)
    {
        var terminal = pipeline.TerminalSelector;
        if (terminal.ExpressionProgram is null)
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        var count = 0L;
        var sum = BytecodeVmValue.Float(0d);
        var prefixSelectors = pipeline.PrefixSelectors;
        var terminalIsIdentity = IsIdentityProjection(terminal.IdentifierSlot, terminal.ExpressionProgram);
        for (var itemIndex = 0; itemIndex < sourceItems.Count; itemIndex++)
        {
            var item = BytecodeVmValue.FromGameEventScriptValue(sourceItems[itemIndex]);
            var include = true;
            for (var selectorIndex = 0; selectorIndex < prefixSelectors.Length; selectorIndex++)
            {
                var selector = prefixSelectors[selectorIndex];
                if (selector.ExpressionProgram is null)
                {
                    value = BytecodeVmValue.Nothing;
                    return false;
                }

                if (selector.Kind == GameEventScriptBytecodeSelectorKind.Filter)
                {
                    if (!TryEvaluateProgramProjection(selector.IdentifierSlot, selector.ExpressionProgram, item, out var prefixPredicate))
                    {
                        value = BytecodeVmValue.Nothing;
                        return false;
                    }

                    if (!prefixPredicate.AsBoolean())
                    {
                        include = false;
                        break;
                    }

                    continue;
                }

                if (selector.Kind == GameEventScriptBytecodeSelectorKind.Select)
                {
                    if (!TryEvaluateProgramProjection(selector.IdentifierSlot, selector.ExpressionProgram, item, out item))
                    {
                        value = BytecodeVmValue.Nothing;
                        return false;
                    }

                    continue;
                }

                value = BytecodeVmValue.Nothing;
                return false;
            }

            if (!include)
            {
                continue;
            }

            BytecodeVmValue projected;
            if (terminalIsIdentity)
            {
                if (!TryConsumeExecutionStep("Expression evaluation budget exhausted."))
                {
                    value = BytecodeVmValue.Nothing;
                    return true;
                }

                projected = item;
            }
            else if (!TryEvaluatePipelineTerminalProjection(terminal.IdentifierSlot, terminal.ExpressionProgram, item, out projected))
            {
                value = BytecodeVmValue.Nothing;
                return false;
            }

            sum = count == 0 ? projected : BytecodeVmValue.Add(sum, projected);
            count++;
        }

        value = count > 0 && sum.TryGetFiniteNumber(out var number)
            ? BytecodeVmValue.Float(number / count, sum.Unit)
            : BytecodeVmValue.Nothing;
        return true;
    }

    private bool TryExecuteProgramCount(
        IReadOnlyList<GameEventScriptValue> sourceItems,
        GameEventScriptBytecodePipelineProgram pipeline,
        out BytecodeVmValue value)
    {
        var terminal = pipeline.TerminalSelector;
        if (terminal.ExpressionProgram is null)
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        var count = 0L;
        var prefixSelectors = pipeline.PrefixSelectors;
        var terminalIsTrue = IsConstantTrueProjection(terminal.ExpressionProgram);
        for (var itemIndex = 0; itemIndex < sourceItems.Count; itemIndex++)
        {
            var item = BytecodeVmValue.FromGameEventScriptValue(sourceItems[itemIndex]);
            var include = true;
            for (var selectorIndex = 0; selectorIndex < prefixSelectors.Length; selectorIndex++)
            {
                var selector = prefixSelectors[selectorIndex];
                if (selector.ExpressionProgram is null)
                {
                    value = BytecodeVmValue.Nothing;
                    return false;
                }

                if (selector.Kind == GameEventScriptBytecodeSelectorKind.Filter)
                {
                    if (!TryEvaluateProgramProjection(selector.IdentifierSlot, selector.ExpressionProgram, item, out var prefixPredicate))
                    {
                        value = BytecodeVmValue.Nothing;
                        return false;
                    }

                    if (!prefixPredicate.AsBoolean())
                    {
                        include = false;
                        break;
                    }

                    continue;
                }

                if (selector.Kind == GameEventScriptBytecodeSelectorKind.Select)
                {
                    if (!TryEvaluateProgramProjection(selector.IdentifierSlot, selector.ExpressionProgram, item, out item))
                    {
                        value = BytecodeVmValue.Nothing;
                        return false;
                    }

                    continue;
                }

                value = BytecodeVmValue.Nothing;
                return false;
            }

            if (!include)
            {
                continue;
            }

            BytecodeVmValue predicate;
            if (terminalIsTrue)
            {
                if (!TryConsumeExecutionStep("Expression evaluation budget exhausted."))
                {
                    value = BytecodeVmValue.Nothing;
                    return true;
                }

                count++;
                continue;
            }

            if (!TryEvaluatePipelineTerminalProjection(terminal.IdentifierSlot, terminal.ExpressionProgram, item, out predicate))
            {
                value = BytecodeVmValue.Nothing;
                return false;
            }

            if (predicate.AsBoolean())
            {
                count++;
            }
        }

        value = BytecodeVmValue.Integer(count);
        return true;
    }

    private bool TryExecuteProgramEdge(
        IReadOnlyList<GameEventScriptValue> sourceItems,
        GameEventScriptBytecodePipelineProgram pipeline,
        out BytecodeVmValue value)
    {
        var terminal = pipeline.TerminalSelector;
        BytecodeVmValue first = BytecodeVmValue.Nothing;
        BytecodeVmValue last = BytecodeVmValue.Nothing;
        var count = 0;
        var prefixSelectors = pipeline.PrefixSelectors;
        for (var itemIndex = 0; itemIndex < sourceItems.Count; itemIndex++)
        {
            var item = BytecodeVmValue.FromGameEventScriptValue(sourceItems[itemIndex]);
            var include = true;
            for (var selectorIndex = 0; selectorIndex < prefixSelectors.Length; selectorIndex++)
            {
                var selector = prefixSelectors[selectorIndex];
                if (selector.ExpressionProgram is null)
                {
                    value = BytecodeVmValue.Nothing;
                    return false;
                }

                if (selector.Kind == GameEventScriptBytecodeSelectorKind.Filter)
                {
                    if (!TryEvaluateProgramProjection(selector.IdentifierSlot, selector.ExpressionProgram, item, out var prefixPredicate))
                    {
                        value = BytecodeVmValue.Nothing;
                        return false;
                    }

                    if (!prefixPredicate.AsBoolean())
                    {
                        include = false;
                        break;
                    }

                    continue;
                }

                if (selector.Kind == GameEventScriptBytecodeSelectorKind.Select)
                {
                    if (!TryEvaluateProgramProjection(selector.IdentifierSlot, selector.ExpressionProgram, item, out item))
                    {
                        value = BytecodeVmValue.Nothing;
                        return false;
                    }

                    continue;
                }

                value = BytecodeVmValue.Nothing;
                return false;
            }

            if (!include)
            {
                continue;
            }

            if (terminal.ExpressionProgram is not null &&
                (!TryEvaluatePipelineTerminalProjection(terminal.IdentifierSlot, terminal.ExpressionProgram, item, out var predicate) || !predicate.AsBoolean()))
            {
                continue;
            }

            first = count == 0 ? item : first;
            last = item;
            count++;
        }

        value = terminal.EdgeMode switch
        {
            "first" => count > 0 ? first : BytecodeVmValue.Nothing,
            "last" => count > 0 ? last : BytecodeVmValue.Nothing,
            "single" => count == 1 ? first : BytecodeVmValue.Nothing,
            _ => BytecodeVmValue.Nothing
        };
        return true;
    }

    private bool TryExecuteProgramExtrema(
        IReadOnlyList<GameEventScriptValue> sourceItems,
        GameEventScriptBytecodePipelineProgram pipeline,
        bool isMax,
        out BytecodeVmValue value)
    {
        var terminal = pipeline.TerminalSelector;
        if (terminal.ExpressionProgram is null)
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        BytecodeVmValue bestItem = BytecodeVmValue.Nothing;
        GameEventScriptValue? bestProjection = null;
        if (!TryForEachIncludedPipelineItem(sourceItems, pipeline.PrefixSelectors, item =>
            {
                if (!TryEvaluateProgramProjection(terminal.IdentifierSlot, terminal.ExpressionProgram, item, out var projection))
                {
                    return false;
                }

                var candidateProjection = projection.ToGameEventScriptValue();
                if (bestProjection is null)
                {
                    bestProjection = candidateProjection;
                    bestItem = item;
                    return true;
                }

                int comparison;
                if (GesValueOperations.TryCoerceNumericForOperation(candidateProjection, out _) &&
                    GesValueOperations.TryCoerceNumericForOperation(bestProjection, out _))
                {
                    if (!GesValueOperations.TryCompareNumericValues(candidateProjection, bestProjection, out comparison))
                    {
                        return false;
                    }
                }
                else
                {
                    comparison = GameEventScriptValue.StableComparer.Compare(candidateProjection, bestProjection);
                }

                if ((isMax && comparison > 0) || (!isMax && comparison < 0))
                {
                    bestProjection = candidateProjection;
                    bestItem = item;
                }

                return true;
            }))
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        value = bestProjection is null ? BytecodeVmValue.Nothing : bestItem;
        return true;
    }

    private bool TryExecuteProgramDictionary(
        IReadOnlyList<GameEventScriptValue> sourceItems,
        GameEventScriptBytecodePipelineProgram pipeline,
        out BytecodeVmValue value)
    {
        var terminal = pipeline.TerminalSelector;
        if (terminal.ExpressionProgram is null)
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        var result = new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal);
        if (!TryForEachIncludedPipelineItem(sourceItems, pipeline.PrefixSelectors, item =>
            {
                if (!TryEvaluateProgramProjection(terminal.IdentifierSlot, terminal.ExpressionProgram, item, out var keyValue))
                {
                    return false;
                }

                var key = keyValue.ToGameEventScriptValue().AsText();
                if (string.IsNullOrEmpty(key))
                {
                    return true;
                }

                if (terminal.SecondaryExpressionProgram is null)
                {
                    result[key] = item.ToGameEventScriptValue();
                    return true;
                }

                if (!TryEvaluateProgramProjection(terminal.IdentifierSlot, terminal.SecondaryExpressionProgram, item, out var projectedValue))
                {
                    return false;
                }

                result[key] = projectedValue.ToGameEventScriptValue();
                return true;
            }))
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        value = BytecodeVmValue.Reference(GameEventScriptValueFactory.GesDictionary(result));
        return true;
    }

    private bool TryExecuteProgramContains(
        IReadOnlyList<GameEventScriptValue> sourceItems,
        GameEventScriptValue terminalTarget,
        GameEventScriptBytecodePipelineProgram pipeline,
        out BytecodeVmValue value)
    {
        var terminal = pipeline.TerminalSelector;
        if (terminal.ExpressionProgram is null ||
            !TryExecuteExpressionProgram(terminal.ExpressionProgram, 0, out var needle))
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        GameEventScriptValue target;
        if (pipeline.PrefixSelectors.Length == 0)
        {
            target = terminalTarget;
        }
        else
        {
            if (!TryMaterializePipelineItems(sourceItems, pipeline.PrefixSelectors, out var targetItems, out value))
            {
                return false;
            }

            target = GameEventScriptValueFactory.GesList(targetItems);
        }

        var boxedNeedle = needle.ToGameEventScriptValue();
        value = terminal.EdgeMode switch
        {
            "single" => BytecodeVmValue.Boolean(target.Contains(boxedNeedle)),
            "all" => BytecodeVmValue.Boolean(EnumerateListLikeValue(boxedNeedle).All(target.Contains)),
            "any" => BytecodeVmValue.Boolean(EnumerateListLikeValue(boxedNeedle).Any(target.Contains)),
            _ => BytecodeVmValue.Boolean(false)
        };
        return true;
    }

    private bool TryExecuteProgramDistinct(
        IReadOnlyList<GameEventScriptValue> sourceItems,
        GameEventScriptValue terminalTarget,
        GameEventScriptBytecodePipelineProgram pipeline,
        out BytecodeVmValue value)
    {
        var terminal = pipeline.TerminalSelector;
        if (!TryMaterializePipelineItems(sourceItems, pipeline, out var items, out value))
        {
            return false;
        }

        var target = pipeline.PrefixSelectors.Length == 0 ? terminalTarget : GameEventScriptListValue.Empty;
        if (terminal.ExpressionProgram is null || terminal.IdentifierSlot < 0)
        {
            value = BytecodeVmValue.FromGameEventScriptValue(GesCollectionOperations.Distinct(target, items));
            return true;
        }

        var distinctItems = new List<GameEventScriptValue>();
        var seenKeys = new HashSet<GameEventScriptValue>();
        foreach (var item in items)
        {
            if (!TryEvaluateProgramProjection(terminal.IdentifierSlot, terminal.ExpressionProgram, BytecodeVmValue.FromGameEventScriptValue(item), out var key))
            {
                value = BytecodeVmValue.Nothing;
                return false;
            }

            if (seenKeys.Add(key.ToGameEventScriptValue()))
            {
                distinctItems.Add(item);
            }
        }

        value = BytecodeVmValue.FromGameEventScriptValue(MaterializeDistinctItems(target, distinctItems));
        return true;
    }

    private bool TryExecuteProgramGroupBy(
        IReadOnlyList<GameEventScriptValue> sourceItems,
        GameEventScriptBytecodePipelineProgram pipeline,
        out BytecodeVmValue value)
    {
        var terminal = pipeline.TerminalSelector;
        if (terminal.ExpressionProgram is null)
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        var groups = new Dictionary<string, List<GameEventScriptValue>>(StringComparer.Ordinal);
        if (!TryForEachIncludedPipelineItem(sourceItems, pipeline.PrefixSelectors, item =>
            {
                if (!TryEvaluateProgramProjection(terminal.IdentifierSlot, terminal.ExpressionProgram, item, out var keyValue))
                {
                    return false;
                }

                var key = keyValue.ToGameEventScriptValue().AsText();
                if (!groups.TryGetValue(key, out var bucket))
                {
                    bucket = [];
                    groups[key] = bucket;
                }

                bucket.Add(item.ToGameEventScriptValue());
                return true;
            }))
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        value = BytecodeVmValue.Reference(GameEventScriptValueFactory.GesDictionary(groups.ToDictionary(
            pair => pair.Key,
            pair => GameEventScriptValueFactory.GesList(pair.Value),
            StringComparer.Ordinal)));
        return true;
    }

    private bool TryExecuteProgramOrderBy(
        IReadOnlyList<GameEventScriptValue> sourceItems,
        GameEventScriptValue terminalTarget,
        GameEventScriptBytecodePipelineProgram pipeline,
        out BytecodeVmValue value)
    {
        var terminal = pipeline.TerminalSelector;
        if (terminal.ExpressionProgram is null ||
            !TryMaterializePipelineItems(sourceItems, pipeline, out var items, out value))
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        var pairs = new List<(GameEventScriptValue Item, GameEventScriptValue Key)>(items.Length);
        foreach (var item in items)
        {
            if (!TryEvaluateProgramProjection(terminal.IdentifierSlot, terminal.ExpressionProgram, BytecodeVmValue.FromGameEventScriptValue(item), out var key))
            {
                value = BytecodeVmValue.Nothing;
                return false;
            }

            pairs.Add((Item: item, Key: key.ToGameEventScriptValue()));
        }

        var comparer = string.Equals(terminal.EdgeMode, "descending", StringComparison.Ordinal)
            ? Comparer<GameEventScriptValue>.Create((left, right) => GameEventScriptValue.StableComparer.Compare(right, left))
            : GameEventScriptValue.StableComparer;
        var ordered = pairs.OrderBy(pair => pair.Key, comparer).Select(pair => pair.Item).ToArray();
        var target = pipeline.PrefixSelectors.Length == 0 ? terminalTarget : GameEventScriptListValue.Empty;
        value = BytecodeVmValue.FromGameEventScriptValue(target.Kind switch
        {
            GameEventScriptValueKind.Dice or GameEventScriptValueKind.List or GameEventScriptValueKind.Set or GameEventScriptValueKind.Range => GameEventScriptValueFactory.GesList(ordered),
            _ => GameEventScriptNothingValue.Instance
        });
        return true;
    }

    private bool TryExecuteProgramPattern(
        GameEventScriptValue target,
        IReadOnlyList<GameEventScriptValue> items,
        GameEventScriptBytecodeSelectorProgram selector,
        out BytecodeVmValue value)
    {
        if (selector.DicePattern is null)
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        if (!TryEvaluateSequencePattern(target, items, selector.DicePattern, out var matches))
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        value = BytecodeVmValue.Boolean(matches);
        return true;
    }

    private bool TryExecuteProgramObjectMatch(
        GameEventScriptValue target,
        IReadOnlyList<GameEventScriptValue> items,
        GameEventScriptBytecodeSelectorProgram selector,
        out BytecodeVmValue value)
    {
        if (selector.ObjectPattern is null)
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        if (!TryEvaluateObjectMatchSelector(target, items, selector.ObjectPattern, out var matches))
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        value = BytecodeVmValue.Boolean(matches);
        return true;
    }

    private bool TryExecuteProgramTakePattern(
        GameEventScriptValue target,
        IReadOnlyList<GameEventScriptValue> items,
        GameEventScriptBytecodeSelectorProgram selector,
        out BytecodeVmValue value)
    {
        if (selector.DicePattern is null)
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        if (!TryEvaluateTakePattern(target, items, selector.DicePattern, out var result))
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        value = BytecodeVmValue.FromGameEventScriptValue(result);
        return true;
    }

    private bool TryExecuteProgramChoose(
        IReadOnlyList<GameEventScriptValue> items,
        GameEventScriptBytecodeSelectorProgram selector,
        out BytecodeVmValue value)
    {
        if (!TryFilterChooseCandidates(items, selector, out var candidates))
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        IReadOnlyList<GameEventScriptValue> chosen;
        if (selector.SecondaryExpressionProgram is not null && selector.SecondaryIdentifierSlot >= 0)
        {
            if (!TryChooseWeightedItems(candidates, selector.Count, selector.SecondaryIdentifierSlot, selector.SecondaryExpressionProgram, out chosen))
            {
                value = BytecodeVmValue.Nothing;
                return false;
            }
        }
        else if (selector.Flag)
        {
            chosen = ChooseRandomItems(candidates, selector.Count);
        }
        else
        {
            chosen = candidates.Take(selector.Count).ToArray();
        }

        if (selector.Count == 1)
        {
            value = chosen.Count == 0
                ? BytecodeVmValue.Nothing
                : BytecodeVmValue.FromGameEventScriptValue(chosen[0]);
            return true;
        }

        value = BytecodeVmValue.Reference(GameEventScriptValueFactory.GesList(chosen));
        return true;
    }

    private bool TryFilterChooseCandidates(
        IReadOnlyList<GameEventScriptValue> items,
        GameEventScriptBytecodeSelectorProgram selector,
        out IReadOnlyList<GameEventScriptValue> candidates)
    {
        if (selector.ExpressionProgram is null || selector.IdentifierSlot < 0)
        {
            candidates = items.ToArray();
            return true;
        }

        var filtered = new List<GameEventScriptValue>();
        foreach (var item in items)
        {
            if (!TryEvaluateProgramProjection(selector.IdentifierSlot, selector.ExpressionProgram, BytecodeVmValue.FromGameEventScriptValue(item), out var predicate))
            {
                candidates = [];
                return false;
            }

            if (predicate.AsBoolean())
            {
                filtered.Add(item);
            }
        }

        candidates = filtered;
        return true;
    }

    private bool TryChooseWeightedItems(
        IReadOnlyList<GameEventScriptValue> candidates,
        int count,
        int identifierSlot,
        GameEventScriptBytecodeExpressionProgram weightProgram,
        out IReadOnlyList<GameEventScriptValue> chosen)
    {
        var remaining = candidates.ToList();
        var result = new List<GameEventScriptValue>();

        while (result.Count < count && remaining.Count > 0)
        {
            var weightedItems = new List<(GameEventScriptValue Item, double Weight)>();
            double totalWeight = 0d;

            foreach (var candidate in remaining)
            {
                if (!TryEvaluateProgramProjection(identifierSlot, weightProgram, BytecodeVmValue.FromGameEventScriptValue(candidate), out var weightValue))
                {
                    chosen = [];
                    return false;
                }

                var weight = EvaluatePositiveWeight(weightValue.ToGameEventScriptValue());
                if (weight <= 0d)
                {
                    continue;
                }

                weightedItems.Add((candidate, weight));
                totalWeight += weight;
            }

            if (weightedItems.Count == 0 || totalWeight <= 0d)
            {
                break;
            }

            if (!TryNextInclusiveFloat(0d, totalWeight, out var threshold))
            {
                break;
            }

            double cumulative = 0d;
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

            result.Add(selected);
            remaining.Remove(selected);
        }

        chosen = result;
        return true;
    }

    private static double EvaluatePositiveWeight(GameEventScriptValue value)
        => GesValueOperations.TryCoerceNumericForOperation(value, out var number) && number.IsFinite
            ? Math.Max(0d, number.Value)
            : 0d;

    private IReadOnlyList<GameEventScriptValue> ChooseRandomItems(IReadOnlyList<GameEventScriptValue> items, int count)
    {
        var pool = items.ToList();
        var result = new List<GameEventScriptValue>(Math.Min(count, pool.Count));
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

    private bool TryForEachIncludedPipelineItem(
        IReadOnlyList<GameEventScriptValue> sourceItems,
        GameEventScriptBytecodeSelectorProgram[] prefixSelectors,
        Func<BytecodeVmValue, bool> action,
        Func<bool>? stopWhen = null)
    {
        for (var itemIndex = 0; itemIndex < sourceItems.Count; itemIndex++)
        {
            if (!TryApplyProgramPipelinePrefix(
                    BytecodeVmValue.FromGameEventScriptValue(sourceItems[itemIndex]),
                    prefixSelectors,
                    out var item,
                    out var include))
            {
                return false;
            }

            if (!include)
            {
                continue;
            }

            if (!action(item))
            {
                return false;
            }

            if (stopWhen?.Invoke() == true)
            {
                break;
            }
        }

        return true;
    }

    private bool TryMaterializePipelineItems(
        IReadOnlyList<GameEventScriptValue> sourceItems,
        GameEventScriptBytecodePipelineProgram pipeline,
        out GameEventScriptValue[] items,
        out BytecodeVmValue value)
        => TryMaterializePipelineItems(sourceItems, pipeline.PrefixSelectors, out items, out value);

    private bool TryMaterializePipelineItems(
        IReadOnlyList<GameEventScriptValue> sourceItems,
        GameEventScriptBytecodeSelectorProgram[] prefixSelectors,
        out GameEventScriptValue[] items,
        out BytecodeVmValue value)
    {
        var result = new List<GameEventScriptValue>(sourceItems.Count);
        if (!TryForEachIncludedPipelineItem(sourceItems, prefixSelectors, item =>
            {
                result.Add(item.ToGameEventScriptValue());
                return true;
            }))
        {
            items = [];
            value = BytecodeVmValue.Nothing;
            return false;
        }

        items = result.ToArray();
        value = BytecodeVmValue.Nothing;
        return true;
    }

    private static IEnumerable<GameEventScriptValue> EnumerateListLikeValue(GameEventScriptValue value)
    {
        if (value.Kind == GameEventScriptValueKind.Optional)
        {
            var optional = value.AsOptional();
            return optional.HasValue
                ? EnumerateListLikeValue(optional.Value)
                : Array.Empty<GameEventScriptValue>();
        }

        return value.Kind is GameEventScriptValueKind.Range or GameEventScriptValueKind.Sequence
            ? value.AsEnumerable()
            : value.AsList();
    }

    private static IReadOnlyList<GameEventScriptValue> MaterializeListLikeValue(GameEventScriptValue value)
    {
        if (value.Kind == GameEventScriptValueKind.Optional)
        {
            var optional = value.AsOptional();
            return optional.HasValue
                ? MaterializeListLikeValue(optional.Value)
                : Array.Empty<GameEventScriptValue>();
        }

        return value.Kind is GameEventScriptValueKind.Range or GameEventScriptValueKind.Sequence
            ? value.AsEnumerable().ToArray()
            : value.AsList();
    }

    private static bool SetValue(BytecodeVmValue input, out BytecodeVmValue value)
    {
        value = input;
        return true;
    }

    private static GameEventScriptValue EvaluateReverseSelector(GameEventScriptValue target, IReadOnlyList<GameEventScriptValue> items)
    {
        if (target.Kind is not (GameEventScriptValueKind.List or GameEventScriptValueKind.Dice))
        {
            return GameEventScriptNothingValue.Instance;
        }

        return GameEventScriptValueFactory.GesList(items.Reverse().ToArray());
    }

    private static GameEventScriptValue EvaluateDrawSelector(GameEventScriptValue target, IReadOnlyList<GameEventScriptValue> items, int count)
    {
        if (target.Kind is not (GameEventScriptValueKind.List or GameEventScriptValueKind.Dice))
        {
            return GameEventScriptNothingValue.Instance;
        }

        var drawn = items.Take(count).ToArray();
        if (count == 1)
        {
            return drawn.Length == 0 ? GameEventScriptNothingValue.Instance : drawn[0];
        }

        return target.Kind == GameEventScriptValueKind.Dice ? GesDice(GameEventScriptDiceValue.Create(drawn.Select(item => (int)item.AsInteger()))) : GesList(drawn);
    }

    private GameEventScriptValue EvaluateShuffleSelector(GameEventScriptValue target, IReadOnlyList<GameEventScriptValue> items)
    {
        if (target.Kind is not (GameEventScriptValueKind.List or GameEventScriptValueKind.Dice))
        {
            return GameEventScriptNothingValue.Instance;
        }

        var shuffled = items.ToArray();
        for (var i = shuffled.Length - 1; i > 0; i--)
        {
            if (!TryNextInclusiveInt(0, i, out var swapIndex))
            {
                return GameEventScriptValueFactory.GesList(shuffled);
            }

            (shuffled[i], shuffled[swapIndex]) = (shuffled[swapIndex], shuffled[i]);
        }

        return GameEventScriptValueFactory.GesList(shuffled);
    }

    private bool TryEvaluateTakePattern(
        GameEventScriptValue target,
        IReadOnlyList<GameEventScriptValue> items,
        GameEventScriptBytecodeDicePattern pattern,
        out GameEventScriptValue value)
    {
        if (!IsPatternSequence(target))
        {
            value = GameEventScriptNothingValue.Instance;
            return true;
        }

        if (!TryTakeSequencePattern(items, pattern, out var takenItems))
        {
            value = GameEventScriptNothingValue.Instance;
            return true;
        }

        value = target.Kind == GameEventScriptValueKind.Dice
            ? GameEventScriptValueFactory.GesDice(GameEventScriptDiceValue.Create(takenItems.Select(item => (int)item.AsInteger())))
            : GameEventScriptValueFactory.GesList(takenItems);
        return true;
    }

    private bool TryEvaluateSequencePattern(
        GameEventScriptValue target,
        IReadOnlyList<GameEventScriptValue> items,
        GameEventScriptBytecodeDicePattern? pattern,
        out bool matches)
    {
        if (pattern is null || !IsPatternSequence(target))
        {
            matches = false;
            return true;
        }

        var counts = items
            .GroupBy(item => item)
            .ToDictionary(group => group.Key, group => group.Count());

        switch (pattern)
        {
            case GameEventScriptBytecodeDiceCountPattern countPattern:
                return TryMatchDiceCountPattern(counts, countPattern, out matches);

            case GameEventScriptBytecodeFullHousePattern:
                matches = counts.Count == 2 && counts.Values.OrderByDescending(x => x).SequenceEqual(new[] { 3, 2 });
                return true;

            case GameEventScriptBytecodeStraightPattern:
                matches = MatchStraight(items);
                return true;

            default:
                matches = false;
                return true;
        }
    }

    private bool TryMatchDiceCountPattern(
        IReadOnlyDictionary<GameEventScriptValue, int> counts,
        GameEventScriptBytecodeDiceCountPattern pattern,
        out bool matches)
    {
        if (pattern.FaceProgram is not null)
        {
            if (!TryExecuteExpressionProgram(pattern.FaceProgram, 0, out var face))
            {
                matches = false;
                return false;
            }

            matches = counts.TryGetValue(face.ToGameEventScriptValue(), out var count) && count >= pattern.Count;
            return true;
        }

        matches = counts.Values.Any(count => count >= pattern.Count);
        return true;
    }

    private static bool MatchStraight(IReadOnlyList<GameEventScriptValue> items)
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

    private static bool IsPatternSequence(GameEventScriptValue value)
        => value.Kind is GameEventScriptValueKind.List or GameEventScriptValueKind.Dice;

    private bool TryTakeSequencePattern(
        IReadOnlyList<GameEventScriptValue> items,
        GameEventScriptBytecodeDicePattern pattern,
        out IReadOnlyList<GameEventScriptValue> takenItems)
    {
        var counts = items
            .GroupBy(item => item)
            .ToDictionary(group => group.Key, group => group.Count());

        switch (pattern)
        {
            case GameEventScriptBytecodeDiceCountPattern countPattern:
                return TryTakeCountPattern(items, counts, countPattern, out takenItems);

            case GameEventScriptBytecodeFullHousePattern:
                return TryTakeFullHouse(items, counts, out takenItems);

            case GameEventScriptBytecodeStraightPattern:
                return TryTakeStraight(items, out takenItems);

            default:
                takenItems = Array.Empty<GameEventScriptValue>();
                return false;
        }
    }

    private bool TryTakeCountPattern(
        IReadOnlyList<GameEventScriptValue> items,
        IReadOnlyDictionary<GameEventScriptValue, int> counts,
        GameEventScriptBytecodeDiceCountPattern pattern,
        out IReadOnlyList<GameEventScriptValue> takenItems)
    {
        if (pattern.FaceProgram is not null)
        {
            if (!TryExecuteExpressionProgram(pattern.FaceProgram, 0, out var face))
            {
                takenItems = Array.Empty<GameEventScriptValue>();
                return false;
            }

            var boxedFace = face.ToGameEventScriptValue();
            if (counts.TryGetValue(boxedFace, out var faceCount) && faceCount >= pattern.Count)
            {
                takenItems = TakeItemsByCounts(items, new Dictionary<GameEventScriptValue, int> { [boxedFace] = pattern.Count });
                return true;
            }

            takenItems = Array.Empty<GameEventScriptValue>();
            return false;
        }

        foreach (var candidate in EnumerateDistinctInSourceOrder(items))
        {
            if (counts.TryGetValue(candidate, out var candidateCount) && candidateCount >= pattern.Count)
            {
                takenItems = TakeItemsByCounts(items, new Dictionary<GameEventScriptValue, int> { [candidate] = pattern.Count });
                return true;
            }
        }

        takenItems = Array.Empty<GameEventScriptValue>();
        return false;
    }

    private static bool TryTakeFullHouse(
        IReadOnlyList<GameEventScriptValue> items,
        IReadOnlyDictionary<GameEventScriptValue, int> counts,
        out IReadOnlyList<GameEventScriptValue> takenItems)
    {
        foreach (var tripleCandidate in EnumerateDistinctInSourceOrder(items))
        {
            if (!counts.TryGetValue(tripleCandidate, out var tripleCount) || tripleCount < 3)
            {
                continue;
            }

            foreach (var pairCandidate in EnumerateDistinctInSourceOrder(items))
            {
                if (GesValueOperations.AreEqual(pairCandidate, tripleCandidate))
                {
                    continue;
                }

                if (counts.TryGetValue(pairCandidate, out var pairCount) && pairCount >= 2)
                {
                    takenItems = TakeItemsByCounts(items, new Dictionary<GameEventScriptValue, int>
                    {
                        [tripleCandidate] = 3,
                        [pairCandidate] = 2
                    });
                    return true;
                }
            }
        }

        takenItems = Array.Empty<GameEventScriptValue>();
        return false;
    }

    private static bool TryTakeStraight(IReadOnlyList<GameEventScriptValue> items, out IReadOnlyList<GameEventScriptValue> takenItems)
    {
        var distinctValues = new List<GameEventScriptValue>();
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
            takenItems = Array.Empty<GameEventScriptValue>();
            return false;
        }

        for (var i = 0; i < uniqueIntegers.Length - 1; i++)
        {
            if (uniqueIntegers[i] - 1 != uniqueIntegers[i + 1])
            {
                takenItems = Array.Empty<GameEventScriptValue>();
                return false;
            }
        }

        takenItems = distinctValues;
        return true;
    }

    private static IReadOnlyList<GameEventScriptValue> TakeItemsByCounts(
        IReadOnlyList<GameEventScriptValue> items,
        IReadOnlyDictionary<GameEventScriptValue, int> requiredCounts)
    {
        var remaining = requiredCounts.ToDictionary(pair => pair.Key, pair => pair.Value);
        var takenItems = new List<GameEventScriptValue>();

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

    private static IEnumerable<GameEventScriptValue> EnumerateDistinctInSourceOrder(IReadOnlyList<GameEventScriptValue> items)
    {
        var seen = new HashSet<GameEventScriptValue>();
        foreach (var item in items)
        {
            if (seen.Add(item))
            {
                yield return item;
            }
        }
    }

    private bool TryEvaluateObjectMatchSelector(
        GameEventScriptValue target,
        IReadOnlyList<GameEventScriptValue> items,
        GameEventScriptBytecodeObjectMatchPattern? pattern,
        out bool matches)
    {
        if (pattern is null || target.Kind is not (GameEventScriptValueKind.List or GameEventScriptValueKind.Set or GameEventScriptValueKind.Dice))
        {
            matches = false;
            return true;
        }

        foreach (var item in items)
        {
            if (!TryMatchesObjectPattern(item, pattern, out var itemMatches))
            {
                matches = false;
                return false;
            }

            if (itemMatches)
            {
                matches = true;
                return true;
            }
        }

        matches = false;
        return true;
    }

    private bool TryMatchesObjectPattern(GameEventScriptValue value, GameEventScriptBytecodeObjectMatchPattern pattern, out bool matches)
    {
        if (value.Kind != GameEventScriptValueKind.Dictionary)
        {
            matches = false;
            return true;
        }

        var dictionary = value.AsDictionary();
        foreach (var entry in pattern.Entries)
        {
            if (!dictionary.TryGetValue(entry.Key, out var actual))
            {
                matches = false;
                return true;
            }

            switch (entry.Value)
            {
                case GameEventScriptBytecodeObjectMatchExpressionValue expressionValue:
                    if (!TryExecuteExpressionProgram(expressionValue.ExpressionProgram, 0, out var expected))
                    {
                        matches = false;
                        return false;
                    }

                    if (!GesValueOperations.AreEqual(actual, expected.ToGameEventScriptValue()))
                    {
                        matches = false;
                        return true;
                    }

                    break;

                case GameEventScriptBytecodeObjectMatchNestedValue nestedValue:
                    if (!TryMatchesObjectPattern(actual, nestedValue.Pattern, out var nestedMatches))
                    {
                        matches = false;
                        return false;
                    }

                    if (!nestedMatches)
                    {
                        matches = false;
                        return true;
                    }

                    break;
            }
        }

        matches = true;
        return true;
    }

    private static GameEventScriptValue EvaluateSequenceSliceSelector(
        GameEventScriptValue target,
        IReadOnlyList<GameEventScriptValue> items,
        GameEventScriptBytecodeSelectorProgram selector)
    {
        if (selector.Count <= 0)
        {
            return target.Kind == GameEventScriptValueKind.Dice
                ? GameEventScriptValueFactory.GesDice(GameEventScriptDiceValue.Empty)
                : GameEventScriptValueFactory.GesList(Array.Empty<GameEventScriptValue>());
        }

        var selectedItems = selector.SecondaryMode switch
        {
            "first" => TakeFirst(items, selector.Count),
            "last" => TakeLast(items, selector.Count),
            "highest" => TakeHighest(items, selector.Count),
            "lowest" => TakeLowest(items, selector.Count),
            _ => Array.Empty<GameEventScriptValue>()
        };

        if (string.Equals(selector.EdgeMode, "drop", StringComparison.Ordinal))
        {
            selectedItems = DropSelection(items, selectedItems);
        }

        return target.Kind switch
        {
            GameEventScriptValueKind.Dice => GameEventScriptValueFactory.GesDice(GameEventScriptDiceValue.Create(selectedItems.Select(item => (int)item.AsInteger()))),
            GameEventScriptValueKind.List => GameEventScriptValueFactory.GesList(selectedItems),
            GameEventScriptValueKind.Set => GameEventScriptValueFactory.GesList(selectedItems),
            _ => GameEventScriptNothingValue.Instance
        };
    }

    private static GameEventScriptValue MaterializeDistinctItems(GameEventScriptValue target, IReadOnlyList<GameEventScriptValue> items)
    {
        return target.Kind switch
        {
            GameEventScriptValueKind.Set => GameEventScriptValueFactory.GesSet(items),
            GameEventScriptValueKind.List => GameEventScriptValueFactory.GesList(items),
            GameEventScriptValueKind.Dice => GameEventScriptValueFactory.GesList(items),
            GameEventScriptValueKind.Range => GameEventScriptValueFactory.GesList(items),
            _ => GameEventScriptNothingValue.Instance
        };
    }

    private static GameEventScriptValue[] TakeFirst(IReadOnlyList<GameEventScriptValue> items, int count)
        => items.Take(count).ToArray();

    private static GameEventScriptValue[] TakeLast(IReadOnlyList<GameEventScriptValue> items, int count)
        => items.Skip(Math.Max(0, items.Count - count)).ToArray();

    private static GameEventScriptValue[] TakeHighest(IReadOnlyList<GameEventScriptValue> items, int count)
        => items
            .OrderByDescending(item => item, GameEventScriptValue.StableComparer)
            .Take(count)
            .ToArray();

    private static GameEventScriptValue[] TakeLowest(IReadOnlyList<GameEventScriptValue> items, int count)
        => items
            .OrderBy(item => item, GameEventScriptValue.StableComparer)
            .Take(count)
            .ToArray();

    private static GameEventScriptValue[] DropSelection(IReadOnlyList<GameEventScriptValue> items, IReadOnlyList<GameEventScriptValue> selection)
    {
        if (selection.Count == 0)
        {
            return items.ToArray();
        }

        var remainingSelections = selection
            .GroupBy(item => item)
            .ToDictionary(group => group.Key, group => group.Count());
        var result = new List<GameEventScriptValue>(items.Count);

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

    private bool TryApplyProgramPipelinePrefix(
        BytecodeVmValue item,
        GameEventScriptBytecodeSelectorProgram[] prefixSelectors,
        out BytecodeVmValue value,
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
                case GameEventScriptBytecodeSelectorKind.Filter:
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

                case GameEventScriptBytecodeSelectorKind.Select:
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

    private bool TryEvaluatePipelineTerminalProjection(
        int identifierSlot,
        GameEventScriptBytecodeExpressionProgram expressionProgram,
        BytecodeVmValue item,
        out BytecodeVmValue value)
    {
        var instructions = expressionProgram.Instructions;
        if (instructions.Length == 1 &&
            (instructions[0].OpCode == GameEventScriptBytecodeOpCode.LoadSlot ||
             instructions[0].OpCode == GameEventScriptBytecodeOpCode.LoadConstant))
        {
            if (!TryConsumeExecutionStep("Expression evaluation budget exhausted."))
            {
                value = BytecodeVmValue.Nothing;
                return true;
            }

            ref readonly var instruction = ref instructions[0];
            if (instruction.OpCode == GameEventScriptBytecodeOpCode.LoadSlot)
            {
                value = instruction.A == identifierSlot ? item : ResolveSlot(instruction.A);
                return true;
            }

            value = LoadConstant(instruction.ConstantIndex);
            return true;
        }

        return TryEvaluateProgramProjection(identifierSlot, expressionProgram, item, out value);
    }

    private static bool IsIdentityProjection(int identifierSlot, GameEventScriptBytecodeExpressionProgram expressionProgram)
    {
        var instructions = expressionProgram.Instructions;
        return instructions.Length == 1 &&
               instructions[0].OpCode == GameEventScriptBytecodeOpCode.LoadSlot &&
               instructions[0].A == identifierSlot;
    }

    private bool IsConstantTrueProjection(GameEventScriptBytecodeExpressionProgram expressionProgram)
    {
        var instructions = expressionProgram.Instructions;
        return instructions.Length == 1 &&
               instructions[0].OpCode == GameEventScriptBytecodeOpCode.LoadConstant &&
               LoadConstant(instructions[0].ConstantIndex).AsBoolean();
    }

    private bool TryEvaluateProgramProjection(
        int identifierSlot,
        GameEventScriptBytecodeExpressionProgram expressionProgram,
        BytecodeVmValue item,
        out BytecodeVmValue value)
    {
        if (TryEvaluateProjectionFast(identifierSlot, expressionProgram, item, out value, out var handled))
        {
            return true;
        }

        return handled
            ? false
            : TryExecuteExpressionProgramWithTemporarySlot(identifierSlot, item, expressionProgram, 0, out value);
    }

    private bool TryEvaluateProjectionFast(
        int identifierSlot,
        GameEventScriptBytecodeExpressionProgram expressionProgram,
        BytecodeVmValue item,
        out BytecodeVmValue value,
        out bool handled)
    {
        value = BytecodeVmValue.Nothing;
        handled = false;
        var instructions = expressionProgram.Instructions;
        if (!expressionProgram.CanEvaluateProjectionFast)
        {
            return false;
        }

        handled = true;
        if (!TryConsumeExecutionSteps(instructions.Length, "Expression evaluation budget exhausted."))
        {
            return true;
        }

        switch (expressionProgram.ProjectionFastKind)
        {
            case GameEventScriptBytecodeProjectionFastKind.Operand:
                return TryGetProjectionOperand(instructions[0], identifierSlot, item, out value);

            case GameEventScriptBytecodeProjectionFastKind.RulePredicate:
                return TryGetProjectionOperand(instructions[0], identifierSlot, item, out var predicateInput) &&
                       TryEvaluateRulePredicate(instructions[1], predicateInput, 0, out value);

            case GameEventScriptBytecodeProjectionFastKind.Binary:
                return TryEvaluateProjectionBinaryPattern(
                    instructions[0],
                    instructions[1],
                    instructions[2],
                    identifierSlot,
                    item,
                    out value);

            case GameEventScriptBytecodeProjectionFastKind.BinaryCastBoolean:
                if (!TryEvaluateProjectionBinaryPattern(
                        instructions[0],
                        instructions[1],
                        instructions[2],
                        identifierSlot,
                        item,
                        out var castSource))
                {
                    return false;
                }

                value = BytecodeVmValue.Boolean(castSource.AsBoolean());
                return true;

            case GameEventScriptBytecodeProjectionFastKind.BinaryThenBinary:
                if (!TryEvaluateProjectionBinaryPattern(
                        instructions[0],
                        instructions[1],
                        instructions[2],
                        identifierSlot,
                        item,
                        out var firstResult) ||
                    !TryGetProjectionOperand(instructions[3], identifierSlot, item, out var secondOperand))
                {
                    return false;
                }

                value = EvaluateProjectionBinary(instructions[4].OpCode, firstResult, secondOperand);
                return true;

            case GameEventScriptBytecodeProjectionFastKind.BinaryThenBinaryThenBinary:
                if (!TryEvaluateProjectionBinaryPattern(
                        instructions[0],
                        instructions[1],
                        instructions[2],
                        identifierSlot,
                        item,
                        out var first) ||
                    !TryGetProjectionOperand(instructions[3], identifierSlot, item, out var middleOperand))
                {
                    return false;
                }

                var second = EvaluateProjectionBinary(instructions[4].OpCode, first, middleOperand);
                if (!TryGetProjectionOperand(instructions[5], identifierSlot, item, out var finalOperand))
                {
                    return false;
                }

                value = EvaluateProjectionBinary(instructions[6].OpCode, second, finalOperand);
                return true;
        }

        var top = 0;
        var stack0 = BytecodeVmValue.Nothing;
        var stack1 = BytecodeVmValue.Nothing;
        var stack2 = BytecodeVmValue.Nothing;

        for (var index = 0; index < instructions.Length; index++)
        {
            ref readonly var instruction = ref instructions[index];
            switch (instruction.OpCode)
            {
                case GameEventScriptBytecodeOpCode.LoadConstant:
                    if (!Push(LoadConstant(instruction.ConstantIndex)))
                    {
                        return false;
                    }

                    break;

                case GameEventScriptBytecodeOpCode.LoadSlot:
                    if (!Push(instruction.A == identifierSlot ? item : ResolveSlot(instruction.A)))
                    {
                        return false;
                    }

                    break;

                case GameEventScriptBytecodeOpCode.Cast:
                    if (top < 1)
                    {
                        return false;
                    }

                    SetAt(top - 1, BytecodeVmValue.Boolean(GetAt(top - 1).AsBoolean()));
                    break;

                case GameEventScriptBytecodeOpCode.RulePredicate:
                    if (top < 1)
                    {
                        return false;
                    }

                    var predicateInput = Pop();
                    if (!TryEvaluateRulePredicate(instruction, predicateInput, 0, out var predicateValue))
                    {
                        return false;
                    }

                    if (!Push(predicateValue))
                    {
                        return false;
                    }

                    break;

                default:
                    if (top < 2)
                    {
                        return false;
                    }

                    var right = Pop();
                    var left = Pop();
                    if (!Push(EvaluateProjectionBinary(instruction.OpCode, left, right)))
                    {
                        return false;
                    }

                    break;
            }
        }

        if (top <= 0)
        {
            return false;
        }

        value = GetAt(top - 1);
        return true;

        bool Push(BytecodeVmValue input)
        {
            switch (top)
            {
                case 0:
                    stack0 = input;
                    top++;
                    return true;
                case 1:
                    stack1 = input;
                    top++;
                    return true;
                case 2:
                    stack2 = input;
                    top++;
                    return true;
                default:
                    return false;
            }
        }

        BytecodeVmValue Pop()
        {
            top--;
            return top switch
            {
                0 => stack0,
                1 => stack1,
                _ => stack2
            };
        }

        BytecodeVmValue GetAt(int index)
            => index switch
            {
                0 => stack0,
                1 => stack1,
                _ => stack2
            };

        void SetAt(int index, BytecodeVmValue input)
        {
            switch (index)
            {
                case 0:
                    stack0 = input;
                    break;
                case 1:
                    stack1 = input;
                    break;
                default:
                    stack2 = input;
                    break;
            }
        }
    }

    private bool TryEvaluateProjectionBinaryPattern(
        in GameEventScriptBytecodeInstruction leftInstruction,
        in GameEventScriptBytecodeInstruction rightInstruction,
        in GameEventScriptBytecodeInstruction opInstruction,
        int identifierSlot,
        BytecodeVmValue item,
        out BytecodeVmValue value)
    {
        if (!TryGetProjectionOperand(leftInstruction, identifierSlot, item, out var left) ||
            !TryGetProjectionOperand(rightInstruction, identifierSlot, item, out var right) ||
            !IsProjectionBinaryOp(opInstruction.OpCode))
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        value = EvaluateProjectionBinary(opInstruction.OpCode, left, right);
        return true;
    }

    private BytecodeVmValue EvaluateProjectionBinary(
        GameEventScriptBytecodeOpCode opCode,
        in BytecodeVmValue left,
        in BytecodeVmValue right)
        => TryEvaluatePrimitiveIntegerProjectionBinary(opCode, left, right, out var value)
            ? value
            : EvaluateProgramBinary(opCode, left, right);

    private static bool TryEvaluatePrimitiveIntegerProjectionBinary(
        GameEventScriptBytecodeOpCode opCode,
        in BytecodeVmValue left,
        in BytecodeVmValue right,
        out BytecodeVmValue value)
    {
        switch (opCode)
        {
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerEqual:
                if (BytecodeVmValue.TryComparePrimitiveIntegers(left, right, out var equalComparison))
                {
                    value = BytecodeVmValue.Boolean(equalComparison == 0);
                    return true;
                }

                break;

            case GameEventScriptBytecodeOpCode.PrimitiveIntegerNotEqual:
                if (BytecodeVmValue.TryComparePrimitiveIntegers(left, right, out var notEqualComparison))
                {
                    value = BytecodeVmValue.Boolean(notEqualComparison != 0);
                    return true;
                }

                break;

            case GameEventScriptBytecodeOpCode.PrimitiveIntegerLess:
                if (BytecodeVmValue.TryComparePrimitiveIntegers(left, right, out var lessComparison))
                {
                    value = BytecodeVmValue.Boolean(lessComparison < 0);
                    return true;
                }

                break;

            case GameEventScriptBytecodeOpCode.PrimitiveIntegerGreater:
                if (BytecodeVmValue.TryComparePrimitiveIntegers(left, right, out var greaterComparison))
                {
                    value = BytecodeVmValue.Boolean(greaterComparison > 0);
                    return true;
                }

                break;

            case GameEventScriptBytecodeOpCode.PrimitiveIntegerLessOrEqual:
                if (BytecodeVmValue.TryComparePrimitiveIntegers(left, right, out var lessOrEqualComparison))
                {
                    value = BytecodeVmValue.Boolean(lessOrEqualComparison <= 0);
                    return true;
                }

                break;

            case GameEventScriptBytecodeOpCode.PrimitiveIntegerGreaterOrEqual:
                if (BytecodeVmValue.TryComparePrimitiveIntegers(left, right, out var greaterOrEqualComparison))
                {
                    value = BytecodeVmValue.Boolean(greaterOrEqualComparison >= 0);
                    return true;
                }

                break;

            case GameEventScriptBytecodeOpCode.PrimitiveIntegerAdd:
                return BytecodeVmValue.TryPrimitiveIntegerAdd(left, right, out value);
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerSubtract:
                return BytecodeVmValue.TryPrimitiveIntegerSubtract(left, right, out value);
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerMultiply:
                return BytecodeVmValue.TryPrimitiveIntegerMultiply(left, right, out value);
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerDivide:
                return BytecodeVmValue.TryPrimitiveIntegerDivide(left, right, out value);
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerFloorDivide:
                return BytecodeVmValue.TryPrimitiveIntegerFloorDivide(left, right, out value);
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerModulo:
                return BytecodeVmValue.TryPrimitiveIntegerModulo(left, right, out value);
            case GameEventScriptBytecodeOpCode.PrimitiveIntegerRemainder:
                return BytecodeVmValue.TryPrimitiveIntegerRemainder(left, right, out value);
        }

        value = BytecodeVmValue.Nothing;
        return false;
    }

    private bool TryGetProjectionOperand(
        in GameEventScriptBytecodeInstruction instruction,
        int identifierSlot,
        BytecodeVmValue item,
        out BytecodeVmValue value)
    {
        switch (instruction.OpCode)
        {
            case GameEventScriptBytecodeOpCode.LoadConstant:
                value = LoadConstant(instruction.ConstantIndex);
                return true;
            case GameEventScriptBytecodeOpCode.LoadSlot:
                value = instruction.A == identifierSlot ? item : ResolveSlot(instruction.A);
                return true;
            default:
                value = BytecodeVmValue.Nothing;
                return false;
        }
    }

    private static bool IsProjectionBinaryOp(GameEventScriptBytecodeOpCode opCode)
        => opCode is GameEventScriptBytecodeOpCode.Or or
            GameEventScriptBytecodeOpCode.Xor or
            GameEventScriptBytecodeOpCode.And or
            GameEventScriptBytecodeOpCode.Power or
            GameEventScriptBytecodeOpCode.Equal or
            GameEventScriptBytecodeOpCode.NotEqual or
            GameEventScriptBytecodeOpCode.ApproxEqual or
            GameEventScriptBytecodeOpCode.Less or
            GameEventScriptBytecodeOpCode.Greater or
            GameEventScriptBytecodeOpCode.LessOrEqual or
            GameEventScriptBytecodeOpCode.GreaterOrEqual or
            GameEventScriptBytecodeOpCode.Add or
            GameEventScriptBytecodeOpCode.Subtract or
            GameEventScriptBytecodeOpCode.Multiply or
            GameEventScriptBytecodeOpCode.Divide or
            GameEventScriptBytecodeOpCode.IntegerDivide or
            GameEventScriptBytecodeOpCode.Modulo or
            GameEventScriptBytecodeOpCode.Remainder or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerEqual or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerNotEqual or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerLess or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerGreater or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerLessOrEqual or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerGreaterOrEqual or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerAdd or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerSubtract or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerMultiply or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerDivide or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerFloorDivide or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerModulo or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerRemainder;

    private bool TryExecuteExpressionProgramWithTemporarySlot(
        int slot,
        BytecodeVmValue slotValue,
        GameEventScriptBytecodeExpressionProgram expressionProgram,
        int stackBase,
        out BytecodeVmValue value)
    {
        if ((uint)slot >= (uint)_locals.Length)
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        var hadValue = _assignedSlots[slot];
        var previous = _locals[slot];
        _locals[slot] = slotValue;
        _assignedSlots[slot] = true;
        var success = TryExecuteExpressionProgram(expressionProgram, stackBase, out value);
        RestoreSlot(slot, hadValue, previous);
        return success;
    }

    private void RestoreSlot(int slot, bool hadValue, BytecodeVmValue previous)
    {
        if (hadValue)
        {
            _locals[slot] = previous;
            _assignedSlots[slot] = true;
        }
        else
        {
            _locals[slot] = BytecodeVmValue.Nothing;
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
                _locals[change.Slot] = BytecodeVmValue.Nothing;
                _assignedSlots[change.Slot] = false;
            }
        }

        _changes.RemoveRange(mark, _changes.Count - mark);
    }

    private bool Define(string name, BytecodeVmValue value)
    {
        if (!_plan.TryGetSlot(name, out var slot))
        {
            return false;
        }

        return DefineSlot(slot, value);
    }

    private bool DefineSlot(int slot, BytecodeVmValue value)
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

    private BytecodeVmValue Resolve(string name)
        => _plan.TryGetSlot(name, out var slot) && _assignedSlots[slot]
            ? _locals[slot]
            : BytecodeVmValue.Nothing;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private BytecodeVmValue ResolveSlot(int slot)
        => (uint)slot < (uint)_locals.Length && _assignedSlots[slot]
            ? _locals[slot]
            : BytecodeVmValue.Nothing;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryConsumeExecutionStep(string detail)
    {
        if (_runtimeBudget.TryConsumeExecutionStep(detail))
        {
            return true;
        }

        _halted = true;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryConsumeExecutionSteps(int count, string detail)
    {
        if (_runtimeBudget.TryConsumeExecutionSteps(count, detail))
        {
            return true;
        }

        _halted = true;
        return false;
    }

    private void RecordParameterBound(string parameter, GameEventScriptValue value)
    {
        if (!_diagnosticsEnabled)
        {
            return;
        }

        RecordDiagnostic(
            GameEventScriptDiagnosticEventKind.ParameterBound,
            parameter,
            CreateSingleArgument(parameter, value),
            $"Bound '{parameter}'");
    }

    private void RecordLetEvaluated(string identifier, BytecodeVmValue value)
    {
        if (!_diagnosticsEnabled)
        {
            return;
        }

        RecordDiagnostic(
            GameEventScriptDiagnosticEventKind.LetEvaluated,
            identifier,
            CreateSingleArgument(identifier, value.ToGameEventScriptValue()),
            $"Let '{identifier}' evaluated");
    }

    private void RecordHandlerInvoked(string message, IReadOnlyDictionary<string, GameEventScriptValue> args)
    {
        if (!_diagnosticsEnabled)
        {
            return;
        }

        RecordDiagnostic(
            GameEventScriptDiagnosticEventKind.HandlerInvoked,
            message,
            args,
            $"Handler '{message}' invoked");
    }

    private void RecordRuleCalled(in GameEventScriptBytecodeInstruction instruction, BytecodeVmValue input)
    {
        if (!_diagnosticsEnabled)
        {
            return;
        }

        var ruleName = instruction.DiagnosticName ?? string.Empty;
        var argumentName = instruction.DiagnosticArgumentName ?? "value";
        RecordDiagnostic(
            GameEventScriptDiagnosticEventKind.RuleCalled,
            ruleName,
            CreateSingleArgument(argumentName, input.ToGameEventScriptValue()),
            $"rule '{ruleName}' called");
    }

    private void RecordCallableCalled(in GameEventScriptBytecodeInstruction instruction, BytecodeVmValue[] stack, int start, int count)
    {
        if (!_diagnosticsEnabled)
        {
            return;
        }

        var callableName = instruction.DiagnosticName ?? string.Empty;
        var parameters = instruction.Names ?? [];
        var pairs = new KeyValuePair<string, GameEventScriptValue>[Math.Min(parameters.Length, count)];
        for (var argumentIndex = 0; argumentIndex < pairs.Length; argumentIndex++)
        {
            pairs[argumentIndex] = new KeyValuePair<string, GameEventScriptValue>(
                parameters[argumentIndex],
                stack[start + argumentIndex].ToGameEventScriptValue());
        }

        var kind = instruction.CallableKind == GameEventScriptBytecodeCallableKind.Rule
            ? GameEventScriptDiagnosticEventKind.RuleCalled
            : GameEventScriptDiagnosticEventKind.SelectCalled;
        var kindText = instruction.CallableKind == GameEventScriptBytecodeCallableKind.Rule ? "rule" : "select";
        RecordDiagnostic(
            kind,
            callableName,
            GameEventScriptNamedArguments.CreateOrdered(pairs),
            $"{kindText} '{callableName}' called");
    }

    private void RecordLetExpressionEvaluatedToNothing(string identifier, BytecodeVmValue value)
    {
        if (!_diagnosticsEnabled || value.Kind != BytecodeVmValueKind.Nothing)
        {
            return;
        }

        RecordDiagnostic(
            GameEventScriptDiagnosticEventKind.ExpressionEvaluatedToNothing,
            identifier,
            CreateSingleArgument(identifier, GameEventScriptNothingValue.Instance),
            $"Let '{identifier}' expression evaluated to Nothing.");
    }

    private void RecordPublishArgumentEvaluatedToNothing(string argumentName, BytecodeVmValue value)
    {
        if (!_diagnosticsEnabled || value.Kind != BytecodeVmValueKind.Nothing)
        {
            return;
        }

        RecordDiagnostic(
            GameEventScriptDiagnosticEventKind.ExpressionEvaluatedToNothing,
            argumentName,
            CreateSingleArgument(argumentName, GameEventScriptNothingValue.Instance),
            $"Publish argument '{argumentName}' evaluated to Nothing.");
    }

    private void RecordExpressionStatementEvaluatedToNothing(string? diagnosticName, BytecodeVmValue value)
    {
        if (!_diagnosticsEnabled || value.Kind != BytecodeVmValueKind.Nothing)
        {
            return;
        }

        var name = string.IsNullOrWhiteSpace(diagnosticName) ? "Expression" : diagnosticName;
        RecordDiagnostic(
            GameEventScriptDiagnosticEventKind.ExpressionEvaluatedToNothing,
            name,
            CreateSingleArgument(name, GameEventScriptNothingValue.Instance),
            "Expression statement evaluated to Nothing.");
    }

    private void RecordDiagnostic(
        GameEventScriptDiagnosticEventKind kind,
        string name,
        IReadOnlyDictionary<string, GameEventScriptValue> arguments,
        string? detail = null)
        => GesInvocationKernel.RecordDiagnostic(_context, _diagnosticsEnabled, kind, name, arguments, detail);

    private static GameEventScriptNamedArguments CreateSingleArgument(string name, GameEventScriptValue value)
        => GameEventScriptNamedArguments.CreateOrdered(
        [
            new KeyValuePair<string, GameEventScriptValue>(name, value)
        ]);

    private static bool Fail(out BytecodeVmValue value)
    {
        value = BytecodeVmValue.Nothing;
        return false;
    }

    private static long ToLongSaturated(double value)
    {
        if (value > long.MaxValue) return long.MaxValue;
        if (value < long.MinValue) return long.MinValue;
        return (long)value;
    }

    internal sealed class MessageTagBuilder
    {
        private readonly List<string> _tags = [];
        private readonly HashSet<string> _seen = new(StringComparer.Ordinal);

        public int Count => _tags.Count;

        public void Add(string? tag)
        {
            var normalized = GameEventScriptMessage.NormalizeTagName(tag);
            if (normalized.Length == 0 || !_seen.Add(normalized))
            {
                return;
            }

            _tags.Add(normalized);
        }

        public IReadOnlyList<string> ToArray()
            => _tags.Count == 0 ? [] : _tags.ToArray();
    }

    private readonly record struct LocalChange(int Slot, bool HadValue, BytecodeVmValue PreviousValue);
}

internal enum BytecodeVmValueKind
{
    Nothing,
    Boolean,
    Integer,
    Float,
    Percentage,
    Reference
}

internal readonly record struct BytecodeVmValue(
    BytecodeVmValueKind Kind,
    double Number,
    long IntegerValue,
    bool BooleanValue,
    GameEventScriptNumericUnit? Unit,
    GameEventScriptValue? ReferenceValue)
{
    public static BytecodeVmValue Nothing { get; } = new(BytecodeVmValueKind.Nothing, 0d, 0, false, null, null);

    public static BytecodeVmValue Boolean(bool value)
        => new(BytecodeVmValueKind.Boolean, value ? 1d : 0d, value ? 1 : 0, value, null, null);

    public static BytecodeVmValue Integer(long value, GameEventScriptNumericUnit? unit = null)
        => new(BytecodeVmValueKind.Integer, value, value, value != 0, unit, null);

    public static BytecodeVmValue Float(double value, GameEventScriptNumericUnit? unit = null)
        => new(BytecodeVmValueKind.Float, value, ToLongSaturated(value), value != 0d, unit, null);

    public static BytecodeVmValue Percentage(double ratio)
        => new(BytecodeVmValueKind.Percentage, ratio, ToLongSaturated(ratio * 100d), ratio != 0d, null, null);

    public static BytecodeVmValue Reference(GameEventScriptValue value)
        => new(BytecodeVmValueKind.Reference, 0d, 0, value.AsBoolean(), null, value);

    public static BytecodeVmValue NaN()
        => Reference(GesFloatNaN());

    public static BytecodeVmValue FromGameEventScriptValue(GameEventScriptValue value)
        => value switch
        {
            GameEventScriptBooleanValue boolean => Boolean(boolean.Value),
            GameEventScriptIntegerValue integer => Integer(integer.Value, integer.Unit),
            GameEventScriptFloatValue floatValue when floatValue.HasSemanticValue() => Float(floatValue.Value, floatValue.Unit),
            GameEventScriptPercentageValue percentage => Percentage(percentage.Ratio),
            _ => Reference(value)
        };

    public static BytecodeVmValue FromGameEventScriptFastValue(GameEventScriptFastValue value)
        => value.Kind switch
        {
            GameEventScriptValueKind.Nothing => Nothing,
            GameEventScriptValueKind.Boolean => Boolean(value.Boolean),
            GameEventScriptValueKind.Integer => Integer(value.Integer, value.Unit),
            GameEventScriptValueKind.Float when !value.IsReferenceBacked => Float(value.Number, value.Unit),
            GameEventScriptValueKind.Percentage when !value.IsReferenceBacked => Percentage(value.Number),
            _ => FromGameEventScriptValue(value.ToGameEventScriptValue())
        };

    public static BytecodeVmValue FromBytecodeConstant(GameEventScriptBytecodeConstant constant)
        => constant.Kind switch
        {
            GameEventScriptBytecodeConstantKind.Nothing => Nothing,
            GameEventScriptBytecodeConstantKind.Boolean => Boolean(constant.Boolean),
            GameEventScriptBytecodeConstantKind.Integer => Integer(constant.Integer, constant.Unit),
            GameEventScriptBytecodeConstantKind.Float when constant.IsNaN => Reference(GesFloatNaN()),
            GameEventScriptBytecodeConstantKind.Float when constant.IsInfinity => Reference(constant.IsNegativeInfinity
                ? GesFloatNegativeInfinity()
                : GesFloatInfinity()),
            GameEventScriptBytecodeConstantKind.Float => Float(constant.Number, constant.Unit),
            GameEventScriptBytecodeConstantKind.Percentage => Percentage(constant.Number),
            GameEventScriptBytecodeConstantKind.Text => Reference(GesText(constant.Text ?? string.Empty)),
            GameEventScriptBytecodeConstantKind.Tag => Reference(GesTag(constant.Text ?? string.Empty)),
            GameEventScriptBytecodeConstantKind.Handler => Reference(GesHandler(GameEventScriptMessageSignature.Create(constant.Text ?? string.Empty, constant.Labels))),
            _ => Nothing
        };

    public bool AsBoolean()
        => Kind switch
        {
            BytecodeVmValueKind.Boolean => BooleanValue,
            BytecodeVmValueKind.Integer => IntegerValue != 0,
            BytecodeVmValueKind.Float => Number != 0d,
            BytecodeVmValueKind.Percentage => Number != 0d,
            BytecodeVmValueKind.Reference => ReferenceValue?.AsBoolean() ?? false,
            _ => false
        };

    public bool TryGetFiniteNumber(out double value)
    {
        if (TryGetPrimitiveFiniteNumber(out value) ||
            TryGetNumeric(out var number, out _, out _) && number.IsFinite && SetNumber(number.Value, out value))
        {
            return true;
        }

        value = 0d;
        return false;
    }

    public GameEventScriptValue ToGameEventScriptValue()
        => Kind switch
        {
            BytecodeVmValueKind.Nothing => GameEventScriptNothingValue.Instance,
            BytecodeVmValueKind.Boolean => GameEventScriptValueFactory.GesBoolean(BooleanValue),
            BytecodeVmValueKind.Integer => GameEventScriptValueFactory.GesInteger(IntegerValue, Unit),
            BytecodeVmValueKind.Float => GameEventScriptValueFactory.GesFloat(Number, Unit),
            BytecodeVmValueKind.Percentage => GameEventScriptValueFactory.GesPercentage(Number),
            BytecodeVmValueKind.Reference => ReferenceValue ?? GameEventScriptNothingValue.Instance,
            _ => GameEventScriptNothingValue.Instance
        };

    public static bool AreEqual(in BytecodeVmValue left, in BytecodeVmValue right)
    {
        if (left.Kind == BytecodeVmValueKind.Integer && right.Kind == BytecodeVmValueKind.Integer)
        {
            return left.IntegerValue == right.IntegerValue;
        }

        if (left.TryGetNumeric(out var leftNumber, out var leftUnit, out _) &&
            right.TryGetNumeric(out var rightNumber, out var rightUnit, out _))
        {
            if (leftUnit != rightUnit)
            {
                return false;
            }

            if (leftNumber.IsNaN || rightNumber.IsNaN)
            {
                return false;
            }

            if (leftNumber.IsInfinity || rightNumber.IsInfinity)
            {
                return leftNumber.Kind == rightNumber.Kind;
            }

            return leftNumber.Value == rightNumber.Value;
        }

        return left.ToGameEventScriptValue().Equals(right.ToGameEventScriptValue());
    }

    public static bool AreApproximatelyEqual(in BytecodeVmValue left, in BytecodeVmValue right)
        => GesValueOperations.AreApproximatelyEqual(left.ToGameEventScriptValue(), right.ToGameEventScriptValue());

    public static int CompareNumeric(in BytecodeVmValue left, in BytecodeVmValue right)
    {
        return TryCompareNumeric(left, right, out var comparison) ? comparison : 0;
    }

    public static bool TryCompareNumeric(in BytecodeVmValue left, in BytecodeVmValue right, out int comparison)
    {
        if (left.Kind == BytecodeVmValueKind.Integer && right.Kind == BytecodeVmValueKind.Integer)
        {
            if (left.Unit != right.Unit)
            {
                comparison = default;
                return false;
            }

            comparison = left.IntegerValue.CompareTo(right.IntegerValue);
            return true;
        }

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

        return GesValueOperations.TryCompareNumeric(leftNumber, rightNumber, out comparison);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryComparePrimitiveIntegers(in BytecodeVmValue left, in BytecodeVmValue right, out int comparison)
    {
        if (left.Kind == BytecodeVmValueKind.Integer && right.Kind == BytecodeVmValueKind.Integer)
        {
            if (left.Unit != right.Unit)
            {
                comparison = default;
                return false;
            }

            comparison = left.IntegerValue.CompareTo(right.IntegerValue);
            return true;
        }

        comparison = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryPrimitiveIntegerAdd(in BytecodeVmValue left, in BytecodeVmValue right, out BytecodeVmValue value)
    {
        if (TryGetPrimitiveIntegerOperands(left, right, out var leftInteger, out var rightInteger) &&
            left.Unit == right.Unit &&
            TryAddInteger(leftInteger, rightInteger, out var result))
        {
            value = Integer(result, left.Unit);
            return true;
        }

        value = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryPrimitiveIntegerSubtract(in BytecodeVmValue left, in BytecodeVmValue right, out BytecodeVmValue value)
    {
        if (TryGetPrimitiveIntegerOperands(left, right, out var leftInteger, out var rightInteger) &&
            left.Unit == right.Unit &&
            TrySubtractInteger(leftInteger, rightInteger, out var result))
        {
            value = Integer(result, left.Unit);
            return true;
        }

        value = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryPrimitiveIntegerMultiply(in BytecodeVmValue left, in BytecodeVmValue right, out BytecodeVmValue value)
    {
        if (TryGetPrimitiveIntegerOperands(left, right, out var leftInteger, out var rightInteger) &&
            !(left.Unit.HasValue && right.Unit.HasValue) &&
            TryMultiplyInteger(leftInteger, rightInteger, out var result))
        {
            value = Integer(result, left.Unit ?? right.Unit);
            return true;
        }

        value = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryPrimitiveIntegerDivide(in BytecodeVmValue left, in BytecodeVmValue right, out BytecodeVmValue value)
    {
        if (TryGetPrimitiveIntegerOperands(left, right, out var leftInteger, out var rightInteger) &&
            TryGetDivideResultUnit(left.Unit, right.Unit, out var resultUnit) &&
            rightInteger != 0 &&
            !(leftInteger == long.MinValue && rightInteger == -1))
        {
            value = Float((double)leftInteger / rightInteger, resultUnit);
            return true;
        }

        value = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryPrimitiveIntegerFloorDivide(in BytecodeVmValue left, in BytecodeVmValue right, out BytecodeVmValue value)
    {
        if (TryGetPrimitiveIntegerOperands(left, right, out var leftInteger, out var rightInteger) &&
            TryGetDivideResultUnit(left.Unit, right.Unit, out var resultUnit) &&
            rightInteger != 0 &&
            !(leftInteger == long.MinValue && rightInteger == -1))
        {
            var quotient = leftInteger / rightInteger;
            var remainder = leftInteger % rightInteger;
            if (remainder != 0 && (remainder > 0) != (rightInteger > 0))
            {
                quotient--;
            }

            value = Integer(quotient, resultUnit);
            return true;
        }

        value = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryPrimitiveIntegerModulo(in BytecodeVmValue left, in BytecodeVmValue right, out BytecodeVmValue value)
    {
        if (TryGetPrimitiveIntegerOperands(left, right, out var leftInteger, out var rightInteger) &&
            left.Unit.HasValue == right.Unit.HasValue &&
            (!left.Unit.HasValue || left.Unit == right.Unit) &&
            rightInteger != 0 &&
            !(leftInteger == long.MinValue && rightInteger == -1))
        {
            var modulo = leftInteger % rightInteger;
            if (modulo != 0 &&
                (modulo < 0 && rightInteger > 0 || modulo > 0 && rightInteger < 0))
            {
                modulo += rightInteger;
            }

            value = Integer(modulo, left.Unit);
            return true;
        }

        value = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryPrimitiveIntegerRemainder(in BytecodeVmValue left, in BytecodeVmValue right, out BytecodeVmValue value)
    {
        if (TryGetPrimitiveIntegerOperands(left, right, out var leftInteger, out var rightInteger) &&
            left.Unit.HasValue == right.Unit.HasValue &&
            (!left.Unit.HasValue || left.Unit == right.Unit) &&
            rightInteger != 0 &&
            !(leftInteger == long.MinValue && rightInteger == -1))
        {
            value = Integer(leftInteger % rightInteger, left.Unit);
            return true;
        }

        value = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetPrimitiveIntegerOperands(in BytecodeVmValue left, in BytecodeVmValue right, out long leftInteger, out long rightInteger)
    {
        if (left.Kind == BytecodeVmValueKind.Integer && right.Kind == BytecodeVmValueKind.Integer)
        {
            leftInteger = left.IntegerValue;
            rightInteger = right.IntegerValue;
            return true;
        }

        leftInteger = default;
        rightInteger = default;
        return false;
    }

    public static BytecodeVmValue Add(in BytecodeVmValue left, in BytecodeVmValue right)
    {
        if (TryEvaluateIntegerBinary(left, "+", right, out var integerResult))
        {
            return integerResult;
        }

        if (TryEvaluatePointBinary(left, "+", right, out var pointResult))
        {
            return pointResult;
        }

        if (TryEvaluateVectorBinary(left, "+", right, out var vectorResult))
        {
            return vectorResult;
        }

        if (TryEvaluatePrimitivePercentage(left, "+", right, out var primitivePercentageResult))
        {
            return primitivePercentageResult;
        }

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

            return GesValueOperations.TryAddFinite(leftPrimitive, rightPrimitive, out var sum)
                ? FromFinitePrimitiveNumericResult(left, "+", right, sum, left.Unit)
                : FromFloatNumeric(
                    GesValueOperations.AddNumeric(
                        GesValueOperations.NumericValue.Finite(leftPrimitive),
                        GesValueOperations.NumericValue.Finite(rightPrimitive)),
                    left.Unit);
        }

        if (!left.TryGetNumeric(out var leftNumber, out var leftUnit, out _) ||
            !right.TryGetNumeric(out var rightNumber, out var rightUnit, out _))
        {
            var leftValue = left.ToGameEventScriptValue();
            var rightValue = right.ToGameEventScriptValue();
            if (GesValueOperations.TryCombineWithPlus(leftValue, rightValue, out var combined))
            {
                return FromGameEventScriptValue(combined);
            }

            return leftValue.IsText() || rightValue.IsText()
                ? Reference(GesText($"{GesValueOperations.ToText(leftValue)}{GesValueOperations.ToText(rightValue)}"))
                : NaN();
        }

        if (leftUnit != rightUnit)
        {
            return NaN();
        }

        return FromFloatNumeric(GesValueOperations.AddNumeric(leftNumber, rightNumber), leftUnit);
    }

    public static BytecodeVmValue Subtract(in BytecodeVmValue left, in BytecodeVmValue right)
    {
        if (TryEvaluateIntegerBinary(left, "-", right, out var integerResult))
        {
            return integerResult;
        }

        if (TryEvaluatePointBinary(left, "-", right, out var pointResult))
        {
            return pointResult;
        }

        if (TryEvaluateVectorBinary(left, "-", right, out var vectorResult))
        {
            return vectorResult;
        }

        if (TryEvaluatePrimitivePercentage(left, "-", right, out var primitivePercentageResult))
        {
            return primitivePercentageResult;
        }

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

            return GesValueOperations.TryNegateFinite(rightPrimitive, out var negatedRight) &&
                   GesValueOperations.TryAddFinite(leftPrimitive, negatedRight, out var difference)
                ? FromFinitePrimitiveNumericResult(left, "-", right, difference, left.Unit)
                : FromFloatNumeric(
                    GesValueOperations.SubtractNumeric(
                        GesValueOperations.NumericValue.Finite(leftPrimitive),
                        GesValueOperations.NumericValue.Finite(rightPrimitive)),
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

        return FromFloatNumeric(GesValueOperations.SubtractNumeric(leftNumber, rightNumber), leftUnit);
    }

    public static BytecodeVmValue Multiply(in BytecodeVmValue left, in BytecodeVmValue right)
    {
        if (TryEvaluateIntegerBinary(left, "*", right, out var integerResult))
        {
            return integerResult;
        }

        if (TryEvaluatePointBinary(left, "*", right, out var pointResult))
        {
            return pointResult;
        }

        if (TryEvaluateVectorBinary(left, "*", right, out var vectorResult))
        {
            return vectorResult;
        }

        if (TryEvaluatePrimitivePercentage(left, "*", right, out var primitivePercentageResult))
        {
            return primitivePercentageResult;
        }

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

            return GesValueOperations.TryMultiplyFinite(leftPrimitive, rightPrimitive, out var product)
                ? FromFinitePrimitiveNumericResult(left, "*", right, product, left.Unit ?? right.Unit)
                : FromFloatNumeric(
                    GesValueOperations.MultiplyNumeric(
                        GesValueOperations.NumericValue.Finite(leftPrimitive),
                        GesValueOperations.NumericValue.Finite(rightPrimitive)),
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

        return FromFloatNumeric(GesValueOperations.MultiplyNumeric(leftNumber, rightNumber), leftUnit ?? rightUnit);
    }

    public static BytecodeVmValue Divide(in BytecodeVmValue left, in BytecodeVmValue right)
    {
        if (TryEvaluateIntegerBinary(left, "/", right, out var integerResult))
        {
            return integerResult;
        }

        if (TryEvaluatePointBinary(left, "/", right, out var pointResult))
        {
            return pointResult;
        }

        if (TryEvaluateVectorBinary(left, "/", right, out var vectorResult))
        {
            return vectorResult;
        }

        if (TryEvaluatePrimitivePercentage(left, "/", right, out var primitivePercentageResult))
        {
            return primitivePercentageResult;
        }

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

            return rightPrimitive != 0d && GesValueOperations.TryDivideFinite(leftPrimitive, rightPrimitive, out var quotient)
                ? Float(quotient, primitiveResultUnit)
                : FromFloatNumeric(
                    GesValueOperations.DivideNumeric(
                        GesValueOperations.NumericValue.Finite(leftPrimitive),
                        GesValueOperations.NumericValue.Finite(rightPrimitive)),
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

        return FromFloatNumeric(GesValueOperations.DivideNumeric(leftNumber, rightNumber), resultUnit);
    }

    public static BytecodeVmValue IntegerDivide(in BytecodeVmValue left, in BytecodeVmValue right)
    {
        if (TryEvaluateIntegerBinary(left, "div", right, out var integerResult))
        {
            return integerResult;
        }

        if (TryEvaluatePointBinary(left, "div", right, out var pointResult))
        {
            return pointResult;
        }

        if (TryEvaluateVectorBinary(left, "div", right, out var vectorResult))
        {
            return vectorResult;
        }

        if (left.TryGetPrimitiveFiniteNumber(out var leftPrimitive) &&
            right.TryGetPrimitiveFiniteNumber(out var rightPrimitive))
        {
            if (!TryGetDivideResultUnit(left.Unit, right.Unit, out var primitiveResultUnit))
            {
                return NaN();
            }

            return rightPrimitive != 0d && GesValueOperations.TryDivideFinite(leftPrimitive, rightPrimitive, out var quotient)
                ? FromFinitePrimitiveNumericResult(left, "div", right, Math.Floor(quotient), primitiveResultUnit)
                : FromFloatNumeric(
                    GesValueOperations.IntegerDivideNumeric(
                        GesValueOperations.NumericValue.Finite(leftPrimitive),
                        GesValueOperations.NumericValue.Finite(rightPrimitive)),
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

        var result = GesValueOperations.IntegerDivideNumeric(leftNumber, rightNumber);
        return resultUnit is null && result.IsFinite && TryToInteger(result.Value, out var integer)
            ? Integer(integer)
            : FromFloatNumeric(result, resultUnit);
    }

    public static BytecodeVmValue Modulo(in BytecodeVmValue left, in BytecodeVmValue right)
    {
        if (TryEvaluateIntegerBinary(left, "mod", right, out var integerResult))
        {
            return integerResult;
        }

        if (TryEvaluatePointBinary(left, "mod", right, out var pointResult))
        {
            return pointResult;
        }

        if (TryEvaluateVectorBinary(left, "mod", right, out var vectorResult))
        {
            return vectorResult;
        }

        if (left.TryGetPrimitiveFiniteNumber(out var leftPrimitive) &&
            right.TryGetPrimitiveFiniteNumber(out var rightPrimitive))
        {
            if (left.Unit.HasValue != right.Unit.HasValue ||
                left.Unit.HasValue && right.Unit.HasValue && left.Unit != right.Unit)
            {
                return NaN();
            }

            return rightPrimitive != 0d && GesValueOperations.TryModuloFinite(leftPrimitive, rightPrimitive, out var modulo)
                ? FromFinitePrimitiveNumericResult(left, "mod", right, modulo, left.Unit)
                : FromFloatNumeric(
                    GesValueOperations.ModuloNumeric(
                        GesValueOperations.NumericValue.Finite(leftPrimitive),
                        GesValueOperations.NumericValue.Finite(rightPrimitive)),
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

        return FromFloatNumeric(GesValueOperations.ModuloNumeric(leftNumber, rightNumber), leftUnit);
    }

    public static BytecodeVmValue Remainder(in BytecodeVmValue left, in BytecodeVmValue right)
    {
        if (TryEvaluateIntegerBinary(left, "rem", right, out var integerResult))
        {
            return integerResult;
        }

        if (TryEvaluatePointBinary(left, "rem", right, out var pointResult))
        {
            return pointResult;
        }

        if (TryEvaluateVectorBinary(left, "rem", right, out var vectorResult))
        {
            return vectorResult;
        }

        if (left.TryGetPrimitiveFiniteNumber(out var leftPrimitive) &&
            right.TryGetPrimitiveFiniteNumber(out var rightPrimitive))
        {
            if (left.Unit.HasValue != right.Unit.HasValue ||
                left.Unit.HasValue && right.Unit.HasValue && left.Unit != right.Unit)
            {
                return NaN();
            }

            return rightPrimitive != 0d && GesValueOperations.TryRemainderFinite(leftPrimitive, rightPrimitive, out var remainder)
                ? FromFinitePrimitiveNumericResult(left, "rem", right, remainder, left.Unit)
                : FromFloatNumeric(
                    GesValueOperations.RemainderNumeric(
                        GesValueOperations.NumericValue.Finite(leftPrimitive),
                        GesValueOperations.NumericValue.Finite(rightPrimitive)),
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

        return FromFloatNumeric(GesValueOperations.RemainderNumeric(leftNumber, rightNumber), leftUnit);
    }

    public static BytecodeVmValue Power(in BytecodeVmValue left, in BytecodeVmValue right)
    {
        if (GesValueOperations.TryEvaluateUnitBinary(left.ToGameEventScriptValue(), "^", right.ToGameEventScriptValue(), out var unitResult))
        {
            return FromGameEventScriptValue(unitResult);
        }

        if (!left.TryGetNumeric(out var leftNumber, out var leftUnit, out _) ||
            !right.TryGetNumeric(out var rightNumber, out var rightUnit, out _))
        {
            return NaN();
        }

        if (leftUnit.HasValue || rightUnit.HasValue)
        {
            return NaN();
        }

        var result = GesValueOperations.PowerNumeric(leftNumber, rightNumber);
        return result.IsFinite &&
               left.Kind == BytecodeVmValueKind.Integer &&
               right.Kind == BytecodeVmValueKind.Integer &&
               TryToInteger(result.Value, out var integer)
            ? Integer(integer)
            : FromFloatNumeric(result);
    }

    private static bool TryGetDivideResultUnit(
        GameEventScriptNumericUnit? leftUnit,
        GameEventScriptNumericUnit? rightUnit,
        out GameEventScriptNumericUnit? resultUnit)
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

    private static bool TryEvaluatePrimitivePercentage(in BytecodeVmValue left, string operation, in BytecodeVmValue right, out BytecodeVmValue value)
    {
        value = default;
        if (!TryGetPrimitiveNumeric(left, out var leftNumber, out var leftUnit, out var leftIsPercentage) ||
            !TryGetPrimitiveNumeric(right, out var rightNumber, out var rightUnit, out var rightIsPercentage) ||
            !leftIsPercentage && !rightIsPercentage)
        {
            return false;
        }

        switch (operation)
        {
            case "+":
                if (leftIsPercentage && rightIsPercentage)
                {
                    value = GesValueOperations.TryAddFinite(leftNumber, rightNumber, out var percentageSum)
                        ? Percentage(percentageSum)
                        : AddPercentage(left, right);
                    return true;
                }

                if (leftIsPercentage)
                {
                    value = NaN();
                    return true;
                }

                if (GesValueOperations.TryMultiplyFinite(leftNumber, rightNumber, out var addDelta) &&
                    GesValueOperations.TryAddFinite(leftNumber, addDelta, out var addResult))
                {
                    value = Float(addResult, leftUnit);
                    return true;
                }

                value = AddPercentage(left, right);
                return true;

            case "-":
                if (leftIsPercentage && rightIsPercentage)
                {
                    value = GesValueOperations.TryNegateFinite(rightNumber, out var negatedRight) &&
                            GesValueOperations.TryAddFinite(leftNumber, negatedRight, out var percentageDifference)
                        ? Percentage(percentageDifference)
                        : SubtractPercentage(left, right);
                    return true;
                }

                if (leftIsPercentage)
                {
                    value = NaN();
                    return true;
                }

                if (GesValueOperations.TryMultiplyFinite(leftNumber, rightNumber, out var subtractDelta) &&
                    GesValueOperations.TryNegateFinite(subtractDelta, out var negatedDelta) &&
                    GesValueOperations.TryAddFinite(leftNumber, negatedDelta, out var subtractResult))
                {
                    value = Float(subtractResult, leftUnit);
                    return true;
                }

                value = SubtractPercentage(left, right);
                return true;

            case "*":
                if (GesValueOperations.TryMultiplyFinite(leftNumber, rightNumber, out var product))
                {
                    value = leftIsPercentage && rightIsPercentage || leftIsPercentage && rightUnit is null
                        ? Percentage(product)
                        : Float(product, leftIsPercentage ? rightUnit : leftUnit);
                    return true;
                }

                value = MultiplyPercentage(left, right);
                return true;

            case "/":
                if (leftIsPercentage && rightUnit.HasValue)
                {
                    value = NaN();
                    return true;
                }

                if (rightNumber != 0d &&
                    GesValueOperations.TryDivideFinite(leftNumber, rightNumber, out var quotient))
                {
                    value = leftIsPercentage && rightIsPercentage
                        ? Float(quotient)
                        : leftIsPercentage
                            ? Percentage(quotient)
                            : Float(quotient, leftUnit);
                    return true;
                }

                value = DividePercentage(left, right);
                return true;

            default:
                return false;
        }
    }

    private static bool TryGetPrimitiveNumeric(
        in BytecodeVmValue input,
        out double number,
        out GameEventScriptNumericUnit? unit,
        out bool isPercentage)
    {
        switch (input.Kind)
        {
            case BytecodeVmValueKind.Boolean:
                number = input.BooleanValue ? 1d : 0d;
                unit = null;
                isPercentage = false;
                return true;
            case BytecodeVmValueKind.Integer:
                number = input.IntegerValue;
                unit = input.Unit;
                isPercentage = false;
                return true;
            case BytecodeVmValueKind.Float:
                number = input.Number;
                unit = input.Unit;
                isPercentage = false;
                return true;
            case BytecodeVmValueKind.Percentage:
                number = input.Number;
                unit = null;
                isPercentage = true;
                return true;
            default:
                number = default;
                unit = null;
                isPercentage = false;
                return false;
        }
    }

    private static BytecodeVmValue AddPercentage(in BytecodeVmValue left, in BytecodeVmValue right)
    {
        if (!left.TryGetNumeric(out var leftNumber, out var leftUnit, out var leftIsPercentage) ||
            !right.TryGetNumeric(out var rightNumber, out _, out var rightIsPercentage))
        {
            return NaN();
        }

        if (leftIsPercentage && rightIsPercentage)
        {
            return FromPercentageNumeric(GesValueOperations.AddNumeric(leftNumber, rightNumber));
        }

        if (leftIsPercentage)
        {
            return NaN();
        }

        var delta = GesValueOperations.MultiplyNumeric(leftNumber, rightNumber);
        return FromFloatNumeric(GesValueOperations.AddNumeric(leftNumber, delta), leftUnit);
    }

    private static BytecodeVmValue SubtractPercentage(in BytecodeVmValue left, in BytecodeVmValue right)
    {
        if (!left.TryGetNumeric(out var leftNumber, out var leftUnit, out var leftIsPercentage) ||
            !right.TryGetNumeric(out var rightNumber, out _, out var rightIsPercentage))
        {
            return NaN();
        }

        if (leftIsPercentage && rightIsPercentage)
        {
            return FromPercentageNumeric(GesValueOperations.SubtractNumeric(leftNumber, rightNumber));
        }

        if (leftIsPercentage)
        {
            return NaN();
        }

        var delta = GesValueOperations.MultiplyNumeric(leftNumber, rightNumber);
        return FromFloatNumeric(GesValueOperations.SubtractNumeric(leftNumber, delta), leftUnit);
    }

    private static BytecodeVmValue MultiplyPercentage(in BytecodeVmValue left, in BytecodeVmValue right)
    {
        if (!left.TryGetNumeric(out var leftNumber, out var leftUnit, out var leftIsPercentage) ||
            !right.TryGetNumeric(out var rightNumber, out var rightUnit, out var rightIsPercentage))
        {
            return NaN();
        }

        var result = GesValueOperations.MultiplyNumeric(leftNumber, rightNumber);
        if (leftIsPercentage && rightIsPercentage)
        {
            return FromPercentageNumeric(result);
        }

        if (leftIsPercentage)
        {
            return rightUnit is { } unit
                ? FromFloatNumeric(result, unit)
                : FromPercentageNumeric(result);
        }

        return FromFloatNumeric(result, leftUnit);
    }

    private static BytecodeVmValue DividePercentage(in BytecodeVmValue left, in BytecodeVmValue right)
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

        var result = GesValueOperations.DivideNumeric(leftNumber, rightNumber);
        if (leftIsPercentage && rightIsPercentage)
        {
            return FromFloatNumeric(result);
        }

        if (leftIsPercentage)
        {
            return FromPercentageNumeric(result);
        }

        return FromFloatNumeric(result, leftUnit);
    }

    private bool TryGetNumeric(
        out GesValueOperations.NumericValue number,
        out GameEventScriptNumericUnit? unit,
        out bool isPercentage)
    {
        switch (Kind)
        {
            case BytecodeVmValueKind.Boolean:
                number = GesValueOperations.NumericValue.Finite(BooleanValue ? 1d : 0d);
                unit = null;
                isPercentage = false;
                return true;
            case BytecodeVmValueKind.Integer:
                number = GesValueOperations.NumericValue.Finite(IntegerValue);
                unit = Unit;
                isPercentage = false;
                return true;
            case BytecodeVmValueKind.Float:
                number = GesValueOperations.NumericValue.Finite(Number);
                unit = Unit;
                isPercentage = false;
                return true;
            case BytecodeVmValueKind.Percentage:
                number = GesValueOperations.NumericValue.Finite(Number);
                unit = null;
                isPercentage = true;
                return true;
            case BytecodeVmValueKind.Reference when ReferenceValue is { } reference &&
                                                       GesValueOperations.TryCoerceNumericForOperation(reference, out var referenceNumber):
                number = referenceNumber;
                unit = GameEventScriptValue.TryGetNumericUnit(reference, out var referenceUnit) ? referenceUnit : null;
                isPercentage = reference.IsPercentage();
                return true;
            default:
                number = default;
                unit = null;
                isPercentage = false;
                return false;
        }
    }

    private bool TryGetPrimitiveFiniteNumber(out double number)
    {
        switch (Kind)
        {
            case BytecodeVmValueKind.Boolean:
                number = BooleanValue ? 1d : 0d;
                return true;
            case BytecodeVmValueKind.Integer:
                number = IntegerValue;
                return true;
            case BytecodeVmValueKind.Float:
            case BytecodeVmValueKind.Percentage:
                number = Number;
                return true;
            default:
                number = default;
                return false;
        }
    }

    private bool IsPercentageLike()
        => Kind == BytecodeVmValueKind.Percentage ||
           ReferenceValue is { } reference && reference.IsPercentage();

    private bool IsVectorLike()
        => ReferenceValue is GameEventScriptVectorValue;

    private bool IsPointLike()
        => ReferenceValue is GameEventScriptPointValue;

    private static bool TryEvaluatePointBinary(in BytecodeVmValue left, string operation, in BytecodeVmValue right, out BytecodeVmValue value)
    {
        if (!left.IsPointLike() && !right.IsPointLike())
        {
            value = default;
            return false;
        }

        if (GesValueOperations.TryEvaluatePointBinary(left.ToGameEventScriptValue(), operation, right.ToGameEventScriptValue(), out var pointValue))
        {
            value = FromGameEventScriptValue(pointValue);
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryEvaluateVectorBinary(in BytecodeVmValue left, string operation, in BytecodeVmValue right, out BytecodeVmValue value)
    {
        if (!left.IsVectorLike() && !right.IsVectorLike())
        {
            value = default;
            return false;
        }

        if (GesValueOperations.TryEvaluateVectorBinary(left.ToGameEventScriptValue(), operation, right.ToGameEventScriptValue(), out var vectorValue))
        {
            value = FromGameEventScriptValue(vectorValue);
            return true;
        }

        value = default;
        return false;
    }

    public bool IsNothingLike()
        => Kind == BytecodeVmValueKind.Nothing ||
           ReferenceValue is { } reference && reference.IsNothing();

    private static BytecodeVmValue FromFloatNumeric(GesValueOperations.NumericValue number, GameEventScriptNumericUnit? unit = null)
        => number.IsFinite
            ? Float(number.Value, unit)
            : Reference(GesValueOperations.ToGameEventScriptFloat(number));

    private static BytecodeVmValue FromFinitePrimitiveNumericResult(
        BytecodeVmValue left,
        string operation,
        BytecodeVmValue right,
        double value,
        GameEventScriptNumericUnit? unit = null)
        => operation == "div" &&
           TryToInteger(value, out var quotient)
            ? Integer(quotient, unit)
            : operation is "+" or "-" or "*" or "mod" or "rem" &&
           left.Kind == BytecodeVmValueKind.Integer &&
           right.Kind == BytecodeVmValueKind.Integer &&
           TryToInteger(value, out var integer)
            ? Integer(integer, unit)
            : Float(value, unit);

    private static BytecodeVmValue FromPercentageNumeric(GesValueOperations.NumericValue number)
        => number.IsFinite
            ? Percentage(number.Value)
            : FromFloatNumeric(number);

    private static bool TryEvaluateIntegerBinary(in BytecodeVmValue left, string operation, in BytecodeVmValue right, out BytecodeVmValue value)
    {
        if (left.Kind != BytecodeVmValueKind.Integer || right.Kind != BytecodeVmValueKind.Integer)
        {
            value = default;
            return false;
        }

        var leftInteger = left.IntegerValue;
        var rightInteger = right.IntegerValue;
        switch (operation)
        {
            case "+":
                if (left.Unit == right.Unit &&
                    TryAddInteger(leftInteger, rightInteger, out var sum))
                {
                    value = Integer(sum, left.Unit);
                    return true;
                }

                break;

            case "-":
                if (left.Unit == right.Unit &&
                    TrySubtractInteger(leftInteger, rightInteger, out var difference))
                {
                    value = Integer(difference, left.Unit);
                    return true;
                }

                break;

            case "*":
                if (!(left.Unit.HasValue && right.Unit.HasValue) &&
                    TryMultiplyInteger(leftInteger, rightInteger, out var product))
                {
                    value = Integer(product, left.Unit ?? right.Unit);
                    return true;
                }

                break;

            case "/":
                if (TryGetDivideResultUnit(left.Unit, right.Unit, out var divideUnit) &&
                    rightInteger != 0 &&
                    !(leftInteger == long.MinValue && rightInteger == -1))
                {
                    value = Float((double)leftInteger / rightInteger, divideUnit);
                    return true;
                }

                break;

            case "div":
                if (TryGetDivideResultUnit(left.Unit, right.Unit, out var integerDivideUnit) &&
                    rightInteger != 0 &&
                    !(leftInteger == long.MinValue && rightInteger == -1))
                {
                    var quotient = leftInteger / rightInteger;
                    var remainder = leftInteger % rightInteger;
                    if (remainder != 0 && (remainder > 0) != (rightInteger > 0))
                    {
                        quotient--;
                    }

                    value = Integer(quotient, integerDivideUnit);
                    return true;
                }

                break;

            case "mod":
                if (left.Unit.HasValue == right.Unit.HasValue &&
                    (!left.Unit.HasValue || left.Unit == right.Unit) &&
                    rightInteger != 0 &&
                    !(leftInteger == long.MinValue && rightInteger == -1))
                {
                    var modulo = leftInteger % rightInteger;
                    if (modulo != 0 &&
                        (modulo < 0 && rightInteger > 0 || modulo > 0 && rightInteger < 0))
                    {
                        modulo += rightInteger;
                    }

                    value = Integer(modulo, left.Unit);
                    return true;
                }

                break;

            case "rem":
                if (left.Unit.HasValue == right.Unit.HasValue &&
                    (!left.Unit.HasValue || left.Unit == right.Unit) &&
                    rightInteger != 0 &&
                    !(leftInteger == long.MinValue && rightInteger == -1))
                {
                    value = Integer(leftInteger % rightInteger, left.Unit);
                    return true;
                }

                break;
        }

        value = default;
        return false;
    }

    private static bool SetNumber(double input, out double output)
    {
        output = input;
        return true;
    }

    private static bool TryAddInteger(long left, long right, out long value)
    {
        value = left + right;
        return ((left ^ value) & (right ^ value)) >= 0;
    }

    private static bool TrySubtractInteger(long left, long right, out long value)
    {
        value = left - right;
        return ((left ^ right) & (left ^ value)) >= 0;
    }

    private static bool TryMultiplyInteger(long left, long right, out long value)
    {
        if (left == 0 || right == 0)
        {
            value = 0;
            return true;
        }

        if (left == -1)
        {
            if (right == long.MinValue)
            {
                value = default;
                return false;
            }

            value = -right;
            return true;
        }

        if (right == -1)
        {
            if (left == long.MinValue)
            {
                value = default;
                return false;
            }

            value = -left;
            return true;
        }

        value = left * right;
        return value / right == left;
    }

    private static bool TryToInteger(double value, out long integer)
    {
        if (value != Math.Truncate(value) ||
            value > long.MaxValue ||
            value < long.MinValue)
        {
            integer = default;
            return false;
        }

        integer = (long)value;
        return true;
    }

    private static long ToLongSaturated(double value)
    {
        if (value > long.MaxValue) return long.MaxValue;
        if (value < long.MinValue) return long.MinValue;
        return (long)value;
    }
}
