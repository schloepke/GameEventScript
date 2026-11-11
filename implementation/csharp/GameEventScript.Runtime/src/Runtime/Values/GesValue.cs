// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using GameEventScript.Api;
using GameEventScript.Runtime;
using GameEventScript.Syntax;
using static GameEventScript.Api.GesBytecodeTypeKind;
using static GameEventScript.Api.GesQuantityUnit;
using static GameEventScript.Runtime.Values.GesValue.GesValueFlags;

namespace GameEventScript.Runtime.Values;

internal readonly record struct GesNumericWithUnit(double Value, GesQuantityUnit Unit);

/// <summary>
/// Represents a ges value.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "ConvertToAutoPropertyWithPrivateSetter")]
[StructLayout(LayoutKind.Explicit, Size = 32)]
public partial struct GesValue : IEquatable<GesValue>
{
    [Flags]
    internal enum GesValueFlags : byte
    {
        None = 0,

        IsTrueFlag = 1 << 0,
        IsFalseFlag = 1 << 1,
        HasValueFlag = 1 << 2,
        IsNumericFlag = 1 << 3,

        StorageObjectFlag = 1 << 4
    }

    [FieldOffset(0)] internal long IntegerValue;
    [FieldOffset(0)] internal double FloatValue;
    [FieldOffset(8)] internal object? ObjectValue;

    [FieldOffset(16)] internal GesBytecodeTypeKind Kind;
    [FieldOffset(17)] internal GesQuantityUnit Unit;
    [FieldOffset(18)] internal GesValueFlags Flags;

    /// <summary>
    /// Performs the ges nothing operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public static GesValue GesNothing() => new();

    /// <summary>
    /// Performs the ges boolean operation.
    /// </summary>
    /// <param name="boolean">The boolean value.</param>
    /// <returns>The result of the operation.</returns>
    public static GesValue GesBoolean(bool boolean)
    {
        var value = new GesValue();
        value.SetBoolean(boolean);
        return value;
    }

    /// <summary>
    /// Performs the ges integer operation.
    /// </summary>
    /// <param name="integer">The integer value.</param>
    /// <param name="unit">The unit value.</param>
    /// <returns>The result of the operation.</returns>
    public static GesValue GesInteger(long integer, GesQuantityUnit unit = UnitNone)
    {
        var value = new GesValue();
        value.SetInteger(integer, unit);
        return value;
    }

    /// <summary>
    /// Performs the ges float operation.
    /// </summary>
    /// <param name="number">The number value.</param>
    /// <param name="unit">The unit value.</param>
    /// <returns>The result of the operation.</returns>
    public static GesValue GesFloat(double number, GesQuantityUnit unit = UnitNone)
    {
        var value = new GesValue();
        value.SetFloat(number, unit);
        return value;
    }

    /// <summary>
    /// Performs the ges number operation.
    /// </summary>
    /// <param name="number">The number value.</param>
    /// <param name="unit">The unit value.</param>
    /// <returns>The result of the operation.</returns>
    public static GesValue GesNumber(double number, GesQuantityUnit unit = UnitNone)
        => GesFloat(number, unit);

    /// <summary>
    /// Performs the ges percentage operation.
    /// </summary>
    /// <param name="ratio">The ratio value.</param>
    /// <returns>The result of the operation.</returns>
    public static GesValue GesPercentage(double ratio)
    {
        var value = new GesValue();
        value.SetPercentage(ratio);
        return value;
    }

    /// <summary>
    /// Performs the ges text operation.
    /// </summary>
    /// <param name="text">The text value.</param>
    /// <returns>The result of the operation.</returns>
    public static GesValue GesText(string text)
    {
        text ??= string.Empty;
        GameEventScript.Syntax.GesText.RequireValidUnicode(text, nameof(text));
        var value = new GesValue();
        value.SetText(text);
        return value;
    }

    /// <summary>
    /// Performs the ges tag operation.
    /// </summary>
    /// <param name="tag">The tag value.</param>
    /// <returns>The result of the operation.</returns>
    public static GesValue GesTag(string tag)
    {
        tag ??= string.Empty;
        if (!GameEventScript.Syntax.GesText.IsTagValue(tag))
            throw new ArgumentException("Tag values must use the portable ASCII name grammar.", nameof(tag));
        var value = new GesValue();
        value.SetTag(tag);
        return value;
    }

    /// <summary>
    /// Performs the ges vector operation.
    /// </summary>
    /// <param name="x">The x value.</param>
    /// <param name="y">The y value.</param>
    /// <param name="z">The z value.</param>
    /// <param name="unit">The unit value.</param>
    /// <returns>The result of the operation.</returns>
    public static GesValue GesVector(double x, double y = 0d, double z = 0d, GesQuantityUnit unit = UnitNone)
    {
        var value = new GesValue();
        value.SetVector(x, y, z, unit);
        return value;
    }

    /// <summary>
    /// Performs the ges point operation.
    /// </summary>
    /// <param name="x">The x value.</param>
    /// <param name="y">The y value.</param>
    /// <param name="z">The z value.</param>
    /// <param name="unit">The unit value.</param>
    /// <returns>The result of the operation.</returns>
    public static GesValue GesPoint(double x, double y = 0d, double z = 0d, GesQuantityUnit unit = UnitNone)
    {
        var value = new GesValue();
        value.SetPoint(x, y, z, unit);
        return value;
    }

    /// <summary>
    /// Performs the ges list operation.
    /// </summary>
    /// <param name="items">The items value.</param>
    /// <returns>The result of the operation.</returns>
    public static GesValue GesList(GesValue[] items)
    {
        GesValue[] copy = items.Length == 0 ? [] : new GesValue[items.Length];
        if (items.Length > 0)
        {
            Array.Copy(items, copy, items.Length);
        }

        var value = new GesValue();
        value.SetList(copy);
        return value;
    }

