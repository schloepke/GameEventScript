// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;

namespace GameEventScript.Runtime.Values;

/// <summary>
/// Represents a ges value slice.
/// </summary>
public readonly struct GesValueSlice
{
    private readonly GesValue[]? _values;

    /// <summary>
    /// Defines the empty value.
    /// </summary>
    public static readonly GesValueSlice Empty = new([], 0, 0);

    /// <summary>
    /// Initializes a new instance of Ges Value Slice.
    /// </summary>
    /// <param name="values">The values value.</param>
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
    /// Gets the start.
    /// </summary>
    public int Start { get; }

    /// <summary>
    /// Gets the length.
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Gets the value at the specified index.
    /// </summary>
    /// <param name="index">The index value.</param>
    public ref readonly GesValue this[int index] => ref _values![Start + index];

    /// <summary>
    /// Performs the value at operation.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
    public ref readonly GesValue ValueAt(int index) => ref _values![Start + index];

    /// <summary>
    /// Performs the kind at operation.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
    public GesBytecodeTypeKind KindAt(int index) => _values![Start + index].Kind;

    /// <summary>
    /// Performs the unit at operation.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
    public GesQuantityUnit UnitAt(int index) => _values![Start + index].Unit;

    /// <summary>
    /// Determines whether has value at.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
    public bool HasValueAt(int index) => _values![Start + index].HasValue;

    /// <summary>
    /// Determines whether is nothing at.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
    public bool IsNothingAt(int index) => _values![Start + index].IsNothing;

    /// <summary>
    /// Determines whether is numeric at.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
    public bool IsNumericAt(int index) => _values![Start + index].IsNumeric;

    /// <summary>
    /// Gets the as integer.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
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
    /// Gets the as number.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
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
    /// Gets the as boolean.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
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
    /// Gets the as text.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
    public string GetAsText(int index)
    {
        ref readonly var value = ref _values![Start + index];
        return value.TextValue.Length > 0 || value.Kind is GesBytecodeTypeKind.Text or GesBytecodeTypeKind.Tag
            ? value.TextValue
            : value.ToText;
    }

    /// <summary>
    /// Gets the x.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
    public double GetX(int index) => _values![Start + index].ObjectValue is GesSpatialValue vector ? vector.X : 0d;

    /// <summary>
    /// Gets the y.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
    public double GetY(int index) => _values![Start + index].ObjectValue is GesSpatialValue vector ? vector.Y : 0d;

    /// <summary>
    /// Gets the z.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
    public double GetZ(int index) => _values![Start + index].ObjectValue is GesSpatialValue vector ? vector.Z : 0d;

    internal ref readonly GesValue VmValueAt(int index) => ref _values![Start + index];
}
