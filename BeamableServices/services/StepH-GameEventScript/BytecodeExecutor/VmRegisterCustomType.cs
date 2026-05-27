using System.Runtime.CompilerServices;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterCustomType
{

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCreateRecord(ref this VmValue dst, ushort typeNameIndex, ushort argumentSlotsIndex, ushort valueSlotsIndex, ref VmState state)
    {
        // FIXME: creating the real custom type here
        dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCreateExternalType(ref this VmValue dst, ushort externalTypeConstructorReferenceIndex, ushort argumentSlotsIndex, ushort valueSlotsIndex, ref VmState state)
    {
        // FIXME: creating the real custom type here
        dst.SetNothing();
    }
}
