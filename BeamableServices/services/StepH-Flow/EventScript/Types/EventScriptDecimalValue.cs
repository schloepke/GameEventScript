#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace StepH.Flow.EventScript.Types;

public sealed class EventScriptDecimalValue : EventScriptValue
{
    private static readonly EventScriptDecimalValue NaNInstance = new(0m, true, false, false);
    private static readonly EventScriptDecimalValue InfinityInstance = new(0m, false, true, false);
    private static readonly EventScriptDecimalValue NegativeInfinityInstance = new(0m, false, true, true);

   
    private EventScriptDecimalValue(decimal value, bool isNaN, bool isInfinity, bool isNegativeInfinity)
    {
        Value = value;
        IsNaNValue = isNaN;
        IsInfinityValue = isInfinity;
        IsNegativeInfinityValue = isNegativeInfinity;
    }

    public decimal Value { get; }
    public bool IsNaNValue { get; }
    public bool IsInfinityValue { get; }
    public bool IsNegativeInfinityValue { get; }
    public override EventScriptValueType Type => EventScriptValueType.Decimal;

    public static EventScriptDecimalValue FromDecimal(decimal value) => new(value, false, false, false);

    public static EventScriptDecimalValue FromDouble(double value)
    {
        if (double.IsNaN(value)) return NaN();
        if (double.IsPositiveInfinity(value)) return Infinity();
        if (double.IsNegativeInfinity(value)) return NegativeInfinity();
        return new EventScriptDecimalValue((decimal)value, false, false, false);
    }

    public static EventScriptDecimalValue FromFloat(float value)
    {
        if (float.IsNaN(value)) return NaN();
        if (float.IsPositiveInfinity(value)) return Infinity();
        if (float.IsNegativeInfinity(value)) return NegativeInfinity();
        return new EventScriptDecimalValue((decimal)value, false, false, false);
    }

    public static EventScriptDecimalValue NaN() => NaNInstance;
    public static EventScriptDecimalValue Infinity() => InfinityInstance;
    public static EventScriptDecimalValue NegativeInfinity() => NegativeInfinityInstance;
}
