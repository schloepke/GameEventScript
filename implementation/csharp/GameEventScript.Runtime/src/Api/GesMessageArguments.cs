// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using GameEventScript.Runtime;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Api;

/// <summary>
/// One named value in an ordered message argument list.
/// </summary>
public readonly struct GesMessageArgument
{
    /// <summary>
    /// Creates an argument from a label and a value. The label is normalized: leading and trailing ASCII whitespace is removed, and
    /// <see langword="null"/>, empty or whitespace-only labels become the positional label <c>_</c>.
    /// </summary>
    /// <param name="name">The external argument label, or <see langword="null"/> for a positional argument.</param>
    /// <param name="value">The argument value; it is stored as given.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not valid Unicode or, after trimming, neither <c>_</c> nor a portable
    /// lowercase argument label.</exception>
    public GesMessageArgument(string? name, GesValue value)
    {
        Name = GesMessageSignature.NormalizeParameterName(name);
        Value = value;
    }

    /// <summary>
    /// Gets the normalized external label, or <c>_</c> for a positional argument.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the argument value.
    /// </summary>
    public GesValue Value { get; }
}

/// <summary>
/// Ordered message arguments. Argument names are part of the message signature
/// and therefore preserve source order.
/// </summary>
public sealed class GesMessageArguments : IReadOnlyCollection<GesMessageArgument>
{
    /// <summary>
    /// Gets the shared instance without arguments.
    /// </summary>
    public static GesMessageArguments Empty { get; } = new([], []);

    private readonly IReadOnlyList<string> _names;
    private readonly GesValue[] _values;

    private GesMessageArguments(IReadOnlyList<string> names, GesValue[] values)
    {
        _names = names;
        _values = values;
        NestingDepth = GesValue.ContainerDepth(values);
    }

    // A Message is one level deeper than its deepest argument (specs/HostRuntime.md#value-nesting).
    internal long NestingDepth { get; }

    /// <summary>
    /// Gets the number of positional and named arguments.
    /// </summary>
    public int Count => _values.Length;

    /// <summary>
    /// Gets an immutable view of the normalized signature labels in argument order.
    /// </summary>
    public IReadOnlyList<string> SignatureLabels => _names;

    /// <summary>
    /// Gets the normalized labels in argument order, including repeated positional <c>_</c> labels. Each access returns a new sequence that
    /// serves as its own enumerator, so enumerate one returned instance only once at a time.
    /// </summary>
    public IEnumerable<string> Keys => new KeyEnumerable(_names);

    /// <summary>
    /// Gets the argument values in argument order. Each access returns a new sequence that serves as its own enumerator, so enumerate one
    /// returned instance only once at a time.
    /// </summary>
    public IEnumerable<GesValue> Values => new ValueEnumerable(this);

    /// <summary>
    /// Gets the argument value at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Count"/>.</param>
    /// <exception cref="IndexOutOfRangeException"><paramref name="index"/> is outside the argument list.</exception>
    public GesValue this[int index] => ValueAt(index);

    /// <summary>
    /// Gets the value of the argument with the label <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The argument label. Leading and trailing ASCII whitespace is ignored.</param>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">No argument carries the label <paramref name="name"/>; positional
    /// <c>_</c> arguments are never found by name.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not valid Unicode or not a portable lowercase argument label.</exception>
    public GesValue this[string name] => ValueAt(IndexOfRequired(name));

    /// <summary>
    /// Gets the normalized label of the argument at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Count"/>.</param>
    /// <returns>The label, or <c>_</c> for a positional argument.</returns>
    public string NameAt(int index) => _names[index];

    /// <summary>
    /// Gets the portable value kind of the argument at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Count"/>.</param>
    /// <returns>The argument's kind, for example Integer for an integral number or quantity.</returns>
    public GesBytecodeTypeKind KindAt(int index) => _values[index].Kind;

    /// <summary>
    /// Gets the quantity unit of the argument at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Count"/>.</param>
    /// <returns>The unit of a quantity, Vector or Point, or <see cref="GesQuantityUnit.UnitNone"/> for a unitless value.</returns>
    public GesQuantityUnit UnitAt(int index) => _values[index].Unit;

    /// <summary>
    /// Determines whether the argument at <paramref name="index"/> carries content.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Count"/>.</param>
    /// <returns><see langword="true"/> for every number, Boolean, Tag, Vector, Point, Handler, Message and external value, including zero and
    /// <see langword="false"/>; <see langword="false"/> for Nothing and for an empty Text, Dice, Bytes, List, Map, Record or Range.</returns>
    public bool HasValueAt(int index) => _values[index].HasValue;