    /// <summary>
    /// Wraps a host-provided external value.
    /// </summary>
    /// <param name="value">The host object.</param>
    /// <returns>The result of the operation.</returns>
    public static GesValue GesExternal(IGesExternalValue value)
    {
        var result = new GesValue();
        result.SetExternal(value);
        return result;
    }

    /// <summary>
    /// Creates a Map from parallel keys and values in canonical key order, keeping the last value of a duplicate key.
    /// Extra keys or values beyond the shorter array are ignored.
    /// </summary>
    /// <param name="keys">The keys value.</param>
    /// <param name="values">The values value.</param>
    /// <returns>The result of the operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an array or a key is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when a key is not valid Unicode.</exception>
    public static GesValue GesMap(string[] keys, GesValue[] values)
    {
        var value = new GesValue();
        value.SetMap(UniqueEntries(keys ?? throw new ArgumentNullException(nameof(keys)), values ?? throw new ArgumentNullException(nameof(values)), nameof(keys)));
        return value;
    }

    /// <summary>
    /// Creates a Map from ordered entries in canonical key order, keeping the last value of a duplicate key.
    /// </summary>
    /// <param name="entries">The ordered key-value entries.</param>
    /// <returns>The result of the operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the array or a key is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when a key is not valid Unicode.</exception>
    public static GesValue GesMap(params (string, GesValue)[] entries)
    {
        SplitEntries(entries ?? throw new ArgumentNullException(nameof(entries)), out var keys, out var values);
        var value = new GesValue();
        value.SetMap(UniqueEntries(keys, values, nameof(entries)));
        return value;
    }

    /// <summary>
    /// Creates a Record from parallel field names and values in canonical key order, keeping the last value of a
    /// duplicate name. Extra names or values beyond the shorter array are ignored.
    /// </summary>
    /// <param name="typeName">The type name value.</param>
    /// <param name="keys">The keys value.</param>
    /// <param name="values">The values value.</param>
    /// <returns>The result of the operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an array or a name is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when a name is not valid Unicode.</exception>
    public static GesValue GesRecord(string typeName, string[] keys, GesValue[] values)
    {
        var value = new GesValue();
        value.SetRecord(typeName ?? string.Empty, UniqueEntries(keys ?? throw new ArgumentNullException(nameof(keys)), values ?? throw new ArgumentNullException(nameof(values)), nameof(keys)));
        return value;
    }

    /// <summary>
    /// Creates a Record from ordered fields in canonical key order, keeping the last value of a duplicate name.
    /// </summary>
    /// <param name="typeName">The type name value.</param>
    /// <param name="fields">The ordered field name and value entries.</param>
    /// <returns>The result of the operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the array or a name is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when a name is not valid Unicode.</exception>
    public static GesValue GesRecord(string typeName, params (string, GesValue)[] fields)
    {
        SplitEntries(fields ?? throw new ArgumentNullException(nameof(fields)), out var keys, out var values);
        var value = new GesValue();
        value.SetRecord(typeName ?? string.Empty, UniqueEntries(keys, values, nameof(fields)));
        return value;
    }

    private static void SplitEntries((string, GesValue)[] entries, out string[] keys, out GesValue[] values)
    {
        keys = new string[entries.Length];
        values = new GesValue[entries.Length];
        for (var index = 0; index < entries.Length; index++)
            (keys[index], values[index]) = entries[index];
    }

    // Sorts once and keeps the highest input position of each run of equal keys, because Array.Sort is not stable.
    private static GesValueMap UniqueEntries(string[] keys, GesValue[] values, string parameterName)
    {
        var count = keys.Length < values.Length ? keys.Length : values.Length;
        var sortedKeys = new string[count];
        var positions = new int[count];
        for (var index = 0; index < count; index++)
        {
            GameEventScript.Syntax.GesText.RequireValidUnicode(keys[index] ?? throw new ArgumentNullException(parameterName), parameterName);
            sortedKeys[index] = keys[index];
            positions[index] = index;
        }

        Array.Sort(sortedKeys, positions, GameEventScript.Syntax.GesText.ScalarComparer);
        var sortedValues = new GesValue[count];
        var uniqueCount = 0;
        var lastPosition = -1;
        for (var index = 0; index < count; index++)
        {
            var position = positions[index];
            if (uniqueCount > 0 && string.Equals(sortedKeys[uniqueCount - 1], sortedKeys[index], StringComparison.Ordinal))
            {
                if (position < lastPosition) continue;
            }
            else
            {
                sortedKeys[uniqueCount++] = sortedKeys[index];
            }

            lastPosition = position;
            sortedValues[uniqueCount - 1] = values[position];
        }

        if (uniqueCount < count)
        {
            Array.Resize(ref sortedKeys, uniqueCount);
            Array.Resize(ref sortedValues, uniqueCount);
        }

        return new GesValueMap(sortedKeys, sortedValues);
    }

    /// <summary>
    /// Performs the ges dice operation.
    /// </summary>
    /// <param name="rolls">The rolls value.</param>
    /// <returns>The result of the operation.</returns>
    public static GesValue GesDice(int[] rolls)
    {
        int[] copy = rolls.Length == 0 ? [] : new int[rolls.Length];
        if (rolls.Length > 0)
        {
            Array.Copy(rolls, copy, rolls.Length);
        }

        var value = new GesValue();
        value.SetDice(copy);
        return value;
    }

    /// <summary>
    /// Creates a Bytes value from a copy of the given bytes.
    /// </summary>
    /// <param name="bytes">The bytes to copy.</param>
    /// <returns>The Bytes value.</returns>
    public static GesValue GesBytes(ReadOnlySpan<byte> bytes)
    {
        var value = new GesValue();
        value.SetBytes(bytes.Length == 0 ? [] : bytes.ToArray());
        return value;
    }

