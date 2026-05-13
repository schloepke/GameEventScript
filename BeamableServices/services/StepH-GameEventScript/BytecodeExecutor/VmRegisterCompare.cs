#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Runtime.CompilerServices;
using static StepH.GameEventScript.BytecodeExecutor.VmRegister.VmValueKind;
using static StepH.GameEventScript.BytecodeExecutor.VmRegisterUnitCalculation;

namespace StepH.GameEventScript.BytecodeExecutor;

public static class VmRegisterCompare
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmEqual(ref this VmRegister dst, ref VmRegister a, ref VmRegister b)
    {
        if (a.Kind is Nothing || b.Kind is Nothing)
        {
            dst.SetNothing();
        }
        else if (!HasSameUnit(ref a, ref b))
        {
            dst.SetBoolean(false);
        }
        else
        {
            dst.SetBoolean(a.Kind == b.Kind && (a.Kind == Float ? a.FloatValue == b.FloatValue : a.IntegerValue == b.IntegerValue));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmNotEqual(ref this VmRegister dst, ref VmRegister a, ref VmRegister b)
    {
        if (a.Kind is Nothing || b.Kind is Nothing)
        {
            dst.SetNothing();
        }
        else if (!HasSameUnit(ref a, ref b))
        {
            dst.SetBoolean(true);
        }
        else
        {
            dst.SetBoolean(a.Kind != b.Kind || (a.Kind == Float ? a.FloatValue != b.FloatValue : a.IntegerValue != b.IntegerValue));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmApproxEqual(ref this VmRegister dst, ref VmRegister a, ref VmRegister b)
    {
        if (a.Kind is Nothing || b.Kind is Nothing)
        {
            dst.SetNothing();
        }
        else if (!HasSameUnit(ref a, ref b))
        {
            dst.SetBoolean(false);
        }
        else if (a.Kind is Float or Integer || b.Kind is Float or Integer)
        {
            var aFloat = a.AsFloatValue;
            var bFloat = b.AsFloatValue;
            dst.SetBoolean(aFloat == bFloat || Math.Abs(aFloat - bFloat) <= Math.Max(Math.Abs(aFloat), Math.Abs(bFloat)) * 1e-12);
        }
        else
        {
            dst.VmEqual(ref a, ref b);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmLess(ref this VmRegister dst, ref VmRegister a, ref VmRegister b)
    {
        if (a.Kind is Nothing || b.Kind is Nothing || !HasSameUnit(ref a, ref b))
        {
            dst.SetNothing();
        }
        else if (a.Kind is Float or Integer && b.Kind is Float or Integer)
        {
            dst.SetBoolean(a.AsFloatValue < b.AsFloatValue);
        }
        else if (a.Kind is VmRegister.VmValueKind.Boolean && b.Kind is VmRegister.VmValueKind.Boolean)
        {
            dst.SetBoolean(a.IsFalse && b.IsTrue);
        }
        else
        {
            dst.SetBoolean(false);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmGreater(ref this VmRegister dst, ref VmRegister a, ref VmRegister b)
    {
        if (a.Kind is Nothing || b.Kind is Nothing || !HasSameUnit(ref a, ref b))
        {
            dst.SetNothing();
        }
        else if (a.Kind is Float or Integer && b.Kind is Float or Integer)
        {
            dst.SetBoolean(a.AsFloatValue > b.AsFloatValue);
        }
        else if (a.Kind is VmRegister.VmValueKind.Boolean && b.Kind is VmRegister.VmValueKind.Boolean)
        {
            dst.SetBoolean(a.IsTrue && b.IsFalse);
        }
        else
        {
            dst.SetBoolean(false);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmLessOrEqual(ref this VmRegister dst, ref VmRegister a, ref VmRegister b)
    {
        if (a.Kind is Nothing || b.Kind is Nothing || !HasSameUnit(ref a, ref b))
        {
            dst.SetNothing();
        }
        else if (a.Kind is Float or Integer && b.Kind is Float or Integer)
        {
            dst.SetBoolean(a.AsFloatValue <= b.AsFloatValue);
        }
        else if (a.Kind is VmRegister.VmValueKind.Boolean && b.Kind is VmRegister.VmValueKind.Boolean)
        {
            dst.SetBoolean(a.IsFalse && b.IsTrue || a.AsBooleanValue == b.AsBooleanValue);
        }
        else
        {
            dst.SetBoolean(false);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmGreaterOrEqual(ref this VmRegister dst, ref VmRegister a, ref VmRegister b)
    {
        if (a.Kind is Nothing || b.Kind is Nothing || !HasSameUnit(ref a, ref b))
        {
            dst.SetNothing();
        }
        else if (a.Kind is Float or Integer && b.Kind is Float or Integer)
        {
            dst.SetBoolean(a.AsFloatValue >= b.AsFloatValue);
        }
        else if (a.Kind is VmRegister.VmValueKind.Boolean && b.Kind is VmRegister.VmValueKind.Boolean)
        {
            dst.SetBoolean(a.IsTrue && b.IsFalse || a.AsBooleanValue == b.AsBooleanValue);
            return;
        }
        else
        {
            dst.SetBoolean(false);
        }
    }

}