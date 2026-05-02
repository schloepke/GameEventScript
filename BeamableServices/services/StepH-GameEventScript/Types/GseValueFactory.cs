#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Collections.Generic;

namespace StepH.GameEventScript.Types;

public static class GseValueFactory
{
    public static GseValue Text(string value) => GseTextValue.GseText(value);

    public static GseValue Tag(string value) => GseTagValue.GseTag(value);

    public static GseValue Decimal(decimal value, GseDecimalUnit? unit = null) => GseDecimalValue.GseDecimal(value, unit);

    public static GseValue Percentage(decimal ratio) => GsePercentageValue.GsePercentage(ratio);

    public static GseValue Degree(decimal degrees) => GseDecimalValue.GseDecimal(degrees, GseDecimalUnit.Degree);

    public static GseValue Meter(decimal meters) => GseDecimalValue.GseDecimal(meters, GseDecimalUnit.Meter);

    public static GseValue Seconds(decimal seconds) => GseDecimalValue.GseDecimal(seconds, GseDecimalUnit.Second);

    public static GseValue Vector2(decimal x, decimal y, GseDecimalUnit? unit = null) => GseVector2Value.GseVector2(x, y, unit);

    public static GseValue Vector3(decimal x, decimal y, decimal z, GseDecimalUnit? unit = null) => GseVector3Value.GseVector3(x, y, z, unit);

    public static GseValue DecimalNaN() => GseDecimalValue.NaN;

    public static GseValue DecimalInfinity() => GseDecimalValue.Infinity;

    public static GseValue DecimalNegativeInfinity() => GseDecimalValue.NegativeInfinity;

    public static GseValue Integer(long value) => GseIntegerValue.GseInteger(value);

    public static GseValue Boolean(bool value) => GseBooleanValue.GseBoolean(value);

    public static GseValue OptionalSome(GseValue value) => GseOptionalValue.GseOptionalSome(value);

    public static GseValue OptionalNone() => GseOptionalValue.None;

    public static GseValue Sequence(GseSequenceMode mode, GseValue source) => GseSequenceValue.GseSequence(mode, source);

    public static GseValue Sequence(IEnumerable<GseValue>? values) => GseSequenceValue.GseSequence(GseSequenceMode.Values, List(values));

    public static GseValue Range(long from, long to, long step = 1) => GseRangeValue.GseRange(from, to, step);

    public static GseValue Message(GseMessage message) => GseMessageValue.GseMessage(message);

    public static GseValue Handler(GseMessageSignature signature) => GseHandlerValue.GseHandler(signature);

    public static GseValue Values(GseValue source) => Sequence(GseSequenceMode.Values, source);

    public static GseValue Keys(GseValue source) => Sequence(GseSequenceMode.Keys, source);

    public static GseValue Entries(GseValue source) => Sequence(GseSequenceMode.Entries, source);

    public static GseValue List(IEnumerable<GseValue>? values) => GseListValue.GseList(values);

    public static GseValue Dictionary(IReadOnlyDictionary<string, GseValue>? values) => GseDictionaryValue.GseDictionary(values);

    public static GseValue CustomType(string typeName, IReadOnlyDictionary<string, GseValue>? values) => GseDictionaryValue.GseCustomType(typeName, values);

    public static GseValue Set(IEnumerable<GseValue>? values) => GseSetValue.GseSet(values);

    public static GseValue Dice(GseDiceValue? diceValue) => GseDiceValue.GseDice(diceValue);

    public static GseValue FromClr(object? value) => GseClrValueConverter.FromClr(value);

    public static IReadOnlyList<GseValue> FromClrList(IEnumerable<object?> values) => GseClrValueConverter.FromClrList(values);
}
