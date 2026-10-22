// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteLengthSquared2D(ushort destinationRegister, in GesValue x, in GesValue y)
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

        SetFloat(destinationRegister, read.A * read.A + read.B * read.B);
    }
}
