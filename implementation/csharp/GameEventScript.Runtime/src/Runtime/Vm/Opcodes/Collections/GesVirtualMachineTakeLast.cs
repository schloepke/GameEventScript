// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteTakeLast(ushort destinationRegister, in GesValue source, short count)
    {
        GesValue result;
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesValue[] list:
                result = TakeLastList(this, list, count);
                break;
            case Dice when source.ObjectValue is int[] dice:
                result = TakeLastDice(dice, count);
                break;
            case GesBytecodeTypeKind.Range when source.ObjectValue is GesIntegerRange range:
                result = TakeLastRange(this, range, count);
                break;
            case GesBytecodeTypeKind.Range when source.ObjectValue is GesFloatRange range:
                result = TakeLastRange(this, range, count);
                break;
            case Iterator when source.ObjectValue is IGesIterator iterator:
                result = TakeLastIterator(this, iterator, count);
                break;
            default:
                result = new GesValue();
                break;
        }

        SetValue(destinationRegister, in result);
    }

    private static GesValue TakeLastList(GesVirtualMachine vm, GesValue[] source, short count)
    {
        var dst = new GesValue();
        if (count <= 0)
        {
            dst.SetList(vm.EmptyList);
            return dst;
        }

        var length = count < source.Length ? count : source.Length;
        var start = source.Length - length;
        var list = new GesValue[length];
        for (var i = 0; i < length; i++) list[i] = source[start + i];
        dst.SetList(list);
        return dst;
    }

    private static GesValue TakeLastIterator(GesVirtualMachine vm, IGesIterator iterator, short count)
    {
        var dst = new GesValue();
        if (count <= 0)
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
            dst.SetList(vm.EmptyList);
            return dst;
        }

        var buffer = new GesVmListBuilder(16);
        try
        {
            GesIteratorResult item;
            while ((item = iterator.Next()).HasValue)
            {
                buffer.Add(in item.Value);
            }

            var length = count < buffer.Count ? count : buffer.Count;
            var start = buffer.Count - length;
            if (length == 0)
            {
                dst.SetList(vm.EmptyList);
                return dst;
            }

            var list = buffer.ToList(start, length);
            dst.SetList(list);
            return dst;
        }
        finally
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
        }
    }

    private static GesValue TakeLastRange(GesVirtualMachine vm, GesIntegerRange valueRangeInteger, short count)
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

        if (GesRangeMath.GetTerm(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step, length - count + 1L) is { } from) dst.SetRange(from, valueRangeInteger.To, valueRangeInteger.Step);
        else dst.SetNothing();
        return dst;
    }

    private static GesValue TakeLastRange(GesVirtualMachine vm, GesFloatRange range, short count)
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

        if (GesRangeMath.GetTerm(range.From, range.To, range.Step, length - count + 1L) is { } from) dst.SetRange(from, range.To, range.Step);
        else dst.SetNothing();
        return dst;
    }
}
