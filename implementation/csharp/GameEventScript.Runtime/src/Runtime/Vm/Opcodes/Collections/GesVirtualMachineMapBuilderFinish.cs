// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteMapBuilderFinish(ushort destinationRegister, in GesValue mapBuilder)
    {
        if (mapBuilder.Kind is MapBuilder && mapBuilder.ObjectValue is GesVmMapBuilder builder)
        {
            SetMap(destinationRegister, builder.ToMap());
            return;
        }

        SetNothing(destinationRegister);
    }
}
