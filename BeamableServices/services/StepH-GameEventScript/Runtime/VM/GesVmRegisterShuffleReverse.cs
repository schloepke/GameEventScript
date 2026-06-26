using System;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;
using StepH.GameEventScript.Runtime.Values;

namespace StepH.GameEventScript.Runtime.VM;

internal static class GesVmRegisterShuffleReverse
{
    internal static void GesVmReverse(this GesVmState vmState, ushort destinationRegister, in GesValue source)
    {
        var dst = new GesValue();
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesValue[] list:
            {
                var result = new GesValue[list.Length];
                for (var i = 0; i < list.Length; i++) result[i] = list[list.Length - i - 1];
                dst.SetList(result);
                break;
            }
            case Dice when source.ObjectValue is int[] dice:
            {
                var result = new GesValue[dice.Length];
                for (var i = 0; i < dice.Length; i++) result[i].SetInteger(dice[dice.Length - i - 1]);
                dst.SetList(result);
                break;
            }
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesValueRangeInteger range:
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
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesValueRangeFloat range:
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
            case Iterator when source.ObjectValue is IGesIterator iterator:
                ReverseIterator(vmState, ref dst, iterator);
                break;
            default:
                dst.SetNothing();
                break;
        }

        vmState.SetValue(destinationRegister, in dst);
    }
    internal static void GesVmShuffle(this GesVmState vmState, ushort destinationRegister, in GesValue source, GesVmXoshiroRandom randomGenerator)
    {
        var dst = new GesValue();
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesValue[] list:
            {
                var result = new GesValue[list.Length];
                for (var i = 0; i < list.Length; i++) result[i] = list[i];
                ShuffleList(result, randomGenerator);
                dst.SetList(result);
                break;
            }
            case Dice when source.ObjectValue is int[] dice:
            {
                var result = new GesValue[dice.Length];
                for (var i = 0; i < dice.Length; i++) result[i].SetInteger(dice[i]);
                ShuffleList(result, randomGenerator);
                dst.SetList(result);
                break;
            }
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesValueRangeInteger range:
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

                var result = new GesValue[(int)length];
                for (var i = 0; i < result.Length; i++)
                {
                    if (GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, i + 1L, out var value)) result[i].SetInteger(value);
                    else result[i].SetNothing();
                }

                ShuffleList(result, randomGenerator);
                dst.SetList(result);
                break;
            }
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesValueRangeFloat range:
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

                var result = new GesValue[(int)length];
                for (var i = 0; i < result.Length; i++)
                {
                    if (GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, i + 1L, out var value)) result[i].SetFloat(value);
                    else result[i].SetNothing();
                }

                ShuffleList(result, randomGenerator);
                dst.SetList(result);
                break;
            }
            case Iterator when source.ObjectValue is IGesIterator iterator:
                ShuffleIterator(vmState, ref dst, iterator, randomGenerator);
                break;
            default:
                dst.SetNothing();
                break;
        }

        vmState.SetValue(destinationRegister, in dst);
    }
    private static void ReverseIterator(GesVmState vmState, ref GesValue dst, IGesIterator iterator)
    {
        var item = new GesValue();
        var values = new GesValue[16];
        var count = 0;
        try
        {
            while (iterator.TryNext(ref item))
            {
                if (count == values.Length)
                {
                    var resized = new GesValue[values.Length << 1];
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

        var result = new GesValue[count];
        for (var i = 0; i < count; i++) result[i] = values[count - i - 1];
        dst.SetList(result);
    }
    private static void ShuffleIterator(GesVmState vmState, ref GesValue dst, IGesIterator iterator, GesVmXoshiroRandom randomGenerator)
    {
        var item = new GesValue();
        var values = new GesValue[16];
        var count = 0;
        try
        {
            while (iterator.TryNext(ref item))
            {
                if (count == values.Length)
                {
                    var resized = new GesValue[values.Length << 1];
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

        var result = new GesValue[count];
        for (var i = 0; i < count; i++) result[i] = values[i];
        ShuffleList(result, randomGenerator);
        dst.SetList(result);
    }
    private static void ShuffleList(GesValue[] list, GesVmXoshiroRandom randomGenerator)
    {
        for (var i = list.Length - 1; i > 0; i--)
        {
            var swapIndex = randomGenerator.NextInclusiveInteger(0, i);
            (list[i], list[swapIndex]) = (list[swapIndex], list[i]);
        }
    }
}
