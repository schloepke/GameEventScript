// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

[SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
internal partial class GesVirtualMachine
{
    private void ExecuteRoundHalfDown(ushort destinationRegister, in GesValue a)
    {
        if (a.Kind is Nothing)
        {
            SetNothing(destinationRegister);
            return;
        }

        if (!a.IsNumeric)
        {
            SetFloat(destinationRegister, double.NaN);
            return;
        }

        var value = a.AsNumeric;
        // Rounding keeps the quantity unit; percentages and Booleans have none.
        SetInteger(destinationRegister, ToIntegerSaturated(GesNumber.RoundHalfTowardZero(value)), a.Unit);
    }
}
