using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmRegisterRange
{
    internal static void GesVmCreateRange(this GesVmState vmState, ushort destinationRegister, in GesVmValue from, in GesVmValue to)
    {
        if (from.Kind is Integer && to.Kind is Integer)
        {
            vmState.SetRange(destinationRegister, from.IntegerValue, to.IntegerValue, 1);
        }
        else if (from.IsNumeric && to.IsNumeric)
        {
            vmState.SetRange(destinationRegister, from.AsNumeric, to.AsNumeric, 1.0d);
        }
        else
        {
            vmState.SetNothing(destinationRegister);
        }
    }
    internal static void GesVmCreateRange(this GesVmState vmState, ushort destinationRegister, in GesVmValue from, in GesVmValue to, in GesVmValue step)
    {
        if (from.Kind is Integer && to.Kind is Integer && step.Kind is Integer)
        {
            vmState.SetRange(destinationRegister, from.IntegerValue, to.IntegerValue, step.IntegerValue);
        }
        else if (from.IsNumeric && to.IsNumeric && step.IsNumeric)
        {
            vmState.SetRange(destinationRegister, from.AsNumeric, to.AsNumeric, step.AsNumeric);
        }
        else
        {
            vmState.SetNothing(destinationRegister);
        }
    }
    internal static void GesVmCreateRangeStream(this GesVmState vmState, ushort destinationRegister, in GesVmValue from, in GesVmValue to, GameEventScriptSession session)
    {
        if (from.Kind is Integer && to.Kind is Integer)
        {
            if (!session.RuntimeBudget.TryCheckRangeLength(GameEventScriptRangeMath.GetLength(from.IntegerValue, to.IntegerValue, 1), "For loop range would enumerate more range items than allowed."))
            {
                vmState.SetStream(destinationRegister, new GesVmIntegerRangeStream(0, 0, 0));
                return;
            }

            vmState.SetStream(destinationRegister, new GesVmIntegerRangeStream(from.IntegerValue, to.IntegerValue, 1));
        }
        else if (from.IsNumeric && to.IsNumeric)
        {
            if (!session.RuntimeBudget.TryCheckRangeLength(GameEventScriptRangeMath.GetLength(from.AsNumeric, to.AsNumeric, 1.0d), "For loop range would enumerate more range items than allowed."))
            {
                vmState.SetStream(destinationRegister, new GesVmIntegerRangeStream(0, 0, 0));
                return;
            }

            vmState.SetStream(destinationRegister, new GesVmFloatRangeStream(from.AsNumeric, to.AsNumeric, 1.0d));
        }
        else
        {
            vmState.SetNothing(destinationRegister);
        }
    }
    internal static void GesVmCreateRangeStream(this GesVmState vmState, ushort destinationRegister, in GesVmValue from, in GesVmValue to, in GesVmValue step, GameEventScriptSession session)
    {
        if (from.Kind is Integer && to.Kind is Integer && step.Kind is Integer)
        {
            if (!session.RuntimeBudget.TryCheckRangeLength(GameEventScriptRangeMath.GetLength(from.IntegerValue, to.IntegerValue, step.IntegerValue), "For loop range would enumerate more range items than allowed."))
            {
                vmState.SetStream(destinationRegister, new GesVmIntegerRangeStream(0, 0, 0));
                return;
            }

            vmState.SetStream(destinationRegister, new GesVmIntegerRangeStream(from.IntegerValue, to.IntegerValue, step.IntegerValue));
        }
        else if (from.IsNumeric && to.IsNumeric && step.IsNumeric)
        {
            if (!session.RuntimeBudget.TryCheckRangeLength(GameEventScriptRangeMath.GetLength(from.AsNumeric, to.AsNumeric, step.AsNumeric), "For loop range would enumerate more range items than allowed."))
            {
                vmState.SetStream(destinationRegister, new GesVmIntegerRangeStream(0, 0, 0));
                return;
            }

            vmState.SetStream(destinationRegister, new GesVmFloatRangeStream(from.AsNumeric, to.AsNumeric, step.AsNumeric));
        }
        else
        {
            vmState.SetNothing(destinationRegister);
        }
    }
}
