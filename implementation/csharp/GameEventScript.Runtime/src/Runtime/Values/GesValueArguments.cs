// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using GameEventScript.Runtime.Vm;

namespace GameEventScript.Runtime.Values;

/// <summary>
/// Ordered arguments of an extension call or external type constructor. A view lent by the virtual machine reads the caller's registers
/// without copying and is valid only during that extension call: once the call has returned, every value access throws
/// <see cref="InvalidOperationException"/> while <see cref="Length"/> stays unchanged. Copy any value needed later. A view created
/// from an array borrows that array.
/// </summary>
public readonly struct GesValueArguments
{
    private readonly GesValue[]? _values;
    private readonly GesVirtualMachine? _vm;
    private readonly GesUInt16IndexList _registers;
    // The extension call this view belongs to; a retained view fails once that call has ended instead of reading
    // whatever a later handler left in the registers.
    private readonly int _generation;

    /// <summary>
    /// An argument view without values.
    /// </summary>
    public static readonly GesValueArguments Empty = new([]);

    /// <summary>
    /// Creates an argument view over an array without copying it; it stays valid as long as the array does. The caller must not change the
    /// array while the view is in use.
    /// </summary>
    /// <param name="values">The ordered argument values; <see langword="null"/> yields an empty view.</param>
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
        _generation = vm.ExtensionCallGeneration;
        Length = registers.Length;
    }

    /// <summary>
    /// Gets the number of arguments. It does not change when a lent view expires.
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Gets a read-only reference to the argument at <paramref name="index"/> without copying it.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Length"/>.</param>
    /// <exception cref="InvalidOperationException">The view was lent by an extension call that has already returned.</exception>
    public ref readonly GesValue this[int index] => ref ValueAt(index);

    /// <summary>
    /// Gets a read-only reference to the argument at <paramref name="index"/> without copying it. A reference into the registers must not be
    /// kept beyond the extension call.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Length"/>.</param>
    /// <returns>A read-only reference to the argument value.</returns>
    /// <exception cref="IndexOutOfRangeException"><paramref name="index"/> is outside an array-backed view.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside a view lent by the virtual machine.</exception>
    /// <exception cref="InvalidOperationException">The view was lent by an extension call that has already returned.</exception>
    public ref readonly GesValue ValueAt(int index)
    {
        if (_vm is { } vm)
        {
            if (vm.ExtensionCallGeneration != _generation) throw new InvalidOperationException("Extension arguments are only readable during their call.");
            return ref vm.Register(_registers[index]);
        }

        return ref _values![index];
    }

    /// <summary>
    /// Gets the portable value kind of the argument at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Length"/>.</param>
    /// <returns>The argument's kind, for example Integer for an integral number or quantity.</returns>
    /// <exception cref="InvalidOperationException">The view was lent by an extension call that has already returned.</exception>
    public GesBytecodeTypeKind KindAt(int index) => ValueAt(index).Kind;

    /// <summary>
    /// Gets the quantity unit of the argument at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Length"/>.</param>
    /// <returns>The unit of a quantity, Vector or Point, or <see cref="GesQuantityUnit.UnitNone"/> for a unitless value.</returns>
    /// <exception cref="InvalidOperationException">The view was lent by an extension call that has already returned.</exception>
    public GesQuantityUnit UnitAt(int index) => ValueAt(index).Unit;

    /// <summary>
    /// Determines whether the argument at <paramref name="index"/> carries content.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Length"/>.</param>
    /// <returns><see langword="true"/> for every number, Boolean, Tag, Vector, Point, Handler, Message and external value, including zero and
    /// <see langword="false"/>; <see langword="false"/> for Nothing and for an empty Text, Dice, Bytes, List, Map, Record or Range.</returns>
    /// <exception cref="InvalidOperationException">The view was lent by an extension call that has already returned.</exception>
    public bool HasValueAt(int index) => ValueAt(index).HasValue;

    /// <summary>
    /// Determines whether the argument at <paramref name="index"/> is Nothing.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Length"/>.</param>
    /// <returns><see langword="true"/> for Nothing and for a binary64 or Percentage value holding NaN; otherwise <see langword="false"/>.</returns>
    /// <exception cref="InvalidOperationException">The view was lent by an extension call that has already returned.</exception>
    public bool IsNothingAt(int index) => ValueAt(index).IsNothing;

    /// <summary>
    /// Determines whether the argument at <paramref name="index"/> is numeric.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Length"/>.</param>
    /// <returns><see langword="true"/> for numbers and quantities, Percentages, Booleans and Dice; otherwise <see langword="false"/>, also for
    /// Vectors and Points.</returns>
    /// <exception cref="InvalidOperationException">The view was lent by an extension call that has already returned.</exception>
    public bool IsNumericAt(int index) => ValueAt(index).IsNumeric;

    /// <summary>
    /// Converts the argument at <paramref name="index"/> to a signed 64-bit integer, as <see cref="GesValue.AsInteger"/> does.
    /// Integer values are returned unchanged and Dice yield the sum of their rolls; Boolean yields 1 or 0; binary64 numbers and
    /// Percentage ratios are truncated toward zero and saturated to the Int64 range. Every other kind, including Nothing, yields 0. A
    /// quantity's unit is ignored, so the number is read in the value's own unit.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Length"/>.</param>
    /// <returns>The converted integer.</returns>
    /// <exception cref="InvalidOperationException">The view was lent by an extension call that has already returned.</exception>
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
    /// Converts the argument at <paramref name="index"/> to a binary64 number, as <see cref="GesValue.AsNumber"/> does.
    /// Integer values and Dice sums are converted to binary64 and may round beyond 2^53; binary64 numbers are returned unchanged;
    /// a Percentage yields its ratio, so 50% is 0.5; Boolean yields 1 or 0. Every other kind, including Nothing, yields NaN. A quantity's unit is
    /// ignored, so the number is read in the value's own unit.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Length"/>.</param>
    /// <returns>The converted number, or NaN for a non-numeric argument.</returns>
    /// <exception cref="InvalidOperationException">The view was lent by an extension call that has already returned.</exception>
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
    /// Converts the argument at <paramref name="index"/> to a Boolean, as <see cref="GesValue.AsBoolean"/> does.
    /// A Boolean is returned unchanged; numbers, quantities and Percentages are true when nonzero; a Vector or Point is true when
    /// any component is nonzero; a Text is true only when it is <c>true</c> (ASCII case-insensitive) or <c>1</c>. Every other kind, including
    /// Nothing, Tag, Dice, Bytes and the collections, yields <see langword="false"/>.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Length"/>.</param>
    /// <returns>The converted Boolean.</returns>
    /// <exception cref="InvalidOperationException">The view was lent by an extension call that has already returned.</exception>
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
    /// Converts the argument at <paramref name="index"/> to text, as <see cref="GesValue.AsText"/> does.
    /// A Text yields its content and a Tag its name without the leading <c>#</c>. Every other kind yields a newly formatted canonical
    /// text form, the same as <see cref="GesValue.ToString"/>, for example <c>nothing</c>, <c>true</c> or <c>:Vector(x: 1, y: 2, z: 3)</c>.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Length"/>.</param>
    /// <returns>The argument text.</returns>
    /// <exception cref="InvalidOperationException">The view was lent by an extension call that has already returned.</exception>
    public string GetAsText(int index)
    {
        ref readonly var value = ref ValueAt(index);
        return value.TextValue.Length > 0 || value.Kind is GesBytecodeTypeKind.Text or GesBytecodeTypeKind.Tag
            ? value.TextValue
            : value.ToText;
    }

    /// <summary>
    /// Gets the X component of the Vector or Point argument at <paramref name="index"/>, in the argument's unit.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Length"/>.</param>
    /// <returns>The X component, or 0 when the argument is not a Vector or Point.</returns>
    /// <exception cref="InvalidOperationException">The view was lent by an extension call that has already returned.</exception>
    public double GetX(int index) => ValueAt(index).ObjectValue is GesSpatialValue vector ? vector.X : 0d;

    /// <summary>
    /// Gets the Y component of the Vector or Point argument at <paramref name="index"/>, in the argument's unit.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Length"/>.</param>
    /// <returns>The Y component, or 0 when the argument is not a Vector or Point.</returns>
    /// <exception cref="InvalidOperationException">The view was lent by an extension call that has already returned.</exception>
    public double GetY(int index) => ValueAt(index).ObjectValue is GesSpatialValue vector ? vector.Y : 0d;

    /// <summary>
    /// Gets the Z component of the Vector or Point argument at <paramref name="index"/>, in the argument's unit.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Length"/>.</param>
    /// <returns>The Z component, or 0 when the argument is not a Vector or Point.</returns>
    /// <exception cref="InvalidOperationException">The view was lent by an extension call that has already returned.</exception>
    public double GetZ(int index) => ValueAt(index).ObjectValue is GesSpatialValue vector ? vector.Z : 0d;

    internal ref readonly GesValue VmValueAt(int index) => ref ValueAt(index);
}
