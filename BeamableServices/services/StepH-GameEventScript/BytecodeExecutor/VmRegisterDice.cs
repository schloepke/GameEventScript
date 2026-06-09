using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterDice
{
    private static readonly int[] EmptyDice = [];
    internal static void VmCreateDice(ref this VmValue dst, short count, short sides, VmState state, GameEventScriptSession session)
    {
        if (count <= 0 || sides <= 0)
        {
            dst.SetDice(EmptyDice);
            return;
        }

        if (!session.RuntimeBudget.TryCheckDice(count, sides))
        {
            dst.SetDice(EmptyDice);
            return;
        }

        var dices = new int[count];
        for (var i = 0; i < count; i++) dices[i] = state.RandomGenerator.NextInclusiveInt(1, sides);
        dst.SetDice(dices);
    }

}
