#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Runtime.CompilerServices;
using static StepH.GameEventScript.BytecodeExecutor.VmRegister.VmValueKind;

namespace StepH.GameEventScript.BytecodeExecutor;

public static class VmRegisterIntegerCompare
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmIntegerEqual(ref this VmRegister dst, ref VmRegister a, ref VmRegister b)
    {
        if (a.Kind != Integer || b.Kind != Integer)
        {
            dst.SetNothing();
            return;
        }

        dst.SetBoolean(a.Unit == b.Unit && a.IntegerValue == b.IntegerValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmIntegerNotEqual(ref this VmRegister dst, ref VmRegister a, ref VmRegister b)
    {
        if (a.Kind != Integer || b.Kind != Integer)
        {
            dst.SetNothing();
            return;
        }

        dst.SetBoolean(a.Unit != b.Unit || a.IntegerValue != b.IntegerValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmIntegerLess(ref this VmRegister dst, ref VmRegister a, ref VmRegister b)
    {
        if (a.Kind != Integer || b.Kind != Integer || a.Unit != b.Unit)
        {
            dst.SetNothing();
            return;
        }

        dst.SetBoolean(a.IntegerValue < b.IntegerValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmIntegerGreater(ref this VmRegister dst, ref VmRegister a, ref VmRegister b)
    {
        if (a.Kind != Integer || b.Kind != Integer || a.Unit != b.Unit)
        {
            dst.SetNothing();
            return;
        }

        dst.SetBoolean(a.IntegerValue > b.IntegerValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmIntegerLessOrEqual(ref this VmRegister dst, ref VmRegister a, ref VmRegister b)
    {
        if (a.Kind != Integer || b.Kind != Integer || a.Unit != b.Unit)
        {
            dst.SetNothing();
            return;
        }

        dst.SetBoolean(a.IntegerValue <= b.IntegerValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmIntegerGreaterOrEqual(ref this VmRegister dst, ref VmRegister a, ref VmRegister b)
    {
        if (a.Kind != Integer || b.Kind != Integer || a.Unit != b.Unit)
        {
            dst.SetNothing();
            return;
        }

        dst.SetBoolean(a.IntegerValue >= b.IntegerValue);
    }
}
