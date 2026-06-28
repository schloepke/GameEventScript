using System;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;
using StepH.GameEventScript.Runtime.Values;

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
        var result = new GesValue();
        if (iterator is { Kind: Iterator, ObjectValue: IGesIterator it } && it.TryNext(ref result))
        {
            vmState.SetValue(destinationRegister, in result);
            return;
        }

        vmState.SetValue(destinationRegister, in result);
        vmState.JumpAddress(noMoreAddress);
    }

    internal static void GesVmIteratorClose(this GesVmState vmState, ushort iteratorRegister)
    {
        ref var iterator = ref vmState.Register(iteratorRegister);
        if (iterator is not { Kind: Iterator, ObjectValue: IDisposable it }) return;
        it.Dispose();
        vmState.SetNothing(iteratorRegister);
    }
}
