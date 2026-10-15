// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteAngleBetween(ushort destinationRegister, in GesValue left, in GesValue right)
    {
        if (left.Kind is Nothing || right.Kind is Nothing)
        {
            SetNothing(destinationRegister);
            return;
        }

        if (ReadPair3D(in left, in right) is not { } pair)
        {
            SetNothing(destinationRegister);
            return;
        }

        SetAngleBetween(this, destinationRegister, pair.Lx, pair.Ly, pair.Lz, pair.Rx, pair.Ry, pair.Rz);
    }
}