    /// <summary>
    /// Performs the ges range operation.
    /// </summary>
    /// <param name="from">The from value.</param>
    /// <param name="to">The to value.</param>
    /// <param name="step">The step value.</param>
    /// <returns>The result of the operation.</returns>
    public static GesValue GesRange(long from, long to, long step = 1)
    {
        var value = new GesValue();
        value.SetIntegerRange(from, to, step);
        return value;
    }

    /// <summary>
    /// Creates a lazy range. Finite endpoints and step that are exactly representable as Int64 normalize to an exact integer range; other finite inputs use binary64 terms. Non-finite inputs produce Nothing.
    /// </summary>
    /// <param name="from">The from value.</param>
    /// <param name="to">The to value.</param>
    /// <param name="step">The step value.</param>
    /// <returns>The result of the operation.</returns>
    public static GesValue GesRange(double from, double to, double step = 1d)
    {
        var value = new GesValue();
        value.SetFloatRange(from, to, step);
        return value;
    }

    /// <summary>
    /// Performs the ges message operation.
    /// </summary>
    /// <param name="message">The message value.</param>
    /// <returns>The result of the operation.</returns>
    public static GesValue GesMessage(GameEventScriptMessage message)
    {
        var value = new GesValue();
        value.SetMessage(message);
        return value;
    }

    /// <summary>
    /// Performs the ges handler operation.
    /// </summary>
    /// <param name="signature">The signature value.</param>
    /// <returns>The result of the operation.</returns>
    public static GesValue GesHandler(GesMessageSignature signature)
    {
        var value = new GesValue();
        value.SetHandler(signature);
        return value;
    }

    internal static GesValue GesSeries(GesSeriesValue series)
    {
        var value = new GesValue();
        value.SetSeries(series);
        return value;
    }

    internal readonly bool IsTrue => (Flags & IsTrueFlag) != 0;
    internal readonly bool IsFalse => (Flags & IsFalseFlag) != 0;
    internal readonly bool IsNotTrue => (Flags & IsTrueFlag) == 0;
    internal readonly bool IsTruthDeterminate => (Flags & (IsTrueFlag | IsFalseFlag)) != 0;
    internal readonly bool IsTruthIndeterminate => (Flags & (IsTrueFlag | IsFalseFlag)) == 0;

    /// <summary>
    /// Gets a value indicating whether is numeric.
    /// </summary>
    public readonly bool IsNumeric => (Flags & IsNumericFlag) != 0;
    /// <summary>
    /// Gets a value indicating whether has value.
    /// </summary>
    public readonly bool HasValue => (Flags & HasValueFlag) != 0;

    /// <summary>
    /// Gets a value indicating whether is nothing.
    /// </summary>
    public readonly bool IsNothing => Kind is Nothing || (Kind is Float or Percentage && double.IsNaN(FloatValue));
    internal readonly bool IsNotNothing => Kind is not Nothing && Kind is not Float and not Percentage || Kind is Float or Percentage && !double.IsNaN(FloatValue);

    internal readonly bool IsUnit(GesQuantityUnit requiredUnit) => Unit == requiredUnit;
    /// <summary>
    /// Gets a value indicating whether has unit.
    /// </summary>
    public readonly bool HasUnit => Unit.IsNumericUnit();
    internal readonly bool IsStorageObject => (Flags & StorageObjectFlag) != 0;

    internal readonly GesBytecodeTypeKind NumericKind => Kind is GesBytecodeTypeKind.Boolean or Dice ? Integer : Kind;

    internal readonly string TextValue => ObjectValue as string ?? (ObjectValue as GesTextPayload)?.Text ?? string.Empty;

    /// <summary>
    /// Gets the value kind.
    /// </summary>
    public readonly GesBytecodeTypeKind ValueKind => Kind;

    /// <summary>
    /// Gets the value unit.
    /// </summary>
    public readonly GesQuantityUnit ValueUnit => Unit;

    /// <summary>
    /// Performs the as integer operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public readonly long AsInteger() => Kind switch
    {
        GesBytecodeTypeKind.Integer or Dice => IntegerValue,
        Float or Percentage => ToIntegerSaturated(FloatValue),
        GesBytecodeTypeKind.Boolean => IsTrue ? 1 : 0,
        _ => ToIntegerSaturated(AsNumeric)
    };

    /// <summary>
    /// Performs the as number operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public readonly double AsNumber() => Kind switch
    {
        GesBytecodeTypeKind.Integer or Dice => IntegerValue,
        Float or Percentage => FloatValue,
        GesBytecodeTypeKind.Boolean => IsTrue ? 1d : 0d,
        _ => AsNumeric
    };

    /// <summary>
    /// Performs the as boolean operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public readonly bool AsBoolean() => Kind switch
    {
        GesBytecodeTypeKind.Boolean => IsTrue,
        GesBytecodeTypeKind.Integer => IntegerValue != 0,
        Float or Percentage => FloatValue != 0d,
        Vector or Point when ObjectValue is GesSpatialValue vector => vector.X != 0d || vector.Y != 0d || vector.Z != 0d,
        _ => IsTrue
    };

    /// <summary>
    /// Performs the as text operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public readonly string AsText() => TextValue.Length > 0 || Kind is GesBytecodeTypeKind.Text or Tag ? TextValue : ToText;

    /// <summary>
    /// Gets the x.
    /// </summary>
    public readonly double X => ObjectValue is GesSpatialValue vector ? vector.X : 0d;

    /// <summary>
    /// Gets the y.
    /// </summary>
    public readonly double Y => ObjectValue is GesSpatialValue vector ? vector.Y : 0d;

    /// <summary>
    /// Gets the z.
    /// </summary>
    public readonly double Z => ObjectValue is GesSpatialValue vector ? vector.Z : 0d;

    /// <summary>
    /// Gets the Unicode scalar or collection count, saturated at Int32.MaxValue; an external value counts its declared
    /// fields without running a getter, also those a materialized map leaves out; scalar values have length zero.
    /// </summary>
    public readonly int Length => (int)Math.Min(CollectionCount, int.MaxValue);

