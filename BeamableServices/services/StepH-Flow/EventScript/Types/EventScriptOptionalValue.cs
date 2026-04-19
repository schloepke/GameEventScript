#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace StepH.Flow.EventScript.Types;

public sealed class EventScriptOptionalValue : EventScriptValue
{
    private static readonly EventScriptValue EmptyValue = EventScriptValue.Integer(0);
    private readonly EventScriptValue _value;

    private EventScriptOptionalValue(bool hasValue, EventScriptValue value)
    {
        HasValue = hasValue;
        _value = value;
    }

    public bool HasValue { get; }
    public EventScriptValue Value => HasValue ? _value : EventScriptValue.Nothing;
    public override EventScriptValueType Type => EventScriptValueType.Optional;

    public static EventScriptOptionalValue Of(EventScriptValue? value) => value == null ? None() : new EventScriptOptionalValue(true, value);
    public static EventScriptOptionalValue Some(EventScriptValue value) => Of(value);
    public static EventScriptOptionalValue None() => new(false, EmptyValue);
}
