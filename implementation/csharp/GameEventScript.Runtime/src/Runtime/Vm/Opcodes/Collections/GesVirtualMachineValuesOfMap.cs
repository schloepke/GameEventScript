// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteValuesOfMap(ushort dst, in GesValue a)
    {
        switch (a.Kind)
        {
            case Map when a.ObjectValue is GesValueMap map:
                SetList(dst, map.ValueList);
                break;
            case Custom when a.ObjectValue is GesCustomObject customObject:
                SetList(dst, customObject.Map.ValueList);
                break;
            case Custom when a.ObjectValue is GesExternalStorage externalValue:
                SetList(dst, externalValue.ToMap().ValueList);
                break;
            default:
                SetNothing(dst);
                break;
        }
    }
}
