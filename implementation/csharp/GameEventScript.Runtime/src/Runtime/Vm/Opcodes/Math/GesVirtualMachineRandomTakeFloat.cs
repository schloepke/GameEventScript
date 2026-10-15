// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;
using static GameEventScript.Runtime.Vm.GesVmUnitCalculation;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteRandomTakeFloat(ushort destinationRegister, in GesValue from, in GesValue to, GesRandomGenerator randomGenerator)
    {
        if (from.Kind is Nothing || to.Kind is Nothing)
        {
            SetNothing(destinationRegister);
            return;
        }

        if (SameUnit(in from, in to) is not { } unit)
        {
            SetFloat(destinationRegister, double.NaN);
            return;
        }

        var left = from.AsNumeric;
        var right = to.AsNumeric;
        if (double.IsFinite(left) && double.IsFinite(right)) SetFloat(destinationRegister, randomGenerator.NextFloat(left, right), unit);
        else SetFloat(destinationRegister, double.NaN);
    }
}
