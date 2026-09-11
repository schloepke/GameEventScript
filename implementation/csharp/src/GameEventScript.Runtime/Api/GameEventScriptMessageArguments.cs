// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using GameEventScript.Runtime;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace GameEventScript.Api;

/// <summary>
/// One named value in an ordered message argument list.
/// </summary>
public readonly struct GameEventScriptMessageArgument
{
    /// <summary>
    /// Initializes a new instance of Game Event Script Message Argument.
    /// </summary>
    /// <param name="name">The name value.</param>
    /// <param name="value">The value value.</param>
    public GameEventScriptMessageArgument(string? name, GesValue value)
    {
        Name = GameEventScriptMessageSignature.NormalizeParameterName(name);
        Value = value;
    }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the value.
    /// </summary>
    public GesValue Value { get; }
}

/// <summary>
/// Ordered message arguments. Argument names are part of the message signature
/// and therefore preserve source order.
/// </summary>
public sealed class GameEventScriptMessageArguments : IReadOnlyCollection<GameEventScriptMessageArgument>
{
    /// <summary>
    /// Gets the empty.
    /// </summary>
    public static GameEventScriptMessageArguments Empty { get; } = new([], []);

    private readonly IReadOnlyList<string> _names;
    private readonly GesValue[] _values;

    private GameEventScriptMessageArguments(IReadOnlyList<string> names, GesValue[] values)
    {
        _names = names;
        _values = values;
    }

    /// <summary>
    /// Gets the count.
    /// </summary>
    public int Count => _values.Length;

    /// <summary>
    /// Gets an immutable view of the normalized signature labels in argument order.
    /// </summary>
    public IReadOnlyList<string> SignatureLabels => _names;

    /// <summary>
    /// Gets the keys.
    /// </summary>
    public IEnumerable<string> Keys => new KeyEnumerable(_names);

    /// <summary>
    /// Gets the values.
    /// </summary>
    public IEnumerable<GesValue> Values => new ValueEnumerable(this);

    /// <summary>
    /// Gets the value at the specified index.
    /// </summary>
    /// <param name="index">The index value.</param>
    public GesValue this[int index] => ValueAt(index);

    /// <summary>
    /// Gets the value at the specified index.
    /// </summary>
    /// <param name="name">The name value.</param>
    public GesValue this[string name] => ValueAt(IndexOfRequired(name));

    /// <summary>
    /// Performs the name at operation.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
    public string NameAt(int index) => _names[index];

    /// <summary>
    /// Performs the kind at operation.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
    public GameEventScriptBytecodeTypeKind KindAt(int index) => _values[index].Kind;

    /// <summary>
    /// Performs the unit at operation.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
    public GameEventScriptBytecodeInstructionUnit UnitAt(int index) => _values[index].Unit;

    /// <summary>
    /// Determines whether has value at.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
    public bool HasValueAt(int index) => _values[index].HasValue;

    /// <summary>
    /// Determines whether is nothing at.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
    public bool IsNothingAt(int index) => _values[index].IsNothing;

    /// <summary>
    /// Determines whether is numeric at.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
    public bool IsNumericAt(int index) => _values[index].IsNumeric;

    /// <summary>
    /// Gets the as integer.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
    public long GetAsInteger(int index)
    {
        ref readonly var value = ref _values[index];
        return value.Kind switch
        {
            GameEventScriptBytecodeTypeKind.Integer => value.IntegerValue,
            Float or Percentage => ToIntegerSaturated(value.FloatValue),
            GameEventScriptBytecodeTypeKind.Boolean => value.IsTrue ? 1 : 0,
            _ => ToIntegerSaturated(value.AsNumeric)
        };
    }

    /// <summary>
    /// Gets the as integer.
    /// </summary>
    /// <param name="name">The name value.</param>
    /// <returns>The result of the operation.</returns>
    public long GetAsInteger(string name) => GetAsInteger(IndexOfRequired(name));

    /// <summary>
    /// Gets the as number.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
    public double GetAsNumber(int index)
    {
        ref readonly var value = ref _values[index];
        return value.Kind switch
        {
            GameEventScriptBytecodeTypeKind.Integer => value.IntegerValue,
            Float or Percentage => value.FloatValue,
            GameEventScriptBytecodeTypeKind.Boolean => value.IsTrue ? 1d : 0d,
            _ => value.AsNumeric
        };
    }

