using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmRegisterDice
{
    private static readonly int[] EmptyDice = [];

    internal static void GesVmCreateDice(this GesVmState vmState, ushort destinationRegister, short count, short sides, GameEventScriptSession session)
    {
        if (count <= 0 || sides <= 0 || !session.RuntimeBudget.TryCheckDice(count, sides))
        {
            vmState.SetDice(destinationRegister, EmptyDice);
            return;
        }

        var dices = new int[count];
        for (var i = 0; i < count; i++) dices[i] = vmState.RandomGenerator.NextInclusiveInt(1, sides);
        vmState.SetDice(destinationRegister, dices);
    }
}
