// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteCheckInteger(ushort destinationRegister, in GesValue xValue)
    {
        if (!xValue.IsNumeric)
        {
            SetBoolean(destinationRegister, false);
            return;
        }

        var number = xValue.AsNumeric;
        SetBoolean(destinationRegister, GesNumber.CanRepresentAsInteger(number));
    }
}
