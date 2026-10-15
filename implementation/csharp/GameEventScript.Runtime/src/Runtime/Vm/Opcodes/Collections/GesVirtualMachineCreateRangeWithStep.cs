// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteCreateRangeWithStep(ushort destinationRegister, in GesValue from, in GesValue to, in GesValue step)
    {
        if (from.Kind is Integer && to.Kind is Integer && step.Kind is Integer)
        {
            SetRange(destinationRegister, from.IntegerValue, to.IntegerValue, step.IntegerValue);
        }
        else if (from.IsNumeric && to.IsNumeric && step.IsNumeric)
        {
            SetRange(destinationRegister, from.AsNumeric, to.AsNumeric, step.AsNumeric);
        }
        else
        {
            SetNothing(destinationRegister);
        }
    }
}
