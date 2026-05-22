using System;
using System.Collections.Generic;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.Runtime;

internal static class GesStandardExtensions
{
    private const double Pi = 3.1415926535897932384626433833d;

    public static bool IsStandardReference(GameEventScriptExtensionReference reference)
        => IsUnaryStandardReference(reference) || IsSeriesStandardReference(reference);

    public static bool TryInvoke(
        GameEventScriptExtensionReference reference,
        ReadOnlySpan<GameEventScriptFastValue> arguments,
        out GameEventScriptFastValue value)
    {
        value = GameEventScriptFastValue.Nothing;
        if (IsSeriesStandardReference(reference))
        {
            value = EvaluateSeries(reference, arguments);
            return true;
        }

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

    public static bool TryInvoke(
        IReadOnlyList<string> stringPool,
        IReadOnlyList<ushort> shape,
        GameEventScriptFastValue argument0,
        GameEventScriptFastValue argument1,
        int argumentCount,
        out GameEventScriptFastValue value)
    {
        value = GameEventScriptFastValue.Nothing;
        if (shape.Count < 2 ||
            !TryReadStringPool(stringPool, shape[0], out var extensionName) ||
            !TryReadStringPool(stringPool, shape[1], out var functionName) ||
            argumentCount != shape.Count - 2)
        {
            return false;
        }

        if (extensionName == "series")
        {
            switch (functionName)
            {
                case "fibonacci" when shape.Count == 2:
                    value = GameEventScriptFastValue.FromGameEventScriptValue(GameEventScriptSeriesValue.Fibonacci());
                    return true;
                case "factorial" when shape.Count == 2:
                    value = GameEventScriptFastValue.FromGameEventScriptValue(GameEventScriptSeriesValue.Factorial());
                    return true;
                case "natural" when IsNaturalSeriesSignature(stringPool, shape):
                    value = GameEventScriptFastValue.FromGameEventScriptValue(EvaluateNaturalSeries(stringPool, shape, argument0, argument1, argumentCount));
                    return true;
            }
        }

        if (shape.Count != 3 ||
            !TryReadStringPool(stringPool, shape[2], out var label) ||
            !IsUnlabeledNormalized(label))
        {
            return false;
        }

        if (extensionName == "integer" &&
            functionName is "floor" or "ceil" or "truncate" or "halfEven" or "halfUp" or "halfDown")
        {
            value = EvaluateInteger(functionName, argument0);
            return true;
        }

        if (extensionName == "degree" &&
            functionName is "wrap" or "toRadians" or "fromRadians")
        {
            value = EvaluateDegree(functionName, argument0);
            return true;
        }

        return false;
    }

    private static bool IsSeriesStandardReference(GameEventScriptExtensionReference reference)
    {
        if (reference.ExtensionName != "series")
        {
            return false;
        }

        return reference.FunctionName switch
        {
            "fibonacci" or "factorial" => reference.ArgumentLabels.Count == 0,
            "natural" => IsNaturalSeriesSignature(reference.ArgumentLabels),
            _ => false
        };
    }

    private static bool IsNaturalSeriesSignature(IReadOnlyList<string> labels)
    {
        if (labels.Count == 0)
        {
            return true;
        }

        if (labels.Count == 1)
        {
            return IsUnlabeled(labels[0]) || IsLabel(labels[0], "start") || IsLabel(labels[0], "step");
        }

        return labels.Count == 2 &&
               IsLabel(labels[0], "start") &&
               IsLabel(labels[1], "step");
    }

    private static bool IsNaturalSeriesSignature(IReadOnlyList<string> stringPool, IReadOnlyList<ushort> shape)
    {
        var labelCount = shape.Count - 2;
        if (labelCount == 0)
        {
            return true;
        }

        if (!TryReadStringPool(stringPool, shape[2], out var first))
        {
            return false;
        }

        if (labelCount == 1)
        {
            return IsUnlabeledNormalized(first) || IsLabelNormalized(first, "start") || IsLabelNormalized(first, "step");
        }

        return labelCount == 2 &&
               TryReadStringPool(stringPool, shape[3], out var second) &&
               IsLabelNormalized(first, "start") &&
               IsLabelNormalized(second, "step");
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

    private static bool IsLabel(string label, string expected)
        => string.Equals(GameEventScriptMessageSignature.NormalizeParameterName(label), expected, StringComparison.Ordinal);

    private static bool IsUnlabeledNormalized(string label)
        => string.Equals(label, GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal);

    private static bool IsLabelNormalized(string label, string expected)
        => string.Equals(label, expected, StringComparison.Ordinal);

    private static GameEventScriptFastValue EvaluateSeries(GameEventScriptExtensionReference reference, ReadOnlySpan<GameEventScriptFastValue> arguments)
    {
        var series = reference.FunctionName switch
        {
            "fibonacci" => GameEventScriptSeriesValue.Fibonacci(),
            "factorial" => GameEventScriptSeriesValue.Factorial(),
            "natural" => EvaluateNaturalSeries(reference.ArgumentLabels, arguments),
            _ => GameEventScriptNothingValue.Instance
        };

        return GameEventScriptFastValue.FromGameEventScriptValue(series);
    }

    private static GameEventScriptValue EvaluateNaturalSeries(IReadOnlyList<string> labels, ReadOnlySpan<GameEventScriptFastValue> arguments)
    {
        long start = 0;
        long step = 1;
        for (var index = 0; index < arguments.Length; index++)
        {
            var label = labels.Count > index ? labels[index] : GameEventScriptMessageSignature.UnlabeledParameterName;
            if (IsUnlabeled(label) || IsLabel(label, "start"))
            {
                start = arguments[index].Integer;
            }
            else if (IsLabel(label, "step"))
            {
                step = arguments[index].Integer;
            }
        }

        return GameEventScriptSeriesValue.Natural(start, step);
    }

    private static GameEventScriptValue EvaluateNaturalSeries(
        IReadOnlyList<string> stringPool,
        IReadOnlyList<ushort> shape,
        GameEventScriptFastValue argument0,
        GameEventScriptFastValue argument1,
        int argumentCount)
    {
        long start = 0;
        long step = 1;
        for (var index = 0; index < argumentCount; index++)
        {
            var label = TryReadStringPool(stringPool, shape[index + 2], out var resolved)
                ? resolved
                : GameEventScriptMessageSignature.UnlabeledParameterName;
            if (IsUnlabeledNormalized(label) || IsLabelNormalized(label, "start"))
            {
                start = index == 0 ? argument0.Integer : argument1.Integer;
            }
            else if (IsLabelNormalized(label, "step"))
            {
                step = index == 0 ? argument0.Integer : argument1.Integer;
            }
        }

        return GameEventScriptSeriesValue.Natural(start, step);
    }

    private static bool TryReadStringPool(IReadOnlyList<string> stringPool, int index, out string value)
    {
        if ((uint)index < (uint)stringPool.Count)
        {
            value = stringPool[index];
            return true;
        }

        value = string.Empty;
        return false;
    }

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
            "truncate" => Math.Truncate(number.Value),
            "halfEven" => Math.Round(number.Value, 0, MidpointRounding.ToEven),
            "halfUp" => Math.Round(number.Value, 0, MidpointRounding.AwayFromZero),
            "halfDown" => RoundHalfTowardZero(number.Value),
            _ => 0d
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

        if (input.Unit.HasValue && input.Unit.Value != GameEventScriptNumericUnit.Degree)
        {
            return GameEventScriptFastValue.FromGameEventScriptValue(GameEventScriptValueFactory.GesFloatNaN());
        }

        if (input.Kind is GameEventScriptValueKind.Number)
        {
            return GameEventScriptFastValue.FromFloat(GameEventScriptValue.WrapDegrees(input.Number), GameEventScriptNumericUnit.Degree);
        }

        return GameEventScriptFastValue.FromGameEventScriptValue(GameEventScriptValueFactory.GesFloatNaN());
    }

    private static GameEventScriptFastValue EvaluateDegreeToRadians(GameEventScriptFastValue input)
    {
        if (!TryReadUnitlessOrDegreeNumeric(input, out var number) || !number.IsFinite)
        {
            return GameEventScriptFastValue.FromGameEventScriptValue(GameEventScriptValueFactory.GesFloatNaN());
        }

        try
        {
            return GameEventScriptFastValue.FromFloat(number.Value / 180d * Pi);
        }
        catch (OverflowException)
        {
            return GameEventScriptFastValue.FromGameEventScriptValue(GameEventScriptValueFactory.GesFloatNaN());
        }
    }

    private static GameEventScriptFastValue EvaluateDegreeFromRadians(GameEventScriptFastValue input)
    {
        if (!TryReadUnitlessNumeric(input, out var number) || !number.IsFinite)
        {
            return GameEventScriptFastValue.FromGameEventScriptValue(GameEventScriptValueFactory.GesFloatNaN());
        }

        try
        {
            return GameEventScriptFastValue.FromFloat(number.Value / Pi * 180d, GameEventScriptNumericUnit.Degree);
        }
        catch (OverflowException)
        {
            return GameEventScriptFastValue.FromGameEventScriptValue(GameEventScriptValueFactory.GesFloatNaN());
        }
    }

    private static bool TryReadUnitlessOrDegreeNumeric(GameEventScriptFastValue input, out GesValueOperations.NumericValue number)
    {
        if (input.IsReferenceBacked)
        {
            var value = input.ToGameEventScriptValue();
            if (GameEventScriptValue.TryGetNumericUnit(value, out var unit) && unit != GameEventScriptNumericUnit.Degree)
            {
                number = GesValueOperations.NumericValue.NaN();
                return false;
            }

            return GesValueOperations.TryCoerceNumericForOperation(value, out number);
        }

        if (input.Unit.HasValue && input.Unit.Value != GameEventScriptNumericUnit.Degree)
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
            if (GameEventScriptValue.TryGetNumericUnit(value, out _))
            {
                number = GesValueOperations.NumericValue.NaN();
                return false;
            }

            return GesValueOperations.TryCoerceNumericForOperation(value, out number);
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
            return GesValueOperations.TryCoerceNumericForOperation(value, out number);
        }

        switch (input.Kind)
        {
            case GameEventScriptValueKind.Number:
            case GameEventScriptValueKind.Percentage:
            case GameEventScriptValueKind.Boolean:
                number = GesValueOperations.NumericValue.Finite(input.Number);
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
