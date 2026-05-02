#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using static StepH.GameEventScript.Types.GseValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class GseListValue : GseValue
{
    private static readonly ReadOnlyCollection<GseValue> EmptyItems = new(Array.Empty<GseValue>());

    public static readonly GseListValue Empty = new(EmptyItems);

    public static GseListValue GseList(IEnumerable<GseValue>? values)
    {
        if (values == null) return Empty;
        var list = values.Select(value => value ?? Nothing).ToArray();
        return list.Length == 0 ? Empty : new GseListValue(new ReadOnlyCollection<GseValue>(list));
    }

    private GseListValue(ReadOnlyCollection<GseValue> items)
    {
        Items = items;
    }

    internal ReadOnlyCollection<GseValue> Items { get; }
    public override GseValueKind Kind => GseValueKind.List;

    public override IReadOnlyList<GseValue> AsList() => Items;

    public override ISet<GseValue> AsSet() => TryConvertToSet(out var value) ? value.AsSet() : new SortedSet<GseValue>(StableComparer);

    public override GseDiceValue AsDice() => TryConvertToDice(out var value) ? value.AsDice() : GseDiceValue.Empty;

    public override IEnumerable<GseValue> AsEnumerable() => Items;

    public override bool HasSemanticValue() => Items.Count > 0;

    public override bool IsSemanticallyEmpty() => Items.Count == 0;

    public override bool Contains(GseValue needle) => Items.Any(item => item.Equals(needle));

    internal override bool TryConvertToList(out GseValue value)
    {
        value = this;
        return true;
    }

    internal override bool TryConvertToSet(out GseValue value)
    {
        value = Set(Items);
        return true;
    }

    internal override bool TryConvertToDice(out GseValue value) => TryConvertSequenceToDice(Items, out value);

}
