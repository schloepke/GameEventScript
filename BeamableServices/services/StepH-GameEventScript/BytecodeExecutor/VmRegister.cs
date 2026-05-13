#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using StepH.GameEventScript.Types;
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
    internal GameEventScriptNumericUnit? Unit;

    public bool IsNothing => Kind is Nothing;
    public bool IsBoolean => Kind is Boolean;
    public bool IsTrue => BooleanValue;
    public bool IsFalse => !BooleanValue && Kind is not Nothing;
    public bool IsNotTrue => !BooleanValue;
    public bool IsInteger => Kind is Integer;
    public bool IsFloat => Kind is Float or Integer;
    
    public bool IsPointer => Kind is StringPointer or DataPointer or CodePointer;

    public bool IsStringPointer => Kind is StringPointer;
    public bool IsDataPointer => Kind is DataPointer;
    public bool IsCodePointer => Kind is CodePointer;

    public bool IsUnit(GameEventScriptNumericUnit requiredUnit) => Unit == requiredUnit;
    public bool HasUnit => Unit is not null;

    public void SetNothing()
    {
        Kind = Nothing;
        FloatValue = 0.0;
        IntegerValue = 0;
        BooleanValue = false;
        Unit = null;
    }

    public void SetBoolean(bool value)
    {
        Kind = Boolean;
        FloatValue = value ? 1.0 : 0.0;
        IntegerValue = value ? 1 : 0;
        BooleanValue = value;
        Unit = null;
    }

    public void SetBooleanOrNothing(bool? value)
    {
        if (value is null) SetNothing();
        else SetBoolean(value.Value);
    }

    public void SetInteger(long value, GameEventScriptNumericUnit? unit = null)
    {
        Kind = Integer;
        FloatValue = value;
        IntegerValue = value;
        BooleanValue = value != 0.0;
        Unit = unit;
    }
    
    public void SetIntegerOrNothing(long? value, GameEventScriptNumericUnit? unit = null)
    {
        if (value is null) SetNothing();
        else SetInteger(value.Value, unit);
    }

    public void SetFloat(double value, GameEventScriptNumericUnit? unit = null)
    {
        Kind = Float;
        FloatValue = value;
        IntegerValue = (long)value;
        BooleanValue = value != 0.0;
        Unit = unit;
    }

    public void SetFloatOrNothing(double? value, GameEventScriptNumericUnit? unit = null)
    {
        if (value is null) SetNothing();
        else SetFloat(value.Value);
    }

    public void SetDataPointer(ushort pointer)
    {
        Kind = DataPointer;
        IntegerValue = pointer;
        FloatValue = 0;
        BooleanValue = true;
        Unit = null;
    }

    public void SetStringPointer(ushort pointer)
    {
        Kind = StringPointer;
        IntegerValue = pointer;
        FloatValue = 0;
        BooleanValue = true;
        Unit = null;
    }

    public void SetTagPointer(ushort pointer)
    {
        Kind = TagPointer;
        IntegerValue = pointer;
        FloatValue = 0;
        BooleanValue = true;
        Unit = null;
    }

    public void SetCodePointer(ushort pointer)
    {
        Kind = CodePointer;
        IntegerValue = pointer;
        FloatValue = 0;
        BooleanValue = true;
        Unit = null;
    }
    
    public ushort? CodePointerOrNothing => IsCodePointer ? (ushort)IntegerValue : null;
    public long? IntegerValueOrNothing => IsInteger ? IntegerValue : null;
    public double? FloatValueOrNothing => IsFloat ? FloatValue : null;
    public bool? BooleanValueOrNothing => IsBoolean ? BooleanValue : null;

    public long AsIntegerValue => IntegerValue;
    public double AsFloatValue => FloatValue;
    public bool AsBooleanValue => BooleanValue;
    
    public VmValueKind ValueKind => Kind;
    
}
