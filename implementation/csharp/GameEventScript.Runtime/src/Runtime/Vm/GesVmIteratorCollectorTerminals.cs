// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using GameEventScript.Runtime;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal static class GesVmIteratorCollectorTerminals
{
    internal static void GesVmFirst(this GesVirtualMachine vm, ushort destinationRegister, in GesValue source)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesValue[] list:
                if (list.Length > 0) vm.SetValue(destinationRegister, in list[0]);
                else vm.SetNothing(destinationRegister);
                return;
            case Dice when source.ObjectValue is int[] dice:
                if (dice.Length > 0) vm.SetInteger(destinationRegister, dice[0]);
                else vm.SetNothing(destinationRegister);
                return;
            case GameEventScript.Api.GesBytecodeTypeKind.Range when source.ObjectValue is GesIntegerRange range:
                if (GesRangeMath.GetLength(range.From, range.To, range.Step) > 0) vm.SetInteger(destinationRegister, range.From);
                else vm.SetNothing(destinationRegister);
                return;
            case GameEventScript.Api.GesBytecodeTypeKind.Range when source.ObjectValue is GesFloatRange range:
                if (GesRangeMath.GetLength(range.From, range.To, range.Step) > 0) vm.SetFloat(destinationRegister, range.From);
                else vm.SetNothing(destinationRegister);
                return;
            case Map when source.ObjectValue is GesValueMap map:
                if (map.ValueList.Length > 0) vm.SetValue(destinationRegister, in map.ValueList[0]);
                else vm.SetNothing(destinationRegister);
                return;
            case Custom when source.ObjectValue is GesCustomObject customObject:
                var customMap = customObject.Map;
                if (customMap.ValueList.Length > 0) vm.SetValue(destinationRegister, in customMap.ValueList[0]);
                else vm.SetNothing(destinationRegister);
                return;
            case Vector or Point when source.ObjectValue is GesSpatialValue triplet:
                vm.SetFloat(destinationRegister, triplet.X);
                return;
            case Text or Tag:
                FirstFromText(vm, destinationRegister, in source);
                return;
            case Iterator when source.ObjectValue is IGesIterator iterator:
                FirstFromIterator(vm, destinationRegister, iterator);
                return;
            default:
                vm.SetNothing(destinationRegister);
                return;
        }
    }
    internal static void GesVmLast(this GesVirtualMachine vm, ushort destinationRegister, in GesValue source)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesValue[] list:
                if (list.Length > 0) vm.SetValue(destinationRegister, in list[list.Length - 1]);
                else vm.SetNothing(destinationRegister);
                return;
            case Dice when source.ObjectValue is int[] dice:
                if (dice.Length > 0) vm.SetInteger(destinationRegister, dice[^1]);
                else vm.SetNothing(destinationRegister);
                return;
            case GameEventScript.Api.GesBytecodeTypeKind.Range when source.ObjectValue is GesIntegerRange range:
                if (GesRangeMath.GetTerm(range.From, range.To, range.Step, GesRangeMath.GetLength(range.From, range.To, range.Step)) is { } lastInteger) vm.SetInteger(destinationRegister, lastInteger);
                else vm.SetNothing(destinationRegister);
                return;
            case GameEventScript.Api.GesBytecodeTypeKind.Range when source.ObjectValue is GesFloatRange range:
                if (GesRangeMath.GetTerm(range.From, range.To, range.Step, GesRangeMath.GetLength(range.From, range.To, range.Step)) is { } lastFloat) vm.SetFloat(destinationRegister, lastFloat);
                else vm.SetNothing(destinationRegister);
                return;
            case Map when source.ObjectValue is GesValueMap map:
                if (map.ValueList.Length > 0) vm.SetValue(destinationRegister, in map.ValueList[map.ValueList.Length - 1]);
                else vm.SetNothing(destinationRegister);
                return;
            case Custom when source.ObjectValue is GesCustomObject customObject:
                var customMap = customObject.Map;
                if (customMap.ValueList.Length > 0) vm.SetValue(destinationRegister, in customMap.ValueList[customMap.ValueList.Length - 1]);
                else vm.SetNothing(destinationRegister);
                return;
            case Vector or Point when source.ObjectValue is GesSpatialValue triplet:
                vm.SetFloat(destinationRegister, triplet.Z);
                return;
            case Text or Tag:
                LastFromText(vm, destinationRegister, in source);
                return;
            case Iterator when source.ObjectValue is IGesIterator iterator:
                LastFromIterator(vm, destinationRegister, iterator);
                return;
            default:
                vm.SetNothing(destinationRegister);
                return;
        }
    }
    internal static void GesVmSingle(this GesVirtualMachine vm, ushort destinationRegister, in GesValue source)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesValue[] list:
                if (list.Length == 1) vm.SetValue(destinationRegister, in list[0]);
                else vm.SetNothing(destinationRegister);
                return;
            case Dice when source.ObjectValue is int[] dice:
                if (dice.Length == 1) vm.SetInteger(destinationRegister, dice[0]);
                else vm.SetNothing(destinationRegister);
                return;
            case GameEventScript.Api.GesBytecodeTypeKind.Range when source.ObjectValue is GesIntegerRange range:
                if (GesRangeMath.GetLength(range.From, range.To, range.Step) == 1) vm.SetInteger(destinationRegister, range.From);
                else vm.SetNothing(destinationRegister);
                return;
            case GameEventScript.Api.GesBytecodeTypeKind.Range when source.ObjectValue is GesFloatRange range:
                if (GesRangeMath.GetLength(range.From, range.To, range.Step) == 1) vm.SetFloat(destinationRegister, range.From);
                else vm.SetNothing(destinationRegister);
                return;
            case Map when source.ObjectValue is GesValueMap map:
                if (map.ValueList.Length == 1) vm.SetValue(destinationRegister, in map.ValueList[0]);
                else vm.SetNothing(destinationRegister);
                return;
            case Custom when source.ObjectValue is GesCustomObject customObject:
                var customMap = customObject.Map;
                if (customMap.ValueList.Length == 1) vm.SetValue(destinationRegister, in customMap.ValueList[0]);
                else vm.SetNothing(destinationRegister);
                return;
            case Text or Tag:
                SingleFromText(vm, destinationRegister, in source);
                return;
            case Iterator when source.ObjectValue is IGesIterator iterator:
                SingleFromIterator(vm, destinationRegister, iterator);
                return;
            default:
                vm.SetNothing(destinationRegister);
                return;
        }
    }
    private static void FirstFromIterator(GesVirtualMachine vm, ushort destinationRegister, IGesIterator iterator)
    {
        try
        {
            var item = iterator.Next();
            if (item.HasValue) vm.SetValue(destinationRegister, in item.Value);
            else vm.SetNothing(destinationRegister);
        }
        finally
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
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
    private static void FirstFromText(GesVirtualMachine vm, ushort destinationRegister, in GesValue source)
    {
        if (GesText.ScalarAt(source.TextValue, 0) is { } scalar) vm.SetText(destinationRegister, scalar);
        else vm.SetNothing(destinationRegister);
    }
    private static void LastFromText(GesVirtualMachine vm, ushort destinationRegister, in GesValue source)
    {
        if (source.CollectionCount > 0 && GesText.ScalarAt(source.TextValue, source.CollectionCount - 1) is { } scalar) vm.SetText(destinationRegister, scalar);
        else vm.SetNothing(destinationRegister);
    }
    private static void SingleFromText(GesVirtualMachine vm, ushort destinationRegister, in GesValue source)
    {
        if (source.CollectionCount == 1) vm.SetText(destinationRegister, source.TextValue);
        else vm.SetNothing(destinationRegister);
    }
}
