#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace StepH.GameEventScript.Types;

public sealed class GseDictionaryValue : GseValue
{
    public static readonly GseDictionaryValue Empty = new(new Dictionary<string, GseValue>(StringComparer.Ordinal));
    public static IReadOnlyDictionary<string, GseValue> EmptyView => Empty.VisibleView;

    public static GseDictionaryValue GseDictionary(IReadOnlyDictionary<string, GseValue>? values)
    {
        if (values == null || values.Count == 0) return Empty;
        var map = new Dictionary<string, GseValue>(StringComparer.Ordinal);
        foreach (var pair in values)
        {
            if (pair.Key == null) continue;
            map[pair.Key] = pair.Value ?? Nothing;
        }
        return map.Count == 0 ? Empty : new GseDictionaryValue(map);
    }

    public static GseDictionaryValue GseCustomType(string? typeName, IReadOnlyDictionary<string, GseValue>? values)
    {
        var map = new Dictionary<string, GseValue>(StringComparer.Ordinal)
        {
            [HiddenTypeKey] = GseTagValue.GseTag(typeName)
        };
        if (values == null) return new GseDictionaryValue(map);
        foreach (var pair in values)
        {
            if (pair.Key == null || IsHiddenKey(pair.Key)) continue;
            map[pair.Key] = pair.Value ?? Nothing;
        }
        return new GseDictionaryValue(map);
    }
    
    private GseDictionaryValue(Dictionary<string, GseValue> storage)
    {
        Storage = storage;
        var visible = new Dictionary<string, GseValue>(
            storage.Where(pair => !pair.Key.StartsWith("__", StringComparison.Ordinal)),
            StringComparer.Ordinal);
        VisibleView = new ReadOnlyDictionary<string, GseValue>(visible);
    }

    internal Dictionary<string, GseValue> Storage { get; }
    public ReadOnlyDictionary<string, GseValue> VisibleView { get; }

    public override GseValueKind Kind => GseValueKind.Dictionary;

    public override IReadOnlyDictionary<string, GseValue> AsDictionary() => VisibleView;

    public override bool HasSemanticValue() => VisibleView.Count > 0;

    public override bool IsSemanticallyEmpty() => VisibleView.Count == 0;

    public override bool Contains(GseValue needle) => VisibleView.ContainsKey(needle.AsText());

    public override bool ContainsValue(GseValue needle) => VisibleView.Values.Any(value => value.Equals(needle));

    public override bool TryGetDictionaryMember(string key, out GseValue value)
    {
        if (!IsHiddenKey(key)) return Storage.TryGetValue(key, out value);
        value = Nothing;
        return false;

    }

    internal override bool TryConvertToDictionary(out GseValue value)
    {
        value = this;
        return true;
    }

}
