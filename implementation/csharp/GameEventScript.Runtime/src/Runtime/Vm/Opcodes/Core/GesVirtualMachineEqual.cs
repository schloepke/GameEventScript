// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteEqual(ushort destinationRegister, in GesValue a, in GesValue b)
    {
        if (a.Kind is Nothing || b.Kind is Nothing) SetNothing(destinationRegister);
        else SetBoolean(destinationRegister, ValuesEqual(in a, in b));
    }
}
