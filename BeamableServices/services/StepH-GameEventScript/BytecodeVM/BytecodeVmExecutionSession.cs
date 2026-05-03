#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Compiler;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptValueFactory;

namespace StepH.GameEventScript.BytecodeVM;

internal sealed class BytecodeVmExecutionSession
{
    private const string CallableCallDepthExceededDetail = "Callable exceeded the configured call depth.";

    private readonly CompiledGameEventScript _compiledScript;
    private readonly GameEventScriptContext _context;
    private readonly BytecodeVmExecutionPlan _plan;
    private readonly BytecodeVmValue[] _locals;
    private readonly bool[] _assignedSlots;
    private readonly BytecodeVmValue[] _evaluationStack;
    private readonly bool _diagnosticsEnabled;
    private readonly List<LocalChange> _changes = [];
    private readonly List<int> _scopeMarks = [];
    private readonly Stack<GameEventScriptRandomGenerator> _randomScopes = new();
    private bool _halted;

    private BytecodeVmExecutionSession(
        CompiledGameEventScript compiledScript,
        GameEventScriptContext context,
        BytecodeVmExecutionPlan plan,
        bool diagnosticsEnabled)
    {
        _compiledScript = compiledScript;
        _context = context;
        _plan = plan;
        _diagnosticsEnabled = diagnosticsEnabled;
        _locals = new BytecodeVmValue[plan.SlotCount];
        _assignedSlots = new bool[plan.SlotCount];
        _evaluationStack = new BytecodeVmValue[Math.Max(16, plan.MaxStackDepth + 16)];
        _randomScopes.Push(context.Random);
    }

    public static void InvokeHandler(
        CompiledGameEventScript compiledScript,
        GameEventScriptContext context,
        CompiledGameEventScriptHandler handler,
        IReadOnlyDictionary<string, GameEventScriptValue> args)
    {
        var session = new BytecodeVmExecutionSession(compiledScript, context, handler.ExecutionPlan, handler.DiagnosticsEnabled);
        if (!session.TryInvoke(handler, args))
        {
            throw new InvalidOperationException(
                $"BytecodeVM invariant failed: handler '{handler.SignatureId}' #{handler.DeclarationOrder} could not be executed by its compiled execution plan.");
        }
    }

    private bool TryInvoke(CompiledGameEventScriptHandler handler, IReadOnlyDictionary<string, GameEventScriptValue> args)
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

                RecordParameterBound(parameter, value);

                if (!Define(parameter, BytecodeVmValue.FromGameEventScriptValue(value)))
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

