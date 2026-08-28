#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.Runtime.Values;

public readonly struct GesValueSlice
{
    private readonly GesValue[]? _values;

    public static readonly GesValueSlice Empty = new([], 0, 0);

    public GesValueSlice(GesValue[] values)
    {
        _values = values ?? [];
        Start = 0;
        Length = _values.Length;
    }

    internal GesValueSlice(GesValue[] values, int start, int length)
    {
        _values = values;
        Start = start;
        Length = length;
    }

    public int Start { get; }

    public int Length { get; }

    public ref readonly GesValue this[int index] => ref _values![Start + index];

    public ref readonly GesValue ValueAt(int index) => ref _values![Start + index];

    public GameEventScriptBytecodeTypeKind KindAt(int index) => _values![Start + index].Kind;

    public GameEventScriptBytecodeInstructionUnit UnitAt(int index) => _values![Start + index].Unit;

    public bool HasValueAt(int index) => _values![Start + index].HasValue;

    public bool IsNothingAt(int index) => _values![Start + index].IsNothing;

    public bool IsNumericAt(int index) => _values![Start + index].IsNumeric;

    public long GetAsInteger(int index)
    {
        ref readonly var value = ref _values![Start + index];
        return value.Kind switch
        {
            GameEventScriptBytecodeTypeKind.Integer => value.IntegerValue,
            GameEventScriptBytecodeTypeKind.Float or GameEventScriptBytecodeTypeKind.Percentage => ToIntegerSaturated(value.FloatValue),
            GameEventScriptBytecodeTypeKind.Boolean => value.IsTrue ? 1 : 0,
            _ => ToIntegerSaturated(value.AsNumeric)
        };
    }

    public double GetAsNumber(int index)
    {
        ref readonly var value = ref _values![Start + index];
        return value.Kind switch
        {
            GameEventScriptBytecodeTypeKind.Integer => value.IntegerValue,
            GameEventScriptBytecodeTypeKind.Float or GameEventScriptBytecodeTypeKind.Percentage => value.FloatValue,
            GameEventScriptBytecodeTypeKind.Boolean => value.IsTrue ? 1d : 0d,
            _ => value.AsNumeric
        };
    }

    public bool GetAsBoolean(int index)
    {
        ref readonly var value = ref _values![Start + index];
        return value.Kind switch
        {
            GameEventScriptBytecodeTypeKind.Boolean => value.IsTrue,
            GameEventScriptBytecodeTypeKind.Integer => value.IntegerValue != 0,
            GameEventScriptBytecodeTypeKind.Float or GameEventScriptBytecodeTypeKind.Percentage => value.FloatValue != 0d,
            GameEventScriptBytecodeTypeKind.Vector or GameEventScriptBytecodeTypeKind.Point when value.ObjectValue is GesValueVectorPoint vector => vector.X != 0d || vector.Y != 0d || vector.Z != 0d,
            _ => value.IsTrue
        };
    }

    public string GetAsText(int index)
    {
        ref readonly var value = ref _values![Start + index];
        return value.TextValue.Length > 0 || value.Kind is GameEventScriptBytecodeTypeKind.Text or GameEventScriptBytecodeTypeKind.Tag
            ? value.TextValue
            : value.ToText;
    }

    public double GetX(int index) => _values![Start + index].ObjectValue is GesValueVectorPoint vector ? vector.X : 0d;

    public double GetY(int index) => _values![Start + index].ObjectValue is GesValueVectorPoint vector ? vector.Y : 0d;

    public double GetZ(int index) => _values![Start + index].ObjectValue is GesValueVectorPoint vector ? vector.Z : 0d;

    internal ref readonly GesValue VmValueAt(int index) => ref _values![Start + index];

    private static long ToIntegerSaturated(double number)
    {
        if (double.IsNaN(number)) return 0;
        var truncated = Math.Truncate(number);
        if (truncated > long.MaxValue) return long.MaxValue;
        if (truncated < long.MinValue) return long.MinValue;
        return (long)truncated;
    }
}
