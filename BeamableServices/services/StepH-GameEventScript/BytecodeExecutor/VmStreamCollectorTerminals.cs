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
    internal static void VmStreamCollectFirst(ref this VmValue dst, ref VmValue iterator)
    {
        if (iterator is not { Kind: Stream, ObjectValue: IVmStream stream })
        {
            dst.SetNothing();
            return;
        }

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
    internal static void VmStreamCollectLast(ref this VmValue dst, ref VmValue iterator)
    {
        if (iterator is not { Kind: Stream, ObjectValue: IVmStream stream })
        {
            dst.SetNothing();
            return;
        }

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
    internal static void VmStreamCollectSingle(ref this VmValue dst, ref VmValue iterator)
    {
        if (iterator is not { Kind: Stream, ObjectValue: IVmStream stream })
        {
            dst.SetNothing();
            return;
        }

        var item = dst.OwningState.CreateNothing();
        var single = dst.OwningState.CreateNothing();
        try
        {
            if (!stream.TryNext(ref single))
            {
                dst.SetNothing();
                return;
            }

            if (stream.TryNext(ref item))
            {
                dst.SetNothing();
                return;
            }

            dst = single;
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }

}