    internal readonly long CollectionCount => ObjectValue switch
    {
        GesTextPayload text => text.Count,
        string text => text.Length,
        int[] dice => dice.Length,
        byte[] bytes => bytes.Length,
        GesValue[] list => list.Length,
        GesValueMap map => map.Length,
        GesCustomObject record => record.Map.Length,
        GesExternalStorage external => external.Definition.Fields.Count,
        GesIntegerRange range => range.Count,
        GesFloatRange range => range.Count,
        _ => 0,
    };

    /// <summary>
    /// Gets the message.
    /// </summary>
    public readonly GameEventScriptMessage? Message => ObjectValue as GameEventScriptMessage;

    /// <summary>
    /// Gets the handler.
    /// </summary>
    public readonly GesMessageSignature? Handler => ObjectValue as GesMessageSignature;

    /// <summary>
    /// Gets the custom type name.
    /// </summary>
    public readonly string? CustomTypeName => ObjectValue switch
    {
        GesExternalStorage externalValue => externalValue.CustomTypeName,
        GesCustomObject customObject => customObject.TypeName,
        _ => null
    };

    /// <summary>
    /// Performs the as list operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public readonly GesValueSlice AsList()
        => ObjectValue is GesValue[] source ? new GesValueSlice(source) : GesValueSlice.Empty;

    /// <summary>
    /// Returns the stored entries of a Map or the stored fields of a Record, otherwise <see langword="null"/>.
    /// External field getters are not invoked; <see cref="MaterializedMap"/> reads them.
    /// </summary>
    /// <returns>The stored entries or fields.</returns>
    public readonly GesValueMap? AsMap() => ObjectValue switch
    {
        GesValueMap map => map,
        GesCustomObject customObject => customObject.Map,
        _ => null
    };

    /// <summary>
    /// Returns the stored entries or fields, or reads every declared field of an external value. Each call runs the
    /// getters again and keeps nothing, so an external value can change between handlers.
    /// </summary>
    /// <returns>The entries or fields, or <see langword="null"/> for every other value.</returns>
    /// <exception cref="GesFatalRuntimeException">Thrown when an external field getter fails.</exception>
    public readonly GesValueMap? MaterializedMap() => (ObjectValue as GesExternalStorage)?.ToMap() ?? AsMap();

    /// <summary>
    /// Reads one declared field of an external value by running only its getter, and applies the declared type.
    /// </summary>
    /// <param name="name">The field name.</param>
    /// <returns>The field, or <see langword="null"/> for an undeclared name, an absent field or a value that is not external.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="GesFatalRuntimeException">Thrown when the external field getter fails.</exception>
    public readonly GesValue? ReadField(string name)
    {
        _ = name ?? throw new ArgumentNullException(nameof(name));
        return (ObjectValue as GesExternalStorage)?.ReadField(name);
    }

    /// <summary>
    /// Gets the host object of an external value, otherwise <see langword="null"/>; the fastest way to its data.
    /// </summary>
    public readonly IGesExternalValue? ExternalValue => (ObjectValue as GesExternalStorage)?.Value;

    /// <summary>
    /// Reads a member by name: the field of a Record, the entry of a Map, or the coordinate <c>x</c>, <c>y</c> or
    /// <c>z</c> of a Vector or Point. A missing name and every other value, including an external value, give
    /// Nothing; external field getters are not invoked, and a stored Nothing reads like a missing name.
    /// </summary>
    /// <param name="name">The member name.</param>
    /// <returns>The member value, or Nothing.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is <see langword="null"/>.</exception>
    public readonly GesValue this[string name]
    {
        get
        {
            _ = name ?? throw new ArgumentNullException(nameof(name));
            return ObjectValue switch
            {
                GesValueMap map => map.Get(name) ?? default,
                GesCustomObject customObject => customObject.Map.Get(name) ?? default,
                GesSpatialValue spatial when spatial.GetComponent(name) is { } component => GesFloat(component, Unit),
                _ => default
            };
        }
    }

    /// <summary>
    /// Gets the bytes of a Bytes value without copying them.
    /// </summary>
    /// <returns>The stored bytes, or an empty span for other kinds.</returns>
    public readonly ReadOnlySpan<byte> AsBytes() => ObjectValue is byte[] bytes && Kind == Bytes ? bytes : default;

    /// <summary>
    /// Performs the as dice operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public readonly int[] AsDice()
    {
        if (ObjectValue is not int[] rolls)
        {
            return [];
        }

        var result = new int[rolls.Length];
        Array.Copy(rolls, result, rolls.Length);
        return result;
    }

    /// <summary>
    /// Gets the integer range.
    /// </summary>
    public readonly GesIntegerRange? IntegerRange => ObjectValue as GesIntegerRange;

    /// <summary>
    /// Gets the float range.
    /// </summary>
    public readonly GesFloatRange? FloatRange => ObjectValue as GesFloatRange;

    /// <summary>
    /// Returns the equals result for this value.
    /// </summary>
    /// <param name="other">The other value.</param>
    /// <returns>The result of the operation.</returns>
    public readonly bool Equals(GesValue other) => EqualsValue(in other);

    /// <summary>
    /// Returns the equals result for this value.
    /// </summary>
    /// <param name="obj">The obj value.</param>
    /// <returns>The result of the operation.</returns>
    public override readonly bool Equals(object? obj) => obj is GesValue other && Equals(other);

    /// <summary>
    /// Returns the get hash code result for this value.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public override readonly int GetHashCode() => GetValueHashCode();

    /// <summary>
    /// Returns the to string result for this value.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public override readonly string ToString() => ToText;

    internal void SetNothing()
    {
        Kind = Nothing;
        Unit = UnitNone;
        Flags = None;
        IntegerValue = 0;
        ObjectValue = null;
    }

