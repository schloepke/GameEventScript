using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmRegisterDice
{
    private static readonly int[] EmptyDice = [];
    internal static void GesVmCreateDice(ref this GesVmValue dst, short count, short sides, GesVmState state, GameEventScriptSession session)
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
