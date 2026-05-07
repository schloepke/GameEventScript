using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.Compiler;

internal sealed class GesLinearBytecodeBuilder
{
    private readonly List<GameEventScriptBytecodeInstruction> _code = [];
    private int _maxFrameSlots = 1;

    public IReadOnlyList<GameEventScriptBytecodeInstruction> Code => _code;

    public int MaxFrameSlots => _maxFrameSlots;

    public void AddHandlers(IEnumerable<GameEventScriptBytecodeHandler> handlers)
    {
        foreach (var handler in handlers)
        {
            handler.EntryAddress = _code.Count;
            _maxFrameSlots = Math.Max(_maxFrameSlots, handler.LocalSlotCount);
            EmitParameterBindings(handler.Parameters, handler.ParameterTypes, handler.ExecutionPlan);
            EmitStatementProgram(handler.ExecutionPlan.StatementProgram, handler.ExecutionPlan);
            Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.Return));
        }
    }

    public void AddCallables(IEnumerable<GameEventScriptBytecodeCallable> callables)
    {
        foreach (var callable in callables)
        {
            callable.EntryAddress = _code.Count;
            callable.LocalSlotCount = Math.Max(callable.Parameters.Count + callable.ExpressionProgram.MaxStackDepth + 1, callable.Parameters.Count + 1);
            _maxFrameSlots = Math.Max(_maxFrameSlots, callable.LocalSlotCount);
            for (var index = 0; index < callable.Parameters.Count; index++)
            {
                Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.BindParameter, Dest: index, A: index));
                if (index < callable.ParameterTypes.Count && !string.IsNullOrEmpty(callable.ParameterTypes[index]))
                {
                    Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.CoerceSlot, Dest: index, A: index));
                }
            }

            var state = new ExpressionState(callable.Parameters.Count);
            var result = EmitExpression(callable.ExpressionProgram, state);
            callable.ReturnSlot = result;
            Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.Return, A: result));
        }
    }

    public void AddTypeDefinitions(IEnumerable<GameEventScriptBytecodeTypeDefinition> types)
    {
        foreach (var field in types.SelectMany(type => type.Fields))
        {
            if (field.MinimumProgram is not null)
            {
                field.MinimumEntryAddress = EmitExpressionEntry(field.MinimumProgram);
            }

            if (field.MaximumProgram is not null)
            {
                field.MaximumEntryAddress = EmitExpressionEntry(field.MaximumProgram);
            }

            if (field.ComputedProgram is not null)
            {
                field.ComputedEntryAddress = EmitExpressionEntry(field.ComputedProgram);
            }
        }
    }

    private int EmitExpressionEntry(GameEventScriptBytecodeExpressionProgram program)
    {
        var entry = _code.Count;
        var state = new ExpressionState(0);
        var result = EmitExpression(program, state);
        _maxFrameSlots = Math.Max(_maxFrameSlots, state.NextSlot);
        Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.Return, A: result));
        return entry;
    }

    private void EmitParameterBindings(
        IReadOnlyList<string> parameters,
        IReadOnlyList<string?> parameterTypes,
        GameEventScriptBytecodeExecutionPlan plan)
    {
        for (var index = 0; index < parameters.Count; index++)
        {
            if (!plan.TryGetSlot(parameters[index], out var slot))
            {
                continue;
            }

            Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.BindParameter, Dest: slot, A: index));
            if (index < parameterTypes.Count && !string.IsNullOrEmpty(parameterTypes[index]))
            {
                Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.CoerceSlot, Dest: slot, A: slot));
            }
        }
    }

    private void EmitStatementProgram(GameEventScriptBytecodeStatementProgram program, GameEventScriptBytecodeExecutionPlan plan)
    {
        if (program.CreatesScope)
        {
            Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.EnterScope));
        }

        foreach (var statement in program.Statements)
        {
            EmitStatement(statement, plan);
        }

        if (program.CreatesScope)
        {
            Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.ExitScope));
        }
    }

    private void EmitStatement(GameEventScriptBytecodeStatement statement, GameEventScriptBytecodeExecutionPlan plan)
    {
        switch (statement.Kind)
        {
            case GameEventScriptBytecodeStatementKind.Let:
                if (statement.ExpressionProgram is not null &&
                    !string.IsNullOrEmpty(statement.Name) &&
                    plan.TryGetSlot(statement.Name, out var letSlot))
                {
                    var result = EmitExpression(statement.ExpressionProgram, new ExpressionState(plan.SlotCount));
                    Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.CopySlot, Dest: letSlot, A: result));
                    if (!string.IsNullOrEmpty(statement.DeclaredType))
                    {
                        Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.CoerceSlot, Dest: letSlot, A: letSlot));
                    }
                }
                break;

            case GameEventScriptBytecodeStatementKind.Expression:
                if (statement.ExpressionProgram is not null)
                {
                    _ = EmitExpression(statement.ExpressionProgram, new ExpressionState(plan.SlotCount));
                }
                break;

            case GameEventScriptBytecodeStatementKind.Publish:
                EmitPublish(statement, plan);
                break;

            case GameEventScriptBytecodeStatementKind.If:
                EmitIf(statement, plan);
                break;

            case GameEventScriptBytecodeStatementKind.ForRange:
            case GameEventScriptBytecodeStatementKind.ForCollection:
                EmitLoop(statement, plan);
                break;

            case GameEventScriptBytecodeStatementKind.SeededRandom:
                EmitSeededRandom(statement, plan);
                break;
        }
    }

    private void EmitPublish(GameEventScriptBytecodeStatement statement, GameEventScriptBytecodeExecutionPlan plan)
    {
        foreach (var tagProgram in statement.TagPrograms)
        {
            _ = EmitExpression(tagProgram, new ExpressionState(plan.SlotCount));
        }

        if (statement.PublishLayout is not null)
        {
            foreach (var argumentProgram in statement.PublishLayout.ArgumentPrograms)
            {
                _ = EmitExpression(argumentProgram, new ExpressionState(plan.SlotCount));
            }

            Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.PublishValue, A: (int)statement.PublishKind));
            return;
        }

        if (statement.ExpressionProgram is not null)
        {
            var messageSlot = EmitExpression(statement.ExpressionProgram, new ExpressionState(plan.SlotCount));
            Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.PublishMessageValue, A: messageSlot, B: (int)statement.PublishKind));
        }
    }

    private void EmitIf(GameEventScriptBytecodeStatement statement, GameEventScriptBytecodeExecutionPlan plan)
    {
        if (statement.ExpressionProgram is null || statement.ThenProgram is null)
        {
            return;
        }

        var condition = EmitExpression(statement.ExpressionProgram, new ExpressionState(plan.SlotCount));
        var jumpToElse = Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.JumpIfNotTrue, A: condition));
        EmitStatementProgram(statement.ThenProgram, plan);
        var jumpToEnd = Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.Jump));
        PatchTarget(jumpToElse, _code.Count);
        if (statement.ElseProgram is not null)
        {
            EmitStatementProgram(statement.ElseProgram, plan);
        }

        PatchTarget(jumpToEnd, _code.Count);
    }

    private void EmitLoop(GameEventScriptBytecodeStatement statement, GameEventScriptBytecodeExecutionPlan plan)
    {
        if (statement.IterationSource is null || statement.BodyProgram is null)
        {
            return;
        }

        var opCode = statement.Kind == GameEventScriptBytecodeStatementKind.ForRange
            ? GameEventScriptBytecodeOpCode.ForRange
            : GameEventScriptBytecodeOpCode.ForCollection;
        var loopInstruction = Emit(new GameEventScriptBytecodeInstruction(opCode));
        var bodyAddress = _code.Count;
        EmitStatementProgram(statement.BodyProgram, plan);
        PatchTargets(loopInstruction, bodyAddress, _code.Count);
    }

    private void EmitSeededRandom(GameEventScriptBytecodeStatement statement, GameEventScriptBytecodeExecutionPlan plan)
    {
        if (statement.ExpressionProgram is not null)
        {
            _ = EmitExpression(statement.ExpressionProgram, new ExpressionState(plan.SlotCount));
        }

        var blockInstruction = Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.SeededRandomBlock));
        var bodyAddress = _code.Count;
        if (statement.BodyProgram is not null)
        {
            EmitStatementProgram(statement.BodyProgram, plan);
        }

        PatchTargets(blockInstruction, bodyAddress, _code.Count);
    }

    private int EmitExpression(GameEventScriptBytecodeExpressionProgram program, ExpressionState state)
    {
        var stack = new Stack<int>();
        foreach (var instruction in program.Instructions)
        {
            EmitExpressionInstruction(instruction, state, stack);
        }

        _maxFrameSlots = Math.Max(_maxFrameSlots, state.NextSlot);
        return stack.Count > 0 ? stack.Peek() : -1;
    }

    private void EmitExpressionInstruction(
        GameEventScriptBytecodeStackInstruction instruction,
        ExpressionState state,
        Stack<int> stack)
    {
        switch (instruction.OpCode)
        {
            case GameEventScriptBytecodeOpCode.LoadConstant:
                stack.Push(EmitValueInstruction(state, instruction.OpCode, data: instruction.ConstantIndex));
                break;

            case GameEventScriptBytecodeOpCode.LoadSlot:
                stack.Push(EmitValueInstruction(state, instruction.OpCode, a: instruction.A));
                break;

            case GameEventScriptBytecodeOpCode.Cast:
            case GameEventScriptBytecodeOpCode.TypeCheck:
            case GameEventScriptBytecodeOpCode.MemberAccess:
            case GameEventScriptBytecodeOpCode.Unary:
                if (stack.TryPop(out var unarySource))
                {
                    stack.Push(EmitValueInstruction(state, instruction.OpCode, a: unarySource, data: (int)instruction.CastKind));
                }
                break;

            case GameEventScriptBytecodeOpCode.ShortCircuitOr:
            case GameEventScriptBytecodeOpCode.ShortCircuitAnd:
            case GameEventScriptBytecodeOpCode.ShortCircuitImplies:
                if (stack.TryPop(out var left))
                {
                    stack.Push(EmitShortCircuitExpression(instruction, state, left));
                }
                break;

            case GameEventScriptBytecodeOpCode.PredicateTest:
                if (stack.TryPop(out var input))
                {
                    stack.Push(EmitValueInstruction(state, instruction.OpCode, a: input));
                }
                break;

            case GameEventScriptBytecodeOpCode.Dice:
            case GameEventScriptBytecodeOpCode.Pipeline:
            case GameEventScriptBytecodeOpCode.GeneratedCollection:
            case GameEventScriptBytecodeOpCode.GuardedChoice:
                stack.Push(EmitValueInstruction(state, instruction.OpCode, a: instruction.A, b: instruction.B));
                break;

            default:
                EmitDefaultExpressionInstruction(instruction, state, stack);
                break;
        }
    }

    private void EmitDefaultExpressionInstruction(
        GameEventScriptBytecodeStackInstruction instruction,
        ExpressionState state,
        Stack<int> stack)
    {
        var argumentCount = GetStackArgumentCount(instruction);
        var operands = new int[Math.Max(argumentCount, 0)];
        for (var index = operands.Length - 1; index >= 0; index--)
        {
            operands[index] = stack.TryPop(out var operand) ? operand : -1;
        }

        var a = operands.Length > 0 ? operands[0] : instruction.A;
        var b = operands.Length > 1 ? operands[1] : instruction.B;
        var c = operands.Length > 2 ? operands[2] : -1;
        stack.Push(EmitValueInstruction(state, instruction.OpCode, a, b, c, data: instruction.A));
    }

    private int EmitShortCircuitExpression(GameEventScriptBytecodeStackInstruction instruction, ExpressionState state, int left)
    {
        var result = state.Allocate();
        if (instruction.OpCode == GameEventScriptBytecodeOpCode.ShortCircuitImplies)
        {
            Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.ShortCircuitImplies, Dest: result, A: left, B: left));
        }
        else
        {
            Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.CopySlot, Dest: result, A: left));
        }

        var branch = instruction.OpCode switch
        {
            GameEventScriptBytecodeOpCode.ShortCircuitOr => GameEventScriptBytecodeOpCode.JumpIfTrue,
            GameEventScriptBytecodeOpCode.ShortCircuitAnd => GameEventScriptBytecodeOpCode.JumpIfFalse,
            _ => GameEventScriptBytecodeOpCode.JumpIfFalse
        };
        var jump = Emit(new GameEventScriptBytecodeInstruction(branch, A: left));
        if (instruction.ExpressionProgram is not null)
        {
            var right = EmitExpression(instruction.ExpressionProgram, state);
            var opCode = instruction.OpCode switch
            {
                GameEventScriptBytecodeOpCode.ShortCircuitOr => GameEventScriptBytecodeOpCode.Or,
                GameEventScriptBytecodeOpCode.ShortCircuitAnd => GameEventScriptBytecodeOpCode.And,
                _ => GameEventScriptBytecodeOpCode.ShortCircuitImplies
            };
            Emit(new GameEventScriptBytecodeInstruction(opCode, Dest: result, A: left, B: right));
        }

        PatchTarget(jump, _code.Count);
        return result;
    }

    private int EmitValueInstruction(
        ExpressionState state,
        GameEventScriptBytecodeOpCode opCode,
        int a = -1,
        int b = -1,
        int c = -1,
        int data = -1)
    {
        var dest = state.Allocate();
        Emit(new GameEventScriptBytecodeInstruction(opCode, Dest: dest, A: a, B: b, C: c, Data: data));
        return dest;
    }

    private int Emit(GameEventScriptBytecodeInstruction instruction)
    {
        var address = _code.Count;
        _code.Add(instruction);
        return address;
    }

    private void PatchTarget(int address, int target)
        => _code[address] = _code[address] with { Target = target };

    private void PatchTargets(int address, int target, int target2)
        => _code[address] = _code[address] with { Target = target, Target2 = target2 };

    private static int GetStackArgumentCount(GameEventScriptBytecodeStackInstruction instruction)
        => instruction.OpCode switch
        {
            GameEventScriptBytecodeOpCode.Range => instruction.A,
            GameEventScriptBytecodeOpCode.TypeConstructor => instruction.A,
            GameEventScriptBytecodeOpCode.BuildList => instruction.A,
            GameEventScriptBytecodeOpCode.BuildSequence => instruction.A,
            GameEventScriptBytecodeOpCode.BuildSet => instruction.A,
            GameEventScriptBytecodeOpCode.BuildDictionary => instruction.A,
            GameEventScriptBytecodeOpCode.BuildMessage => instruction.A,
            GameEventScriptBytecodeOpCode.BindHandler => instruction.A + 1,
            GameEventScriptBytecodeOpCode.CallExtension => instruction.A,
            GameEventScriptBytecodeOpCode.Call => instruction.A,
            GameEventScriptBytecodeOpCode.Variadic => instruction.A,
            GameEventScriptBytecodeOpCode.Clamp => 3,
            GameEventScriptBytecodeOpCode.Random => 2,
            GameEventScriptBytecodeOpCode.IndexedAccess => 2,
            GameEventScriptBytecodeOpCode.SeededRandom => 1,
            _ => IsBinary(instruction.OpCode) ? 2 : 0
        };

    private static bool IsBinary(GameEventScriptBytecodeOpCode opCode)
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
            GameEventScriptBytecodeOpCode.PrimitiveIntegerRemainder or
            GameEventScriptBytecodeOpCode.Default or
            GameEventScriptBytecodeOpCode.Contains or
            GameEventScriptBytecodeOpCode.ContainsValue or
            GameEventScriptBytecodeOpCode.StartsWith or
            GameEventScriptBytecodeOpCode.EndsWith or
            GameEventScriptBytecodeOpCode.Intersect or
            GameEventScriptBytecodeOpCode.Combine or
            GameEventScriptBytecodeOpCode.Except or
            GameEventScriptBytecodeOpCode.Zip;

    private sealed class ExpressionState(int nextSlot)
    {
        public int NextSlot { get; private set; } = Math.Max(0, nextSlot);

        public int Allocate()
            => NextSlot++;
    }
}