    internal void SetBoolean(bool value)
    {
        Kind = GesBytecodeTypeKind.Boolean;
        Flags = IsNumericFlag | HasValueFlag | (value ? IsTrueFlag : IsFalseFlag);
        Unit = UnitNone;
        IntegerValue = value ? 1 : 0;
        ObjectValue = null;
    }

    internal void SetInteger(long value, GesQuantityUnit unit = UnitNone)
    {
        Kind = Integer;
        Flags = IsNumericFlag | HasValueFlag | (value != 0 ? IsTrueFlag : IsFalseFlag);
        Unit = unit;
        IntegerValue = value;
        ObjectValue = null;
    }

    internal void SetFloat(double value, GesQuantityUnit unit = UnitNone)
    {
        if (double.IsNaN(value))
        {
            SetNothing();
            return;
        }

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (Runtime.GesNumber.CanRepresentAsInteger(value))
        {
            var intValue = (long)value;
            Kind = Integer;
            Flags = IsNumericFlag | HasValueFlag | (intValue != 0 ? IsTrueFlag : IsFalseFlag);
            IntegerValue = intValue;
        }
        else
        {
            Kind = Float;
            Flags = IsNumericFlag | HasValueFlag | (value != 0 ? IsTrueFlag : IsFalseFlag);
            FloatValue = value;
        }

        Unit = unit;
        ObjectValue = null;
    }

    internal void SetPercentage(double ratio)
    {
        if (double.IsNaN(ratio))
        {
            SetNothing();
            return;
        }
        ratio = Runtime.GesNumber.CanonicalizeZero(ratio);
        if (double.IsFinite(ratio))
        {
            Kind = Percentage;
            Flags = IsNumericFlag | HasValueFlag | (ratio != 0 ? IsTrueFlag : IsFalseFlag);
        }
        else
        {
            Kind = Float;
            Flags = IsNumericFlag | IsTrueFlag | HasValueFlag;
        }

        Unit = UnitNone;
        FloatValue = ratio;
        ObjectValue = null;
    }

    internal void SetText(string text)
        => SetText(text, GameEventScript.Syntax.GesText.CountScalars(text));

    internal void SetText(string text, int scalarCount)
    {
        Kind = Text;
        Flags = StorageObjectFlag |
                (text.Length == 0 ? None : HasValueFlag) |
                (GameEventScript.Syntax.GesText.EqualsAsciiIgnoreCase(text, "true") || text == "1" ? IsTrueFlag : IsFalseFlag);
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = scalarCount == text.Length ? text : new GesTextPayload(text, scalarCount);
    }

    internal void SetTag(string tag)
    {
        Kind = Tag;
        Flags = StorageObjectFlag |
                HasValueFlag |
                IsFalseFlag;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = tag;
    }

    internal void SetTextConstant(in GesValue value, bool tag = false)
    {
        this = value;
        if (!tag) return;
        Kind = Tag;
        Flags = StorageObjectFlag | HasValueFlag | IsFalseFlag;
    }

    internal void SetVector(double x, double y, double z, GesQuantityUnit unit = UnitNone)
    {
        if (double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(z))
        {
            SetNothing();
            return;
        }

        x = Runtime.GesNumber.CanonicalizeZero(x);
        y = Runtime.GesNumber.CanonicalizeZero(y);
        z = Runtime.GesNumber.CanonicalizeZero(z);
        Kind = Vector;
        Flags = StorageObjectFlag | HasValueFlag | (x is 0 or double.NaN && y is 0 or double.NaN && z is 0 or double.NaN ? IsFalseFlag : IsTrueFlag);
        Unit = unit;
        IntegerValue = 0;
        ObjectValue = new GesSpatialValue(x, y, z);
    }

    internal void SetVector(GesSpatialValue vector, GesQuantityUnit unit = UnitNone)
        => SetVector(vector.X, vector.Y, vector.Z, unit);

    internal void SetPoint(double x, double y, double z, GesQuantityUnit unit = UnitNone)
    {
        if (double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(z))
        {
            SetNothing();
            return;
        }

        x = Runtime.GesNumber.CanonicalizeZero(x);
        y = Runtime.GesNumber.CanonicalizeZero(y);
        z = Runtime.GesNumber.CanonicalizeZero(z);
        Kind = Point;
        Flags = StorageObjectFlag | HasValueFlag | (x is 0 or double.NaN && y is 0 or double.NaN && z is 0 or double.NaN ? IsFalseFlag : IsTrueFlag);
        Unit = unit;
        IntegerValue = 0;
        ObjectValue = new GesSpatialValue(x, y, z);
    }

    internal void SetPoint(GesSpatialValue point, GesQuantityUnit unit = UnitNone)
        => SetPoint(point.X, point.Y, point.Z, unit);

    internal void SetDice(int[] values)
    {
        Kind = Dice;
        Flags = values.Length > 0 ? HasValueFlag | StorageObjectFlag | IsNumericFlag : StorageObjectFlag | IsNumericFlag;
        Unit = UnitNone;
        IntegerValue = SumDices(values);
        Array.Sort(values);
        Array.Reverse(values);
        ObjectValue = values;
    }

    // The array is owned by the value and never changed afterwards.
    internal void SetBytes(byte[] bytes)
    {
        Kind = Bytes;
        Flags = bytes.Length > 0 ? StorageObjectFlag | HasValueFlag : StorageObjectFlag;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = bytes;
    }

    internal void SetList(GesValue[] list)
    {
        Kind = List;
        Flags = list.Length > 0 ? StorageObjectFlag | HasValueFlag : StorageObjectFlag;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = list;
    }

    internal void SetMap(GesValueMap valueMap)
    {
        Kind = Map;
        Flags = valueMap.Length > 0 ? StorageObjectFlag | HasValueFlag : StorageObjectFlag;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = valueMap;
    }

    internal void SetRecord(string typeName, GesValueMap record)
    {
        Kind = Custom;
        Flags = record.Length > 0 ? StorageObjectFlag | HasValueFlag : StorageObjectFlag;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = new GesCustomObject(typeName, record);
    }

