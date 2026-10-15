// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteChance(ushort dstRegister, in GesValue a)
    {
        if (a.IsNothing || a.HasUnit)
        {
            SetNothing(dstRegister);
            return;
        }

        var ratio = 0d;
        switch (a.Kind)
        {
            case Integer:
                ratio = a.IntegerValue / 100d;
                break;
            case Float:
                ratio = a.FloatValue is > 1d or < -1d ? a.FloatValue / 100d : a.FloatValue;
                break;
            case Percentage:
                ratio = a.FloatValue;
                break;
            case Tag when a.IsNumeric:
                ratio = a.AsNumeric;
                ratio = ratio is > 1d or < -1d ? ratio / 100d : ratio;
                break;
            default:
                SetNothing(dstRegister);
                return;
        }

        if (double.IsNaN(ratio) || double.IsInfinity(ratio))
        {
            SetNothing(dstRegister);
        }
        else if (ratio <= 0d)
        {
            SetBoolean(dstRegister, false);
        }
        else if (ratio >= 1d)
        {
            SetBoolean(dstRegister, true);
        }
        else
        {
            SetBoolean(dstRegister, RandomGenerator.NextFloat(0, 1.0) < ratio);
        }
    }
}
