#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using StepH.Flow.EventScript.Types;

namespace StepH.Flow.EventScript.Runtime;

internal static class EventScriptStandardExtensions
{
    private const decimal Pi = 3.1415926535897932384626433833m;

    public static bool IsStandardReference(EventScriptExtensionReference reference)
        => IsUnaryStandardReference(reference);

    public static bool TryInvoke(
        EventScriptExtensionReference reference,
        ReadOnlySpan<EventScriptFastValue> arguments,
        out EventScriptFastValue value)
    {
        value = EventScriptFastValue.Nothing;
        if (!IsUnaryStandardReference(reference) || arguments.Length != 1)
        {
            return false;
        }

        var result = reference.ExtensionName switch
        {
            "integer" => EvaluateInteger(reference.FunctionName, arguments[0]),
            "degree" => EvaluateDegree(reference.FunctionName, arguments[0]),
            _ => EventScriptFastValue.Nothing
        };

        value = result;
        return true;
    }

    private static bool IsUnaryStandardReference(EventScriptExtensionReference reference)
        => reference.ArgumentLabels.Count == 1 &&
           IsUnlabeled(reference.ArgumentLabels[0]) &&
           ((reference.ExtensionName == "integer" &&
             reference.FunctionName is "floor" or "ceil" or "truncate" or "halfEven" or "halfUp" or "halfDown") ||
            (reference.ExtensionName == "degree" &&
             reference.FunctionName is "wrap" or "toRadians" or "fromRadians"));

    private static bool IsUnlabeled(string label)
        => string.Equals(
            EventScriptMessageSignature.NormalizeParameterName(label),
            EventScriptMessageSignature.UnlabeledParameterName,
            StringComparison.Ordinal);

    private static EventScriptFastValue EvaluateInteger(string functionName, EventScriptFastValue input)
    {
        if (!TryReadNumeric(input, out var number))
        {
            return EventScriptFastValue.FromInteger(input.Integer);
        }

        if (number.IsNaN)
        {
            return EventScriptFastValue.FromInteger(0);
        }

        if (number.IsPositiveInfinity)
        {
            return EventScriptFastValue.FromInteger(long.MaxValue);
        }

        if (number.IsNegativeInfinity)
        {
            return EventScriptFastValue.FromInteger(long.MinValue);
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

        return EventScriptFastValue.FromInteger(EventScriptValueAlu.ToIntegerSaturated(rounded));
    }

    private static EventScriptFastValue EvaluateDegree(string functionName, EventScriptFastValue input)
        => functionName switch
        {
            "wrap" => EvaluateDegreeWrap(input),
            "toRadians" => EvaluateDegreeToRadians(input),
            "fromRadians" => EvaluateDegreeFromRadians(input),
            _ => EventScriptFastValue.Nothing
        };

    private static EventScriptFastValue EvaluateDegreeWrap(EventScriptFastValue input)
    {
        if (input.IsReferenceBacked)
        {
            return EventScriptFastValue.FromEventScriptValue(EventScriptValueAlu.EvaluateWrapDegree(input.ToEventScriptValue()));
        }

        if (input.Unit.HasValue && input.Unit.Value != EventScriptDecimalUnit.Degree)
        {
            return EventScriptFastValue.FromEventScriptValue(EventScriptValueFactory.DecimalNaN());
        }

        if (input.Kind is EventScriptValueKind.Decimal or EventScriptValueKind.Integer)
        {
            return EventScriptFastValue.FromDecimal(EventScriptValue.WrapDegrees(input.Number), EventScriptDecimalUnit.Degree);
        }

        return EventScriptFastValue.FromEventScriptValue(EventScriptValueFactory.DecimalNaN());
    }

    private static EventScriptFastValue EvaluateDegreeToRadians(EventScriptFastValue input)
    {
        if (!TryReadUnitlessOrDegreeNumeric(input, out var number) || !number.IsFinite)
        {
            return EventScriptFastValue.FromEventScriptValue(EventScriptValueFactory.DecimalNaN());
        }

        try
        {
            return EventScriptFastValue.FromDecimal(number.Value / 180m * Pi);
        }
        catch (OverflowException)
        {
            return EventScriptFastValue.FromEventScriptValue(EventScriptValueFactory.DecimalNaN());
        }
    }

    private static EventScriptFastValue EvaluateDegreeFromRadians(EventScriptFastValue input)
    {
        if (!TryReadUnitlessNumeric(input, out var number) || !number.IsFinite)
        {
            return EventScriptFastValue.FromEventScriptValue(EventScriptValueFactory.DecimalNaN());
        }

        try
        {
            return EventScriptFastValue.FromDecimal(number.Value / Pi * 180m, EventScriptDecimalUnit.Degree);
        }
        catch (OverflowException)
        {
            return EventScriptFastValue.FromEventScriptValue(EventScriptValueFactory.DecimalNaN());
        }
    }

    private static bool TryReadUnitlessOrDegreeNumeric(EventScriptFastValue input, out EventScriptValueAlu.NumericValue number)
    {
        if (input.IsReferenceBacked)
        {
            var value = input.ToEventScriptValue();
            if (!EventScriptValueAlu.TryUnwrapOptionalForOperation(value, out var unwrapped))
            {
                number = EventScriptValueAlu.NumericValue.NaN();
                return true;
            }

            if (EventScriptValue.TryGetDecimalUnit(unwrapped, out var unit) && unit != EventScriptDecimalUnit.Degree)
            {
                number = EventScriptValueAlu.NumericValue.NaN();
                return false;
            }

            return EventScriptValueAlu.TryCoerceNumericForOperation(unwrapped, out number);
        }

        if (input.Unit.HasValue && input.Unit.Value != EventScriptDecimalUnit.Degree)
        {
            number = EventScriptValueAlu.NumericValue.NaN();
            return false;
        }

        return TryReadNumeric(input, out number);
    }

    private static bool TryReadUnitlessNumeric(EventScriptFastValue input, out EventScriptValueAlu.NumericValue number)
    {
        if (input.IsReferenceBacked)
        {
            var value = input.ToEventScriptValue();
            if (!EventScriptValueAlu.TryUnwrapOptionalForOperation(value, out var unwrapped))
            {
                number = EventScriptValueAlu.NumericValue.NaN();
                return true;
            }

            if (EventScriptValue.TryGetDecimalUnit(unwrapped, out _))
            {
                number = EventScriptValueAlu.NumericValue.NaN();
                return false;
            }

            return EventScriptValueAlu.TryCoerceNumericForOperation(unwrapped, out number);
        }

        if (input.Unit.HasValue)
        {
            number = EventScriptValueAlu.NumericValue.NaN();
            return false;
        }

        return TryReadNumeric(input, out number);
    }

    private static bool TryReadNumeric(EventScriptFastValue input, out EventScriptValueAlu.NumericValue number)
    {
        if (input.IsReferenceBacked)
        {
            var value = input.ToEventScriptValue();
            if (!EventScriptValueAlu.TryUnwrapOptionalForOperation(value, out var unwrapped))
            {
                number = EventScriptValueAlu.NumericValue.NaN();
                return true;
            }

            return EventScriptValueAlu.TryCoerceNumericForOperation(unwrapped, out number);
        }

        switch (input.Kind)
        {
            case EventScriptValueKind.Decimal:
            case EventScriptValueKind.Integer:
            case EventScriptValueKind.Percentage:
            case EventScriptValueKind.Boolean:
                number = EventScriptValueAlu.NumericValue.Finite(input.Number);
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
