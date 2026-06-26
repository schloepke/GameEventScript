using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;
using static StepH.GameEventScript.Runtime.VM.GesVmUnitCalculation;

namespace StepH.GameEventScript.Runtime.VM;

internal static class GesVmRegisterRandom
{
    internal static void GesVmRandom(this GesVmState vmState, ushort destinationRegister, in GesVmValue from, in GesVmValue to, GesVmXoshiroRandom randomGenerator)
    {
        switch (from.Kind)
        {
            case Integer when to.Kind is Integer:
                if (TrySameUnit(in from, in to, out var unit)) vmState.SetInteger(destinationRegister, randomGenerator.NextInclusiveInteger(from.IntegerValue, to.IntegerValue), unit);
                else vmState.SetFloat(destinationRegister, double.NaN);
                return;
            case Float when to.Kind is Float:
                if (TrySameUnit(in from, in to, out unit))
                {
                    var left = from.FloatValue;
                    var right = to.FloatValue;
                    if (double.IsFinite(left) && double.IsFinite(right)) vmState.SetFloat(destinationRegister, randomGenerator.NextInclusiveFloat(left, right), unit);
                    else vmState.SetFloat(destinationRegister, double.NaN);
                }
                else vmState.SetFloat(destinationRegister, double.NaN);

                return;
            case Float or Integer when to.Kind is Float or Integer:
                if (TrySameUnit(in from, in to, out unit))
                {
                    var left = from.AsNumeric;
                    var right = to.AsNumeric;
                    if (double.IsFinite(left) && double.IsFinite(right)) vmState.SetFloat(destinationRegister, randomGenerator.NextInclusiveFloat(left, right), unit);
                    else vmState.SetFloat(destinationRegister, double.NaN);
                }
                else vmState.SetFloat(destinationRegister, double.NaN);

                return;
            case Percentage when to.Kind is Percentage:
                if (double.IsFinite(from.FloatValue) && double.IsFinite(to.FloatValue)) vmState.SetFloat(destinationRegister, randomGenerator.NextInclusiveFloat(from.FloatValue, to.FloatValue));
                else vmState.SetFloat(destinationRegister, double.NaN);
                return;
            case Percentage when to.Kind is Integer or Float:
                if (to.HasUnit)
                {
                    vmState.SetFloat(destinationRegister, double.NaN);
                    return;
                }

                var percentageLeft = from.FloatValue;
                var numericRight = to.AsNumeric;
                if (double.IsFinite(percentageLeft) && double.IsFinite(numericRight)) vmState.SetFloat(destinationRegister, randomGenerator.NextInclusiveFloat(percentageLeft, numericRight));
                else vmState.SetFloat(destinationRegister, double.NaN);
                return;
            case Integer or Float when to.Kind is Percentage:
                if (from.HasUnit)
                {
                    vmState.SetFloat(destinationRegister, double.NaN);
                    return;
                }

                var numericLeft = from.AsNumeric;
                var percentageRight = to.FloatValue;
                if (double.IsFinite(numericLeft) && double.IsFinite(percentageRight)) vmState.SetFloat(destinationRegister, randomGenerator.NextInclusiveFloat(numericLeft, percentageRight));
                else vmState.SetFloat(destinationRegister, double.NaN);
                return;
            case Nothing:
                vmState.SetNothing(destinationRegister);
                return;
            default:
                if (to.Kind is Nothing)
                {
                    vmState.SetNothing(destinationRegister);
                    return;
                }

                if (!TrySameUnit(in from, in to, out unit))
                {
                    vmState.SetFloat(destinationRegister, double.NaN);
                    return;
                }

                var fallbackLeft = from.AsNumeric;
                var fallbackRight = to.AsNumeric;
                if (double.IsFinite(fallbackLeft) && double.IsFinite(fallbackRight)) vmState.SetFloat(destinationRegister, randomGenerator.NextInclusiveFloat(fallbackLeft, fallbackRight), unit);
                else vmState.SetFloat(destinationRegister, double.NaN);
                return;
        }
    }
    internal static void GesVmRandomFloat(this GesVmState vmState, ushort destinationRegister, in GesVmValue from, in GesVmValue to, GesVmXoshiroRandom randomGenerator)
    {
        if (from.Kind is Nothing || to.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (!TrySameUnit(in from, in to, out var unit))
        {
            vmState.SetFloat(destinationRegister, double.NaN);
            return;
        }

        var left = from.AsNumeric;
        var right = to.AsNumeric;
        if (double.IsFinite(left) && double.IsFinite(right)) vmState.SetFloat(destinationRegister, randomGenerator.NextInclusiveFloat(left, right), unit);
        else vmState.SetFloat(destinationRegister, double.NaN);
    }
}