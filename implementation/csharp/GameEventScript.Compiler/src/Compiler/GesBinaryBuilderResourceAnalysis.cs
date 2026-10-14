// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Api;

namespace GameEventScript.Compiler;

internal sealed partial class GesBinaryBuilder
{
    private static GesCompileException CompileFailure(string code, string message, string? symbol = null)
        => new(new GesDiagnostic(
            GesDiagnosticPhase.Compile,
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
            if (routine.Kind == GesBinaryBindKind.Record)
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
                case GesBytecodeOpCode.Call:
                    AddCallByEntryLabel(calls[routineId], instruction.Y, routineByEntryLabel, _routines[routineId].Name);
                    currentStageCounts[routineId] = 0;
                    break;
                case GesBytecodeOpCode.CreateRecord:
                    AddRecordConstructorCall(calls[routineId], instruction.X, routineByEntryLabel, _routines[routineId].Name);
                    currentStageCounts[routineId] = 0;
                    break;
                case GesBytecodeOpCode.CastCustom:
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

        var routineRequirements = new RoutineResourceRequirement[_routines.Count];
        foreach (var routineId in OrderRoutineDependencies(calls))
        {
            routineRequirements[routineId] = CalculateRoutineRequirement(routineId, calls, maximumStageCounts, registerAllocation, routineRequirements);
        }

        var bindRequirements = new RoutineResourceRequirement[_binds.Count];
        var requiredRegisterCount = 0;
        var requiredCallStackDepth = 0;
        for (var routineId = 0; routineId < _routines.Count; routineId++)
        {
            var routine = _routines[routineId];
            if (routine.Kind is not (GesBinaryBindKind.MessageHandler or GesBinaryBindKind.MessageNameHandler) ||
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
            throw CompileFailure(GesDiagnosticCodes.CompileInvalidResourceMetadata,
                $"Routine '{callerName}' calls an address that is not a routine entry point.");
        }

        AddUniqueCall(calls, routineByEntryLabel[operand.LabelRef.Id]);
    }

    private void AddRecordConstructorCall(List<int> calls, GesOperand operand, int[] routineByEntryLabel, string callerName)
    {
        if (operand.Kind != GesOperandKind.Bind ||
            operand.BindRef.Id < 0 ||
            operand.BindRef.Id >= _binds.Count ||
            _binds[operand.BindRef.Id] is not { Kind: GesBinaryBindKind.Record, EntryLabel: { } entryLabel } ||
            routineByEntryLabel[entryLabel.Id] == NoRoutineId)
        {
            throw CompileFailure(GesDiagnosticCodes.CompileInvalidResourceMetadata,
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

    private List<int> OrderRoutineDependencies(IReadOnlyList<List<int>> calls)
    {
        var visitStates = new byte[_routines.Count];
        var ordered = new List<int>(_routines.Count);
        var pending = new List<(int RoutineId, int NextCall)>();
        for (var root = 0; root < _routines.Count; root++)
        {
            if (visitStates[root] != 0) continue;
            visitStates[root] = 1;
            pending.Add((root, 0));
            while (pending.Count > 0)
            {
                var (routineId, nextCall) = pending[^1];
                if (nextCall == calls[routineId].Count)
                {
                    pending.RemoveAt(pending.Count - 1);
                    visitStates[routineId] = 2;
                    ordered.Add(routineId);
                    continue;
                }

                pending[^1] = (routineId, nextCall + 1);
                var callee = calls[routineId][nextCall];
                if (visitStates[callee] == 2) continue;
                if (visitStates[callee] == 1)
                {
                    var cycleStart = 0;
                    while (pending[cycleStart].RoutineId != callee) cycleStart++;
                    var names = new string[pending.Count - cycleStart + 1];
                    for (var index = cycleStart; index < pending.Count; index++) names[index - cycleStart] = _routines[pending[index].RoutineId].Name;
                    names[^1] = _routines[callee].Name;
                    throw CompileFailure(GesDiagnosticCodes.CompileCyclicCallGraph,
                        "Recursive calls are not allowed. Cyclic call path: " + string.Join(" -> ", names) + ".");
                }

                visitStates[callee] = 1;
                pending.Add((callee, 0));
            }
        }

        return ordered;
    }

    private RoutineResourceRequirement CalculateRoutineRequirement(int routineId, IReadOnlyList<List<int>> calls, int[] maximumStageCounts, RegisterAllocationResult registerAllocation, RoutineResourceRequirement[] requirements)
    {
        var routine = _routines[routineId];
        var frameRegisterCount = routine.ArgumentRegisters.Count + registerAllocation.RoutineLocalCounts[routineId];
        var requiredRegisterCount = frameRegisterCount + maximumStageCounts[routineId];
        var requiredCallStackDepth = 0;
        var routineCalls = calls[routineId];
        for (var index = 0; index < routineCalls.Count; index++)
        {
            var callee = requirements[routineCalls[index]];
            requiredRegisterCount = Math.Max(requiredRegisterCount, frameRegisterCount + callee.RequiredRegisterCount);
            requiredCallStackDepth = Math.Max(requiredCallStackDepth, 1 + callee.RequiredCallStackDepth);
        }

        var result = new RoutineResourceRequirement(
            ToResourceUShort(requiredRegisterCount, $"register requirement of routine '{routine.Name}'"),
            ToResourceUShort(requiredCallStackDepth, $"call-stack requirement of routine '{routine.Name}'"));
        return result;
    }

    private static ushort ToResourceUShort(int value, string name)
        => value is < 0 or > ushort.MaxValue
            ? throw CompileFailure(GesDiagnosticCodes.CompileInvalidResourceMetadata,
                $"The {name} is {value}, but portable GameEventScript resource metadata is limited to {ushort.MaxValue}.")
            : checked((ushort)value);

    private static bool IsStageOpcode(GesBytecodeOpCode opcode)
        => opcode is GesBytecodeOpCode.StageRegister or
            GesBytecodeOpCode.StageNothing or
            GesBytecodeOpCode.StageTrue or
            GesBytecodeOpCode.StageFalse or
            GesBytecodeOpCode.StageInteger or
            GesBytecodeOpCode.StageFloat or
            GesBytecodeOpCode.StageText or
            GesBytecodeOpCode.StageTag or
            GesBytecodeOpCode.StagePercentage;

    private static bool ClearsStage(GesBytecodeOpCode opcode)
        => opcode is GesBytecodeOpCode.CreateVector or
            GesBytecodeOpCode.CreatePoint or
            GesBytecodeOpCode.CreateList or
            GesBytecodeOpCode.CreateMap or
            GesBytecodeOpCode.CreateExternalType or
            GesBytecodeOpCode.ReturnVoid or
            GesBytecodeOpCode.ReturnValue;

    private sealed record ProgramResourceAnalysis(IReadOnlyList<RoutineResourceRequirement> RoutineRequirements, IReadOnlyList<RoutineResourceRequirement> BindRequirements, ushort RequiredRegisterCount, ushort RequiredCallStackDepth);

    private readonly record struct RoutineResourceRequirement(ushort RequiredRegisterCount, ushort RequiredCallStackDepth);
}
