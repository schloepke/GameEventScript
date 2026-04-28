#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using static StepH.Flow.EventScript.Types.EventScriptValueFactory;

namespace StepH.Flow.EventScript.Types;

public sealed class EventScriptDegreeValue : EventScriptValue
{
    public static readonly EventScriptDegreeValue Zero = new(0m);

    public static EventScriptDegreeValue EventScriptDegree(decimal degrees)
    {
        var normalized = Normalize(degrees);
        return normalized == 0m ? Zero : new EventScriptDegreeValue(normalized);
    }

    private EventScriptDegreeValue(decimal degrees)
    {
        Degrees = degrees;
    }

    public decimal Degrees { get; }
    public override EventScriptValueKind Kind => EventScriptValueKind.Degree;

    public override string AsText() => FormatDegree(Degrees);

    public override bool AsBoolean() => Degrees != 0m;

    public override long AsInteger() => ToIntegerSaturated(Degrees);

    public override decimal AsNumber() => Degrees;

    internal override bool TryConvertToNumber(out EventScriptValue value)
    {
        value = Decimal(Degrees);
        return true;
    }

    internal override bool TryConvertToInteger(out EventScriptValue value)
    {
        value = Integer(ToIntegerSaturated(Degrees));
        return true;
    }

    internal override bool TryConvertToBoolean(out EventScriptValue value)
    {
        value = Boolean(Degrees != 0m);
        return true;
    }

    internal override bool TryConvertToText(out EventScriptValue value)
    {
        value = Text(FormatDegree(Degrees));
        return true;
    }

    private static decimal Normalize(decimal degrees)
    {
        var normalized = degrees % 360m;
        if (normalized < 0m)
        {
            normalized += 360m;
        }

        return normalized == 360m ? 0m : normalized;
    }
}