    private static bool TryGetArgumentValue(IReadOnlyDictionary<string, GameEventScriptValue> args, string parameter, int parameterIndex, out GameEventScriptValue value)
    {
        if (args is GameEventScriptNamedArguments namedArguments && parameterIndex >= 0 && parameterIndex < namedArguments.Count)
        {
            value = namedArguments[parameterIndex];
            return true;
        }

        return args.TryGetValue(parameter, out value!);
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

        PushSeededRandomScope(seed.ToGameEventScriptValue());
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

        var length = GameEventScriptRuntimeLimitUtilities.GetRangeLength(from, to, step);
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
        if (!TryEvaluate(sourceExpression, out var sourceVmValue))
        {
            return false;
        }

        var sourceValue = sourceVmValue.ToGameEventScriptValue();
        if (GameEventScriptRuntimeLimitUtilities.TryGetRangeLength(sourceValue, out var length) &&
            !_context.RuntimeBudget.TryCheckRangeLength(length, "Iteration source would enumerate more range items than allowed."))
        {
            return true;
        }

        foreach (var item in sourceValue.AsEnumerable())
        {
            if (!TryExecuteLoopIteration(forStatement, BytecodeVmValue.FromGameEventScriptValue(item)))
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
        => TryExecuteLoopIteration(forStatement, BytecodeVmValue.Integer(item));

    private bool TryExecuteLoopIteration(ForStatementNode forStatement, BytecodeVmValue item)
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

    private bool TryEvaluateMessageArguments(MessageLiteralExpressionNode message, out IReadOnlyDictionary<string, GameEventScriptValue> arguments)
    {
        if (message.Arguments.Count == 0)
        {
            arguments = GameEventScriptNamedArguments.Empty;
            return true;
        }

        var pairs = new KeyValuePair<string, GameEventScriptValue>[message.Arguments.Count];
        for (var argumentIndex = 0; argumentIndex < message.Arguments.Count; argumentIndex++)
        {
            var argument = message.Arguments[argumentIndex];
            if (!TryEvaluate(argument.Expression, out var value))
            {
                arguments = GameEventScriptNamedArguments.Empty;
                return false;
            }

            pairs[argumentIndex] = new KeyValuePair<string, GameEventScriptValue>(argument.Name, value.ToGameEventScriptValue());
        }

        arguments = GameEventScriptNamedArguments.CreateOrdered(pairs);
        return true;
    }

    private bool TryPublish(BytecodeVmPublishLayout layout)
    {
        var argumentNames = layout.ArgumentNames;
        var argumentPrograms = layout.ArgumentPrograms;
        if (argumentNames.Length == 0)
        {
            _context.Publish(GameEventScriptMessage.CreatePrecomputed(
                layout.MessageName,
                GameEventScriptNamedArguments.Empty,
                layout.SignatureId));
            return true;
        }

        var pairs = new KeyValuePair<string, GameEventScriptValue>[argumentNames.Length];
        for (var argumentIndex = 0; argumentIndex < argumentNames.Length; argumentIndex++)
        {
            if (!TryExecuteExpressionProgram(argumentPrograms[argumentIndex], 0, out var value))
            {
                return false;
            }

            RecordPublishArgumentEvaluatedToNothing(argumentNames[argumentIndex], value);

            pairs[argumentIndex] = new KeyValuePair<string, GameEventScriptValue>(
                argumentNames[argumentIndex],
                value.ToGameEventScriptValue());
        }

        _context.Publish(GameEventScriptMessage.CreatePrecomputed(
            layout.MessageName,
            GameEventScriptNamedArguments.CreateOrdered(pairs),
            layout.SignatureId));
        return true;
    }

    private bool TryPublish(ExpressionNode messageExpression)
    {
        if (!TryEvaluate(messageExpression, out var publishValue))
        {
            return false;
        }

        var boxed = publishValue.ToGameEventScriptValue();
        if (GameEventScriptMessageValueCodec.TryReadMessageValue(boxed, out var message))
        {
            _context.Publish(message);
        }

        return true;
    }

    private bool TryEvaluate(ExpressionNode expression, out BytecodeVmValue value)
    {
        if (_plan.TryGetExpressionProgram(expression, out var program))
        {
            return TryExecuteExpressionProgram(program, 0, out value);
        }

        if (!TryConsumeExecutionStep("Expression evaluation budget exhausted."))
        {
            value = BytecodeVmValue.Nothing;
            return true;
        }

        switch (expression)
        {
            case BooleanLiteralExpressionNode boolean:
                value = BytecodeVmValue.Boolean(boolean.Value);
                return true;
            case IntegerLiteralExpressionNode integer:
                value = BytecodeVmValue.Integer(integer.Value);
                return true;
            case DecimalLiteralExpressionNode decimalLiteral:
                value = BytecodeVmValue.Decimal(decimalLiteral.Value);
                return true;
            case PercentageLiteralExpressionNode percentage:
                value = BytecodeVmValue.Percentage(percentage.PercentValue / 100m);
                return true;
            case UnitDecimalLiteralExpressionNode unitDecimal:
                value = GameEventScriptDecimalUnits.TryParseTypeName(unitDecimal.UnitName, out var unit)
                    ? BytecodeVmValue.Decimal(unitDecimal.Value, unit)
                    : BytecodeVmValue.NaN();
                return true;
            case TextLiteralExpressionNode text:
                value = BytecodeVmValue.Reference(GesText(text.Value));
                return true;
            case TagLiteralExpressionNode tag:
                value = BytecodeVmValue.Reference(GesTag(tag.Name));
                return true;
            case HandlerLiteralExpressionNode handler:
                value = BytecodeVmValue.Reference(GesHandler(GameEventScriptMessageSignature.Create(handler.Message, handler.SignatureLabels)));
                return true;
            case HandlerBindExpressionNode handlerBind:
                return TryEvaluateHandlerBind(handlerBind, out value);
            case ListLiteralExpressionNode list:
                return TryEvaluateListLiteral(list, out value);
            case SequenceLiteralExpressionNode sequence:
                return TryEvaluateSequenceLiteral(sequence, out value);
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
            case GeneratedCollectionExpressionNode generatedCollection:
                return TryEvaluateGeneratedCollectionExpression(generatedCollection, out value);
            case GuardedChoiceExpressionNode guardedChoice:
                return TryEvaluateGuardedChoiceExpression(guardedChoice, out value);
            case BinaryExpressionNode binary:
                return TryEvaluateBinary(binary, out value);
            case RulePredicateExpressionNode rulePredicate:
                return TryEvaluateRulePredicate(rulePredicate, out value);
            case ExtensionPredicateExpressionNode extensionPredicate:
                return TryEvaluateExtensionPredicate(extensionPredicate, out value);
            case CallExpressionNode call:
                return TryEvaluateCall(call, out value);
            case ExtensionCallExpressionNode extensionCall:
                return TryEvaluateExtensionCall(extensionCall, out value);
            case TypeConstructorExpressionNode typeConstructor:
                return TryEvaluateTypeConstructor(typeConstructor, out value);
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
                    value = BytecodeVmValue.Nothing;
                    return false;
                }

                value = BytecodeVmValue.Reference(GesMessage(GameEventScriptMessage.Create(message.Message, arguments)));
                return true;
            default:
                value = BytecodeVmValue.Nothing;
                return false;
        }
    }

    private bool TryExecuteExpressionProgram(BytecodeVmExpressionProgram program, int stackBase, out BytecodeVmValue value)
    {
        if (stackBase + program.MaxStackDepth > _evaluationStack.Length)
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        var top = stackBase;
        var instructions = program.Instructions;
        if (!TryConsumeExecutionSteps(instructions.Length, "Expression evaluation budget exhausted."))
        {
            value = BytecodeVmValue.Nothing;
            return true;
        }

        for (var instructionIndex = 0; instructionIndex < instructions.Length; instructionIndex++)
        {
            var instruction = instructions[instructionIndex];
            switch (instruction.OpCode)
            {
                case BytecodeVmProgramOpCode.LoadConstant:
                    _evaluationStack[top++] = instruction.Constant;
                    break;

                case BytecodeVmProgramOpCode.LoadSlot:
                    _evaluationStack[top++] = ResolveSlot(instruction.A);
                    break;

                case BytecodeVmProgramOpCode.Or:
                case BytecodeVmProgramOpCode.Xor:
                case BytecodeVmProgramOpCode.And:
                case BytecodeVmProgramOpCode.Equal:
                case BytecodeVmProgramOpCode.NotEqual:
                case BytecodeVmProgramOpCode.Less:
                case BytecodeVmProgramOpCode.Greater:
                case BytecodeVmProgramOpCode.LessOrEqual:
                case BytecodeVmProgramOpCode.GreaterOrEqual:
                case BytecodeVmProgramOpCode.Add:
                case BytecodeVmProgramOpCode.Subtract:
                case BytecodeVmProgramOpCode.Multiply:
                case BytecodeVmProgramOpCode.Divide:
                case BytecodeVmProgramOpCode.IntegerDivide:
                case BytecodeVmProgramOpCode.Modulo:
                case BytecodeVmProgramOpCode.Remainder:
                case BytecodeVmProgramOpCode.Default:
                case BytecodeVmProgramOpCode.Contains:
                case BytecodeVmProgramOpCode.ContainsValue:
                case BytecodeVmProgramOpCode.StartsWith:
                case BytecodeVmProgramOpCode.EndsWith:
                case BytecodeVmProgramOpCode.Intersect:
                case BytecodeVmProgramOpCode.Combine:
                case BytecodeVmProgramOpCode.Except:
                case BytecodeVmProgramOpCode.Zip:
                    var right = _evaluationStack[--top];
                    var left = _evaluationStack[--top];
                    _evaluationStack[top++] = EvaluateProgramBinary(instruction.OpCode, left, right);
                    break;

                case BytecodeVmProgramOpCode.Unary:
                    if (!TryEvaluateUnaryOperation(instruction.DiagnosticName, _evaluationStack[top - 1], out var unaryValue))
                    {
                        value = BytecodeVmValue.Nothing;
                        return false;
                    }

                    _evaluationStack[top - 1] = unaryValue;
                    break;

                case BytecodeVmProgramOpCode.Variadic:
                    top -= instruction.A;
                    if (!TryEvaluateVariadicOperation(instruction.DiagnosticName, _evaluationStack, top, instruction.A, out var variadicValue))
                    {
                        value = BytecodeVmValue.Nothing;
                        return false;
                    }

                    _evaluationStack[top++] = variadicValue;
                    break;

                case BytecodeVmProgramOpCode.Clamp:
                    top -= 3;
                    _evaluationStack[top] = EvaluateClamp(
                        _evaluationStack[top],
                        _evaluationStack[top + 1],
                        _evaluationStack[top + 2]);
                    top++;
                    break;

                case BytecodeVmProgramOpCode.Random:
                    var to = _evaluationStack[--top];
                    var from = _evaluationStack[--top];
                    _evaluationStack[top++] = EvaluateRandomExpression(from, to);
                    break;

                case BytecodeVmProgramOpCode.Range:
                    top -= instruction.A;
                    _evaluationStack[top] = EvaluateRangeExpression(
                        _evaluationStack[top],
                        _evaluationStack[top + 1],
                        instruction.A == 3 ? _evaluationStack[top + 2] : BytecodeVmValue.Integer(1));
                    top++;
                    break;

                case BytecodeVmProgramOpCode.Dice:
                    _evaluationStack[top++] = EvaluateDiceExpression(instruction.A, instruction.B);
                    break;

                case BytecodeVmProgramOpCode.SeededRandom:
                    var seed = _evaluationStack[--top];
                    if (instruction.ExpressionProgram is null ||
                        !TryEvaluateSeededRandomExpression(seed, instruction.ExpressionProgram, top, out var seededValue))
                    {
                        value = BytecodeVmValue.Nothing;
                        return false;
                    }

                    _evaluationStack[top++] = seededValue;
                    break;

                case BytecodeVmProgramOpCode.Cast:
                    _evaluationStack[top - 1] = EvaluateProgramCast(instruction.CastKind, _evaluationStack[top - 1]);
                    break;

                case BytecodeVmProgramOpCode.TypeConstructor:
                    top -= instruction.A;
                    _evaluationStack[top] = EvaluateTypeConstructor(
                        instruction.DiagnosticName,
                        instruction.Names,
                        _evaluationStack,
                        top,
                        instruction.A);
                    top++;
                    break;

                case BytecodeVmProgramOpCode.TypeCheck:
                    _evaluationStack[top - 1] = BytecodeVmValue.Boolean(IsValueOfType(
                        _evaluationStack[top - 1],
                        instruction.DiagnosticName));
                    break;

                case BytecodeVmProgramOpCode.RulePredicate:
                    var input = _evaluationStack[--top];
                    if (!TryEvaluateRulePredicate(instruction, input, top, out var predicateValue))
                    {
                        value = BytecodeVmValue.Nothing;
                        return false;
                    }

                    _evaluationStack[top++] = predicateValue;
                    break;

                case BytecodeVmProgramOpCode.Call:
                    top -= instruction.A;
                    if (!TryEvaluateCallable(instruction, _evaluationStack, top, instruction.A, out var callValue))
                    {
                        value = BytecodeVmValue.Nothing;
                        return false;
                    }

                    _evaluationStack[top++] = callValue;
                    break;

                case BytecodeVmProgramOpCode.MemberAccess:
                    _evaluationStack[top - 1] = EvaluateMemberAccess(_evaluationStack[top - 1], instruction.DiagnosticName);
                    break;

                case BytecodeVmProgramOpCode.IndexedAccess:
                    var selector = _evaluationStack[--top];
                    var target = _evaluationStack[--top];
                    _evaluationStack[top++] = EvaluateIndexedAccess(target, selector);
                    break;

                case BytecodeVmProgramOpCode.BuildList:
                    top -= instruction.A;
                    _evaluationStack[top] = BuildListValue(_evaluationStack, top, instruction.A);
                    top++;
                    break;

                case BytecodeVmProgramOpCode.BuildSequence:
                    top -= instruction.A;
                    _evaluationStack[top] = BuildSequenceValue(_evaluationStack, top, instruction.A);
                    top++;
                    break;

                case BytecodeVmProgramOpCode.BuildSet:
                    top -= instruction.A;
                    _evaluationStack[top] = BuildSetValue(_evaluationStack, top, instruction.A);
                    top++;
                    break;

                case BytecodeVmProgramOpCode.BuildDictionary:
                    top -= instruction.A;
                    _evaluationStack[top] = BuildDictionaryValue(_evaluationStack, top, instruction.A, instruction.Names);
                    top++;
                    break;

                case BytecodeVmProgramOpCode.BuildMessage:
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

                case BytecodeVmProgramOpCode.BindHandler:
                    top -= instruction.A + 1;
                    _evaluationStack[top] = BindHandlerValue(
                        _evaluationStack[top],
                        _evaluationStack,
                        top + 1,
                        instruction.A,
                        instruction.Names);
                    top++;
                    break;

                case BytecodeVmProgramOpCode.CallExtension:
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

                case BytecodeVmProgramOpCode.Pipeline:
                    if (instruction.PipelineProgram is null ||
                        !TryExecutePipelineProgram(instruction.PipelineProgram, out var pipelineValue))
                    {
                        value = BytecodeVmValue.Nothing;
                        return false;
                    }

                    _evaluationStack[top++] = pipelineValue;
                    break;

                case BytecodeVmProgramOpCode.GeneratedCollection:
                    if (instruction.GeneratedCollectionProgram is null ||
                        !TryExecuteGeneratedCollectionProgram(instruction.GeneratedCollectionProgram, out var generatedValue))
                    {
                        value = BytecodeVmValue.Nothing;
                        return false;
                    }

                    _evaluationStack[top++] = generatedValue;
                    break;

                case BytecodeVmProgramOpCode.GuardedChoice:
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

    private bool TryEvaluateHandlerBind(HandlerBindExpressionNode handlerBind, out BytecodeVmValue value)
    {
        if (!TryEvaluate(handlerBind.CalleeExpression, out var callee))
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        var arguments = new Dictionary<string, GameEventScriptValue>(handlerBind.Arguments.Count, StringComparer.Ordinal);
        foreach (var argument in handlerBind.Arguments)
        {
            if (!TryEvaluate(argument.Expression, out var argumentValue))
            {
                value = BytecodeVmValue.Nothing;
                return false;
            }

            arguments[argument.Name] = argumentValue.ToGameEventScriptValue();
        }

        value = GameEventScriptMessageValueCodec.TryBindHandlerValue(callee.ToGameEventScriptValue(), arguments, out var message)
            ? BytecodeVmValue.Reference(GameEventScriptMessageValueCodec.CreateMessageValue(message))
            : BytecodeVmValue.Nothing;
        return true;
    }

    private bool TryEvaluateTypeCheck(TypeCheckExpressionNode typeCheck, out BytecodeVmValue value)
    {
        if (!TryEvaluate(typeCheck.Value, out var input))
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        value = BytecodeVmValue.Boolean(IsValueOfType(input, typeCheck.TypeName));
        return true;
    }

    private bool TryEvaluateListLiteral(ListLiteralExpressionNode list, out BytecodeVmValue value)
    {
        var items = new GameEventScriptValue[list.Items.Count];
        for (var itemIndex = 0; itemIndex < list.Items.Count; itemIndex++)
        {
            if (!TryEvaluate(list.Items[itemIndex], out var item))
            {
                value = BytecodeVmValue.Nothing;
                return false;
            }

            items[itemIndex] = item.ToGameEventScriptValue();
        }

        value = BytecodeVmValue.Reference(GameEventScriptValueFactory.GesList(items));
        return true;
    }

    private bool TryEvaluateSequenceLiteral(SequenceLiteralExpressionNode sequence, out BytecodeVmValue value)
    {
        var items = new GameEventScriptValue[sequence.Items.Count];
        for (var itemIndex = 0; itemIndex < sequence.Items.Count; itemIndex++)
        {
            if (!TryEvaluate(sequence.Items[itemIndex], out var item))
            {
                value = BytecodeVmValue.Nothing;
                return false;
            }

            items[itemIndex] = item.ToGameEventScriptValue();
        }

        value = BytecodeVmValue.Reference(GameEventScriptValueFactory.GesSequence(items));
        return true;
    }

    private bool TryEvaluateSetLiteral(SetLiteralExpressionNode set, out BytecodeVmValue value)
    {
        var items = new GameEventScriptValue[set.Items.Count];
        for (var itemIndex = 0; itemIndex < set.Items.Count; itemIndex++)
        {
            if (!TryEvaluate(set.Items[itemIndex], out var item))
            {
                value = BytecodeVmValue.Nothing;
                return false;
            }

            items[itemIndex] = item.ToGameEventScriptValue();
        }

        value = BytecodeVmValue.Reference(GameEventScriptValueFactory.GseSet(items));
        return true;
    }

    private bool TryEvaluateDictionaryLiteral(DictionaryLiteralExpressionNode dictionary, out BytecodeVmValue value)
    {
        var map = new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal);
        foreach (var entry in dictionary.Entries)
        {
            if (!TryEvaluate(entry.Value, out var entryValue))
            {
                value = BytecodeVmValue.Nothing;
                return false;
            }

            map[entry.Key] = entryValue.ToGameEventScriptValue();
        }

        value = BytecodeVmValue.Reference(GameEventScriptValueFactory.GseDictionary(map));
        return true;
    }

    private bool TryEvaluateMemberAccess(MemberAccessExpressionNode memberAccess, out BytecodeVmValue value)
    {
        if (!TryEvaluate(memberAccess.Target, out var target))
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        value = EvaluateMemberAccess(target, memberAccess.Member);
        return true;
    }

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

        return BytecodeVmValue.Reference(GameEventScriptValueFactory.GseSet(items));
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

        return BytecodeVmValue.Reference(GameEventScriptValueFactory.GseDictionary(map));
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
        return GameEventScriptMessageValueCodec.TryBindHandlerValue(callee.ToGameEventScriptValue(), arguments, out var message)
            ? BytecodeVmValue.Reference(GameEventScriptMessageValueCodec.CreateMessageValue(message))
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

        if (GameEventScriptStandardExtensions.TryInvoke(reference, arguments, out var standardValue))
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
            BytecodeVmValueKind.Integer => GameEventScriptFastValue.FromInteger(value.IntegerValue),
            BytecodeVmValueKind.Decimal => GameEventScriptFastValue.FromDecimal(value.Number, value.Unit),
            BytecodeVmValueKind.Percentage => GameEventScriptFastValue.FromPercentage(value.Number),
            BytecodeVmValueKind.Reference => GameEventScriptFastValue.FromGameEventScriptValue(value.ReferenceValue ?? GameEventScriptValue.Nothing),
            _ => GameEventScriptFastValue.Nothing
        };

    private BytecodeVmValue EvaluateProgramBinary(BytecodeVmProgramOpCode opCode, BytecodeVmValue left, BytecodeVmValue right)
    {
        if (opCode != BytecodeVmProgramOpCode.Default &&
            (left.IsNothingLike() || right.IsNothingLike()))
        {
            return BytecodeVmValue.Nothing;
        }

        switch (opCode)
        {
            case BytecodeVmProgramOpCode.Or:
                return BytecodeVmValue.Boolean(left.AsBoolean() || right.AsBoolean());
            case BytecodeVmProgramOpCode.Xor:
                return BytecodeVmValue.Boolean(left.AsBoolean() ^ right.AsBoolean());
            case BytecodeVmProgramOpCode.And:
                return BytecodeVmValue.Boolean(left.AsBoolean() && right.AsBoolean());
            case BytecodeVmProgramOpCode.Equal:
                return BytecodeVmValue.Boolean(BytecodeVmValue.AreEqual(left, right));
            case BytecodeVmProgramOpCode.NotEqual:
                return BytecodeVmValue.Boolean(!BytecodeVmValue.AreEqual(left, right));
            case BytecodeVmProgramOpCode.Less:
                return BytecodeVmValue.Boolean(BytecodeVmValue.TryCompareNumeric(left, right, out var lessComparison) && lessComparison < 0);
            case BytecodeVmProgramOpCode.Greater:
                return BytecodeVmValue.Boolean(BytecodeVmValue.TryCompareNumeric(left, right, out var greaterComparison) && greaterComparison > 0);
            case BytecodeVmProgramOpCode.LessOrEqual:
                return BytecodeVmValue.Boolean(BytecodeVmValue.TryCompareNumeric(left, right, out var lessOrEqualComparison) && lessOrEqualComparison <= 0);
            case BytecodeVmProgramOpCode.GreaterOrEqual:
                return BytecodeVmValue.Boolean(BytecodeVmValue.TryCompareNumeric(left, right, out var greaterOrEqualComparison) && greaterOrEqualComparison >= 0);
            case BytecodeVmProgramOpCode.Add:
                return BytecodeVmValue.Add(left, right);
            case BytecodeVmProgramOpCode.Subtract:
                return BytecodeVmValue.Subtract(left, right);
            case BytecodeVmProgramOpCode.Multiply:
                return BytecodeVmValue.Multiply(left, right);
            case BytecodeVmProgramOpCode.Divide:
                return BytecodeVmValue.Divide(left, right);
            case BytecodeVmProgramOpCode.IntegerDivide:
                return BytecodeVmValue.IntegerDivide(left, right);
            case BytecodeVmProgramOpCode.Modulo:
                return BytecodeVmValue.Modulo(left, right);
            case BytecodeVmProgramOpCode.Remainder:
                return BytecodeVmValue.Remainder(left, right);
            default:
                return TryEvaluateBinaryOperation(GetBinaryOperator(opCode), left, right, out var value)
                    ? value
                    : throw new InvalidOperationException($"BytecodeVM invariant failed: binary opcode '{opCode}' could not be evaluated.");
        }
    }

    private BytecodeVmValue EvaluateProgramCast(BytecodeVmCastKind castKind, BytecodeVmValue input)
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
            "degree" => value.Kind == BytecodeVmValueKind.Decimal && value.Unit == GameEventScriptDecimalUnit.Degree ||
                        (value.ReferenceValue?.IsDecimalUnit(GameEventScriptDecimalUnit.Degree) ?? false),
            "meter" => value.Kind == BytecodeVmValueKind.Decimal && value.Unit == GameEventScriptDecimalUnit.Meter ||
                       (value.ReferenceValue?.IsDecimalUnit(GameEventScriptDecimalUnit.Meter) ?? false),
            "second" => value.Kind == BytecodeVmValueKind.Decimal && value.Unit == GameEventScriptDecimalUnit.Second ||
                        (value.ReferenceValue?.IsDecimalUnit(GameEventScriptDecimalUnit.Second) ?? false),
            "vector2" => value.ReferenceValue?.IsVector2() ?? false,
            "vector3" => value.ReferenceValue?.IsVector3() ?? false,
            "decimal" => value.Kind is BytecodeVmValueKind.Integer or BytecodeVmValueKind.Decimal or BytecodeVmValueKind.Percentage ||
                         (value.ReferenceValue?.IsNumber() ?? false),
            "integer" => value.Kind == BytecodeVmValueKind.Integer || (value.ReferenceValue?.IsInteger() ?? false),
            "boolean" => value.Kind == BytecodeVmValueKind.Boolean || value.ReferenceValue?.Kind == GameEventScriptValueKind.Boolean,
            "optional" => value.ReferenceValue?.IsOptional() ?? false,
            "sequence" => value.ReferenceValue?.IsSequence() ?? false,
            "list" => value.ReferenceValue?.IsList() ?? false,
            "range" => value.ReferenceValue?.IsRange() ?? false,
            "message" => value.ReferenceValue is { } messageValue &&
                         (messageValue.Kind == GameEventScriptValueKind.Message || GameEventScriptMessageValueCodec.TryReadMessageValue(messageValue, out _)),
            "handler" => value.ReferenceValue is { } handlerValue &&
                         (handlerValue.Kind == GameEventScriptValueKind.Handler || GameEventScriptMessageValueCodec.TryReadHandlerValue(handlerValue, out _)),
            "dictionary" => value.ReferenceValue?.IsDictionary() ?? false,
            "set" => value.ReferenceValue?.IsSet() ?? false,
            "dice" => value.ReferenceValue?.IsDice() ?? false,
            _ => value.ReferenceValue is { } customValue &&
                 customValue.TryGetCustomTypeName(out var customTypeName) &&
                 string.Equals(customTypeName, typeName, StringComparison.Ordinal)
        };
    }

    private bool TryEvaluateBinary(BinaryExpressionNode binary, out BytecodeVmValue value)
    {
        if (!TryEvaluate(binary.Left, out var left) ||
            !TryEvaluate(binary.Right, out var right))
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        return TryEvaluateBinaryOperation(binary.Operator, left, right, out value);
    }

    private bool TryEvaluateUnary(UnaryExpressionNode unary, out BytecodeVmValue value)
    {
        if (!TryEvaluate(unary.Operand, out var operand))
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        return TryEvaluateUnaryOperation(unary.Operator, operand, out value);
    }

