// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteNormalize3D(ushort destinationRegister, in GesValue x, in GesValue y, in GesValue z)
    {
        if (x.Kind is Nothing || y.Kind is Nothing || z.Kind is Nothing)
        {
            SetNothing(destinationRegister);
            return;
        }

        if (ReadSameUnit3(in x, in y, in z) is not { } read)
        {
            SetNothing(destinationRegister);
            return;
        }

        var length = Math.Sqrt(read.A * read.A + read.B * read.B + read.C * read.C);
        if (length == 0d || double.IsNaN(length))
        {
            SetNothing(destinationRegister);
            return;
        }

        SetVector(destinationRegister, read.A / length, read.B / length, read.C / length);
    }
}
