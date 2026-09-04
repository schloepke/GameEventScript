namespace StepH.GameEventScript.Api;

/// <summary>
/// Represents a game event script integer range.
/// </summary>
public sealed class GameEventScriptIntegerRange(long from, long to, long step)
{
    /// <summary>
    /// Gets the from.
    /// </summary>
    public long From { get; } = from;

    /// <summary>
    /// Gets the to.
    /// </summary>
    public long To { get; } = to;

    /// <summary>
    /// Gets the step.
    /// </summary>
    public long Step { get; } = step;
}

/// <summary>
/// Represents a game event script float range.
/// </summary>
public sealed class GameEventScriptFloatRange(double from, double to, double step)
{
    /// <summary>
    /// Gets the from.
    /// </summary>
    public double From { get; } = from;

    /// <summary>
    /// Gets the to.
    /// </summary>
    public double To { get; } = to;

    /// <summary>
    /// Gets the step.
    /// </summary>
    public double Step { get; } = step;
}
