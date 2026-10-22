// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteDropLowest(ushort destinationRegister, in GesValue source, short count)
    {
        GesValue result;
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesValue[] list:
                result = DropExtremeList(this, list, count, false);
                break;
            case Dice when source.ObjectValue is int[] dice:
                result = DropLastDice(dice, count);
                break;
            case GesBytecodeTypeKind.Range when source.ObjectValue is GesIntegerRange range:
                result = DropExtremeRange(in source, range, count, false);
                break;
            case GesBytecodeTypeKind.Range when source.ObjectValue is GesFloatRange range:
                result = DropExtremeRange(in source, range, count, false);
                break;
            case Iterator when source.ObjectValue is IGesIterator iterator:
                result = DropExtremeIterator(this, iterator, count, false);
                break;
            default:
                result = new GesValue();
                break;
        }

        SetValue(destinationRegister, in result);
    }
}
