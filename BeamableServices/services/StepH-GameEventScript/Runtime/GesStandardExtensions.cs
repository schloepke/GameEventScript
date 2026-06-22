using System;
using System.Collections.Generic;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.VirtualMachine;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeInstructionUnit;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;
using static StepH.GameEventScript.Api.GameEventScriptMessageSignature;
using static StepH.GameEventScript.Api.GameEventScriptNumericRules;
using static StepH.GameEventScript.Api.GameEventScriptValueFactory;

namespace StepH.GameEventScript.Runtime;

internal static class GesStandardExtensions
{
    private const double Pi = 3.1415926535897932384626433833d;

    public static bool IsStandardReference(GameEventScriptExtensionReference reference) => IsUnaryStandardReference(reference) || IsSeriesStandardReference(reference);

    public static bool TryInvoke(GameEventScriptExtensionReference reference, ReadOnlySpan<GameEventScriptValue> arguments, out GameEventScriptValue value)
    {
        value = GesNothing();
        if (IsSeriesStandardReference(reference))
        {
            value = EvaluateSeries(reference, arguments);
            return true;
        }

        if (!IsUnaryStandardReference(reference) || arguments.Length != 1) return false;

        var result = reference.ExtensionName switch
        {
            "integer" => EvaluateInteger(reference.FunctionName, arguments[0]),
            "degree" => EvaluateDegree(reference.FunctionName, arguments[0]),
            _ => GesNothing()
        };

        value = result;
        return true;
    }

    private static bool IsSeriesStandardReference(GameEventScriptExtensionReference reference) => reference.ExtensionName == "series" && reference.FunctionName switch
    {
        "fibonacci" or "factorial" => reference.ArgumentLabels.Count == 0,
        "natural" => IsNaturalSeriesSignature(reference.ArgumentLabels),
        _ => false
    };

    private static bool IsNaturalSeriesSignature(IReadOnlyList<string> labels) => labels.Count switch
    {
        0 => true,
        1 => IsUnlabeled(labels[0]) || IsLabel(labels[0], "start") || IsLabel(labels[0], "step"),
        _ => labels.Count == 2 && IsLabel(labels[0], "start") && IsLabel(labels[1], "step")
    };

    private static bool IsUnaryStandardReference(GameEventScriptExtensionReference reference)
        => reference.ArgumentLabels.Count == 1 && IsUnlabeled(reference.ArgumentLabels[0]) &&
           reference is { ExtensionName: "integer", FunctionName: "floor" or "ceil" or "truncate" or "halfEven" or "halfUp" or "halfDown" } or { ExtensionName: "degree", FunctionName: "wrap" or "toRadians" or "fromRadians" };

    private static bool IsUnlabeled(string label) => string.Equals(NormalizeParameterName(label), UnlabeledParameterName, StringComparison.Ordinal);

    private static bool IsLabel(string label, string expected) => string.Equals(NormalizeParameterName(label), expected, StringComparison.Ordinal);

    private static GameEventScriptValue EvaluateSeries(GameEventScriptExtensionReference reference, ReadOnlySpan<GameEventScriptValue> arguments)
    {
        var series = reference.FunctionName switch
        {
            "fibonacci" => GesVmSeries.Fibonacci(),
            "factorial" => GesVmSeries.Factorial(),
            "natural" => EvaluateNaturalSeries(reference.ArgumentLabels, arguments),
            _ => null
        };

        return series is null ? GesNothing() : GesSeries(series);
    }

    private static GesVmSeries EvaluateNaturalSeries(IReadOnlyList<string> labels, ReadOnlySpan<GameEventScriptValue> arguments)
    {
        long start = 0;
        long step = 1;
        for (var index = 0; index < arguments.Length; index++)
        {
            var label = labels.Count > index ? labels[index] : UnlabeledParameterName;
            if (IsUnlabeled(label) || IsLabel(label, "start"))
            {
                start = arguments[index].Integer;
            }
            else if (IsLabel(label, "step"))
            {
                step = arguments[index].Integer;
            }
        }

        return GesVmSeries.Natural(start, step);
    }

