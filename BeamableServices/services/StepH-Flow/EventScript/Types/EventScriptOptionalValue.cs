using System.Collections.Generic;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace StepH.Flow.EventScript.Types;

public sealed class EventScriptOptionalValue : EventScriptValue
{
    public static readonly EventScriptOptionalValue None = new(false, Nothing);

    public static EventScriptOptionalValue EventScriptOptionalSome(EventScriptValue? value) => value == null ? None : new EventScriptOptionalValue(true, value);

    private EventScriptOptionalValue(bool hasValue, EventScriptValue value)
    {
        HasValue = hasValue;
        _value = value;
    }

    private readonly EventScriptValue _value;

    public bool HasValue { get; }
    public EventScriptValue Value => HasValue ? _value : Nothing;
    public override EventScriptValueKind Kind => EventScriptValueKind.Optional;

    public override EventScriptOptionalValue AsOptional() => this;

    public override string AsText() => TryConvertToText(out var value) ? value.AsText() : string.Empty;

    public override bool AsBoolean() => TryConvertToBoolean(out var value) && value.AsBoolean();

    public override long AsInteger() => TryConvertToInteger(out var value) ? value.AsInteger() : 0;

    public override decimal AsNumber() => TryConvertToNumber(out var value) ? value.AsNumber() : 0m;

    public override IReadOnlyList<EventScriptValue> AsList() => TryConvertToList(out var value) ? value.AsList() : System.Array.Empty<EventScriptValue>();

    public override IReadOnlyDictionary<string, EventScriptValue> AsDictionary() => TryConvertToDictionary(out var value) ? value.AsDictionary() : EventScriptDictionaryValue.EmptyView;

    public override ISet<EventScriptValue> AsSet() => TryConvertToSet(out var value) ? value.AsSet() : new SortedSet<EventScriptValue>(StableComparer);

    public override EventScriptDiceValue AsDice() => TryConvertToDice(out var value) ? value.AsDice() : EventScriptDiceValue.Empty;

    internal override bool TryConvertToNumber(out EventScriptValue value)
        => TryConvertValue(static source => source.TryConvertToNumber(out var converted) ? converted : null, out value);

    internal override bool TryConvertToInteger(out EventScriptValue value)
        => TryConvertValue(static source => source.TryConvertToInteger(out var converted) ? converted : null, out value);

    internal override bool TryConvertToBoolean(out EventScriptValue value)
        => TryConvertValue(static source => source.TryConvertToBoolean(out var converted) ? converted : null, out value);

    internal override bool TryConvertToText(out EventScriptValue value)
        => TryConvertValue(static source => source.TryConvertToText(out var converted) ? converted : null, out value);

    internal override bool TryConvertToList(out EventScriptValue value)
        => TryConvertValue(static source => source.TryConvertToList(out var converted) ? converted : null, out value);

    internal override bool TryConvertToDictionary(out EventScriptValue value)
        => TryConvertValue(static source => source.TryConvertToDictionary(out var converted) ? converted : null, out value);

    internal override bool TryConvertToSet(out EventScriptValue value)
        => TryConvertValue(static source => source.TryConvertToSet(out var converted) ? converted : null, out value);

    internal override bool TryConvertToDice(out EventScriptValue value)
        => TryConvertValue(static source => source.TryConvertToDice(out var converted) ? converted : null, out value);

    private bool TryConvertValue(System.Func<EventScriptValue, EventScriptValue?> converter, out EventScriptValue value)
    {
        if (!HasValue)
        {
            value = default!;
            return false;
        }

        var converted = converter(Value);
        if (converted == null)
        {
            value = default!;
            return false;
        }

        value = converted;
        return true;
    }

}
