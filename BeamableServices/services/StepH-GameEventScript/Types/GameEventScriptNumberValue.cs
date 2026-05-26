#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class GameEventScriptNumberValue : GameEventScriptValue
{
    public static readonly GameEventScriptNumberValue NaN = new(0, 0d, null, isInteger: false, isNaN: true, isInfinity: false, isNegativeInfinity: false);
    public static readonly GameEventScriptNumberValue Infinity = new(0, 0d, null, isInteger: false, isNaN: false, isInfinity: true, isNegativeInfinity: false);
    public static readonly GameEventScriptNumberValue NegativeInfinity = new(0, 0d, null, isInteger: false, isNaN: false, isInfinity: true, isNegativeInfinity: true);

    public static GameEventScriptNumberValue CreateInteger(long value, GameEventScriptBytecodeInstructionUnit? unit = null)
        => new(value, value, unit, isInteger: true, isNaN: false, isInfinity: false, isNegativeInfinity: false);

    public static GameEventScriptNumberValue CreateFloat(double value, GameEventScriptBytecodeInstructionUnit? unit = null)
    {
        if (double.IsPositiveInfinity(value)) return Infinity;
        if (double.IsNegativeInfinity(value)) return NegativeInfinity;
        if (double.IsNaN(value)) return NaN;

        return double.IsFinite(value) &&
               value >= long.MinValue &&
               value <= long.MaxValue &&
               value == Math.Truncate(value)
            ? CreateInteger((long)value, unit)
            : new GameEventScriptNumberValue(ToIntegerSaturated(value), value, unit, isInteger: false, isNaN: false, isInfinity: false, isNegativeInfinity: false);
    }

    private GameEventScriptNumberValue(
        long integerValue,
        double numberValue,
        GameEventScriptBytecodeInstructionUnit? unit,
        bool isInteger,
        bool isNaN,
        bool isInfinity,
        bool isNegativeInfinity)
    {
        IntegerValue = integerValue;
        NumberValue = numberValue;
        Unit = isNaN || isInfinity ? GameEventScriptBytecodeInstructionUnit.UnitNone : unit.ToStoredUnit();
        IsIntegerValue = isInteger;
        IsNaNValue = isNaN;
        IsInfinityValue = isInfinity;
        IsNegativeInfinityValue = isNegativeInfinity;
    }

    public long IntegerValue { get; }
    public double NumberValue { get; }
    public override GameEventScriptBytecodeInstructionUnit Unit { get; }
    public bool IsIntegerValue { get; }
    public bool IsFractionalValue => !IsIntegerValue && !IsNaNValue && !IsInfinityValue;
    public bool IsNaNValue { get; }
    public bool IsInfinityValue { get; }
    public bool IsNegativeInfinityValue { get; }
    public override GameEventScriptValueKind Kind => GameEventScriptValueKind.Number;

    public override string AsText() => ToString();

    public override bool AsBoolean() => !IsNaNValue && AsNumber() != 0;

    public override long AsInteger()
    {
        if (IsNaNValue) return 0;
        if (IsInfinityValue) return IsNegativeInfinityValue ? long.MinValue : long.MaxValue;
        return IsIntegerValue ? IntegerValue : ToIntegerSaturated(NumberValue);
    }

    public override double AsNumber()
        => IsNaNValue ? 0d :
            IsInfinityValue ? IsNegativeInfinityValue ? double.MinValue : double.MaxValue :
            IsIntegerValue ? IntegerValue : NumberValue;

    public override bool HasSemanticValue() => !IsNaNValue;

    internal override bool TryConvertToNumber(out GameEventScriptValue value)
    {
        value = Unit.IsNumericUnit()
            ? IsIntegerValue ? GesInteger(IntegerValue) : GesFloat(NumberValue)
            : this;
        return true;
    }

    internal override bool TryConvertToInteger(out GameEventScriptValue value)
    {
        value = GesInteger(AsInteger());
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
