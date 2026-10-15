// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteOrderBuilderFinishAscending(ushort destinationRegister, in GesValue orderBuilder)
    {
        if (orderBuilder.Kind is OrderBuilder && orderBuilder.ObjectValue is GesVmOrderBuilder builder && builder.ToList(descending: false) is { } list)
        {
            SetList(destinationRegister, list);
            return;
        }

        SetNothing(destinationRegister);
    }
}
