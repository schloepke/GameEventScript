using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmRegisterMemberIndexAccess
{
    internal static void GesVmMemberAccess(this GesVmState state, ushort destinationRegister, string key, in GesVmValue obj)
    {
        switch (obj.Kind)
        {
            case Map or Custom when obj.ObjectValue is GesVmValueMap map && map.TryGet(key, out var value):
                state.SetValue(destinationRegister, in value);
                return;
            case Custom when obj.ObjectValue is GameEventScriptValue externalValue && externalValue.TryGetMapMember(key, out var value):
                state.Register(destinationRegister).BindArguments(value);
                return;
            case Vector or Point when obj.ObjectValue is GesVmValueVectorPoint vp && vp.TryGetComponent(key, out var value):
                state.SetFloat(destinationRegister, value, obj.Unit);
                return;
            case Message when obj.ObjectValue is GameEventScriptMessage message:
                switch (key)
                {
                    case "name":
                        state.SetText(destinationRegister, message.Name);
                        return;
                    case "arguments":
                        var entries = new GesVmValueMapBuilder(state, message.Arguments.Count);
                        foreach (var argumentKey in message.Arguments.Keys)
                        {
                            var argumentValue = state.CreateNothing();
                            argumentValue.BindArguments(message.Arguments[argumentKey]);
                            entries.Set(argumentKey, argumentValue);
                        }
                        state.SetMap(destinationRegister, entries.ToMap());
                        return;
                    case "tags":
                        var tagList = state.CreateList(message.Tags.Count);
                        for (var i = 0; i < tagList.Length; i++) tagList[i].SetTag(message.Tags[i]);
                        state.SetList(destinationRegister, tagList);
                        return;
                    case "signature":
                        state.SetText(destinationRegister, message.SignatureId);
                        return;
                    default:
                        state.SetNothing(destinationRegister);
                        return;
                }
            case Handler when obj.ObjectValue is GameEventScriptMessageSignature signature:
                switch (key)
                {
                    case "name":
                        state.SetText(destinationRegister, signature.Name);
                        return;
                    case "parameters":
                        var list = state.CreateList(signature.Parameters.Count);
                        for (var i = 0; i < list.Length; i++) list[i].SetText(signature.Parameters[i]);
                        state.SetList(destinationRegister, list);
                        return;
                    case "signature":
                        state.SetText(destinationRegister, signature.SignatureId);
                        return;
                    default:
                        state.SetNothing(destinationRegister);
                        return;
                }
            default:
                state.SetNothing(destinationRegister);
                return;
        }
    }

    internal static void GesVmIndexAccess(this GesVmState state, ushort destinationRegister, long indexIn, in GesVmValue obj)
    {
        if (indexIn <= 0)
        {
            state.SetNothing(destinationRegister);
            return;
        }

        var index = (int)indexIn - 1;
        switch (obj.Kind)
        {
            case List when obj.ObjectValue is GesVmValue[] list:
                if (index < list.Length) state.SetValue(destinationRegister, in list[index]);
                else state.SetNothing(destinationRegister);
                return;
            case Dice when obj.ObjectValue is int[] dices:
                if (index < dices.Length) state.SetInteger(destinationRegister, dices[index]);
                else state.SetNothing(destinationRegister);
                return;
            case Vector or Point when obj.ObjectValue is GesVmValueVectorPoint vp:
                switch (index)
                {
                    case 0:
                        state.SetFloat(destinationRegister, vp.X, obj.Unit);
                        break;
                    case 1:
                        state.SetFloat(destinationRegister, vp.Y, obj.Unit);
                        break;
                    case 2:
                        state.SetFloat(destinationRegister, vp.Z, obj.Unit);
                        break;
                    default:
                        state.SetNothing(destinationRegister);
                        break;
                }
                return;
            case Range when obj.ObjectValue is GesVmValueRangeInteger integerRange:
                var intRangeValue = integerRange.From + index * integerRange.Step;
                if (integerRange.Step > 0 && intRangeValue <= integerRange.To || integerRange.Step < 0 && intRangeValue >= integerRange.To) state.SetInteger(destinationRegister, intRangeValue);
                else state.SetNothing(destinationRegister);
                return;
            case Range when obj.ObjectValue is GesVmValueRangeFloat floatRange:
                var floatRangeValue = floatRange.From + index * floatRange.Step;
                if (floatRange.Step > 0 && floatRangeValue <= floatRange.To || floatRange.Step < 0 && floatRangeValue >= floatRange.To) state.SetFloat(destinationRegister, floatRangeValue);
                else state.SetNothing(destinationRegister);
                return;
            case Text or Tag when obj is { IsStorageObject: true, ObjectValue: string text }:
                if (index < text.Length) state.SetText(destinationRegister, text[index].ToString());
                else state.SetNothing(destinationRegister);
                return;
            default:
                state.SetNothing(destinationRegister);
                return;
        }
    }

    internal static void GesVmPropertyAccess(this GesVmState state, ushort destinationRegister, in GesVmValue property, in GesVmValue obj)
    {
        if (property.TryGetInteger(out var indexIn))
        {
            state.GesVmIndexAccess(destinationRegister, indexIn, in obj);
            return;
        }

        switch (property.Kind)
        {
            case Text or Tag when property.ObjectValue is string key:
                state.GesVmMemberAccess(destinationRegister, key, in obj);
                return;
            default:
                state.SetNothing(destinationRegister);
                return;
        }
    }
}
