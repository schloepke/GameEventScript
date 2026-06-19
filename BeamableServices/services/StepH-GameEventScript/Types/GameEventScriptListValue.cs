#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class GameEventScriptListValue : GameEventScriptValue
{
    private static readonly ReadOnlyCollection<GameEventScriptValue> EmptyItems = new(Array.Empty<GameEventScriptValue>());

    public static readonly GameEventScriptListValue Empty = new(EmptyItems);

    public static GameEventScriptListValue Create(IEnumerable<GameEventScriptValue>? values)
    {
        if (values == null) return Empty;
        var list = values.Select(value => value ?? GameEventScriptNothingValue.Instance).ToArray();
        return list.Length == 0 ? Empty : new GameEventScriptListValue(new ReadOnlyCollection<GameEventScriptValue>(list));
    }

    private GameEventScriptListValue(ReadOnlyCollection<GameEventScriptValue> items)
    {
        Items = items;
    }

    internal ReadOnlyCollection<GameEventScriptValue> Items { get; }
    public override GameEventScriptBytecodeTypeKind Kind => GameEventScriptBytecodeTypeKind.List;

    public override IReadOnlyList<GameEventScriptValue> AsList() => Items;

    public override GameEventScriptDiceValue AsDice() => TryConvertToDice(out var value) ? value.AsDice() : GameEventScriptDiceValue.Empty;

    public override IEnumerable<GameEventScriptValue> AsEnumerable() => Items;

    public override bool HasSemanticValue() => Items.Count > 0;

    public override bool IsSemanticallyEmpty() => Items.Count == 0;

    public override bool Contains(GameEventScriptValue needle) => Items.Any(item => item.Equals(needle));

    internal override bool TryConvertToList(out GameEventScriptValue value)
    {
        value = this;
        return true;
    }

    internal override bool TryConvertToDice(out GameEventScriptValue value) => TryConvertSequenceToDice(Items, out value);

}
