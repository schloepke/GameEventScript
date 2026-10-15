// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteCastNumeric(ushort destinationRegister, in GesValue xValue)
    {
        var dst = CastNumericValue(in xValue, this);
        SetValue(destinationRegister, in dst);
    }
}
