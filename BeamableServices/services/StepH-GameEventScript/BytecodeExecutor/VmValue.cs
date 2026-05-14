#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.BytecodeExecutor.VmValue.VmValueKind;

namespace StepH.GameEventScript.BytecodeExecutor;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "ConvertToAutoPropertyWithPrivateSetter")]
public struct VmValue
{
    public enum VmValueKind
    {
        Nothing,
        Boolean,
        Integer,
        Float,
        Dice,
        StringPointer,
        TagPointer,
        DataPointer,
        CodePointer,
        TextObject,
        ListObject,
        DictionaryObject,
        TagObject,
        DiceObject
    }

    internal VmValueKind Kind;
    internal bool BooleanValue;
    internal long IntegerValue;
    internal double FloatValue;
    internal GameEventScriptBytecodeInstructionUnit Unit;
    internal IVmObject? ObjectValue;

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
    
    public void SetObject(VmValueKind kind, IVmObject value)
    {
        Kind = kind;
        ObjectValue = value;
        FloatValue = 0;
        IntegerValue = 0;
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
}

public interface IVmObject
{
}

public class VmTextObject(string text) : IVmObject
{
    internal string Text = text;
}

public class VmListObject(IReadOnlyList<VmValue> items) : IVmObject
{
    internal IReadOnlyList<VmValue> Items = items;
}

public class VmDictionaryObject(IReadOnlyDictionary<string, VmValue> entries) : IVmObject
{
    internal IReadOnlyDictionary<string, VmValue> Entries = entries;
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