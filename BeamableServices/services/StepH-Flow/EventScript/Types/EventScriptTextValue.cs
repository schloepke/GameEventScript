#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using static StepH.Flow.EventScript.Types.EventScriptValueFactory;
using System.Collections.Generic;
using System.Globalization;

namespace StepH.Flow.EventScript.Types;

public sealed class EventScriptTextValue : EventScriptValue
{
    public static readonly EventScriptTextValue Empty = new(string.Empty);

    public static EventScriptTextValue EventScriptText(string? value) => string.IsNullOrEmpty(value) ? Empty : new EventScriptTextValue(value);

    private EventScriptTextValue(string value)
    {
        Value = value;
    }

    public string Value { get; }
    public override EventScriptValueKind Kind => EventScriptValueKind.Text;

    public override string AsText() => Value;

    public override bool AsBoolean() => TryConvertToBoolean(out var value) && value.AsBoolean();

    public override long AsInteger() => TryConvertToInteger(out var value) ? value.AsInteger() : 0;

    public override decimal AsNumber() => TryConvertToNumber(out var value) ? value.AsNumber() : 0m;

    public override IReadOnlyList<EventScriptValue> AsList() => CreateCharacterList(Value);

    public override IEnumerable<EventScriptValue> AsEnumerable() => AsList();

    public override bool HasSemanticValue() => Value.Length > 0;

    public override bool IsSemanticallyEmpty() => Value.Length == 0;

    public override bool Contains(EventScriptValue needle)
        => Value.Contains(ToComparableText(needle), System.StringComparison.Ordinal);

    public override bool StartsWith(EventScriptValue prefix)
        => prefix.Kind == EventScriptValueKind.Text && Value.StartsWith(prefix.AsText(), System.StringComparison.Ordinal);

    public override bool EndsWith(EventScriptValue suffix)
        => suffix.Kind == EventScriptValueKind.Text && Value.EndsWith(suffix.AsText(), System.StringComparison.Ordinal);

    internal override bool TryConvertToNumber(out EventScriptValue value)
    {
        if (decimal.TryParse(Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
        {
            value = Decimal(number);
            return true;
        }

        value = default!;
        return false;
    }

    internal override bool TryConvertToInteger(out EventScriptValue value)
    {
        if (long.TryParse(Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
        {
            value = Integer(integer);
            return true;
        }

        if (decimal.TryParse(Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalNumber))
        {
            value = Integer(ToIntegerSaturated(decimalNumber));
            return true;
        }

        value = default!;
        return false;
    }

    internal override bool TryConvertToBoolean(out EventScriptValue value)
    {
        if (bool.TryParse(Value, out var boolean))
        {
            value = Boolean(boolean);
            return true;
        }

        if (string.Equals(Value, "1", System.StringComparison.Ordinal))
        {
            value = Boolean(true);
            return true;
        }

        if (string.Equals(Value, "0", System.StringComparison.Ordinal))
        {
            value = Boolean(false);
            return true;
        }

        value = default!;
        return false;
    }

    internal override bool TryConvertToText(out EventScriptValue value)
    {
        value = this;
        return true;
    }

    internal override bool TryConvertToList(out EventScriptValue value)
    {
        value = List(AsList());
        return true;
    }

}
