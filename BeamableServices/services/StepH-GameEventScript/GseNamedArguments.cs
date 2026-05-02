#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript;

public sealed class GseNamedArguments : IReadOnlyDictionary<string, GseValue>
{
    private readonly IReadOnlyList<KeyValuePair<string, GseValue>> _orderedPairs;
    private readonly IReadOnlyList<string> _signatureLabels;
    private readonly IReadOnlyDictionary<string, GseValue> _values;

    public static IReadOnlyDictionary<string, GseValue> Normalize(IReadOnlyDictionary<string, GseValue>? values)
        => values is null || values.Count == 0 ? Empty : values.ToDictionary(pair => pair.Key, pair => pair.Value ?? GseValue.Nothing, StringComparer.Ordinal);

    private GseNamedArguments(
        IReadOnlyList<KeyValuePair<string, GseValue>> orderedPairs,
        IReadOnlyList<string> signatureLabels,
        IReadOnlyDictionary<string, GseValue> values)
    {
        _orderedPairs = orderedPairs;
        _signatureLabels = signatureLabels;
        _values = values;
    }
    
    public static GseNamedArguments Empty { get; } = new(
        Array.Empty<KeyValuePair<string, GseValue>>(),
        Array.Empty<string>(),
        new Dictionary<string, GseValue>(StringComparer.Ordinal));

    public static GseNamedArguments Create(IReadOnlyDictionary<string, GseValue>? values)
    {
        if (values is null || values.Count == 0) return Empty;
        var orderedPairs = new KeyValuePair<string, GseValue>[values.Count];
        var index = 0;
        foreach (var pair in values)
        {
            orderedPairs[index++] = new KeyValuePair<string, GseValue>(pair.Key, pair.Value ?? GseValue.Nothing);
        }

        return CreateOrdered(orderedPairs);
    }

    internal static GseNamedArguments CreateOrdered(KeyValuePair<string, GseValue>[] orderedPairs)
        => CreateOrdered(
            orderedPairs,
            orderedPairs.Select(pair => GseMessageSignature.NormalizeParameterName(pair.Key)).ToArray());

    internal static GseNamedArguments CreateOrdered(
        KeyValuePair<string, GseValue>[] orderedPairs,
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

        var values = new Dictionary<string, GseValue>(orderedPairs.Length, StringComparer.Ordinal);
        var normalizedSignatureLabels = new string[signatureLabels.Count];
        for (var index = 0; index < orderedPairs.Length; index++)
        {
            var pair = orderedPairs[index];
            var value = pair.Value ?? GseValue.Nothing;
            if (!ReferenceEquals(value, pair.Value))
            {
                pair = new KeyValuePair<string, GseValue>(pair.Key, value);
                orderedPairs[index] = pair;
            }

            var normalizedName = GseMessageSignature.NormalizeParameterName(pair.Key);
            var storageName = string.IsNullOrEmpty(pair.Key) ? normalizedName : pair.Key;
            if (!string.Equals(storageName, pair.Key, StringComparison.Ordinal))
            {
                pair = new KeyValuePair<string, GseValue>(storageName, value);
                orderedPairs[index] = pair;
            }

            normalizedSignatureLabels[index] = GseMessageSignature.NormalizeParameterName(signatureLabels[index]);
            if (!string.Equals(normalizedName, GseMessageSignature.UnlabeledParameterName, StringComparison.Ordinal))
            {
                values[normalizedName] = value;
            }
        }

        return new GseNamedArguments(orderedPairs, normalizedSignatureLabels, values);
    }

    public GseValue this[string key] => _values[key];
    public GseValue this[int index] => _orderedPairs[index].Value;
    public IEnumerable<string> Keys => _orderedPairs.Select(pair => pair.Key);
    public IReadOnlyList<string> SignatureLabels => _signatureLabels;
    public IEnumerable<GseValue> Values => _orderedPairs.Select(pair => pair.Value);
    public int Count => _orderedPairs.Count;
    public bool ContainsKey(string key) => _values.ContainsKey(key);
    public bool TryGetValue(string key, out GseValue value) => _values.TryGetValue(key, out value!);
    public IEnumerator<KeyValuePair<string, GseValue>> GetEnumerator() => _orderedPairs.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public override string ToString() => _orderedPairs.Count == 0 ? "" : _orderedPairs.Select(pair => $"{pair.Key}: {pair.Value}").Aggregate((a, b) => a + ", " + b);
}
