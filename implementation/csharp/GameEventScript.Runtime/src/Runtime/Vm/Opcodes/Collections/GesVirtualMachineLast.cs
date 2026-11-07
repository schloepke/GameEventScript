// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using GameEventScript.Syntax;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteLast(ushort destinationRegister, in GesValue source)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesValue[] list:
                if (list.Length > 0) SetValue(destinationRegister, in list[list.Length - 1]);
                else SetNothing(destinationRegister);
                return;
            case Dice when source.ObjectValue is int[] dice:
                if (dice.Length > 0) SetInteger(destinationRegister, dice[^1]);
                else SetNothing(destinationRegister);
                return;
            case GameEventScript.Api.GesBytecodeTypeKind.Range when source.ObjectValue is GesIntegerRange range:
                if (GesRangeMath.GetTerm(range.From, range.To, range.Step, GesRangeMath.GetLength(range.From, range.To, range.Step)) is { } lastInteger) SetInteger(destinationRegister, lastInteger);
                else SetNothing(destinationRegister);
                return;
            case GameEventScript.Api.GesBytecodeTypeKind.Range when source.ObjectValue is GesFloatRange range:
                if (GesRangeMath.GetTerm(range.From, range.To, range.Step, GesRangeMath.GetLength(range.From, range.To, range.Step)) is { } lastFloat) SetFloat(destinationRegister, lastFloat);
                else SetNothing(destinationRegister);
                return;
            case Map when source.ObjectValue is GesValueMap map:
                if (map.ValueList.Length > 0) SetValue(destinationRegister, in map.ValueList[map.ValueList.Length - 1]);
                else SetNothing(destinationRegister);
                return;
            case Custom when source.ObjectValue is GesCustomObject customObject:
                var customMap = customObject.Map;
                if (customMap.ValueList.Length > 0) SetValue(destinationRegister, in customMap.ValueList[customMap.ValueList.Length - 1]);
                else SetNothing(destinationRegister);
                return;
            case Vector or Point when source.ObjectValue is GesSpatialValue triplet:
                SetFloat(destinationRegister, triplet.Z);
                return;
            case Text or Tag:
                LastFromText(this, destinationRegister, in source);
                return;
            case Iterator when source.ObjectValue is IGesIterator iterator:
                LastFromIterator(this, destinationRegister, iterator);
                return;
            default:
                SetNothing(destinationRegister);
                return;
        }
    }

    private static void LastFromIterator(GesVirtualMachine vm, ushort destinationRegister, IGesIterator iterator)
    {
        var last = new GesValue();
        var found = false;
        try
        {
            GesIteratorResult item;
            while ((item = iterator.Next()).HasValue)
            {
                last = item.Value;
                found = true;
            }

            if (found) vm.SetValue(destinationRegister, in last);
            else vm.SetNothing(destinationRegister);
        }
        finally
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
        }
    }

    private static void LastFromText(GesVirtualMachine vm, ushort destinationRegister, in GesValue source)
    {
        if (source.CollectionCount > 0 && GesText.ScalarAt(source.TextValue, source.CollectionCount - 1) is { } scalar) vm.SetText(destinationRegister, scalar);
        else vm.SetNothing(destinationRegister);
    }
}
