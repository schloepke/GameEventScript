#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.Runtime;

internal static class GseStandardExtensions
{
    private const decimal Pi = 3.1415926535897932384626433833m;

    public static bool IsStandardReference(GseExtensionReference reference)
        => IsUnaryStandardReference(reference);

    public static bool TryInvoke(
        GseExtensionReference reference,
        ReadOnlySpan<GseFastValue> arguments,
        out GseFastValue value)
    {
        value = GseFastValue.Nothing;
        if (!IsUnaryStandardReference(reference) || arguments.Length != 1)
        {
            return false;
        }

        var result = reference.ExtensionName switch
        {
            "integer" => EvaluateInteger(reference.FunctionName, arguments[0]),
            "degree" => EvaluateDegree(reference.FunctionName, arguments[0]),
            _ => GseFastValue.Nothing
        };

        value = result;
        return true;
    }

    private static bool IsUnaryStandardReference(GseExtensionReference reference)
        => reference.ArgumentLabels.Count == 1 &&
           IsUnlabeled(reference.ArgumentLabels[0]) &&
           ((reference.ExtensionName == "integer" &&
             reference.FunctionName is "floor" or "ceil" or "truncate" or "halfEven" or "halfUp" or "halfDown") ||
            (reference.ExtensionName == "degree" &&
             reference.FunctionName is "wrap" or "toRadians" or "fromRadians"));

    private static bool IsUnlabeled(string label)
        => string.Equals(
            GseMessageSignature.NormalizeParameterName(label),
            GseMessageSignature.UnlabeledParameterName,
            StringComparison.Ordinal);

    private static GseFastValue EvaluateInteger(string functionName, GseFastValue input)
    {
        if (!TryReadNumeric(input, out var number))
        {
            return GseFastValue.FromInteger(input.Integer);
        }

        if (number.IsNaN)
        {
            return GseFastValue.FromInteger(0);
        }

        if (number.IsPositiveInfinity)
        {
            return GseFastValue.FromInteger(long.MaxValue);
        }

        if (number.IsNegativeInfinity)
        {
            return GseFastValue.FromInteger(long.MinValue);
        }

        var rounded = functionName switch
        {
            "floor" => Math.Floor(number.Value),
            "ceil" => Math.Ceiling(number.Value),
            "truncate" => decimal.Truncate(number.Value),
            "halfEven" => Math.Round(number.Value, 0, MidpointRounding.ToEven),
            "halfUp" => Math.Round(number.Value, 0, MidpointRounding.AwayFromZero),
            "halfDown" => RoundHalfTowardZero(number.Value),
            _ => 0m
        };

        return GseFastValue.FromInteger(GseValueAlu.ToIntegerSaturated(rounded));
    }

    private static GseFastValue EvaluateDegree(string functionName, GseFastValue input)
        => functionName switch
        {
            "wrap" => EvaluateDegreeWrap(input),
            "toRadians" => EvaluateDegreeToRadians(input),
            "fromRadians" => EvaluateDegreeFromRadians(input),
            _ => GseFastValue.Nothing
        };

    private static GseFastValue EvaluateDegreeWrap(GseFastValue input)
    {
        if (input.IsReferenceBacked)
        {
            return GseFastValue.FromGseValue(GseValueAlu.EvaluateWrapDegree(input.ToGseValue()));
        }

        if (input.Unit.HasValue && input.Unit.Value != GseDecimalUnit.Degree)
        {
            return GseFastValue.FromGseValue(GseValueFactory.DecimalNaN());
        }

        if (input.Kind is GseValueKind.Decimal or GseValueKind.Integer)
        {
            return GseFastValue.FromDecimal(GseValue.WrapDegrees(input.Number), GseDecimalUnit.Degree);
        }

        return GseFastValue.FromGseValue(GseValueFactory.DecimalNaN());
    }

