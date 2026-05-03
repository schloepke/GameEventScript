#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using static StepH.GameEventScript.Api.GameEventScriptValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class GameEventScriptBooleanValue : GameEventScriptValue
{
    public static readonly GameEventScriptBooleanValue True = new(true);
    public static readonly GameEventScriptBooleanValue False = new(false);

    public static GameEventScriptBooleanValue Create(bool value) => value ? True : False;

   private GameEventScriptBooleanValue(bool value)
    {
        Value = value;
    }

    public bool Value { get; }
    
    public override GameEventScriptValueKind Kind => GameEventScriptValueKind.Boolean;

    public override string AsText() => ToString();

    public override bool AsBoolean() => Value;

    public override long AsInteger() => Value ? 1 : 0;

    public override decimal AsNumber() => Value ? 1m : 0m;

    internal override bool TryConvertToNumber(out GameEventScriptValue value)
    {
        value = GesDecimal(Value ? 1m : 0m);
        return true;
    }

    internal override bool TryConvertToInteger(out GameEventScriptValue value)
    {
        value = GesInteger(Value ? 1 : 0);
        return true;
    }

    internal override bool TryConvertToBoolean(out GameEventScriptValue value)
    {
        value = this;
        return true;
    }

    internal override bool TryConvertToText(out GameEventScriptValue value)
    {
        value = GesText(ToString());
        return true;
    }

}
