#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace StepH.Flow.EventScript.Types;

internal sealed class EventScriptNothingValue : EventScriptValue
{
    public static readonly EventScriptNothingValue Instance = new();

    private EventScriptNothingValue()
    {
    }

    public override EventScriptValueType Type => EventScriptValueType.Nothing;
}
