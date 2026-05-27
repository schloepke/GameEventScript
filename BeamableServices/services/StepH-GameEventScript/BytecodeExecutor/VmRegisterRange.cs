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
            return;
        }
        
        dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCreateRange(ref this VmValue dst, ref VmValue from, ref VmValue to, ref VmValue step)
    {
        if (from.Kind is Integer && to.Kind is Integer && step.Kind is Integer)
        {
            dst.SetRange(from.IntegerValue, to.IntegerValue, step.IntegerValue);
            return;
        }
        
        dst.SetNothing();
    }

}