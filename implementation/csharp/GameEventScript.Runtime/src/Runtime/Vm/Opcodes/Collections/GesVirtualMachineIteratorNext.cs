// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private bool ExecuteIteratorNext(ushort destinationRegister, in GesValue iterator, ushort noMoreAddress)
    {
        if (iterator is { Kind: Iterator, ObjectValue: GesIntegerRangeIterator range })
        {
            // Hot loop form: write the integer term in place instead of copying an iterator result.
            if (range.NextInteger() is { } term)
            {
                SetInteger(destinationRegister, term);
                return true;
            }
        }
        else if (iterator is { Kind: Iterator, ObjectValue: IGesIterator it })
        {
            var next = it.Next();
            if (next.HasValue)
            {
                SetValue(destinationRegister, in next.Value);
                return true;
            }
        }

        var result = default(GesValue);
        SetValue(destinationRegister, in result);
        JumpAddress(noMoreAddress);
        return false;
    }

    private bool ExecuteIteratorNextComponents(GesBytecodeInstruction instruction)
    {
        var iterator = Register(instruction.XRegister).ObjectValue as IGesIterator;
        var targets = FetchUInt16SliceTableByPointer(instruction.DestinationRegister);
        var components = iterator as IGesComponentIterator;
        var item = components is null && iterator is not null ? iterator.Next() : default;
        var hasValue = components is not null ? components.MoveNext() : item.HasValue;
        for (var index = 0; index < targets.Length; index++)
        {
            var value = !hasValue ? default : components is not null ? components.Component(index) : GesCompoundSources.Decompose(item.Value, index);
            SetValue(targets[index], value);
        }
        if (!hasValue) JumpAddress(instruction.TargetAddress);
        return hasValue;
    }
}
