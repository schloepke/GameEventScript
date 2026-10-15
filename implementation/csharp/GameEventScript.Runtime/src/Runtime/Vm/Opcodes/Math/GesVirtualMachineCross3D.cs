// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteCross3D(ushort destinationRegister, in GesValue x1, in GesValue y1, in GesValue z1, in GesValue x2, in GesValue y2, in GesValue z2)
    {
        if (x1.Kind is Nothing || y1.Kind is Nothing || z1.Kind is Nothing || x2.Kind is Nothing || y2.Kind is Nothing || z2.Kind is Nothing)
        {
            SetNothing(destinationRegister);
            return;
        }

        if (ReadSameUnit6(in x1, in y1, in z1, in x2, in y2, in z2) is not { } read)
        {
            SetNothing(destinationRegister);
            return;
        }

        SetVector(destinationRegister, read.B * read.F - read.C * read.E, read.C * read.D - read.A * read.F, read.A * read.E - read.B * read.D);
    }
}
