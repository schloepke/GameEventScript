// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteCreateRange(ushort destinationRegister, in GesValue from, in GesValue to)
    {
        if (from.Kind is Integer && to.Kind is Integer)
        {
            SetRange(destinationRegister, from.IntegerValue, to.IntegerValue, 1);
        }
        else if (from.IsNumeric && to.IsNumeric)
        {
            SetRange(destinationRegister, from.AsNumeric, to.AsNumeric, 1.0d);
        }
        else
        {
            SetNothing(destinationRegister);
        }
    }
}
