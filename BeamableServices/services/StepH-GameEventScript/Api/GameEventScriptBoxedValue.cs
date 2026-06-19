#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using StepH.GameEventScript.VirtualMachine;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeInstructionUnit;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.Api;

/// <summary>
/// Migration value type backed by the same compact storage used by the virtual machine.
/// </summary>
public sealed class GameEventScriptBoxedValue : IEquatable<GameEventScriptBoxedValue>
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
    public string AsText() => _value.ConvertToText();

    public bool IsNumericUnit(GameEventScriptBytecodeInstructionUnit unit) => Unit == unit && unit.IsNumericUnit();

    public IReadOnlyList<GameEventScriptBoxedValue> AsList()
    {
        if (_value.ObjectValue is not GesVmValue[] source)
        {
            return [];
        }

        var list = new GameEventScriptBoxedValue[source.Length];
        for (var index = 0; index < source.Length; index++)
        {
            list[index] = FromVmValue(in source[index]);
        }

        return list;
    }

    public IReadOnlyDictionary<string, GameEventScriptBoxedValue> AsMap()
    {
        if (_value.ObjectValue is not GesVmValueMap map)
        {
            if (_value.ObjectValue is GesVmExternalObject externalObject)
            {
                map = externalObject.ToMap();
            }
            else
            {
                return new Dictionary<string, GameEventScriptBoxedValue>(0, StringComparer.Ordinal);
            }
        }

        if (map.Length == 0)
        {
            return new Dictionary<string, GameEventScriptBoxedValue>(0, StringComparer.Ordinal);
        }

        var result = new Dictionary<string, GameEventScriptBoxedValue>(map.Length, StringComparer.Ordinal);
        for (var index = 0; index < map.StorageLength; index++)
        {
            if (!map.IsVisibleAt(index))
            {
                continue;
            }

            var mapValue = map.ValueAt(index);
            result[map.KeyAt(index)] = FromVmValue(in mapValue);
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

    public bool TryGetMapValue(string key, out GameEventScriptBoxedValue value)
    {
        var map = _value.ObjectValue switch
        {
            GesVmValueMap vmMap => vmMap,
            GesVmExternalObject externalObject => externalObject.ToMap(),
            _ => null
        };

        if (map is not null && map.TryGet(key, out var mapValue) && !key.StartsWith("_", StringComparison.Ordinal))
        {
            value = FromVmValue(in mapValue);
            return true;
        }

        value = Nothing();
        return false;
    }

    public bool TryGetCustomTypeName(out string typeName)
    {
        if (_value.ObjectValue is GesVmExternalObject externalObject)
        {
            typeName = externalObject.CustomTypeName;
            return true;
        }

        if (_value.Kind is Custom &&
            _value.ObjectValue is GesVmValueMap map &&
            map.TryGet(GesVmValueMap.HiddenRecordTypeField, out var marker) &&
            marker.Kind is Tag)
        {
            typeName = marker.TextValue;
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

    internal static GameEventScriptBoxedValue FromExternalObject(object instance, GameEventScriptExternalTypeDefinition definition)
    {
        var value = new GesVmValue();
        value.SetExternalCustomType(new GesVmExternalObject(instance, definition));
        return new GameEventScriptBoxedValue(in value);
    }

    public bool Equals(GameEventScriptBoxedValue? other)
        => other is not null && _value.EqualsValue(in other._value);

    public override bool Equals(object? obj)
        => obj is GameEventScriptBoxedValue other && Equals(other);

    public override int GetHashCode()
        => _value.GetValueHashCode();

    public override string ToString()
        => _value.ConvertToText();

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

    public static GameEventScriptBoxedValue FromList(IEnumerable<GameEventScriptBoxedValue>? items)
    {
        var source = items is null ? [] : ToArray(items);
        var list = new GesVmValue[source.Length];
        for (var index = 0; index < source.Length; index++)
        {
            list[index] = source[index].GetVmValue();
        }

        var value = new GesVmValue();
        value.SetList(list);
        return new GameEventScriptBoxedValue(in value);
    }

    public static GameEventScriptBoxedValue FromMap(IEnumerable<KeyValuePair<string, GameEventScriptBoxedValue>>? entries)
    {
        var builder = new GesVmValueMapBuilder();
        if (entries is not null)
        {
            foreach (var entry in entries)
            {
                builder.Set(entry.Key, entry.Value.GetVmValue());
            }
        }

        var value = new GesVmValue();
        value.SetMap(builder.ToMap());
        return new GameEventScriptBoxedValue(in value);
    }

    public static GameEventScriptBoxedValue FromRecord(string typeName, IEnumerable<KeyValuePair<string, GameEventScriptBoxedValue>>? fields)
    {
        var builder = new GesVmValueMapBuilder();
        var marker = new GesVmValue();
        marker.SetTag(typeName ?? string.Empty);
        builder.Set(GesVmValueMap.HiddenRecordTypeField, marker);
        if (fields is not null)
        {
            foreach (var field in fields)
            {
                if (string.Equals(field.Key, GesVmValueMap.HiddenRecordTypeField, StringComparison.Ordinal))
                {
                    continue;
                }

                builder.Set(field.Key, field.Value.GetVmValue());
            }
        }

        var value = new GesVmValue();
        value.SetRecord(builder.ToMap());
        return new GameEventScriptBoxedValue(in value);
    }

    public static GameEventScriptBoxedValue FromDice(IEnumerable<int>? rolls)
    {
        var values = rolls is null ? [] : ToArray(rolls);
        var value = new GesVmValue();
        value.SetDice(values);
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

    internal static GameEventScriptBoxedValue FromSeries(GesVmSeries series)
    {
        var value = new GesVmValue();
        value.SetSeries(series);
        return new GameEventScriptBoxedValue(in value);
    }

    internal static GameEventScriptBoxedValue FromGameEventScriptValue(GameEventScriptValue? source)
    {
        var value = new GesVmValue();
        value.BindArguments(source ?? GameEventScriptNothingValue.Instance);
        return new GameEventScriptBoxedValue(in value);
    }

    internal static GameEventScriptBoxedValue FromVmValue(in GesVmValue value) => new(in value);

    private static GameEventScriptBoxedValue[] ToArray(IEnumerable<GameEventScriptBoxedValue> values)
    {
        if (values is GameEventScriptBoxedValue[] array)
        {
            return array;
        }

        if (values is ICollection<GameEventScriptBoxedValue> collection)
        {
            var result = new GameEventScriptBoxedValue[collection.Count];
            collection.CopyTo(result, 0);
            return result;
        }

        var list = new List<GameEventScriptBoxedValue>();
        foreach (var value in values)
        {
            list.Add(value);
        }

        return list.ToArray();
    }

    private static int[] ToArray(IEnumerable<int> values)
    {
        if (values is int[] array)
        {
            var copy = new int[array.Length];
            Array.Copy(array, copy, array.Length);
            return copy;
        }

        if (values is ICollection<int> collection)
        {
            var result = new int[collection.Count];
            collection.CopyTo(result, 0);
            return result;
        }

        var list = new List<int>();
        foreach (var value in values)
        {
            list.Add(value);
        }

        return list.ToArray();
    }
}
