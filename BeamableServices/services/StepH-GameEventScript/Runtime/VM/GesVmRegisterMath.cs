using System;
using System.Diagnostics.CodeAnalysis;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeInstructionUnit;
using static StepH.GameEventScript.Runtime.VM.GesVmUnitCalculation;
using StepH.GameEventScript.Runtime.Values;

namespace StepH.GameEventScript.Runtime.VM;

[SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
internal static class GesVmRegisterMath
{
    internal static void GesVmAdd(this GesVmState vmState, ushort destinationRegister, in GesValue a, in GesValue b)
    {
        var dst = GesVmAdd(in a, in b, vmState);
        vmState.SetValue(destinationRegister, in dst);
    }
    internal static GesValue GesVmAdd(in GesValue a, in GesValue b, GesVmState state)
    {
        var dst = new GesValue();
        switch (a.Kind)
        {
            case Integer when b.Kind is Integer:
                if (SameUnit(in a, in b) is { } unit)
                {
                    if (GameEventScriptNumber.AddExact(a.IntegerValue, b.IntegerValue) is { } integerResult) dst.SetInteger(integerResult, unit);
                    else dst.SetFloat((double)a.IntegerValue + b.IntegerValue, unit);
                }
                else dst.SetFloat(double.NaN);
                break;
            case Float when b.Kind is Float:
                if (SameUnit(in a, in b) is { } floatUnit) dst.SetFloat(a.FloatValue + b.FloatValue, floatUnit);
                else dst.SetFloat(double.NaN);
                break;
            case Float or Integer when b.Kind is Float or Integer:
                if (SameUnit(in a, in b) is { } mixedUnit) dst.SetFloat(a.AsNumeric + b.AsNumeric, mixedUnit);
                else dst.SetFloat(double.NaN);
                break;
            case Integer when b.Kind is Percentage:
                dst.SetFloat(a.IntegerValue + a.IntegerValue * b.FloatValue, a.Unit);
                break;
            case Float when b.Kind is Percentage:
                dst.SetFloat(a.FloatValue + a.FloatValue * b.FloatValue, a.Unit);
                break;
            case Percentage when b.Kind is Percentage:
                dst.SetPercentage(a.FloatValue + b.FloatValue);
                break;
            case Vector when b.Kind is Vector:
                if (a.ObjectValue is GesValueVectorPoint av && b.ObjectValue is GesValueVectorPoint bv && SameUnit(in a, in b) is { } vectorUnit) dst.SetVector(av.X + bv.X, av.Y + bv.Y, av.Z + bv.Z, vectorUnit);
                else dst.SetFloat(double.NaN);
                break;
            case Point when b.Kind is Vector:
                if (a.ObjectValue is GesValueVectorPoint ap && b.ObjectValue is GesValueVectorPoint bvv && SameUnit(in a, in b) is { } pointUnit) dst.SetPoint(ap.X + bvv.X, ap.Y + bvv.Y, ap.Z + bvv.Z, pointUnit);
                else dst.SetFloat(double.NaN);
                break;
            case Integer or Float or Percentage when b.Kind is Vector or Point:
                dst.SetFloat(double.NaN);
                break;
            case Text when b.Kind is not Nothing:
                dst.SetText(a.TextValue + b.ToText);
                break;
            case not Nothing when b.Kind is Text:
                dst.SetText(a.ToText + b.TextValue);
                break;
            case List when b.Kind is not Nothing && a.ObjectValue is GesValue[] aList:
            {
                var list = new GesValue[aList.Length + 1];
                for (var i = 0; i < aList.Length; i++) list[i] = aList[i];
                list[aList.Length] = b;
                dst.SetList(list);
                break;
            }
            case not List and not Nothing when b.Kind is List && b.ObjectValue is GesValue[] rightList:
            {
                var list = new GesValue[rightList.Length + 1];
                list[0] = a;
                for (var i = 0; i < rightList.Length; i++) list[i + 1] = rightList[i];
                dst.SetList(list);
                break;
            }
            case Dice when b.Kind is Integer && b.Unit is UnitNone && b.IntegerValue is > 0 and <= int.MaxValue && a.ObjectValue is int[] aDice:
            {
                var dice = new int[aDice.Length + 1];
                Array.Copy(aDice, dice, aDice.Length);
                dice[aDice.Length] = (int)b.IntegerValue;
                dst.SetDice(dice);
                break;
            }
            case Integer when a.Unit is UnitNone && a.IntegerValue is > 0 and <= int.MaxValue && b.Kind is Dice && b.ObjectValue is int[] bDice:
            {
                var dice = new int[bDice.Length + 1];
                dice[0] = (int)a.IntegerValue;
                Array.Copy(bDice, 0, dice, 1, bDice.Length);
                dst.SetDice(dice);
                break;
            }
            case Percentage:
            case Vector:
            case Point:
                if (b.Kind is not Nothing) dst.SetFloat(double.NaN);
                else dst.SetNothing();
                break;
            case Dice when b.Kind is not List:
            case not List and not Map and not Nothing when b.Kind is Map or Dice:
            case Nothing:
                dst.SetNothing();
                break;
            default:
                if (b.Kind is Nothing)
                {
                    dst.SetNothing();
                    break;
                }

                var aNum = a.AsNumeric;
                if (double.IsNaN(aNum))
                {
                    dst.SetFloat(double.NaN);
                }
                else
                {
                    var bNum = b.AsNumeric;
                    if (double.IsNaN(bNum)) dst.SetFloat(double.NaN);
                    else if (b.Kind is Percentage) dst.SetFloat(aNum + aNum * bNum, a.Unit);
                    else if (SameUnit(in a, in b) is { } fallbackUnit) dst.SetFloat(aNum + bNum, fallbackUnit);
                    else dst.SetFloat(double.NaN);
                }

                break;
        }
        return dst;
    }
    internal static void GesVmSubtract(this GesVmState vmState, ushort dst, in GesValue a, in GesValue b)
    {
        switch (a.Kind)
        {
            case Integer when b.Kind is Integer:
                if (SameUnit(in a, in b) is { } unit)
                {
                    if (GameEventScriptNumber.SubtractExact(a.IntegerValue, b.IntegerValue) is { } integerResult) vmState.SetInteger(dst, integerResult, unit);
                    else vmState.SetFloat(dst, (double)a.IntegerValue - b.IntegerValue, unit);
                }
                else vmState.SetFloat(dst, double.NaN);
                return;
            case Float when b.Kind is Float:
                if (SameUnit(in a, in b) is { } floatUnit) vmState.SetFloat(dst, a.FloatValue - b.FloatValue, floatUnit);
                else vmState.SetFloat(dst, double.NaN);
                return;
            case Float or Integer when b.Kind is Float or Integer:
                if (SameUnit(in a, in b) is { } mixedUnit) vmState.SetFloat(dst, a.AsNumeric - b.AsNumeric, mixedUnit);
                else vmState.SetFloat(dst, double.NaN);
                return;
            case Integer when b.Kind is Percentage:
                vmState.SetFloat(dst, a.IntegerValue - a.IntegerValue * b.FloatValue, a.Unit);
                return;
            case Float when b.Kind is Percentage:
                vmState.SetFloat(dst, a.FloatValue - a.FloatValue * b.FloatValue, a.Unit);
                return;
            case Percentage when b.Kind is Percentage:
                vmState.SetPercentage(dst, a.FloatValue - b.FloatValue);
                return;
            case Vector when b.Kind is Vector:
            {
                if (a.ObjectValue is GesValueVectorPoint av && b.ObjectValue is GesValueVectorPoint bv && SameUnit(in a, in b) is { } vectorUnit) vmState.SetVector(dst, av.X - bv.X, av.Y - bv.Y, av.Z - bv.Z, vectorUnit);
                else vmState.SetFloat(dst, double.NaN);
                return;
            }
            case Vector:
                if (b.Kind is not Nothing) vmState.SetFloat(dst, double.NaN);
                else vmState.SetNothing(dst);
                return;
            case Point when b.Kind is Vector:
            {
                if (a.ObjectValue is GesValueVectorPoint ap && b.ObjectValue is GesValueVectorPoint bv && SameUnit(in a, in b) is { } pointUnit) vmState.SetPoint(dst, ap.X - bv.X, ap.Y - bv.Y, ap.Z - bv.Z, pointUnit);
                else vmState.SetFloat(dst, double.NaN);
                return;
            }
            case Point when b.Kind is Point:
            {
                if (a.ObjectValue is GesValueVectorPoint ap && b.ObjectValue is GesValueVectorPoint bp && SameUnit(in a, in b) is { } pointUnit) vmState.SetVector(dst, ap.X - bp.X, ap.Y - bp.Y, ap.Z - bp.Z, pointUnit);
                else vmState.SetFloat(dst, double.NaN);
                return;
            }
            case Point:
                if (b.Kind is not Nothing) vmState.SetFloat(dst, double.NaN);
                else vmState.SetNothing(dst);
                return;
            case Integer or Float or Percentage when b.Kind is Vector or Point:
                vmState.SetFloat(dst, double.NaN);
                return;
            case List when b.Kind is List or Dice and not Nothing or not Map && a.ObjectValue is GesValue[] aList:
            {
                var removeList = b.Kind is List && b.ObjectValue is GesValue[] bList ? bList : null;
                var removeDice = b.Kind is Dice && b.ObjectValue is int[] bDice ? bDice : null;
                var removeCount = removeList?.Length ?? removeDice?.Length ?? 1;
                var removed = new bool[removeCount];
                var resultLength = 0;
                for (var i = 0; i < aList.Length; i++)
                {
                    var shouldRemove = false;
                    for (var j = 0; j < removeCount; j++)
                    {
                        if (removed[j]) continue;
                        var candidate = removeList is not null ? removeList[j] : b;
                        if (removeList is null && removeDice is not null)
                        {
                            candidate = new GesValue();
                            candidate.SetInteger(removeDice[j]);
                        }
                        if (!candidate.EqualsValue(in aList[i])) continue;
                        removed[j] = true;
                        shouldRemove = true;
                        break;
                    }

                    if (!shouldRemove) resultLength++;
                }

                var list = new GesValue[resultLength];
                var index = 0;
                Array.Clear(removed, 0, removed.Length);
                for (var i = 0; i < aList.Length; i++)
                {
                    var shouldRemove = false;
                    for (var j = 0; j < removeCount; j++)
                    {
                        if (removed[j]) continue;
                        var candidate = removeList is not null ? removeList[j] : b;
                        if (removeList is null && removeDice is not null)
                        {
                            candidate = new GesValue();
                            candidate.SetInteger(removeDice[j]);
                        }
                        if (!candidate.EqualsValue(in aList[i])) continue;
                        removed[j] = true;
                        shouldRemove = true;
                        break;
                    }

                    if (!shouldRemove) list[index++] = aList[i];
                }

                vmState.SetList(dst, list);
                return;
            }
            case Dice when b.Kind is Dice && a.ObjectValue is int[] aDice && b.ObjectValue is int[] bDice:
            {
                var removed = new bool[bDice.Length];
                var resultLength = 0;
                for (var i = 0; i < aDice.Length; i++)
                {
                    var shouldRemove = false;
                    for (var j = 0; j < bDice.Length; j++)
                    {
                        if (removed[j] || aDice[i] != bDice[j]) continue;
                        removed[j] = true;
                        shouldRemove = true;
                        break;
                    }

                    if (!shouldRemove) resultLength++;
                }

                var dice = new int[resultLength];
                var index = 0;
                Array.Clear(removed, 0, removed.Length);
                for (var i = 0; i < aDice.Length; i++)
                {
                    var shouldRemove = false;
                    for (var j = 0; j < bDice.Length; j++)
                    {
                        if (removed[j] || aDice[i] != bDice[j]) continue;
                        removed[j] = true;
                        shouldRemove = true;
                        break;
                    }

                    if (!shouldRemove) dice[index++] = aDice[i];
                }

                vmState.SetDice(dst, dice);
                return;
            }
            case Dice when b.Kind is List && a.ObjectValue is int[] aDice && b.ObjectValue is GesValue[] removeList:
            {
                var removed = new bool[removeList.Length];
                var resultLength = 0;
                for (var i = 0; i < aDice.Length; i++)
                {
                    var item = new GesValue();
                    item.SetInteger(aDice[i]);
                    var shouldRemove = false;
                    for (var j = 0; j < removeList.Length; j++)
                    {
                        if (removed[j] || !item.EqualsValue(in removeList[j])) continue;
                        removed[j] = true;
                        shouldRemove = true;
                        break;
                    }

                    if (!shouldRemove) resultLength++;
                }

                var list = new GesValue[resultLength];
                var index = 0;
                Array.Clear(removed, 0, removed.Length);
                for (var i = 0; i < aDice.Length; i++)
                {
                    var item = new GesValue();
                    item.SetInteger(aDice[i]);
                    var shouldRemove = false;
                    for (var j = 0; j < removeList.Length; j++)
                    {
                        if (removed[j] || !item.EqualsValue(in removeList[j])) continue;
                        removed[j] = true;
                        shouldRemove = true;
                        break;
                    }

                    if (!shouldRemove) list[index++].SetInteger(aDice[i]);
                }

                vmState.SetList(dst, list);
                return;
            }
            case Dice when b.Kind is Integer && b.Unit is UnitNone && a.ObjectValue is int[] aDice && b.IntegerValue is > 0 and <= int.MaxValue:
            {
                var roll = (int)b.IntegerValue;
                var removed = false;
                var resultLength = 0;
                for (var i = 0; i < aDice.Length; i++)
                {
                    if (!removed && aDice[i] == roll)
                    {
                        removed = true;
                        continue;
                    }

                    resultLength++;
                }

                var dice = new int[resultLength];
                var index = 0;
                removed = false;
                for (var i = 0; i < aDice.Length; i++)
                {
                    if (!removed && aDice[i] == roll)
                    {
                        removed = true;
                        continue;
                    }

                    dice[index++] = aDice[i];
                }

                vmState.SetDice(dst, dice);
                return;
            }
            case Map when b.Kind is not Nothing && a.ObjectValue is GesValueMap aMap:
            {
                string? singleKey = null;
                string[]? keys = null;
                GesValueMap? bMap = null;
                if (b.Kind is Map && b.ObjectValue is GesValueMap rightMap)
                {
                    bMap = rightMap;
                    // Keys are already sorted in GesValueMap.
                }
                else if (b.Kind is Text or Tag)
                {
                    singleKey = b.TextValue;
                }
                else if (b.Kind is List && b.ObjectValue is GesValue[] keyList)
                {
                    keys = new string[keyList.Length];
                    for (var i = 0; i < keyList.Length; i++)
                    {
                        var keyValue = keyList[i];
                        if (keyValue.Kind is not (Text or Tag))
                        {
                            vmState.SetNothing(dst);
                            return;
                        }

                        keys[i] = keyValue.TextValue;
                    }

                    Array.Sort(keys, GameEventScriptText.ScalarComparer);
                }
                else
                {
                    vmState.SetNothing(dst);
                    return;
                }

                var map = new GesVmMapBuilder(aMap.StorageLength);
                if (bMap is not null)
                {
                    var bi = 0;
                    for (var ai = 0; ai < aMap.StorageLength; ai++)
                    {
                        var key = aMap.KeyAt(ai);
                        while (bi < bMap.StorageLength && GameEventScriptText.CompareScalarOrdinal(bMap.KeyAt(bi), key) < 0) bi++;
                        if (bi < bMap.StorageLength && string.Equals(bMap.KeyAt(bi), key, StringComparison.Ordinal)) continue;
                        map.Set(key, aMap.ValueAt(ai));
                    }
                }
                else if (keys is not null)
                {
                    var ki = 0;
                    for (var ai = 0; ai < aMap.StorageLength; ai++)
                    {
                        var key = aMap.KeyAt(ai);
                        while (ki < keys.Length && GameEventScriptText.CompareScalarOrdinal(keys[ki], key) < 0) ki++;
                        if (ki < keys.Length && string.Equals(keys[ki], key, StringComparison.Ordinal)) continue;
                        map.Set(key, aMap.ValueAt(ai));
                    }
                }
                else
                {
                    for (var ai = 0; ai < aMap.StorageLength; ai++)
                    {
                        var key = aMap.KeyAt(ai);
                        if (string.Equals(key, singleKey, StringComparison.Ordinal)) continue;
                        map.Set(key, aMap.ValueAt(ai));
                    }
                }

                vmState.SetMap(dst, map.ToMap());
                return;
            }
            case not List and not Map and not Nothing when b.Kind is List or Map:
                vmState.SetNothing(dst);
                return;
            case Percentage:
                if (b.Kind is not Nothing) vmState.SetFloat(dst, double.NaN);
                else vmState.SetNothing(dst);
                break;
            case Dice:
                vmState.SetNothing(dst);
                break;
            case Nothing:
                vmState.SetNothing(dst);
                return;
            default:
                if (b.Kind is Nothing)
                {
                    vmState.SetNothing(dst);
                }
                else
                {
                    var aNum = a.AsNumeric;
                    if (double.IsNaN(aNum))
                    {
                        vmState.SetFloat(dst, double.NaN);
                    }
                    else
                    {
                        var bNum = b.AsNumeric;
                        if (double.IsNaN(bNum)) vmState.SetFloat(dst, double.NaN);
                        else if (b.Kind is Percentage) vmState.SetFloat(dst, aNum - aNum * bNum, a.Unit);
                        else if (SameUnit(in a, in b) is { } fallbackUnit) vmState.SetFloat(dst, aNum - bNum, fallbackUnit);
                        else vmState.SetFloat(dst, double.NaN);
                    }
                }

                break;
        }
    }
    internal static void GesVmMultiply(this GesVmState vmState, ushort dst, in GesValue a, in GesValue b)
    {
        switch (a.Kind)
        {
            case Integer when b.Kind is Integer:
                if (ProductUnit(in a, in b) is { } unit)
                {
                    if (GameEventScriptNumber.MultiplyExact(a.IntegerValue, b.IntegerValue) is { } integerResult) vmState.SetInteger(dst, integerResult, unit);
                    else vmState.SetFloat(dst, (double)a.IntegerValue * b.IntegerValue, unit);
                }
                else vmState.SetFloat(dst, double.NaN);
                return;
            case Float when b.Kind is Float:
                if (ProductUnit(in a, in b) is { } floatUnit) vmState.SetFloat(dst, a.FloatValue * b.FloatValue, floatUnit);
                else vmState.SetFloat(dst, double.NaN);
                return;
            case Float or Integer when b.Kind is Float or Integer:
                if (ProductUnit(in a, in b) is { } mixedUnit) vmState.SetFloat(dst, a.AsNumeric * b.AsNumeric, mixedUnit);
                else vmState.SetFloat(dst, double.NaN);
                return;
            case Integer when b.Kind is Percentage:
                vmState.SetFloat(dst, a.IntegerValue * b.FloatValue, a.Unit);
                return;
            case Float when b.Kind is Percentage:
                vmState.SetFloat(dst, a.FloatValue * b.FloatValue, a.Unit);
                return;
            case Percentage when b.Kind is Percentage:
                vmState.SetPercentage(dst, a.FloatValue * b.FloatValue);
                return;
            case Percentage when b.Kind is Integer:
                vmState.SetFloat(dst, a.FloatValue * b.IntegerValue, b.Unit);
                return;
            case Percentage when b.Kind is Float:
                vmState.SetFloat(dst, a.FloatValue * b.FloatValue, b.Unit);
                return;
            case Percentage when b.Kind is Vector:
                if (b.ObjectValue is GesValueVectorPoint percentageVector) vmState.SetVector(dst, a.FloatValue * percentageVector.X, a.FloatValue * percentageVector.Y, a.FloatValue * percentageVector.Z, b.Unit);
                else vmState.SetFloat(dst, double.NaN);
                return;
            case Percentage when b.Kind is Point:
                vmState.SetFloat(dst, double.NaN);
                return;
            case Vector when b.Kind is Integer:
                if (a.ObjectValue is GesValueVectorPoint vectorInt && ProductUnit(in a, in b) is { } vectorIntegerUnit) vmState.SetVector(dst, vectorInt.X * b.IntegerValue, vectorInt.Y * b.IntegerValue, vectorInt.Z * b.IntegerValue, vectorIntegerUnit);
                else vmState.SetFloat(dst, double.NaN);
                return;
            case Vector when b.Kind is Float:
                if (a.ObjectValue is GesValueVectorPoint vectorFloat && double.IsFinite(b.FloatValue) && ProductUnit(in a, in b) is { } vectorFloatUnit) vmState.SetVector(dst, vectorFloat.X * b.FloatValue, vectorFloat.Y * b.FloatValue, vectorFloat.Z * b.FloatValue, vectorFloatUnit);
                else vmState.SetFloat(dst, double.NaN);
                return;
            case Vector when b.Kind is Percentage:
                if (a.ObjectValue is GesValueVectorPoint vectorPercent) vmState.SetVector(dst, vectorPercent.X * b.FloatValue, vectorPercent.Y * b.FloatValue, vectorPercent.Z * b.FloatValue, a.Unit);
                else vmState.SetFloat(dst, double.NaN);
                return;
            case Vector when b.Kind is Nothing:
                vmState.SetNothing(dst);
                return;
            case Vector:
                var vectorScalar = b.AsNumeric;
                if (a.ObjectValue is GesValueVectorPoint vector && !double.IsNaN(vectorScalar) && double.IsFinite(vectorScalar) && ProductUnit(in a, in b) is { } vectorScalarUnit)
                    vmState.SetVector(dst, vector.X * vectorScalar, vector.Y * vectorScalar, vector.Z * vectorScalar, vectorScalarUnit);
                else vmState.SetFloat(dst, double.NaN);
                return;
            case Point when b.Kind is Nothing:
                vmState.SetNothing(dst);
                return;
            case Point:
                vmState.SetFloat(dst, double.NaN);
                return;
            case Integer or Float when b.Kind is Vector:
                var scalar = a.AsNumeric;
                if (b.ObjectValue is GesValueVectorPoint rightVector && double.IsFinite(scalar) && ProductUnit(in a, in b) is { } rightVectorUnit) vmState.SetVector(dst, scalar * rightVector.X, scalar * rightVector.Y, scalar * rightVector.Z, rightVectorUnit);
                else vmState.SetFloat(dst, double.NaN);
                return;
            case Integer or Float when b.Kind is Point:
                vmState.SetFloat(dst, double.NaN);
                return;
            case Percentage:
                if (b.Kind is Nothing)
                {
                    vmState.SetNothing(dst);
                    return;
                }

                var percentageScalar = b.AsNumeric;
                if (double.IsNaN(percentageScalar))
                {
                    vmState.SetFloat(dst, double.NaN);
                    return;
                }

                var percentageResult = a.FloatValue * percentageScalar;
                if (b.HasUnit) vmState.SetFloat(dst, percentageResult, b.Unit);
                else vmState.SetFloat(dst, percentageResult);
                return;
            case Nothing:
                vmState.SetNothing(dst);
                return;
        }

        if (b.Kind is Nothing)
        {
            vmState.SetNothing(dst);
            return;
        }

        var fallbackLeft = a.AsNumeric;
        if (double.IsNaN(fallbackLeft))
        {
            vmState.SetFloat(dst, double.NaN);
            return;
        }

        switch (b.Kind)
        {
            case Vector:
            {
                if (b.ObjectValue is GesValueVectorPoint vector && double.IsFinite(fallbackLeft) && ProductUnit(in a, in b) is { } vectorUnit) vmState.SetVector(dst, fallbackLeft * vector.X, fallbackLeft * vector.Y, fallbackLeft * vector.Z, vectorUnit);
                else vmState.SetFloat(dst, double.NaN);
                return;
            }
            case Point:
            {
                vmState.SetFloat(dst, double.NaN);
                return;
            }
        }

        var bNum = b.AsNumeric;
        if (double.IsNaN(bNum) || ProductUnit(in a, in b) is not { } fallbackUnit)
        {
            vmState.SetFloat(dst, double.NaN);
            return;
        }

        vmState.SetFloat(dst, fallbackLeft * bNum, b.Kind is Percentage ? a.Unit : fallbackUnit);
    }
    internal static void GesVmDivide(this GesVmState vmState, ushort destinationRegister, in GesValue a, in GesValue b)
    {
        var dst = GesVmDivide(in a, in b, vmState);
        vmState.SetValue(destinationRegister, in dst);
    }
    internal static GesValue GesVmDivide(in GesValue a, in GesValue b, GesVmState state)
    {
        var dst = new GesValue();
        switch (a.Kind)
        {
            case Integer when b.Kind is Integer:
                if (QuotientUnit(in a, in b) is { } unit) dst.SetFloat((double)a.IntegerValue / b.IntegerValue, unit);
                else dst.SetFloat(double.NaN);
                return dst;
            case Float when b.Kind is Float:
                if (QuotientUnit(in a, in b) is { } floatUnit) dst.SetFloat(a.FloatValue / b.FloatValue, floatUnit);
                else dst.SetFloat(double.NaN);
                return dst;
            case Float or Integer when b.Kind is Float or Integer:
                if (QuotientUnit(in a, in b) is { } mixedUnit) dst.SetFloat(a.AsNumeric / b.AsNumeric, mixedUnit);
                else dst.SetFloat(double.NaN);
                return dst;
            case Percentage when b.Kind is Percentage:
                dst.SetFloat(a.FloatValue / b.FloatValue);
                return dst;
            case Percentage when b.Kind is Integer or Float:
                if (b.HasUnit || QuotientUnit(in a, in b) is not { } percentageUnit)
                {
                    dst.SetFloat(double.NaN);
                    return dst;
                }

                dst.SetPercentage(a.FloatValue / b.AsNumeric);
                return dst;
            case Integer or Float when b.Kind is Percentage:
                if (QuotientUnit(in a, in b) is { } percentageRightUnit) dst.SetFloat(a.AsNumeric / b.FloatValue, percentageRightUnit);
                else dst.SetFloat(double.NaN);
                return dst;
            case Vector when b.Kind is Integer:
                if (a.ObjectValue is GesValueVectorPoint vectorInt && b.IntegerValue != 0 && QuotientUnit(in a, in b) is { } vectorIntegerUnit) dst.SetVector(vectorInt.X / b.IntegerValue, vectorInt.Y / b.IntegerValue, vectorInt.Z / b.IntegerValue, vectorIntegerUnit);
                else dst.SetFloat(double.NaN);
                return dst;
            case Vector when b.Kind is Float:
                if (a.ObjectValue is GesValueVectorPoint vectorFloat && double.IsFinite(b.FloatValue) && b.FloatValue != 0d && QuotientUnit(in a, in b) is { } vectorFloatUnit)
                    dst.SetVector(vectorFloat.X / b.FloatValue, vectorFloat.Y / b.FloatValue, vectorFloat.Z / b.FloatValue, vectorFloatUnit);
                else dst.SetFloat(double.NaN);
                return dst;
            case Vector when b.Kind is Percentage:
                if (a.ObjectValue is GesValueVectorPoint vectorPercent && b.FloatValue != 0d) dst.SetVector(vectorPercent.X / b.FloatValue, vectorPercent.Y / b.FloatValue, vectorPercent.Z / b.FloatValue, a.Unit);
                else dst.SetFloat(double.NaN);
                return dst;
            case Vector when b.Kind is Nothing:
                dst.SetNothing();
                return dst;
            case Vector:
                var vectorDivisor = b.AsNumeric;
                if (a.ObjectValue is GesValueVectorPoint vector && !double.IsNaN(vectorDivisor) && double.IsFinite(vectorDivisor) && vectorDivisor != 0d && QuotientUnit(in a, in b) is { } vectorDivisorUnit)
                    dst.SetVector(vector.X / vectorDivisor, vector.Y / vectorDivisor, vector.Z / vectorDivisor, vectorDivisorUnit);
                else dst.SetFloat(double.NaN);
                return dst;
            case Point when b.Kind is Nothing:
                dst.SetNothing();
                return dst;
            case Point:
                dst.SetFloat(double.NaN);
                return dst;
            case Integer or Float or Percentage when b.Kind is Vector or Point:
                dst.SetFloat(double.NaN);
                return dst;
            case Percentage:
                if (b.Kind is Nothing)
                {
                    dst.SetNothing();
                    return dst;
                }

                var percentageDivisor = b.AsNumeric;
                if (double.IsNaN(percentageDivisor) || b.HasUnit || QuotientUnit(in a, in b) is not { } percentageFallbackUnit)
                {
                    dst.SetFloat(double.NaN);
                    return dst;
                }

                dst.SetPercentage(a.FloatValue / percentageDivisor);
                return dst;
            case Nothing:
                dst.SetNothing();
                return dst;
        }

        if (b.Kind is Nothing)
        {
            dst.SetNothing();
            return dst;
        }

        if (b.Kind is Vector or Point)
        {
            dst.SetFloat(double.NaN);
            return dst;
        }

        var aNum = a.AsNumeric;
        if (double.IsNaN(aNum))
        {
            dst.SetFloat(double.NaN);
            return dst;
        }

        var bNum = b.AsNumeric;
        if (double.IsNaN(bNum))
        {
            dst.SetFloat(double.NaN);
            return dst;
        }

        if (a.Kind is Percentage && b.HasUnit)
        {
            dst.SetFloat(double.NaN);
            return dst;
        }

        if (QuotientUnit(in a, in b) is not { } fallbackUnit)
        {
            dst.SetFloat(double.NaN);
            return dst;
        }

        var fallbackResult = aNum / bNum;
        if (a.Kind is Percentage) dst.SetPercentage(fallbackResult);
        else dst.SetFloat(fallbackResult, fallbackUnit);
        return dst;
    }
    internal static void GesVmPower(this GesVmState vmState, ushort destinationRegister, in GesValue a, in GesValue b)
    {
        var dst = GesVmPower(in a, in b, vmState);
        vmState.SetValue(destinationRegister, in dst);
    }
    private static GesValue GesVmPower(in GesValue a, in GesValue b, GesVmState state)
    {
        var dst = new GesValue();
        switch (a.Kind)
        {
            case Integer when b.Kind is Integer:
                if (b.HasUnit)
                {
                    dst.SetFloat(double.NaN);
                    return dst;
                }

                var integerUnit = UnitNone;
                if (a.HasUnit)
                {
                    if (b.IntegerValue == 1) integerUnit = a.Unit;
                    else if (b.IntegerValue != 0)
                    {
                        dst.SetFloat(double.NaN);
                        return dst;
                    }
                }

                dst.SetFloat(Math.Pow(a.IntegerValue, b.IntegerValue), integerUnit);
                return dst;
            case Float when b.Kind is Float:
                if (b.HasUnit)
                {
                    dst.SetFloat(double.NaN);
                    return dst;
                }

                var floatUnit = UnitNone;
                if (a.HasUnit)
                {
                    if (b.FloatValue == 1d) floatUnit = a.Unit;
                    else if (b.FloatValue != 0d)
                    {
                        dst.SetFloat(double.NaN);
                        return dst;
                    }
                }

                dst.SetFloat(Math.Pow(a.FloatValue, b.FloatValue), floatUnit);
                return dst;
            case Float or Integer when b.Kind is Float or Integer:
                var rightNumber = b.AsNumeric;
                if (b.HasUnit)
                {
                    dst.SetFloat(double.NaN);
                    return dst;
                }

                var mixedUnit = UnitNone;
                if (a.HasUnit)
                {
                    if (rightNumber == 1d) mixedUnit = a.Unit;
                    else if (rightNumber != 0d)
                    {
                        dst.SetFloat(double.NaN);
                        return dst;
                    }
                }

                dst.SetFloat(Math.Pow(a.AsNumeric, rightNumber), mixedUnit);
                return dst;
            case Nothing:
                dst.SetNothing();
                return dst;
        }

        if (b.Kind is Nothing)
        {
            dst.SetNothing();
            return dst;
        }

        if (b.HasUnit)
        {
            dst.SetFloat(double.NaN);
            return dst;
        }

        var right = b.AsNumeric;
        if (double.IsNaN(right))
        {
            dst.SetFloat(double.NaN);
            return dst;
        }

        var unit = UnitNone;
        if (a.HasUnit)
        {
            if (right == 1d) unit = a.Unit;
            else if (right != 0d)
            {
                dst.SetFloat(double.NaN);
                return dst;
            }
        }

        var left = a.AsNumeric;
        if (double.IsNaN(left)) dst.SetFloat(double.NaN);
        else dst.SetFloat(Math.Pow(left, right), unit);
        return dst;
    }
    internal static void GesVmFloorDivide(this GesVmState vmState, ushort destinationRegister, in GesValue a, in GesValue b)
    {
        var dst = GesVmFloorDivide(in a, in b, vmState);
        vmState.SetValue(destinationRegister, in dst);
    }
    private static GesValue GesVmFloorDivide(in GesValue a, in GesValue b, GesVmState state)
    {
        var dst = new GesValue();
        switch (a.Kind)
        {
            case Integer when b.Kind is Integer:
                if (QuotientUnit(in a, in b) is { } integerUnit)
                {
                    if (GameEventScriptNumber.FloorDivideExact(a.IntegerValue, b.IntegerValue) is { } integerResult) dst.SetInteger(integerResult, integerUnit);
                    else dst.SetFloat(Math.Floor((double)a.IntegerValue / b.IntegerValue), integerUnit);
                }
                else dst.SetFloat(double.NaN);

                return dst;
            case Float when b.Kind is Float:
                if (QuotientUnit(in a, in b) is { } floatUnit)
                {
                    var result = Math.Floor(a.FloatValue / b.FloatValue);
                    dst.SetFloat(result, floatUnit);
                }
                else dst.SetFloat(double.NaN);

                return dst;
            case Float or Integer when b.Kind is Float or Integer:
                if (QuotientUnit(in a, in b) is { } mixedUnit)
                {
                    var result = Math.Floor(a.AsNumeric / b.AsNumeric);
                    dst.SetFloat(result, mixedUnit);
                }
                else dst.SetFloat(double.NaN);

                return dst;
            case Integer or Float when b.Kind is Percentage:
                if (QuotientUnit(in a, in b) is { } percentageRightUnit)
                {
                    var result = Math.Floor(a.AsNumeric / b.FloatValue);
                    dst.SetFloat(result, percentageRightUnit);
                }
                else dst.SetFloat(double.NaN);

                return dst;
            case Percentage when b.Kind is Integer or Float or Percentage:
                if (QuotientUnit(in a, in b) is { } percentageLeftUnit)
                {
                    var result = Math.Floor(a.FloatValue / b.AsNumeric);
                    dst.SetFloat(result, percentageLeftUnit);
                }
                else dst.SetFloat(double.NaN);

                return dst;
            case Vector or Point:
                if (b.Kind is Nothing) dst.SetNothing();
                else dst.SetFloat(double.NaN);
                return dst;
            case Integer or Float or Percentage when b.Kind is Vector or Point:
                dst.SetFloat(double.NaN);
                return dst;
            case Nothing:
                dst.SetNothing();
                return dst;
        }

        if (b.Kind is Nothing)
        {
            dst.SetNothing();
            return dst;
        }

        if (b.Kind is Vector or Point || QuotientUnit(in a, in b) is not { } unit)
        {
            dst.SetFloat(double.NaN);
            return dst;
        }

        var left = a.AsNumeric;
        if (double.IsNaN(left))
        {
            dst.SetFloat(double.NaN);
            return dst;
        }

        var right = b.AsNumeric;
        if (double.IsNaN(right))
        {
            dst.SetFloat(double.NaN);
            return dst;
        }

        var floorResult = Math.Floor(left / right);
        dst.SetFloat(floorResult, unit);
        return dst;
    }
    internal static void GesVmModulo(this GesVmState vmState, ushort destinationRegister, in GesValue a, in GesValue b)
    {
        var dst = GesVmModulo(in a, in b, vmState);
        vmState.SetValue(destinationRegister, in dst);
    }
    private static GesValue GesVmModulo(in GesValue a, in GesValue b, GesVmState state)
    {
        var dst = new GesValue();
        switch (a.Kind)
        {
            case Integer when b.Kind is Integer:
                if (SameUnit(in a, in b) is { } integerUnit)
                {
                    var rightInteger = b.IntegerValue;
                    if (rightInteger == 0)
                    {
                        dst.SetFloat(double.NaN, integerUnit);
                        return dst;
                    }

                    dst.SetInteger(GameEventScriptNumber.Modulo(a.IntegerValue, rightInteger), integerUnit);
                }
                else dst.SetFloat(double.NaN);

                return dst;
            case Float when b.Kind is Float:
                if (SameUnit(in a, in b) is { } floatUnit)
                {
                    var leftFloat = a.FloatValue;
                    var rightFloat = b.FloatValue;
                    if (double.IsNaN(leftFloat) || double.IsNaN(rightFloat) || double.IsInfinity(leftFloat) || rightFloat == 0d)
                    {
                        dst.SetFloat(double.NaN, floatUnit);
                        return dst;
                    }

                    if (double.IsInfinity(rightFloat))
                    {
                        dst.SetFloat(leftFloat, floatUnit);
                        return dst;
                    }

                    var floatModuloResult = leftFloat % rightFloat;
                    if (floatModuloResult != 0d && (floatModuloResult < 0d && rightFloat > 0d || floatModuloResult > 0d && rightFloat < 0d)) floatModuloResult += rightFloat;
                    dst.SetFloat(floatModuloResult, floatUnit);
                }
                else dst.SetFloat(double.NaN);

                return dst;
            case Float or Integer when b.Kind is Float or Integer:
                if (SameUnit(in a, in b) is { } mixedUnit)
                {
                    var leftNumber = a.AsNumeric;
                    var rightNumber = b.AsNumeric;
                    if (double.IsNaN(leftNumber) || double.IsNaN(rightNumber) || double.IsInfinity(leftNumber) || rightNumber == 0d)
                    {
                        dst.SetFloat(double.NaN, mixedUnit);
                        return dst;
                    }

                    if (double.IsInfinity(rightNumber))
                    {
                        dst.SetFloat(leftNumber, mixedUnit);
                        return dst;
                    }

                    var mixedModuloResult = leftNumber % rightNumber;
                    if (mixedModuloResult != 0d && (mixedModuloResult < 0d && rightNumber > 0d || mixedModuloResult > 0d && rightNumber < 0d)) mixedModuloResult += rightNumber;
                    dst.SetFloat(mixedModuloResult, mixedUnit);
                }
                else dst.SetFloat(double.NaN);

                return dst;
            case Nothing:
                dst.SetNothing();
                return dst;
        }

        if (b.Kind is Nothing)
        {
            dst.SetNothing();
            return dst;
        }

        if (a.Kind is Vector or Point || b.Kind is Vector or Point || SameUnit(in a, in b) is not { } unit)
        {
            dst.SetFloat(double.NaN);
            return dst;
        }

        var left = a.AsNumeric;
        if (double.IsNaN(left))
        {
            dst.SetFloat(double.NaN);
            return dst;
        }

        var right = b.AsNumeric;
        if (double.IsNaN(right) || double.IsInfinity(left) || right == 0d)
        {
            dst.SetFloat(double.NaN, unit);
            return dst;
        }

        if (double.IsInfinity(right))
        {
            dst.SetFloat(left, unit);
            return dst;
        }

        var fallbackModuloResult = left % right;
        if (fallbackModuloResult != 0d && (fallbackModuloResult < 0d && right > 0d || fallbackModuloResult > 0d && right < 0d)) fallbackModuloResult += right;
        dst.SetFloat(fallbackModuloResult, unit);
        return dst;
    }
    internal static void GesVmRemainder(this GesVmState vmState, ushort destinationRegister, in GesValue a, in GesValue b)
    {
        var dst = GesVmRemainder(in a, in b, vmState);
        vmState.SetValue(destinationRegister, in dst);
    }
    private static GesValue GesVmRemainder(in GesValue a, in GesValue b, GesVmState state)
    {
        var dst = new GesValue();
        switch (a.Kind)
        {
            case Integer when b.Kind is Integer:
                if (SameUnit(in a, in b) is { } integerUnit)
                {
                    var rightInteger = b.IntegerValue;
                    if (rightInteger == 0)
                    {
                        dst.SetFloat(double.NaN, integerUnit);
                        return dst;
                    }

                    dst.SetInteger(GameEventScriptNumber.Remainder(a.IntegerValue, rightInteger), integerUnit);
                }
                else dst.SetFloat(double.NaN);

                return dst;
            case Float when b.Kind is Float:
                if (SameUnit(in a, in b) is { } floatUnit)
                {
                    var leftFloat = a.FloatValue;
                    var rightFloat = b.FloatValue;
                    if (double.IsNaN(leftFloat) || double.IsNaN(rightFloat) || double.IsInfinity(leftFloat) || rightFloat == 0d)
                    {
                        dst.SetFloat(double.NaN, floatUnit);
                        return dst;
                    }

                    dst.SetFloat(double.IsInfinity(rightFloat) ? leftFloat : leftFloat % rightFloat, floatUnit);
                }
                else dst.SetFloat(double.NaN);

                return dst;
            case Float or Integer when b.Kind is Float or Integer:
                if (SameUnit(in a, in b) is { } mixedUnit)
                {
                    var leftNumber = a.AsNumeric;
                    var rightNumber = b.AsNumeric;
                    if (double.IsNaN(leftNumber) || double.IsNaN(rightNumber) || double.IsInfinity(leftNumber) || rightNumber == 0d)
                    {
                        dst.SetFloat(double.NaN, mixedUnit);
                        return dst;
                    }

                    dst.SetFloat(double.IsInfinity(rightNumber) ? leftNumber : leftNumber % rightNumber, mixedUnit);
                }
                else dst.SetFloat(double.NaN);

                return dst;
            case Nothing:
                dst.SetNothing();
                return dst;
        }

        if (b.Kind is Nothing)
        {
            dst.SetNothing();
            return dst;
        }

        if (a.Kind is Vector or Point || b.Kind is Vector or Point || SameUnit(in a, in b) is not { } unit)
        {
            dst.SetFloat(double.NaN);
            return dst;
        }

        var left = a.AsNumeric;
        if (double.IsNaN(left))
        {
            dst.SetFloat(double.NaN);
            return dst;
        }

        var right = b.AsNumeric;
        if (double.IsNaN(right) || double.IsInfinity(left) || right == 0d)
        {
            dst.SetFloat(double.NaN, unit);
            return dst;
        }

        dst.SetFloat(double.IsInfinity(right) ? left : left % right, unit);
        return dst;
    }
    internal static void GesVmMin(this GesVmState vmState, ushort destinationRegister, in GesValue a, in GesValue b)
    {
        var dst = GesVmMin(in a, in b, vmState);
        vmState.SetValue(destinationRegister, in dst);
    }
    private static GesValue GesVmMin(in GesValue a, in GesValue b, GesVmState state)
    {
        var dst = new GesValue();
        switch (a.Kind)
        {
            case Integer when b.Kind is Integer:
                if (SameUnit(in a, in b) is null) dst.SetFloat(double.NaN);
                else dst = b.IntegerValue < a.IntegerValue ? b : a;
                return dst;
            case Float when b.Kind is Float:
                if (SameUnit(in a, in b) is null || double.IsNaN(a.FloatValue) || double.IsNaN(b.FloatValue)) dst.SetFloat(double.NaN);
                else dst = b.FloatValue < a.FloatValue ? b : a;
                return dst;
            case Float or Integer when b.Kind is Float or Integer:
                if (SameUnit(in a, in b) is null)
                {
                    dst.SetFloat(double.NaN);
                    return dst;
                }

                var fastLeft = a.AsNumeric;
                var fastRight = b.AsNumeric;
                if (double.IsNaN(fastLeft) || double.IsNaN(fastRight)) dst.SetFloat(double.NaN);
                else dst = fastRight < fastLeft ? b : a;
                return dst;
            case Nothing:
                dst.SetNothing();
                return dst;
        }

        if (b.Kind is Nothing)
        {
            dst.SetNothing();
            return dst;
        }

        var leftNumber = double.NaN;
        var leftIsNumeric = false;
        var rightNumber = double.NaN;
        var rightIsNumeric = false;
        var leftRank = 8;
        var rightRank = 8;
        string? leftText = null;
        string? rightText = null;

        switch (a.Kind)
        {
            case Integer:
                leftNumber = a.IntegerValue;
                leftIsNumeric = true;
                leftRank = 1;
                break;
            case Float:
                leftNumber = a.FloatValue;
                leftIsNumeric = true;
                leftRank = 1;
                break;
            case Percentage:
                leftNumber = a.FloatValue;
                leftIsNumeric = true;
                leftRank = 1;
                break;
            case GameEventScriptBytecodeTypeKind.Boolean:
                leftNumber = a.IsTrue ? 1d : 0d;
                leftIsNumeric = true;
                leftRank = 6;
                break;
            case Text:
                leftText = a.TextValue;
                leftRank = 2;
                break;
            case Tag:
                leftText = a.TextValue;
                leftRank = 3;
                break;
            case Vector:
                leftRank = 4;
                break;
            case Point:
                leftRank = 5;
                break;
            case GameEventScriptBytecodeTypeKind.Range:
                leftRank = 8;
                break;
            case Message:
                leftRank = 9;
                break;
            case Handler:
                leftRank = 10;
                break;
            case List:
                leftRank = 11;
                break;
            case Map:
                leftRank = 12;
                break;
            case Dice:
                leftRank = 13;
                break;
        }

        switch (b.Kind)
        {
            case Integer:
                rightNumber = b.IntegerValue;
                rightIsNumeric = true;
                rightRank = 1;
                break;
            case Float:
                rightNumber = b.FloatValue;
                rightIsNumeric = true;
                rightRank = 1;
                break;
            case Percentage:
                rightNumber = b.FloatValue;
                rightIsNumeric = true;
                rightRank = 1;
                break;
            case GameEventScriptBytecodeTypeKind.Boolean:
                rightNumber = b.IsTrue ? 1d : 0d;
                rightIsNumeric = true;
                rightRank = 6;
                break;
            case Text:
                rightText = b.TextValue;
                rightRank = 2;
                break;
            case Tag:
                rightText = b.TextValue;
                rightRank = 3;
                break;
            case Vector:
                rightRank = 4;
                break;
            case Point:
                rightRank = 5;
                break;
            case GameEventScriptBytecodeTypeKind.Range:
                rightRank = 8;
                break;
            case Message:
                rightRank = 9;
                break;
            case Handler:
                rightRank = 10;
                break;
            case List:
                rightRank = 11;
                break;
            case Map:
                rightRank = 12;
                break;
            case Dice:
                rightRank = 13;
                break;
        }

        if (leftIsNumeric && rightIsNumeric)
        {
            if (SameUnit(in a, in b) is null || double.IsNaN(leftNumber) || double.IsNaN(rightNumber))
            {
                dst.SetFloat(double.NaN);
                return dst;
            }

            dst = rightNumber < leftNumber ? b : a;
            return dst;
        }

        if (leftRank != rightRank)
        {
            dst = rightRank < leftRank ? b : a;
            return dst;
        }

        var comparison = 0;
        switch (a.Kind)
        {
            case Text when b.Kind is Text:
                leftText ??= a.TextValue;
                rightText ??= b.TextValue;
                comparison = GameEventScriptText.CompareScalarOrdinal(rightText, leftText);
                break;
            case Tag when b.Kind is Tag:
                leftText ??= a.TextValue;
                rightText ??= b.TextValue;
                comparison = GameEventScriptText.CompareScalarOrdinal(rightText, leftText);
                break;
            case Vector when b.Kind is Vector:
            case Point when b.Kind is Point:
                if (a.Unit != b.Unit)
                {
                    comparison = b.Unit.CompareTo(a.Unit);
                    break;
                }

                if (a.ObjectValue is GesValueVectorPoint leftTriplet && b.ObjectValue is GesValueVectorPoint rightTriplet)
                {
                    comparison = rightTriplet.X.CompareTo(leftTriplet.X);
                    if (comparison == 0) comparison = rightTriplet.Y.CompareTo(leftTriplet.Y);
                    if (comparison == 0) comparison = rightTriplet.Z.CompareTo(leftTriplet.Z);
                }

                break;
            case GameEventScriptBytecodeTypeKind.Boolean when b.Kind is GameEventScriptBytecodeTypeKind.Boolean:
                comparison = b.IsTrue.CompareTo(a.IsTrue);
                break;
            default:
                comparison = b.Kind.CompareTo(a.Kind);
                break;
        }

        dst = comparison < 0 ? b : a;
        return dst;
    }
    internal static void GesVmMax(this GesVmState vmState, ushort destinationRegister, in GesValue a, in GesValue b)
    {
        var dst = GesVmMax(in a, in b, vmState);
        vmState.SetValue(destinationRegister, in dst);
    }
    private static GesValue GesVmMax(in GesValue a, in GesValue b, GesVmState state)
    {
        var dst = new GesValue();
        switch (a.Kind)
        {
            case Integer when b.Kind is Integer:
                if (SameUnit(in a, in b) is null) dst.SetFloat(double.NaN);
                else dst = b.IntegerValue > a.IntegerValue ? b : a;
                return dst;
            case Float when b.Kind is Float:
                if (SameUnit(in a, in b) is null || double.IsNaN(a.FloatValue) || double.IsNaN(b.FloatValue)) dst.SetFloat(double.NaN);
                else dst = b.FloatValue > a.FloatValue ? b : a;
                return dst;
            case Float or Integer when b.Kind is Float or Integer:
                if (SameUnit(in a, in b) is null)
                {
                    dst.SetFloat(double.NaN);
                    return dst;
                }

                var fastLeft = a.AsNumeric;
                var fastRight = b.AsNumeric;
                if (double.IsNaN(fastLeft) || double.IsNaN(fastRight)) dst.SetFloat(double.NaN);
                else dst = fastRight > fastLeft ? b : a;
                return dst;
            case Nothing:
                dst.SetNothing();
                return dst;
        }

        if (b.Kind is Nothing)
        {
            dst.SetNothing();
            return dst;
        }

        var leftNumber = double.NaN;
        var leftIsNumeric = false;
        var rightNumber = double.NaN;
        var rightIsNumeric = false;
        var leftRank = 8;
        var rightRank = 8;
        string? leftText = null;
        string? rightText = null;

        switch (a.Kind)
        {
            case Integer:
                leftNumber = a.IntegerValue;
                leftIsNumeric = true;
                leftRank = 1;
                break;
            case Float:
                leftNumber = a.FloatValue;
                leftIsNumeric = true;
                leftRank = 1;
                break;
            case Percentage:
                leftNumber = a.FloatValue;
                leftIsNumeric = true;
                leftRank = 1;
                break;
            case GameEventScriptBytecodeTypeKind.Boolean:
                leftNumber = a.IsTrue ? 1d : 0d;
                leftIsNumeric = true;
                leftRank = 6;
                break;
            case Text:
                leftText = a.TextValue;
                leftRank = 2;
                break;
            case Tag:
                leftText = a.TextValue;
                leftRank = 3;
                break;
            case Vector:
                leftRank = 4;
                break;
            case Point:
                leftRank = 5;
                break;
            case GameEventScriptBytecodeTypeKind.Range:
                leftRank = 8;
                break;
            case Message:
                leftRank = 9;
                break;
            case Handler:
                leftRank = 10;
                break;
            case List:
                leftRank = 11;
                break;
            case Map:
                leftRank = 12;
                break;
            case Dice:
                leftRank = 13;
                break;
        }

        switch (b.Kind)
        {
            case Integer:
                rightNumber = b.IntegerValue;
                rightIsNumeric = true;
                rightRank = 1;
                break;
            case Float:
                rightNumber = b.FloatValue;
                rightIsNumeric = true;
                rightRank = 1;
                break;
            case Percentage:
                rightNumber = b.FloatValue;
                rightIsNumeric = true;
                rightRank = 1;
                break;
            case GameEventScriptBytecodeTypeKind.Boolean:
                rightNumber = b.IsTrue ? 1d : 0d;
                rightIsNumeric = true;
                rightRank = 6;
                break;
            case Text:
                rightText = b.TextValue;
                rightRank = 2;
                break;
            case Tag:
                rightText = b.TextValue;
                rightRank = 3;
                break;
            case Vector:
                rightRank = 4;
                break;
            case Point:
                rightRank = 5;
                break;
            case GameEventScriptBytecodeTypeKind.Range:
                rightRank = 8;
                break;
            case Message:
                rightRank = 9;
                break;
            case Handler:
                rightRank = 10;
                break;
            case List:
                rightRank = 11;
                break;
            case Map:
                rightRank = 12;
                break;
            case Dice:
                rightRank = 13;
                break;
        }

        if (leftIsNumeric && rightIsNumeric)
        {
            if (SameUnit(in a, in b) is null || double.IsNaN(leftNumber) || double.IsNaN(rightNumber))
            {
                dst.SetFloat(double.NaN);
                return dst;
            }

            dst = rightNumber > leftNumber ? b : a;
            return dst;
        }

        if (leftRank != rightRank)
        {
            dst = rightRank > leftRank ? b : a;
            return dst;
        }

        var comparison = 0;
        switch (a.Kind)
        {
            case Text when b.Kind is Text:
                leftText ??= a.TextValue;
                rightText ??= b.TextValue;
                comparison = GameEventScriptText.CompareScalarOrdinal(rightText, leftText);
                break;
            case Tag when b.Kind is Tag:
                leftText ??= a.TextValue;
                rightText ??= b.TextValue;
                comparison = GameEventScriptText.CompareScalarOrdinal(rightText, leftText);
                break;
            case Vector when b.Kind is Vector:
            case Point when b.Kind is Point:
                if (a.Unit != b.Unit)
                {
                    comparison = b.Unit.CompareTo(a.Unit);
                    break;
                }

                if (a.ObjectValue is GesValueVectorPoint leftTriplet && b.ObjectValue is GesValueVectorPoint rightTriplet)
                {
                    comparison = rightTriplet.X.CompareTo(leftTriplet.X);
                    if (comparison == 0) comparison = rightTriplet.Y.CompareTo(leftTriplet.Y);
                    if (comparison == 0) comparison = rightTriplet.Z.CompareTo(leftTriplet.Z);
                }

                break;
            case GameEventScriptBytecodeTypeKind.Boolean when b.Kind is GameEventScriptBytecodeTypeKind.Boolean:
                comparison = b.IsTrue.CompareTo(a.IsTrue);
                break;
            default:
                comparison = b.Kind.CompareTo(a.Kind);
                break;
        }

        dst = comparison > 0 ? b : a;
        return dst;
    }
    internal static void GesVmNegate(this GesVmState vmState, ushort destinationRegister, in GesValue a)
    {
        var dst = GesVmNegate(in a, vmState);
        vmState.SetValue(destinationRegister, in dst);
    }
    private static GesValue GesVmNegate(in GesValue a, GesVmState state)
    {
        var dst = new GesValue();
        switch (a.Kind)
        {
            case Integer:
                if (a.IntegerValue == long.MinValue) dst.SetFloat(-(double)a.IntegerValue, a.Unit);
                else dst.SetInteger(-a.IntegerValue, a.Unit);
                return dst;
            case Float:
                dst.SetFloat(-a.FloatValue, a.Unit);
                return dst;
            case Percentage:
                dst.SetPercentage(-a.FloatValue);
                return dst;
            case Vector:
                if (a.ObjectValue is GesValueVectorPoint vector)
                {
                    dst.SetVector(-vector.X, -vector.Y, -vector.Z, a.Unit);
                }
                else dst.SetFloat(double.NaN);

                return dst;
            case Point:
                dst.SetFloat(double.NaN);
                return dst;
            case Nothing:
                dst.SetNothing();
                return dst;
            default:
                var number = a.AsNumeric;
                if (double.IsNaN(number)) dst.SetFloat(double.NaN);
                else dst.SetFloat(-number);
                return dst;
        }
    }
    internal static void GesVmAbs(this GesVmState vmState, ushort destinationRegister, in GesValue a)
    {
        var dst = GesVmAbs(in a, vmState);
        vmState.SetValue(destinationRegister, in dst);
    }
    private static GesValue GesVmAbs(in GesValue a, GesVmState state)
    {
        var dst = new GesValue();
        switch (a.Kind)
        {
            case Integer:
                if (a.IntegerValue == long.MinValue) dst.SetFloat(-(double)a.IntegerValue, a.Unit);
                else dst.SetInteger(Math.Abs(a.IntegerValue), a.Unit);
                return dst;
            case Float:
                dst.SetFloat(Math.Abs(a.FloatValue), a.Unit);
                return dst;
            case Percentage:
                dst.SetPercentage(Math.Abs(a.FloatValue));
                return dst;
            case Vector:
                if (a.ObjectValue is not GesValueVectorPoint vector)
                {
                    dst.SetFloat(double.NaN);
                    return dst;
                }

                var length = Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y + vector.Z * vector.Z);
                if (double.IsNaN(length)) dst.SetFloat(double.NaN);
                else dst.SetFloat(length, a.Unit);
                return dst;
            case Point:
                dst.SetFloat(double.NaN);
                return dst;
            case Nothing:
                dst.SetNothing();
                return dst;
            default:
                var number = a.AsNumeric;
                dst.SetFloat(double.IsNaN(number) ? double.NaN : Math.Abs(number));
                return dst;
        }
    }
    internal static void GesVmClamp(this GesVmState vmState, ushort destinationRegister, in GesValue value, in GesValue min, in GesValue max)
    {
        var dst = GesVmClamp(in value, in min, in max, vmState);
        vmState.SetValue(destinationRegister, in dst);
    }
    private static GesValue GesVmClamp(in GesValue value, in GesValue min, in GesValue max, GesVmState state)
    {
        var dst = new GesValue();
        switch (value.Kind)
        {
            case Integer when min.Kind is Integer && max.Kind is Integer:
            {
                if (SameUnit(in value, in min, in max) is not { } integerUnit)
                {
                    dst.SetFloat(double.NaN);
                    return dst;
                }

                var lower = min.IntegerValue <= max.IntegerValue ? min.IntegerValue : max.IntegerValue;
                var upper = min.IntegerValue <= max.IntegerValue ? max.IntegerValue : min.IntegerValue;
                var result = value.IntegerValue < lower ? lower : value.IntegerValue > upper ? upper : value.IntegerValue;
                dst.SetInteger(result, integerUnit);
                return dst;
            }
            case Float when min.Kind is Float && max.Kind is Float:
            {
                if (SameUnit(in value, in min, in max) is not { } floatUnit)
                {
                    dst.SetFloat(double.NaN);
                    return dst;
                }

                var lower = min.FloatValue <= max.FloatValue ? min.FloatValue : max.FloatValue;
                var upper = min.FloatValue <= max.FloatValue ? max.FloatValue : min.FloatValue;
                if (double.IsNaN(value.FloatValue) || double.IsNaN(lower) || double.IsNaN(upper))
                {
                    dst.SetFloat(double.NaN, floatUnit);
                    return dst;
                }

                var result = value.FloatValue < lower ? lower : value.FloatValue > upper ? upper : value.FloatValue;
                dst.SetFloat(result, floatUnit);
                return dst;
            }
            case Float or Integer when min.Kind is Float or Integer && max.Kind is Float or Integer:
            {
                if (SameUnit(in value, in min, in max) is not { } mixedUnit)
                {
                    dst.SetFloat(double.NaN);
                    return dst;
                }

                var x = value.AsNumeric;
                var a = min.AsNumeric;
                var b = max.AsNumeric;
                var lower = a <= b ? a : b;
                var upper = a <= b ? b : a;
                if (double.IsNaN(x) || double.IsNaN(lower) || double.IsNaN(upper))
                {
                    dst.SetFloat(double.NaN, mixedUnit);
                    return dst;
                }

                var result = x < lower ? lower : x > upper ? upper : x;
                dst.SetFloat(result, mixedUnit);
                return dst;
            }
            case Nothing:
                dst.SetNothing();
                return dst;
            case Vector or Point:
                if (min.Kind is Nothing || max.Kind is Nothing) dst.SetNothing();
                else dst.SetFloat(double.NaN);
                return dst;
        }

        if (min.Kind is Nothing || max.Kind is Nothing)
        {
            dst.SetNothing();
            return dst;
        }

        if (min.Kind is Vector or Point || max.Kind is Vector or Point || SameUnit(in value, in min, in max) is not { } unit)
        {
            dst.SetFloat(double.NaN);
            return dst;
        }

        var raw = value.AsNumeric;
        var minimum = min.AsNumeric;
        var maximum = max.AsNumeric;
        var lowerFallback = minimum <= maximum ? minimum : maximum;
        var upperFallback = minimum <= maximum ? maximum : minimum;
        if (double.IsNaN(raw) || double.IsNaN(lowerFallback) || double.IsNaN(upperFallback))
        {
            dst.SetFloat(double.NaN, unit);
            return dst;
        }

        var fallbackResult = raw < lowerFallback ? lowerFallback : raw > upperFallback ? upperFallback : raw;
        dst.SetFloat(fallbackResult, unit);
        return dst;
    }
    internal static void GesVmNaturalLog(this GesVmState vmState, ushort destinationRegister, in GesValue a)
    {
        var dst = GesVmNaturalLog(in a, vmState);
        vmState.SetValue(destinationRegister, in dst);
    }
    private static GesValue GesVmNaturalLog(in GesValue a, GesVmState state)
    {
        var dst = new GesValue();
        switch (a.Kind)
        {
            case Integer:
                if (a.HasUnit) dst.SetFloat(double.NaN);
                else if (a.IntegerValue < 0) dst.SetFloat(double.NaN);
                else if (a.IntegerValue == 0) dst.SetFloat(double.NegativeInfinity);
                else dst.SetFloat(Math.Log(a.IntegerValue));
                return dst;
            case Float:
                if (a.HasUnit)
                {
                    dst.SetFloat(double.NaN);
                    return dst;
                }

                var floatValue = a.FloatValue;
                if (double.IsNaN(floatValue) || floatValue < 0d || double.IsNegativeInfinity(floatValue)) dst.SetFloat(double.NaN);
                else if (double.IsPositiveInfinity(floatValue)) dst.SetFloat(double.PositiveInfinity);
                else dst.SetFloat(Math.Log(floatValue));
                return dst;
            case Percentage:
                var percentageValue = a.FloatValue;
                if (double.IsNaN(percentageValue) || percentageValue < 0d || double.IsNegativeInfinity(percentageValue)) dst.SetFloat(double.NaN);
                else if (double.IsPositiveInfinity(percentageValue)) dst.SetFloat(double.PositiveInfinity);
                else dst.SetFloat(Math.Log(percentageValue));
                return dst;
            case Nothing:
                dst.SetNothing();
                return dst;
            default:
                if (a.HasUnit)
                {
                    dst.SetFloat(double.NaN);
                    return dst;
                }

                var number = a.AsNumeric;
                if (double.IsNaN(number) || number < 0d || double.IsNegativeInfinity(number)) dst.SetFloat(double.NaN);
                else if (double.IsPositiveInfinity(number)) dst.SetFloat(double.PositiveInfinity);
                else dst.SetFloat(Math.Log(number));
                return dst;
        }
    }
    internal static void GesVmExp(this GesVmState vmState, ushort destinationRegister, in GesValue a)
    {
        if (a.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (a.HasUnit || !a.IsNumeric)
        {
            vmState.SetFloat(destinationRegister, double.NaN);
            return;
        }

        vmState.SetFloat(destinationRegister, Math.Exp(a.AsNumeric));
    }
    internal static void GesVmFloor(this GesVmState vmState, ushort destinationRegister, in GesValue a)
    {
        if (a.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (!a.IsNumeric)
        {
            vmState.SetFloat(destinationRegister, double.NaN);
            return;
        }

        vmState.SetInteger(destinationRegister, ToIntegerSaturated(Math.Floor(a.AsNumeric)));
    }
    internal static void GesVmCeil(this GesVmState vmState, ushort destinationRegister, in GesValue a)
    {
        if (a.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (!a.IsNumeric)
        {
            vmState.SetFloat(destinationRegister, double.NaN);
            return;
        }

        vmState.SetInteger(destinationRegister, ToIntegerSaturated(Math.Ceiling(a.AsNumeric)));
    }
    internal static void GesVmTruncate(this GesVmState vmState, ushort destinationRegister, in GesValue a)
    {
        if (a.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (!a.IsNumeric)
        {
            vmState.SetFloat(destinationRegister, double.NaN);
            return;
        }

        vmState.SetInteger(destinationRegister, ToIntegerSaturated(Math.Truncate(a.AsNumeric)));
    }
    internal static void GesVmRoundHalfEven(this GesVmState vmState, ushort destinationRegister, in GesValue a)
    {
        if (a.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (!a.IsNumeric)
        {
            vmState.SetFloat(destinationRegister, double.NaN);
            return;
        }

        vmState.SetInteger(destinationRegister, ToIntegerSaturated(Math.Round(a.AsNumeric, 0, MidpointRounding.ToEven)));
    }
    internal static void GesVmRoundHalfUp(this GesVmState vmState, ushort destinationRegister, in GesValue a)
    {
        if (a.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (!a.IsNumeric)
        {
            vmState.SetFloat(destinationRegister, double.NaN);
            return;
        }

        vmState.SetInteger(destinationRegister, ToIntegerSaturated(Math.Round(a.AsNumeric, 0, MidpointRounding.AwayFromZero)));
    }
    internal static void GesVmRoundHalfDown(this GesVmState vmState, ushort destinationRegister, in GesValue a)
    {
        if (a.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (!a.IsNumeric)
        {
            vmState.SetFloat(destinationRegister, double.NaN);
            return;
        }

        var value = a.AsNumeric;
        vmState.SetInteger(destinationRegister, ToIntegerSaturated(GameEventScriptNumber.RoundHalfTowardZero(value)));
    }
    internal static void GesVmDegreeToRadians(this GesVmState vmState, ushort destinationRegister, in GesValue a)
    {
        if (a.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (!a.IsNumeric || a.Unit.IsNumericUnit() && a.Unit != UnitDegree)
        {
            vmState.SetFloat(destinationRegister, double.NaN);
            return;
        }

        var number = a.AsNumeric;
        if (!double.IsFinite(number))
        {
            vmState.SetFloat(destinationRegister, double.NaN);
            return;
        }

        vmState.SetFloat(destinationRegister, number / 180d * GameEventScriptMathConstants.Pi);
    }
    internal static void GesVmDegreeFromRadians(this GesVmState vmState, ushort destinationRegister, in GesValue a)
    {
        if (a.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (!a.IsNumeric || a.Unit.IsNumericUnit())
        {
            vmState.SetFloat(destinationRegister, double.NaN);
            return;
        }

        var number = a.AsNumeric;
        if (!double.IsFinite(number))
        {
            vmState.SetFloat(destinationRegister, double.NaN);
            return;
        }

        vmState.SetFloat(destinationRegister, number / GameEventScriptMathConstants.Pi * 180d, UnitDegree);
    }
    internal static void GesVmWrapDegree(this GesVmState vmState, ushort destinationRegister, in GesValue a)
    {
        if (a.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (!a.IsNumeric || a.Unit.IsNumericUnit() && a.Unit != UnitDegree)
        {
            vmState.SetFloat(destinationRegister, double.NaN);
            return;
        }

        var number = a.AsNumeric;
        if (!double.IsFinite(number))
        {
            vmState.SetFloat(destinationRegister, double.NaN);
            return;
        }

        var wrapped = number % 360d;
        if (wrapped < 0d) wrapped += 360d;
        vmState.SetFloat(destinationRegister, wrapped == 360d ? 0d : wrapped, UnitDegree);
    }
    private static long ToIntegerSaturated(double number) => GameEventScriptNumber.ToIntegerSaturated(number);
    internal static void GesVmTerm(this GesVmState vmState, ushort destinationRegister, in GesValue source, in GesValue termValue)
    {
        var dst = GesVmTerm(in source, in termValue, vmState);
        vmState.SetValue(destinationRegister, in dst);
    }
    private static GesValue GesVmTerm(in GesValue source, in GesValue termValue, GesVmState state)
    {
        var dst = new GesValue();
        long index;
        bool ret;
        switch (termValue.Kind)
        {
            case Integer:
                index = termValue.IntegerValue;
                ret = true;
                break;
            case Float or Percentage:
                ret = !double.IsNaN(termValue.FloatValue);
                index = ret ? GameEventScriptNumber.ToIntegerSaturated(termValue.FloatValue) : 0;

                break;
            case GameEventScriptBytecodeTypeKind.Boolean:
                index = termValue.IsTrue ? 1 : 0;
                ret = true;
                break;
            default:
                if (termValue.IsNumeric)
                {
                    var numeric = termValue.AsNumeric;
                    ret = !double.IsNaN(numeric);
                    index = ret ? GameEventScriptNumber.ToIntegerSaturated(numeric) : 0;

                    break;
                }

                index = 0;
                ret = false;
                break;
        }

        if (source.Kind is not Series || source.ObjectValue is not GesSeries series || !ret) dst.SetNothing();
        else dst = series.GetTerm(index);
        return dst;
    }
}
