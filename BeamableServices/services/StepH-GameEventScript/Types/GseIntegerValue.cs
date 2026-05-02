#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using static StepH.GameEventScript.Types.GseValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class GseIntegerValue : GseValue
{
    
    public static GseIntegerValue GseInteger(long value) => new(value);

    private GseIntegerValue(long value)
    {
        Value = value;
    }

    public long Value { get; }
    public override GseValueKind Kind => GseValueKind.Integer;

    public override string AsText() => ToString();

    public override bool AsBoolean() => Value != 0;

    public override long AsInteger() => Value;

    public override decimal AsNumber() => Value;

    internal override bool TryConvertToNumber(out GseValue value)
    {
        value = Decimal(Value);
        return true;
    }

    internal override bool TryConvertToInteger(out GseValue value)
    {
        value = this;
        return true;
    }

    internal override bool TryConvertToBoolean(out GseValue value)
    {
        value = Boolean(Value != 0);
        return true;
    }

    internal override bool TryConvertToText(out GseValue value)
    {
        value = Text(ToString());
        return true;
    }

}
