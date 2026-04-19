#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace StepH.Flow.EventScript.Types;

internal sealed class EventScriptTagValue : EventScriptValue
{
    private EventScriptTagValue(string value)
    {
        Value = value;
    }

    public string Value { get; }
    public override EventScriptValueType Type => EventScriptValueType.Tag;

    public static EventScriptTagValue Create(string? value) => new(value ?? string.Empty);
}
