using System;
using System.Runtime.CompilerServices;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.BytecodeExecutor.VmRegisterUnitCalculation;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterArithmetic
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmAdd(ref this VmRegister dst, ref VmRegister a, ref VmRegister b)
    {
        if (!TryReadNumber(ref a, out var left) || !TryReadNumber(ref b, out var right) || !TrySameUnit(ref a, ref b, out var unit))
        {
            dst.SetNothing();
            return;
        }

        dst.SetFloat(left + right, unit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmSubtract(ref this VmRegister dst, ref VmRegister a, ref VmRegister b)
    {
        if (!TryReadNumber(ref a, out var left) || !TryReadNumber(ref b, out var right) || !TrySameUnit(ref a, ref b, out var unit))
        {
            dst.SetNothing();
            return;
        }

        dst.SetFloat(left - right, unit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmMultiply(ref this VmRegister dst, ref VmRegister a, ref VmRegister b)
    {
        if (!TryReadNumber(ref a, out var left) || !TryReadNumber(ref b, out var right) || !TryProductUnit(ref a, ref b, out var unit))
        {
            dst.SetNothing();
            return;
        }

        dst.SetFloat(left * right, unit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmDivide(ref this VmRegister dst, ref VmRegister a, ref VmRegister b)
    {
        if (!TryReadNumber(ref a, out var left) || !TryReadNumber(ref b, out var right) || right == 0.0 || !TryQuotientUnit(ref a, ref b, out var unit))
        {
            dst.SetNothing();
            return;
        }

        dst.SetFloat(left / right, unit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmPower(ref this VmRegister dst, ref VmRegister a, ref VmRegister b)
    {
        if (!TryReadNumber(ref a, out var left) || !TryReadNumber(ref b, out var right) || b.HasUnit || !TryPowerUnit(ref a, right, out var unit))
        {
            dst.SetNothing();
            return;
        }

        dst.SetFloat(Math.Pow(left, right), unit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmModulo(ref this VmRegister dst, ref VmRegister a, ref VmRegister b)
    {
        if (!TryReadNumber(ref a, out var left) || !TryReadNumber(ref b, out var right) || right == 0.0 || !TrySameUnit(ref a, ref b, out var unit))
        {
            dst.SetNothing();
            return;
        }

        dst.SetFloat(left % right, unit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmRemainder(ref this VmRegister dst, ref VmRegister a, ref VmRegister b)
    {
        dst.VmModulo(ref a, ref b);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmNegate(ref this VmRegister dst, ref VmRegister a)
    {
        if (a.Kind is VmRegister.VmValueKind.Integer) dst.SetInteger(-a.AsIntegerValue, a.Unit);
        else if (a.Kind is VmRegister.VmValueKind.Float or VmRegister.VmValueKind.Integer) dst.SetFloat(-a.AsFloatValue, a.Unit);
        else dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryReadNumber(ref VmRegister value, out double number)
    {
        if (value.Kind is VmRegister.VmValueKind.Float or VmRegister.VmValueKind.Integer)
        {
            number = value.AsFloatValue;
            return true;
        }
        number = 0.0;
        return false;
    }
    
}