// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteNot(ushort dstRegister, in GesValue a)
    {
        if (a.IsTruthDeterminate) SetBoolean(dstRegister, !a.IsTrue);
        else SetNothing(dstRegister);
    }
}
