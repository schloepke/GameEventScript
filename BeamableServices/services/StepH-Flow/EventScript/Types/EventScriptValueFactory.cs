#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Collections.Generic;

namespace StepH.Flow.EventScript.Types;

public static class EventScriptValueFactory
{
    public static EventScriptValue Text(string value) => EventScriptTextValue.EventScriptText(value);

    public static EventScriptValue Tag(string value) => EventScriptTagValue.EventScriptTag(value);

    public static EventScriptValue Decimal(decimal value, EventScriptDecimalUnit? unit = null) => EventScriptDecimalValue.EventScriptDecimal(value, unit);

    public static EventScriptValue Percentage(decimal ratio) => EventScriptPercentageValue.EventScriptPercentage(ratio);

    public static EventScriptValue Degree(decimal degrees) => EventScriptDecimalValue.EventScriptDecimal(degrees, EventScriptDecimalUnit.Degree);

    public static EventScriptValue Meter(decimal meters) => EventScriptDecimalValue.EventScriptDecimal(meters, EventScriptDecimalUnit.Meter);

    public static EventScriptValue Seconds(decimal seconds) => EventScriptDecimalValue.EventScriptDecimal(seconds, EventScriptDecimalUnit.Second);

    public static EventScriptValue Vector2(decimal x, decimal y) => EventScriptVector2Value.EventScriptVector2(x, y);

    public static EventScriptValue Vector3(decimal x, decimal y, decimal z) => EventScriptVector3Value.EventScriptVector3(x, y, z);

    public static EventScriptValue DecimalNaN() => EventScriptDecimalValue.NaN;

    public static EventScriptValue DecimalInfinity() => EventScriptDecimalValue.Infinity;

    public static EventScriptValue DecimalNegativeInfinity() => EventScriptDecimalValue.NegativeInfinity;

    public static EventScriptValue Integer(long value) => EventScriptIntegerValue.EventScriptInteger(value);

    public static EventScriptValue Boolean(bool value) => EventScriptBooleanValue.EventScriptBoolean(value);

    public static EventScriptValue OptionalSome(EventScriptValue value) => EventScriptOptionalValue.EventScriptOptionalSome(value);

    public static EventScriptValue OptionalNone() => EventScriptOptionalValue.None;

    public static EventScriptValue Iterator(EventScriptIteratorMode mode, EventScriptValue source) => EventScriptIteratorValue.EventScriptIterator(mode, source);

    public static EventScriptValue Range(long from, long to, long step = 1) => EventScriptRangeValue.EventScriptRange(from, to, step);

    public static EventScriptValue Message(EventScriptMessage message) => EventScriptMessageValue.EventScriptMessage(message);

    public static EventScriptValue Handler(EventScriptMessageSignature signature) => EventScriptHandlerValue.EventScriptHandler(signature);

    public static EventScriptValue Values(EventScriptValue source) => Iterator(EventScriptIteratorMode.Values, source);

    public static EventScriptValue Keys(EventScriptValue source) => Iterator(EventScriptIteratorMode.Keys, source);

    public static EventScriptValue Entries(EventScriptValue source) => Iterator(EventScriptIteratorMode.Entries, source);

    public static EventScriptValue List(IEnumerable<EventScriptValue>? values) => EventScriptListValue.EventScriptList(values);

    public static EventScriptValue Dictionary(IReadOnlyDictionary<string, EventScriptValue>? values) => EventScriptDictionaryValue.EventScriptDictionary(values);

    public static EventScriptValue CustomType(string typeName, IReadOnlyDictionary<string, EventScriptValue>? values) => EventScriptDictionaryValue.EventScriptCustomType(typeName, values);

    public static EventScriptValue Set(IEnumerable<EventScriptValue>? values) => EventScriptSetValue.EventScriptSet(values);

    public static EventScriptValue Dice(EventScriptDiceValue? diceValue) => EventScriptDiceValue.EventScriptDice(diceValue);

    public static EventScriptValue FromClr(object? value) => EventScriptClrValueConverter.FromClr(value);

    public static IReadOnlyList<EventScriptValue> FromClrList(IEnumerable<object?> values) => EventScriptClrValueConverter.FromClrList(values);
}
