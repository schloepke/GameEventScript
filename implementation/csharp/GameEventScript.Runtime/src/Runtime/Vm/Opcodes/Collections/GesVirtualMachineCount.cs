// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteCount(ushort destinationRegister, in GesValue source)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesValue[] list:
                SetInteger(destinationRegister, list.Length);
                return;
            case Map when source.ObjectValue is GesValueMap map:
                SetInteger(destinationRegister, map.Length);
                return;
            case Dice when source.ObjectValue is int[] dice:
                SetInteger(destinationRegister, dice.Length);
                return;
            case GesBytecodeTypeKind.Range when source.ObjectValue is GesIntegerRange range:
                SetInteger(destinationRegister, GesRangeMath.GetLength(range.From, range.To, range.Step));
                return;
            case GesBytecodeTypeKind.Range when source.ObjectValue is GesFloatRange range:
                SetInteger(destinationRegister, GesRangeMath.GetLength(range.From, range.To, range.Step));
                return;
            case Vector or Point when source.ObjectValue is GesSpatialValue:
                SetInteger(destinationRegister, 3);
                return;
            case Text or Tag:
                SetInteger(destinationRegister, source.CollectionCount);
                return;
            case Nothing:
                SetInteger(destinationRegister, 0);
                return;
        }

        IGesIterator iterator;
        if (source is { Kind: Iterator, ObjectValue: IGesIterator sourceIterator }) iterator = sourceIterator;
        else if (source.CreateIterator() is not { } createdIterator)
        {
            SetNothing(destinationRegister);
            return;
        }
        else
        {
            iterator = createdIterator;
        }

        long count = 0;
        try
        {
            while (iterator.Next().HasValue) count++;
            SetInteger(destinationRegister, count);
        }
        finally
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
        }
    }
}
