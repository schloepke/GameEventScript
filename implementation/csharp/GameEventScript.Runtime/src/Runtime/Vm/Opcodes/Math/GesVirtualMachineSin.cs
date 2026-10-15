// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteSin(ushort destinationRegister, in GesValue value)
    {
        if (value.Kind is Nothing)
        {
            SetNothing(destinationRegister);
            return;
        }

        if (ReadRadians(in value) is not { } radians)
        {
            SetNothing(destinationRegister);
            return;
        }

        SetFloat(destinationRegister, Math.Sin(radians));
    }
}
