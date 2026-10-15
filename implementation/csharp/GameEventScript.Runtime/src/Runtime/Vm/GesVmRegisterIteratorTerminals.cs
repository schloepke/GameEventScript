// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using GameEventScript.Runtime;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal static class GesVmRegisterIteratorTerminals
{
    internal static void GesVmCount(this GesVirtualMachine vm, ushort destinationRegister, in GesValue source)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesValue[] list:
                vm.SetInteger(destinationRegister, list.Length);
                return;
            case Map when source.ObjectValue is GesValueMap map:
                vm.SetInteger(destinationRegister, map.Length);
                return;
            case Dice when source.ObjectValue is int[] dice:
                vm.SetInteger(destinationRegister, dice.Length);
                return;
            case GesBytecodeTypeKind.Range when source.ObjectValue is GesIntegerRange range:
                vm.SetInteger(destinationRegister, GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step));
                return;
            case GesBytecodeTypeKind.Range when source.ObjectValue is GesFloatRange range:
                vm.SetInteger(destinationRegister, GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step));
                return;
            case Vector or Point when source.ObjectValue is GesSpatialValue:
                vm.SetInteger(destinationRegister, 3);
                return;
            case Text or Tag:
                vm.SetInteger(destinationRegister, source.CollectionCount);
                return;
            case Nothing:
                vm.SetInteger(destinationRegister, 0);
                return;
        }

        IGesIterator iterator;
        if (source is { Kind: Iterator, ObjectValue: IGesIterator sourceIterator }) iterator = sourceIterator;
        else if (source.CreateIterator() is not { } createdIterator)
        {
            vm.SetNothing(destinationRegister);
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
            vm.SetInteger(destinationRegister, count);
        }
        finally
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
        }
    }

}
