#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeInstructionUnit;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;
using static StepH.GameEventScript.Runtime.Values.GesValue.GesValueFlags;

namespace StepH.GameEventScript.Runtime.Values;

internal readonly record struct GesNumericWithUnit(double Value, GameEventScriptBytecodeInstructionUnit Unit);

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "ConvertToAutoPropertyWithPrivateSetter")]
[StructLayout(LayoutKind.Explicit, Size = 32)]
public struct GesValue : IEquatable<GesValue>
{
    [Flags]
    internal enum GesValueFlags : byte
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
    [FieldOffset(18)] internal GesValueFlags Flags;

    public static GesValue GesNothing() => new();

    public static GesValue GesBoolean(bool boolean)
    {
        var value = new GesValue();
        value.SetBoolean(boolean);
        return value;
    }

    public static GesValue GesInteger(long integer, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
    {
        var value = new GesValue();
        value.SetInteger(integer, unit);
        return value;
    }

    public static GesValue GesFloat(double number, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
    {
        var value = new GesValue();
        value.SetFloat(number, unit);
        return value;
    }

    public static GesValue GesNumber(double number, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
        => GesFloat(number, unit);

    public static GesValue GesPercentage(double ratio)
    {
        var value = new GesValue();
        value.SetPercentage(ratio);
        return value;
    }

    public static GesValue GesText(string text)
    {
        text ??= string.Empty;
        GameEventScriptText.RequireValidUnicode(text, nameof(text));
        var value = new GesValue();
        value.SetText(text);
        return value;
    }

    public static GesValue GesTag(string tag)
    {
        tag ??= string.Empty;
        if (!GameEventScriptText.IsTagValue(tag))
            throw new ArgumentException("Tag values must use the portable ASCII name grammar.", nameof(tag));
        var value = new GesValue();
        value.SetTag(tag);
        return value;
    }

    public static GesValue GesVector(double x, double y = 0d, double z = 0d, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
    {
        var value = new GesValue();
        value.SetVector(x, y, z, unit);
        return value;
    }

    public static GesValue GesPoint(double x, double y = 0d, double z = 0d, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
    {
        var value = new GesValue();
        value.SetPoint(x, y, z, unit);
        return value;
    }

    public static GesValue GesList(GesValue[] items)
    {
        GesValue[] copy = items.Length == 0 ? [] : new GesValue[items.Length];
        if (items.Length > 0)
        {
            Array.Copy(items, copy, items.Length);
        }

        var value = new GesValue();
        value.SetList(copy);
        return value;
    }

    public static GesValue GesMap(string[] keys, GesValue[] values)
    {
        var count = keys.Length < values.Length ? keys.Length : values.Length;
        var uniqueKeys = new string[count];
        var uniqueValues = new GesValue[count];
        var uniqueCount = 0;
        for (var sourceIndex = 0; sourceIndex < count; sourceIndex++)
        {
            var key = keys[sourceIndex];
            var existingIndex = -1;
            for (var index = 0; index < uniqueCount; index++)
            {
                if (!string.Equals(uniqueKeys[index], key, StringComparison.Ordinal)) continue;
                existingIndex = index;
                break;
            }

            if (existingIndex >= 0)
            {
                uniqueValues[existingIndex] = values[sourceIndex];
                continue;
            }

            uniqueKeys[uniqueCount] = key;
            uniqueValues[uniqueCount] = values[sourceIndex];
            uniqueCount++;
        }

        var value = new GesValue();
        value.SetMap(new GesValueMap(uniqueKeys, uniqueValues, uniqueCount));
        return value;
    }

    public static GesValue GesMap(IReadOnlyDictionary<string, GesValue>? entries)
    {
        if (entries is null || entries.Count == 0)
        {
            var empty = new GesValue();
            empty.SetMap(new GesValueMap([], [], 0));
            return empty;
        }

        var keys = new string[entries.Count];
        var values = new GesValue[entries.Count];
        var index = 0;
        foreach (var entry in entries)
        {
            keys[index] = entry.Key;
            values[index] = entry.Value;
            index++;
        }

        var value = new GesValue();
        value.SetMap(new GesValueMap(keys, values, entries.Count));
        return value;
    }

    public static GesValue GesRecord(string typeName, string[] keys, GesValue[] values)
    {
        var value = new GesValue();
        value.SetRecord(typeName ?? string.Empty, new GesValueMap(keys, values, keys.Length < values.Length ? keys.Length : values.Length));
        return value;
    }

    public static GesValue GesRecord(string typeName, IReadOnlyDictionary<string, GesValue>? fields)
    {
        if (fields is null || fields.Count == 0)
        {
            var empty = new GesValue();
            empty.SetRecord(typeName ?? string.Empty, new GesValueMap([], [], 0));
            return empty;
        }

        var keys = new string[fields.Count];
        var values = new GesValue[fields.Count];
        var index = 0;
        foreach (var field in fields)
        {
            keys[index] = field.Key;
            values[index] = field.Value;
            index++;
        }

        return GesRecord(typeName, keys, values);
    }

    public static GesValue GesDice(int[] rolls)
    {
        int[] copy = rolls.Length == 0 ? [] : new int[rolls.Length];
        if (rolls.Length > 0)
        {
            Array.Copy(rolls, copy, rolls.Length);
        }

        var value = new GesValue();
        value.SetDice(copy);
        return value;
    }

    public static GesValue GesRange(long from, long to, long step = 1)
    {
        var value = new GesValue();
        value.SetRange(from, to, step);
        return value;
    }

    public static GesValue GesRange(double from, double to, double step = 1d)
    {
        var value = new GesValue();
        value.SetRange(from, to, step);
        return value;
    }

    public static GesValue GesMessage(GameEventScriptMessage message)
    {
        var value = new GesValue();
        value.SetMessage(message);
        return value;
    }

    public static GesValue GesHandler(GameEventScriptMessageSignature signature)
    {
        var value = new GesValue();
        value.SetMessageHandler(signature);
        return value;
    }

    internal static GesValue GesSeries(GesSeries series)
    {
        var value = new GesValue();
        value.SetSeries(series);
        return value;
    }

    internal readonly bool IsTrue => (Flags & IsTrueFlag) != 0;
    internal readonly bool IsFalse => (Flags & IsFalseFlag) != 0;
    internal readonly bool IsNotTrue => (Flags & IsTrueFlag) == 0;
    internal readonly bool IsTruthDeterminate => (Flags & (IsTrueFlag | IsFalseFlag)) != 0;
    internal readonly bool IsTruthIndeterminate => (Flags & (IsTrueFlag | IsFalseFlag)) == 0;

    public readonly bool IsNumeric => (Flags & IsNumericFlag) != 0;
    public readonly bool HasValue => (Flags & HasValueFlag) != 0;

    public readonly bool IsNothing => Kind is Nothing || (Kind is Float or Percentage && double.IsNaN(FloatValue));
    internal readonly bool IsNotNothing => Kind is not Nothing && Kind is not Float and not Percentage || Kind is Float or Percentage && !double.IsNaN(FloatValue);

    internal readonly bool IsUnit(GameEventScriptBytecodeInstructionUnit requiredUnit) => Unit == requiredUnit;
    public readonly bool HasUnit => Unit.IsNumericUnit();
    internal readonly bool IsStorageObject => (Flags & StorageObjectFlag) != 0;

    internal readonly string TextValue => ObjectValue as string ?? string.Empty;

    public readonly GameEventScriptBytecodeTypeKind ValueKind => Kind;

    public readonly GameEventScriptBytecodeInstructionUnit ValueUnit => Unit;

    public readonly long AsInteger() => Kind switch
    {
        GameEventScriptBytecodeTypeKind.Integer => IntegerValue,
        Float or Percentage => ToIntegerSaturated(FloatValue),
        GameEventScriptBytecodeTypeKind.Boolean => IsTrue ? 1 : 0,
        _ => ToIntegerSaturated(AsNumeric)
    };

    public readonly double AsNumber() => Kind switch
    {
        GameEventScriptBytecodeTypeKind.Integer => IntegerValue,
        Float or Percentage => FloatValue,
        GameEventScriptBytecodeTypeKind.Boolean => IsTrue ? 1d : 0d,
        _ => AsNumeric
    };

    public readonly bool AsBoolean() => Kind switch
    {
        GameEventScriptBytecodeTypeKind.Boolean => IsTrue,
        GameEventScriptBytecodeTypeKind.Integer => IntegerValue != 0,
        Float or Percentage => FloatValue != 0d,
        Vector or Point when ObjectValue is GesValueVectorPoint vector => vector.X != 0d || vector.Y != 0d || vector.Z != 0d,
        _ => IsTrue
    };

    public readonly string AsText() => TextValue.Length > 0 || Kind is GameEventScriptBytecodeTypeKind.Text or Tag ? TextValue : ToText;

    public readonly double X => ObjectValue is GesValueVectorPoint vector ? vector.X : 0d;

    public readonly double Y => ObjectValue is GesValueVectorPoint vector ? vector.Y : 0d;

    public readonly double Z => ObjectValue is GesValueVectorPoint vector ? vector.Z : 0d;

    public readonly int Length => IntegerValue < 0 ? 0 : IntegerValue > int.MaxValue ? int.MaxValue : (int)IntegerValue;

    public readonly GameEventScriptMessage? Message => ObjectValue as GameEventScriptMessage;

    public readonly GameEventScriptMessageSignature? Handler => ObjectValue as GameEventScriptMessageSignature;

    public readonly string? CustomTypeName => ObjectValue switch
    {
        GesExternalValue externalValue => externalValue.CustomTypeName,
        GesCustomObject customObject => customObject.TypeName,
        _ => null
    };

    public readonly GesValueSlice AsList()
        => ObjectValue is GesValue[] source ? new GesValueSlice(source) : GesValueSlice.Empty;

    public readonly GesValueMap? AsMap() => ObjectValue switch
    {
        GesValueMap map => map,
        GesCustomObject customObject => customObject.Map,
        GesExternalValue externalValue => externalValue.ToMap(),
        _ => null
    };

    public readonly int[] AsDice()
    {
        if (ObjectValue is not int[] rolls)
        {
            return [];
        }

        var result = new int[rolls.Length];
        Array.Copy(rolls, result, rolls.Length);
        return result;
    }

    public readonly GameEventScriptIntegerRange? IntegerRange => ObjectValue is GesValueRangeInteger range
        ? new GameEventScriptIntegerRange(range.From, range.To, range.Step)
        : null;

    public readonly GameEventScriptFloatRange? FloatRange => ObjectValue is GesValueRangeFloat range
        ? new GameEventScriptFloatRange(range.From, range.To, range.Step)
        : null;

    public readonly bool Equals(GesValue other) => EqualsValue(in other);

    public override readonly bool Equals(object? obj) => obj is GesValue other && Equals(other);

    public override readonly int GetHashCode() => GetValueHashCode();

    public override readonly string ToString() => ToText;

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
        if (GameEventScriptNumber.CanRepresentAsInteger(value))
        {
            var intValue = (long)value;
            Kind = Integer;
            Flags = IsNumericFlag | HasValueFlag | (intValue != 0 ? IsTrueFlag : IsFalseFlag);
            IntegerValue = intValue;
        }
        else
        {
            Kind = Float;
            Flags = IsNumericFlag | HasValueFlag | (value != 0 ? IsTrueFlag : IsFalseFlag);
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
        ratio = GameEventScriptNumber.CanonicalizeZero(ratio);
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
        => SetText(text, GameEventScriptText.CountScalars(text));

    internal void SetText(string text, int scalarCount)
    {
        Kind = Text;
        Flags = StorageObjectFlag |
                (text.Length == 0 ? None : HasValueFlag) |
                (GameEventScriptText.EqualsAsciiIgnoreCase(text, "true") || text == "1" ? IsTrueFlag : IsFalseFlag);
        Unit = UnitNone;
        IntegerValue = scalarCount;
        ObjectValue = text;
    }

    internal void SetTag(string tag)
    {
        Kind = Tag;
        Flags = StorageObjectFlag |
                HasValueFlag |
                IsFalseFlag;
        Unit = UnitNone;
        IntegerValue = GameEventScriptText.CountScalars(tag);
        ObjectValue = tag;
    }

    internal void SetVector(double x, double y, double z, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
    {
        if (double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(z))
        {
            SetNothing();
            return;
        }

        x = GameEventScriptNumber.CanonicalizeZero(x);
        y = GameEventScriptNumber.CanonicalizeZero(y);
        z = GameEventScriptNumber.CanonicalizeZero(z);
        Kind = Vector;
        Flags = StorageObjectFlag | HasValueFlag | (x is 0 or double.NaN && y is 0 or double.NaN && z is 0 or double.NaN ? IsFalseFlag : IsTrueFlag);
        Unit = unit;
        IntegerValue = 0;
        ObjectValue = new GesValueVectorPoint(x, y, z);
    }

    internal void SetVector(GesValueVectorPoint vector, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
        => SetVector(vector.X, vector.Y, vector.Z, unit);

    internal void SetPoint(double x, double y, double z, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
    {
        if (double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(z))
        {
            SetNothing();
            return;
        }

        x = GameEventScriptNumber.CanonicalizeZero(x);
        y = GameEventScriptNumber.CanonicalizeZero(y);
        z = GameEventScriptNumber.CanonicalizeZero(z);
        Kind = Point;
        Flags = StorageObjectFlag | HasValueFlag | (x is 0 or double.NaN && y is 0 or double.NaN && z is 0 or double.NaN ? IsFalseFlag : IsTrueFlag);
        Unit = unit;
        IntegerValue = 0;
        ObjectValue = new GesValueVectorPoint(x, y, z);
    }

    internal void SetPoint(GesValueVectorPoint point, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
        => SetPoint(point.X, point.Y, point.Z, unit);

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

    internal void SetList(GesValue[] list)
    {
        Kind = List;
        Flags = list.Length > 0 ? StorageObjectFlag | HasValueFlag : StorageObjectFlag;
        Unit = UnitNone;
        IntegerValue = list.Length;
        ObjectValue = list;
    }

    internal void SetMap(GesValueMap valueMap)
    {
        Kind = Map;
        Flags = valueMap.Length > 0 ? StorageObjectFlag | HasValueFlag : StorageObjectFlag;
        Unit = UnitNone;
        IntegerValue = valueMap.Length;
        ObjectValue = valueMap;
    }

    internal void SetRecord(string typeName, GesValueMap record)
    {
        Kind = Custom;
        Flags = record.Length > 0 ? StorageObjectFlag | HasValueFlag : StorageObjectFlag;
        Unit = UnitNone;
        IntegerValue = record.Length;
        ObjectValue = new GesCustomObject(typeName, record);
    }

    internal void SetExternalType(IGameEventScriptExternalValue value)
    {
        var externalValue = new GesExternalValue(value);
        Kind = Custom;
        Flags = StorageObjectFlag | HasValueFlag;
        Unit = UnitNone;
        IntegerValue = externalValue.Definition.Fields.Count;
        ObjectValue = externalValue;
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
            ObjectValue = new GesValueRangeInteger(from, to, step);
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

        from = GameEventScriptNumber.CanonicalizeZero(from);
        to = GameEventScriptNumber.CanonicalizeZero(to);
        step = GameEventScriptNumber.CanonicalizeZero(step);
        Kind = GameEventScriptBytecodeTypeKind.Range;
        Unit = UnitNone;
        if (step == 0 || (step > 0 && from > to) || (step < 0 && from < to))
        {
            ObjectValue = _emptyValueRangeInteger;
            IntegerValue = 0;
        }
        else
        {
            ObjectValue = new GesValueRangeFloat(from, to, step);
            IntegerValue = (long)Math.Floor(Math.Abs(to - from) / Math.Abs(step)) + 1;
        }

        Flags = IntegerValue > 0 ? StorageObjectFlag | HasValueFlag : StorageObjectFlag;
    }

    private static GesValueRangeInteger _emptyValueRangeInteger = new(0, 0, 0);

    internal void SetMessageHandler(GameEventScriptMessageSignature handler)
    {
        Kind = GameEventScriptBytecodeTypeKind.Handler;
        Flags = StorageObjectFlag | HasValueFlag;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = handler;
    }

    internal void SetMessage(GameEventScriptMessage message)
    {
        Kind = GameEventScriptBytecodeTypeKind.Message;
        Flags = StorageObjectFlag | HasValueFlag;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = message;
    }

    internal void SetSeries(GesSeries series)
    {
        Kind = Series;
        Flags = StorageObjectFlag | HasValueFlag;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = series;
    }

    internal void SetIterator(IGesIterator value)
    {
        Kind = Iterator;
        Flags = StorageObjectFlag;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = value;
    }

    internal void SetListBuilder(object listBuilder)
    {
        Kind = ListBuilder;
        Flags = StorageObjectFlag;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = listBuilder;
    }

    internal void SetMapBuilder(object mapBuilder)
    {
        Kind = MapBuilder;
        Flags = StorageObjectFlag;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = mapBuilder;
    }

    internal void SetDistinctBuilder(object distinctBuilder)
    {
        Kind = DistinctBuilder;
        Flags = StorageObjectFlag;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = distinctBuilder;
    }

    internal void SetGroupBuilder(object groupBuilder)
    {
        Kind = GroupBuilder;
        Flags = StorageObjectFlag;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = groupBuilder;
    }

    internal void SetOrderBuilder(object orderBuilder)
    {
        Kind = OrderBuilder;
        Flags = StorageObjectFlag;
        Unit = UnitNone;
        IntegerValue = 0;
        ObjectValue = orderBuilder;
    }

    public readonly double AsNumeric => Kind switch
    {
        Integer => IntegerValue,
        Float or Percentage => FloatValue,
        GameEventScriptBytecodeTypeKind.Boolean => IsTrue ? 1d : 0d,
        Dice when ObjectValue is int[] dices => SumDices(dices),
        _ => double.NaN,
    };
    private static long SumDices(int[] values)
    {
        long sum = 0;
        foreach (var value in values) sum += value;
        return sum;
    }

    private static long ToIntegerSaturated(double number) => GameEventScriptNumber.ToIntegerSaturated(number);
    internal GesNumericWithUnit AsNumericWithUnit()
        => new(AsNumeric, Unit);

    internal readonly bool EqualsValue(in GesValue other)
    {
        if (Unit != other.Unit || Kind != other.Kind)
        {
            return false;
        }

        switch (Kind)
        {
            case Nothing:
                return true;
            case Integer:
                return IntegerValue == other.IntegerValue;
            case Float or Percentage:
                return FloatValue.Equals(other.FloatValue);
            case GameEventScriptBytecodeTypeKind.Boolean:
                return IsTrue == other.IsTrue;
            case Text or Tag:
                return string.Equals(TextValue, other.TextValue, StringComparison.Ordinal);
            case Vector or Point when ObjectValue is GesValueVectorPoint left && other.ObjectValue is GesValueVectorPoint right:
                return left.X.Equals(right.X) && left.Y.Equals(right.Y) && left.Z.Equals(right.Z);
            case Dice when ObjectValue is int[] left && other.ObjectValue is int[] right:
                if (left.Length != right.Length) return false;
                for (var i = 0; i < left.Length; i++)
                {
                    if (left[i] != right[i]) return false;
                }

                return true;
            case List when ObjectValue is GesValue[] left && other.ObjectValue is GesValue[] right:
                if (left.Length != right.Length) return false;
                for (var i = 0; i < left.Length; i++)
                {
                    if (!left[i].EqualsValue(in right[i])) return false;
                }

                return true;
            case Map when ObjectValue is GesValueMap left && other.ObjectValue is GesValueMap right:
                if (left.StorageLength != right.StorageLength || left.Length != right.Length) return false;
                for (var i = 0; i < left.StorageLength; i++)
                {
                    if (!string.Equals(left.KeyAt(i), right.KeyAt(i), StringComparison.Ordinal)) return false;
                    var leftValue = left.ValueAt(i);
                    var rightValue = right.ValueAt(i);
                    if (!leftValue.EqualsValue(in rightValue)) return false;
                }

                return true;
            case Custom when ObjectValue is GesCustomObject leftCustom && other.ObjectValue is GesCustomObject rightCustom:
                if (!string.Equals(leftCustom.TypeName, rightCustom.TypeName, StringComparison.Ordinal)) return false;
                var customLeftMap = leftCustom.Map;
                var customRightMap = rightCustom.Map;
                if (customLeftMap.StorageLength != customRightMap.StorageLength || customLeftMap.Length != customRightMap.Length) return false;
                for (var i = 0; i < customLeftMap.StorageLength; i++)
                {
                    if (!string.Equals(customLeftMap.KeyAt(i), customRightMap.KeyAt(i), StringComparison.Ordinal)) return false;
                    var leftValue = customLeftMap.ValueAt(i);
                    var rightValue = customRightMap.ValueAt(i);
                    if (!leftValue.EqualsValue(in rightValue)) return false;
                }

                return true;
            case GameEventScriptBytecodeTypeKind.Range when ObjectValue is GesValueRangeInteger left && other.ObjectValue is GesValueRangeInteger right:
                return left.From == right.From && left.To == right.To && left.Step == right.Step;
            case GameEventScriptBytecodeTypeKind.Range when ObjectValue is GesValueRangeFloat left && other.ObjectValue is GesValueRangeFloat right:
                return left.From.Equals(right.From) && left.To.Equals(right.To) && left.Step.Equals(right.Step);
            case GameEventScriptBytecodeTypeKind.Handler when ObjectValue is GameEventScriptMessageSignature left && other.ObjectValue is GameEventScriptMessageSignature right:
                return left.Equals(right);
            case GameEventScriptBytecodeTypeKind.Message when ObjectValue is GameEventScriptMessage left && other.ObjectValue is GameEventScriptMessage right:
                return left.Equals(right);
            case Series when ObjectValue is GesSeries left && other.ObjectValue is GesSeries right:
                return string.Equals(left.SignatureId, right.SignatureId, StringComparison.Ordinal) && left.Offset == right.Offset;
            default:
                return ReferenceEquals(ObjectValue, other.ObjectValue);
        }
    }

    internal readonly int GetValueHashCode()
    {
        var hash = new HashCode();
        hash.Add(Kind);
        hash.Add(Unit);
        switch (Kind)
        {
            case Nothing:
                break;
            case Integer:
                hash.Add(IntegerValue);
                break;
            case Float or Percentage:
                hash.Add(FloatValue);
                break;
            case GameEventScriptBytecodeTypeKind.Boolean:
                hash.Add(IsTrue);
                break;
            case Text or Tag:
                hash.Add(TextValue, StringComparer.Ordinal);
                break;
            case Vector or Point when ObjectValue is GesValueVectorPoint vector:
                hash.Add(vector.X);
                hash.Add(vector.Y);
                hash.Add(vector.Z);
                break;
            case Dice when ObjectValue is int[] dice:
                for (var i = 0; i < dice.Length; i++) hash.Add(dice[i]);
                break;
            case List when ObjectValue is GesValue[] list:
                for (var i = 0; i < list.Length; i++) hash.Add(list[i].GetValueHashCode());
                break;
            case Map when ObjectValue is GesValueMap map:
                for (var i = 0; i < map.StorageLength; i++)
                {
                    hash.Add(map.KeyAt(i), StringComparer.Ordinal);
                    var value = map.ValueAt(i);
                    hash.Add(value.GetValueHashCode());
                }

                break;
            case Custom when ObjectValue is GesCustomObject custom:
                hash.Add(custom.TypeName, StringComparer.Ordinal);
                var customMap = custom.Map;
                for (var i = 0; i < customMap.StorageLength; i++)
                {
                    hash.Add(customMap.KeyAt(i), StringComparer.Ordinal);
                    var value = customMap.ValueAt(i);
                    hash.Add(value.GetValueHashCode());
                }

                break;
            case GameEventScriptBytecodeTypeKind.Range when ObjectValue is GesValueRangeInteger range:
                hash.Add(range.From);
                hash.Add(range.To);
                hash.Add(range.Step);
                break;
            case GameEventScriptBytecodeTypeKind.Range when ObjectValue is GesValueRangeFloat range:
                hash.Add(range.From);
                hash.Add(range.To);
                hash.Add(range.Step);
                break;
            case GameEventScriptBytecodeTypeKind.Handler when ObjectValue is GameEventScriptMessageSignature signature:
                hash.Add(signature);
                break;
            case GameEventScriptBytecodeTypeKind.Message when ObjectValue is GameEventScriptMessage message:
                hash.Add(message);
                break;
            case Series when ObjectValue is GesSeries series:
                hash.Add(series.SignatureId, StringComparer.Ordinal);
                hash.Add(series.Offset);
                break;
            default:
                hash.Add(ObjectValue);
                break;
        }

        return hash.ToHashCode();
    }

    internal IGesIterator? CreateIterator()
    {
        switch (Kind)
        {
            case GameEventScriptBytecodeTypeKind.Range when ObjectValue is GesValueRangeInteger range:
                return new GesIntegerRangeIterator(range.From, range.To, range.Step);
            case GameEventScriptBytecodeTypeKind.Range when ObjectValue is GesValueRangeFloat range:
                return new GesFloatRangeIterator(range.From, range.To, range.Step);
            case List when ObjectValue is GesValue[] list:
                return new GesListIterator(list);
            case Dice when ObjectValue is int[] dices:
                return new GesIntIterator(dices);
            case Map when ObjectValue is GesValueMap map:
                return new GesListIterator(map.ValueList);
            case Custom when ObjectValue is GesCustomObject custom:
                return new GesListIterator(custom.Map.ValueList);
            case Vector or Point when ObjectValue is GesValueVectorPoint vp:
                return new GesTripletIterator(vp);
            case Text or Tag when this is { IsStorageObject: true, ObjectValue: string text }:
                return new GesStringIterator(text);
            case Series:
            default:
                return null;
        }
    }

    internal readonly string ToText => Kind switch
    {
        Nothing => string.Empty,
        Integer => FormatNumber(IntegerValue, Unit),
        Float => FormatNumber(FloatValue, Unit),
        Percentage => $"{GameEventScriptNumber.FormatCanonicalFloat(FloatValue * 100d)}%",
        GameEventScriptBytecodeTypeKind.Boolean => IsTrue ? "True" : "False",
        Text => TextValue,
        Tag => ":" + TextValue,
        Vector when ObjectValue is GesValueVectorPoint vector => FormatTriplet("vector", vector, Unit),
        Point when ObjectValue is GesValueVectorPoint point => FormatTriplet("point", point, Unit),
        Dice when ObjectValue is int[] dice => FormatDice(dice),
        List when ObjectValue is GesValue[] list => FormatList(list),
        Map when ObjectValue is GesValueMap map => FormatMap(map),
        Custom when ObjectValue is GesCustomObject custom => FormatMap(custom.Map),
        GameEventScriptBytecodeTypeKind.Range when ObjectValue is GesValueRangeInteger range => FormatRange(range.From, range.To, range.Step),
        GameEventScriptBytecodeTypeKind.Range when ObjectValue is GesValueRangeFloat range => FormatRange(range.From, range.To, range.Step),
        Series when ObjectValue is GesSeries series => $"series[{series.SignatureId} offset {series.Offset}]",
        GameEventScriptBytecodeTypeKind.Handler when ObjectValue is GameEventScriptMessageSignature signature => $"handler {signature.SignatureId}",
        GameEventScriptBytecodeTypeKind.Message when ObjectValue is GameEventScriptMessage message => message.ToString(),
        _ => Kind.ToString()
    };

    private static string FormatNumber(long value, GameEventScriptBytecodeInstructionUnit unit)
        => unit.IsNumericUnit()
            ? $"{value.ToString(CultureInfo.InvariantCulture)}{unit.ToSuffix()}"
            : value.ToString(CultureInfo.InvariantCulture);
    private static string FormatNumber(double value, GameEventScriptBytecodeInstructionUnit unit)
    {
        var formatted = GameEventScriptNumber.FormatCanonicalFloat(value);
        return unit.IsNumericUnit() ? $"{formatted}{unit.ToSuffix()}" : formatted;
    }
    private static string FormatTriplet(string typeName, GesValueVectorPoint triplet, GameEventScriptBytecodeInstructionUnit unit)
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
    private static string FormatList(GesValue[] list)
    {
        var builder = new StringBuilder("[");
        for (var i = 0; i < list.Length; i++)
        {
            if (i > 0) builder.Append(", ");
            builder.Append(list[i].ToText);
        }

        builder.Append(']');
        return builder.ToString();
    }
    private static string FormatMap(GesValueMap valueMap)
    {
        var builder = new StringBuilder("map[");
        var first = true;
        for (var i = 0; i < valueMap.StorageLength; i++)
        {
            if (!first) builder.Append(", ");
            first = false;
            builder.Append(valueMap.KeyAt(i));
            builder.Append(": ");
            GesValue tempQualifier = valueMap.ValueAt(i);
            builder.Append(tempQualifier.ToText);
        }

        builder.Append(']');
        return builder.ToString();
    }
    private static string FormatRange(long from, long to, long step)
        => $"range[{from.ToString(CultureInfo.InvariantCulture)} to {to.ToString(CultureInfo.InvariantCulture)} step {step.ToString(CultureInfo.InvariantCulture)}]";
    private static string FormatRange(double from, double to, double step)
        => $"range[{FormatRangeComponent(from)} to {FormatRangeComponent(to)} step {FormatRangeComponent(step)}]";
    private static string FormatRangeComponent(double value)
        => GameEventScriptNumber.FormatCanonicalFloat(value);
}
