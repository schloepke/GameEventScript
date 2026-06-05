using System.Runtime.CompilerServices;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterSeries
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmTerm(ref this VmValue dst, ref VmValue source, ref VmValue termSlot)
    {
        if (source.Kind is not Series || source.ObjectValue is not GameEventScriptSeriesValue series || !TryReadTermIndex(ref termSlot, out var index))
        {
            dst.SetNothing();
            return;
        }

        dst.BindArguments(series.GetTerm(index));
    }

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
            case Range when source.ObjectValue is VmRange range:
                TakeFirstRange(ref dst, range, count);
                return;
            case Range when source.ObjectValue is VmFloatRange range:
                TakeFirstRange(ref dst, range, count);
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
            case Range when source.ObjectValue is VmRange range:
                DropFirstRange(ref dst, ref source, range, count);
                return;
            case Range when source.ObjectValue is VmFloatRange range:
                DropFirstRange(ref dst, ref source, range, count);
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
            case Range when source.ObjectValue is VmRange range:
                TakeLastRange(ref dst, range, count);
                return;
            case Range when source.ObjectValue is VmFloatRange range:
                TakeLastRange(ref dst, range, count);
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
            case Range when source.ObjectValue is VmRange range:
                DropLastRange(ref dst, ref source, range, count);
                return;
            case Range when source.ObjectValue is VmFloatRange range:
                DropLastRange(ref dst, ref source, range, count);
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
    private static bool TryReadTermIndex(ref VmValue value, out long index)
    {
        switch (value.Kind)
        {
            case Integer:
                index = value.IntegerValue;
                return true;
            case Float or Percentage:
                return TryTruncateToInteger(value.FloatValue, out index);
            case Boolean:
                index = value.IsTrue ? 1 : 0;
                return true;
            default:
                if (value.IsNumeric) return TryTruncateToInteger(value.AsNumeric, out index);
                index = 0;
                return false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryTruncateToInteger(double value, out long index)
    {
        if (double.IsNaN(value))
        {
            index = 0;
            return false;
        }

        if (value <= long.MinValue)
        {
            index = long.MinValue;
            return true;
        }

        if (value >= long.MaxValue)
        {
            index = long.MaxValue;
            return true;
        }

        index = (long)value;
        return true;
    }
}
