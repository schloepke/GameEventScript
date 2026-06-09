using System;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterStreams
{
    internal static void VmStreamCreate(ref this VmValue dst, ref VmValue x)
    {
        if (x.TryCreateStream(out var stream)) dst.SetStream(stream);
        else dst.SetNothing();
    }
    internal static void VmStreamNext(ref this VmValue dst, ref VmValue stream, ushort noMoreAddress)
    {
        if (stream is not { Kind: Stream, ObjectValue: IVmStream it } || !it.TryNext(ref dst)) dst.OwningState.JumpAddress(noMoreAddress);
    }
    internal static void VmStreamClose(ref this VmValue iterator)
    {
        if (iterator is not { Kind: Stream, ObjectValue: IDisposable it }) return;
        it.Dispose();
        iterator.SetNothing();
    }
    internal static void VmStreamMap(ref this VmValue dst, ref VmValue stream, ushort mapEntryAddress, ushort helperSlot, ushort captureSlotListIndex, IVmStreamEntryEvaluator evaluator)
    {
        if (stream is not { Kind: Stream, ObjectValue: IVmStream source })
        {
            dst.SetNothing();
            return;
        }

        var captureSlots = dst.OwningState.Binary.Uint16ConstantTable.Resolve(captureSlotListIndex);
        var captures = captureSlots.Length == 0 ? [] : new VmValue[captureSlots.Length];
        for (var i = 0; i < captureSlots.Length; i++) captures[i] = dst.OwningState.Register(captureSlots[i]);
        dst.SetStream(new VmTransformStream(dst.OwningState, source, evaluator, mapEntryAddress, helperSlot, captures, filter: false));
    }
    internal static void VmStreamFilter(ref this VmValue dst, ref VmValue stream, ushort predicateEntryAddress, ushort helperSlot, ushort captureSlotListIndex, IVmStreamEntryEvaluator evaluator)
    {
        if (stream is not { Kind: Stream, ObjectValue: IVmStream source })
        {
            dst.SetNothing();
            return;
        }

        var captureSlots = dst.OwningState.Binary.Uint16ConstantTable.Resolve(captureSlotListIndex);
        var captures = captureSlots.Length == 0 ? [] : new VmValue[captureSlots.Length];
        for (var i = 0; i < captureSlots.Length; i++) captures[i] = dst.OwningState.Register(captureSlots[i]);
        dst.SetStream(new VmTransformStream(dst.OwningState, source, evaluator, predicateEntryAddress, helperSlot, captures, filter: true));
    }

   
}
