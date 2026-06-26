using System;
using System.Globalization;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeInstructionUnit;
using StepH.GameEventScript.Runtime.Values;

namespace StepH.GameEventScript.Runtime.VM;

internal static class GesVmRegisterTypeCastCheck
{
    internal static void GesVmCastUnit(this GesVmState vmState, ushort destinationRegister, in GesValue xValue, GameEventScriptBytecodeInstructionUnit unit)
    {
        var dst = new GesValue();
        GesVmCastUnit(ref dst, in xValue, unit, vmState);
        vmState.SetValue(destinationRegister, in dst);
    }
    internal static void GesVmCastUnit(ref GesValue dst, in GesValue xValue, GameEventScriptBytecodeInstructionUnit unit, GesVmState state)
    {
        switch (xValue.Kind)
        {
            case Integer:
                if (unit is UnitNone || xValue.Unit is UnitNone || xValue.Unit == unit) dst.SetInteger(xValue.IntegerValue, unit);
                else dst.SetFloat(double.NaN);
                return;
            case Float:
                if ((unit is UnitNone || xValue.Unit is UnitNone || xValue.Unit == unit) && double.IsFinite(xValue.FloatValue)) dst.SetFloat(xValue.FloatValue, unit);
                else dst.SetFloat(double.NaN);
                return;
            case Vector:
                if (xValue.ObjectValue is GesValueVectorPoint vector && (unit is UnitNone || xValue.Unit is UnitNone || xValue.Unit == unit)) dst.SetVector(vector, unit);
                else dst.SetFloat(double.NaN);
                return;
            case Point:
                if (xValue.ObjectValue is GesValueVectorPoint point && (unit is UnitNone || xValue.Unit is UnitNone || xValue.Unit == unit)) dst.SetPoint(point, unit);
                else dst.SetFloat(double.NaN);
                return;
            default:
                dst.SetFloat(double.NaN);
                return;
        }
    }
    internal static void GesVmCheckUnit(this GesVmState vmState, ushort destinationRegister, in GesValue xValue, GameEventScriptBytecodeInstructionUnit unit)
    {
        vmState.SetBoolean(destinationRegister, xValue.Kind is Integer or Float or Vector or Point && xValue.Unit == unit);
    }
    internal static void GesVmCast(this GesVmState vmState, ushort destinationRegister, in GesValue xValue, GameEventScriptBytecodeTypeKind type, GameEventScriptSession? session = null)
    {
        var dst = new GesValue();
        GesVmCast(ref dst, in xValue, type, vmState, destinationRegister, session);
        vmState.SetValue(destinationRegister, in dst);
    }
    internal static void GesVmCast(ref GesValue dst, in GesValue xValue, GameEventScriptBytecodeTypeKind type, GesVmState state, ushort destinationRegister, GameEventScriptSession? session = null)
    {
        switch (type)
        {
            case Nothing:
                dst.SetNothing();
                return;
            case GameEventScriptBytecodeTypeKind.Boolean:
                dst.SetBoolean(xValue.IsTrue);
                return;
            case Integer:
                var integerNumber = xValue.AsNumeric;
                if (double.IsFinite(integerNumber) && integerNumber is >= long.MinValue and <= long.MaxValue) dst.SetInteger((long)integerNumber, xValue.Kind is Integer or Float ? xValue.Unit : UnitNone);
                else dst.SetNothing();
                return;
            case Float:
                dst.SetFloat(xValue.AsNumeric, xValue.Kind is Integer or Float ? xValue.Unit : UnitNone);
                return;
            case Percentage:
                if (xValue.HasUnit)
                {
                    dst.SetNothing();
                    return;
                }

                var percentageNumber = xValue.AsNumeric;
                if (double.IsFinite(percentageNumber)) dst.SetPercentage(xValue.Kind is Integer || percentageNumber is > 1d or < -1d ? percentageNumber / 100d : percentageNumber);
                else dst.SetNothing();
                return;
            case Text:
                CastText(ref dst, in xValue);
                return;
            case Tag:
                CastTag(ref dst, in xValue);
                return;
            case Vector:
                CastVectorOrPoint(ref dst, in xValue, asPoint: false);
                return;
            case Point:
                CastVectorOrPoint(ref dst, in xValue, asPoint: true);
                return;
            case Dice:
                switch (xValue.Kind)
                {
                    case Dice:
                        dst = xValue;
                        return;
                    case List when xValue.ObjectValue is GesValue[] list:
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
                CastList(state, ref dst, in xValue, session);
                return;
            case Map:
                CastMap(state, ref dst, in xValue);
                return;

            case Custom:
                dst.SetNothing();
                return;
            default:
                if (xValue.Kind == type) dst = xValue;
                else dst.SetNothing(); 
                return;
        }
    }
    internal static void GesVmCastNumeric(this GesVmState vmState, ushort destinationRegister, in GesValue xValue)
    {
        var dst = new GesValue();
        GesVmCastNumeric(ref dst, in xValue, vmState);
        vmState.SetValue(destinationRegister, in dst);
    }
    internal static void GesVmCastNumeric(ref GesValue dst, in GesValue xValue, GesVmState state)
    {
        if (xValue.Kind is Text)
        {
            if (double.TryParse(xValue.TextValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            {
                dst.SetFloat(parsed);
            }
            else
            {
                dst.SetNothing();
            }

            return;
        }

        if (xValue.Kind is Series && xValue.ObjectValue is GesSeries series)
        {
            series.TryGetTerm(0, ref dst);
            GesVmCastNumeric(ref dst, in dst, state);
            return;
        }

        dst.SetFloat(xValue.AsNumeric, xValue.Kind is Integer or Float ? xValue.Unit : UnitNone);
    }
    internal static void GesVmCastCustom(this GesVmState vmState, ushort destinationRegister, in GesValue xValue, ushort typeTextPointer)
    {
        var dst = new GesValue();
        var typeName = vmState.FetchStringByPointer(typeTextPointer);
        if (IsCustomType(in xValue, typeName))
        {
            if (xValue.Kind is Custom)
            {
                dst = xValue;
                vmState.SetValue(destinationRegister, in dst);
                return;
            }

            dst.SetNothing();
            vmState.SetValue(destinationRegister, in dst);
            return;
        }

        if (xValue.Kind is Map && xValue.ObjectValue is GesValueMap map)
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
    internal static void GesVmCheckType(this GesVmState vmState, ushort destinationRegister, in GesValue xValue, GameEventScriptBytecodeTypeKind type)
    {
        vmState.SetBoolean(destinationRegister, type switch
        {
            Nothing => xValue.IsNothing,
            Map => xValue.Kind is Map or Custom,
            Custom => false,
            _ => xValue.IsNotNothing && xValue.Kind == type
        });
    }
    internal static void GesVmCheckNumeric(this GesVmState vmState, ushort destinationRegister, in GesValue xValue) => vmState.SetBoolean(destinationRegister, xValue.IsNumeric);
    internal static void GesVmCheckInteger(this GesVmState vmState, ushort destinationRegister, in GesValue xValue)
    {
        if (!xValue.IsNumeric)
        {
            vmState.SetBoolean(destinationRegister, false);
            return;
        }

        var number = xValue.AsNumeric;
        vmState.SetBoolean(destinationRegister, double.IsFinite(number) && number is >= long.MinValue and <= long.MaxValue && number == Math.Truncate(number));
    }
    internal static void GesVmCheckFractional(this GesVmState vmState, ushort destinationRegister, in GesValue xValue)
    {
        if (!xValue.IsNumeric)
        {
            vmState.SetBoolean(destinationRegister, false);
            return;
        }

        var number = xValue.AsNumeric;
        vmState.SetBoolean(destinationRegister, double.IsFinite(number) && number != Math.Truncate(number));
    }
    internal static void GesVmCheckCustomType(this GesVmState vmState, ushort destinationRegister, in GesValue xValue, ushort typeTextPointer)
    {
        vmState.SetBoolean(destinationRegister, IsCustomType(in xValue, vmState.FetchStringByPointer(typeTextPointer)));
    }
    private static void CastText(ref GesValue dst, in GesValue xValue)
    {
        if (xValue.Kind is Text)
        {
            dst = xValue;
            return;
        }

        dst.SetText(xValue.ToText);
    }
    private static void CastList(GesVmState vmState, ref GesValue dst, in GesValue xValue, GameEventScriptSession? session)
    {
        switch (xValue.Kind)
        {
            case List:
                dst = xValue;
                return;
            case Text or Tag:
            {
                var text = xValue.TextValue;
                if (text.Length == 0)
                {
                    dst.SetList(vmState.EmptyList);
                    return;
                }

                var list = new GesValue[text.Length];
                for (var i = 0; i < text.Length; i++) list[i].SetText(text[i].ToString());
                dst.SetList(list);
                return;
            }
            case Vector or Point when xValue.ObjectValue is GesValueVectorPoint triplet:
            {
                var list = new GesValue[3];
                list[0].SetFloat(triplet.X, xValue.Unit);
                list[1].SetFloat(triplet.Y, xValue.Unit);
                list[2].SetFloat(triplet.Z, xValue.Unit);
                dst.SetList(list);
                return;
            }
            case Dice when xValue.ObjectValue is int[] dice:
            {
                var list = new GesValue[dice.Length];
                for (var i = 0; i < dice.Length; i++) list[i].SetInteger(dice[i]);
                dst.SetList(list);
                return;
            }
            case GameEventScriptBytecodeTypeKind.Range when xValue.ObjectValue is GesValueRangeInteger range:
            {
                if (xValue.IntegerValue > int.MaxValue)
                {
                    dst.SetNothing();
                    return;
                }

                if (session is not null && !session.RuntimeBudget.TryCheckRangeLength(xValue.IntegerValue, "Range length exceeds the configured limit."))
                {
                    dst.SetList(vmState.EmptyList);
                    return;
                }

                var list = new GesValue[(int)xValue.IntegerValue];
                var current = range.From;
                for (var i = 0; i < list.Length; i++)
                {
                    list[i].SetInteger(current);
                    current += range.Step;
                }

                dst.SetList(list);
                return;
            }
            case GameEventScriptBytecodeTypeKind.Range when xValue.ObjectValue is GesValueRangeFloat range:
            {
                if (xValue.IntegerValue > int.MaxValue)
                {
                    dst.SetNothing();
                    return;
                }

                if (session is not null && !session.RuntimeBudget.TryCheckRangeLength(xValue.IntegerValue, "Range length exceeds the configured limit."))
                {
                    dst.SetList(vmState.EmptyList);
                    return;
                }

                var list = new GesValue[(int)xValue.IntegerValue];
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
    private static void CastMap(GesVmState vmState, ref GesValue dst, in GesValue xValue)
    {
        switch (xValue.Kind)
        {
            case Map when xValue.ObjectValue is GesValueMap map:
            {
                if (!map.HasHiddenEntries)
                {
                    dst = xValue;
                    return;
                }

                var visibleEntries = new GesVmMapBuilder(map.Length);
                for (var i = 0; i < map.StorageLength; i++)
                {
                    if (map.IsVisibleAt(i)) visibleEntries.Set(map.KeyAt(i), map.ValueAt(i));
                }

                dst.SetMap(visibleEntries.ToMap());
                return;
            }
            case Custom when xValue.ObjectValue is GesCustomObject customObject:
            {
                var map = customObject.Map;
                if (!map.HasHiddenEntries)
                {
                    dst.SetMap(map);
                    return;
                }

                var visibleEntries = new GesVmMapBuilder(map.Length);
                for (var i = 0; i < map.StorageLength; i++)
                {
                    if (map.IsVisibleAt(i)) visibleEntries.Set(map.KeyAt(i), map.ValueAt(i));
                }

                dst.SetMap(visibleEntries.ToMap());
                return;
            }
            case Custom when xValue.ObjectValue is GesExternalObject externalObject:
            {
                var sourceEntries = externalObject.ToMap();
                var entries = new GesVmMapBuilder(sourceEntries.Length);
                for (var i = 0; i < sourceEntries.StorageLength; i++)
                {
                    var key = sourceEntries.KeyAt(i);
                    if (key.StartsWith("_", StringComparison.Ordinal)) continue;
                    entries.Set(key, sourceEntries.ValueAt(i));
                }

                dst.SetMap(entries.ToMap());
                return;
            }
            case Vector or Point when xValue.ObjectValue is GesValueVectorPoint triplet:
            {
                var x = new GesValue();
                x.SetFloat(triplet.X, xValue.Unit);
                var y = new GesValue();
                y.SetFloat(triplet.Y, xValue.Unit);
                var z = new GesValue();
                z.SetFloat(triplet.Z, xValue.Unit);
                var entries = new GesVmMapBuilder(3);
                entries.Set("x", x);
                entries.Set("y", y);
                entries.Set("z", z);
                dst.SetMap(entries.ToMap());
                return;
            }
            default:
                dst.SetMap(new GesVmMapBuilder(0).ToMap());
                return;
        }
    }
    private static void CastVectorOrPoint(ref GesValue dst, in GesValue xValue, bool asPoint)
    {
        var unit = UnitNone;
        double x = 0;
        double y = 0;
        double z = 0;

        switch (xValue.Kind)
        {
            case Vector when xValue.ObjectValue is GesValueVectorPoint vector:
                if (asPoint) dst.SetPoint(vector, xValue.Unit);
                else dst = xValue;
                return;
            case Point when xValue.ObjectValue is GesValueVectorPoint point:
                if (asPoint) dst = xValue;
                else dst.SetVector(point, xValue.Unit);
                return;
            case Integer or Float or Percentage or Tag or Dice or GameEventScriptBytecodeTypeKind.Boolean:
                if (xValue.Kind is Dice)
                {
                    if (xValue.ObjectValue is not int[] dice)
                    {
                        dst.SetNothing();
                        return;
                    }

                    if (dice.Length > 0) x = dice[0];
                    if (dice.Length > 1) y = dice[1];
                    if (dice.Length > 2) z = dice[2];
                    break;
                }

                var number = xValue.AsNumeric;
                if (!double.IsFinite(number))
                {
                    dst.SetNothing();
                    return;
                }

                x = number;
                unit = xValue.Kind is Integer or Float ? xValue.Unit : UnitNone;
                break;
            case List when xValue.ObjectValue is GesValue[] list:
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
            case Map when xValue.ObjectValue is GesValueMap map:
            {
                if (map.TryGet("x", out var mapXValue))
                {
                    if (!mapXValue.IsNumeric && mapXValue.Kind is not GameEventScriptBytecodeTypeKind.Boolean)
                    {
                        dst.SetNothing();
                        return;
                    }

                    x = mapXValue.AsNumeric;
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
            case Custom when xValue.ObjectValue is GesCustomObject customObject:
            {
                var map = customObject.Map;
                if (map.TryGet("x", out var mapXValue))
                {
                    if (!mapXValue.IsNumeric && mapXValue.Kind is not GameEventScriptBytecodeTypeKind.Boolean)
                    {
                        dst.SetNothing();
                        return;
                    }

                    x = mapXValue.AsNumeric;
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

                break;
            }
            case GameEventScriptBytecodeTypeKind.Range when xValue.ObjectValue is GesValueRangeInteger range:
            {
                var current = range.From;
                for (var i = 0; i < xValue.IntegerValue && i < 3; i++)
                {
                    if (i == 0) x = current;
                    else if (i == 1) y = current;
                    else z = current;
                    current += range.Step;
                }

                break;
            }
            case GameEventScriptBytecodeTypeKind.Range when xValue.ObjectValue is GesValueRangeFloat range:
            {
                var current = range.From;
                for (var i = 0; i < xValue.IntegerValue && i < 3; i++)
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
    private static void CastTag(ref GesValue dst, in GesValue xValue)
    {
        switch (xValue.Kind)
        {
            case Tag:
                if (GameEventScriptTagRules.IsValidTagName(xValue.TextValue)) dst = xValue;
                else dst.SetNothing();
                return;
            case Text when xValue.ObjectValue is string text:
                if (GameEventScriptTagRules.TryNormalizeTextCast(text, out var tag)) dst.SetTag(tag);
                else dst.SetNothing();
                return;
            case GameEventScriptBytecodeTypeKind.Boolean:
                dst.SetTag(xValue.IsTrue ? "true" : "false");
                return;
            default:
                dst.SetNothing();
                return;
        }
    }
    private static bool IsCustomType(in GesValue value, string typeName)
    {
        if (value.ObjectValue is GesExternalObject externalObject) return string.Equals(externalObject.CustomTypeName, typeName, StringComparison.Ordinal);
        return value.Kind switch
        {
            Custom when value.ObjectValue is GesCustomObject customObject => string.Equals(customObject.TypeName, typeName, StringComparison.Ordinal),
            _ => false
        };
    }
}
