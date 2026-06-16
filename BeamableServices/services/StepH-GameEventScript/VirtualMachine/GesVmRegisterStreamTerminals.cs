using System;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmRegisterStreamTerminals
{
    internal static void GesVmCount(this GesVmState state, ushort destinationRegister, in GesVmValue iterator)
    {
        switch (iterator.Kind)
        {
            case List when iterator.ObjectValue is GesVmValue[] list:
                state.SetInteger(destinationRegister, list.Length);
                return;
            case Map when iterator.ObjectValue is GesVmValueMap map:
                state.SetInteger(destinationRegister, map.Length);
                return;
            case Dice when iterator.ObjectValue is int[] dice:
                state.SetInteger(destinationRegister, dice.Length);
                return;
            case GameEventScriptBytecodeTypeKind.Range when iterator.ObjectValue is GesVmValueRangeInteger range:
                state.SetInteger(destinationRegister, GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step));
                return;
            case GameEventScriptBytecodeTypeKind.Range when iterator.ObjectValue is GesVmValueRangeFloat range:
                state.SetInteger(destinationRegister, GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step));
                return;
            case Vector or Point when iterator.ObjectValue is GesVmValueVectorPoint:
                state.SetInteger(destinationRegister, 3);
                return;
            case Text or Tag:
                state.SetInteger(destinationRegister, iterator.TextValue.Length);
                return;
        }

        IGesVmStream stream;
        if (iterator is { Kind: Stream, ObjectValue: IGesVmStream sourceStream }) stream = sourceStream;
        else if (!iterator.TryCreateStream(out stream))
        {
            state.SetNothing(destinationRegister);
            return;
        }

        long count = 0;
        var item = state.CreateNothing();
        try
        {
            while (stream.TryNext(ref item)) count++;
            state.SetInteger(destinationRegister, count);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }

    internal static void GesVmSum(this GesVmState state, ushort destinationRegister, in GesVmValue iterator)
    {
        var result = state.CreateNothing();
        switch (iterator.Kind)
        {
            case List when iterator.ObjectValue is GesVmValue[] list:
            {
                if (list.Length == 0)
                {
                    state.SetFloat(destinationRegister, 0d);
                    return;
                }

                var listSum = list[0];
                var listNext = state.CreateNothing();
                for (var i = 1; i < list.Length; i++)
                {
                    listNext.GesVmAdd(ref listSum, ref list[i], ref state.Binary.TextConstantTable);
                    listSum = listNext;
                }

                state.SetValue(destinationRegister, in listSum);
                return;
            }
            case Map when iterator.ObjectValue is GesVmValueMap map:
            {
                var values = map.ValueList;
                if (values.Length == 0)
                {
                    state.SetFloat(destinationRegister, 0d);
                    return;
                }

                var mapSum = values[0];
                var mapNext = state.CreateNothing();
                for (var i = 1; i < values.Length; i++)
                {
                    mapNext.GesVmAdd(ref mapSum, ref values[i], ref state.Binary.TextConstantTable);
                    mapSum = mapNext;
                }

                state.SetValue(destinationRegister, in mapSum);
                return;
            }
            case Dice when iterator.ObjectValue is int[] dice:
            {
                if (dice.Length == 0)
                {
                    state.SetFloat(destinationRegister, 0d);
                    return;
                }

                long diceSum = 0;
                for (var i = 0; i < dice.Length; i++) diceSum += dice[i];
                state.SetInteger(destinationRegister, diceSum);
                return;
            }
            case GameEventScriptBytecodeTypeKind.Range when iterator.ObjectValue is GesVmValueRangeInteger range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
                if (length <= 0)
                {
                    state.SetFloat(destinationRegister, 0d);
                    return;
                }

                long rangeSum = 0;
                for (long i = 1; i <= length; i++)
                {
                    if (GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, i, out var value)) rangeSum += value;
                }

                state.SetInteger(destinationRegister, rangeSum);
                return;
            }
            case GameEventScriptBytecodeTypeKind.Range when iterator.ObjectValue is GesVmValueRangeFloat range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
                if (length <= 0)
                {
                    state.SetFloat(destinationRegister, 0d);
                    return;
                }

                var floatRangeSum = 0d;
                for (long i = 1; i <= length; i++)
                {
                    if (GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, i, out var value)) floatRangeSum += value;
                }

                state.SetFloat(destinationRegister, floatRangeSum);
                return;
            }
        }

        if (iterator is not { Kind: Stream, ObjectValue: IGesVmStream stream })
        {
            state.SetNothing(destinationRegister);
            return;
        }

        var item = state.CreateNothing();
        var sum = state.CreateNothing();
        var next = state.CreateNothing();
        try
        {
            if (!stream.TryNext(ref sum))
            {
                state.SetFloat(destinationRegister, 0d);
                return;
            }

            while (stream.TryNext(ref item))
            {
                next.GesVmAdd(ref sum, ref item, ref state.Binary.TextConstantTable);
                sum = next;
            }

            result = sum;
            state.SetValue(destinationRegister, in result);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }

    internal static void GesVmAverage(this GesVmState state, ushort destinationRegister, in GesVmValue iterator)
    {
        var result = state.CreateNothing();
        switch (iterator.Kind)
        {
            case List when iterator.ObjectValue is GesVmValue[] list:
            {
                if (list.Length == 0)
                {
                    state.SetNothing(destinationRegister);
                    return;
                }

                var listSum = list[0];
                var listNext = state.CreateNothing();
                for (var i = 1; i < list.Length; i++)
                {
                    listNext.GesVmAdd(ref listSum, ref list[i], ref state.Binary.TextConstantTable);
                    listSum = listNext;
                }

                var countValue = state.CreateInteger(list.Length);
                listNext.GesVmDivide(ref listSum, ref countValue, ref state.Binary.TextConstantTable);
                state.SetValue(destinationRegister, in listNext);
                return;
            }
            case Map when iterator.ObjectValue is GesVmValueMap map:
            {
                var values = map.ValueList;
                if (values.Length == 0)
                {
                    state.SetNothing(destinationRegister);
                    return;
                }

                var mapSum = values[0];
                var mapNext = state.CreateNothing();
                for (var i = 1; i < values.Length; i++)
                {
                    mapNext.GesVmAdd(ref mapSum, ref values[i], ref state.Binary.TextConstantTable);
                    mapSum = mapNext;
                }

                var countValue = state.CreateInteger(values.Length);
                mapNext.GesVmDivide(ref mapSum, ref countValue, ref state.Binary.TextConstantTable);
                state.SetValue(destinationRegister, in mapNext);
                return;
            }
            case Dice when iterator.ObjectValue is int[] dice:
            {
                if (dice.Length == 0)
                {
                    state.SetNothing(destinationRegister);
                    return;
                }

                long diceSum = 0;
                for (var i = 0; i < dice.Length; i++) diceSum += dice[i];
                state.SetFloat(destinationRegister, (double)diceSum / dice.Length);
                return;
            }
            case GameEventScriptBytecodeTypeKind.Range when iterator.ObjectValue is GesVmValueRangeInteger range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
                if (length <= 0)
                {
                    state.SetNothing(destinationRegister);
                    return;
                }

                long rangeSum = 0;
                for (long i = 1; i <= length; i++)
                {
                    if (GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, i, out var value)) rangeSum += value;
                }

                state.SetFloat(destinationRegister, (double)rangeSum / length);
                return;
            }
            case GameEventScriptBytecodeTypeKind.Range when iterator.ObjectValue is GesVmValueRangeFloat range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
                if (length <= 0)
                {
                    state.SetNothing(destinationRegister);
                    return;
                }

                var floatRangeSum = 0d;
                for (long i = 1; i <= length; i++)
                {
                    if (GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, i, out var value)) floatRangeSum += value;
                }

                state.SetFloat(destinationRegister, floatRangeSum / length);
                return;
            }
        }

        IGesVmStream stream;
        if (iterator is { Kind: Stream, ObjectValue: IGesVmStream sourceStream }) stream = sourceStream;
        else if (!iterator.TryCreateStream(out stream))
        {
            state.SetNothing(destinationRegister);
            return;
        }

        long count = 0;
        var item = state.CreateNothing();
        var sum = state.CreateNothing();
        var next = state.CreateNothing();
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

                next.GesVmAdd(ref sum, ref item, ref state.Binary.TextConstantTable);
                sum = next;
            }

            if (count == 0)
            {
                state.SetNothing(destinationRegister);
                return;
            }

            var countValue = state.CreateInteger(count);
            next.GesVmDivide(ref sum, ref countValue, ref state.Binary.TextConstantTable);
            result = next;
            state.SetValue(destinationRegister, in result);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }

    internal static void GesVmStreamMin(this GesVmState state, ushort destinationRegister, in GesVmValue iterator, ushort itemSlot, ushort projectionEntryAddress, IGesVmStreamEntryEvaluator evaluator)
    {
        GesVmStreamMinMax(state, destinationRegister, in iterator, itemSlot, projectionEntryAddress, evaluator, isMax: false);
    }

    internal static void GesVmStreamMax(this GesVmState state, ushort destinationRegister, in GesVmValue iterator, ushort itemSlot, ushort projectionEntryAddress, IGesVmStreamEntryEvaluator evaluator)
    {
        GesVmStreamMinMax(state, destinationRegister, in iterator, itemSlot, projectionEntryAddress, evaluator, isMax: true);
    }

    internal static void GesVmStreamOneWeighted(this GesVmState state, ushort destinationRegister, in GesVmValue iterator, ushort itemSlot, ushort weightEntryAddress, ushort captureSlotListIndex, IGesVmStreamEntryEvaluator evaluator,
        GesVmXoshiroRandom randomGenerator)
    {
        if (iterator is not { Kind: Stream, ObjectValue: IGesVmStream stream })
        {
            state.SetNothing(destinationRegister);
            return;
        }

        var item = state.CreateNothing();
        var weightValue = state.CreateNothing();
        var items = new GesVmValue[16];
        var weights = new double[16];
        var itemCount = 0;
        double totalWeight = 0d;
        var captureSlots = state.Binary.Uint16ConstantTable.Resolve(captureSlotListIndex);
        var captures = captureSlots.Length == 0 ? [] : new GesVmValue[captureSlots.Length];
        for (var i = 0; i < captureSlots.Length; i++) captures[i] = state.Register(captureSlots[i]);
        try
        {
            while (stream.TryNext(ref item))
            {
                if (!evaluator.TryEvaluateStreamEntry(weightEntryAddress, itemSlot, ref item, captures, ref weightValue))
                {
                    state.SetNothing(destinationRegister);
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
                state.SetNothing(destinationRegister);
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

            state.SetValue(destinationRegister, in items[selected]);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }

    internal static void GesVmStreamTakeWeighted(this GesVmState state, ushort destinationRegister, in GesVmValue iterator, short count, ushort itemSlot, ushort weightEntryAddress, ushort captureSlotListIndex, IGesVmStreamEntryEvaluator evaluator,
        GesVmXoshiroRandom randomGenerator)
    {
        if (iterator is not { Kind: Stream, ObjectValue: IGesVmStream stream })
        {
            state.SetNothing(destinationRegister);
            return;
        }

        if (count <= 0)
        {
            if (stream is IDisposable disposable) disposable.Dispose();
            state.SetList(destinationRegister, state.EmptyList);
            return;
        }

        var item = state.CreateNothing();
        var weightValue = state.CreateNothing();
        var items = new GesVmValue[16];
        var weights = new double[16];
        var itemCount = 0;
        double totalWeight = 0d;
        var captureSlots = state.Binary.Uint16ConstantTable.Resolve(captureSlotListIndex);
        var captures = captureSlots.Length == 0 ? [] : new GesVmValue[captureSlots.Length];
        for (var i = 0; i < captureSlots.Length; i++) captures[i] = state.Register(captureSlots[i]);
        try
        {
            while (stream.TryNext(ref item))
            {
                if (!evaluator.TryEvaluateStreamEntry(weightEntryAddress, itemSlot, ref item, captures, ref weightValue))
                {
                    state.SetNothing(destinationRegister);
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
                state.SetList(destinationRegister, state.EmptyList);
                return;
            }

            var selectedCount = count < itemCount ? count : itemCount;
            var list = state.CreateList(selectedCount);
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

            state.SetList(destinationRegister, list);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }

    private static void GesVmStreamMinMax(GesVmState state, ushort destinationRegister, in GesVmValue iterator, ushort itemSlot, ushort projectionEntryAddress, IGesVmStreamEntryEvaluator evaluator, bool isMax)
    {
        if (iterator is not { Kind: Stream, ObjectValue: IGesVmStream stream })
        {
            state.SetNothing(destinationRegister);
            return;
        }

        var item = state.CreateNothing();
        var projection = state.CreateNothing();
        var winner = state.CreateNothing();
        var winnerProjection = state.CreateNothing();
        var hasWinner = false;
        try
        {
            while (stream.TryNext(ref item))
            {
                if (!evaluator.TryEvaluateStreamEntry(projectionEntryAddress, itemSlot, ref item, null, ref projection))
                {
                    state.SetNothing(destinationRegister);
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

            if (hasWinner) state.SetValue(destinationRegister, in winner);
            else state.SetNothing(destinationRegister);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }
}
