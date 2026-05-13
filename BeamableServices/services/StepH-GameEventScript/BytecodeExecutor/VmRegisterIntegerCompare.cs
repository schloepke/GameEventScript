#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Runtime.CompilerServices;
using static StepH.GameEventScript.BytecodeExecutor.VmRegister.VmValueKind;

namespace StepH.GameEventScript.BytecodeExecutor;

public static class VmRegisterIntegerCompare
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool? VmIntegerEqual(ref this VmRegister a, ref VmRegister b)
    {
        if (a.Kind != Integer && b.Kind != Integer) return null;
        return a.IntegerValue == b.IntegerValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool? VmIntegerNotEqual(ref this VmRegister a, ref VmRegister b)
    {
        if (a.Kind != Integer && b.Kind != Integer) return null;
        return a.IntegerValue != b.IntegerValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool? VmIntegerLess(ref this VmRegister a, ref VmRegister b)
    {
        if (a.Kind != Integer && b.Kind != Integer) return null;
        return a.IntegerValue < b.IntegerValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool? VmIntegerGreater(ref this VmRegister a, ref VmRegister b)
    {
        if (a.Kind != Integer && b.Kind != Integer) return null;
        return a.IntegerValue > b.IntegerValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool? VmIntegerLessOrEqual(ref this VmRegister a, ref VmRegister b)
    {
        if (a.Kind != Integer && b.Kind != Integer) return null;
        return a.IntegerValue <= b.IntegerValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool? VmIntegerGreaterOrEqual(ref this VmRegister a, ref VmRegister b)
    {
        if (a.Kind != Integer && b.Kind != Integer) return null;
        return a.IntegerValue >= b.IntegerValue;
    }
}