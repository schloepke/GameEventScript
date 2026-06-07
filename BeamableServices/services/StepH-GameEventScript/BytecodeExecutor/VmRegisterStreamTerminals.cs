namespace StepH.GameEventScript.BytecodeExecutor;
using System;
using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;
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
    internal static void VmStreamMin(ref VmValue dst, ref VmValue iterator, ushort itemSlot, ushort projectionEntryAddress, IVmStreamEntryEvaluator evaluator)
    {
        VmStreamMinMax(ref dst, ref iterator, itemSlot, projectionEntryAddress, evaluator, isMax: false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmStreamMax(ref VmValue dst, ref VmValue iterator, ushort itemSlot, ushort projectionEntryAddress, IVmStreamEntryEvaluator evaluator)
    {
        VmStreamMinMax(ref dst, ref iterator, itemSlot, projectionEntryAddress, evaluator, isMax: true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmStreamOneWeighted(ref VmValue dst, ref VmValue iterator, ushort itemSlot, ushort weightEntryAddress, IVmStreamEntryEvaluator evaluator, GameEventScriptRandomGenerator randomGenerator)
    {
        if (iterator is not { Kind: Stream, ObjectValue: IVmStream stream })
        {
            dst.SetNothing();
            return;
        }

        var item = dst.OwningState.CreateNothing();
        var weightValue = dst.OwningState.CreateNothing();
        var items = new VmValue[16];
        var weights = new double[16];
        var itemCount = 0;
        double totalWeight = 0d;
        try
        {
            while (stream.TryNext(ref item))
            {
                if (!evaluator.TryEvaluateStreamEntry(weightEntryAddress, itemSlot, ref item, null, ref weightValue))
                {
                    dst.SetNothing();
                    return;
                }

                var weight = weightValue.AsNumeric;
                if (!double.IsFinite(weight) || weight <= 0d) continue;
                if (itemCount == items.Length)
                {
                    Array.Resize(ref items, items.Length << 1);
                    Array.Resize(ref weights, weights.Length << 1);
                }

                items[itemCount] = item;
                weights[itemCount] = weight;
                totalWeight += weight;
                itemCount++;
            }

            if (itemCount == 0 || totalWeight <= 0d)
            {
                dst.SetNothing();
                return;
            }

            var threshold = randomGenerator.NextInclusiveFloat(0d, totalWeight);
            double cumulative = 0d;
            var selected = itemCount - 1;
            for (var i = 0; i < itemCount; i++)
            {
                cumulative += weights[i];
                if (threshold < cumulative)
                {
                    selected = i;
                    break;
                }
            }

            dst = items[selected];
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmStreamTakeWeighted(ref VmValue dst, ref VmValue iterator, short count, ushort itemSlot, ushort weightEntryAddress, IVmStreamEntryEvaluator evaluator, GameEventScriptRandomGenerator randomGenerator)
    {
        if (iterator is not { Kind: Stream, ObjectValue: IVmStream stream })
        {
            dst.SetNothing();
            return;
        }

        if (count <= 0)
        {
            if (stream is IDisposable disposable) disposable.Dispose();
            dst.SetList(dst.OwningState.EmptyList);
            return;
        }

        var item = dst.OwningState.CreateNothing();
        var weightValue = dst.OwningState.CreateNothing();
        var items = new VmValue[16];
        var weights = new double[16];
        var itemCount = 0;
        double totalWeight = 0d;
        try
        {
            while (stream.TryNext(ref item))
            {
                if (!evaluator.TryEvaluateStreamEntry(weightEntryAddress, itemSlot, ref item, null, ref weightValue))
                {
                    dst.SetNothing();
                    return;
                }

                var weight = weightValue.AsNumeric;
                if (!double.IsFinite(weight) || weight <= 0d) continue;
                if (itemCount == items.Length)
                {
                    Array.Resize(ref items, items.Length << 1);
                    Array.Resize(ref weights, weights.Length << 1);
                }

                items[itemCount] = item;
                weights[itemCount] = weight;
                totalWeight += weight;
                itemCount++;
            }

            if (itemCount == 0 || totalWeight <= 0d)
            {
                dst.SetList(dst.OwningState.EmptyList);
                return;
            }

            var selectedCount = count < itemCount ? count : itemCount;
            var list = dst.OwningState.CreateList(selectedCount);
            var remainingCount = itemCount;
            for (var target = 0; target < selectedCount && remainingCount > 0 && totalWeight > 0d; target++)
            {
                var threshold = randomGenerator.NextInclusiveFloat(0d, totalWeight);
                double cumulative = 0d;
                var selected = remainingCount - 1;
                for (var i = 0; i < remainingCount; i++)
                {
                    cumulative += weights[i];
                    if (threshold < cumulative)
                    {
                        selected = i;
                        break;
                    }
                }

                list.Items[target] = items[selected];
                totalWeight -= weights[selected];
                if (selected < remainingCount - 1)
                {
                    Array.Copy(items, selected + 1, items, selected, remainingCount - selected - 1);
                    Array.Copy(weights, selected + 1, weights, selected, remainingCount - selected - 1);
                }

                remainingCount--;
            }

            dst.SetList(list);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void VmStreamMinMax(ref VmValue dst, ref VmValue iterator, ushort itemSlot, ushort projectionEntryAddress, IVmStreamEntryEvaluator evaluator, bool isMax)
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
