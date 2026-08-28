#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace StepH.GameEventScript.Api;

public sealed class GameEventScriptIntegerRange(long from, long to, long step)
{
    public long From { get; } = from;

    public long To { get; } = to;

    public long Step { get; } = step;
}

public sealed class GameEventScriptFloatRange(double from, double to, double step)
{
    public double From { get; } = from;

    public double To { get; } = to;

    public double Step { get; } = step;
}
