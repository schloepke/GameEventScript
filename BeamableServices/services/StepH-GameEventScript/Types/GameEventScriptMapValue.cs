#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace StepH.GameEventScript.Types;

public sealed class GameEventScriptMapValue : GameEventScriptValue
{
    public static readonly GameEventScriptMapValue Empty = new(new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal));
    public static IReadOnlyDictionary<string, GameEventScriptValue> EmptyView => Empty.VisibleView;

    public static GameEventScriptMapValue Create(IReadOnlyDictionary<string, GameEventScriptValue>? values)
    {
        if (values == null || values.Count == 0) return Empty;
        var map = new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal);
        foreach (var pair in values)
        {
            if (pair.Key == null) continue;
            map[pair.Key] = pair.Value ?? GameEventScriptNothingValue.Instance;
        }
        return map.Count == 0 ? Empty : new GameEventScriptMapValue(map);
    }

    public static GameEventScriptMapValue Create(string? typeName, IReadOnlyDictionary<string, GameEventScriptValue>? values)
    {
        var map = new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal)
        {
            [HiddenTypeKey] = GameEventScriptTagValue.Create(typeName)
        };
        if (values == null) return new GameEventScriptMapValue(map);
        foreach (var pair in values)
        {
            if (pair.Key == null || IsHiddenKey(pair.Key)) continue;
            map[pair.Key] = pair.Value ?? GameEventScriptNothingValue.Instance;
        }
        return new GameEventScriptMapValue(map);
    }
    
    private GameEventScriptMapValue(Dictionary<string, GameEventScriptValue> storage)
    {
        Storage = storage;
        var visible = new Dictionary<string, GameEventScriptValue>(
            storage.Where(pair => !pair.Key.StartsWith("__", StringComparison.Ordinal)),
            StringComparer.Ordinal);
        VisibleView = new ReadOnlyDictionary<string, GameEventScriptValue>(visible);
    }

    internal Dictionary<string, GameEventScriptValue> Storage { get; }
    public ReadOnlyDictionary<string, GameEventScriptValue> VisibleView { get; }

    public override GameEventScriptValueKind Kind => GameEventScriptValueKind.Map;

    public override IReadOnlyDictionary<string, GameEventScriptValue> AsMap() => VisibleView;

    public override bool HasSemanticValue() => VisibleView.Count > 0;

    public override bool IsSemanticallyEmpty() => VisibleView.Count == 0;

    public override bool Contains(GameEventScriptValue needle) => VisibleView.ContainsKey(needle.AsText());

    public override bool ContainsValue(GameEventScriptValue needle) => VisibleView.Values.Any(value => value.Equals(needle));

    public override IEnumerable<GameEventScriptValue> AsEnumerable() => VisibleView.Values;

    public override bool TryGetMapMember(string key, out GameEventScriptValue value)
    {
        if (!IsHiddenKey(key)) return Storage.TryGetValue(key, out value);
        value = GameEventScriptNothingValue.Instance;
        return false;

    }

    internal override bool TryConvertToMap(out GameEventScriptValue value)
    {
        value = this;
        return true;
    }

}
