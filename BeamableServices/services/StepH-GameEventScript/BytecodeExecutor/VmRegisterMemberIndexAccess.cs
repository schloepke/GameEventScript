using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterMemberIndexAccess
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmMemberAccess(ref this VmValue dst, ushort memberNameIndex, ref VmValue obj, ref GameEventScriptTextTable textTable)
    {
        
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmIndexAccess(ref this VmValue dst, ushort index, ref VmValue obj, ref GameEventScriptTextTable textTable)
    {
        
    }

}