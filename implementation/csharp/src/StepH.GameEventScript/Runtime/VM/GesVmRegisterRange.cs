// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Runtime.Values;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.Runtime.VM;

internal static class GesVmRegisterRange
{
    internal static void GesVmCreateRange(this GesVmState vmState, ushort destinationRegister, in GesValue from, in GesValue to)
    {
        if (from.Kind is Integer && to.Kind is Integer)
        {
            vmState.SetRange(destinationRegister, from.IntegerValue, to.IntegerValue, 1);
        }
        else if (from.IsNumeric && to.IsNumeric)
        {
            vmState.SetRange(destinationRegister, from.AsNumeric, to.AsNumeric, 1.0d);
        }
        else
        {
            vmState.SetNothing(destinationRegister);
        }
    }
    internal static void GesVmCreateRange(this GesVmState vmState, ushort destinationRegister, in GesValue from, in GesValue to, in GesValue step)
    {
        if (from.Kind is Integer && to.Kind is Integer && step.Kind is Integer)
        {
            vmState.SetRange(destinationRegister, from.IntegerValue, to.IntegerValue, step.IntegerValue);
        }
        else if (from.IsNumeric && to.IsNumeric && step.IsNumeric)
        {
            vmState.SetRange(destinationRegister, from.AsNumeric, to.AsNumeric, step.AsNumeric);
        }
        else
        {
            vmState.SetNothing(destinationRegister);
        }
    }
    internal static void GesVmCreateRangeIterator(this GesVmState vmState, ushort destinationRegister, in GesValue from, in GesValue to, GameEventScriptContext context)
    {
        if (from.Kind is Integer && to.Kind is Integer)
        {
            if (!context.RuntimeBudget.CheckRangeLengthWithinLimit(GameEventScriptRangeMath.GetLength(from.IntegerValue, to.IntegerValue, 1), "For loop range would enumerate more range items than allowed."))
            {
                vmState.SetIterator(destinationRegister, new GesIntegerRangeIterator(0, 0, 0));
                return;
            }

            vmState.SetIterator(destinationRegister, new GesIntegerRangeIterator(from.IntegerValue, to.IntegerValue, 1));
        }
        else if (from.IsNumeric && to.IsNumeric)
        {
            if (!context.RuntimeBudget.CheckRangeLengthWithinLimit(GameEventScriptRangeMath.GetLength(from.AsNumeric, to.AsNumeric, 1.0d), "For loop range would enumerate more range items than allowed."))
            {
                vmState.SetIterator(destinationRegister, new GesIntegerRangeIterator(0, 0, 0));
                return;
            }

            vmState.SetIterator(destinationRegister, new GesFloatRangeIterator(from.AsNumeric, to.AsNumeric, 1.0d));
        }
        else
        {
            vmState.SetNothing(destinationRegister);
        }
    }
    internal static void GesVmCreateRangeIterator(this GesVmState vmState, ushort destinationRegister, in GesValue from, in GesValue to, in GesValue step, GameEventScriptContext context)
    {
        if (from.Kind is Integer && to.Kind is Integer && step.Kind is Integer)
        {
            if (!context.RuntimeBudget.CheckRangeLengthWithinLimit(GameEventScriptRangeMath.GetLength(from.IntegerValue, to.IntegerValue, step.IntegerValue), "For loop range would enumerate more range items than allowed."))
            {
                vmState.SetIterator(destinationRegister, new GesIntegerRangeIterator(0, 0, 0));
                return;
            }

            vmState.SetIterator(destinationRegister, new GesIntegerRangeIterator(from.IntegerValue, to.IntegerValue, step.IntegerValue));
        }
        else if (from.IsNumeric && to.IsNumeric && step.IsNumeric)
        {
            if (!context.RuntimeBudget.CheckRangeLengthWithinLimit(GameEventScriptRangeMath.GetLength(from.AsNumeric, to.AsNumeric, step.AsNumeric), "For loop range would enumerate more range items than allowed."))
            {
                vmState.SetIterator(destinationRegister, new GesIntegerRangeIterator(0, 0, 0));
                return;
            }

            vmState.SetIterator(destinationRegister, new GesFloatRangeIterator(from.AsNumeric, to.AsNumeric, step.AsNumeric));
        }
        else
        {
            vmState.SetNothing(destinationRegister);
        }
    }
}
