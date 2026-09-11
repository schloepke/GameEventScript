// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using GameEventScript.Runtime;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace GameEventScript.Runtime.VM;

internal static class GesVmRegisterTakeDrop
{
    internal static void GesVmTakeFirst(this GesVmState vmState, ushort destinationRegister, in GesValue source, short count)
    {
        GesValue result;
        switch (source.Kind)
        {
            case Series when source.ObjectValue is GesSeries series:
                result = TakeFirstSeries(vmState, series, count);
                break;
            case List when source.ObjectValue is GesValue[] list:
                result = TakeFirstList(vmState, list, count);
                break;
            case Dice when source.ObjectValue is int[] dice:
                result = TakeFirstDice(vmState, dice, count);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesValueRangeInteger range:
                result = TakeFirstRange(vmState, range, count);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesValueRangeFloat range:
                result = TakeFirstRange(vmState, range, count);
                break;
            case Iterator when source.ObjectValue is IGesIterator iterator:
                result = TakeFirstIterator(vmState, iterator, count);
                break;
            default:
                result = new GesValue();
                break;
        }

        vmState.SetValue(destinationRegister, in result);
    }
    internal static void GesVmOneRandom(this GesVmState vmState, ushort destinationRegister, in GesValue source, GameEventScriptRandomGenerator randomGenerator)
    {
        GesValue result;
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesValue[] list:
                result = list.Length > 0 ? list[randomGenerator.NextInclusiveInteger(0, list.Length - 1)] : new GesValue();
                break;
            case Dice when source.ObjectValue is int[] dice:
                result = new GesValue();
                if (dice.Length > 0) result.SetInteger(dice[randomGenerator.NextInclusiveInteger(0, dice.Length - 1)]);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesValueRangeInteger range:
                result = OneRandomRange(range, randomGenerator);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesValueRangeFloat range:
                result = OneRandomRange(range, randomGenerator);
                break;
            case Iterator when source.ObjectValue is IGesIterator iterator:
                result = OneRandomIterator(iterator, randomGenerator);
                break;
            default:
                result = new GesValue();
                break;
        }

