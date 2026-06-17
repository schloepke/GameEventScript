#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using StepH.GameEventScript.VirtualMachine;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeInstructionUnit;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.Api;

/// <summary>
/// Migration value type backed by the same compact storage used by the virtual machine.
/// </summary>
public sealed class GameEventScriptBoxedValue
{
    private readonly GesVmValue _value;

    private GameEventScriptBoxedValue(in GesVmValue value)
    {
        _value = value;
    }

    /// <summary>
    /// Gets the concrete stored value kind.
    /// </summary>
    public GameEventScriptBytecodeTypeKind Kind => _value.Kind;

    /// <summary>
    /// Gets the numeric unit attached to the value, or <see cref="GameEventScriptBytecodeInstructionUnit.UnitNone"/>.
    /// </summary>
    public GameEventScriptBytecodeInstructionUnit Unit => _value.Unit;

    /// <summary>
    /// Gets whether this value can be interpreted as numeric without boxing through the old value hierarchy.
    /// </summary>
    public bool IsNumeric => _value.IsNumeric;

    /// <summary>
    /// Gets whether this value carries semantic content.
    /// </summary>
    public bool HasValue => _value.HasValue;

    /// <summary>
    /// Gets whether this value is nothing.
    /// </summary>
    public bool IsNothing => _value.IsNothing;

    /// <summary>
    /// Gets whether the value has a numeric unit.
    /// </summary>
    public bool HasUnit => _value.HasUnit;

    /// <summary>
    /// Reads the value as a numeric value or <see cref="double.NaN"/> when it is not numeric.
    /// </summary>
    public double AsNumeric() => _value.AsNumeric;

    /// <summary>
    /// Reads the value as text using the VM formatting rules.
    /// </summary>
    public string AsText() => _value.ConvertToText();

    internal ref readonly GesVmValue GetVmValue() => ref _value;

    public static GameEventScriptBoxedValue Nothing()
    {
        var value = new GesVmValue();
        return new GameEventScriptBoxedValue(in value);
    }

    public static GameEventScriptBoxedValue Boolean(bool boolean)
    {
        var value = new GesVmValue();
        value.SetBoolean(boolean);
        return new GameEventScriptBoxedValue(in value);
    }

    public static GameEventScriptBoxedValue Integer(long integer, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
    {
        var value = new GesVmValue();
        value.SetInteger(integer, unit);
        return new GameEventScriptBoxedValue(in value);
    }

    public static GameEventScriptBoxedValue Float(double number, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
    {
        var value = new GesVmValue();
        value.SetFloat(number, unit);
        return new GameEventScriptBoxedValue(in value);
    }

    public static GameEventScriptBoxedValue Percentage(double ratio)
    {
        var value = new GesVmValue();
        value.SetPercentage(ratio);
        return new GameEventScriptBoxedValue(in value);
    }

    public static GameEventScriptBoxedValue Text(string text)
    {
        var value = new GesVmValue();
        value.SetText(text ?? string.Empty);
        return new GameEventScriptBoxedValue(in value);
    }

    public static GameEventScriptBoxedValue Tag(string tag)
    {
        var value = new GesVmValue();
        value.SetTag(tag ?? string.Empty);
        return new GameEventScriptBoxedValue(in value);
    }

    public static GameEventScriptBoxedValue Vector(double x, double y = 0d, double z = 0d, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
    {
        var value = new GesVmValue();
        value.SetVector(x, y, z, unit);
        return new GameEventScriptBoxedValue(in value);
    }

    public static GameEventScriptBoxedValue Point(double x, double y = 0d, double z = 0d, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
    {
        var value = new GesVmValue();
        value.SetPoint(x, y, z, unit);
        return new GameEventScriptBoxedValue(in value);
    }

    public static GameEventScriptBoxedValue IntegerRange(long from, long to, long step = 1)
    {
        var value = new GesVmValue();
        value.SetRange(from, to, step);
        return new GameEventScriptBoxedValue(in value);
    }

    public static GameEventScriptBoxedValue FloatRange(double from, double to, double step = 1d)
    {
        var value = new GesVmValue();
        value.SetRange(from, to, step);
        return new GameEventScriptBoxedValue(in value);
    }

    public static GameEventScriptBoxedValue Message(GameEventScriptMessage message)
    {
        var value = new GesVmValue();
        value.SetMessage(message);
        return new GameEventScriptBoxedValue(in value);
    }

    public static GameEventScriptBoxedValue Handler(GameEventScriptMessageSignature signature)
    {
        var value = new GesVmValue();
        value.SetMessageHandler(signature);
        return new GameEventScriptBoxedValue(in value);
    }

    internal static GameEventScriptBoxedValue FromVmValue(in GesVmValue value) => new(in value);
}