    internal void SetExternal(IGesExternalValue value)
    {
        var externalValue = new GesExternalStorage(value);
        Kind = Custom;
        Flags = StorageObjectFlag | HasValueFlag;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = externalValue;
    }

    internal void SetIntegerRange(long from, long to, long step)
    {
        Kind = GesBytecodeTypeKind.Range;
        Unit = UnitNone;
        if (step == 0 || (step > 0 && from > to) || (step < 0 && from < to))
        {
            ObjectValue = _emptyValueRangeInteger;
            IntegerValue = 0;
        }
        else
        {
            ObjectValue = new GesIntegerRange(from, to, step);
            IntegerValue = 0;
        }

        Flags = CollectionCount > 0 ? StorageObjectFlag | HasValueFlag : StorageObjectFlag;
    }

    internal void SetFloatRange(double from, double to, double step)
    {
        if (!(double.IsFinite(from) && double.IsFinite(to) && double.IsFinite(step)))
        {
            SetNothing();
            return;
        }

        if (Runtime.GesNumber.CanRepresentAsInteger(from) && Runtime.GesNumber.CanRepresentAsInteger(to) && Runtime.GesNumber.CanRepresentAsInteger(step))
        {
            SetIntegerRange((long)from, (long)to, (long)step);
            return;
        }
        from = Runtime.GesNumber.CanonicalizeZero(from);
        to = Runtime.GesNumber.CanonicalizeZero(to);
        step = Runtime.GesNumber.CanonicalizeZero(step);
        Kind = GesBytecodeTypeKind.Range;
        Unit = UnitNone;
        if (step == 0 || (step > 0 && from > to) || (step < 0 && from < to))
        {
            ObjectValue = _emptyValueRangeInteger;
            IntegerValue = 0;
        }
        else
        {
            ObjectValue = new GesFloatRange(from, to, step);
            IntegerValue = 0;
        }

        Flags = CollectionCount > 0 ? StorageObjectFlag | HasValueFlag : StorageObjectFlag;
    }

    private static GesIntegerRange _emptyValueRangeInteger = new(0, 0, 0);

    internal void SetHandler(GesMessageSignature handler)
    {
        Kind = GesBytecodeTypeKind.Handler;
        Flags = StorageObjectFlag | HasValueFlag;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = handler;
    }

    internal void SetMessage(GameEventScriptMessage message)
    {
        Kind = GesBytecodeTypeKind.Message;
        Flags = StorageObjectFlag | HasValueFlag;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = message;
    }

    internal void SetSeries(GesSeriesValue series)
    {
        Kind = Series;
        Flags = StorageObjectFlag | HasValueFlag;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = series;
    }

    internal void SetIterator(IGesIterator value)
    {
        Kind = Iterator;
        Flags = StorageObjectFlag;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = value;
    }

    internal void SetListBuilder(object listBuilder)
    {
        Kind = ListBuilder;
        Flags = StorageObjectFlag;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = listBuilder;
    }

    internal void SetMapBuilder(object mapBuilder)
    {
        Kind = MapBuilder;
        Flags = StorageObjectFlag;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = mapBuilder;
    }

    internal void SetDistinctBuilder(object distinctBuilder)
    {
        Kind = DistinctBuilder;
        Flags = StorageObjectFlag;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = distinctBuilder;
    }

    internal void SetGroupBuilder(object groupBuilder)
    {
        Kind = GroupBuilder;
        Flags = StorageObjectFlag;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = groupBuilder;
    }

    internal void SetOrderBuilder(object orderBuilder)
    {
        Kind = OrderBuilder;
        Flags = StorageObjectFlag;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = orderBuilder;
    }

    /// <summary>
    /// Gets the as numeric.
    /// </summary>
    public readonly double AsNumeric => Kind switch
    {
        Integer => IntegerValue,
        Float or Percentage => FloatValue,
        GesBytecodeTypeKind.Boolean => IsTrue ? 1d : 0d,
        Dice => IntegerValue,
        _ => double.NaN,
    };
    private static long SumDices(int[] values)
    {
        long sum = 0;
        foreach (var value in values) sum += value;
        return sum;
    }

    private static long ToIntegerSaturated(double number) => Runtime.GesNumber.ToIntegerSaturated(number);
    internal GesNumericWithUnit AsNumericWithUnit()
        => new(AsNumeric, Unit);

