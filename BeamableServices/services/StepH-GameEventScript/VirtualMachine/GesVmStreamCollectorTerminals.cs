using System;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmStreamCollectorTerminals
{
    internal static void GesVmStreamCollectList(ref this GesVmValue dst, ref GesVmValue iterator)
    {
        if (iterator is not { Kind: Stream, ObjectValue: IGesVmStream stream })
        {
            dst.SetNothing();
            return;
        }

        var item = dst.OwningState.CreateNothing();
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
                dst.SetList(dst.OwningState.EmptyList);
                return;
            }

            var list = dst.OwningState.CreateList(count);
            Array.Copy(buffer, list, count);
            dst.SetList(list);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }
    internal static void GesVmStreamCollectMap(ref this GesVmValue dst, ref GesVmValue iterator, ushort itemSlot, ushort keyEntryAddress, IGesVmStreamEntryEvaluator evaluator)
    {
        if (iterator is not { Kind: Stream, ObjectValue: IGesVmStream stream })
        {
            dst.SetNothing();
            return;
        }

        var item = dst.OwningState.CreateNothing();
        var keyValue = dst.OwningState.CreateNothing();
        var map = new GesVmValueMapBuilder(dst.OwningState);
        try
        {
            while (stream.TryNext(ref item))
            {
                if (!evaluator.TryEvaluateStreamEntry(keyEntryAddress, itemSlot, ref item, null, ref keyValue))
                {
                    dst.SetNothing();
                    return;
                }

                var key = keyValue.Kind switch
                {
                    Text or Tag => keyValue.ReadTextOrTag(),
                    Nothing => string.Empty,
                    _ => keyValue.ConvertToText()
                };
                if (key.Length == 0) continue;
                map.Set(key, item);
            }

            dst.SetMap(map.ToMap());
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }
    internal static void GesVmStreamCollectMapValue(ref this GesVmValue dst, ref GesVmValue iterator, ushort itemSlot, ushort keyEntryAddress, ushort valueEntryAddress, IGesVmStreamEntryEvaluator evaluator)
    {
        if (iterator is not { Kind: Stream, ObjectValue: IGesVmStream stream })
        {
            dst.SetNothing();
            return;
        }

        var item = dst.OwningState.CreateNothing();
        var keyValue = dst.OwningState.CreateNothing();
        var value = dst.OwningState.CreateNothing();
        var map = new GesVmValueMapBuilder(dst.OwningState);
        try
        {
            while (stream.TryNext(ref item))
            {
                if (!evaluator.TryEvaluateStreamEntry(keyEntryAddress, itemSlot, ref item, null, ref keyValue))
                {
                    dst.SetNothing();
                    return;
                }

                var key = keyValue.Kind switch
                {
                    Text or Tag => keyValue.ReadTextOrTag(),
                    Nothing => string.Empty,
                    _ => keyValue.ConvertToText()
                };
                if (key.Length == 0) continue;

                if (!evaluator.TryEvaluateStreamEntry(valueEntryAddress, itemSlot, ref item, null, ref value))
                {
                    dst.SetNothing();
                    return;
                }

                map.Set(key, value);
            }

            dst.SetMap(map.ToMap());
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }
    internal static void GesVmFirst(ref this GesVmValue dst, ref GesVmValue source)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesVmValue[] list:
                dst = list.Length > 0 ? list[0] : dst.OwningState.CreateNothing();
                return;
            case Dice when source.ObjectValue is int[] dice:
                if (dice.Length > 0) dst.SetInteger(dice[0]);
                else dst.SetNothing();
                return;
            case StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeInteger range:
                if (StepH.GameEventScript.Types.GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step) > 0) dst.SetInteger(range.From);
                else dst.SetNothing();
                return;
            case StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeFloat range:
                if (StepH.GameEventScript.Types.GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step) > 0) dst.SetFloat(range.From);
                else dst.SetNothing();
                return;
            case Map or Custom when source.ObjectValue is GesVmValueMap map:
                dst = map.ValueList.Length > 0 ? map.ValueList[0] : dst.OwningState.CreateNothing();
                return;
            case Vector or Point when source.ObjectValue is GesVmValueVectorPoint triplet:
                dst.SetFloat(triplet.X);
                return;
            case Text or Tag:
                FirstFromText(ref dst, ref source);
                return;
            case Stream when source.ObjectValue is IGesVmStream stream:
                FirstFromStream(ref dst, stream);
                return;
            default:
                dst.SetNothing();
                return;
        }
    }
    internal static void GesVmLast(ref this GesVmValue dst, ref GesVmValue source)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesVmValue[] list:
                dst = list.Length > 0 ? list[list.Length - 1] : dst.OwningState.CreateNothing();
                return;
            case Dice when source.ObjectValue is int[] dice:
                if (dice.Length > 0) dst.SetInteger(dice[^1]);
                else dst.SetNothing();
                return;
            case StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeInteger range:
                if (StepH.GameEventScript.Types.GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, StepH.GameEventScript.Types.GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step), out var lastInteger)) dst.SetInteger(lastInteger);
                else dst.SetNothing();
                return;
            case StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeFloat range:
                if (StepH.GameEventScript.Types.GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, StepH.GameEventScript.Types.GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step), out var lastFloat)) dst.SetFloat(lastFloat);
                else dst.SetNothing();
                return;
            case Map or Custom when source.ObjectValue is GesVmValueMap map:
                dst = map.ValueList.Length > 0 ? map.ValueList[map.ValueList.Length - 1] : dst.OwningState.CreateNothing();
                return;
            case Vector or Point when source.ObjectValue is GesVmValueVectorPoint triplet:
                dst.SetFloat(triplet.Z);
                return;
            case Text or Tag:
                LastFromText(ref dst, ref source);
                return;
            case Stream when source.ObjectValue is IGesVmStream stream:
                LastFromStream(ref dst, stream);
                return;
            default:
                dst.SetNothing();
                return;
        }
    }
    internal static void GesVmSingle(ref this GesVmValue dst, ref GesVmValue source)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesVmValue[] list:
                dst = list.Length == 1 ? list[0] : dst.OwningState.CreateNothing();
                return;
            case Dice when source.ObjectValue is int[] dice:
                if (dice.Length == 1) dst.SetInteger(dice[0]);
                else dst.SetNothing();
                return;
            case StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeInteger range:
                if (StepH.GameEventScript.Types.GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step) == 1) dst.SetInteger(range.From);
                else dst.SetNothing();
                return;
            case StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeFloat range:
                if (StepH.GameEventScript.Types.GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step) == 1) dst.SetFloat(range.From);
                else dst.SetNothing();
                return;
            case Map or Custom when source.ObjectValue is GesVmValueMap map:
                dst = map.ValueList.Length == 1 ? map.ValueList[0] : dst.OwningState.CreateNothing();
                return;
            case Text or Tag:
                SingleFromText(ref dst, ref source);
                return;
            case Stream when source.ObjectValue is IGesVmStream stream:
                SingleFromStream(ref dst, stream);
                return;
            default:
                dst.SetNothing();
                return;
        }
    }
    private static void FirstFromStream(ref GesVmValue dst, IGesVmStream stream)
    {
        var item = dst.OwningState.CreateNothing();
        try
        {
            if (stream.TryNext(ref item)) dst = item;
            else dst.SetNothing();
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }
    private static void LastFromStream(ref GesVmValue dst, IGesVmStream stream)
    {
        var item = dst.OwningState.CreateNothing();
        var last = dst.OwningState.CreateNothing();
        var found = false;
        try
        {
            while (stream.TryNext(ref item))
            {
                last = item;
                found = true;
            }

            if (found) dst = last;
            else dst.SetNothing();
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }
    private static void SingleFromStream(ref GesVmValue dst, IGesVmStream stream)
    {
        var item = dst.OwningState.CreateNothing();
        try
        {
            if (!stream.TryNext(ref item))
            {
                dst.SetNothing();
                return;
            }

            var second = dst.OwningState.CreateNothing();
            if (stream.TryNext(ref second)) dst.SetNothing();
            else dst = item;
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }
    private static void FirstFromText(ref GesVmValue dst, ref GesVmValue source)
    {
        var text = source.ReadTextOrTag();
        if (text.Length > 0) dst.SetText(text[0].ToString());
        else dst.SetNothing();
    }
    private static void LastFromText(ref GesVmValue dst, ref GesVmValue source)
    {
        var text = source.ReadTextOrTag();
        if (text.Length > 0) dst.SetText(text[^1].ToString());
        else dst.SetNothing();
    }
    private static void SingleFromText(ref GesVmValue dst, ref GesVmValue source)
    {
        var text = source.ReadTextOrTag();
        if (text.Length == 1) dst.SetText(text);
        else dst.SetNothing();
    }

}
