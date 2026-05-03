#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using static StepH.GameEventScript.Types.GameEventScriptValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class GameEventScriptDiceValue : GameEventScriptValue
{
    public static readonly GameEventScriptDiceValue Empty = new([]);
    
    private readonly int[] _rollsDescending;

    private GameEventScriptDiceValue(int[] rollsDescending)
    {
        _rollsDescending = rollsDescending;
        Rolls = new ReadOnlyCollection<int>(rollsDescending);
    }

    public IReadOnlyList<int> Rolls { get; }

    public override GameEventScriptValueKind Kind => GameEventScriptValueKind.Dice;

    public override string AsText() => ToString();

    public override long AsInteger() => (long)Sum();

    public override decimal AsNumber() => Sum();

    public override IReadOnlyList<GameEventScriptValue> AsList() => CreateReadOnlyList(Rolls.Select(roll => Integer(roll)));

    public override GameEventScriptDiceValue AsDice() => this;

    public override IEnumerable<GameEventScriptValue> AsEnumerable() => Rolls.Select(roll => Integer(roll));

    public override bool HasSemanticValue() => Rolls.Count > 0;

    public override bool IsSemanticallyEmpty() => Rolls.Count == 0;

    public override bool Contains(GameEventScriptValue needle) => AsList().Any(item => item.Equals(needle));

    internal override bool TryConvertToNumber(out GameEventScriptValue value)
    {
        value = Decimal(Sum());
        return true;
    }

    internal override bool TryConvertToInteger(out GameEventScriptValue value)
    {
        value = Integer((long)Sum());
        return true;
    }

    internal override bool TryConvertToText(out GameEventScriptValue value)
    {
        value = Text(ToString());
        return true;
    }

    internal override bool TryConvertToList(out GameEventScriptValue value)
    {
        value = List(Rolls.Select(roll => Integer(roll)));
        return true;
    }

    internal override bool TryConvertToDice(out GameEventScriptValue value)
    {
        value = this;
        return true;
    }

    public decimal Sum()
    {
        decimal sum = 0;
        foreach (var roll in _rollsDescending) sum += roll;
        return sum;
    }

    public GameEventScriptDiceValue KeepHighest(int count)
        => _rollsDescending.Length == 0 || count < 0 || count > _rollsDescending.Length ? Empty : GameEventScriptDice(_rollsDescending.Take(count));

    public GameEventScriptDiceValue DropLowest(int count)
        => _rollsDescending.Length == 0 || count < 0 || count > _rollsDescending.Length ? Empty : GameEventScriptDice(_rollsDescending.Take(_rollsDescending.Length - count));

    public static GameEventScriptDiceValue GameEventScriptDice(GameEventScriptDiceValue? diceValue) => diceValue ?? Empty;

    public static GameEventScriptDiceValue GameEventScriptDice(IEnumerable<int>? rolls)
    {
        if (rolls == null) return Empty;
        var values = rolls.ToArray();
        if (values.Length == 0 || values.Any(roll => roll <= 0)) return Empty;
        Array.Sort(values);
        Array.Reverse(values);
        return new GameEventScriptDiceValue(values);
    }

}