    private bool TryEvaluateVariadic(VariadicTaggedExpressionNode variadic, out BytecodeVmValue value)
    {
        var values = new BytecodeVmValue[variadic.Arguments.Count];
        for (var argumentIndex = 0; argumentIndex < variadic.Arguments.Count; argumentIndex++)
        {
            if (!TryEvaluate(variadic.Arguments[argumentIndex], out values[argumentIndex]))
            {
                value = BytecodeVmValue.Nothing;
                return false;
            }
        }

        return TryEvaluateVariadicOperation(variadic.Operator, values, 0, values.Length, out value);
    }

    private bool TryEvaluateClamp(ClampExpressionNode clamp, out BytecodeVmValue value)
    {
        if (!TryEvaluate(clamp.Value, out var raw) ||
            !TryEvaluate(clamp.Minimum, out var minimum) ||
            !TryEvaluate(clamp.Maximum, out var maximum))
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        value = EvaluateClamp(raw, minimum, maximum);
        return true;
    }

    private bool TryEvaluateRandom(RandomExpressionNode random, out BytecodeVmValue value)
    {
        if (!TryEvaluate(random.FromExpression, out var from) ||
            !TryEvaluate(random.ToExpression, out var to))
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        value = EvaluateRandomExpression(from, to);
        return true;
    }

    private bool TryEvaluateRange(RangeExpressionNode range, out BytecodeVmValue value)
    {
        if (!TryEvaluate(range.FromExpression, out var from) ||
            !TryEvaluate(range.ToExpression, out var to))
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        if (range.StepExpression is not null)
        {
            if (!TryEvaluate(range.StepExpression, out var step))
            {
                value = BytecodeVmValue.Nothing;
                return false;
            }

            value = EvaluateRangeExpression(from, to, step);
            return true;
        }

        value = EvaluateRangeExpression(from, to, BytecodeVmValue.Integer(1));
        return true;
    }

    private bool TryEvaluateSeededRandomExpression(SeededRandomExpressionNode seededRandom, out BytecodeVmValue value)
    {
        if (!TryEvaluate(seededRandom.SeedExpression, out var seed) ||
            !_plan.TryGetExpressionProgram(seededRandom.BodyExpression, out var bodyProgram))
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        return TryEvaluateSeededRandomExpression(seed, bodyProgram, 0, out value);
    }

