#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using StepH.Flow.EventScript.Types;

namespace StepH.Flow.EventScript.Interpreter;

public sealed class EventScriptNamedArguments : IReadOnlyDictionary<string, EventScriptValue>
{
    private readonly IReadOnlyList<KeyValuePair<string, EventScriptValue>> _orderedPairs;
    private readonly IReadOnlyDictionary<string, EventScriptValue> _values;

    private EventScriptNamedArguments(IReadOnlyList<KeyValuePair<string, EventScriptValue>> orderedPairs)
    {
        _orderedPairs = orderedPairs;
        _values = orderedPairs.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }

    public static EventScriptNamedArguments Empty { get; } = new([]);

    public static EventScriptNamedArguments Create(IReadOnlyDictionary<string, EventScriptValue>? values)
    {
        if (values is null || values.Count == 0)
        {
            return Empty;
        }

        return new EventScriptNamedArguments(
            values.Select(pair => new KeyValuePair<string, EventScriptValue>(pair.Key, pair.Value ?? EventScriptValue.Nothing)).ToArray());
    }

    public EventScriptValue this[string key] => _values[key];

    public EventScriptValue this[int index] => _orderedPairs[index].Value;

    public IEnumerable<string> Keys => _orderedPairs.Select(pair => pair.Key);

    public IEnumerable<EventScriptValue> Values => _orderedPairs.Select(pair => pair.Value);

    public int Count => _orderedPairs.Count;

    public bool ContainsKey(string key) => _values.ContainsKey(key);

    public bool TryGetValue(string key, out EventScriptValue value) => _values.TryGetValue(key, out value!);

    public IEnumerator<KeyValuePair<string, EventScriptValue>> GetEnumerator() => _orderedPairs.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString() => _orderedPairs.Count == 0 ? "" : _orderedPairs.Select(pair => $"{pair.Key}: {pair.Value}").Aggregate((a, b) => a + ", " + b);
}
