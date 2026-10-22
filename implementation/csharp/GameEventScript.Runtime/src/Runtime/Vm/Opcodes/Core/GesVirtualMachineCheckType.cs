// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteCheckType(ushort destinationRegister, in GesValue xValue, GesBytecodeTypeKind type)
    {
        SetBoolean(destinationRegister, type switch
        {
            Nothing => xValue.IsNothing,
            Map => xValue.Kind is Map or Custom,
            Custom => false,
            _ => xValue.IsNotNothing && xValue.Kind == type
        });
    }
}