    private bool TryEvaluateSeededRandomExpression(
        BytecodeVmValue seed,
        BytecodeVmExpressionProgram bodyProgram,
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

    private bool TryEvaluateGeneratedCollectionExpression(
        GeneratedCollectionExpressionNode generatedCollection,
        out BytecodeVmValue value)
    {
        if (!_plan.TryGetSlot(generatedCollection.Identifier, out var identifierSlot))
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        if (!TryMaterializeIterationSource(generatedCollection.Source, out var sourceItems))
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        var values = new List<GameEventScriptValue>();
        foreach (var item in sourceItems)
        {
            if (!_context.RuntimeBudget.TryConsumeLoopIteration("Generated collection iteration budget exhausted."))
            {
                break;
            }

            var fastItem = BytecodeVmValue.FromGameEventScriptValue(item);
            if (generatedCollection.Predicate is not null)
            {
                if (!TryEvaluateExpressionWithTemporarySlot(identifierSlot, fastItem, generatedCollection.Predicate, out var predicate))
                {
                    value = BytecodeVmValue.Nothing;
                    return false;
                }

                if (!predicate.AsBoolean())
                {
                    continue;
                }
            }

            if (!_context.RuntimeBudget.TryCheckGeneratedCollectionItemCount(values.Count + 1, "Generated collection item count exceeds the configured limit."))
            {
                break;
            }

            if (!TryEvaluateExpressionWithTemporarySlot(identifierSlot, fastItem, generatedCollection.Projection, out var projected))
            {
                value = BytecodeVmValue.Nothing;
                return false;
            }

            values.Add(projected.ToGameEventScriptValue());
        }

        value = BytecodeVmValue.Reference(generatedCollection.CollectionType == "set"
            ? GameEventScriptValueFactory.GseSet(values)
            : GameEventScriptValueFactory.GesList(values));
        return true;
    }

    private bool TryExecuteGeneratedCollectionProgram(BytecodeVmGeneratedCollectionProgram program, out BytecodeVmValue value)
    {
        if (!TryMaterializeIterationSource(program.Source, out var sourceItems))
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        var values = new List<GameEventScriptValue>();
        foreach (var item in sourceItems)
        {
            if (!_context.RuntimeBudget.TryConsumeLoopIteration("Generated collection iteration budget exhausted."))
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

            if (!_context.RuntimeBudget.TryCheckGeneratedCollectionItemCount(values.Count + 1, "Generated collection item count exceeds the configured limit."))
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
            ? GameEventScriptValueFactory.GseSet(values)
            : GameEventScriptValueFactory.GesList(values));
        return true;
    }

    private bool TryMaterializeIterationSource(IterationSourceNode source, out GameEventScriptValue[] items)
    {
        switch (source)
        {
            case CollectionIterationSourceNode collection:
                if (!TryEvaluate(collection.Expression, out var collectionValue))
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

            case RangeIterationSourceNode range:
                if (!TryEvaluateRange(range.RangeExpression, out var rangeValue))
                {
                    items = [];
                    return false;
                }

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

    private bool TryEvaluateGuardedChoiceExpression(GuardedChoiceExpressionNode guardedChoice, out BytecodeVmValue value)
    {
        foreach (var branch in guardedChoice.Branches)
        {
            if (!TryEvaluate(branch.ConditionExpression, out var condition))
            {
                value = BytecodeVmValue.Nothing;
                return false;
            }

            if (condition.AsBoolean())
            {
                return TryEvaluate(branch.ValueExpression, out value);
            }
        }

        return TryEvaluate(guardedChoice.OtherwiseExpression, out value);
    }

    private bool TryExecuteGuardedChoiceProgram(BytecodeVmGuardedChoiceProgram program, out BytecodeVmValue value)
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

        if (GameEventScriptValueAlu.TryUnwrapOptionalForOperation(fromRaw, out var unwrappedFrom) &&
            GameEventScriptValueAlu.TryUnwrapOptionalForOperation(toRaw, out var unwrappedTo) &&
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

        if (!GameEventScriptValueAlu.TryCoerceNumericForOperation(fromRaw, out var fromNumber) ||
            !GameEventScriptValueAlu.TryCoerceNumericForOperation(toRaw, out var toNumber) ||
            !fromNumber.IsFinite ||
            !toNumber.IsFinite)
        {
            return BytecodeVmValue.Nothing;
        }

        var lower = Math.Min(fromNumber.Value, toNumber.Value);
        var upper = Math.Max(fromNumber.Value, toNumber.Value);
        if (lower == upper)
        {
            return BytecodeVmValue.Decimal(lower);
        }

        return TryNextInclusiveDecimal(lower, upper, out var nextDecimal)
            ? BytecodeVmValue.Decimal(nextDecimal)
            : BytecodeVmValue.Nothing;
    }

    private static BytecodeVmValue EvaluateRangeExpression(BytecodeVmValue fromValue, BytecodeVmValue toValue, BytecodeVmValue stepValue)
    {
        if (!GameEventScriptValueAlu.TryCoerceNumericForOperation(fromValue.ToGameEventScriptValue(), out var fromNumber) ||
            !GameEventScriptValueAlu.TryCoerceNumericForOperation(toValue.ToGameEventScriptValue(), out var toNumber) ||
            !GameEventScriptValueAlu.TryCoerceNumericForOperation(stepValue.ToGameEventScriptValue(), out var stepNumber) ||
            !fromNumber.IsFinite ||
            !toNumber.IsFinite ||
            !stepNumber.IsFinite)
        {
            return BytecodeVmValue.Nothing;
        }

        return BytecodeVmValue.Reference(GesRange(
            GameEventScriptValueAlu.ToIntegerSaturated(fromNumber.Value),
            GameEventScriptValueAlu.ToIntegerSaturated(toNumber.Value),
            GameEventScriptValueAlu.ToIntegerSaturated(stepNumber.Value)));
    }

    private BytecodeVmValue EvaluateDiceExpression(int diceCount, int sideCount)
    {
        if (diceCount <= 0 || sideCount <= 0)
        {
            return BytecodeVmValue.Reference(GseDice(GameEventScriptDiceValue.Empty));
        }

        if (!_context.RuntimeBudget.TryCheckDice(new DiceExpressionNode(diceCount, sideCount)))
        {
            return BytecodeVmValue.Reference(GseDice(GameEventScriptDiceValue.Empty));
        }

        var rolls = new int[diceCount];
        for (var i = 0; i < rolls.Length; i++)
        {
            if (!TryNextInclusiveInt(1, sideCount, out var roll))
            {
                return BytecodeVmValue.Reference(GseDice(GameEventScriptDiceValue.Empty));
            }

            rolls[i] = roll;
        }

        return BytecodeVmValue.Reference(GseDice(GameEventScriptDiceValue.Create(rolls)));
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
            "min" => BytecodeVmValue.FromGameEventScriptValue(GameEventScriptValueAlu.EvaluateMinMax(boxedValues, isMax: false)),
            "max" => BytecodeVmValue.FromGameEventScriptValue(GameEventScriptValueAlu.EvaluateMinMax(boxedValues, isMax: true)),
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

        if (!GameEventScriptValueAlu.TryUnwrapOptionalForOperation(leftRaw, out var left) ||
            !GameEventScriptValueAlu.TryUnwrapOptionalForOperation(rightRaw, out var right))
        {
            value = BytecodeVmValue.Reference(GesOptionalNone());
            return true;
        }

        value = operation switch
        {
            "|" => BytecodeVmValue.Boolean(left.AsBoolean() || right.AsBoolean()),
            "^" => BytecodeVmValue.Boolean(left.AsBoolean() ^ right.AsBoolean()),
            "&" => BytecodeVmValue.Boolean(left.AsBoolean() && right.AsBoolean()),
            "=" or "==" => BytecodeVmValue.Boolean(GameEventScriptValueAlu.AreEqual(left, right)),
            "<>" => BytecodeVmValue.Boolean(!GameEventScriptValueAlu.AreEqual(left, right)),
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
            "intersect" => BytecodeVmValue.Reference(GameEventScriptValueAlu.EvaluateCollectionIntersect(left, right)),
            "combine" or "merge" => BytecodeVmValue.Reference(GameEventScriptValueAlu.EvaluateCollectionCombine(left, right)),
            "except" => BytecodeVmValue.Reference(GameEventScriptValueAlu.EvaluateCollectionExcept(left, right)),
            "zip" => BytecodeVmValue.Reference(GameEventScriptValueAlu.EvaluateCollectionZip(left, right)),
            _ => throw new InvalidOperationException($"BytecodeVM invariant failed: unknown binary operator '{operation}'.")
        };

        return true;
    }

    private static string GetBinaryOperator(BytecodeVmProgramOpCode opCode)
        => opCode switch
        {
            BytecodeVmProgramOpCode.Or => "|",
            BytecodeVmProgramOpCode.Xor => "^",
            BytecodeVmProgramOpCode.And => "&",
            BytecodeVmProgramOpCode.Equal => "=",
            BytecodeVmProgramOpCode.NotEqual => "<>",
            BytecodeVmProgramOpCode.Less => "<",
            BytecodeVmProgramOpCode.Greater => ">",
            BytecodeVmProgramOpCode.LessOrEqual => "<=",
            BytecodeVmProgramOpCode.GreaterOrEqual => ">=",
            BytecodeVmProgramOpCode.Add => "+",
            BytecodeVmProgramOpCode.Subtract => "-",
            BytecodeVmProgramOpCode.Multiply => "*",
            BytecodeVmProgramOpCode.Divide => "/",
            BytecodeVmProgramOpCode.IntegerDivide => "div",
            BytecodeVmProgramOpCode.Modulo => "mod",
            BytecodeVmProgramOpCode.Remainder => "rem",
            BytecodeVmProgramOpCode.Default => "default",
            BytecodeVmProgramOpCode.Contains => "in",
            BytecodeVmProgramOpCode.ContainsValue => "value in",
            BytecodeVmProgramOpCode.StartsWith => "starts with",
            BytecodeVmProgramOpCode.EndsWith => "ends with",
            BytecodeVmProgramOpCode.Intersect => "intersect",
            BytecodeVmProgramOpCode.Combine => "combine",
            BytecodeVmProgramOpCode.Except => "except",
            BytecodeVmProgramOpCode.Zip => "zip",
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
        => GameEventScriptValueAlu.TryCompareNumericValues(left, right, out var comparison) &&
           predicate(comparison);

    private static GameEventScriptValue EvaluateAddBinary(GameEventScriptValue left, GameEventScriptValue right)
    {
        if (GameEventScriptValueAlu.TryEvaluateVectorBinary(left, "+", right, out var vector))
        {
            return vector;
        }

        if (GameEventScriptValueAlu.TryEvaluatePercentageBinary(left, "+", right, out var percentage))
        {
            return percentage;
        }

        if (GameEventScriptValueAlu.TryEvaluateUnitBinary(left, "+", right, out var unit))
        {
            return unit;
        }

        if (GameEventScriptValueAlu.TryCoerceNumericForOperation(left, out var leftNumeric) &&
            GameEventScriptValueAlu.TryCoerceNumericForOperation(right, out var rightNumeric))
        {
            return GameEventScriptValueAlu.ToGameEventScriptNumericResult(left, "+", right, GameEventScriptValueAlu.AddNumeric(leftNumeric, rightNumeric));
        }

        if (GameEventScriptValueAlu.TryCombineWithPlus(left, right, out var combined))
        {
            return combined;
        }

        return left.IsText() || right.IsText()
            ? GesText($"{GameEventScriptValueAlu.ToText(left)}{GameEventScriptValueAlu.ToText(right)}")
            : GesDecimalNaN();
    }

    private static GameEventScriptValue EvaluateNumericBinary(GameEventScriptValue left, string operation, GameEventScriptValue right)
    {
        if (GameEventScriptValueAlu.TryEvaluateVectorBinary(left, operation, right, out var vector))
        {
            return vector;
        }

        if (GameEventScriptValueAlu.TryEvaluatePercentageBinary(left, operation, right, out var percentage))
        {
            return percentage;
        }

        if (GameEventScriptValueAlu.TryEvaluateUnitBinary(left, operation, right, out var unit))
        {
            return unit;
        }

        if (!GameEventScriptValueAlu.TryCoerceNumericForOperation(left, out var leftNumeric) ||
            !GameEventScriptValueAlu.TryCoerceNumericForOperation(right, out var rightNumeric))
        {
            return GesDecimalNaN();
        }

        var result = operation switch
        {
            "-" => GameEventScriptValueAlu.SubtractNumeric(leftNumeric, rightNumeric),
            "*" => GameEventScriptValueAlu.MultiplyNumeric(leftNumeric, rightNumeric),
            "/" => GameEventScriptValueAlu.DivideNumeric(leftNumeric, rightNumeric),
            "div" => GameEventScriptValueAlu.IntegerDivideNumeric(leftNumeric, rightNumeric),
            "mod" => GameEventScriptValueAlu.ModuloNumeric(leftNumeric, rightNumeric),
            "rem" => GameEventScriptValueAlu.RemainderNumeric(leftNumeric, rightNumeric),
            _ => GameEventScriptValueAlu.NumericValue.NaN()
        };
        return GameEventScriptValueAlu.ToGameEventScriptNumericResult(left, operation, right, result);
    }

    private static GameEventScriptValue EvaluateNegateUnary(GameEventScriptValue operand)
    {
        if (operand.IsNothing())
        {
            return GameEventScriptValue.Nothing;
        }

        if (!GameEventScriptValueAlu.TryUnwrapOptionalForOperation(operand, out var unwrapped))
        {
            return GesOptionalNone();
        }

        if (unwrapped.IsPercentage())
        {
            return GesPercentage(-unwrapped.AsNumber());
        }

        if (GameEventScriptValueAlu.TryEvaluateVectorUnary(unwrapped, "-", out var vectorNegation))
        {
            return vectorNegation;
        }

        if (GameEventScriptValue.TryGetDecimalUnit(unwrapped, out var unit))
        {
            return GesDecimal(-unwrapped.AsNumber(), unit);
        }

        return GameEventScriptValueAlu.TryCoerceNumericForOperation(unwrapped, out var number)
            ? GameEventScriptValueAlu.ToGameEventScriptDecimal(GameEventScriptValueAlu.NegateNumeric(number))
            : GameEventScriptValue.Nothing;
    }

    private static GameEventScriptValue EvaluateNotUnary(GameEventScriptValue operand)
    {
        if (operand.IsNothing())
        {
            return GameEventScriptValue.Nothing;
        }

        return GameEventScriptValueAlu.TryUnwrapOptionalForOperation(operand, out var unwrapped)
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
            _ => GameEventScriptValue.Nothing
        };
    }

    private GameEventScriptValue EvaluateRangeLength(GameEventScriptValue operand)
    {
        if (!GameEventScriptRuntimeLimitUtilities.TryGetRangeLength(operand, out var length))
        {
            return GameEventScriptValue.Nothing;
        }

        return _context.RuntimeBudget.TryCheckRangeLength(length, "Range length exceeds the configured limit.")
            ? GesInteger(length)
            : GameEventScriptValue.Nothing;
    }

    private GameEventScriptValue CountEnumerableWithBudget(IEnumerable<GameEventScriptValue> values, string detail)
    {
        long count = 0;
        foreach (var _ in values)
        {
            if (!_context.RuntimeBudget.TryConsumeLoopIteration(detail))
            {
                return GameEventScriptValue.Nothing;
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
        if (ratio <= 0m)
        {
            return GesBoolean(false);
        }

        if (ratio >= 1m)
        {
            return GesBoolean(true);
        }

        return TryNextInclusiveDecimal(0m, 1m, out var randomValue)
            ? GesBoolean(randomValue < ratio)
            : GesBoolean(false);
    }

    private static GameEventScriptValue EvaluateAbsUnary(GameEventScriptValue operand)
    {
        if (operand.IsNothing())
        {
            return GameEventScriptValue.Nothing;
        }

        if (GameEventScriptValueAlu.TryEvaluateVectorUnary(operand, "abs", out var vectorLength))
        {
            return vectorLength;
        }

        return GameEventScriptValueAlu.TryCoerceNumericForOperation(operand, out var number) && number.IsFinite
            ? GesDecimal(Math.Abs(number.Value))
            : GameEventScriptValue.Nothing;
    }

    private static BytecodeVmValue EvaluateClamp(BytecodeVmValue rawValue, BytecodeVmValue minimumValue, BytecodeVmValue maximumValue)
    {
        var raw = rawValue.ToGameEventScriptValue();
        var minimum = minimumValue.ToGameEventScriptValue();
        var maximum = maximumValue.ToGameEventScriptValue();

        if (!GameEventScriptValueAlu.HaveCompatibleNumericUnits(raw, minimum) ||
            !GameEventScriptValueAlu.HaveCompatibleNumericUnits(raw, maximum) ||
            !GameEventScriptValueAlu.HaveCompatibleNumericUnits(minimum, maximum))
        {
            return BytecodeVmValue.NaN();
        }

        if (!GameEventScriptValueAlu.TryCoerceNumericForOperation(raw, out var rawNumber) ||
            !GameEventScriptValueAlu.TryCoerceNumericForOperation(minimum, out var minimumNumber) ||
            !GameEventScriptValueAlu.TryCoerceNumericForOperation(maximum, out var maximumNumber) ||
            !rawNumber.IsFinite ||
            !minimumNumber.IsFinite ||
            !maximumNumber.IsFinite)
        {
            return BytecodeVmValue.Nothing;
        }

        var lower = Math.Min(minimumNumber.Value, maximumNumber.Value);
        var upper = Math.Max(minimumNumber.Value, maximumNumber.Value);
        GameEventScriptValue.TryGetDecimalUnit(raw, out var unit);
        return BytecodeVmValue.FromGameEventScriptValue(GesDecimal(
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
            GameEventScriptValueKind.Vector2 => BuildVector2StableSeedText((GameEventScriptVector2Value)value),
            GameEventScriptValueKind.Vector3 => BuildVector3StableSeedText((GameEventScriptVector3Value)value),
            GameEventScriptValueKind.Decimal => value.IsNaN()
                ? "decimal:nan"
                : value.IsNegativeInfinity()
                    ? "decimal:-infinity"
                    : value.IsInfinity()
                        ? "decimal:infinity"
                        : value is GameEventScriptDecimalValue { Unit: { } unit }
                            ? $"decimal:{value.AsNumber().ToString(CultureInfo.InvariantCulture)}:{unit.ToTypeName()}"
                            : $"decimal:{value.AsNumber().ToString(CultureInfo.InvariantCulture)}",
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

    private static string BuildVector2StableSeedText(GameEventScriptVector2Value value)
        => value.Unit.HasValue
            ? $"vector2:{value.X.ToString(CultureInfo.InvariantCulture)}:{value.Y.ToString(CultureInfo.InvariantCulture)}:{value.Unit.Value.ToTypeName()}"
            : $"vector2:{value.X.ToString(CultureInfo.InvariantCulture)}:{value.Y.ToString(CultureInfo.InvariantCulture)}";

    private static string BuildVector3StableSeedText(GameEventScriptVector3Value value)
        => value.Unit.HasValue
            ? $"vector3:{value.X.ToString(CultureInfo.InvariantCulture)}:{value.Y.ToString(CultureInfo.InvariantCulture)}:{value.Z.ToString(CultureInfo.InvariantCulture)}:{value.Unit.Value.ToTypeName()}"
            : $"vector3:{value.X.ToString(CultureInfo.InvariantCulture)}:{value.Y.ToString(CultureInfo.InvariantCulture)}:{value.Z.ToString(CultureInfo.InvariantCulture)}";

    private bool TryEvaluateRulePredicate(RulePredicateExpressionNode rulePredicate, out BytecodeVmValue value)
    {
        if (!_compiledScript.Callables.TryGetValue(rulePredicate.RuleName, out var callable) ||
            callable.Kind != GameEventScriptCallableKind.Rule ||
            callable.Parameters.Count != 1 ||
            !TryEvaluate(rulePredicate.Value, out var input))
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        var instruction = new BytecodeVmProgramInstruction(
            BytecodeVmProgramOpCode.RulePredicate,
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

    private bool TryEvaluateCall(CallExpressionNode call, out BytecodeVmValue value)
    {
        if (!_compiledScript.Callables.TryGetValue(call.Name, out var callable))
        {
            var handlerValue = Resolve(call.Name);
            if (handlerValue.IsNothingLike())
            {
                value = BytecodeVmValue.Nothing;
                return false;
            }

            var dynamicPairs = new KeyValuePair<string, GameEventScriptValue>[call.ArgumentList.Count];
            var dynamicLabels = new string[call.ArgumentList.Count];
            for (var argumentIndex = 0; argumentIndex < call.ArgumentList.Count; argumentIndex++)
            {
                var argument = call.ArgumentList.Arguments[argumentIndex];
                if (!TryEvaluate(argument.Expression, out var argumentValue))
                {
                    value = BytecodeVmValue.Nothing;
                    return false;
                }

                dynamicLabels[argumentIndex] = argument.Name;
                dynamicPairs[argumentIndex] = new KeyValuePair<string, GameEventScriptValue>(argument.Name, argumentValue.ToGameEventScriptValue());
            }

            var dynamicArguments = GameEventScriptNamedArguments.CreateOrdered(dynamicPairs, dynamicLabels);
            value = GameEventScriptMessageValueCodec.TryBindHandlerValue(handlerValue.ToGameEventScriptValue(), dynamicArguments, out var message)
                ? BytecodeVmValue.Reference(GameEventScriptMessageValueCodec.CreateMessageValue(message))
                : BytecodeVmValue.Nothing;
            return true;
        }

        if (callable.Parameters.Count != call.Arguments.Count)
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        var arguments = new BytecodeVmValue[call.Arguments.Count];
        for (var argumentIndex = 0; argumentIndex < call.Arguments.Count; argumentIndex++)
        {
            if (!TryEvaluate(call.Arguments[argumentIndex], out arguments[argumentIndex]))
            {
                value = BytecodeVmValue.Nothing;
                return false;
            }
        }

        var slots = new int[callable.Parameters.Count];
        for (var parameterIndex = 0; parameterIndex < callable.Parameters.Count; parameterIndex++)
        {
            if (!_plan.TryGetSlot(callable.Parameters[parameterIndex], out slots[parameterIndex]))
            {
                value = BytecodeVmValue.Nothing;
                return false;
            }
        }

        var instruction = new BytecodeVmProgramInstruction(
            BytecodeVmProgramOpCode.Call,
            A: call.Arguments.Count,
            CallableKind: callable.Kind == GameEventScriptCallableKind.Rule
                ? BytecodeVmCallableKind.Rule
                : BytecodeVmCallableKind.Select,
            ExpressionProgram: _plan.TryGetExpressionProgram(callable.Expression, out var expressionProgram)
                ? expressionProgram
                : null,
            DiagnosticName: callable.Name,
            Names: callable.Parameters.ToArray(),
            Slots: slots);

        return TryEvaluateCallable(instruction, arguments, 0, arguments.Length, out value);
    }

    private bool TryEvaluateExtensionCall(ExtensionCallExpressionNode extensionCall, out BytecodeVmValue value)
    {
        var arguments = new BytecodeVmValue[extensionCall.ArgumentList.Count];
        var labels = new string[extensionCall.ArgumentList.Count];
        for (var argumentIndex = 0; argumentIndex < extensionCall.ArgumentList.Count; argumentIndex++)
        {
            var argument = extensionCall.ArgumentList.Arguments[argumentIndex];
            labels[argumentIndex] = argument.Name;
            if (!TryEvaluate(argument.Expression, out arguments[argumentIndex]))
            {
                value = BytecodeVmValue.Nothing;
                return false;
            }
        }

        return TryCallExtension(
            extensionCall.ExtensionName,
            extensionCall.FunctionName,
            labels,
            -1,
            arguments,
            0,
            arguments.Length,
            out value);
    }

    private bool TryEvaluateExtensionPredicate(ExtensionPredicateExpressionNode extensionPredicate, out BytecodeVmValue value)
    {
        if (!TryEvaluate(extensionPredicate.Value, out var input))
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        var arguments = new[] { input };
        return TryCallExtension(
            extensionPredicate.ExtensionName,
            extensionPredicate.FunctionName,
            [GameEventScriptMessageSignature.UnlabeledParameterName],
            -1,
            arguments,
            0,
            1,
            out value);
    }

    private bool TryEvaluateRulePredicate(
        BytecodeVmProgramInstruction instruction,
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
                    value = BytecodeVmValue.Nothing;
                    return false;
                }

                value = BytecodeVmValue.Boolean(value.AsBoolean());
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
                value = BytecodeVmValue.Nothing;
                return false;
            }

            value = BytecodeVmValue.Boolean(value.AsBoolean());
            return true;
        }
        finally
        {
            ExitScope();
            _context.RuntimeBudget.ExitCall();
        }
    }

    private bool TryEvaluateCallable(
        BytecodeVmProgramInstruction instruction,
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
                    value = BytecodeVmValue.Nothing;
                    return false;
                }
            }

            if (!TryExecuteExpressionProgram(instruction.ExpressionProgram, start, out value))
            {
                return false;
            }

            if (instruction.CallableKind == BytecodeVmCallableKind.Rule)
            {
                value = BytecodeVmValue.Boolean(value.AsBoolean());
            }

            return true;
        }
        finally
        {
            ExitScope();
            _context.RuntimeBudget.ExitCall();
        }
    }

    private bool TryEvaluateTypeCast(TypeCastExpressionNode typeCast, out BytecodeVmValue value)
    {
        if (!TryEvaluate(typeCast.Value, out var input))
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        if (!TryConvertDeclaredType(typeCast.TypeName, input, out value))
        {
            throw new InvalidOperationException($"BytecodeVM invariant failed: type cast '{typeCast.TypeName}' could not be evaluated.");
        }

        return true;
    }

    private bool TryEvaluateTypeConstructor(TypeConstructorExpressionNode constructor, out BytecodeVmValue value)
    {
        var arguments = new BytecodeVmValue[constructor.Arguments.Count];
        var labels = new string[constructor.Arguments.Count];
        for (var argumentIndex = 0; argumentIndex < constructor.Arguments.Count; argumentIndex++)
        {
            var argument = constructor.Arguments[argumentIndex];
            labels[argumentIndex] = argument.Name;
            if (!TryEvaluate(argument.Expression, out arguments[argumentIndex]))
            {
                value = BytecodeVmValue.Nothing;
                return false;
            }
        }

        value = EvaluateTypeConstructor(constructor.TypeName, labels, arguments, 0, arguments.Length);
        return true;
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

        if (typeName is "vector2" or "vector3")
        {
            return EvaluateVectorConstructor(typeName, labels, stack, start, count);
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

            return BytecodeVmValue.FromGameEventScriptValue(ConvertToCustomType(GseDictionary(values), typeDefinition));
        }

        if (count != 1 || labels is not { Length: > 0 } || !string.Equals(labels[0], GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal))
        {
            return BytecodeVmValue.Nothing;
        }

        return TryConvertDeclaredType(typeName, stack[start], out var converted)
            ? converted
            : BytecodeVmValue.Nothing;
    }

    private BytecodeVmValue EvaluateVectorConstructor(
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

        if (typeName == "vector3" &&
            count == 2 &&
            labels is { Length: >= 2 } &&
            string.Equals(labels[0], GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal) &&
            string.Equals(labels[1], GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal))
        {
            return GameEventScriptValueAlu.TryCreateVector3(
                stack[start].ToGameEventScriptValue(),
                stack[start + 1].ToGameEventScriptValue(),
                out var lifted)
                ? BytecodeVmValue.FromGameEventScriptValue(lifted)
                : BytecodeVmValue.Nothing;
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

            return GameEventScriptValueAlu.TryCreateVectorFromLabeledComponents(typeName, labeledComponents, out var vector)
                ? BytecodeVmValue.FromGameEventScriptValue(vector)
                : BytecodeVmValue.Nothing;
        }

        var expectedCount = typeName == "vector2" ? 2 : 3;
        if (count != expectedCount)
        {
            return BytecodeVmValue.Nothing;
        }

        return expectedCount == 2
            ? GameEventScriptValueAlu.TryCreateVector2(
                stack[start].ToGameEventScriptValue(),
                stack[start + 1].ToGameEventScriptValue(),
                out var vector2)
                ? BytecodeVmValue.FromGameEventScriptValue(vector2)
                : BytecodeVmValue.Nothing
            : GameEventScriptValueAlu.TryCreateVector3(
                stack[start].ToGameEventScriptValue(),
                stack[start + 1].ToGameEventScriptValue(),
                stack[start + 2].ToGameEventScriptValue(),
                out var vector3)
                ? BytecodeVmValue.FromGameEventScriptValue(vector3)
                : BytecodeVmValue.Nothing;
    }

    private bool TryConvertDeclaredType(string declaredType, BytecodeVmValue input, out BytecodeVmValue value)
    {
        var boxed = input.ToGameEventScriptValue();
        value = declaredType switch
        {
            "nothing" => BytecodeVmValue.Nothing,
            "tag" => BytecodeVmValue.Reference(GesTag(boxed.AsText())),
            "text" => BytecodeVmValue.Reference(GesText(GameEventScriptValueAlu.ToText(boxed))),
            "percentage" => BytecodeVmValue.FromGameEventScriptValue(ConvertToPercentage(boxed)),
            "degree" => BytecodeVmValue.FromGameEventScriptValue(ConvertToDecimalUnit(boxed, GameEventScriptDecimalUnit.Degree)),
            "meter" => BytecodeVmValue.FromGameEventScriptValue(ConvertToDecimalUnit(boxed, GameEventScriptDecimalUnit.Meter)),
            "second" => BytecodeVmValue.FromGameEventScriptValue(ConvertToDecimalUnit(boxed, GameEventScriptDecimalUnit.Second)),
            "vector2" => BytecodeVmValue.Reference(ConvertToVector2(boxed)),
            "vector3" => BytecodeVmValue.Reference(ConvertToVector3(boxed)),
            "boolean" => BytecodeVmValue.Boolean(boxed.AsBoolean()),
            "integer" => BytecodeVmValue.Integer(boxed.AsInteger()),
            "decimal" or "number" => BytecodeVmValue.FromGameEventScriptValue(ConvertToDecimal(boxed)),
            "sequence" => BytecodeVmValue.Reference(boxed.IsSequence() ? boxed : GameEventScriptValueFactory.GesSequence(boxed.AsEnumerable())),
            "list" => TryCheckMaterializedValue(boxed, "List conversion would materialize more range items than allowed.")
                ? BytecodeVmValue.Reference(GesList(boxed.AsList()))
                : BytecodeVmValue.Nothing,
            "range" => BytecodeVmValue.Reference(boxed.IsRange() ? boxed : GameEventScriptValue.Nothing),
            "message" => boxed.Kind == GameEventScriptValueKind.Message
                ? input
                : GameEventScriptMessageValueCodec.TryReadMessageValue(boxed, out var message)
                    ? BytecodeVmValue.Reference(GameEventScriptMessageValueCodec.CreateMessageValue(message))
                    : BytecodeVmValue.Nothing,
            "handler" => boxed.Kind == GameEventScriptValueKind.Handler
                ? input
                : GameEventScriptMessageValueCodec.TryReadHandlerValue(boxed, out var handler)
                    ? BytecodeVmValue.Reference(GameEventScriptMessageValueCodec.CreateHandlerValue(handler))
                    : BytecodeVmValue.Nothing,
            "dictionary" => BytecodeVmValue.Reference(GseDictionary(boxed.AsDictionary())),
            "set" => TryCheckMaterializedValue(boxed, "Set conversion would materialize more range items than allowed.")
                ? BytecodeVmValue.Reference(GseSet(boxed.AsSet()))
                : BytecodeVmValue.Nothing,
            "dice" => BytecodeVmValue.Reference(GseDice(boxed.AsDice())),
            "optional" => boxed.IsOptional()
                ? input
                : boxed.IsNothing()
                    ? BytecodeVmValue.Reference(GesOptionalNone())
                    : BytecodeVmValue.Reference(GesOptionalSome(boxed)),
            _ => _compiledScript.TypeDefinitions.TryGetValue(declaredType, out var typeDefinition)
                ? BytecodeVmValue.FromGameEventScriptValue(ConvertToCustomType(boxed, typeDefinition))
                : input
        };

        return true;
    }

    private bool TryCheckMaterializedValue(GameEventScriptValue value, string detail)
        => !GameEventScriptRuntimeLimitUtilities.TryGetRangeLength(value, out var length) ||
           _context.RuntimeBudget.TryCheckRangeLength(length, detail);

    private static string GetCastTypeName(BytecodeVmCastKind castKind)
        => castKind switch
        {
            BytecodeVmCastKind.Boolean => "boolean",
            BytecodeVmCastKind.Integer => "integer",
            BytecodeVmCastKind.Decimal => "decimal",
            BytecodeVmCastKind.Number => "number",
            BytecodeVmCastKind.Percentage => "percentage",
            BytecodeVmCastKind.Degree => "degree",
            BytecodeVmCastKind.Meter => "meter",
            BytecodeVmCastKind.Second => "second",
            BytecodeVmCastKind.Sequence => "sequence",
            _ => string.Empty
        };

    private static GameEventScriptValue ConvertToDecimal(GameEventScriptValue value)
    {
        if (!GameEventScriptValueAlu.TryUnwrapOptionalForOperation(value, out var unwrappedNumber))
        {
            return GesDecimalNaN();
        }

        if (GameEventScriptValueAlu.TryEraseVectorUnit(unwrappedNumber, out var vectorWithoutUnit))
        {
            return vectorWithoutUnit;
        }

        return GameEventScriptValueAlu.TryCoerceNumericForOperation(unwrappedNumber, out var number)
            ? GameEventScriptValueAlu.ToGameEventScriptDecimal(number)
            : GesDecimalNaN();
    }

    private static GameEventScriptValue ConvertToPercentage(GameEventScriptValue value)
    {
        if (!GameEventScriptValueAlu.TryUnwrapOptionalForOperation(value, out var unwrapped))
        {
            return GesDecimalNaN();
        }

        if (unwrapped.IsPercentage())
        {
            return unwrapped;
        }

        if (unwrapped.HasDecimalUnit())
        {
            return GesDecimalNaN();
        }

        if (!GameEventScriptValueAlu.TryCoerceNumericForOperation(unwrapped, out var number) ||
            !number.IsFinite)
        {
            return GesDecimalNaN();
        }

        var ratio = unwrapped.Kind == GameEventScriptValueKind.Integer
            ? number.Value / 100m
            : number.Value > 1m || number.Value < -1m
                ? number.Value / 100m
                : number.Value;
        return GesPercentage(ratio);
    }

    private static GameEventScriptValue ConvertToDecimalUnit(GameEventScriptValue value, GameEventScriptDecimalUnit unit)
    {
        if (!GameEventScriptValueAlu.TryUnwrapOptionalForOperation(value, out var unwrapped))
        {
            return GesDecimalNaN();
        }

        if (GameEventScriptValueAlu.TryApplyVectorUnit(unwrapped, unit, out var vectorWithUnit))
        {
            return vectorWithUnit;
        }

        if (GameEventScriptValue.TryGetDecimalUnit(unwrapped, out var existingUnit) && existingUnit != unit)
        {
            return GesDecimalNaN();
        }

        if (unwrapped.Kind is GameEventScriptValueKind.Decimal or GameEventScriptValueKind.Integer &&
            GameEventScriptValueAlu.TryCoerceNumericForOperation(unwrapped, out var number) &&
            number.IsFinite)
        {
            return GesDecimal(number.Value, unit);
        }

        return GesDecimalNaN();
    }

    private static GameEventScriptValue ConvertToVector2(GameEventScriptValue value)
    {
        if (!GameEventScriptValueAlu.TryUnwrapOptionalForOperation(value, out var unwrapped))
        {
            return GameEventScriptValue.Nothing;
        }

        if (unwrapped is GameEventScriptVector2Value vector2)
        {
            return vector2;
        }

        if (unwrapped is GameEventScriptVector3Value vector3)
        {
            return GesVector2(vector3.X, vector3.Y, vector3.Unit);
        }

        if (unwrapped.TryGetDictionaryMember("x", out var x) &&
            unwrapped.TryGetDictionaryMember("y", out var y) &&
            GameEventScriptValueAlu.TryCreateVector2(x, y, out var vectorFromMembers))
        {
            return vectorFromMembers;
        }

        var items = unwrapped.AsList();
        if (items.Count >= 2 &&
            GameEventScriptValueAlu.TryCreateVector2(items[0], items[1], out var vectorFromItems))
        {
            return vectorFromItems;
        }

        return GameEventScriptValue.Nothing;
    }

    private static GameEventScriptValue ConvertToVector3(GameEventScriptValue value)
    {
        if (!GameEventScriptValueAlu.TryUnwrapOptionalForOperation(value, out var unwrapped))
        {
            return GameEventScriptValue.Nothing;
        }

        if (unwrapped is GameEventScriptVector3Value vector3)
        {
            return vector3;
        }

        if (unwrapped is GameEventScriptVector2Value vector2)
        {
            return GesVector3(vector2.X, vector2.Y, 0m, vector2.Unit);
        }

        if (unwrapped.TryGetDictionaryMember("x", out var x) &&
            unwrapped.TryGetDictionaryMember("y", out var y))
        {
            if (unwrapped.TryGetDictionaryMember("z", out var z))
            {
                return GameEventScriptValueAlu.TryCreateVector3(x, y, z, out var vectorFromMembers)
                    ? vectorFromMembers
                    : GameEventScriptValue.Nothing;
            }

            return GameEventScriptValueAlu.TryCreateVector2(x, y, out var xyVector) && xyVector is GameEventScriptVector2Value xy
                ? GesVector3(xy.X, xy.Y, 0m, xy.Unit)
                : xyVector;
        }

        var items = unwrapped.AsList();
        if (items.Count >= 3 &&
            GameEventScriptValueAlu.TryCreateVector3(items[0], items[1], items[2], out var vectorFromItems))
        {
            return vectorFromItems;
        }

        if (items.Count >= 2 &&
            GameEventScriptValueAlu.TryCreateVector2(items[0], items[1], out var xyVectorFromItems) &&
            xyVectorFromItems is GameEventScriptVector2Value xyFromItems)
        {
            return GesVector3(xyFromItems.X, xyFromItems.Y, 0m, xyFromItems.Unit);
        }

        return GameEventScriptValue.Nothing;
    }

    private GameEventScriptValue ConvertToCustomType(GameEventScriptValue value, BytecodeVmTypeDefinition typeDefinition)
    {
        if (value.TryGetCustomTypeName(out var existingTypeName) &&
            string.Equals(existingTypeName, typeDefinition.Name, StringComparison.Ordinal))
        {
            return value;
        }

        var sourceValues = value.AsDictionary().ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var materializedValues = new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal);

        foreach (var field in typeDefinition.Fields.Where(field => field.ComputedExpression is null))
        {
            sourceValues.TryGetValue(field.Name, out var rawValue);
            rawValue ??= GameEventScriptValue.Nothing;

            var fieldValue = ConvertValueToDeclaredType(rawValue, field.TypeName);
            fieldValue = ApplyFieldClamp(typeDefinition, field, fieldValue, sourceValues, materializedValues);
            fieldValue = ConvertValueToDeclaredType(fieldValue, field.TypeName);
            materializedValues[field.Name] = fieldValue;
        }

        foreach (var field in typeDefinition.Fields.Where(field => field.ComputedExpression is not null))
        {
            var computedValue = EvaluateCustomTypeExpression(field.ComputedExpression!, sourceValues, materializedValues);
            materializedValues[field.Name] = ConvertValueToDeclaredType(computedValue, field.TypeName);
        }

        return GameEventScriptValueFactory.GseCustomType(typeDefinition.Name, materializedValues);
    }

    private GameEventScriptValue ConvertValueToDeclaredType(GameEventScriptValue value, string declaredType)
        => TryConvertDeclaredType(declaredType, BytecodeVmValue.FromGameEventScriptValue(value), out var converted)
            ? converted.ToGameEventScriptValue()
            : GameEventScriptValue.Nothing;

    private GameEventScriptValue ApplyFieldClamp(
        BytecodeVmTypeDefinition typeDefinition,
        BytecodeVmTypeFieldDefinition field,
        GameEventScriptValue fieldValue,
        IReadOnlyDictionary<string, GameEventScriptValue> sourceValues,
        IReadOnlyDictionary<string, GameEventScriptValue> materializedValues)
    {
        _ = typeDefinition;
        if (field.MinimumExpression is null || field.MaximumExpression is null)
        {
            return fieldValue;
        }

        var minimum = EvaluateCustomTypeExpression(field.MinimumExpression, sourceValues, materializedValues);
        var maximum = EvaluateCustomTypeExpression(field.MaximumExpression, sourceValues, materializedValues);
        if (!GameEventScriptValueAlu.HaveCompatibleNumericUnits(fieldValue, minimum) ||
            !GameEventScriptValueAlu.HaveCompatibleNumericUnits(fieldValue, maximum) ||
            !GameEventScriptValueAlu.HaveCompatibleNumericUnits(minimum, maximum))
        {
            return GameEventScriptValueFactory.GesDecimalNaN();
        }

        if (!GameEventScriptValueAlu.TryCoerceNumericForOperation(fieldValue, out var valueNumber) ||
            !GameEventScriptValueAlu.TryCoerceNumericForOperation(minimum, out var minimumNumber) ||
            !GameEventScriptValueAlu.TryCoerceNumericForOperation(maximum, out var maximumNumber))
        {
            return fieldValue;
        }

        if (!valueNumber.IsFinite || !minimumNumber.IsFinite || !maximumNumber.IsFinite)
        {
            if (maximumNumber.IsPositiveInfinity && minimumNumber.IsFinite && valueNumber.IsFinite)
            {
                return GameEventScriptValueFactory.GesDecimal(Math.Max(valueNumber.Value, minimumNumber.Value));
            }

            return fieldValue;
        }

        var lower = Math.Min(minimumNumber.Value, maximumNumber.Value);
        var upper = Math.Max(minimumNumber.Value, maximumNumber.Value);
        GameEventScriptValue.TryGetDecimalUnit(fieldValue, out var unit);
        return GameEventScriptValueFactory.GesDecimal(Math.Min(Math.Max(valueNumber.Value, lower), upper), fieldValue.HasDecimalUnit() ? unit : null);
    }

    private GameEventScriptValue EvaluateCustomTypeExpression(
        ExpressionNode expression,
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
                    return GameEventScriptValue.Nothing;
                }
            }

            foreach (var pair in materializedValues)
            {
                if (!Define(pair.Key, BytecodeVmValue.FromGameEventScriptValue(pair.Value)))
                {
                    return GameEventScriptValue.Nothing;
                }
            }

            return TryEvaluate(expression, out var value)
                ? value.ToGameEventScriptValue()
                : GameEventScriptValue.Nothing;
        }
        finally
        {
            ExitScope();
        }
    }

