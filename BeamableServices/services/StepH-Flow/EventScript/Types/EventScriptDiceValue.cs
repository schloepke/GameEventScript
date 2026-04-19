#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace StepH.Flow.EventScript.Types;

public sealed class EventScriptDiceValue : EventScriptValue
{
    private readonly int[] _rollsDescending;
    private readonly IReadOnlyList<int> _rollsView;

    private EventScriptDiceValue(int[] rollsDescending)
    {
        _rollsDescending = rollsDescending;
        _rollsView = new ReadOnlyCollection<int>(rollsDescending);
    }

    public IReadOnlyList<int> Rolls => _rollsView;
    public override EventScriptValueType Type => EventScriptValueType.Dice;

    public decimal Sum()
    {
        decimal sum = 0;
        foreach (var roll in _rollsDescending) sum += roll;
        return sum;
    }

    public EventScriptDiceValue KeepHighest(int count)
        => count < 0 || count > _rollsDescending.Length ? Create(Array.Empty<int>()) : Create(_rollsDescending.Take(count));

    public EventScriptDiceValue DropLowest(int count)
        => count < 0 || count > _rollsDescending.Length ? Create(Array.Empty<int>()) : Create(_rollsDescending.Take(_rollsDescending.Length - count));

    public static EventScriptDiceValue Create(IEnumerable<int> rolls)
    {
        if (rolls == null) return new EventScriptDiceValue([]);
        var values = rolls.ToArray();
        if (values.Any(roll => roll <= 0)) return new EventScriptDiceValue([]);
        Array.Sort(values);
        Array.Reverse(values);
        return new EventScriptDiceValue(values);
    }
}
