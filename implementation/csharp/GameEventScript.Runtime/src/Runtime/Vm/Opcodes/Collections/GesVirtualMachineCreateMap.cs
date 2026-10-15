// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteCreateMap(ushort destinationRegister, ushort keyNamesIndex)
    {
        var layout = ActiveProgram!.MapLayout(keyNamesIndex);
        if (layout.InputCount != StageLength)
        {
            SetNothing(destinationRegister);
            return;
        }

        var values = new GesValue[layout.Keys.Length];
        for (var i = 0; i < values.Length; i++) values[i] = RegisterStaged((ushort)layout.Sources[i]);
        SetMap(destinationRegister, new GesValueMap(layout.Keys, values));
    }
}
