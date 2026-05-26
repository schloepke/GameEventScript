using System.Runtime.CompilerServices;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterDice
{
    private static readonly long[] EmptyDice = [];
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmDice(ref this VmValue dst, short count, short sides, ref VmState state)
    {
        if (count <= 0 || sides <= 0)
        {
            dst.SetDice(EmptyDice);
            return;
        }
        var dices = new long[count];
        for (var i = 0; i < count; i++) dices[i] = state.RandomGenerator.NextInclusiveInteger(1, sides);
        dst.SetDice(dices);
    }

}
