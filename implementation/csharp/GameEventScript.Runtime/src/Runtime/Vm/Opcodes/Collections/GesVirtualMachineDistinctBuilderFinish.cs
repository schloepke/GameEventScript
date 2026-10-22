// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteDistinctBuilderFinish(ushort destinationRegister, in GesValue distinctBuilder)
    {
        if (distinctBuilder.Kind is DistinctBuilder && distinctBuilder.ObjectValue is GesVmDistinctBuilder builder)
        {
            SetList(destinationRegister, builder.ToList());
            return;
        }

        SetNothing(destinationRegister);
    }
}
