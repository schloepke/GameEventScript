#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using static StepH.GameEventScript.Api.GameEventScriptValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class GameEventScriptNothingValue : GameEventScriptValue
{
    public static readonly GameEventScriptNothingValue Instance = new();

    private GameEventScriptNothingValue()
    {
    }

    public override GameEventScriptValueKind Kind => GameEventScriptValueKind.Nothing;

    public override string AsText() => string.Empty;

    public override bool AsBoolean() => false;

    public override long AsInteger() => 0;

    public override double AsNumber() => 0d;

    public override GameEventScriptOptionalValue AsOptional() => GameEventScriptOptionalValue.None;

    public override IReadOnlyList<GameEventScriptValue> AsList() => [];

    public override IReadOnlyDictionary<string, GameEventScriptValue> AsDictionary() => GameEventScriptDictionaryValue.EmptyView;

    public override ISet<GameEventScriptValue> AsSet() => new SortedSet<GameEventScriptValue>(StableComparer);

    public override GameEventScriptDiceValue AsDice() => GameEventScriptDiceValue.Empty;

    public override bool HasSemanticValue() => false;

    public override bool IsSemanticallyEmpty() => true;

    internal override bool TryConvertToNumber(out GameEventScriptValue value)
    {
        value = GesFloat(0d);
        return true;
    }

    internal override bool TryConvertToInteger(out GameEventScriptValue value)
    {
        value = GesInteger(0);
        return true;
    }

    internal override bool TryConvertToBoolean(out GameEventScriptValue value)
    {
        value = GesBoolean(false);
        return true;
    }

    internal override bool TryConvertToText(out GameEventScriptValue value)
    {
        value = GesText(string.Empty);
        return true;
    }

    internal override bool TryConvertToList(out GameEventScriptValue value)
    {
        value = GesList([]);
        return true;
    }

    internal override bool TryConvertToDictionary(out GameEventScriptValue value)
    {
        value = GesDictionary(new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal));
        return true;
    }

    internal override bool TryConvertToSet(out GameEventScriptValue value)
    {
        value = GesSet([]);
        return true;
    }

    internal override bool TryConvertToDice(out GameEventScriptValue value)
    {
        value = GesDice(GameEventScriptDiceValue.Empty);
        return true;
    }
}
