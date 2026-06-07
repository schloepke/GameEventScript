using System;
using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;
using static StepH.GameEventScript.BytecodeExecutor.VmMathConstants;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterSortGroupDistinct
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmDistinct(ref this VmValue dst, ref VmValue source)
    {
        switch (source.Kind)
        {
            case Nothing:
                dst.SetNothing();
                return;
            case List when source.ObjectValue is VmListObject list:
            {
                if (list.Length == 0)
                {
                    dst.SetList(dst.OwningState.EmptyList);
                    return;
                }

                var result = dst.OwningState.CreateList(list.Length);
                var resultLength = 0;
                for (var i = 0; i < list.Length; i++)
                {
                    var item = list.Items[i];
                    var found = false;
                    for (var j = 0; j < resultLength; j++)
                    {
                        if (!result.Items[j].EqualsValue(ref item)) continue;
                        found = true;
                        break;
                    }

                    if (found) continue;
                    result.Items[resultLength++] = item;
                }

                if (resultLength == list.Length)
                {
                    dst.SetList(result);
                    return;
                }

                var compact = dst.OwningState.CreateList(resultLength);
                for (var i = 0; i < resultLength; i++) compact.Items[i] = result.Items[i];
                dst.SetList(compact);
                return;
            }
            case Dice when source.ObjectValue is int[] dice:
            {
                if (dice.Length == 0)
                {
                    dst.SetDice([]);
                    return;
                }

                var values = new int[dice.Length];
                var count = 0;
                var previous = 0;
                for (var i = 0; i < dice.Length; i++)
                {
                    var value = dice[i];
                    if (count > 0 && value == previous) continue;
                    values[count++] = value;
                    previous = value;
                }

                if (count == values.Length)
                {
                    dst.SetDice(values);
                    return;
                }

                var compact = new int[count];
                Array.Copy(values, compact, count);
                dst.SetDice(compact);
                return;
            }
            case Stream when source.ObjectValue is IVmStream stream:
            {
                var item = dst.OwningState.CreateNothing();
                var values = dst.OwningState.CreateRegisterArray(16);
                var count = 0;
                try
                {
                    while (stream.TryNext(ref item))
                    {
                        var found = false;
                        for (var i = 0; i < count; i++)
                        {
                            if (!values[i].EqualsValue(ref item)) continue;
                            found = true;
                            break;
                        }

                        if (found) continue;
                        if (count == values.Length)
                        {
                            var resized = dst.OwningState.CreateRegisterArray(values.Length << 1);
                            Array.Copy(values, resized, values.Length);
                            values = resized;
                        }

                        values[count++] = item;
                    }
                }
                finally
                {
                    if (stream is IDisposable disposable) disposable.Dispose();
                }

                var result = dst.OwningState.CreateList(count);
                for (var i = 0; i < count; i++) result.Items[i] = values[i];
                dst.SetList(result);
                return;
            }
            default:
                dst.SetNothing();
                return;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmDistinctBy(ref this VmValue dst, ref VmValue source, ushort itemSlot, ushort keyEntryAddress, IVmStreamEntryEvaluator evaluator, ushort destinationSlot)
    {
        var state = dst.OwningState;
        switch (source.Kind)
        {
            case List when source.ObjectValue is VmListObject list:
            {
                if (list.Length == 0)
                {
                    state.Register(destinationSlot).SetList(state.EmptyList);
                    return;
                }

                var values = state.CreateList(list.Length);
                var keys = state.CreateRegisterArray(list.Length);
                var key = state.CreateNothing();
                var count = 0;
                for (var i = 0; i < list.Length; i++)
                {
                    var item = list.Items[i];
                    if (!evaluator.TryEvaluateStreamEntry(keyEntryAddress, itemSlot, ref item, null, ref key))
                    {
                        state.Register(destinationSlot).SetNothing();
                        return;
                    }

                    var found = false;
                    for (var j = 0; j < count; j++)
                    {
                        if (!keys[j].EqualsValue(ref key)) continue;
                        found = true;
                        break;
                    }

                    if (found) continue;
                    values.Items[count] = item;
                    keys[count] = key;
                    count++;
                }

                if (count == list.Length)
                {
                    state.Register(destinationSlot).SetList(values);
                    return;
                }

                var compact = state.CreateList(count);
                for (var i = 0; i < count; i++) compact.Items[i] = values.Items[i];
                state.Register(destinationSlot).SetList(compact);
                return;
            }
            case Stream when source.ObjectValue is IVmStream stream:
            {
                var item = state.CreateNothing();
                var key = state.CreateNothing();
                var values = state.CreateRegisterArray(16);
                var keys = state.CreateRegisterArray(16);
                var count = 0;
                try
                {
                    while (stream.TryNext(ref item))
                    {
                        if (!evaluator.TryEvaluateStreamEntry(keyEntryAddress, itemSlot, ref item, null, ref key))
                        {
                            state.Register(destinationSlot).SetNothing();
                            return;
                        }

                        var found = false;
                        for (var i = 0; i < count; i++)
                        {
                            if (!keys[i].EqualsValue(ref key)) continue;
                            found = true;
                            break;
                        }

                        if (found) continue;
                        if (count == values.Length)
                        {
                            var resizedValues = state.CreateRegisterArray(values.Length << 1);
                            var resizedKeys = state.CreateRegisterArray(keys.Length << 1);
                            Array.Copy(values, resizedValues, values.Length);
                            Array.Copy(keys, resizedKeys, keys.Length);
                            values = resizedValues;
                            keys = resizedKeys;
                        }

                        values[count] = item;
                        keys[count] = key;
                        count++;
                    }
                }
                finally
                {
                    if (stream is IDisposable disposable) disposable.Dispose();
                }

                var result = state.CreateList(count);
                for (var i = 0; i < count; i++) result.Items[i] = values[i];
                state.Register(destinationSlot).SetList(result);
                return;
            }
            default:
                state.Register(destinationSlot).SetNothing();
                return;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmGroupBy(ref this VmValue dst, ref VmValue source, ushort itemSlot, ushort keyEntryAddress, IVmStreamEntryEvaluator evaluator)
    {
        dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmSortAscending(ref this VmValue dst, ref VmValue source)
    {
        VmSort(ref dst, ref source, descending: false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmSortDescending(ref this VmValue dst, ref VmValue source)
    {
        VmSort(ref dst, ref source, descending: true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmOrderByAscending(ref this VmValue dst, ref VmValue source, ushort itemSlot, ushort keyEntryAddress, IVmStreamEntryEvaluator evaluator, ushort destinationSlot)
    {
        VmOrderBy(ref dst, ref source, itemSlot, keyEntryAddress, evaluator, destinationSlot, descending: false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmOrderByDescending(ref this VmValue dst, ref VmValue source, ushort itemSlot, ushort keyEntryAddress, IVmStreamEntryEvaluator evaluator, ushort destinationSlot)
    {
        VmOrderBy(ref dst, ref source, itemSlot, keyEntryAddress, evaluator, destinationSlot, descending: true);
    }

    private static void VmSort(ref VmValue dst, ref VmValue source, bool descending)
    {
        var state = dst.OwningState;
        switch (source.Kind)
        {
            case List when source.ObjectValue is VmListObject list:
            {
                if (list.Length == 0)
                {
                    dst.SetList(state.EmptyList);
                    return;
                }

                var result = state.CreateList(list.Length);
                for (var i = 0; i < list.Length; i++) result.Items[i] = list.Items[i];
                if (!SortValues(result.Items, list.Length, descending))
                {
                    dst.SetNothing();
                    return;
                }

                dst.SetList(result);
                return;
            }
            case Dice when source.ObjectValue is int[] dice:
            {
                var result = state.CreateList(dice.Length);
                if (descending)
                {
                    for (var i = 0; i < dice.Length; i++) result.Items[i].SetInteger(dice[i]);
                }
                else
                {
                    for (var i = 0; i < dice.Length; i++) result.Items[i].SetInteger(dice[dice.Length - i - 1]);
                }

                dst.SetList(result);
                return;
            }
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is VmRange range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.from, range.to, range.step);
                if (length == 0)
                {
                    dst.SetRange(0, 0, 0);
                    return;
                }

                var sourceDescending = range.step < 0;
                if (sourceDescending == descending)
                {
                    dst.SetRange(range.from, range.to, range.step);
                    return;
                }

                if (GameEventScriptRangeMath.TryGetTerm(range.from, range.to, range.step, length, out var last))
                {
                    dst.SetRange(last, range.from, -range.step);
                    return;
                }

                dst.SetNothing();
                return;
            }
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is VmFloatRange range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.from, range.to, range.step);
                if (length == 0)
                {
                    dst.SetRange(0, 0, 0);
                    return;
                }

                var sourceDescending = range.step < 0d;
                if (sourceDescending == descending)
                {
                    dst.SetRange(range.from, range.to, range.step);
                    return;
                }

                if (GameEventScriptRangeMath.TryGetTerm(range.from, range.to, range.step, length, out var last))
                {
                    dst.SetRange(last, range.from, -range.step);
                    return;
                }

                dst.SetNothing();
                return;
            }
            case Stream when source.ObjectValue is IVmStream stream:
            {
                var item = state.CreateNothing();
                var values = state.CreateRegisterArray(16);
                var count = 0;
                try
                {
                    while (stream.TryNext(ref item))
                    {
                        if (count == values.Length)
                        {
                            var resized = state.CreateRegisterArray(values.Length << 1);
                            Array.Copy(values, resized, values.Length);
                            values = resized;
                        }

                        values[count++] = item;
                    }
                }
                finally
                {
                    if (stream is IDisposable disposable) disposable.Dispose();
                }

                if (!SortValues(values, count, descending))
                {
                    dst.SetNothing();
                    return;
                }

                var result = state.CreateList(count);
                for (var i = 0; i < count; i++) result.Items[i] = values[i];
                dst.SetList(result);
                return;
            }
            default:
                dst.SetNothing();
                return;
        }
    }

    private static void VmOrderBy(ref VmValue dst, ref VmValue source, ushort itemSlot, ushort keyEntryAddress, IVmStreamEntryEvaluator evaluator, ushort destinationSlot, bool descending)
    {
        var state = dst.OwningState;
        switch (source.Kind)
        {
            case List when source.ObjectValue is VmListObject list:
            {
                if (list.Length == 0)
                {
                    state.Register(destinationSlot).SetList(state.EmptyList);
                    return;
                }

                var values = state.CreateRegisterArray(list.Length);
                var keys = state.CreateRegisterArray(list.Length);
                var key = state.CreateNothing();
                for (var i = 0; i < list.Length; i++)
                {
                    var item = list.Items[i];
                    if (!evaluator.TryEvaluateStreamEntry(keyEntryAddress, itemSlot, ref item, null, ref key))
                    {
                        state.Register(destinationSlot).SetNothing();
                        return;
                    }

                    values[i] = item;
                    keys[i] = key;
                }

                if (!SortValuesByKeys(values, keys, list.Length, descending))
                {
                    state.Register(destinationSlot).SetNothing();
                    return;
                }

                var result = state.CreateList(list.Length);
                for (var i = 0; i < list.Length; i++) result.Items[i] = values[i];
                state.Register(destinationSlot).SetList(result);
                return;
            }
            case Stream when source.ObjectValue is IVmStream stream:
            {
                var item = state.CreateNothing();
                var key = state.CreateNothing();
                var values = state.CreateRegisterArray(16);
                var keys = state.CreateRegisterArray(16);
                var count = 0;
                try
                {
                    while (stream.TryNext(ref item))
                    {
                        if (count == values.Length)
                        {
                            var resizedValues = state.CreateRegisterArray(values.Length << 1);
                            var resizedKeys = state.CreateRegisterArray(keys.Length << 1);
                            Array.Copy(values, resizedValues, values.Length);
                            Array.Copy(keys, resizedKeys, keys.Length);
                            values = resizedValues;
                            keys = resizedKeys;
                        }

                        if (!evaluator.TryEvaluateStreamEntry(keyEntryAddress, itemSlot, ref item, null, ref key))
                        {
                            state.Register(destinationSlot).SetNothing();
                            return;
                        }

                        values[count] = item;
                        keys[count] = key;
                        count++;
                    }
                }
                finally
                {
                    if (stream is IDisposable disposable) disposable.Dispose();
                }

                if (!SortValuesByKeys(values, keys, count, descending))
                {
                    state.Register(destinationSlot).SetNothing();
                    return;
                }

                var result = state.CreateList(count);
                for (var i = 0; i < count; i++) result.Items[i] = values[i];
                state.Register(destinationSlot).SetList(result);
                return;
            }
            default:
                state.Register(destinationSlot).SetNothing();
                return;
        }
    }

    private static bool SortValues(VmValue[] values, int count, bool descending)
    {
        for (var i = 1; i < count; i++)
        {
            var value = values[i];
            var j = i - 1;
            while (j >= 0)
            {
                if (!TryCompareAscending(ref values[j], ref value, out var comparison)) return false;
                if ((descending ? -comparison : comparison) <= 0) break;
                values[j + 1] = values[j];
                j--;
            }

            values[j + 1] = value;
        }

        return true;
    }

    private static bool SortValuesByKeys(VmValue[] values, VmValue[] keys, int count, bool descending)
    {
        for (var i = 1; i < count; i++)
        {
            var value = values[i];
            var key = keys[i];
            var j = i - 1;
            while (j >= 0)
            {
                if (!TryCompareAscending(ref keys[j], ref key, out var comparison)) return false;
                if ((descending ? -comparison : comparison) <= 0) break;
                values[j + 1] = values[j];
                keys[j + 1] = keys[j];
                j--;
            }

            values[j + 1] = value;
            keys[j + 1] = key;
        }

        return true;
    }

    private static bool TryCompareAscending(ref VmValue a, ref VmValue b, out int comparison)
    {
        comparison = 0;
        if (a.Kind is Nothing || b.Kind is Nothing)
        {
            return false;
        }

        if (a.IsNumeric && b.IsNumeric)
        {
            if (a.Unit != b.Unit)
            {
                return false;
            }

            var left = a.AsNumeric;
            var right = b.AsNumeric;
            if (double.IsNaN(left) || double.IsNaN(right))
            {
                return false;
            }

            comparison = left.CompareTo(right);
            return true;
        }

        var leftRank = GetStableRank(ref a);
        var rightRank = GetStableRank(ref b);
        if (leftRank != rightRank)
        {
            comparison = leftRank.CompareTo(rightRank);
            return true;
        }

        switch (a.Kind)
        {
            case Text when b.Kind is Text:
            case Tag when b.Kind is Tag:
                comparison = StringComparer.Ordinal.Compare(a.ReadTextOrTag(), b.ReadTextOrTag());
                return true;
            case Vector when b.Kind is Vector:
            case Point when b.Kind is Point:
                if (a.Unit != b.Unit ||
                    a.ObjectValue is not VmFloatTriplet leftTriplet ||
                    b.ObjectValue is not VmFloatTriplet rightTriplet)
                {
                    return false;
                }

                comparison = leftTriplet.X.CompareTo(rightTriplet.X);
                if (comparison == 0) comparison = leftTriplet.Y.CompareTo(rightTriplet.Y);
                if (comparison == 0) comparison = leftTriplet.Z.CompareTo(rightTriplet.Z);
                return true;
            case GameEventScriptBytecodeTypeKind.Boolean when b.Kind is GameEventScriptBytecodeTypeKind.Boolean:
                comparison = a.IsTrue.CompareTo(b.IsTrue);
                return true;
            default:
                comparison = 0;
                return true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetStableRank(ref VmValue value)
    {
        switch (value.Kind)
        {
            case Integer:
            case Float:
            case Percentage:
                return 1;
            case Tag:
            {
                var text = value.ReadTextOrTag();
                return IsNumericTag(text) ? 1 : 3;
            }
            case Text:
                return 2;
            case Vector:
                return 4;
            case Point:
                return 5;
            case GameEventScriptBytecodeTypeKind.Boolean:
                return 6;
            case GameEventScriptBytecodeTypeKind.Range:
                return 8;
            case Message:
                return 9;
            case Handler:
                return 10;
            case List:
                return 11;
            case Map:
                return 12;
            case Dice:
                return 13;
            default:
                return 8;
        }
    }
}
