// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using StepH.GameEventScript.Runtime.Values;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.Runtime.VM;

internal static class GesVmRegisterIterators
{
    internal static void GesVmIteratorCreate(this GesVmState vmState, ushort destinationRegister, in GesValue x)
    {
        if (x.CreateIterator() is { } iterator) vmState.SetIterator(destinationRegister, iterator);
        else vmState.SetNothing(destinationRegister);
    }

    internal static void GesVmIteratorNext(this GesVmState vmState, ushort destinationRegister, in GesValue iterator, ushort noMoreAddress)
    {
        if (iterator is { Kind: Iterator, ObjectValue: IGesIterator it })
        {
            var next = it.Next();
            if (next.HasValue)
            {
                vmState.SetValue(destinationRegister, in next.Value);
                return;
            }
        }

        var result = default(GesValue);
        vmState.SetValue(destinationRegister, in result);
        vmState.JumpAddress(noMoreAddress);
    }

    internal static void GesVmIteratorClose(this GesVmState vmState, ushort iteratorRegister)
    {
        var iterator = vmState.Register(iteratorRegister);
        if (iterator is not { Kind: Iterator, ObjectValue: IDisposable it }) return;
        it.Dispose();
        vmState.SetNothing(iteratorRegister);
    }
}
