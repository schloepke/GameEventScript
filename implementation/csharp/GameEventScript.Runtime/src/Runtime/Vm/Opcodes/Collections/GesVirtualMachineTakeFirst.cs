// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteTakeFirst(ushort destinationRegister, in GesValue source, short count)
    {
        GesValue result;
        switch (source.Kind)
        {
            case Series when source.ObjectValue is GesSeriesValue series:
                result = TakeFirstSeries(this, series, count);
                break;
            case List when source.ObjectValue is GesValue[] list:
                result = TakeFirstList(this, list, count);
                break;
            case Dice when source.ObjectValue is int[] dice:
                result = TakeFirstDice(this, dice, count);
                break;
            case GesBytecodeTypeKind.Range when source.ObjectValue is GesIntegerRange range:
                result = TakeFirstRange(this, range, count);
                break;
            case GesBytecodeTypeKind.Range when source.ObjectValue is GesFloatRange range:
                result = TakeFirstRange(this, range, count);
                break;
            case Iterator when source.ObjectValue is IGesIterator iterator:
                result = TakeFirstIterator(this, iterator, count);
                break;
            default:
                result = new GesValue();
                break;
        }

        SetValue(destinationRegister, in result);
    }

    private static GesValue TakeFirstSeries(GesVirtualMachine vm, GesSeriesValue series, short count)
    {
        var dst = new GesValue();
        if (count <= 0)
        {
            dst.SetList(vm.EmptyList);
            return dst;
        }

        var list = new GesValue[count];
        for (var i = 0; i < count; i++) list[i] = series.GetTerm(i);
        dst.SetList(list);
        return dst;
    }

    private static GesValue TakeFirstList(GesVirtualMachine vm, GesValue[] source, short count)
    {
        var dst = new GesValue();
        if (count <= 0)
        {
            dst.SetList(vm.EmptyList);
            return dst;
        }

        var length = count < source.Length ? count : source.Length;
        var list = new GesValue[length];
        for (var i = 0; i < length; i++) list[i] = source[i];
        dst.SetList(list);
        return dst;
    }

    private static GesValue TakeFirstIterator(GesVirtualMachine vm, IGesIterator iterator, short count)
    {
        var dst = new GesValue();
        if (count <= 0)
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
            dst.SetList(vm.EmptyList);
            return dst;
        }

        var buffer = new GesVmListBuilder(count < 16 ? count : 16);
        try
        {
            GesIteratorResult item;
            while (buffer.Count < count && (item = iterator.Next()).HasValue)
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

    private static GesValue TakeFirstRange(GesVirtualMachine vm, GesIntegerRange valueRangeInteger, short count)
    {
        var dst = new GesValue();
        var length = GesRangeMath.GetLength(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step);
        if (count <= 0 || length <= 0)
        {
            dst.SetRange(0, 0, 0);
            return dst;
        }

        if (count >= length)
        {
            dst.SetRange(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step);
            return dst;
        }

        if (GesRangeMath.GetTerm(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step, count) is { } to) dst.SetRange(valueRangeInteger.From, to, valueRangeInteger.Step);
        else dst.SetNothing();
        return dst;
    }

    private static GesValue TakeFirstRange(GesVirtualMachine vm, GesFloatRange range, short count)
    {
        var dst = new GesValue();
        var length = GesRangeMath.GetLength(range.From, range.To, range.Step);
        if (count <= 0 || length <= 0)
        {
            dst.SetRange(0, 0, 0);
            return dst;
        }

        if (count >= length)
        {
            dst.SetRange(range.From, range.To, range.Step);
            return dst;
        }

        if (GesRangeMath.GetTerm(range.From, range.To, range.Step, count) is { } to) dst.SetRange(range.From, to, range.Step);
        else dst.SetNothing();
        return dst;
    }
}
