// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteIndexAccess(ushort destinationRegister, long indexIn, in GesValue obj)
    {
        if (indexIn <= 0)
        {
            SetNothing(destinationRegister);
            return;
        }

        var index = indexIn - 1L;
        switch (obj.Kind)
        {
            case List when obj.ObjectValue is GesValue[] list:
                if (index < list.Length) SetValue(destinationRegister, in list[(int)index]);
                else SetNothing(destinationRegister);
                return;
            case Dice when obj.ObjectValue is int[] dices:
                if (index < dices.Length) SetInteger(destinationRegister, dices[(int)index]);
                else SetNothing(destinationRegister);
                return;
            case Vector or Point when obj.ObjectValue is GesSpatialValue vp:
                switch (index)
                {
                    case 0:
                        SetFloat(destinationRegister, vp.X, obj.Unit);
                        break;
                    case 1:
                        SetFloat(destinationRegister, vp.Y, obj.Unit);
                        break;
                    case 2:
                        SetFloat(destinationRegister, vp.Z, obj.Unit);
                        break;
                    default:
                        SetNothing(destinationRegister);
                        break;
                }
                return;
            case Range when obj.ObjectValue is GesIntegerRange integerRange:
                if (GesRangeMath.GetTerm(integerRange.From, integerRange.To, integerRange.Step, indexIn) is { } intRangeValue) SetInteger(destinationRegister, intRangeValue);
                else SetNothing(destinationRegister);
                return;
            case Range when obj.ObjectValue is GesFloatRange floatRange:
                if (GesRangeMath.GetTerm(floatRange.From, floatRange.To, floatRange.Step, indexIn) is { } floatRangeValue) SetFloat(destinationRegister, floatRangeValue);
                else SetNothing(destinationRegister);
                return;
            case Text or Tag:
                if (GesText.ScalarAt(obj.TextValue, index) is { } scalar) SetText(destinationRegister, scalar);
                else SetNothing(destinationRegister);
                return;
            default:
                SetNothing(destinationRegister);
                return;
        }
    }
}
