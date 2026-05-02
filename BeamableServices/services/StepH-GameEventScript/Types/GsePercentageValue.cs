#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using static StepH.GameEventScript.Types.GseValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class GsePercentageValue : GseValue
{
    public static GsePercentageValue GsePercentage(decimal ratio) => new(ratio);

    private GsePercentageValue(decimal ratio)
    {
        Ratio = ratio;
    }

    public decimal Ratio { get; }
    public override GseValueKind Kind => GseValueKind.Percentage;

    public override string AsText() => FormatPercentage(Ratio);

    public override bool AsBoolean() => Ratio != 0m;

    public override long AsInteger() => ToIntegerPercentage(Ratio);

    public override decimal AsNumber() => Ratio;

    internal override bool TryConvertToNumber(out GseValue value)
    {
        value = Decimal(Ratio);
        return true;
    }

    internal override bool TryConvertToInteger(out GseValue value)
    {
        value = Integer(ToIntegerPercentage(Ratio));
        return true;
    }

    internal override bool TryConvertToBoolean(out GseValue value)
    {
        value = Boolean(Ratio != 0m);
        return true;
    }

    internal override bool TryConvertToText(out GseValue value)
    {
        value = Text(FormatPercentage(Ratio));
        return true;
    }

}