    /// <summary>
    /// Determines whether the argument at <paramref name="index"/> is Nothing.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Count"/>.</param>
    /// <returns><see langword="true"/> for Nothing and for a binary64 or Percentage value holding NaN; otherwise <see langword="false"/>.</returns>
    public bool IsNothingAt(int index) => _values[index].IsNothing;

    /// <summary>
    /// Determines whether the argument at <paramref name="index"/> is numeric.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Count"/>.</param>
    /// <returns><see langword="true"/> for numbers and quantities, Percentages, Booleans and Dice; otherwise <see langword="false"/>, also for
    /// Vectors and Points.</returns>
    public bool IsNumericAt(int index) => _values[index].IsNumeric;

    /// <summary>
    /// Converts the argument at <paramref name="index"/> to a signed 64-bit integer, as <see cref="GesValue.AsInteger"/> does.
    /// Integer values are returned unchanged and Dice yield the sum of their rolls; Boolean yields 1 or 0; binary64 numbers and
    /// Percentage ratios are truncated toward zero and saturated to the Int64 range. Every other kind, including Nothing, yields 0. A
    /// quantity's unit is ignored, so the number is read in the value's own unit.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Count"/>.</param>
    /// <returns>The converted integer.</returns>
    public long GetAsInteger(int index)
    {
        ref readonly var value = ref _values[index];
        return value.Kind switch
        {
            GesBytecodeTypeKind.Integer or GesBytecodeTypeKind.Dice => value.IntegerValue,
            Float or Percentage => ToIntegerSaturated(value.FloatValue),
            GesBytecodeTypeKind.Boolean => value.IsTrue ? 1 : 0,
            _ => ToIntegerSaturated(value.AsNumeric)
        };
    }

    /// <summary>
    /// Converts the argument with the label <paramref name="name"/> to a signed 64-bit integer, as <see cref="GetAsInteger(int)"/> does.
    /// </summary>
    /// <param name="name">The argument label. Leading and trailing ASCII whitespace is ignored.</param>
    /// <returns>The converted integer.</returns>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">No argument carries the label <paramref name="name"/>; positional
    /// <c>_</c> arguments are never found by name.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not valid Unicode or not a portable lowercase argument label.</exception>
    public long GetAsInteger(string name) => GetAsInteger(IndexOfRequired(name));

    /// <summary>
    /// Converts the argument at <paramref name="index"/> to a binary64 number, as <see cref="GesValue.AsNumber"/> does.
    /// Integer values and Dice sums are converted to binary64 and may round beyond 2^53; binary64 numbers are returned unchanged;
    /// a Percentage yields its ratio, so 50% is 0.5; Boolean yields 1 or 0. Every other kind, including Nothing, yields NaN. A quantity's unit is
    /// ignored, so the number is read in the value's own unit.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Count"/>.</param>
    /// <returns>The converted number, or NaN for a non-numeric argument.</returns>
    public double GetAsNumber(int index)
    {
        ref readonly var value = ref _values[index];
        return value.Kind switch
        {
            GesBytecodeTypeKind.Integer or GesBytecodeTypeKind.Dice => value.IntegerValue,
            Float or Percentage => value.FloatValue,
            GesBytecodeTypeKind.Boolean => value.IsTrue ? 1d : 0d,
            _ => value.AsNumeric
        };
    }

    /// <summary>
    /// Converts the argument with the label <paramref name="name"/> to a binary64 number, as <see cref="GetAsNumber(int)"/> does.
    /// </summary>
    /// <param name="name">The argument label. Leading and trailing ASCII whitespace is ignored.</param>
    /// <returns>The converted number, or NaN for a non-numeric argument.</returns>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">No argument carries the label <paramref name="name"/>; positional
    /// <c>_</c> arguments are never found by name.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not valid Unicode or not a portable lowercase argument label.</exception>
    public double GetAsNumber(string name) => GetAsNumber(IndexOfRequired(name));

    /// <summary>
    /// Converts the argument at <paramref name="index"/> to a Boolean, as <see cref="GesValue.AsBoolean"/> does.
    /// A Boolean is returned unchanged; numbers, quantities and Percentages are true when nonzero; a Vector or Point is true when
    /// any component is nonzero; a Text is true only when it is <c>true</c> (ASCII case-insensitive) or <c>1</c>. Every other kind, including
    /// Nothing, Tag, Dice, Bytes and the collections, yields <see langword="false"/>.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Count"/>.</param>
    /// <returns>The converted Boolean.</returns>
    public bool GetAsBoolean(int index)
    {
        ref readonly var value = ref _values[index];
        return value.Kind switch
        {
            GesBytecodeTypeKind.Boolean => value.IsTrue,
            GesBytecodeTypeKind.Integer => value.IntegerValue != 0,
            Float or Percentage => value.FloatValue != 0d,
            Vector or Point when value.ObjectValue is GesSpatialValue vector => vector.X != 0d || vector.Y != 0d || vector.Z != 0d,
            _ => value.IsTrue
        };
    }

