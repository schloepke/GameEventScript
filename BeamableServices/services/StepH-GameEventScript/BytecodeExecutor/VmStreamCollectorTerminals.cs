namespace StepH.GameEventScript.BytecodeExecutor;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

internal static class VmStreamCollectorTerminals
{

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmStreamCollectList(ref this VmValue dst, ref VmValue iterator)
    {
        if (iterator is not { Kind: Stream, ObjectValue: IVmStream stream })
        {
            dst.SetNothing();
            return;
        }

        var item = dst.OwningState.CreateNothing();
        var count = 0;
        var buffer = new VmValue[16];
        try
        {
            while (stream.TryNext(ref item))
            {
                if (count == buffer.Length) Array.Resize(ref buffer, buffer.Length << 1);
                buffer[count++] = item;
            }

            if (count == 0)
            {
                dst.SetList(dst.OwningState.EmptyList);
                return;
            }

            var list = dst.OwningState.CreateList(count);
            Array.Copy(buffer, list.Items, count);
            dst.SetList(list);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmStreamCollectMap(ref this VmValue dst, ref VmValue iterator, ushort itemSlot, ushort keyEntryAddress, IVmStreamEntryEvaluator evaluator)
    {
        if (iterator is not { Kind: Stream, ObjectValue: IVmStream stream })
        {
            dst.SetNothing();
            return;
        }

        var item = dst.OwningState.CreateNothing();
        var keyValue = dst.OwningState.CreateNothing();
        var map = new Dictionary<string, VmValue>(StringComparer.Ordinal);
        try
        {
            while (stream.TryNext(ref item))
            {
                if (!evaluator.TryEvaluateStreamEntry(keyEntryAddress, itemSlot, ref item, null, ref keyValue))
                {
                    dst.SetNothing();
                    return;
                }

                var key = keyValue.Kind switch
                {
                    Text or Tag => keyValue.ReadTextOrTag(),
                    Nothing => string.Empty,
                    _ => keyValue.ConvertToText()
                };
                if (key.Length == 0) continue;
                map[key] = item;
            }

            dst.SetMap(new VmMapObject(dst.OwningState, map));
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmStreamCollectMapValue(ref this VmValue dst, ref VmValue iterator, ushort itemSlot, ushort keyEntryAddress, ushort valueEntryAddress, IVmStreamEntryEvaluator evaluator)
    {
        if (iterator is not { Kind: Stream, ObjectValue: IVmStream stream })
        {
            dst.SetNothing();
            return;
        }

        var item = dst.OwningState.CreateNothing();
        var keyValue = dst.OwningState.CreateNothing();
        var value = dst.OwningState.CreateNothing();
        var map = new Dictionary<string, VmValue>(StringComparer.Ordinal);
        try
        {
            while (stream.TryNext(ref item))
            {
                if (!evaluator.TryEvaluateStreamEntry(keyEntryAddress, itemSlot, ref item, null, ref keyValue))
                {
                    dst.SetNothing();
                    return;
                }

                var key = keyValue.Kind switch
                {
                    Text or Tag => keyValue.ReadTextOrTag(),
                    Nothing => string.Empty,
                    _ => keyValue.ConvertToText()
                };
                if (key.Length == 0) continue;

                if (!evaluator.TryEvaluateStreamEntry(valueEntryAddress, itemSlot, ref item, null, ref value))
                {
                    dst.SetNothing();
                    return;
                }

                map[key] = value;
            }

            dst.SetMap(new VmMapObject(dst.OwningState, map));
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmFirst(ref this VmValue dst, ref VmValue source)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is VmListObject list:
                dst = list.Length > 0 ? list.Items[0] : dst.OwningState.CreateNothing();
                return;
            case Dice when source.ObjectValue is int[] dice:
                if (dice.Length > 0) dst.SetInteger(dice[0]);
                else dst.SetNothing();
                return;
            case StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is VmRange range:
                if (StepH.GameEventScript.Types.GameEventScriptRangeMath.GetLength(range.from, range.to, range.step) > 0) dst.SetInteger(range.from);
                else dst.SetNothing();
                return;
            case StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is VmFloatRange range:
                if (StepH.GameEventScript.Types.GameEventScriptRangeMath.GetLength(range.from, range.to, range.step) > 0) dst.SetFloat(range.from);
                else dst.SetNothing();
                return;
            case Map or Custom when source.ObjectValue is VmMapObject map:
                dst = map.ValueList.Length > 0 ? map.ValueList.Items[0] : dst.OwningState.CreateNothing();
                return;
            case Vector or Point when source.ObjectValue is VmFloatTriplet triplet:
                dst.SetFloat(triplet.X);
                return;
            case Text or Tag:
                FirstFromText(ref dst, ref source);
                return;
            case Stream when source.ObjectValue is IVmStream stream:
                FirstFromStream(ref dst, stream);
                return;
            default:
                dst.SetNothing();
                return;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmLast(ref this VmValue dst, ref VmValue source)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is VmListObject list:
                dst = list.Length > 0 ? list.Items[list.Length - 1] : dst.OwningState.CreateNothing();
                return;
            case Dice when source.ObjectValue is int[] dice:
                if (dice.Length > 0) dst.SetInteger(dice[^1]);
                else dst.SetNothing();
                return;
            case StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is VmRange range:
                if (StepH.GameEventScript.Types.GameEventScriptRangeMath.TryGetTerm(range.from, range.to, range.step, StepH.GameEventScript.Types.GameEventScriptRangeMath.GetLength(range.from, range.to, range.step), out var lastInteger)) dst.SetInteger(lastInteger);
                else dst.SetNothing();
                return;
            case StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is VmFloatRange range:
                if (StepH.GameEventScript.Types.GameEventScriptRangeMath.TryGetTerm(range.from, range.to, range.step, StepH.GameEventScript.Types.GameEventScriptRangeMath.GetLength(range.from, range.to, range.step), out var lastFloat)) dst.SetFloat(lastFloat);
                else dst.SetNothing();
                return;
            case Map or Custom when source.ObjectValue is VmMapObject map:
                dst = map.ValueList.Length > 0 ? map.ValueList.Items[map.ValueList.Length - 1] : dst.OwningState.CreateNothing();
                return;
            case Vector or Point when source.ObjectValue is VmFloatTriplet triplet:
                dst.SetFloat(triplet.Z);
                return;
            case Text or Tag:
                LastFromText(ref dst, ref source);
                return;
            case Stream when source.ObjectValue is IVmStream stream:
                LastFromStream(ref dst, stream);
                return;
            default:
                dst.SetNothing();
                return;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmSingle(ref this VmValue dst, ref VmValue source)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is VmListObject list:
                dst = list.Length == 1 ? list.Items[0] : dst.OwningState.CreateNothing();
                return;
            case Dice when source.ObjectValue is int[] dice:
                if (dice.Length == 1) dst.SetInteger(dice[0]);
                else dst.SetNothing();
                return;
            case StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is VmRange range:
                if (StepH.GameEventScript.Types.GameEventScriptRangeMath.GetLength(range.from, range.to, range.step) == 1) dst.SetInteger(range.from);
                else dst.SetNothing();
                return;
            case StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is VmFloatRange range:
                if (StepH.GameEventScript.Types.GameEventScriptRangeMath.GetLength(range.from, range.to, range.step) == 1) dst.SetFloat(range.from);
                else dst.SetNothing();
                return;
            case Map or Custom when source.ObjectValue is VmMapObject map:
                dst = map.ValueList.Length == 1 ? map.ValueList.Items[0] : dst.OwningState.CreateNothing();
                return;
            case Text or Tag:
                SingleFromText(ref dst, ref source);
                return;
            case Stream when source.ObjectValue is IVmStream stream:
                SingleFromStream(ref dst, stream);
                return;
            default:
                dst.SetNothing();
                return;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FirstFromStream(ref VmValue dst, IVmStream stream)
    {
        var item = dst.OwningState.CreateNothing();
        try
        {
            if (stream.TryNext(ref item)) dst = item;
            else dst.SetNothing();
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LastFromStream(ref VmValue dst, IVmStream stream)
    {
        var item = dst.OwningState.CreateNothing();
        var last = dst.OwningState.CreateNothing();
        var found = false;
        try
        {
            while (stream.TryNext(ref item))
            {
                last = item;
                found = true;
            }

            if (found) dst = last;
            else dst.SetNothing();
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SingleFromStream(ref VmValue dst, IVmStream stream)
    {
        var item = dst.OwningState.CreateNothing();
        try
        {
            if (!stream.TryNext(ref item))
            {
                dst.SetNothing();
                return;
            }

            var second = dst.OwningState.CreateNothing();
            if (stream.TryNext(ref second)) dst.SetNothing();
            else dst = item;
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FirstFromText(ref VmValue dst, ref VmValue source)
    {
        var text = source.ReadTextOrTag();
        if (text.Length > 0) dst.SetText(text[0].ToString());
        else dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LastFromText(ref VmValue dst, ref VmValue source)
    {
        var text = source.ReadTextOrTag();
        if (text.Length > 0) dst.SetText(text[^1].ToString());
        else dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SingleFromText(ref VmValue dst, ref VmValue source)
    {
        var text = source.ReadTextOrTag();
        if (text.Length == 1) dst.SetText(text);
        else dst.SetNothing();
    }

}
