#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using static StepH.Flow.EventScript.Types.EventScriptValueFactory;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace StepH.Flow.EventScript.Types;

public sealed class EventScriptDiceValue : EventScriptValue
{
    public static readonly EventScriptDiceValue Empty = new([]);
    
    private readonly int[] _rollsDescending;

    private EventScriptDiceValue(int[] rollsDescending)
    {
        _rollsDescending = rollsDescending;
        Rolls = new ReadOnlyCollection<int>(rollsDescending);
    }

    public IReadOnlyList<int> Rolls { get; }

    public override EventScriptValueKind Kind => EventScriptValueKind.Dice;

    public override string AsText() => ToString();

    public override long AsInteger() => (long)Sum();

    public override decimal AsNumber() => Sum();

    public override IReadOnlyList<EventScriptValue> AsList() => CreateReadOnlyList(Rolls.Select(roll => Integer(roll)));

    public override EventScriptDiceValue AsDice() => this;

    public override IEnumerable<EventScriptValue> AsEnumerable() => Rolls.Select(roll => Integer(roll));

    public override bool HasSemanticValue() => Rolls.Count > 0;

    public override bool IsSemanticallyEmpty() => Rolls.Count == 0;

    public override bool Contains(EventScriptValue needle) => AsList().Any(item => item.Equals(needle));

    internal override bool TryConvertToNumber(out EventScriptValue value)
    {
        value = Decimal(Sum());
        return true;
    }

    internal override bool TryConvertToInteger(out EventScriptValue value)
    {
        value = Integer((long)Sum());
        return true;
    }

    internal override bool TryConvertToText(out EventScriptValue value)
    {
        value = Text(ToString());
        return true;
    }

    internal override bool TryConvertToList(out EventScriptValue value)
    {
        value = List(Rolls.Select(roll => Integer(roll)));
        return true;
    }

    internal override bool TryConvertToDice(out EventScriptValue value)
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

    public EventScriptDiceValue KeepHighest(int count)
        => _rollsDescending.Length == 0 || count < 0 || count > _rollsDescending.Length ? Empty : EventScriptDice(_rollsDescending.Take(count));

    public EventScriptDiceValue DropLowest(int count)
        => _rollsDescending.Length == 0 || count < 0 || count > _rollsDescending.Length ? Empty : EventScriptDice(_rollsDescending.Take(_rollsDescending.Length - count));

    public static EventScriptDiceValue EventScriptDice(EventScriptDiceValue? diceValue) => diceValue ?? Empty;

    public static EventScriptDiceValue EventScriptDice(IEnumerable<int>? rolls)
    {
        if (rolls == null) return Empty;
        var values = rolls.ToArray();
        if (values.Length == 0 || values.Any(roll => roll <= 0)) return Empty;
        Array.Sort(values);
        Array.Reverse(values);
        return new EventScriptDiceValue(values);
    }

}
