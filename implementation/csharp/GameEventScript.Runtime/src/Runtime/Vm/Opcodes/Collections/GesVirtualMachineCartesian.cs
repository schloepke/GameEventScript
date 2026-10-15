// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteCartesian(ushort destination, in GesValue left, in GesValue right, GesRuntimeBudget budget)
    {
        if (left.Kind != List || right.Kind != List) { SetNothing(destination); return; }
        var a = (GesValue[])left.ObjectValue!;
        var b = (GesValue[])right.ObjectValue!;
        var count = (long)a.Length * b.Length;
        if (!budget.CheckGeneratedCollectionItemCountWithinLimit(count) || count > int.MaxValue || count > 0 && !budget.CheckGeneratedCollectionItemCountWithinLimit(2))
        {
            SetNothing(destination);
            return;
        }
        var result = new GesValue[(int)count];
        var index = 0;
        foreach (var first in a)
            foreach (var second in b) result[index++].SetList([first, second]);
        SetList(destination, result);
    }
}
