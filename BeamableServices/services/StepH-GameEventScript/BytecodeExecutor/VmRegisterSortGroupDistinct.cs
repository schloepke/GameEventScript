using System;
using System.Runtime.CompilerServices;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

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
        dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmSortDescending(ref this VmValue dst, ref VmValue source)
    {
        dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmOrderByAscending(ref this VmValue dst, ref VmValue source, ushort itemSlot, ushort keyEntryAddress, IVmStreamEntryEvaluator evaluator)
    {
        dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmOrderByDescending(ref this VmValue dst, ref VmValue source, ushort itemSlot, ushort keyEntryAddress, IVmStreamEntryEvaluator evaluator)
    {
        dst.SetNothing();
    }
}
