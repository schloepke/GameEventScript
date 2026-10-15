// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteDistinctBuilderAdd(in GesValue distinctBuilder, in GesValue keyValue, in GesValue value, GesRuntimeBudget budget)
    {
        if (distinctBuilder.Kind is DistinctBuilder && distinctBuilder.ObjectValue is GesVmDistinctBuilder builder) builder.Add(in keyValue, in value, budget);
    }
}
