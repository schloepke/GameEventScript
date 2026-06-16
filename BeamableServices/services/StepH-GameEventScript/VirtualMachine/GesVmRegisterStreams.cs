using System;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmRegisterStreams
{
    internal static void GesVmStreamCreate(this GesVmState state, ushort destinationRegister, in GesVmValue x)
    {
        if (x.TryCreateStream(out var stream)) state.SetStream(destinationRegister, stream);
        else state.SetNothing(destinationRegister);
    }

    internal static void GesVmStreamNext(this GesVmState state, ushort destinationRegister, in GesVmValue stream, ushort noMoreAddress)
    {
        var result = state.CreateNothing();
        if (stream is { Kind: Stream, ObjectValue: IGesVmStream it } && it.TryNext(ref result))
        {
            state.SetValue(destinationRegister, in result);
            return;
        }

        state.SetValue(destinationRegister, in result);
        state.JumpAddress(noMoreAddress);
    }

    internal static void GesVmStreamClose(this GesVmState state, ushort iteratorRegister)
    {
        ref var iterator = ref state.Register(iteratorRegister);
        if (iterator is not { Kind: Stream, ObjectValue: IDisposable it }) return;
        it.Dispose();
        state.SetNothing(iteratorRegister);
    }

    internal static void GesVmStreamMap(this GesVmState state, ushort destinationRegister, in GesVmValue stream, ushort mapEntryAddress, ushort helperSlot, ushort captureSlotListIndex, IGesVmStreamEntryEvaluator evaluator)
    {
        if (stream is not { Kind: Stream, ObjectValue: IGesVmStream source })
        {
            state.SetNothing(destinationRegister);
            return;
        }

        var captureSlots = state.Binary.Uint16ConstantTable.Resolve(captureSlotListIndex);
        var captures = captureSlots.Length == 0 ? [] : new GesVmValue[captureSlots.Length];
        for (var i = 0; i < captureSlots.Length; i++) captures[i] = state.Register(captureSlots[i]);
        state.SetStream(destinationRegister, new GesVmTransformStream(state, source, evaluator, mapEntryAddress, helperSlot, captures, filter: false));
    }

    internal static void GesVmStreamFilter(this GesVmState state, ushort destinationRegister, in GesVmValue stream, ushort predicateEntryAddress, ushort helperSlot, ushort captureSlotListIndex, IGesVmStreamEntryEvaluator evaluator)
    {
        if (stream is not { Kind: Stream, ObjectValue: IGesVmStream source })
        {
            state.SetNothing(destinationRegister);
            return;
        }

        var captureSlots = state.Binary.Uint16ConstantTable.Resolve(captureSlotListIndex);
        var captures = captureSlots.Length == 0 ? [] : new GesVmValue[captureSlots.Length];
        for (var i = 0; i < captureSlots.Length; i++) captures[i] = state.Register(captureSlots[i]);
        state.SetStream(destinationRegister, new GesVmTransformStream(state, source, evaluator, predicateEntryAddress, helperSlot, captures, filter: true));
    }
}