    private bool TryExecutePipelineProgram(BytecodeVmPipelineProgram pipeline, out BytecodeVmValue value)
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
            BytecodeVmSelectorKind.Filter => TryExecuteProgramFilter(sourceItems, pipeline, out value),
            BytecodeVmSelectorKind.Select => TryExecuteProgramSelect(sourceItems, pipeline, out value),
            BytecodeVmSelectorKind.Predicate => TryExecuteProgramPredicate(sourceItems, pipeline, out value),
            BytecodeVmSelectorKind.Sum => TryExecuteProgramSum(sourceItems, pipeline, out value),
            BytecodeVmSelectorKind.Average => TryExecuteProgramAverage(sourceItems, pipeline, out value),
            BytecodeVmSelectorKind.Count => TryExecuteProgramCount(sourceItems, pipeline, out value),
            BytecodeVmSelectorKind.Edge => TryExecuteProgramEdge(sourceItems, pipeline, out value),
            BytecodeVmSelectorKind.Pattern => TryMaterializePipelineItems(sourceItems, pipeline, out var patternItems, out value)
                ? TryExecuteProgramPattern(terminalTarget, patternItems, pipeline.TerminalSelector, out value)
                : false,
            BytecodeVmSelectorKind.ObjectMatch => TryMaterializePipelineItems(sourceItems, pipeline, out var objectItems, out value)
                ? TryExecuteProgramObjectMatch(terminalTarget, objectItems, pipeline.TerminalSelector, out value)
                : false,
            BytecodeVmSelectorKind.TakePattern => TryMaterializePipelineItems(sourceItems, pipeline, out var takePatternItems, out value)
                ? TryExecuteProgramTakePattern(terminalTarget, takePatternItems, pipeline.TerminalSelector, out value)
                : false,
            BytecodeVmSelectorKind.Min => TryExecuteProgramExtrema(sourceItems, pipeline, isMax: false, out value),
            BytecodeVmSelectorKind.Max => TryExecuteProgramExtrema(sourceItems, pipeline, isMax: true, out value),
            BytecodeVmSelectorKind.Dictionary => TryExecuteProgramDictionary(sourceItems, pipeline, out value),
            BytecodeVmSelectorKind.Contains => TryExecuteProgramContains(sourceItems, terminalTarget, pipeline, out value),
            BytecodeVmSelectorKind.Choose => TryMaterializePipelineItems(sourceItems, pipeline, out var chooseItems, out value)
                ? TryExecuteProgramChoose(chooseItems, pipeline.TerminalSelector, out value)
                : false,
            BytecodeVmSelectorKind.Draw => TryMaterializePipelineItems(sourceItems, pipeline, out var drawItems, out value)
                ? SetValue(BytecodeVmValue.FromGameEventScriptValue(EvaluateDrawSelector(terminalTarget, drawItems, pipeline.TerminalSelector.Count)), out value)
                : false,
            BytecodeVmSelectorKind.Shuffle => TryMaterializePipelineItems(sourceItems, pipeline, out var shuffleItems, out value)
                ? SetValue(BytecodeVmValue.FromGameEventScriptValue(EvaluateShuffleSelector(terminalTarget, shuffleItems)), out value)
                : false,
            BytecodeVmSelectorKind.Sort => TryMaterializePipelineItems(sourceItems, pipeline, out var sortItems, out value)
                ? SetValue(BytecodeVmValue.FromGameEventScriptValue(GameEventScriptCollectionOperators.Sort(terminalTarget, sortItems, pipeline.TerminalSelector.EdgeMode ?? "ascending")), out value)
                : false,
            BytecodeVmSelectorKind.Distinct => TryExecuteProgramDistinct(sourceItems, terminalTarget, pipeline, out value),
            BytecodeVmSelectorKind.GroupBy => TryExecuteProgramGroupBy(sourceItems, pipeline, out value),
            BytecodeVmSelectorKind.OrderBy => TryExecuteProgramOrderBy(sourceItems, terminalTarget, pipeline, out value),
            BytecodeVmSelectorKind.Reverse => TryMaterializePipelineItems(sourceItems, pipeline, out var reverseItems, out value)
                ? SetValue(BytecodeVmValue.FromGameEventScriptValue(EvaluateReverseSelector(terminalTarget, reverseItems)), out value)
                : false,
            BytecodeVmSelectorKind.SequenceSlice => TryMaterializePipelineItems(sourceItems, pipeline, out var sliceItems, out value)
                ? SetValue(BytecodeVmValue.FromGameEventScriptValue(EvaluateSequenceSliceSelector(terminalTarget, sliceItems, pipeline.TerminalSelector)), out value)
                : false,
            _ => Fail(out value)
        };
    }

    private bool TryExecuteProgramFilter(
        IReadOnlyList<GameEventScriptValue> sourceItems,
        BytecodeVmPipelineProgram pipeline,
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
        BytecodeVmPipelineProgram pipeline,
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
        BytecodeVmPipelineProgram pipeline,
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
        BytecodeVmPipelineProgram pipeline,
        out BytecodeVmValue value)
    {
        var terminal = pipeline.TerminalSelector;
        if (terminal.ExpressionProgram is null)
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        var hasValue = false;
        var sum = BytecodeVmValue.Decimal(0m);
        var prefixSelectors = pipeline.PrefixSelectors;
        for (var itemIndex = 0; itemIndex < sourceItems.Count; itemIndex++)
        {
            if (!TryApplyProgramPipelinePrefix(
                    BytecodeVmValue.FromGameEventScriptValue(sourceItems[itemIndex]),
                    prefixSelectors,
                    out var item,
                    out var include))
            {
                value = BytecodeVmValue.Nothing;
                return false;
            }

            if (!include)
            {
                continue;
            }

            if (!TryEvaluateProgramProjection(terminal.IdentifierSlot, terminal.ExpressionProgram, item, out var projected))
            {
                value = BytecodeVmValue.Nothing;
                return false;
            }

            sum = hasValue ? BytecodeVmValue.Add(sum, projected) : projected;
            hasValue = true;
        }

        value = hasValue ? sum : BytecodeVmValue.Decimal(0m);
        return true;
    }

    private bool TryExecuteProgramAverage(
        IReadOnlyList<GameEventScriptValue> sourceItems,
        BytecodeVmPipelineProgram pipeline,
        out BytecodeVmValue value)
    {
        var terminal = pipeline.TerminalSelector;
        if (terminal.ExpressionProgram is null)
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        var count = 0L;
        var sum = BytecodeVmValue.Decimal(0m);
        var prefixSelectors = pipeline.PrefixSelectors;
        for (var itemIndex = 0; itemIndex < sourceItems.Count; itemIndex++)
        {
            if (!TryApplyProgramPipelinePrefix(
                    BytecodeVmValue.FromGameEventScriptValue(sourceItems[itemIndex]),
                    prefixSelectors,
                    out var item,
                    out var include))
            {
                value = BytecodeVmValue.Nothing;
                return false;
            }

            if (!include)
            {
                continue;
            }

            if (!TryEvaluateProgramProjection(terminal.IdentifierSlot, terminal.ExpressionProgram, item, out var projected))
            {
                value = BytecodeVmValue.Nothing;
                return false;
            }

            sum = count == 0 ? projected : BytecodeVmValue.Add(sum, projected);
            count++;
        }

        value = count > 0 && sum.TryGetFiniteNumber(out var number)
            ? BytecodeVmValue.Decimal(number / count, sum.Unit)
            : BytecodeVmValue.Nothing;
        return true;
    }

    private bool TryExecuteProgramCount(
        IReadOnlyList<GameEventScriptValue> sourceItems,
        BytecodeVmPipelineProgram pipeline,
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
        for (var itemIndex = 0; itemIndex < sourceItems.Count; itemIndex++)
        {
            if (!TryApplyProgramPipelinePrefix(
                    BytecodeVmValue.FromGameEventScriptValue(sourceItems[itemIndex]),
                    prefixSelectors,
                    out var item,
                    out var include))
            {
                value = BytecodeVmValue.Nothing;
                return false;
            }

            if (!include)
            {
                continue;
            }

            if (!TryEvaluateProgramProjection(terminal.IdentifierSlot, terminal.ExpressionProgram, item, out var predicate))
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
        BytecodeVmPipelineProgram pipeline,
        out BytecodeVmValue value)
    {
        var terminal = pipeline.TerminalSelector;
        BytecodeVmValue first = BytecodeVmValue.Nothing;
        BytecodeVmValue last = BytecodeVmValue.Nothing;
        var count = 0;
        var prefixSelectors = pipeline.PrefixSelectors;
        for (var itemIndex = 0; itemIndex < sourceItems.Count; itemIndex++)
        {
            if (!TryApplyProgramPipelinePrefix(
                    BytecodeVmValue.FromGameEventScriptValue(sourceItems[itemIndex]),
                    prefixSelectors,
                    out var item,
                    out var include))
            {
                value = BytecodeVmValue.Nothing;
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
            "first" => count > 0 ? first : BytecodeVmValue.Nothing,
            "last" => count > 0 ? last : BytecodeVmValue.Nothing,
            "single" => count == 1 ? first : BytecodeVmValue.Nothing,
            _ => BytecodeVmValue.Nothing
        };
        return true;
    }

    private bool TryExecuteProgramExtrema(
        IReadOnlyList<GameEventScriptValue> sourceItems,
        BytecodeVmPipelineProgram pipeline,
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
                if (GameEventScriptValueAlu.TryCoerceNumericForOperation(candidateProjection, out _) &&
                    GameEventScriptValueAlu.TryCoerceNumericForOperation(bestProjection, out _))
                {
                    if (!GameEventScriptValueAlu.TryCompareNumericValues(candidateProjection, bestProjection, out comparison))
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
        BytecodeVmPipelineProgram pipeline,
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

        value = BytecodeVmValue.Reference(GameEventScriptValueFactory.GseDictionary(result));
        return true;
    }

    private bool TryExecuteProgramContains(
        IReadOnlyList<GameEventScriptValue> sourceItems,
        GameEventScriptValue terminalTarget,
        BytecodeVmPipelineProgram pipeline,
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
        BytecodeVmPipelineProgram pipeline,
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
            value = BytecodeVmValue.FromGameEventScriptValue(GameEventScriptCollectionOperators.Distinct(target, items));
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
        BytecodeVmPipelineProgram pipeline,
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

        value = BytecodeVmValue.Reference(GameEventScriptValueFactory.GseDictionary(groups.ToDictionary(
            pair => pair.Key,
            pair => GameEventScriptValueFactory.GesList(pair.Value),
            StringComparer.Ordinal)));
        return true;
    }

    private bool TryExecuteProgramOrderBy(
        IReadOnlyList<GameEventScriptValue> sourceItems,
        GameEventScriptValue terminalTarget,
        BytecodeVmPipelineProgram pipeline,
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
            _ => GameEventScriptValue.Nothing
        });
        return true;
    }

    private bool TryExecuteProgramPattern(
        GameEventScriptValue target,
        IReadOnlyList<GameEventScriptValue> items,
        BytecodeVmSelectorProgram selector,
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
        BytecodeVmSelectorProgram selector,
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
        BytecodeVmSelectorProgram selector,
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
        BytecodeVmSelectorProgram selector,
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
        BytecodeVmSelectorProgram selector,
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
        BytecodeVmExpressionProgram weightProgram,
        out IReadOnlyList<GameEventScriptValue> chosen)
    {
        var remaining = candidates.ToList();
        var result = new List<GameEventScriptValue>();

        while (result.Count < count && remaining.Count > 0)
        {
            var weightedItems = new List<(GameEventScriptValue Item, decimal Weight)>();
            decimal totalWeight = 0m;

            foreach (var candidate in remaining)
            {
                if (!TryEvaluateProgramProjection(identifierSlot, weightProgram, BytecodeVmValue.FromGameEventScriptValue(candidate), out var weightValue))
                {
                    chosen = [];
                    return false;
                }

                var weight = EvaluatePositiveWeight(weightValue.ToGameEventScriptValue());
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

            result.Add(selected);
            remaining.Remove(selected);
        }

        chosen = result;
        return true;
    }

    private static decimal EvaluatePositiveWeight(GameEventScriptValue value)
        => GameEventScriptValueAlu.TryCoerceNumericForOperation(value, out var number) && number.IsFinite
            ? Math.Max(0m, number.Value)
            : 0m;

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
        BytecodeVmSelectorProgram[] prefixSelectors,
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
        BytecodeVmPipelineProgram pipeline,
        out GameEventScriptValue[] items,
        out BytecodeVmValue value)
        => TryMaterializePipelineItems(sourceItems, pipeline.PrefixSelectors, out items, out value);

    private bool TryMaterializePipelineItems(
        IReadOnlyList<GameEventScriptValue> sourceItems,
        BytecodeVmSelectorProgram[] prefixSelectors,
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
            return GameEventScriptValue.Nothing;
        }

        return GameEventScriptValueFactory.GesList(items.Reverse().ToArray());
    }

    private static GameEventScriptValue EvaluateDrawSelector(GameEventScriptValue target, IReadOnlyList<GameEventScriptValue> items, int count)
    {
        if (target.Kind is not (GameEventScriptValueKind.List or GameEventScriptValueKind.Dice))
        {
            return GameEventScriptValue.Nothing;
        }

        var drawn = items.Take(count).ToArray();
        if (count == 1)
        {
            return drawn.Length == 0 ? GameEventScriptValue.Nothing : drawn[0];
        }

        return target.Kind == GameEventScriptValueKind.Dice ? GseDice(GameEventScriptDiceValue.Create(drawn.Select(item => (int)item.AsInteger()))) : GesList(drawn);
    }

    private GameEventScriptValue EvaluateShuffleSelector(GameEventScriptValue target, IReadOnlyList<GameEventScriptValue> items)
    {
        if (target.Kind is not (GameEventScriptValueKind.List or GameEventScriptValueKind.Dice))
        {
            return GameEventScriptValue.Nothing;
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
        DicePatternNode pattern,
        out GameEventScriptValue value)
    {
        if (!IsPatternSequence(target))
        {
            value = GameEventScriptValue.Nothing;
            return true;
        }

        if (!TryTakeSequencePattern(items, pattern, out var takenItems))
        {
            value = GameEventScriptValue.Nothing;
            return true;
        }

        value = target.Kind == GameEventScriptValueKind.Dice
            ? GameEventScriptValueFactory.GseDice(GameEventScriptDiceValue.Create(takenItems.Select(item => (int)item.AsInteger())))
            : GameEventScriptValueFactory.GesList(takenItems);
        return true;
    }

    private bool TryEvaluateSequencePattern(
        GameEventScriptValue target,
        IReadOnlyList<GameEventScriptValue> items,
        DicePatternNode? pattern,
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
            case DiceCountPatternNode countPattern:
                return TryMatchDiceCountPattern(counts, countPattern, out matches);

            case DiceFullHousePatternNode:
                matches = counts.Count == 2 && counts.Values.OrderByDescending(x => x).SequenceEqual(new[] { 3, 2 });
                return true;

            case DiceStraightPatternNode:
                matches = MatchStraight(items);
                return true;

            default:
                matches = false;
                return true;
        }
    }

    private bool TryMatchDiceCountPattern(
        IReadOnlyDictionary<GameEventScriptValue, int> counts,
        DiceCountPatternNode pattern,
        out bool matches)
    {
        if (pattern.Face is not null)
        {
            if (!TryEvaluate(pattern.Face, out var face))
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
        DicePatternNode pattern,
        out IReadOnlyList<GameEventScriptValue> takenItems)
    {
        var counts = items
            .GroupBy(item => item)
            .ToDictionary(group => group.Key, group => group.Count());

        switch (pattern)
        {
            case DiceCountPatternNode countPattern:
                return TryTakeCountPattern(items, counts, countPattern, out takenItems);

            case DiceFullHousePatternNode:
                return TryTakeFullHouse(items, counts, out takenItems);

            case DiceStraightPatternNode:
                return TryTakeStraight(items, out takenItems);

            default:
                takenItems = Array.Empty<GameEventScriptValue>();
                return false;
        }
    }

    private bool TryTakeCountPattern(
        IReadOnlyList<GameEventScriptValue> items,
        IReadOnlyDictionary<GameEventScriptValue, int> counts,
        DiceCountPatternNode pattern,
        out IReadOnlyList<GameEventScriptValue> takenItems)
    {
        if (pattern.Face is not null)
        {
            if (!TryEvaluate(pattern.Face, out var face))
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
                if (GameEventScriptValueAlu.AreEqual(pairCandidate, tripleCandidate))
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
        ObjectMatchPatternNode? pattern,
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

    private bool TryMatchesObjectPattern(GameEventScriptValue value, ObjectMatchPatternNode pattern, out bool matches)
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
                case ObjectMatchExpressionValueNode expressionValue:
                    if (!TryEvaluate(expressionValue.Expression, out var expected))
                    {
                        matches = false;
                        return false;
                    }

                    if (!GameEventScriptValueAlu.AreEqual(actual, expected.ToGameEventScriptValue()))
                    {
                        matches = false;
                        return true;
                    }

                    break;

                case ObjectMatchNestedValueNode nestedValue:
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
        BytecodeVmSelectorProgram selector)
    {
        if (selector.Count <= 0)
        {
            return target.Kind == GameEventScriptValueKind.Dice
                ? GameEventScriptValueFactory.GseDice(GameEventScriptDiceValue.Empty)
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
            GameEventScriptValueKind.Dice => GameEventScriptValueFactory.GseDice(GameEventScriptDiceValue.Create(selectedItems.Select(item => (int)item.AsInteger()))),
            GameEventScriptValueKind.List => GameEventScriptValueFactory.GesList(selectedItems),
            GameEventScriptValueKind.Set => GameEventScriptValueFactory.GesList(selectedItems),
            _ => GameEventScriptValue.Nothing
        };
    }

    private static GameEventScriptValue MaterializeDistinctItems(GameEventScriptValue target, IReadOnlyList<GameEventScriptValue> items)
    {
        return target.Kind switch
        {
            GameEventScriptValueKind.Set => GameEventScriptValueFactory.GseSet(items),
            GameEventScriptValueKind.List => GameEventScriptValueFactory.GesList(items),
            GameEventScriptValueKind.Dice => GameEventScriptValueFactory.GesList(items),
            GameEventScriptValueKind.Range => GameEventScriptValueFactory.GesList(items),
            _ => GameEventScriptValue.Nothing
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
        BytecodeVmSelectorProgram[] prefixSelectors,
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
                case BytecodeVmSelectorKind.Filter:
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

                case BytecodeVmSelectorKind.Select:
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
        BytecodeVmExpressionProgram expressionProgram,
        BytecodeVmValue item,
        out BytecodeVmValue value)
        => TryExecuteExpressionProgramWithTemporarySlot(identifierSlot, item, expressionProgram, 0, out value);

    private bool TryEvaluateExpressionWithTemporarySlot(
        int slot,
        BytecodeVmValue slotValue,
        ExpressionNode expression,
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
        try
        {
            return TryEvaluate(expression, out value);
        }
        finally
        {
            RestoreSlot(slot, hadValue, previous);
        }
    }

    private bool TryEvaluateCollectionAccess(CollectionAccessExpressionNode expression, out BytecodeVmValue value)
    {
        if (expression.Selector is ExpressionSelectorNode expressionSelector)
        {
            if (!TryEvaluate(expression.Target, out var target) ||
                !TryEvaluate(expressionSelector.Expression, out var selector))
            {
                value = BytecodeVmValue.Nothing;
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
            value = BytecodeVmValue.Nothing;
            return false;
        }

        var sourceItems = sourceValue.ToGameEventScriptValue().AsList();
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
        IReadOnlyList<GameEventScriptValue> sourceItems,
        IReadOnlyList<CollectionSelectorNode> selectors,
        int prefixCount,
        SumSelectorNode selector,
        out BytecodeVmValue value)
    {
        var hasValue = false;
        var sum = BytecodeVmValue.Decimal(0m);
        var ok = TryForEachPipelineItem(sourceItems, selectors, prefixCount, item =>
        {
            if (!TryEvaluateProjection(selector.Identifier, selector.Projection, item, out var projected))
            {
                return false;
            }

            sum = hasValue ? BytecodeVmValue.Add(sum, projected) : projected;
            hasValue = true;
            return true;
        });

        value = hasValue ? sum : BytecodeVmValue.Decimal(0m);
        return ok;
    }

    private bool TryEvaluateAverage(
        IReadOnlyList<GameEventScriptValue> sourceItems,
        IReadOnlyList<CollectionSelectorNode> selectors,
        int prefixCount,
        AverageSelectorNode selector,
        out BytecodeVmValue value)
    {
        var count = 0L;
        var sum = BytecodeVmValue.Decimal(0m);
        var ok = TryForEachPipelineItem(sourceItems, selectors, prefixCount, item =>
        {
            if (!TryEvaluateProjection(selector.Identifier, selector.Projection, item, out var projected))
            {
                return false;
            }

            sum = count == 0 ? projected : BytecodeVmValue.Add(sum, projected);
            count++;
            return true;
        });

        value = ok && count > 0 && sum.TryGetFiniteNumber(out var number)
            ? BytecodeVmValue.Decimal(number / count, sum.Unit)
            : BytecodeVmValue.Nothing;
        return ok;
    }

    private bool TryEvaluateCount(
        IReadOnlyList<GameEventScriptValue> sourceItems,
        IReadOnlyList<CollectionSelectorNode> selectors,
        int prefixCount,
        CountSelectorNode selector,
        out BytecodeVmValue value)
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

        value = BytecodeVmValue.Integer(count);
        return ok;
    }

    private bool TryEvaluateEdge(
        IReadOnlyList<GameEventScriptValue> sourceItems,
        IReadOnlyList<CollectionSelectorNode> selectors,
        int prefixCount,
        EdgeSelectorNode selector,
        out BytecodeVmValue value)
    {
        BytecodeVmValue first = BytecodeVmValue.Nothing;
        BytecodeVmValue last = BytecodeVmValue.Nothing;
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
            "first" => count > 0 ? first : BytecodeVmValue.Nothing,
            "last" => count > 0 ? last : BytecodeVmValue.Nothing,
            "single" => count == 1 ? first : BytecodeVmValue.Nothing,
            _ => BytecodeVmValue.Nothing
        };
        return ok;
    }

    private bool TryForEachPipelineItem(
        IReadOnlyList<GameEventScriptValue> sourceItems,
        IReadOnlyList<CollectionSelectorNode> selectors,
        int prefixCount,
        Func<BytecodeVmValue, bool> action)
    {
        for (var index = 0; index < sourceItems.Count; index++)
        {
            if (!TryApplyPipelinePrefix(BytecodeVmValue.FromGameEventScriptValue(sourceItems[index]), selectors, 0, prefixCount, action))
            {
                return false;
            }
        }

        return true;
    }

    private bool TryApplyPipelinePrefix(
        BytecodeVmValue item,
        IReadOnlyList<CollectionSelectorNode> selectors,
        int index,
        int prefixCount,
        Func<BytecodeVmValue, bool> action)
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

    private bool TryEvaluateProjection(string identifier, ExpressionNode expression, BytecodeVmValue item, out BytecodeVmValue value)
    {
        if (!_plan.TryGetSlot(identifier, out var slot))
        {
            value = BytecodeVmValue.Nothing;
            return false;
        }

        if ((uint)slot >= (uint)_locals.Length)
        {
            value = BytecodeVmValue.Nothing;
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
        BytecodeVmValue slotValue,
        BytecodeVmExpressionProgram expressionProgram,
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
        try
        {
            return TryExecuteExpressionProgram(expressionProgram, stackBase, out value);
        }
        finally
        {
            RestoreSlot(slot, hadValue, previous);
        }
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

    private BytecodeVmValue ResolveSlot(int slot)
        => (uint)slot < (uint)_locals.Length && _assignedSlots[slot]
            ? _locals[slot]
            : BytecodeVmValue.Nothing;

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

    private void RecordRuleCalled(BytecodeVmProgramInstruction instruction, BytecodeVmValue input)
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

    private void RecordCallableCalled(BytecodeVmProgramInstruction instruction, BytecodeVmValue[] stack, int start, int count)
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

        var kind = instruction.CallableKind == BytecodeVmCallableKind.Rule
            ? GameEventScriptDiagnosticEventKind.RuleCalled
            : GameEventScriptDiagnosticEventKind.SelectCalled;
        var kindText = instruction.CallableKind == BytecodeVmCallableKind.Rule ? "rule" : "select";
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
            CreateSingleArgument(identifier, GameEventScriptValue.Nothing),
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
            CreateSingleArgument(argumentName, GameEventScriptValue.Nothing),
            $"Publish argument '{argumentName}' evaluated to Nothing.");
    }

    private void RecordExpressionStatementEvaluatedToNothing(ExpressionNode expression, BytecodeVmValue value)
    {
        if (!_diagnosticsEnabled || value.Kind != BytecodeVmValueKind.Nothing)
        {
            return;
        }

        var name = expression.GetType().Name;
        RecordDiagnostic(
            GameEventScriptDiagnosticEventKind.ExpressionEvaluatedToNothing,
            name,
            CreateSingleArgument(name, GameEventScriptValue.Nothing),
            "Expression statement evaluated to Nothing.");
    }

    private void RecordDiagnostic(
        GameEventScriptDiagnosticEventKind kind,
        string name,
        IReadOnlyDictionary<string, GameEventScriptValue> arguments,
        string? detail = null)
        => GameEventScriptInvocationKernel.RecordDiagnostic(_context, _diagnosticsEnabled, kind, name, arguments, detail);

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

    private static long ToLongSaturated(decimal value)
    {
        if (value > long.MaxValue) return long.MaxValue;
        if (value < long.MinValue) return long.MinValue;
        return (long)value;
    }

    private readonly record struct LocalChange(int Slot, bool HadValue, BytecodeVmValue PreviousValue);
}

internal enum BytecodeVmValueKind
{
    Nothing,
    Boolean,
    Integer,
    Decimal,
    Percentage,
    Reference
}

internal readonly record struct BytecodeVmValue(
    BytecodeVmValueKind Kind,
    decimal Number,
    long IntegerValue,
    bool BooleanValue,
    GameEventScriptDecimalUnit? Unit,
    GameEventScriptValue? ReferenceValue)
{
    public static BytecodeVmValue Nothing { get; } = new(BytecodeVmValueKind.Nothing, 0m, 0, false, null, null);

    public static BytecodeVmValue Boolean(bool value)
        => new(BytecodeVmValueKind.Boolean, value ? 1m : 0m, value ? 1 : 0, value, null, null);

    public static BytecodeVmValue Integer(long value)
        => new(BytecodeVmValueKind.Integer, value, value, value != 0, null, null);

    public static BytecodeVmValue Decimal(decimal value, GameEventScriptDecimalUnit? unit = null)
        => new(BytecodeVmValueKind.Decimal, value, ToLongSaturated(value), value != 0m, unit, null);

    public static BytecodeVmValue Percentage(decimal ratio)
        => new(BytecodeVmValueKind.Percentage, ratio, ToLongSaturated(ratio * 100m), ratio != 0m, null, null);

    public static BytecodeVmValue Reference(GameEventScriptValue value)
        => new(BytecodeVmValueKind.Reference, 0m, 0, value.AsBoolean(), null, value);

    public static BytecodeVmValue NaN()
        => Reference(GesDecimalNaN());

    public static BytecodeVmValue FromGameEventScriptValue(GameEventScriptValue value)
        => value switch
        {
            GameEventScriptBooleanValue boolean => Boolean(boolean.Value),
            GameEventScriptIntegerValue integer => Integer(integer.Value),
            GameEventScriptDecimalValue decimalValue when decimalValue.HasSemanticValue() => Decimal(decimalValue.Value, decimalValue.Unit),
            GameEventScriptPercentageValue percentage => Percentage(percentage.Ratio),
            _ => Reference(value)
        };

    public static BytecodeVmValue FromGameEventScriptFastValue(GameEventScriptFastValue value)
        => value.Kind switch
        {
            GameEventScriptValueKind.Nothing => Nothing,
            GameEventScriptValueKind.Boolean => Boolean(value.Boolean),
            GameEventScriptValueKind.Integer => Integer(value.Integer),
            GameEventScriptValueKind.Decimal when !value.IsReferenceBacked => Decimal(value.Number, value.Unit),
            GameEventScriptValueKind.Percentage when !value.IsReferenceBacked => Percentage(value.Number),
            _ => FromGameEventScriptValue(value.ToGameEventScriptValue())
        };

    public bool AsBoolean()
        => Kind switch
        {
            BytecodeVmValueKind.Boolean => BooleanValue,
            BytecodeVmValueKind.Integer => IntegerValue != 0,
            BytecodeVmValueKind.Decimal => Number != 0m,
            BytecodeVmValueKind.Percentage => Number != 0m,
            BytecodeVmValueKind.Reference => ReferenceValue?.AsBoolean() ?? false,
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

    public GameEventScriptValue ToGameEventScriptValue()
        => Kind switch
        {
            BytecodeVmValueKind.Nothing => GameEventScriptValue.Nothing,
            BytecodeVmValueKind.Boolean => GameEventScriptValueFactory.GesBoolean(BooleanValue),
            BytecodeVmValueKind.Integer => GameEventScriptValueFactory.GesInteger(IntegerValue),
            BytecodeVmValueKind.Decimal => GameEventScriptValueFactory.GesDecimal(Number, Unit),
            BytecodeVmValueKind.Percentage => GameEventScriptValueFactory.GesPercentage(Number),
            BytecodeVmValueKind.Reference => ReferenceValue ?? GameEventScriptValue.Nothing,
            _ => GameEventScriptValue.Nothing
        };

    public static bool AreEqual(BytecodeVmValue left, BytecodeVmValue right)
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

        return left.ToGameEventScriptValue().Equals(right.ToGameEventScriptValue());
    }

    public static int CompareNumeric(BytecodeVmValue left, BytecodeVmValue right)
    {
        return TryCompareNumeric(left, right, out var comparison) ? comparison : 0;
    }

    public static bool TryCompareNumeric(BytecodeVmValue left, BytecodeVmValue right, out int comparison)
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

        return GameEventScriptValueAlu.TryCompareNumeric(leftNumber, rightNumber, out comparison);
    }

    public static BytecodeVmValue Add(BytecodeVmValue left, BytecodeVmValue right)
    {
        if (TryEvaluateVectorBinary(left, "+", right, out var vectorResult))
        {
            return vectorResult;
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

            return GameEventScriptValueAlu.TryAddFinite(leftPrimitive, rightPrimitive, out var sum)
                ? FromFinitePrimitiveNumericResult(left, "+", right, sum, left.Unit)
                : FromDecimalNumeric(
                    GameEventScriptValueAlu.AddNumeric(
                        GameEventScriptValueAlu.NumericValue.Finite(leftPrimitive),
                        GameEventScriptValueAlu.NumericValue.Finite(rightPrimitive)),
                    left.Unit);
        }

        if (!left.TryGetNumeric(out var leftNumber, out var leftUnit, out _) ||
            !right.TryGetNumeric(out var rightNumber, out var rightUnit, out _))
        {
            var leftValue = left.ToGameEventScriptValue();
            var rightValue = right.ToGameEventScriptValue();
            if (GameEventScriptValueAlu.TryCombineWithPlus(leftValue, rightValue, out var combined))
            {
                return FromGameEventScriptValue(combined);
            }

            return leftValue.IsText() || rightValue.IsText()
                ? Reference(GesText($"{GameEventScriptValueAlu.ToText(leftValue)}{GameEventScriptValueAlu.ToText(rightValue)}"))
                : NaN();
        }

        if (leftUnit != rightUnit)
        {
            return NaN();
        }

        return FromDecimalNumeric(GameEventScriptValueAlu.AddNumeric(leftNumber, rightNumber), leftUnit);
    }

    public static BytecodeVmValue Subtract(BytecodeVmValue left, BytecodeVmValue right)
    {
        if (TryEvaluateVectorBinary(left, "-", right, out var vectorResult))
        {
            return vectorResult;
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

            return GameEventScriptValueAlu.TryNegateFinite(rightPrimitive, out var negatedRight) &&
                   GameEventScriptValueAlu.TryAddFinite(leftPrimitive, negatedRight, out var difference)
                ? FromFinitePrimitiveNumericResult(left, "-", right, difference, left.Unit)
                : FromDecimalNumeric(
                    GameEventScriptValueAlu.SubtractNumeric(
                        GameEventScriptValueAlu.NumericValue.Finite(leftPrimitive),
                        GameEventScriptValueAlu.NumericValue.Finite(rightPrimitive)),
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

        return FromDecimalNumeric(GameEventScriptValueAlu.SubtractNumeric(leftNumber, rightNumber), leftUnit);
    }

    public static BytecodeVmValue Multiply(BytecodeVmValue left, BytecodeVmValue right)
    {
        if (TryEvaluateVectorBinary(left, "*", right, out var vectorResult))
        {
            return vectorResult;
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

            return GameEventScriptValueAlu.TryMultiplyFinite(leftPrimitive, rightPrimitive, out var product)
                ? FromFinitePrimitiveNumericResult(left, "*", right, product, left.Unit ?? right.Unit)
                : FromDecimalNumeric(
                    GameEventScriptValueAlu.MultiplyNumeric(
                        GameEventScriptValueAlu.NumericValue.Finite(leftPrimitive),
                        GameEventScriptValueAlu.NumericValue.Finite(rightPrimitive)),
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

        return FromDecimalNumeric(GameEventScriptValueAlu.MultiplyNumeric(leftNumber, rightNumber), leftUnit ?? rightUnit);
    }

    public static BytecodeVmValue Divide(BytecodeVmValue left, BytecodeVmValue right)
    {
        if (TryEvaluateVectorBinary(left, "/", right, out var vectorResult))
        {
            return vectorResult;
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

            return rightPrimitive != 0m && GameEventScriptValueAlu.TryDivideFinite(leftPrimitive, rightPrimitive, out var quotient)
                ? Decimal(quotient, primitiveResultUnit)
                : FromDecimalNumeric(
                    GameEventScriptValueAlu.DivideNumeric(
                        GameEventScriptValueAlu.NumericValue.Finite(leftPrimitive),
                        GameEventScriptValueAlu.NumericValue.Finite(rightPrimitive)),
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

        return FromDecimalNumeric(GameEventScriptValueAlu.DivideNumeric(leftNumber, rightNumber), resultUnit);
    }

    public static BytecodeVmValue IntegerDivide(BytecodeVmValue left, BytecodeVmValue right)
    {
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

            return rightPrimitive != 0m && GameEventScriptValueAlu.TryDivideFinite(leftPrimitive, rightPrimitive, out var quotient)
                ? FromFinitePrimitiveNumericResult(left, "div", right, Math.Floor(quotient), primitiveResultUnit)
                : FromDecimalNumeric(
                    GameEventScriptValueAlu.IntegerDivideNumeric(
                        GameEventScriptValueAlu.NumericValue.Finite(leftPrimitive),
                        GameEventScriptValueAlu.NumericValue.Finite(rightPrimitive)),
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

        var result = GameEventScriptValueAlu.IntegerDivideNumeric(leftNumber, rightNumber);
        return resultUnit is null && result.IsFinite && TryToInteger(result.Value, out var integer)
            ? Integer(integer)
            : FromDecimalNumeric(result, resultUnit);
    }

    public static BytecodeVmValue Modulo(BytecodeVmValue left, BytecodeVmValue right)
    {
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

            return rightPrimitive != 0m && GameEventScriptValueAlu.TryModuloFinite(leftPrimitive, rightPrimitive, out var modulo)
                ? FromFinitePrimitiveNumericResult(left, "mod", right, modulo, left.Unit)
                : FromDecimalNumeric(
                    GameEventScriptValueAlu.ModuloNumeric(
                        GameEventScriptValueAlu.NumericValue.Finite(leftPrimitive),
                        GameEventScriptValueAlu.NumericValue.Finite(rightPrimitive)),
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

        return FromDecimalNumeric(GameEventScriptValueAlu.ModuloNumeric(leftNumber, rightNumber), leftUnit);
    }

    public static BytecodeVmValue Remainder(BytecodeVmValue left, BytecodeVmValue right)
    {
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

            return rightPrimitive != 0m && GameEventScriptValueAlu.TryRemainderFinite(leftPrimitive, rightPrimitive, out var remainder)
                ? FromFinitePrimitiveNumericResult(left, "rem", right, remainder, left.Unit)
                : FromDecimalNumeric(
                    GameEventScriptValueAlu.RemainderNumeric(
                        GameEventScriptValueAlu.NumericValue.Finite(leftPrimitive),
                        GameEventScriptValueAlu.NumericValue.Finite(rightPrimitive)),
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

        return FromDecimalNumeric(GameEventScriptValueAlu.RemainderNumeric(leftNumber, rightNumber), leftUnit);
    }

    private static bool TryGetDivideResultUnit(
        GameEventScriptDecimalUnit? leftUnit,
        GameEventScriptDecimalUnit? rightUnit,
        out GameEventScriptDecimalUnit? resultUnit)
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

    private static BytecodeVmValue AddPercentage(BytecodeVmValue left, BytecodeVmValue right)
    {
        if (!left.TryGetNumeric(out var leftNumber, out var leftUnit, out var leftIsPercentage) ||
            !right.TryGetNumeric(out var rightNumber, out _, out var rightIsPercentage))
        {
            return NaN();
        }

        if (leftIsPercentage && rightIsPercentage)
        {
            return FromPercentageNumeric(GameEventScriptValueAlu.AddNumeric(leftNumber, rightNumber));
        }

        if (leftIsPercentage)
        {
            return NaN();
        }

        var delta = GameEventScriptValueAlu.MultiplyNumeric(leftNumber, rightNumber);
        return FromDecimalNumeric(GameEventScriptValueAlu.AddNumeric(leftNumber, delta), leftUnit);
    }

    private static BytecodeVmValue SubtractPercentage(BytecodeVmValue left, BytecodeVmValue right)
    {
        if (!left.TryGetNumeric(out var leftNumber, out var leftUnit, out var leftIsPercentage) ||
            !right.TryGetNumeric(out var rightNumber, out _, out var rightIsPercentage))
        {
            return NaN();
        }

        if (leftIsPercentage && rightIsPercentage)
        {
            return FromPercentageNumeric(GameEventScriptValueAlu.SubtractNumeric(leftNumber, rightNumber));
        }

        if (leftIsPercentage)
        {
            return NaN();
        }

        var delta = GameEventScriptValueAlu.MultiplyNumeric(leftNumber, rightNumber);
        return FromDecimalNumeric(GameEventScriptValueAlu.SubtractNumeric(leftNumber, delta), leftUnit);
    }

    private static BytecodeVmValue MultiplyPercentage(BytecodeVmValue left, BytecodeVmValue right)
    {
        if (!left.TryGetNumeric(out var leftNumber, out var leftUnit, out var leftIsPercentage) ||
            !right.TryGetNumeric(out var rightNumber, out var rightUnit, out var rightIsPercentage))
        {
            return NaN();
        }

        var result = GameEventScriptValueAlu.MultiplyNumeric(leftNumber, rightNumber);
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

    private static BytecodeVmValue DividePercentage(BytecodeVmValue left, BytecodeVmValue right)
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

        var result = GameEventScriptValueAlu.DivideNumeric(leftNumber, rightNumber);
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
        out GameEventScriptValueAlu.NumericValue number,
        out GameEventScriptDecimalUnit? unit,
        out bool isPercentage)
    {
        switch (Kind)
        {
            case BytecodeVmValueKind.Boolean:
                number = GameEventScriptValueAlu.NumericValue.Finite(BooleanValue ? 1m : 0m);
                unit = null;
                isPercentage = false;
                return true;
            case BytecodeVmValueKind.Integer:
                number = GameEventScriptValueAlu.NumericValue.Finite(IntegerValue);
                unit = null;
                isPercentage = false;
                return true;
            case BytecodeVmValueKind.Decimal:
                number = GameEventScriptValueAlu.NumericValue.Finite(Number);
                unit = Unit;
                isPercentage = false;
                return true;
            case BytecodeVmValueKind.Percentage:
                number = GameEventScriptValueAlu.NumericValue.Finite(Number);
                unit = null;
                isPercentage = true;
                return true;
            case BytecodeVmValueKind.Reference when ReferenceValue is { } reference &&
                                                       GameEventScriptValueAlu.TryCoerceNumericForOperation(reference, out var referenceNumber):
                number = referenceNumber;
                unit = GameEventScriptValue.TryGetDecimalUnit(reference, out var referenceUnit) ? referenceUnit : null;
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
            case BytecodeVmValueKind.Boolean:
                number = BooleanValue ? 1m : 0m;
                return true;
            case BytecodeVmValueKind.Integer:
                number = IntegerValue;
                return true;
            case BytecodeVmValueKind.Decimal:
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
        => ReferenceValue is GameEventScriptVector2Value or GameEventScriptVector3Value;

    private static bool TryEvaluateVectorBinary(BytecodeVmValue left, string operation, BytecodeVmValue right, out BytecodeVmValue value)
    {
        if (!left.IsVectorLike() && !right.IsVectorLike())
        {
            value = default;
            return false;
        }

        if (GameEventScriptValueAlu.TryEvaluateVectorBinary(left.ToGameEventScriptValue(), operation, right.ToGameEventScriptValue(), out var vectorValue))
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

    private static BytecodeVmValue FromDecimalNumeric(GameEventScriptValueAlu.NumericValue number, GameEventScriptDecimalUnit? unit = null)
        => number.IsFinite
            ? Decimal(number.Value, unit)
            : Reference(GameEventScriptValueAlu.ToGameEventScriptDecimal(number));

    private static BytecodeVmValue FromFinitePrimitiveNumericResult(
        BytecodeVmValue left,
        string operation,
        BytecodeVmValue right,
        decimal value,
        GameEventScriptDecimalUnit? unit = null)
        => unit is null &&
           operation == "div" &&
           TryToInteger(value, out var quotient)
            ? Integer(quotient)
            : unit is null &&
           operation is "+" or "-" or "*" or "mod" or "rem" &&
           left.Kind == BytecodeVmValueKind.Integer &&
           right.Kind == BytecodeVmValueKind.Integer &&
           TryToInteger(value, out var integer)
            ? Integer(integer)
            : Decimal(value, unit);

    private static BytecodeVmValue FromPercentageNumeric(GameEventScriptValueAlu.NumericValue number)
        => number.IsFinite
            ? Percentage(number.Value)
            : FromDecimalNumeric(number);

    private static bool SetNumber(decimal input, out decimal output)
    {
        output = input;
        return true;
    }

    private static bool TryToInteger(decimal value, out long integer)
    {
        if (value != decimal.Truncate(value) ||
            value > long.MaxValue ||
            value < long.MinValue)
        {
            integer = default;
            return false;
        }

        integer = (long)value;
        return true;
    }

    private static long ToLongSaturated(decimal value)
    {
        if (value > long.MaxValue) return long.MaxValue;
        if (value < long.MinValue) return long.MinValue;
        return (long)value;
    }
}
