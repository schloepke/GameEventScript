using System;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmRegisterStreams
{
    internal static void GesVmStreamCreate(this GesVmState vmState, ushort destinationRegister, in GesVmValue x)
    {
        if (x.TryCreateStream(out var stream)) vmState.SetStream(destinationRegister, stream);
        else vmState.SetNothing(destinationRegister);
    }

    internal static void GesVmStreamNext(this GesVmState vmState, ushort destinationRegister, in GesVmValue stream, ushort noMoreAddress)
    {
        var result = new GesVmValue();
        if (stream is { Kind: Stream, ObjectValue: IGesVmStream it } && it.TryNext(ref result))
        {
            vmState.SetValue(destinationRegister, in result);
            return;
        }

        vmState.SetValue(destinationRegister, in result);
        vmState.JumpAddress(noMoreAddress);
    }

    internal static void GesVmStreamClose(this GesVmState vmState, ushort iteratorRegister)
    {
        ref var iterator = ref vmState.Register(iteratorRegister);
        if (iterator is not { Kind: Stream, ObjectValue: IDisposable it }) return;
        it.Dispose();
        vmState.SetNothing(iteratorRegister);
    }
}
