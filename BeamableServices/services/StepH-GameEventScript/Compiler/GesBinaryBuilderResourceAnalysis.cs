// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.Compiler;

internal sealed partial class GesBinaryBuilder
{
    private static GameEventScriptCompileException CompileFailure(string code, string message, string? symbol = null)
        => new(new GameEventScriptDiagnostic(
            GameEventScriptDiagnosticPhase.Compile,
            code,
            message,
            symbol));

    private ProgramResourceAnalysis AnalyzeProgramResources(IReadOnlyList<PlanItem> items, RegisterAllocationResult registerAllocation)
    {
        if (_routines.Count == 0)
        {
            return new ProgramResourceAnalysis([], [], 0, 0);
        }

        var routineByEntryLabel = new int[_labels.Count];
        Array.Fill(routineByEntryLabel, NoRoutineId);
        var recordRoutineByName = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var routineIndex = 0; routineIndex < _routines.Count; routineIndex++)
        {
            var routine = _routines[routineIndex];
            routineByEntryLabel[routine.EntryLabel.Id] = routine.Id;
            if (routine.Kind == GameEventScriptBinaryBindKind.Record)
            {
                recordRoutineByName.Add(routine.Name, routine.Id);
            }
        }

        var calls = new List<int>[_routines.Count];
        var maximumStageCounts = new int[_routines.Count];
        var currentStageCounts = new int[_routines.Count];
        for (var index = 0; index < calls.Length; index++) calls[index] = [];

        for (var itemIndex = 0; itemIndex < items.Count; itemIndex++)
        {
            if (items[itemIndex].Instruction is not { } instruction || instruction.RoutineId == NoRoutineId)
            {
                continue;
            }

            var routineId = instruction.RoutineId;
            if (IsStageOpcode(instruction.OpCode))
            {
                var stageCount = ++currentStageCounts[routineId];
                if (stageCount > maximumStageCounts[routineId]) maximumStageCounts[routineId] = stageCount;
            }

            switch (instruction.OpCode)
            {
                case GameEventScriptBytecodeOpCode.Call:
                    AddCallByEntryLabel(calls[routineId], instruction.Y, routineByEntryLabel, _routines[routineId].Name);
                    currentStageCounts[routineId] = 0;
                    break;
                case GameEventScriptBytecodeOpCode.CreateRecord:
                    AddRecordConstructorCall(calls[routineId], instruction.X, routineByEntryLabel, _routines[routineId].Name);
                    currentStageCounts[routineId] = 0;
                    break;
                case GameEventScriptBytecodeOpCode.CastCustom:
                    if (instruction.Y.Kind == GesOperandKind.Text &&
                        instruction.Y.TextValue is { } typeName &&
                        recordRoutineByName.TryGetValue(typeName, out var recordRoutineId))
                    {
                        AddUniqueCall(calls[routineId], recordRoutineId);
                    }

                    break;
                default:
                    if (ClearsStage(instruction.OpCode)) currentStageCounts[routineId] = 0;
                    break;
            }
        }

        var visitStates = new byte[_routines.Count];
        var path = new List<int>();
        for (var routineId = 0; routineId < _routines.Count; routineId++)
        {
            RejectCyclicCalls(routineId, calls, visitStates, path);
        }

        var calculated = new bool[_routines.Count];
        var routineRequirements = new RoutineResourceRequirement[_routines.Count];
        for (var routineId = 0; routineId < _routines.Count; routineId++)
        {
            CalculateRoutineRequirement(
                routineId,
                calls,
                maximumStageCounts,
                registerAllocation,
                calculated,
                routineRequirements);
        }

        var bindRequirements = new RoutineResourceRequirement[_binds.Count];
        var requiredRegisterCount = 0;
        var requiredCallStackDepth = 0;
        for (var routineId = 0; routineId < _routines.Count; routineId++)
        {
            var routine = _routines[routineId];
            if (routine.Kind is not (GameEventScriptBinaryBindKind.MessageHandler or GameEventScriptBinaryBindKind.MessageNameHandler) ||
                routine.Bind is not { } bind)
            {
                continue;
            }

            var requirement = routineRequirements[routineId];
            bindRequirements[bind.Id] = requirement;
            requiredRegisterCount = Math.Max(requiredRegisterCount, requirement.RequiredRegisterCount);
            requiredCallStackDepth = Math.Max(requiredCallStackDepth, requirement.RequiredCallStackDepth);
        }