    /// <summary>
    /// Converts the argument with the label <paramref name="name"/> to a Boolean, as <see cref="GetAsBoolean(int)"/> does.
    /// </summary>
    /// <param name="name">The argument label. Leading and trailing ASCII whitespace is ignored.</param>
    /// <returns>The converted Boolean.</returns>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">No argument carries the label <paramref name="name"/>; positional
    /// <c>_</c> arguments are never found by name.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not valid Unicode or not a portable lowercase argument label.</exception>
    public bool GetAsBoolean(string name) => GetAsBoolean(IndexOfRequired(name));

    /// <summary>
    /// Converts the argument at <paramref name="index"/> to text, as <see cref="GesValue.AsText"/> does.
    /// A Text yields its content and a Tag its name without the leading <c>#</c>. Every other kind yields a newly formatted canonical
    /// text form, the same as <see cref="GesValue.ToString"/>, for example <c>nothing</c>, <c>true</c> or <c>:Vector(x: 1, y: 2, z: 3)</c>.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Count"/>.</param>
    /// <returns>The argument text.</returns>
    public string GetAsText(int index) => _values[index].TextValue.Length > 0 || _values[index].Kind is GesBytecodeTypeKind.Text or GesBytecodeTypeKind.Tag
        ? _values[index].TextValue
        : _values[index].ToText;

    /// <summary>
    /// Converts the argument with the label <paramref name="name"/> to text, as <see cref="GetAsText(int)"/> does.
    /// </summary>
    /// <param name="name">The argument label. Leading and trailing ASCII whitespace is ignored.</param>
    /// <returns>The argument text.</returns>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">No argument carries the label <paramref name="name"/>; positional
    /// <c>_</c> arguments are never found by name.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not valid Unicode or not a portable lowercase argument label.</exception>
    public string GetAsText(string name) => GetAsText(IndexOfRequired(name));

    /// <summary>
    /// Gets the argument value at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The zero-based argument index; it must be nonnegative and less than <see cref="Count"/>.</param>
    /// <returns>The stored value.</returns>
    /// <exception cref="IndexOutOfRangeException"><paramref name="index"/> is outside the argument list.</exception>
    public GesValue ValueAt(int index) => _values[index];

    /// <summary>
    /// Determines whether an argument carries the label <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The argument label. Leading and trailing ASCII whitespace is ignored.</param>
    /// <returns><see langword="true"/> when a named argument has the normalized label; always <see langword="false"/> for <c>_</c>, an empty
    /// or a whitespace-only label.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not valid Unicode or not a portable lowercase argument label.</exception>
    public bool ContainsKey(string name) => IndexOf(name) >= 0;