    /// <summary>
    /// Gets the as number.
    /// </summary>
    /// <param name="name">The name value.</param>
    /// <returns>The result of the operation.</returns>
    public double GetAsNumber(string name) => GetAsNumber(IndexOfRequired(name));

    /// <summary>
    /// Gets the as boolean.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
    public bool GetAsBoolean(int index)
    {
        ref readonly var value = ref _values[index];
        return value.Kind switch
        {
            GameEventScriptBytecodeTypeKind.Boolean => value.IsTrue,
            GameEventScriptBytecodeTypeKind.Integer => value.IntegerValue != 0,
            Float or Percentage => value.FloatValue != 0d,
            Vector or Point when value.ObjectValue is GesValueVectorPoint vector => vector.X != 0d || vector.Y != 0d || vector.Z != 0d,
            _ => value.IsTrue
        };
    }

    /// <summary>
    /// Gets the as boolean.
    /// </summary>
    /// <param name="name">The name value.</param>
    /// <returns>The result of the operation.</returns>
    public bool GetAsBoolean(string name) => GetAsBoolean(IndexOfRequired(name));

    /// <summary>
    /// Gets the as text.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
    public string GetAsText(int index) => _values[index].TextValue.Length > 0 || _values[index].Kind is GameEventScriptBytecodeTypeKind.Text or GameEventScriptBytecodeTypeKind.Tag
        ? _values[index].TextValue
        : _values[index].ToText;

    /// <summary>
    /// Gets the as text.
    /// </summary>
    /// <param name="name">The name value.</param>
    /// <returns>The result of the operation.</returns>
    public string GetAsText(string name) => GetAsText(IndexOfRequired(name));

    /// <summary>
    /// Performs the value at operation.
    /// </summary>
    /// <param name="index">The index value.</param>
    /// <returns>The result of the operation.</returns>
    public GesValue ValueAt(int index) => _values[index];

    /// <summary>
    /// Performs the contains key operation.
    /// </summary>
    /// <param name="name">The name value.</param>
    /// <returns>The result of the operation.</returns>
    public bool ContainsKey(string name) => IndexOf(name) >= 0;

    /// <summary>
    /// Performs the index of operation.
    /// </summary>
    /// <param name="name">The name value.</param>
    /// <returns>The result of the operation.</returns>
    public int IndexOf(string name)
    {
        var normalizedName = GameEventScriptMessageSignature.NormalizeParameterName(name);
        if (string.Equals(normalizedName, GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal))
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
    /// Gets the enumerator.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IEnumerator<GameEventScriptMessageArgument> GetEnumerator() => new PairEnumerator(this);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Returns the to string result for this value.
    /// </summary>
    /// <returns>The result of the operation.</returns>
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
    /// Creates a value.
    /// </summary>
    /// <param name="arguments">The arguments value.</param>
    /// <returns>The result of the operation.</returns>
    public static GameEventScriptMessageArguments Create(IReadOnlyList<GameEventScriptMessageArgument>? arguments)
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

    internal static GameEventScriptMessageArguments CreateOrdered(string[] names, GesValue[] values)
    {
        if (values.Length == 0)
        {
            return Empty;
        }

        return CreateOrderedCore(names, values);
    }

    // Labels must already be immutable; ownership of the values is transferred to the message.
    internal static GameEventScriptMessageArguments CreatePrecomputed(IReadOnlyList<string> normalizedNames, GesValue[] values)
    {
        if (values.Length == 0)
        {
            return Empty;
        }

        return new GameEventScriptMessageArguments(normalizedNames, values);
    }

    internal ref readonly GesValue VmValueAt(int index) => ref _values[index];

    private static long ToIntegerSaturated(double number) => GameEventScriptNumber.ToIntegerSaturated(number);

    private static GameEventScriptMessageArguments CreateOrderedCore(string[] names, GesValue[] values)
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
            var normalizedName = GameEventScriptMessageSignature.NormalizeParameterName(names[index]);
            if (!string.Equals(normalizedName, GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal))
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

        return new GameEventScriptMessageArguments(GameEventScriptReadOnlyArray<string>.FromOwnedArray(names), values);
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

    private sealed class ValueEnumerable(GameEventScriptMessageArguments arguments) : IEnumerable<GesValue>, IEnumerator<GesValue>
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

    private sealed class PairEnumerator(GameEventScriptMessageArguments arguments) : IEnumerator<GameEventScriptMessageArgument>
    {
        private int _index = -1;

        public GameEventScriptMessageArgument Current => new(arguments.NameAt(_index), arguments.ValueAt(_index));

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
