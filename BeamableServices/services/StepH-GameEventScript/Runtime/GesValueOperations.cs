using System;
using System.Collections.Generic;
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

    internal readonly record struct NumericValue(NumericKind Kind, double Value)
    {
        public bool IsFinite => Kind == NumericKind.Finite;
        public bool IsNaN => Kind == NumericKind.NaN;
        public bool IsPositiveInfinity => Kind == NumericKind.PositiveInfinity;
        public bool IsNegativeInfinity => Kind == NumericKind.NegativeInfinity;
        public bool IsInfinity => IsPositiveInfinity || IsNegativeInfinity;

        public static NumericValue Finite(double value) => new(NumericKind.Finite, value);
        public static NumericValue NaN() => new(NumericKind.NaN, 0d);
        public static NumericValue PositiveInfinity() => new(NumericKind.PositiveInfinity, 0d);
        public static NumericValue NegativeInfinity() => new(NumericKind.NegativeInfinity, 0d);
    }

    public static GameEventScriptValue EvaluateMinMax(IReadOnlyList<GameEventScriptValue> values, bool isMax)
    {
        if (values.All(value => TryCoerceNumericForOperation(value, out _)))
        {
            if (!TryGetCommonNumericUnit(values, out _))
            {
                return GameEventScriptValueFactory.GesFloatNaN();
            }

            var numericBest = values[0];
            TryCoerceNumericForOperation(numericBest, out var bestNumber);
            for (var i = 1; i < values.Count; i++)
            {
                TryCoerceNumericForOperation(values[i], out var number);
                if (!TryCompareNumeric(number, bestNumber, out var comparison))
                {
                    return GameEventScriptValueFactory.GesFloatNaN();
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

    public static GameEventScriptValue EvaluateMinMax(GameEventScriptValue left, GameEventScriptValue right, bool isMax)
    {
        if (TryCoerceNumericForOperation(left, out var leftNumber) &&
            TryCoerceNumericForOperation(right, out var rightNumber))
        {
            if (!HaveCompatibleNumericUnits(left, right) || !TryCompareNumeric(rightNumber, leftNumber, out var numericComparison)) return GameEventScriptValueFactory.GesFloatNaN();
            return (isMax && numericComparison > 0) || (!isMax && numericComparison < 0) ? right : left;
        }

        var comparison = GameEventScriptValue.StableComparer.Compare(right, left);
        return (isMax && comparison > 0) || (!isMax && comparison < 0) ? right : left;
    }

    public static bool TryCombineWithPlus(GameEventScriptValue left, GameEventScriptValue right, out GameEventScriptValue value)
    {
        if (left.IsNothing() || right.IsNothing())
        {
            value = GameEventScriptNothingValue.Instance;
            return true;
        }

        if (left.IsText() || right.IsText())
        {
            value = GameEventScriptValueFactory.GesText(ToText(left) + ToText(right));
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

        if (left.Kind == GameEventScriptValueKind.Dice && IsUnitlessInteger(right, out var rightRoll) && rightRoll > 0)
        {
            value = GameEventScriptValueFactory.GesDice(left.AsDice().Rolls.Append(rightRoll));
            return true;
        }

        if (right.Kind == GameEventScriptValueKind.Dice && IsUnitlessInteger(left, out var leftRoll) && leftRoll > 0)
        {
            value = GameEventScriptValueFactory.GesDice(new[] { leftRoll }.Concat(right.AsDice().Rolls));
            return true;
        }

        if (left.Kind == GameEventScriptValueKind.Dice || right.Kind == GameEventScriptValueKind.Dice || left.Kind == GameEventScriptValueKind.Map || right.Kind == GameEventScriptValueKind.Map)
        {
            value = GameEventScriptNothingValue.Instance;
            return true;
        }

        value = GameEventScriptNothingValue.Instance;
        return false;
    }

    public static bool TrySubtractWithMinus(GameEventScriptValue left, GameEventScriptValue right, out GameEventScriptValue value)
    {
        if (left.IsNothing() || right.IsNothing())
        {
            value = GameEventScriptNothingValue.Instance;
            return true;
        }

        if (left.Kind == GameEventScriptValueKind.List)
        {
            if (right.Kind == GameEventScriptValueKind.Map)
            {
                value = GameEventScriptNothingValue.Instance;
                return true;
            }

            var remaining = right.Kind is GameEventScriptValueKind.List or GameEventScriptValueKind.Dice ? right.AsList().ToList() : [right];
            value = GameEventScriptValueFactory.GesList(MultisetSubtract(left.AsList(), remaining));
            return true;
        }

        if (left.Kind == GameEventScriptValueKind.Dice)
        {
            if (right.Kind == GameEventScriptValueKind.Dice)
            {
                value = GameEventScriptValueFactory.GesDice(MultisetSubtract(left.AsList(), right.AsList()).Select(item => checked((int)item.AsInteger())));
                return true;
            }

            if (right.Kind == GameEventScriptValueKind.List)
            {
                value = GameEventScriptValueFactory.GesList(MultisetSubtract(left.AsList(), right.AsList()));
                return true;
            }

            if (IsUnitlessInteger(right, out var roll) && roll > 0)
            {
                value = GameEventScriptValueFactory.GesDice(MultisetSubtract(left.AsList(), [GameEventScriptValueFactory.GesInteger(roll)]).Select(item => checked((int)item.AsInteger())));
                return true;
            }

            value = GameEventScriptNothingValue.Instance;
            return true;
        }

        if (left.Kind == GameEventScriptValueKind.Map)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            if (right.Kind == GameEventScriptValueKind.Map)
            {
                keys.UnionWith(right.AsMap().Keys);
            }
            else if (right.Kind is GameEventScriptValueKind.Text or GameEventScriptValueKind.Tag)
            {
                keys.Add(right.AsText());
            }
            else if (right.Kind == GameEventScriptValueKind.List)
            {
                foreach (var item in right.AsList())
                {
                    if (item.Kind is not (GameEventScriptValueKind.Text or GameEventScriptValueKind.Tag))
                    {
                        value = GameEventScriptNothingValue.Instance;
                        return true;
                    }

                    keys.Add(item.AsText());
                }
            }
            else
            {
                value = GameEventScriptNothingValue.Instance;
                return true;
            }

            var map = left.AsMap().Where(pair => !keys.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            value = GameEventScriptValueFactory.GesMap(map);
            return true;
        }

        if (right.Kind is GameEventScriptValueKind.List or GameEventScriptValueKind.Dice)
        {
            value = GameEventScriptNothingValue.Instance;
            return true;
        }

        if (right.Kind == GameEventScriptValueKind.Map)
        {
            value = GameEventScriptNothingValue.Instance;
            return true;
        }

        value = GameEventScriptNothingValue.Instance;
        return false;
    }

    public static GameEventScriptValue EvaluateCollectionUnion(GameEventScriptValue left, GameEventScriptValue right)
    {
        if (left.IsNothing() || right.IsNothing()) return GameEventScriptNothingValue.Instance;
        switch (left.Kind)
        {
            case GameEventScriptValueKind.Map when right.Kind == GameEventScriptValueKind.Map: return EvaluateDictionaryCombine(left, right);
            case GameEventScriptValueKind.Map:
            {
                if (!TryReadKeyList(right, out var keys)) return GameEventScriptNothingValue.Instance;
                var map = new Dictionary<string, GameEventScriptValue>(left.AsMap(), StringComparer.Ordinal);
                foreach (var key in keys) map.TryAdd(key, GameEventScriptValueFactory.GesBoolean(true));
                return GameEventScriptValueFactory.GesMap(map);
            }
            case GameEventScriptValueKind.List when right.Kind == GameEventScriptValueKind.List:
            case GameEventScriptValueKind.List when right.Kind == GameEventScriptValueKind.Dice:
            case GameEventScriptValueKind.Dice when right.Kind == GameEventScriptValueKind.List: return GameEventScriptValueFactory.GesList(left.AsList().Concat(right.AsList()));
            case GameEventScriptValueKind.Dice when right.Kind == GameEventScriptValueKind.Dice: return GameEventScriptValueFactory.GesDice(left.AsDice().Rolls.Concat(right.AsDice().Rolls));
            default: return GameEventScriptNothingValue.Instance;
        }
    }

    public static GameEventScriptValue EvaluateCollectionIntersect(GameEventScriptValue left, GameEventScriptValue right)
    {
        if (left.IsNothing() || right.IsNothing()) return GameEventScriptNothingValue.Instance;
        switch (left.Kind)
        {
            case GameEventScriptValueKind.Map when right.Kind == GameEventScriptValueKind.Map:
            {
                var rightKeys = new HashSet<string>(right.AsMap().Keys, StringComparer.Ordinal);
                var map = left.AsMap()
                    .Where(pair => rightKeys.Contains(pair.Key))
                    .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
                return GameEventScriptValueFactory.GesMap(map);
            }
            case GameEventScriptValueKind.Map when TryReadKeyList(right, out var keys):
            {
                var keySet = new HashSet<string>(keys, StringComparer.Ordinal);
                var map = left.AsMap()
                    .Where(pair => keySet.Contains(pair.Key))
                    .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
                return GameEventScriptValueFactory.GesMap(map);
            }
            case GameEventScriptValueKind.List or GameEventScriptValueKind.Dice when
                right.Kind is GameEventScriptValueKind.List or GameEventScriptValueKind.Dice:
            {
                var result = MultisetIntersect(left.AsList(), right.AsList());
                return left.Kind == GameEventScriptValueKind.Dice && right.Kind == GameEventScriptValueKind.Dice
                    ? GameEventScriptValueFactory.GesDice(result.Select(item => checked((int)item.AsInteger())))
                    : GameEventScriptValueFactory.GesList(result);
            }
            default:
                return GameEventScriptNothingValue.Instance;
        }
    }

    public static GameEventScriptValue EvaluateCollectionZip(GameEventScriptValue left, GameEventScriptValue right)
    {
        if (left.Kind != GameEventScriptValueKind.List || right.Kind != GameEventScriptValueKind.List) return GameEventScriptNothingValue.Instance;
        var leftItems = left.AsList();
        var rightItems = right.AsList();
        var count = Math.Min(leftItems.Count, rightItems.Count);
        var zipped = new List<GameEventScriptValue>(count);
        for (var i = 0; i < count; i++)
        {
            zipped.Add(GameEventScriptValueFactory.GesMap(new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal)
            {
                ["left"] = leftItems[i],
                ["right"] = rightItems[i]
            }));
        }

        return GameEventScriptValueFactory.GesList(zipped);
    }

    public static GameEventScriptValue EvaluateDictionaryCombine(GameEventScriptValue left, GameEventScriptValue right)
    {
        var map = new Dictionary<string, GameEventScriptValue>(left.AsMap(), StringComparer.Ordinal);
        foreach (var pair in right.AsMap()) map[pair.Key] = pair.Value;
        return GameEventScriptValueFactory.GesMap(map);
    }

    private static List<GameEventScriptValue> MultisetSubtract(IReadOnlyList<GameEventScriptValue> left, IReadOnlyList<GameEventScriptValue> right)
    {
        var remaining = right.ToList();
        var result = new List<GameEventScriptValue>();
        foreach (var item in left)
        {
            var index = remaining.FindIndex(candidate => AreEqual(candidate, item));
            if (index >= 0)
            {
                remaining.RemoveAt(index);
                continue;
            }

            result.Add(item);
        }

        return result;
    }

    private static List<GameEventScriptValue> MultisetIntersect(IReadOnlyList<GameEventScriptValue> left, IReadOnlyList<GameEventScriptValue> right)
    {
        var remaining = right.ToList();
        var result = new List<GameEventScriptValue>();
        foreach (var item in left)
        {
            var index = remaining.FindIndex(candidate => AreEqual(candidate, item));
            if (index < 0) continue;
            result.Add(item);
            remaining.RemoveAt(index);
        }

        return result;
    }

    private static bool TryReadKeyList(GameEventScriptValue value, out IReadOnlyList<string> keys)
    {
        if (value.Kind != GameEventScriptValueKind.List)
        {
            keys = [];
            return false;
        }

        var result = new List<string>();
        foreach (var item in value.AsList())
        {
            if (item.Kind is not (GameEventScriptValueKind.Text or GameEventScriptValueKind.Tag))
            {
                keys = [];
                return false;
            }

            result.Add(item.AsText());
        }

        keys = result;
        return true;
    }

    private static bool IsUnitlessInteger(GameEventScriptValue value, out int integer)
    {
        if (value is GameEventScriptNumberValue { IsIntegerValue: true } && !value.HasNumericUnit() && value.AsInteger() is >= int.MinValue and <= int.MaxValue)
        {
            integer = (int)value.AsInteger();
            return true;
        }

        integer = 0;
        return false;
    }

    public static bool AreEqual(GameEventScriptValue left, GameEventScriptValue right)
    {
        if (!HaveCompatibleNumericUnits(left, right)) return false;
        if (!TryCoerceNumericForOperation(left, out var leftNumeric) || !TryCoerceNumericForOperation(right, out var rightNumeric)) return left.Equals(right);
        if (leftNumeric.IsNaN || rightNumeric.IsNaN) return false;
        if (leftNumeric.IsInfinity || rightNumeric.IsInfinity) return leftNumeric.Kind == rightNumeric.Kind;
        return AreEqualWithinTwoUlps(leftNumeric.Value, rightNumeric.Value);
    }

    private static bool AreEqualWithinTwoUlps(double left, double right)
    {
        var leftBits = BitConverter.DoubleToInt64Bits(left);
        var rightBits = BitConverter.DoubleToInt64Bits(right);
        if (leftBits == rightBits) return true;
        if (leftBits < 0) leftBits = long.MinValue - leftBits;
        if (rightBits < 0) rightBits = long.MinValue - rightBits;
        return (leftBits > rightBits ? (ulong)(leftBits - rightBits) : (ulong)(rightBits - leftBits)) <= 2;
    }

    public static bool TryCoerceNumericForOperation(GameEventScriptValue value, out NumericValue number)
    {
        if (value.IsNothing())
        {
            number = default;
            return false;
        }

        if (value is GameEventScriptTagValue && value.TryConvertToNumber(out var convertedTag)) return TryCoerceNumericForOperation(convertedTag, out number);
        switch (value.Kind)
        {
            case GameEventScriptValueKind.Number when value.IsNaN():
                number = NumericValue.NaN();
                return true;
            case GameEventScriptValueKind.Number when value.IsInfinity():
                number = value.IsNegativeInfinity() ? NumericValue.NegativeInfinity() : NumericValue.PositiveInfinity();
                return true;
            case GameEventScriptValueKind.Number:
                number = NumericValue.Finite(value.AsNumber());
                return true;
            case GameEventScriptValueKind.Percentage:
                number = NumericValue.Finite(value.AsNumber());
                return true;
            case GameEventScriptValueKind.Dice:
                number = NumericValue.Finite(value.AsDice().Sum());
                return true;
            case GameEventScriptValueKind.Boolean:
                number = NumericValue.Finite(value.AsBoolean() ? 1d : 0d);
                return true;
            default:
                number = default;
                return false;
        }
    }

    public static GameEventScriptValue ToGameEventScriptFloat(NumericValue number, GameEventScriptBytecodeInstructionUnit? unit = null) => number.Kind switch
    {
        NumericKind.Finite => GameEventScriptValueFactory.GesFloat(number.Value, unit),
        NumericKind.NaN => GameEventScriptValueFactory.GesFloatNaN(),
        NumericKind.PositiveInfinity => GameEventScriptValueFactory.GesFloatInfinity(),
        NumericKind.NegativeInfinity => GameEventScriptValueFactory.GesFloatNegativeInfinity(),
        _ => GameEventScriptValueFactory.GesFloatNaN()
    };

    public static GameEventScriptValue ToGameEventScriptNumber(NumericValue number, GameEventScriptBytecodeInstructionUnit? unit = null)
        => TryToInteger(number, out var integer) ? GameEventScriptValueFactory.GesInteger(integer, unit) : ToGameEventScriptFloat(number, unit);

    public static GameEventScriptValue ToGameEventScriptNumericResult(GameEventScriptValue left, string operation, GameEventScriptValue right, NumericValue number, GameEventScriptBytecodeInstructionUnit? unit = null)
    {
        _ = left;
        _ = operation;
        _ = right;
        return ToGameEventScriptNumber(number, unit);
    }

    public static bool TryEvaluateIntegerBinary(GameEventScriptValue left, string operation, GameEventScriptValue right, out GameEventScriptValue value)
    {
        if (left is not GameEventScriptNumberValue { IsIntegerValue: true } leftIntegerValue || right is not GameEventScriptNumberValue { IsIntegerValue: true } rightIntegerValue)
        {
            value = GameEventScriptNothingValue.Instance;
            return false;
        }

        var leftInteger = leftIntegerValue.IntegerValue;
        var rightInteger = rightIntegerValue.IntegerValue;
        switch (operation)
        {
            case "+":
                if (leftIntegerValue.Unit == rightIntegerValue.Unit && TryAddInteger(leftInteger, rightInteger, out var sum))
                {
                    value = GameEventScriptValueFactory.GesInteger(sum, leftIntegerValue.Unit);
                    return true;
                }

                break;
            case "-":
                if (leftIntegerValue.Unit == rightIntegerValue.Unit && TrySubtractInteger(leftInteger, rightInteger, out var difference))
                {
                    value = GameEventScriptValueFactory.GesInteger(difference, leftIntegerValue.Unit);
                    return true;
                }

                break;
            case "*":
                if (!(leftIntegerValue.Unit.IsNumericUnit() && rightIntegerValue.Unit.IsNumericUnit()) && TryMultiplyInteger(leftInteger, rightInteger, out var product))
                {
                    value = GameEventScriptValueFactory.GesInteger(product, leftIntegerValue.Unit.IsNumericUnit() ? leftIntegerValue.Unit : rightIntegerValue.Unit);
                    return true;
                }

                break;
            case "div":
                if (TryGetDivideResultUnit(leftIntegerValue.Unit, rightIntegerValue.Unit, out var integerDivideUnit) && rightInteger != 0 && !(leftInteger == long.MinValue && rightInteger == -1))
                {
                    var quotient = leftInteger / rightInteger;
                    var remainder = leftInteger % rightInteger;
                    if (remainder != 0 && (remainder > 0) != (rightInteger > 0)) quotient--;
                    value = GameEventScriptValueFactory.GesInteger(quotient, integerDivideUnit);
                    return true;
                }

                break;
            case "mod":
                if (leftIntegerValue.Unit.IsNumericUnit() == rightIntegerValue.Unit.IsNumericUnit() && (!leftIntegerValue.Unit.IsNumericUnit() || leftIntegerValue.Unit == rightIntegerValue.Unit) && rightInteger != 0 &&
                    !(leftInteger == long.MinValue && rightInteger == -1))
                {
                    var modulo = leftInteger % rightInteger;
                    if (modulo != 0 &&
                        (modulo < 0 && rightInteger > 0 || modulo > 0 && rightInteger < 0))
                    {
                        modulo += rightInteger;
                    }

                    value = GameEventScriptValueFactory.GesInteger(modulo, leftIntegerValue.Unit);
                    return true;
                }

                break;
            case "rem":
                if (leftIntegerValue.Unit.IsNumericUnit() == rightIntegerValue.Unit.IsNumericUnit() && (!leftIntegerValue.Unit.IsNumericUnit() || leftIntegerValue.Unit == rightIntegerValue.Unit) && rightInteger != 0 &&
                    !(leftInteger == long.MinValue && rightInteger == -1))
                {
                    value = GameEventScriptValueFactory.GesInteger(leftInteger % rightInteger, leftIntegerValue.Unit);
                    return true;
                }

                break;
        }

        value = GameEventScriptNothingValue.Instance;
        return false;
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

        if (!TryCoerceNumericForOperation(left, out var leftNumber) || !TryCoerceNumericForOperation(right, out var rightNumber))
        {
            value = GameEventScriptValueFactory.GesFloatNaN();
            return true;
        }

        var leftHasUnit = GameEventScriptValue.TryGetNumericUnit(left, out var leftUnit);
        var rightHasUnit = GameEventScriptValue.TryGetNumericUnit(right, out var rightUnit);
        switch (leftIsPercentage)
        {
            case true when rightHasUnit && operation is "+" or "-" or "/":
                value = GameEventScriptValueFactory.GesFloatNaN();
                return true;
            case true when rightIsPercentage:
                value = operation switch
                {
                    "+" => ToGameEventScriptPercentage(AddNumeric(leftNumber, rightNumber)),
                    "-" => ToGameEventScriptPercentage(SubtractNumeric(leftNumber, rightNumber)),
                    "*" => ToGameEventScriptPercentage(MultiplyNumeric(leftNumber, rightNumber)),
                    "/" => ToGameEventScriptNumber(DivideNumeric(leftNumber, rightNumber)),
                    _ => GameEventScriptValueFactory.GesFloatNaN()
                };
                return true;
        }

        switch (operation)
        {
            case "+" or "-" when leftIsPercentage:
                value = GameEventScriptValueFactory.GesFloatNaN();
                return true;
            case "+" or "-":
            {
                var delta = MultiplyNumeric(leftNumber, rightNumber);
                var result = operation == "+"
                    ? AddNumeric(leftNumber, delta)
                    : SubtractNumeric(leftNumber, delta);
                value = ToGameEventScriptNumber(result, leftHasUnit ? leftUnit : null);
                return true;
            }
            case "*":
            {
                var result = MultiplyNumeric(leftNumber, rightNumber);
                if (leftIsPercentage)
                {
                    value = rightHasUnit
                        ? ToGameEventScriptFloat(result, rightUnit)
                        : ToGameEventScriptNumber(result);
                    return true;
                }

                value = ToGameEventScriptNumber(result, leftHasUnit ? leftUnit : null);
                return true;
            }
            case "/":
            {
                var result = DivideNumeric(leftNumber, rightNumber);
                value = leftIsPercentage
                    ? ToGameEventScriptPercentage(result)
                    : ToGameEventScriptNumber(result, leftHasUnit ? leftUnit : null);
                return true;
            }
            default:
                value = GameEventScriptNothingValue.Instance;
                return false;
        }
    }

    public static bool TryEvaluateUnitBinary(GameEventScriptValue left, string operation, GameEventScriptValue right, out GameEventScriptValue value)
    {
        var leftHasUnit = GameEventScriptValue.TryGetNumericUnit(left, out var leftUnit);
        var rightHasUnit = GameEventScriptValue.TryGetNumericUnit(right, out var rightUnit);
        if (operation is not ("+" or "-" or "*" or "/" or "div" or "mod" or "rem" or "^") || (!leftHasUnit && !rightHasUnit))
        {
            value = GameEventScriptNothingValue.Instance;
            return false;
        }

        if (!TryCoerceNumericForOperation(left, out var leftNumber) || !TryCoerceNumericForOperation(right, out var rightNumber))
        {
            value = GameEventScriptValueFactory.GesFloatNaN();
            return true;
        }

        var resultUnit = default(GameEventScriptBytecodeInstructionUnit?);
        var valid = operation switch
        {
            "+" or "-" => leftHasUnit && rightHasUnit && leftUnit == rightUnit && SetUnit(leftUnit, out resultUnit),
            "*" => leftHasUnit != rightHasUnit && SetUnit(leftHasUnit ? leftUnit : rightUnit, out resultUnit),
            "/" or "div" => leftHasUnit && !rightHasUnit
                ? SetUnit(leftUnit, out resultUnit)
                : leftHasUnit && rightHasUnit && leftUnit == rightUnit && SetUnit(null, out resultUnit),
            "mod" or "rem" => leftHasUnit && rightHasUnit && leftUnit == rightUnit && SetUnit(leftUnit, out resultUnit),
            "^" => leftHasUnit && !rightHasUnit && rightNumber is { IsFinite: true, Value: 0d or 1d } &&
                   SetUnit(rightNumber.Value == 1d ? leftUnit : null, out resultUnit),
            _ => false
        };

        if (!valid)
        {
            value = GameEventScriptValueFactory.GesFloatNaN();
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
            "^" => PowerNumeric(leftNumber, rightNumber),
            _ => NumericValue.NaN()
        };

        value = ToGameEventScriptNumericResult(left, operation, right, result, resultUnit);
        return true;
    }

    private static bool TryGetDivideResultUnit(GameEventScriptBytecodeInstructionUnit? leftUnit, GameEventScriptBytecodeInstructionUnit? rightUnit, out GameEventScriptBytecodeInstructionUnit? resultUnit)
    {
        leftUnit = ToOptionalNumericUnit(leftUnit);
        rightUnit = ToOptionalNumericUnit(rightUnit);
        if (!leftUnit.HasValue && !rightUnit.HasValue)
        {
            resultUnit = null;
            return true;
        }

        if (leftUnit.HasValue && !rightUnit.HasValue)
        {
            resultUnit = leftUnit;
            return true;
        }

        if (leftUnit.HasValue && rightUnit.HasValue && leftUnit == rightUnit)
        {
            resultUnit = null;
            return true;
        }

        resultUnit = null;
        return false;
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

        switch (operation)
        {
            case "div" or "mod" or "rem":
                value = GameEventScriptValueFactory.GesFloatNaN();
                return true;
            case "+" or "-" when !leftIsVector || !rightIsVector:
                value = operation == "+"
                    ? GameEventScriptNothingValue.Instance
                    : GameEventScriptValueFactory.GesFloatNaN();
                return operation != "+";
            case "+" or "-" when leftVector.Unit != rightVector.Unit:
                value = GameEventScriptValueFactory.GesFloatNaN();
                return true;
            case "+" or "-":
                value = CreateVector(
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
            case "*" when leftIsVector && rightIsVector:
                value = GameEventScriptValueFactory.GesFloatNaN();
                return true;
            case "*":
                value = leftIsVector
                    ? ScaleVector(leftVector, right, operation)
                    : ScaleVector(rightVector, left, operation);
                return true;
            case "/":
                value = leftIsVector && !rightIsVector
                    ? ScaleVector(leftVector, right, operation)
                    : GameEventScriptValueFactory.GesFloatNaN();
                return true;
            default:
                value = GameEventScriptNothingValue.Instance;
                return false;
        }
    }

    public static bool TryEvaluatePointBinary(GameEventScriptValue left, string operation, GameEventScriptValue right, out GameEventScriptValue value)
    {
        var leftIsPoint = TryReadPoint(left, out var leftPoint);
        var rightIsPoint = TryReadPoint(right, out var rightPoint);
        if (operation is not ("+" or "-" or "*" or "/" or "div" or "mod" or "rem") || (!leftIsPoint && !rightIsPoint))
        {
            value = GameEventScriptNothingValue.Instance;
            return false;
        }

        switch (operation)
        {
            case "+" when leftIsPoint && TryReadVector(right, out var rightVector) && leftPoint.Unit == rightVector.Unit:
                value = CreatePoint(
                    AddNumeric(NumericValue.Finite(leftPoint.X), NumericValue.Finite(rightVector.X)),
                    AddNumeric(NumericValue.Finite(leftPoint.Y), NumericValue.Finite(rightVector.Y)),
                    AddNumeric(NumericValue.Finite(leftPoint.Z), NumericValue.Finite(rightVector.Z)),
                    leftPoint.Unit);
                return true;
            case "-" when leftIsPoint:
            {
                if (TryReadVector(right, out var subtractedVector) &&
                    leftPoint.Unit == subtractedVector.Unit)
                {
                    value = CreatePoint(
                        SubtractNumeric(NumericValue.Finite(leftPoint.X), NumericValue.Finite(subtractedVector.X)),
                        SubtractNumeric(NumericValue.Finite(leftPoint.Y), NumericValue.Finite(subtractedVector.Y)),
                        SubtractNumeric(NumericValue.Finite(leftPoint.Z), NumericValue.Finite(subtractedVector.Z)),
                        leftPoint.Unit);
                    return true;
                }

                if (rightIsPoint &&
                    leftPoint.Unit == rightPoint.Unit)
                {
                    value = CreateVector(
                        SubtractNumeric(NumericValue.Finite(leftPoint.X), NumericValue.Finite(rightPoint.X)),
                        SubtractNumeric(NumericValue.Finite(leftPoint.Y), NumericValue.Finite(rightPoint.Y)),
                        SubtractNumeric(NumericValue.Finite(leftPoint.Z), NumericValue.Finite(rightPoint.Z)),
                        leftPoint.Unit);
                    return true;
                }

                break;
            }
        }

        value = GameEventScriptValueFactory.GesFloatNaN();
        return true;
    }

    public static bool TryEvaluatePointUnary(GameEventScriptValue operand, string operation, out GameEventScriptValue value)
    {
        if (!TryReadPoint(operand, out _) || operation is not ("-" or "abs"))
        {
            value = GameEventScriptNothingValue.Instance;
            return false;
        }

        value = GameEventScriptValueFactory.GesFloatNaN();
        return true;
    }

    public static bool TryEvaluateVectorUnary(GameEventScriptValue operand, string operation, out GameEventScriptValue value)
    {
        if (!TryReadVector(operand, out var vector) || operation is not ("-" or "abs"))
        {
            value = GameEventScriptNothingValue.Instance;
            return false;
        }

        value = operation == "-" ? CreateVector(NegateNumeric(NumericValue.Finite(vector.X)), NegateNumeric(NumericValue.Finite(vector.Y)), NegateNumeric(NumericValue.Finite(vector.Z)), vector.Unit) : EvaluateVectorLength(vector);
        return true;
    }

    public static bool TryCreateVector(GameEventScriptValue x, GameEventScriptValue y, GameEventScriptValue z, out GameEventScriptValue value)
        => TryCreateVectorFromComponents([x, y, z], out value);

    public static bool TryCreatePoint(GameEventScriptValue x, GameEventScriptValue y, GameEventScriptValue z, out GameEventScriptValue value)
        => TryCreatePointFromComponents([x, y, z], out value);

    public static bool TryCreateVectorFromLabeledComponents(string typeName, IReadOnlyDictionary<string, GameEventScriptValue> components, out GameEventScriptValue value)
    {
        if (typeName != "vector")
        {
            value = GameEventScriptNothingValue.Instance;
            return false;
        }

        var labels = new[] { "x", "y", "z" };
        var values = new double[3];
        var unit = default(GameEventScriptBytecodeInstructionUnit?);
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
                value = invalid ? GameEventScriptValueFactory.GesFloatNaN() : GameEventScriptNothingValue.Instance;
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
                value = GameEventScriptValueFactory.GesFloatNaN();
                return true;
            }
        }

        value = GameEventScriptValueFactory.GesVector(values[0], values[1], values[2], unit);
        return true;
    }

    public static bool TryCreatePointFromLabeledComponents(string typeName, IReadOnlyDictionary<string, GameEventScriptValue> components, out GameEventScriptValue value)
    {
        if (typeName != "point")
        {
            value = GameEventScriptNothingValue.Instance;
            return false;
        }

        var labels = new[] { "x", "y", "z" };
        var values = new double[3];
        var unit = default(GameEventScriptBytecodeInstructionUnit?);
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
                value = invalid ? GameEventScriptValueFactory.GesFloatNaN() : GameEventScriptNothingValue.Instance;
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
                value = GameEventScriptValueFactory.GesFloatNaN();
                return true;
            }
        }

        value = GameEventScriptValueFactory.GesPoint(values[0], values[1], values[2], unit);
        return true;
    }

    public static bool TryCreateVector(GameEventScriptValue xy, GameEventScriptValue z, out GameEventScriptValue value)
    {
        if (xy is GameEventScriptVectorValue vector) return TryCreateVector(GameEventScriptValueFactory.GesFloat(vector.X, vector.Unit), GameEventScriptValueFactory.GesFloat(vector.Y, vector.Unit), z, out value);
        value = GameEventScriptNothingValue.Instance;
        return false;
    }

    public static bool TryCreatePoint(GameEventScriptValue xy, GameEventScriptValue z, out GameEventScriptValue value)
    {
        if (xy is GameEventScriptPointValue point) return TryCreatePoint(GameEventScriptValueFactory.GesFloat(point.X, point.Unit), GameEventScriptValueFactory.GesFloat(point.Y, point.Unit), z, out value);
        value = GameEventScriptNothingValue.Instance;
        return false;
    }

    public static bool TryEraseVectorUnit(GameEventScriptValue value, out GameEventScriptValue converted)
    {
        switch (value)
        {
            case GameEventScriptVectorValue vector:
                converted = GameEventScriptValueFactory.GesVector(vector.X, vector.Y, vector.Z);
                return true;
            case GameEventScriptPointValue point:
                converted = GameEventScriptValueFactory.GesPoint(point.X, point.Y, point.Z);
                return true;
            default:
                converted = GameEventScriptNothingValue.Instance;
                return false;
        }
    }

    public static bool TryApplyVectorUnit(GameEventScriptValue value, GameEventScriptBytecodeInstructionUnit unit, out GameEventScriptValue converted)
    {
        switch (value)
        {
            case GameEventScriptVectorValue vector:
                converted = vector.Unit.IsNumericUnit() && vector.Unit != unit ? GameEventScriptValueFactory.GesFloatNaN() : GameEventScriptValueFactory.GesVector(vector.X, vector.Y, vector.Z, unit);
                return true;
            case GameEventScriptPointValue point:
                converted = point.Unit.IsNumericUnit() && point.Unit != unit ? GameEventScriptValueFactory.GesFloatNaN() : GameEventScriptValueFactory.GesPoint(point.X, point.Y, point.Z, unit);
                return true;
            default:
                converted = GameEventScriptNothingValue.Instance;
                return false;
        }
    }

    public static GameEventScriptValue EvaluateWrapDegree(GameEventScriptValue operand)
    {
        if (operand.IsNothing()) return GameEventScriptNothingValue.Instance;
        if (GameEventScriptValue.TryGetNumericUnit(operand, out var unit) && unit != GameEventScriptBytecodeInstructionUnit.UnitDegree) return GameEventScriptValueFactory.GesFloatNaN();
        if (operand.Kind is GameEventScriptValueKind.Number && TryCoerceNumericForOperation(operand, out var number) && number.IsFinite) return GameEventScriptValueFactory.GesDegree(GameEventScriptValue.WrapDegrees(number.Value));
        return GameEventScriptValueFactory.GesFloatNaN();
    }

    public static bool TryCompareNumericValues(GameEventScriptValue left, GameEventScriptValue right, out int comparison)
    {
        if (HaveCompatibleNumericUnits(left, right) && TryCoerceNumericForOperation(left, out var leftNumeric) && TryCoerceNumericForOperation(right, out var rightNumeric)) return TryCompareNumeric(leftNumeric, rightNumeric, out comparison);
        comparison = default;
        return false;
    }

    public static bool HaveCompatibleNumericUnits(GameEventScriptValue left, GameEventScriptValue right)
    {
        var leftHasUnit = GameEventScriptValue.TryGetNumericUnit(left, out var leftUnit);
        var rightHasUnit = GameEventScriptValue.TryGetNumericUnit(right, out var rightUnit);
        return leftHasUnit == rightHasUnit && (!leftHasUnit || leftUnit == rightUnit);
    }

    private static bool TryGetCommonNumericUnit(IEnumerable<GameEventScriptValue> values, out GameEventScriptBytecodeInstructionUnit? unit)
    {
        unit = null;
        var initialized = false;
        foreach (var value in values)
        {
            var hasUnit = GameEventScriptValue.TryGetNumericUnit(value, out var currentUnit);
            if (!initialized)
            {
                unit = hasUnit ? currentUnit : null;
                initialized = true;
                continue;
            }

            if (hasUnit != unit.HasValue || (hasUnit && unit.HasValue && currentUnit != unit.Value)) return false;
        }

        return true;
    }

    private static bool SetUnit(GameEventScriptBytecodeInstructionUnit? value, out GameEventScriptBytecodeInstructionUnit? unit)
    {
        unit = ToOptionalNumericUnit(value);
        return true;
    }

    private static GameEventScriptBytecodeInstructionUnit? ToOptionalNumericUnit(GameEventScriptBytecodeInstructionUnit? unit)
        => unit is { } value && value.IsNumericUnit() ? value : null;

    private readonly record struct VectorComponents(double X, double Y, double Z, GameEventScriptBytecodeInstructionUnit? Unit);

    private readonly record struct PointComponents(double X, double Y, double Z, GameEventScriptBytecodeInstructionUnit? Unit);

    private static bool TryReadVector(GameEventScriptValue value, out VectorComponents vector)
    {
        switch (value)
        {
            case GameEventScriptVectorValue vectorValue:
                vector = new VectorComponents(vectorValue.X, vectorValue.Y, vectorValue.Z, vectorValue.Unit);
                return true;
            default:
                vector = default;
                return false;
        }
    }

    private static bool TryReadPoint(GameEventScriptValue value, out PointComponents point)
    {
        switch (value)
        {
            case GameEventScriptPointValue pointValue:
                point = new PointComponents(pointValue.X, pointValue.Y, pointValue.Z, pointValue.Unit);
                return true;
            default:
                point = default;
                return false;
        }
    }

    public static bool TryCreateVectorFromComponents(IReadOnlyList<GameEventScriptValue> components, out GameEventScriptValue value)
    {
        if (components.Count > 3)
        {
            value = GameEventScriptNothingValue.Instance;
            return false;
        }

        var values = new double[3];
        var unit = default(GameEventScriptBytecodeInstructionUnit?);
        var initialized = false;

        for (var index = 0; index < components.Count; index++)
        {
            if (!TryReadVectorComponent(components[index], out values[index], out var componentUnit, out var invalid))
            {
                value = invalid ? GameEventScriptValueFactory.GesFloatNaN() : GameEventScriptNothingValue.Instance;
                return invalid;
            }

            if (!initialized)
            {
                unit = componentUnit;
                initialized = true;
                continue;
            }

            if (unit == componentUnit) continue;
            value = GameEventScriptValueFactory.GesFloatNaN();
            return true;
        }

        value = GameEventScriptValueFactory.GesVector(values[0], values[1], values[2], unit);
        return true;
    }

    public static bool TryCreatePointFromComponents(IReadOnlyList<GameEventScriptValue> components, out GameEventScriptValue value)
    {
        if (components.Count > 3)
        {
            value = GameEventScriptNothingValue.Instance;
            return false;
        }

        var values = new double[3];
        var unit = default(GameEventScriptBytecodeInstructionUnit?);
        var initialized = false;

        for (var index = 0; index < components.Count; index++)
        {
            if (!TryReadVectorComponent(components[index], out values[index], out var componentUnit, out var invalid))
            {
                value = invalid ? GameEventScriptValueFactory.GesFloatNaN() : GameEventScriptNothingValue.Instance;
                return invalid;
            }

            if (!initialized)
            {
                unit = componentUnit;
                initialized = true;
                continue;
            }

            if (unit == componentUnit) continue;
            value = GameEventScriptValueFactory.GesFloatNaN();
            return true;
        }

        value = GameEventScriptValueFactory.GesPoint(values[0], values[1], values[2], unit);
        return true;
    }

    private static bool TryReadVectorComponent(GameEventScriptValue component, out double value, out GameEventScriptBytecodeInstructionUnit? unit, out bool invalid)
    {
        value = default;
        unit = null;
        invalid = false;

        if (!TryCoerceNumericForOperation(component, out var number)) return false;
        if (!number.IsFinite)
        {
            invalid = true;
            return false;
        }

        value = number.Value;
        unit = GameEventScriptValue.TryGetNumericUnit(component, out var floatUnit) ? floatUnit : null;
        return true;
    }

    private static GameEventScriptValue ScaleVector(VectorComponents vector, GameEventScriptValue scalar, string operation)
    {
        if (!TryCoerceNumericForOperation(scalar, out var scalarNumber) || !scalarNumber.IsFinite) return GameEventScriptValueFactory.GesFloatNaN();
        var scalarHasUnit = GameEventScriptValue.TryGetNumericUnit(scalar, out var scalarUnit);
        if (!TryGetVectorScalarResultUnit(vector.Unit, scalarHasUnit ? scalarUnit : null, operation, out var resultUnit) || operation == "/" && scalarNumber.Value == 0d) return GameEventScriptValueFactory.GesFloatNaN();
        var x = operation == "*" ? MultiplyNumeric(NumericValue.Finite(vector.X), scalarNumber) : DivideNumeric(NumericValue.Finite(vector.X), scalarNumber);
        var y = operation == "*" ? MultiplyNumeric(NumericValue.Finite(vector.Y), scalarNumber) : DivideNumeric(NumericValue.Finite(vector.Y), scalarNumber);
        var z = operation == "*" ? MultiplyNumeric(NumericValue.Finite(vector.Z), scalarNumber) : DivideNumeric(NumericValue.Finite(vector.Z), scalarNumber);
        return CreateVector(x, y, z, resultUnit);
    }

    private static bool TryGetVectorScalarResultUnit(GameEventScriptBytecodeInstructionUnit? vectorUnit, GameEventScriptBytecodeInstructionUnit? scalarUnit, string operation, out GameEventScriptBytecodeInstructionUnit? resultUnit)
    {
        vectorUnit = ToOptionalNumericUnit(vectorUnit);
        scalarUnit = ToOptionalNumericUnit(scalarUnit);
        resultUnit = null;
        switch (operation)
        {
            case "*" when vectorUnit.HasValue && scalarUnit.HasValue:
                return false;
            case "*":
                resultUnit = vectorUnit ?? scalarUnit;
                return true;
            case "/" when !vectorUnit.HasValue && !scalarUnit.HasValue:
                return true;
            case "/" when vectorUnit.HasValue && !scalarUnit.HasValue:
                resultUnit = vectorUnit;
                return true;
            case "/" when vectorUnit.HasValue && scalarUnit.HasValue && vectorUnit == scalarUnit:
                return true;
            default:
                return false;
        }
    }

    private static GameEventScriptValue CreateVector(NumericValue x, NumericValue y, NumericValue z, GameEventScriptBytecodeInstructionUnit? unit)
    {
        if (!x.IsFinite || !y.IsFinite || !z.IsFinite) return GameEventScriptValueFactory.GesFloatNaN();
        return GameEventScriptValueFactory.GesVector(x.Value, y.Value, z.Value, unit);
    }

    private static GameEventScriptValue CreatePoint(NumericValue x, NumericValue y, NumericValue z, GameEventScriptBytecodeInstructionUnit? unit)
    {
        if (!x.IsFinite || !y.IsFinite || !z.IsFinite) return GameEventScriptValueFactory.GesFloatNaN();
        return GameEventScriptValueFactory.GesPoint(x.Value, y.Value, z.Value, unit);
    }

    private static GameEventScriptValue EvaluateVectorLength(VectorComponents vector)
    {
        try
        {
            var squared = vector.X * vector.X + vector.Y * vector.Y + vector.Z * vector.Z;
            var length = Math.Sqrt(squared);
            if (double.IsNaN(length) || double.IsInfinity(length)) return GameEventScriptValueFactory.GesFloatNaN();
            return GameEventScriptValueFactory.GesFloat(length, vector.Unit);
        }
        catch (OverflowException)
        {
            return GameEventScriptValueFactory.GesFloatNaN();
        }
    }

    private static GameEventScriptValue ToGameEventScriptPercentage(NumericValue number)
        => number.IsFinite ? GameEventScriptValueFactory.GesPercentage(number.Value) : ToGameEventScriptFloat(number);

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

        if (TryAddFinite(left.Value, right.Value, out var sum)) return NumericValue.Finite(sum);
        return left.Value switch
        {
            > 0d when right.Value > 0d => NumericValue.PositiveInfinity(),
            < 0d when right.Value < 0d => NumericValue.NegativeInfinity(),
            _ => NumericValue.NaN()
        };
    }

    public static NumericValue SubtractNumeric(NumericValue left, NumericValue right) => AddNumeric(left, NegateNumeric(right));

    public static NumericValue MultiplyNumeric(NumericValue left, NumericValue right)
    {
        if (left.IsNaN || right.IsNaN || (left.IsInfinity && IsZero(right)) || (right.IsInfinity && IsZero(left))) return NumericValue.NaN();
        if (left.IsInfinity || right.IsInfinity) return SignOf(left) * SignOf(right) >= 0 ? NumericValue.PositiveInfinity() : NumericValue.NegativeInfinity();
        if (TryMultiplyFinite(left.Value, right.Value, out var product)) return NumericValue.Finite(product);
        return SignOf(left) * SignOf(right) >= 0 ? NumericValue.PositiveInfinity() : NumericValue.NegativeInfinity();
    }

    public static NumericValue DivideNumeric(NumericValue left, NumericValue right)
    {
        if (left.IsNaN || right.IsNaN) return NumericValue.NaN();
        if (right is { IsFinite: true, Value: 0d })
        {
            if (left is { IsFinite: true, Value: 0d }) return NumericValue.NaN();
            return SignOf(left) >= 0 ? NumericValue.PositiveInfinity() : NumericValue.NegativeInfinity();
        }

        switch (left.IsInfinity)
        {
            case true when right.IsInfinity:
                return NumericValue.NaN();
            case true:
                return SignOf(left) * SignOf(right) >= 0 ? NumericValue.PositiveInfinity() : NumericValue.NegativeInfinity();
        }

        if (right.IsInfinity) return NumericValue.Finite(0d);
        if (TryDivideFinite(left.Value, right.Value, out var quotient)) return NumericValue.Finite(quotient);
        return SignOf(left) * SignOf(right) >= 0 ? NumericValue.PositiveInfinity() : NumericValue.NegativeInfinity();
    }

    public static NumericValue IntegerDivideNumeric(NumericValue left, NumericValue right)
    {
        var quotient = DivideNumeric(left, right);
        return !quotient.IsFinite ? quotient : NumericValue.Finite(Math.Floor(quotient.Value));
    }

    public static NumericValue ModuloNumeric(NumericValue left, NumericValue right)
    {
        if (left.IsNaN || right.IsNaN || left.IsInfinity) return NumericValue.NaN();
        if (right.IsInfinity) return left.IsFinite ? NumericValue.Finite(left.Value) : NumericValue.NaN();
        if (right.Value == 0d) return NumericValue.NaN();
        return TryModuloFinite(left.Value, right.Value, out var modulo) ? NumericValue.Finite(modulo) : NumericValue.NaN();
    }

    public static NumericValue RemainderNumeric(NumericValue left, NumericValue right)
    {
        if (left.IsNaN || right.IsNaN || left.IsInfinity) return NumericValue.NaN();
        if (right.IsInfinity) return left.IsFinite ? NumericValue.Finite(left.Value) : NumericValue.NaN();
        if (right.Value == 0d) return NumericValue.NaN();
        return TryRemainderFinite(left.Value, right.Value, out var remainder) ? NumericValue.Finite(remainder) : NumericValue.NaN();
    }

    public static NumericValue PowerNumeric(NumericValue left, NumericValue right)
    {
        if (left.IsNaN || right.IsNaN) return NumericValue.NaN();
        if (left.IsFinite && right.IsFinite && TryGetIntegerExponent(right.Value, out var exponent) && exponent != long.MinValue)
            return exponent < 0 ? DivideNumeric(NumericValue.Finite(1d), PowerFiniteNonNegativeIntegerExponent(left.Value, -exponent)) : PowerFiniteNonNegativeIntegerExponent(left.Value, exponent);
        return FromDoublePower(ToDouble(left), ToDouble(right));
    }

    private static NumericValue PowerFiniteNonNegativeIntegerExponent(double value, long exponent)
    {
        var result = NumericValue.Finite(1d);
        var factor = NumericValue.Finite(value);
        while (exponent > 0)
        {
            if ((exponent & 1L) != 0) result = MultiplyNumeric(result, factor);
            exponent >>= 1;
            if (exponent > 0) factor = MultiplyNumeric(factor, factor);
        }

        return result;
    }

    private static bool TryGetIntegerExponent(double value, out long exponent)
    {
        if (value != Math.Truncate(value) || value > long.MaxValue || value < long.MinValue)
        {
            exponent = default;
            return false;
        }

        exponent = (long)value;
        return true;
    }

    private static double ToDouble(NumericValue value) => value.Kind switch
    {
        NumericKind.Finite => value.Value,
        NumericKind.PositiveInfinity => double.PositiveInfinity,
        NumericKind.NegativeInfinity => double.NegativeInfinity,
        _ => double.NaN
    };

    private static NumericValue FromDoublePower(double left, double right)
    {
        var result = Math.Pow(left, right);
        if (double.IsNaN(result)) return NumericValue.NaN();
        if (double.IsPositiveInfinity(result)) return NumericValue.PositiveInfinity();
        if (double.IsNegativeInfinity(result)) return NumericValue.NegativeInfinity();

        try
        {
            return NumericValue.Finite(result);
        }
        catch (OverflowException)
        {
            return result < 0d ? NumericValue.NegativeInfinity() : NumericValue.PositiveInfinity();
        }
    }

    public static NumericValue NegateNumeric(NumericValue value)
    {
        if (value.IsNaN) return NumericValue.NaN();
        if (value.IsPositiveInfinity) return NumericValue.NegativeInfinity();
        if (value.IsNegativeInfinity) return NumericValue.PositiveInfinity();
        if (TryNegateFinite(value.Value, out var negated)) return NumericValue.Finite(negated);
        return value.Value < 0d ? NumericValue.PositiveInfinity() : NumericValue.NegativeInfinity();
    }

    public static int SignOf(NumericValue value)
    {
        if (value.IsPositiveInfinity) return 1;
        if (value.IsNegativeInfinity) return -1;
        return !value.IsFinite ? 0 : value.Value.CompareTo(0d);
    }

    public static bool IsZero(NumericValue value) => value is { IsFinite: true, Value: 0d };

    private static bool TryAddInteger(long left, long right, out long value)
    {
        value = left + right;
        return ((left ^ value) & (right ^ value)) >= 0;
    }

    private static bool TrySubtractInteger(long left, long right, out long value)
    {
        value = left - right;
        return ((left ^ right) & (left ^ value)) >= 0;
    }

    private static bool TryMultiplyInteger(long left, long right, out long value)
    {
        if (left == 0 || right == 0)
        {
            value = 0;
            return true;
        }

        if (left == -1)
        {
            if (right == long.MinValue)
            {
                value = default;
                return false;
            }

            value = -right;
            return true;
        }

        if (right == -1)
        {
            if (left == long.MinValue)
            {
                value = default;
                return false;
            }

            value = -left;
            return true;
        }

        value = left * right;
        return value / right == left;
    }

    public static bool TryAddFinite(double left, double right, out double value)
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

    public static bool TryMultiplyFinite(double left, double right, out double value)
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

    public static bool TryDivideFinite(double left, double right, out double value)
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

    public static bool TryModuloFinite(double left, double right, out double value)
    {
        try
        {
            var remainder = left % right;
            if (remainder != 0d &&
                (remainder < 0d && right > 0d || remainder > 0d && right < 0d))
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

    public static bool TryRemainderFinite(double left, double right, out double value)
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

    public static bool TryNegateFinite(double input, out double value)
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
        if (value.IsNothing()) return string.Empty;
        return value.Kind switch
        {
            GameEventScriptValueKind.Text => value.AsText(),
            GameEventScriptValueKind.Number => value.ToString(),
            GameEventScriptValueKind.Boolean => value.AsBoolean().ToString(),
            _ => value.ToString()
        };
    }

    public static long ToIntegerSaturated(double number)
    {
        var truncated = Math.Truncate(number);
        return truncated switch
        {
            > long.MaxValue => long.MaxValue,
            < long.MinValue => long.MinValue,
            _ => (long)truncated
        };
    }

    private static bool TryToInteger(NumericValue number, out long integer)
    {
        if (!number.IsFinite || number.Value != Math.Truncate(number.Value) || number.Value > long.MaxValue || number.Value < long.MinValue)
        {
            integer = default;
            return false;
        }

        integer = (long)number.Value;
        return true;
    }
}