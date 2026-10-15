// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteIteratorCreate(GesBytecodeInstruction instruction, GesRuntimeBudget budget)
    {
        var mode = instruction.UnitAndFlags >> 5;
        if (mode == 0)
        {
            CreateIterator(instruction.DestinationRegister, Register(instruction.XRegister));
            return;
        }
        IGesIterator? iterator = null;
        if (mode == 6)
        {
            var source = Register(instruction.XRegister);
            var map = source.ObjectValue switch
            {
                GesValueMap value => value,
                GesCustomObject custom => custom.Map,
                GesExternalStorage external => external.ToMap(),
                _ => null
            };
            if (map is not null) iterator = new GesEntriesIterator(map, budget);
        }
        else
        {
            var registers = FetchUInt16SliceTableByPointer(instruction.XRegister);
            var sources = new GesValue[registers.Length];
            for (var index = 0; index < sources.Length; index++) sources[index] = Register(registers[index]);
            iterator = GesCompoundSources.Create(sources, mode, budget);
        }
        if (iterator is null) SetNothing(instruction.DestinationRegister);
        else SetIterator(instruction.DestinationRegister, iterator);
    }
}
