#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.BytecodeExecutor;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "ConvertToAutoPropertyWithPrivateSetter")]
[StructLayout(LayoutKind.Explicit, Size = 24)]
public struct VmValue
{
    [Flags]
    public enum VmValueFlags : byte
    {
        None = 0,

        IsTrue = 1 << 0,
        IsFalse = 1 << 1,

        StoragePointer = 1 << 2,
        StorageObject = 1 << 3
    }

    [FieldOffset(16)] internal GameEventScriptBytecodeTypeKind Kind;
    [FieldOffset(18)] internal GameEventScriptBytecodeInstructionUnit Unit;
    [FieldOffset(19)] internal VmValueFlags Flags;
    [FieldOffset(0)] internal long IntegerValue;
    [FieldOffset(0)] internal double FloatValue;
    [FieldOffset(8)] internal object? ObjectValue;

    public bool IsTrue => (Flags & VmValueFlags.IsTrue) != 0;
    public bool IsFalse => (Flags & VmValueFlags.IsFalse) != 0;
    public bool IsNotTrue => (Flags & VmValueFlags.IsTrue) == 0;
    
    public bool IsNothing => Kind is Nothing;
    public bool IsNotNothing => Kind is not Nothing;

    public bool IsUnit(GameEventScriptBytecodeInstructionUnit requiredUnit) => Unit == requiredUnit;
    public bool HasUnit => Unit is not GameEventScriptBytecodeInstructionUnit.UnitNone;
    public bool IsStoragePointer => (Flags & VmValueFlags.StoragePointer) != 0;
    public bool IsStorageObject => (Flags & VmValueFlags.StorageObject) != 0;
    
    public void SetNothing()
    {
        Kind = Nothing;
        Unit = GameEventScriptBytecodeInstructionUnit.UnitNothing;
        Flags = VmValueFlags.None;
        IntegerValue = 0;
        ObjectValue = null;
    }

    public void SetBoolean(bool value)
    {
        Kind = GameEventScriptBytecodeTypeKind.Boolean;
        Flags = value ? VmValueFlags.IsTrue : VmValueFlags.IsFalse;
        Unit = GameEventScriptBytecodeInstructionUnit.UnitNone;
        IntegerValue = value ? 1 : 0;
        ObjectValue = null;
    }

    public void SetInteger(long value, GameEventScriptBytecodeInstructionUnit unit = GameEventScriptBytecodeInstructionUnit.UnitNone)
    {
        Kind = Integer;
        Flags = value != 0 ? VmValueFlags.IsTrue : VmValueFlags.IsFalse;
        Unit = unit;
        IntegerValue = value;
        ObjectValue = null;
    }

    public void SetFloat(double value, GameEventScriptBytecodeInstructionUnit unit = GameEventScriptBytecodeInstructionUnit.UnitNone)
    {
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (double.IsFinite(value) && value is >= long.MinValue and <= long.MaxValue && value == Math.Truncate(value))
        {
            var intValue = (long)value;
            Kind = Integer;
            Flags = intValue != 0 ? VmValueFlags.IsTrue : VmValueFlags.IsFalse;
            IntegerValue = intValue;
        }
        else
        {
            Kind = Float;
            Flags = value != 0 && double.IsFinite(value) && !double.IsNaN(value) ? VmValueFlags.IsTrue : VmValueFlags.IsFalse;
            FloatValue = value;
        }
        Unit = unit;
        ObjectValue = null;
    }

    public void SetPercentage(double ratio)
    {
        if (double.IsFinite(ratio))
        {
            Kind = Percentage;
            Flags = ratio != 0 ? VmValueFlags.IsTrue : VmValueFlags.IsFalse;
        }
        else
        {
            Kind = Float;
            Flags = double.IsNaN(ratio) ? VmValueFlags.None : VmValueFlags.IsTrue;
        }
        Unit = GameEventScriptBytecodeInstructionUnit.UnitNone;
        FloatValue = ratio;
        ObjectValue = null;
    }

    public void SetTextPointer(ushort pointer)
    {
        Kind = Text;
        Flags = VmValueFlags.StoragePointer | VmValueFlags.IsTrue;
        Unit = GameEventScriptBytecodeInstructionUnit.UnitNone;
        IntegerValue = pointer;
        ObjectValue = null;
    }

    public void SetText(string text)
    {
        Kind = Text;
        Flags = VmValueFlags.StorageObject | (text.Length == 0 ? VmValueFlags.IsFalse : VmValueFlags.IsTrue);
        Unit = GameEventScriptBytecodeInstructionUnit.UnitNone;
        IntegerValue = 0;
        ObjectValue = text;
    }

    public void SetTagPointer(ushort pointer)
    {
        Kind = Tag;
        Flags = VmValueFlags.StoragePointer | VmValueFlags.IsTrue;
        Unit = GameEventScriptBytecodeInstructionUnit.UnitNone;
        IntegerValue = pointer;
        ObjectValue = null;
    }

    public void SetTag(string tag)
    {
        Kind = Text;
        Flags = VmValueFlags.StorageObject | (tag.Length == 0 ? VmValueFlags.IsFalse : VmValueFlags.IsTrue);
        Unit = GameEventScriptBytecodeInstructionUnit.UnitNone;
        IntegerValue = 0;
        ObjectValue = tag;
    }

    public void SetObject(GameEventScriptBytecodeTypeKind kind, object value, GameEventScriptBytecodeInstructionUnit unit = GameEventScriptBytecodeInstructionUnit.UnitNone)
    {
        Kind = kind;
        Flags = VmValueFlags.StorageObject | VmValueFlags.IsTrue;
        Unit = unit;
        IntegerValue = 0;
        ObjectValue = value;
    }

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
    
    public double AsNumberValueWithUnit(out GameEventScriptBytecodeInstructionUnit unit)
    {
        unit = Unit;
        return Kind switch
        {
            Float or Percentage => FloatValue,
            Integer => IntegerValue,
            _ => double.NaN,
        };
    }
}
