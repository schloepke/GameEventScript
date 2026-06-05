using System;
using System.Runtime.CompilerServices;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterStreams
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmStreamCreate(ref this VmValue dst, ref VmValue x)
    {
        if (x.TryCreateStream(out var stream)) dst.SetStream(stream);
        else dst.SetNothing();
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmStreamNext(ref this VmValue dst, ref VmValue stream, ushort noMoreAddress)
    {
        if (stream is not { Kind: Stream, ObjectValue: IVmStream it } || !it.TryNext(ref dst)) dst.OwningState.JumpAddress(noMoreAddress);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmStreamClose(ref this VmValue iterator)
    {
        if (iterator is not { Kind: Stream, ObjectValue: IDisposable it }) return;
        it.Dispose();
        iterator.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmStreamMap(ref this VmValue dst, ref VmValue stream, ushort mapEntryAddress, ref VmValue helperSlot, ref VmValue captureSlotList)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmStreamFilter(ref this VmValue dst, ref VmValue stream, ushort predicateEntryAddress, ref VmValue helperSlot, ref VmValue captureSlotList)
    {
    }

   
}