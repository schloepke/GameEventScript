using System.Collections.Generic;
using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterMemberIndexAccess
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmMemberAccess(ref this VmValue dst, ushort memberNameIndex, ref VmValue obj)
    {
        dst.VmMemberAccess(dst.OwningState.Binary.TextConstantTable.Resolve(memberNameIndex), ref obj);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmMemberAccess(ref this VmValue dst, string key, ref VmValue obj)
    {
        switch (obj.Kind)
        {
            case Map or Custom when obj.ObjectValue is VmMapObject map && map.TryGet(key, out var value):
                dst = value;
                return;
            case Vector or Point when obj.ObjectValue is VmFloatTriplet vp && vp.TryGet(key, out var value):
                dst.SetFloat(value, obj.Unit);
                return;
            case Message when obj.ObjectValue is GameEventScriptMessage message:
                switch (key)
                {
                    case "name":
                        dst.SetText(message.Name);
                        return;
                    case "arguments":
                        var entries = new Dictionary<string, VmValue>();
                        foreach(var argumentKey in message.Arguments.Keys)
                        {
                            var value = dst.OwningState.CreateNothing();
                            value.BindArguments(message.Arguments[argumentKey]);
                            entries.Add(argumentKey, value);
                        }
                        dst.SetMap(dst.OwningState.CreateMap(entries));
                        return;
                    case "tags":
                        var tagList = dst.OwningState.CreateList(message.Tags.Count);
                        for(var i = 0; i < tagList.Length; i++) tagList.Items[i].SetTag(message.Tags[i]);
                        dst.SetList(tagList);
                        return;
                    case "signature":
                        dst.SetText(message.SignatureId);
                        return;
                    default:
                        dst.SetNothing();
                        return;
                }
            case Handler when obj.ObjectValue is GameEventScriptMessageSignature signature:
                switch (key)
                {
                    case "name":
                        dst.SetText(signature.Name);
                        return;
                    case "parameters":
                        var list = dst.OwningState.CreateList(signature.Parameters.Count);
                        for (var i = 0; i < list.Length; i++) list.Items[i].SetText(signature.Parameters[i]);
                        dst.SetList(list);
                        return;
                    case "signature":
                        dst.SetText(signature.SignatureId);
                        return;
                    default:
                        dst.SetNothing();
                        return;
                }
            default:
                dst.SetNothing();
                return;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmIndexAccess(ref this VmValue dst, long indexIn, ref VmValue obj)
    {
        if (indexIn <= 0)
        {
            dst.SetNothing();
            return;
        }

        var index = (int)indexIn - 1;
        switch (obj.Kind)
        {
            case List when obj.ObjectValue is VmListObject map && map.TryGet(index, out var value):
                dst = value;
                return;
            case Dice when obj.ObjectValue is int[] dices:
                if (index < dices.Length) dst.SetInteger(dices[index]);
                else dst.SetNothing();
                return;
            case Vector or Point when obj.ObjectValue is VmFloatTriplet vp && vp.TryGet(index, out var value):
                dst.SetFloat(value, obj.Unit);
                return;
            case Range when obj.ObjectValue is VmRange integerRange:
                var intRangeValue = integerRange.from + index * integerRange.step;
                if (integerRange.step > 0 && intRangeValue <= integerRange.to || integerRange.step < 0 && intRangeValue >= integerRange.to) dst.SetInteger(intRangeValue);
                else dst.SetNothing();
                return;
            case Range when obj.ObjectValue is VmFloatRange floatRange:
                var floatRangeValue = floatRange.from + index * floatRange.step;
                if (floatRange.step > 0 && floatRangeValue <= floatRange.to || floatRange.step < 0 && floatRangeValue >= floatRange.to) dst.SetFloat(floatRangeValue);
                else dst.SetNothing();
                return;
            case Text or Tag when obj.IsStoragePointer:
                var resolvedText = dst.OwningState.Binary.TextConstantTable.Resolve((ushort)obj.IntegerValue);
                if (index < resolvedText.Length) dst.SetText(resolvedText[index].ToString());
                else dst.SetNothing();
                return;
            case Text or Tag when obj is { IsStorageObject: true, ObjectValue: string text }:
                if (index < text.Length) dst.SetText(text[index].ToString());
                else dst.SetNothing();
                return;
            default:
                dst.SetNothing();
                return;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmPropertyAccess(ref this VmValue dst, ref VmValue property, ref VmValue obj)
    {
        if (property.TryGetInteger(out var indexIn))
        {
            dst.VmIndexAccess(indexIn, ref obj);
            return;
        }

        switch (property.Kind)
        {
            case Text or Tag when property.IsStoragePointer:
                dst.VmMemberAccess((ushort)property.IntegerValue, ref obj);
                return;
            case Text or Tag when property.ObjectValue is string key:
                dst.VmMemberAccess(key, ref obj);
                return;
            default:
                dst.SetNothing();
                return;
        }
    }
}
