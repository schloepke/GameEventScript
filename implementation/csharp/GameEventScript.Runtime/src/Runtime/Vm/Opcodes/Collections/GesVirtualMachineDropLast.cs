// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteDropLast(ushort destinationRegister, in GesValue source, short count)
    {
        GesValue result;
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesValue[] list:
                result = DropLastList(list, count);
                break;
            case Dice when source.ObjectValue is int[] dice:
                result = DropLastDice(dice, count);
                break;
            case GesBytecodeTypeKind.Range when source.ObjectValue is GesIntegerRange range:
                result = DropLastRange(in source, range, count);
                break;
            case GesBytecodeTypeKind.Range when source.ObjectValue is GesFloatRange range:
                result = DropLastRange(in source, range, count);
                break;
            case Iterator when source.ObjectValue is IGesIterator iterator:
                result = DropLastIterator(this, iterator, count);
                break;
            default:
                result = new GesValue();
                break;
        }

        SetValue(destinationRegister, in result);
    }

    private static GesValue DropLastList(GesValue[] source, short count)
    {
        var dst = new GesValue();
        if (count <= 0)
        {
            dst.SetList(source);
            return dst;
        }

        var length = count < source.Length ? source.Length - count : 0;
        var list = new GesValue[length];
        for (var i = 0; i < length; i++) list[i] = source[i];
        dst.SetList(list);
        return dst;
    }

    private static GesValue DropLastIterator(GesVirtualMachine vm, IGesIterator iterator, short count)
    {
        var buffer = new GesVmListBuilder(16);
        try
        {
            GesIteratorResult item;
            while ((item = iterator.Next()).HasValue)
            {
                buffer.Add(in item.Value);
            }

            var length = count <= 0 ? buffer.Count : count < buffer.Count ? buffer.Count - count : 0;
            return ListFromBuffer(vm, buffer.Items, length);
        }
        finally
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
        }
    }
}
