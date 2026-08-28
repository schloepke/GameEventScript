#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using StepH.GameEventScript.Runtime.Values;

namespace StepH.GameEventScript.Api;

/// <summary>
/// Ordered message arguments. Argument names are part of the message signature
/// and therefore preserve source order.
/// </summary>
public sealed class GameEventScriptMessageArguments : IReadOnlyCollection<KeyValuePair<string, GameEventScriptValue>>
{
    public static GameEventScriptMessageArguments Empty { get; } = new([], [], [], null, null);

    private readonly string[] _names;
    private readonly GesValue[] _values;
    private readonly IReadOnlyList<string> _signatureLabels;
    private Dictionary<string, int>? _lookup;
    private GameEventScriptValue[]? _boxedValues;

    private GameEventScriptMessageArguments(
        string[] names,
        GesValue[] values,
        IReadOnlyList<string> signatureLabels,
        Dictionary<string, int>? lookup,
        GameEventScriptValue[]? boxedValues)
    {
        _names = names;
        _values = values;
        _signatureLabels = signatureLabels;
        _lookup = lookup;
        _boxedValues = boxedValues;
    }

    public int Count => _values.Length;

    public IReadOnlyList<string> SignatureLabels => _signatureLabels;

    public IEnumerable<string> Keys => new KeyEnumerable(_names);

    public IEnumerable<GameEventScriptValue> Values => new ValueEnumerable(this);

    public GameEventScriptValue this[int index] => ValueAt(index);

    public GameEventScriptValue this[string name] => ValueAt(IndexOfRequired(name));

    public string NameAt(int index) => _names[index];

    public GameEventScriptBytecodeTypeKind KindAt(int index) => _values[index].Kind;

    public GameEventScriptBytecodeInstructionUnit UnitAt(int index) => _values[index].Unit;

    public bool HasValueAt(int index) => _values[index].HasValue;

    public bool IsNothingAt(int index) => _values[index].IsNothing;

    public bool IsNumericAt(int index) => _values[index].IsNumeric;

    public long GetAsInteger(int index)
    {
        ref readonly var value = ref _values[index];
        return value.Kind switch
        {
            GameEventScriptBytecodeTypeKind.Integer => value.IntegerValue,
            GameEventScriptBytecodeTypeKind.Float or GameEventScriptBytecodeTypeKind.Percentage => ToIntegerSaturated(value.FloatValue),
            GameEventScriptBytecodeTypeKind.Boolean => value.IsTrue ? 1 : 0,
            _ => ToIntegerSaturated(value.AsNumeric)
        };
    }

    public long GetAsInteger(string name) => GetAsInteger(IndexOfRequired(name));

    public double GetAsNumber(int index)
    {
        ref readonly var value = ref _values[index];
        return value.Kind switch
        {
            GameEventScriptBytecodeTypeKind.Integer => value.IntegerValue,
            GameEventScriptBytecodeTypeKind.Float or GameEventScriptBytecodeTypeKind.Percentage => value.FloatValue,
            GameEventScriptBytecodeTypeKind.Boolean => value.IsTrue ? 1d : 0d,
            _ => value.AsNumeric
        };
    }

    public double GetAsNumber(string name) => GetAsNumber(IndexOfRequired(name));

    public bool GetAsBoolean(int index)
    {
        ref readonly var value = ref _values[index];
        return value.Kind switch
        {
            GameEventScriptBytecodeTypeKind.Boolean => value.IsTrue,
            GameEventScriptBytecodeTypeKind.Integer => value.IntegerValue != 0,
            GameEventScriptBytecodeTypeKind.Float or GameEventScriptBytecodeTypeKind.Percentage => value.FloatValue != 0d,
            GameEventScriptBytecodeTypeKind.Vector or GameEventScriptBytecodeTypeKind.Point when value.ObjectValue is GesValueVectorPoint vector => vector.X != 0d || vector.Y != 0d || vector.Z != 0d,
            _ => value.IsTrue
        };
    }

    public bool GetAsBoolean(string name) => GetAsBoolean(IndexOfRequired(name));

    public string GetAsText(int index) => _values[index].TextValue.Length > 0 || _values[index].Kind is GameEventScriptBytecodeTypeKind.Text or GameEventScriptBytecodeTypeKind.Tag
        ? _values[index].TextValue
        : _values[index].ToText;

    public string GetAsText(string name) => GetAsText(IndexOfRequired(name));

    public GameEventScriptValue ValueAt(int index)
    {
        var boxedValues = _boxedValues;
        if (boxedValues is null)
        {
            boxedValues = new GameEventScriptValue[_values.Length];
            _boxedValues = boxedValues;
        }

        return boxedValues[index] ??= GameEventScriptValueFactory.FromVmValue(in _values[index]);
    }

