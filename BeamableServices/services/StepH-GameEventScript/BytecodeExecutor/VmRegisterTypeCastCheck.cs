using System;
using System.Runtime.CompilerServices;
using System.Globalization;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeInstructionUnit;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterTypeCastCheck
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCastUnit(ref this VmValue dst, ref VmValue xSlot, GameEventScriptBytecodeInstructionUnit unit)
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
                if (xSlot.ObjectValue is VmFloatTriplet vector && (unit is UnitNone || xSlot.Unit is UnitNone || xSlot.Unit == unit)) dst.SetVector(vector, unit);
                else dst.SetFloat(double.NaN);
                return;
            case Point:
                if (xSlot.ObjectValue is VmFloatTriplet point && (unit is UnitNone || xSlot.Unit is UnitNone || xSlot.Unit == unit)) dst.SetPoint(point, unit);
                else dst.SetFloat(double.NaN);
                return;
            default:
                dst.SetFloat(double.NaN);
                return;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCheckUnit(ref this VmValue dst, ref VmValue xSlot, GameEventScriptBytecodeInstructionUnit unit)
    {
        dst.SetBoolean(xSlot.Kind is Integer or Float or Vector or Point && xSlot.Unit == unit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCast(ref this VmValue dst, ref VmValue xSlot, GameEventScriptBytecodeTypeKind type)
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
                var percentageNumber = xSlot.AsNumeric;
                if (double.IsFinite(percentageNumber)) dst.SetPercentage(percentageNumber is > 1d or < -1d ? percentageNumber / 100d : percentageNumber);
                else dst.SetNothing();
                return;
            case Text:
                CastText(ref dst, ref xSlot);
                return;
            case Tag:
                CastTag(ref dst, ref xSlot);
                return;
            case Vector:
                switch (xSlot.Kind)
                {
                    case Vector:
                        dst = xSlot;
                        return;
                    case Point when xSlot.ObjectValue is VmFloatTriplet point:
                        dst.SetVector(point, xSlot.Unit);
                        return;
                    default:
                        dst.SetNothing();
                        return;
                }
            case Point:
                switch (xSlot.Kind)
                {
                    case Point:
                        dst = xSlot;
                        return;
                    case Vector when xSlot.ObjectValue is VmFloatTriplet vector:
                        dst.SetPoint(vector, xSlot.Unit);
                        return;
                    default:
                        dst.SetNothing();
                        return;
                }
            case Dice:
                switch (xSlot.Kind)
                {
                    case Dice:
                        dst = xSlot;
                        return;
                    case List when xSlot.ObjectValue is VmListObject list:
                    {
                        var dice = new int[list.Length];
                        for (var i = 0; i < list.Length; i++)
                        {
                            var item = list.Items[i];
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
                    case Nothing:
                        dst.SetDice([]);
                        return;
                    default:
                        dst.SetNothing();
                        return;
                }

            case Custom:
            case Invalid:
                dst.SetNothing();
                return;
            default:
                dst = xSlot.Kind == type ? xSlot : default;
                if (xSlot.Kind != type) dst.SetNothing();
                return;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCastNumeric(ref this VmValue dst, ref VmValue xSlot)
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

        dst.SetFloat(xSlot.AsNumeric, xSlot.Kind is Integer or Float ? xSlot.Unit : UnitNone);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCastCustom(ref this VmValue dst, ref VmValue xSlot, ushort typeTextPointer)
    {
        if (IsCustomType(ref xSlot, dst.OwningState.Binary.TextConstantTable.Resolve(typeTextPointer), ref dst.OwningState.Binary.TextConstantTable))
        {
            dst = xSlot;
            return;
        }

        dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCheckType(ref this VmValue dst, ref VmValue xSlot, GameEventScriptBytecodeTypeKind type)
    {
        dst.SetBoolean(type switch
        {
            Nothing => xSlot.IsNothing,
            Invalid or Custom => false,
            _ => xSlot.IsNotNothing && xSlot.Kind == type
        });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCheckNumeric(ref this VmValue dst, ref VmValue xSlot) => dst.SetBoolean(xSlot.IsNumeric);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCheckInteger(ref this VmValue dst, ref VmValue xSlot)
    {
        if (!xSlot.IsNumeric)
        {
            dst.SetBoolean(false);
            return;
        }

        var number = xSlot.AsNumeric;
        dst.SetBoolean(double.IsFinite(number) && number is >= long.MinValue and <= long.MaxValue && number == Math.Truncate(number));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCheckFractional(ref this VmValue dst, ref VmValue xSlot)
    {
        if (!xSlot.IsNumeric)
        {
            dst.SetBoolean(false);
            return;
        }

        var number = xSlot.AsNumeric;
        dst.SetBoolean(double.IsFinite(number) && number != Math.Truncate(number));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCheckCustomType(ref this VmValue dst, ref VmValue xSlot, ushort typeTextPointer)
    {
        dst.SetBoolean(IsCustomType(ref xSlot, dst.OwningState.Binary.TextConstantTable.Resolve(typeTextPointer), ref dst.OwningState.Binary.TextConstantTable));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CastText(ref VmValue dst, ref VmValue xSlot)
    {
        switch (xSlot.Kind)
        {
            case Text:
                dst = xSlot;
                return;
            case Tag when xSlot.IsStoragePointer:
                dst.SetTextPointer((ushort)xSlot.IntegerValue);
                return;
            case Tag when xSlot.ObjectValue is string tag:
                dst.SetText(tag);
                return;
            case Integer:
                dst.SetText(xSlot.IntegerValue.ToString(CultureInfo.InvariantCulture));
                return;
            case Float:
                dst.SetText(xSlot.FloatValue.ToString("R", CultureInfo.InvariantCulture));
                return;
            case Percentage:
                dst.SetText(xSlot.FloatValue.ToString("R", CultureInfo.InvariantCulture));
                return;
            case GameEventScriptBytecodeTypeKind.Boolean:
                dst.SetText(xSlot.IsTrue ? "true" : "false");
                return;
            default:
                dst.SetNothing();
                return;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CastTag(ref VmValue dst, ref VmValue xSlot)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsCustomType(ref VmValue value, string typeName, ref GameEventScriptTextTable textTable)
    {
        if (value.ObjectValue is IGameEventScriptCustomTypeValue custom) return string.Equals(custom.CustomTypeName, typeName, StringComparison.Ordinal);
        return value.Kind switch
        {
            Custom when value.ObjectValue is string customTypeName => string.Equals(customTypeName, typeName, StringComparison.Ordinal),
            Map when value.ObjectValue is IVmKeyAccess<VmValue> map && map.TryGet(GameEventScriptValue.HiddenTypeKey, out var marker) => marker.Kind switch
            {
                Tag when marker.IsStoragePointer => string.Equals(textTable.Resolve((ushort)marker.IntegerValue), typeName, StringComparison.Ordinal),
                Tag when marker.ObjectValue is string markerTypeName => string.Equals(markerTypeName, typeName, StringComparison.Ordinal),
                _ => false
            },
            _ => false
        };
    }
}