    internal readonly bool EqualsValue(in GesValue other)
    {
        if (Unit != other.Unit || Kind != other.Kind)
        {
            return false;
        }

        switch (Kind)
        {
            case Nothing:
                return true;
            case Integer:
                return IntegerValue == other.IntegerValue;
            case Float or Percentage:
                return FloatValue.Equals(other.FloatValue);
            case GesBytecodeTypeKind.Boolean:
                return IsTrue == other.IsTrue;
            case Text or Tag:
                return string.Equals(TextValue, other.TextValue, StringComparison.Ordinal);
            case Vector or Point when ObjectValue is GesSpatialValue left && other.ObjectValue is GesSpatialValue right:
                return left.X.Equals(right.X) && left.Y.Equals(right.Y) && left.Z.Equals(right.Z);
            case Bytes when ObjectValue is byte[] left && other.ObjectValue is byte[] right:
                return left.AsSpan().SequenceEqual(right);
            case Dice when ObjectValue is int[] left && other.ObjectValue is int[] right:
                if (left.Length != right.Length) return false;
                for (var i = 0; i < left.Length; i++)
                {
                    if (left[i] != right[i]) return false;
                }

                return true;
            case List when ObjectValue is GesValue[] left && other.ObjectValue is GesValue[] right:
                if (left.Length != right.Length) return false;
                for (var i = 0; i < left.Length; i++)
                {
                    if (!left[i].EqualsValue(in right[i])) return false;
                }

                return true;
            case Map when ObjectValue is GesValueMap left && other.ObjectValue is GesValueMap right:
                if (left.Length != right.Length) return false;
                for (var i = 0; i < left.Length; i++)
                {
                    if (!string.Equals(left.KeyAt(i), right.KeyAt(i), StringComparison.Ordinal)) return false;
                    var leftValue = left.ValueAt(i);
                    var rightValue = right.ValueAt(i);
                    if (!leftValue.EqualsValue(in rightValue)) return false;
                }

                return true;
            case Custom when ObjectValue is GesCustomObject leftCustom && other.ObjectValue is GesCustomObject rightCustom:
                if (!string.Equals(leftCustom.TypeName, rightCustom.TypeName, StringComparison.Ordinal)) return false;
                var customLeftMap = leftCustom.Map;
                var customRightMap = rightCustom.Map;
                if (customLeftMap.Length != customRightMap.Length) return false;
                for (var i = 0; i < customLeftMap.Length; i++)
                {
                    if (!string.Equals(customLeftMap.KeyAt(i), customRightMap.KeyAt(i), StringComparison.Ordinal)) return false;
                    var leftValue = customLeftMap.ValueAt(i);
                    var rightValue = customRightMap.ValueAt(i);
                    if (!leftValue.EqualsValue(in rightValue)) return false;
                }

                return true;
            case GesBytecodeTypeKind.Range when ObjectValue is GesIntegerRange left && other.ObjectValue is GesIntegerRange right:
                return left.From == right.From && left.To == right.To && left.Step == right.Step;
            case GesBytecodeTypeKind.Range when ObjectValue is GesFloatRange left && other.ObjectValue is GesFloatRange right:
                return left.From.Equals(right.From) && left.To.Equals(right.To) && left.Step.Equals(right.Step);
            case GesBytecodeTypeKind.Handler when ObjectValue is GesMessageSignature left && other.ObjectValue is GesMessageSignature right:
                return left.Equals(right);
            case GesBytecodeTypeKind.Message when ObjectValue is GameEventScriptMessage left && other.ObjectValue is GameEventScriptMessage right:
                return left.Equals(right);
            case Series when ObjectValue is GesSeriesValue left && other.ObjectValue is GesSeriesValue right:
                return string.Equals(left.SignatureId, right.SignatureId, StringComparison.Ordinal) && left.Offset == right.Offset;
            default:
                return ReferenceEquals(ObjectValue, other.ObjectValue);
        }
    }

    internal readonly int GetValueHashCode()
    {
        var hash = new HashCode();
        hash.Add(Kind);
        hash.Add(Unit);
        switch (Kind)
        {
            case Nothing:
                break;
            case Integer:
                hash.Add(IntegerValue);
                break;
            case Float or Percentage:
                hash.Add(FloatValue);
                break;
            case GesBytecodeTypeKind.Boolean:
                hash.Add(IsTrue);
                break;
            case Text or Tag:
                hash.Add(TextValue, StringComparer.Ordinal);
                break;
            case Vector or Point when ObjectValue is GesSpatialValue vector:
                hash.Add(vector.X);
                hash.Add(vector.Y);
                hash.Add(vector.Z);
                break;
            case Bytes when ObjectValue is byte[] bytes:
                for (var i = 0; i < bytes.Length; i++) hash.Add(bytes[i]);
                break;
            case Dice when ObjectValue is int[] dice:
                for (var i = 0; i < dice.Length; i++) hash.Add(dice[i]);
                break;
            case List when ObjectValue is GesValue[] list:
                for (var i = 0; i < list.Length; i++) hash.Add(list[i].GetValueHashCode());
                break;
            case Map when ObjectValue is GesValueMap map:
                for (var i = 0; i < map.Length; i++)
                {
                    hash.Add(map.KeyAt(i), StringComparer.Ordinal);
                    var value = map.ValueAt(i);
                    hash.Add(value.GetValueHashCode());
                }

                break;
            case Custom when ObjectValue is GesCustomObject custom:
                hash.Add(custom.TypeName, StringComparer.Ordinal);
                var customMap = custom.Map;
                for (var i = 0; i < customMap.Length; i++)
                {
                    hash.Add(customMap.KeyAt(i), StringComparer.Ordinal);
                    var value = customMap.ValueAt(i);
                    hash.Add(value.GetValueHashCode());
                }

                break;
            case GesBytecodeTypeKind.Range when ObjectValue is GesIntegerRange range:
                hash.Add(range.From);
                hash.Add(range.To);
                hash.Add(range.Step);
                break;
            case GesBytecodeTypeKind.Range when ObjectValue is GesFloatRange range:
                hash.Add(range.From);
                hash.Add(range.To);
                hash.Add(range.Step);
                break;
            case GesBytecodeTypeKind.Handler when ObjectValue is GesMessageSignature signature:
                hash.Add(signature);
                break;
            case GesBytecodeTypeKind.Message when ObjectValue is GameEventScriptMessage message:
                hash.Add(message);
                break;
            case Series when ObjectValue is GesSeriesValue series:
                hash.Add(series.SignatureId, StringComparer.Ordinal);
                hash.Add(series.Offset);
                break;
            default:
                hash.Add(ObjectValue);
                break;
        }

        return hash.ToHashCode();
    }

    internal IGesIterator? CreateIterator()
    {
        switch (Kind)
        {
            case GesBytecodeTypeKind.Range when ObjectValue is GesIntegerRange range:
                return new GesIntegerRangeIterator(range.From, range.To, range.Step);
            case GesBytecodeTypeKind.Range when ObjectValue is GesFloatRange range:
                return new GesFloatRangeIterator(range.From, range.To, range.Step);
            case List when ObjectValue is GesValue[] list:
                return new GesListIterator(list);
            case Dice when ObjectValue is int[] dices:
                return new GesIntIterator(dices);
            case Bytes when ObjectValue is byte[] bytes:
                return new GesBytesIterator(bytes);
            case Map when ObjectValue is GesValueMap map:
                return new GesListIterator(map.ValueList);
            case Custom when ObjectValue is GesCustomObject custom:
                return new GesListIterator(custom.Map.ValueList);
            case Vector or Point when ObjectValue is GesSpatialValue vp:
                return new GesTripletIterator(vp);
            case Text or Tag:
                return new GesStringIterator(TextValue);
            case Series:
            default:
                return null;
        }
    }

