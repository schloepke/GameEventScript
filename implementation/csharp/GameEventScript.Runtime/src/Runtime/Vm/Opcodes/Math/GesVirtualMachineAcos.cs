// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteAcos(ushort destinationRegister, in GesValue value)
    {
        if (value.Kind is Nothing)
        {
            SetNothing(destinationRegister);
            return;
        }

        if (ReadUnitlessNumeric(in value) is not { } number || number < -1d || number > 1d)
        {
            SetNothing(destinationRegister);
            return;
        }

        SetFloat(destinationRegister, Math.Acos(number));
    }
}
