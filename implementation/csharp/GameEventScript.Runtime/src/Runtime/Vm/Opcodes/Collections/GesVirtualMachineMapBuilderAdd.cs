// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteMapBuilderAdd(in GesValue mapBuilder, in GesValue keyValue, in GesValue value, GesRuntimeBudget budget)
    {
        if (mapBuilder.Kind is not MapBuilder || mapBuilder.ObjectValue is not GesVmMapBuilder builder) return;
        var key = keyValue.Kind switch
        {
            Text or Tag => keyValue.TextValue,
            Nothing => string.Empty,
            _ => keyValue.ToText
        };
        if (key.Length == 0) return;
        builder.Set(key, value, budget);
    }
}
