using System;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Runtime.Values;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.Runtime.VM;

internal static class GesVmIteratorCollectorTerminals
{
    internal static void GesVmFirst(this GesVmState vmState, ushort destinationRegister, in GesValue source)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesValue[] list:
                if (list.Length > 0) vmState.SetValue(destinationRegister, in list[0]);
                else vmState.SetNothing(destinationRegister);
                return;
            case Dice when source.ObjectValue is int[] dice:
                if (dice.Length > 0) vmState.SetInteger(destinationRegister, dice[0]);
                else vmState.SetNothing(destinationRegister);
                return;
            case StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesValueRangeInteger range:
                if (GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step) > 0) vmState.SetInteger(destinationRegister, range.From);
                else vmState.SetNothing(destinationRegister);
                return;
            case StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesValueRangeFloat range:
                if (GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step) > 0) vmState.SetFloat(destinationRegister, range.From);
                else vmState.SetNothing(destinationRegister);
                return;
            case Map when source.ObjectValue is GesValueMap map:
                if (map.ValueList.Length > 0) vmState.SetValue(destinationRegister, in map.ValueList[0]);
                else vmState.SetNothing(destinationRegister);
                return;
            case Custom when source.ObjectValue is GesCustomObject customObject:
                var customMap = customObject.Map;
                if (customMap.ValueList.Length > 0) vmState.SetValue(destinationRegister, in customMap.ValueList[0]);
                else vmState.SetNothing(destinationRegister);
                return;
            case Vector or Point when source.ObjectValue is GesValueVectorPoint triplet:
                vmState.SetFloat(destinationRegister, triplet.X);
                return;
            case Text or Tag:
                FirstFromText(vmState, destinationRegister, in source);
                return;
            case Iterator when source.ObjectValue is IGesIterator iterator:
                FirstFromIterator(vmState, destinationRegister, iterator);
                return;
            default:
                vmState.SetNothing(destinationRegister);
                return;
        }
    }
    internal static void GesVmLast(this GesVmState vmState, ushort destinationRegister, in GesValue source)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesValue[] list:
                if (list.Length > 0) vmState.SetValue(destinationRegister, in list[list.Length - 1]);
                else vmState.SetNothing(destinationRegister);
                return;
            case Dice when source.ObjectValue is int[] dice:
                if (dice.Length > 0) vmState.SetInteger(destinationRegister, dice[^1]);
                else vmState.SetNothing(destinationRegister);
                return;
            case StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesValueRangeInteger range:
                if (GameEventScriptRangeMath.GetTerm(range.From, range.To, range.Step, GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step)) is { } lastInteger) vmState.SetInteger(destinationRegister, lastInteger);
                else vmState.SetNothing(destinationRegister);
                return;
            case StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesValueRangeFloat range:
                if (GameEventScriptRangeMath.GetTerm(range.From, range.To, range.Step, GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step)) is { } lastFloat) vmState.SetFloat(destinationRegister, lastFloat);
                else vmState.SetNothing(destinationRegister);
                return;
            case Map when source.ObjectValue is GesValueMap map:
                if (map.ValueList.Length > 0) vmState.SetValue(destinationRegister, in map.ValueList[map.ValueList.Length - 1]);
                else vmState.SetNothing(destinationRegister);
                return;
            case Custom when source.ObjectValue is GesCustomObject customObject:
                var customMap = customObject.Map;
                if (customMap.ValueList.Length > 0) vmState.SetValue(destinationRegister, in customMap.ValueList[customMap.ValueList.Length - 1]);
                else vmState.SetNothing(destinationRegister);
                return;
            case Vector or Point when source.ObjectValue is GesValueVectorPoint triplet:
                vmState.SetFloat(destinationRegister, triplet.Z);
                return;
            case Text or Tag:
                LastFromText(vmState, destinationRegister, in source);
                return;
            case Iterator when source.ObjectValue is IGesIterator iterator:
                LastFromIterator(vmState, destinationRegister, iterator);
                return;
            default:
                vmState.SetNothing(destinationRegister);
                return;
        }
    }
    internal static void GesVmSingle(this GesVmState vmState, ushort destinationRegister, in GesValue source)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesValue[] list:
                if (list.Length == 1) vmState.SetValue(destinationRegister, in list[0]);
                else vmState.SetNothing(destinationRegister);
                return;
            case Dice when source.ObjectValue is int[] dice:
                if (dice.Length == 1) vmState.SetInteger(destinationRegister, dice[0]);
                else vmState.SetNothing(destinationRegister);
                return;
            case StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesValueRangeInteger range:
                if (GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step) == 1) vmState.SetInteger(destinationRegister, range.From);
                else vmState.SetNothing(destinationRegister);
                return;
            case StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesValueRangeFloat range:
                if (GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step) == 1) vmState.SetFloat(destinationRegister, range.From);
                else vmState.SetNothing(destinationRegister);
                return;
            case Map when source.ObjectValue is GesValueMap map:
                if (map.ValueList.Length == 1) vmState.SetValue(destinationRegister, in map.ValueList[0]);
                else vmState.SetNothing(destinationRegister);
                return;
            case Custom when source.ObjectValue is GesCustomObject customObject:
                var customMap = customObject.Map;
                if (customMap.ValueList.Length == 1) vmState.SetValue(destinationRegister, in customMap.ValueList[0]);
                else vmState.SetNothing(destinationRegister);
                return;
            case Text or Tag:
                SingleFromText(vmState, destinationRegister, in source);
                return;
            case Iterator when source.ObjectValue is IGesIterator iterator:
                SingleFromIterator(vmState, destinationRegister, iterator);
                return;
            default:
                vmState.SetNothing(destinationRegister);
                return;
        }
    }
    private static void FirstFromIterator(GesVmState vmState, ushort destinationRegister, IGesIterator iterator)
    {
        try
        {
            var item = iterator.Next();
            if (item.HasValue) vmState.SetValue(destinationRegister, in item.Value);
            else vmState.SetNothing(destinationRegister);
        }
        finally
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
        }
    }
    private static void LastFromIterator(GesVmState vmState, ushort destinationRegister, IGesIterator iterator)
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

            if (found) vmState.SetValue(destinationRegister, in last);
            else vmState.SetNothing(destinationRegister);
        }
        finally
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
        }
    }
    private static void SingleFromIterator(GesVmState vmState, ushort destinationRegister, IGesIterator iterator)
    {
        try
        {
            var item = iterator.Next();
            if (!item.HasValue)
            {
                vmState.SetNothing(destinationRegister);
                return;
            }

            var second = iterator.Next();
            if (second.HasValue) vmState.SetNothing(destinationRegister);
            else vmState.SetValue(destinationRegister, in item.Value);
        }
        finally
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
        }
    }
    private static void FirstFromText(GesVmState vmState, ushort destinationRegister, in GesValue source)
    {
        if (GameEventScriptText.ScalarAt(source.TextValue, 0) is { } scalar) vmState.SetText(destinationRegister, scalar);
        else vmState.SetNothing(destinationRegister);
    }
    private static void LastFromText(GesVmState vmState, ushort destinationRegister, in GesValue source)
    {
        if (source.IntegerValue > 0 && GameEventScriptText.ScalarAt(source.TextValue, source.IntegerValue - 1) is { } scalar) vmState.SetText(destinationRegister, scalar);
        else vmState.SetNothing(destinationRegister);
    }
    private static void SingleFromText(GesVmState vmState, ushort destinationRegister, in GesValue source)
    {
        if (source.IntegerValue == 1) vmState.SetText(destinationRegister, source.TextValue);
        else vmState.SetNothing(destinationRegister);
    }
}
