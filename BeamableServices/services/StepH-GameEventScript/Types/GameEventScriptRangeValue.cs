#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Collections.Generic;
using static StepH.GameEventScript.Api.GameEventScriptValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class GameEventScriptRangeValue : GameEventScriptValue
{
    public static GameEventScriptRangeValue Create(long from, long to, long step) => new(from, to, step);

    private GameEventScriptRangeValue(long from, long to, long step)
    {
        From = from;
        To = to;
        Step = step;
    }

    public long From { get; }
    public long To { get; }
    public long Step { get; }

    public override GameEventScriptValueKind Kind => GameEventScriptValueKind.Range;

    public override string AsText() => ToString();

    public override IReadOnlyList<GameEventScriptValue> AsList() => CreateReadOnlyList(AsEnumerable());

    public override bool HasSemanticValue() => GetLength() > 0;

    public override bool IsSemanticallyEmpty() => GetLength() == 0;

    public override bool Contains(GameEventScriptValue needle)
    {
        if (!needle.IsNumber())
        {
            return false;
        }

        var value = needle.AsInteger();
        if (!GesInteger(value).Equals(needle))
        {
            return false;
        }

        if (Step == 0)
        {
            return false;
        }

        return GameEventScriptRangeMath.Contains(From, To, Step, value);
    }

    public override IEnumerable<GameEventScriptValue> AsEnumerable()
    {
        switch (Step)
        {
            case 0:
                yield break;
            case > 0:
            {
                var current = From;
                while (current <= To)
                {
                    yield return GesInteger(current);
                    var next = current + Step;
                    if (next <= current)
                    {
                        yield break;
                    }

                    current = next;
                }

                yield break;
            }
        }

        var descendingCurrent = From;
        while (descendingCurrent >= To)
        {
            yield return GesInteger(descendingCurrent);
            var next = descendingCurrent + Step;
            if (next >= descendingCurrent)
            {
                yield break;
            }

            descendingCurrent = next;
        }
    }

    protected override GameEventScriptValue LookupCore(GameEventScriptValue selector)
    {
        if (!GameEventScriptRangeMath.TryGetTerm(From, To, Step, selector.AsInteger(), out var value))
        {
            return GameEventScriptNothingValue.Instance;
        }

        return GesInteger(value);
    }

    private long GetLength() => GameEventScriptRangeMath.GetLength(From, To, Step);

    internal override bool TryConvertToText(out GameEventScriptValue value)
    {
        value = GesText(ToString());
        return true;
    }

    internal override bool TryConvertToList(out GameEventScriptValue value)
    {
        value = GesList(AsEnumerable());
        return true;
    }

}
