using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.BytecodeVM;

internal sealed class GesLinearExpressionProgram
{
    private static readonly ConditionalWeakTable<GameEventScriptBytecodeExpressionProgram, GesLinearExpressionProgram> Cache = new();

    private GesLinearExpressionProgram(
        GesLinearExpressionInstruction[] instructions,
        int maxSlots,
        int maxOperandCount,
        int returnSlot)
    {
        Instructions = instructions;
        MaxSlots = Math.Max(1, maxSlots);
        MaxOperandCount = maxOperandCount;
        ReturnSlot = returnSlot;
    }

    public GesLinearExpressionInstruction[] Instructions { get; }

    public int MaxSlots { get; }

    public int MaxOperandCount { get; }

    public int ReturnSlot { get; }

    public static GesLinearExpressionProgram GetOrCreate(GameEventScriptBytecodeExpressionProgram program)
        => Cache.GetValue(program, Create);

    private static GesLinearExpressionProgram Create(GameEventScriptBytecodeExpressionProgram program)
    {
        var builder = new Builder();
        return builder.Build(program);
    }

    private sealed class Builder
    {
        private readonly List<GesLinearExpressionInstruction> _instructions = [];
        private int _nextSlot;
        private int _maxOperandCount;

        public GesLinearExpressionProgram Build(GameEventScriptBytecodeExpressionProgram program)
        {
            var stack = new Stack<int>();
            foreach (var instruction in program.Instructions)
            {
                EmitInstruction(instruction, stack);
            }

            var returnSlot = stack.Count > 0 ? stack.Peek() : -1;
            return new GesLinearExpressionProgram(_instructions.ToArray(), _nextSlot, _maxOperandCount, returnSlot);
        }

        private void EmitInstruction(GameEventScriptBytecodeStackInstruction instruction, Stack<int> stack)
        {
            switch (instruction.OpCode)
            {
                case GameEventScriptBytecodeOpCode.LoadConstant:
                    stack.Push(EmitValueInstruction(instruction, data: instruction.ConstantIndex));
                    return;

                case GameEventScriptBytecodeOpCode.LoadSlot:
                    stack.Push(EmitValueInstruction(instruction, a: instruction.A));
                    return;

                case GameEventScriptBytecodeOpCode.Cast:
                case GameEventScriptBytecodeOpCode.TypeCheck:
                case GameEventScriptBytecodeOpCode.MemberAccess:
                case GameEventScriptBytecodeOpCode.Unary:
                    if (stack.TryPop(out var unarySource))
                    {
                        stack.Push(EmitValueInstruction(instruction, a: unarySource, data: (int)instruction.CastKind, operands: [unarySource]));
                    }
                    return;

                case GameEventScriptBytecodeOpCode.ShortCircuitOr:
                case GameEventScriptBytecodeOpCode.ShortCircuitAnd:
                case GameEventScriptBytecodeOpCode.ShortCircuitImplies:
                    if (stack.TryPop(out var left))
                    {
                        stack.Push(EmitShortCircuitExpression(instruction, left));
                    }
                    return;

                case GameEventScriptBytecodeOpCode.PredicateTest:
                    if (stack.TryPop(out var input))
                    {
                        stack.Push(EmitValueInstruction(instruction, a: input, operands: [input]));
                    }
                    return;

                case GameEventScriptBytecodeOpCode.Dice:
                case GameEventScriptBytecodeOpCode.Pipeline:
                case GameEventScriptBytecodeOpCode.GeneratedCollection:
                case GameEventScriptBytecodeOpCode.GuardedChoice:
                    stack.Push(EmitValueInstruction(instruction, a: instruction.A, b: instruction.B));
                    return;

                default:
                    EmitDefaultInstruction(instruction, stack);
                    return;
            }
        }