        return new ProgramResourceAnalysis(
            routineRequirements,
            bindRequirements,
            ToResourceUShort(requiredRegisterCount, "program register requirement"),
            ToResourceUShort(requiredCallStackDepth, "program call-stack requirement"));
    }

    private void AddCallByEntryLabel(List<int> calls, GesOperand operand, int[] routineByEntryLabel, string callerName)
    {
        if (operand.Kind != GesOperandKind.Label ||
            operand.LabelRef.Id < 0 ||
            operand.LabelRef.Id >= routineByEntryLabel.Length ||
            routineByEntryLabel[operand.LabelRef.Id] == NoRoutineId)
        {
            throw CompileFailure(GameEventScriptDiagnosticCodes.CompileInvalidResourceMetadata,
                $"Routine '{callerName}' calls an address that is not a routine entry point.");
        }

        AddUniqueCall(calls, routineByEntryLabel[operand.LabelRef.Id]);
    }

    private void AddRecordConstructorCall(List<int> calls, GesOperand operand, int[] routineByEntryLabel, string callerName)
    {
        if (operand.Kind != GesOperandKind.Bind ||
            operand.BindRef.Id < 0 ||
            operand.BindRef.Id >= _binds.Count ||
            _binds[operand.BindRef.Id] is not { Kind: GameEventScriptBinaryBindKind.Record, EntryLabel: { } entryLabel } ||
            routineByEntryLabel[entryLabel.Id] == NoRoutineId)
        {
            throw CompileFailure(GameEventScriptDiagnosticCodes.CompileInvalidResourceMetadata,
                $"Routine '{callerName}' references an invalid record-constructor call target.");
        }

        AddUniqueCall(calls, routineByEntryLabel[entryLabel.Id]);
    }

    private static void AddUniqueCall(List<int> calls, int calleeRoutineId)
    {
        for (var index = 0; index < calls.Count; index++)
        {
            if (calls[index] == calleeRoutineId) return;
        }

        calls.Add(calleeRoutineId);
    }

    private void RejectCyclicCalls(int routineId, IReadOnlyList<List<int>> calls, byte[] visitStates, List<int> path)
    {
        if (visitStates[routineId] == 2) return;
        if (visitStates[routineId] == 1)
        {
            var cycleStart = 0;
            while (cycleStart < path.Count && path[cycleStart] != routineId) cycleStart++;
            var names = new string[path.Count - cycleStart + 1];
            for (var index = cycleStart; index < path.Count; index++)
            {
                names[index - cycleStart] = _routines[path[index]].Name;
            }

            names[^1] = _routines[routineId].Name;
            throw CompileFailure(GameEventScriptDiagnosticCodes.CompileCyclicCallGraph,
                "Recursive calls are not allowed. Cyclic call path: " + string.Join(" -> ", names) + ".");
        }

        visitStates[routineId] = 1;
        path.Add(routineId);
        var routineCalls = calls[routineId];
        for (var index = 0; index < routineCalls.Count; index++)
        {
            RejectCyclicCalls(routineCalls[index], calls, visitStates, path);
        }

        path.RemoveAt(path.Count - 1);
        visitStates[routineId] = 2;
    }

    private RoutineResourceRequirement CalculateRoutineRequirement(int routineId, IReadOnlyList<List<int>> calls, int[] maximumStageCounts, RegisterAllocationResult registerAllocation, bool[] calculated, RoutineResourceRequirement[] requirements)
    {
        if (calculated[routineId]) return requirements[routineId];

        var routine = _routines[routineId];
        var frameRegisterCount = routine.ArgumentRegisters.Count + registerAllocation.RoutineLocalCounts[routineId];
        var requiredRegisterCount = frameRegisterCount + maximumStageCounts[routineId];
        var requiredCallStackDepth = 0;
        var routineCalls = calls[routineId];
        for (var index = 0; index < routineCalls.Count; index++)
        {
            var callee = CalculateRoutineRequirement(
                routineCalls[index],
                calls,
                maximumStageCounts,
                registerAllocation,
                calculated,
                requirements);
            requiredRegisterCount = Math.Max(requiredRegisterCount, frameRegisterCount + callee.RequiredRegisterCount);
            requiredCallStackDepth = Math.Max(requiredCallStackDepth, 1 + callee.RequiredCallStackDepth);
        }

        var result = new RoutineResourceRequirement(
            ToResourceUShort(requiredRegisterCount, $"register requirement of routine '{routine.Name}'"),
            ToResourceUShort(requiredCallStackDepth, $"call-stack requirement of routine '{routine.Name}'"));
        requirements[routineId] = result;
        calculated[routineId] = true;
        return result;
    }

    private static ushort ToResourceUShort(int value, string name)
        => value is < 0 or > ushort.MaxValue
            ? throw CompileFailure(GameEventScriptDiagnosticCodes.CompileInvalidResourceMetadata,
                $"The {name} is {value}, but portable GameEventScript resource metadata is limited to {ushort.MaxValue}.")
            : checked((ushort)value);

    private static bool IsStageOpcode(GameEventScriptBytecodeOpCode opcode)
        => opcode is GameEventScriptBytecodeOpCode.StageRegister or
            GameEventScriptBytecodeOpCode.StageNothing or
            GameEventScriptBytecodeOpCode.StageTrue or
            GameEventScriptBytecodeOpCode.StageFalse or
            GameEventScriptBytecodeOpCode.StageInteger or
            GameEventScriptBytecodeOpCode.StageFloat or
            GameEventScriptBytecodeOpCode.StageText or
            GameEventScriptBytecodeOpCode.StageTag or
            GameEventScriptBytecodeOpCode.StagePercentage;

    private static bool ClearsStage(GameEventScriptBytecodeOpCode opcode)
        => opcode is GameEventScriptBytecodeOpCode.CreateVector or
            GameEventScriptBytecodeOpCode.CreatePoint or
            GameEventScriptBytecodeOpCode.CreateList or
            GameEventScriptBytecodeOpCode.CreateMap or
            GameEventScriptBytecodeOpCode.CreateExternalType or
            GameEventScriptBytecodeOpCode.ReturnVoid or
            GameEventScriptBytecodeOpCode.ReturnValue;

    private sealed record ProgramResourceAnalysis(IReadOnlyList<RoutineResourceRequirement> RoutineRequirements, IReadOnlyList<RoutineResourceRequirement> BindRequirements, ushort RequiredRegisterCount, ushort RequiredCallStackDepth);

    private readonly record struct RoutineResourceRequirement(ushort RequiredRegisterCount, ushort RequiredCallStackDepth);
}
