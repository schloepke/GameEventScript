using System;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.Runtime.VM;

internal static class GesVmRegisterShuffleReverse
{
    internal static void GesVmReverse(this GesVmState vmState, ushort destinationRegister, in GesVmValue source)
    {
        var dst = new GesVmValue();
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesVmValue[] list:
            {
                var result = new GesVmValue[list.Length];
                for (var i = 0; i < list.Length; i++) result[i] = list[list.Length - i - 1];
                dst.SetList(result);
                break;
            }
            case Dice when source.ObjectValue is int[] dice:
            {
                var result = new GesVmValue[dice.Length];
                for (var i = 0; i < dice.Length; i++) result[i].SetInteger(dice[dice.Length - i - 1]);
                dst.SetList(result);
                break;
            }
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeInteger range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
                if (length <= 0)
                {
                    dst.SetRange(0, 0, 0);
                    break;
                }

                if (GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, length, out var last)) dst.SetRange(last, range.From, -range.Step);
                else dst.SetNothing();
                break;
            }
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeFloat range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
                if (length <= 0)
                {
                    dst.SetRange(0d, 0d, 0d);
                    break;
                }

                if (GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, length, out var last)) dst.SetRange(last, range.From, -range.Step);
                else dst.SetNothing();
                break;
            }
            case Iterator when source.ObjectValue is IGesVmIterator iterator:
                ReverseIterator(vmState, ref dst, iterator);
                break;
            default:
                dst.SetNothing();
                break;
        }

        vmState.SetValue(destinationRegister, in dst);
    }
    internal static void GesVmShuffle(this GesVmState vmState, ushort destinationRegister, in GesVmValue source, GesVmXoshiroRandom randomGenerator)
    {
        var dst = new GesVmValue();
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesVmValue[] list:
            {
                var result = new GesVmValue[list.Length];
                for (var i = 0; i < list.Length; i++) result[i] = list[i];
                ShuffleList(result, randomGenerator);
                dst.SetList(result);
                break;
            }
            case Dice when source.ObjectValue is int[] dice:
            {
                var result = new GesVmValue[dice.Length];
                for (var i = 0; i < dice.Length; i++) result[i].SetInteger(dice[i]);
                ShuffleList(result, randomGenerator);
                dst.SetList(result);
                break;
            }
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeInteger range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
                if (length <= 0)
                {
                    dst.SetList(vmState.EmptyList);
                    break;
                }

                if (length > int.MaxValue)
                {
                    dst.SetNothing();
                    break;
                }

                var result = new GesVmValue[(int)length];
                for (var i = 0; i < result.Length; i++)
                {
                    if (GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, i + 1L, out var value)) result[i].SetInteger(value);
                    else result[i].SetNothing();
                }

                ShuffleList(result, randomGenerator);
                dst.SetList(result);
                break;
            }
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeFloat range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
                if (length <= 0)
                {
                    dst.SetList(vmState.EmptyList);
                    break;
                }

                if (length > int.MaxValue)
                {
                    dst.SetNothing();
                    break;
                }

                var result = new GesVmValue[(int)length];
                for (var i = 0; i < result.Length; i++)
                {
                    if (GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, i + 1L, out var value)) result[i].SetFloat(value);
                    else result[i].SetNothing();
                }

                ShuffleList(result, randomGenerator);
                dst.SetList(result);
                break;
            }
            case Iterator when source.ObjectValue is IGesVmIterator iterator:
                ShuffleIterator(vmState, ref dst, iterator, randomGenerator);
                break;
            default:
                dst.SetNothing();
                break;
        }

        vmState.SetValue(destinationRegister, in dst);
    }
    private static void ReverseIterator(GesVmState vmState, ref GesVmValue dst, IGesVmIterator iterator)
    {
        var item = new GesVmValue();
        var values = new GesVmValue[16];
        var count = 0;
        try
        {
            while (iterator.TryNext(ref item))
            {
                if (count == values.Length)
                {
                    var resized = new GesVmValue[values.Length << 1];
                    Array.Copy(values, resized, values.Length);
                    values = resized;
                }

                values[count++] = item;
            }
        }
        finally
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
        }

        var result = new GesVmValue[count];
        for (var i = 0; i < count; i++) result[i] = values[count - i - 1];
        dst.SetList(result);
    }
    private static void ShuffleIterator(GesVmState vmState, ref GesVmValue dst, IGesVmIterator iterator, GesVmXoshiroRandom randomGenerator)
    {
        var item = new GesVmValue();
        var values = new GesVmValue[16];
        var count = 0;
        try
        {
            while (iterator.TryNext(ref item))
            {
                if (count == values.Length)
                {
                    var resized = new GesVmValue[values.Length << 1];
                    Array.Copy(values, resized, values.Length);
                    values = resized;
                }

                values[count++] = item;
            }
        }
        finally
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
        }

        var result = new GesVmValue[count];
        for (var i = 0; i < count; i++) result[i] = values[i];
        ShuffleList(result, randomGenerator);
        dst.SetList(result);
    }
    private static void ShuffleList(GesVmValue[] list, GesVmXoshiroRandom randomGenerator)
    {
        for (var i = list.Length - 1; i > 0; i--)
        {
            var swapIndex = randomGenerator.NextInclusiveInteger(0, i);
            (list[i], list[swapIndex]) = (list[swapIndex], list[i]);
        }
    }
}
