using System;
using System.Runtime.CompilerServices;
using System.Globalization;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeInstructionUnit;
using static StepH.GameEventScript.BytecodeExecutor.VmMathConstants;
using static StepH.GameEventScript.BytecodeExecutor.VmRegisterTypeCastCheck.NumericKind;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterTypeCastCheck
{

    internal enum NumericKind : byte
    {
        NumericNone = 0,
        NumericFinite = 1,
        NumericNaN = 2,
        NumericPositiveInfinity = 3,
        NumericNegativeInfinity = 4,
        NumericInvalid = 5,
    }
    
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
    internal static void VmCast(ref this VmValue dst, ref VmValue xSlot, GameEventScriptBytecodeTypeKind type, ref GameEventScriptTextTable textTable)
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
            {
                var numberKind = xSlot.ReadNumeric(ref textTable, out var number);
                switch (numberKind)
                {
                    case NumericFinite when number is >= long.MinValue and <= long.MaxValue:
                        dst.SetInteger((long)number, xSlot.Kind is Integer or Float ? xSlot.Unit : UnitNone);
                        break;
                    case NumericNone:
                        dst.SetNothing();
                        break;
                    default:
                        dst.SetFloat(double.NaN);
                        break;
                }

                return;
            }
            case Float:
            {
                var numberKind = xSlot.ReadNumeric(ref textTable, out var number);
                switch (numberKind)
                {
                    case NumericFinite:
                        dst.SetFloat(number, xSlot.Kind is Integer or Float ? xSlot.Unit : UnitNone);
                        return;
                    case NumericNaN:
                        dst.SetFloat(double.NaN);
                        return;
                    case NumericPositiveInfinity:
                        dst.SetFloat(double.PositiveInfinity);
                        return;
                    case NumericNegativeInfinity:
                        dst.SetFloat(double.NegativeInfinity);
                        return;
                    case NumericInvalid:
                        dst.SetFloat(double.NaN);
                        return;
                    default:
                        dst.SetNothing();
                        return;
                }
            }
            case Percentage:
            {
                var numberKind = xSlot.ReadNumeric(ref textTable, out var number);
                switch (numberKind)
                {
                    case NumericFinite:
                        dst.SetPercentage(number is > 1d or < -1d ? number / 100d : number);
                        break;
                    case NumericNone:
                        dst.SetNothing();
                        break;
                    default:
                        dst.SetFloat(double.NaN);
                        break;
                }

                return;
            }
            case Text:
                CastText(ref dst, ref xSlot);
                return;
            case Tag:
                CastTag(ref dst, ref xSlot);
                return;
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
    internal static void VmCastNumeric(ref this VmValue dst, ref VmValue xSlot, ref GameEventScriptTextTable textTable)
    {
        var numberKind = xSlot.ReadNumeric(ref textTable, out var number);
        switch (numberKind)
        {
            case NumericFinite:
                dst.SetFloat(number, xSlot.Kind is Integer or Float ? xSlot.Unit : UnitNone);
                break;
            case NumericNaN:
                dst.SetFloat(double.NaN);
                break;
            case NumericPositiveInfinity:
                dst.SetFloat(double.PositiveInfinity);
                break;
            case NumericNegativeInfinity:
                dst.SetFloat(double.NegativeInfinity);
                break;
            case NumericInvalid:
                dst.SetFloat(double.NaN);
                break;
            default:
                dst.SetNothing();
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCastCustom(ref this VmValue dst, ref VmValue xSlot, ushort typeTextPointer, ref GameEventScriptTextTable textTable)
    {
        if (IsCustomType(ref xSlot, textTable.Resolve(typeTextPointer), ref textTable))
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
    internal static void VmCheckNumeric(ref this VmValue dst, ref VmValue xSlot, ref GameEventScriptTextTable textTable)
    {
        var numberKind = ReadNumeric(ref xSlot, ref textTable, out _);
        dst.SetBoolean(numberKind != NumericNone && numberKind != NumericInvalid);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCheckInteger(ref this VmValue dst, ref VmValue xSlot, ref GameEventScriptTextTable textTable)
    {
        var numberKind = ReadNumeric(ref xSlot, ref textTable, out var number);
        dst.SetBoolean(numberKind == NumericFinite && number is >= long.MinValue and <= long.MaxValue && number == Math.Truncate(number));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCheckFractional(ref this VmValue dst, ref VmValue xSlot, ref GameEventScriptTextTable textTable)
    {
        var numberKind = ReadNumeric(ref xSlot, ref textTable, out var number);
        dst.SetBoolean(numberKind == NumericFinite && number != Math.Truncate(number));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCheckCustomType(ref this VmValue dst, ref VmValue xSlot, ushort typeTextPointer, ref GameEventScriptTextTable textTable)
    {
        dst.SetBoolean(IsCustomType(ref xSlot, textTable.Resolve(typeTextPointer), ref textTable));
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
                dst = xSlot;
                return;
            case Text when xSlot.IsStoragePointer:
                dst.SetTagPointer((ushort)xSlot.IntegerValue);
                return;
            case Text when xSlot.ObjectValue is string text:
                dst.SetTag(text);
                return;
            case Integer:
                dst.SetTag(xSlot.IntegerValue.ToString(CultureInfo.InvariantCulture));
                return;
            case Float:
                dst.SetTag(xSlot.FloatValue.ToString("R", CultureInfo.InvariantCulture));
                return;
            case Percentage:
                dst.SetTag(xSlot.FloatValue.ToString("R", CultureInfo.InvariantCulture));
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
    internal static NumericKind ReadNumeric(ref this VmValue value, ref GameEventScriptTextTable textTable, out double number)
    {
        switch (value.Kind)
        {
            case Integer:
                number = value.IntegerValue;
                return NumericFinite;
            case Float:
            case Percentage:
                number = value.FloatValue;
                return double.IsNaN(number)
                    ? NumericNaN
                    : double.IsPositiveInfinity(number)
                        ? NumericPositiveInfinity
                        : double.IsNegativeInfinity(number)
                            ? NumericNegativeInfinity
                            : NumericFinite;
            case GameEventScriptBytecodeTypeKind.Boolean:
                number = value.IsTrue ? 1d : 0d;
                return NumericFinite;
            case Text when value.IsStoragePointer:
                return ReadNumericText(textTable.Resolve((ushort)value.IntegerValue), out number);
            case Text when value.ObjectValue is string text:
                return ReadNumericText(text, out number);
            case Tag when value.IsStoragePointer:
                return ReadNumericTag(textTable.Resolve((ushort)value.IntegerValue), out number);
            case Tag when value.ObjectValue is string tag:
                return ReadNumericTag(tag, out number);
            default:
                number = 0d;
                return NumericNone;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static NumericKind ReadNumericText(string text, out double number)
    {
        if (double.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out number))
        {
            return NumericFinite;
        }

        number = 0d;
        return NumericInvalid;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static NumericKind ReadNumericTag(string tag, out double number)
    {
        switch (tag)
        {
            case "infinity":
                number = double.PositiveInfinity;
                return NumericPositiveInfinity;
            case "negativeinfinity":
                number = double.NegativeInfinity;
                return NumericNegativeInfinity;
            case "pi":
                number = GesPi;
                return NumericFinite;
            case "e":
                number = GesEulerNumber;
                return NumericFinite;
            case "tau":
                number = GesTau;
                return NumericFinite;
            case "phi":
                number = GesPhi;
                return NumericFinite;
            default:
                number = 0d;
                return NumericInvalid;
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
