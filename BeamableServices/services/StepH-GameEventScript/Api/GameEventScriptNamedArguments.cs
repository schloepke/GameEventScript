using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
namespace StepH.GameEventScript.Api;

/// <summary>
/// Represents a collection of named arguments in a game event script.
/// Provides functionality for accessing arguments by name or index,
/// as well as normalizing argument collections to ensure consistent formatting.
/// </summary>
public sealed class GameEventScriptNamedArguments : IReadOnlyCollection<KeyValuePair<string, GameEventScriptValue>>
{
    /// Represents an empty instance of the GameEventScriptNamedArguments class.
    /// Provides a shared, immutable, and pre-initialized empty object that can be used
    /// wherever an empty set of named arguments is required, avoiding the overhead of
    /// creating new instances.
    public static GameEventScriptNamedArguments Empty { get; } = new([], [], [], null);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptNamedArguments"/> by normalizing
    /// the provided dictionary of arguments. Any null values within the input dictionary
    /// are replaced with the default value "Nothing."
    /// If the input dictionary is null or empty, a predefined empty instance is returned.
    /// </summary>
    /// <param name="values">The dictionary of named arguments to be normalized. Can be null or empty.</param>
    /// <returns>A new instance of <see cref="GameEventScriptNamedArguments"/>
    /// containing normalized arguments. If the input dictionary is null or empty,
    /// the predefined empty instance is returned.</returns>
    public static GameEventScriptNamedArguments Create(IReadOnlyDictionary<string, GameEventScriptValue>? values)
    {
        if (values is null || values.Count == 0) return Empty;
        var names = new string[values.Count];
        var argumentValues = new GameEventScriptValue[values.Count];
        var index = 0;
        foreach (var pair in values)
        {
            names[index] = pair.Key;
            argumentValues[index] = pair.Value ?? GameEventScriptValueFactory.GesNothing();
            index++;
        }

        return CreateOrdered(names, argumentValues);
    }


    /// <summary>
    /// Normalizes a given collection of named arguments by ensuring that any null values
    /// are replaced with the default "Nothing" value. If the input collection is null
    /// or empty, an empty collection is returned.
    /// </summary>
    /// <param name="values">The collection of named arguments to be normalized. Can be null.</param>
    /// <returns>A normalized read-only dictionary where null values are replaced with "Nothing".
    /// If the input collection is null or empty, an empty collection is returned.</returns>
    public static IReadOnlyDictionary<string, GameEventScriptValue> Normalize(IReadOnlyDictionary<string, GameEventScriptValue>? values)
    {
        var normalized = new Dictionary<string, GameEventScriptValue>(values?.Count ?? 0, StringComparer.Ordinal);
        if (values is null || values.Count == 0)
        {
            return normalized;
        }

        foreach (var pair in values)
        {
            normalized[pair.Key] = pair.Value ?? GameEventScriptValueFactory.GesNothing();
        }

        return normalized;
    }

    /// <summary>
    /// Retrieves the value associated with the specified key from the collection of named arguments.
    /// Provides indexed access to the underlying dictionary storing the arguments.
    /// Throws a KeyNotFoundException if the specified key does not exist in the collection.
    /// </summary>
    /// <param name="key">The key identifying the argument to retrieve.</param>
    /// <returns>The value associated with the specified key.</returns>
    public GameEventScriptValue this[string key] => GetLookup()[key];

    /// <summary>
    /// Provides access to the value at the specified index within a collection of ordered arguments.
    /// Allows retrieval of a game event script value by its positional index.
    /// </summary>
    public GameEventScriptValue this[int index] => _values[index];

    /// Represents a collection of keys from the ordered pairs of named arguments in a game event script.
    /// Provides an enumerable sequence of keys, maintaining the order in which the arguments were defined.
    public IEnumerable<string> Keys => new KeyEnumerable(_names);

    /// Provides a collection of labels associated with the signature of the arguments.
    /// The labels represent the normalized keys used to construct the signature of a
    /// GameEventScriptNamedArguments instance, enabling consistent identification and comparison
    /// of argument sets.
    public IReadOnlyList<string> SignatureLabels { get; }

    /// Provides an enumerable collection of values associated with the named arguments in a game event script.
    /// Each value corresponds to the argument at a specific position, maintaining the order of the argument collection.
    public IEnumerable<GameEventScriptValue> Values => new ValueEnumerable(_values);

    /// Gets the total number of key-value pairs contained in the instance.
    /// This property reflects the combined count of all ordered named arguments,
    /// allowing consumers to determine the total size of the collection.
    public int Count => _values.Length;

    /// <summary>
    /// Determines whether the current collection contains a specified key.
    /// </summary>
    /// <param name="key">The key to locate in the collection.</param>
    /// <returns><c>true</c> if the key is found in the collection; otherwise, <c>false</c>.</returns>
    public bool ContainsKey(string key) => GetLookup().ContainsKey(key);

    /// <summary>
    /// Returns an enumerator that iterates through the key-value pairs
    /// of the named arguments in this collection. The enumerator provides
    /// access to the arguments in their normalized order.
    /// </summary>
    /// <returns>An enumerator for iterating through the key-value pairs
    /// in the collection.</returns>
    public IEnumerator<KeyValuePair<string, GameEventScriptValue>> GetEnumerator() => new PairEnumerator(_names, _values);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Returns a string representation of the current instance of <see cref="GameEventScriptNamedArguments"/>.
    /// Each key-value pair in the collection is converted to a string in the format "Key: Value",
    /// and pairs are separated by commas. If the collection is empty, an empty string is returned.
    /// </summary>
    /// <returns>A string representing the named arguments in the collection, or an empty
    /// string if the collection contains no elements.</returns>
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

