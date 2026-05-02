#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using static StepH.GameEventScript.Types.GseValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class GseBooleanValue : GseValue
{
    public static readonly GseBooleanValue True = new(true);
    public static readonly GseBooleanValue False = new(false);

    public static GseBooleanValue GseBoolean(bool value) => value ? True : False;

   private GseBooleanValue(bool value)
    {
        Value = value;
    }

    public bool Value { get; }
    
    public override GseValueKind Kind => GseValueKind.Boolean;

    public override string AsText() => ToString();

    public override bool AsBoolean() => Value;

    public override long AsInteger() => Value ? 1 : 0;

    public override decimal AsNumber() => Value ? 1m : 0m;

    internal override bool TryConvertToNumber(out GseValue value)
    {
        value = Decimal(Value ? 1m : 0m);
        return true;
    }

    internal override bool TryConvertToInteger(out GseValue value)
    {
        value = Integer(Value ? 1 : 0);
        return true;
    }

    internal override bool TryConvertToBoolean(out GseValue value)
    {
        value = this;
        return true;
    }

    internal override bool TryConvertToText(out GseValue value)
    {
        value = Text(ToString());
        return true;
    }

}
