using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeInstructionUnit;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;
using static StepH.GameEventScript.VirtualMachine.GesVmMathConstants;
using static StepH.GameEventScript.VirtualMachine.GesVmValue.GesVmValueFlags;

namespace StepH.GameEventScript.VirtualMachine;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "ConvertToAutoPropertyWithPrivateSetter")]
[StructLayout(LayoutKind.Explicit, Size = 32)]
internal struct GesVmValue
{
    [Flags]
    internal enum GesVmValueFlags : byte
    {
        None = 0,

        IsTrueFlag = 1 << 0,
        IsFalseFlag = 1 << 1,
        HasValueFlag = 1 << 2,
        IsNumericFlag = 1 << 3,

        StorageObjectFlag = 1 << 4
    }

    [FieldOffset(0)] internal long IntegerValue;
    [FieldOffset(0)] internal double FloatValue;
    [FieldOffset(8)] internal object? ObjectValue;

    [FieldOffset(16)] internal GameEventScriptBytecodeTypeKind Kind;
    [FieldOffset(17)] internal GameEventScriptBytecodeInstructionUnit Unit;
    [FieldOffset(18)] internal GesVmValueFlags Flags;

    internal readonly bool IsTrue => (Flags & IsTrueFlag) != 0;
    internal readonly bool IsFalse => (Flags & IsFalseFlag) != 0;
    internal readonly bool IsNotTrue => (Flags & IsTrueFlag) == 0;
    internal readonly bool IsTruthDeterminate => (Flags & (IsTrueFlag | IsFalseFlag)) != 0;
    internal readonly bool IsTruthIndeterminate => (Flags & (IsTrueFlag | IsFalseFlag)) == 0;

    internal readonly bool IsNumeric => (Flags & IsNumericFlag) != 0;
    internal readonly bool HasValue => (Flags & HasValueFlag) != 0;

    internal readonly bool IsNothing => Kind is Nothing || (Kind is Float or Percentage && double.IsNaN(FloatValue));
    internal readonly bool IsNotNothing => Kind is not Nothing && Kind is not Float and not Percentage || Kind is Float or Percentage && !double.IsNaN(FloatValue);

    internal readonly bool IsUnit(GameEventScriptBytecodeInstructionUnit requiredUnit) => Unit == requiredUnit;
    internal readonly bool HasUnit => Unit.IsNumericUnit();
    internal readonly bool IsStorageObject => (Flags & StorageObjectFlag) != 0;

    internal readonly string TextValue => ObjectValue as string ?? string.Empty;

    internal void SetNothing()
    {
        Kind = Nothing;
        Unit = UnitNone;
        Flags = None;
        IntegerValue = 0;
        ObjectValue = null;
    }

    internal void SetBoolean(bool value)
    {
        Kind = GameEventScriptBytecodeTypeKind.Boolean;
        Flags = IsNumericFlag | HasValueFlag | (value ? IsTrueFlag : IsFalseFlag);
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
        if (double.IsNaN(ratio))
        {
            SetNothing();
            return;
        }
        if (double.IsFinite(ratio))
        {
            Kind = Percentage;
            Flags = IsNumericFlag | HasValueFlag | (ratio != 0 ? IsTrueFlag : IsFalseFlag);
        }
        else
        {
            Kind = Float;
            Flags = IsNumericFlag | IsTrueFlag | HasValueFlag;
        }

        Unit = UnitNone;
        FloatValue = ratio;
        ObjectValue = null;
    }

    internal void SetText(string text)
    {
        Kind = Text;
        Flags = StorageObjectFlag |
                (text.Length == 0 ? None : HasValueFlag) |
                (text.Equals("true", StringComparison.OrdinalIgnoreCase) || text == "1" ? IsTrueFlag : IsFalseFlag);
        Unit = UnitNone;
        IntegerValue = text.Length;
        ObjectValue = text;
    }

    internal void SetTag(string tag)
    {
        Kind = Tag;
        Flags = StorageObjectFlag |
                HasValueFlag |
                (IsNumericTag(tag) ? IsNumericFlag : None) |
                (tag is "true" or "infinity" or "negativeinfinity" or "pi" or "e" or "tau" or "phi" ? IsTrueFlag : IsFalseFlag);
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
        ObjectValue = new GesVmValueVectorPoint(x, y, z);
    }

    internal void SetVector(GesVmValueVectorPoint vector, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
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
        ObjectValue = new GesVmValueVectorPoint(x, y, z);
    }

