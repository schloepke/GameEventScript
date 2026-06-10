using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmRegisterRange
{
    internal static void VmCreateRange(ref this GesVmValue dst, ref GesVmValue from, ref GesVmValue to)
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
    internal static void VmCreateRange(ref this GesVmValue dst, ref GesVmValue from, ref GesVmValue to, ref GesVmValue step)
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
    internal static void VmCreateRangeStream(ref this GesVmValue dst, ref GesVmValue from, ref GesVmValue to, GameEventScriptSession session)
    {
        if (from.Kind is Integer && to.Kind is Integer)
        {
            if (!session.RuntimeBudget.TryCheckRangeLength(GameEventScriptRangeMath.GetLength(from.IntegerValue, to.IntegerValue, 1), "For loop range would enumerate more range items than allowed."))
            {
                dst.SetStream(new GesVmIntegerRangeStream(0, 0, 0));
                return;
            }

            dst.SetStream(new GesVmIntegerRangeStream(from.IntegerValue, to.IntegerValue, 1));
        }
        else if (from.IsNumeric && to.IsNumeric)
        {
            if (!session.RuntimeBudget.TryCheckRangeLength(GameEventScriptRangeMath.GetLength(from.AsNumeric, to.AsNumeric, 1.0d), "For loop range would enumerate more range items than allowed."))
            {
                dst.SetStream(new GesVmIntegerRangeStream(0, 0, 0));
                return;
            }

            dst.SetStream(new GesVmFloatRangeStream(from.AsNumeric, to.AsNumeric, 1.0d));
        }
        else
        {
            dst.SetNothing();
        }
    }
    internal static void VmCreateRangeStream(ref this GesVmValue dst, ref GesVmValue from, ref GesVmValue to, ref GesVmValue step, GameEventScriptSession session)
    {
        if (from.Kind is Integer && to.Kind is Integer && step.Kind is Integer)
        {
            if (!session.RuntimeBudget.TryCheckRangeLength(GameEventScriptRangeMath.GetLength(from.IntegerValue, to.IntegerValue, step.IntegerValue), "For loop range would enumerate more range items than allowed."))
            {
                dst.SetStream(new GesVmIntegerRangeStream(0, 0, 0));
                return;
            }

            dst.SetStream(new GesVmIntegerRangeStream(from.IntegerValue, to.IntegerValue, step.IntegerValue));
        }
        else if (from.IsNumeric && to.IsNumeric && step.IsNumeric)
        {
            if (!session.RuntimeBudget.TryCheckRangeLength(GameEventScriptRangeMath.GetLength(from.AsNumeric, to.AsNumeric, step.AsNumeric), "For loop range would enumerate more range items than allowed."))
            {
                dst.SetStream(new GesVmIntegerRangeStream(0, 0, 0));
                return;
            }

            dst.SetStream(new GesVmFloatRangeStream(from.AsNumeric, to.AsNumeric, step.AsNumeric));
        }
        else
        {
            dst.SetNothing();
        }
    }
}
