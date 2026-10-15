// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal static class GesVmRegisterMemberIndexAccess
{
    internal static void GesVmMemberAccess(this GesVirtualMachine vm, ushort destinationRegister, string key, in GesValue obj)
    {
        switch (obj.Kind)
        {
            case Map when obj.ObjectValue is GesValueMap map && map.Get(key) is { } value:
                vm.SetValue(destinationRegister, in value);
                return;
            case Custom when obj.ObjectValue is GesCustomObject customObject && customObject.Map.Get(key) is { } value:
                vm.SetValue(destinationRegister, in value);
                return;
            case Custom when obj.ObjectValue is GesExternalStorage externalValue && externalValue.ToMap().Get(key) is { } value:
                vm.SetValue(destinationRegister, in value);
                return;
            case Vector or Point when obj.ObjectValue is GesSpatialValue vp && vp.GetComponent(key) is { } value:
                vm.SetFloat(destinationRegister, value, obj.Unit);
                return;
            case Message when obj.ObjectValue is GameEventScriptMessage message:
                switch (key)
                {
                    case "name":
                        vm.SetText(destinationRegister, message.Name);
                        return;
                    case "arguments":
                        var entries = new GesVmMapBuilder(message.Arguments.Count);
                        for (var index = 0; index < message.Arguments.Count; index++)
                        {
                            entries.Set(message.Arguments.NameAt(index), message.Arguments.VmValueAt(index));
                        }
                        vm.SetMap(destinationRegister, entries.ToMap());
                        return;
                    case "tags":
                        var tagList = new GesValue[message.Tags.Count];
                        for (var i = 0; i < tagList.Length; i++) tagList[i].SetTag(message.Tags[i]);
                        vm.SetList(destinationRegister, tagList);
                        return;
                    case "signature":
                        vm.SetText(destinationRegister, message.SignatureId);
                        return;
                    default:
                        vm.SetNothing(destinationRegister);
                        return;
                }
            case Handler when obj.ObjectValue is GesMessageSignature signature:
                switch (key)
                {
                    case "name":
                        vm.SetText(destinationRegister, signature.Name);
                        return;
                    case "parameters":
                        var list = new GesValue[signature.Parameters.Count];
                        for (var i = 0; i < list.Length; i++) list[i].SetText(signature.Parameters[i]);
                        vm.SetList(destinationRegister, list);
                        return;
                    case "signature":
                        vm.SetText(destinationRegister, signature.SignatureId);
                        return;
                    default:
                        vm.SetNothing(destinationRegister);
                        return;
                }
            default:
                vm.SetNothing(destinationRegister);
                return;
        }
    }

    internal static void GesVmIndexAccess(this GesVirtualMachine vm, ushort destinationRegister, long indexIn, in GesValue obj)
    {
        if (indexIn <= 0)
        {
            vm.SetNothing(destinationRegister);
            return;
        }

        var index = indexIn - 1L;
        switch (obj.Kind)
        {
            case List when obj.ObjectValue is GesValue[] list:
                if (index < list.Length) vm.SetValue(destinationRegister, in list[(int)index]);
                else vm.SetNothing(destinationRegister);
                return;
            case Dice when obj.ObjectValue is int[] dices:
                if (index < dices.Length) vm.SetInteger(destinationRegister, dices[(int)index]);
                else vm.SetNothing(destinationRegister);
                return;
            case Vector or Point when obj.ObjectValue is GesSpatialValue vp:
                switch (index)
                {
                    case 0:
                        vm.SetFloat(destinationRegister, vp.X, obj.Unit);
                        break;
                    case 1:
                        vm.SetFloat(destinationRegister, vp.Y, obj.Unit);
                        break;
                    case 2:
                        vm.SetFloat(destinationRegister, vp.Z, obj.Unit);
                        break;
                    default:
                        vm.SetNothing(destinationRegister);
                        break;
                }
                return;
            case Range when obj.ObjectValue is GesIntegerRange integerRange:
                if (GesRangeMath.GetTerm(integerRange.From, integerRange.To, integerRange.Step, indexIn) is { } intRangeValue) vm.SetInteger(destinationRegister, intRangeValue);
                else vm.SetNothing(destinationRegister);
                return;
            case Range when obj.ObjectValue is GesFloatRange floatRange:
                if (GesRangeMath.GetTerm(floatRange.From, floatRange.To, floatRange.Step, indexIn) is { } floatRangeValue) vm.SetFloat(destinationRegister, floatRangeValue);
                else vm.SetNothing(destinationRegister);
                return;
            case Text or Tag:
                if (GesText.ScalarAt(obj.TextValue, index) is { } scalar) vm.SetText(destinationRegister, scalar);
                else vm.SetNothing(destinationRegister);
                return;
            default:
                vm.SetNothing(destinationRegister);
                return;
        }
    }

    internal static void GesVmPropertyAccess(this GesVirtualMachine vm, ushort destinationRegister, in GesValue property, in GesValue obj)
    {
        switch (property.Kind)
        {
            case Integer:
                vm.GesVmIndexAccess(destinationRegister, property.IntegerValue, in obj);
                return;
            case Text or Tag:
                vm.GesVmMemberAccess(destinationRegister, property.TextValue, in obj);
                return;
            default:
                vm.SetNothing(destinationRegister);
                return;
        }
    }
}