    private static GseFastValue EvaluateDegreeToRadians(GseFastValue input)
    {
        if (!TryReadUnitlessOrDegreeNumeric(input, out var number) || !number.IsFinite)
        {
            return GseFastValue.FromGseValue(GseValueFactory.DecimalNaN());
        }

        try
        {
            return GseFastValue.FromDecimal(number.Value / 180m * Pi);
        }
        catch (OverflowException)
        {
            return GseFastValue.FromGseValue(GseValueFactory.DecimalNaN());
        }
    }

    private static GseFastValue EvaluateDegreeFromRadians(GseFastValue input)
    {
        if (!TryReadUnitlessNumeric(input, out var number) || !number.IsFinite)
        {
            return GseFastValue.FromGseValue(GseValueFactory.DecimalNaN());
        }

        try
        {
            return GseFastValue.FromDecimal(number.Value / Pi * 180m, GseDecimalUnit.Degree);
        }
        catch (OverflowException)
        {
            return GseFastValue.FromGseValue(GseValueFactory.DecimalNaN());
        }
    }

    private static bool TryReadUnitlessOrDegreeNumeric(GseFastValue input, out GseValueAlu.NumericValue number)
    {
        if (input.IsReferenceBacked)
        {
            var value = input.ToGseValue();
            if (!GseValueAlu.TryUnwrapOptionalForOperation(value, out var unwrapped))
            {
                number = GseValueAlu.NumericValue.NaN();
                return true;
            }

            if (GseValue.TryGetDecimalUnit(unwrapped, out var unit) && unit != GseDecimalUnit.Degree)
            {
                number = GseValueAlu.NumericValue.NaN();
                return false;
            }

            return GseValueAlu.TryCoerceNumericForOperation(unwrapped, out number);
        }

        if (input.Unit.HasValue && input.Unit.Value != GseDecimalUnit.Degree)
        {
            number = GseValueAlu.NumericValue.NaN();
            return false;
        }

        return TryReadNumeric(input, out number);
    }

    private static bool TryReadUnitlessNumeric(GseFastValue input, out GseValueAlu.NumericValue number)
    {
        if (input.IsReferenceBacked)
        {
            var value = input.ToGseValue();
            if (!GseValueAlu.TryUnwrapOptionalForOperation(value, out var unwrapped))
            {
                number = GseValueAlu.NumericValue.NaN();
                return true;
            }

            if (GseValue.TryGetDecimalUnit(unwrapped, out _))
            {
                number = GseValueAlu.NumericValue.NaN();
                return false;
            }

            return GseValueAlu.TryCoerceNumericForOperation(unwrapped, out number);
        }

        if (input.Unit.HasValue)
        {
            number = GseValueAlu.NumericValue.NaN();
            return false;
        }

        return TryReadNumeric(input, out number);
    }

    private static bool TryReadNumeric(GseFastValue input, out GseValueAlu.NumericValue number)
    {
        if (input.IsReferenceBacked)
        {
            var value = input.ToGseValue();
            if (!GseValueAlu.TryUnwrapOptionalForOperation(value, out var unwrapped))
            {
                number = GseValueAlu.NumericValue.NaN();
                return true;
            }

            return GseValueAlu.TryCoerceNumericForOperation(unwrapped, out number);
        }

        switch (input.Kind)
        {
            case GseValueKind.Decimal:
            case GseValueKind.Integer:
            case GseValueKind.Percentage:
            case GseValueKind.Boolean:
                number = GseValueAlu.NumericValue.Finite(input.Number);
                return true;
            default:
                number = default;
                return false;
        }
    }

    private static decimal RoundHalfTowardZero(decimal value)
    {
        var sign = Math.Sign(value);
        var absolute = Math.Abs(value);
        var floor = Math.Floor(absolute);
        var fraction = absolute - floor;
        var roundedAbsolute = fraction > 0.5m ? floor + 1m : floor;
        return sign < 0 ? -roundedAbsolute : roundedAbsolute;
    }
}