    public bool ContainsKey(string name) => IndexOf(name) >= 0;

    public int IndexOf(string name)
    {
        var normalizedName = GameEventScriptMessageSignature.NormalizeParameterName(name);
        var lookup = GetLookup();
        return lookup.TryGetValue(normalizedName, out var index) ? index : -1;
    }

    public IEnumerator<KeyValuePair<string, GameEventScriptValue>> GetEnumerator() => new PairEnumerator(this);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

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

    public static GameEventScriptMessageArguments Create(IReadOnlyDictionary<string, GameEventScriptValue>? values)
    {
        if (values is null || values.Count == 0)
        {
            return Empty;
        }

        var names = new string[values.Count];
        var vmValues = new GesValue[values.Count];
        var boxedValues = new GameEventScriptValue[values.Count];
        var index = 0;
        foreach (var pair in values)
        {
            names[index] = pair.Key;
            var value = pair.Value ?? GameEventScriptValueFactory.GesNothing();
            boxedValues[index] = value;
            vmValues[index] = value.GetVmValue();
            index++;
        }

        return CreateOrdered(names, vmValues, boxedValues);
    }

    internal static GameEventScriptMessageArguments CreateOrdered(string[] names, GameEventScriptValue[] values)
    {
        if (values.Length == 0)
        {
            return Empty;
        }

        var vmValues = new GesValue[values.Length];
        for (var index = 0; index < values.Length; index++)
        {
            var value = values[index] ?? GameEventScriptValueFactory.GesNothing();
            values[index] = value;
            vmValues[index] = value.GetVmValue();
        }

        return CreateOrdered(names, vmValues, values);
    }

    internal static GameEventScriptMessageArguments CreatePrecomputed(string[] normalizedNames, GesValue[] values)
    {
        if (values.Length == 0)
        {
            return Empty;
        }

        return new GameEventScriptMessageArguments(normalizedNames, values, normalizedNames, null, null);
    }

    internal ref readonly GesValue VmValueAt(int index) => ref _values[index];

    private static long ToIntegerSaturated(double number)
    {
        if (double.IsNaN(number)) return 0;
        var truncated = Math.Truncate(number);
        if (truncated > long.MaxValue) return long.MaxValue;
        if (truncated < long.MinValue) return long.MinValue;
        return (long)truncated;
    }

    private static GameEventScriptMessageArguments CreateOrdered(string[] names, GesValue[] values, GameEventScriptValue[]? boxedValues)
    {
        if (values.Length == 0)
        {
            return Empty;
        }

        if (names.Length != values.Length)
        {
            throw new ArgumentException("Argument name count must match argument value count.", nameof(names));
        }

        var signatureLabels = new string[names.Length];
        for (var index = 0; index < names.Length; index++)
        {
            var normalizedName = GameEventScriptMessageSignature.NormalizeParameterName(names[index]);
            signatureLabels[index] = normalizedName;
            if (string.IsNullOrEmpty(names[index]))
            {
                names[index] = normalizedName;
            }
        }

        return new GameEventScriptMessageArguments(names, values, signatureLabels, null, boxedValues);
    }

    private Dictionary<string, int> GetLookup()
    {
        if (_lookup is { } lookup)
        {
            return lookup;
        }

        var created = new Dictionary<string, int>(_values.Length, StringComparer.Ordinal);
        for (var index = 0; index < _values.Length; index++)
        {
            var normalizedName = _signatureLabels[index];
            if (!string.Equals(normalizedName, GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal))
            {
                created[normalizedName] = index;
            }
        }

        _lookup = created;
        return created;
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

    private sealed class KeyEnumerable(string[] names) : IEnumerable<string>, IEnumerator<string>
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
            if (next >= names.Length)
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

    private sealed class ValueEnumerable(GameEventScriptMessageArguments arguments) : IEnumerable<GameEventScriptValue>, IEnumerator<GameEventScriptValue>
    {
        private int _index = -1;

        public GameEventScriptValue Current => arguments.ValueAt(_index);

        object IEnumerator.Current => Current;

        public IEnumerator<GameEventScriptValue> GetEnumerator()
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

    private sealed class PairEnumerator(GameEventScriptMessageArguments arguments) : IEnumerator<KeyValuePair<string, GameEventScriptValue>>
    {
        private int _index = -1;

        public KeyValuePair<string, GameEventScriptValue> Current => new(arguments.NameAt(_index), arguments.ValueAt(_index));

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
