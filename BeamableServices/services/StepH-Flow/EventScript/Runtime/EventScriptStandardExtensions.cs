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

        var input = arguments[0].ToEventScriptValue();
        var result = reference.ExtensionName switch
        {
            "integer" => EvaluateInteger(reference.FunctionName, input),
            "degree" => EvaluateDegree(reference.FunctionName, input),
            _ => null
        };

        if (result is null)
        {
            return false;
        }

        value = EventScriptFastValue.FromEventScriptValue(result);
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

    private static EventScriptValue? EvaluateInteger(string functionName, EventScriptValue input)
    {
        if (!TryReadNumeric(input, out var number))
        {
            return EventScriptValueFactory.Integer(input.AsInteger());
        }

        if (number.IsNaN)
        {
            return EventScriptValueFactory.Integer(0);
        }

        if (number.IsPositiveInfinity)
        {
            return EventScriptValueFactory.Integer(long.MaxValue);
        }

        if (number.IsNegativeInfinity)
        {
            return EventScriptValueFactory.Integer(long.MinValue);
        }

        var rounded = functionName switch
        {
            "floor" => Math.Floor(number.Value),
            "ceil" => Math.Ceiling(number.Value),
            "truncate" => decimal.Truncate(number.Value),
            "halfEven" => Math.Round(number.Value, 0, MidpointRounding.ToEven),
            "halfUp" => Math.Round(number.Value, 0, MidpointRounding.AwayFromZero),
            "halfDown" => RoundHalfTowardZero(number.Value),
            _ => (decimal?)null
        };

        return rounded.HasValue
            ? EventScriptValueFactory.Integer(EventScriptValueAlu.ToIntegerSaturated(rounded.Value))
            : null;
    }

    private static EventScriptValue? EvaluateDegree(string functionName, EventScriptValue input)
        => functionName switch
        {
            "wrap" => EventScriptValueAlu.EvaluateWrapDegree(input),
            "toRadians" => EvaluateDegreeToRadians(input),
            "fromRadians" => EvaluateDegreeFromRadians(input),
            _ => null
        };

    private static EventScriptValue EvaluateDegreeToRadians(EventScriptValue input)
    {
        if (!TryReadUnitlessOrDegreeNumeric(input, out var number) || !number.IsFinite)
        {
            return EventScriptValueFactory.DecimalNaN();
        }

        try
        {
            return EventScriptValueFactory.Decimal(number.Value / 180m * Pi);
        }
        catch (OverflowException)
        {
            return EventScriptValueFactory.DecimalNaN();
        }
    }

    private static EventScriptValue EvaluateDegreeFromRadians(EventScriptValue input)
    {
        if (!TryReadUnitlessNumeric(input, out var number) || !number.IsFinite)
        {
            return EventScriptValueFactory.DecimalNaN();
        }

        try
        {
            return EventScriptValueFactory.Degree(number.Value / Pi * 180m);
        }
        catch (OverflowException)
        {
            return EventScriptValueFactory.DecimalNaN();
        }
    }

    private static bool TryReadUnitlessOrDegreeNumeric(EventScriptValue input, out EventScriptValueAlu.NumericValue number)
    {
        if (!EventScriptValueAlu.TryUnwrapOptionalForOperation(input, out var unwrapped))
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

    private static bool TryReadUnitlessNumeric(EventScriptValue input, out EventScriptValueAlu.NumericValue number)
    {
        if (!EventScriptValueAlu.TryUnwrapOptionalForOperation(input, out var unwrapped))
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

    private static bool TryReadNumeric(EventScriptValue input, out EventScriptValueAlu.NumericValue number)
    {
        if (!EventScriptValueAlu.TryUnwrapOptionalForOperation(input, out var unwrapped))
        {
            number = EventScriptValueAlu.NumericValue.NaN();
            return true;
        }

        return EventScriptValueAlu.TryCoerceNumericForOperation(unwrapped, out number);
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
