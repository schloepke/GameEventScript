using System.Runtime.CompilerServices;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeInstructionUnit;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;
using static StepH.GameEventScript.BytecodeExecutor.VmRegisterUnitCalculation;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterIntegerMath
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmIntegerAdd(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.Kind != Integer || b.Kind != Integer || a.Unit != b.Unit) dst.SetNothing();
        else dst.SetInteger(a.IntegerValue + b.IntegerValue, a.Unit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmIntegerSubtract(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.Kind != Integer || b.Kind != Integer || a.Unit != b.Unit) dst.SetNothing();
        else dst.SetInteger(a.IntegerValue - b.IntegerValue, a.Unit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmIntegerMultiply(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.Kind != Integer || b.Kind != Integer || (a.HasUnit && b.HasUnit)) dst.SetNothing();
        else dst.SetInteger(a.IntegerValue * b.IntegerValue, a.Unit is UnitNone ? b.Unit : a.Unit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmIntegerDivide(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.Kind != Integer || b.Kind != Integer || b.IntegerValue == 0 || !TryQuotientUnit(ref a, ref b, out var unit)) dst.SetNothing();
        else dst.SetInteger(a.IntegerValue / b.IntegerValue, unit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmIntegerFloorDivide(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        dst.VmIntegerDivide(ref a, ref b);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmIntegerModulo(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.Kind != Integer || b.Kind != Integer || b.IntegerValue == 0 || a.Unit != b.Unit) dst.SetNothing();
        else dst.SetInteger(a.IntegerValue % b.IntegerValue, a.Unit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmIntegerRemainder(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        dst.VmIntegerModulo(ref a, ref b);
    }
    
}