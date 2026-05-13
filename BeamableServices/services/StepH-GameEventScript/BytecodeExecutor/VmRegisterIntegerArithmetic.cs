#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Runtime.CompilerServices;
using static StepH.GameEventScript.BytecodeExecutor.VmRegister.VmValueKind;

namespace StepH.GameEventScript.BytecodeExecutor;

public static class VmRegisterIntegerArithmetic
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long? VmIntegerAdd(ref this VmRegister a, ref VmRegister b)
    {
        if (a.Kind != Integer || b.Kind != Integer) return null;
        return a.IntegerValue + b.IntegerValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long? VmIntegerSubtract(ref this VmRegister a, ref VmRegister b)
    {
        if (a.Kind != Integer || b.Kind != Integer) return null;
        return a.IntegerValue - b.IntegerValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long? VmIntegerMultiply(ref this VmRegister a, ref VmRegister b)
    {
        if (a.Kind != Integer || b.Kind != Integer) return null;
        return a.IntegerValue * b.IntegerValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long? VmIntegerDivide(ref this VmRegister a, ref VmRegister b)
    {
        if (a.Kind != Integer || b.Kind != Integer) return null;
        return a.IntegerValue / b.IntegerValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long? VmIntegerFloorDivide(ref this VmRegister a, ref VmRegister b)
    {
        if (a.Kind != Integer || b.Kind != Integer) return null;
        // FIXME: Needs correct implementation
        return a.IntegerValue / b.IntegerValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long? VmIntegerModulo(ref this VmRegister a, ref VmRegister b)
    {
        if (a.Kind != Integer || b.Kind != Integer) return null;
        return a.IntegerValue % b.IntegerValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long? VmIntegerRemainder(ref this VmRegister a, ref VmRegister b)
    {
        if (a.Kind != Integer || b.Kind != Integer) return null;
        // FIXME: Needs correct implementation
        return a.IntegerValue % b.IntegerValue;
    }
}