using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.BytecodeExecutor;

[SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
internal static class VmRegisterCompare
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmEqual(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.Kind is Nothing || b.Kind is Nothing)
        {
            dst.SetNothing();
        }
        else if (a.Unit != b.Unit)
        {
            dst.SetBoolean(false);
        }
        else
        {
            dst.SetBoolean(a.Kind == b.Kind && (a.Kind == Float ? a.FloatValue == b.FloatValue : a.IntegerValue == b.IntegerValue));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmNotEqual(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.Kind is Nothing || b.Kind is Nothing)
        {
            dst.SetNothing();
        }
        else if (a.Unit != b.Unit)
        {
            dst.SetBoolean(true);
        }
        else
        {
            dst.SetBoolean(a.Kind != b.Kind || (a.Kind == Float ? a.FloatValue != b.FloatValue : a.IntegerValue != b.IntegerValue));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmApproxEqual(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.Kind is Nothing || b.Kind is Nothing)
        {
            dst.SetNothing();
        }
        else if (a.Unit != b.Unit)
        {
            dst.SetBoolean(false);
        }
        else if (a.Kind is Float or Integer || b.Kind is Float or Integer)
        {
            var aFloat = a.AsNumeric;
            var bFloat = b.AsNumeric;
            dst.SetBoolean(aFloat == bFloat || Math.Abs(aFloat - bFloat) <= Math.Max(Math.Abs(aFloat), Math.Abs(bFloat)) * 1e-12);
        }
        else
        {
            dst.VmEqual(ref a, ref b);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmLess(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.Kind is Nothing || b.Kind is Nothing || a.Unit != b.Unit)
        {
            dst.SetNothing();
        }
        else if (a.Kind is Float or Integer && b.Kind is Float or Integer)
        {
            dst.SetBoolean(a.AsNumeric < b.AsNumeric);
        }
        else if (a.Kind is GameEventScriptBytecodeTypeKind.Boolean && b.Kind is GameEventScriptBytecodeTypeKind.Boolean)
        {
            dst.SetBoolean(a.IsFalse && b.IsTrue);
        }
        else
        {
            dst.SetBoolean(false);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmGreater(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.Kind is Nothing || b.Kind is Nothing || a.Unit != b.Unit)
        {
            dst.SetNothing();
        }
        else if (a.Kind is Float or Integer && b.Kind is Float or Integer)
        {
            dst.SetBoolean(a.AsNumeric > b.AsNumeric);
        }
        else if (a.Kind is GameEventScriptBytecodeTypeKind.Boolean && b.Kind is GameEventScriptBytecodeTypeKind.Boolean)
        {
            dst.SetBoolean(a.IsTrue && b.IsFalse);
        }
        else
        {
            dst.SetBoolean(false);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmLessOrEqual(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.Kind is Nothing || b.Kind is Nothing || a.Unit != b.Unit)
        {
            dst.SetNothing();
        }
        else if (a.Kind is Float or Integer && b.Kind is Float or Integer)
        {
            dst.SetBoolean(a.AsNumeric <= b.AsNumeric);
        }
        else if (a.Kind is GameEventScriptBytecodeTypeKind.Boolean && b.Kind is GameEventScriptBytecodeTypeKind.Boolean)
        {
            dst.SetBoolean(a.IsFalse && b.IsTrue || a.IsTrue == b.IsTrue);
        }
        else
        {
            dst.SetBoolean(false);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmGreaterOrEqual(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.Kind is Nothing || b.Kind is Nothing || a.Unit != b.Unit)
        {
            dst.SetNothing();
        }
        else if (a.Kind is Float or Integer && b.Kind is Float or Integer)
        {
            dst.SetBoolean(a.AsNumeric >= b.AsNumeric);
        }
        else if (a.Kind is GameEventScriptBytecodeTypeKind.Boolean && b.Kind is GameEventScriptBytecodeTypeKind.Boolean)
        {
            dst.SetBoolean(a.IsTrue && b.IsFalse || a.IsTrue == b.IsTrue);
        }
        else
        {
            dst.SetBoolean(false);
        }
    }
}
