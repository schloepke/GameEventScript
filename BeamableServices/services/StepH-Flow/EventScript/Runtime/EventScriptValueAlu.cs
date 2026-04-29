using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StepH.Flow.EventScript.Types;

namespace StepH.Flow.EventScript.Runtime;

internal static class EventScriptValueAlu
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

    public static EventScriptValue EvaluateMinMax(IReadOnlyList<EventScriptValue> values, bool isMax)
    {
        if (values.All(value => TryCoerceNumericForOperation(value, out _)))
        {
            if (!TryGetCommonNumericUnit(values, out _))
            {
                return EventScriptValueFactory.DecimalNaN();
            }

            var numericBest = values[0];
            TryCoerceNumericForOperation(numericBest, out var bestNumber);
            for (var i = 1; i < values.Count; i++)
            {
                TryCoerceNumericForOperation(values[i], out var number);
                if (!TryCompareNumeric(number, bestNumber, out var comparison))
                {
                    return EventScriptValueFactory.DecimalNaN();
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
            var comparison = EventScriptValue.StableComparer.Compare(values[i], best);
            if ((isMax && comparison > 0) || (!isMax && comparison < 0))
            {
                best = values[i];
            }
        }

        return best;
    }

    public static bool TryCombineWithPlus(EventScriptValue left, EventScriptValue right, out EventScriptValue value)
    {
        if (left.Kind == EventScriptValueKind.Dictionary && right.Kind == EventScriptValueKind.Dictionary)
        {
            value = EvaluateDictionaryCombine(left, right);
            return true;
        }

        if (left.Kind == EventScriptValueKind.List && right.Kind == EventScriptValueKind.List)
        {
            value = EventScriptValueFactory.List(left.AsList().Concat(right.AsList()));
            return true;
        }

        if (left.Kind == EventScriptValueKind.List)
        {
            value = EventScriptValueFactory.List(left.AsList().Append(right));
            return true;
        }

        if (right.Kind == EventScriptValueKind.List)
        {
            value = EventScriptValueFactory.List(new[] { left }.Concat(right.AsList()));
            return true;
        }

        value = EventScriptValue.Nothing;
        return false;
    }

    public static EventScriptValue EvaluateCollectionCombine(EventScriptValue left, EventScriptValue right)
    {
        if (left.Kind == EventScriptValueKind.Dictionary && right.Kind == EventScriptValueKind.Dictionary)
        {
            return EvaluateDictionaryCombine(left, right);
        }

        if (left.Kind == EventScriptValueKind.Set && right.Kind == EventScriptValueKind.Set)
        {
            return EventScriptValueFactory.Set(left.AsSet().Concat(right.AsSet()));
        }

        if (left.Kind is EventScriptValueKind.List or EventScriptValueKind.Dice &&
            right.Kind is EventScriptValueKind.List or EventScriptValueKind.Dice)
        {
            return EventScriptValueFactory.List(left.AsList().Concat(right.AsList()));
        }

        return EventScriptValue.Nothing;
    }

    public static EventScriptValue EvaluateCollectionIntersect(EventScriptValue left, EventScriptValue right)
    {
        if (left.Kind == EventScriptValueKind.Dictionary && right.Kind == EventScriptValueKind.Dictionary)
        {
            var rightKeys = new HashSet<string>(right.AsDictionary().Keys, StringComparer.Ordinal);
            var map = left.AsDictionary()
                .Where(pair => rightKeys.Contains(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            return EventScriptValueFactory.Dictionary(map);
        }

        if (left.Kind == EventScriptValueKind.Set && right.Kind == EventScriptValueKind.Set)
        {
            var rightSet = right.AsSet();
            return EventScriptValueFactory.Set(left.AsSet().Where(item => rightSet.Contains(item)));
        }

        if (left.Kind is EventScriptValueKind.List or EventScriptValueKind.Dice &&
            right.Kind is EventScriptValueKind.List or EventScriptValueKind.Dice)
        {
            var remaining = right.AsList().ToList();
            var result = new List<EventScriptValue>();
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

            return EventScriptValueFactory.List(result);
        }

        return EventScriptValue.Nothing;
    }

    public static EventScriptValue EvaluateCollectionExcept(EventScriptValue left, EventScriptValue right)
    {
        if (left.Kind == EventScriptValueKind.Dictionary && right.Kind == EventScriptValueKind.Dictionary)
        {
            var rightKeys = new HashSet<string>(right.AsDictionary().Keys, StringComparer.Ordinal);
            var map = left.AsDictionary()
                .Where(pair => !rightKeys.Contains(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            return EventScriptValueFactory.Dictionary(map);
        }

        if (left.Kind == EventScriptValueKind.Set && right.Kind == EventScriptValueKind.Set)
        {
            var rightSet = right.AsSet();
            return EventScriptValueFactory.Set(left.AsSet().Where(item => !rightSet.Contains(item)));
        }

        if (left.Kind is EventScriptValueKind.List or EventScriptValueKind.Dice &&
            right.Kind is EventScriptValueKind.List or EventScriptValueKind.Dice)
        {
            var remaining = right.AsList().ToList();
            var result = new List<EventScriptValue>();
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

            return EventScriptValueFactory.List(result);
        }

        return EventScriptValue.Nothing;
    }

    public static EventScriptValue EvaluateCollectionZip(EventScriptValue left, EventScriptValue right)
    {
        if (left.Kind is not (EventScriptValueKind.List or EventScriptValueKind.Dice) ||
            right.Kind is not (EventScriptValueKind.List or EventScriptValueKind.Dice))
        {
            return EventScriptValue.Nothing;
        }

        var leftItems = left.AsList();
        var rightItems = right.AsList();
        var count = Math.Min(leftItems.Count, rightItems.Count);
        var zipped = new List<EventScriptValue>(count);
        for (var i = 0; i < count; i++)
        {
            zipped.Add(EventScriptValueFactory.Dictionary(new Dictionary<string, EventScriptValue>(StringComparer.Ordinal)
            {
                ["left"] = leftItems[i],
                ["right"] = rightItems[i]
            }));
        }

        return EventScriptValueFactory.List(zipped);
    }

    public static EventScriptValue EvaluateDictionaryCombine(EventScriptValue left, EventScriptValue right)
    {
        var map = new Dictionary<string, EventScriptValue>(left.AsDictionary(), StringComparer.Ordinal);
        foreach (var pair in right.AsDictionary())
        {
            map[pair.Key] = pair.Value;
        }

        return EventScriptValueFactory.Dictionary(map);
    }

    public static bool AreEqual(EventScriptValue left, EventScriptValue right) => left.Equals(right);

    public static bool TryUnwrapOptionalForOperation(EventScriptValue value, out EventScriptValue unwrapped)
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

    public static bool TryCoerceNumericForOperation(EventScriptValue value, out NumericValue number)
    {
        if (value.IsNothing())
        {
            number = default;
            return false;
        }

        if (value.Kind == EventScriptValueKind.Decimal)
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

        if (value.Kind == EventScriptValueKind.Integer)
        {
            number = NumericValue.Finite(value.AsInteger());
            return true;
        }

        if (value.Kind == EventScriptValueKind.Percentage)
        {
            number = NumericValue.Finite(value.AsNumber());
            return true;
        }

        if (value.Kind == EventScriptValueKind.Dice)
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

        if (value.Kind == EventScriptValueKind.Boolean)
        {
            number = NumericValue.Finite(value.AsBoolean() ? 1m : 0m);
            return true;
        }

        number = default;
        return false;
    }

    public static EventScriptValue ToEventScriptDecimal(NumericValue number, EventScriptDecimalUnit? unit = null)
    {
        return number.Kind switch
        {
            NumericKind.Finite => EventScriptValueFactory.Decimal(number.Value, unit),
            NumericKind.NaN => EventScriptValueFactory.DecimalNaN(),
            NumericKind.PositiveInfinity => EventScriptValueFactory.DecimalInfinity(),
            NumericKind.NegativeInfinity => EventScriptValueFactory.DecimalNegativeInfinity(),
            _ => EventScriptValueFactory.DecimalNaN()
        };
    }

    public static EventScriptValue ToEventScriptNumericResult(
        EventScriptValue left,
        string operation,
        EventScriptValue right,
        NumericValue number,
        EventScriptDecimalUnit? unit = null)
    {
        if (unit is null &&
            operation == "div" &&
            TryToInteger(number, out var quotient))
        {
            return EventScriptValueFactory.Integer(quotient);
        }

        if (unit is null &&
            operation is "+" or "-" or "*" or "mod" or "rem" &&
            left.Kind == EventScriptValueKind.Integer &&
            right.Kind == EventScriptValueKind.Integer &&
            TryToInteger(number, out var integer))
        {
            return EventScriptValueFactory.Integer(integer);
        }

        return ToEventScriptDecimal(number, unit);
    }

    public static bool TryEvaluatePercentageBinary(EventScriptValue left, string operation, EventScriptValue right, out EventScriptValue value)
    {
        var leftIsPercentage = left.IsPercentage();
        var rightIsPercentage = right.IsPercentage();
        if (operation is not ("+" or "-" or "*" or "/") || (!leftIsPercentage && !rightIsPercentage))
        {
            value = EventScriptValue.Nothing;
            return false;
        }

        if (!TryCoerceNumericForOperation(left, out var leftNumber) ||
            !TryCoerceNumericForOperation(right, out var rightNumber))
        {
            value = EventScriptValueFactory.DecimalNaN();
            return true;
        }

        var leftHasUnit = EventScriptValue.TryGetDecimalUnit(left, out var leftUnit);
        var rightHasUnit = EventScriptValue.TryGetDecimalUnit(right, out var rightUnit);
        if (leftIsPercentage && rightHasUnit && operation is "+" or "-" or "/")
        {
            value = EventScriptValueFactory.DecimalNaN();
            return true;
        }

        if (leftIsPercentage && rightIsPercentage)
        {
            value = operation switch
            {
                "+" => ToEventScriptPercentage(AddNumeric(leftNumber, rightNumber)),
                "-" => ToEventScriptPercentage(SubtractNumeric(leftNumber, rightNumber)),
                "*" => ToEventScriptPercentage(MultiplyNumeric(leftNumber, rightNumber)),
                "/" => ToEventScriptDecimal(DivideNumeric(leftNumber, rightNumber)),
                _ => EventScriptValueFactory.DecimalNaN()
            };
            return true;
        }

        if (operation is "+" or "-")
        {
            if (leftIsPercentage)
            {
                value = EventScriptValueFactory.DecimalNaN();
                return true;
            }

            var delta = MultiplyNumeric(leftNumber, rightNumber);
            var result = operation == "+"
                ? AddNumeric(leftNumber, delta)
                : SubtractNumeric(leftNumber, delta);
            value = ToEventScriptDecimal(result, leftHasUnit ? leftUnit : null);
            return true;
        }

        if (operation == "*")
        {
            var result = MultiplyNumeric(leftNumber, rightNumber);
            if (leftIsPercentage)
            {
                value = rightHasUnit
                    ? ToEventScriptDecimal(result, rightUnit)
                    : ToEventScriptPercentage(result);
                return true;
            }

            value = ToEventScriptDecimal(result, leftHasUnit ? leftUnit : null);
            return true;
        }

        if (operation == "/")
        {
            var result = DivideNumeric(leftNumber, rightNumber);
            value = leftIsPercentage
                ? ToEventScriptPercentage(result)
                : ToEventScriptDecimal(result, leftHasUnit ? leftUnit : null);
            return true;
        }

        value = EventScriptValue.Nothing;
        return false;
    }

    public static bool TryEvaluateUnitBinary(EventScriptValue left, string operation, EventScriptValue right, out EventScriptValue value)
    {
        var leftHasUnit = EventScriptValue.TryGetDecimalUnit(left, out var leftUnit);
        var rightHasUnit = EventScriptValue.TryGetDecimalUnit(right, out var rightUnit);
        if (operation is not ("+" or "-" or "*" or "/" or "div" or "mod" or "rem") || (!leftHasUnit && !rightHasUnit))
        {
            value = EventScriptValue.Nothing;
            return false;
        }

        if (!TryCoerceNumericForOperation(left, out var leftNumber) ||
            !TryCoerceNumericForOperation(right, out var rightNumber))
        {
            value = EventScriptValueFactory.DecimalNaN();
            return true;
        }

        var resultUnit = default(EventScriptDecimalUnit?);
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
            value = EventScriptValueFactory.DecimalNaN();
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

        value = ToEventScriptNumericResult(left, operation, right, result, resultUnit);
        return true;
    }

    public static bool TryEvaluateUnitRounding(EventScriptValue operand, string operation, out EventScriptValue value)
    {
        if (!EventScriptValue.TryGetDecimalUnit(operand, out var unit))
        {
            value = EventScriptValue.Nothing;
            return false;
        }

        var degrees = operand.AsNumber();
        var rounded = operation switch
        {
            "floor" or "rounddown" => Math.Floor(degrees),
            "ceil" or "roundup" => Math.Ceiling(degrees),
            "round" or "roundeven" => Math.Round(degrees, 0, MidpointRounding.ToEven),
            _ => degrees
        };

        value = EventScriptValueFactory.Decimal(rounded, unit);
        return true;
    }

    public static EventScriptValue EvaluateWrapDegree(EventScriptValue operand)
    {
        if (operand.IsNothing())
        {
            return EventScriptValue.Nothing;
        }

        if (!TryUnwrapOptionalForOperation(operand, out var unwrapped))
        {
            return EventScriptValueFactory.DecimalNaN();
        }

        if (EventScriptValue.TryGetDecimalUnit(unwrapped, out var unit) && unit != EventScriptDecimalUnit.Degree)
        {
            return EventScriptValueFactory.DecimalNaN();
        }

        if (unwrapped.Kind is EventScriptValueKind.Decimal or EventScriptValueKind.Integer &&
            TryCoerceNumericForOperation(unwrapped, out var number) &&
            number.IsFinite)
        {
            return EventScriptValueFactory.Degree(EventScriptValue.WrapDegrees(number.Value));
        }

        return EventScriptValueFactory.DecimalNaN();
    }

    public static bool TryCompareNumericValues(EventScriptValue left, EventScriptValue right, out int comparison)
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

    public static bool HaveCompatibleNumericUnits(EventScriptValue left, EventScriptValue right)
    {
        var leftHasUnit = EventScriptValue.TryGetDecimalUnit(left, out var leftUnit);
        var rightHasUnit = EventScriptValue.TryGetDecimalUnit(right, out var rightUnit);
        return leftHasUnit == rightHasUnit && (!leftHasUnit || leftUnit == rightUnit);
    }

    private static bool TryGetCommonNumericUnit(IEnumerable<EventScriptValue> values, out EventScriptDecimalUnit? unit)
    {
        unit = null;
        var initialized = false;
        foreach (var value in values)
        {
            var hasUnit = EventScriptValue.TryGetDecimalUnit(value, out var currentUnit);
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

    private static bool SetUnit(EventScriptDecimalUnit? value, out EventScriptDecimalUnit? unit)
    {
        unit = value;
        return true;
    }

    private static EventScriptValue ToEventScriptPercentage(NumericValue number)
        => number.IsFinite
            ? EventScriptValueFactory.Percentage(number.Value)
            : ToEventScriptDecimal(number);

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

    public static string ToText(EventScriptValue value)
    {
        if (value.IsNothing())
        {
            return string.Empty;
        }

        return value.Kind switch
        {
            EventScriptValueKind.Text => value.AsText(),
            EventScriptValueKind.Decimal => value.ToString(),
            EventScriptValueKind.Integer => value.AsInteger().ToString(CultureInfo.InvariantCulture),
            EventScriptValueKind.Boolean => value.AsBoolean().ToString(),
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
