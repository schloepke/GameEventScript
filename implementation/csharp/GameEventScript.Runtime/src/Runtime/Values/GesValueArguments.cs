// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using GameEventScript.Runtime.Vm;

namespace GameEventScript.Runtime.Values;

/// <summary>
/// Represents a ges value arguments.
/// </summary>
public readonly struct GesValueArguments
{
    private readonly GesValue[]? _values;
    private readonly GesVirtualMachine? _vm;
    private readonly GesUInt16IndexList _registers;

    /// <summary>
    /// Defines the empty value.
    /// </summary>
    public static readonly GesValueArguments Empty = new([]);

    /// <summary>
    /// Initializes a new instance of Ges Value Arguments.
    /// </summary>
    /// <param name="values">The values value.</param>
    public GesValueArguments(GesValue[] values)
    {
        _values = values ?? [];
        _vm = null;
        _registers = default;
        Length = _values.Length;
    }

    internal GesValueArguments(GesVirtualMachine vm, GesUInt16IndexList registers)
    {
        _values = null;
        _vm = vm ?? throw new ArgumentNullException(nameof(vm));
        _registers = registers;
        Length = registers.Length;
    }

    /// <summary>
    /// Gets the length.
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Gets the value at the specified index.
    /// </summary>
    /// <param name="index">The index value.</param>
    public ref readonly GesValue this[int index] => ref ValueAt(index);

    /// <summary>
    /// Performs the value at operation.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
    public ref readonly GesValue ValueAt(int index)
    {
        if (_vm is { } vm)
        {
            return ref vm.Register(_registers[index]);
        }

        return ref _values![index];
    }

    /// <summary>
    /// Performs the kind at operation.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
    public GesBytecodeTypeKind KindAt(int index) => ValueAt(index).Kind;

    /// <summary>
    /// Performs the unit at operation.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
    public GesQuantityUnit UnitAt(int index) => ValueAt(index).Unit;

    /// <summary>
    /// Determines whether has value at.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
    public bool HasValueAt(int index) => ValueAt(index).HasValue;

    /// <summary>
    /// Determines whether is nothing at.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
    public bool IsNothingAt(int index) => ValueAt(index).IsNothing;

    /// <summary>
    /// Determines whether is numeric at.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
    public bool IsNumericAt(int index) => ValueAt(index).IsNumeric;

    /// <summary>
    /// Gets the as integer.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
    public long GetAsInteger(int index)
    {
        ref readonly var value = ref ValueAt(index);
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
        ref readonly var value = ref ValueAt(index);
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
        ref readonly var value = ref ValueAt(index);
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
        ref readonly var value = ref ValueAt(index);
        return value.TextValue.Length > 0 || value.Kind is GesBytecodeTypeKind.Text or GesBytecodeTypeKind.Tag
            ? value.TextValue
            : value.ToText;
    }

    /// <summary>
    /// Gets the x.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
    public double GetX(int index) => ValueAt(index).ObjectValue is GesSpatialValue vector ? vector.X : 0d;

    /// <summary>
    /// Gets the y.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
    public double GetY(int index) => ValueAt(index).ObjectValue is GesSpatialValue vector ? vector.Y : 0d;

    /// <summary>
    /// Gets the z.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
    public double GetZ(int index) => ValueAt(index).ObjectValue is GesSpatialValue vector ? vector.Z : 0d;

    internal ref readonly GesValue VmValueAt(int index) => ref ValueAt(index);
}
