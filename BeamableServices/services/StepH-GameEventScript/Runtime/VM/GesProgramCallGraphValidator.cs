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
                throw new GameEventScriptDynamicLinkException(
                    $"Executable bind id '{bind.Id}' has invalid entry address '{bind.EntryAddress}'.");
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
                    throw new GameEventScriptDynamicLinkException(
                        $"Call target address '{instruction.TargetAddress}' is outside the instruction table.");
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
                            throw new GameEventScriptDynamicLinkException(
                                $"Record-constructor bind id '{instruction.BindId}' was not found.");
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
        var path = new List<int>();
        for (var routineIndex = 0; routineIndex < entries.Length; routineIndex++)
        {
            RejectCycle(routineIndex, calls, states, path, entries, entryNames);
        }
    }

    private static bool IsExecutableBind(GameEventScriptBinaryBindKind kind)
        => kind is MessageHandler or MessageNameHandler or Function or Predicate or Record;

    private static void AddCall(List<int> calls, ushort targetAddress, IReadOnlyDictionary<ushort, int> entryIndexes)
    {
        if (!entryIndexes.TryGetValue(targetAddress, out var targetIndex))
        {
            throw new GameEventScriptDynamicLinkException(
                $"Call target address '{targetAddress}' is not a routine entry point.");
        }

        for (var index = 0; index < calls.Count; index++)
        {
            if (calls[index] == targetIndex) return;
        }

        calls.Add(targetIndex);
    }

    private static void RejectCycle(
        int routineIndex,
        IReadOnlyList<List<int>> calls,
        byte[] states,
        List<int> path,
        ushort[] entries,
        IReadOnlyDictionary<ushort, string> entryNames)
    {
        if (states[routineIndex] == 2) return;
        if (states[routineIndex] == 1)
        {
            var cycleStart = 0;
            while (cycleStart < path.Count && path[cycleStart] != routineIndex) cycleStart++;
            var names = new string[path.Count - cycleStart + 1];
            for (var index = cycleStart; index < path.Count; index++)
            {
                names[index - cycleStart] = ResolveName(path[index], entries, entryNames);
            }

            names[^1] = ResolveName(routineIndex, entries, entryNames);
            throw new GameEventScriptDynamicLinkException(
                "Recursive calls are not allowed. Cyclic call path: " + string.Join(" -> ", names) + ".");
        }

        states[routineIndex] = 1;
        path.Add(routineIndex);
        var routineCalls = calls[routineIndex];
        for (var index = 0; index < routineCalls.Count; index++)
        {
            RejectCycle(routineCalls[index], calls, states, path, entries, entryNames);
        }

        path.RemoveAt(path.Count - 1);
        states[routineIndex] = 2;
    }

    private static string ResolveName(
        int routineIndex,
        IReadOnlyList<ushort> entries,
        IReadOnlyDictionary<ushort, string> entryNames)
    {
        var entry = entries[routineIndex];
        return entryNames.TryGetValue(entry, out var name) ? name : "@" + entry;
    }
}