    /// <summary>
    /// Finds the index of the argument with the label <paramref name="name"/>, comparing normalized labels ordinally.
    /// </summary>
    /// <param name="name">The argument label. Leading and trailing ASCII whitespace is ignored.</param>
    /// <returns>The zero-based index, or -1 when no named argument has the label. Positional <c>_</c> arguments are never found, so <c>_</c>, an
    /// empty or a whitespace-only label returns -1.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not valid Unicode or not a portable lowercase argument label.</exception>
    public int IndexOf(string name)
    {
        var normalizedName = GesMessageSignature.NormalizeParameterName(name);
        if (string.Equals(normalizedName, GesMessageSignature.UnlabeledParameterName, StringComparison.Ordinal))
        {
            return -1;
        }

        for (var index = 0; index < _names.Count; index++)
        {
            if (string.Equals(_names[index], normalizedName, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Returns a new enumerator over the label and value pairs in argument order.
    /// </summary>
    /// <returns>An enumerator that creates each <see cref="GesMessageArgument"/> on access without copying the argument list.</returns>
    public IEnumerator<GesMessageArgument> GetEnumerator() => new PairEnumerator(this);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Formats the arguments as <c>label: value</c> pairs separated by <c>, </c>, in argument order.
    /// </summary>
    /// <returns>The formatted arguments, with <c>_</c> as the label of positional arguments and each value in its canonical text form, or an
    /// empty string when there are no arguments.</returns>
    public override string ToString()
    {
        if (_values.Length == 0)
        {
            return "";
        }

        var builder = new StringBuilder();
        for (var index = 0; index < _values.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }

            builder.Append(_names[index]).Append(": ").Append(_values[index].ToText);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Creates arguments from an ordered list by copying its labels and values; the order defines the message signature.
    /// </summary>
    /// <param name="arguments">The arguments in signature order; <see langword="null"/> or an empty list yields <see cref="Empty"/>.</param>
    /// <returns>A new immutable argument list, or <see cref="Empty"/>.</returns>
    /// <exception cref="ArgumentException">A named label occurs more than once; the positional label <c>_</c> may repeat.</exception>
    public static GesMessageArguments Create(IReadOnlyList<GesMessageArgument>? arguments)
    {
        if (arguments is null || arguments.Count == 0)
        {
            return Empty;
        }

        var names = new string[arguments.Count];
        var vmValues = new GesValue[arguments.Count];
        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            names[index] = argument.Name;
            vmValues[index] = argument.Value;
        }

        return CreateOrdered(names, vmValues);
    }

    internal static GesMessageArguments CreateOrdered(string[] names, GesValue[] values)
    {
        if (values.Length == 0)
        {
            return Empty;
        }

        return CreateOrderedCore(names, values);
    }

    // Labels must already be immutable; ownership of the values is transferred to the message.
    internal static GesMessageArguments CreatePrecomputed(IReadOnlyList<string> normalizedNames, GesValue[] values)
    {
        if (values.Length == 0)
        {
            return Empty;
        }

        return new GesMessageArguments(normalizedNames, values);
    }

    internal ref readonly GesValue VmValueAt(int index) => ref _values[index];

    private static long ToIntegerSaturated(double number) => GesNumber.ToIntegerSaturated(number);

    private static GesMessageArguments CreateOrderedCore(string[] names, GesValue[] values)
    {
        if (values.Length == 0)
        {
            return Empty;
        }

        if (names.Length != values.Length)
        {
            throw new ArgumentException("Argument name count must match argument value count.", nameof(names));
        }

        for (var index = 0; index < names.Length; index++)
        {
            var normalizedName = GesMessageSignature.NormalizeParameterName(names[index]);
            if (!string.Equals(normalizedName, GesMessageSignature.UnlabeledParameterName, StringComparison.Ordinal))
            {
                for (var previous = 0; previous < index; previous++)
                {
                    if (string.Equals(names[previous], normalizedName, StringComparison.Ordinal))
                    {
                        throw new ArgumentException($"Message argument name '{normalizedName}' occurs more than once after normalization.", nameof(names));
                    }
                }
            }

            names[index] = normalizedName;
        }

        return new GesMessageArguments(GesReadOnlyArray<string>.FromOwnedArray(names), values);
    }

    private int IndexOfRequired(string name)
    {
        var index = IndexOf(name);
        if (index < 0)
        {
            throw new KeyNotFoundException($"Message argument '{name}' was not found.");
        }

        return index;
    }

    private sealed class KeyEnumerable(IReadOnlyList<string> names) : IEnumerable<string>, IEnumerator<string>
    {
        private int _index = -1;

        public string Current => names[_index];

        object IEnumerator.Current => Current;

        public IEnumerator<string> GetEnumerator()
        {
            _index = -1;
            return this;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool MoveNext()
        {
            var next = _index + 1;
            if (next >= names.Count)
            {
                return false;
            }

            _index = next;
            return true;
        }

        public void Reset() => _index = -1;

        public void Dispose()
        {
        }
    }

    private sealed class ValueEnumerable(GesMessageArguments arguments) : IEnumerable<GesValue>, IEnumerator<GesValue>
    {
        private int _index = -1;

        public GesValue Current => arguments.ValueAt(_index);

        object IEnumerator.Current => Current;

        public IEnumerator<GesValue> GetEnumerator()
        {
            _index = -1;
            return this;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool MoveNext()
        {
            var next = _index + 1;
            if (next >= arguments.Count)
            {
                return false;
            }

            _index = next;
            return true;
        }

        public void Reset() => _index = -1;

        public void Dispose()
        {
        }
    }

    private sealed class PairEnumerator(GesMessageArguments arguments) : IEnumerator<GesMessageArgument>
    {
        private int _index = -1;

        public GesMessageArgument Current => new(arguments.NameAt(_index), arguments.ValueAt(_index));

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            var next = _index + 1;
            if (next >= arguments.Count)
            {
                return false;
            }

            _index = next;
            return true;
        }

        public void Reset() => _index = -1;

        public void Dispose()
        {
        }
    }
}
