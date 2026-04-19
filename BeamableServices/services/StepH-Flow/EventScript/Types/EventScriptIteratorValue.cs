#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace StepH.Flow.EventScript.Types;

public enum EventScriptIteratorMode
{
    Values,
    Keys,
    Entries
}

public sealed class EventScriptIteratorValue : EventScriptValue
{
    private EventScriptIteratorValue(EventScriptIteratorMode mode, EventScriptValue source)
    {
        Mode = mode;
        Source = source ?? EventScriptValue.Nothing;
    }

    public EventScriptIteratorMode Mode { get; }
    public EventScriptValue Source { get; }
    public override EventScriptValueType Type => EventScriptValueType.Iterator;

    public static EventScriptIteratorValue Create(EventScriptIteratorMode mode, EventScriptValue source)
        => new(mode, source ?? EventScriptValue.Nothing);
}
