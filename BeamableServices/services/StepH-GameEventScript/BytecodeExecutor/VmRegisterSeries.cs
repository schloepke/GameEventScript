using System.Runtime.CompilerServices;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterSeries
{

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmSeriesTerm(ref this VmValue dst, ref VmValue seriesSource, ref VmValue termSlot)
    {
        if (seriesSource.Kind is not Series || seriesSource.ObjectValue is not GameEventScriptSeriesValue series || !TryReadTermIndex(ref termSlot, out var index))
        {
            dst.SetNothing();
            return;
        }

        dst.BindArguments(series.GetTerm(index));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmSeriesTake(ref this VmValue dst, ref VmValue seriesSource, short count)
    {
        switch (seriesSource.Kind)
        {
            case Series when seriesSource.ObjectValue is GameEventScriptSeriesValue series:
            {
                if (count <= 0)
                {
                    dst.SetList(dst.OwningState.EmptyList);
                    return;
                }

                var list = dst.OwningState.CreateList(count);
                for (var i = 0; i < count; i++) list.Items[i].BindArguments(series.GetTerm(i));
                dst.SetList(list);
                return;
            }
            case List when seriesSource.ObjectValue is VmListObject source:
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
                return;
            }
            case Dice when seriesSource.ObjectValue is int[] source:
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
                return;
            }
            default:
                dst.SetNothing();
                return;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmSeriesDrop(ref this VmValue dst, ref VmValue seriesSource, short count)
    {
        switch (seriesSource.Kind)
        {
            case Series when seriesSource.ObjectValue is GameEventScriptSeriesValue series:
                dst.SetSeries(series.Drop(count));
                return;
            case List when seriesSource.ObjectValue is VmListObject source:
            {
                if (count <= 0)
                {
                    dst.SetList(dst.OwningState.EmptyList);
                    return;
                }

                var start = count < source.Length ? count : source.Length;
                var length = source.Length - start;
                var list = dst.OwningState.CreateList(length);
                for (var i = 0; i < length; i++) list.Items[i] = source.Items[start + i];
                dst.SetList(list);
                return;
            }
            case Dice when seriesSource.ObjectValue is int[] source:
            {
                if (count <= 0)
                {
                    dst.SetDice([]);
                    return;
                }

                var start = count < source.Length ? count : source.Length;
                var length = source.Length - start;
                var dice = new int[length];
                for (var i = 0; i < length; i++) dice[i] = source[start + i];
                dst.SetDice(dice);
                return;
            }
            default:
                dst.SetNothing();
                return;
        }
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
                if (value.IsNumeric)
                {
                    return TryTruncateToInteger(value.AsNumeric, out index);
                }

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
