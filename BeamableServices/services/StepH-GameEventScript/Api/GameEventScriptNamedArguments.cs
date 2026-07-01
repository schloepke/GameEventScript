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
    public static GameEventScriptNamedArguments Empty { get; } = new([], [], new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal));

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
        var orderedPairs = new KeyValuePair<string, GameEventScriptValue>[values.Count];
        var index = 0;
        foreach (var pair in values)
        {
            orderedPairs[index++] = new KeyValuePair<string, GameEventScriptValue>(pair.Key, pair.Value ?? GameEventScriptValueFactory.GesNothing());
        }

        return CreateOrdered(orderedPairs);
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
    public GameEventScriptValue this[string key] => _values[key];

    /// <summary>
    /// Provides access to the value at the specified index within a collection of ordered arguments.
    /// Allows retrieval of a game event script value by its positional index.
    /// </summary>
    public GameEventScriptValue this[int index] => _orderedPairs[index].Value;

    /// Represents a collection of keys from the ordered pairs of named arguments in a game event script.
    /// Provides an enumerable sequence of keys, maintaining the order in which the arguments were defined.
    public IEnumerable<string> Keys => new KeyEnumerable(_orderedPairs);

    /// Provides a collection of labels associated with the signature of the arguments.
    /// The labels represent the normalized keys used to construct the signature of a
    /// GameEventScriptNamedArguments instance, enabling consistent identification and comparison
    /// of argument sets.
    public IReadOnlyList<string> SignatureLabels { get; }

    /// Provides an enumerable collection of values associated with the named arguments in a game event script.
    /// Each value corresponds to the argument at a specific position, maintaining the order of the argument collection.
    public IEnumerable<GameEventScriptValue> Values => new ValueEnumerable(_orderedPairs);

    /// Gets the total number of key-value pairs contained in the instance.
    /// This property reflects the combined count of all ordered named arguments,
    /// allowing consumers to determine the total size of the collection.
    public int Count => _orderedPairs.Count;

    /// <summary>
    /// Determines whether the current collection contains a specified key.
    /// </summary>
    /// <param name="key">The key to locate in the collection.</param>
    /// <returns><c>true</c> if the key is found in the collection; otherwise, <c>false</c>.</returns>
    public bool ContainsKey(string key) => _values.ContainsKey(key);

    /// <summary>
    /// Returns an enumerator that iterates through the key-value pairs
    /// of the named arguments in this collection. The enumerator provides
    /// access to the arguments in their normalized order.
    /// </summary>
    /// <returns>An enumerator for iterating through the key-value pairs
    /// in the collection.</returns>
    public IEnumerator<KeyValuePair<string, GameEventScriptValue>> GetEnumerator() => _orderedPairs.GetEnumerator();

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
        if (_orderedPairs.Count == 0)
        {
            return "";
        }

        var builder = new StringBuilder();
        for (var index = 0; index < _orderedPairs.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }

            var pair = _orderedPairs[index];
            builder.Append(pair.Key).Append(": ").Append(pair.Value);
        }

        return builder.ToString();
    }

    private GameEventScriptNamedArguments(IReadOnlyList<KeyValuePair<string, GameEventScriptValue>> orderedPairs, IReadOnlyList<string> signatureLabels,
        IReadOnlyDictionary<string, GameEventScriptValue> values)
    {
        _orderedPairs = orderedPairs;
        SignatureLabels = signatureLabels;
        _values = values;
    }

    private readonly IReadOnlyList<KeyValuePair<string, GameEventScriptValue>> _orderedPairs;
    private readonly IReadOnlyDictionary<string, GameEventScriptValue> _values;

    internal static GameEventScriptNamedArguments CreateOrdered(KeyValuePair<string, GameEventScriptValue>[] orderedPairs)
    {
        var signatureLabels = new string[orderedPairs.Length];
        for (var index = 0; index < orderedPairs.Length; index++)
        {
            signatureLabels[index] = GameEventScriptMessageSignature.NormalizeParameterName(orderedPairs[index].Key);
        }

        return CreateOrdered(orderedPairs, signatureLabels);
    }

    internal static GameEventScriptNamedArguments CreateOrdered(KeyValuePair<string, GameEventScriptValue>[] orderedPairs, IReadOnlyList<string> signatureLabels)
    {
        if (orderedPairs.Length == 0)
        {
            return Empty;
        }

        if (signatureLabels.Count != orderedPairs.Length)
        {
            throw new ArgumentException("Signature label count must match argument count.", nameof(signatureLabels));
        }

        var values = new Dictionary<string, GameEventScriptValue>(orderedPairs.Length, StringComparer.Ordinal);
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
        for (var index = 0; index < orderedPairs.Length; index++)
        {
            var pair = orderedPairs[index];
            var value = pair.Value ?? GameEventScriptValueFactory.GesNothing();
            if (!ReferenceEquals(value, pair.Value))
            {
                pair = new KeyValuePair<string, GameEventScriptValue>(pair.Key, value);
                orderedPairs[index] = pair;
            }

            var normalizedName = GameEventScriptMessageSignature.NormalizeParameterName(pair.Key);
            var storageName = string.IsNullOrEmpty(pair.Key) ? normalizedName : pair.Key;
            if (!string.Equals(storageName, pair.Key, StringComparison.Ordinal))
            {
                pair = new KeyValuePair<string, GameEventScriptValue>(storageName, value);
                orderedPairs[index] = pair;
            }

            if (!reuseSignatureLabels)
            {
                copiedSignatureLabels![index] = GameEventScriptMessageSignature.NormalizeParameterName(signatureLabels[index]);
            }

            if (!string.Equals(normalizedName, GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal))
            {
                values[normalizedName] = value;
            }
        }

        return new GameEventScriptNamedArguments(orderedPairs, normalizedSignatureLabels, values);
    }

    private sealed class KeyEnumerable(IReadOnlyList<KeyValuePair<string, GameEventScriptValue>> pairs) : IEnumerable<string>, IEnumerator<string>
    {
        private int _index = -1;

        public string Current => pairs[_index].Key;

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
            if (next >= pairs.Count)
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

    private sealed class ValueEnumerable(IReadOnlyList<KeyValuePair<string, GameEventScriptValue>> pairs) : IEnumerable<GameEventScriptValue>, IEnumerator<GameEventScriptValue>
    {
        private int _index = -1;

        public GameEventScriptValue Current => pairs[_index].Value;

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
            if (next >= pairs.Count)
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
