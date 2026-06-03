using System.Runtime.CompilerServices;
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
    internal static void VmCreateRangeStream(ref this VmValue dst, ref VmValue from, ref VmValue to)
    {
        if (from.Kind is Integer && to.Kind is Integer)
        {
            dst.SetStream(new VmIntegerRangeStream(from.IntegerValue, to.IntegerValue, 1));
        }
        else if (from.IsNumeric && to.IsNumeric)
        {
            dst.SetStream(new VmFloatRangeStream(from.AsNumeric, to.AsNumeric, 1.0d));
        }
        else
        {
            dst.SetNothing();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCreateRangeStream(ref this VmValue dst, ref VmValue from, ref VmValue to, ref VmValue step)
    {
        if (from.Kind is Integer && to.Kind is Integer && step.Kind is Integer)
        {
            dst.SetStream(new VmIntegerRangeStream(from.IntegerValue, to.IntegerValue, step.IntegerValue));
        }
        else if (from.IsNumeric && to.IsNumeric && step.IsNumeric)
        {
            dst.SetStream(new VmFloatRangeStream(from.AsNumeric, to.AsNumeric, step.AsNumeric));
        }
        else
        {
            dst.SetNothing();
        }
    }
}