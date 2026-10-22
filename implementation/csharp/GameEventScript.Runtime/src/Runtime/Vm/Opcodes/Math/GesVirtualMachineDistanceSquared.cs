// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteDistanceSquared(ushort destinationRegister, in GesValue left, in GesValue right)
    {
        if (left.Kind is Nothing || right.Kind is Nothing)
        {
            SetNothing(destinationRegister);
            return;
        }

        if (left.IsNumeric && right.IsNumeric)
        {
            if (ReadSameUnit2(in left, in right) is not { } scalarRead)
            {
                SetNothing(destinationRegister);
                return;
            }

            var d = scalarRead.A - scalarRead.B;
            SetFloat(destinationRegister, d * d);
            return;
        }

        if (ReadPair3D(in left, in right) is not { } pair)
        {
            SetNothing(destinationRegister);
            return;
        }

        var dx = pair.Lx - pair.Rx;
        var dy = pair.Ly - pair.Ry;
        var dz = pair.Lz - pair.Rz;
        SetFloat(destinationRegister, dx * dx + dy * dy + dz * dz);
    }
}
