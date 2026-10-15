// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteDistance2D(ushort destinationRegister, in GesValue x1, in GesValue y1, in GesValue x2, in GesValue y2)
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

        var dx = read.A - read.C;
        var dy = read.B - read.D;
        SetFloat(destinationRegister, Math.Sqrt(dx * dx + dy * dy), read.Unit);
    }
}
