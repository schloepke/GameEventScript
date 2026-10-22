// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteShuffle(ushort destinationRegister, in GesValue source, GesRandomGenerator randomGenerator)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesValue[] list:
            {
                var result = new GesValue[list.Length];
                for (var i = 0; i < list.Length; i++) result[i] = list[i];
                ShuffleList(result, randomGenerator);
                SetList(destinationRegister, result);
                break;
            }
            case Dice when source.ObjectValue is int[] dice:
            {
                var result = new GesValue[dice.Length];
                for (var i = 0; i < dice.Length; i++) result[i].SetInteger(dice[i]);
                ShuffleList(result, randomGenerator);
                SetList(destinationRegister, result);
                break;
            }
            case GesBytecodeTypeKind.Range when source.ObjectValue is GesIntegerRange range:
            {
                var length = GesRangeMath.GetLength(range.From, range.To, range.Step);
                if (length <= 0)
                {
                    SetList(destinationRegister, EmptyList);
                    break;
                }

                if (length > int.MaxValue)
                {
                    SetNothing(destinationRegister);
                    break;
                }

                var result = new GesValue[(int)length];
                for (var i = 0; i < result.Length; i++)
                {
                    if (GesRangeMath.GetTerm(range.From, range.To, range.Step, i + 1L) is { } value) result[i].SetInteger(value);
                    else result[i].SetNothing();
                }

                ShuffleList(result, randomGenerator);
                SetList(destinationRegister, result);
                break;
            }
            case GesBytecodeTypeKind.Range when source.ObjectValue is GesFloatRange range:
            {
                var length = GesRangeMath.GetLength(range.From, range.To, range.Step);
                if (length <= 0)
                {
                    SetList(destinationRegister, EmptyList);
                    break;
                }

                if (length > int.MaxValue)
                {
                    SetNothing(destinationRegister);
                    break;
                }

                var result = new GesValue[(int)length];
                for (var i = 0; i < result.Length; i++)
                {
                    if (GesRangeMath.GetTerm(range.From, range.To, range.Step, i + 1L) is { } value) result[i].SetFloat(value);
                    else result[i].SetNothing();
                }

                ShuffleList(result, randomGenerator);
                SetList(destinationRegister, result);
                break;
            }
            case Iterator when source.ObjectValue is IGesIterator iterator:
                ShuffleIterator(this, destinationRegister, iterator, randomGenerator);
                break;
            default:
                SetNothing(destinationRegister);
                break;
        }
    }

    private static void ShuffleIterator(GesVirtualMachine vm, ushort destinationRegister, IGesIterator iterator, GesRandomGenerator randomGenerator)
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
        vm.SetList(destinationRegister, result);
    }

    private static void ShuffleList(GesValue[] list, GesRandomGenerator randomGenerator)
    {
        for (var i = list.Length - 1; i > 0; i--)
        {
            var swapIndex = randomGenerator.NextInclusiveInteger(0, i);
            (list[i], list[swapIndex]) = (list[swapIndex], list[i]);
        }
    }
}
