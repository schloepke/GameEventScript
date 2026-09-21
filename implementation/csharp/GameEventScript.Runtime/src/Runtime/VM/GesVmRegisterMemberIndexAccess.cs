// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace GameEventScript.Runtime.VM;

internal static class GesVmRegisterMemberIndexAccess
{
    internal static void GesVmMemberAccess(this GesVmState vmState, ushort destinationRegister, string key, in GesValue obj)
    {
        switch (obj.Kind)
        {
            case Map when obj.ObjectValue is GesValueMap map && map.Get(key) is { } value:
                vmState.SetValue(destinationRegister, in value);
                return;
            case Custom when obj.ObjectValue is GesCustomObject customObject && customObject.Map.Get(key) is { } value:
                vmState.SetValue(destinationRegister, in value);
                return;
            case Custom when obj.ObjectValue is GesExternalValue externalValue && externalValue.ToMap().Get(key) is { } value:
                vmState.SetValue(destinationRegister, in value);
                return;
            case Vector or Point when obj.ObjectValue is GesValueVectorPoint vp && vp.GetComponent(key) is { } value:
                vmState.SetFloat(destinationRegister, value, obj.Unit);
                return;
            case Message when obj.ObjectValue is GameEventScriptMessage message:
                switch (key)
                {
                    case "name":
                        vmState.SetText(destinationRegister, message.Name);
                        return;
                    case "arguments":
                        var entries = new GesVmMapBuilder(message.Arguments.Count);
                        for (var index = 0; index < message.Arguments.Count; index++)
                        {
                            entries.Set(message.Arguments.NameAt(index), message.Arguments.VmValueAt(index));
                        }
                        vmState.SetMap(destinationRegister, entries.ToMap());
                        return;
                    case "tags":
                        var tagList = new GesValue[message.Tags.Count];
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
                        var list = new GesValue[signature.Parameters.Count];
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

    internal static void GesVmIndexAccess(this GesVmState vmState, ushort destinationRegister, long indexIn, in GesValue obj)
    {
        if (indexIn <= 0)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        var index = indexIn - 1L;
        switch (obj.Kind)
        {
            case List when obj.ObjectValue is GesValue[] list:
                if (index < list.Length) vmState.SetValue(destinationRegister, in list[(int)index]);
                else vmState.SetNothing(destinationRegister);
                return;
            case Dice when obj.ObjectValue is int[] dices:
                if (index < dices.Length) vmState.SetInteger(destinationRegister, dices[(int)index]);
                else vmState.SetNothing(destinationRegister);
                return;
            case Vector or Point when obj.ObjectValue is GesValueVectorPoint vp:
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
            case Range when obj.ObjectValue is GesValueRangeInteger integerRange:
                if (GameEventScriptRangeMath.GetTerm(integerRange.From, integerRange.To, integerRange.Step, indexIn) is { } intRangeValue) vmState.SetInteger(destinationRegister, intRangeValue);
                else vmState.SetNothing(destinationRegister);
                return;
            case Range when obj.ObjectValue is GesValueRangeFloat floatRange:
                if (GameEventScriptRangeMath.GetTerm(floatRange.From, floatRange.To, floatRange.Step, indexIn) is { } floatRangeValue) vmState.SetFloat(destinationRegister, floatRangeValue);
                else vmState.SetNothing(destinationRegister);
                return;
            case Text or Tag when obj is { IsStorageObject: true, ObjectValue: string text }:
                if (GameEventScriptText.ScalarAt(text, index) is { } scalar) vmState.SetText(destinationRegister, scalar);
                else vmState.SetNothing(destinationRegister);
                return;
            default:
                vmState.SetNothing(destinationRegister);
                return;
        }
    }

    internal static void GesVmPropertyAccess(this GesVmState vmState, ushort destinationRegister, in GesValue property, in GesValue obj)
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
