// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using static GameEventScript.Api.GameEventScriptBytecodeOpCode;
using static GameEventScript.Api.GameEventScriptOpcodePrinter.OperandPart;

namespace GameEventScript.Runtime.VM;

// Follow each internal producer until its register is overwritten. This avoids
// retaining a register-state matrix for every instruction in an untrusted file.
internal static class GesProgramInternalValueValidator
{
    internal static void Validate(GameEventScriptProgram program, ushort[] entries, ReadOnlySpan<int> frames)
    {
        int[]? visited = null;
        int[]? pending = null;
        var routine = -1;
        for (var origin = 0; origin < program.Code.Count; origin++)
        {
            if (routine + 1 < entries.Length && origin == entries[routine + 1]) routine++;
            var producer = program.Code[origin];
            var kind = InternalKind(producer.OpCode);
            if (kind == 0 || frames[origin] < 0) continue;
            visited ??= new int[program.Code.Count];
            pending ??= new int[program.Code.Count];
            var start = entries[routine];
            var end = routine + 1 < entries.Length ? entries[routine + 1] : program.Code.Count;
            var register = producer.DestinationRegister;
            var count = 0;
            Enqueue(origin + 1, start, end, origin + 1, visited, pending, ref count);
            // IteratorCreateOrJump writes Nothing on the jump edge, so only
            // its successful fallthrough can introduce an internal value.
            for (var cursor = 0; cursor < count; cursor++)
            {
                var address = pending[cursor];
                var instruction = program.Code[address];
                if (frames[address] <= register) continue;
                var operands = GameEventScriptOpcodePrinter.PrintInstruction(instruction);
                var overwritten = false;
                for (var position = 0; position < operands.Length; position++)
                {
                    var operand = operands[position];
                    if (operand == TargetRegister)
                    {
                        overwritten |= instruction.DestinationRegister == register;
                    }
                    else if (GameEventScriptProgramValidator.IsRegisterOperand(operand) && GameEventScriptProgramValidator.ReadRegisterOperand(instruction, operand, position) == register)
                    {
                        if (!Allows(instruction, operand, kind)) Invalid(address);
                    }
                    else if (operand is ArgumentRegisterList or ItemRegisterList or ValueRegisterList or CaptureRegisterList or TagRegisterList)
                    {
                        var list = program.UInt16IndexLists.Resolve(GameEventScriptProgramValidator.ReadListOperand(instruction, operand));
                        for (var element = 0; element < list.Length; element++)
                            if (list[element] == register) Invalid(address);
                    }
                }
                if (overwritten || instruction.OpCode == IteratorClose && instruction.XRegister == register || instruction.OpCode is ReturnVoid or ReturnValue) continue;
                if (instruction.OpCode is Jump or JumpIfTrue or JumpIfFalse or JumpIfNotTrue or JumpIfNothing or IteratorCreateOrJump or IteratorNext)
                    Enqueue(instruction.TargetAddress, start, end, origin + 1, visited, pending, ref count);
                if (instruction.OpCode != Jump) Enqueue(address + 1, start, end, origin + 1, visited, pending, ref count);
            }
        }
    }

    private static void Enqueue(int address, int start, int end, int stamp, int[] visited, int[] pending, ref int count)
    {
        if (address < start || address >= end || visited[address] == stamp) return;
        visited[address] = stamp;
        pending[count++] = address;
    }

    private static bool Allows(GameEventScriptBytecodeInstruction instruction, GameEventScriptOpcodePrinter.OperandPart operand, int kind)
    {
        var op = instruction.OpCode;
        if (operand == BuilderRegister)
        {
            return kind == (op switch
            {
                ListBuilderAdd or ListBuilderFinish => 2,
                MapBuilderAdd or MapBuilderFinish => 3,
                DistinctBuilderAdd or DistinctBuilderFinish => 4,
                GroupBuilderAdd or GroupBuilderFinish => 5,
                OrderBuilderAdd or OrderBuilderFinishAscending or OrderBuilderFinishDescending => 6,
                _ => 0
            });
        }
        if (kind != 1) return false;
        if (op is IteratorNext or IteratorClose) return operand == IteratorRegister;
        if (op is ContainsAny or ContainsAll) return operand is LeftRegister or RightRegister or NeedleRegister or CollectionRegister;
        return operand is SourceRegister or SourceIteratorRegister or OperandRegister or CollectionRegister && op is
            TakeFirst or DropFirst or TakeLast or DropLast or TakeHighest or TakeLowest or DropHighest or DropLowest or OneRandom or TakeRandom or
            Count or HasAny or HasAll or First or Last or GameEventScriptBytecodeOpCode.Single or Distinct or SortAscending or SortDescending or Reverse or Shuffle or HasPattern or TakePattern;
    }

    private static int InternalKind(GameEventScriptBytecodeOpCode op)
        => op switch
        {
            IteratorCreate or IteratorCreateOrJump or CreateRangeIterator or CreateRangeIteratorWithStep or CreateRangeIteratorShort => 1,
            ListBuilderCreate => 2,
            MapBuilderCreate => 3,
            DistinctBuilderCreate => 4,
            GroupBuilderCreate => 5,
            OrderBuilderCreate => 6,
            _ => 0
        };

    private static void Invalid(int index)
        => throw new GameEventScriptProgramFormatException(GameEventScriptProgramFormatErrorCode.InvalidOperand,
            "Internal iterator or builder value escapes its permitted operand role.", sectionType: (ushort)GameEventScriptSectionType.Code, entryIndex: index);
}
