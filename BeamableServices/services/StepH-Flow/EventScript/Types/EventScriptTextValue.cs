#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace StepH.Flow.EventScript.Types;

internal sealed class EventScriptTextValue : EventScriptValue
{
    private EventScriptTextValue(string value)
    {
        Value = value;
    }

    public string Value { get; }
    public override EventScriptValueType Type => EventScriptValueType.Text;

    public static EventScriptTextValue Create(string? value) => new(value ?? string.Empty);
}
