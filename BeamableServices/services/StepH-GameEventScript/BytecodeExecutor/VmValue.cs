#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeInstructionUnit;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;
using static StepH.GameEventScript.BytecodeExecutor.VmMathConstants;
using static StepH.GameEventScript.BytecodeExecutor.VmValue.VmValueFlags;

namespace StepH.GameEventScript.BytecodeExecutor;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "ConvertToAutoPropertyWithPrivateSetter")]
[StructLayout(LayoutKind.Explicit, Size = 32)]
internal struct VmValue
{
    [Flags]
    internal enum VmValueFlags : byte
    {
        None = 0,

        IsTrueFlag = 1 << 0,
        IsFalseFlag = 1 << 1,
        HasValueFlag = 1 << 2,
        IsNumericFlag = 1 << 3,

        StoragePointerFlag = 1 << 4,
        StorageObjectFlag = 1 << 5
    }

    [FieldOffset(0)] internal long IntegerValue;
    [FieldOffset(0)] internal double FloatValue;
    [FieldOffset(8)] internal object? ObjectValue;

    [FieldOffset(16)] internal GameEventScriptBytecodeTypeKind Kind;
    [FieldOffset(17)] internal GameEventScriptBytecodeInstructionUnit Unit;
    [FieldOffset(18)] internal VmValueFlags Flags;

    [FieldOffset(24)] internal VmState OwningState;

    internal bool IsTrue => (Flags & IsTrueFlag) != 0;
    internal bool IsFalse => (Flags & IsFalseFlag) != 0;
    internal bool IsNotTrue => (Flags & IsTrueFlag) == 0;
    internal bool IsTruthDeterminate => (Flags & (IsTrueFlag | IsFalseFlag)) != 0;
    internal bool IsTruthIndeterminate => (Flags & (IsTrueFlag | IsFalseFlag)) == 0;

    internal bool IsNumeric => (Flags & IsNumericFlag) != 0;
    internal bool HasValue => (Flags & HasValueFlag) != 0;

    internal bool IsNothing => Kind is Nothing || (Kind is Float or Percentage && double.IsNaN(FloatValue));
    internal bool IsNotNothing => Kind is not Nothing && Kind is not Float and not Percentage || Kind is Float or Percentage && !double.IsNaN(FloatValue);

    internal bool IsUnit(GameEventScriptBytecodeInstructionUnit requiredUnit) => Unit == requiredUnit;
    internal bool HasUnit => Unit.IsNumericUnit();
    internal bool IsStoragePointer => (Flags & StoragePointerFlag) != 0;
    internal bool IsStorageObject => (Flags & StorageObjectFlag) != 0;

    internal void InitRegister(VmState state)
    {
        OwningState = state;
        SetNothing();
    }

    internal void UpdatedTextTruthinessCache()
    {
        if (!IsTruthIndeterminate) return;
        switch (Kind)
        {
            case Text:
                var resolvedText = IsStoragePointer ? OwningState.Binary.TextConstantTable.Resolve((ushort)IntegerValue) : ObjectValue as string ?? string.Empty;
                if (resolvedText.Equals("true", StringComparison.OrdinalIgnoreCase) || resolvedText == "1") Flags |= IsTrueFlag;
                else Flags |= IsFalseFlag;
                break;
            case Tag:
                if ((IsStoragePointer ? OwningState.Binary.TextConstantTable.Resolve((ushort)IntegerValue) : ObjectValue as string ?? string.Empty) switch
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
                    Flags |= IsTrueFlag;
                }
                else
                {
                    Flags |= IsFalseFlag;
                }

                break;
        }
    }

    internal void SetNothing()
    {
        Kind = Nothing;
        Unit = UnitNothing;
        Flags = None;
        IntegerValue = 0;
        ObjectValue = null;
    }

    internal void SetBoolean(bool value)
    {
        Kind = GameEventScriptBytecodeTypeKind.Boolean;
        Flags = HasValueFlag | (value ? IsTrueFlag : IsFalseFlag);
        Unit = UnitNone;
        IntegerValue = value ? 1 : 0;
        ObjectValue = null;
    }

