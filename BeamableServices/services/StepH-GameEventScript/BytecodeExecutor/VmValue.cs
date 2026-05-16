#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.BytecodeExecutor.VmValue.VmValueKind;

namespace StepH.GameEventScript.BytecodeExecutor;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "ConvertToAutoPropertyWithPrivateSetter")]
[StructLayout(LayoutKind.Explicit, Size = 12)]
public struct VmValue
{
    public enum VmValueKind : byte
    {
        Nothing,
        Boolean,
        Integer,
        Float,
        Percentage,
        Dice,
        StringPointer,
        TagPointer,
        DataPointer,
        CodePointer,
        TextObject,
        ListObject,
        DictionaryObject,
        SetObject,
        TagObject,
        DiceObject,
        Iterator
    }

    [Flags]
    public enum VmValueFlags : byte
    {
        None = 0,

        IsTrue = 1 << 0,
        IsFalse = 1 << 1,

        StoragePointer = 1 << 2,
        StorageObject = 1 << 3,
    }
    
    [FieldOffset(16)]
    internal VmValueKind Kind;
    [FieldOffset(17)]
    internal GameEventScriptBytecodeInstructionUnit Unit;
    [FieldOffset(18)]
    internal VmValueFlags Flags;
    [FieldOffset(0)]
    internal long IntegerValue;
    [FieldOffset(0)]
    internal double FloatValue;
    [FieldOffset(8)]
    internal IVmObject? ObjectValue;

    public bool IsTrue => IntegerValue != 0;
    public bool IsFalse => IntegerValue == 0 && Kind is not Nothing;
    public bool IsNotTrue => IntegerValue != 0;

    public bool IsUnit(GameEventScriptBytecodeInstructionUnit requiredUnit) => Unit == requiredUnit;
    public bool HasUnit => Unit is not GameEventScriptBytecodeInstructionUnit.UnitNone;

    public void SetNothing()
    {
        Kind = Nothing;
        Unit = GameEventScriptBytecodeInstructionUnit.UnitNothing;
        IntegerValue = 0;
        ObjectValue = null;
    }

    public void SetBoolean(bool value)
    {
        Kind = VmValueKind.Boolean;
        Unit = GameEventScriptBytecodeInstructionUnit.UnitNone;
        IntegerValue = value ? 1 : 0;
        ObjectValue = null;
    }

    public void SetBooleanOrNothing(bool? value)
    {
        if (value is null) SetNothing();
        else SetBoolean(value.Value);
    }

    public void SetInteger(long value, GameEventScriptBytecodeInstructionUnit unit = GameEventScriptBytecodeInstructionUnit.UnitNone)
    {
        Kind = Integer;
        Unit = unit;
        IntegerValue = value;
        ObjectValue = null;
    }

    public void SetIntegerOrNothing(long? value, GameEventScriptBytecodeInstructionUnit unit = GameEventScriptBytecodeInstructionUnit.UnitNone)
    {
        if (value is null) SetNothing();
        else SetInteger(value.Value, unit);
    }

    public void SetFloat(double value, GameEventScriptBytecodeInstructionUnit unit = GameEventScriptBytecodeInstructionUnit.UnitNone)
    {
        Kind = Float;
        Unit = unit;
        FloatValue = value;
        ObjectValue = null;
    }

    public void SetPercentage(double ratio)
    {
        Kind = Percentage;
        Unit = GameEventScriptBytecodeInstructionUnit.UnitNone;
        FloatValue = ratio;
        ObjectValue = null;
    }

    public void SetFloatOrNothing(double? value, GameEventScriptBytecodeInstructionUnit unit = GameEventScriptBytecodeInstructionUnit.UnitNone)
    {
        if (value is null) SetNothing();
        else SetFloat(value.Value, unit);
    }

    public void SetDataPointer(ushort pointer)
    {
        Kind = DataPointer;
        Unit = GameEventScriptBytecodeInstructionUnit.UnitNone;
        IntegerValue = pointer;
        FloatValue = 0;
        ObjectValue = null;
    }

    public void SetStringPointer(ushort pointer)
    {
        Kind = StringPointer;
        Unit = GameEventScriptBytecodeInstructionUnit.UnitNone;
        IntegerValue = pointer;
        ObjectValue = null;
    }

    public void SetTagPointer(ushort pointer)
    {
        Kind = TagPointer;
        Unit = GameEventScriptBytecodeInstructionUnit.UnitNone;
        IntegerValue = pointer;
        ObjectValue = null;
    }

    public void SetCodePointer(ushort pointer)
    {
        Kind = CodePointer;
        Unit = GameEventScriptBytecodeInstructionUnit.UnitNone;
        IntegerValue = pointer;
        ObjectValue = null;
    }
    
    public void SetObject(VmValueKind kind, IVmObject value)
    {
        Kind = kind;
        Unit = GameEventScriptBytecodeInstructionUnit.UnitNone;
        IntegerValue = 0;
        ObjectValue = value;
    }

    public ushort? CodePointerOrNothing => Kind is CodePointer ? (ushort)IntegerValue : null;
    public long? IntegerValueOrNothing => Kind is Integer ? IntegerValue : null;
    public double? FloatValueOrNothing => Kind is Float or Integer or Percentage ? FloatValue : null;
    public bool? BooleanValueOrNothing => Kind is VmValueKind.Boolean ? IntegerValue != 0 : null;

    public long AsIntegerValue => IntegerValue;
    public double AsFloatValue => FloatValue;
    public bool AsBooleanValue => IntegerValue != 0;

    public double AsNumberValue => Kind switch
    {
        Float => FloatValue,
        Integer => IntegerValue,
        _ => double.NaN,
    };
    
}

public interface IVmObject
{
}

public interface IVmValueObject : IVmObject
{
    int Length { get; }
}

public class VmTextObject(string text) : IVmValueObject
{
    internal string Text = text;
    public int Length { get; } = text.Length;
}

public class VmListObject(IReadOnlyList<VmValue> items) : IVmValueObject
{
    internal IReadOnlyList<VmValue> Items = items;
    public int Length { get; } = items.Count;
}

public class VmDictionaryObject(IReadOnlyDictionary<string, VmValue> entries) : IVmValueObject
{
    internal IReadOnlyDictionary<string, VmValue> Entries = entries;
    public int Length { get; } = entries.Count;
}

public interface IVmIterator : IVmObject
{
    public bool HasNext();
    public bool TryNext(ref VmValue value);
}

public class VmIntegerRangeIterator(long from, long to, long step) : IVmIterator
{
    private long current = from;
    private long end = to;
    private long step = step;

    public bool HasNext() => step switch
    {
        > 0 => current <= end,
        < 0 => current >= end,
        _ => false
    };

    public bool TryNext(ref VmValue value)
    {
        if (!HasNext()) return false;
        value.SetInteger(current);
        current += step;
        return true;
    }
}
