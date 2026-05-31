using System.Runtime.CompilerServices;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterCustomType
{

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCreateExternalType(ref this VmValue dst, ushort externalTypeConstructorReferenceIndex, ushort argumentNamesIndex)
    {
        // FIXME: creating the real custom type here
        dst.SetNothing();
    }
}
