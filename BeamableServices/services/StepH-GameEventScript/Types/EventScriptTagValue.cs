#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using static StepH.GameEventScript.Types.EventScriptValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class EventScriptTagValue : EventScriptValue
{
    public static readonly EventScriptTagValue Empty = new(string.Empty);

    public static EventScriptTagValue EventScriptTag(string? value) => string.IsNullOrEmpty(value) ? Empty : new EventScriptTagValue(value);

    private EventScriptTagValue(string value)
    {
        Value = value;
    }

    public string Value { get; }
    public override EventScriptValueKind Kind => EventScriptValueKind.Tag;

    public override string AsText() => Value;

    public override decimal AsNumber() => TryConvertToNumber(out var value) ? value.AsNumber() : 0m;

    public override IReadOnlyList<EventScriptValue> AsList() => CreateCharacterList(Value);

    public override IEnumerable<EventScriptValue> AsEnumerable() => AsList();

    internal override bool TryConvertToNumber(out EventScriptValue value)
    {
        if (string.Equals(Value, "infinity", StringComparison.Ordinal))
        {
            value = DecimalInfinity();
            return true;
        }

        if (string.Equals(Value, "negativeinfinity", StringComparison.Ordinal))
        {
            value = DecimalNegativeInfinity();
            return true;
        }

        if (string.Equals(Value, "nan", StringComparison.Ordinal))
        {
            value = DecimalNaN();
            return true;
        }

        value = default!;
        return false;
    }

    internal override bool TryConvertToText(out EventScriptValue value)
    {
        value = Text(Value);
        return true;
    }

    internal override bool TryConvertToList(out EventScriptValue value)
    {
        value = List(AsList());
        return true;
    }

}
