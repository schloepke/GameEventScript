using System;
using System.Runtime.CompilerServices;
using static StepH.GameEventScript.BytecodeExecutor.VmRegisterUnitCalculation;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterMath
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmAdd(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (TrySameUnit(ref a, ref b, out var unit))
        {
            var left = a.AsNumberValue;
            if (!double.IsNaN(left))
            {
                var right = b.AsNumberValue;
                if (!double.IsNaN(right))
                {
                    dst.SetFloat(left + right, unit);
                    return;
                }
            }
        }

        dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmSubtract(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (TrySameUnit(ref a, ref b, out var unit))
        {
            var left = a.AsNumberValue;
            if (!double.IsNaN(left))
            {
                var right = b.AsNumberValue;
                if (!double.IsNaN(right))
                {
                    dst.SetFloat(left - right, unit);
                    return;
                }
            }
        }

        dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmMultiply(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (TryProductUnit(ref a, ref b, out var unit))
        {
            var left = a.AsNumberValue;
            if (!double.IsNaN(left))
            {
                var right = b.AsNumberValue;
                if (!double.IsNaN(right))
                {
                    dst.SetFloat(left * right, unit);
                    return;
                }
            }
        }

        dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmDivide(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (TryQuotientUnit(ref a, ref b, out var unit))
        {
            var left = a.AsNumberValue;
            if (!double.IsNaN(left))
            {
                var right = b.AsNumberValue;
                if (!double.IsNaN(right) && right != 0.0)
                {
                    dst.SetFloat(left / right, unit);
                    return;
                }
            }
        }

        dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmPower(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        var right = b.AsNumberValue;
        if (!double.IsNaN(right))
        {
            if (TryPowerUnit(ref a, right, out var unit))
            {
                var left = a.AsNumberValue;
                if (!double.IsNaN(left))
                {
                    dst.SetFloat(Math.Pow(left, right), unit);
                    return;
                }
            }
        }

        dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmModulo(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (TrySameUnit(ref a, ref b, out var unit))
        {
            var left = a.AsNumberValue;
            if (!double.IsNaN(left))
            {
                var right = b.AsNumberValue;
                if (!double.IsNaN(right))
                {
                    dst.SetFloat(left - right * Math.Floor(left / right), unit);
                    return;
                }
            }
        }

        dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmRemainder(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (TrySameUnit(ref a, ref b, out var unit))
        {
            var left = a.AsNumberValue;
            if (!double.IsNaN(left))
            {
                var right = b.AsNumberValue;
                if (!double.IsNaN(right))
                {
                    dst.SetFloat(left - right * Math.Truncate(left / right), unit);
                    return;
                }
            }
        }

        dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmNegate(ref this VmValue dst, ref VmValue a)
    {
        switch (a.Kind)
        {
            case VmValue.VmValueKind.Integer:
                dst.SetInteger(-a.AsIntegerValue, a.Unit);
                break;
            case VmValue.VmValueKind.Float:
                dst.SetFloat(-a.AsFloatValue, a.Unit);
                break;
            default:
                dst.SetNothing();
                break;
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmAbs(ref this VmValue dst, ref VmValue a)
    {
        switch (a.Kind)
        {
            case VmValue.VmValueKind.Integer:
                dst.SetInteger(Math.Abs(a.AsIntegerValue), a.Unit);
                break;
            case VmValue.VmValueKind.Float or VmValue.VmValueKind.Percentage:
                dst.SetFloat(Math.Abs(a.AsFloatValue), a.Unit);
                break;
            default:
                dst.SetNothing();
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VmNaturalLog(ref this VmValue dst, ref VmValue a)
    {
        if(a.Kind is VmValue.VmValueKind.Nothing)
        {
            dst.SetNothing();
        }
        else if (a.HasUnit)
        {
            dst.SetFloat(double.NaN);
        }
        else
        {
            var x = a.AsNumberValue;
            if (double.IsNaN(x) || x < 0d || double.IsNegativeInfinity(x)) dst.SetFloat (double.NaN);
            else if (double.IsPositiveInfinity(x)) dst.SetFloat(double.PositiveInfinity);
            else dst.SetFloat(Math.Log(x));
        }
    }

}