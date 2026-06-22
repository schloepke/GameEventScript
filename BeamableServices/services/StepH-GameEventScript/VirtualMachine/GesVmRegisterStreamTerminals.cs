using System;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;
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

    internal static void GesVmSum(this GesVmState vmState, ushort destinationRegister, in GesVmValue iterator)
    {
        var result = new GesVmValue();
        switch (iterator.Kind)
        {
            case List when iterator.ObjectValue is GesVmValue[] list:
            {
                if (list.Length == 0)
                {
                    vmState.SetFloat(destinationRegister, 0d);
                    return;
                }

                var listSum = list[0];
                var listNext = new GesVmValue();
                for (var i = 1; i < list.Length; i++)
                {
                    GesVmRegisterMath.GesVmAdd(ref listNext, in listSum, in list[i], vmState);
                    listSum = listNext;
                }

                vmState.SetValue(destinationRegister, in listSum);
                return;
            }
            case Map when iterator.ObjectValue is GesVmValueMap map:
            {
                var values = map.ValueList;
                if (values.Length == 0)
                {
                    vmState.SetFloat(destinationRegister, 0d);
                    return;
                }

                var mapSum = values[0];
                var mapNext = new GesVmValue();
                for (var i = 1; i < values.Length; i++)
                {
                    GesVmRegisterMath.GesVmAdd(ref mapNext, in mapSum, in values[i], vmState);
                    mapSum = mapNext;
                }

                vmState.SetValue(destinationRegister, in mapSum);
                return;
            }
            case Dice when iterator.ObjectValue is int[] dice:
            {
                if (dice.Length == 0)
                {
                    vmState.SetFloat(destinationRegister, 0d);
                    return;
                }

                long diceSum = 0;
                for (var i = 0; i < dice.Length; i++) diceSum += dice[i];
                vmState.SetInteger(destinationRegister, diceSum);
                return;
            }
            case GameEventScriptBytecodeTypeKind.Range when iterator.ObjectValue is GesVmValueRangeInteger range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
                if (length <= 0)
                {
                    vmState.SetFloat(destinationRegister, 0d);
                    return;
                }

                long rangeSum = 0;
                for (long i = 1; i <= length; i++)
                {
                    if (GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, i, out var value)) rangeSum += value;
                }

                vmState.SetInteger(destinationRegister, rangeSum);
                return;
            }
            case GameEventScriptBytecodeTypeKind.Range when iterator.ObjectValue is GesVmValueRangeFloat range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
                if (length <= 0)
                {
                    vmState.SetFloat(destinationRegister, 0d);
                    return;
                }

                var floatRangeSum = 0d;
                for (long i = 1; i <= length; i++)
                {
                    if (GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, i, out var value)) floatRangeSum += value;
                }

                vmState.SetFloat(destinationRegister, floatRangeSum);
                return;
            }
        }

        if (iterator is not { Kind: Stream, ObjectValue: IGesVmStream stream })
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        var item = new GesVmValue();
        var sum = new GesVmValue();
        var next = new GesVmValue();
        try
        {
            if (!stream.TryNext(ref sum))
            {
                vmState.SetFloat(destinationRegister, 0d);
                return;
            }

            while (stream.TryNext(ref item))
            {
                GesVmRegisterMath.GesVmAdd(ref next, in sum, in item, vmState);
                sum = next;
            }

            result = sum;
            vmState.SetValue(destinationRegister, in result);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }

    internal static void GesVmAverage(this GesVmState vmState, ushort destinationRegister, in GesVmValue iterator)
    {
        var result = new GesVmValue();
        switch (iterator.Kind)
        {
            case List when iterator.ObjectValue is GesVmValue[] list:
            {
                if (list.Length == 0)
                {
                    vmState.SetNothing(destinationRegister);
                    return;
                }

                var listSum = list[0];
                var listNext = new GesVmValue();
                for (var i = 1; i < list.Length; i++)
                {
                    GesVmRegisterMath.GesVmAdd(ref listNext, in listSum, in list[i], vmState);
                    listSum = listNext;
                }

                var countValue = new GesVmValue();

                countValue.SetInteger(list.Length);
                GesVmRegisterMath.GesVmDivide(ref listNext, in listSum, in countValue, vmState);
                vmState.SetValue(destinationRegister, in listNext);
                return;
            }
            case Map when iterator.ObjectValue is GesVmValueMap map:
            {
                var values = map.ValueList;
                if (values.Length == 0)
                {
                    vmState.SetNothing(destinationRegister);
                    return;
                }

                var mapSum = values[0];
                var mapNext = new GesVmValue();
                for (var i = 1; i < values.Length; i++)
                {
                    GesVmRegisterMath.GesVmAdd(ref mapNext, in mapSum, in values[i], vmState);
                    mapSum = mapNext;
                }

                var countValue = new GesVmValue();

                countValue.SetInteger(values.Length);
                GesVmRegisterMath.GesVmDivide(ref mapNext, in mapSum, in countValue, vmState);
                vmState.SetValue(destinationRegister, in mapNext);
                return;
            }
            case Dice when iterator.ObjectValue is int[] dice:
            {
                if (dice.Length == 0)
                {
                    vmState.SetNothing(destinationRegister);
                    return;
                }

                long diceSum = 0;
                for (var i = 0; i < dice.Length; i++) diceSum += dice[i];
                vmState.SetFloat(destinationRegister, (double)diceSum / dice.Length);
                return;
            }
            case GameEventScriptBytecodeTypeKind.Range when iterator.ObjectValue is GesVmValueRangeInteger range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
                if (length <= 0)
                {
                    vmState.SetNothing(destinationRegister);
                    return;
                }

                long rangeSum = 0;
                for (long i = 1; i <= length; i++)
                {
                    if (GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, i, out var value)) rangeSum += value;
                }

                vmState.SetFloat(destinationRegister, (double)rangeSum / length);
                return;
            }
            case GameEventScriptBytecodeTypeKind.Range when iterator.ObjectValue is GesVmValueRangeFloat range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
                if (length <= 0)
                {
                    vmState.SetNothing(destinationRegister);
                    return;
                }

                var floatRangeSum = 0d;
                for (long i = 1; i <= length; i++)
                {
                    if (GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, i, out var value)) floatRangeSum += value;
                }

                vmState.SetFloat(destinationRegister, floatRangeSum / length);
                return;
            }
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
        var sum = new GesVmValue();
        var next = new GesVmValue();
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

                GesVmRegisterMath.GesVmAdd(ref next, in sum, in item, vmState);
                sum = next;
            }

            if (count == 0)
            {
                vmState.SetNothing(destinationRegister);
                return;
            }

            var countValue = new GesVmValue();

            countValue.SetInteger(count);
            GesVmRegisterMath.GesVmDivide(ref next, in sum, in countValue, vmState);
            result = next;
            vmState.SetValue(destinationRegister, in result);
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
