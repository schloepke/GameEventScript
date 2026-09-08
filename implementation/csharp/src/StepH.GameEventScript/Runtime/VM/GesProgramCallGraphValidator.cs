// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptBinaryBindKind;

namespace StepH.GameEventScript.Runtime.VM;

internal static class GesProgramCallGraphValidator
{
    internal static void Validate(GameEventScriptProgram program)
    {
        if (program.Code.Count == 0) return;

        var entryAddresses = new HashSet<ushort>();
        var entryNames = new Dictionary<ushort, string>();
        var recordEntriesById = new Dictionary<ushort, ushort>();
        var recordEntriesByName = new Dictionary<string, ushort>(StringComparer.Ordinal);
        foreach (var bind in program.Bindings.Entries)
        {
            if (!IsExecutableBind(bind.Kind)) continue;
            if (bind.EntryAddress >= program.Code.Count)
            {
                throw LinkError(GameEventScriptDiagnosticCodes.LinkInvalidProgram,
                    $"Executable bind id '{bind.Id}' has invalid entry address '{bind.EntryAddress}'.", bind.Id.ToString());
            }

            entryAddresses.Add(bind.EntryAddress);
            var name = program.StringConstants.Resolve(bind.Name);
            entryNames[bind.EntryAddress] = name;
            if (bind.Kind == Record)
            {
                recordEntriesById[bind.Id] = bind.EntryAddress;
                recordEntriesByName[name] = bind.EntryAddress;
            }
        }

        for (var instructionIndex = 0; instructionIndex < program.Code.Count; instructionIndex++)
        {
            var instruction = program.Code[instructionIndex];
            if (instruction.OpCode == GameEventScriptBytecodeOpCode.Call)
            {
                if (instruction.TargetAddress >= program.Code.Count)
                {
                    throw LinkError(GameEventScriptDiagnosticCodes.LinkInvalidProgram,
                        $"Call target address '{instruction.TargetAddress}' is outside the instruction table.", instruction.TargetAddress.ToString());
                }

                entryAddresses.Add(instruction.TargetAddress);
            }
        }

        if (entryAddresses.Count == 0) return;
        var entries = new ushort[entryAddresses.Count];
        entryAddresses.CopyTo(entries);
        Array.Sort(entries);
        var entryIndexes = new Dictionary<ushort, int>();
        for (var index = 0; index < entries.Length; index++) entryIndexes.Add(entries[index], index);

        var calls = new List<int>[entries.Length];
        for (var index = 0; index < calls.Length; index++) calls[index] = [];
        for (var routineIndex = 0; routineIndex < entries.Length; routineIndex++)
        {
            var start = entries[routineIndex];
            var end = routineIndex + 1 < entries.Length ? entries[routineIndex + 1] : program.Code.Count;
            for (var instructionIndex = start; instructionIndex < end; instructionIndex++)
            {
                var instruction = program.Code[instructionIndex];
                switch (instruction.OpCode)
                {
                    case GameEventScriptBytecodeOpCode.Call:
                        AddCall(calls[routineIndex], instruction.TargetAddress, entryIndexes);
                        break;
                    case GameEventScriptBytecodeOpCode.CreateRecord:
                        if (!recordEntriesById.TryGetValue(instruction.BindId, out var recordEntry))
                        {
                            throw LinkError(GameEventScriptDiagnosticCodes.LinkInvalidProgram,
                                $"Record-constructor bind id '{instruction.BindId}' was not found.", instruction.BindId.ToString());
                        }

                        AddCall(calls[routineIndex], recordEntry, entryIndexes);
                        break;
                    case GameEventScriptBytecodeOpCode.CastCustom:
                        var typeName = program.StringConstants.Resolve(instruction.SecondaryStringIndex);
                        if (recordEntriesByName.TryGetValue(typeName, out var castRecordEntry))
                        {
                            AddCall(calls[routineIndex], castRecordEntry, entryIndexes);
                        }

                        break;
                }
            }
        }

        var states = new byte[entries.Length];
        var path = new int[entries.Length];
        var nextCalls = new int[entries.Length];
        for (var routineIndex = 0; routineIndex < entries.Length; routineIndex++)
        {
            RejectCycle(routineIndex, calls, states, path, nextCalls, entries, entryNames);
        }
    }

    private static bool IsExecutableBind(GameEventScriptBinaryBindKind kind)
        => kind is MessageHandler or MessageNameHandler or Function or Predicate or Record;

    private static void AddCall(List<int> calls, ushort targetAddress, IReadOnlyDictionary<ushort, int> entryIndexes)
    {
        if (!entryIndexes.TryGetValue(targetAddress, out var targetIndex))
        {
            throw LinkError(GameEventScriptDiagnosticCodes.LinkInvalidProgram,
                $"Call target address '{targetAddress}' is not a routine entry point.", targetAddress.ToString());
        }

        for (var index = 0; index < calls.Count; index++)
        {
            if (calls[index] == targetIndex) return;
        }

        calls.Add(targetIndex);
    }

    private static void RejectCycle(int routineIndex, IReadOnlyList<List<int>> calls, byte[] states, int[] path, int[] nextCalls, ushort[] entries, IReadOnlyDictionary<ushort, string> entryNames)
    {
        if (states[routineIndex] == 2) return;
        var depth = 1;
        path[0] = routineIndex;
        states[routineIndex] = 1;
        while (depth > 0)
        {
            var current = path[depth - 1];
            if (nextCalls[current] == calls[current].Count)
            {
                states[current] = 2;
                depth--;
                continue;
            }

            var target = calls[current][nextCalls[current]++];
            if (states[target] == 2) continue;
            if (states[target] == 1)
            {
                var cycleStart = 0;
                while (path[cycleStart] != target) cycleStart++;
                var names = new string[depth - cycleStart + 1];
                for (var index = cycleStart; index < depth; index++)
                {
                    names[index - cycleStart] = ResolveName(path[index], entries, entryNames);
                }

                names[^1] = ResolveName(target, entries, entryNames);
                throw LinkError(GameEventScriptDiagnosticCodes.LinkCyclicCallGraph,
                    "Recursive calls are not allowed. Cyclic call path: " + string.Join(" -> ", names) + ".",
                    names[0]);
            }

            states[target] = 1;
            path[depth++] = target;
        }
    }

    private static string ResolveName(int routineIndex, IReadOnlyList<ushort> entries, IReadOnlyDictionary<ushort, string> entryNames)
    {
        var entry = entries[routineIndex];
        return entryNames.TryGetValue(entry, out var name) ? name : "@" + entry;
    }

    private static GameEventScriptDynamicLinkException LinkError(string code, string message, string? symbol = null)
        => new(new GameEventScriptDiagnostic(GameEventScriptDiagnosticPhase.Link, code, message, symbol));
}
