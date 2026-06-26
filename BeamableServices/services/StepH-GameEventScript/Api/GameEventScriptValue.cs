using System;
using System.Collections.Generic;
using StepH.GameEventScript.Runtime.VM;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;
using StepH.GameEventScript.Runtime.Values;

namespace StepH.GameEventScript.Api;

/// <summary>
/// External value type backed by the same compact storage used by the virtual machine.
/// </summary>
public sealed class GameEventScriptValue : IEquatable<GameEventScriptValue>
{
    private readonly GesVmValue _value;

    internal GameEventScriptValue(in GesVmValue value)
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
        Vector or Point when _value.ObjectValue is GesVmValueVectorPoint vector => vector.X != 0d || vector.Y != 0d || vector.Z != 0d,
        _ => _value.IsTrue
    };

    public string Text => _value.TextValue.Length > 0 || Kind is GameEventScriptBytecodeTypeKind.Text or Tag ? _value.TextValue : _value.ToText;

    public double X => _value.ObjectValue is GesVmValueVectorPoint vector ? vector.X : 0d;

    public double Y => _value.ObjectValue is GesVmValueVectorPoint vector ? vector.Y : 0d;

    public double Z => _value.ObjectValue is GesVmValueVectorPoint vector ? vector.Z : 0d;

    public bool IsIntegerNumber => Kind is GameEventScriptBytecodeTypeKind.Integer;

    public int Length => _value.IntegerValue < 0 ? 0 : _value.IntegerValue > int.MaxValue ? int.MaxValue : (int)_value.IntegerValue;

    public GameEventScriptMessage? Message => _value.ObjectValue as GameEventScriptMessage;

    public GameEventScriptMessageSignature? Handler => _value.ObjectValue as GameEventScriptMessageSignature;

    public bool IsIntegerRange => _value.ObjectValue is GesVmValueRangeInteger;

    public bool IsFloatRange => _value.ObjectValue is GesVmValueRangeFloat;

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
        if (_value.ObjectValue is not GesVmValue[] source)
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
        if (_value.ObjectValue is not GesVmValueMap map)
        {
            if (_value.ObjectValue is GesVmCustomObject customObject)
            {
                map = customObject.Map;
            }
            else if (_value.ObjectValue is GesVmExternalObject externalObject)
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

    public bool TryGetMapValue(string key, out GameEventScriptValue value)
    {
        var map = _value.ObjectValue switch
        {
            GesVmValueMap vmMap => vmMap,
            GesVmCustomObject customObject => customObject.Map,
            GesVmExternalObject externalObject => externalObject.ToMap(),
            _ => null
        };

        if (map is not null && map.TryGet(key, out var mapValue) && !key.StartsWith("_", StringComparison.Ordinal))
        {
            value = GameEventScriptValueFactory.FromVmValue(in mapValue);
            return true;
        }

        value = GameEventScriptValueFactory.GesNothing();
        return false;
    }

    public bool TryGetCustomTypeName(out string typeName)
    {
        if (_value.ObjectValue is GesVmExternalObject externalObject)
        {
            typeName = externalObject.CustomTypeName;
            return true;
        }

        if (_value.ObjectValue is GesVmCustomObject customObject)
        {
            typeName = customObject.TypeName;
            return true;
        }

        typeName = string.Empty;
        return false;
    }

    public bool TryGetIntegerRange(out long from, out long to, out long step)
    {
        if (_value.ObjectValue is GesVmValueRangeInteger range)
        {
            from = range.From;
            to = range.To;
            step = range.Step;
            return true;
        }

        from = 0;
        to = 0;
        step = 0;
        return false;
    }

    public bool TryGetFloatRange(out double from, out double to, out double step)
    {
        if (_value.ObjectValue is GesVmValueRangeFloat range)
        {
            from = range.From;
            to = range.To;
            step = range.Step;
            return true;
        }

        from = 0d;
        to = 0d;
        step = 0d;
        return false;
    }

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
        if (_value.ObjectValue is GesVmExternalObject externalObject &&
            objectType.IsInstanceOfType(externalObject.Instance))
        {
            value = externalObject.Instance;
            return true;
        }

        value = default!;
        return false;
    }

    internal ref readonly GesVmValue GetVmValue() => ref _value;

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
