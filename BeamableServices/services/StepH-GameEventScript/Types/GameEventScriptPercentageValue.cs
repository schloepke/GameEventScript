#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using static StepH.GameEventScript.Api.GameEventScriptValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class GameEventScriptPercentageValue : GameEventScriptValue
{
    public static GameEventScriptPercentageValue Create(double ratio) => new(ratio);

    private GameEventScriptPercentageValue(double ratio)
    {
        Ratio = ratio;
    }

    public double Ratio { get; }
    public override GameEventScriptValueKind Kind => GameEventScriptValueKind.Percentage;

    public override string AsText() => FormatPercentage(Ratio);

    public override bool AsBoolean() => Ratio != 0d;

    public override long AsInteger() => ToIntegerPercentage(Ratio);

    public override double AsNumber() => Ratio;

    internal override bool TryConvertToNumber(out GameEventScriptValue value)
    {
        value = GesFloat(Ratio);
        return true;
    }

    internal override bool TryConvertToInteger(out GameEventScriptValue value)
    {
        value = GesInteger(ToIntegerPercentage(Ratio));
        return true;
    }

    internal override bool TryConvertToBoolean(out GameEventScriptValue value)
    {
        value = GesBoolean(Ratio != 0d);
        return true;
    }

    internal override bool TryConvertToText(out GameEventScriptValue value)
    {
        value = GesText(FormatPercentage(Ratio));
        return true;
    }

}
