using System;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmRegisterTakeDrop
{
    internal static void GesVmTakeFirst(this GesVmState state, ushort destinationRegister, in GesVmValue source, short count)
    {
        var dst = state.CreateNothing();
        switch (source.Kind)
        {
            case Series when source.ObjectValue is GameEventScriptSeriesValue series:
                TakeFirstSeries(state, ref dst, series, count);
                break;
            case List when source.ObjectValue is GesVmValue[] list:
                TakeFirstList(state, ref dst, list, count);
                break;
            case Dice when source.ObjectValue is int[] dice:
                TakeFirstDice(state, ref dst, dice, count);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeInteger range:
                TakeFirstRange(state, ref dst, range, count);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeFloat range:
                TakeFirstRange(state, ref dst, range, count);
                break;
            case Stream when source.ObjectValue is IGesVmStream stream:
                TakeFirstStream(state, ref dst, stream, count);
                break;
            default:
                dst.SetNothing();
                break;
        }

        state.SetValue(destinationRegister, in dst);
    }
    internal static void GesVmOneRandom(this GesVmState state, ushort destinationRegister, in GesVmValue source, GesVmXoshiroRandom randomGenerator)
    {
        var dst = state.CreateNothing();
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesVmValue[] list:
                if (list.Length > 0) dst = list[randomGenerator.NextInclusiveInt(0, list.Length - 1)];
                else dst.SetNothing();
                break;
            case Dice when source.ObjectValue is int[] dice:
                if (dice.Length > 0) dst.SetInteger(dice[randomGenerator.NextInclusiveInt(0, dice.Length - 1)]);
                else dst.SetNothing();
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeInteger range:
                OneRandomRange(state, ref dst, range, randomGenerator);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeFloat range:
                OneRandomRange(state, ref dst, range, randomGenerator);
                break;
            case Stream when source.ObjectValue is IGesVmStream stream:
                OneRandomStream(state, ref dst, stream, randomGenerator);
                break;
            default:
                dst.SetNothing();
                break;
        }

        state.SetValue(destinationRegister, in dst);
    }
    internal static void GesVmTakeRandom(this GesVmState state, ushort destinationRegister, in GesVmValue source, short count, GesVmXoshiroRandom randomGenerator)
    {
        var dst = state.CreateNothing();
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesVmValue[] list:
                TakeRandomList(state, ref dst, list, count, randomGenerator);
                break;
            case Dice when source.ObjectValue is int[] dice:
                TakeRandomDice(state, ref dst, dice, count, randomGenerator);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeInteger range:
                TakeRandomRange(state, ref dst, range, count, randomGenerator);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeFloat range:
                TakeRandomRange(state, ref dst, range, count, randomGenerator);
                break;
            case Stream when source.ObjectValue is IGesVmStream stream:
                TakeRandomStream(state, ref dst, stream, count, randomGenerator);
                break;
            default:
                dst.SetNothing();
                break;
        }

        state.SetValue(destinationRegister, in dst);
    }
    internal static void GesVmDropFirst(this GesVmState state, ushort destinationRegister, in GesVmValue source, short count)
    {
        var dst = state.CreateNothing();
        switch (source.Kind)
        {
            case Series when source.ObjectValue is GameEventScriptSeriesValue series:
                dst.SetSeries(count <= 0 ? series : series.Drop(count));
                break;
            case List when source.ObjectValue is GesVmValue[] list:
                DropFirstList(state, ref dst, list, count);
                break;
            case Dice when source.ObjectValue is int[] dice:
                DropFirstDice(state, ref dst, dice, count);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeInteger range:
                DropFirstRange(state, ref dst, in source, range, count);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeFloat range:
                DropFirstRange(state, ref dst, in source, range, count);
                break;
            case Stream when source.ObjectValue is IGesVmStream stream:
                DropFirstStream(state, ref dst, stream, count);
                break;
            default:
                dst.SetNothing();
                break;
        }

        state.SetValue(destinationRegister, in dst);
    }
    internal static void GesVmTakeLast(this GesVmState state, ushort destinationRegister, in GesVmValue source, short count)
    {
        var dst = state.CreateNothing();
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesVmValue[] list:
                TakeLastList(state, ref dst, list, count);
                break;
            case Dice when source.ObjectValue is int[] dice:
                TakeLastDice(state, ref dst, dice, count);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeInteger range:
                TakeLastRange(state, ref dst, range, count);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeFloat range:
                TakeLastRange(state, ref dst, range, count);
                break;
            case Stream when source.ObjectValue is IGesVmStream stream:
                TakeLastStream(state, ref dst, stream, count);
                break;
            default:
                dst.SetNothing();
                break;
        }

        state.SetValue(destinationRegister, in dst);
    }
    internal static void GesVmDropLast(this GesVmState state, ushort destinationRegister, in GesVmValue source, short count)
    {
        var dst = state.CreateNothing();
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesVmValue[] list:
                DropLastList(state, ref dst, list, count);
                break;
            case Dice when source.ObjectValue is int[] dice:
                DropLastDice(state, ref dst, dice, count);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeInteger range:
                DropLastRange(state, ref dst, in source, range, count);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeFloat range:
                DropLastRange(state, ref dst, in source, range, count);
                break;
            case Stream when source.ObjectValue is IGesVmStream stream:
                DropLastStream(state, ref dst, stream, count);
                break;
            default:
                dst.SetNothing();
                break;
        }

        state.SetValue(destinationRegister, in dst);
    }
    internal static void GesVmTakeHighest(this GesVmState state, ushort destinationRegister, in GesVmValue source, short count)
    {
        var dst = state.CreateNothing();
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesVmValue[] list:
                TakeExtremeList(state, ref dst, list, count, true);
                break;
            case Dice when source.ObjectValue is int[] dice:
                TakeFirstDice(state, ref dst, dice, count);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeInteger range:
                TakeExtremeRange(state, ref dst, range, count, true);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeFloat range:
                TakeExtremeRange(state, ref dst, range, count, true);
                break;
            case Stream when source.ObjectValue is IGesVmStream stream:
                TakeExtremeStream(state, ref dst, stream, count, true);
                break;
            default:
                dst.SetNothing();
                break;
        }

        state.SetValue(destinationRegister, in dst);
    }
    internal static void GesVmTakeLowest(this GesVmState state, ushort destinationRegister, in GesVmValue source, short count)
    {
        var dst = state.CreateNothing();
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesVmValue[] list:
                TakeExtremeList(state, ref dst, list, count, false);
                break;
            case Dice when source.ObjectValue is int[] dice:
                TakeLastDice(state, ref dst, dice, count);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeInteger range:
                TakeExtremeRange(state, ref dst, range, count, false);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeFloat range:
                TakeExtremeRange(state, ref dst, range, count, false);
                break;
            case Stream when source.ObjectValue is IGesVmStream stream:
                TakeExtremeStream(state, ref dst, stream, count, false);
                break;
            default:
                dst.SetNothing();
                break;
        }

        state.SetValue(destinationRegister, in dst);
    }
    internal static void GesVmDropHighest(this GesVmState state, ushort destinationRegister, in GesVmValue source, short count)
    {
        var dst = state.CreateNothing();
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesVmValue[] list:
                DropExtremeList(state, ref dst, list, count, true);
                break;
            case Dice when source.ObjectValue is int[] dice:
                DropFirstDice(state, ref dst, dice, count);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeInteger range:
                DropExtremeRange(state, ref dst, in source, range, count, true);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeFloat range:
                DropExtremeRange(state, ref dst, in source, range, count, true);
                break;
            case Stream when source.ObjectValue is IGesVmStream stream:
                DropExtremeStream(state, ref dst, stream, count, true);
                break;
            default:
                dst.SetNothing();
                break;
        }

        state.SetValue(destinationRegister, in dst);
    }
    internal static void GesVmDropLowest(this GesVmState state, ushort destinationRegister, in GesVmValue source, short count)
    {
        var dst = state.CreateNothing();
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesVmValue[] list:
                DropExtremeList(state, ref dst, list, count, false);
                break;
            case Dice when source.ObjectValue is int[] dice:
                DropLastDice(state, ref dst, dice, count);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeInteger range:
                DropExtremeRange(state, ref dst, in source, range, count, false);
                break;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeFloat range:
                DropExtremeRange(state, ref dst, in source, range, count, false);
                break;
            case Stream when source.ObjectValue is IGesVmStream stream:
                DropExtremeStream(state, ref dst, stream, count, false);
                break;
            default:
                dst.SetNothing();
                break;
        }

        state.SetValue(destinationRegister, in dst);
    }
    private static void TakeFirstSeries(GesVmState state, ref GesVmValue dst, GameEventScriptSeriesValue series, short count)
    {
        if (count <= 0)
        {
            dst.SetList(state.EmptyList);
            return;
        }

        var list = state.CreateList(count);
        for (var i = 0; i < count; i++) list[i].BindArguments(series.GetTerm(i));
        dst.SetList(list);
    }
    private static void TakeFirstList(GesVmState state, ref GesVmValue dst, GesVmValue[] source, short count)
    {
        if (count <= 0)
        {
            dst.SetList(state.EmptyList);
            return;
        }

        var length = count < source.Length ? count : source.Length;
        var list = state.CreateList(length);
        for (var i = 0; i < length; i++) list[i] = source[i];
        dst.SetList(list);
    }
    private static void DropFirstList(GesVmState state, ref GesVmValue dst, GesVmValue[] source, short count)
    {
        if (count <= 0)
        {
            dst.SetList(source);
            return;
        }

        var start = count < source.Length ? count : source.Length;
        var length = source.Length - start;
        var list = state.CreateList(length);
        for (var i = 0; i < length; i++) list[i] = source[start + i];
        dst.SetList(list);
    }
    private static void TakeLastList(GesVmState state, ref GesVmValue dst, GesVmValue[] source, short count)
    {
        if (count <= 0)
        {
            dst.SetList(state.EmptyList);
            return;
        }

        var length = count < source.Length ? count : source.Length;
        var start = source.Length - length;
        var list = state.CreateList(length);
        for (var i = 0; i < length; i++) list[i] = source[start + i];
        dst.SetList(list);
    }
    private static void DropLastList(GesVmState state, ref GesVmValue dst, GesVmValue[] source, short count)
    {
        if (count <= 0)
        {
            dst.SetList(source);
            return;
        }

        var length = count < source.Length ? source.Length - count : 0;
        var list = state.CreateList(length);
        for (var i = 0; i < length; i++) list[i] = source[i];
        dst.SetList(list);
    }
    private static void TakeFirstDice(GesVmState state, ref GesVmValue dst, int[] source, short count)
    {
        if (count <= 0)
        {
            dst.SetDice([]);
            return;
        }

        var length = count < source.Length ? count : source.Length;
        var dice = new int[length];
        for (var i = 0; i < length; i++) dice[i] = source[i];
        dst.SetDice(dice);
    }
    private static void DropFirstDice(GesVmState state, ref GesVmValue dst, int[] source, short count)
    {
        if (count <= 0)
        {
            dst.SetDice(source);
            return;
        }

        var start = count < source.Length ? count : source.Length;
        var length = source.Length - start;
        var dice = new int[length];
        for (var i = 0; i < length; i++) dice[i] = source[start + i];
        dst.SetDice(dice);
    }
    private static void TakeLastDice(GesVmState state, ref GesVmValue dst, int[] source, short count)
    {
        if (count <= 0)
        {
            dst.SetDice([]);
            return;
        }

        var length = count < source.Length ? count : source.Length;
        var start = source.Length - length;
        var dice = new int[length];
        for (var i = 0; i < length; i++) dice[i] = source[start + i];
        dst.SetDice(dice);
    }
    private static void DropLastDice(GesVmState state, ref GesVmValue dst, int[] source, short count)
    {
        if (count <= 0)
        {
            dst.SetDice(source);
            return;
        }

        var length = count < source.Length ? source.Length - count : 0;
        var dice = new int[length];
        for (var i = 0; i < length; i++) dice[i] = source[i];
        dst.SetDice(dice);
    }
    private static void TakeExtremeList(GesVmState state, ref GesVmValue dst, GesVmValue[] source, short count, bool highest)
    {
        if (count <= 0 || source.Length == 0)
        {
            dst.SetList(state.EmptyList);
            return;
        }

        var length = count < source.Length ? count : source.Length;
        var selected = new bool[source.Length];
        var list = state.CreateList(length);
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
    }
    private static void DropExtremeList(GesVmState state, ref GesVmValue dst, GesVmValue[] source, short count, bool highest)
    {
        if (count <= 0)
        {
            dst.SetList(source);
            return;
        }

        var selectedCount = count < source.Length ? count : source.Length;
        if (selectedCount == source.Length)
        {
            dst.SetList(state.EmptyList);
            return;
        }

        var selected = new bool[source.Length];
        SelectExtremeSlots(source, source.Length, selected, selectedCount, highest);
        var list = state.CreateList(source.Length - selectedCount);
        var target = 0;
        for (var i = 0; i < source.Length; i++)
        {
            if (selected[i]) continue;
            list[target++] = source[i];
        }

        dst.SetList(list);
    }
    private static void SelectExtremeSlots(GesVmValue[] items, int length, bool[] selected, int selectedCount, bool highest)
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
    private static void TakeFirstStream(GesVmState state, ref GesVmValue dst, IGesVmStream stream, short count)
    {
        if (count <= 0)
        {
            if (stream is IDisposable disposable) disposable.Dispose();
            dst.SetList(state.EmptyList);
            return;
        }

        var item = state.CreateNothing();
        var buffer = new GesVmValue[count < 16 ? count : 16];
        var itemCount = 0;
        try
        {
            while (itemCount < count && stream.TryNext(ref item))
            {
                if (itemCount == buffer.Length) Array.Resize(ref buffer, buffer.Length << 1);
                buffer[itemCount++] = item;
            }

            SetListFromBuffer(state, ref dst, buffer, itemCount);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }
    private static void DropFirstStream(GesVmState state, ref GesVmValue dst, IGesVmStream stream, short count)
    {
        var item = state.CreateNothing();
        var skipped = 0;
        var buffer = new GesVmValue[16];
        var itemCount = 0;
        try
        {
            while (count > 0 && skipped < count && stream.TryNext(ref item)) skipped++;
            while (stream.TryNext(ref item))
            {
                if (itemCount == buffer.Length) Array.Resize(ref buffer, buffer.Length << 1);
                buffer[itemCount++] = item;
            }

            SetListFromBuffer(state, ref dst, buffer, itemCount);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }
    private static void TakeLastStream(GesVmState state, ref GesVmValue dst, IGesVmStream stream, short count)
    {
        if (count <= 0)
        {
            if (stream is IDisposable disposable) disposable.Dispose();
            dst.SetList(state.EmptyList);
            return;
        }

        var item = state.CreateNothing();
        var buffer = new GesVmValue[16];
        var itemCount = 0;
        try
        {
            while (stream.TryNext(ref item))
            {
                if (itemCount == buffer.Length) Array.Resize(ref buffer, buffer.Length << 1);
                buffer[itemCount++] = item;
            }

            var length = count < itemCount ? count : itemCount;
            var start = itemCount - length;
            if (length == 0)
            {
                dst.SetList(state.EmptyList);
                return;
            }

            var list = state.CreateList(length);
            Array.Copy(buffer, start, list, 0, length);
            dst.SetList(list);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }
    private static void DropLastStream(GesVmState state, ref GesVmValue dst, IGesVmStream stream, short count)
    {
        var item = state.CreateNothing();
        var buffer = new GesVmValue[16];
        var itemCount = 0;
        try
        {
            while (stream.TryNext(ref item))
            {
                if (itemCount == buffer.Length) Array.Resize(ref buffer, buffer.Length << 1);
                buffer[itemCount++] = item;
            }

            var length = count <= 0 ? itemCount : count < itemCount ? itemCount - count : 0;
            SetListFromBuffer(state, ref dst, buffer, length);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }
    private static void TakeExtremeStream(GesVmState state, ref GesVmValue dst, IGesVmStream stream, short count, bool highest)
    {
        if (count <= 0)
        {
            if (stream is IDisposable disposable) disposable.Dispose();
            dst.SetList(state.EmptyList);
            return;
        }

        var item = state.CreateNothing();
        var buffer = new GesVmValue[count];
        var itemCount = 0;
        try
        {
            while (stream.TryNext(ref item))
            {
                if (itemCount < count)
                {
                    InsertExtreme(buffer, in item, ref itemCount, highest);
                    continue;
                }

                var comparison = CompareForOrdering(in item, in buffer[count - 1]);
                if (highest ? comparison > 0 : comparison < 0)
                {
                    itemCount--;
                    InsertExtreme(buffer, in item, ref itemCount, highest);
                }
            }

            SetListFromBuffer(state, ref dst, buffer, itemCount);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }
    private static void DropExtremeStream(GesVmState state, ref GesVmValue dst, IGesVmStream stream, short count, bool highest)
    {
        var item = state.CreateNothing();
        var buffer = new GesVmValue[16];
        var itemCount = 0;
        try
        {
            while (stream.TryNext(ref item))
            {
                if (itemCount == buffer.Length) Array.Resize(ref buffer, buffer.Length << 1);
                buffer[itemCount++] = item;
            }

            if (count <= 0)
            {
                SetListFromBuffer(state, ref dst, buffer, itemCount);
                return;
            }

            var selectedCount = count < itemCount ? count : itemCount;
            if (selectedCount == itemCount)
            {
                dst.SetList(state.EmptyList);
                return;
            }

            var selected = new bool[itemCount];
            SelectExtremeSlots(buffer, itemCount, selected, selectedCount, highest);
            var list = state.CreateList(itemCount - selectedCount);
            var target = 0;
            for (var i = 0; i < itemCount; i++)
            {
                if (selected[i]) continue;
                list[target++] = buffer[i];
            }

            dst.SetList(list);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }
    private static void OneRandomRange(GesVmState state, ref GesVmValue dst, GesVmValueRangeInteger valueRangeInteger, GesVmXoshiroRandom randomGenerator)
    {
        var length = GameEventScriptRangeMath.GetLength(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step);
        if (length <= 0)
        {
            dst.SetNothing();
            return;
        }

        var index = randomGenerator.NextInclusiveInteger(0, length - 1L);
        if (GameEventScriptRangeMath.TryGetTerm(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step, index + 1L, out var value)) dst.SetInteger(value);
        else dst.SetNothing();
    }
    private static void OneRandomRange(GesVmState state, ref GesVmValue dst, GesVmValueRangeFloat range, GesVmXoshiroRandom randomGenerator)
    {
        var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
        if (length <= 0)
        {
            dst.SetNothing();
            return;
        }

        var index = randomGenerator.NextInclusiveInteger(0, length - 1L);
        if (GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, index + 1L, out var value)) dst.SetFloat(value);
        else dst.SetNothing();
    }
    private static void OneRandomStream(GesVmState state, ref GesVmValue dst, IGesVmStream stream, GesVmXoshiroRandom randomGenerator)
    {
        var item = state.CreateNothing();
        var chosen = state.CreateNothing();
        var count = 0;
        try
        {
            while (stream.TryNext(ref item))
            {
                count++;
                if (randomGenerator.NextInclusiveInt(1, count) == 1) chosen = item;
            }

            if (count > 0) dst = chosen;
            else dst.SetNothing();
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }
    private static void TakeRandomList(GesVmState state, ref GesVmValue dst, GesVmValue[] source, short count, GesVmXoshiroRandom randomGenerator)
    {
        if (count <= 0 || source.Length == 0)
        {
            dst.SetList(state.EmptyList);
            return;
        }

        var length = count < source.Length ? count : source.Length;
        var indices = CreateShuffledPrefix(source.Length, length, randomGenerator);
        var list = state.CreateList(length);
        for (var i = 0; i < length; i++) list[i] = source[indices[i]];
        dst.SetList(list);
    }
    private static void TakeRandomDice(GesVmState state, ref GesVmValue dst, int[] source, short count, GesVmXoshiroRandom randomGenerator)
    {
        if (count <= 0 || source.Length == 0)
        {
            dst.SetDice([]);
            return;
        }

        var length = count < source.Length ? count : source.Length;
        var indices = CreateShuffledPrefix(source.Length, length, randomGenerator);
        var dice = new int[length];
        for (var i = 0; i < length; i++) dice[i] = source[indices[i]];
        dst.SetDice(dice);
    }
    private static void TakeRandomRange(GesVmState state, ref GesVmValue dst, GesVmValueRangeInteger valueRangeInteger, short count, GesVmXoshiroRandom randomGenerator)
    {
        var sourceLength = GameEventScriptRangeMath.GetLength(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step);
        if (count <= 0 || sourceLength <= 0)
        {
            dst.SetList(state.EmptyList);
            return;
        }

        var length = count < sourceLength ? count : checked((int)sourceLength);
        var list = state.CreateList(length);
        if (sourceLength <= int.MaxValue)
        {
            var indices = CreateShuffledPrefix(checked((int)sourceLength), length, randomGenerator);
            for (var i = 0; i < length; i++)
            {
                if (GameEventScriptRangeMath.TryGetTerm(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step, indices[i] + 1L, out var value)) list[i].SetInteger(value);
                else list[i].SetNothing();
            }

            dst.SetList(list);
            return;
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
            if (GameEventScriptRangeMath.TryGetTerm(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step, index + 1L, out var value)) list[i].SetInteger(value);
            else list[i].SetNothing();
        }

        dst.SetList(list);
    }
    private static void TakeRandomRange(GesVmState state, ref GesVmValue dst, GesVmValueRangeFloat range, short count, GesVmXoshiroRandom randomGenerator)
    {
        var sourceLength = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
        if (count <= 0 || sourceLength <= 0)
        {
            dst.SetList(state.EmptyList);
            return;
        }

        var length = count < sourceLength ? count : checked((int)sourceLength);
        var list = state.CreateList(length);
        if (sourceLength <= int.MaxValue)
        {
            var indices = CreateShuffledPrefix(checked((int)sourceLength), length, randomGenerator);
            for (var i = 0; i < length; i++)
            {
                if (GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, indices[i] + 1L, out var value)) list[i].SetFloat(value);
                else list[i].SetNothing();
            }

            dst.SetList(list);
            return;
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
            if (GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, index + 1L, out var value)) list[i].SetFloat(value);
            else list[i].SetNothing();
        }

        dst.SetList(list);
    }
    private static void TakeRandomStream(GesVmState state, ref GesVmValue dst, IGesVmStream stream, short count, GesVmXoshiroRandom randomGenerator)
    {
        if (count <= 0)
        {
            if (stream is IDisposable disposable) disposable.Dispose();
            dst.SetList(state.EmptyList);
            return;
        }

        var item = state.CreateNothing();
        var buffer = new GesVmValue[16];
        var itemCount = 0;
        try
        {
            while (stream.TryNext(ref item))
            {
                if (itemCount == buffer.Length) Array.Resize(ref buffer, buffer.Length << 1);
                buffer[itemCount++] = item;
            }

            if (itemCount == 0)
            {
                dst.SetList(state.EmptyList);
                return;
            }

            var length = count < itemCount ? count : itemCount;
            var indices = CreateShuffledPrefix(itemCount, length, randomGenerator);
            var list = state.CreateList(length);
            for (var i = 0; i < length; i++) list[i] = buffer[indices[i]];
            dst.SetList(list);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }
    private static int[] CreateShuffledPrefix(int sourceLength, int prefixLength, GesVmXoshiroRandom randomGenerator)
    {
        var indices = new int[sourceLength];
        for (var i = 0; i < sourceLength; i++) indices[i] = i;
        var selectedIndices = new int[prefixLength];
        var remainingLength = sourceLength;
        for (var i = 0; i < prefixLength; i++)
        {
            var selected = randomGenerator.NextInclusiveInt(0, remainingLength - 1);
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
    private static void InsertExtreme(GesVmValue[] buffer, in GesVmValue item, ref int itemCount, bool highest)
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
        itemCount++;
    }
    private static void SetListFromBuffer(GesVmState state, ref GesVmValue dst, GesVmValue[] buffer, int length)
    {
        if (length == 0)
        {
            dst.SetList(state.EmptyList);
            return;
        }

        var list = state.CreateList(length);
        Array.Copy(buffer, list, length);
        dst.SetList(list);
    }
    private static void TakeFirstRange(GesVmState state, ref GesVmValue dst, GesVmValueRangeInteger valueRangeInteger, short count)
    {
        var length = GameEventScriptRangeMath.GetLength(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step);
        if (count <= 0 || length <= 0)
        {
            dst.SetRange(0, 0, 0);
            return;
        }

        if (count >= length)
        {
            dst.SetRange(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step);
            return;
        }

        if (GameEventScriptRangeMath.TryGetTerm(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step, count, out var to)) dst.SetRange(valueRangeInteger.From, to, valueRangeInteger.Step);
        else dst.SetNothing();
    }
    private static void DropFirstRange(GesVmState state, ref GesVmValue dst, in GesVmValue source, GesVmValueRangeInteger valueRangeInteger, short count)
    {
        var length = GameEventScriptRangeMath.GetLength(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step);
        if (count <= 0)
        {
            dst = source;
            return;
        }

        if (count >= length)
        {
            dst.SetRange(0, 0, 0);
            return;
        }

        if (GameEventScriptRangeMath.TryGetTerm(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step, count + 1L, out var from)) dst.SetRange(from, valueRangeInteger.To, valueRangeInteger.Step);
        else dst.SetNothing();
    }
    private static void TakeLastRange(GesVmState state, ref GesVmValue dst, GesVmValueRangeInteger valueRangeInteger, short count)
    {
        var length = GameEventScriptRangeMath.GetLength(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step);
        if (count <= 0 || length <= 0)
        {
            dst.SetRange(0, 0, 0);
            return;
        }

        if (count >= length)
        {
            dst.SetRange(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step);
            return;
        }

        if (GameEventScriptRangeMath.TryGetTerm(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step, length - count + 1L, out var from)) dst.SetRange(from, valueRangeInteger.To, valueRangeInteger.Step);
        else dst.SetNothing();
    }
    private static void DropLastRange(GesVmState state, ref GesVmValue dst, in GesVmValue source, GesVmValueRangeInteger valueRangeInteger, short count)
    {
        var length = GameEventScriptRangeMath.GetLength(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step);
        if (count <= 0)
        {
            dst = source;
            return;
        }

        if (count >= length)
        {
            dst.SetRange(0, 0, 0);
            return;
        }

        if (GameEventScriptRangeMath.TryGetTerm(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step, length - count, out var to)) dst.SetRange(valueRangeInteger.From, to, valueRangeInteger.Step);
        else dst.SetNothing();
    }
    private static void TakeFirstRange(GesVmState state, ref GesVmValue dst, GesVmValueRangeFloat range, short count)
    {
        var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
        if (count <= 0 || length <= 0)
        {
            dst.SetRange(0, 0, 0);
            return;
        }

        if (count >= length)
        {
            dst.SetRange(range.From, range.To, range.Step);
            return;
        }

        if (GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, count, out var to)) dst.SetRange(range.From, to, range.Step);
        else dst.SetNothing();
    }
    private static void DropFirstRange(GesVmState state, ref GesVmValue dst, in GesVmValue source, GesVmValueRangeFloat range, short count)
    {
        var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
        if (count <= 0)
        {
            dst = source;
            return;
        }

        if (count >= length)
        {
            dst.SetRange(0, 0, 0);
            return;
        }

        if (GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, count + 1L, out var from)) dst.SetRange(from, range.To, range.Step);
        else dst.SetNothing();
    }
    private static void TakeLastRange(GesVmState state, ref GesVmValue dst, GesVmValueRangeFloat range, short count)
    {
        var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
        if (count <= 0 || length <= 0)
        {
            dst.SetRange(0, 0, 0);
            return;
        }

        if (count >= length)
        {
            dst.SetRange(range.From, range.To, range.Step);
            return;
        }

        if (GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, length - count + 1L, out var from)) dst.SetRange(from, range.To, range.Step);
        else dst.SetNothing();
    }
    private static void DropLastRange(GesVmState state, ref GesVmValue dst, in GesVmValue source, GesVmValueRangeFloat range, short count)
    {
        var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
        if (count <= 0)
        {
            dst = source;
            return;
        }

        if (count >= length)
        {
            dst.SetRange(0, 0, 0);
            return;
        }

        if (GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, length - count, out var to)) dst.SetRange(range.From, to, range.Step);
        else dst.SetNothing();
    }
    private static void TakeExtremeRange(GesVmState state, ref GesVmValue dst, GesVmValueRangeInteger valueRangeInteger, short count, bool highest)
    {
        var length = GameEventScriptRangeMath.GetLength(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step);
        if (count <= 0 || length <= 0 || valueRangeInteger.Step == 0)
        {
            dst.SetRange(0, 0, 0);
            return;
        }

        var takeLength = count < length ? count : length;
        if (!GameEventScriptRangeMath.TryGetTerm(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step, length, out var last))
        {
            dst.SetNothing();
            return;
        }

        var ascending = valueRangeInteger.Step > 0;
        if (highest)
        {
            if (ascending)
            {
                if (GameEventScriptRangeMath.TryGetTerm(last, valueRangeInteger.From, -valueRangeInteger.Step, takeLength, out var to)) dst.SetRange(last, to, -valueRangeInteger.Step);
                else dst.SetNothing();
            }
            else
            {
                if (GameEventScriptRangeMath.TryGetTerm(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step, takeLength, out var to)) dst.SetRange(valueRangeInteger.From, to, valueRangeInteger.Step);
                else dst.SetNothing();
            }
        }
        else
        {
            if (ascending)
            {
                if (GameEventScriptRangeMath.TryGetTerm(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step, takeLength, out var to)) dst.SetRange(valueRangeInteger.From, to, valueRangeInteger.Step);
                else dst.SetNothing();
            }
            else
            {
                if (GameEventScriptRangeMath.TryGetTerm(last, valueRangeInteger.From, -valueRangeInteger.Step, takeLength, out var to)) dst.SetRange(last, to, -valueRangeInteger.Step);
                else dst.SetNothing();
            }
        }
    }
    private static void DropExtremeRange(GesVmState state, ref GesVmValue dst, in GesVmValue source, GesVmValueRangeInteger valueRangeInteger, short count, bool highest)
    {
        if (valueRangeInteger.Step > 0)
        {
            if (highest) DropLastRange(state, ref dst, in source, valueRangeInteger, count);
            else DropFirstRange(state, ref dst, in source, valueRangeInteger, count);
            return;
        }

        if (highest) DropFirstRange(state, ref dst, in source, valueRangeInteger, count);
        else DropLastRange(state, ref dst, in source, valueRangeInteger, count);
    }
    private static void TakeExtremeRange(GesVmState state, ref GesVmValue dst, GesVmValueRangeFloat range, short count, bool highest)
    {
        var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
        if (count <= 0 || length <= 0 || range.Step == 0d)
        {
            dst.SetRange(0, 0, 0);
            return;
        }

        var takeLength = count < length ? count : length;
        if (!GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, length, out var last))
        {
            dst.SetNothing();
            return;
        }

        var ascending = range.Step > 0d;
        if (highest)
        {
            if (ascending)
            {
                if (GameEventScriptRangeMath.TryGetTerm(last, range.From, -range.Step, takeLength, out var to)) dst.SetRange(last, to, -range.Step);
                else dst.SetNothing();
            }
            else
            {
                if (GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, takeLength, out var to)) dst.SetRange(range.From, to, range.Step);
                else dst.SetNothing();
            }
        }
        else
        {
            if (ascending)
            {
                if (GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, takeLength, out var to)) dst.SetRange(range.From, to, range.Step);
                else dst.SetNothing();
            }
            else
            {
                if (GameEventScriptRangeMath.TryGetTerm(last, range.From, -range.Step, takeLength, out var to)) dst.SetRange(last, to, -range.Step);
                else dst.SetNothing();
            }
        }
    }
    private static void DropExtremeRange(GesVmState state, ref GesVmValue dst, in GesVmValue source, GesVmValueRangeFloat range, short count, bool highest)
    {
        if (range.Step > 0d)
        {
            if (highest) DropLastRange(state, ref dst, in source, range, count);
            else DropFirstRange(state, ref dst, in source, range, count);
            return;
        }

        if (highest) DropFirstRange(state, ref dst, in source, range, count);
        else DropLastRange(state, ref dst, in source, range, count);
    }
    private static int CompareForOrdering(in GesVmValue left, in GesVmValue right)
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
                return StringComparer.Ordinal.Compare(left.TextValue, right.TextValue);
            case Vector when right.Kind is Vector:
            case Point when right.Kind is Point:
                if (left.Unit != right.Unit) return left.Unit.CompareTo(right.Unit);
                if (left.ObjectValue is GesVmValueVectorPoint leftTriplet && right.ObjectValue is GesVmValueVectorPoint rightTriplet)
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
    private static int GetOrderingRank(in GesVmValue value)
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