    private static GameEventScriptValue EvaluateInteger(string functionName, GameEventScriptValue input)
    {
        if (!TryReadNumeric(input, out var number)) return GesInteger(input.Integer);
        if (number.IsNaN) return GesInteger(0);
        if (number.IsPositiveInfinity) return GesInteger(long.MaxValue);
        if (number.IsNegativeInfinity) return GesInteger(long.MinValue);
        return GesInteger(ToIntegerSaturated(functionName switch
        {
            "floor" => Math.Floor(number.Value),
            "ceil" => Math.Ceiling(number.Value),
            "truncate" => Math.Truncate(number.Value),
            "halfEven" => Math.Round(number.Value, 0, MidpointRounding.ToEven),
            "halfUp" => Math.Round(number.Value, 0, MidpointRounding.AwayFromZero),
            "halfDown" => RoundHalfTowardZero(number.Value),
            _ => 0d
        }));
    }

    private static GameEventScriptValue EvaluateDegree(string functionName, GameEventScriptValue input) => functionName switch
    {
        "wrap" => EvaluateDegreeWrap(input),
        "toRadians" => EvaluateDegreeToRadians(input),
        "fromRadians" => EvaluateDegreeFromRadians(input),
        _ => GesNothing()
    };

    private static GameEventScriptValue EvaluateDegreeWrap(GameEventScriptValue input)
    {
        if (input.Unit.IsNumericUnit() && input.Unit != UnitDegree) return GesNothing();
        return input.Kind is Integer or Float ? GesFloat(WrapDegrees(input.Number), UnitDegree) : GesNothing();
    }

    private static GameEventScriptValue EvaluateDegreeToRadians(GameEventScriptValue input)
    {
        if (!TryReadUnitlessOrDegreeNumeric(input, out var number) || !number.IsFinite) return GesNothing();
        try
        {
            return GesFloat(number.Value / 180d * Pi);
        }
        catch (OverflowException)
        {
            return GesNothing();
        }
    }

    private static GameEventScriptValue EvaluateDegreeFromRadians(GameEventScriptValue input)
    {
        if (!TryReadUnitlessNumeric(input, out var number) || !number.IsFinite) return GesNothing();
        try
        {
            return GesFloat(number.Value / Pi * 180d, UnitDegree);
        }
        catch (OverflowException)
        {
            return GesNothing();
        }
    }

    private static bool TryReadUnitlessOrDegreeNumeric(GameEventScriptValue input, out NumericValue number)
    {
        if (!input.Unit.IsNumericUnit() || input.Unit == UnitDegree) return TryReadNumeric(input, out number);
        number = NumericValue.NaN();
        return false;
    }

    private static bool TryReadUnitlessNumeric(GameEventScriptValue input, out NumericValue number)
    {
        if (!input.Unit.IsNumericUnit()) return TryReadNumeric(input, out number);
        number = NumericValue.NaN();
        return false;
    }

    private static bool TryReadNumeric(GameEventScriptValue input, out NumericValue number)
    {
        switch (input.Kind)
        {
            case Integer:
            case Percentage:
            case GameEventScriptBytecodeTypeKind.Boolean:
                number = NumericValue.Finite(input.Number);
                return true;
            case Float:
                var value = input.Number;
                if (double.IsPositiveInfinity(value)) number = NumericValue.PositiveInfinity();
                else if (double.IsNegativeInfinity(value)) number = NumericValue.NegativeInfinity();
                else number = double.IsNaN(value) ? NumericValue.NaN() : NumericValue.Finite(value);
                return true;
            default:
                number = default;
                return false;
        }
    }

    private static double RoundHalfTowardZero(double value)
    {
        var sign = Math.Sign(value);
        var absolute = Math.Abs(value);
        var floor = Math.Floor(absolute);
        var fraction = absolute - floor;
        var roundedAbsolute = fraction > 0.5d ? floor + 1d : floor;
        return sign < 0 ? -roundedAbsolute : roundedAbsolute;
    }
}