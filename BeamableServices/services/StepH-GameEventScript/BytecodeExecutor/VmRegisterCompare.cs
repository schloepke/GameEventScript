using System;
using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.BytecodeExecutor.VmValue.VmValueKind;
using static StepH.GameEventScript.BytecodeExecutor.VmRegisterUnitCalculation;
using static StepH.GameEventScript.BytecodeExecutor.VmValue;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterCompare
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmEmpty(ref this VmValue dst, ref VmValue a, ref GameEventScriptTextTable textTable)
    {
        switch (a.Kind)
        {
            case Nothing:
                dst.SetBoolean(true);
                break;
            case Integer or Float or Percentage or VmValueKind.Boolean:
                dst.SetBoolean(false);
                break;
            case Text or Tag when a.IsStoragePointer():
                dst.SetBoolean(textTable.Resolve((ushort)dst.IntegerValue).Length == 0);
                break;
            case Text or Tag when a.IsStorageObject() && a.ObjectValue is string text :
                dst.SetBoolean(text.Length == 0);
                break;
            case List or Map or Dice when a.ObjectValue is IVmLengthAccess objectValue:
                dst.SetBoolean(objectValue.Length == 0);
                break;
            // Fixme: Special handling for series, uuid, ref, external type
            default:
                dst.SetBoolean(true);
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmHasValue(ref this VmValue dst, ref VmValue a, ref GameEventScriptTextTable textTable)
    {
        switch (a.Kind)
        {
            case Nothing:
                dst.SetBoolean(false);
                break;
            case Integer or VmValueKind.Boolean:
                dst.SetBoolean(true);
                break;
            case Float or Percentage:
                dst.SetBoolean(!double.IsNaN(dst.AsFloatValue) || !double.IsInfinity(dst.AsFloatValue) || !double.IsNegativeInfinity(dst.AsFloatValue));
                break;
            case Text or Tag when a.IsStoragePointer():
                dst.SetBoolean(textTable.Resolve((ushort)dst.IntegerValue).Length > 0);
                break;
            case Text or Tag when a.IsStorageObject() && a.ObjectValue is string text:
                dst.SetBoolean(text.Length > 0);
                break;
            case Text or List or Map or  Tag or Dice when a.ObjectValue is IVmLengthAccess objectValue:
                dst.SetBoolean(objectValue.Length > 0);
                break;
            // Fixme: Special handling for series, uuid, ref, external type
            default:
                dst.SetBoolean(false);
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmEqual(ref this VmValue dst, ref VmValue a, ref VmValue b)
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
    internal static void VmNotEqual(ref this VmValue dst, ref VmValue a, ref VmValue b)
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
    internal static void VmApproxEqual(ref this VmValue dst, ref VmValue a, ref VmValue b)
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
    internal static void VmLess(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.Kind is Nothing || b.Kind is Nothing || !HasSameUnit(ref a, ref b))
        {
            dst.SetNothing();
        }
        else if (a.Kind is Float or Integer && b.Kind is Float or Integer)
        {
            dst.SetBoolean(a.AsFloatValue < b.AsFloatValue);
        }
        else if (a.Kind is VmValueKind.Boolean && b.Kind is VmValueKind.Boolean)
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
        if (a.Kind is Nothing || b.Kind is Nothing || !HasSameUnit(ref a, ref b))
        {
            dst.SetNothing();
        }
        else if (a.Kind is Float or Integer && b.Kind is Float or Integer)
        {
            dst.SetBoolean(a.AsFloatValue > b.AsFloatValue);
        }
        else if (a.Kind is VmValueKind.Boolean && b.Kind is VmValueKind.Boolean)
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
        if (a.Kind is Nothing || b.Kind is Nothing || !HasSameUnit(ref a, ref b))
        {
            dst.SetNothing();
        }
        else if (a.Kind is Float or Integer && b.Kind is Float or Integer)
        {
            dst.SetBoolean(a.AsFloatValue <= b.AsFloatValue);
        }
        else if (a.Kind is VmValueKind.Boolean && b.Kind is VmValueKind.Boolean)
        {
            dst.SetBoolean(a.IsFalse && b.IsTrue || a.AsBooleanValue == b.AsBooleanValue);
        }
        else
        {
            dst.SetBoolean(false);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmGreaterOrEqual(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.Kind is Nothing || b.Kind is Nothing || !HasSameUnit(ref a, ref b))
        {
            dst.SetNothing();
        }
        else if (a.Kind is Float or Integer && b.Kind is Float or Integer)
        {
            dst.SetBoolean(a.AsFloatValue >= b.AsFloatValue);
        }
        else if (a.Kind is VmValueKind.Boolean && b.Kind is VmValueKind.Boolean)
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