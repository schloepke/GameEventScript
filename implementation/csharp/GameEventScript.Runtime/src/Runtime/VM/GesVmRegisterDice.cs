// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;

namespace GameEventScript.Runtime.VM;

internal static class GesVmRegisterDice
{
    private static readonly int[] EmptyDice = [];

    internal static void GesVmCreateDice(this GesVirtualMachine vm, ushort destinationRegister, short count, short sides, GameEventScriptContext context)
    {
        if (count <= 0 || sides <= 0 || !context.RuntimeBudget.CheckDiceWithinLimit(count, sides))
        {
            vm.SetDice(destinationRegister, EmptyDice);
            return;
        }

        var dices = new int[count];
        for (var i = 0; i < count; i++) dices[i] = (int)vm.RandomGenerator.NextInclusiveInteger(1, sides);
        vm.SetDice(destinationRegister, dices);
    }
}
