// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteMemberAccess(ushort destinationRegister, string key, in GesValue obj)
    {
        switch (obj.Kind)
        {
            case Map when obj.ObjectValue is GesValueMap map && map.Get(key) is { } value:
                SetValue(destinationRegister, in value);
                return;
            case Custom when obj.ObjectValue is GesCustomObject customObject && customObject.Map.Get(key) is { } value:
                SetValue(destinationRegister, in value);
                return;
            case Custom when obj.ObjectValue is GesExternalStorage externalValue && externalValue.ToMap().Get(key) is { } value:
                SetValue(destinationRegister, in value);
                return;
            case Vector or Point when obj.ObjectValue is GesSpatialValue vp && vp.GetComponent(key) is { } value:
                SetFloat(destinationRegister, value, obj.Unit);
                return;
            case Message when obj.ObjectValue is GameEventScriptMessage message:
                switch (key)
                {
                    case "name":
                        SetText(destinationRegister, message.Name);
                        return;
                    case "arguments":
                        var entries = new GesVmMapBuilder(message.Arguments.Count);
                        for (var index = 0; index < message.Arguments.Count; index++)
                        {
                            entries.Set(message.Arguments.NameAt(index), message.Arguments.VmValueAt(index));
                        }
                        SetMap(destinationRegister, entries.ToMap());
                        return;
                    case "tags":
                        var tagList = new GesValue[message.Tags.Count];
                        for (var i = 0; i < tagList.Length; i++) tagList[i].SetTag(message.Tags[i]);
                        SetList(destinationRegister, tagList);
                        return;
                    case "signature":
                        SetText(destinationRegister, message.SignatureId);
                        return;
                    default:
                        SetNothing(destinationRegister);
                        return;
                }
            case Handler when obj.ObjectValue is GesMessageSignature signature:
                switch (key)
                {
                    case "name":
                        SetText(destinationRegister, signature.Name);
                        return;
                    case "parameters":
                        var list = new GesValue[signature.Parameters.Count];
                        for (var i = 0; i < list.Length; i++) list[i].SetText(signature.Parameters[i]);
                        SetList(destinationRegister, list);
                        return;
                    case "signature":
                        SetText(destinationRegister, signature.SignatureId);
                        return;
                    default:
                        SetNothing(destinationRegister);
                        return;
                }
            default:
                SetNothing(destinationRegister);
                return;
        }
    }
}
