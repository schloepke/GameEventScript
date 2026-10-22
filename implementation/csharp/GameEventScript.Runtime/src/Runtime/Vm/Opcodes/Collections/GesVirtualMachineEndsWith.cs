// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteEndsWith(ushort dst, in GesValue a, in GesValue b)
    {
        switch (a.Kind)
        {
            case Text or Tag when b.Kind is Text or Tag:
                SetBoolean(dst, a.TextValue.EndsWith(b.TextValue, StringComparison.Ordinal));
                return;
            case Nothing:
                SetNothing(dst);
                return;
            case not (List or Dice or GesBytecodeTypeKind.Range):
                SetBoolean(dst, false);
                return;
        }

        if (b.Kind is not (List or Dice or GesBytecodeTypeKind.Range))
        {
            SetBoolean(dst, false);
            return;
        }

        GesValue[]? leftList = null;
        GesValue[]? rightList = null;
        int[]? leftDice = null;
        int[]? rightDice = null;
        GesIntegerRange? leftRange = null;
        GesIntegerRange? rightRange = null;
        GesFloatRange? leftFloatRange = null;
        GesFloatRange? rightFloatRange = null;

        switch (a.Kind)
        {
            case List when a.ObjectValue is GesValue[] value:
                leftList = value;
                break;
            case Dice when a.ObjectValue is int[] value:
                leftDice = value;
                break;
            case GesBytecodeTypeKind.Range when a.ObjectValue is GesIntegerRange value:
                leftRange = value;
                break;
            case GesBytecodeTypeKind.Range when a.ObjectValue is GesFloatRange value:
                leftFloatRange = value;
                break;
            default:
                SetBoolean(dst, false);
                return;
        }

        switch (b.Kind)
        {
            case List when b.ObjectValue is GesValue[] value:
                rightList = value;
                break;
            case Dice when b.ObjectValue is int[] value:
                rightDice = value;
                break;
            case GesBytecodeTypeKind.Range when b.ObjectValue is GesIntegerRange value:
                rightRange = value;
                break;
            case GesBytecodeTypeKind.Range when b.ObjectValue is GesFloatRange value:
                rightFloatRange = value;
                break;
            default:
                SetBoolean(dst, false);
                return;
        }

        if (b.CollectionCount > a.CollectionCount)
        {
            SetBoolean(dst, false);
            return;
        }

        var leftOffset = a.CollectionCount - b.CollectionCount;
        var leftInteger = leftRange is not null ? leftRange.From + leftRange.Step * leftOffset : 0;
        var rightInteger = rightRange?.From ?? 0;

        for (var i = 0L; i < b.CollectionCount; i++)
        {
            var leftIndex = leftOffset + i;
            var left = new GesValue();
            if (leftList is not null) left = leftList[leftIndex];
            else if (leftDice is not null) left.SetInteger(leftDice[leftIndex]);
            else if (leftRange is not null)
            {
                left.SetInteger(leftInteger);
                leftInteger += leftRange.Step;
            }
            else if (leftFloatRange is not null)
            {
                left.SetFloat(GesRangeMath.GetFloatTerm(leftFloatRange.From, leftFloatRange.To, leftFloatRange.Step, leftIndex));
            }

            var right = new GesValue();
            if (rightList is not null) right = rightList[i];
            else if (rightDice is not null) right.SetInteger(rightDice[i]);
            else if (rightRange is not null)
            {
                right.SetInteger(rightInteger);
                rightInteger += rightRange.Step;
            }
            else if (rightFloatRange is not null)
            {
                right.SetFloat(GesRangeMath.GetFloatTerm(rightFloatRange.From, rightFloatRange.To, rightFloatRange.Step, i));
            }

            if (left.EqualsValue(right)) continue;
            SetBoolean(dst, false);
            return;
        }

        SetBoolean(dst, true);
    }
}
