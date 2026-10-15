// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;
using static GameEventScript.Runtime.Vm.GesVmUnitCalculation;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteRandomTake(ushort destinationRegister, in GesValue from, in GesValue to, GesRandomGenerator randomGenerator)
    {
        switch (from.Kind)
        {
            case Integer when to.Kind is Integer:
                if (SameUnit(in from, in to) is { } unit) SetInteger(destinationRegister, randomGenerator.NextInclusiveInteger(from.IntegerValue, to.IntegerValue), unit);
                else SetFloat(destinationRegister, double.NaN);
                return;
            case Float when to.Kind is Float:
                if (SameUnit(in from, in to) is { } floatUnit)
                {
                    var left = from.FloatValue;
                    var right = to.FloatValue;
                    if (double.IsFinite(left) && double.IsFinite(right)) SetFloat(destinationRegister, randomGenerator.NextFloat(left, right), floatUnit);
                    else SetFloat(destinationRegister, double.NaN);
                }
                else SetFloat(destinationRegister, double.NaN);

                return;
            case Float or Integer when to.Kind is Float or Integer:
                if (SameUnit(in from, in to) is { } mixedUnit)
                {
                    var left = from.AsNumeric;
                    var right = to.AsNumeric;
                    if (double.IsFinite(left) && double.IsFinite(right)) SetFloat(destinationRegister, randomGenerator.NextFloat(left, right), mixedUnit);
                    else SetFloat(destinationRegister, double.NaN);
                }
                else SetFloat(destinationRegister, double.NaN);

                return;
            case Percentage when to.Kind is Percentage:
                if (double.IsFinite(from.FloatValue) && double.IsFinite(to.FloatValue)) SetFloat(destinationRegister, randomGenerator.NextFloat(from.FloatValue, to.FloatValue));
                else SetFloat(destinationRegister, double.NaN);
                return;
            case Percentage when to.Kind is Integer or Float:
                if (to.HasUnit)
                {
                    SetFloat(destinationRegister, double.NaN);
                    return;
                }

                var percentageLeft = from.FloatValue;
                var numericRight = to.AsNumeric;
                if (double.IsFinite(percentageLeft) && double.IsFinite(numericRight)) SetFloat(destinationRegister, randomGenerator.NextFloat(percentageLeft, numericRight));
                else SetFloat(destinationRegister, double.NaN);
                return;
            case Integer or Float when to.Kind is Percentage:
                if (from.HasUnit)
                {
                    SetFloat(destinationRegister, double.NaN);
                    return;
                }

                var numericLeft = from.AsNumeric;
                var percentageRight = to.FloatValue;
                if (double.IsFinite(numericLeft) && double.IsFinite(percentageRight)) SetFloat(destinationRegister, randomGenerator.NextFloat(numericLeft, percentageRight));
                else SetFloat(destinationRegister, double.NaN);
                return;
            case Nothing:
                SetNothing(destinationRegister);
                return;
            default:
                if (to.Kind is Nothing)
                {
                    SetNothing(destinationRegister);
                    return;
                }

                if (SameUnit(in from, in to) is not { } fallbackUnit)
                {
                    SetFloat(destinationRegister, double.NaN);
                    return;
                }

                var fallbackLeft = from.AsNumeric;
                var fallbackRight = to.AsNumeric;
                if (double.IsFinite(fallbackLeft) && double.IsFinite(fallbackRight)) SetFloat(destinationRegister, randomGenerator.NextFloat(fallbackLeft, fallbackRight), fallbackUnit);
                else SetFloat(destinationRegister, double.NaN);
                return;
        }
    }
}
