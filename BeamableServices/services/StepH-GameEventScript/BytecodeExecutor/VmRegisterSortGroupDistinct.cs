using System.Runtime.CompilerServices;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterSortGroupDistinct
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmDistinct(ref this VmValue dst, ref VmValue source)
    {
        dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmDistinctBy(ref this VmValue dst, ref VmValue source, ushort itemSlot, ushort keyEntryAddress, IVmStreamEntryEvaluator evaluator)
    {
        dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmGroupBy(ref this VmValue dst, ref VmValue source, ushort itemSlot, ushort keyEntryAddress, IVmStreamEntryEvaluator evaluator)
    {
        dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmSortAscending(ref this VmValue dst, ref VmValue source)
    {
        dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmSortDescending(ref this VmValue dst, ref VmValue source)
    {
        dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmOrderByAscending(ref this VmValue dst, ref VmValue source, ushort itemSlot, ushort keyEntryAddress, IVmStreamEntryEvaluator evaluator)
    {
        dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmOrderByDescending(ref this VmValue dst, ref VmValue source, ushort itemSlot, ushort keyEntryAddress, IVmStreamEntryEvaluator evaluator)
    {
        dst.SetNothing();
    }
}
