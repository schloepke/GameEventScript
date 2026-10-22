// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteCastUnit(ushort destinationRegister, in GesValue xValue, GesQuantityUnit unit)
    {
        var dst = CastUnitValue(in xValue, unit, this);
        SetValue(destinationRegister, in dst);
    }
}
