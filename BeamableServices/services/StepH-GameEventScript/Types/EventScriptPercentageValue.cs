#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using static StepH.GameEventScript.Types.EventScriptValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class EventScriptPercentageValue : EventScriptValue
{
    public static EventScriptPercentageValue EventScriptPercentage(decimal ratio) => new(ratio);

    private EventScriptPercentageValue(decimal ratio)
    {
        Ratio = ratio;
    }

    public decimal Ratio { get; }
    public override EventScriptValueKind Kind => EventScriptValueKind.Percentage;

    public override string AsText() => FormatPercentage(Ratio);

    public override bool AsBoolean() => Ratio != 0m;

    public override long AsInteger() => ToIntegerPercentage(Ratio);

    public override decimal AsNumber() => Ratio;

    internal override bool TryConvertToNumber(out EventScriptValue value)
    {
        value = Decimal(Ratio);
        return true;
    }

    internal override bool TryConvertToInteger(out EventScriptValue value)
    {
        value = Integer(ToIntegerPercentage(Ratio));
        return true;
    }

    internal override bool TryConvertToBoolean(out EventScriptValue value)
    {
        value = Boolean(Ratio != 0m);
        return true;
    }

    internal override bool TryConvertToText(out EventScriptValue value)
    {
        value = Text(FormatPercentage(Ratio));
        return true;
    }

}
