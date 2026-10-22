// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteKeysOfMap(ushort dst, in GesValue a)
    {
        switch (a.Kind)
        {
            case Map when a.ObjectValue is GesValueMap map:
                SetList(dst, map.KeyList);
                break;
            case Custom when a.ObjectValue is GesCustomObject customObject:
                SetList(dst, customObject.Map.KeyList);
                break;
            case Custom when a.ObjectValue is GesExternalStorage externalValue:
                SetList(dst, externalValue.ToMap().KeyList);
                break;
            default:
                SetNothing(dst);
                break;
        }
    }
}
