namespace StepH.GameEventScript.Runtime.Values;

internal sealed class GesValueRangeInteger(long from, long to, long step)
{
    internal readonly long From = from;
    internal readonly long To = to;
    internal readonly long Step = step;
}

internal sealed class GesValueRangeFloat(double from, double to, double step)
{
    internal readonly double From = from;
    internal readonly double To = to;
    internal readonly double Step = step;
}
