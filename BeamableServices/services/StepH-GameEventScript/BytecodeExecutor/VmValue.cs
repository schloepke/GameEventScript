#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.BytecodeExecutor.VmValue.VmValueKind;

namespace StepH.GameEventScript.BytecodeExecutor;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "ConvertToAutoPropertyWithPrivateSetter")]
[StructLayout(LayoutKind.Explicit, Size = 24)]
public struct VmValue
{
    public enum VmValueKind : byte
    {
        Nothing,
        DataPointer,
        CodePointer,
        Boolean,
        Integer,
        Float,
        Percentage,
        Dice,
        Text,
        List,
        Map,
        Tag,
        Iterator,
        Reference,
        Uuid
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

    [FieldOffset(16)] internal VmValueKind Kind;
    [FieldOffset(17)] internal GameEventScriptBytecodeInstructionUnit Unit;
    [FieldOffset(18)] internal VmValueFlags Flags;
    [FieldOffset(0)] internal long IntegerValue;
    [FieldOffset(0)] internal double FloatValue;
    [FieldOffset(8)] internal object? ObjectValue;

    public bool IsTrue => IntegerValue != 0;
    public bool IsFalse => IntegerValue == 0 && Kind is not Nothing;
    public bool IsNotTrue => IntegerValue != 0;

    public bool IsUnit(GameEventScriptBytecodeInstructionUnit requiredUnit) => Unit == requiredUnit;
    public bool HasUnit => Unit is not GameEventScriptBytecodeInstructionUnit.UnitNone;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsStoragePointer() => (Flags & VmValueFlags.StoragePointer) != 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsStorageObject() => (Flags & VmValueFlags.StoragePointer) != 0;
    
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
        Flags = VmValueFlags.None;
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
        Flags = VmValueFlags.None;
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
        Flags = VmValueFlags.None;
        Unit = unit;
        FloatValue = value;
        ObjectValue = null;
    }

    public void SetPercentage(double ratio)
    {
        Kind = Percentage;
        Flags = VmValueFlags.None;
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
        Flags = VmValueFlags.StoragePointer;
        Unit = GameEventScriptBytecodeInstructionUnit.UnitNone;
        IntegerValue = pointer;
        FloatValue = 0;
        ObjectValue = null;
    }

    public void SetStringPointer(ushort pointer)
    {
        Kind = Text;
        Flags = VmValueFlags.StoragePointer;
        Unit = GameEventScriptBytecodeInstructionUnit.UnitNone;
        IntegerValue = pointer;
        ObjectValue = null;
    }

    public void SetTagPointer(ushort pointer)
    {
        Kind = Tag;
        Flags = VmValueFlags.StoragePointer;
        Unit = GameEventScriptBytecodeInstructionUnit.UnitNone;
        IntegerValue = pointer;
        ObjectValue = null;
    }

    public void SetCodePointer(ushort pointer)
    {
        Kind = CodePointer;
        Flags = VmValueFlags.StoragePointer;
        Unit = GameEventScriptBytecodeInstructionUnit.UnitNone;
        IntegerValue = pointer;
        ObjectValue = null;
    }

    public void SetObject(VmValueKind kind, object value)
    {
        Kind = kind;
        Flags = VmValueFlags.StorageObject;
        Unit = GameEventScriptBytecodeInstructionUnit.UnitNone;
        IntegerValue = 0;
        ObjectValue = value;
    }

    public ushort? CodePointerOrNothing => Kind is CodePointer ? (ushort)IntegerValue : null;
    public long? IntegerValueOrNothing => Kind is Integer ? IntegerValue : null;
    public double? FloatValueOrNothing => Kind is Float or Integer or Percentage ? FloatValue : null;
    public bool? BooleanValueOrNothing => Kind is VmValueKind.Boolean ? IntegerValue != 0 : null;

    public bool AsBooleanValue => IntegerValue != 0;

    public bool TryGetInteger(out long intValue)
    {
        switch(Kind)
        {
            case Integer:
                intValue = IntegerValue;
                return true;
            case Float or Percentage:
                intValue = (long)FloatValue;
                return true;
            default:
                intValue = 0;
                return false;
        };   
    }
    
    public double AsNumberValue => Kind switch
    {
        Float or Percentage => FloatValue,
        Integer => IntegerValue,
        _ => double.NaN,
    };
}
