using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.Runtime;

internal static class GesValueOperations
{
    internal enum NumericKind
    {
        Finite,
        NaN,
        PositiveInfinity,
        NegativeInfinity
    }

    internal readonly record struct NumericValue(NumericKind Kind, decimal Value)
    {
        public bool IsFinite => Kind == NumericKind.Finite;
        public bool IsNaN => Kind == NumericKind.NaN;
        public bool IsPositiveInfinity => Kind == NumericKind.PositiveInfinity;
        public bool IsNegativeInfinity => Kind == NumericKind.NegativeInfinity;
        public bool IsInfinity => IsPositiveInfinity || IsNegativeInfinity;

        public static NumericValue Finite(decimal value) => new(NumericKind.Finite, value);
        public static NumericValue NaN() => new(NumericKind.NaN, 0m);
        public static NumericValue PositiveInfinity() => new(NumericKind.PositiveInfinity, 0m);
        public static NumericValue NegativeInfinity() => new(NumericKind.NegativeInfinity, 0m);
    }

    public static GameEventScriptValue EvaluateMinMax(IReadOnlyList<GameEventScriptValue> values, bool isMax)
    {
        if (values.All(value => TryCoerceNumericForOperation(value, out _)))
        {
            if (!TryGetCommonNumericUnit(values, out _))
            {
                return GameEventScriptValueFactory.GesDecimalNaN();
            }

            var numericBest = values[0];
            TryCoerceNumericForOperation(numericBest, out var bestNumber);
            for (var i = 1; i < values.Count; i++)
            {
                TryCoerceNumericForOperation(values[i], out var number);
                if (!TryCompareNumeric(number, bestNumber, out var comparison))
                {
                    return GameEventScriptValueFactory.GesDecimalNaN();
                }

                if ((isMax && comparison > 0) || (!isMax && comparison < 0))
                {
                    numericBest = values[i];
                    bestNumber = number;
                }
            }

            return numericBest;
        }

        var best = values[0];
        for (var i = 1; i < values.Count; i++)
        {
            var comparison = GameEventScriptValue.StableComparer.Compare(values[i], best);
            if ((isMax && comparison > 0) || (!isMax && comparison < 0))
            {
                best = values[i];
            }
        }

        return best;
    }

    public static bool TryCombineWithPlus(GameEventScriptValue left, GameEventScriptValue right, out GameEventScriptValue value)
    {
        if (left.Kind == GameEventScriptValueKind.Dictionary && right.Kind == GameEventScriptValueKind.Dictionary)
        {
            value = EvaluateDictionaryCombine(left, right);
            return true;
        }

        if (left.Kind == GameEventScriptValueKind.List && right.Kind == GameEventScriptValueKind.List)
        {
            value = GameEventScriptValueFactory.GesList(left.AsList().Concat(right.AsList()));
            return true;
        }

        if (left.Kind == GameEventScriptValueKind.List)
        {
            value = GameEventScriptValueFactory.GesList(left.AsList().Append(right));
            return true;
        }

        if (right.Kind == GameEventScriptValueKind.List)
        {
            value = GameEventScriptValueFactory.GesList(new[] { left }.Concat(right.AsList()));
            return true;
        }

        value = GameEventScriptNothingValue.Instance;
        return false;
    }

    public static GameEventScriptValue EvaluateCollectionCombine(GameEventScriptValue left, GameEventScriptValue right)
    {
        if (left.Kind == GameEventScriptValueKind.Dictionary && right.Kind == GameEventScriptValueKind.Dictionary)
        {
            return EvaluateDictionaryCombine(left, right);
        }

        if (left.Kind == GameEventScriptValueKind.Set && right.Kind == GameEventScriptValueKind.Set)
        {
            return GameEventScriptValueFactory.GseSet(left.AsSet().Concat(right.AsSet()));
        }

        if (left.Kind is GameEventScriptValueKind.List or GameEventScriptValueKind.Dice &&
            right.Kind is GameEventScriptValueKind.List or GameEventScriptValueKind.Dice)
        {
            return GameEventScriptValueFactory.GesList(left.AsList().Concat(right.AsList()));
        }

        return GameEventScriptNothingValue.Instance;
    }

