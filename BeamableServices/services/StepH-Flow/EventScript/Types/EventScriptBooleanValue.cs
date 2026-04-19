#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace StepH.Flow.EventScript.Types;

public sealed class EventScriptBooleanValue : EventScriptValue
{
    private static readonly EventScriptBooleanValue True = new(true);
    private static readonly EventScriptBooleanValue False = new(false);

    private EventScriptBooleanValue(bool value)
    {
        Value = value;
    }

    public bool Value { get; }
    public override EventScriptValueType Type => EventScriptValueType.Boolean;

    public static EventScriptBooleanValue FromBoolean(bool value) => value ? True : False;
}
