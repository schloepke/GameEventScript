#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript;

public sealed class GameEventScriptNamedArguments : IReadOnlyDictionary<string, GameEventScriptValue>
{
    private readonly IReadOnlyList<KeyValuePair<string, GameEventScriptValue>> _orderedPairs;
    private readonly IReadOnlyList<string> _signatureLabels;
    private readonly IReadOnlyDictionary<string, GameEventScriptValue> _values;

    public static IReadOnlyDictionary<string, GameEventScriptValue> Normalize(IReadOnlyDictionary<string, GameEventScriptValue>? values)
        => values is null || values.Count == 0 ? Empty : values.ToDictionary(pair => pair.Key, pair => pair.Value ?? GameEventScriptValue.Nothing, StringComparer.Ordinal);

    private GameEventScriptNamedArguments(
        IReadOnlyList<KeyValuePair<string, GameEventScriptValue>> orderedPairs,
        IReadOnlyList<string> signatureLabels,
        IReadOnlyDictionary<string, GameEventScriptValue> values)
    {
        _orderedPairs = orderedPairs;
        _signatureLabels = signatureLabels;
        _values = values;
    }
    
    public static GameEventScriptNamedArguments Empty { get; } = new(
        Array.Empty<KeyValuePair<string, GameEventScriptValue>>(),
        Array.Empty<string>(),
        new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal));

    public static GameEventScriptNamedArguments Create(IReadOnlyDictionary<string, GameEventScriptValue>? values)
    {
        if (values is null || values.Count == 0) return Empty;
        var orderedPairs = new KeyValuePair<string, GameEventScriptValue>[values.Count];
        var index = 0;
        foreach (var pair in values)
        {
            orderedPairs[index++] = new KeyValuePair<string, GameEventScriptValue>(pair.Key, pair.Value ?? GameEventScriptValue.Nothing);
        }

        return CreateOrdered(orderedPairs);
    }

    internal static GameEventScriptNamedArguments CreateOrdered(KeyValuePair<string, GameEventScriptValue>[] orderedPairs)
        => CreateOrdered(
            orderedPairs,
            orderedPairs.Select(pair => GameEventScriptMessageSignature.NormalizeParameterName(pair.Key)).ToArray());

    internal static GameEventScriptNamedArguments CreateOrdered(
        KeyValuePair<string, GameEventScriptValue>[] orderedPairs,
        IReadOnlyList<string> signatureLabels)
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
        var normalizedSignatureLabels = new string[signatureLabels.Count];
        for (var index = 0; index < orderedPairs.Length; index++)
        {
            var pair = orderedPairs[index];
            var value = pair.Value ?? GameEventScriptValue.Nothing;
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

            normalizedSignatureLabels[index] = GameEventScriptMessageSignature.NormalizeParameterName(signatureLabels[index]);
            if (!string.Equals(normalizedName, GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal))
            {
                values[normalizedName] = value;
            }
        }

        return new GameEventScriptNamedArguments(orderedPairs, normalizedSignatureLabels, values);
    }

    public GameEventScriptValue this[string key] => _values[key];
    public GameEventScriptValue this[int index] => _orderedPairs[index].Value;
    public IEnumerable<string> Keys => _orderedPairs.Select(pair => pair.Key);
    public IReadOnlyList<string> SignatureLabels => _signatureLabels;
    public IEnumerable<GameEventScriptValue> Values => _orderedPairs.Select(pair => pair.Value);
    public int Count => _orderedPairs.Count;
    public bool ContainsKey(string key) => _values.ContainsKey(key);
    public bool TryGetValue(string key, out GameEventScriptValue value) => _values.TryGetValue(key, out value!);
    public IEnumerator<KeyValuePair<string, GameEventScriptValue>> GetEnumerator() => _orderedPairs.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public override string ToString() => _orderedPairs.Count == 0 ? "" : _orderedPairs.Select(pair => $"{pair.Key}: {pair.Value}").Aggregate((a, b) => a + ", " + b);
}
