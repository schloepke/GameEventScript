#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace StepH.GameEventScript.Types;

public sealed class GameEventScriptDictionaryValue : GameEventScriptValue
{
    public static readonly GameEventScriptDictionaryValue Empty = new(new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal));
    public static IReadOnlyDictionary<string, GameEventScriptValue> EmptyView => Empty.VisibleView;

    public static GameEventScriptDictionaryValue Create(IReadOnlyDictionary<string, GameEventScriptValue>? values)
    {
        if (values == null || values.Count == 0) return Empty;
        var map = new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal);
        foreach (var pair in values)
        {
            if (pair.Key == null) continue;
            map[pair.Key] = pair.Value ?? Nothing;
        }
        return map.Count == 0 ? Empty : new GameEventScriptDictionaryValue(map);
    }

    public static GameEventScriptDictionaryValue Create(string? typeName, IReadOnlyDictionary<string, GameEventScriptValue>? values)
    {
        var map = new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal)
        {
            [HiddenTypeKey] = GameEventScriptTagValue.Create(typeName)
        };
        if (values == null) return new GameEventScriptDictionaryValue(map);
        foreach (var pair in values)
        {
            if (pair.Key == null || IsHiddenKey(pair.Key)) continue;
            map[pair.Key] = pair.Value ?? Nothing;
        }
        return new GameEventScriptDictionaryValue(map);
    }
    
    private GameEventScriptDictionaryValue(Dictionary<string, GameEventScriptValue> storage)
    {
        Storage = storage;
        var visible = new Dictionary<string, GameEventScriptValue>(
            storage.Where(pair => !pair.Key.StartsWith("__", StringComparison.Ordinal)),
            StringComparer.Ordinal);
        VisibleView = new ReadOnlyDictionary<string, GameEventScriptValue>(visible);
    }

    internal Dictionary<string, GameEventScriptValue> Storage { get; }
    public ReadOnlyDictionary<string, GameEventScriptValue> VisibleView { get; }

    public override GameEventScriptValueKind Kind => GameEventScriptValueKind.Dictionary;

    public override IReadOnlyDictionary<string, GameEventScriptValue> AsDictionary() => VisibleView;

    public override bool HasSemanticValue() => VisibleView.Count > 0;

    public override bool IsSemanticallyEmpty() => VisibleView.Count == 0;

    public override bool Contains(GameEventScriptValue needle) => VisibleView.ContainsKey(needle.AsText());

    public override bool ContainsValue(GameEventScriptValue needle) => VisibleView.Values.Any(value => value.Equals(needle));

    public override bool TryGetDictionaryMember(string key, out GameEventScriptValue value)
    {
        if (!IsHiddenKey(key)) return Storage.TryGetValue(key, out value);
        value = Nothing;
        return false;

    }

    internal override bool TryConvertToDictionary(out GameEventScriptValue value)
    {
        value = this;
        return true;
    }

}
