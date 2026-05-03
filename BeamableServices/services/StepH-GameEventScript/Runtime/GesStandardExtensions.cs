using System;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.Runtime;

internal static class GesStandardExtensions
{
    private const decimal Pi = 3.1415926535897932384626433833m;

    public static bool IsStandardReference(GameEventScriptExtensionReference reference)
        => IsUnaryStandardReference(reference);

    public static bool TryInvoke(
        GameEventScriptExtensionReference reference,
        ReadOnlySpan<GameEventScriptFastValue> arguments,
        out GameEventScriptFastValue value)
    {
        value = GameEventScriptFastValue.Nothing;
        if (!IsUnaryStandardReference(reference) || arguments.Length != 1)
        {
            return false;
        }

        var result = reference.ExtensionName switch
        {
            "integer" => EvaluateInteger(reference.FunctionName, arguments[0]),
            "degree" => EvaluateDegree(reference.FunctionName, arguments[0]),
            _ => GameEventScriptFastValue.Nothing
        };

        value = result;
        return true;
    }

    private static bool IsUnaryStandardReference(GameEventScriptExtensionReference reference)
        => reference.ArgumentLabels.Count == 1 &&
           IsUnlabeled(reference.ArgumentLabels[0]) &&
           ((reference.ExtensionName == "integer" &&
             reference.FunctionName is "floor" or "ceil" or "truncate" or "halfEven" or "halfUp" or "halfDown") ||
            (reference.ExtensionName == "degree" &&
             reference.FunctionName is "wrap" or "toRadians" or "fromRadians"));

    private static bool IsUnlabeled(string label)
        => string.Equals(
            GameEventScriptMessageSignature.NormalizeParameterName(label),
            GameEventScriptMessageSignature.UnlabeledParameterName,
            StringComparison.Ordinal);

