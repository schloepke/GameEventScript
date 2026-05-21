using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterTypeCastCheck
{
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCast(ref this VmValue dst, ref VmValue source, ref GameEventScriptBytecodeTypeKind type)
    {
    }

}