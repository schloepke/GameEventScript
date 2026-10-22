// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteSingle(ushort destinationRegister, in GesValue source)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesValue[] list:
                if (list.Length == 1) SetValue(destinationRegister, in list[0]);
                else SetNothing(destinationRegister);
                return;
            case Dice when source.ObjectValue is int[] dice:
                if (dice.Length == 1) SetInteger(destinationRegister, dice[0]);
                else SetNothing(destinationRegister);
                return;
            case GameEventScript.Api.GesBytecodeTypeKind.Range when source.ObjectValue is GesIntegerRange range:
                if (GesRangeMath.GetLength(range.From, range.To, range.Step) == 1) SetInteger(destinationRegister, range.From);
                else SetNothing(destinationRegister);
                return;
            case GameEventScript.Api.GesBytecodeTypeKind.Range when source.ObjectValue is GesFloatRange range:
                if (GesRangeMath.GetLength(range.From, range.To, range.Step) == 1) SetFloat(destinationRegister, range.From);
                else SetNothing(destinationRegister);
                return;
            case Map when source.ObjectValue is GesValueMap map:
                if (map.ValueList.Length == 1) SetValue(destinationRegister, in map.ValueList[0]);
                else SetNothing(destinationRegister);
                return;
            case Custom when source.ObjectValue is GesCustomObject customObject:
                var customMap = customObject.Map;
                if (customMap.ValueList.Length == 1) SetValue(destinationRegister, in customMap.ValueList[0]);
                else SetNothing(destinationRegister);
                return;
            case Text or Tag:
                SingleFromText(this, destinationRegister, in source);
                return;
            case Iterator when source.ObjectValue is IGesIterator iterator:
                SingleFromIterator(this, destinationRegister, iterator);
                return;
            default:
                SetNothing(destinationRegister);
                return;
        }
    }

    private static void SingleFromIterator(GesVirtualMachine vm, ushort destinationRegister, IGesIterator iterator)
    {
        try
        {
            var item = iterator.Next();
            if (!item.HasValue)
            {
                vm.SetNothing(destinationRegister);
                return;
            }

            var second = iterator.Next();
            if (second.HasValue) vm.SetNothing(destinationRegister);
            else vm.SetValue(destinationRegister, in item.Value);
        }
        finally
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
        }
    }

    private static void SingleFromText(GesVirtualMachine vm, ushort destinationRegister, in GesValue source)
    {
        if (source.CollectionCount == 1) vm.SetText(destinationRegister, source.TextValue);
        else vm.SetNothing(destinationRegister);
    }
}
