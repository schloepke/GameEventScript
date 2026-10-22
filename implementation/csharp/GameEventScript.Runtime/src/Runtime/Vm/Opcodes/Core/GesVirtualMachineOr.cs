// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteOr(ushort dstRegister, in GesValue a, in GesValue b)
    {
        if (a.IsTruthDeterminate && b.IsTruthDeterminate) SetBoolean(dstRegister, a.IsTrue || b.IsTrue);
        else if (a.IsTruthIndeterminate && b.IsTrue || a.IsTrue && b.IsTruthIndeterminate) SetBoolean(dstRegister, true);
        else SetNothing(dstRegister);
    }
}