    private static GameEventScriptFastValue EvaluateInteger(string functionName, GameEventScriptFastValue input)
    {
        if (!TryReadNumeric(input, out var number))
        {
            return GameEventScriptFastValue.FromInteger(input.Integer);
        }

        if (number.IsNaN)
        {
            return GameEventScriptFastValue.FromInteger(0);
        }

        if (number.IsPositiveInfinity)
        {
            return GameEventScriptFastValue.FromInteger(long.MaxValue);
        }

        if (number.IsNegativeInfinity)
        {
            return GameEventScriptFastValue.FromInteger(long.MinValue);
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

        return GameEventScriptFastValue.FromInteger(GesValueOperations.ToIntegerSaturated(rounded));
    }

    private static GameEventScriptFastValue EvaluateDegree(string functionName, GameEventScriptFastValue input)
        => functionName switch
        {
            "wrap" => EvaluateDegreeWrap(input),
            "toRadians" => EvaluateDegreeToRadians(input),
            "fromRadians" => EvaluateDegreeFromRadians(input),
            _ => GameEventScriptFastValue.Nothing
        };

    private static GameEventScriptFastValue EvaluateDegreeWrap(GameEventScriptFastValue input)
    {
        if (input.IsReferenceBacked)
        {
            return GameEventScriptFastValue.FromGameEventScriptValue(GesValueOperations.EvaluateWrapDegree(input.ToGameEventScriptValue()));
        }

        if (input.Unit.HasValue && input.Unit.Value != GameEventScriptDecimalUnit.Degree)
        {
            return GameEventScriptFastValue.FromGameEventScriptValue(GameEventScriptValueFactory.GesDecimalNaN());
        }

        if (input.Kind is GameEventScriptValueKind.Decimal or GameEventScriptValueKind.Integer)
        {
            return GameEventScriptFastValue.FromDecimal(GameEventScriptValue.WrapDegrees(input.Number), GameEventScriptDecimalUnit.Degree);
        }

        return GameEventScriptFastValue.FromGameEventScriptValue(GameEventScriptValueFactory.GesDecimalNaN());
    }

    private static GameEventScriptFastValue EvaluateDegreeToRadians(GameEventScriptFastValue input)
    {
        if (!TryReadUnitlessOrDegreeNumeric(input, out var number) || !number.IsFinite)
        {
            return GameEventScriptFastValue.FromGameEventScriptValue(GameEventScriptValueFactory.GesDecimalNaN());
        }

        try
        {
            return GameEventScriptFastValue.FromDecimal(number.Value / 180m * Pi);
        }
        catch (OverflowException)
        {
            return GameEventScriptFastValue.FromGameEventScriptValue(GameEventScriptValueFactory.GesDecimalNaN());
        }
    }

    private static GameEventScriptFastValue EvaluateDegreeFromRadians(GameEventScriptFastValue input)
    {
        if (!TryReadUnitlessNumeric(input, out var number) || !number.IsFinite)
        {
            return GameEventScriptFastValue.FromGameEventScriptValue(GameEventScriptValueFactory.GesDecimalNaN());
        }

        try
        {
            return GameEventScriptFastValue.FromDecimal(number.Value / Pi * 180m, GameEventScriptDecimalUnit.Degree);
        }
        catch (OverflowException)
        {
            return GameEventScriptFastValue.FromGameEventScriptValue(GameEventScriptValueFactory.GesDecimalNaN());
        }
    }

    private static bool TryReadUnitlessOrDegreeNumeric(GameEventScriptFastValue input, out GesValueOperations.NumericValue number)
    {
        if (input.IsReferenceBacked)
        {
            var value = input.ToGameEventScriptValue();
            if (!GesValueOperations.TryUnwrapOptionalForOperation(value, out var unwrapped))
            {
                number = GesValueOperations.NumericValue.NaN();
                return true;
            }

            if (GameEventScriptValue.TryGetDecimalUnit(unwrapped, out var unit) && unit != GameEventScriptDecimalUnit.Degree)
            {
                number = GesValueOperations.NumericValue.NaN();
                return false;
            }

            return GesValueOperations.TryCoerceNumericForOperation(unwrapped, out number);
        }

        if (input.Unit.HasValue && input.Unit.Value != GameEventScriptDecimalUnit.Degree)
        {
            number = GesValueOperations.NumericValue.NaN();
            return false;
        }

        return TryReadNumeric(input, out number);
    }

    private static bool TryReadUnitlessNumeric(GameEventScriptFastValue input, out GesValueOperations.NumericValue number)
    {
        if (input.IsReferenceBacked)
        {
            var value = input.ToGameEventScriptValue();
            if (!GesValueOperations.TryUnwrapOptionalForOperation(value, out var unwrapped))
            {
                number = GesValueOperations.NumericValue.NaN();
                return true;
            }

            if (GameEventScriptValue.TryGetDecimalUnit(unwrapped, out _))
            {
                number = GesValueOperations.NumericValue.NaN();
                return false;
            }

            return GesValueOperations.TryCoerceNumericForOperation(unwrapped, out number);
        }

        if (input.Unit.HasValue)
        {
            number = GesValueOperations.NumericValue.NaN();
            return false;
        }

        return TryReadNumeric(input, out number);
    }

    private static bool TryReadNumeric(GameEventScriptFastValue input, out GesValueOperations.NumericValue number)
    {
        if (input.IsReferenceBacked)
        {
            var value = input.ToGameEventScriptValue();
            if (!GesValueOperations.TryUnwrapOptionalForOperation(value, out var unwrapped))
            {
                number = GesValueOperations.NumericValue.NaN();
                return true;
            }

            return GesValueOperations.TryCoerceNumericForOperation(unwrapped, out number);
        }

        switch (input.Kind)
        {
            case GameEventScriptValueKind.Decimal:
            case GameEventScriptValueKind.Integer:
            case GameEventScriptValueKind.Percentage:
            case GameEventScriptValueKind.Boolean:
                number = GesValueOperations.NumericValue.Finite(input.Number);
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
