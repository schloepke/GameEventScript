#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Runtime.CompilerServices;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeInstructionUnit;
using static StepH.GameEventScript.BytecodeExecutor.VmValue.VmValueKind;
using static StepH.GameEventScript.BytecodeExecutor.VmRegisterUnitCalculation;

namespace StepH.GameEventScript.BytecodeExecutor;

public static class VmRegisterIntegerMath
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmIntegerAdd(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.Kind != Integer || b.Kind != Integer || a.Unit != b.Unit) dst.SetNothing();
        else dst.SetInteger(a.IntegerValue + b.IntegerValue, a.Unit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmIntegerSubtract(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.Kind != Integer || b.Kind != Integer || a.Unit != b.Unit) dst.SetNothing();
        else dst.SetInteger(a.IntegerValue - b.IntegerValue, a.Unit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmIntegerMultiply(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.Kind != Integer || b.Kind != Integer || (a.HasUnit && b.HasUnit)) dst.SetNothing();
        else dst.SetInteger(a.IntegerValue * b.IntegerValue, a.Unit is UnitNone ? b.Unit : a.Unit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmIntegerDivide(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.Kind != Integer || b.Kind != Integer || b.IntegerValue == 0 || !TryQuotientUnit(ref a, ref b, out var unit)) dst.SetNothing();
        else dst.SetInteger(a.IntegerValue / b.IntegerValue, unit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmIntegerFloorDivide(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        dst.VmIntegerDivide(ref a, ref b);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmIntegerModulo(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.Kind != Integer || b.Kind != Integer || b.IntegerValue == 0 || a.Unit != b.Unit) dst.SetNothing();
        else dst.SetInteger(a.IntegerValue % b.IntegerValue, a.Unit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmIntegerRemainder(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        dst.VmIntegerModulo(ref a, ref b);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmIntegerNegate(ref this VmValue dst, ref VmValue a)
    {
        if (a.Kind != Integer) dst.SetNothing();
        else dst.SetInteger(-a.IntegerValue, a.Unit);
    }

}