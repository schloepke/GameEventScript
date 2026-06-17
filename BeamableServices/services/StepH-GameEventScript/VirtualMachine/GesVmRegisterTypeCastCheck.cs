using System;
using System.Globalization;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeInstructionUnit;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmRegisterTypeCastCheck
{
    internal static void GesVmCastUnit(this GesVmState vmState, ushort destinationRegister, in GesVmValue xSlot, GameEventScriptBytecodeInstructionUnit unit)
    {
        var dst = new GesVmValue();
        dst.SetNothing();
        GesVmCastUnit(ref dst, in xSlot, unit, vmState);
        vmState.SetValue(destinationRegister, in dst);
    }
    internal static void GesVmCastUnit(ref GesVmValue dst, in GesVmValue xSlot, GameEventScriptBytecodeInstructionUnit unit, GesVmState state)
    {
        switch (xSlot.Kind)
        {
            case Integer:
                if (unit is UnitNone || xSlot.Unit is UnitNone || xSlot.Unit == unit) dst.SetInteger(xSlot.IntegerValue, unit);
                else dst.SetFloat(double.NaN);
                return;
            case Float:
                if ((unit is UnitNone || xSlot.Unit is UnitNone || xSlot.Unit == unit) && double.IsFinite(xSlot.FloatValue)) dst.SetFloat(xSlot.FloatValue, unit);
                else dst.SetFloat(double.NaN);
                return;
            case Vector:
                if (xSlot.ObjectValue is GesVmValueVectorPoint vector && (unit is UnitNone || xSlot.Unit is UnitNone || xSlot.Unit == unit)) dst.SetVector(vector, unit);
                else dst.SetFloat(double.NaN);
                return;
            case Point:
                if (xSlot.ObjectValue is GesVmValueVectorPoint point && (unit is UnitNone || xSlot.Unit is UnitNone || xSlot.Unit == unit)) dst.SetPoint(point, unit);
                else dst.SetFloat(double.NaN);
                return;
            default:
                dst.SetFloat(double.NaN);
                return;
        }
    }
    internal static void GesVmCheckUnit(this GesVmState vmState, ushort destinationRegister, in GesVmValue xSlot, GameEventScriptBytecodeInstructionUnit unit)
    {
        vmState.SetBoolean(destinationRegister, xSlot.Kind is Integer or Float or Vector or Point && xSlot.Unit == unit);
    }
    internal static void GesVmCast(this GesVmState vmState, ushort destinationRegister, in GesVmValue xSlot, GameEventScriptBytecodeTypeKind type, GameEventScriptSession? session = null)
    {
        var dst = new GesVmValue();
        dst.SetNothing();
        GesVmCast(ref dst, in xSlot, type, vmState, destinationRegister, session);
        vmState.SetValue(destinationRegister, in dst);
    }
    internal static void GesVmCast(ref GesVmValue dst, in GesVmValue xSlot, GameEventScriptBytecodeTypeKind type, GesVmState state, ushort destinationRegister, GameEventScriptSession? session = null)
    {
        switch (type)
        {
            case Nothing:
                dst.SetNothing();
                return;
            case GameEventScriptBytecodeTypeKind.Boolean:
                dst.SetBoolean(xSlot.IsTrue);
                return;
            case Integer:
                var integerNumber = xSlot.AsNumeric;
                if (double.IsFinite(integerNumber) && integerNumber is >= long.MinValue and <= long.MaxValue) dst.SetInteger((long)integerNumber, xSlot.Kind is Integer or Float ? xSlot.Unit : UnitNone);
                else dst.SetNothing();
                return;
            case Float:
                dst.SetFloat(xSlot.AsNumeric, xSlot.Kind is Integer or Float ? xSlot.Unit : UnitNone);
                return;
            case Percentage:
                if (xSlot.HasUnit)
                {
                    dst.SetNothing();
                    return;
                }

                var percentageNumber = xSlot.AsNumeric;
                if (double.IsFinite(percentageNumber)) dst.SetPercentage(xSlot.Kind is Integer || percentageNumber is > 1d or < -1d ? percentageNumber / 100d : percentageNumber);
                else dst.SetNothing();
                return;
            case Text:
                CastText(ref dst, in xSlot);
                return;
            case Tag:
                CastTag(ref dst, in xSlot);
                return;
            case Vector:
                CastVectorOrPoint(ref dst, in xSlot, asPoint: false);
                return;
            case Point:
                CastVectorOrPoint(ref dst, in xSlot, asPoint: true);
                return;
            case Dice:
                switch (xSlot.Kind)
                {
                    case Dice:
                        dst = xSlot;
                        return;
                    case List when xSlot.ObjectValue is GesVmValue[] list:
                    {
                        var dice = new int[list.Length];
                        for (var i = 0; i < list.Length; i++)
                        {
                            var item = list[i];
                            if (item.Kind is not Integer || item.IntegerValue <= 0 || item.IntegerValue > int.MaxValue)
                            {
                                dst.SetNothing();
                                return;
                            }

                            dice[i] = (int)item.IntegerValue;
                        }

                        dst.SetDice(dice);
                        return;
                    }
                    case not List:
                        dst.SetDice([]);
                        return;
                    default:
                        dst.SetNothing();
                        return;
                }
            case List:
                CastList(state, ref dst, in xSlot, session);
                return;
            case Map:
                CastMap(state, ref dst, in xSlot);
                return;

            case Custom:
            case Invalid:
                dst.SetNothing();
                return;
            default:
                if (xSlot.Kind == type) dst = xSlot;
                else dst.SetNothing(); 
                return;
        }
    }
    internal static void GesVmCastNumeric(this GesVmState vmState, ushort destinationRegister, in GesVmValue xSlot)
    {
        var dst = new GesVmValue();
        dst.SetNothing();
        GesVmCastNumeric(ref dst, in xSlot, vmState);
        vmState.SetValue(destinationRegister, in dst);
    }
    internal static void GesVmCastNumeric(ref GesVmValue dst, in GesVmValue xSlot, GesVmState state)
    {
        if (xSlot.Kind is Text)
        {
            if (double.TryParse(xSlot.TextValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            {
                dst.SetFloat(parsed);
            }
            else
            {
                dst.SetNothing();
            }

            return;
        }

        if (xSlot.Kind is Series && xSlot.ObjectValue is GameEventScriptSeriesValue series)
        {
            dst.BindArguments(series.FirstTerm);
            GesVmCastNumeric(ref dst, in dst, state);
            return;
        }

        dst.SetFloat(xSlot.AsNumeric, xSlot.Kind is Integer or Float ? xSlot.Unit : UnitNone);
    }
    internal static void GesVmCastCustom(this GesVmState vmState, ushort destinationRegister, in GesVmValue xSlot, ushort typeTextPointer)
    {
        var dst = new GesVmValue();
        dst.SetNothing();
        var typeName = vmState.FetchStringByPointer(typeTextPointer);
        if (IsCustomType(in xSlot, typeName))
        {
            if (xSlot.ObjectValue is GesVmValueMap typedMap)
            {
                dst.SetRecord(typedMap);
                vmState.SetValue(destinationRegister, in dst);
                return;
            }

            if (xSlot.Kind is Custom)
            {
                dst = xSlot;
                vmState.SetValue(destinationRegister, in dst);
                return;
            }

            dst.SetNothing();
            vmState.SetValue(destinationRegister, in dst);
            return;
        }

        if (xSlot.Kind is Map && xSlot.ObjectValue is GesVmValueMap map)
        {
            for (ushort recordId = 0; recordId < vmState.RecordConstructors.Length; recordId++)
            {
                var bind = vmState.RecordConstructors[recordId];
                if (bind.Kind is not GameEventScriptBinaryBindKind.Record ||
                    !string.Equals(vmState.FetchStringByPointer(bind.Name), typeName, StringComparison.Ordinal))
                {
                    continue;
                }

                vmState.ClearStage();
                for (var argumentIndex = 0; argumentIndex < bind.ArgumentNames.Count; argumentIndex++)
                {
                    var argumentName = vmState.FetchStringByPointer(bind.ArgumentNames[argumentIndex]);
                    if (map.TryGet(argumentName, out var argument)) vmState.StageValue(ref argument);
                    else vmState.StageNothing();
                }

                vmState.CallRecordConstructor(recordId, destinationRegister);
                return;
            }

            dst.SetNothing();
            vmState.SetValue(destinationRegister, in dst);
            return;
        }

        dst.SetNothing();
        vmState.SetValue(destinationRegister, in dst);
    }
    internal static void GesVmCheckType(this GesVmState vmState, ushort destinationRegister, in GesVmValue xSlot, GameEventScriptBytecodeTypeKind type)
    {
        vmState.SetBoolean(destinationRegister, type switch
        {
            Nothing => xSlot.IsNothing,
            Map => xSlot.Kind is Map or Custom,
            Invalid or Custom => false,
            _ => xSlot.IsNotNothing && xSlot.Kind == type
        });
    }
    internal static void GesVmCheckNumeric(this GesVmState vmState, ushort destinationRegister, in GesVmValue xSlot) => vmState.SetBoolean(destinationRegister, xSlot.IsNumeric);
    internal static void GesVmCheckInteger(this GesVmState vmState, ushort destinationRegister, in GesVmValue xSlot)
    {
        if (!xSlot.IsNumeric)
        {
            vmState.SetBoolean(destinationRegister, false);
            return;
        }

        var number = xSlot.AsNumeric;
        vmState.SetBoolean(destinationRegister, double.IsFinite(number) && number is >= long.MinValue and <= long.MaxValue && number == Math.Truncate(number));
    }
    internal static void GesVmCheckFractional(this GesVmState vmState, ushort destinationRegister, in GesVmValue xSlot)
    {
        if (!xSlot.IsNumeric)
        {
            vmState.SetBoolean(destinationRegister, false);
            return;
        }

        var number = xSlot.AsNumeric;
        vmState.SetBoolean(destinationRegister, double.IsFinite(number) && number != Math.Truncate(number));
    }
    internal static void GesVmCheckCustomType(this GesVmState vmState, ushort destinationRegister, in GesVmValue xSlot, ushort typeTextPointer)
    {
        vmState.SetBoolean(destinationRegister, IsCustomType(in xSlot, vmState.FetchStringByPointer(typeTextPointer)));
    }
    private static void CastText(ref GesVmValue dst, in GesVmValue xSlot)
    {
        if (xSlot.Kind is Text)
        {
            dst = xSlot;
            return;
        }

        dst.SetText(xSlot.ConvertToText());
    }
    private static void CastList(GesVmState vmState, ref GesVmValue dst, in GesVmValue xSlot, GameEventScriptSession? session)
    {
        switch (xSlot.Kind)
        {
            case List:
                dst = xSlot;
                return;
            case Text or Tag:
            {
                var text = xSlot.TextValue;
                if (text.Length == 0)
                {
                    dst.SetList(vmState.EmptyList);
                    return;
                }

                var list = new GesVmValue[text.Length];
                for (var i = 0; i < text.Length; i++) list[i].SetText(text[i].ToString());
                dst.SetList(list);
                return;
            }
            case Vector or Point when xSlot.ObjectValue is GesVmValueVectorPoint triplet:
            {
                var list = new GesVmValue[3];
                list[0].SetFloat(triplet.X, xSlot.Unit);
                list[1].SetFloat(triplet.Y, xSlot.Unit);
                list[2].SetFloat(triplet.Z, xSlot.Unit);
                dst.SetList(list);
                return;
            }
            case Dice when xSlot.ObjectValue is int[] dice:
            {
                var list = new GesVmValue[dice.Length];
                for (var i = 0; i < dice.Length; i++) list[i].SetInteger(dice[i]);
                dst.SetList(list);
                return;
            }
            case GameEventScriptBytecodeTypeKind.Range when xSlot.ObjectValue is GesVmValueRangeInteger range:
            {
                if (xSlot.IntegerValue > int.MaxValue)
                {
                    dst.SetNothing();
                    return;
                }

                if (session is not null && !session.RuntimeBudget.TryCheckRangeLength(xSlot.IntegerValue, "Range length exceeds the configured limit."))
                {
                    dst.SetList(vmState.EmptyList);
                    return;
                }

                var list = new GesVmValue[(int)xSlot.IntegerValue];
                var current = range.From;
                for (var i = 0; i < list.Length; i++)
                {
                    list[i].SetInteger(current);
                    current += range.Step;
                }

                dst.SetList(list);
                return;
            }
            case GameEventScriptBytecodeTypeKind.Range when xSlot.ObjectValue is GesVmValueRangeFloat range:
            {
                if (xSlot.IntegerValue > int.MaxValue)
                {
                    dst.SetNothing();
                    return;
                }

                if (session is not null && !session.RuntimeBudget.TryCheckRangeLength(xSlot.IntegerValue, "Range length exceeds the configured limit."))
                {
                    dst.SetList(vmState.EmptyList);
                    return;
                }

                var list = new GesVmValue[(int)xSlot.IntegerValue];
                var current = range.From;
                for (var i = 0; i < list.Length; i++)
                {
                    list[i].SetFloat(current);
                    current += range.Step;
                }

                dst.SetList(list);
                return;
            }
            default:
                dst.SetList(vmState.EmptyList);
                return;
        }
    }
    private static void CastMap(GesVmState vmState, ref GesVmValue dst, in GesVmValue xSlot)
    {
        switch (xSlot.Kind)
        {
            case Map or Custom when xSlot.ObjectValue is GesVmValueMap map:
            {
                if (!map.HasHiddenEntries)
                {
                    dst = xSlot;
                    return;
                }

                var visibleEntries = new GesVmValueMapBuilder(map.Length);
                for (var i = 0; i < map.StorageLength; i++)
                {
                    if (map.IsVisibleAt(i)) visibleEntries.Set(map.KeyAt(i), map.ValueAt(i));
                }

                dst.SetMap(visibleEntries.ToMap());
                return;
            }
            case Custom when xSlot.ObjectValue is GameEventScriptValue externalValue:
            {
                var sourceEntries = externalValue.AsMap();
                var entries = new GesVmValueMapBuilder(sourceEntries.Count);
                foreach (var (key, sourceValue) in sourceEntries)
                {
                    if (key.StartsWith("_", StringComparison.Ordinal)) continue;
                    var value = new GesVmValue();
                    value.SetNothing();
                    value.BindArguments(sourceValue);
                    entries.Set(key, value);
                }

                dst.SetMap(entries.ToMap());
                return;
            }
            case Vector or Point when xSlot.ObjectValue is GesVmValueVectorPoint triplet:
            {
                var x = new GesVmValue();
                x.SetNothing();
                x.SetFloat(triplet.X, xSlot.Unit);
                var y = new GesVmValue();
                y.SetNothing();
                y.SetFloat(triplet.Y, xSlot.Unit);
                var z = new GesVmValue();
                z.SetNothing();
                z.SetFloat(triplet.Z, xSlot.Unit);
                var entries = new GesVmValueMapBuilder(3);
                entries.Set("x", x);
                entries.Set("y", y);
                entries.Set("z", z);
                dst.SetMap(entries.ToMap());
                return;
            }
            default:
                dst.SetMap(new GesVmValueMapBuilder(0).ToMap());
                return;
        }
    }
    private static void CastVectorOrPoint(ref GesVmValue dst, in GesVmValue xSlot, bool asPoint)
    {
        var unit = UnitNone;
        double x = 0;
        double y = 0;
        double z = 0;

        switch (xSlot.Kind)
        {
            case Vector when xSlot.ObjectValue is GesVmValueVectorPoint vector:
                if (asPoint) dst.SetPoint(vector, xSlot.Unit);
                else dst = xSlot;
                return;
            case Point when xSlot.ObjectValue is GesVmValueVectorPoint point:
                if (asPoint) dst = xSlot;
                else dst.SetVector(point, xSlot.Unit);
                return;
            case Integer or Float or Percentage or Tag or Dice or GameEventScriptBytecodeTypeKind.Boolean:
                if (xSlot.Kind is Dice)
                {
                    if (xSlot.ObjectValue is not int[] dice)
                    {
                        dst.SetNothing();
                        return;
                    }

                    if (dice.Length > 0) x = dice[0];
                    if (dice.Length > 1) y = dice[1];
                    if (dice.Length > 2) z = dice[2];
                    break;
                }

                var number = xSlot.AsNumeric;
                if (!double.IsFinite(number))
                {
                    dst.SetNothing();
                    return;
                }

                x = number;
                unit = xSlot.Kind is Integer or Float ? xSlot.Unit : UnitNone;
                break;
            case List when xSlot.ObjectValue is GesVmValue[] list:
                for (var i = 0; i < list.Length && i < 3; i++)
                {
                    var item = list[i];
                    if (!item.IsNumeric && item.Kind is not GameEventScriptBytecodeTypeKind.Boolean)
                    {
                        dst.SetNothing();
                        return;
                    }

                    var value = item.AsNumeric;
                    if (!double.IsFinite(value))
                    {
                        dst.SetNothing();
                        return;
                    }

                    if (i == 0) x = value;
                    else if (i == 1) y = value;
                    else z = value;
                }

                break;
            case Map or Custom when xSlot.ObjectValue is GesVmValueMap map:
            {
                if (map.TryGet("x", out var xValue))
                {
                    if (!xValue.IsNumeric && xValue.Kind is not GameEventScriptBytecodeTypeKind.Boolean)
                    {
                        dst.SetNothing();
                        return;
                    }

                    x = xValue.AsNumeric;
                }

                if (map.TryGet("y", out var yValue))
                {
                    if (!yValue.IsNumeric && yValue.Kind is not GameEventScriptBytecodeTypeKind.Boolean)
                    {
                        dst.SetNothing();
                        return;
                    }

                    y = yValue.AsNumeric;
                }

                if (map.TryGet("z", out var zValue))
                {
                    if (!zValue.IsNumeric && zValue.Kind is not GameEventScriptBytecodeTypeKind.Boolean)
                    {
                        dst.SetNothing();
                        return;
                    }

                    z = zValue.AsNumeric;
                }

                if (!(double.IsFinite(x) && double.IsFinite(y) && double.IsFinite(z)))
                {
                    dst.SetNothing();
                    return;
                }

                break;
            }
            case GameEventScriptBytecodeTypeKind.Range when xSlot.ObjectValue is GesVmValueRangeInteger range:
            {
                var current = range.From;
                for (var i = 0; i < xSlot.IntegerValue && i < 3; i++)
                {
                    if (i == 0) x = current;
                    else if (i == 1) y = current;
                    else z = current;
                    current += range.Step;
                }

                break;
            }
            case GameEventScriptBytecodeTypeKind.Range when xSlot.ObjectValue is GesVmValueRangeFloat range:
            {
                var current = range.From;
                for (var i = 0; i < xSlot.IntegerValue && i < 3; i++)
                {
                    if (i == 0) x = current;
                    else if (i == 1) y = current;
                    else z = current;
                    current += range.Step;
                }

                break;
            }
            default:
                dst.SetNothing();
                return;
        }

        if (asPoint) dst.SetPoint(x, y, z, unit);
        else dst.SetVector(x, y, z, unit);
    }
    private static void CastTag(ref GesVmValue dst, in GesVmValue xSlot)
    {
        switch (xSlot.Kind)
        {
            case Tag:
                if (GameEventScriptTagValue.IsValidTagName(xSlot.TextValue)) dst = xSlot;
                else dst.SetNothing();
                return;
            case Text when xSlot.ObjectValue is string text:
                if (GameEventScriptTagValue.TryNormalizeTextCast(text, out var tag)) dst.SetTag(tag);
                else dst.SetNothing();
                return;
            case GameEventScriptBytecodeTypeKind.Boolean:
                dst.SetTag(xSlot.IsTrue ? "true" : "false");
                return;
            default:
                dst.SetNothing();
                return;
        }
    }
    private static bool IsCustomType(in GesVmValue value, string typeName)
    {
        if (value.ObjectValue is IGameEventScriptCustomTypeValue custom) return string.Equals(custom.CustomTypeName, typeName, StringComparison.Ordinal);
        return value.Kind switch
        {
            Custom when value.ObjectValue is string customTypeName => string.Equals(customTypeName, typeName, StringComparison.Ordinal),
            Map or Custom when value.ObjectValue is GesVmValueMap map && map.TryGet(GesVmValueMap.HiddenRecordTypeField, out var marker) => marker.Kind switch
            {
                Tag when marker.ObjectValue is string markerTypeName => string.Equals(markerTypeName, typeName, StringComparison.Ordinal),
                _ => false
            },
            _ => false
        };
    }
}
