// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteTakeWeighted(ushort destinationRegister, in GesValue itemsValue, in GesValue weightsValue, short count, GesRandomGenerator randomGenerator)
    {
        if (itemsValue is not { Kind: List, ObjectValue: GesValue[] items } ||
            weightsValue is not { Kind: List, ObjectValue: GesValue[] weights } ||
            weights.Length < items.Length)
        {
            SetNothing(destinationRegister);
            return;
        }

        if (count <= 0 || items.Length == 0)
        {
            SetList(destinationRegister, EmptyList);
            return;
        }

        var itemCount = items.Length;
        var itemBuffer = new GesValue[itemCount];
        var weightBuffer = new double[itemCount];
        double totalWeight = 0d;
        for (var i = 0; i < itemCount; i++)
        {
            var weight = weights[i].AsNumeric;
            if (!double.IsFinite(weight) || weight <= 0d)
            {
                SetList(destinationRegister, EmptyList);
                return;
            }

            itemBuffer[i] = items[i];
            weightBuffer[i] = weight;
            totalWeight += weight;
        }

        if (totalWeight <= 0d)
        {
            SetList(destinationRegister, EmptyList);
            return;
        }

        var selectedCount = count < itemCount ? count : itemCount;
        var list = new GesValue[selectedCount];
        var remainingCount = itemCount;
        for (var target = 0; target < selectedCount && remainingCount > 0 && totalWeight > 0d; target++)
        {
            var threshold = randomGenerator.NextFloat(0d, totalWeight);
            double cumulative = 0d;
            var selected = remainingCount - 1;
            for (var i = 0; i < remainingCount; i++)
            {
                cumulative += weightBuffer[i];
                if (threshold < cumulative)
                {
                    selected = i;
                    break;
                }
            }

            list[target] = itemBuffer[selected];
            totalWeight -= weightBuffer[selected];
            if (selected < remainingCount - 1)
            {
                Array.Copy(itemBuffer, selected + 1, itemBuffer, selected, remainingCount - selected - 1);
                Array.Copy(weightBuffer, selected + 1, weightBuffer, selected, remainingCount - selected - 1);
            }

            remainingCount--;
        }

        SetList(destinationRegister, list);
    }
}
