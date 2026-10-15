// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteAtan2(ushort destinationRegister, in GesValue y, in GesValue x)
    {
        if (y.Kind is Nothing || x.Kind is Nothing)
        {
            SetNothing(destinationRegister);
            return;
        }

        var yValue = ReadNumeric(in y);
        var xValue = ReadNumeric(in x);
        if (yValue is not { } yNumber ||
            xValue is not { } xNumber ||
            yNumber.Unit != xNumber.Unit)
        {
            SetNothing(destinationRegister);
            return;
        }

        SetFloat(destinationRegister, yNumber.Number == 0d && xNumber.Number == 0d ? 0d : Math.Atan2(yNumber.Number, xNumber.Number));
    }
}
