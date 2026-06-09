using System;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterShuffleReverse
{
    internal static void VmReverse(ref this VmValue dst, ref VmValue source)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is VmListObject list:
            {
                var result = dst.OwningState.CreateList(list.Length);
                for (var i = 0; i < list.Length; i++) result.Items[i] = list.Items[list.Length - i - 1];
                dst.SetList(result);
                return;
            }
            case Dice when source.ObjectValue is int[] dice:
            {
                var result = dst.OwningState.CreateList(dice.Length);
                for (var i = 0; i < dice.Length; i++) result.Items[i].SetInteger(dice[dice.Length - i - 1]);
                dst.SetList(result);
                return;
            }
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is VmRange range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.from, range.to, range.step);
                if (length <= 0)
                {
                    dst.SetRange(0, 0, 0);
                    return;
                }

                if (GameEventScriptRangeMath.TryGetTerm(range.from, range.to, range.step, length, out var last)) dst.SetRange(last, range.from, -range.step);
                else dst.SetNothing();
                return;
            }
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is VmFloatRange range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.from, range.to, range.step);
                if (length <= 0)
                {
                    dst.SetRange(0d, 0d, 0d);
                    return;
                }

                if (GameEventScriptRangeMath.TryGetTerm(range.from, range.to, range.step, length, out var last)) dst.SetRange(last, range.from, -range.step);
                else dst.SetNothing();
                return;
            }
            case Stream when source.ObjectValue is IVmStream stream:
                ReverseStream(ref dst, stream);
                return;
            default:
                dst.SetNothing();
                return;
        }
    }
    internal static void VmShuffle(ref this VmValue dst, ref VmValue source, GameEventScriptRandomGenerator randomGenerator)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is VmListObject list:
            {
                var result = dst.OwningState.CreateList(list.Length);
                for (var i = 0; i < list.Length; i++) result.Items[i] = list.Items[i];
                ShuffleList(result, randomGenerator);
                dst.SetList(result);
                return;
            }
            case Dice when source.ObjectValue is int[] dice:
            {
                var result = dst.OwningState.CreateList(dice.Length);
                for (var i = 0; i < dice.Length; i++) result.Items[i].SetInteger(dice[i]);
                ShuffleList(result, randomGenerator);
                dst.SetList(result);
                return;
            }
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is VmRange range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.from, range.to, range.step);
                if (length <= 0)
                {
                    dst.SetList(dst.OwningState.EmptyList);
                    return;
                }

                if (length > int.MaxValue)
                {
                    dst.SetNothing();
                    return;
                }

                var result = dst.OwningState.CreateList((int)length);
                for (var i = 0; i < result.Length; i++)
                {
                    if (GameEventScriptRangeMath.TryGetTerm(range.from, range.to, range.step, i + 1L, out var value)) result.Items[i].SetInteger(value);
                    else result.Items[i].SetNothing();
                }

                ShuffleList(result, randomGenerator);
                dst.SetList(result);
                return;
            }
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is VmFloatRange range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.from, range.to, range.step);
                if (length <= 0)
                {
                    dst.SetList(dst.OwningState.EmptyList);
                    return;
                }

                if (length > int.MaxValue)
                {
                    dst.SetNothing();
                    return;
                }

                var result = dst.OwningState.CreateList((int)length);
                for (var i = 0; i < result.Length; i++)
                {
                    if (GameEventScriptRangeMath.TryGetTerm(range.from, range.to, range.step, i + 1L, out var value)) result.Items[i].SetFloat(value);
                    else result.Items[i].SetNothing();
                }

                ShuffleList(result, randomGenerator);
                dst.SetList(result);
                return;
            }
            case Stream when source.ObjectValue is IVmStream stream:
                ShuffleStream(ref dst, stream, randomGenerator);
                return;
            default:
                dst.SetNothing();
                return;
        }
    }
    private static void ReverseStream(ref VmValue dst, IVmStream stream)
    {
        var item = dst.OwningState.CreateNothing();
        var values = dst.OwningState.CreateRegisterArray(16);
        var count = 0;
        try
        {
            while (stream.TryNext(ref item))
            {
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
        for (var i = 0; i < count; i++) result.Items[i] = values[count - i - 1];
        dst.SetList(result);
    }
    private static void ShuffleStream(ref VmValue dst, IVmStream stream, GameEventScriptRandomGenerator randomGenerator)
    {
        var item = dst.OwningState.CreateNothing();
        var values = dst.OwningState.CreateRegisterArray(16);
        var count = 0;
        try
        {
            while (stream.TryNext(ref item))
            {
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
        ShuffleList(result, randomGenerator);
        dst.SetList(result);
    }
    private static void ShuffleList(VmListObject list, GameEventScriptRandomGenerator randomGenerator)
    {
        for (var i = list.Length - 1; i > 0; i--)
        {
            var swapIndex = randomGenerator.NextInclusiveInt(0, i);
            (list.Items[i], list.Items[swapIndex]) = (list.Items[swapIndex], list.Items[i]);
        }
    }
}
