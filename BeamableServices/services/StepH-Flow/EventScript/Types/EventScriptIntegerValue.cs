#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using static StepH.Flow.EventScript.Types.EventScriptValueFactory;
namespace StepH.Flow.EventScript.Types;

public sealed class EventScriptIntegerValue : EventScriptValue
{
    
    public static EventScriptIntegerValue EventScriptInteger(long value) => new(value);

    private EventScriptIntegerValue(long value)
    {
        Value = value;
    }

    public long Value { get; }
    public override EventScriptValueKind Kind => EventScriptValueKind.Integer;

    public override string AsText() => ToString();

    public override bool AsBoolean() => Value != 0;

    public override long AsInteger() => Value;

    public override decimal AsNumber() => Value;

    internal override bool TryConvertToNumber(out EventScriptValue value)
    {
        value = Decimal(Value);
        return true;
    }

    internal override bool TryConvertToInteger(out EventScriptValue value)
    {
        value = this;
        return true;
    }

    internal override bool TryConvertToBoolean(out EventScriptValue value)
    {
        value = Boolean(Value != 0);
        return true;
    }

    internal override bool TryConvertToText(out EventScriptValue value)
    {
        value = Text(ToString());
        return true;
    }

}
