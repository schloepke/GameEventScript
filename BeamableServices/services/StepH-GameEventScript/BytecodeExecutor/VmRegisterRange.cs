using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterRange
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCreateRange(ref this VmValue dst, ref VmValue from, ref VmValue to)
    {
        if (from.Kind is Integer && to.Kind is Integer)
        {
            dst.SetRange(from.IntegerValue, to.IntegerValue, 1);
        }
        else if (from.IsNumeric && to.IsNumeric)
        {
            dst.SetRange(from.AsNumeric, to.AsNumeric, 1.0d);
        }
        else
        {
            dst.SetNothing();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCreateRange(ref this VmValue dst, ref VmValue from, ref VmValue to, ref VmValue step)
    {
        if (from.Kind is Integer && to.Kind is Integer && step.Kind is Integer)
        {
            dst.SetRange(from.IntegerValue, to.IntegerValue, step.IntegerValue);
        }
        else if (from.IsNumeric && to.IsNumeric && step.IsNumeric)
        {
            dst.SetRange(from.AsNumeric, to.AsNumeric, step.AsNumeric);
        }
        else
        {
            dst.SetNothing();
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCreateRangeStream(ref this VmValue dst, ref VmValue from, ref VmValue to, GameEventScriptSession session)
    {
        if (from.Kind is Integer && to.Kind is Integer)
        {
            if (!session.RuntimeBudget.TryCheckRangeLength(GameEventScriptRangeMath.GetLength(from.IntegerValue, to.IntegerValue, 1), "For loop range would enumerate more range items than allowed."))
            {
                dst.SetStream(new VmIntegerRangeStream(0, 0, 0));
                return;
            }

            dst.SetStream(new VmIntegerRangeStream(from.IntegerValue, to.IntegerValue, 1));
        }
        else if (from.IsNumeric && to.IsNumeric)
        {
            if (!session.RuntimeBudget.TryCheckRangeLength(GameEventScriptRangeMath.GetLength(from.AsNumeric, to.AsNumeric, 1.0d), "For loop range would enumerate more range items than allowed."))
            {
                dst.SetStream(new VmIntegerRangeStream(0, 0, 0));
                return;
            }

            dst.SetStream(new VmFloatRangeStream(from.AsNumeric, to.AsNumeric, 1.0d));
        }
        else
        {
            dst.SetNothing();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCreateRangeStream(ref this VmValue dst, ref VmValue from, ref VmValue to, ref VmValue step, GameEventScriptSession session)
    {
        if (from.Kind is Integer && to.Kind is Integer && step.Kind is Integer)
        {
            if (!session.RuntimeBudget.TryCheckRangeLength(GameEventScriptRangeMath.GetLength(from.IntegerValue, to.IntegerValue, step.IntegerValue), "For loop range would enumerate more range items than allowed."))
            {
                dst.SetStream(new VmIntegerRangeStream(0, 0, 0));
                return;
            }

            dst.SetStream(new VmIntegerRangeStream(from.IntegerValue, to.IntegerValue, step.IntegerValue));
        }
        else if (from.IsNumeric && to.IsNumeric && step.IsNumeric)
        {
            if (!session.RuntimeBudget.TryCheckRangeLength(GameEventScriptRangeMath.GetLength(from.AsNumeric, to.AsNumeric, step.AsNumeric), "For loop range would enumerate more range items than allowed."))
            {
                dst.SetStream(new VmIntegerRangeStream(0, 0, 0));
                return;
            }

            dst.SetStream(new VmFloatRangeStream(from.AsNumeric, to.AsNumeric, step.AsNumeric));
        }
        else
        {
            dst.SetNothing();
        }
    }
}
