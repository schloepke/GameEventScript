using System;
using System.Runtime.CompilerServices;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterArithmetic
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double? VmAdd(ref this VmRegister a, ref VmRegister b)
    {
        if (a.IsNothing || b.IsNothing) return null;
        if (a.IsInteger && b.IsInteger) return a.AsIntegerValue + b.AsIntegerValue;
        if (a.IsFloat && b.IsFloat) return a.AsFloatValue + b.AsFloatValue;
        if (a.IsFloat && b.IsInteger) return a.AsFloatValue + b.AsIntegerValue;
        if (a.IsInteger && b.IsFloat) return a.AsIntegerValue + b.AsFloatValue;
        // FIXME We need coercing to number for string here
        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double? VmSubtract(ref this VmRegister a, ref VmRegister b)
    {
        if (a.IsNothing || b.IsNothing) return null;
        if (a.IsInteger && b.IsInteger) return a.AsIntegerValue - b.AsIntegerValue;
        if (a.IsFloat && b.IsFloat) return a.AsFloatValue - b.AsFloatValue;
        if (a.IsFloat && b.IsInteger) return a.AsFloatValue - b.AsIntegerValue;
        if (a.IsInteger && b.IsFloat) return a.AsIntegerValue - b.AsFloatValue;
        // FIXME We need coercing to number for string here
        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double? VmMultiply(ref this VmRegister a, ref VmRegister b)
    {
        // FIXME: Needs correct implementation
        throw new NotImplementedException();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double? VmDivide(ref this VmRegister a, ref VmRegister b)
    {
        // FIXME: Needs correct implementation
        throw new NotImplementedException();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double? VmPower(ref this VmRegister a, ref VmRegister b)
    {
        // FIXME: Needs correct implementation
        throw new NotImplementedException();
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double? VmModulo(ref this VmRegister a, ref VmRegister b)
    {
        // FIXME: Needs correct implementation
        throw new NotImplementedException();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double? VmRemainder(ref this VmRegister a, ref VmRegister b)
    {
        // FIXME: Needs correct implementation
        throw new NotImplementedException();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double? VmNegate(ref this VmRegister a)
    {
        // FIXME: Needs correct implementation
        throw new NotImplementedException();
    }

}