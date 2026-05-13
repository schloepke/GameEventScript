#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Runtime.CompilerServices;
using static StepH.GameEventScript.BytecodeExecutor.VmRegister.VmValueKind;

namespace StepH.GameEventScript.BytecodeExecutor;

public static class VmRegisterCompare
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool? VmEqual(ref this VmRegister a, ref VmRegister b)
    {
        if (a.IsNothing || b.IsNothing) return null;
        return a.Kind == b.Kind && (a.Kind == Float ? a.FloatValue == b.FloatValue : a.IntegerValue == b.IntegerValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool? VmNotEqual(ref this VmRegister a, ref VmRegister b)
    {
        if (a.IsNothing || b.IsNothing) return null;
        return a.Kind != b.Kind || (a.Kind == Float ? a.FloatValue != b.FloatValue : a.IntegerValue != b.IntegerValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool? VmApproxEqual(ref this VmRegister a, ref VmRegister b)
    {
        if (a.IsNothing || b.IsNothing) return null;
        if (a.IsFloat || b.IsFloat)
        {
            var aFloat = a.AsFloatValue;
            var bFloat = b.AsFloatValue;
            return aFloat == bFloat || Math.Abs(aFloat - bFloat) <= Math.Max(Math.Abs(aFloat), Math.Abs(bFloat)) * 1e-12;
        }

        return VmEqual(ref a, ref b);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool? VmLess(ref this VmRegister a, ref VmRegister b)
    {
        if (a.IsNothing || b.IsNothing) return null;
        if (a.IsFloat && b.IsFloat) return a.AsFloatValue < b.AsFloatValue;
        if (a.IsFloat && b.IsInteger) return a.AsFloatValue < b.AsIntegerValue;
        if (a.IsInteger && b.IsInteger) return a.AsIntegerValue < b.AsIntegerValue;
        if (a.IsBoolean && b.IsBoolean) return a.IsFalse && b.IsTrue;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool? VmGreater(ref this VmRegister a, ref VmRegister b)
    {
        if (a.IsNothing || b.IsNothing) return null;
        if (a.IsFloat && b.IsFloat) return a.AsFloatValue > b.AsFloatValue;
        if (a.IsFloat && b.IsInteger) return a.AsFloatValue > b.AsIntegerValue;
        if (a.IsInteger && b.IsInteger) return a.AsIntegerValue > b.AsIntegerValue;
        if (a.IsBoolean && b.IsBoolean) return a.IsTrue && b.IsFalse;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool? VmLessOrEqual(ref this VmRegister a, ref VmRegister b)
    {
        if (a.IsNothing || b.IsNothing) return null;
        if (a.IsFloat && b.IsFloat) return a.AsFloatValue <= b.AsFloatValue;
        if (a.IsFloat && b.IsInteger) return a.AsFloatValue <= b.AsIntegerValue;
        if (a.IsInteger && b.IsInteger) return a.AsIntegerValue <= b.AsIntegerValue;
        if (a.IsBoolean && b.IsBoolean) return a.IsFalse && b.IsTrue || a.AsBooleanValue == b.AsBooleanValue;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool? VmGreaterOrEqual(ref this VmRegister a, ref VmRegister b)
    {
        if (a.IsNothing || b.IsNothing) return null;
        if (a.IsFloat && b.IsFloat) return a.AsFloatValue >= b.AsFloatValue;
        if (a.IsFloat && b.IsInteger) return a.AsFloatValue >= b.AsIntegerValue;
        if (a.IsInteger && b.IsInteger) return a.AsIntegerValue >= b.AsIntegerValue;
        if (a.IsBoolean && b.IsBoolean) return a.IsTrue && b.IsFalse || a.AsBooleanValue == b.AsBooleanValue;
        return false;
    }
}