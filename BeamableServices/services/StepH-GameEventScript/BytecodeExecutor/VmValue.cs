#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeInstructionUnit;
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
    public bool IsTruthDeterminate => (Flags & (VmValueFlags.IsTrue | VmValueFlags.IsFalse)) != 0;
    public bool IsTruthIndeterminate => (Flags & (VmValueFlags.IsTrue | VmValueFlags.IsFalse)) == 0;

    public bool IsNothing => Kind is Nothing;
    public bool IsNotNothing => Kind is not Nothing;

    public bool IsUnit(GameEventScriptBytecodeInstructionUnit requiredUnit) => Unit == requiredUnit;
    public bool HasUnit => Unit.IsNumericUnit();
    public bool IsStoragePointer => (Flags & VmValueFlags.StoragePointer) != 0;
    public bool IsStorageObject => (Flags & VmValueFlags.StorageObject) != 0;

    public void UpdatedTextTruthinessCache(ref GameEventScriptTextTable textTable)
    {
        if (IsTruthIndeterminate)
        {
            switch (Kind)
            {
                case Text:
                    var resolvedText = IsStoragePointer ? textTable.Resolve((ushort)IntegerValue) : ObjectValue as string ?? string.Empty;
                    if (resolvedText.Equals("true", StringComparison.OrdinalIgnoreCase) || resolvedText == "1") Flags |= VmValueFlags.IsTrue;
                    else Flags |= VmValueFlags.IsFalse;
                    break;
                case Tag:
                    if ((IsStoragePointer ? textTable.Resolve((ushort)IntegerValue) : ObjectValue as string ?? string.Empty) switch
                        {
                            "true" => true,
                            "infinity" => true,
                            "negativeinfinity" => true,
                            "pi" => true,
                            "e" => true,
                            "tau" => true,
                            "phi" => true,
                            _ => false
                        })
                    {
                        Flags |= VmValueFlags.IsTrue;
                    }
                    else
                    {
                        Flags |= VmValueFlags.IsFalse;
                    }

                    break;
            }
        }
    }

    public void SetNothing()
    {
        Kind = Nothing;
        Unit = UnitNothing;
        Flags = VmValueFlags.None;
        IntegerValue = 0;
        ObjectValue = null;
    }

    public void SetBoolean(bool value)
    {
        Kind = GameEventScriptBytecodeTypeKind.Boolean;
        Flags = value ? VmValueFlags.IsTrue : VmValueFlags.IsFalse;
        Unit = UnitNone;
        IntegerValue = value ? 1 : 0;
        ObjectValue = null;
    }

    public void SetInteger(long value, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
    {
        Kind = Integer;
        Flags = value != 0 ? VmValueFlags.IsTrue : VmValueFlags.IsFalse;
        Unit = unit;
        IntegerValue = value;
        ObjectValue = null;
    }

    public void SetFloat(double value, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
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

        Unit = UnitNone;
        FloatValue = ratio;
        ObjectValue = null;
    }

    public void SetTextPointer(ushort pointer)
    {
        Kind = Text;
        Flags = VmValueFlags.StoragePointer;
        Unit = UnitNone;
        IntegerValue = pointer;
        ObjectValue = null;
    }

    public void SetText(string text)
    {
        Kind = Text;
        Flags = VmValueFlags.StorageObject;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = text;
    }

    public void SetTagPointer(ushort pointer)
    {
        Kind = Tag;
        Flags = VmValueFlags.StoragePointer;
        Unit = UnitNone;
        IntegerValue = pointer;
        ObjectValue = null;
    }

    public void SetTag(string tag)
    {
        Kind = Tag;
        Flags = VmValueFlags.StorageObject;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = tag;
    }

    public void SetVector(double x, double y, double z, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
    {
        Kind = Vector;
        Flags = VmValueFlags.StorageObject | (x is 0 or double.NaN && y is 0 or double.NaN && z is 0 or double.NaN ? VmValueFlags.IsFalse : VmValueFlags.IsTrue);
        Unit = unit;
        IntegerValue = 0;
        ObjectValue = new VmFloatTriplet(x, y, z);
    }

    internal void SetVector(VmFloatTriplet vector, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
    {
        Kind = Vector;
        Flags = VmValueFlags.StorageObject | (vector.X is 0 or double.NaN && vector.Y is 0 or double.NaN && vector.Z is 0 or double.NaN ? VmValueFlags.IsFalse : VmValueFlags.IsTrue);
        Unit = unit;
        IntegerValue = 0;
        ObjectValue = vector;
    }

    public void SetPoint(double x, double y, double z, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
    {
        Kind = Point;
        Flags = VmValueFlags.StorageObject | (x is 0 or double.NaN && y is 0 or double.NaN && z is 0 or double.NaN ? VmValueFlags.IsFalse : VmValueFlags.IsTrue);
        Unit = unit;
        IntegerValue = 0;
        ObjectValue = new VmFloatTriplet(x, y, z);
    }

    internal void SetPoint(VmFloatTriplet point, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
    {
        Kind = Point;
        Flags = VmValueFlags.StorageObject | (point.X is 0 or double.NaN && point.Y is 0 or double.NaN && point.Z is 0 or double.NaN ? VmValueFlags.IsFalse : VmValueFlags.IsTrue);
        Unit = unit;
        IntegerValue = 0;
        ObjectValue = point;
    }

    public void SetDice(long[] values)
    {
        Kind = Dice;
        Flags = VmValueFlags.StorageObject;
        Unit = UnitNone;
        IntegerValue = 0;
        Array.Sort(values);
        Array.Reverse(values);
        var list = new VmListObject(values.Length);
        for (var i = 0; i < values.Length; i++) list.Items[i].SetInteger(values[i]);
        ObjectValue = list;
    }

    public void SetMessageHandler(GameEventScriptMessageSignature handler)
    {
        Kind = Handler;
        Flags = VmValueFlags.StorageObject;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = handler;
    }

    public void SetMessage(GameEventScriptMessage message)
    {
        Kind = Message;
        Flags = VmValueFlags.StorageObject;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = message;
    }

    internal void SetStream(IVmStream value)
    {
        Kind = Stream;
        Flags = VmValueFlags.StorageObject;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = value;
    }

    internal void SetList(VmListObject list)
    {
        Kind = List;
        Flags = VmValueFlags.StorageObject;
        Unit = UnitNone;
        IntegerValue = list.Length;
        ObjectValue = list;
    }

    internal void CreateListBuilder()
    {
        Kind = List;
        Flags = VmValueFlags.StorageObject;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = new List<VmValue>();
    }

    internal void SetMap(VmMapObject map)
    {
        Kind = Map;
        Flags = VmValueFlags.StorageObject;
        Unit = UnitNone;
        IntegerValue = map.Length;
        ObjectValue = map;
    }

    public bool TryGetInteger(out long intValue)
    {
        switch (Kind)
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
        }
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