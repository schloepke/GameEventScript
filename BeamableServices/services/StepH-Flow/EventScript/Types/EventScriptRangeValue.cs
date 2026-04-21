#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace StepH.Flow.EventScript.Types;

internal sealed class EventScriptRangeValue : EventScriptValue
{
    private EventScriptRangeValue(long from, long to, long step)
    {
        From = from;
        To = to;
        Step = step;
    }

    public long From { get; }
    public long To { get; }
    public long Step { get; }

    public override EventScriptValueType Type => EventScriptValueType.Range;

    public static EventScriptRangeValue Create(long from, long to, long step) => new(from, to, step);
}
