using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmRegisterDice
{
    private static readonly int[] EmptyDice = [];

    internal static void GesVmCreateDice(this GesVmState state, ushort destinationRegister, short count, short sides, GameEventScriptSession session)
    {
        if (count <= 0 || sides <= 0 || !session.RuntimeBudget.TryCheckDice(count, sides))
        {
            state.SetDice(destinationRegister, EmptyDice);
            return;
        }

        var dices = new int[count];
        for (var i = 0; i < count; i++) dices[i] = state.RandomGenerator.NextInclusiveInt(1, sides);
        state.SetDice(destinationRegister, dices);
    }
}
