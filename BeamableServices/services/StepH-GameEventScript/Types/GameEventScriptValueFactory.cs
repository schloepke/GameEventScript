#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Collections.Generic;

namespace StepH.GameEventScript.Types;

public static class GameEventScriptValueFactory
{
    public static GameEventScriptValue Text(string value) => GameEventScriptTextValue.GameEventScriptText(value);

    public static GameEventScriptValue Tag(string value) => GameEventScriptTagValue.GameEventScriptTag(value);

    public static GameEventScriptValue Decimal(decimal value, GameEventScriptDecimalUnit? unit = null) => GameEventScriptDecimalValue.GameEventScriptDecimal(value, unit);

    public static GameEventScriptValue Percentage(decimal ratio) => GameEventScriptPercentageValue.GameEventScriptPercentage(ratio);

    public static GameEventScriptValue Degree(decimal degrees) => GameEventScriptDecimalValue.GameEventScriptDecimal(degrees, GameEventScriptDecimalUnit.Degree);

    public static GameEventScriptValue Meter(decimal meters) => GameEventScriptDecimalValue.GameEventScriptDecimal(meters, GameEventScriptDecimalUnit.Meter);

    public static GameEventScriptValue Seconds(decimal seconds) => GameEventScriptDecimalValue.GameEventScriptDecimal(seconds, GameEventScriptDecimalUnit.Second);

    public static GameEventScriptValue Vector2(decimal x, decimal y, GameEventScriptDecimalUnit? unit = null) => GameEventScriptVector2Value.GameEventScriptVector2(x, y, unit);

    public static GameEventScriptValue Vector3(decimal x, decimal y, decimal z, GameEventScriptDecimalUnit? unit = null) => GameEventScriptVector3Value.GameEventScriptVector3(x, y, z, unit);

    public static GameEventScriptValue DecimalNaN() => GameEventScriptDecimalValue.NaN;

    public static GameEventScriptValue DecimalInfinity() => GameEventScriptDecimalValue.Infinity;

    public static GameEventScriptValue DecimalNegativeInfinity() => GameEventScriptDecimalValue.NegativeInfinity;

    public static GameEventScriptValue Integer(long value) => GameEventScriptIntegerValue.GameEventScriptInteger(value);

    public static GameEventScriptValue Boolean(bool value) => GameEventScriptBooleanValue.GameEventScriptBoolean(value);

    public static GameEventScriptValue OptionalSome(GameEventScriptValue value) => GameEventScriptOptionalValue.GameEventScriptOptionalSome(value);

    public static GameEventScriptValue OptionalNone() => GameEventScriptOptionalValue.None;

    public static GameEventScriptValue Sequence(GameEventScriptSequenceMode mode, GameEventScriptValue source) => GameEventScriptSequenceValue.GameEventScriptSequence(mode, source);

    public static GameEventScriptValue Sequence(IEnumerable<GameEventScriptValue>? values) => GameEventScriptSequenceValue.GameEventScriptSequence(GameEventScriptSequenceMode.Values, List(values));

    public static GameEventScriptValue Range(long from, long to, long step = 1) => GameEventScriptRangeValue.GameEventScriptRange(from, to, step);

    public static GameEventScriptValue Message(GameEventScriptMessage message) => GameEventScriptMessageValue.GameEventScriptMessage(message);

    public static GameEventScriptValue Handler(GameEventScriptMessageSignature signature) => GameEventScriptHandlerValue.GameEventScriptHandler(signature);

    public static GameEventScriptValue Values(GameEventScriptValue source) => Sequence(GameEventScriptSequenceMode.Values, source);

    public static GameEventScriptValue Keys(GameEventScriptValue source) => Sequence(GameEventScriptSequenceMode.Keys, source);

    public static GameEventScriptValue Entries(GameEventScriptValue source) => Sequence(GameEventScriptSequenceMode.Entries, source);

    public static GameEventScriptValue List(IEnumerable<GameEventScriptValue>? values) => GameEventScriptListValue.GameEventScriptList(values);

    public static GameEventScriptValue Dictionary(IReadOnlyDictionary<string, GameEventScriptValue>? values) => GameEventScriptDictionaryValue.GameEventScriptDictionary(values);

    public static GameEventScriptValue CustomType(string typeName, IReadOnlyDictionary<string, GameEventScriptValue>? values) => GameEventScriptDictionaryValue.GameEventScriptCustomType(typeName, values);

    public static GameEventScriptValue Set(IEnumerable<GameEventScriptValue>? values) => GameEventScriptSetValue.GameEventScriptSet(values);

    public static GameEventScriptValue Dice(GameEventScriptDiceValue? diceValue) => GameEventScriptDiceValue.GameEventScriptDice(diceValue);

    public static GameEventScriptValue FromClr(object? value) => GameEventScriptClrValueConverter.FromClr(value);

    public static IReadOnlyList<GameEventScriptValue> FromClrList(IEnumerable<object?> values) => GameEventScriptClrValueConverter.FromClrList(values);
}
