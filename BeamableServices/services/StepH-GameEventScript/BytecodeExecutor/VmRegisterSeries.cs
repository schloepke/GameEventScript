using System.Runtime.CompilerServices;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterSeries
{

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmSeriesTerm(ref this VmValue dst, ref VmValue seriesSource, ref VmValue termSlot)
    {
        dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmSeriesTake(ref this VmValue dst, ref VmValue seriesSource, short count)
    {
        dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmSeriesDrop(ref this VmValue dst, ref VmValue seriesSource, short count)
    {
        dst.SetNothing();
    }

}