    internal void SetPoint(GesVmValueVectorPoint point, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
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
        Flags = values.Length > 0 ? HasValueFlag | StorageObjectFlag | IsNumericFlag : StorageObjectFlag | IsNumericFlag;
        Unit = UnitNone;
        IntegerValue = values.Length;
        Array.Sort(values);
        Array.Reverse(values);
        ObjectValue = values;
    }

    internal void SetList(GesVmValue[] list)
    {
        Kind = List;
        Flags = list.Length > 0 ? StorageObjectFlag | HasValueFlag : StorageObjectFlag;
        Unit = UnitNone;
        IntegerValue = list.Length;
        ObjectValue = list;
    }

    internal void SetMap(GesVmValueMap valueMap)
    {
        Kind = Map;
        Flags = valueMap.Length > 0 ? StorageObjectFlag | HasValueFlag : StorageObjectFlag;
        Unit = UnitNone;
        IntegerValue = valueMap.Length;
        ObjectValue = valueMap;
    }

    internal void SetRecord(GesVmValueMap record)
    {
        Kind = Custom;
        Flags = record.Length > 0 ? StorageObjectFlag | HasValueFlag : StorageObjectFlag;
        Unit = UnitNone;
        IntegerValue = record.Length;
        ObjectValue = record;
    }

    internal void SetExternalCustomType(GameEventScriptValue value)
    {
        Kind = Custom;
        Flags = StorageObjectFlag | HasValueFlag;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = value;
    }

    internal void SetRange(long from, long to, long step)
    {
        Kind = GameEventScriptBytecodeTypeKind.Range;
        Unit = UnitNone;
        if (step == 0 || (step > 0 && from > to) || (step < 0 && from < to))
        {
            ObjectValue = _emptyValueRangeInteger;
            IntegerValue = 0;
        }
        else
        {
            ObjectValue = new GesVmValueRangeInteger(from, to, step);
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
            ObjectValue = _emptyValueRangeInteger;
            IntegerValue = 0;
        }
        else
        {
            ObjectValue = new GesVmValueRangeFloat(from, to, step);
            IntegerValue = (long)Math.Floor(Math.Abs(to - from) / Math.Abs(step)) + 1;
        }

        Flags = IntegerValue > 0 ? StorageObjectFlag | HasValueFlag : StorageObjectFlag;
    }

    private static GesVmValueRangeInteger _emptyValueRangeInteger = new(0, 0, 0);

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

    internal void SetStream(IGesVmStream value)
    {
        Kind = Stream;
        Flags = StorageObjectFlag;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = value;
    }
    
    internal void SetListBuilder(GesVmValueListBuilder listBuilder)
    {
        Kind = ListBuilder;
        Flags = StorageObjectFlag;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = listBuilder;
    }

    public double AsNumeric => Kind switch
    {
        Integer => IntegerValue,
        Float or Percentage => FloatValue,
        GameEventScriptBytecodeTypeKind.Boolean => IsTrue ? 1d : 0d,
        Tag when ObjectValue is string tag => ResolveNumericTagValue(tag),
        Dice when ObjectValue is int[] dices => SumDices(dices),
        _ => double.NaN,
    };
    private static long SumDices(int[] values)
    {
        long sum = 0;
        foreach (var value in values) sum += value;
        return sum;
    }
    public double AsNumericWithUnit(out GameEventScriptBytecodeInstructionUnit unit)
    {
        unit = Unit;
        return AsNumeric;
    }

    internal bool EqualsValue(in GesVmValue other) => Unit == other.Unit && Kind == other.Kind && Kind switch
    {
        Integer => IntegerValue == other.IntegerValue,
        Float or Percentage => FloatValue == other.FloatValue,
        GameEventScriptBytecodeTypeKind.Boolean => IsTrue == other.IsTrue,
        Text or Tag => string.Equals(TextValue, other.TextValue, StringComparison.Ordinal),
        _ => ReferenceEquals(ObjectValue, other.ObjectValue)
    };
    
