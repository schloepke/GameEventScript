#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using static StepH.GameEventScript.Api.GameEventScriptValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class GameEventScriptFloatValue : GameEventScriptValue
{
    public static readonly GameEventScriptFloatValue NaN = new(0d, null, true, false, false);
    public static readonly GameEventScriptFloatValue Infinity = new(0d, null, false, true, false);
    public static readonly GameEventScriptFloatValue NegativeInfinity = new(0d, null, false, true, true);

    public static GameEventScriptFloatValue Create(double value, GameEventScriptNumericUnit? unit = null)
    {
        if (double.IsPositiveInfinity(value)) return Infinity;
        if (double.IsNegativeInfinity(value)) return NegativeInfinity;
        return double.IsNaN(value) ? NaN : new GameEventScriptFloatValue(value, unit, false, false, false);
    }

    private GameEventScriptFloatValue(double value, GameEventScriptNumericUnit? unit, bool isNaN, bool isInfinity, bool isNegativeInfinity)
    {
        Value = value;
        Unit = isNaN || isInfinity ? null : unit;
        IsNaNValue = isNaN;
        IsInfinityValue = isInfinity;
        IsNegativeInfinityValue = isNegativeInfinity;
    }

    public double Value { get; }
    public GameEventScriptNumericUnit? Unit { get; }
    public bool IsNaNValue { get; }
    public bool IsInfinityValue { get; }
    public bool IsNegativeInfinityValue { get; }
    public override GameEventScriptValueKind Kind => GameEventScriptValueKind.Float;

    public override string AsText() => ToString();

    public override bool AsBoolean() => !IsNaNValue && AsNumber() != 0;

    public override long AsInteger()
    {
        if (IsNaNValue) return 0;
        if (IsInfinityValue) return IsNegativeInfinityValue ? long.MinValue : long.MaxValue;
        return ToIntegerSaturated(Value);
    }

    public override double AsNumber()
        => IsNaNValue ? 0d : IsInfinityValue ? IsNegativeInfinityValue ? double.MinValue : double.MaxValue : Value;

    public override bool HasSemanticValue() => !IsNaNValue && !IsInfinityValue;

    internal override bool TryConvertToNumber(out GameEventScriptValue value)
    {
        value = Unit.HasValue ? Create(AsNumber()) : this;
        return true;
    }

    internal override bool TryConvertToInteger(out GameEventScriptValue value)
    {
        if (IsNaNValue)
        {
            value = GesInteger(0);
            return true;
        }

        if (IsInfinityValue)
        {
            value = GesInteger(IsNegativeInfinityValue ? long.MinValue : long.MaxValue);
            return true;
        }

        value = GesInteger(ToIntegerSaturated(Value));
        return true;
    }

    internal override bool TryConvertToBoolean(out GameEventScriptValue value)
    {
        value = GesBoolean(!IsNaNValue && AsNumber() != 0);
        return true;
    }

    internal override bool TryConvertToText(out GameEventScriptValue value)
    {
        value = GesText(ToString());
        return true;
    }

}
