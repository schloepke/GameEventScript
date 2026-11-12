// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace GameEventScript.Api;

// Register lists shared by many instructions are summarized once per validation: their largest register for frame checks
// and, for long lists, a membership set. Untrusted Programs may reference one long list from every instruction, so
// scanning it per reference would make validation quadratic (specs/ProgramModel.md).
internal sealed class GesProgramListMembers(GameEventScriptProgram program)
{
    private const int SetThreshold = 8;
    private readonly int[] _maxima = new int[program.UInt16IndexLists.Slices.Count];
    private readonly bool[] _measured = new bool[program.UInt16IndexLists.Slices.Count];
    private readonly HashSet<ushort>?[] _sets = new HashSet<ushort>?[program.UInt16IndexLists.Slices.Count];

    // The largest register in a list, or -1 for an empty list.
    internal int Max(ushort listIndex)
    {
        if (_measured[listIndex]) return _maxima[listIndex];
        var list = program.UInt16IndexLists.Resolve(listIndex);
        var max = -1;
        for (var index = 0; index < list.Length; index++)
            if (list[index] > max) max = list[index];
        _maxima[listIndex] = max;
        _measured[listIndex] = true;
        return max;
    }

    internal bool Contains(ushort listIndex, ushort register)
    {
        var list = program.UInt16IndexLists.Resolve(listIndex);
        if (list.Length <= SetThreshold)
        {
            for (var index = 0; index < list.Length; index++)
                if (list[index] == register) return true;
            return false;
        }

        if (_sets[listIndex] is not { } set)
        {
            set = new HashSet<ushort>();
            for (var index = 0; index < list.Length; index++) set.Add(list[index]);
            _sets[listIndex] = set;
        }

        return set.Contains(register);
    }
}
