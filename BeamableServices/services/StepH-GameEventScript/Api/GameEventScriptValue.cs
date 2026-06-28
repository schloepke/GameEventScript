using System;
using System.Collections.Generic;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;
using StepH.GameEventScript.Runtime.Values;

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

/// <summary>
/// External value type backed by the same compact storage used by the virtual machine.
/// </summary>
public sealed class GameEventScriptValue : IEquatable<GameEventScriptValue>
{
    private readonly GesValue _value;

    internal GameEventScriptValue(in GesValue value)
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
    /// Gets whether this value can be interpreted as numeric.
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
        Float or Percentage => ToIntegerSaturated(_value.FloatValue),
        GameEventScriptBytecodeTypeKind.Boolean => _value.IsTrue ? 1 : 0,
        _ => ToIntegerSaturated(_value.AsNumeric)
    };

    public double Number => Kind switch
    {
        GameEventScriptBytecodeTypeKind.Integer => _value.IntegerValue,
        Float or Percentage => _value.FloatValue,
        GameEventScriptBytecodeTypeKind.Boolean => _value.IsTrue ? 1d : 0d,
        _ => _value.AsNumeric
    };

    public bool Boolean => Kind switch
    {
        GameEventScriptBytecodeTypeKind.Boolean => _value.IsTrue,
        GameEventScriptBytecodeTypeKind.Integer => _value.IntegerValue != 0,
        Float or Percentage => _value.FloatValue != 0d,
        Vector or Point when _value.ObjectValue is GesValueVectorPoint vector => vector.X != 0d || vector.Y != 0d || vector.Z != 0d,
        _ => _value.IsTrue
    };

    public string Text => _value.TextValue.Length > 0 || Kind is GameEventScriptBytecodeTypeKind.Text or Tag ? _value.TextValue : _value.ToText;

    public double X => _value.ObjectValue is GesValueVectorPoint vector ? vector.X : 0d;

    public double Y => _value.ObjectValue is GesValueVectorPoint vector ? vector.Y : 0d;

    public double Z => _value.ObjectValue is GesValueVectorPoint vector ? vector.Z : 0d;

    public bool IsIntegerNumber => Kind is GameEventScriptBytecodeTypeKind.Integer;

    public int Length => _value.IntegerValue < 0 ? 0 : _value.IntegerValue > int.MaxValue ? int.MaxValue : (int)_value.IntegerValue;

    public GameEventScriptMessage? Message => _value.ObjectValue as GameEventScriptMessage;

    public GameEventScriptMessageSignature? Handler => _value.ObjectValue as GameEventScriptMessageSignature;

    public bool IsIntegerRange => _value.ObjectValue is GesValueRangeInteger;

    public bool IsFloatRange => _value.ObjectValue is GesValueRangeFloat;

    /// <summary>
    /// Reads the value as a numeric value or <see cref="double.NaN"/> when it is not numeric.
    /// </summary>
    public double AsNumeric() => _value.AsNumeric;

    public long AsInteger() => Integer;

    public double AsNumber() => Number;

    public bool AsBoolean() => Boolean;

    /// <summary>
    /// Reads the value as text using the VM formatting rules.
    /// </summary>
    public string AsText() => _value.ToText;

    public bool IsNumericUnit(GameEventScriptBytecodeInstructionUnit unit) => Unit == unit && unit.IsNumericUnit();

    public IReadOnlyList<GameEventScriptValue> AsList()
    {
        if (_value.ObjectValue is not GesValue[] source)
        {
            return [];
        }

        var list = new GameEventScriptValue[source.Length];
        for (var index = 0; index < source.Length; index++)
        {
            list[index] = GameEventScriptValueFactory.FromVmValue(in source[index]);
        }

        return list;
    }

    public IReadOnlyDictionary<string, GameEventScriptValue> AsMap()
    {
        if (_value.ObjectValue is not GesValueMap map)
        {
            if (_value.ObjectValue is GesCustomObject customObject)
            {
                map = customObject.Map;
            }
            else if (_value.ObjectValue is GesExternalObject externalObject)
            {
                map = externalObject.ToMap();
            }
            else
            {
                return new Dictionary<string, GameEventScriptValue>(0, StringComparer.Ordinal);
            }
        }

        if (map.Length == 0)
        {
            return new Dictionary<string, GameEventScriptValue>(0, StringComparer.Ordinal);
        }

        var result = new Dictionary<string, GameEventScriptValue>(map.Length, StringComparer.Ordinal);
        for (var index = 0; index < map.StorageLength; index++)
        {
            if (!map.IsVisibleAt(index))
            {
                continue;
            }

            var mapValue = map.ValueAt(index);
            result[map.KeyAt(index)] = GameEventScriptValueFactory.FromVmValue(in mapValue);
        }

        return result;
    }

    public IReadOnlyList<int> AsDice()
    {
        if (_value.ObjectValue is not int[] rolls)
        {
            return [];
        }

        var result = new int[rolls.Length];
        Array.Copy(rolls, result, rolls.Length);
        return result;
    }

    public GameEventScriptValue? GetMapValue(string key)
    {
        var map = _value.ObjectValue switch
        {
            GesValueMap vmMap => vmMap,
            GesCustomObject customObject => customObject.Map,
            GesExternalObject externalObject => externalObject.ToMap(),
            _ => null
        };

        if (map is not null && map.Get(key) is { } mapValue && !key.StartsWith("_", StringComparison.Ordinal))
        {
            return GameEventScriptValueFactory.FromVmValue(in mapValue);
        }

        return null;
    }

    public string? CustomTypeName => _value.ObjectValue switch
    {
        GesExternalObject externalObject => externalObject.CustomTypeName,
        GesCustomObject customObject => customObject.TypeName,
        _ => null
    };

    public GameEventScriptIntegerRange? IntegerRange => _value.ObjectValue is GesValueRangeInteger range
        ? new GameEventScriptIntegerRange(range.From, range.To, range.Step)
        : null;

    public GameEventScriptFloatRange? FloatRange => _value.ObjectValue is GesValueRangeFloat range
        ? new GameEventScriptFloatRange(range.From, range.To, range.Step)
        : null;

    public T? GetExternalObject<T>()
        => GetExternalObject(typeof(T)) is T typed ? typed : default;

    public object? GetExternalObject(Type objectType)
    {
        _ = objectType ?? throw new ArgumentNullException(nameof(objectType));
        if (_value.ObjectValue is GesExternalObject externalObject &&
            objectType.IsInstanceOfType(externalObject.Instance))
        {
            return externalObject.Instance;
        }

        return null;
    }

    internal ref readonly GesValue GetVmValue() => ref _value;

    public bool Equals(GameEventScriptValue? other)
        => other is not null && _value.EqualsValue(in other._value);

    public override bool Equals(object? obj)
        => obj is GameEventScriptValue other && Equals(other);

    public override int GetHashCode()
        => _value.GetValueHashCode();

    public override string ToString()
        => _value.ToText;

    private static long ToIntegerSaturated(double number)
    {
        if (double.IsNaN(number)) return 0;
        var truncated = Math.Truncate(number);
        if (truncated > long.MaxValue) return long.MaxValue;
        if (truncated < long.MinValue) return long.MinValue;
        return (long)truncated;
    }
}
