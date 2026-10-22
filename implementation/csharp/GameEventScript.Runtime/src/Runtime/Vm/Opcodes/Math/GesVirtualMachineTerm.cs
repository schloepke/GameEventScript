// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

[SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
internal partial class GesVirtualMachine
{
    private void ExecuteTerm(ushort destinationRegister, in GesValue source, in GesValue termValue)
    {
        var dst = TermValue(in source, in termValue, this);
        SetValue(destinationRegister, in dst);
    }

    private static GesValue TermValue(in GesValue source, in GesValue termValue, GesVirtualMachine state)
    {
        var dst = new GesValue();
        long index;
        bool ret;
        switch (termValue.Kind)
        {
            case Integer:
                index = termValue.IntegerValue;
                ret = true;
                break;
            case Float or Percentage:
                ret = !double.IsNaN(termValue.FloatValue);
                index = ret ? GesNumber.ToIntegerSaturated(termValue.FloatValue) : 0;

                break;
            case GesBytecodeTypeKind.Boolean:
                index = termValue.IsTrue ? 1 : 0;
                ret = true;
                break;
            default:
                if (termValue.IsNumeric)
                {
                    var numeric = termValue.AsNumeric;
                    ret = !double.IsNaN(numeric);
                    index = ret ? GesNumber.ToIntegerSaturated(numeric) : 0;

                    break;
                }

                index = 0;
                ret = false;
                break;
        }

        if (source.Kind is not Series || source.ObjectValue is not GesSeriesValue series || !ret) dst.SetNothing();
        else dst = series.GetTerm(index);
        return dst;
    }
}
