// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteImplies(ushort dstRegister, in GesValue a, in GesValue b)
    {
        if (a.IsTruthDeterminate && b.IsTruthDeterminate) SetBoolean(dstRegister, !a.IsTrue || b.IsTrue);
        else if (a.IsFalse && b.IsTruthIndeterminate || a.IsTruthIndeterminate && b.IsTrue) SetBoolean(dstRegister, true);
        else SetNothing(dstRegister);
    }
}
