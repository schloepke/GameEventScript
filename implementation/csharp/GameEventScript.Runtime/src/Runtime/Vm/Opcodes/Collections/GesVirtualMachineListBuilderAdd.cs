// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteListBuilderAdd(in GesValue listBuilder, in GesValue value, GesRuntimeBudget budget)
    {
        if (listBuilder.Kind is ListBuilder && listBuilder.ObjectValue is GesVmListBuilder builder && budget.CheckGeneratedCollectionItemCountWithinLimit((long)builder.Count + 1)) builder.Add(value);
    }
}
