#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime.VM;

namespace StepH.GameEventScript.Runtime.Values;

public readonly struct GesValueArguments
{
    private readonly GesValue[]? _values;
    private readonly GesVmState? _vmState;
    private readonly GameEventScriptUInt16IndexList _registers;

    public static readonly GesValueArguments Empty = new([]);

    public GesValueArguments(GesValue[] values)
    {
        _values = values ?? [];
        _vmState = null;
        _registers = default;
        Length = _values.Length;
    }

    internal GesValueArguments(GesVmState vmState, GameEventScriptUInt16IndexList registers)
    {
        _values = null;
        _vmState = vmState ?? throw new ArgumentNullException(nameof(vmState));
        _registers = registers;
        Length = registers.Length;
    }

    public int Length { get; }

    public ref readonly GesValue this[int index] => ref ValueAt(index);

    public ref readonly GesValue ValueAt(int index)
    {
        if (_vmState is { } vmState)
        {
            return ref vmState.Register(_registers[index]);
        }

        return ref _values![index];
    }

    public GameEventScriptBytecodeTypeKind KindAt(int index) => ValueAt(index).Kind;

    public GameEventScriptBytecodeInstructionUnit UnitAt(int index) => ValueAt(index).Unit;

    public bool HasValueAt(int index) => ValueAt(index).HasValue;

    public bool IsNothingAt(int index) => ValueAt(index).IsNothing;

    public bool IsNumericAt(int index) => ValueAt(index).IsNumeric;

    public long GetAsInteger(int index)
    {
        ref readonly var value = ref ValueAt(index);
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
        ref readonly var value = ref ValueAt(index);
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
        ref readonly var value = ref ValueAt(index);
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
        ref readonly var value = ref ValueAt(index);
        return value.TextValue.Length > 0 || value.Kind is GameEventScriptBytecodeTypeKind.Text or GameEventScriptBytecodeTypeKind.Tag
            ? value.TextValue
            : value.ToText;
    }

    public double GetX(int index) => ValueAt(index).ObjectValue is GesValueVectorPoint vector ? vector.X : 0d;

    public double GetY(int index) => ValueAt(index).ObjectValue is GesValueVectorPoint vector ? vector.Y : 0d;

    public double GetZ(int index) => ValueAt(index).ObjectValue is GesValueVectorPoint vector ? vector.Z : 0d;

    internal ref readonly GesValue VmValueAt(int index) => ref ValueAt(index);

    private static long ToIntegerSaturated(double number)
    {
        if (double.IsNaN(number)) return 0;
        var truncated = Math.Truncate(number);
        if (truncated > long.MaxValue) return long.MaxValue;
        if (truncated < long.MinValue) return long.MinValue;
        return (long)truncated;
    }
}
