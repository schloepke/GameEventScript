// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteCreateRangeIteratorWithStep(ushort destinationRegister, in GesValue from, in GesValue to, in GesValue step, GesContext context)
    {
        if (from.Kind is Integer && to.Kind is Integer && step.Kind is Integer)
        {
            if (!context.RuntimeBudget.CheckRangeLengthWithinLimit(GesRangeMath.GetLength(from.IntegerValue, to.IntegerValue, step.IntegerValue), "For loop range would enumerate more range items than allowed."))
            {
                SetIterator(destinationRegister, new GesIntegerRangeIterator(0, 0, 0));
                return;
            }

            SetIterator(destinationRegister, new GesIntegerRangeIterator(from.IntegerValue, to.IntegerValue, step.IntegerValue));
        }
        else if (from.IsNumeric && to.IsNumeric && step.IsNumeric)
        {
            if (!context.RuntimeBudget.CheckRangeLengthWithinLimit(GesRangeMath.GetLength(from.AsNumeric, to.AsNumeric, step.AsNumeric), "For loop range would enumerate more range items than allowed."))
            {
                SetIterator(destinationRegister, new GesIntegerRangeIterator(0, 0, 0));
                return;
            }

            SetIterator(destinationRegister, new GesFloatRangeIterator(from.AsNumeric, to.AsNumeric, step.AsNumeric));
        }
        else
        {
            SetNothing(destinationRegister);
        }
    }
}
