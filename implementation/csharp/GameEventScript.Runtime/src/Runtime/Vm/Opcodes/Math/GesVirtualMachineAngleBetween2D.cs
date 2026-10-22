// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteAngleBetween2D(ushort destinationRegister, in GesValue x1, in GesValue y1, in GesValue x2, in GesValue y2)
    {
        if (x1.Kind is Nothing || y1.Kind is Nothing || x2.Kind is Nothing || y2.Kind is Nothing)
        {
            SetNothing(destinationRegister);
            return;
        }

        if (ReadSameUnit4(in x1, in y1, in x2, in y2) is not { } read)
        {
            SetNothing(destinationRegister);
            return;
        }

        SetAngleBetween(this, destinationRegister, read.A, read.B, 0d, read.C, read.D, 0d);
    }
}
