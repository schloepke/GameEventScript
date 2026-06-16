using System;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmStreamCollectorTerminals
{
    internal static void GesVmStreamCollectList(this GesVmState state, ushort destinationRegister, in GesVmValue iterator)
    {
        if (iterator is not { Kind: Stream, ObjectValue: IGesVmStream stream })
        {
            state.SetNothing(destinationRegister);
            return;
        }

        var item = state.CreateNothing();
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
                state.SetList(destinationRegister, state.EmptyList);
                return;
            }

            var list = state.CreateList(count);
            Array.Copy(buffer, list, count);
            state.SetList(destinationRegister, list);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }
    internal static void GesVmStreamCollectMap(this GesVmState state, ushort destinationRegister, in GesVmValue iterator, ushort itemSlot, ushort keyEntryAddress, IGesVmStreamEntryEvaluator evaluator)
    {
        if (iterator is not { Kind: Stream, ObjectValue: IGesVmStream stream })
        {
            state.SetNothing(destinationRegister);
            return;
        }

        var item = state.CreateNothing();
        var keyValue = state.CreateNothing();
        var map = new GesVmValueMapBuilder(state);
        try
        {
            while (stream.TryNext(ref item))
            {
                if (!evaluator.TryEvaluateStreamEntry(keyEntryAddress, itemSlot, ref item, null, ref keyValue))
                {
                    state.SetNothing(destinationRegister);
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

            state.SetMap(destinationRegister, map.ToMap());
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }
    internal static void GesVmStreamCollectMapValue(this GesVmState state, ushort destinationRegister, in GesVmValue iterator, ushort itemSlot, ushort keyEntryAddress, ushort valueEntryAddress, IGesVmStreamEntryEvaluator evaluator)
    {
        if (iterator is not { Kind: Stream, ObjectValue: IGesVmStream stream })
        {
            state.SetNothing(destinationRegister);
            return;
        }

        var item = state.CreateNothing();
        var keyValue = state.CreateNothing();
        var value = state.CreateNothing();
        var map = new GesVmValueMapBuilder(state);
        try
        {
            while (stream.TryNext(ref item))
            {
                if (!evaluator.TryEvaluateStreamEntry(keyEntryAddress, itemSlot, ref item, null, ref keyValue))
                {
                    state.SetNothing(destinationRegister);
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
                    state.SetNothing(destinationRegister);
                    return;
                }

                map.Set(key, value);
            }

            state.SetMap(destinationRegister, map.ToMap());
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }
    internal static void GesVmFirst(this GesVmState state, ushort destinationRegister, in GesVmValue source)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesVmValue[] list:
                if (list.Length > 0) state.SetValue(destinationRegister, in list[0]);
                else state.SetNothing(destinationRegister);
                return;
            case Dice when source.ObjectValue is int[] dice:
                if (dice.Length > 0) state.SetInteger(destinationRegister, dice[0]);
                else state.SetNothing(destinationRegister);
                return;
            case StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeInteger range:
                if (StepH.GameEventScript.Types.GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step) > 0) state.SetInteger(destinationRegister, range.From);
                else state.SetNothing(destinationRegister);
                return;
            case StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeFloat range:
                if (StepH.GameEventScript.Types.GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step) > 0) state.SetFloat(destinationRegister, range.From);
                else state.SetNothing(destinationRegister);
                return;
            case Map or Custom when source.ObjectValue is GesVmValueMap map:
                if (map.ValueList.Length > 0) state.SetValue(destinationRegister, in map.ValueList[0]);
                else state.SetNothing(destinationRegister);
                return;
            case Vector or Point when source.ObjectValue is GesVmValueVectorPoint triplet:
                state.SetFloat(destinationRegister, triplet.X);
                return;
            case Text or Tag:
                FirstFromText(state, destinationRegister, in source);
                return;
            case Stream when source.ObjectValue is IGesVmStream stream:
                FirstFromStream(state, destinationRegister, stream);
                return;
            default:
                state.SetNothing(destinationRegister);
                return;
        }
    }
    internal static void GesVmLast(this GesVmState state, ushort destinationRegister, in GesVmValue source)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesVmValue[] list:
                if (list.Length > 0) state.SetValue(destinationRegister, in list[list.Length - 1]);
                else state.SetNothing(destinationRegister);
                return;
            case Dice when source.ObjectValue is int[] dice:
                if (dice.Length > 0) state.SetInteger(destinationRegister, dice[^1]);
                else state.SetNothing(destinationRegister);
                return;
            case StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeInteger range:
                if (StepH.GameEventScript.Types.GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, StepH.GameEventScript.Types.GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step), out var lastInteger)) state.SetInteger(destinationRegister, lastInteger);
                else state.SetNothing(destinationRegister);
                return;
            case StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeFloat range:
                if (StepH.GameEventScript.Types.GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, StepH.GameEventScript.Types.GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step), out var lastFloat)) state.SetFloat(destinationRegister, lastFloat);
                else state.SetNothing(destinationRegister);
                return;
            case Map or Custom when source.ObjectValue is GesVmValueMap map:
                if (map.ValueList.Length > 0) state.SetValue(destinationRegister, in map.ValueList[map.ValueList.Length - 1]);
                else state.SetNothing(destinationRegister);
                return;
            case Vector or Point when source.ObjectValue is GesVmValueVectorPoint triplet:
                state.SetFloat(destinationRegister, triplet.Z);
                return;
            case Text or Tag:
                LastFromText(state, destinationRegister, in source);
                return;
            case Stream when source.ObjectValue is IGesVmStream stream:
                LastFromStream(state, destinationRegister, stream);
                return;
            default:
                state.SetNothing(destinationRegister);
                return;
        }
    }
    internal static void GesVmSingle(this GesVmState state, ushort destinationRegister, in GesVmValue source)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesVmValue[] list:
                if (list.Length == 1) state.SetValue(destinationRegister, in list[0]);
                else state.SetNothing(destinationRegister);
                return;
            case Dice when source.ObjectValue is int[] dice:
                if (dice.Length == 1) state.SetInteger(destinationRegister, dice[0]);
                else state.SetNothing(destinationRegister);
                return;
            case StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeInteger range:
                if (StepH.GameEventScript.Types.GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step) == 1) state.SetInteger(destinationRegister, range.From);
                else state.SetNothing(destinationRegister);
                return;
            case StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeFloat range:
                if (StepH.GameEventScript.Types.GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step) == 1) state.SetFloat(destinationRegister, range.From);
                else state.SetNothing(destinationRegister);
                return;
            case Map or Custom when source.ObjectValue is GesVmValueMap map:
                if (map.ValueList.Length == 1) state.SetValue(destinationRegister, in map.ValueList[0]);
                else state.SetNothing(destinationRegister);
                return;
            case Text or Tag:
                SingleFromText(state, destinationRegister, in source);
                return;
            case Stream when source.ObjectValue is IGesVmStream stream:
                SingleFromStream(state, destinationRegister, stream);
                return;
            default:
                state.SetNothing(destinationRegister);
                return;
        }
    }
    private static void FirstFromStream(GesVmState state, ushort destinationRegister, IGesVmStream stream)
    {
        var item = state.CreateNothing();
        try
        {
            if (stream.TryNext(ref item)) state.SetValue(destinationRegister, in item);
            else state.SetNothing(destinationRegister);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }
    private static void LastFromStream(GesVmState state, ushort destinationRegister, IGesVmStream stream)
    {
        var item = state.CreateNothing();
        var last = state.CreateNothing();
        var found = false;
        try
        {
            while (stream.TryNext(ref item))
            {
                last = item;
                found = true;
            }

            if (found) state.SetValue(destinationRegister, in last);
            else state.SetNothing(destinationRegister);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }
    private static void SingleFromStream(GesVmState state, ushort destinationRegister, IGesVmStream stream)
    {
        var item = state.CreateNothing();
        try
        {
            if (!stream.TryNext(ref item))
            {
                state.SetNothing(destinationRegister);
                return;
            }

            var second = state.CreateNothing();
            if (stream.TryNext(ref second)) state.SetNothing(destinationRegister);
            else state.SetValue(destinationRegister, in item);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }
    private static void FirstFromText(GesVmState state, ushort destinationRegister, in GesVmValue source)
    {
        var text = source.TextValue;
        if (text.Length > 0) state.SetText(destinationRegister, text[0].ToString());
        else state.SetNothing(destinationRegister);
    }
    private static void LastFromText(GesVmState state, ushort destinationRegister, in GesVmValue source)
    {
        var text = source.TextValue;
        if (text.Length > 0) state.SetText(destinationRegister, text[^1].ToString());
        else state.SetNothing(destinationRegister);
    }
    private static void SingleFromText(GesVmState state, ushort destinationRegister, in GesVmValue source)
    {
        var text = source.TextValue;
        if (text.Length == 1) state.SetText(destinationRegister, text);
        else state.SetNothing(destinationRegister);
    }
}
