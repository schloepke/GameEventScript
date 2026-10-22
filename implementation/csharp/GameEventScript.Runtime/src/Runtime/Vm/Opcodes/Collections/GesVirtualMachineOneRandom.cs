// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteOneRandom(ushort destinationRegister, in GesValue source, GesRandomGenerator randomGenerator)
    {
        GesValue result;
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesValue[] list:
                result = list.Length > 0 ? list[randomGenerator.NextInclusiveInteger(0, list.Length - 1)] : new GesValue();
                break;
            case Dice when source.ObjectValue is int[] dice:
                result = new GesValue();
                if (dice.Length > 0) result.SetInteger(dice[randomGenerator.NextInclusiveInteger(0, dice.Length - 1)]);
                break;
            case GesBytecodeTypeKind.Range when source.ObjectValue is GesIntegerRange range:
                result = OneRandomRange(range, randomGenerator);
                break;
            case GesBytecodeTypeKind.Range when source.ObjectValue is GesFloatRange range:
                result = OneRandomRange(range, randomGenerator);
                break;
            case Iterator when source.ObjectValue is IGesIterator iterator:
                result = OneRandomIterator(iterator, randomGenerator);
                break;
            default:
                result = new GesValue();
                break;
        }

        SetValue(destinationRegister, in result);
    }

    private static GesValue OneRandomRange(GesIntegerRange valueRangeInteger, GesRandomGenerator randomGenerator)
    {
        var dst = new GesValue();
        var length = GesRangeMath.GetLength(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step);
        if (length <= 0)
        {
            dst.SetNothing();
            return dst;
        }

        var index = randomGenerator.NextInclusiveInteger(0, length - 1L);
        if (GesRangeMath.GetTerm(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step, index + 1L) is { } value) dst.SetInteger(value);
        else dst.SetNothing();
        return dst;
    }

    private static GesValue OneRandomRange(GesFloatRange range, GesRandomGenerator randomGenerator)
    {
        var dst = new GesValue();
        var length = GesRangeMath.GetLength(range.From, range.To, range.Step);
        if (length <= 0)
        {
            dst.SetNothing();
            return dst;
        }

        var index = randomGenerator.NextInclusiveInteger(0, length - 1L);
        if (GesRangeMath.GetTerm(range.From, range.To, range.Step, index + 1L) is { } value) dst.SetFloat(value);
        else dst.SetNothing();
        return dst;
    }

    private static GesValue OneRandomIterator(IGesIterator iterator, GesRandomGenerator randomGenerator)
    {
        var chosen = new GesValue();
        var count = 0;
        try
        {
            GesIteratorResult item;
            while ((item = iterator.Next()).HasValue)
            {
                count++;
                if (randomGenerator.NextInclusiveInteger(1, count) == 1) chosen = item.Value;
            }

            return count > 0 ? chosen : new GesValue();
        }
        finally
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
        }
    }
}
