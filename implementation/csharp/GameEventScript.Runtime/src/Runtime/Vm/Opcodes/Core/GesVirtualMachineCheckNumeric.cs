// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteCheckNumeric(ushort destinationRegister, in GesValue xValue) => SetBoolean(destinationRegister, xValue.IsNumeric);
}
