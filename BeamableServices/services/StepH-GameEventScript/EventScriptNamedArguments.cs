#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript;

public sealed class EventScriptNamedArguments : IReadOnlyDictionary<string, EventScriptValue>
{
    private readonly IReadOnlyList<KeyValuePair<string, EventScriptValue>> _orderedPairs;
    private readonly IReadOnlyList<string> _signatureLabels;
    private readonly IReadOnlyDictionary<string, EventScriptValue> _values;

    public static IReadOnlyDictionary<string, EventScriptValue> Normalize(IReadOnlyDictionary<string, EventScriptValue>? values)
        => values is null || values.Count == 0 ? Empty : values.ToDictionary(pair => pair.Key, pair => pair.Value ?? EventScriptValue.Nothing, StringComparer.Ordinal);

    private EventScriptNamedArguments(
        IReadOnlyList<KeyValuePair<string, EventScriptValue>> orderedPairs,
        IReadOnlyList<string> signatureLabels,
        IReadOnlyDictionary<string, EventScriptValue> values)
    {
        _orderedPairs = orderedPairs;
        _signatureLabels = signatureLabels;
        _values = values;
    }
    
    public static EventScriptNamedArguments Empty { get; } = new(
        Array.Empty<KeyValuePair<string, EventScriptValue>>(),
        Array.Empty<string>(),
        new Dictionary<string, EventScriptValue>(StringComparer.Ordinal));

    public static EventScriptNamedArguments Create(IReadOnlyDictionary<string, EventScriptValue>? values)
    {
        if (values is null || values.Count == 0) return Empty;
        var orderedPairs = new KeyValuePair<string, EventScriptValue>[values.Count];
        var index = 0;
        foreach (var pair in values)
        {
            orderedPairs[index++] = new KeyValuePair<string, EventScriptValue>(pair.Key, pair.Value ?? EventScriptValue.Nothing);
        }

        return CreateOrdered(orderedPairs);
    }

    internal static EventScriptNamedArguments CreateOrdered(KeyValuePair<string, EventScriptValue>[] orderedPairs)
        => CreateOrdered(
            orderedPairs,
            orderedPairs.Select(pair => EventScriptMessageSignature.NormalizeParameterName(pair.Key)).ToArray());

    internal static EventScriptNamedArguments CreateOrdered(
        KeyValuePair<string, EventScriptValue>[] orderedPairs,
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

        var values = new Dictionary<string, EventScriptValue>(orderedPairs.Length, StringComparer.Ordinal);
        var normalizedSignatureLabels = new string[signatureLabels.Count];
        for (var index = 0; index < orderedPairs.Length; index++)
        {
            var pair = orderedPairs[index];
            var value = pair.Value ?? EventScriptValue.Nothing;
            if (!ReferenceEquals(value, pair.Value))
            {
                pair = new KeyValuePair<string, EventScriptValue>(pair.Key, value);
                orderedPairs[index] = pair;
            }

            var normalizedName = EventScriptMessageSignature.NormalizeParameterName(pair.Key);
            var storageName = string.IsNullOrEmpty(pair.Key) ? normalizedName : pair.Key;
            if (!string.Equals(storageName, pair.Key, StringComparison.Ordinal))
            {
                pair = new KeyValuePair<string, EventScriptValue>(storageName, value);
                orderedPairs[index] = pair;
            }

            normalizedSignatureLabels[index] = EventScriptMessageSignature.NormalizeParameterName(signatureLabels[index]);
            if (!string.Equals(normalizedName, EventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal))
            {
                values[normalizedName] = value;
            }
        }

        return new EventScriptNamedArguments(orderedPairs, normalizedSignatureLabels, values);
    }

    public EventScriptValue this[string key] => _values[key];
    public EventScriptValue this[int index] => _orderedPairs[index].Value;
    public IEnumerable<string> Keys => _orderedPairs.Select(pair => pair.Key);
    public IReadOnlyList<string> SignatureLabels => _signatureLabels;
    public IEnumerable<EventScriptValue> Values => _orderedPairs.Select(pair => pair.Value);
    public int Count => _orderedPairs.Count;
    public bool ContainsKey(string key) => _values.ContainsKey(key);
    public bool TryGetValue(string key, out EventScriptValue value) => _values.TryGetValue(key, out value!);
    public IEnumerator<KeyValuePair<string, EventScriptValue>> GetEnumerator() => _orderedPairs.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public override string ToString() => _orderedPairs.Count == 0 ? "" : _orderedPairs.Select(pair => $"{pair.Key}: {pair.Value}").Aggregate((a, b) => a + ", " + b);
}
