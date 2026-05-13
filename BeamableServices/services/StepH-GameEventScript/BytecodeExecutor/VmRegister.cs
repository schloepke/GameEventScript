#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Diagnostics.CodeAnalysis;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.BytecodeExecutor.VmRegister.VmValueKind;

namespace StepH.GameEventScript.BytecodeExecutor;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "ConvertToAutoPropertyWithPrivateSetter")]
public struct VmRegister
{
    public enum VmValueKind
    {
        Nothing,
        Boolean,
        Integer,
        Float,
        StringPointer,
        TagPointer,
        DataPointer,
        CodePointer
    }
    
    internal VmValueKind Kind;
    internal long IntegerValue;
    internal double FloatValue;
    internal bool BooleanValue;
    internal GameEventScriptBytecodeInstructionUnit Unit;

    public bool IsTrue => BooleanValue;
    public bool IsFalse => !BooleanValue && Kind is not Nothing;
    public bool IsNotTrue => !BooleanValue;

    public bool IsUnit(GameEventScriptBytecodeInstructionUnit requiredUnit) => Unit == requiredUnit;
    public bool HasUnit => Unit is not GameEventScriptBytecodeInstructionUnit.UnitNone;

    public void SetNothing()
    {
        Kind = Nothing;
        FloatValue = 0.0;
        IntegerValue = 0;
        BooleanValue = false;
        Unit = GameEventScriptBytecodeInstructionUnit.UnitNone;
    }

    public void SetBoolean(bool value)
    {
        Kind = Boolean;
        FloatValue = value ? 1.0 : 0.0;
        IntegerValue = value ? 1 : 0;
        BooleanValue = value;
        Unit = GameEventScriptBytecodeInstructionUnit.UnitNone;
    }

    public void SetBooleanOrNothing(bool? value)
    {
        if (value is null) SetNothing();
        else SetBoolean(value.Value);
    }

    public void SetInteger(long value, GameEventScriptBytecodeInstructionUnit unit = GameEventScriptBytecodeInstructionUnit.UnitNone)
    {
        Kind = Integer;
        FloatValue = value;
        IntegerValue = value;
        BooleanValue = value != 0.0;
        Unit = unit;
    }
    
    public void SetIntegerOrNothing(long? value, GameEventScriptBytecodeInstructionUnit unit = GameEventScriptBytecodeInstructionUnit.UnitNone)
    {
        if (value is null) SetNothing();
        else SetInteger(value.Value, unit);
    }

    public void SetFloat(double value, GameEventScriptBytecodeInstructionUnit unit = GameEventScriptBytecodeInstructionUnit.UnitNone)
    {
        Kind = Float;
        FloatValue = value;
        IntegerValue = (long)value;
        BooleanValue = value != 0.0;
        Unit = unit;
    }

    public void SetFloatOrNothing(double? value, GameEventScriptBytecodeInstructionUnit unit = GameEventScriptBytecodeInstructionUnit.UnitNone)
    {
        if (value is null) SetNothing();
        else SetFloat(value.Value, unit);
    }

    public void SetDataPointer(ushort pointer)
    {
        Kind = DataPointer;
        IntegerValue = pointer;
        FloatValue = 0;
        BooleanValue = true;
        Unit = GameEventScriptBytecodeInstructionUnit.UnitNone;
    }

    public void SetStringPointer(ushort pointer)
    {
        Kind = StringPointer;
        IntegerValue = pointer;
        FloatValue = 0;
        BooleanValue = true;
        Unit = GameEventScriptBytecodeInstructionUnit.UnitNone;
    }

    public void SetTagPointer(ushort pointer)
    {
        Kind = TagPointer;
        IntegerValue = pointer;
        FloatValue = 0;
        BooleanValue = true;
        Unit = GameEventScriptBytecodeInstructionUnit.UnitNone;
    }

    public void SetCodePointer(ushort pointer)
    {
        Kind = CodePointer;
        IntegerValue = pointer;
        FloatValue = 0;
        BooleanValue = true;
        Unit = GameEventScriptBytecodeInstructionUnit.UnitNone;
    }
    
    public ushort? CodePointerOrNothing => Kind is CodePointer ? (ushort)IntegerValue : null;
    public long? IntegerValueOrNothing => Kind is Integer ? IntegerValue : null;
    public double? FloatValueOrNothing => Kind is Float or Integer ? FloatValue : null;
    public bool? BooleanValueOrNothing => Kind is Boolean ? BooleanValue : null;

    public long AsIntegerValue => IntegerValue;
    public double AsFloatValue => FloatValue;
    public bool AsBooleanValue => BooleanValue;
    
    public VmValueKind ValueKind => Kind;
    
}
