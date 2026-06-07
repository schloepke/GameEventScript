using System;
using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterSeries
{

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmTakeFirst(ref this VmValue dst, ref VmValue source, short count)
    {
        switch (source.Kind)
        {
            case Series when source.ObjectValue is GameEventScriptSeriesValue series:
                TakeFirstSeries(ref dst, series, count);
                return;
            case List when source.ObjectValue is VmListObject list:
                TakeFirstList(ref dst, list, count);
                return;
            case Dice when source.ObjectValue is int[] dice:
                TakeFirstDice(ref dst, dice, count);
                return;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is VmRange range:
                TakeFirstRange(ref dst, range, count);
                return;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is VmFloatRange range:
                TakeFirstRange(ref dst, range, count);
                return;
            case Stream when source.ObjectValue is IVmStream stream:
                TakeFirstStream(ref dst, stream, count);
                return;
            default:
                dst.SetNothing();
                return;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmOneRandom(ref this VmValue dst, ref VmValue source, GameEventScriptRandomGenerator randomGenerator)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is VmListObject list:
                if (list.Length > 0) dst = list.Items[randomGenerator.NextInclusiveInt(0, list.Length - 1)];
                else dst.SetNothing();
                return;
            case Dice when source.ObjectValue is int[] dice:
                if (dice.Length > 0) dst.SetInteger(dice[randomGenerator.NextInclusiveInt(0, dice.Length - 1)]);
                else dst.SetNothing();
                return;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is VmRange range:
                OneRandomRange(ref dst, range, randomGenerator);
                return;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is VmFloatRange range:
                OneRandomRange(ref dst, range, randomGenerator);
                return;
            case Stream when source.ObjectValue is IVmStream stream:
                OneRandomStream(ref dst, stream, randomGenerator);
                return;
            default:
                dst.SetNothing();
                return;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmTakeRandom(ref this VmValue dst, ref VmValue source, short count, GameEventScriptRandomGenerator randomGenerator)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is VmListObject list:
                TakeRandomList(ref dst, list, count, randomGenerator);
                return;
            case Dice when source.ObjectValue is int[] dice:
                TakeRandomDice(ref dst, dice, count, randomGenerator);
                return;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is VmRange range:
                TakeRandomRange(ref dst, range, count, randomGenerator);
                return;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is VmFloatRange range:
                TakeRandomRange(ref dst, range, count, randomGenerator);
                return;
            case Stream when source.ObjectValue is IVmStream stream:
                TakeRandomStream(ref dst, stream, count, randomGenerator);
                return;
            default:
                dst.SetNothing();
                return;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmDropFirst(ref this VmValue dst, ref VmValue source, short count)
    {
        switch (source.Kind)
        {
            case Series when source.ObjectValue is GameEventScriptSeriesValue series:
                dst.SetSeries(count <= 0 ? series : series.Drop(count));
                return;
            case List when source.ObjectValue is VmListObject list:
                DropFirstList(ref dst, list, count);
                return;
            case Dice when source.ObjectValue is int[] dice:
                DropFirstDice(ref dst, dice, count);
                return;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is VmRange range:
                DropFirstRange(ref dst, ref source, range, count);
                return;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is VmFloatRange range:
                DropFirstRange(ref dst, ref source, range, count);
                return;
            case Stream when source.ObjectValue is IVmStream stream:
                DropFirstStream(ref dst, stream, count);
                return;
            default:
                dst.SetNothing();
                return;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmTakeLast(ref this VmValue dst, ref VmValue source, short count)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is VmListObject list:
                TakeLastList(ref dst, list, count);
                return;
            case Dice when source.ObjectValue is int[] dice:
                TakeLastDice(ref dst, dice, count);
                return;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is VmRange range:
                TakeLastRange(ref dst, range, count);
                return;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is VmFloatRange range:
                TakeLastRange(ref dst, range, count);
                return;
            case Stream when source.ObjectValue is IVmStream stream:
                TakeLastStream(ref dst, stream, count);
                return;
            default:
                dst.SetNothing();
                return;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmDropLast(ref this VmValue dst, ref VmValue source, short count)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is VmListObject list:
                DropLastList(ref dst, list, count);
                return;
            case Dice when source.ObjectValue is int[] dice:
                DropLastDice(ref dst, dice, count);
                return;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is VmRange range:
                DropLastRange(ref dst, ref source, range, count);
                return;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is VmFloatRange range:
                DropLastRange(ref dst, ref source, range, count);
                return;
            case Stream when source.ObjectValue is IVmStream stream:
                DropLastStream(ref dst, stream, count);
                return;
            default:
                dst.SetNothing();
                return;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmTakeHighest(ref this VmValue dst, ref VmValue source, short count)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is VmListObject list:
                TakeExtremeList(ref dst, list, count, true);
                return;
            case Dice when source.ObjectValue is int[] dice:
                TakeFirstDice(ref dst, dice, count);
                return;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is VmRange range:
                TakeExtremeRange(ref dst, range, count, true);
                return;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is VmFloatRange range:
                TakeExtremeRange(ref dst, range, count, true);
                return;
            case Stream when source.ObjectValue is IVmStream stream:
                TakeExtremeStream(ref dst, stream, count, true);
                return;
            default:
                dst.SetNothing();
                return;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmTakeLowest(ref this VmValue dst, ref VmValue source, short count)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is VmListObject list:
                TakeExtremeList(ref dst, list, count, false);
                return;
            case Dice when source.ObjectValue is int[] dice:
                TakeLastDice(ref dst, dice, count);
                return;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is VmRange range:
                TakeExtremeRange(ref dst, range, count, false);
                return;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is VmFloatRange range:
                TakeExtremeRange(ref dst, range, count, false);
                return;
            case Stream when source.ObjectValue is IVmStream stream:
                TakeExtremeStream(ref dst, stream, count, false);
                return;
            default:
                dst.SetNothing();
                return;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmDropHighest(ref this VmValue dst, ref VmValue source, short count)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is VmListObject list:
                DropExtremeList(ref dst, list, count, true);
                return;
            case Dice when source.ObjectValue is int[] dice:
                DropFirstDice(ref dst, dice, count);
                return;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is VmRange range:
                DropExtremeRange(ref dst, ref source, range, count, true);
                return;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is VmFloatRange range:
                DropExtremeRange(ref dst, ref source, range, count, true);
                return;
            case Stream when source.ObjectValue is IVmStream stream:
                DropExtremeStream(ref dst, stream, count, true);
                return;
            default:
                dst.SetNothing();
                return;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmDropLowest(ref this VmValue dst, ref VmValue source, short count)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is VmListObject list:
                DropExtremeList(ref dst, list, count, false);
                return;
            case Dice when source.ObjectValue is int[] dice:
                DropLastDice(ref dst, dice, count);
                return;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is VmRange range:
                DropExtremeRange(ref dst, ref source, range, count, false);
                return;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is VmFloatRange range:
                DropExtremeRange(ref dst, ref source, range, count, false);
                return;
            case Stream when source.ObjectValue is IVmStream stream:
                DropExtremeStream(ref dst, stream, count, false);
                return;
            default:
                dst.SetNothing();
                return;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void TakeFirstSeries(ref VmValue dst, GameEventScriptSeriesValue series, short count)
    {
        if (count <= 0)
        {
            dst.SetList(dst.OwningState.EmptyList);
            return;
        }

        var list = dst.OwningState.CreateList(count);
        for (var i = 0; i < count; i++) list.Items[i].BindArguments(series.GetTerm(i));
        dst.SetList(list);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void TakeFirstList(ref VmValue dst, VmListObject source, short count)
    {
        if (count <= 0)
        {
            dst.SetList(dst.OwningState.EmptyList);
            return;
        }

        var length = count < source.Length ? count : source.Length;
        var list = dst.OwningState.CreateList(length);
        for (var i = 0; i < length; i++) list.Items[i] = source.Items[i];
        dst.SetList(list);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DropFirstList(ref VmValue dst, VmListObject source, short count)
    {
        if (count <= 0)
        {
            dst.SetList(source);
            return;
        }

        var start = count < source.Length ? count : source.Length;
        var length = source.Length - start;
        var list = dst.OwningState.CreateList(length);
        for (var i = 0; i < length; i++) list.Items[i] = source.Items[start + i];
        dst.SetList(list);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void TakeLastList(ref VmValue dst, VmListObject source, short count)
    {
        if (count <= 0)
        {
            dst.SetList(dst.OwningState.EmptyList);
            return;
        }

        var length = count < source.Length ? count : source.Length;
        var start = source.Length - length;
        var list = dst.OwningState.CreateList(length);
        for (var i = 0; i < length; i++) list.Items[i] = source.Items[start + i];
        dst.SetList(list);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DropLastList(ref VmValue dst, VmListObject source, short count)
    {
        if (count <= 0)
        {
            dst.SetList(source);
            return;
        }

        var length = count < source.Length ? source.Length - count : 0;
        var list = dst.OwningState.CreateList(length);
        for (var i = 0; i < length; i++) list.Items[i] = source.Items[i];
        dst.SetList(list);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void TakeFirstDice(ref VmValue dst, int[] source, short count)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DropFirstDice(ref VmValue dst, int[] source, short count)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void TakeLastDice(ref VmValue dst, int[] source, short count)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DropLastDice(ref VmValue dst, int[] source, short count)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void TakeExtremeList(ref VmValue dst, VmListObject source, short count, bool highest)
    {
        if (count <= 0 || source.Length == 0)
        {
            dst.SetList(dst.OwningState.EmptyList);
            return;
        }

        var length = count < source.Length ? count : source.Length;
        var selected = new bool[source.Length];
        var list = dst.OwningState.CreateList(length);
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

                var comparison = CompareForOrdering(ref source.Items[j], ref source.Items[best]);
                if (highest ? comparison > 0 : comparison < 0) best = j;
            }

            selected[best] = true;
            list.Items[i] = source.Items[best];
        }

        dst.SetList(list);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DropExtremeList(ref VmValue dst, VmListObject source, short count, bool highest)
    {
        if (count <= 0)
        {
            dst.SetList(source);
            return;
        }

        var selectedCount = count < source.Length ? count : source.Length;
        if (selectedCount == source.Length)
        {
            dst.SetList(dst.OwningState.EmptyList);
            return;
        }

        var selected = new bool[source.Length];
        SelectExtremeSlots(source.Items, source.Length, selected, selectedCount, highest);
        var list = dst.OwningState.CreateList(source.Length - selectedCount);
        var target = 0;
        for (var i = 0; i < source.Length; i++)
        {
            if (selected[i]) continue;
            list.Items[target++] = source.Items[i];
        }

        dst.SetList(list);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SelectExtremeSlots(VmValue[] items, int length, bool[] selected, int selectedCount, bool highest)
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

                var comparison = CompareForOrdering(ref items[j], ref items[best]);
                if (highest ? comparison > 0 : comparison < 0) best = j;
            }

            selected[best] = true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void TakeFirstStream(ref VmValue dst, IVmStream stream, short count)
    {
        if (count <= 0)
        {
            if (stream is IDisposable disposable) disposable.Dispose();
            dst.SetList(dst.OwningState.EmptyList);
            return;
        }

        var item = dst.OwningState.CreateNothing();
        var buffer = new VmValue[count < 16 ? count : 16];
        var itemCount = 0;
        try
        {
            while (itemCount < count && stream.TryNext(ref item))
            {
                if (itemCount == buffer.Length) Array.Resize(ref buffer, buffer.Length << 1);
                buffer[itemCount++] = item;
            }

            SetListFromBuffer(ref dst, buffer, itemCount);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DropFirstStream(ref VmValue dst, IVmStream stream, short count)
    {
        var item = dst.OwningState.CreateNothing();
        var skipped = 0;
        var buffer = new VmValue[16];
        var itemCount = 0;
        try
        {
            while (count > 0 && skipped < count && stream.TryNext(ref item)) skipped++;
            while (stream.TryNext(ref item))
            {
                if (itemCount == buffer.Length) Array.Resize(ref buffer, buffer.Length << 1);
                buffer[itemCount++] = item;
            }

            SetListFromBuffer(ref dst, buffer, itemCount);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void TakeLastStream(ref VmValue dst, IVmStream stream, short count)
    {
        if (count <= 0)
        {
            if (stream is IDisposable disposable) disposable.Dispose();
            dst.SetList(dst.OwningState.EmptyList);
            return;
        }

        var item = dst.OwningState.CreateNothing();
        var buffer = new VmValue[16];
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
                dst.SetList(dst.OwningState.EmptyList);
                return;
            }

            var list = dst.OwningState.CreateList(length);
            Array.Copy(buffer, start, list.Items, 0, length);
            dst.SetList(list);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DropLastStream(ref VmValue dst, IVmStream stream, short count)
    {
        var item = dst.OwningState.CreateNothing();
        var buffer = new VmValue[16];
        var itemCount = 0;
        try
        {
            while (stream.TryNext(ref item))
            {
                if (itemCount == buffer.Length) Array.Resize(ref buffer, buffer.Length << 1);
                buffer[itemCount++] = item;
            }

            var length = count <= 0 ? itemCount : count < itemCount ? itemCount - count : 0;
            SetListFromBuffer(ref dst, buffer, length);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void TakeExtremeStream(ref VmValue dst, IVmStream stream, short count, bool highest)
    {
        if (count <= 0)
        {
            if (stream is IDisposable disposable) disposable.Dispose();
            dst.SetList(dst.OwningState.EmptyList);
            return;
        }

        var item = dst.OwningState.CreateNothing();
        var buffer = new VmValue[count];
        var itemCount = 0;
        try
        {
            while (stream.TryNext(ref item))
            {
                if (itemCount < count)
                {
                    InsertExtreme(buffer, ref item, ref itemCount, highest);
                    continue;
                }

                var comparison = CompareForOrdering(ref item, ref buffer[count - 1]);
                if (highest ? comparison > 0 : comparison < 0)
                {
                    itemCount--;
                    InsertExtreme(buffer, ref item, ref itemCount, highest);
                }
            }

            SetListFromBuffer(ref dst, buffer, itemCount);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DropExtremeStream(ref VmValue dst, IVmStream stream, short count, bool highest)
    {
        var item = dst.OwningState.CreateNothing();
        var buffer = new VmValue[16];
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
                SetListFromBuffer(ref dst, buffer, itemCount);
                return;
            }

            var selectedCount = count < itemCount ? count : itemCount;
            if (selectedCount == itemCount)
            {
                dst.SetList(dst.OwningState.EmptyList);
                return;
            }

            var selected = new bool[itemCount];
            SelectExtremeSlots(buffer, itemCount, selected, selectedCount, highest);
            var list = dst.OwningState.CreateList(itemCount - selectedCount);
            var target = 0;
            for (var i = 0; i < itemCount; i++)
            {
                if (selected[i]) continue;
                list.Items[target++] = buffer[i];
            }

            dst.SetList(list);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OneRandomRange(ref VmValue dst, VmRange range, GameEventScriptRandomGenerator randomGenerator)
    {
        var length = GameEventScriptRangeMath.GetLength(range.from, range.to, range.step);
        if (length <= 0)
        {
            dst.SetNothing();
            return;
        }

        var index = randomGenerator.NextInclusiveInteger(0, length - 1L);
        if (GameEventScriptRangeMath.TryGetTerm(range.from, range.to, range.step, index + 1L, out var value)) dst.SetInteger(value);
        else dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OneRandomRange(ref VmValue dst, VmFloatRange range, GameEventScriptRandomGenerator randomGenerator)
    {
        var length = GameEventScriptRangeMath.GetLength(range.from, range.to, range.step);
        if (length <= 0)
        {
            dst.SetNothing();
            return;
        }

        var index = randomGenerator.NextInclusiveInteger(0, length - 1L);
        if (GameEventScriptRangeMath.TryGetTerm(range.from, range.to, range.step, index + 1L, out var value)) dst.SetFloat(value);
        else dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OneRandomStream(ref VmValue dst, IVmStream stream, GameEventScriptRandomGenerator randomGenerator)
    {
        var item = dst.OwningState.CreateNothing();
        var chosen = dst.OwningState.CreateNothing();
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void TakeRandomList(ref VmValue dst, VmListObject source, short count, GameEventScriptRandomGenerator randomGenerator)
    {
        if (count <= 0 || source.Length == 0)
        {
            dst.SetList(dst.OwningState.EmptyList);
            return;
        }

        var length = count < source.Length ? count : source.Length;
        var indices = CreateShuffledPrefix(source.Length, length, randomGenerator);
        var list = dst.OwningState.CreateList(length);
        for (var i = 0; i < length; i++) list.Items[i] = source.Items[indices[i]];
        dst.SetList(list);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void TakeRandomDice(ref VmValue dst, int[] source, short count, GameEventScriptRandomGenerator randomGenerator)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void TakeRandomRange(ref VmValue dst, VmRange range, short count, GameEventScriptRandomGenerator randomGenerator)
    {
        var sourceLength = GameEventScriptRangeMath.GetLength(range.from, range.to, range.step);
        if (count <= 0 || sourceLength <= 0)
        {
            dst.SetList(dst.OwningState.EmptyList);
            return;
        }

        var length = count < sourceLength ? count : checked((int)sourceLength);
        var list = dst.OwningState.CreateList(length);
        if (sourceLength <= int.MaxValue)
        {
            var indices = CreateShuffledPrefix(checked((int)sourceLength), length, randomGenerator);
            for (var i = 0; i < length; i++)
            {
                if (GameEventScriptRangeMath.TryGetTerm(range.from, range.to, range.step, indices[i] + 1L, out var value)) list.Items[i].SetInteger(value);
                else list.Items[i].SetNothing();
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
            if (GameEventScriptRangeMath.TryGetTerm(range.from, range.to, range.step, index + 1L, out var value)) list.Items[i].SetInteger(value);
            else list.Items[i].SetNothing();
        }

        dst.SetList(list);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void TakeRandomRange(ref VmValue dst, VmFloatRange range, short count, GameEventScriptRandomGenerator randomGenerator)
    {
        var sourceLength = GameEventScriptRangeMath.GetLength(range.from, range.to, range.step);
        if (count <= 0 || sourceLength <= 0)
        {
            dst.SetList(dst.OwningState.EmptyList);
            return;
        }

        var length = count < sourceLength ? count : checked((int)sourceLength);
        var list = dst.OwningState.CreateList(length);
        if (sourceLength <= int.MaxValue)
        {
            var indices = CreateShuffledPrefix(checked((int)sourceLength), length, randomGenerator);
            for (var i = 0; i < length; i++)
            {
                if (GameEventScriptRangeMath.TryGetTerm(range.from, range.to, range.step, indices[i] + 1L, out var value)) list.Items[i].SetFloat(value);
                else list.Items[i].SetNothing();
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
            if (GameEventScriptRangeMath.TryGetTerm(range.from, range.to, range.step, index + 1L, out var value)) list.Items[i].SetFloat(value);
            else list.Items[i].SetNothing();
        }

        dst.SetList(list);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void TakeRandomStream(ref VmValue dst, IVmStream stream, short count, GameEventScriptRandomGenerator randomGenerator)
    {
        if (count <= 0)
        {
            if (stream is IDisposable disposable) disposable.Dispose();
            dst.SetList(dst.OwningState.EmptyList);
            return;
        }

        var item = dst.OwningState.CreateNothing();
        var buffer = new VmValue[16];
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
                dst.SetList(dst.OwningState.EmptyList);
                return;
            }

            var length = count < itemCount ? count : itemCount;
            var indices = CreateShuffledPrefix(itemCount, length, randomGenerator);
            var list = dst.OwningState.CreateList(length);
            for (var i = 0; i < length; i++) list.Items[i] = buffer[indices[i]];
            dst.SetList(list);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int[] CreateShuffledPrefix(int sourceLength, int prefixLength, GameEventScriptRandomGenerator randomGenerator)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InsertExtreme(VmValue[] buffer, ref VmValue item, ref int itemCount, bool highest)
    {
        var index = itemCount;
        while (index > 0)
        {
            var comparison = CompareForOrdering(ref item, ref buffer[index - 1]);
            if (highest ? comparison <= 0 : comparison >= 0) break;
            buffer[index] = buffer[index - 1];
            index--;
        }

        buffer[index] = item;
        itemCount++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SetListFromBuffer(ref VmValue dst, VmValue[] buffer, int length)
    {
        if (length == 0)
        {
            dst.SetList(dst.OwningState.EmptyList);
            return;
        }

        var list = dst.OwningState.CreateList(length);
        Array.Copy(buffer, list.Items, length);
        dst.SetList(list);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void TakeFirstRange(ref VmValue dst, VmRange range, short count)
    {
        var length = GameEventScriptRangeMath.GetLength(range.from, range.to, range.step);
        if (count <= 0 || length <= 0)
        {
            dst.SetRange(0, 0, 0);
            return;
        }

        if (count >= length)
        {
            dst.SetRange(range.from, range.to, range.step);
            return;
        }

        if (GameEventScriptRangeMath.TryGetTerm(range.from, range.to, range.step, count, out var to)) dst.SetRange(range.from, to, range.step);
        else dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DropFirstRange(ref VmValue dst, ref VmValue source, VmRange range, short count)
    {
        var length = GameEventScriptRangeMath.GetLength(range.from, range.to, range.step);
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

        if (GameEventScriptRangeMath.TryGetTerm(range.from, range.to, range.step, count + 1L, out var from)) dst.SetRange(from, range.to, range.step);
        else dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void TakeLastRange(ref VmValue dst, VmRange range, short count)
    {
        var length = GameEventScriptRangeMath.GetLength(range.from, range.to, range.step);
        if (count <= 0 || length <= 0)
        {
            dst.SetRange(0, 0, 0);
            return;
        }

        if (count >= length)
        {
            dst.SetRange(range.from, range.to, range.step);
            return;
        }

        if (GameEventScriptRangeMath.TryGetTerm(range.from, range.to, range.step, length - count + 1L, out var from)) dst.SetRange(from, range.to, range.step);
        else dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DropLastRange(ref VmValue dst, ref VmValue source, VmRange range, short count)
    {
        var length = GameEventScriptRangeMath.GetLength(range.from, range.to, range.step);
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

        if (GameEventScriptRangeMath.TryGetTerm(range.from, range.to, range.step, length - count, out var to)) dst.SetRange(range.from, to, range.step);
        else dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void TakeFirstRange(ref VmValue dst, VmFloatRange range, short count)
    {
        var length = GameEventScriptRangeMath.GetLength(range.from, range.to, range.step);
        if (count <= 0 || length <= 0)
        {
            dst.SetRange(0, 0, 0);
            return;
        }

        if (count >= length)
        {
            dst.SetRange(range.from, range.to, range.step);
            return;
        }

        if (GameEventScriptRangeMath.TryGetTerm(range.from, range.to, range.step, count, out var to)) dst.SetRange(range.from, to, range.step);
        else dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DropFirstRange(ref VmValue dst, ref VmValue source, VmFloatRange range, short count)
    {
        var length = GameEventScriptRangeMath.GetLength(range.from, range.to, range.step);
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

        if (GameEventScriptRangeMath.TryGetTerm(range.from, range.to, range.step, count + 1L, out var from)) dst.SetRange(from, range.to, range.step);
        else dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void TakeLastRange(ref VmValue dst, VmFloatRange range, short count)
    {
        var length = GameEventScriptRangeMath.GetLength(range.from, range.to, range.step);
        if (count <= 0 || length <= 0)
        {
            dst.SetRange(0, 0, 0);
            return;
        }

        if (count >= length)
        {
            dst.SetRange(range.from, range.to, range.step);
            return;
        }

        if (GameEventScriptRangeMath.TryGetTerm(range.from, range.to, range.step, length - count + 1L, out var from)) dst.SetRange(from, range.to, range.step);
        else dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DropLastRange(ref VmValue dst, ref VmValue source, VmFloatRange range, short count)
    {
        var length = GameEventScriptRangeMath.GetLength(range.from, range.to, range.step);
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

        if (GameEventScriptRangeMath.TryGetTerm(range.from, range.to, range.step, length - count, out var to)) dst.SetRange(range.from, to, range.step);
        else dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void TakeExtremeRange(ref VmValue dst, VmRange range, short count, bool highest)
    {
        var length = GameEventScriptRangeMath.GetLength(range.from, range.to, range.step);
        if (count <= 0 || length <= 0 || range.step == 0)
        {
            dst.SetRange(0, 0, 0);
            return;
        }

        var takeLength = count < length ? count : length;
        if (!GameEventScriptRangeMath.TryGetTerm(range.from, range.to, range.step, length, out var last))
        {
            dst.SetNothing();
            return;
        }

        var ascending = range.step > 0;
        if (highest)
        {
            if (ascending)
            {
                if (GameEventScriptRangeMath.TryGetTerm(last, range.from, -range.step, takeLength, out var to)) dst.SetRange(last, to, -range.step);
                else dst.SetNothing();
            }
            else
            {
                if (GameEventScriptRangeMath.TryGetTerm(range.from, range.to, range.step, takeLength, out var to)) dst.SetRange(range.from, to, range.step);
                else dst.SetNothing();
            }
        }
        else
        {
            if (ascending)
            {
                if (GameEventScriptRangeMath.TryGetTerm(range.from, range.to, range.step, takeLength, out var to)) dst.SetRange(range.from, to, range.step);
                else dst.SetNothing();
            }
            else
            {
                if (GameEventScriptRangeMath.TryGetTerm(last, range.from, -range.step, takeLength, out var to)) dst.SetRange(last, to, -range.step);
                else dst.SetNothing();
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DropExtremeRange(ref VmValue dst, ref VmValue source, VmRange range, short count, bool highest)
    {
        if (range.step > 0)
        {
            if (highest) DropLastRange(ref dst, ref source, range, count);
            else DropFirstRange(ref dst, ref source, range, count);
            return;
        }

        if (highest) DropFirstRange(ref dst, ref source, range, count);
        else DropLastRange(ref dst, ref source, range, count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void TakeExtremeRange(ref VmValue dst, VmFloatRange range, short count, bool highest)
    {
        var length = GameEventScriptRangeMath.GetLength(range.from, range.to, range.step);
        if (count <= 0 || length <= 0 || range.step == 0d)
        {
            dst.SetRange(0, 0, 0);
            return;
        }

        var takeLength = count < length ? count : length;
        if (!GameEventScriptRangeMath.TryGetTerm(range.from, range.to, range.step, length, out var last))
        {
            dst.SetNothing();
            return;
        }

        var ascending = range.step > 0d;
        if (highest)
        {
            if (ascending)
            {
                if (GameEventScriptRangeMath.TryGetTerm(last, range.from, -range.step, takeLength, out var to)) dst.SetRange(last, to, -range.step);
                else dst.SetNothing();
            }
            else
            {
                if (GameEventScriptRangeMath.TryGetTerm(range.from, range.to, range.step, takeLength, out var to)) dst.SetRange(range.from, to, range.step);
                else dst.SetNothing();
            }
        }
        else
        {
            if (ascending)
            {
                if (GameEventScriptRangeMath.TryGetTerm(range.from, range.to, range.step, takeLength, out var to)) dst.SetRange(range.from, to, range.step);
                else dst.SetNothing();
            }
            else
            {
                if (GameEventScriptRangeMath.TryGetTerm(last, range.from, -range.step, takeLength, out var to)) dst.SetRange(last, to, -range.step);
                else dst.SetNothing();
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DropExtremeRange(ref VmValue dst, ref VmValue source, VmFloatRange range, short count, bool highest)
    {
        if (range.step > 0d)
        {
            if (highest) DropLastRange(ref dst, ref source, range, count);
            else DropFirstRange(ref dst, ref source, range, count);
            return;
        }

        if (highest) DropFirstRange(ref dst, ref source, range, count);
        else DropLastRange(ref dst, ref source, range, count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CompareForOrdering(ref VmValue left, ref VmValue right)
    {
        if (left.IsNumeric && right.IsNumeric)
        {
            var unitComparison = left.Unit.CompareTo(right.Unit);
            if (unitComparison != 0) return unitComparison;
            return left.AsNumeric.CompareTo(right.AsNumeric);
        }

        var rankComparison = GetOrderingRank(ref left).CompareTo(GetOrderingRank(ref right));
        if (rankComparison != 0) return rankComparison;

        switch (left.Kind)
        {
            case Text when right.Kind is Text:
            case Tag when right.Kind is Tag:
                return StringComparer.Ordinal.Compare(left.ReadTextOrTag(), right.ReadTextOrTag());
            case Vector when right.Kind is Vector:
            case Point when right.Kind is Point:
                if (left.Unit != right.Unit) return left.Unit.CompareTo(right.Unit);
                if (left.ObjectValue is VmFloatTriplet leftTriplet && right.ObjectValue is VmFloatTriplet rightTriplet)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetOrderingRank(ref VmValue value)
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
