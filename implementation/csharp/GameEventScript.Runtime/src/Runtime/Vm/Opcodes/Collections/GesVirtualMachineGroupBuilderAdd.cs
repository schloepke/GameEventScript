// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteGroupBuilderAdd(in GesValue groupBuilder, in GesValue keyValue, in GesValue value, GesRuntimeBudget budget)
    {
        if (groupBuilder.Kind is not GroupBuilder || groupBuilder.ObjectValue is not GesVmGroupBuilder builder) return;
        var key = keyValue.Kind is Text or Tag ? keyValue.TextValue : keyValue.ToText;
        builder.Add(key, value, budget);
    }
}
