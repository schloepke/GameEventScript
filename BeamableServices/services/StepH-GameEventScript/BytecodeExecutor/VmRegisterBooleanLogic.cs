#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Runtime.CompilerServices;

namespace StepH.GameEventScript.BytecodeExecutor;

public static class VmRegisterBooleanLogic
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool? VmOr(ref this VmRegister a, ref VmRegister b)
    {
        var x = a.BooleanValueOrNothing;
        var y = b.BooleanValueOrNothing;
        if (x.HasValue && y.HasValue) return x.Value || y.Value;
        if (x.HasValue && x.Value) return true;
        if (y.HasValue && y.Value) return true;
        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool? VmAnd(ref this VmRegister a, ref VmRegister b)
    {
        var x = a.BooleanValueOrNothing;
        var y = b.BooleanValueOrNothing;
        if (x.HasValue && y.HasValue) return x.Value && y.Value;
        if (x.HasValue && !x.Value) return false;
        if (y.HasValue && !y.Value) return false;
        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool? VmXor(ref this VmRegister a, ref VmRegister b)
    {
        var x = a.BooleanValueOrNothing;
        var y = b.BooleanValueOrNothing;
        if(x.HasValue && y.HasValue) return x.Value ^ y.Value;
        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool? VmNot(ref this VmRegister a)
    {
        return !a.BooleanValueOrNothing;
    }


}