using System;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmRegisterStreams
{
    internal static void VmStreamCreate(ref this GesVmValue dst, ref GesVmValue x)
    {
        if (x.TryCreateStream(out var stream)) dst.SetStream(stream);
        else dst.SetNothing();
    }
    internal static void VmStreamNext(ref this GesVmValue dst, ref GesVmValue stream, ushort noMoreAddress)
    {
        if (stream is not { Kind: Stream, ObjectValue: IGesVmStream it } || !it.TryNext(ref dst)) dst.OwningState.JumpAddress(noMoreAddress);
    }
    internal static void VmStreamClose(ref this GesVmValue iterator)
    {
        if (iterator is not { Kind: Stream, ObjectValue: IDisposable it }) return;
        it.Dispose();
        iterator.SetNothing();
    }
    internal static void VmStreamMap(ref this GesVmValue dst, ref GesVmValue stream, ushort mapEntryAddress, ushort helperSlot, ushort captureSlotListIndex, IGesVmStreamEntryEvaluator evaluator)
    {
        if (stream is not { Kind: Stream, ObjectValue: IGesVmStream source })
        {
            dst.SetNothing();
            return;
        }

        var captureSlots = dst.OwningState.Binary.Uint16ConstantTable.Resolve(captureSlotListIndex);
        var captures = captureSlots.Length == 0 ? [] : new GesVmValue[captureSlots.Length];
        for (var i = 0; i < captureSlots.Length; i++) captures[i] = dst.OwningState.Register(captureSlots[i]);
        dst.SetStream(new GesVmTransformStream(dst.OwningState, source, evaluator, mapEntryAddress, helperSlot, captures, filter: false));
    }
    internal static void VmStreamFilter(ref this GesVmValue dst, ref GesVmValue stream, ushort predicateEntryAddress, ushort helperSlot, ushort captureSlotListIndex, IGesVmStreamEntryEvaluator evaluator)
    {
        if (stream is not { Kind: Stream, ObjectValue: IGesVmStream source })
        {
            dst.SetNothing();
            return;
        }

        var captureSlots = dst.OwningState.Binary.Uint16ConstantTable.Resolve(captureSlotListIndex);
        var captures = captureSlots.Length == 0 ? [] : new GesVmValue[captureSlots.Length];
        for (var i = 0; i < captureSlots.Length; i++) captures[i] = dst.OwningState.Register(captureSlots[i]);
        dst.SetStream(new GesVmTransformStream(dst.OwningState, source, evaluator, predicateEntryAddress, helperSlot, captures, filter: true));
    }

   
}
