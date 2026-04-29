#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using StepH.Flow.EventScript.Types;
using static StepH.Flow.EventScript.Types.EventScriptValueFactory;

namespace StepH.Flow.EventScript.RegisterVM;

internal enum RegisterVmValueKind
{
    Nothing,
    Boolean,
    Integer,
    Decimal,
    Percentage,
    Reference
}

internal readonly struct RegisterVmValue
{
    private readonly EventScriptValue? _reference;

    private RegisterVmValue(
        RegisterVmValueKind kind,
        long integer,
        decimal number,
        bool boolean,
        EventScriptDecimalUnit? unit,
        EventScriptValue? reference)
    {
        Kind = kind;
        Integer = integer;
        Number = number;
        Boolean = boolean;
        Unit = unit;
        _reference = reference;
    }

    public static RegisterVmValue Nothing { get; } = new(RegisterVmValueKind.Nothing, 0, 0m, false, null, null);

    public RegisterVmValueKind Kind { get; }

    public long Integer { get; }

    public decimal Number { get; }

    public bool Boolean { get; }

    public EventScriptDecimalUnit? Unit { get; }

    public static RegisterVmValue FromBoolean(bool value)
        => new(RegisterVmValueKind.Boolean, 0, 0m, value, null, null);

    public static RegisterVmValue FromInteger(long value)
        => new(RegisterVmValueKind.Integer, value, value, value != 0, null, null);

    public static RegisterVmValue FromDecimal(decimal value, EventScriptDecimalUnit? unit = null)
        => new(RegisterVmValueKind.Decimal, 0, value, value != 0m, unit, null);

    public static RegisterVmValue FromPercentage(decimal ratio)
        => new(RegisterVmValueKind.Percentage, 0, ratio, ratio != 0m, null, null);

    public static RegisterVmValue FromReference(EventScriptValue value)
        => value switch
        {
            null => Nothing,
            EventScriptBooleanValue boolean => FromBoolean(boolean.Value),
            EventScriptIntegerValue integer => FromInteger(integer.Value),
            EventScriptDecimalValue decimalValue when decimalValue.HasSemanticValue() => FromDecimal(decimalValue.Value, decimalValue.Unit),
            EventScriptPercentageValue percentage => FromPercentage(percentage.Ratio),
            _ => new RegisterVmValue(RegisterVmValueKind.Reference, 0, 0m, false, null, value)
        };

    public EventScriptValue ToEventScriptValue()
        => Kind switch
        {
            RegisterVmValueKind.Nothing => EventScriptValue.Nothing,
            RegisterVmValueKind.Boolean => EventScriptValueFactory.Boolean(Boolean),
            RegisterVmValueKind.Integer => EventScriptValueFactory.Integer(Integer),
            RegisterVmValueKind.Decimal => Decimal(Number, Unit),
            RegisterVmValueKind.Percentage => Percentage(Number),
            RegisterVmValueKind.Reference => _reference ?? EventScriptValue.Nothing,
            _ => throw new InvalidOperationException($"Unsupported RegisterVM value kind '{Kind}'.")
        };
}
