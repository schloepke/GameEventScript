// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteReverse(ushort destinationRegister, in GesValue source)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesValue[] list:
            {
                var result = new GesValue[list.Length];
                for (var i = 0; i < list.Length; i++) result[i] = list[list.Length - i - 1];
                SetList(destinationRegister, result);
                break;
            }
            case Dice when source.ObjectValue is int[] dice:
            {
                var result = new GesValue[dice.Length];
                for (var i = 0; i < dice.Length; i++) result[i].SetInteger(dice[dice.Length - i - 1]);
                SetList(destinationRegister, result);
                break;
            }
            case GesBytecodeTypeKind.Range when source.ObjectValue is GesIntegerRange range:
            {
                var length = GesRangeMath.GetLength(range.From, range.To, range.Step);
                if (length <= 0)
                {
                    SetIntegerRange(destinationRegister, 0, 0, 0);
                    break;
                }

                if (GesRangeMath.GetTerm(range.From, range.To, range.Step, length) is { } last) SetIntegerRange(destinationRegister, last, range.From, -range.Step);
                else SetNothing(destinationRegister);
                break;
            }
            case GesBytecodeTypeKind.Range when source.ObjectValue is GesFloatRange range:
            {
                var length = GesRangeMath.GetLength(range.From, range.To, range.Step);
                if (length <= 0)
                {
                    SetFloatRange(destinationRegister, 0d, 0d, 0d);
                    break;
                }

                if (GesRangeMath.GetTerm(range.From, range.To, range.Step, length) is { } last) SetFloatRange(destinationRegister, last, range.From, -range.Step);
                else SetNothing(destinationRegister);
                break;
            }
            case Iterator when source.ObjectValue is IGesIterator iterator:
                ReverseIterator(this, destinationRegister, iterator);
                break;
            default:
                SetNothing(destinationRegister);
                break;
        }
    }

    private static void ReverseIterator(GesVirtualMachine vm, ushort destinationRegister, IGesIterator iterator)
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
        vm.SetList(destinationRegister, result);
    }
}