    public static GameEventScriptValue EvaluateCollectionIntersect(GameEventScriptValue left, GameEventScriptValue right)
    {
        if (left.Kind == GameEventScriptValueKind.Dictionary && right.Kind == GameEventScriptValueKind.Dictionary)
        {
            var rightKeys = new HashSet<string>(right.AsDictionary().Keys, StringComparer.Ordinal);
            var map = left.AsDictionary()
                .Where(pair => rightKeys.Contains(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            return GameEventScriptValueFactory.GseDictionary(map);
        }

        if (left.Kind == GameEventScriptValueKind.Set && right.Kind == GameEventScriptValueKind.Set)
        {
            var rightSet = right.AsSet();
            return GameEventScriptValueFactory.GseSet(left.AsSet().Where(item => rightSet.Contains(item)));
        }

        if (left.Kind is GameEventScriptValueKind.List or GameEventScriptValueKind.Dice &&
            right.Kind is GameEventScriptValueKind.List or GameEventScriptValueKind.Dice)
        {
            var remaining = right.AsList().ToList();
            var result = new List<GameEventScriptValue>();
            foreach (var item in left.AsList())
            {
                var index = remaining.FindIndex(candidate => AreEqual(candidate, item));
                if (index < 0)
                {
                    continue;
                }

                result.Add(item);
                remaining.RemoveAt(index);
            }

            return GameEventScriptValueFactory.GesList(result);
        }

        return GameEventScriptNothingValue.Instance;
    }

    public static GameEventScriptValue EvaluateCollectionExcept(GameEventScriptValue left, GameEventScriptValue right)
    {
        if (left.Kind == GameEventScriptValueKind.Dictionary && right.Kind == GameEventScriptValueKind.Dictionary)
        {
            var rightKeys = new HashSet<string>(right.AsDictionary().Keys, StringComparer.Ordinal);
            var map = left.AsDictionary()
                .Where(pair => !rightKeys.Contains(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            return GameEventScriptValueFactory.GseDictionary(map);
        }

        if (left.Kind == GameEventScriptValueKind.Set && right.Kind == GameEventScriptValueKind.Set)
        {
            var rightSet = right.AsSet();
            return GameEventScriptValueFactory.GseSet(left.AsSet().Where(item => !rightSet.Contains(item)));
        }

        if (left.Kind is GameEventScriptValueKind.List or GameEventScriptValueKind.Dice &&
            right.Kind is GameEventScriptValueKind.List or GameEventScriptValueKind.Dice)
        {
            var remaining = right.AsList().ToList();
            var result = new List<GameEventScriptValue>();
            foreach (var item in left.AsList())
            {
                var index = remaining.FindIndex(candidate => AreEqual(candidate, item));
                if (index >= 0)
                {
                    remaining.RemoveAt(index);
                    continue;
                }

                result.Add(item);
            }

            return GameEventScriptValueFactory.GesList(result);
        }

        return GameEventScriptNothingValue.Instance;
    }

    public static GameEventScriptValue EvaluateCollectionZip(GameEventScriptValue left, GameEventScriptValue right)
    {
        if (left.Kind is not (GameEventScriptValueKind.List or GameEventScriptValueKind.Dice) ||
            right.Kind is not (GameEventScriptValueKind.List or GameEventScriptValueKind.Dice))
        {
            return GameEventScriptNothingValue.Instance;
        }

        var leftItems = left.AsList();
        var rightItems = right.AsList();
        var count = Math.Min(leftItems.Count, rightItems.Count);
        var zipped = new List<GameEventScriptValue>(count);
        for (var i = 0; i < count; i++)
        {
            zipped.Add(GameEventScriptValueFactory.GseDictionary(new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal)
            {
                ["left"] = leftItems[i],
                ["right"] = rightItems[i]
            }));
        }

        return GameEventScriptValueFactory.GesList(zipped);
    }

    public static GameEventScriptValue EvaluateDictionaryCombine(GameEventScriptValue left, GameEventScriptValue right)
    {
        var map = new Dictionary<string, GameEventScriptValue>(left.AsDictionary(), StringComparer.Ordinal);
        foreach (var pair in right.AsDictionary())
        {
            map[pair.Key] = pair.Value;
        }

        return GameEventScriptValueFactory.GseDictionary(map);
    }

    public static bool AreEqual(GameEventScriptValue left, GameEventScriptValue right) => left.Equals(right);

    public static bool TryUnwrapOptionalForOperation(GameEventScriptValue value, out GameEventScriptValue unwrapped)
    {
        if (!value.IsOptional())
        {
            unwrapped = value;
            return true;
        }

        var optional = value.AsOptional();
        if (!optional.HasValue)
        {
            unwrapped = default!;
            return false;
        }

        unwrapped = optional.Value;
        return true;
    }

    public static bool TryCoerceNumericForOperation(GameEventScriptValue value, out NumericValue number)
    {
        if (value.IsNothing())
        {
            number = default;
            return false;
        }

        if (value.Kind == GameEventScriptValueKind.Decimal)
        {
            if (value.IsNaN())
            {
                number = NumericValue.NaN();
                return true;
            }

            if (value.IsInfinity())
            {
                number = value.IsNegativeInfinity()
                    ? NumericValue.NegativeInfinity()
                    : NumericValue.PositiveInfinity();
                return true;
            }

            number = NumericValue.Finite(value.AsNumber());
            return true;
        }

        if (value.Kind == GameEventScriptValueKind.Integer)
        {
            number = NumericValue.Finite(value.AsInteger());
            return true;
        }

        if (value.Kind == GameEventScriptValueKind.Percentage)
        {
            number = NumericValue.Finite(value.AsNumber());
            return true;
        }

        if (value.Kind == GameEventScriptValueKind.Dice)
        {
            number = NumericValue.Finite(value.AsDice().Sum());
            return true;
        }

        if (value.IsText())
        {
            if (decimal.TryParse(value.AsText(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            {
                number = NumericValue.Finite(parsed);
                return true;
            }

            number = default;
            return false;
        }

        if (value.Kind == GameEventScriptValueKind.Boolean)
        {
            number = NumericValue.Finite(value.AsBoolean() ? 1m : 0m);
            return true;
        }

        number = default;
        return false;
    }

    public static GameEventScriptValue ToGameEventScriptDecimal(NumericValue number, GameEventScriptDecimalUnit? unit = null)
    {
        return number.Kind switch
        {
            NumericKind.Finite => GameEventScriptValueFactory.GesDecimal(number.Value, unit),
            NumericKind.NaN => GameEventScriptValueFactory.GesDecimalNaN(),
            NumericKind.PositiveInfinity => GameEventScriptValueFactory.GesDecimalInfinity(),
            NumericKind.NegativeInfinity => GameEventScriptValueFactory.GesDecimalNegativeInfinity(),
            _ => GameEventScriptValueFactory.GesDecimalNaN()
        };
    }

    public static GameEventScriptValue ToGameEventScriptNumericResult(
        GameEventScriptValue left,
        string operation,
        GameEventScriptValue right,
        NumericValue number,
        GameEventScriptDecimalUnit? unit = null)
    {
        if (unit is null &&
            operation == "div" &&
            TryToInteger(number, out var quotient))
        {
            return GameEventScriptValueFactory.GesInteger(quotient);
        }

        if (unit is null &&
            operation is "+" or "-" or "*" or "mod" or "rem" &&
            left.Kind == GameEventScriptValueKind.Integer &&
            right.Kind == GameEventScriptValueKind.Integer &&
            TryToInteger(number, out var integer))
        {
            return GameEventScriptValueFactory.GesInteger(integer);
        }

        return ToGameEventScriptDecimal(number, unit);
    }

    public static bool TryEvaluatePercentageBinary(GameEventScriptValue left, string operation, GameEventScriptValue right, out GameEventScriptValue value)
    {
        var leftIsPercentage = left.IsPercentage();
        var rightIsPercentage = right.IsPercentage();
        if (operation is not ("+" or "-" or "*" or "/") || (!leftIsPercentage && !rightIsPercentage))
        {
            value = GameEventScriptNothingValue.Instance;
            return false;
        }

        if (!TryCoerceNumericForOperation(left, out var leftNumber) ||
            !TryCoerceNumericForOperation(right, out var rightNumber))
        {
            value = GameEventScriptValueFactory.GesDecimalNaN();
            return true;
        }

        var leftHasUnit = GameEventScriptValue.TryGetDecimalUnit(left, out var leftUnit);
        var rightHasUnit = GameEventScriptValue.TryGetDecimalUnit(right, out var rightUnit);
        if (leftIsPercentage && rightHasUnit && operation is "+" or "-" or "/")
        {
            value = GameEventScriptValueFactory.GesDecimalNaN();
            return true;
        }

        if (leftIsPercentage && rightIsPercentage)
        {
            value = operation switch
            {
                "+" => ToGameEventScriptPercentage(AddNumeric(leftNumber, rightNumber)),
                "-" => ToGameEventScriptPercentage(SubtractNumeric(leftNumber, rightNumber)),
                "*" => ToGameEventScriptPercentage(MultiplyNumeric(leftNumber, rightNumber)),
                "/" => ToGameEventScriptDecimal(DivideNumeric(leftNumber, rightNumber)),
                _ => GameEventScriptValueFactory.GesDecimalNaN()
            };
            return true;
        }

        if (operation is "+" or "-")
        {
            if (leftIsPercentage)
            {
                value = GameEventScriptValueFactory.GesDecimalNaN();
                return true;
            }

            var delta = MultiplyNumeric(leftNumber, rightNumber);
            var result = operation == "+"
                ? AddNumeric(leftNumber, delta)
                : SubtractNumeric(leftNumber, delta);
            value = ToGameEventScriptDecimal(result, leftHasUnit ? leftUnit : null);
            return true;
        }

        if (operation == "*")
        {
            var result = MultiplyNumeric(leftNumber, rightNumber);
            if (leftIsPercentage)
            {
                value = rightHasUnit
                    ? ToGameEventScriptDecimal(result, rightUnit)
                    : ToGameEventScriptPercentage(result);
                return true;
            }

            value = ToGameEventScriptDecimal(result, leftHasUnit ? leftUnit : null);
            return true;
        }

        if (operation == "/")
        {
            var result = DivideNumeric(leftNumber, rightNumber);
            value = leftIsPercentage
                ? ToGameEventScriptPercentage(result)
                : ToGameEventScriptDecimal(result, leftHasUnit ? leftUnit : null);
            return true;
        }

        value = GameEventScriptNothingValue.Instance;
        return false;
    }

    public static bool TryEvaluateUnitBinary(GameEventScriptValue left, string operation, GameEventScriptValue right, out GameEventScriptValue value)
    {
        var leftHasUnit = GameEventScriptValue.TryGetDecimalUnit(left, out var leftUnit);
        var rightHasUnit = GameEventScriptValue.TryGetDecimalUnit(right, out var rightUnit);
        if (operation is not ("+" or "-" or "*" or "/" or "div" or "mod" or "rem") || (!leftHasUnit && !rightHasUnit))
        {
            value = GameEventScriptNothingValue.Instance;
            return false;
        }

        if (!TryCoerceNumericForOperation(left, out var leftNumber) ||
            !TryCoerceNumericForOperation(right, out var rightNumber))
        {
            value = GameEventScriptValueFactory.GesDecimalNaN();
            return true;
        }

        var resultUnit = default(GameEventScriptDecimalUnit?);
        var valid = operation switch
        {
            "+" or "-" => leftHasUnit && rightHasUnit && leftUnit == rightUnit && SetUnit(leftUnit, out resultUnit),
            "*" => leftHasUnit != rightHasUnit && SetUnit(leftHasUnit ? leftUnit : rightUnit, out resultUnit),
            "/" or "div" => leftHasUnit && !rightHasUnit
                ? SetUnit(leftUnit, out resultUnit)
                : leftHasUnit && rightHasUnit && leftUnit == rightUnit && SetUnit(null, out resultUnit),
            "mod" or "rem" => leftHasUnit && rightHasUnit && leftUnit == rightUnit && SetUnit(leftUnit, out resultUnit),
            _ => false
        };

        if (!valid)
        {
            value = GameEventScriptValueFactory.GesDecimalNaN();
            return true;
        }

        var result = operation switch
        {
            "+" => AddNumeric(leftNumber, rightNumber),
            "-" => SubtractNumeric(leftNumber, rightNumber),
            "*" => MultiplyNumeric(leftNumber, rightNumber),
            "/" => DivideNumeric(leftNumber, rightNumber),
            "div" => IntegerDivideNumeric(leftNumber, rightNumber),
            "mod" => ModuloNumeric(leftNumber, rightNumber),
            "rem" => RemainderNumeric(leftNumber, rightNumber),
            _ => NumericValue.NaN()
        };

        value = ToGameEventScriptNumericResult(left, operation, right, result, resultUnit);
        return true;
    }

    public static bool TryEvaluateVectorBinary(GameEventScriptValue left, string operation, GameEventScriptValue right, out GameEventScriptValue value)
    {
        var leftIsVector = TryReadVector(left, out var leftVector);
        var rightIsVector = TryReadVector(right, out var rightVector);
        if (operation is not ("+" or "-" or "*" or "/" or "div" or "mod" or "rem") || (!leftIsVector && !rightIsVector))
        {
            value = GameEventScriptNothingValue.Instance;
            return false;
        }

        if (operation is "div" or "mod" or "rem")
        {
            value = GameEventScriptValueFactory.GesDecimalNaN();
            return true;
        }

        if (operation is "+" or "-")
        {
            if (!leftIsVector || !rightIsVector)
            {
                value = operation == "+"
                    ? GameEventScriptNothingValue.Instance
                    : GameEventScriptValueFactory.GesDecimalNaN();
                return operation != "+";
            }

            if (
                leftVector.Dimension != rightVector.Dimension ||
                leftVector.Unit != rightVector.Unit)
            {
                value = GameEventScriptValueFactory.GesDecimalNaN();
                return true;
            }

            value = CreateVector(
                leftVector.Dimension,
                operation == "+"
                    ? AddNumeric(NumericValue.Finite(leftVector.X), NumericValue.Finite(rightVector.X))
                    : SubtractNumeric(NumericValue.Finite(leftVector.X), NumericValue.Finite(rightVector.X)),
                operation == "+"
                    ? AddNumeric(NumericValue.Finite(leftVector.Y), NumericValue.Finite(rightVector.Y))
                    : SubtractNumeric(NumericValue.Finite(leftVector.Y), NumericValue.Finite(rightVector.Y)),
                operation == "+"
                    ? AddNumeric(NumericValue.Finite(leftVector.Z), NumericValue.Finite(rightVector.Z))
                    : SubtractNumeric(NumericValue.Finite(leftVector.Z), NumericValue.Finite(rightVector.Z)),
                leftVector.Unit);
            return true;
        }

        if (operation == "*")
        {
            if (leftIsVector && rightIsVector)
            {
                value = GameEventScriptValueFactory.GesDecimalNaN();
                return true;
            }

            value = leftIsVector
                ? ScaleVector(leftVector, right, operation)
                : ScaleVector(rightVector, left, operation);
            return true;
        }

        if (operation == "/")
        {
            value = leftIsVector && !rightIsVector
                ? ScaleVector(leftVector, right, operation)
                : GameEventScriptValueFactory.GesDecimalNaN();
            return true;
        }

        value = GameEventScriptNothingValue.Instance;
        return false;
    }

    public static bool TryEvaluateVectorUnary(GameEventScriptValue operand, string operation, out GameEventScriptValue value)
    {
        if (!TryReadVector(operand, out var vector) || operation is not ("-" or "abs"))
        {
            value = GameEventScriptNothingValue.Instance;
            return false;
        }

        value = operation == "-"
            ? CreateVector(
                vector.Dimension,
                NegateNumeric(NumericValue.Finite(vector.X)),
                NegateNumeric(NumericValue.Finite(vector.Y)),
                NegateNumeric(NumericValue.Finite(vector.Z)),
                vector.Unit)
            : EvaluateVectorLength(vector);
        return true;
    }

    public static bool TryCreateVector2(GameEventScriptValue x, GameEventScriptValue y, out GameEventScriptValue value)
        => TryCreateVectorFromComponents([x, y], 2, out value);

    public static bool TryCreateVector3(GameEventScriptValue x, GameEventScriptValue y, GameEventScriptValue z, out GameEventScriptValue value)
        => TryCreateVectorFromComponents([x, y, z], 3, out value);

    public static bool TryCreateVectorFromLabeledComponents(
        string typeName,
        IReadOnlyDictionary<string, GameEventScriptValue> components,
        out GameEventScriptValue value)
    {
        var labels = typeName switch
        {
            "vector2" => new[] { "x", "y" },
            "vector3" => new[] { "x", "y", "z" },
            _ => []
        };

        if (labels.Length == 0)
        {
            value = GameEventScriptNothingValue.Instance;
            return false;
        }

        var values = new decimal[labels.Length];
        var unit = default(GameEventScriptDecimalUnit?);
        var initialized = false;

        foreach (var pair in components)
        {
            var index = Array.IndexOf(labels, pair.Key);
            if (index < 0)
            {
                value = GameEventScriptNothingValue.Instance;
                return false;
            }

            if (!TryReadVectorComponent(pair.Value, out values[index], out var componentUnit, out var invalid))
            {
                value = invalid ? GameEventScriptValueFactory.GesDecimalNaN() : GameEventScriptNothingValue.Instance;
                return invalid;
            }

            if (!initialized)
            {
                unit = componentUnit;
                initialized = true;
                continue;
            }

            if (unit != componentUnit)
            {
                value = GameEventScriptValueFactory.GesDecimalNaN();
                return true;
            }
        }

        value = labels.Length == 2
            ? GameEventScriptValueFactory.GesVector2(values[0], values[1], unit)
            : GameEventScriptValueFactory.GesVector3(values[0], values[1], values[2], unit);
        return true;
    }

    public static bool TryCreateVector3(GameEventScriptValue xy, GameEventScriptValue z, out GameEventScriptValue value)
    {
        if (!TryUnwrapOptionalForOperation(xy, out var unwrapped) ||
            unwrapped is not GameEventScriptVector2Value vector2)
        {
            value = GameEventScriptNothingValue.Instance;
            return false;
        }

        return TryCreateVector3(
            GameEventScriptValueFactory.GesDecimal(vector2.X, vector2.Unit),
            GameEventScriptValueFactory.GesDecimal(vector2.Y, vector2.Unit),
            z,
            out value);
    }

    public static bool TryEraseVectorUnit(GameEventScriptValue value, out GameEventScriptValue converted)
    {
        switch (value)
        {
            case GameEventScriptVector2Value vector2:
                converted = GameEventScriptValueFactory.GesVector2(vector2.X, vector2.Y);
                return true;
            case GameEventScriptVector3Value vector3:
                converted = GameEventScriptValueFactory.GesVector3(vector3.X, vector3.Y, vector3.Z);
                return true;
            default:
                converted = GameEventScriptNothingValue.Instance;
                return false;
        }
    }

    public static bool TryApplyVectorUnit(GameEventScriptValue value, GameEventScriptDecimalUnit unit, out GameEventScriptValue converted)
    {
        switch (value)
        {
            case GameEventScriptVector2Value vector2:
                converted = vector2.Unit.HasValue && vector2.Unit.Value != unit
                    ? GameEventScriptValueFactory.GesDecimalNaN()
                    : GameEventScriptValueFactory.GesVector2(vector2.X, vector2.Y, unit);
                return true;
            case GameEventScriptVector3Value vector3:
                converted = vector3.Unit.HasValue && vector3.Unit.Value != unit
                    ? GameEventScriptValueFactory.GesDecimalNaN()
                    : GameEventScriptValueFactory.GesVector3(vector3.X, vector3.Y, vector3.Z, unit);
                return true;
            default:
                converted = GameEventScriptNothingValue.Instance;
                return false;
        }
    }

    public static GameEventScriptValue EvaluateWrapDegree(GameEventScriptValue operand)
    {
        if (operand.IsNothing())
        {
            return GameEventScriptNothingValue.Instance;
        }

        if (!TryUnwrapOptionalForOperation(operand, out var unwrapped))
        {
            return GameEventScriptValueFactory.GesDecimalNaN();
        }

        if (GameEventScriptValue.TryGetDecimalUnit(unwrapped, out var unit) && unit != GameEventScriptDecimalUnit.Degree)
        {
            return GameEventScriptValueFactory.GesDecimalNaN();
        }

        if (unwrapped.Kind is GameEventScriptValueKind.Decimal or GameEventScriptValueKind.Integer &&
            TryCoerceNumericForOperation(unwrapped, out var number) &&
            number.IsFinite)
        {
            return GameEventScriptValueFactory.GesDegree(GameEventScriptValue.WrapDegrees(number.Value));
        }

        return GameEventScriptValueFactory.GesDecimalNaN();
    }

    public static bool TryCompareNumericValues(GameEventScriptValue left, GameEventScriptValue right, out int comparison)
    {
        if (!HaveCompatibleNumericUnits(left, right))
        {
            comparison = default;
            return false;
        }

        if (!TryCoerceNumericForOperation(left, out var leftNumeric) ||
            !TryCoerceNumericForOperation(right, out var rightNumeric))
        {
            comparison = default;
            return false;
        }

        return TryCompareNumeric(leftNumeric, rightNumeric, out comparison);
    }

    public static bool HaveCompatibleNumericUnits(GameEventScriptValue left, GameEventScriptValue right)
    {
        var leftHasUnit = GameEventScriptValue.TryGetDecimalUnit(left, out var leftUnit);
        var rightHasUnit = GameEventScriptValue.TryGetDecimalUnit(right, out var rightUnit);
        return leftHasUnit == rightHasUnit && (!leftHasUnit || leftUnit == rightUnit);
    }

    private static bool TryGetCommonNumericUnit(IEnumerable<GameEventScriptValue> values, out GameEventScriptDecimalUnit? unit)
    {
        unit = null;
        var initialized = false;
        foreach (var value in values)
        {
            var hasUnit = GameEventScriptValue.TryGetDecimalUnit(value, out var currentUnit);
            if (!initialized)
            {
                unit = hasUnit ? currentUnit : null;
                initialized = true;
                continue;
            }

            if (hasUnit != unit.HasValue || (hasUnit && unit.HasValue && currentUnit != unit.Value))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SetUnit(GameEventScriptDecimalUnit? value, out GameEventScriptDecimalUnit? unit)
    {
        unit = value;
        return true;
    }

    private readonly record struct VectorComponents(int Dimension, decimal X, decimal Y, decimal Z, GameEventScriptDecimalUnit? Unit);

    private static bool TryReadVector(GameEventScriptValue value, out VectorComponents vector)
    {
        switch (value)
        {
            case GameEventScriptVector2Value vector2:
                vector = new VectorComponents(2, vector2.X, vector2.Y, 0m, vector2.Unit);
                return true;
            case GameEventScriptVector3Value vector3:
                vector = new VectorComponents(3, vector3.X, vector3.Y, vector3.Z, vector3.Unit);
                return true;
            default:
                vector = default;
                return false;
        }
    }

    private static bool TryCreateVectorFromComponents(IReadOnlyList<GameEventScriptValue> components, int dimension, out GameEventScriptValue value)
    {
        var values = new decimal[dimension];
        var unit = default(GameEventScriptDecimalUnit?);
        var initialized = false;

        for (var index = 0; index < dimension; index++)
        {
            if (!TryReadVectorComponent(components[index], out values[index], out var componentUnit, out var invalid))
            {
                value = invalid ? GameEventScriptValueFactory.GesDecimalNaN() : GameEventScriptNothingValue.Instance;
                return invalid;
            }

            if (!initialized)
            {
                unit = componentUnit;
                initialized = true;
                continue;
            }

            if (unit != componentUnit)
            {
                value = GameEventScriptValueFactory.GesDecimalNaN();
                return true;
            }
        }

        value = dimension == 2
            ? GameEventScriptValueFactory.GesVector2(values[0], values[1], unit)
            : GameEventScriptValueFactory.GesVector3(values[0], values[1], values[2], unit);
        return true;
    }

    private static bool TryReadVectorComponent(
        GameEventScriptValue component,
        out decimal value,
        out GameEventScriptDecimalUnit? unit,
        out bool invalid)
    {
        value = default;
        unit = null;
        invalid = false;

        if (!TryUnwrapOptionalForOperation(component, out var unwrapped) ||
            !TryCoerceNumericForOperation(unwrapped, out var number))
        {
            return false;
        }

        if (!number.IsFinite)
        {
            invalid = true;
            return false;
        }

        value = number.Value;
        unit = GameEventScriptValue.TryGetDecimalUnit(unwrapped, out var decimalUnit) ? decimalUnit : null;
        return true;
    }

    private static GameEventScriptValue ScaleVector(VectorComponents vector, GameEventScriptValue scalar, string operation)
    {
        if (!TryCoerceNumericForOperation(scalar, out var scalarNumber) || !scalarNumber.IsFinite)
        {
            return GameEventScriptValueFactory.GesDecimalNaN();
        }

        var scalarHasUnit = GameEventScriptValue.TryGetDecimalUnit(scalar, out var scalarUnit);
        if (!TryGetVectorScalarResultUnit(vector.Unit, scalarHasUnit ? scalarUnit : null, operation, out var resultUnit))
        {
            return GameEventScriptValueFactory.GesDecimalNaN();
        }

        if (operation == "/" && scalarNumber.Value == 0m)
        {
            return GameEventScriptValueFactory.GesDecimalNaN();
        }

        var x = operation == "*"
            ? MultiplyNumeric(NumericValue.Finite(vector.X), scalarNumber)
            : DivideNumeric(NumericValue.Finite(vector.X), scalarNumber);
        var y = operation == "*"
            ? MultiplyNumeric(NumericValue.Finite(vector.Y), scalarNumber)
            : DivideNumeric(NumericValue.Finite(vector.Y), scalarNumber);
        var z = operation == "*"
            ? MultiplyNumeric(NumericValue.Finite(vector.Z), scalarNumber)
            : DivideNumeric(NumericValue.Finite(vector.Z), scalarNumber);

        return CreateVector(vector.Dimension, x, y, z, resultUnit);
    }

    private static bool TryGetVectorScalarResultUnit(
        GameEventScriptDecimalUnit? vectorUnit,
        GameEventScriptDecimalUnit? scalarUnit,
        string operation,
        out GameEventScriptDecimalUnit? resultUnit)
    {
        resultUnit = null;
        if (operation == "*")
        {
            if (vectorUnit.HasValue && scalarUnit.HasValue)
            {
                return false;
            }

            resultUnit = vectorUnit ?? scalarUnit;
            return true;
        }

        if (operation == "/")
        {
            if (!vectorUnit.HasValue && !scalarUnit.HasValue)
            {
                return true;
            }

            if (vectorUnit.HasValue && !scalarUnit.HasValue)
            {
                resultUnit = vectorUnit;
                return true;
            }

            if (vectorUnit.HasValue && scalarUnit.HasValue && vectorUnit == scalarUnit)
            {
                return true;
            }
        }

        return false;
    }

    private static GameEventScriptValue CreateVector(
        int dimension,
        NumericValue x,
        NumericValue y,
        NumericValue z,
        GameEventScriptDecimalUnit? unit)
    {
        if (!x.IsFinite || !y.IsFinite || !z.IsFinite)
        {
            return GameEventScriptValueFactory.GesDecimalNaN();
        }

        return dimension == 2
            ? GameEventScriptValueFactory.GesVector2(x.Value, y.Value, unit)
            : GameEventScriptValueFactory.GesVector3(x.Value, y.Value, z.Value, unit);
    }

    private static GameEventScriptValue EvaluateVectorLength(VectorComponents vector)
    {
        try
        {
            var squared = (double)(vector.X * vector.X + vector.Y * vector.Y + (vector.Dimension == 3 ? vector.Z * vector.Z : 0m));
            var length = Math.Sqrt(squared);
            if (double.IsNaN(length) || double.IsInfinity(length))
            {
                return GameEventScriptValueFactory.GesDecimalNaN();
            }

            return GameEventScriptValueFactory.GesDecimal((decimal)length, vector.Unit);
        }
        catch (OverflowException)
        {
            return GameEventScriptValueFactory.GesDecimalNaN();
        }
    }

    private static GameEventScriptValue ToGameEventScriptPercentage(NumericValue number)
        => number.IsFinite
            ? GameEventScriptValueFactory.GesPercentage(number.Value)
            : ToGameEventScriptDecimal(number);

    public static bool TryCompareNumeric(NumericValue left, NumericValue right, out int comparison)
    {
        if (left.IsNaN || right.IsNaN)
        {
            comparison = default;
            return false;
        }

        if (left.IsPositiveInfinity)
        {
            comparison = right.IsPositiveInfinity ? 0 : 1;
            return true;
        }

        if (left.IsNegativeInfinity)
        {
            comparison = right.IsNegativeInfinity ? 0 : -1;
            return true;
        }

        if (right.IsPositiveInfinity)
        {
            comparison = -1;
            return true;
        }

        if (right.IsNegativeInfinity)
        {
            comparison = 1;
            return true;
        }

        comparison = left.Value.CompareTo(right.Value);
        return true;
    }

    public static NumericValue AddNumeric(NumericValue left, NumericValue right)
    {
        if (left.IsNaN || right.IsNaN) return NumericValue.NaN();

        if (left.IsInfinity || right.IsInfinity)
        {
            if (left.IsPositiveInfinity && right.IsNegativeInfinity) return NumericValue.NaN();
            if (left.IsNegativeInfinity && right.IsPositiveInfinity) return NumericValue.NaN();
            if (left.IsPositiveInfinity || right.IsPositiveInfinity) return NumericValue.PositiveInfinity();
            return NumericValue.NegativeInfinity();
        }

        if (TryAddFinite(left.Value, right.Value, out var sum))
        {
            return NumericValue.Finite(sum);
        }

        if (left.Value > 0m && right.Value > 0m) return NumericValue.PositiveInfinity();
        if (left.Value < 0m && right.Value < 0m) return NumericValue.NegativeInfinity();
        return NumericValue.NaN();
    }

    public static NumericValue SubtractNumeric(NumericValue left, NumericValue right) => AddNumeric(left, NegateNumeric(right));

    public static NumericValue MultiplyNumeric(NumericValue left, NumericValue right)
    {
        if (left.IsNaN || right.IsNaN) return NumericValue.NaN();

        if ((left.IsInfinity && IsZero(right)) || (right.IsInfinity && IsZero(left)))
        {
            return NumericValue.NaN();
        }

        if (left.IsInfinity || right.IsInfinity)
        {
            return SignOf(left) * SignOf(right) >= 0
                ? NumericValue.PositiveInfinity()
                : NumericValue.NegativeInfinity();
        }

        if (TryMultiplyFinite(left.Value, right.Value, out var product))
        {
            return NumericValue.Finite(product);
        }

        return SignOf(left) * SignOf(right) >= 0
            ? NumericValue.PositiveInfinity()
            : NumericValue.NegativeInfinity();
    }

    public static NumericValue DivideNumeric(NumericValue left, NumericValue right)
    {
        if (left.IsNaN || right.IsNaN) return NumericValue.NaN();

        if (right.IsFinite && right.Value == 0m)
        {
            if (left.IsFinite && left.Value == 0m) return NumericValue.NaN();
            return SignOf(left) >= 0 ? NumericValue.PositiveInfinity() : NumericValue.NegativeInfinity();
        }

        if (left.IsInfinity && right.IsInfinity) return NumericValue.NaN();

        if (left.IsInfinity)
        {
            return SignOf(left) * SignOf(right) >= 0
                ? NumericValue.PositiveInfinity()
                : NumericValue.NegativeInfinity();
        }

        if (right.IsInfinity)
        {
            return NumericValue.Finite(0m);
        }

        if (TryDivideFinite(left.Value, right.Value, out var quotient))
        {
            return NumericValue.Finite(quotient);
        }

        return SignOf(left) * SignOf(right) >= 0
            ? NumericValue.PositiveInfinity()
            : NumericValue.NegativeInfinity();
    }

    public static NumericValue IntegerDivideNumeric(NumericValue left, NumericValue right)
    {
        var quotient = DivideNumeric(left, right);
        if (!quotient.IsFinite)
        {
            return quotient;
        }

        return NumericValue.Finite(Math.Floor(quotient.Value));
    }

    public static NumericValue ModuloNumeric(NumericValue left, NumericValue right)
    {
        if (left.IsNaN || right.IsNaN) return NumericValue.NaN();

        if (left.IsInfinity) return NumericValue.NaN();
        if (right.IsInfinity) return left.IsFinite ? NumericValue.Finite(left.Value) : NumericValue.NaN();

        if (right.Value == 0m) return NumericValue.NaN();

        if (TryModuloFinite(left.Value, right.Value, out var modulo))
        {
            return NumericValue.Finite(modulo);
        }

        return NumericValue.NaN();
    }

    public static NumericValue RemainderNumeric(NumericValue left, NumericValue right)
    {
        if (left.IsNaN || right.IsNaN) return NumericValue.NaN();

        if (left.IsInfinity) return NumericValue.NaN();
        if (right.IsInfinity) return left.IsFinite ? NumericValue.Finite(left.Value) : NumericValue.NaN();

        if (right.Value == 0m) return NumericValue.NaN();

        if (TryRemainderFinite(left.Value, right.Value, out var remainder))
        {
            return NumericValue.Finite(remainder);
        }

        return NumericValue.NaN();
    }

    public static NumericValue NegateNumeric(NumericValue value)
    {
        if (value.IsNaN) return NumericValue.NaN();
        if (value.IsPositiveInfinity) return NumericValue.NegativeInfinity();
        if (value.IsNegativeInfinity) return NumericValue.PositiveInfinity();

        if (TryNegateFinite(value.Value, out var negated))
        {
            return NumericValue.Finite(negated);
        }

        return value.Value < 0m ? NumericValue.PositiveInfinity() : NumericValue.NegativeInfinity();
    }

    public static int SignOf(NumericValue value)
    {
        if (value.IsPositiveInfinity) return 1;
        if (value.IsNegativeInfinity) return -1;
        if (!value.IsFinite) return 0;
        return value.Value.CompareTo(0m);
    }

    public static bool IsZero(NumericValue value) => value.IsFinite && value.Value == 0m;

    public static bool TryAddFinite(decimal left, decimal right, out decimal value)
    {
        try
        {
            value = left + right;
            return true;
        }
        catch (Exception exception) when (exception is OverflowException or DivideByZeroException)
        {
            value = default;
            return false;
        }
    }

    public static bool TryMultiplyFinite(decimal left, decimal right, out decimal value)
    {
        try
        {
            value = left * right;
            return true;
        }
        catch (Exception exception) when (exception is OverflowException or DivideByZeroException)
        {
            value = default;
            return false;
        }
    }

    public static bool TryDivideFinite(decimal left, decimal right, out decimal value)
    {
        try
        {
            value = left / right;
            return true;
        }
        catch (OverflowException)
        {
            value = default;
            return false;
        }
    }

    public static bool TryModuloFinite(decimal left, decimal right, out decimal value)
    {
        try
        {
            var remainder = left % right;
            if (remainder != 0m &&
                (remainder < 0m && right > 0m || remainder > 0m && right < 0m))
            {
                remainder += right;
            }

            value = remainder;
            return true;
        }
        catch (OverflowException)
        {
            value = default;
            return false;
        }
    }

    public static bool TryRemainderFinite(decimal left, decimal right, out decimal value)
    {
        try
        {
            value = left % right;
            return true;
        }
        catch (OverflowException)
        {
            value = default;
            return false;
        }
    }

    public static bool TryNegateFinite(decimal input, out decimal value)
    {
        try
        {
            value = -input;
            return true;
        }
        catch (OverflowException)
        {
            value = default;
            return false;
        }
    }

    public static string ToText(GameEventScriptValue value)
    {
        if (value.IsNothing())
        {
            return string.Empty;
        }

        return value.Kind switch
        {
            GameEventScriptValueKind.Text => value.AsText(),
            GameEventScriptValueKind.Decimal => value.ToString(),
            GameEventScriptValueKind.Integer => value.AsInteger().ToString(CultureInfo.InvariantCulture),
            GameEventScriptValueKind.Boolean => value.AsBoolean().ToString(),
            _ => value.ToString()
        };
    }

    public static long ToIntegerSaturated(decimal number)
    {
        var truncated = decimal.Truncate(number);
        return truncated switch
        {
            > long.MaxValue => long.MaxValue,
            < long.MinValue => long.MinValue,
            _ => (long)truncated
        };
    }

    private static bool TryToInteger(NumericValue number, out long integer)
    {
        if (!number.IsFinite ||
            number.Value != decimal.Truncate(number.Value) ||
            number.Value > long.MaxValue ||
            number.Value < long.MinValue)
        {
            integer = default;
            return false;
        }

        integer = (long)number.Value;
        return true;
    }
}
