// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteTakeRandom(ushort destinationRegister, in GesValue source, short count, GesRandomGenerator randomGenerator)
    {
        GesValue result;
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesValue[] list:
                result = TakeRandomList(this, list, count, randomGenerator);
                break;
            case Dice when source.ObjectValue is int[] dice:
                result = TakeRandomDice(dice, count, randomGenerator);
                break;
            case GesBytecodeTypeKind.Range when source.ObjectValue is GesIntegerRange range:
                result = TakeRandomRange(this, range, count, randomGenerator);
                break;
            case GesBytecodeTypeKind.Range when source.ObjectValue is GesFloatRange range:
                result = TakeRandomRange(this, range, count, randomGenerator);
                break;
            case Iterator when source.ObjectValue is IGesIterator iterator:
                result = TakeRandomIterator(this, iterator, count, randomGenerator);
                break;
            default:
                result = new GesValue();
                break;
        }

        SetValue(destinationRegister, in result);
    }

    private static GesValue TakeRandomList(GesVirtualMachine vm, GesValue[] source, short count, GesRandomGenerator randomGenerator)
    {
        var dst = new GesValue();
        if (count <= 0 || source.Length == 0)
        {
            dst.SetList(vm.EmptyList);
            return dst;
        }

        var length = count < source.Length ? count : source.Length;
        var indices = CreateShuffledPrefix(source.Length, length, randomGenerator);
        var list = new GesValue[length];
        for (var i = 0; i < length; i++) list[i] = source[indices[i]];
        dst.SetList(list);
        return dst;
    }

    private static GesValue TakeRandomDice(int[] source, short count, GesRandomGenerator randomGenerator)
    {
        var dst = new GesValue();
        if (count <= 0 || source.Length == 0)
        {
            dst.SetDice([]);
            return dst;
        }

        var length = count < source.Length ? count : source.Length;
        var indices = CreateShuffledPrefix(source.Length, length, randomGenerator);
        var dice = new int[length];
        for (var i = 0; i < length; i++) dice[i] = source[indices[i]];
        dst.SetDice(dice);
        return dst;
    }

    private static GesValue TakeRandomRange(GesVirtualMachine vm, GesIntegerRange valueRangeInteger, short count, GesRandomGenerator randomGenerator)
    {
        var dst = new GesValue();
        var sourceLength = GesRangeMath.GetLength(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step);
        if (count <= 0 || sourceLength <= 0)
        {
            dst.SetList(vm.EmptyList);
            return dst;
        }

        var length = count < sourceLength ? count : checked((int)sourceLength);
        var list = new GesValue[length];
        if (sourceLength <= int.MaxValue)
        {
            var indices = CreateShuffledPrefix(checked((int)sourceLength), length, randomGenerator);
            for (var i = 0; i < length; i++)
            {
                if (GesRangeMath.GetTerm(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step, indices[i] + 1L) is { } value) list[i].SetInteger(value);
                else list[i].SetNothing();
            }

            dst.SetList(list);
            return dst;
        }

        var terms = new long[length];
        for (var i = 0; i < length; i++)
        {
            long index;
            var duplicate = false;
            do
            {
                duplicate = false;
                index = randomGenerator.NextInclusiveInteger(0, sourceLength - 1L);
                for (var j = 0; j < i; j++)
                {
                    if (terms[j] == index)
                    {
                        duplicate = true;
                        break;
                    }
                }
            } while (duplicate);

            terms[i] = index;
            if (GesRangeMath.GetTerm(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step, index + 1L) is { } value) list[i].SetInteger(value);
            else list[i].SetNothing();
        }

        dst.SetList(list);
        return dst;
    }

    private static GesValue TakeRandomRange(GesVirtualMachine vm, GesFloatRange range, short count, GesRandomGenerator randomGenerator)
    {
        var dst = new GesValue();
        var sourceLength = GesRangeMath.GetLength(range.From, range.To, range.Step);
        if (count <= 0 || sourceLength <= 0)
        {
            dst.SetList(vm.EmptyList);
            return dst;
        }

        var length = count < sourceLength ? count : checked((int)sourceLength);
        var list = new GesValue[length];
        if (sourceLength <= int.MaxValue)
        {
            var indices = CreateShuffledPrefix(checked((int)sourceLength), length, randomGenerator);
            for (var i = 0; i < length; i++)
            {
                if (GesRangeMath.GetTerm(range.From, range.To, range.Step, indices[i] + 1L) is { } value) list[i].SetFloat(value);
                else list[i].SetNothing();
            }

            dst.SetList(list);
            return dst;
        }

        var terms = new long[length];
        for (var i = 0; i < length; i++)
        {
            long index;
            var duplicate = false;
            do
            {
                duplicate = false;
                index = randomGenerator.NextInclusiveInteger(0, sourceLength - 1L);
                for (var j = 0; j < i; j++)
                {
                    if (terms[j] == index)
                    {
                        duplicate = true;
                        break;
                    }
                }
            } while (duplicate);

            terms[i] = index;
            if (GesRangeMath.GetTerm(range.From, range.To, range.Step, index + 1L) is { } value) list[i].SetFloat(value);
            else list[i].SetNothing();
        }

        dst.SetList(list);
        return dst;
    }

    private static GesValue TakeRandomIterator(GesVirtualMachine vm, IGesIterator iterator, short count, GesRandomGenerator randomGenerator)
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

            if (buffer.Count == 0)
            {
                dst.SetList(vm.EmptyList);
                return dst;
            }

            var length = count < buffer.Count ? count : buffer.Count;
            var indices = CreateShuffledPrefix(buffer.Count, length, randomGenerator);
            var list = new GesValue[length];
            for (var i = 0; i < length; i++) list[i] = buffer[indices[i]];
            dst.SetList(list);
            return dst;
        }
        finally
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
        }
    }

    private static int[] CreateShuffledPrefix(int sourceLength, int prefixLength, GesRandomGenerator randomGenerator)
    {
        var indices = new int[sourceLength];
        for (var i = 0; i < sourceLength; i++) indices[i] = i;
        var selectedIndices = new int[prefixLength];
        var remainingLength = sourceLength;
        for (var i = 0; i < prefixLength; i++)
        {
            var selected = randomGenerator.NextInclusiveInteger(0, remainingLength - 1);
            var value = indices[selected];
            if (selected < remainingLength - 1)
            {
                Array.Copy(indices, selected + 1, indices, selected, remainingLength - selected - 1);
            }

            selectedIndices[i] = value;
            remainingLength--;
        }

        return selectedIndices;
    }
}
