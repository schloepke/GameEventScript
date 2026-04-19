#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace StepH.Flow.EventScript.Types;

internal sealed class EventScriptDictionaryValue : EventScriptValue
{
    private readonly ReadOnlyDictionary<string, EventScriptValue> _visibleView;

    public EventScriptDictionaryValue(Dictionary<string, EventScriptValue> storage)
    {
        Storage = storage;
        var visible = new Dictionary<string, EventScriptValue>(
            storage.Where(pair => !pair.Key.StartsWith("__", StringComparison.Ordinal)),
            StringComparer.Ordinal);
        _visibleView = new ReadOnlyDictionary<string, EventScriptValue>(visible);
    }

    public Dictionary<string, EventScriptValue> Storage { get; }
    public ReadOnlyDictionary<string, EventScriptValue> VisibleView => _visibleView;
    public override EventScriptValueType Type => EventScriptValueType.Dictionary;

    public static EventScriptDictionaryValue Create(IReadOnlyDictionary<string, EventScriptValue>? values)
    {
        var map = new Dictionary<string, EventScriptValue>(StringComparer.Ordinal);
        foreach (var pair in values ?? new Dictionary<string, EventScriptValue>(StringComparer.Ordinal))
        {
            if (pair.Key == null) continue;
            map[pair.Key] = pair.Value ?? EventScriptValue.Nothing;
        }

        return new EventScriptDictionaryValue(map);
    }
}

internal static class EmptyDictionaryView
{
    public static readonly IReadOnlyDictionary<string, EventScriptValue> Instance =
        new ReadOnlyDictionary<string, EventScriptValue>(new Dictionary<string, EventScriptValue>(StringComparer.Ordinal));
}
