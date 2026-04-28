#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using static StepH.Flow.EventScript.Types.EventScriptValueFactory;
namespace StepH.Flow.EventScript.Types;

public sealed class EventScriptDecimalValue : EventScriptValue
{
    public static readonly EventScriptDecimalValue NaN = new(0m, null, true, false, false);
    public static readonly EventScriptDecimalValue Infinity = new(0m, null, false, true, false);
    public static readonly EventScriptDecimalValue NegativeInfinity = new(0m, null, false, true, true);

    public static EventScriptDecimalValue EventScriptDecimal(decimal value, EventScriptDecimalUnit? unit = null) => new(value, unit, false, false, false);

    public static EventScriptDecimalValue EventScriptDecimal(double value)
    {
        if (double.IsPositiveInfinity(value)) return Infinity;
        if (double.IsNegativeInfinity(value)) return NegativeInfinity;
        return double.IsNaN(value) ? NaN : new EventScriptDecimalValue((decimal)value, null, false, false, false);
    }

    public static EventScriptDecimalValue EventScriptDecimal(float value)
    {
        if (float.IsPositiveInfinity(value)) return Infinity;
        if (float.IsNegativeInfinity(value)) return NegativeInfinity;
        return float.IsNaN(value) ? NaN : new EventScriptDecimalValue((decimal)value, null, false, false, false);
    }

    private EventScriptDecimalValue(decimal value, EventScriptDecimalUnit? unit, bool isNaN, bool isInfinity, bool isNegativeInfinity)
    {
        Value = value;
        Unit = isNaN || isInfinity ? null : unit;
        IsNaNValue = isNaN;
        IsInfinityValue = isInfinity;
        IsNegativeInfinityValue = isNegativeInfinity;
    }

    public decimal Value { get; }
    public EventScriptDecimalUnit? Unit { get; }
    public bool IsNaNValue { get; }
    public bool IsInfinityValue { get; }
    public bool IsNegativeInfinityValue { get; }
    public override EventScriptValueKind Kind => EventScriptValueKind.Decimal;

    public override string AsText() => ToString();

    public override bool AsBoolean() => !IsNaNValue && AsNumber() != 0;

    public override long AsInteger()
    {
        if (IsNaNValue) return 0;
        if (IsInfinityValue) return IsNegativeInfinityValue ? long.MinValue : long.MaxValue;
        return ToIntegerSaturated(Value);
    }

    public override decimal AsNumber()
        => IsNaNValue ? 0m : IsInfinityValue ? IsNegativeInfinityValue ? decimal.MinValue : decimal.MaxValue : Value;

    public override bool HasSemanticValue() => !IsNaNValue && !IsInfinityValue;

    internal override bool TryConvertToNumber(out EventScriptValue value)
    {
        value = Unit.HasValue ? EventScriptDecimal(AsNumber()) : this;
        return true;
    }

    internal override bool TryConvertToInteger(out EventScriptValue value)
    {
        if (IsNaNValue)
        {
            value = Integer(0);
            return true;
        }

        if (IsInfinityValue)
        {
            value = Integer(IsNegativeInfinityValue ? long.MinValue : long.MaxValue);
            return true;
        }

        value = Integer(ToIntegerSaturated(Value));
        return true;
    }

    internal override bool TryConvertToBoolean(out EventScriptValue value)
    {
        value = Boolean(!IsNaNValue && AsNumber() != 0);
        return true;
    }

    internal override bool TryConvertToText(out EventScriptValue value)
    {
        value = Text(ToString());
        return true;
    }

}
