// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteCreateRangeIterator(ushort destinationRegister, in GesValue from, in GesValue to, GesContext context)
    {
        if (from.Kind is Integer && to.Kind is Integer)
        {
            if (!context.RuntimeBudget.CheckRangeLengthWithinLimit(GesRangeMath.GetLength(from.IntegerValue, to.IntegerValue, 1), "For loop range would enumerate more range items than allowed."))
            {
                SetIterator(destinationRegister, new GesIntegerRangeIterator(0, 0, 0));
                return;
            }

            SetIterator(destinationRegister, new GesIntegerRangeIterator(from.IntegerValue, to.IntegerValue, 1));
        }
        else if (from.IsNumeric && to.IsNumeric)
        {
            if (!context.RuntimeBudget.CheckRangeLengthWithinLimit(GesRangeMath.GetLength(from.AsNumeric, to.AsNumeric, 1.0d), "For loop range would enumerate more range items than allowed."))
            {
                SetIterator(destinationRegister, new GesIntegerRangeIterator(0, 0, 0));
                return;
            }

            SetIterator(destinationRegister, new GesFloatRangeIterator(from.AsNumeric, to.AsNumeric, 1.0d));
        }
        else
        {
            SetNothing(destinationRegister);
        }
    }
}
