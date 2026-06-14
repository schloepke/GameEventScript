using System;
using System.Collections.Generic;
using System.Globalization;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeInstructionUnit;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmRegisterTypeCastCheck
{
    internal static void GesVmCastUnit(ref this GesVmValue dst, ref GesVmValue xSlot, GameEventScriptBytecodeInstructionUnit unit)
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
                if (xSlot.ObjectValue is GesVmFloatTriplet vector && (unit is UnitNone || xSlot.Unit is UnitNone || xSlot.Unit == unit)) dst.SetVector(vector, unit);
                else dst.SetFloat(double.NaN);
                return;
            case Point:
                if (xSlot.ObjectValue is GesVmFloatTriplet point && (unit is UnitNone || xSlot.Unit is UnitNone || xSlot.Unit == unit)) dst.SetPoint(point, unit);
                else dst.SetFloat(double.NaN);
                return;
            default:
                dst.SetFloat(double.NaN);
                return;
        }
    }
    internal static void GesVmCheckUnit(ref this GesVmValue dst, ref GesVmValue xSlot, GameEventScriptBytecodeInstructionUnit unit)
    {
        dst.SetBoolean(xSlot.Kind is Integer or Float or Vector or Point && xSlot.Unit == unit);
    }
    internal static void GesVmCast(ref this GesVmValue dst, ref GesVmValue xSlot, GameEventScriptBytecodeTypeKind type, GameEventScriptSession? session = null)
    {
        switch (type)
        {
            case Nothing:
                dst.SetNothing();
                return;
            case GameEventScriptBytecodeTypeKind.Boolean:
                xSlot.UpdatedTextTruthinessCache();
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
                CastText(ref dst, ref xSlot);
                return;
            case Tag:
                CastTag(ref dst, ref xSlot);
                return;
            case Vector:
                CastVectorOrPoint(ref dst, ref xSlot, asPoint: false);
                return;
            case Point:
                CastVectorOrPoint(ref dst, ref xSlot, asPoint: true);
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
                CastList(ref dst, ref xSlot, session);
                return;
            case Map:
                CastMap(ref dst, ref xSlot);
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
    internal static void GesVmCastNumeric(ref this GesVmValue dst, ref GesVmValue xSlot)
    {
        if (xSlot.Kind is Text)
        {
            if (double.TryParse(xSlot.ReadTextOrTag(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
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
            dst.GesVmCastNumeric(ref dst);
            return;
        }

        dst.SetFloat(xSlot.AsNumeric, xSlot.Kind is Integer or Float ? xSlot.Unit : UnitNone);
    }
    internal static void GesVmCastCustom(ref this GesVmValue dst, ref GesVmValue xSlot, ushort typeTextPointer, ushort destinationSlot)
    {
        var state = dst.OwningState;
        var typeName = state.Binary.TextConstantTable.Resolve(typeTextPointer);
        if (IsCustomType(ref xSlot, typeName, ref state.Binary.TextConstantTable))
        {
            if (xSlot.ObjectValue is GesVmMapObject typedMap)
            {
                dst.SetRecord(typedMap);
                return;
            }

            if (xSlot.Kind is Custom)
            {
                dst = xSlot;
                return;
            }

            dst.SetNothing();
            return;
        }

        if (xSlot.Kind is Map && xSlot.ObjectValue is GesVmMapObject map)
        {
            for (ushort recordId = 0; recordId < state.RecordConstructors.Length; recordId++)
            {
                var bind = state.RecordConstructors[recordId];
                if (bind.Kind is not GameEventScriptBinaryBindKind.Record ||
                    !string.Equals(state.Binary.TextConstantTable.Resolve(bind.Name), typeName, StringComparison.Ordinal))
                {
                    continue;
                }

                state.ClearStage();
                for (var argumentIndex = 0; argumentIndex < bind.ArgumentNames.Count; argumentIndex++)
                {
                    var argumentName = state.Binary.TextConstantTable.Resolve(bind.ArgumentNames[argumentIndex]);
                    if (map.TryGet(argumentName, out var argument)) state.StageValue(ref argument);
                    else state.StageNothing();
                }

                state.CallRecordConstructor(recordId, destinationSlot);
                return;
            }

            dst.SetNothing();
            return;
        }

        dst.SetNothing();
    }
    internal static void GesVmCheckType(ref this GesVmValue dst, ref GesVmValue xSlot, GameEventScriptBytecodeTypeKind type)
    {
        dst.SetBoolean(type switch
        {
            Nothing => xSlot.IsNothing,
            Map => xSlot.Kind is Map or Custom,
            Invalid or Custom => false,
            _ => xSlot.IsNotNothing && xSlot.Kind == type
        });
    }
    internal static void GesVmCheckNumeric(ref this GesVmValue dst, ref GesVmValue xSlot) => dst.SetBoolean(xSlot.IsNumeric);
    internal static void GesVmCheckInteger(ref this GesVmValue dst, ref GesVmValue xSlot)
    {
        if (!xSlot.IsNumeric)
        {
            dst.SetBoolean(false);
            return;
        }

        var number = xSlot.AsNumeric;
        dst.SetBoolean(double.IsFinite(number) && number is >= long.MinValue and <= long.MaxValue && number == Math.Truncate(number));
    }
    internal static void GesVmCheckFractional(ref this GesVmValue dst, ref GesVmValue xSlot)
    {
        if (!xSlot.IsNumeric)
        {
            dst.SetBoolean(false);
            return;
        }

        var number = xSlot.AsNumeric;
        dst.SetBoolean(double.IsFinite(number) && number != Math.Truncate(number));
    }
    internal static void GesVmCheckCustomType(ref this GesVmValue dst, ref GesVmValue xSlot, ushort typeTextPointer)
    {
        dst.SetBoolean(IsCustomType(ref xSlot, dst.OwningState.Binary.TextConstantTable.Resolve(typeTextPointer), ref dst.OwningState.Binary.TextConstantTable));
    }
    private static void CastText(ref GesVmValue dst, ref GesVmValue xSlot)
    {
        if (xSlot.Kind is Text)
        {
            dst = xSlot;
            return;
        }

        dst.SetText(xSlot.ConvertToText());
    }
    private static void CastList(ref GesVmValue dst, ref GesVmValue xSlot, GameEventScriptSession? session)
    {
        switch (xSlot.Kind)
        {
            case List:
                dst = xSlot;
                return;
            case Text or Tag:
            {
                var text = xSlot.ReadTextOrTag();
                if (text.Length == 0)
                {
                    dst.SetList(dst.OwningState.EmptyList);
                    return;
                }

                var list = dst.OwningState.CreateList(text.Length);
                for (var i = 0; i < text.Length; i++) list[i].SetText(text[i].ToString());
                dst.SetList(list);
                return;
            }
            case Vector or Point when xSlot.ObjectValue is GesVmFloatTriplet triplet:
            {
                var list = dst.OwningState.CreateList(3);
                list[0].SetFloat(triplet.X, xSlot.Unit);
                list[1].SetFloat(triplet.Y, xSlot.Unit);
                list[2].SetFloat(triplet.Z, xSlot.Unit);
                dst.SetList(list);
                return;
            }
            case Dice when xSlot.ObjectValue is int[] dice:
            {
                var list = dst.OwningState.CreateList(dice.Length);
                for (var i = 0; i < dice.Length; i++) list[i].SetInteger(dice[i]);
                dst.SetList(list);
                return;
            }
            case GameEventScriptBytecodeTypeKind.Range when xSlot.ObjectValue is GesVmRange range:
            {
                if (xSlot.IntegerValue > int.MaxValue)
                {
                    dst.SetNothing();
                    return;
                }

                if (session is not null && !session.RuntimeBudget.TryCheckRangeLength(xSlot.IntegerValue, "Range length exceeds the configured limit."))
                {
                    dst.SetList(dst.OwningState.EmptyList);
                    return;
                }

                var list = dst.OwningState.CreateList((int)xSlot.IntegerValue);
                var current = range.from;
                for (var i = 0; i < list.Length; i++)
                {
                    list[i].SetInteger(current);
                    current += range.step;
                }

                dst.SetList(list);
                return;
            }
            case GameEventScriptBytecodeTypeKind.Range when xSlot.ObjectValue is GesVmFloatRange range:
            {
                if (xSlot.IntegerValue > int.MaxValue)
                {
                    dst.SetNothing();
                    return;
                }

                if (session is not null && !session.RuntimeBudget.TryCheckRangeLength(xSlot.IntegerValue, "Range length exceeds the configured limit."))
                {
                    dst.SetList(dst.OwningState.EmptyList);
                    return;
                }

                var list = dst.OwningState.CreateList((int)xSlot.IntegerValue);
                var current = range.from;
                for (var i = 0; i < list.Length; i++)
                {
                    list[i].SetFloat(current);
                    current += range.step;
                }

                dst.SetList(list);
                return;
            }
            default:
                dst.SetList(dst.OwningState.EmptyList);
                return;
        }
    }
    private static void CastMap(ref GesVmValue dst, ref GesVmValue xSlot)
    {
        switch (xSlot.Kind)
        {
            case Map or Custom when xSlot.ObjectValue is GesVmMapObject map:
            {
                var visibleCount = 0;
                foreach (var key in map.Entries.Keys)
                {
                    if (!key.StartsWith("_", StringComparison.Ordinal)) visibleCount++;
                }

                if (visibleCount == map.Entries.Count)
                {
                    dst = xSlot;
                    return;
                }

                var visibleEntries = new Dictionary<string, GesVmValue>(visibleCount, StringComparer.Ordinal);
                foreach (var (key, value) in map.Entries)
                {
                    if (!key.StartsWith("_", StringComparison.Ordinal)) visibleEntries[key] = value;
                }

                dst.SetMap(new GesVmMapObject(dst.OwningState, visibleEntries));
                return;
            }
            case Custom when xSlot.ObjectValue is GameEventScriptValue externalValue:
            {
                var sourceEntries = externalValue.AsMap();
                var entries = new Dictionary<string, GesVmValue>(sourceEntries.Count, StringComparer.Ordinal);
                foreach (var (key, sourceValue) in sourceEntries)
                {
                    if (key.StartsWith("_", StringComparison.Ordinal)) continue;
                    var value = dst.OwningState.CreateNothing();
                    value.BindArguments(sourceValue);
                    entries[key] = value;
                }

                dst.SetMap(new GesVmMapObject(dst.OwningState, entries));
                return;
            }
            case Vector or Point when xSlot.ObjectValue is GesVmFloatTriplet triplet:
            {
                var x = dst.OwningState.CreateNothing();
                x.SetFloat(triplet.X, xSlot.Unit);
                var y = dst.OwningState.CreateNothing();
                y.SetFloat(triplet.Y, xSlot.Unit);
                var z = dst.OwningState.CreateNothing();
                z.SetFloat(triplet.Z, xSlot.Unit);
                dst.SetMap(new GesVmMapObject(dst.OwningState, new Dictionary<string, GesVmValue>(StringComparer.Ordinal)
                {
                    ["x"] = x,
                    ["y"] = y,
                    ["z"] = z
                }));
                return;
            }
            default:
                dst.SetMap(new GesVmMapObject(dst.OwningState, new Dictionary<string, GesVmValue>(StringComparer.Ordinal)));
                return;
        }
    }
    private static void CastVectorOrPoint(ref GesVmValue dst, ref GesVmValue xSlot, bool asPoint)
    {
        var unit = UnitNone;
        double x = 0;
        double y = 0;
        double z = 0;

        switch (xSlot.Kind)
        {
            case Vector when xSlot.ObjectValue is GesVmFloatTriplet vector:
                if (asPoint) dst.SetPoint(vector, xSlot.Unit);
                else dst = xSlot;
                return;
            case Point when xSlot.ObjectValue is GesVmFloatTriplet point:
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
            case Map or Custom when xSlot.ObjectValue is GesVmMapObject map:
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
            case GameEventScriptBytecodeTypeKind.Range when xSlot.ObjectValue is GesVmRange range:
            {
                var current = range.from;
                for (var i = 0; i < xSlot.IntegerValue && i < 3; i++)
                {
                    if (i == 0) x = current;
                    else if (i == 1) y = current;
                    else z = current;
                    current += range.step;
                }

                break;
            }
            case GameEventScriptBytecodeTypeKind.Range when xSlot.ObjectValue is GesVmFloatRange range:
            {
                var current = range.from;
                for (var i = 0; i < xSlot.IntegerValue && i < 3; i++)
                {
                    if (i == 0) x = current;
                    else if (i == 1) y = current;
                    else z = current;
                    current += range.step;
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
    private static void CastTag(ref GesVmValue dst, ref GesVmValue xSlot)
    {
        switch (xSlot.Kind)
        {
            case Tag:
                if (GameEventScriptTagValue.IsValidTagName(xSlot.ReadTextOrTag())) dst = xSlot;
                else dst.SetNothing();
                return;
            case Text when xSlot.IsStoragePointer:
                var pointerText = xSlot.ReadTextOrTag();
                if (GameEventScriptTagValue.TryNormalizeTextCast(pointerText, out var pointerTag))
                {
                    if (pointerTag == pointerText) dst.SetTagPointer((ushort)xSlot.IntegerValue);
                    else dst.SetTag(pointerTag);
                }
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
    private static bool IsCustomType(ref GesVmValue value, string typeName, ref GameEventScriptTextTable textTable)
    {
        if (value.ObjectValue is IGameEventScriptCustomTypeValue custom) return string.Equals(custom.CustomTypeName, typeName, StringComparison.Ordinal);
        return value.Kind switch
        {
            Custom when value.ObjectValue is string customTypeName => string.Equals(customTypeName, typeName, StringComparison.Ordinal),
            Map or Custom when value.ObjectValue is IGesVmKeyAccess<GesVmValue> map && map.TryGet(GameEventScriptValue.HiddenTypeKey, out var marker) => marker.Kind switch
            {
                Tag when marker.IsStoragePointer => string.Equals(textTable.Resolve((ushort)marker.IntegerValue), typeName, StringComparison.Ordinal),
                Tag when marker.ObjectValue is string markerTypeName => string.Equals(markerTypeName, typeName, StringComparison.Ordinal),
                _ => false
            },
            _ => false
        };
    }
}
