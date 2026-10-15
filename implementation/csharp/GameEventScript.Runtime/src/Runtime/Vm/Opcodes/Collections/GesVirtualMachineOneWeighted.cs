// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteOneWeighted(ushort destinationRegister, in GesValue itemsValue, in GesValue weightsValue, GesRandomGenerator randomGenerator)
    {
        if (itemsValue is not { Kind: List, ObjectValue: GesValue[] items } ||
            weightsValue is not { Kind: List, ObjectValue: GesValue[] weights } ||
            items.Length == 0 ||
            weights.Length < items.Length)
        {
            SetNothing(destinationRegister);
            return;
        }

        double totalWeight = 0d;
        for (var i = 0; i < items.Length; i++)
        {
            var weight = weights[i].AsNumeric;
            if (!double.IsFinite(weight) || weight <= 0d)
            {
                SetNothing(destinationRegister);
                return;
            }

            totalWeight += weight;
        }

        if (totalWeight <= 0d)
        {
            SetNothing(destinationRegister);
            return;
        }

        var threshold = randomGenerator.NextFloat(0d, totalWeight);
        double cumulative = 0d;
        var selected = items.Length - 1;
        for (var i = 0; i < items.Length; i++)
        {
            cumulative += weights[i].AsNumeric;
            if (threshold < cumulative)
            {
                selected = i;
                break;
            }
        }

        SetValue(destinationRegister, in items[selected]);
    }
}
