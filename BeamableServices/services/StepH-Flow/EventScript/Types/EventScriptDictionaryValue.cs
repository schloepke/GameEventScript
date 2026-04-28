#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace StepH.Flow.EventScript.Types;

public sealed class EventScriptDictionaryValue : EventScriptValue
{
    public static readonly EventScriptDictionaryValue Empty = new(new Dictionary<string, EventScriptValue>(StringComparer.Ordinal));
    public static IReadOnlyDictionary<string, EventScriptValue> EmptyView => Empty.VisibleView;

    public static EventScriptDictionaryValue EventScriptDictionary(IReadOnlyDictionary<string, EventScriptValue>? values)
    {
        if (values == null || values.Count == 0) return Empty;
        var map = new Dictionary<string, EventScriptValue>(StringComparer.Ordinal);
        foreach (var pair in values)
        {
            if (pair.Key == null) continue;
            map[pair.Key] = pair.Value ?? Nothing;
        }
        return map.Count == 0 ? Empty : new EventScriptDictionaryValue(map);
    }

    public static EventScriptDictionaryValue EventScriptCustomType(string? typeName, IReadOnlyDictionary<string, EventScriptValue>? values)
    {
        var map = new Dictionary<string, EventScriptValue>(StringComparer.Ordinal)
        {
            [HiddenTypeKey] = EventScriptTagValue.EventScriptTag(typeName)
        };
        if (values == null) return new EventScriptDictionaryValue(map);
        foreach (var pair in values)
        {
            if (pair.Key == null || IsHiddenKey(pair.Key)) continue;
            map[pair.Key] = pair.Value ?? Nothing;
        }
        return new EventScriptDictionaryValue(map);
    }
    
    private EventScriptDictionaryValue(Dictionary<string, EventScriptValue> storage)
    {
        Storage = storage;
        var visible = new Dictionary<string, EventScriptValue>(
            storage.Where(pair => !pair.Key.StartsWith("__", StringComparison.Ordinal)),
            StringComparer.Ordinal);
        VisibleView = new ReadOnlyDictionary<string, EventScriptValue>(visible);
    }

    internal Dictionary<string, EventScriptValue> Storage { get; }
    public ReadOnlyDictionary<string, EventScriptValue> VisibleView { get; }

    public override EventScriptValueKind Kind => EventScriptValueKind.Dictionary;

    public override IReadOnlyDictionary<string, EventScriptValue> AsDictionary() => VisibleView;

    public override bool TryGetDictionaryMember(string key, out EventScriptValue value)
    {
        if (!IsHiddenKey(key)) return Storage.TryGetValue(key, out value);
        value = Nothing;
        return false;

    }

    internal override bool TryConvertToDictionary(out EventScriptValue value)
    {
        value = this;
        return true;
    }

}
