using System.Runtime.CompilerServices;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterIntegerCompare
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmIntegerEqual(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.Kind != Integer || b.Kind != Integer)
        {
            dst.SetNothing();
            return;
        }

        dst.SetBoolean(a.Unit == b.Unit && a.IntegerValue == b.IntegerValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmIntegerNotEqual(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.Kind != Integer || b.Kind != Integer)
        {
            dst.SetNothing();
            return;
        }

        dst.SetBoolean(a.Unit != b.Unit || a.IntegerValue != b.IntegerValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmIntegerLess(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.Kind != Integer || b.Kind != Integer || a.Unit != b.Unit)
        {
            dst.SetNothing();
            return;
        }

        dst.SetBoolean(a.IntegerValue < b.IntegerValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmIntegerGreater(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.Kind != Integer || b.Kind != Integer || a.Unit != b.Unit)
        {
            dst.SetNothing();
            return;
        }

        dst.SetBoolean(a.IntegerValue > b.IntegerValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmIntegerLessOrEqual(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.Kind != Integer || b.Kind != Integer || a.Unit != b.Unit)
        {
            dst.SetNothing();
            return;
        }

        dst.SetBoolean(a.IntegerValue <= b.IntegerValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmIntegerGreaterOrEqual(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.Kind != Integer || b.Kind != Integer || a.Unit != b.Unit)
        {
            dst.SetNothing();
            return;
        }

        dst.SetBoolean(a.IntegerValue >= b.IntegerValue);
    }
}
