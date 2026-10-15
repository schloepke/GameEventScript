// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal static class GesVmRegisterRange
{
    internal static void GesVmCreateRange(this GesVirtualMachine vm, ushort destinationRegister, in GesValue from, in GesValue to)
    {
        if (from.Kind is Integer && to.Kind is Integer)
        {
            vm.SetRange(destinationRegister, from.IntegerValue, to.IntegerValue, 1);
        }
        else if (from.IsNumeric && to.IsNumeric)
        {
            vm.SetRange(destinationRegister, from.AsNumeric, to.AsNumeric, 1.0d);
        }
        else
        {
            vm.SetNothing(destinationRegister);
        }
    }
    internal static void GesVmCreateRange(this GesVirtualMachine vm, ushort destinationRegister, in GesValue from, in GesValue to, in GesValue step)
    {
        if (from.Kind is Integer && to.Kind is Integer && step.Kind is Integer)
        {
            vm.SetRange(destinationRegister, from.IntegerValue, to.IntegerValue, step.IntegerValue);
        }
        else if (from.IsNumeric && to.IsNumeric && step.IsNumeric)
        {
            vm.SetRange(destinationRegister, from.AsNumeric, to.AsNumeric, step.AsNumeric);
        }
        else
        {
            vm.SetNothing(destinationRegister);
        }
    }
    internal static void GesVmCreateRangeIterator(this GesVirtualMachine vm, ushort destinationRegister, in GesValue from, in GesValue to, GesContext context)
    {
        if (from.Kind is Integer && to.Kind is Integer)
        {
            if (!context.RuntimeBudget.CheckRangeLengthWithinLimit(GameEventScriptRangeMath.GetLength(from.IntegerValue, to.IntegerValue, 1), "For loop range would enumerate more range items than allowed."))
            {
                vm.SetIterator(destinationRegister, new GesIntegerRangeIterator(0, 0, 0));
                return;
            }

            vm.SetIterator(destinationRegister, new GesIntegerRangeIterator(from.IntegerValue, to.IntegerValue, 1));
        }
        else if (from.IsNumeric && to.IsNumeric)
        {
            if (!context.RuntimeBudget.CheckRangeLengthWithinLimit(GameEventScriptRangeMath.GetLength(from.AsNumeric, to.AsNumeric, 1.0d), "For loop range would enumerate more range items than allowed."))
            {
                vm.SetIterator(destinationRegister, new GesIntegerRangeIterator(0, 0, 0));
                return;
            }

            vm.SetIterator(destinationRegister, new GesFloatRangeIterator(from.AsNumeric, to.AsNumeric, 1.0d));
        }
        else
        {
            vm.SetNothing(destinationRegister);
        }
    }
    internal static void GesVmCreateRangeIterator(this GesVirtualMachine vm, ushort destinationRegister, in GesValue from, in GesValue to, in GesValue step, GesContext context)
    {
        if (from.Kind is Integer && to.Kind is Integer && step.Kind is Integer)
        {
            if (!context.RuntimeBudget.CheckRangeLengthWithinLimit(GameEventScriptRangeMath.GetLength(from.IntegerValue, to.IntegerValue, step.IntegerValue), "For loop range would enumerate more range items than allowed."))
            {
                vm.SetIterator(destinationRegister, new GesIntegerRangeIterator(0, 0, 0));
                return;
            }

            vm.SetIterator(destinationRegister, new GesIntegerRangeIterator(from.IntegerValue, to.IntegerValue, step.IntegerValue));
        }
        else if (from.IsNumeric && to.IsNumeric && step.IsNumeric)
        {
            if (!context.RuntimeBudget.CheckRangeLengthWithinLimit(GameEventScriptRangeMath.GetLength(from.AsNumeric, to.AsNumeric, step.AsNumeric), "For loop range would enumerate more range items than allowed."))
            {
                vm.SetIterator(destinationRegister, new GesIntegerRangeIterator(0, 0, 0));
                return;
            }

            vm.SetIterator(destinationRegister, new GesFloatRangeIterator(from.AsNumeric, to.AsNumeric, step.AsNumeric));
        }
        else
        {
            vm.SetNothing(destinationRegister);
        }
    }
}
