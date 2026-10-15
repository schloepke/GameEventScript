// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteXor(ushort dstRegister, in GesValue a, in GesValue b)
    {
        if (a.IsTruthDeterminate && b.IsTruthDeterminate) SetBoolean(dstRegister, a.IsTrue ^ b.IsTrue);
        else SetNothing(dstRegister);
    }
}
