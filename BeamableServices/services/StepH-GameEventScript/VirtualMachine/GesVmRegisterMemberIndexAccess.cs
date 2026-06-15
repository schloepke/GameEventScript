using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmRegisterMemberIndexAccess
{
    internal static void GesVmMemberAccess(ref this GesVmValue dst, ushort memberNameIndex, ref GesVmValue obj)
    {
        dst.GesVmMemberAccess(dst.OwningState.Binary.TextConstantTable.Resolve(memberNameIndex), ref obj);
    }
    internal static void GesVmMemberAccess(ref this GesVmValue dst, string key, ref GesVmValue obj)
    {
        switch (obj.Kind)
        {
            case Map or Custom when obj.ObjectValue is GesVmValueMap map && map.TryGet(key, out var value):
                dst = value;
                return;
            case Custom when obj.ObjectValue is GameEventScriptValue externalValue && externalValue.TryGetMapMember(key, out var value):
                dst.BindArguments(value);
                return;
            case Vector or Point when obj.ObjectValue is GesVmValueVectorPoint vp && vp.TryGetComponent(key, out var value):
                dst.SetFloat(value, obj.Unit);
                return;
            case Message when obj.ObjectValue is GameEventScriptMessage message:
                switch (key)
                {
                    case "name":
                        dst.SetText(message.Name);
                        return;
                    case "arguments":
                        var entries = new GesVmValueMapBuilder(dst.OwningState, message.Arguments.Count);
                        foreach(var argumentKey in message.Arguments.Keys)
                        {
                            var value = dst.OwningState.CreateNothing();
                            value.BindArguments(message.Arguments[argumentKey]);
                            entries.Set(argumentKey, value);
                        }
                        dst.SetMap(entries.ToMap());
                        return;
                    case "tags":
                        var tagList = dst.OwningState.CreateList(message.Tags.Count);
                        for(var i = 0; i < tagList.Length; i++) tagList[i].SetTag(message.Tags[i]);
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
                        for (var i = 0; i < list.Length; i++) list[i].SetText(signature.Parameters[i]);
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
    internal static void GesVmIndexAccess(ref this GesVmValue dst, long indexIn, ref GesVmValue obj)
    {
        if (indexIn <= 0)
        {
            dst.SetNothing();
            return;
        }

        var index = (int)indexIn - 1;
        switch (obj.Kind)
        {
            case List when obj.ObjectValue is GesVmValue[] list:
                if (index < list.Length) dst = list[index];
                else dst.SetNothing();
                return;
            case Dice when obj.ObjectValue is int[] dices:
                if (index < dices.Length) dst.SetInteger(dices[index]);
                else dst.SetNothing();
                return;
            case Vector or Point when obj.ObjectValue is GesVmValueVectorPoint vp:
                switch (index)
                {
                    case 0:
                        dst.SetFloat(vp.X, obj.Unit);
                        break;
                    case 1:
                        dst.SetFloat(vp.Y, obj.Unit);
                        break;
                    case 2:
                        dst.SetFloat(vp.Z, obj.Unit);
                        break;
                    default:
                        dst.SetNothing();
                        break;
                }
                return;
            case Range when obj.ObjectValue is GesVmValueRangeInteger integerRange:
                var intRangeValue = integerRange.From + index * integerRange.Step;
                if (integerRange.Step > 0 && intRangeValue <= integerRange.To || integerRange.Step < 0 && intRangeValue >= integerRange.To) dst.SetInteger(intRangeValue);
                else dst.SetNothing();
                return;
            case Range when obj.ObjectValue is GesVmValueRangeFloat floatRange:
                var floatRangeValue = floatRange.From + index * floatRange.Step;
                if (floatRange.Step > 0 && floatRangeValue <= floatRange.To || floatRange.Step < 0 && floatRangeValue >= floatRange.To) dst.SetFloat(floatRangeValue);
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
    internal static void GesVmPropertyAccess(ref this GesVmValue dst, ref GesVmValue property, ref GesVmValue obj)
    {
        if (property.TryGetInteger(out var indexIn))
        {
            dst.GesVmIndexAccess(indexIn, ref obj);
            return;
        }

        switch (property.Kind)
        {
            case Text or Tag when property.IsStoragePointer:
                dst.GesVmMemberAccess((ushort)property.IntegerValue, ref obj);
                return;
            case Text or Tag when property.ObjectValue is string key:
                dst.GesVmMemberAccess(key, ref obj);
                return;
            default:
                dst.SetNothing();
                return;
        }
    }
}
