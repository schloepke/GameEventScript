// ReSharper disable MemberCanBePrivate.Global

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.Api;

/// <summary>
/// Provides a set of static factory methods to create instances of <c>GameEventScriptValue</c>
/// based on various types and data structures. These methods are used to construct script
/// values for handling game events with specific formats and semantics.
/// </summary>
public static class GameEventScriptValueFactory
{
    /// <summary>
    /// Returns a predefined instance representing the "nothing" value
    /// for GameEventScript operations.
    /// </summary>
    /// <returns>A <see cref="GameEventScriptValue"/> instance representing nothing.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesNothing() => GameEventScriptNothingValue.Instance;
    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing a text value.
    /// </summary>
    /// <param name="value">The text value to encapsulate. Cannot be null or empty.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance containing the text value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesText(string value) => GameEventScriptTextValue.Create(value);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing a tag value.
    /// </summary>
    /// <param name="value">The tag value to encapsulate. Cannot be null or empty.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance containing the tag value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesTag(string value) => GameEventScriptTagValue.Create(value);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> encapsulating a double value
    /// with an optional unit.
    /// </summary>
    /// <param name="value">The double value to encapsulate.</param>
    /// <param name="unit">An optional unit of type <see cref="GameEventScriptNumericUnit"/>
    /// representing the measurement unit of the value. Defaults to null.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance containing the double value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesFloat(double value, GameEventScriptNumericUnit? unit = null) => GameEventScriptFloatValue.Create(value, unit);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing a percentage value.
    /// </summary>
    /// <param name="ratio">The percentage value to encapsulate. Must be a valid double value representing a ratio.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance containing the percentage value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesPercentage(double ratio) => GameEventScriptPercentageValue.Create(ratio);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing a degree value.
    /// </summary>
    /// <param name="degrees">The degree value to encapsulate. Represents an angle measurement in degrees.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance containing the degree value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesDegree(double degrees) => GameEventScriptFloatValue.Create(degrees, GameEventScriptNumericUnit.Degree);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing a value in meters.
    /// </summary>
    /// <param name="meters">The value in meters to encapsulate. Must be a valid double number.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance containing the specified value in meters.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesMeter(double meters) => GameEventScriptFloatValue.Create(meters, GameEventScriptNumericUnit.Meter);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing a duration in seconds.
    /// </summary>
    /// <param name="seconds">The duration value to encapsulate, measured in seconds.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance containing the duration value with the "second" unit.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesSeconds(double seconds) => GameEventScriptFloatValue.Create(seconds, GameEventScriptNumericUnit.Second);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing a vector.
    /// </summary>
    /// <param name="x">The X component of the vector.</param>
    /// <param name="y">The Y component of the vector.</param>
    /// <param name="z">The Z component of the vector.</param>
    /// <param name="unit">The unit of measurement for the vector values. Can be null if no unit is specified.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance representing the vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesVector(double x, double y = 0d, double z = 0d, GameEventScriptNumericUnit? unit = null) => GameEventScriptVectorValue.Create(x, y, z, unit);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing a point.
    /// </summary>
    /// <param name="x">The X-coordinate of the point.</param>
    /// <param name="y">The Y-coordinate of the point.</param>
    /// <param name="z">The Z-coordinate of the point.</param>
    /// <param name="unit">The unit of measurement for the point coordinates. Can be null if no unit is specified.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance representing the point.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesPoint(double x, double y = 0d, double z = 0d, GameEventScriptNumericUnit? unit = null) => GameEventScriptPointValue.Create(x, y, z, unit);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptFloatValue"/> representing a "Not-a-Number" (NaN) double value.
    /// </summary>
    /// <returns>A <see cref="GameEventScriptFloatValue"/> instance preconfigured as NaN.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesFloatNaN() => GameEventScriptFloatValue.NaN;

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing a positive infinity double value.
    /// </summary>
    /// <returns>A <see cref="GameEventScriptValue"/> instance equivalent to a positive infinity double constant.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesFloatInfinity() => GameEventScriptFloatValue.Infinity;

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing a double
    /// value equivalent to negative infinity.
    /// </summary>
    /// <returns>A <see cref="GameEventScriptValue"/> instance representing negative infinity.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesFloatNegativeInfinity() => GameEventScriptFloatValue.NegativeInfinity;

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing an integer value.
    /// </summary>
    /// <param name="value">The integer value to encapsulate.</param>
    /// <param name="unit">An optional measurement unit to attach to the integer value.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance containing the integer value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesInteger(long value, GameEventScriptNumericUnit? unit = null) => GameEventScriptIntegerValue.Create(value, unit);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing a boolean value.
    /// </summary>
    /// <param name="value">The boolean value to encapsulate.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance containing the boolean value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesBoolean(bool value) => GameEventScriptBooleanValue.Create(value);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptOptionalValue"/> wrapping the provided value.
    /// </summary>
    /// <param name="value">The value to wrap. Cannot be null.</param>
    /// <returns>A new <see cref="GameEventScriptOptionalValue"/> instance encapsulating the provided value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesOptionalSome(GameEventScriptValue value) => GameEventScriptOptionalValue.Create(value);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing an optional "none" value.
    /// </summary>
    /// <returns>A <see cref="GameEventScriptOptionalValue"/> instance representing the absence of a value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesOptionalNone() => GameEventScriptOptionalValue.None;

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing a sequence value.
    /// </summary>
    /// <param name="mode">The mode of the sequence, specifying how the source value should be interpreted (e.g., Values, Keys, or Entries).</param>
    /// <param name="source">The source <see cref="GameEventScriptValue"/> to use as the basis of the sequence. Cannot be null.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance representing the specified sequence.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesSequence(GameEventScriptSequenceMode mode, GameEventScriptValue source) => GameEventScriptSequenceValue.Create(mode, source);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing a sequence of values.
    /// </summary>
    /// <param name="values">The collection of <see cref="GameEventScriptValue"/> instances to include in the sequence. Can be null or empty.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance encapsulating the sequence of values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesSequence(IEnumerable<GameEventScriptValue>? values) => GameEventScriptSequenceValue.Create(GameEventScriptSequenceMode.Values, GesList(values));

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptRangeValue"/> representing a numeric range.
    /// </summary>
    /// <param name="from">The starting value of the range.</param>
    /// <param name="to">The ending value of the range.</param>
    /// <param name="step">The step increment between values in the range. Defaults to 1.</param>
    /// <returns>A new <see cref="GameEventScriptRangeValue"/> instance representing the specified range.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesRange(long from, long to, long step = 1) => GameEventScriptRangeValue.Create(from, to, step);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing a message value.
    /// </summary>
    /// <param name="message">The message to encapsulate. Cannot be null.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance containing the message value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesMessage(GameEventScriptMessage message) => GameEventScriptMessageValue.Create(message);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing a handler value.
    /// </summary>
    /// <param name="signature">The message signature used to create the handler value. Cannot be null.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance encapsulating the handler value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesHandler(GameEventScriptMessageSignature signature) => GameEventScriptHandlerValue.Create(signature);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing a typed reference.
    /// </summary>
    /// <param name="typeName">The referenced record or external type name. A leading colon is allowed.</param>
    /// <param name="id">The stable reference id.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance representing the typed reference.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesRef(string typeName, string id) => GameEventScriptRefValue.Create(typeName, id);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing a sequence of values derived from the provided source value.
    /// </summary>
    /// <param name="source">The source <see cref="GameEventScriptValue"/> to extract values from. Cannot be null.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance containing the resulting sequence of values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesValues(GameEventScriptValue source) => GesSequence(GameEventScriptSequenceMode.Values, source);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing the keys of a specified sequence value.
    /// </summary>
    /// <param name="source">The source sequence value. Must represent a sequence in a valid format.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance containing the keys of the specified sequence value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesKeys(GameEventScriptValue source) => GesSequence(GameEventScriptSequenceMode.Keys, source);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing a sequence
    /// in entry mode derived from the specified source.
    /// </summary>
    /// <param name="source">The source <see cref="GameEventScriptValue"/> used to generate the entry sequence. Cannot be null.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance containing the entry sequence.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesEntries(GameEventScriptValue source) => GesSequence(GameEventScriptSequenceMode.Entries, source);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptListValue"/> representing a list of script values.
    /// </summary>
    /// <param name="values">The collection of <see cref="GameEventScriptValue"/> elements to include in the list. Can be null.</param>
    /// <returns>A new <see cref="GameEventScriptListValue"/> instance containing the provided values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesList(IEnumerable<GameEventScriptValue>? values) => GameEventScriptListValue.Create(values);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptDictionaryValue"/> encapsulating a dictionary of key-value pairs.
    /// </summary>
    /// <param name="values">The dictionary containing key-value pairs where keys are strings and values are instances of <see cref="GameEventScriptValue"/>. Can be null.</param>
    /// <returns>A new <see cref="GameEventScriptDictionaryValue"/> instance representing the key-value pairs.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesDictionary(IReadOnlyDictionary<string, GameEventScriptValue>? values) => GameEventScriptDictionaryValue.Create(values);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing a custom type value.
    /// </summary>
    /// <param name="typeName">The name of the custom type to create. Cannot be null or empty.</param>
    /// <param name="values">The dictionary of field names and their respective <see cref="GameEventScriptValue"/> values. Can be null.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance encapsulating the custom type with the specified fields.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesCustomType(string typeName, IReadOnlyDictionary<string, GameEventScriptValue>? values) => GameEventScriptDictionaryValue.Create(typeName, values);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing a set of values.
    /// </summary>
    /// <param name="values">The collection of <see cref="GameEventScriptValue"/> elements to include in the set. Can be null to create an empty set.</param>
    /// <returns>A new <see cref="GameEventScriptSetValue"/> instance containing the specified values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesSet(IEnumerable<GameEventScriptValue>? values) => GameEventScriptSetValue.Create(values);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing a dice value.
    /// </summary>
    /// <param name="diceValue">The dice value to encapsulate. Can be null, in which case an empty dice value is created.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance containing the dice value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesDice(GameEventScriptDiceValue? diceValue) => GameEventScriptDiceValue.Create(diceValue);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing a dice roll result.
    /// </summary>
    /// <param name="rolls">An enumerable collection of integers representing the dice rolls. Can be null to represent no rolls.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance encapsulating the dice roll data.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesDice(IEnumerable<int>? rolls) => GameEventScriptDiceValue.Create(rolls);
}
