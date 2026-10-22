// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteDistinct(ushort destinationRegister, in GesValue source)
    {
        var resultValue = new GesValue();
        switch (source.Kind)
        {
            case Nothing:
                SetNothing(destinationRegister);
                return;
            case List when source.ObjectValue is GesValue[] list:
            {
                if (list.Length == 0)
                {
                    SetList(destinationRegister, EmptyList);
                    return;
                }

                var result = new GesValue[list.Length];
                var resultLength = 0;
                for (var i = 0; i < list.Length; i++)
                {
                    var item = list[i];
                    var found = false;
                    for (var j = 0; j < resultLength; j++)
                    {
                        if (!result[j].EqualsValue(in item)) continue;
                        found = true;
                        break;
                    }

                    if (found) continue;
                    result[resultLength++] = item;
                }

                if (resultLength == list.Length)
                {
                    SetList(destinationRegister, result);
                    return;
                }

                var compact = new GesValue[resultLength];
                for (var i = 0; i < resultLength; i++) compact[i] = result[i];
                SetList(destinationRegister, compact);
                return;
            }
            case Dice when source.ObjectValue is int[] dice:
            {
                if (dice.Length == 0)
                {
                    SetDice(destinationRegister, []);
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
                    SetDice(destinationRegister, values);
                    return;
                }

                var compact = new int[count];
                Array.Copy(values, compact, count);
                SetDice(destinationRegister, compact);
                return;
            }
            case Iterator when source.ObjectValue is IGesIterator iterator:
            {
                var values = new GesValue[16];
                var count = 0;
                try
                {
                    GesIteratorResult item;
                    while ((item = iterator.Next()).HasValue)
                    {
                        var found = false;
                        for (var i = 0; i < count; i++)
                        {
                            if (!values[i].EqualsValue(in item.Value)) continue;
                            found = true;
                            break;
                        }

                        if (found) continue;
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
                SetList(destinationRegister, result);
                return;
            }
            default:
                resultValue.SetNothing();
                SetValue(destinationRegister, in resultValue);
                return;
        }
    }
}
