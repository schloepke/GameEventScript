// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecutePropertyAccess(ushort destinationRegister, in GesValue property, in GesValue obj)
    {
        switch (property.Kind)
        {
            case Integer:
                ExecuteIndexAccess(destinationRegister, property.IntegerValue, in obj);
                return;
            case Text or Tag:
                ExecuteMemberAccess(destinationRegister, property.TextValue, in obj);
                return;
            default:
                SetNothing(destinationRegister);
                return;
        }
    }
}
