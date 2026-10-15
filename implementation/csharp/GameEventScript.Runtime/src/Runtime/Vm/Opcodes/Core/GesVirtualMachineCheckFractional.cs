// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteCheckFractional(ushort destinationRegister, in GesValue xValue)
    {
        if (!xValue.IsNumeric)
        {
            SetBoolean(destinationRegister, false);
            return;
        }

        var number = xValue.AsNumeric;
        SetBoolean(destinationRegister, double.IsFinite(number) && number != Math.Truncate(number));
    }
}
