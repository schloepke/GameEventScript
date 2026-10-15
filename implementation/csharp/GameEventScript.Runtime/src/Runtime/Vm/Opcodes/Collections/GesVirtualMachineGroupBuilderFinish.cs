// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteGroupBuilderFinish(ushort destinationRegister, in GesValue groupBuilder)
    {
        if (groupBuilder.Kind is GroupBuilder && groupBuilder.ObjectValue is GesVmGroupBuilder builder)
        {
            var result = builder.ToMapValue();
            SetValue(destinationRegister, in result);
            return;
        }

        SetNothing(destinationRegister);
    }
}
