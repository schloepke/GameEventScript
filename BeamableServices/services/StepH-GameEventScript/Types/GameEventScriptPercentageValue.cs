#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using static StepH.GameEventScript.Types.GameEventScriptValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class GameEventScriptPercentageValue : GameEventScriptValue
{
    public static GameEventScriptPercentageValue GameEventScriptPercentage(decimal ratio) => new(ratio);

    private GameEventScriptPercentageValue(decimal ratio)
    {
        Ratio = ratio;
    }

    public decimal Ratio { get; }
    public override GameEventScriptValueKind Kind => GameEventScriptValueKind.Percentage;

    public override string AsText() => FormatPercentage(Ratio);

    public override bool AsBoolean() => Ratio != 0m;

    public override long AsInteger() => ToIntegerPercentage(Ratio);

    public override decimal AsNumber() => Ratio;

    internal override bool TryConvertToNumber(out GameEventScriptValue value)
    {
        value = Decimal(Ratio);
        return true;
    }

    internal override bool TryConvertToInteger(out GameEventScriptValue value)
    {
        value = Integer(ToIntegerPercentage(Ratio));
        return true;
    }

    internal override bool TryConvertToBoolean(out GameEventScriptValue value)
    {
        value = Boolean(Ratio != 0m);
        return true;
    }

    internal override bool TryConvertToText(out GameEventScriptValue value)
    {
        value = Text(FormatPercentage(Ratio));
        return true;
    }

}
