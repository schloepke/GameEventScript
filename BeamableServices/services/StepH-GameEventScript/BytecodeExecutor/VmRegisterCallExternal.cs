using System.Runtime.CompilerServices;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterCallExternal
{
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCallStandard(ref this VmValue dst, ushort extensionShape, ushort argumentSlotList, ref VmState state)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCallStandardPredicate(ref this VmValue dst, ushort extensionShape, ushort argumentSlotList, ref VmState state)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCallExternal(ref this VmValue dst, ushort extensionShape, ushort argumentSlotList, ref VmState state)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCallExternalPredicate(ref this VmValue dst, ushort extensionShape, ushort argumentSlotList, ref VmState state)
    {
    }
}