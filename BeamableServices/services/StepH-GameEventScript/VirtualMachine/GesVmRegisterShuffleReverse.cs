using System;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmRegisterShuffleReverse
{
    internal static void GesVmReverse(ref this GesVmValue dst, ref GesVmValue source)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesVmValue[] list:
            {
                var result = dst.OwningState.CreateList(list.Length);
                for (var i = 0; i < list.Length; i++) result[i] = list[list.Length - i - 1];
                dst.SetList(result);
                return;
            }
            case Dice when source.ObjectValue is int[] dice:
            {
                var result = dst.OwningState.CreateList(dice.Length);
                for (var i = 0; i < dice.Length; i++) result[i].SetInteger(dice[dice.Length - i - 1]);
                dst.SetList(result);
                return;
            }
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeInteger range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
                if (length <= 0)
                {
                    dst.SetRange(0, 0, 0);
                    return;
                }

                if (GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, length, out var last)) dst.SetRange(last, range.From, -range.Step);
                else dst.SetNothing();
                return;
            }
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeFloat range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
                if (length <= 0)
                {
                    dst.SetRange(0d, 0d, 0d);
                    return;
                }

                if (GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, length, out var last)) dst.SetRange(last, range.From, -range.Step);
                else dst.SetNothing();
                return;
            }
            case Stream when source.ObjectValue is IGesVmStream stream:
                ReverseStream(ref dst, stream);
                return;
            default:
                dst.SetNothing();
                return;
        }
    }
    internal static void GesVmShuffle(ref this GesVmValue dst, ref GesVmValue source, GesVmXoshiroRandom randomGenerator)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesVmValue[] list:
            {
                var result = dst.OwningState.CreateList(list.Length);
                for (var i = 0; i < list.Length; i++) result[i] = list[i];
                ShuffleList(result, randomGenerator);
                dst.SetList(result);
                return;
            }
            case Dice when source.ObjectValue is int[] dice:
            {
                var result = dst.OwningState.CreateList(dice.Length);
                for (var i = 0; i < dice.Length; i++) result[i].SetInteger(dice[i]);
                ShuffleList(result, randomGenerator);
                dst.SetList(result);
                return;
            }
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeInteger range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
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
                    if (GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, i + 1L, out var value)) result[i].SetInteger(value);
                    else result[i].SetNothing();
                }

                ShuffleList(result, randomGenerator);
                dst.SetList(result);
                return;
            }
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeFloat range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
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
                    if (GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, i + 1L, out var value)) result[i].SetFloat(value);
                    else result[i].SetNothing();
                }

                ShuffleList(result, randomGenerator);
                dst.SetList(result);
                return;
            }
            case Stream when source.ObjectValue is IGesVmStream stream:
                ShuffleStream(ref dst, stream, randomGenerator);
                return;
            default:
                dst.SetNothing();
                return;
        }
    }
    private static void ReverseStream(ref GesVmValue dst, IGesVmStream stream)
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
        for (var i = 0; i < count; i++) result[i] = values[count - i - 1];
        dst.SetList(result);
    }
    private static void ShuffleStream(ref GesVmValue dst, IGesVmStream stream, GesVmXoshiroRandom randomGenerator)
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
        for (var i = 0; i < count; i++) result[i] = values[i];
        ShuffleList(result, randomGenerator);
        dst.SetList(result);
    }
    private static void ShuffleList(GesVmValue[] list, GesVmXoshiroRandom randomGenerator)
    {
        for (var i = list.Length - 1; i > 0; i--)
        {
            var swapIndex = randomGenerator.NextInclusiveInt(0, i);
            (list[i], list[swapIndex]) = (list[swapIndex], list[i]);
        }
    }
}
