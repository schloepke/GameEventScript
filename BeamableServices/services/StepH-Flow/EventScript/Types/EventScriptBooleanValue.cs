#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using static StepH.Flow.EventScript.Types.EventScriptValueFactory;
namespace StepH.Flow.EventScript.Types;

public sealed class EventScriptBooleanValue : EventScriptValue
{
    public static readonly EventScriptBooleanValue True = new(true);
    public static readonly EventScriptBooleanValue False = new(false);

    public static EventScriptBooleanValue EventScriptBoolean(bool value) => value ? True : False;

   private EventScriptBooleanValue(bool value)
    {
        Value = value;
    }

    public bool Value { get; }
    
    public override EventScriptValueKind Kind => EventScriptValueKind.Boolean;

    public override string AsText() => ToString();

    public override bool AsBoolean() => Value;

    public override long AsInteger() => Value ? 1 : 0;

    public override decimal AsNumber() => Value ? 1m : 0m;

    internal override bool TryConvertToNumber(out EventScriptValue value)
    {
        value = Decimal(Value ? 1m : 0m);
        return true;
    }

    internal override bool TryConvertToInteger(out EventScriptValue value)
    {
        value = Integer(Value ? 1 : 0);
        return true;
    }

    internal override bool TryConvertToBoolean(out EventScriptValue value)
    {
        value = this;
        return true;
    }

    internal override bool TryConvertToText(out EventScriptValue value)
    {
        value = Text(ToString());
        return true;
    }

}
