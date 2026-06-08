using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;
using static StepH.GameEventScript.BytecodeExecutor.VmUnitCalculation;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterRandom
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmRandom(ref this VmValue dst, ref VmValue from, ref VmValue to, GameEventScriptRandomGenerator randomGenerator, ref GameEventScriptTextTable textTable)
    {
        switch (from.Kind)
        {
            case Integer when to.Kind is Integer:
                if (TrySameUnit(ref from, ref to, out var unit)) dst.SetInteger(randomGenerator.NextInclusiveInteger(from.IntegerValue, to.IntegerValue), unit);
                else dst.SetFloat(double.NaN);
                return;
            case Float when to.Kind is Float:
                if (TrySameUnit(ref from, ref to, out unit))
                {
                    var left = from.FloatValue;
                    var right = to.FloatValue;
                    if (double.IsFinite(left) && double.IsFinite(right)) dst.SetFloat(randomGenerator.NextInclusiveFloat(left, right), unit);
                    else dst.SetFloat(double.NaN);
                }
                else dst.SetFloat(double.NaN);

                return;
            case Float or Integer when to.Kind is Float or Integer:
                if (TrySameUnit(ref from, ref to, out unit))
                {
                    var left = from.AsNumeric;
                    var right = to.AsNumeric;
                    if (double.IsFinite(left) && double.IsFinite(right)) dst.SetFloat(randomGenerator.NextInclusiveFloat(left, right), unit);
                    else dst.SetFloat(double.NaN);
                }
                else dst.SetFloat(double.NaN);

                return;
            case Percentage when to.Kind is Percentage:
                if (double.IsFinite(from.FloatValue) && double.IsFinite(to.FloatValue)) dst.SetFloat(randomGenerator.NextInclusiveFloat(from.FloatValue, to.FloatValue));
                else dst.SetFloat(double.NaN);
                return;
            case Percentage when to.Kind is Integer or Float:
                if (to.HasUnit)
                {
                    dst.SetFloat(double.NaN);
                    return;
                }

                var percentageLeft = from.FloatValue;
                var numericRight = to.AsNumeric;
                if (double.IsFinite(percentageLeft) && double.IsFinite(numericRight)) dst.SetFloat(randomGenerator.NextInclusiveFloat(percentageLeft, numericRight));
                else dst.SetFloat(double.NaN);
                return;
            case Integer or Float when to.Kind is Percentage:
                if (from.HasUnit)
                {
                    dst.SetFloat(double.NaN);
                    return;
                }

                var numericLeft = from.AsNumeric;
                var percentageRight = to.FloatValue;
                if (double.IsFinite(numericLeft) && double.IsFinite(percentageRight)) dst.SetFloat(randomGenerator.NextInclusiveFloat(numericLeft, percentageRight));
                else dst.SetFloat(double.NaN);
                return;
            case Nothing:
                dst.SetNothing();
                return;
            default:
                if (to.Kind is Nothing)
                {
                    dst.SetNothing();
                    return;
                }

                if (!TrySameUnit(ref from, ref to, out unit))
                {
                    dst.SetFloat(double.NaN);
                    return;
                }

                var fallbackLeft = from.AsNumeric;
                var fallbackRight = to.AsNumeric;
                if (double.IsFinite(fallbackLeft) && double.IsFinite(fallbackRight)) dst.SetFloat(randomGenerator.NextInclusiveFloat(fallbackLeft, fallbackRight), unit);
                else dst.SetFloat(double.NaN);
                return;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmRandomFloat(ref this VmValue dst, ref VmValue from, ref VmValue to, GameEventScriptRandomGenerator randomGenerator)
    {
        if (from.Kind is Nothing || to.Kind is Nothing)
        {
            dst.SetNothing();
            return;
        }

        if (!TrySameUnit(ref from, ref to, out var unit))
        {
            dst.SetFloat(double.NaN);
            return;
        }

        var left = from.AsNumeric;
        var right = to.AsNumeric;
        if (double.IsFinite(left) && double.IsFinite(right)) dst.SetFloat(randomGenerator.NextInclusiveFloat(left, right), unit);
        else dst.SetFloat(double.NaN);
    }
}