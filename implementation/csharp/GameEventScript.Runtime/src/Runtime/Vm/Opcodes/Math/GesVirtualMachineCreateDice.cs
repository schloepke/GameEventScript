// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteCreateDice(ushort destinationRegister, short count, short sides, GesContext context)
    {
        if (count <= 0 || sides <= 0 || !context.RuntimeBudget.CheckDiceWithinLimit(count, sides))
        {
            SetDice(destinationRegister, EmptyDice);
            return;
        }

        var dices = new int[count];
        for (var i = 0; i < count; i++) dices[i] = (int)RandomGenerator.NextInclusiveInteger(1, sides);
        SetDice(destinationRegister, dices);
    }

    private static readonly int[] EmptyDice = [];
}
