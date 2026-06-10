using System.Collections.Generic;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmRegisterMemberIndexAccess
{
    internal static void VmMemberAccess(ref this GesVmValue dst, ushort memberNameIndex, ref GesVmValue obj)
    {
        dst.VmMemberAccess(dst.OwningState.Binary.TextConstantTable.Resolve(memberNameIndex), ref obj);
    }
    internal static void VmMemberAccess(ref this GesVmValue dst, string key, ref GesVmValue obj)
    {
        switch (obj.Kind)
        {
            case Map or Custom when obj.ObjectValue is GesVmMapObject map && map.TryGet(key, out var value):
                dst = value;
                return;
            case Custom when obj.ObjectValue is GameEventScriptValue externalValue && externalValue.TryGetMapMember(key, out var value):
                dst.BindArguments(value);
                return;
            case Vector or Point when obj.ObjectValue is GesVmFloatTriplet vp && vp.TryGet(key, out var value):
                dst.SetFloat(value, obj.Unit);
                return;
            case Message when obj.ObjectValue is GameEventScriptMessage message:
                switch (key)
                {
                    case "name":
                        dst.SetText(message.Name);
                        return;
                    case "arguments":
                        var entries = new Dictionary<string, GesVmValue>();
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
    internal static void VmIndexAccess(ref this GesVmValue dst, long indexIn, ref GesVmValue obj)
    {
        if (indexIn <= 0)
        {
            dst.SetNothing();
            return;
        }

        var index = (int)indexIn - 1;
        switch (obj.Kind)
        {
            case List when obj.ObjectValue is GesVmListObject map && map.TryGet(index, out var value):
                dst = value;
                return;
            case Dice when obj.ObjectValue is int[] dices:
                if (index < dices.Length) dst.SetInteger(dices[index]);
                else dst.SetNothing();
                return;
            case Vector or Point when obj.ObjectValue is GesVmFloatTriplet vp && vp.TryGet(index, out var value):
                dst.SetFloat(value, obj.Unit);
                return;
            case Range when obj.ObjectValue is GesVmRange integerRange:
                var intRangeValue = integerRange.from + index * integerRange.step;
                if (integerRange.step > 0 && intRangeValue <= integerRange.to || integerRange.step < 0 && intRangeValue >= integerRange.to) dst.SetInteger(intRangeValue);
                else dst.SetNothing();
                return;
            case Range when obj.ObjectValue is GesVmFloatRange floatRange:
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
    internal static void VmPropertyAccess(ref this GesVmValue dst, ref GesVmValue property, ref GesVmValue obj)
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
