using System;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmRegisterStreamTerminals
{
    internal static void GesVmCount(ref this GesVmValue dst, ref GesVmValue iterator)
    {
        switch (iterator.Kind)
        {
            case List when iterator.ObjectValue is GesVmValue[] list:
                dst.SetInteger(list.Length);
                return;
            case Map when iterator.ObjectValue is GesVmValueMap map:
                dst.SetInteger(map.Length);
                return;
            case Dice when iterator.ObjectValue is int[] dice:
                dst.SetInteger(dice.Length);
                return;
            case GameEventScriptBytecodeTypeKind.Range when iterator.ObjectValue is GesVmValueRangeInteger range:
                dst.SetInteger(GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step));
                return;
            case GameEventScriptBytecodeTypeKind.Range when iterator.ObjectValue is GesVmValueRangeFloat range:
                dst.SetInteger(GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step));
                return;
            case Vector or Point when iterator.ObjectValue is GesVmValueVectorPoint:
                dst.SetInteger(3);
                return;
            case Text or Tag:
                dst.SetInteger(iterator.ReadTextOrTag().Length);
                return;
        }

        IGesVmStream stream;
        if (iterator is { Kind: Stream, ObjectValue: IGesVmStream sourceStream }) stream = sourceStream;
        else if (!iterator.TryCreateStream(out stream))
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

    internal static void GesVmSum(ref this GesVmValue dst, ref GesVmValue iterator)
    {
        switch (iterator.Kind)
        {
            case List when iterator.ObjectValue is GesVmValue[] list:
            {
                if (list.Length == 0)
                {
                    dst.SetFloat(0d);
                    return;
                }

                var listSum = list[0];
                var listNext = dst.OwningState.CreateNothing();
                for (var i = 1; i < list.Length; i++)
                {
                    listNext.GesVmAdd(ref listSum, ref list[i], ref dst.OwningState.Binary.TextConstantTable);
                    listSum = listNext;
                }

                dst = listSum;
                return;
            }
            case Map when iterator.ObjectValue is GesVmValueMap map:
            {
                var values = map.ValueList;
                if (values.Length == 0)
                {
                    dst.SetFloat(0d);
                    return;
                }

                var mapSum = values[0];
                var mapNext = dst.OwningState.CreateNothing();
                for (var i = 1; i < values.Length; i++)
                {
                    mapNext.GesVmAdd(ref mapSum, ref values[i], ref dst.OwningState.Binary.TextConstantTable);
                    mapSum = mapNext;
                }

                dst = mapSum;
                return;
            }
            case Dice when iterator.ObjectValue is int[] dice:
            {
                if (dice.Length == 0)
                {
                    dst.SetFloat(0d);
                    return;
                }

                long diceSum = 0;
                for (var i = 0; i < dice.Length; i++) diceSum += dice[i];
                dst.SetInteger(diceSum);
                return;
            }
            case GameEventScriptBytecodeTypeKind.Range when iterator.ObjectValue is GesVmValueRangeInteger range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
                if (length <= 0)
                {
                    dst.SetFloat(0d);
                    return;
                }

                long rangeSum = 0;
                for (long i = 1; i <= length; i++)
                {
                    if (GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, i, out var value)) rangeSum += value;
                }

                dst.SetInteger(rangeSum);
                return;
            }
            case GameEventScriptBytecodeTypeKind.Range when iterator.ObjectValue is GesVmValueRangeFloat range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
                if (length <= 0)
                {
                    dst.SetFloat(0d);
                    return;
                }

                var floatRangeSum = 0d;
                for (long i = 1; i <= length; i++)
                {
                    if (GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, i, out var value)) floatRangeSum += value;
                }

                dst.SetFloat(floatRangeSum);
                return;
            }
        }

        if (iterator is not { Kind: Stream, ObjectValue: IGesVmStream stream })
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
                next.GesVmAdd(ref sum, ref item, ref dst.OwningState.Binary.TextConstantTable);
                sum = next;
            }

            dst = sum;
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }

    internal static void GesVmAverage(ref this GesVmValue dst, ref GesVmValue iterator)
    {
        switch (iterator.Kind)
        {
            case List when iterator.ObjectValue is GesVmValue[] list:
            {
                if (list.Length == 0)
                {
                    dst.SetNothing();
                    return;
                }

                var listSum = list[0];
                var listNext = dst.OwningState.CreateNothing();
                for (var i = 1; i < list.Length; i++)
                {
                    listNext.GesVmAdd(ref listSum, ref list[i], ref dst.OwningState.Binary.TextConstantTable);
                    listSum = listNext;
                }

                var countValue = dst.OwningState.CreateInteger(list.Length);
                listNext.GesVmDivide(ref listSum, ref countValue, ref dst.OwningState.Binary.TextConstantTable);
                dst = listNext;
                return;
            }
            case Map when iterator.ObjectValue is GesVmValueMap map:
            {
                var values = map.ValueList;
                if (values.Length == 0)
                {
                    dst.SetNothing();
                    return;
                }

                var mapSum = values[0];
                var mapNext = dst.OwningState.CreateNothing();
                for (var i = 1; i < values.Length; i++)
                {
                    mapNext.GesVmAdd(ref mapSum, ref values[i], ref dst.OwningState.Binary.TextConstantTable);
                    mapSum = mapNext;
                }

                var countValue = dst.OwningState.CreateInteger(values.Length);
                mapNext.GesVmDivide(ref mapSum, ref countValue, ref dst.OwningState.Binary.TextConstantTable);
                dst = mapNext;
                return;
            }
            case Dice when iterator.ObjectValue is int[] dice:
            {
                if (dice.Length == 0)
                {
                    dst.SetNothing();
                    return;
                }

                long diceSum = 0;
                for (var i = 0; i < dice.Length; i++) diceSum += dice[i];
                dst.SetFloat((double)diceSum / dice.Length);
                return;
            }
            case GameEventScriptBytecodeTypeKind.Range when iterator.ObjectValue is GesVmValueRangeInteger range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
                if (length <= 0)
                {
                    dst.SetNothing();
                    return;
                }

                long rangeSum = 0;
                for (long i = 1; i <= length; i++)
                {
                    if (GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, i, out var value)) rangeSum += value;
                }

                dst.SetFloat((double)rangeSum / length);
                return;
            }
            case GameEventScriptBytecodeTypeKind.Range when iterator.ObjectValue is GesVmValueRangeFloat range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
                if (length <= 0)
                {
                    dst.SetNothing();
                    return;
                }

                var floatRangeSum = 0d;
                for (long i = 1; i <= length; i++)
                {
                    if (GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, i, out var value)) floatRangeSum += value;
                }

                dst.SetFloat(floatRangeSum / length);
                return;
            }
        }

        IGesVmStream stream;
        if (iterator is { Kind: Stream, ObjectValue: IGesVmStream sourceStream }) stream = sourceStream;
        else if (!iterator.TryCreateStream(out stream))
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

                next.GesVmAdd(ref sum, ref item, ref dst.OwningState.Binary.TextConstantTable);
                sum = next;
            }

            if (count == 0)
            {
                dst.SetNothing();
                return;
            }

            var countValue = dst.OwningState.CreateInteger(count);
            next.GesVmDivide(ref sum, ref countValue, ref dst.OwningState.Binary.TextConstantTable);
            dst = next;
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }

    internal static void GesVmStreamMin(ref GesVmValue dst, ref GesVmValue iterator, ushort itemSlot, ushort projectionEntryAddress, IGesVmStreamEntryEvaluator evaluator)
    {
        GesVmStreamMinMax(ref dst, ref iterator, itemSlot, projectionEntryAddress, evaluator, isMax: false);
    }

    internal static void GesVmStreamMax(ref GesVmValue dst, ref GesVmValue iterator, ushort itemSlot, ushort projectionEntryAddress, IGesVmStreamEntryEvaluator evaluator)
    {
        GesVmStreamMinMax(ref dst, ref iterator, itemSlot, projectionEntryAddress, evaluator, isMax: true);
    }

    internal static void GesVmStreamOneWeighted(ref GesVmValue dst, ref GesVmValue iterator, ushort itemSlot, ushort weightEntryAddress, ushort captureSlotListIndex, IGesVmStreamEntryEvaluator evaluator,
        GesVmXoshiroRandom randomGenerator)
    {
        if (iterator is not { Kind: Stream, ObjectValue: IGesVmStream stream })
        {
            dst.SetNothing();
            return;
        }

        var item = dst.OwningState.CreateNothing();
        var weightValue = dst.OwningState.CreateNothing();
        var items = new GesVmValue[16];
        var weights = new double[16];
        var itemCount = 0;
        double totalWeight = 0d;
        var captureSlots = dst.OwningState.Binary.Uint16ConstantTable.Resolve(captureSlotListIndex);
        var captures = captureSlots.Length == 0 ? [] : new GesVmValue[captureSlots.Length];
        for (var i = 0; i < captureSlots.Length; i++) captures[i] = dst.OwningState.Register(captureSlots[i]);
        try
        {
            while (stream.TryNext(ref item))
            {
                if (!evaluator.TryEvaluateStreamEntry(weightEntryAddress, itemSlot, ref item, captures, ref weightValue))
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

    internal static void GesVmStreamTakeWeighted(ref GesVmValue dst, ref GesVmValue iterator, short count, ushort itemSlot, ushort weightEntryAddress, ushort captureSlotListIndex, IGesVmStreamEntryEvaluator evaluator,
        GesVmXoshiroRandom randomGenerator)
    {
        if (iterator is not { Kind: Stream, ObjectValue: IGesVmStream stream })
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
        var items = new GesVmValue[16];
        var weights = new double[16];
        var itemCount = 0;
        double totalWeight = 0d;
        var captureSlots = dst.OwningState.Binary.Uint16ConstantTable.Resolve(captureSlotListIndex);
        var captures = captureSlots.Length == 0 ? [] : new GesVmValue[captureSlots.Length];
        for (var i = 0; i < captureSlots.Length; i++) captures[i] = dst.OwningState.Register(captureSlots[i]);
        try
        {
            while (stream.TryNext(ref item))
            {
                if (!evaluator.TryEvaluateStreamEntry(weightEntryAddress, itemSlot, ref item, captures, ref weightValue))
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

                list[target] = items[selected];
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

    private static void GesVmStreamMinMax(ref GesVmValue dst, ref GesVmValue iterator, ushort itemSlot, ushort projectionEntryAddress, IGesVmStreamEntryEvaluator evaluator, bool isMax)
    {
        if (iterator is not { Kind: Stream, ObjectValue: IGesVmStream stream })
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