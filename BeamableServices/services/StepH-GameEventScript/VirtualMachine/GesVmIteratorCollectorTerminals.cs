using System;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmIteratorCollectorTerminals
{
    internal static void GesVmFirst(this GesVmState vmState, ushort destinationRegister, in GesVmValue source)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesVmValue[] list:
                if (list.Length > 0) vmState.SetValue(destinationRegister, in list[0]);
                else vmState.SetNothing(destinationRegister);
                return;
            case Dice when source.ObjectValue is int[] dice:
                if (dice.Length > 0) vmState.SetInteger(destinationRegister, dice[0]);
                else vmState.SetNothing(destinationRegister);
                return;
            case StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeInteger range:
                if (GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step) > 0) vmState.SetInteger(destinationRegister, range.From);
                else vmState.SetNothing(destinationRegister);
                return;
            case StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeFloat range:
                if (GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step) > 0) vmState.SetFloat(destinationRegister, range.From);
                else vmState.SetNothing(destinationRegister);
                return;
            case Map or Custom when source.ObjectValue is GesVmValueMap map:
                if (map.ValueList.Length > 0) vmState.SetValue(destinationRegister, in map.ValueList[0]);
                else vmState.SetNothing(destinationRegister);
                return;
            case Vector or Point when source.ObjectValue is GesVmValueVectorPoint triplet:
                vmState.SetFloat(destinationRegister, triplet.X);
                return;
            case Text or Tag:
                FirstFromText(vmState, destinationRegister, in source);
                return;
            case Iterator when source.ObjectValue is IGesVmIterator iterator:
                FirstFromIterator(vmState, destinationRegister, iterator);
                return;
            default:
                vmState.SetNothing(destinationRegister);
                return;
        }
    }
    internal static void GesVmLast(this GesVmState vmState, ushort destinationRegister, in GesVmValue source)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesVmValue[] list:
                if (list.Length > 0) vmState.SetValue(destinationRegister, in list[list.Length - 1]);
                else vmState.SetNothing(destinationRegister);
                return;
            case Dice when source.ObjectValue is int[] dice:
                if (dice.Length > 0) vmState.SetInteger(destinationRegister, dice[^1]);
                else vmState.SetNothing(destinationRegister);
                return;
            case StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeInteger range:
                if (GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step), out var lastInteger)) vmState.SetInteger(destinationRegister, lastInteger);
                else vmState.SetNothing(destinationRegister);
                return;
            case StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeFloat range:
                if (GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step), out var lastFloat)) vmState.SetFloat(destinationRegister, lastFloat);
                else vmState.SetNothing(destinationRegister);
                return;
            case Map or Custom when source.ObjectValue is GesVmValueMap map:
                if (map.ValueList.Length > 0) vmState.SetValue(destinationRegister, in map.ValueList[map.ValueList.Length - 1]);
                else vmState.SetNothing(destinationRegister);
                return;
            case Vector or Point when source.ObjectValue is GesVmValueVectorPoint triplet:
                vmState.SetFloat(destinationRegister, triplet.Z);
                return;
            case Text or Tag:
                LastFromText(vmState, destinationRegister, in source);
                return;
            case Iterator when source.ObjectValue is IGesVmIterator iterator:
                LastFromIterator(vmState, destinationRegister, iterator);
                return;
            default:
                vmState.SetNothing(destinationRegister);
                return;
        }
    }
    internal static void GesVmSingle(this GesVmState vmState, ushort destinationRegister, in GesVmValue source)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesVmValue[] list:
                if (list.Length == 1) vmState.SetValue(destinationRegister, in list[0]);
                else vmState.SetNothing(destinationRegister);
                return;
            case Dice when source.ObjectValue is int[] dice:
                if (dice.Length == 1) vmState.SetInteger(destinationRegister, dice[0]);
                else vmState.SetNothing(destinationRegister);
                return;
            case StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeInteger range:
                if (GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step) == 1) vmState.SetInteger(destinationRegister, range.From);
                else vmState.SetNothing(destinationRegister);
                return;
            case StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeFloat range:
                if (GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step) == 1) vmState.SetFloat(destinationRegister, range.From);
                else vmState.SetNothing(destinationRegister);
                return;
            case Map or Custom when source.ObjectValue is GesVmValueMap map:
                if (map.ValueList.Length == 1) vmState.SetValue(destinationRegister, in map.ValueList[0]);
                else vmState.SetNothing(destinationRegister);
                return;
            case Text or Tag:
                SingleFromText(vmState, destinationRegister, in source);
                return;
            case Iterator when source.ObjectValue is IGesVmIterator iterator:
                SingleFromIterator(vmState, destinationRegister, iterator);
                return;
            default:
                vmState.SetNothing(destinationRegister);
                return;
        }
    }
    private static void FirstFromIterator(GesVmState vmState, ushort destinationRegister, IGesVmIterator iterator)
    {
        var item = new GesVmValue();
        try
        {
            if (iterator.TryNext(ref item)) vmState.SetValue(destinationRegister, in item);
            else vmState.SetNothing(destinationRegister);
        }
        finally
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
        }
    }
    private static void LastFromIterator(GesVmState vmState, ushort destinationRegister, IGesVmIterator iterator)
    {
        var item = new GesVmValue();
        var last = new GesVmValue();
        var found = false;
        try
        {
            while (iterator.TryNext(ref item))
            {
                last = item;
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
    private static void SingleFromIterator(GesVmState vmState, ushort destinationRegister, IGesVmIterator iterator)
    {
        var item = new GesVmValue();
        try
        {
            if (!iterator.TryNext(ref item))
            {
                vmState.SetNothing(destinationRegister);
                return;
            }

            var second = new GesVmValue();
            if (iterator.TryNext(ref second)) vmState.SetNothing(destinationRegister);
            else vmState.SetValue(destinationRegister, in item);
        }
        finally
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
        }
    }
    private static void FirstFromText(GesVmState vmState, ushort destinationRegister, in GesVmValue source)
    {
        var text = source.TextValue;
        if (text.Length > 0) vmState.SetText(destinationRegister, text[0].ToString());
        else vmState.SetNothing(destinationRegister);
    }
    private static void LastFromText(GesVmState vmState, ushort destinationRegister, in GesVmValue source)
    {
        var text = source.TextValue;
        if (text.Length > 0) vmState.SetText(destinationRegister, text[^1].ToString());
        else vmState.SetNothing(destinationRegister);
    }
    private static void SingleFromText(GesVmState vmState, ushort destinationRegister, in GesVmValue source)
    {
        var text = source.TextValue;
        if (text.Length == 1) vmState.SetText(destinationRegister, text);
        else vmState.SetNothing(destinationRegister);
    }
}