    internal readonly string ToText => Kind switch
    {
        Nothing => "nothing",
        Integer => FormatNumber(IntegerValue, Unit),
        Float => FormatNumber(FloatValue, Unit),
        Percentage => GesTextNumberCast.FormatPercentage(FloatValue),
        GesBytecodeTypeKind.Boolean => IsTrue ? "true" : "false",
        Text => TextValue,
        Tag => "#" + TextValue,
        Vector when ObjectValue is GesSpatialValue vector => FormatTriplet(":Vector", vector, Unit),
        Point when ObjectValue is GesSpatialValue point => FormatTriplet(":Point", point, Unit),
        Dice when ObjectValue is int[] dice => FormatDice(dice),
        Bytes when ObjectValue is byte[] bytes => GesBytesText.Format(bytes),
        List when ObjectValue is GesValue[] list => FormatList(list),
        Map when ObjectValue is GesValueMap map => FormatMap(map),
        Custom when ObjectValue is GesCustomObject custom => FormatRecord(custom.TypeName, custom.Map),
        GesBytecodeTypeKind.Range when ObjectValue is GesIntegerRange range => FormatRange(range.From, range.To, range.Step),
        GesBytecodeTypeKind.Range when ObjectValue is GesFloatRange range => FormatRange(range.From, range.To, range.Step),
        Series when ObjectValue is GesSeriesValue series => $":Series({series.SignatureId}, offset: {series.Offset.ToString(CultureInfo.InvariantCulture)})",
        GesBytecodeTypeKind.Handler when ObjectValue is GesMessageSignature signature => $":Handler({signature.SignatureId.Replace(",", ", ")})",
        GesBytecodeTypeKind.Message when ObjectValue is GameEventScriptMessage message => FormatMessage(message),
        _ => Kind.ToString()
    };

    private static string FormatNumber(long value, GesQuantityUnit unit) => GesTextNumberCast.Format(value, unit);
    private static string FormatNumber(double value, GesQuantityUnit unit) => GesTextNumberCast.Format(value, unit);
    private static string FormatTriplet(string typeName, GesSpatialValue triplet, GesQuantityUnit unit)
        => $"{typeName}(x: {FormatNumber(triplet.X, unit)}, y: {FormatNumber(triplet.Y, unit)}, z: {FormatNumber(triplet.Z, unit)})";
    private static string FormatDice(int[] dice)
    {
        if (dice.Length == 0) return ":Dice[]";

        var builder = new StringBuilder(":Dice[");
        for (var i = 0; i < dice.Length; i++)
        {
            if (i > 0) builder.Append(", ");
            builder.Append(dice[i].ToString(CultureInfo.InvariantCulture));
        }

        builder.Append(']');
        return builder.ToString();
    }
    private static void AppendNestedText(StringBuilder builder, in GesValue value)
    {
        if (value.Kind == Text) GesTextLiteralReader.AppendQuoted(builder, value.TextValue);
        else builder.Append(value.ToText);
    }
    private static string FormatList(GesValue[] list)
    {
        var builder = new StringBuilder("[");
        for (var i = 0; i < list.Length; i++)
        {
            if (i > 0) builder.Append(", ");
            AppendNestedText(builder, in list[i]);
        }

        builder.Append(']');
        return builder.ToString();
    }
    private static string FormatMap(GesValueMap valueMap)
    {
        var builder = new StringBuilder(valueMap.Length == 0 ? "[:" : "[");
        var first = true;
        for (var i = 0; i < valueMap.Length; i++)
        {
            if (!first) builder.Append(", ");
            first = false;
            var key = valueMap.KeyAt(i);
            if (GameEventScript.Syntax.GesText.IsFieldName(key)) builder.Append(key);
            else GesTextLiteralReader.AppendQuoted(builder, key);
            builder.Append(": ");
            GesValue tempQualifier = valueMap.ValueAt(i);
            AppendNestedText(builder, in tempQualifier);
        }

        builder.Append(']');
        return builder.ToString();
    }
    private static string FormatRecord(string name, GesValueMap fields)
    {
        var builder = new StringBuilder(":Record(");
        GesTextLiteralReader.AppendQuoted(builder, name);
        return builder.Append(", ").Append(FormatMap(fields)).Append(')').ToString();
    }
    private static string FormatMessage(GameEventScriptMessage message)
    {
        var builder = new StringBuilder(":Message(").Append(message.Name).Append('(');
        for (var i = 0; i < message.Arguments.Count; i++)
        {
            if (i > 0) builder.Append(", ");
            var name = message.Arguments.NameAt(i);
            if (name != "_") builder.Append(name).Append(": ");
            AppendNestedText(builder, message.Arguments[i]);
        }
        builder.Append(')');
        for (var i = 0; i < message.Tags.Count; i++) builder.Append(i == 0 ? " with #" : ", #").Append(message.Tags[i]);
        return builder.Append(')').ToString();
    }
    private static string FormatRange(long from, long to, long step)
        => $":Range(from: {from.ToString(CultureInfo.InvariantCulture)}, to: {to.ToString(CultureInfo.InvariantCulture)}, step: {step.ToString(CultureInfo.InvariantCulture)})";
    private static string FormatRange(double from, double to, double step)
        => $":Range(from: {FormatRangeComponent(from)}, to: {FormatRangeComponent(to)}, step: {FormatRangeComponent(step)})";
    private static string FormatRangeComponent(double value)
        => Runtime.GesNumber.FormatCanonicalFloat(value);
}
