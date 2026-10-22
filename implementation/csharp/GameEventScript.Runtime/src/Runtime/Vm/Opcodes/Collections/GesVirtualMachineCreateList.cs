// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteCreateList(ushort destinationRegister)
    {
        var list = new GesValue[StageLength];
        for (ushort i = 0; i < StageLength; i++) list[i] = RegisterStaged(i);
        SetList(destinationRegister, list);
    }
}
