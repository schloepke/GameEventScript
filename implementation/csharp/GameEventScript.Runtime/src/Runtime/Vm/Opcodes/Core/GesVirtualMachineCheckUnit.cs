// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteCheckUnit(ushort destinationRegister, in GesValue xValue, GesQuantityUnit unit)
    {
        SetBoolean(destinationRegister, xValue.Kind is Integer or Float or Vector or Point && xValue.Unit == unit);
    }
}
