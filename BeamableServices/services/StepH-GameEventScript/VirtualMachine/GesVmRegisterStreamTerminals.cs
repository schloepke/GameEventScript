using System;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmRegisterStreamTerminals
{
    internal static void GesVmCount(this GesVmState vmState, ushort destinationRegister, in GesVmValue iterator)
    {
        switch (iterator.Kind)
        {
            case List when iterator.ObjectValue is GesVmValue[] list:
                vmState.SetInteger(destinationRegister, list.Length);
                return;
            case Map when iterator.ObjectValue is GesVmValueMap map:
                vmState.SetInteger(destinationRegister, map.Length);
                return;
            case Dice when iterator.ObjectValue is int[] dice:
                vmState.SetInteger(destinationRegister, dice.Length);
                return;
            case GameEventScriptBytecodeTypeKind.Range when iterator.ObjectValue is GesVmValueRangeInteger range:
                vmState.SetInteger(destinationRegister, GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step));
                return;
            case GameEventScriptBytecodeTypeKind.Range when iterator.ObjectValue is GesVmValueRangeFloat range:
                vmState.SetInteger(destinationRegister, GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step));
                return;
            case Vector or Point when iterator.ObjectValue is GesVmValueVectorPoint:
                vmState.SetInteger(destinationRegister, 3);
                return;
            case Text or Tag:
                vmState.SetInteger(destinationRegister, iterator.TextValue.Length);
                return;
            case Nothing:
                vmState.SetInteger(destinationRegister, 0);
                return;
        }

        IGesVmStream stream;
        if (iterator is { Kind: Stream, ObjectValue: IGesVmStream sourceStream }) stream = sourceStream;
        else if (!iterator.TryCreateStream(out stream))
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        long count = 0;
        var item = new GesVmValue();
        try
        {
            while (stream.TryNext(ref item)) count++;
            vmState.SetInteger(destinationRegister, count);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }

    internal static void GesVmStreamMin(this GesVmState vmState, ushort destinationRegister, in GesVmValue iterator, ushort itemSlot, ushort projectionEntryAddress, IGesVmStreamEntryEvaluator evaluator)
    {
        GesVmStreamMinMax(vmState, destinationRegister, in iterator, itemSlot, projectionEntryAddress, evaluator, isMax: false);
    }

    internal static void GesVmStreamMax(this GesVmState vmState, ushort destinationRegister, in GesVmValue iterator, ushort itemSlot, ushort projectionEntryAddress, IGesVmStreamEntryEvaluator evaluator)
    {
        GesVmStreamMinMax(vmState, destinationRegister, in iterator, itemSlot, projectionEntryAddress, evaluator, isMax: true);
    }

    internal static void GesVmStreamOneWeighted(this GesVmState vmState, ushort destinationRegister, in GesVmValue iterator, ushort itemSlot, ushort weightEntryAddress, ushort captureSlotListIndex, IGesVmStreamEntryEvaluator evaluator,
        GesVmXoshiroRandom randomGenerator)
    {
        if (iterator is not { Kind: Stream, ObjectValue: IGesVmStream stream })
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        var item = new GesVmValue();
        var weightValue = new GesVmValue();
        var items = new GesVmValue[16];
        var weights = new double[16];
        var itemCount = 0;
        double totalWeight = 0d;
        var captureSlots = vmState.Binary.Uint16ConstantTable.Resolve(captureSlotListIndex);
        var captures = captureSlots.Length == 0 ? [] : new GesVmValue[captureSlots.Length];
        for (var i = 0; i < captureSlots.Length; i++) captures[i] = vmState.Register(captureSlots[i]);
        try
        {
            while (stream.TryNext(ref item))
            {
                if (!evaluator.TryEvaluateStreamEntry(weightEntryAddress, itemSlot, ref item, captures, ref weightValue))
                {
                    vmState.SetNothing(destinationRegister);
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
                vmState.SetNothing(destinationRegister);
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

            vmState.SetValue(destinationRegister, in items[selected]);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }

    internal static void GesVmStreamTakeWeighted(this GesVmState vmState, ushort destinationRegister, in GesVmValue iterator, short count, ushort itemSlot, ushort weightEntryAddress, ushort captureSlotListIndex, IGesVmStreamEntryEvaluator evaluator,
        GesVmXoshiroRandom randomGenerator)
    {
        if (iterator is not { Kind: Stream, ObjectValue: IGesVmStream stream })
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (count <= 0)
        {
            if (stream is IDisposable disposable) disposable.Dispose();
            vmState.SetList(destinationRegister, vmState.EmptyList);
            return;
        }

        var item = new GesVmValue();
        var weightValue = new GesVmValue();
        var items = new GesVmValue[16];
        var weights = new double[16];
        var itemCount = 0;
        double totalWeight = 0d;
        var captureSlots = vmState.Binary.Uint16ConstantTable.Resolve(captureSlotListIndex);
        var captures = captureSlots.Length == 0 ? [] : new GesVmValue[captureSlots.Length];
        for (var i = 0; i < captureSlots.Length; i++) captures[i] = vmState.Register(captureSlots[i]);
        try
        {
            while (stream.TryNext(ref item))
            {
                if (!evaluator.TryEvaluateStreamEntry(weightEntryAddress, itemSlot, ref item, captures, ref weightValue))
                {
                    vmState.SetNothing(destinationRegister);
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
                vmState.SetList(destinationRegister, vmState.EmptyList);
                return;
            }

            var selectedCount = count < itemCount ? count : itemCount;
            var list = new GesVmValue[selectedCount];
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

                list[target] = items[selected];
                totalWeight -= weights[selected];
                if (selected < remainingCount - 1)
                {
                    Array.Copy(items, selected + 1, items, selected, remainingCount - selected - 1);
                    Array.Copy(weights, selected + 1, weights, selected, remainingCount - selected - 1);
                }

                remainingCount--;
            }

            vmState.SetList(destinationRegister, list);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }

    private static void GesVmStreamMinMax(GesVmState vmState, ushort destinationRegister, in GesVmValue iterator, ushort itemSlot, ushort projectionEntryAddress, IGesVmStreamEntryEvaluator evaluator, bool isMax)
    {
        if (iterator is not { Kind: Stream, ObjectValue: IGesVmStream stream })
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        var item = new GesVmValue();
        var projection = new GesVmValue();
        var winner = new GesVmValue();
        var winnerProjection = new GesVmValue();
        var hasWinner = false;
        try
        {
            while (stream.TryNext(ref item))
            {
                if (!evaluator.TryEvaluateStreamEntry(projectionEntryAddress, itemSlot, ref item, null, ref projection))
                {
                    vmState.SetNothing(destinationRegister);
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

            if (hasWinner) vmState.SetValue(destinationRegister, in winner);
            else vmState.SetNothing(destinationRegister);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }
}
