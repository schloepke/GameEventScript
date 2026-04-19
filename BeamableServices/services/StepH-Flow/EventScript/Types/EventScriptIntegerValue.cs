#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace StepH.Flow.EventScript.Types;

internal sealed class EventScriptIntegerValue : EventScriptValue
{
    private EventScriptIntegerValue(long value)
    {
        Value = value;
    }

    public long Value { get; }
    public override EventScriptValueType Type => EventScriptValueType.Integer;

    public static EventScriptIntegerValue FromInteger(long value) => new(value);
}
