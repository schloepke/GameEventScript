// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteCreateRecordValue(ushort destinationRegister, in GesValue mapValue, ushort typeTextPointer)
    {
        if (mapValue.Kind is Map && mapValue.ObjectValue is GesValueMap map)
        {
            SetRecord(destinationRegister, FetchStringByPointer(typeTextPointer), map);
            return;
        }

        SetNothing(destinationRegister);
    }
}
