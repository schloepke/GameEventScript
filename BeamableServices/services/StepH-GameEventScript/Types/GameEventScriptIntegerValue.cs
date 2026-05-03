#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using static StepH.GameEventScript.Types.GameEventScriptValueFactory;

namespace StepH.GameEventScript.Types;

public sealed class GameEventScriptIntegerValue : GameEventScriptValue
{
    
    public static GameEventScriptIntegerValue GameEventScriptInteger(long value) => new(value);

    private GameEventScriptIntegerValue(long value)
    {
        Value = value;
    }

    public long Value { get; }
    public override GameEventScriptValueKind Kind => GameEventScriptValueKind.Integer;

    public override string AsText() => ToString();

    public override bool AsBoolean() => Value != 0;

    public override long AsInteger() => Value;

    public override decimal AsNumber() => Value;

    internal override bool TryConvertToNumber(out GameEventScriptValue value)
    {
        value = Decimal(Value);
        return true;
    }

    internal override bool TryConvertToInteger(out GameEventScriptValue value)
    {
        value = this;
        return true;
    }

    internal override bool TryConvertToBoolean(out GameEventScriptValue value)
    {
        value = Boolean(Value != 0);
        return true;
    }

    internal override bool TryConvertToText(out GameEventScriptValue value)
    {
        value = Text(ToString());
        return true;
    }

}
