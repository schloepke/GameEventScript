using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterTypeCastCheck
{
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCast(ref this VmValue dst, ref VmValue xSlot, GameEventScriptBytecodeTypeKind type)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCastNumeric(ref this VmValue dst, ref VmValue xSlot)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCastCustom(ref this VmValue dst, ref VmValue xSlot, ushort typeTextPointer)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCheckType(ref this VmValue dst, ref VmValue xSlot, GameEventScriptBytecodeTypeKind type)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCheckNumeric(ref this VmValue dst, ref VmValue xSlot)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCheckInteger(ref this VmValue dst, ref VmValue xSlot)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCheckFractional(ref this VmValue dst, ref VmValue xSlot)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCheckCustomType(ref this VmValue dst, ref VmValue xSlot, ushort typeTextPointer)
    {
    }

}
