// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteDropFirst(ushort destinationRegister, in GesValue source, short count)
    {
        GesValue result;
        switch (source.Kind)
        {
            case Series when source.ObjectValue is GesSeriesValue series:
                result = new GesValue();
                result.SetSeries(count <= 0 ? series : series.Drop(count));
                break;
            case List when source.ObjectValue is GesValue[] list:
                result = DropFirstList(list, count);
                break;
            case Dice when source.ObjectValue is int[] dice:
                result = DropFirstDice(dice, count);
                break;
            case GesBytecodeTypeKind.Range when source.ObjectValue is GesIntegerRange range:
                result = DropFirstRange(in source, range, count);
                break;
            case GesBytecodeTypeKind.Range when source.ObjectValue is GesFloatRange range:
                result = DropFirstRange(in source, range, count);
                break;
            case Iterator when source.ObjectValue is IGesIterator iterator:
                result = DropFirstIterator(this, iterator, count);
                break;
            default:
                result = new GesValue();
                break;
        }

        SetValue(destinationRegister, in result);
    }

    private static GesValue DropFirstList(GesValue[] source, short count)
    {
        var dst = new GesValue();
        if (count <= 0)
        {
            dst.SetList(source);
            return dst;
        }

        var start = count < source.Length ? count : source.Length;
        var length = source.Length - start;
        var list = new GesValue[length];
        for (var i = 0; i < length; i++) list[i] = source[start + i];
        dst.SetList(list);
        return dst;
    }

    private static GesValue DropFirstIterator(GesVirtualMachine vm, IGesIterator iterator, short count)
    {
        var skipped = 0;
        var buffer = new GesVmListBuilder(16);
        try
        {
            GesIteratorResult item;
            while (count > 0 && skipped < count && (item = iterator.Next()).HasValue) skipped++;
            while ((item = iterator.Next()).HasValue)
            {
                buffer.Add(in item.Value);
            }

            return ListFromBuffer(vm, buffer.Items, buffer.Count);
        }
        finally
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
        }
    }
}
