#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Collections.Generic;
using System.Linq;
using static StepH.GameEventScript.Types.GseValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class GseSetValue : GseValue
{
    public static readonly GseSetValue Empty = new(new SortedSet<GseValue>(StableComparer));

    public static GseSetValue GseSet(IEnumerable<GseValue>? values, IComparer<GseValue>? comparer = null)
    {
        if (values == null) return Empty;
        var set = new SortedSet<GseValue>(values.Select(value => value ?? Nothing), comparer ?? StableComparer);
        return set.Count == 0 ? Empty : new GseSetValue(set);
    }

    private GseSetValue(SortedSet<GseValue> set)
    {
        Items = set;
    }

    public SortedSet<GseValue> Items { get; }
    public override GseValueKind Kind => GseValueKind.Set;

    public override IReadOnlyList<GseValue> AsList() => CreateReadOnlyList(Items);

    public override ISet<GseValue> AsSet() => new SortedSet<GseValue>(Items, StableComparer);

    public override GseDiceValue AsDice() => TryConvertToDice(out var value) ? value.AsDice() : GseDiceValue.Empty;

    public override IEnumerable<GseValue> AsEnumerable() => Items;

    public override bool HasSemanticValue() => Items.Count > 0;

    public override bool IsSemanticallyEmpty() => Items.Count == 0;

    public override bool Contains(GseValue needle) => Items.Any(item => item.Equals(needle));

    internal override bool TryConvertToList(out GseValue value)
    {
        value = List(AsSet());
        return true;
    }

    internal override bool TryConvertToSet(out GseValue value)
    {
        value = this;
        return true;
    }

    internal override bool TryConvertToDice(out GseValue value) => TryConvertSequenceToDice(AsSet(), out value);

}
