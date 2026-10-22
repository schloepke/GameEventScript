// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics.CodeAnalysis;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

[SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
internal partial class GesVirtualMachine
{
    private void ExecuteLogN(ushort destinationRegister, in GesValue a)
    {
        var dst = LogNValue(in a, this);
        SetValue(destinationRegister, in dst);
    }

    private static GesValue LogNValue(in GesValue a, GesVirtualMachine state)
    {
        var dst = new GesValue();
        switch (a.Kind)
        {
            case Integer:
                if (a.HasUnit) dst.SetFloat(double.NaN);
                else if (a.IntegerValue < 0) dst.SetFloat(double.NaN);
                else if (a.IntegerValue == 0) dst.SetFloat(double.NegativeInfinity);
                else dst.SetFloat(Math.Log(a.IntegerValue));
                return dst;
            case Float:
                if (a.HasUnit)
                {
                    dst.SetFloat(double.NaN);
                    return dst;
                }

                var floatValue = a.FloatValue;
                if (double.IsNaN(floatValue) || floatValue < 0d || double.IsNegativeInfinity(floatValue)) dst.SetFloat(double.NaN);
                else if (double.IsPositiveInfinity(floatValue)) dst.SetFloat(double.PositiveInfinity);
                else dst.SetFloat(Math.Log(floatValue));
                return dst;
            case Percentage:
                var percentageValue = a.FloatValue;
                if (double.IsNaN(percentageValue) || percentageValue < 0d || double.IsNegativeInfinity(percentageValue)) dst.SetFloat(double.NaN);
                else if (double.IsPositiveInfinity(percentageValue)) dst.SetFloat(double.PositiveInfinity);
                else dst.SetFloat(Math.Log(percentageValue));
                return dst;
            case Nothing:
                dst.SetNothing();
                return dst;
            default:
                if (a.HasUnit)
                {
                    dst.SetFloat(double.NaN);
                    return dst;
                }

                var number = a.AsNumeric;
                if (double.IsNaN(number) || number < 0d || double.IsNegativeInfinity(number)) dst.SetFloat(double.NaN);
                else if (double.IsPositiveInfinity(number)) dst.SetFloat(double.PositiveInfinity);
                else dst.SetFloat(Math.Log(number));
                return dst;
        }
    }
}
