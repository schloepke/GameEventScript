// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptBinaryBindKind;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeOpCode;

namespace StepH.GameEventScript.Runtime.VM;

internal readonly struct GesProgramResourceValidator
{
    private readonly GameEventScriptProgram _program;
    private readonly ushort[] _entries;
    private readonly IReadOnlyDictionary<ushort, int> _entryIndexes;
    private readonly IReadOnlyDictionary<ushort, ushort> _recordEntriesById;
    private readonly IReadOnlyDictionary<string, ushort> _recordEntriesByName;
    private readonly int[] _workspace;

    private Span<int> Arguments => _workspace.AsSpan(0, _entries.Length);
    private Span<int> Registers => _workspace.AsSpan(_entries.Length, _entries.Length);
    private Span<int> Depths => _workspace.AsSpan(2 * _entries.Length, _entries.Length);
    private Span<int> MaximumFrames => _workspace.AsSpan(3 * _entries.Length, _entries.Length);
    private Span<int> Frames => _workspace.AsSpan(4 * _entries.Length, _program.Code.Count);
    private Span<int> Stages => _workspace.AsSpan(4 * _entries.Length + _program.Code.Count, _program.Code.Count);
    private Span<int> Pending => _workspace.AsSpan(4 * _entries.Length + 2 * _program.Code.Count, _program.Code.Count);

    internal GesProgramResourceValidator(
        GameEventScriptProgram program,
        ushort[] entries,
        IReadOnlyDictionary<ushort, int> entryIndexes,
        IReadOnlyDictionary<ushort, ushort> recordEntriesById,
        IReadOnlyDictionary<string, ushort> recordEntriesByName
    )
    {
        _program = program;
        _entries = entries;
        _entryIndexes = entryIndexes;
        _recordEntriesById = recordEntriesById;
        _recordEntriesByName = recordEntriesByName;
        // Keep all bounded analysis scratch in one allocation; nothing is retained by the Program.
        _workspace = new int[4 * entries.Length + 3 * program.Code.Count];
        Arguments.Fill(-1);
        Frames.Fill(-1);
    }

    internal void Validate(int[] completed)
    {
        foreach (var bind in _program.Bindings.Entries)
        {
            if (bind.Kind is not (MessageHandler or MessageNameHandler or Function or Predicate or Record)) continue;
            var routine = _entryIndexes[bind.EntryAddress];
            var count = bind.Kind == MessageNameHandler ? 1 : bind.ArgumentNames.Count;
            if (Arguments[routine] >= 0 && Arguments[routine] != count) Invalid("Routine bindings disagree on the initial frame length.", bind.EntryAddress);
            Arguments[routine] = count;
        }

        var staged = 0;
        for (var index = 0; index < _program.Code.Count; index++)
        {
            var instruction = _program.Code[index];
            if (IsStage(instruction.OpCode)) { staged++; continue; }
            if (instruction.OpCode == Call)
            {
                var target = _entryIndexes[instruction.TargetAddress];
                if (Arguments[target] >= 0 && Arguments[target] != staged) Invalid("Call stages a different number of arguments than its target frame requires.", index);
                Arguments[target] = staged;
            }
            staged = 0;
        }

        foreach (var routine in completed) Analyze(routine);
        ValidateInactiveCodeAndDebugSymbols();
        for (var index = 0; index < _program.Bindings.Entries.Count; index++)
        {
            var bind = _program.Bindings.Entries[index];
            if (bind.Kind is not (MessageHandler or MessageNameHandler)) continue;
            var routine = _entryIndexes[bind.EntryAddress];
            if (bind.RequiredRegisterCount < Registers[routine] || bind.RequiredCallStackDepth < Depths[routine])
            {
                throw new GameEventScriptProgramFormatException(GameEventScriptProgramFormatErrorCode.InvalidResourceMetadata,
                    $"Handler declares {bind.RequiredRegisterCount} registers and call depth {bind.RequiredCallStackDepth}, but its code requires {Registers[routine]} registers and call depth {Depths[routine]}.",
                    sectionType: (ushort)GameEventScriptSectionType.Bindings, entryIndex: index);
            }
        }
    }

    private void ValidateInactiveCodeAndDebugSymbols()
    {
        // Even retained, unreachable code must reference a register in its own routine.
        var routine = -1;
        for (var index = 0; index < _program.Code.Count; index++)
        {
            if (routine + 1 < _entries.Length && index == _entries[routine + 1]) routine++;
            if (Frames[index] >= 0) continue;
            GameEventScriptProgramValidator.ValidateInstructionFrame(_program, _program.Code[index], index, routine < 0 ? 0 : MaximumFrames[routine]);
        }

        if (_program.DebugSymbols is not { } debug) return;
        for (var index = 0; index < debug.Symbols.Count; index++)
        {
            var symbol = debug.Symbols[index];
            routine = Array.BinarySearch(_entries, checked((ushort)symbol.CodeStart));
            if (routine < 0) routine = ~routine - 1;
            var end = routine + 1 < _entries.Length ? _entries[routine + 1] : _program.Code.Count;
            if (routine < 0 || (ulong)symbol.CodeStart + symbol.CodeLength > (ulong)end || symbol.RegisterId >= MaximumFrames[routine])
            {
                throw new GameEventScriptProgramFormatException(GameEventScriptProgramFormatErrorCode.InvalidDebugSymbol,
                    "Debug symbol is outside its routine frame or code range.", sectionType: (ushort)GameEventScriptSectionType.DebugSymbols, entryIndex: index);
            }
        }
    }

    private void Analyze(int routine)
    {
        var start = _entries[routine];
        var end = routine + 1 < _entries.Length ? _entries[routine + 1] : _program.Code.Count;
        var count = 0;
        Registers[routine] = Math.Max(0, Arguments[routine]);
        MaximumFrames[routine] = Registers[routine];
        Enqueue(start, Registers[routine], 0, start, end, ref count);
        for (var cursor = 0; cursor < count; cursor++)
        {
            var index = Pending[cursor];
            var instruction = _program.Code[index];
            var opcode = instruction.OpCode;
            var frame = Frames[index];
            var stage = Stages[index];
            if (stage > 0 && !IsStage(opcode) && !ConsumesStage(opcode)) Invalid("A staged argument sequence must be contiguous and end in a stage consumer.", index);
            if (opcode == RegisterLocals)
            {
                frame += instruction.Count;
                if (frame is < 0 or > ushort.MaxValue) Invalid("Local register adjustment exceeds the frame bounds.", index);
            }
            MaximumFrames[routine] = Math.Max(MaximumFrames[routine], frame);
            GameEventScriptProgramValidator.ValidateInstructionFrame(_program, instruction, index, frame);
            if (IsStage(opcode)) stage++;
            Registers[routine] = Math.Max(Registers[routine], frame + stage);

            int? callee = opcode switch
            {
                Call => _entryIndexes[instruction.TargetAddress],
                CreateRecord => _entryIndexes[_recordEntriesById[instruction.BindId]],
                CastCustom when _recordEntriesByName.TryGetValue(_program.StringConstants.Resolve(instruction.SecondaryStringIndex), out var address) => _entryIndexes[address],
                _ => null
            };
            if (callee is { } target)
            {
                if (opcode is Call or CreateRecord && stage != Arguments[target]) Invalid("Staged arguments disagree with the callee's initial frame length.", index);
                Registers[routine] = Math.Max(Registers[routine], frame + Registers[target]);
                Depths[routine] = Math.Max(Depths[routine], 1 + Depths[target]);
            }
            if (Registers[routine] > ushort.MaxValue || Depths[routine] > ushort.MaxValue) Invalid("Routine resource requirements exceed the V1 limits.", index);
            if (ConsumesStage(opcode)) stage = 0;
            if (opcode is ReturnVoid or ReturnValue) continue;
            if (opcode is Jump or JumpIfTrue or JumpIfFalse or JumpIfNotTrue or JumpIfNothing or IteratorCreateOrJump or IteratorNext)
                Enqueue(instruction.TargetAddress, frame, stage, start, end, ref count);
            if (opcode != Jump) Enqueue(index + 1, frame, stage, start, end, ref count);
        }
    }

    private void Enqueue(int address, int frame, int stage, int start, int end, ref int count)
    {
        if (address < start || address >= end) Invalid("Control flow leaves its routine without a call or return.", address);
        if (Frames[address] >= 0)
        {
            if (Frames[address] != frame || Stages[address] != stage) Invalid("Control-flow paths disagree on frame or stage length.", address);
            return;
        }
        Frames[address] = frame;
        Stages[address] = stage;
        Pending[count++] = address;
    }

    private static bool IsStage(GameEventScriptBytecodeOpCode opcode)
        => opcode is StageRegister or StageNothing or StageTrue or StageFalse or StageInteger or StageFloat or StageText or StageTag or StagePercentage;

    private static bool ConsumesStage(GameEventScriptBytecodeOpCode opcode)
        => opcode is Call or CreateVector or CreatePoint or CreateList or CreateMap or CreateRecord or CreateExternalType;

    private static void Invalid(string message, int instructionIndex)
        => throw new GameEventScriptProgramFormatException(GameEventScriptProgramFormatErrorCode.InvalidResourceMetadata, message, sectionType: (ushort)GameEventScriptSectionType.Code, entryIndex: instructionIndex);
}
