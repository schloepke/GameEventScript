#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Runtime.CompilerServices;

namespace StepH.GameEventScript.BytecodeExecutor;

public static class VmRegisterBooleanLogic
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool? VmOr(ref this VmRegister a, ref VmRegister b)
    {
        if(a.IsNothing || b.IsNothing) return null;
        return a.IsTrue || b.IsTrue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool? VmAnd(ref this VmRegister a, ref VmRegister b)
    {
        if(a.IsNothing || b.IsNothing) return null;
        return a.IsTrue && b.IsTrue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool? VmXor(ref this VmRegister a, ref VmRegister b)
    {
        if(a.IsNothing || b.IsNothing) return null;
        return a.IsTrue ^ b.IsTrue;
    }

}