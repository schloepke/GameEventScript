// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;

namespace GameEventScript.Runtime.Values;

/// <summary>
/// Read-only view of a contiguous run of values, for example the elements of a List returned by <see cref="GesValue.AsList"/>.
/// The view borrows its storage without copying; indexers return read-only references into that storage.
/// </summary>
public readonly struct GesValueSlice
{
    private readonly GesValue[]? _values;

    /// <summary>
    /// A slice without values.
    /// </summary>
    public static readonly GesValueSlice Empty = new([], 0, 0);

    /// <summary>
    /// Creates a slice over a complete array without copying it. The caller must not change the array while the slice is in use.
    /// </summary>
    /// <param name="values">The values to view; <see langword="null"/> yields an empty slice.</param>
    public GesValueSlice(GesValue[] values)
    {
        _values = values ?? [];
        Start = 0;
        Length = _values.Length;
    }

    internal GesValueSlice(GesValue[] values, int start, int length)
    {
        _values = values;
        Start = start;
        Length = length;
    }

    /// <summary>
    /// Gets the offset of the first viewed value in the underlying storage.
    /// </summary>
    public int Start { get; }

    /// <summary>
    /// Gets the number of values in the slice.
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Gets a read-only reference to the value at <paramref name="index"/> without copying it.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Length"/>.</param>
    /// <exception cref="IndexOutOfRangeException"><paramref name="index"/> is outside the slice.</exception>
    public ref readonly GesValue this[int index] => ref _values![Start + index];

    /// <summary>
    /// Gets a read-only reference to the value at <paramref name="index"/> without copying it.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Length"/>.</param>
    /// <returns>A read-only reference into the viewed storage.</returns>
    /// <exception cref="IndexOutOfRangeException"><paramref name="index"/> is outside the slice.</exception>
    public ref readonly GesValue ValueAt(int index) => ref _values![Start + index];

    /// <summary>
    /// Gets the portable value kind of the argument at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Length"/>.</param>
    /// <returns>The argument's kind, for example Integer for an integral number or quantity.</returns>
    public GesBytecodeTypeKind KindAt(int index) => _values![Start + index].Kind;

    /// <summary>
    /// Gets the quantity unit of the argument at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Length"/>.</param>
    /// <returns>The unit of a quantity, Vector or Point, or <see cref="GesQuantityUnit.UnitNone"/> for a unitless value.</returns>
    public GesQuantityUnit UnitAt(int index) => _values![Start + index].Unit;

    /// <summary>
    /// Determines whether the argument at <paramref name="index"/> carries content.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Length"/>.</param>
    /// <returns><see langword="true"/> for every number, Boolean, Tag, Vector, Point, Handler, Message and external value, including zero and
    /// <see langword="false"/>; <see langword="false"/> for Nothing and for an empty Text, Dice, Bytes, List, Map, Record or Range.</returns>
    public bool HasValueAt(int index) => _values![Start + index].HasValue;

    /// <summary>
    /// Determines whether the argument at <paramref name="index"/> is Nothing.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Length"/>.</param>
    /// <returns><see langword="true"/> for Nothing and for a binary64 or Percentage value holding NaN; otherwise <see langword="false"/>.</returns>
    public bool IsNothingAt(int index) => _values![Start + index].IsNothing;

    /// <summary>
    /// Determines whether the argument at <paramref name="index"/> is numeric.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Length"/>.</param>
    /// <returns><see langword="true"/> for numbers and quantities, Percentages, Booleans and Dice; otherwise <see langword="false"/>, also for
    /// Vectors and Points.</returns>
    public bool IsNumericAt(int index) => _values![Start + index].IsNumeric;

    /// <summary>
    /// Converts the argument at <paramref name="index"/> to a signed 64-bit integer, as <see cref="GesValue.AsInteger"/> does.
    /// Integer values are returned unchanged and Dice yield the sum of their rolls; Boolean yields 1 or 0; binary64 numbers and
    /// Percentage ratios are truncated toward zero and saturated to the Int64 range. Every other kind, including Nothing, yields 0. A
    /// quantity's unit is ignored, so the number is read in the value's own unit.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Length"/>.</param>
    /// <returns>The converted integer.</returns>
    public long GetAsInteger(int index)
    {
        ref readonly var value = ref _values![Start + index];
        return value.Kind switch
        {
            GesBytecodeTypeKind.Integer or GesBytecodeTypeKind.Dice => value.IntegerValue,
            GesBytecodeTypeKind.Float or GesBytecodeTypeKind.Percentage => GesNumber.ToIntegerSaturated(value.FloatValue),
            GesBytecodeTypeKind.Boolean => value.IsTrue ? 1 : 0,
            _ => GesNumber.ToIntegerSaturated(value.AsNumeric)
        };
    }

    /// <summary>
    /// Converts the argument at <paramref name="index"/> to a binary64 number, as <see cref="GesValue.AsNumber"/> does.
    /// Integer values and Dice sums are converted to binary64 and may round beyond 2^53; binary64 numbers are returned unchanged;
    /// a Percentage yields its ratio, so 50% is 0.5; Boolean yields 1 or 0. Every other kind, including Nothing, yields NaN. A quantity's unit is
    /// ignored, so the number is read in the value's own unit.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Length"/>.</param>
    /// <returns>The converted number, or NaN for a non-numeric value.</returns>
    public double GetAsNumber(int index)
    {
        ref readonly var value = ref _values![Start + index];
        return value.Kind switch
        {
            GesBytecodeTypeKind.Integer or GesBytecodeTypeKind.Dice => value.IntegerValue,
            GesBytecodeTypeKind.Float or GesBytecodeTypeKind.Percentage => value.FloatValue,
            GesBytecodeTypeKind.Boolean => value.IsTrue ? 1d : 0d,
            _ => value.AsNumeric
        };
    }

    /// <summary>
    /// Converts the argument at <paramref name="index"/> to a Boolean, as <see cref="GesValue.AsBoolean"/> does.
    /// A Boolean is returned unchanged; numbers, quantities and Percentages are true when nonzero; a Vector or Point is true when
    /// any component is nonzero; a Text is true only when it is <c>true</c> (ASCII case-insensitive) or <c>1</c>. Every other kind, including
    /// Nothing, Tag, Dice, Bytes and the collections, yields <see langword="false"/>.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Length"/>.</param>
    /// <returns>The converted Boolean.</returns>
    public bool GetAsBoolean(int index)
    {
        ref readonly var value = ref _values![Start + index];
        return value.Kind switch
        {
            GesBytecodeTypeKind.Boolean => value.IsTrue,
            GesBytecodeTypeKind.Integer => value.IntegerValue != 0,
            GesBytecodeTypeKind.Float or GesBytecodeTypeKind.Percentage => value.FloatValue != 0d,
            GesBytecodeTypeKind.Vector or GesBytecodeTypeKind.Point when value.ObjectValue is GesSpatialValue vector => vector.X != 0d || vector.Y != 0d || vector.Z != 0d,
            _ => value.IsTrue
        };
    }

    /// <summary>
    /// Converts the argument at <paramref name="index"/> to text, as <see cref="GesValue.AsText"/> does.
    /// A Text yields its content and a Tag its name without the leading <c>#</c>. Every other kind yields a newly formatted canonical
    /// text form, the same as <see cref="GesValue.ToString"/>, for example <c>nothing</c>, <c>true</c> or <c>:Vector(x: 1, y: 2, z: 3)</c>.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Length"/>.</param>
    /// <returns>The value text.</returns>
    public string GetAsText(int index)
    {
        ref readonly var value = ref _values![Start + index];
        return value.TextValue.Length > 0 || value.Kind is GesBytecodeTypeKind.Text or GesBytecodeTypeKind.Tag
            ? value.TextValue
            : value.ToText;
    }

    /// <summary>
    /// Gets the X component of the Vector or Point argument at <paramref name="index"/>, in the argument's unit.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Length"/>.</param>
    /// <returns>The X component, or 0 when the argument is not a Vector or Point.</returns>
    public double GetX(int index) => _values![Start + index].ObjectValue is GesSpatialValue vector ? vector.X : 0d;

    /// <summary>
    /// Gets the Y component of the Vector or Point argument at <paramref name="index"/>, in the argument's unit.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Length"/>.</param>
    /// <returns>The Y component, or 0 when the argument is not a Vector or Point.</returns>
    public double GetY(int index) => _values![Start + index].ObjectValue is GesSpatialValue vector ? vector.Y : 0d;

    /// <summary>
    /// Gets the Z component of the Vector or Point argument at <paramref name="index"/>, in the argument's unit.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Length"/>.</param>
    /// <returns>The Z component, or 0 when the argument is not a Vector or Point.</returns>
    public double GetZ(int index) => _values![Start + index].ObjectValue is GesSpatialValue vector ? vector.Z : 0d;

    internal ref readonly GesValue VmValueAt(int index) => ref _values![Start + index];
}