        vmState.SetValue(destinationRegister, in result);
    }
    internal static void GesVmTakeRandom(this GesVmState vmState, ushort destinationRegister, in GesValue source, short count, GameEventScriptRandomGenerator randomGenerator)
    {
        GesValue result;
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesValue[] list:
                result = TakeRandomList(vmState, list, count, randomGenerator);
                break;
            case Dice when source.ObjectValue is int[] dice:
                result = TakeRandomDice(dice, count, randomGenerator);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesValueRangeInteger range:
                result = TakeRandomRange(vmState, range, count, randomGenerator);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesValueRangeFloat range:
                result = TakeRandomRange(vmState, range, count, randomGenerator);
                break;
            case Iterator when source.ObjectValue is IGesIterator iterator:
                result = TakeRandomIterator(vmState, iterator, count, randomGenerator);
                break;
            default:
                result = new GesValue();
                break;
        }

        vmState.SetValue(destinationRegister, in result);
    }

    internal static void GesVmOneWeighted(this GesVmState vmState, ushort destinationRegister, in GesValue itemsValue, in GesValue weightsValue, GameEventScriptRandomGenerator randomGenerator)
    {
        if (itemsValue is not { Kind: List, ObjectValue: GesValue[] items } ||
            weightsValue is not { Kind: List, ObjectValue: GesValue[] weights } ||
            items.Length == 0 ||
            weights.Length < items.Length)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        double totalWeight = 0d;
        for (var i = 0; i < items.Length; i++)
        {
            var weight = weights[i].AsNumeric;
            if (!double.IsFinite(weight) || weight <= 0d)
            {
                vmState.SetNothing(destinationRegister);
                return;
            }

            totalWeight += weight;
        }

        if (totalWeight <= 0d)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        var threshold = randomGenerator.NextFloat(0d, totalWeight);
        double cumulative = 0d;
        var selected = items.Length - 1;
        for (var i = 0; i < items.Length; i++)
        {
            cumulative += weights[i].AsNumeric;
            if (threshold < cumulative)
            {
                selected = i;
                break;
            }
        }

        vmState.SetValue(destinationRegister, in items[selected]);
    }

    internal static void GesVmTakeWeighted(this GesVmState vmState, ushort destinationRegister, in GesValue itemsValue, in GesValue weightsValue, short count, GameEventScriptRandomGenerator randomGenerator)
    {
        if (itemsValue is not { Kind: List, ObjectValue: GesValue[] items } ||
            weightsValue is not { Kind: List, ObjectValue: GesValue[] weights } ||
            weights.Length < items.Length)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (count <= 0 || items.Length == 0)
        {
            vmState.SetList(destinationRegister, vmState.EmptyList);
            return;
        }

        var itemCount = items.Length;
        var itemBuffer = new GesValue[itemCount];
        var weightBuffer = new double[itemCount];
        double totalWeight = 0d;
        for (var i = 0; i < itemCount; i++)
        {
            var weight = weights[i].AsNumeric;
            if (!double.IsFinite(weight) || weight <= 0d)
            {
                vmState.SetList(destinationRegister, vmState.EmptyList);
                return;
            }

            itemBuffer[i] = items[i];
            weightBuffer[i] = weight;
            totalWeight += weight;
        }

        if (totalWeight <= 0d)
        {
            vmState.SetList(destinationRegister, vmState.EmptyList);
            return;
        }

        var selectedCount = count < itemCount ? count : itemCount;
        var list = new GesValue[selectedCount];
        var remainingCount = itemCount;
        for (var target = 0; target < selectedCount && remainingCount > 0 && totalWeight > 0d; target++)
        {
            var threshold = randomGenerator.NextFloat(0d, totalWeight);
            double cumulative = 0d;
            var selected = remainingCount - 1;
            for (var i = 0; i < remainingCount; i++)
            {
                cumulative += weightBuffer[i];
                if (threshold < cumulative)
                {
                    selected = i;
                    break;
                }
            }

            list[target] = itemBuffer[selected];
            totalWeight -= weightBuffer[selected];
            if (selected < remainingCount - 1)
            {
                Array.Copy(itemBuffer, selected + 1, itemBuffer, selected, remainingCount - selected - 1);
                Array.Copy(weightBuffer, selected + 1, weightBuffer, selected, remainingCount - selected - 1);
            }

            remainingCount--;
        }

        vmState.SetList(destinationRegister, list);
    }

    internal static void GesVmDropFirst(this GesVmState vmState, ushort destinationRegister, in GesValue source, short count)
    {
        GesValue result;
        switch (source.Kind)
        {
            case Series when source.ObjectValue is GesSeries series:
                result = new GesValue();
                result.SetSeries(count <= 0 ? series : series.Drop(count));
                break;
            case List when source.ObjectValue is GesValue[] list:
                result = DropFirstList(list, count);
                break;
            case Dice when source.ObjectValue is int[] dice:
                result = DropFirstDice(dice, count);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesValueRangeInteger range:
                result = DropFirstRange(in source, range, count);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesValueRangeFloat range:
                result = DropFirstRange(in source, range, count);
                break;
            case Iterator when source.ObjectValue is IGesIterator iterator:
                result = DropFirstIterator(vmState, iterator, count);
                break;
            default:
                result = new GesValue();
                break;
        }

        vmState.SetValue(destinationRegister, in result);
    }
    internal static void GesVmTakeLast(this GesVmState vmState, ushort destinationRegister, in GesValue source, short count)
    {
        GesValue result;
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesValue[] list:
                result = TakeLastList(vmState, list, count);
                break;
            case Dice when source.ObjectValue is int[] dice:
                result = TakeLastDice(dice, count);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesValueRangeInteger range:
                result = TakeLastRange(vmState, range, count);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesValueRangeFloat range:
                result = TakeLastRange(vmState, range, count);
                break;
            case Iterator when source.ObjectValue is IGesIterator iterator:
                result = TakeLastIterator(vmState, iterator, count);
                break;
            default:
                result = new GesValue();
                break;
        }

        vmState.SetValue(destinationRegister, in result);
    }
    internal static void GesVmDropLast(this GesVmState vmState, ushort destinationRegister, in GesValue source, short count)
    {
        GesValue result;
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesValue[] list:
                result = DropLastList(list, count);
                break;
            case Dice when source.ObjectValue is int[] dice:
                result = DropLastDice(dice, count);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesValueRangeInteger range:
                result = DropLastRange(in source, range, count);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesValueRangeFloat range:
                result = DropLastRange(in source, range, count);
                break;
            case Iterator when source.ObjectValue is IGesIterator iterator:
                result = DropLastIterator(vmState, iterator, count);
                break;
            default:
                result = new GesValue();
                break;
        }

        vmState.SetValue(destinationRegister, in result);
    }
    internal static void GesVmTakeHighest(this GesVmState vmState, ushort destinationRegister, in GesValue source, short count)
    {
        GesValue result;
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesValue[] list:
                result = TakeExtremeList(vmState, list, count, true);
                break;
            case Dice when source.ObjectValue is int[] dice:
                result = TakeFirstDice(vmState, dice, count);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesValueRangeInteger range:
                result = TakeExtremeRange(vmState, range, count, true);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesValueRangeFloat range:
                result = TakeExtremeRange(vmState, range, count, true);
                break;
            case Iterator when source.ObjectValue is IGesIterator iterator:
                result = TakeExtremeIterator(vmState, iterator, count, true);
                break;
            default:
                result = new GesValue();
                break;
        }

        vmState.SetValue(destinationRegister, in result);
    }
    internal static void GesVmTakeLowest(this GesVmState vmState, ushort destinationRegister, in GesValue source, short count)
    {
        GesValue result;
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesValue[] list:
                result = TakeExtremeList(vmState, list, count, false);
                break;
            case Dice when source.ObjectValue is int[] dice:
                result = TakeLastDice(dice, count);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesValueRangeInteger range:
                result = TakeExtremeRange(vmState, range, count, false);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesValueRangeFloat range:
                result = TakeExtremeRange(vmState, range, count, false);
                break;
            case Iterator when source.ObjectValue is IGesIterator iterator:
                result = TakeExtremeIterator(vmState, iterator, count, false);
                break;
            default:
                result = new GesValue();
                break;
        }

        vmState.SetValue(destinationRegister, in result);
    }
    internal static void GesVmDropHighest(this GesVmState vmState, ushort destinationRegister, in GesValue source, short count)
    {
        GesValue result;
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesValue[] list:
                result = DropExtremeList(vmState, list, count, true);
                break;
            case Dice when source.ObjectValue is int[] dice:
                result = DropFirstDice(dice, count);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesValueRangeInteger range:
                result = DropExtremeRange(in source, range, count, true);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesValueRangeFloat range:
                result = DropExtremeRange(in source, range, count, true);
                break;
            case Iterator when source.ObjectValue is IGesIterator iterator:
                result = DropExtremeIterator(vmState, iterator, count, true);
                break;
            default:
                result = new GesValue();
                break;
        }

        vmState.SetValue(destinationRegister, in result);
    }
    internal static void GesVmDropLowest(this GesVmState vmState, ushort destinationRegister, in GesValue source, short count)
    {
        GesValue result;
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesValue[] list:
                result = DropExtremeList(vmState, list, count, false);
                break;
            case Dice when source.ObjectValue is int[] dice:
                result = DropLastDice(dice, count);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesValueRangeInteger range:
                result = DropExtremeRange(in source, range, count, false);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesValueRangeFloat range:
                result = DropExtremeRange(in source, range, count, false);
                break;
            case Iterator when source.ObjectValue is IGesIterator iterator:
                result = DropExtremeIterator(vmState, iterator, count, false);
                break;
            default:
                result = new GesValue();
                break;
        }

        vmState.SetValue(destinationRegister, in result);
    }
    private static GesValue TakeFirstSeries(GesVmState vmState, GesSeries series, short count)
    {
        var dst = new GesValue();
        if (count <= 0)
        {
            dst.SetList(vmState.EmptyList);
            return dst;
        }

        var list = new GesValue[count];
        for (var i = 0; i < count; i++) list[i] = series.GetTerm(i);
        dst.SetList(list);
        return dst;
    }
    private static GesValue TakeFirstList(GesVmState vmState, GesValue[] source, short count)
    {
        var dst = new GesValue();
        if (count <= 0)
        {
            dst.SetList(vmState.EmptyList);
            return dst;
        }

        var length = count < source.Length ? count : source.Length;
        var list = new GesValue[length];
        for (var i = 0; i < length; i++) list[i] = source[i];
        dst.SetList(list);
        return dst;
    }
    private static GesValue DropFirstList(GesValue[] source, short count)
    {
        var dst = new GesValue();
        if (count <= 0)
        {
            dst.SetList(source);
            return dst;
        }

        var start = count < source.Length ? count : source.Length;
        var length = source.Length - start;
        var list = new GesValue[length];
        for (var i = 0; i < length; i++) list[i] = source[start + i];
        dst.SetList(list);
        return dst;
    }
    private static GesValue TakeLastList(GesVmState vmState, GesValue[] source, short count)
    {
        var dst = new GesValue();
        if (count <= 0)
        {
            dst.SetList(vmState.EmptyList);
            return dst;
        }

        var length = count < source.Length ? count : source.Length;
        var start = source.Length - length;
        var list = new GesValue[length];
        for (var i = 0; i < length; i++) list[i] = source[start + i];
        dst.SetList(list);
        return dst;
    }
    private static GesValue DropLastList(GesValue[] source, short count)
    {
        var dst = new GesValue();
        if (count <= 0)
        {
            dst.SetList(source);
            return dst;
        }

        var length = count < source.Length ? source.Length - count : 0;
        var list = new GesValue[length];
        for (var i = 0; i < length; i++) list[i] = source[i];
        dst.SetList(list);
        return dst;
    }
    private static GesValue TakeFirstDice(GesVmState vmState, int[] source, short count)
    {
        var dst = new GesValue();
        if (count <= 0)
        {
            dst.SetDice([]);
            return dst;
        }

        var length = count < source.Length ? count : source.Length;
        var dice = new int[length];
        for (var i = 0; i < length; i++) dice[i] = source[i];
        dst.SetDice(dice);
        return dst;
    }
    private static GesValue DropFirstDice(int[] source, short count)
    {
        var dst = new GesValue();
        if (count <= 0)
        {
            dst.SetDice(source);
            return dst;
        }

        var start = count < source.Length ? count : source.Length;
        var length = source.Length - start;
        var dice = new int[length];
        for (var i = 0; i < length; i++) dice[i] = source[start + i];
        dst.SetDice(dice);
        return dst;
    }
    private static GesValue TakeLastDice(int[] source, short count)
    {
        var dst = new GesValue();
        if (count <= 0)
        {
            dst.SetDice([]);
            return dst;
        }

        var length = count < source.Length ? count : source.Length;
        var start = source.Length - length;
        var dice = new int[length];
        for (var i = 0; i < length; i++) dice[i] = source[start + i];
        dst.SetDice(dice);
        return dst;
    }
    private static GesValue DropLastDice(int[] source, short count)
    {
        var dst = new GesValue();
        if (count <= 0)
        {
            dst.SetDice(source);
            return dst;
        }

        var length = count < source.Length ? source.Length - count : 0;
        var dice = new int[length];
        for (var i = 0; i < length; i++) dice[i] = source[i];
        dst.SetDice(dice);
        return dst;
    }
    private static GesValue TakeExtremeList(GesVmState vmState, GesValue[] source, short count, bool highest)
    {
        var dst = new GesValue();
        if (count <= 0 || source.Length == 0)
        {
            dst.SetList(vmState.EmptyList);
            return dst;
        }

        var length = count < source.Length ? count : source.Length;
        var selected = new bool[source.Length];
        var list = new GesValue[length];
        for (var i = 0; i < length; i++)
        {
            var best = -1;
            for (var j = 0; j < source.Length; j++)
            {
                if (selected[j]) continue;
                if (best < 0)
                {
                    best = j;
                    continue;
                }

                var comparison = CompareForOrdering(in source[j], in source[best]);
                if (highest ? comparison > 0 : comparison < 0) best = j;
            }

            selected[best] = true;
            list[i] = source[best];
        }

        dst.SetList(list);
        return dst;
    }
    private static GesValue DropExtremeList(GesVmState vmState, GesValue[] source, short count, bool highest)
    {
        var dst = new GesValue();
        if (count <= 0)
        {
            dst.SetList(source);
            return dst;
        }

        var selectedCount = count < source.Length ? count : source.Length;
        if (selectedCount == source.Length)
        {
            dst.SetList(vmState.EmptyList);
            return dst;
        }

        var selected = new bool[source.Length];
        SelectExtremeItems(source, source.Length, selected, selectedCount, highest);
        var list = new GesValue[source.Length - selectedCount];
        var target = 0;
        for (var i = 0; i < source.Length; i++)
        {
            if (selected[i]) continue;
            list[target++] = source[i];
        }

        dst.SetList(list);
        return dst;
    }
    private static void SelectExtremeItems(GesValue[] items, int length, bool[] selected, int selectedCount, bool highest)
    {
        for (var i = 0; i < selectedCount; i++)
        {
            var best = -1;
            for (var j = 0; j < length; j++)
            {
                if (selected[j]) continue;
                if (best < 0)
                {
                    best = j;
                    continue;
                }

                var comparison = CompareForOrdering(in items[j], in items[best]);
                if (highest ? comparison > 0 : comparison < 0) best = j;
            }

            selected[best] = true;
        }
    }
    private static GesValue TakeFirstIterator(GesVmState vmState, IGesIterator iterator, short count)
    {
        var dst = new GesValue();
        if (count <= 0)
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
            dst.SetList(vmState.EmptyList);
            return dst;
        }

        var buffer = new GesVmListBuilder(count < 16 ? count : 16);
        try
        {
            GesIteratorResult item;
            while (buffer.Count < count && (item = iterator.Next()).HasValue)
            {
                buffer.Add(in item.Value);
            }

            return ListFromBuffer(vmState, buffer.Items, buffer.Count);
        }
        finally
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
        }
    }
    private static GesValue DropFirstIterator(GesVmState vmState, IGesIterator iterator, short count)
    {
        var skipped = 0;
        var buffer = new GesVmListBuilder(16);
        try
        {
            GesIteratorResult item;
            while (count > 0 && skipped < count && (item = iterator.Next()).HasValue) skipped++;
            while ((item = iterator.Next()).HasValue)
            {
                buffer.Add(in item.Value);
            }

            return ListFromBuffer(vmState, buffer.Items, buffer.Count);
        }
        finally
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
        }
    }
    private static GesValue TakeLastIterator(GesVmState vmState, IGesIterator iterator, short count)
    {
        var dst = new GesValue();
        if (count <= 0)
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
            dst.SetList(vmState.EmptyList);
            return dst;
        }

        var buffer = new GesVmListBuilder(16);
        try
        {
            GesIteratorResult item;
            while ((item = iterator.Next()).HasValue)
            {
                buffer.Add(in item.Value);
            }

            var length = count < buffer.Count ? count : buffer.Count;
            var start = buffer.Count - length;
            if (length == 0)
            {
                dst.SetList(vmState.EmptyList);
                return dst;
            }

            var list = buffer.ToList(start, length);
            dst.SetList(list);
            return dst;
        }
        finally
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
        }
    }
    private static GesValue DropLastIterator(GesVmState vmState, IGesIterator iterator, short count)
    {
        var buffer = new GesVmListBuilder(16);
        try
        {
            GesIteratorResult item;
            while ((item = iterator.Next()).HasValue)
            {
                buffer.Add(in item.Value);
            }

            var length = count <= 0 ? buffer.Count : count < buffer.Count ? buffer.Count - count : 0;
            return ListFromBuffer(vmState, buffer.Items, length);
        }
        finally
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
        }
    }
    private static GesValue TakeExtremeIterator(GesVmState vmState, IGesIterator iterator, short count, bool highest)
    {
        var dst = new GesValue();
        if (count <= 0)
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
            dst.SetList(vmState.EmptyList);
            return dst;
        }

        var buffer = new GesValue[count];
        var itemCount = 0;
        try
        {
            GesIteratorResult item;
            while ((item = iterator.Next()).HasValue)
            {
                if (itemCount < count)
                {
                    itemCount = InsertExtreme(buffer, in item.Value, itemCount, highest);
                    continue;
                }

                var comparison = CompareForOrdering(in item.Value, in buffer[count - 1]);
                if (highest ? comparison > 0 : comparison < 0)
                {
                    itemCount--;
                    itemCount = InsertExtreme(buffer, in item.Value, itemCount, highest);
                }
            }

            return ListFromBuffer(vmState, buffer, itemCount);
        }
        finally
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
        }
    }
    private static GesValue DropExtremeIterator(GesVmState vmState, IGesIterator iterator, short count, bool highest)
    {
        var dst = new GesValue();
        var buffer = new GesVmListBuilder(16);
        try
        {
            GesIteratorResult item;
            while ((item = iterator.Next()).HasValue)
            {
                buffer.Add(in item.Value);
            }

            if (count <= 0)
            {
                return ListFromBuffer(vmState, buffer.Items, buffer.Count);
            }

            var selectedCount = count < buffer.Count ? count : buffer.Count;
            if (selectedCount == buffer.Count)
            {
                dst.SetList(vmState.EmptyList);
                return dst;
            }

            var selected = new bool[buffer.Count];
            SelectExtremeItems(buffer.Items, buffer.Count, selected, selectedCount, highest);
            var list = new GesValue[buffer.Count - selectedCount];
            var target = 0;
            for (var i = 0; i < buffer.Count; i++)
            {
                if (selected[i]) continue;
                list[target++] = buffer[i];
            }

            dst.SetList(list);
            return dst;
        }
        finally
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
        }
    }
    private static GesValue OneRandomRange(GesValueRangeInteger valueRangeInteger, GameEventScriptRandomGenerator randomGenerator)
    {
        var dst = new GesValue();
        var length = GameEventScriptRangeMath.GetLength(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step);
        if (length <= 0)
        {
            dst.SetNothing();
            return dst;
        }

        var index = randomGenerator.NextInclusiveInteger(0, length - 1L);
        if (GameEventScriptRangeMath.GetTerm(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step, index + 1L) is { } value) dst.SetInteger(value);
        else dst.SetNothing();
        return dst;
    }
    private static GesValue OneRandomRange(GesValueRangeFloat range, GameEventScriptRandomGenerator randomGenerator)
    {
        var dst = new GesValue();
        var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
        if (length <= 0)
        {
            dst.SetNothing();
            return dst;
        }

        var index = randomGenerator.NextInclusiveInteger(0, length - 1L);
        if (GameEventScriptRangeMath.GetTerm(range.From, range.To, range.Step, index + 1L) is { } value) dst.SetFloat(value);
        else dst.SetNothing();
        return dst;
    }
    private static GesValue OneRandomIterator(IGesIterator iterator, GameEventScriptRandomGenerator randomGenerator)
    {
        var chosen = new GesValue();
        var count = 0;
        try
        {
            GesIteratorResult item;
            while ((item = iterator.Next()).HasValue)
            {
                count++;
                if (randomGenerator.NextInclusiveInteger(1, count) == 1) chosen = item.Value;
            }

            return count > 0 ? chosen : new GesValue();
        }
        finally
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
        }
    }
    private static GesValue TakeRandomList(GesVmState vmState, GesValue[] source, short count, GameEventScriptRandomGenerator randomGenerator)
    {
        var dst = new GesValue();
        if (count <= 0 || source.Length == 0)
        {
            dst.SetList(vmState.EmptyList);
            return dst;
        }

        var length = count < source.Length ? count : source.Length;
        var indices = CreateShuffledPrefix(source.Length, length, randomGenerator);
        var list = new GesValue[length];
        for (var i = 0; i < length; i++) list[i] = source[indices[i]];
        dst.SetList(list);
        return dst;
    }
    private static GesValue TakeRandomDice(int[] source, short count, GameEventScriptRandomGenerator randomGenerator)
    {
        var dst = new GesValue();
        if (count <= 0 || source.Length == 0)
        {
            dst.SetDice([]);
            return dst;
        }

        var length = count < source.Length ? count : source.Length;
        var indices = CreateShuffledPrefix(source.Length, length, randomGenerator);
        var dice = new int[length];
        for (var i = 0; i < length; i++) dice[i] = source[indices[i]];
        dst.SetDice(dice);
        return dst;
    }
    private static GesValue TakeRandomRange(GesVmState vmState, GesValueRangeInteger valueRangeInteger, short count, GameEventScriptRandomGenerator randomGenerator)
    {
        var dst = new GesValue();
        var sourceLength = GameEventScriptRangeMath.GetLength(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step);
        if (count <= 0 || sourceLength <= 0)
        {
            dst.SetList(vmState.EmptyList);
            return dst;
        }

        var length = count < sourceLength ? count : checked((int)sourceLength);
        var list = new GesValue[length];
        if (sourceLength <= int.MaxValue)
        {
            var indices = CreateShuffledPrefix(checked((int)sourceLength), length, randomGenerator);
            for (var i = 0; i < length; i++)
            {
                if (GameEventScriptRangeMath.GetTerm(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step, indices[i] + 1L) is { } value) list[i].SetInteger(value);
                else list[i].SetNothing();
            }

            dst.SetList(list);
            return dst;
        }

        var terms = new long[length];
        for (var i = 0; i < length; i++)
        {
            long index;
            var duplicate = false;
            do
            {
                duplicate = false;
                index = randomGenerator.NextInclusiveInteger(0, sourceLength - 1L);
                for (var j = 0; j < i; j++)
                {
                    if (terms[j] == index)
                    {
                        duplicate = true;
                        break;
                    }
                }
            } while (duplicate);

            terms[i] = index;
            if (GameEventScriptRangeMath.GetTerm(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step, index + 1L) is { } value) list[i].SetInteger(value);
            else list[i].SetNothing();
        }

        dst.SetList(list);
        return dst;
    }
    private static GesValue TakeRandomRange(GesVmState vmState, GesValueRangeFloat range, short count, GameEventScriptRandomGenerator randomGenerator)
    {
        var dst = new GesValue();
        var sourceLength = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
        if (count <= 0 || sourceLength <= 0)
        {
            dst.SetList(vmState.EmptyList);
            return dst;
        }

        var length = count < sourceLength ? count : checked((int)sourceLength);
        var list = new GesValue[length];
        if (sourceLength <= int.MaxValue)
        {
            var indices = CreateShuffledPrefix(checked((int)sourceLength), length, randomGenerator);
            for (var i = 0; i < length; i++)
            {
                if (GameEventScriptRangeMath.GetTerm(range.From, range.To, range.Step, indices[i] + 1L) is { } value) list[i].SetFloat(value);
                else list[i].SetNothing();
            }

            dst.SetList(list);
            return dst;
        }

        var terms = new long[length];
        for (var i = 0; i < length; i++)
        {
            long index;
            var duplicate = false;
            do
            {
                duplicate = false;
                index = randomGenerator.NextInclusiveInteger(0, sourceLength - 1L);
                for (var j = 0; j < i; j++)
                {
                    if (terms[j] == index)
                    {
                        duplicate = true;
                        break;
                    }
                }
            } while (duplicate);

            terms[i] = index;
            if (GameEventScriptRangeMath.GetTerm(range.From, range.To, range.Step, index + 1L) is { } value) list[i].SetFloat(value);
            else list[i].SetNothing();
        }

        dst.SetList(list);
        return dst;
    }
    private static GesValue TakeRandomIterator(GesVmState vmState, IGesIterator iterator, short count, GameEventScriptRandomGenerator randomGenerator)
    {
        var dst = new GesValue();
        if (count <= 0)
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
            dst.SetList(vmState.EmptyList);
            return dst;
        }

        var buffer = new GesVmListBuilder(16);
        try
        {
            GesIteratorResult item;
            while ((item = iterator.Next()).HasValue)
            {
                buffer.Add(in item.Value);
            }

            if (buffer.Count == 0)
            {
                dst.SetList(vmState.EmptyList);
                return dst;
            }

            var length = count < buffer.Count ? count : buffer.Count;
            var indices = CreateShuffledPrefix(buffer.Count, length, randomGenerator);
            var list = new GesValue[length];
            for (var i = 0; i < length; i++) list[i] = buffer[indices[i]];
            dst.SetList(list);
            return dst;
        }
        finally
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
        }
    }
    private static int[] CreateShuffledPrefix(int sourceLength, int prefixLength, GameEventScriptRandomGenerator randomGenerator)
    {
        var indices = new int[sourceLength];
        for (var i = 0; i < sourceLength; i++) indices[i] = i;
        var selectedIndices = new int[prefixLength];
        var remainingLength = sourceLength;
        for (var i = 0; i < prefixLength; i++)
        {
            var selected = randomGenerator.NextInclusiveInteger(0, remainingLength - 1);
            var value = indices[selected];
            if (selected < remainingLength - 1)
            {
                Array.Copy(indices, selected + 1, indices, selected, remainingLength - selected - 1);
            }

            selectedIndices[i] = value;
            remainingLength--;
        }

        return selectedIndices;
    }
    private static int InsertExtreme(GesValue[] buffer, in GesValue item, int itemCount, bool highest)
    {
        var index = itemCount;
        while (index > 0)
        {
            var comparison = CompareForOrdering(in item, in buffer[index - 1]);
            if (highest ? comparison <= 0 : comparison >= 0) break;
            buffer[index] = buffer[index - 1];
            index--;
        }

        buffer[index] = item;
        return itemCount + 1;
    }
    private static GesValue ListFromBuffer(GesVmState vmState, GesValue[] buffer, int length)
    {
        var dst = new GesValue();
        if (length == 0)
        {
            dst.SetList(vmState.EmptyList);
            return dst;
        }

        var list = new GesValue[length];
        Array.Copy(buffer, list, length);
        dst.SetList(list);
        return dst;
    }
    private static GesValue TakeFirstRange(GesVmState vmState, GesValueRangeInteger valueRangeInteger, short count)
    {
        var dst = new GesValue();
        var length = GameEventScriptRangeMath.GetLength(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step);
        if (count <= 0 || length <= 0)
        {
            dst.SetRange(0, 0, 0);
            return dst;
        }

        if (count >= length)
        {
            dst.SetRange(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step);
            return dst;
        }

        if (GameEventScriptRangeMath.GetTerm(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step, count) is { } to) dst.SetRange(valueRangeInteger.From, to, valueRangeInteger.Step);
        else dst.SetNothing();
        return dst;
    }
    private static GesValue DropFirstRange(in GesValue source, GesValueRangeInteger valueRangeInteger, short count)
    {
        var dst = new GesValue();
        var length = GameEventScriptRangeMath.GetLength(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step);
        if (count <= 0)
        {
            return source;
        }

        if (count >= length)
        {
            dst.SetRange(0, 0, 0);
            return dst;
        }

        if (GameEventScriptRangeMath.GetTerm(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step, count + 1L) is { } from) dst.SetRange(from, valueRangeInteger.To, valueRangeInteger.Step);
        else dst.SetNothing();
        return dst;
    }
    private static GesValue TakeLastRange(GesVmState vmState, GesValueRangeInteger valueRangeInteger, short count)
    {
        var dst = new GesValue();
        var length = GameEventScriptRangeMath.GetLength(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step);
        if (count <= 0 || length <= 0)
        {
            dst.SetRange(0, 0, 0);
            return dst;
        }

        if (count >= length)
        {
            dst.SetRange(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step);
            return dst;
        }

        if (GameEventScriptRangeMath.GetTerm(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step, length - count + 1L) is { } from) dst.SetRange(from, valueRangeInteger.To, valueRangeInteger.Step);
        else dst.SetNothing();
        return dst;
    }
    private static GesValue DropLastRange(in GesValue source, GesValueRangeInteger valueRangeInteger, short count)
    {
        var dst = new GesValue();
        var length = GameEventScriptRangeMath.GetLength(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step);
        if (count <= 0)
        {
            return source;
        }

        if (count >= length)
        {
            dst.SetRange(0, 0, 0);
            return dst;
        }

        if (GameEventScriptRangeMath.GetTerm(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step, length - count) is { } to) dst.SetRange(valueRangeInteger.From, to, valueRangeInteger.Step);
        else dst.SetNothing();
        return dst;
    }
    private static GesValue TakeFirstRange(GesVmState vmState, GesValueRangeFloat range, short count)
    {
        var dst = new GesValue();
        var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
        if (count <= 0 || length <= 0)
        {
            dst.SetRange(0, 0, 0);
            return dst;
        }

        if (count >= length)
        {
            dst.SetRange(range.From, range.To, range.Step);
            return dst;
        }

        if (GameEventScriptRangeMath.GetTerm(range.From, range.To, range.Step, count) is { } to) dst.SetRange(range.From, to, range.Step);
        else dst.SetNothing();
        return dst;
    }
    private static GesValue DropFirstRange(in GesValue source, GesValueRangeFloat range, short count)
    {
        var dst = new GesValue();
        var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
        if (count <= 0)
        {
            return source;
        }

        if (count >= length)
        {
            dst.SetRange(0, 0, 0);
            return dst;
        }

        if (GameEventScriptRangeMath.GetTerm(range.From, range.To, range.Step, count + 1L) is { } from) dst.SetRange(from, range.To, range.Step);
        else dst.SetNothing();
        return dst;
    }
    private static GesValue TakeLastRange(GesVmState vmState, GesValueRangeFloat range, short count)
    {
        var dst = new GesValue();
        var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
        if (count <= 0 || length <= 0)
        {
            dst.SetRange(0, 0, 0);
            return dst;
        }

        if (count >= length)
        {
            dst.SetRange(range.From, range.To, range.Step);
            return dst;
        }

        if (GameEventScriptRangeMath.GetTerm(range.From, range.To, range.Step, length - count + 1L) is { } from) dst.SetRange(from, range.To, range.Step);
        else dst.SetNothing();
        return dst;
    }
    private static GesValue DropLastRange(in GesValue source, GesValueRangeFloat range, short count)
    {
        var dst = new GesValue();
        var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
        if (count <= 0)
        {
            return source;
        }

        if (count >= length)
        {
            dst.SetRange(0, 0, 0);
            return dst;
        }

        if (GameEventScriptRangeMath.GetTerm(range.From, range.To, range.Step, length - count) is { } to) dst.SetRange(range.From, to, range.Step);
        else dst.SetNothing();
        return dst;
    }
    private static GesValue TakeExtremeRange(GesVmState vmState, GesValueRangeInteger valueRangeInteger, short count, bool highest)
    {
        var dst = new GesValue();
        var length = GameEventScriptRangeMath.GetLength(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step);
        if (count <= 0 || length <= 0 || valueRangeInteger.Step == 0)
        {
            dst.SetRange(0, 0, 0);
            return dst;
        }

        var takeLength = count < length ? count : length;
        if (GameEventScriptRangeMath.GetTerm(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step, length) is not { } last)
        {
            dst.SetNothing();
            return dst;
        }

        var ascending = valueRangeInteger.Step > 0;
        if (highest)
        {
            if (ascending)
            {
                if (GameEventScriptRangeMath.GetTerm(last, valueRangeInteger.From, -valueRangeInteger.Step, takeLength) is { } to) dst.SetRange(last, to, -valueRangeInteger.Step);
                else dst.SetNothing();
            }
            else
            {
                if (GameEventScriptRangeMath.GetTerm(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step, takeLength) is { } to) dst.SetRange(valueRangeInteger.From, to, valueRangeInteger.Step);
                else dst.SetNothing();
            }
        }
        else
        {
            if (ascending)
            {
                if (GameEventScriptRangeMath.GetTerm(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step, takeLength) is { } to) dst.SetRange(valueRangeInteger.From, to, valueRangeInteger.Step);
                else dst.SetNothing();
            }
            else
            {
                if (GameEventScriptRangeMath.GetTerm(last, valueRangeInteger.From, -valueRangeInteger.Step, takeLength) is { } to) dst.SetRange(last, to, -valueRangeInteger.Step);
                else dst.SetNothing();
            }
        }
        return dst;
    }
    private static GesValue DropExtremeRange(in GesValue source, GesValueRangeInteger valueRangeInteger, short count, bool highest)
    {
        if (valueRangeInteger.Step > 0)
        {
            return highest
                ? DropLastRange(in source, valueRangeInteger, count)
                : DropFirstRange(in source, valueRangeInteger, count);
        }

        return highest
            ? DropFirstRange(in source, valueRangeInteger, count)
            : DropLastRange(in source, valueRangeInteger, count);
    }
    private static GesValue TakeExtremeRange(GesVmState vmState, GesValueRangeFloat range, short count, bool highest)
    {
        var dst = new GesValue();
        var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
        if (count <= 0 || length <= 0 || range.Step == 0d)
        {
            dst.SetRange(0, 0, 0);
            return dst;
        }

        var takeLength = count < length ? count : length;
        if (GameEventScriptRangeMath.GetTerm(range.From, range.To, range.Step, length) is not { } last)
        {
            dst.SetNothing();
            return dst;
        }

        var ascending = range.Step > 0d;
        if (highest)
        {
            if (ascending)
            {
                if (GameEventScriptRangeMath.GetTerm(last, range.From, -range.Step, takeLength) is { } to) dst.SetRange(last, to, -range.Step);
                else dst.SetNothing();
            }
            else
            {
                if (GameEventScriptRangeMath.GetTerm(range.From, range.To, range.Step, takeLength) is { } to) dst.SetRange(range.From, to, range.Step);
                else dst.SetNothing();
            }
        }
        else
        {
            if (ascending)
            {
                if (GameEventScriptRangeMath.GetTerm(range.From, range.To, range.Step, takeLength) is { } to) dst.SetRange(range.From, to, range.Step);
                else dst.SetNothing();
            }
            else
            {
                if (GameEventScriptRangeMath.GetTerm(last, range.From, -range.Step, takeLength) is { } to) dst.SetRange(last, to, -range.Step);
                else dst.SetNothing();
            }
        }
        return dst;
    }
    private static GesValue DropExtremeRange(in GesValue source, GesValueRangeFloat range, short count, bool highest)
    {
        if (range.Step > 0d)
        {
            return highest
                ? DropLastRange(in source, range, count)
                : DropFirstRange(in source, range, count);
        }

        return highest
            ? DropFirstRange(in source, range, count)
            : DropLastRange(in source, range, count);
    }
    private static int CompareForOrdering(in GesValue left, in GesValue right)
    {
        if (left.IsNumeric && right.IsNumeric)
        {
            var unitComparison = left.Unit.CompareTo(right.Unit);
            if (unitComparison != 0) return unitComparison;
            return left.AsNumeric.CompareTo(right.AsNumeric);
        }

        var rankComparison = GetOrderingRank(in left).CompareTo(GetOrderingRank(in right));
        if (rankComparison != 0) return rankComparison;

        switch (left.Kind)
        {
            case Text when right.Kind is Text:
            case Tag when right.Kind is Tag:
                return GameEventScriptText.CompareScalarOrdinal(left.TextValue, right.TextValue);
            case Vector when right.Kind is Vector:
            case Point when right.Kind is Point:
                if (left.Unit != right.Unit) return left.Unit.CompareTo(right.Unit);
                if (left.ObjectValue is GesValueVectorPoint leftTriplet && right.ObjectValue is GesValueVectorPoint rightTriplet)
                {
                    var comparison = leftTriplet.X.CompareTo(rightTriplet.X);
                    if (comparison != 0) return comparison;
                    comparison = leftTriplet.Y.CompareTo(rightTriplet.Y);
                    return comparison != 0 ? comparison : leftTriplet.Z.CompareTo(rightTriplet.Z);
                }
                return 0;
            case GameEventScriptBytecodeTypeKind.Boolean when right.Kind is GameEventScriptBytecodeTypeKind.Boolean:
                return left.IsTrue.CompareTo(right.IsTrue);
            case Dice when right.Kind is Dice && left.ObjectValue is int[] leftDice && right.ObjectValue is int[] rightDice:
            {
                var countComparison = leftDice.Length.CompareTo(rightDice.Length);
                if (countComparison != 0) return countComparison;
                for (var i = 0; i < leftDice.Length; i++)
                {
                    var comparison = leftDice[i].CompareTo(rightDice[i]);
                    if (comparison != 0) return comparison;
                }

                return 0;
            }
            default:
                return left.Kind.CompareTo(right.Kind);
        }
    }
    private static int GetOrderingRank(in GesValue value)
    {
        if (value.IsNumeric) return 1;
        return value.Kind switch
        {
            Nothing => 0,
            Tag => 1,
            Text => 2,
            Percentage => 3,
            Vector => 4,
            Point => 5,
            GameEventScriptBytecodeTypeKind.Boolean => 6,
            Series => 7,
            GameEventScriptBytecodeTypeKind.Range => 8,
            Message => 9,
            Handler => 10,
            List => 11,
            Map or Custom => 12,
            Dice => 13,
            _ => 8
        };
    }
}
