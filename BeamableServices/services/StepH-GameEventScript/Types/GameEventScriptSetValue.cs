#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Collections.Generic;
using System.Linq;
using static StepH.GameEventScript.Api.GameEventScriptValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class GameEventScriptSetValue : GameEventScriptValue
{
    public static readonly GameEventScriptSetValue Empty = new(new SortedSet<GameEventScriptValue>(StableComparer));

    public static GameEventScriptSetValue Create(IEnumerable<GameEventScriptValue>? values, IComparer<GameEventScriptValue>? comparer = null)
    {
        if (values == null) return Empty;
        var set = new SortedSet<GameEventScriptValue>(values.Select(value => value ?? Nothing), comparer ?? StableComparer);
        return set.Count == 0 ? Empty : new GameEventScriptSetValue(set);
    }

    private GameEventScriptSetValue(SortedSet<GameEventScriptValue> set)
    {
        Items = set;
    }

    public SortedSet<GameEventScriptValue> Items { get; }
    public override GameEventScriptValueKind Kind => GameEventScriptValueKind.Set;

    public override IReadOnlyList<GameEventScriptValue> AsList() => CreateReadOnlyList(Items);

    public override ISet<GameEventScriptValue> AsSet() => new SortedSet<GameEventScriptValue>(Items, StableComparer);

    public override GameEventScriptDiceValue AsDice() => TryConvertToDice(out var value) ? value.AsDice() : GameEventScriptDiceValue.Empty;

    public override IEnumerable<GameEventScriptValue> AsEnumerable() => Items;

    public override bool HasSemanticValue() => Items.Count > 0;

    public override bool IsSemanticallyEmpty() => Items.Count == 0;

    public override bool Contains(GameEventScriptValue needle) => Items.Any(item => item.Equals(needle));

    internal override bool TryConvertToList(out GameEventScriptValue value)
    {
        value = GesList(AsSet());
        return true;
    }

    internal override bool TryConvertToSet(out GameEventScriptValue value)
    {
        value = this;
        return true;
    }

    internal override bool TryConvertToDice(out GameEventScriptValue value) => TryConvertSequenceToDice(AsSet(), out value);

}
