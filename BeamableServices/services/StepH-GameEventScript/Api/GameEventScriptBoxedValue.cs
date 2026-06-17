#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using StepH.GameEventScript.VirtualMachine;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;
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

    public long Integer => Kind switch
    {
        GameEventScriptBytecodeTypeKind.Integer => _value.IntegerValue,
        GameEventScriptBytecodeTypeKind.Float or GameEventScriptBytecodeTypeKind.Percentage => GesValueOperations.ToIntegerSaturated(_value.FloatValue),
        GameEventScriptBytecodeTypeKind.Boolean => _value.IsTrue ? 1 : 0,
        _ => GesValueOperations.ToIntegerSaturated(_value.AsNumeric)
    };

    public double Number => Kind switch
    {
        GameEventScriptBytecodeTypeKind.Integer => _value.IntegerValue,
        GameEventScriptBytecodeTypeKind.Float or GameEventScriptBytecodeTypeKind.Percentage => _value.FloatValue,
        GameEventScriptBytecodeTypeKind.Boolean => _value.IsTrue ? 1d : 0d,
        _ => _value.AsNumeric
    };

    public bool Boolean => Kind switch
    {
        GameEventScriptBytecodeTypeKind.Boolean => _value.IsTrue,
        GameEventScriptBytecodeTypeKind.Integer => _value.IntegerValue != 0,
        GameEventScriptBytecodeTypeKind.Float or GameEventScriptBytecodeTypeKind.Percentage => _value.FloatValue != 0d,
        GameEventScriptBytecodeTypeKind.Vector or GameEventScriptBytecodeTypeKind.Point when _value.ObjectValue is GesVmValueVectorPoint vector => vector.X != 0d || vector.Y != 0d || vector.Z != 0d,
        _ => _value.IsTrue
    };

    public string Text => _value.TextValue.Length > 0 || Kind is GameEventScriptBytecodeTypeKind.Text or GameEventScriptBytecodeTypeKind.Tag ? _value.TextValue : _value.ConvertToText();

    public double X => _value.ObjectValue is GesVmValueVectorPoint vector ? vector.X : 0d;

    public double Y => _value.ObjectValue is GesVmValueVectorPoint vector ? vector.Y : 0d;

    public double Z => _value.ObjectValue is GesVmValueVectorPoint vector ? vector.Z : 0d;

    public bool IsIntegerNumber => Kind is GameEventScriptBytecodeTypeKind.Integer;

    /// <summary>
    /// Reads the value as a numeric value or <see cref="double.NaN"/> when it is not numeric.
    /// </summary>
    public double AsNumeric() => _value.AsNumeric;

    /// <summary>
    /// Reads the value as text using the VM formatting rules.
    /// </summary>
    public string AsText() => _value.ConvertToText();

    public bool TryGetExternalObject<T>(out T value)
    {
        if (TryGetExternalObject(typeof(T), out var externalObject) &&
            externalObject is T typed)
        {
            value = typed;
            return true;
        }

        value = default!;
        return false;
    }

    public bool TryGetExternalObject(Type objectType, out object value)
    {
        _ = objectType ?? throw new ArgumentNullException(nameof(objectType));
        if (_value.ObjectValue is IGameEventScriptExternalObjectValue externalObject &&
            objectType.IsInstanceOfType(externalObject.Instance))
        {
            value = externalObject.Instance;
            return true;
        }

        value = default!;
        return false;
    }

    internal ref readonly GesVmValue GetVmValue() => ref _value;

    public static GameEventScriptBoxedValue Nothing()
    {
        var value = new GesVmValue();
        return new GameEventScriptBoxedValue(in value);
    }

    public static GameEventScriptBoxedValue FromBoolean(bool boolean)
    {
        var value = new GesVmValue();
        value.SetBoolean(boolean);
        return new GameEventScriptBoxedValue(in value);
    }

    public static GameEventScriptBoxedValue FromInteger(long integer, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
    {
        var value = new GesVmValue();
        value.SetInteger(integer, unit);
        return new GameEventScriptBoxedValue(in value);
    }

    public static GameEventScriptBoxedValue FromFloat(double number, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
    {
        var value = new GesVmValue();
        value.SetFloat(number, unit);
        return new GameEventScriptBoxedValue(in value);
    }

    public static GameEventScriptBoxedValue FromPercentage(double ratio)
    {
        var value = new GesVmValue();
        value.SetPercentage(ratio);
        return new GameEventScriptBoxedValue(in value);
    }

    public static GameEventScriptBoxedValue FromText(string text)
    {
        var value = new GesVmValue();
        value.SetText(text ?? string.Empty);
        return new GameEventScriptBoxedValue(in value);
    }

    public static GameEventScriptBoxedValue FromTag(string tag)
    {
        var value = new GesVmValue();
        value.SetTag(tag ?? string.Empty);
        return new GameEventScriptBoxedValue(in value);
    }

    public static GameEventScriptBoxedValue FromVector(double x, double y = 0d, double z = 0d, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
    {
        var value = new GesVmValue();
        value.SetVector(x, y, z, unit);
        return new GameEventScriptBoxedValue(in value);
    }

    public static GameEventScriptBoxedValue FromPoint(double x, double y = 0d, double z = 0d, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
    {
        var value = new GesVmValue();
        value.SetPoint(x, y, z, unit);
        return new GameEventScriptBoxedValue(in value);
    }

    public static GameEventScriptBoxedValue FromIntegerRange(long from, long to, long step = 1)
    {
        var value = new GesVmValue();
        value.SetRange(from, to, step);
        return new GameEventScriptBoxedValue(in value);
    }

    public static GameEventScriptBoxedValue FromFloatRange(double from, double to, double step = 1d)
    {
        var value = new GesVmValue();
        value.SetRange(from, to, step);
        return new GameEventScriptBoxedValue(in value);
    }

    public static GameEventScriptBoxedValue FromMessage(GameEventScriptMessage message)
    {
        var value = new GesVmValue();
        value.SetMessage(message);
        return new GameEventScriptBoxedValue(in value);
    }

    public static GameEventScriptBoxedValue FromHandler(GameEventScriptMessageSignature signature)
    {
        var value = new GesVmValue();
        value.SetMessageHandler(signature);
        return new GameEventScriptBoxedValue(in value);
    }

    internal static GameEventScriptBoxedValue FromGameEventScriptValue(GameEventScriptValue? source)
    {
        var value = new GesVmValue();
        value.BindArguments(source ?? GameEventScriptNothingValue.Instance);
        return new GameEventScriptBoxedValue(in value);
    }

    internal static GameEventScriptBoxedValue FromVmValue(in GesVmValue value) => new(in value);
}
