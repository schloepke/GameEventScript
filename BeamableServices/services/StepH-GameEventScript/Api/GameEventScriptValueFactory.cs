// ReSharper disable MemberCanBePrivate.Global

using System;
using System.Collections.Generic;
using System.Linq;
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
    /// <param name="unit">An optional unit of type <see cref="GameEventScriptBytecodeInstructionUnit"/>
    /// representing the measurement unit of the value. Defaults to null.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance containing the double value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesFloat(double value, GameEventScriptBytecodeInstructionUnit? unit = null) => GameEventScriptNumberValue.CreateFloat(value, unit);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing a number value.
    /// Exact finite whole values are represented as integer numbers internally.
    /// </summary>
    /// <param name="value">The numeric value to encapsulate.</param>
    /// <param name="unit">An optional measurement unit to attach to the number value.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance containing the number value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesNumber(double value, GameEventScriptBytecodeInstructionUnit? unit = null) => GameEventScriptNumberValue.CreateFloat(value, unit);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing a percentage value.
    /// </summary>
    /// <param name="ratio">The percentage value to encapsulate. Must be a valid double value representing a ratio.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance containing the percentage value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesPercentage(double ratio) => GameEventScriptNumberValue.CreatePercentage(ratio);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing a degree value.
    /// </summary>
    /// <param name="degrees">The degree value to encapsulate. Represents an angle measurement in degrees.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance containing the degree value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesDegree(double degrees) => GameEventScriptNumberValue.CreateFloat(degrees, GameEventScriptBytecodeInstructionUnit.UnitDegree);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing a value in meters.
    /// </summary>
    /// <param name="meters">The value in meters to encapsulate. Must be a valid double number.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance containing the specified value in meters.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesMeter(double meters) => GameEventScriptNumberValue.CreateFloat(meters, GameEventScriptBytecodeInstructionUnit.UnitMeter);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing a duration in seconds.
    /// </summary>
    /// <param name="seconds">The duration value to encapsulate, measured in seconds.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance containing the duration value with the "second" unit.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesSeconds(double seconds) => GameEventScriptNumberValue.CreateFloat(seconds, GameEventScriptBytecodeInstructionUnit.UnitSecond);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing a vector.
    /// </summary>
    /// <param name="x">The X component of the vector.</param>
    /// <param name="y">The Y component of the vector.</param>
    /// <param name="z">The Z component of the vector.</param>
    /// <param name="unit">The unit of measurement for the vector values. Can be null if no unit is specified.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance representing the vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesVector(double x, double y = 0d, double z = 0d, GameEventScriptBytecodeInstructionUnit? unit = null) => GameEventScriptVectorValue.Create(x, y, z, unit);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing a point.
    /// </summary>
    /// <param name="x">The X-coordinate of the point.</param>
    /// <param name="y">The Y-coordinate of the point.</param>
    /// <param name="z">The Z-coordinate of the point.</param>
    /// <param name="unit">The unit of measurement for the point coordinates. Can be null if no unit is specified.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance representing the point.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesPoint(double x, double y = 0d, double z = 0d, GameEventScriptBytecodeInstructionUnit? unit = null) => GameEventScriptPointValue.Create(x, y, z, unit);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptNumberValue"/> representing a "Not-a-Number" (NaN) double value.
    /// </summary>
    /// <returns>A <see cref="GameEventScriptNumberValue"/> instance preconfigured as NaN.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesFloatNaN() => GameEventScriptNumberValue.NaN;

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing a positive infinity double value.
    /// </summary>
    /// <returns>A <see cref="GameEventScriptValue"/> instance equivalent to a positive infinity double constant.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesFloatInfinity() => GameEventScriptNumberValue.Infinity;

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing a double
    /// value equivalent to negative infinity.
    /// </summary>
    /// <returns>A <see cref="GameEventScriptValue"/> instance representing negative infinity.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesFloatNegativeInfinity() => GameEventScriptNumberValue.NegativeInfinity;

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing an integer value.
    /// </summary>
    /// <param name="value">The integer value to encapsulate.</param>
    /// <param name="unit">An optional measurement unit to attach to the integer value.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance containing the integer value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesInteger(long value, GameEventScriptBytecodeInstructionUnit? unit = null) => GameEventScriptNumberValue.CreateInteger(value, unit);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing a boolean value.
    /// </summary>
    /// <param name="value">The boolean value to encapsulate.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance containing the boolean value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesBoolean(bool value) => GameEventScriptBooleanValue.Create(value);

    /// <summary>
    /// Returns the given value, or <see cref="GameEventScriptNothingValue.Instance"/> when it is null.
    /// </summary>
    /// <param name="value">The maybe-present value.</param>
    /// <returns>The provided value, or the canonical nothing value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesMaybe(GameEventScriptValue? value) => value ?? GesNothing();

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptSeriesValue"/> from an index-addressed series provider.
    /// </summary>
    /// <param name="series">The series provider.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance representing the series.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesSeries(IGameEventScriptSeries series) => GameEventScriptSeriesValue.Create(series);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptSeriesValue"/> from a term provider delegate.
    /// </summary>
    /// <param name="signatureId">A stable identifier for the series and its arguments.</param>
    /// <param name="termProvider">A function that returns a term for a zero-based index.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance representing the series.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesSeries(string signatureId, Func<long, GameEventScriptValue> termProvider) => GameEventScriptSeriesValue.Create(signatureId, termProvider);

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
    /// Creates a new instance of <see cref="GameEventScriptRangeValue"/> representing a numeric range.
    /// </summary>
    /// <param name="from">The starting value of the range.</param>
    /// <param name="to">The ending value of the range.</param>
    /// <param name="step">The step increment between values in the range. Defaults to 1.</param>
    /// <returns>A new <see cref="GameEventScriptRangeValue"/> instance representing the specified range.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesRange(double from, double to, double step = 1d) => GameEventScriptRangeValue.Create(from, to, step);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing a handler value.
    /// </summary>
    /// <param name="signature">The message signature used to create the handler value. Cannot be null.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance encapsulating the handler value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesHandler(GameEventScriptMessageSignature signature) => GameEventScriptHandlerValue.Create(signature);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing a list of values derived from the provided source value.
    /// </summary>
    /// <param name="source">The source <see cref="GameEventScriptValue"/> to extract values from. Cannot be null.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance containing the resulting sequence of values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesValues(GameEventScriptValue source)
    {
        source ??= GesNothing();
        return source.IsNothing()
            ? GesNothing()
            : source.Kind == GameEventScriptBytecodeTypeKind.Map
                ? GesList(EnumerateValues(source))
                : GesNothing();
    }

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing the keys of a map value.
    /// </summary>
    /// <param name="source">The source sequence value. Must represent a sequence in a valid format.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance containing the keys of the specified sequence value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesKeys(GameEventScriptValue source)
    {
        source ??= GesNothing();
        return source.IsNothing()
            ? GesNothing()
            : source.Kind == GameEventScriptBytecodeTypeKind.Map
                ? GesList(EnumerateKeys(source))
                : GesNothing();
    }

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing map entries
    /// derived from the specified source.
    /// </summary>
    /// <param name="source">The source <see cref="GameEventScriptValue"/> used to generate the entry sequence. Cannot be null.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance containing the entry sequence.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesEntries(GameEventScriptValue source)
    {
        source ??= GesNothing();
        return source.IsNothing()
            ? GesNothing()
            : source.Kind == GameEventScriptBytecodeTypeKind.Map
                ? GesList(EnumerateEntries(source))
                : GesNothing();
    }

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptListValue"/> representing a list of script values.
    /// </summary>
    /// <param name="values">The collection of <see cref="GameEventScriptValue"/> elements to include in the list. Can be null.</param>
    /// <returns>A new <see cref="GameEventScriptListValue"/> instance containing the provided values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesList(IEnumerable<GameEventScriptValue>? values) => GameEventScriptListValue.Create(values);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptMapValue"/> encapsulating a dictionary of key-value pairs.
    /// </summary>
    /// <param name="values">The dictionary containing key-value pairs where keys are strings and values are instances of <see cref="GameEventScriptValue"/>. Can be null.</param>
    /// <returns>A new <see cref="GameEventScriptMapValue"/> instance representing the key-value pairs.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesMap(IReadOnlyDictionary<string, GameEventScriptValue>? values) => GameEventScriptMapValue.Create(values);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptValue"/> representing a custom type value.
    /// </summary>
    /// <param name="typeName">The name of the custom type to create. Cannot be null or empty.</param>
    /// <param name="values">The dictionary of field names and their respective <see cref="GameEventScriptValue"/> values. Can be null.</param>
    /// <returns>A new <see cref="GameEventScriptValue"/> instance encapsulating the custom type with the specified fields.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptValue GesCustomType(string typeName, IReadOnlyDictionary<string, GameEventScriptValue>? values) => GameEventScriptMapValue.Create(typeName, values);

    private static IEnumerable<GameEventScriptValue> EnumerateValues(GameEventScriptValue? source)
    {
        source ??= GesNothing();
        if (source.Kind != GameEventScriptBytecodeTypeKind.Map)
        {
            yield break;
        }

        foreach (var key in source.AsMap().Keys.OrderBy(key => key, StringComparer.Ordinal))
        {
            yield return source.AsMap()[key];
        }
    }

    private static IEnumerable<GameEventScriptValue> EnumerateKeys(GameEventScriptValue? source)
    {
        source ??= GesNothing();
        if (source.Kind != GameEventScriptBytecodeTypeKind.Map)
        {
            yield break;
        }

        foreach (var key in source.AsMap().Keys.OrderBy(key => key, StringComparer.Ordinal))
        {
            yield return GesTag(key);
        }
    }

    private static IEnumerable<GameEventScriptValue> EnumerateEntries(GameEventScriptValue? source)
    {
        source ??= GesNothing();
        if (source.Kind != GameEventScriptBytecodeTypeKind.Map)
        {
            yield break;
        }

        foreach (var pair in source.AsMap().OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            yield return GesMap(new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal)
            {
                ["key"] = GesTag(pair.Key),
                ["value"] = pair.Value
            });
        }
    }

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