    internal void SetInteger(long value, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
    {
        Kind = Integer;
        Flags = IsNumericFlag | HasValueFlag | (value != 0 ? IsTrueFlag : IsFalseFlag);
        Unit = unit;
        IntegerValue = value;
        ObjectValue = null;
    }

    internal void SetFloat(double value, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
    {
        if (double.IsNaN(value))
        {
            SetNothing();
            return;
        }

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (double.IsFinite(value) && value is >= long.MinValue and <= long.MaxValue && value == Math.Truncate(value))
        {
            var intValue = (long)value;
            Kind = Integer;
            Flags = IsNumericFlag | HasValueFlag | (intValue != 0 ? IsTrueFlag : IsFalseFlag);
            IntegerValue = intValue;
        }
        else
        {
            Kind = Float;
            Flags = IsNumericFlag | HasValueFlag | (value != 0 && double.IsFinite(value) ? IsTrueFlag : IsFalseFlag);
            FloatValue = value;
        }

        Unit = unit;
        ObjectValue = null;
    }

    internal void SetPercentage(double ratio)
    {
        if (double.IsFinite(ratio))
        {
            Kind = Percentage;
            Flags = IsNumericFlag | HasValueFlag | (ratio != 0 ? IsTrueFlag : IsFalseFlag);
        }
        else
        {
            Kind = Float;
            Flags = IsNumericFlag | (double.IsNaN(ratio) ? None : IsTrueFlag | HasValueFlag);
        }

        Unit = UnitNone;
        FloatValue = ratio;
        ObjectValue = null;
    }

    internal void SetTextPointer(ushort pointer)
    {
        Kind = Text;
        Flags = StoragePointerFlag | (OwningState.Binary.TextConstantTable.ResolveSize(pointer) == 0 ? None : HasValueFlag);
        Unit = UnitNone;
        IntegerValue = pointer;
        ObjectValue = null;
    }

    internal void SetText(string text)
    {
        Kind = Text;
        Flags = StorageObjectFlag | (text.Length == 0 ? None : HasValueFlag);
        Unit = UnitNone;
        IntegerValue = text.Length;
        ObjectValue = text;
    }

    internal void SetTagPointer(ushort pointer)
    {
        Kind = Tag;
        Flags = StoragePointerFlag | HasValueFlag;
        Unit = UnitNone;
        IntegerValue = pointer;
        ObjectValue = null;
    }

    internal void SetTag(string tag)
    {
        Kind = Tag;
        Flags = StorageObjectFlag | HasValueFlag;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = tag;
    }

    internal void SetVector(double x, double y, double z, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
    {
        Kind = Vector;
        Flags = StorageObjectFlag | HasValueFlag | (x is 0 or double.NaN && y is 0 or double.NaN && z is 0 or double.NaN ? IsFalseFlag : IsTrueFlag);
        Unit = unit;
        IntegerValue = 0;
        ObjectValue = new VmFloatTriplet(x, y, z);
    }

    internal void SetVector(VmFloatTriplet vector, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
    {
        Kind = Vector;
        Flags = StorageObjectFlag | HasValueFlag | (vector.X is 0 or double.NaN && vector.Y is 0 or double.NaN && vector.Z is 0 or double.NaN ? IsFalseFlag : IsTrueFlag);
        Unit = unit;
        IntegerValue = 0;
        ObjectValue = vector;
    }

    internal void SetPoint(double x, double y, double z, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
    {
        Kind = Point;
        Flags = StorageObjectFlag | HasValueFlag | (x is 0 or double.NaN && y is 0 or double.NaN && z is 0 or double.NaN ? IsFalseFlag : IsTrueFlag);
        Unit = unit;
        IntegerValue = 0;
        ObjectValue = new VmFloatTriplet(x, y, z);
    }

    internal void SetPoint(VmFloatTriplet point, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
    {
        Kind = Point;
        Flags = StorageObjectFlag | HasValueFlag | (point.X is 0 or double.NaN && point.Y is 0 or double.NaN && point.Z is 0 or double.NaN ? IsFalseFlag : IsTrueFlag);
        Unit = unit;
        IntegerValue = 0;
        ObjectValue = point;
    }

    internal void SetDice(int[] values)
    {
        Kind = Dice;
        Flags = values.Length > 0 ? (HasValueFlag | StorageObjectFlag) : StorageObjectFlag;
        Unit = UnitNone;
        IntegerValue = values.Length;
        Array.Sort(values);
        Array.Reverse(values);
        ObjectValue = values;
    }

    internal void SetList(VmListObject list)
    {
        Kind = List;
        Flags = list.Length > 0 ? StorageObjectFlag | HasValueFlag : StorageObjectFlag;
        Unit = UnitNone;
        IntegerValue = list.Length;
        ObjectValue = list;
    }

    internal void SetMap(VmMapObject map)
    {
        Kind = Map;
        Flags = map.Length > 0 ? StorageObjectFlag | HasValueFlag : StorageObjectFlag;
        Unit = UnitNone;
        IntegerValue = map.Length;
        ObjectValue = map;
    }

    internal void SetRange(long from, long to, long step)
    {
        Kind = GameEventScriptBytecodeTypeKind.Range;
        Unit = UnitNone;
        if (step == 0 || (step > 0 && from > to) || (step < 0 && from < to))
        {
            ObjectValue = EmptyRange;
            IntegerValue = 0;
        }
        else
        {
            ObjectValue = new VmRange(from, to, step);
            if (step > 0) IntegerValue = unchecked((ulong)to - (ulong)from) / (ulong)step >= long.MaxValue ? long.MaxValue : (long)(unchecked((ulong)to - (ulong)from) / (ulong)step + 1UL);
            else IntegerValue = unchecked((ulong)from - (ulong)to) / unchecked(0UL - (ulong)step) >= long.MaxValue ? long.MaxValue : (long)(unchecked((ulong)from - (ulong)to) / unchecked(0UL - (ulong)step) + 1UL);
        }

        Flags = IntegerValue > 0 ? StorageObjectFlag | HasValueFlag : StorageObjectFlag;
    }

    internal void SetRange(double from, double to, double step)
    {
        if (!(double.IsFinite(from) && double.IsFinite(to) && double.IsFinite(step)))
        {
            SetNothing();
            return;
        }

        Kind = GameEventScriptBytecodeTypeKind.Range;
        Unit = UnitNone;
        if (step == 0 || (step > 0 && from > to) || (step < 0 && from < to))
        {
            ObjectValue = EmptyRange;
            IntegerValue = 0;
        }
        else
        {
            ObjectValue = new VmFloatRange(from, to, step);
            IntegerValue = (long)Math.Floor(Math.Abs(to - from) / Math.Abs(step)) + 1;
        }

        Flags = IntegerValue > 0 ? StorageObjectFlag | HasValueFlag : StorageObjectFlag;
    }

    private static VmRange EmptyRange = new(0, 0, 0);

    internal void SetMessageHandler(GameEventScriptMessageSignature handler)
    {
        Kind = Handler;
        Flags = StorageObjectFlag | HasValueFlag;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = handler;
    }

    internal void SetMessage(GameEventScriptMessage message)
    {
        Kind = Message;
        Flags = StorageObjectFlag | HasValueFlag;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = message;
    }

    internal void SetSeries(GameEventScriptSeriesValue series)
    {
        Kind = Series;
        Flags = StorageObjectFlag | HasValueFlag;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = series;
    }

    internal void SetStream(IVmStream value)
    {
        Kind = Stream;
        Flags = StorageObjectFlag;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = value;
    }

    internal void CreateListBuilder()
    {
        Kind = ListBuilder;
        Flags = StorageObjectFlag;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = new List<VmValue>();
    }

    public double AsNumeric => Kind switch
    {
        Integer => IntegerValue,
        Float or Percentage => FloatValue,
        GameEventScriptBytecodeTypeKind.Boolean => IsTrue ? 1d : 0d,
        Text when IsStoragePointer => double.TryParse(OwningState.Binary.TextConstantTable.Resolve((ushort)IntegerValue), NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ? number : double.NaN,
        Text when ObjectValue is string text => double.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ? number : double.NaN,
        Tag when IsStoragePointer => ResolveNumericTagValue(OwningState.Binary.TextConstantTable.Resolve((ushort)IntegerValue)),
        Tag when ObjectValue is string tag => ResolveNumericTagValue(tag),
        _ => double.NaN,
    };

    public double AsNumericWithUnit(out GameEventScriptBytecodeInstructionUnit unit)
    {
        unit = Unit;
        return Kind switch
        {
            Float or Percentage => FloatValue,
            Integer => IntegerValue,
            _ => double.NaN,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal string ReadTextOrTag() => IsStoragePointer ? OwningState.Binary.TextConstantTable.Resolve((ushort)IntegerValue) : ObjectValue as string ?? string.Empty;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ReadOnlySpan<ushort> ResolveIntegerAsPointerList() => OwningState.Binary.Uint16ConstantTable.Resolve((ushort)IntegerValue);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool EqualsValue(ref VmValue other) => Unit == other.Unit && Kind == other.Kind && Kind switch
    {
        Integer => IntegerValue == other.IntegerValue,
        Float or Percentage => FloatValue == other.FloatValue,
        GameEventScriptBytecodeTypeKind.Boolean => IsTrue == other.IsTrue,
        Text or Tag => string.Equals(ReadTextOrTag(), other.ReadTextOrTag(), StringComparison.Ordinal),
        _ => ReferenceEquals(ObjectValue, other.ObjectValue)
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryGetInteger(out long value)
    {
        if (Kind != Integer)
        {
            value = 0;
            return false;
        }

        value = IntegerValue;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryCreateStream(out IVmStream stream)
    {
        switch (Kind)
        {
            case GameEventScriptBytecodeTypeKind.Range when ObjectValue is VmRange range:
                stream = new VmIntegerRangeStream(range.from, range.to, range.step);
                return true;
            case GameEventScriptBytecodeTypeKind.Range when ObjectValue is VmFloatRange range:
                stream = new VmFloatRangeStream(range.from, range.to, range.step);
                return true;
            case List when ObjectValue is VmListObject list:
                stream = new VmListStream(list);
                return true;
            case Dice when ObjectValue is int[] dices:
                stream = new VmIntStream(dices);
                return true;
            case Map when ObjectValue is VmMapObject map:
                stream = new VmListStream(map.ValueList);
                return true;
            case Vector or Point when ObjectValue is VmFloatTriplet vp:
                stream = new VmIndexAccessStream(vp);
                return true;
            case Text or Tag when IsStoragePointer:
                stream = new VmStringStream(OwningState.Binary.TextConstantTable.Resolve((ushort)IntegerValue));
                return true;
            case Text or Tag when this is { IsStorageObject: true, ObjectValue: string text }:
                stream = new VmStringStream(text);
                return true;
            case Series:
            default:
                stream = default;
                return false;
        }
    }
}