// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteZip(ushort dst, in GesValue a, in GesValue b)
    {
        if (a.Kind is not List || b.Kind is not List || a.ObjectValue is not GesValue[] aList || b.ObjectValue is not GesValue[] bList)
        {
            SetNothing(dst);
            return;
        }

        var length = Math.Min(aList.Length, bList.Length);
        var list = new GesValue[length];
        for (var i = 0; i < length; i++)
        {
            var pair = new GesVmMapBuilder(2);
            pair.Set("left", aList[i]);
            pair.Set("right", bList[i]);
            list[i].SetMap(pair.ToMap());
        }

        SetList(dst, list);
    }
}
