using System;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Runtime.Values;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.Runtime.VM;

internal static class GesVmRegisterShuffleReverse
{
    internal static void GesVmReverse(this GesVmState vmState, ushort destinationRegister, in GesValue source)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesValue[] list:
            {
                var result = new GesValue[list.Length];
                for (var i = 0; i < list.Length; i++) result[i] = list[list.Length - i - 1];
                vmState.SetList(destinationRegister, result);
                break;
            }
            case Dice when source.ObjectValue is int[] dice:
            {
                var result = new GesValue[dice.Length];
                for (var i = 0; i < dice.Length; i++) result[i].SetInteger(dice[dice.Length - i - 1]);
                vmState.SetList(destinationRegister, result);
                break;
            }
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesValueRangeInteger range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
                if (length <= 0)
                {
                    vmState.SetRange(destinationRegister, 0, 0, 0);
                    break;
                }

                if (GameEventScriptRangeMath.GetTerm(range.From, range.To, range.Step, length) is { } last) vmState.SetRange(destinationRegister, last, range.From, -range.Step);
                else vmState.SetNothing(destinationRegister);
                break;
            }
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesValueRangeFloat range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
                if (length <= 0)
                {
                    vmState.SetRange(destinationRegister, 0d, 0d, 0d);
                    break;
                }

                if (GameEventScriptRangeMath.GetTerm(range.From, range.To, range.Step, length) is { } last) vmState.SetRange(destinationRegister, last, range.From, -range.Step);
                else vmState.SetNothing(destinationRegister);
                break;
            }
            case Iterator when source.ObjectValue is IGesIterator iterator:
                ReverseIterator(vmState, destinationRegister, iterator);
                break;
            default:
                vmState.SetNothing(destinationRegister);
                break;
        }
    }
    internal static void GesVmShuffle(this GesVmState vmState, ushort destinationRegister, in GesValue source, GameEventScriptRandomGenerator randomGenerator)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesValue[] list:
            {
                var result = new GesValue[list.Length];
                for (var i = 0; i < list.Length; i++) result[i] = list[i];
                ShuffleList(result, randomGenerator);
                vmState.SetList(destinationRegister, result);
                break;
            }
            case Dice when source.ObjectValue is int[] dice:
            {
                var result = new GesValue[dice.Length];
                for (var i = 0; i < dice.Length; i++) result[i].SetInteger(dice[i]);
                ShuffleList(result, randomGenerator);
                vmState.SetList(destinationRegister, result);
                break;
            }
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesValueRangeInteger range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
                if (length <= 0)
                {
                    vmState.SetList(destinationRegister, vmState.EmptyList);
                    break;
                }

                if (length > int.MaxValue)
                {
                    vmState.SetNothing(destinationRegister);
                    break;
                }

                var result = new GesValue[(int)length];
                for (var i = 0; i < result.Length; i++)
                {
                    if (GameEventScriptRangeMath.GetTerm(range.From, range.To, range.Step, i + 1L) is { } value) result[i].SetInteger(value);
                    else result[i].SetNothing();
                }

                ShuffleList(result, randomGenerator);
                vmState.SetList(destinationRegister, result);
                break;
            }
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesValueRangeFloat range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
                if (length <= 0)
                {
                    vmState.SetList(destinationRegister, vmState.EmptyList);
                    break;
                }

                if (length > int.MaxValue)
                {
                    vmState.SetNothing(destinationRegister);
                    break;
                }

                var result = new GesValue[(int)length];
                for (var i = 0; i < result.Length; i++)
                {
                    if (GameEventScriptRangeMath.GetTerm(range.From, range.To, range.Step, i + 1L) is { } value) result[i].SetFloat(value);
                    else result[i].SetNothing();
                }

                ShuffleList(result, randomGenerator);
                vmState.SetList(destinationRegister, result);
                break;
            }
            case Iterator when source.ObjectValue is IGesIterator iterator:
                ShuffleIterator(vmState, destinationRegister, iterator, randomGenerator);
                break;
            default:
                vmState.SetNothing(destinationRegister);
                break;
        }
    }
    private static void ReverseIterator(GesVmState vmState, ushort destinationRegister, IGesIterator iterator)
    {
        var values = new GesValue[16];
        var count = 0;
        try
        {
            GesIteratorResult item;
            while ((item = iterator.Next()).HasValue)
            {
                if (count == values.Length)
                {
                    var resized = new GesValue[values.Length << 1];
                    Array.Copy(values, resized, values.Length);
                    values = resized;
                }

                values[count++] = item.Value;
            }
        }
        finally
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
        }

        var result = new GesValue[count];
        for (var i = 0; i < count; i++) result[i] = values[count - i - 1];
        vmState.SetList(destinationRegister, result);
    }
    private static void ShuffleIterator(GesVmState vmState, ushort destinationRegister, IGesIterator iterator, GameEventScriptRandomGenerator randomGenerator)
    {
        var values = new GesValue[16];
        var count = 0;
        try
        {
            GesIteratorResult item;
            while ((item = iterator.Next()).HasValue)
            {
                if (count == values.Length)
                {
                    var resized = new GesValue[values.Length << 1];
                    Array.Copy(values, resized, values.Length);
                    values = resized;
                }

                values[count++] = item.Value;
            }
        }
        finally
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
        }

        var result = new GesValue[count];
        for (var i = 0; i < count; i++) result[i] = values[i];
        ShuffleList(result, randomGenerator);
        vmState.SetList(destinationRegister, result);
    }
    private static void ShuffleList(GesValue[] list, GameEventScriptRandomGenerator randomGenerator)
    {
        for (var i = list.Length - 1; i > 0; i--)
        {
            var swapIndex = randomGenerator.NextInclusiveInteger(0, i);
            (list[i], list[swapIndex]) = (list[swapIndex], list[i]);
        }
    }
}
