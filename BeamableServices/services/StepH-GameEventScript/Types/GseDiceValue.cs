#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using static StepH.GameEventScript.Types.GseValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class GseDiceValue : GseValue
{
    public static readonly GseDiceValue Empty = new([]);
    
    private readonly int[] _rollsDescending;

    private GseDiceValue(int[] rollsDescending)
    {
        _rollsDescending = rollsDescending;
        Rolls = new ReadOnlyCollection<int>(rollsDescending);
    }

    public IReadOnlyList<int> Rolls { get; }

    public override GseValueKind Kind => GseValueKind.Dice;

    public override string AsText() => ToString();

    public override long AsInteger() => (long)Sum();

    public override decimal AsNumber() => Sum();

    public override IReadOnlyList<GseValue> AsList() => CreateReadOnlyList(Rolls.Select(roll => Integer(roll)));

    public override GseDiceValue AsDice() => this;

    public override IEnumerable<GseValue> AsEnumerable() => Rolls.Select(roll => Integer(roll));

    public override bool HasSemanticValue() => Rolls.Count > 0;

    public override bool IsSemanticallyEmpty() => Rolls.Count == 0;

    public override bool Contains(GseValue needle) => AsList().Any(item => item.Equals(needle));

    internal override bool TryConvertToNumber(out GseValue value)
    {
        value = Decimal(Sum());
        return true;
    }

    internal override bool TryConvertToInteger(out GseValue value)
    {
        value = Integer((long)Sum());
        return true;
    }

    internal override bool TryConvertToText(out GseValue value)
    {
        value = Text(ToString());
        return true;
    }

    internal override bool TryConvertToList(out GseValue value)
    {
        value = List(Rolls.Select(roll => Integer(roll)));
        return true;
    }

    internal override bool TryConvertToDice(out GseValue value)
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

    public GseDiceValue KeepHighest(int count)
        => _rollsDescending.Length == 0 || count < 0 || count > _rollsDescending.Length ? Empty : GseDice(_rollsDescending.Take(count));

    public GseDiceValue DropLowest(int count)
        => _rollsDescending.Length == 0 || count < 0 || count > _rollsDescending.Length ? Empty : GseDice(_rollsDescending.Take(_rollsDescending.Length - count));

    public static GseDiceValue GseDice(GseDiceValue? diceValue) => diceValue ?? Empty;

    public static GseDiceValue GseDice(IEnumerable<int>? rolls)
    {
        if (rolls == null) return Empty;
        var values = rolls.ToArray();
        if (values.Length == 0 || values.Any(roll => roll <= 0)) return Empty;
        Array.Sort(values);
        Array.Reverse(values);
        return new GseDiceValue(values);
    }

}
