// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteOrderBuilderAdd(in GesValue orderBuilder, in GesValue keyValue, in GesValue value, GesRuntimeBudget budget)
    {
        if (orderBuilder.Kind is OrderBuilder && orderBuilder.ObjectValue is GesVmOrderBuilder builder) builder.Add(in keyValue, in value, budget);
    }
}
