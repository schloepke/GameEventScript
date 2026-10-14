// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace GameEventScript.Runtime.VM;

internal static class GesVmRegisterIterators
{
    internal static void GesVmIteratorCreate(this GesVirtualMachine vm, ushort destinationRegister, in GesValue x)
    {
        if (x.CreateIterator() is { } iterator) vm.SetIterator(destinationRegister, iterator);
        else vm.SetNothing(destinationRegister);
    }

    internal static bool GesVmIteratorNext(this GesVirtualMachine vm, ushort destinationRegister, in GesValue iterator, ushort noMoreAddress)
    {
        if (iterator is { Kind: Iterator, ObjectValue: GesIntegerRangeIterator range })
        {
            // Hot loop form: write the integer term in place instead of copying an iterator result.
            if (range.NextInteger() is { } term)
            {
                vm.SetInteger(destinationRegister, term);
                return true;
            }
        }
        else if (iterator is { Kind: Iterator, ObjectValue: IGesIterator it })
        {
            var next = it.Next();
            if (next.HasValue)
            {
                vm.SetValue(destinationRegister, in next.Value);
                return true;
            }
        }

        var result = default(GesValue);
        vm.SetValue(destinationRegister, in result);
        vm.JumpAddress(noMoreAddress);
        return false;
    }

    internal static void GesVmIteratorClose(this GesVirtualMachine vm, ushort iteratorRegister)
    {
        var iterator = vm.Register(iteratorRegister);
        if (iterator is not { Kind: Iterator, ObjectValue: IDisposable it }) return;
        it.Dispose();
        vm.SetNothing(iteratorRegister);
    }
}
