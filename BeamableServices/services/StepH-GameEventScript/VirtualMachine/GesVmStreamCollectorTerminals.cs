using System;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmStreamCollectorTerminals
{
    internal static void GesVmStreamCollectList(this GesVmState vmState, ushort destinationRegister, in GesVmValue iterator)
    {
        if (iterator is not { Kind: Stream, ObjectValue: IGesVmStream stream })
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        var item = new GesVmValue();
        var count = 0;
        var buffer = new GesVmValue[16];
        try
        {
            while (stream.TryNext(ref item))
            {
                if (count == buffer.Length) Array.Resize(ref buffer, buffer.Length << 1);
                buffer[count++] = item;
            }

            if (count == 0)
            {
                vmState.SetList(destinationRegister, vmState.EmptyList);
                return;
            }

            var list = new GesVmValue[count];
            Array.Copy(buffer, list, count);
            vmState.SetList(destinationRegister, list);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }
    internal static void GesVmStreamCollectMap(this GesVmState vmState, ushort destinationRegister, in GesVmValue iterator, ushort itemSlot, ushort keyEntryAddress, IGesVmStreamEntryEvaluator evaluator)
    {
        if (iterator is not { Kind: Stream, ObjectValue: IGesVmStream stream })
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        var item = new GesVmValue();
        var keyValue = new GesVmValue();
        var map = new GesVmValueMapBuilder();
        try
        {
            while (stream.TryNext(ref item))
            {
                if (!evaluator.TryEvaluateStreamEntry(keyEntryAddress, itemSlot, ref item, null, ref keyValue))
                {
                    vmState.SetNothing(destinationRegister);
                    return;
                }

                var key = keyValue.Kind switch
                {
                    Text or Tag => keyValue.TextValue,
                    Nothing => string.Empty,
                    _ => keyValue.ConvertToText()
                };
                if (key.Length == 0) continue;
                map.Set(key, item);
            }

            vmState.SetMap(destinationRegister, map.ToMap());
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }
    internal static void GesVmStreamCollectMapValue(this GesVmState vmState, ushort destinationRegister, in GesVmValue iterator, ushort itemSlot, ushort keyEntryAddress, ushort valueEntryAddress, IGesVmStreamEntryEvaluator evaluator)
    {
        if (iterator is not { Kind: Stream, ObjectValue: IGesVmStream stream })
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        var item = new GesVmValue();
        var keyValue = new GesVmValue();
        var value = new GesVmValue();
        var map = new GesVmValueMapBuilder();
        try
        {
            while (stream.TryNext(ref item))
            {
                if (!evaluator.TryEvaluateStreamEntry(keyEntryAddress, itemSlot, ref item, null, ref keyValue))
                {
                    vmState.SetNothing(destinationRegister);
                    return;
                }

                var key = keyValue.Kind switch
                {
                    Text or Tag => keyValue.TextValue,
                    Nothing => string.Empty,
                    _ => keyValue.ConvertToText()
                };
                if (key.Length == 0) continue;

                if (!evaluator.TryEvaluateStreamEntry(valueEntryAddress, itemSlot, ref item, null, ref value))
                {
                    vmState.SetNothing(destinationRegister);
                    return;
                }

                map.Set(key, value);
            }

            vmState.SetMap(destinationRegister, map.ToMap());
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }
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
            case Stream when source.ObjectValue is IGesVmStream stream:
                FirstFromStream(vmState, destinationRegister, stream);
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
            case Stream when source.ObjectValue is IGesVmStream stream:
                LastFromStream(vmState, destinationRegister, stream);
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
            case Stream when source.ObjectValue is IGesVmStream stream:
                SingleFromStream(vmState, destinationRegister, stream);
                return;
            default:
                vmState.SetNothing(destinationRegister);
                return;
        }
    }
    private static void FirstFromStream(GesVmState vmState, ushort destinationRegister, IGesVmStream stream)
    {
        var item = new GesVmValue();
        try
        {
            if (stream.TryNext(ref item)) vmState.SetValue(destinationRegister, in item);
            else vmState.SetNothing(destinationRegister);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }
    private static void LastFromStream(GesVmState vmState, ushort destinationRegister, IGesVmStream stream)
    {
        var item = new GesVmValue();
        var last = new GesVmValue();
        var found = false;
        try
        {
            while (stream.TryNext(ref item))
            {
                last = item;
                found = true;
            }

            if (found) vmState.SetValue(destinationRegister, in last);
            else vmState.SetNothing(destinationRegister);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }
    private static void SingleFromStream(GesVmState vmState, ushort destinationRegister, IGesVmStream stream)
    {
        var item = new GesVmValue();
        try
        {
            if (!stream.TryNext(ref item))
            {
                vmState.SetNothing(destinationRegister);
                return;
            }

            var second = new GesVmValue();
            if (stream.TryNext(ref second)) vmState.SetNothing(destinationRegister);
            else vmState.SetValue(destinationRegister, in item);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
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
