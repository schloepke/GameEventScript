using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmRegisterMemberIndexAccess
{
    internal static void GesVmMemberAccess(this GesVmState vmState, ushort destinationRegister, string key, in GesVmValue obj)
    {
        switch (obj.Kind)
        {
            case Map or Custom when obj.ObjectValue is GesVmValueMap map && map.TryGet(key, out var value):
                vmState.SetValue(destinationRegister, in value);
                return;
            case Custom when obj.ObjectValue is GesVmExternalObject externalObject && externalObject.ToMap().TryGet(key, out var value):
                vmState.SetValue(destinationRegister, in value);
                return;
            case Vector or Point when obj.ObjectValue is GesVmValueVectorPoint vp && vp.TryGetComponent(key, out var value):
                vmState.SetFloat(destinationRegister, value, obj.Unit);
                return;
            case Message when obj.ObjectValue is GameEventScriptMessage message:
                switch (key)
                {
                    case "name":
                        vmState.SetText(destinationRegister, message.Name);
                        return;
                    case "arguments":
                        var entries = new GesVmValueMapBuilder(message.Arguments.Count);
                        foreach (var argumentKey in message.Arguments.Keys)
                        {
                            entries.Set(argumentKey, message.Arguments[argumentKey].GetVmValue());
                        }
                        vmState.SetMap(destinationRegister, entries.ToMap());
                        return;
                    case "tags":
                        var tagList = new GesVmValue[message.Tags.Count];
                        for (var i = 0; i < tagList.Length; i++) tagList[i].SetTag(message.Tags[i]);
                        vmState.SetList(destinationRegister, tagList);
                        return;
                    case "signature":
                        vmState.SetText(destinationRegister, message.SignatureId);
                        return;
                    default:
                        vmState.SetNothing(destinationRegister);
                        return;
                }
            case Handler when obj.ObjectValue is GameEventScriptMessageSignature signature:
                switch (key)
                {
                    case "name":
                        vmState.SetText(destinationRegister, signature.Name);
                        return;
                    case "parameters":
                        var list = new GesVmValue[signature.Parameters.Count];
                        for (var i = 0; i < list.Length; i++) list[i].SetText(signature.Parameters[i]);
                        vmState.SetList(destinationRegister, list);
                        return;
                    case "signature":
                        vmState.SetText(destinationRegister, signature.SignatureId);
                        return;
                    default:
                        vmState.SetNothing(destinationRegister);
                        return;
                }
            default:
                vmState.SetNothing(destinationRegister);
                return;
        }
    }

    internal static void GesVmIndexAccess(this GesVmState vmState, ushort destinationRegister, long indexIn, in GesVmValue obj)
    {
        if (indexIn <= 0)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        var index = (int)indexIn - 1;
        switch (obj.Kind)
        {
            case List when obj.ObjectValue is GesVmValue[] list:
                if (index < list.Length) vmState.SetValue(destinationRegister, in list[index]);
                else vmState.SetNothing(destinationRegister);
                return;
            case Dice when obj.ObjectValue is int[] dices:
                if (index < dices.Length) vmState.SetInteger(destinationRegister, dices[index]);
                else vmState.SetNothing(destinationRegister);
                return;
            case Vector or Point when obj.ObjectValue is GesVmValueVectorPoint vp:
                switch (index)
                {
                    case 0:
                        vmState.SetFloat(destinationRegister, vp.X, obj.Unit);
                        break;
                    case 1:
                        vmState.SetFloat(destinationRegister, vp.Y, obj.Unit);
                        break;
                    case 2:
                        vmState.SetFloat(destinationRegister, vp.Z, obj.Unit);
                        break;
                    default:
                        vmState.SetNothing(destinationRegister);
                        break;
                }
                return;
            case Range when obj.ObjectValue is GesVmValueRangeInteger integerRange:
                var intRangeValue = integerRange.From + index * integerRange.Step;
                if (integerRange.Step > 0 && intRangeValue <= integerRange.To || integerRange.Step < 0 && intRangeValue >= integerRange.To) vmState.SetInteger(destinationRegister, intRangeValue);
                else vmState.SetNothing(destinationRegister);
                return;
            case Range when obj.ObjectValue is GesVmValueRangeFloat floatRange:
                var floatRangeValue = floatRange.From + index * floatRange.Step;
                if (floatRange.Step > 0 && floatRangeValue <= floatRange.To || floatRange.Step < 0 && floatRangeValue >= floatRange.To) vmState.SetFloat(destinationRegister, floatRangeValue);
                else vmState.SetNothing(destinationRegister);
                return;
            case Text or Tag when obj is { IsStorageObject: true, ObjectValue: string text }:
                if (index < text.Length) vmState.SetText(destinationRegister, text[index].ToString());
                else vmState.SetNothing(destinationRegister);
                return;
            default:
                vmState.SetNothing(destinationRegister);
                return;
        }
    }

    internal static void GesVmPropertyAccess(this GesVmState vmState, ushort destinationRegister, in GesVmValue property, in GesVmValue obj)
    {
        switch (property.Kind)
        {
            case Integer:
                vmState.GesVmIndexAccess(destinationRegister, property.IntegerValue, in obj);
                return;
            case Text or Tag when property.ObjectValue is string key:
                vmState.GesVmMemberAccess(destinationRegister, key, in obj);
                return;
            default:
                vmState.SetNothing(destinationRegister);
                return;
        }
    }
}
