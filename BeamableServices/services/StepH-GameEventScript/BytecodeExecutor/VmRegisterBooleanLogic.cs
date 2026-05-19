#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Runtime.CompilerServices;

namespace StepH.GameEventScript.BytecodeExecutor;

public static class VmRegisterBooleanLogic
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmOr(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        var x = a.BooleanValueOrNothing;
        var y = b.BooleanValueOrNothing;
        if (x.HasValue && y.HasValue)
        {
            dst.SetBoolean(x.Value || y.Value);
            return;
        }

        if ((x.HasValue && x.Value) || (y.HasValue && y.Value))
        {
            dst.SetBoolean(true);
            return;
        }

        dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmAnd(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        var x = a.BooleanValueOrNothing;
        var y = b.BooleanValueOrNothing;
        if (x.HasValue && y.HasValue)
        {
            dst.SetBoolean(x.Value && y.Value);
            return;
        }

        if ((x.HasValue && !x.Value) || (y.HasValue && !y.Value))
        {
            dst.SetBoolean(false);
            return;
        }

        dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmXor(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        var x = a.BooleanValueOrNothing;
        var y = b.BooleanValueOrNothing;
        if (x.HasValue && y.HasValue)
        {
            dst.SetBoolean(x.Value ^ y.Value);
            return;
        }

        dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmNot(ref this VmValue dst, ref VmValue a)
    {
        var x = a.BooleanValueOrNothing;
        if (x.HasValue)
        {
            dst.SetBoolean(!x.Value);
            return;
        }

        dst.SetNothing();
    }

    public static void VmChance(ref this VmValue dst, ref VmValue a, ref VmState state)
    {
        var x = a.AsNumberValue;
        switch (x)
        {
            case <= 0:
                dst.SetBoolean(false);
                break;
            case >= 1:
                dst.SetBoolean(true);
                break;
            case double.NaN:
                dst.SetNothing();
                break;
            default:
                dst.SetBoolean(state.RandomGenerator.NextInclusiveFloat(0, 1.0) < x);
                break;
        }
    }

}