            builder.Append(_names[index]).Append(": ").Append(_values[index]);
        }

        return builder.ToString();
    }

    private GameEventScriptNamedArguments(string[] names, GameEventScriptValue[] values, IReadOnlyList<string> signatureLabels,
        Dictionary<string, GameEventScriptValue>? lookup)
    {
        _names = names;
        _values = values;
        SignatureLabels = signatureLabels;
        _lookup = lookup;
    }

    private readonly string[] _names;
    private readonly GameEventScriptValue[] _values;
    private Dictionary<string, GameEventScriptValue>? _lookup;

    internal static GameEventScriptNamedArguments CreateOrdered(KeyValuePair<string, GameEventScriptValue>[] orderedPairs)
    {
        var names = new string[orderedPairs.Length];
        var values = new GameEventScriptValue[orderedPairs.Length];
        var signatureLabels = new string[orderedPairs.Length];
        for (var index = 0; index < orderedPairs.Length; index++)
        {
            var pair = orderedPairs[index];
            names[index] = pair.Key;
            values[index] = pair.Value;
            signatureLabels[index] = GameEventScriptMessageSignature.NormalizeParameterName(pair.Key);
        }

        return CreateOrdered(names, values, signatureLabels);
    }

    internal static GameEventScriptNamedArguments CreateOrdered(KeyValuePair<string, GameEventScriptValue>[] orderedPairs, IReadOnlyList<string> signatureLabels)
    {
        var names = new string[orderedPairs.Length];
        var values = new GameEventScriptValue[orderedPairs.Length];
        for (var index = 0; index < orderedPairs.Length; index++)
        {
            names[index] = orderedPairs[index].Key;
            values[index] = orderedPairs[index].Value;
        }

        return CreateOrdered(names, values, signatureLabels);
    }

    internal static GameEventScriptNamedArguments CreatePrecomputed(string[] normalizedNames, GameEventScriptValue[] values)
    {
        if (values.Length == 0)
        {
            return Empty;
        }

        return new GameEventScriptNamedArguments(normalizedNames, values, normalizedNames, null);
    }

    private static GameEventScriptNamedArguments CreateOrdered(string[] names, GameEventScriptValue[] values)
    {
        var signatureLabels = new string[names.Length];
        for (var index = 0; index < names.Length; index++)
        {
            signatureLabels[index] = GameEventScriptMessageSignature.NormalizeParameterName(names[index]);
        }

        return CreateOrdered(names, values, signatureLabels);
    }

    private static GameEventScriptNamedArguments CreateOrdered(string[] names, GameEventScriptValue[] argumentValues, IReadOnlyList<string> signatureLabels)
    {
        if (argumentValues.Length == 0)
        {
            return Empty;
        }

        if (names.Length != argumentValues.Length)
        {
            throw new ArgumentException("Argument name count must match argument value count.", nameof(names));
        }

        if (signatureLabels.Count != argumentValues.Length)
        {
            throw new ArgumentException("Signature label count must match argument count.", nameof(signatureLabels));
        }

        var reuseSignatureLabels = signatureLabels is string[];
        string[]? copiedSignatureLabels = null;
        IReadOnlyList<string> normalizedSignatureLabels;
        if (reuseSignatureLabels)
        {
            normalizedSignatureLabels = signatureLabels;
        }
        else
        {
            copiedSignatureLabels = new string[signatureLabels.Count];
            normalizedSignatureLabels = copiedSignatureLabels;
        }
        for (var index = 0; index < argumentValues.Length; index++)
        {
            var value = argumentValues[index] ?? GameEventScriptValueFactory.GesNothing();
            argumentValues[index] = value;

            var normalizedName = GameEventScriptMessageSignature.NormalizeParameterName(names[index]);
            if (string.IsNullOrEmpty(names[index]))
            {
                names[index] = normalizedName;
            }

            if (!reuseSignatureLabels)
            {
                copiedSignatureLabels![index] = GameEventScriptMessageSignature.NormalizeParameterName(signatureLabels[index]);
            }

        }

        return new GameEventScriptNamedArguments(names, argumentValues, normalizedSignatureLabels, null);
    }

    private Dictionary<string, GameEventScriptValue> GetLookup()
    {
        if (_lookup is { } lookup)
        {
            return lookup;
        }

        var created = new Dictionary<string, GameEventScriptValue>(_values.Length, StringComparer.Ordinal);
        for (var index = 0; index < _values.Length; index++)
        {
            var normalizedName = SignatureLabels[index];
            if (!string.Equals(normalizedName, GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal))
            {
                created[normalizedName] = _values[index];
            }
        }

        _lookup = created;
        return created;
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

    private sealed class ValueEnumerable(GameEventScriptValue[] values) : IEnumerable<GameEventScriptValue>, IEnumerator<GameEventScriptValue>
    {
        private int _index = -1;

        public GameEventScriptValue Current => values[_index];

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
            if (next >= values.Length)
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

    private sealed class PairEnumerator(string[] names, GameEventScriptValue[] values) : IEnumerator<KeyValuePair<string, GameEventScriptValue>>
    {
        private int _index = -1;

        public KeyValuePair<string, GameEventScriptValue> Current => new(names[_index], values[_index]);

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            var next = _index + 1;
            if (next >= values.Length)
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
