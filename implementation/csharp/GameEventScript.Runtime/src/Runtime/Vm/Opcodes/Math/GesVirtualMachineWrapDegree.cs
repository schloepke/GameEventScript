// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;
using static GameEventScript.Api.GesQuantityUnit;

namespace GameEventScript.Runtime.Vm;

[SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
internal partial class GesVirtualMachine
{
    private void ExecuteWrapDegree(ushort destinationRegister, in GesValue a)
    {
        if (a.Kind is Nothing)
        {
            SetNothing(destinationRegister);
            return;
        }

        if (!a.IsNumeric || a.Unit.IsNumericUnit() && a.Unit != UnitDegree)
        {
            SetFloat(destinationRegister, double.NaN);
            return;
        }

        var number = a.AsNumeric;
        if (!double.IsFinite(number))
        {
            SetFloat(destinationRegister, double.NaN);
            return;
        }

        var wrapped = number % 360d;
        if (wrapped < 0d) wrapped += 360d;
        SetFloat(destinationRegister, wrapped == 360d ? 0d : wrapped, UnitDegree);
    }
}