    internal bool TryCreateStream(out IGesVmStream stream)
    {
        switch (Kind)
        {
            case GameEventScriptBytecodeTypeKind.Range when ObjectValue is GesVmValueRangeInteger range:
                stream = new GesVmIntegerRangeStream(range.From, range.To, range.Step);
                return true;
            case GameEventScriptBytecodeTypeKind.Range when ObjectValue is GesVmValueRangeFloat range:
                stream = new GesVmFloatRangeStream(range.From, range.To, range.Step);
                return true;
            case List when ObjectValue is GesVmValue[] list:
                stream = new GesVmListStream(list);
                return true;
            case Dice when ObjectValue is int[] dices:
                stream = new GesVmIntStream(dices);
                return true;
            case Map when ObjectValue is GesVmValueMap map:
                stream = new GesVmListStream(map.ValueList);
                return true;
            case Vector or Point when ObjectValue is GesVmValueVectorPoint vp:
                stream = new GesVmTripletStream(vp);
                return true;
            case Text or Tag when this is { IsStorageObject: true, ObjectValue: string text }:
                stream = new GesVmStringStream(text);
                return true;
            case Series:
            default:
                stream = default;
                return false;
        }
    }
    internal string ConvertToText()
    {
        return Kind switch
        {
            Nothing => string.Empty,
            Integer => FormatNumber(IntegerValue, Unit),
            Float => FormatNumber(FloatValue, Unit),
            Percentage => $"{(FloatValue * 100d).ToString("0.############################", CultureInfo.InvariantCulture)}%",
            GameEventScriptBytecodeTypeKind.Boolean => IsTrue ? "True" : "False",
            Text => TextValue,
            Tag => ":" + TextValue,
            Vector when ObjectValue is GesVmValueVectorPoint vector => FormatTriplet("vector", vector, Unit),
            Point when ObjectValue is GesVmValueVectorPoint point => FormatTriplet("point", point, Unit),
            Dice when ObjectValue is int[] dice => FormatDice(dice),
            List when ObjectValue is GesVmValue[] list => FormatList(list),
            Map when ObjectValue is GesVmValueMap map => FormatMap(map),
            GameEventScriptBytecodeTypeKind.Range when ObjectValue is GesVmValueRangeInteger range => FormatRange(range.From, range.To, range.Step),
            GameEventScriptBytecodeTypeKind.Range when ObjectValue is GesVmValueRangeFloat range => FormatRange(range.From, range.To, range.Step),
            Series when ObjectValue is GameEventScriptSeriesValue series => $"series[{series.SignatureId} offset {series.Offset}]",
            Handler when ObjectValue is GameEventScriptMessageSignature signature => $"handler {signature.SignatureId}",
            Message when ObjectValue is GameEventScriptMessage message => message.ToString(),
            _ => Kind.ToString()
        };
    }
    private static string FormatNumber(long value, GameEventScriptBytecodeInstructionUnit unit)
        => unit.IsNumericUnit()
            ? $"{value.ToString(CultureInfo.InvariantCulture)}{unit.ToSuffix()}"
            : value.ToString(CultureInfo.InvariantCulture);
    private static string FormatNumber(double value, GameEventScriptBytecodeInstructionUnit unit)
    {
        var formatted = value.ToString("0.############################", CultureInfo.InvariantCulture);
        return unit.IsNumericUnit() ? $"{formatted}{unit.ToSuffix()}" : formatted;
    }
    private static string FormatTriplet(string typeName, GesVmValueVectorPoint triplet, GameEventScriptBytecodeInstructionUnit unit)
        => $"{typeName}[x: {FormatNumber(triplet.X, unit)}, y: {FormatNumber(triplet.Y, unit)}, z: {FormatNumber(triplet.Z, unit)}]";
    private static string FormatDice(int[] dice)
    {
        if (dice.Length == 0) return "dice[]";

        var builder = new StringBuilder("dice[");
        for (var i = 0; i < dice.Length; i++)
        {
            if (i > 0) builder.Append(", ");
            builder.Append(dice[i].ToString(CultureInfo.InvariantCulture));
        }

        builder.Append(']');
        return builder.ToString();
    }
    private static string FormatList(GesVmValue[] list)
    {
        var builder = new StringBuilder("[");
        for (var i = 0; i < list.Length; i++)
        {
            if (i > 0) builder.Append(", ");
            builder.Append(list[i].ConvertToText());
        }

        builder.Append(']');
        return builder.ToString();
    }
    private static string FormatMap(GesVmValueMap valueMap)
    {
        var builder = new StringBuilder("map[");
        var first = true;
        for (var i = 0; i < valueMap.StorageLength; i++)
        {
            if (!first) builder.Append(", ");
            first = false;
            builder.Append(valueMap.KeyAt(i));
            builder.Append(": ");
            builder.Append(valueMap.ValueAt(i).ConvertToText());
        }

        builder.Append(']');
        return builder.ToString();
    }
    private static string FormatRange(long from, long to, long step)
        => $"range[{from.ToString(CultureInfo.InvariantCulture)} to {to.ToString(CultureInfo.InvariantCulture)} step {step.ToString(CultureInfo.InvariantCulture)}]";
    private static string FormatRange(double from, double to, double step)
        => $"range[{FormatRangeComponent(from)} to {FormatRangeComponent(to)} step {FormatRangeComponent(step)}]";
    private static string FormatRangeComponent(double value)
        => value.ToString("0.############################", CultureInfo.InvariantCulture);
}
