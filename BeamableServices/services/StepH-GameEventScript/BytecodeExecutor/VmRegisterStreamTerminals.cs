namespace StepH.GameEventScript.BytecodeExecutor;
using System;
using System.Runtime.CompilerServices;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

internal static class VmRegisterStreamTerminals
{
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmStreamCount(ref this VmValue dst, ref VmValue iterator)
    {
        if (iterator is not { Kind: Stream, ObjectValue: IVmStream stream })
        {
            dst.SetNothing();
            return;
        }

        long count = 0;
        var item = dst.OwningState.CreateNothing();
        try
        {
            while (stream.TryNext(ref item)) count++;
            dst.SetInteger(count);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmStreamSum(ref this VmValue dst, ref VmValue iterator)
    {
        if (iterator is not { Kind: Stream, ObjectValue: IVmStream stream })
        {
            dst.SetNothing();
            return;
        }

        var item = dst.OwningState.CreateNothing();
        var sum = dst.OwningState.CreateNothing();
        var next = dst.OwningState.CreateNothing();
        try
        {
            if (!stream.TryNext(ref sum))
            {
                dst.SetFloat(0d);
                return;
            }

            while (stream.TryNext(ref item))
            {
                next.VmAdd(ref sum, ref item, ref dst.OwningState.Binary.TextConstantTable);
                sum = next;
            }

            dst = sum;
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmStreamAverage(ref this VmValue dst, ref VmValue iterator)
    {
        if (iterator is not { Kind: Stream, ObjectValue: IVmStream stream })
        {
            dst.SetNothing();
            return;
        }

        long count = 0;
        var item = dst.OwningState.CreateNothing();
        var sum = dst.OwningState.CreateNothing();
        var next = dst.OwningState.CreateNothing();
        try
        {
            while (stream.TryNext(ref item))
            {
                count++;
                if (count == 1)
                {
                    sum = item;
                    continue;
                }

                next.VmAdd(ref sum, ref item, ref dst.OwningState.Binary.TextConstantTable);
                sum = next;
            }

            if (count == 0)
            {
                dst.SetNothing();
                return;
            }

            var countValue = dst.OwningState.CreateInteger(count);
            next.VmDivide(ref sum, ref countValue, ref dst.OwningState.Binary.TextConstantTable);
            dst = next;
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmStreamMin(ref this VmValue dst, ref VmValue iterator, ushort itemSlot, ushort projectionEntryAddress, IVmStreamEntryEvaluator evaluator)
    {
        dst.VmStreamMinMax(ref iterator, itemSlot, projectionEntryAddress, evaluator, isMax: false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmStreamMax(ref this VmValue dst, ref VmValue iterator, ushort itemSlot, ushort projectionEntryAddress, IVmStreamEntryEvaluator evaluator)
    {
        dst.VmStreamMinMax(ref iterator, itemSlot, projectionEntryAddress, evaluator, isMax: true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void VmStreamMinMax(ref this VmValue dst, ref VmValue iterator, ushort itemSlot, ushort projectionEntryAddress, IVmStreamEntryEvaluator evaluator, bool isMax)
    {
        if (iterator is not { Kind: Stream, ObjectValue: IVmStream stream })
        {
            dst.SetNothing();
            return;
        }

        var item = dst.OwningState.CreateNothing();
        var projection = dst.OwningState.CreateNothing();
        var winner = dst.OwningState.CreateNothing();
        var winnerProjection = dst.OwningState.CreateNothing();
        var hasWinner = false;
        try
        {
            while (stream.TryNext(ref item))
            {
                if (!evaluator.TryEvaluateStreamEntry(projectionEntryAddress, itemSlot, ref item, null, ref projection))
                {
                    dst.SetNothing();
                    return;
                }

                if (!hasWinner)
                {
                    winner = item;
                    winnerProjection = projection;
                    hasWinner = true;
                    continue;
                }

                var left = projection.AsNumericWithUnit(out var leftUnit);
                var right = winnerProjection.AsNumericWithUnit(out var rightUnit);
                if (leftUnit != rightUnit || double.IsNaN(left) || double.IsNaN(right)) continue;
                if ((isMax && left > right) || (!isMax && left < right))
                {
                    winner = item;
                    winnerProjection = projection;
                }
            }

            if (hasWinner) dst = winner;
            else dst.SetNothing();
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }

}
