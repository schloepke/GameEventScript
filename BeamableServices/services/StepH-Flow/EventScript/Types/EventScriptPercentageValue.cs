#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace StepH.Flow.EventScript.Types;

internal sealed class EventScriptPercentageValue : EventScriptValue
{
    private EventScriptPercentageValue(decimal ratio)
    {
        Ratio = ratio;
    }

    public decimal Ratio { get; }
    public override EventScriptValueType Type => EventScriptValueType.Percentage;

    public static EventScriptPercentageValue FromRatio(decimal ratio) => new(ratio);
}
