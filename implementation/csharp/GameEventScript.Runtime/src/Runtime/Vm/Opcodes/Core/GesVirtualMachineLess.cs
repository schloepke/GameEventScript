// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteLess(ushort destinationRegister, in GesValue a, in GesValue b)
    {
        switch (a.Kind)
        {
            case Nothing:
                SetNothing(destinationRegister);
                return;
            case Integer when b.Kind is Integer:
                SetBoolean(destinationRegister, a.Unit == b.Unit && a.IntegerValue < b.IntegerValue);
                return;
            case Float when b.Kind is Float:
                SetBoolean(destinationRegister, a.Unit == b.Unit && a.FloatValue < b.FloatValue);
                return;
            default:
                if (b.Kind is Nothing)
                {
                    SetNothing(destinationRegister);
                    return;
                }

                if (a.IsNumeric && b.IsNumeric)
                {
                    SetBoolean(destinationRegister, a.Unit == b.Unit && a.AsNumeric < b.AsNumeric);
                    return;
                }

                SetBoolean(destinationRegister, false);
                return;
        }
    }
}
