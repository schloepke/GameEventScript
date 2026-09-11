// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;

namespace GameEventScript.Runtime.VM;

internal static class GesVmRegisterDice
{
    private static readonly int[] EmptyDice = [];

    internal static void GesVmCreateDice(this GesVmState vmState, ushort destinationRegister, short count, short sides, GameEventScriptContext context)
    {
        if (count <= 0 || sides <= 0 || !context.RuntimeBudget.CheckDiceWithinLimit(count, sides))
        {
            vmState.SetDice(destinationRegister, EmptyDice);
            return;
        }

        var dices = new int[count];
        for (var i = 0; i < count; i++) dices[i] = (int)vmState.RandomGenerator.NextInclusiveInteger(1, sides);
        vmState.SetDice(destinationRegister, dices);
    }
}
