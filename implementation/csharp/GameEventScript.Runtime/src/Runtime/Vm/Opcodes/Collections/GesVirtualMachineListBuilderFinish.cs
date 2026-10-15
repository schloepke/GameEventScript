// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteListBuilderFinish(ushort destinationRegister, in GesValue listBuilder)
    {
        if (listBuilder.Kind is ListBuilder && listBuilder.ObjectValue is GesVmListBuilder builder)
        {
            SetList(destinationRegister, builder.ToList());
            return;
        }

        SetNothing(destinationRegister);
    }
}
