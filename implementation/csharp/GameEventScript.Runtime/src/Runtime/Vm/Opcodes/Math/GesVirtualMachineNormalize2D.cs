// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteNormalize2D(ushort destinationRegister, in GesValue x, in GesValue y)
    {
        if (x.Kind is Nothing || y.Kind is Nothing)
        {
            SetNothing(destinationRegister);
            return;
        }

        if (ReadSameUnit2(in x, in y) is not { } read)
        {
            SetNothing(destinationRegister);
            return;
        }

        var length = Math.Sqrt(read.A * read.A + read.B * read.B);
        if (length == 0d || double.IsNaN(length))
        {
            SetNothing(destinationRegister);
            return;
        }

        SetVector(destinationRegister, read.A / length, read.B / length, 0d);
    }
}