        private void EmitDefaultInstruction(GameEventScriptBytecodeStackInstruction instruction, Stack<int> stack)
        {
            var operandCount = GetStackArgumentCount(instruction);
            var operands = new int[Math.Max(operandCount, 0)];
            for (var index = operands.Length - 1; index >= 0; index--)
            {
                operands[index] = stack.TryPop(out var operand) ? operand : -1;
            }

            var a = operands.Length > 0 ? operands[0] : instruction.A;
            var b = operands.Length > 1 ? operands[1] : instruction.B;
            var c = operands.Length > 2 ? operands[2] : -1;
            stack.Push(EmitValueInstruction(instruction, a, b, c, data: instruction.A, operands: operands));
        }

        private int EmitShortCircuitExpression(GameEventScriptBytecodeStackInstruction instruction, int left)
        {
            var result = Allocate();
            if (instruction.OpCode == GameEventScriptBytecodeOpCode.ShortCircuitImplies)
            {
                Emit(
                    new GameEventScriptBytecodeInstruction(
                        GameEventScriptBytecodeOpCode.ShortCircuitImplies,
                        Dest: result,
                        A: left,
                        B: left),
                    instruction,
                    [left, left]);
            }
            else
            {
                Emit(
                    new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.CopySlot, Dest: result, A: left),
                    instruction,
                    [left]);
            }

            var branch = instruction.OpCode switch
            {
                GameEventScriptBytecodeOpCode.ShortCircuitOr => GameEventScriptBytecodeOpCode.JumpIfTrue,
                GameEventScriptBytecodeOpCode.ShortCircuitAnd => GameEventScriptBytecodeOpCode.JumpIfFalse,
                _ => GameEventScriptBytecodeOpCode.JumpIfFalse
            };
            var jump = Emit(new GameEventScriptBytecodeInstruction(branch, A: left), instruction, [left]);
            if (instruction.ExpressionProgram is not null)
            {
                var stack = new Stack<int>();
                foreach (var rightInstruction in instruction.ExpressionProgram.Instructions)
                {
                    EmitInstruction(rightInstruction, stack);
                }

                if (stack.Count > 0)
                {
                    var right = stack.Peek();
                    var opCode = instruction.OpCode switch
                    {
                        GameEventScriptBytecodeOpCode.ShortCircuitOr => GameEventScriptBytecodeOpCode.Or,
                        GameEventScriptBytecodeOpCode.ShortCircuitAnd => GameEventScriptBytecodeOpCode.And,
                        _ => GameEventScriptBytecodeOpCode.ShortCircuitImplies
                    };
                    Emit(new GameEventScriptBytecodeInstruction(opCode, Dest: result, A: left, B: right), instruction, [left, right]);
                }
            }

            PatchTarget(jump, _instructions.Count);
            return result;
        }

        private int EmitValueInstruction(
            GameEventScriptBytecodeStackInstruction metadata,
            int a = -1,
            int b = -1,
            int c = -1,
            int data = -1,
            int[]? operands = null)
        {
            var dest = Allocate();
            Emit(new GameEventScriptBytecodeInstruction(metadata.OpCode, Dest: dest, A: a, B: b, C: c, Data: data), metadata, operands);
            return dest;
        }

        private int Emit(GameEventScriptBytecodeInstruction instruction, GameEventScriptBytecodeStackInstruction metadata, int[]? operands)
        {
            var address = _instructions.Count;
            var operandArray = operands ?? [];
            _maxOperandCount = Math.Max(_maxOperandCount, operandArray.Length);
            _instructions.Add(new GesLinearExpressionInstruction(instruction, metadata, operandArray));
            return address;
        }

        private void PatchTarget(int address, int target)
            => _instructions[address] = _instructions[address] with
            {
                Instruction = _instructions[address].Instruction with { Target = target }
            };

        private int Allocate()
            => _nextSlot++;

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
                GameEventScriptBytecodeOpCode.ShortCircuitImplies or
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
    }
}

internal readonly record struct GesLinearExpressionInstruction(
    GameEventScriptBytecodeInstruction Instruction,
    GameEventScriptBytecodeStackInstruction Metadata,
    int[] Operands);
