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
    internal static void GesVmAdd(this GesVmState vmState, ushort destinationRegister, in GesVmValue a, in GesVmValue b)
    {
        var dst = new GesVmValue();
        GesVmAdd(ref dst, in a, in b, vmState);
        vmState.SetValue(destinationRegister, in dst);
    }
    internal static void GesVmAdd(ref GesVmValue dst, in GesVmValue a, in GesVmValue b, GesVmState state)
    {
        switch (a.Kind)
        {
            case Integer when b.Kind is Integer:
                if (TrySameUnit(in a, in b, out var unit)) dst.SetInteger(a.IntegerValue + b.IntegerValue, unit);
                else dst.SetFloat(double.NaN);
                break;
            case Float when b.Kind is Float:
                if (TrySameUnit(in a, in b, out unit)) dst.SetFloat(a.FloatValue + b.FloatValue, unit);
                else dst.SetFloat(double.NaN);
                break;
            case Float or Integer when b.Kind is Float or Integer:
                if (TrySameUnit(in a, in b, out unit)) dst.SetFloat(a.AsNumeric + b.AsNumeric, unit);
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
                if (a.ObjectValue is GesVmValueVectorPoint av && b.ObjectValue is GesVmValueVectorPoint bv && TrySameUnit(in a, in b, out var vectorUnit)) dst.SetVector(av.X + bv.X, av.Y + bv.Y, av.Z + bv.Z, vectorUnit);
                else dst.SetFloat(double.NaN);
                break;
            case Point when b.Kind is Vector:
                if (a.ObjectValue is GesVmValueVectorPoint ap && b.ObjectValue is GesVmValueVectorPoint bvv && TrySameUnit(in a, in b, out var pointUnit)) dst.SetPoint(ap.X + bvv.X, ap.Y + bvv.Y, ap.Z + bvv.Z, pointUnit);
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
            case List when b.Kind is not Nothing && a.ObjectValue is GesVmValue[] aList:
            {
                var list = new GesVmValue[aList.Length + 1];
                for (var i = 0; i < aList.Length; i++) list[i] = aList[i];
                list[aList.Length] = b;
                dst.SetList(list);
                break;
            }
            case not List and not Nothing when b.Kind is List && b.ObjectValue is GesVmValue[] rightList:
            {
                var list = new GesVmValue[rightList.Length + 1];
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
                    else if (TrySameUnit(in a, in b, out unit)) dst.SetFloat(aNum + bNum, unit);
                    else dst.SetFloat(double.NaN);
                }

                break;
        }
    }
    internal static void GesVmSubtract(this GesVmState vmState, ushort dst, in GesVmValue a, in GesVmValue b)
    {
        switch (a.Kind)
        {
            case Integer when b.Kind is Integer:
                if (TrySameUnit(in a, in b, out var unit)) vmState.SetInteger(dst, a.IntegerValue - b.IntegerValue, unit);
                else vmState.SetFloat(dst, double.NaN);
                return;
            case Float when b.Kind is Float:
                if (TrySameUnit(in a, in b, out unit)) vmState.SetFloat(dst, a.FloatValue - b.FloatValue, unit);
                else vmState.SetFloat(dst, double.NaN);
                return;
            case Float or Integer when b.Kind is Float or Integer:
                if (TrySameUnit(in a, in b, out unit)) vmState.SetFloat(dst, a.AsNumeric - b.AsNumeric, unit);
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
                if (a.ObjectValue is GesVmValueVectorPoint av && b.ObjectValue is GesVmValueVectorPoint bv && TrySameUnit(in a, in b, out var vectorUnit)) vmState.SetVector(dst, av.X - bv.X, av.Y - bv.Y, av.Z - bv.Z, vectorUnit);
                else vmState.SetFloat(dst, double.NaN);
                return;
            }
            case Vector:
                if (b.Kind is not Nothing) vmState.SetFloat(dst, double.NaN);
                else vmState.SetNothing(dst);
                return;
            case Point when b.Kind is Vector:
            {
                if (a.ObjectValue is GesVmValueVectorPoint ap && b.ObjectValue is GesVmValueVectorPoint bv && TrySameUnit(in a, in b, out var pointUnit)) vmState.SetPoint(dst, ap.X - bv.X, ap.Y - bv.Y, ap.Z - bv.Z, pointUnit);
                else vmState.SetFloat(dst, double.NaN);
                return;
            }
            case Point when b.Kind is Point:
            {
                if (a.ObjectValue is GesVmValueVectorPoint ap && b.ObjectValue is GesVmValueVectorPoint bp && TrySameUnit(in a, in b, out var pointUnit)) vmState.SetVector(dst, ap.X - bp.X, ap.Y - bp.Y, ap.Z - bp.Z, pointUnit);
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
            case List when b.Kind is List or Dice and not Nothing or not Map && a.ObjectValue is GesVmValue[] aList:
            {
                var removeList = b.Kind is List && b.ObjectValue is GesVmValue[] bList ? bList : null;
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
                            candidate = new GesVmValue();
                            candidate.SetInteger(removeDice[j]);
                        }
                        if (!candidate.EqualsValue(in aList[i])) continue;
                        removed[j] = true;
                        shouldRemove = true;
                        break;
                    }

                    if (!shouldRemove) resultLength++;
                }

                var list = new GesVmValue[resultLength];
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
                            candidate = new GesVmValue();
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
            case Dice when b.Kind is List && a.ObjectValue is int[] aDice && b.ObjectValue is GesVmValue[] removeList:
            {
                var removed = new bool[removeList.Length];
                var resultLength = 0;
                for (var i = 0; i < aDice.Length; i++)
                {
                    var item = new GesVmValue();
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

                var list = new GesVmValue[resultLength];
                var index = 0;
                Array.Clear(removed, 0, removed.Length);
                for (var i = 0; i < aDice.Length; i++)
                {
                    var item = new GesVmValue();
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
            case Map when b.Kind is not Nothing && a.ObjectValue is GesVmValueMap aMap:
            {
                string? singleKey = null;
                string[]? keys = null;
                GesVmValueMap? bMap = null;
                if (b.Kind is Map && b.ObjectValue is GesVmValueMap rightMap)
                {
                    bMap = rightMap;
                    // Keys are already sorted in GesVmValueMap.
                }
                else if (b.Kind is Text or Tag)
                {
                    singleKey = b.TextValue;
                }
                else if (b.Kind is List && b.ObjectValue is GesVmValue[] keyList)
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

                    Array.Sort(keys, StringComparer.Ordinal);
                }
                else
                {
                    vmState.SetNothing(dst);
                    return;
                }

                var map = new GesVmValueMapBuilder(aMap.StorageLength);
                if (bMap is not null)
                {
                    var bi = 0;
                    for (var ai = 0; ai < aMap.StorageLength; ai++)
                    {
                        var key = aMap.KeyAt(ai);
                        while (bi < bMap.StorageLength && string.CompareOrdinal(bMap.KeyAt(bi), key) < 0) bi++;
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
                        while (ki < keys.Length && string.CompareOrdinal(keys[ki], key) < 0) ki++;
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
                        else if (TrySameUnit(in a, in b, out unit)) vmState.SetFloat(dst, aNum - bNum, unit);
                        else vmState.SetFloat(dst, double.NaN);
                    }
                }

                break;
        }
    }
    internal static void GesVmMultiply(this GesVmState vmState, ushort dst, in GesVmValue a, in GesVmValue b)
    {
        switch (a.Kind)
        {
            case Integer when b.Kind is Integer:
                if (TryProductUnit(in a, in b, out var unit)) vmState.SetFloat(dst, (double)a.IntegerValue * b.IntegerValue, unit);
                else vmState.SetFloat(dst, double.NaN);
                return;
            case Float when b.Kind is Float:
                if (TryProductUnit(in a, in b, out unit)) vmState.SetFloat(dst, a.FloatValue * b.FloatValue, unit);
                else vmState.SetFloat(dst, double.NaN);
                return;
            case Float or Integer when b.Kind is Float or Integer:
                if (TryProductUnit(in a, in b, out unit)) vmState.SetFloat(dst, a.AsNumeric * b.AsNumeric, unit);
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
                if (b.ObjectValue is GesVmValueVectorPoint percentageVector) vmState.SetVector(dst, a.FloatValue * percentageVector.X, a.FloatValue * percentageVector.Y, a.FloatValue * percentageVector.Z, b.Unit);
                else vmState.SetFloat(dst, double.NaN);
                return;
            case Percentage when b.Kind is Point:
                vmState.SetFloat(dst, double.NaN);
                return;
            case Vector when b.Kind is Integer:
                if (a.ObjectValue is GesVmValueVectorPoint vectorInt && TryProductUnit(in a, in b, out unit)) vmState.SetVector(dst, vectorInt.X * b.IntegerValue, vectorInt.Y * b.IntegerValue, vectorInt.Z * b.IntegerValue, unit);
                else vmState.SetFloat(dst, double.NaN);
                return;
            case Vector when b.Kind is Float:
                if (a.ObjectValue is GesVmValueVectorPoint vectorFloat && double.IsFinite(b.FloatValue) && TryProductUnit(in a, in b, out unit)) vmState.SetVector(dst, vectorFloat.X * b.FloatValue, vectorFloat.Y * b.FloatValue, vectorFloat.Z * b.FloatValue, unit);
                else vmState.SetFloat(dst, double.NaN);
                return;
            case Vector when b.Kind is Percentage:
                if (a.ObjectValue is GesVmValueVectorPoint vectorPercent) vmState.SetVector(dst, vectorPercent.X * b.FloatValue, vectorPercent.Y * b.FloatValue, vectorPercent.Z * b.FloatValue, a.Unit);
                else vmState.SetFloat(dst, double.NaN);
                return;
            case Vector when b.Kind is Nothing:
                vmState.SetNothing(dst);
                return;
            case Vector:
                var vectorScalar = b.AsNumeric;
                if (a.ObjectValue is GesVmValueVectorPoint vector && !double.IsNaN(vectorScalar) && double.IsFinite(vectorScalar) && TryProductUnit(in a, in b, out unit))
                    vmState.SetVector(dst, vector.X * vectorScalar, vector.Y * vectorScalar, vector.Z * vectorScalar, unit);
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
                if (b.ObjectValue is GesVmValueVectorPoint rightVector && double.IsFinite(scalar) && TryProductUnit(in a, in b, out unit)) vmState.SetVector(dst, scalar * rightVector.X, scalar * rightVector.Y, scalar * rightVector.Z, unit);
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
                if (b.ObjectValue is GesVmValueVectorPoint vector && double.IsFinite(fallbackLeft) && TryProductUnit(in a, in b, out var vectorUnit)) vmState.SetVector(dst, fallbackLeft * vector.X, fallbackLeft * vector.Y, fallbackLeft * vector.Z, vectorUnit);
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
        if (double.IsNaN(bNum) || !TryProductUnit(in a, in b, out var fallbackUnit))
        {
            vmState.SetFloat(dst, double.NaN);
            return;
        }

        vmState.SetFloat(dst, fallbackLeft * bNum, b.Kind is Percentage ? a.Unit : fallbackUnit);
    }
    internal static void GesVmDivide(this GesVmState vmState, ushort destinationRegister, in GesVmValue a, in GesVmValue b)
    {
        var dst = new GesVmValue();
        GesVmDivide(ref dst, in a, in b, vmState);
        vmState.SetValue(destinationRegister, in dst);
    }
    internal static void GesVmDivide(ref GesVmValue dst, in GesVmValue a, in GesVmValue b, GesVmState state)
    {
        switch (a.Kind)
        {
            case Integer when b.Kind is Integer:
                if (TryQuotientUnit(in a, in b, out var unit)) dst.SetFloat((double)a.IntegerValue / b.IntegerValue, unit);
                else dst.SetFloat(double.NaN);
                return;
            case Float when b.Kind is Float:
                if (TryQuotientUnit(in a, in b, out unit)) dst.SetFloat(a.FloatValue / b.FloatValue, unit);
                else dst.SetFloat(double.NaN);
                return;
            case Float or Integer when b.Kind is Float or Integer:
                if (TryQuotientUnit(in a, in b, out unit)) dst.SetFloat(a.AsNumeric / b.AsNumeric, unit);
                else dst.SetFloat(double.NaN);
                return;
            case Percentage when b.Kind is Percentage:
                dst.SetFloat(a.FloatValue / b.FloatValue);
                return;
            case Percentage when b.Kind is Integer or Float:
                if (b.HasUnit || !TryQuotientUnit(in a, in b, out unit))
                {
                    dst.SetFloat(double.NaN);
                    return;
                }

                dst.SetPercentage(a.FloatValue / b.AsNumeric);
                return;
            case Integer or Float when b.Kind is Percentage:
                if (TryQuotientUnit(in a, in b, out unit)) dst.SetFloat(a.AsNumeric / b.FloatValue, unit);
                else dst.SetFloat(double.NaN);
                return;
            case Vector when b.Kind is Integer:
                if (a.ObjectValue is GesVmValueVectorPoint vectorInt && b.IntegerValue != 0 && TryQuotientUnit(in a, in b, out unit)) dst.SetVector(vectorInt.X / b.IntegerValue, vectorInt.Y / b.IntegerValue, vectorInt.Z / b.IntegerValue, unit);
                else dst.SetFloat(double.NaN);
                return;
            case Vector when b.Kind is Float:
                if (a.ObjectValue is GesVmValueVectorPoint vectorFloat && double.IsFinite(b.FloatValue) && b.FloatValue != 0d && TryQuotientUnit(in a, in b, out unit))
                    dst.SetVector(vectorFloat.X / b.FloatValue, vectorFloat.Y / b.FloatValue, vectorFloat.Z / b.FloatValue, unit);
                else dst.SetFloat(double.NaN);
                return;
            case Vector when b.Kind is Percentage:
                if (a.ObjectValue is GesVmValueVectorPoint vectorPercent && b.FloatValue != 0d) dst.SetVector(vectorPercent.X / b.FloatValue, vectorPercent.Y / b.FloatValue, vectorPercent.Z / b.FloatValue, a.Unit);
                else dst.SetFloat(double.NaN);
                return;
            case Vector when b.Kind is Nothing:
                dst.SetNothing();
                return;
            case Vector:
                var vectorDivisor = b.AsNumeric;
                if (a.ObjectValue is GesVmValueVectorPoint vector && !double.IsNaN(vectorDivisor) && double.IsFinite(vectorDivisor) && vectorDivisor != 0d && TryQuotientUnit(in a, in b, out unit))
                    dst.SetVector(vector.X / vectorDivisor, vector.Y / vectorDivisor, vector.Z / vectorDivisor, unit);
                else dst.SetFloat(double.NaN);
                return;
            case Point when b.Kind is Nothing:
                dst.SetNothing();
                return;
            case Point:
                dst.SetFloat(double.NaN);
                return;
            case Integer or Float or Percentage when b.Kind is Vector or Point:
                dst.SetFloat(double.NaN);
                return;
            case Percentage:
                if (b.Kind is Nothing)
                {
                    dst.SetNothing();
                    return;
                }

                var percentageDivisor = b.AsNumeric;
                if (double.IsNaN(percentageDivisor) || b.HasUnit || !TryQuotientUnit(in a, in b, out unit))
                {
                    dst.SetFloat(double.NaN);
                    return;
                }

                dst.SetPercentage(a.FloatValue / percentageDivisor);
                return;
            case Nothing:
                dst.SetNothing();
                return;
        }

        if (b.Kind is Nothing)
        {
            dst.SetNothing();
            return;
        }

        if (b.Kind is Vector or Point)
        {
            dst.SetFloat(double.NaN);
            return;
        }

        var aNum = a.AsNumeric;
        if (double.IsNaN(aNum))
        {
            dst.SetFloat(double.NaN);
            return;
        }

        var bNum = b.AsNumeric;
        if (double.IsNaN(bNum))
        {
            dst.SetFloat(double.NaN);
            return;
        }

        if (a.Kind is Percentage && b.HasUnit)
        {
            dst.SetFloat(double.NaN);
            return;
        }

        if (!TryQuotientUnit(in a, in b, out var fallbackUnit))
        {
            dst.SetFloat(double.NaN);
            return;
        }

        var fallbackResult = aNum / bNum;
        if (a.Kind is Percentage) dst.SetPercentage(fallbackResult);
        else dst.SetFloat(fallbackResult, fallbackUnit);
    }
    internal static void GesVmPower(this GesVmState vmState, ushort destinationRegister, in GesVmValue a, in GesVmValue b)
    {
        var dst = new GesVmValue();
        GesVmPower(ref dst, in a, in b, vmState);
        vmState.SetValue(destinationRegister, in dst);
    }
    private static void GesVmPower(ref GesVmValue dst, in GesVmValue a, in GesVmValue b, GesVmState state)
    {
        switch (a.Kind)
        {
            case Integer when b.Kind is Integer:
                if (b.HasUnit)
                {
                    dst.SetFloat(double.NaN);
                    return;
                }

                var integerUnit = UnitNone;
                if (a.HasUnit)
                {
                    if (b.IntegerValue == 1) integerUnit = a.Unit;
                    else if (b.IntegerValue != 0)
                    {
                        dst.SetFloat(double.NaN);
                        return;
                    }
                }

                dst.SetFloat(Math.Pow(a.IntegerValue, b.IntegerValue), integerUnit);
                return;
            case Float when b.Kind is Float:
                if (b.HasUnit)
                {
                    dst.SetFloat(double.NaN);
                    return;
                }

                var floatUnit = UnitNone;
                if (a.HasUnit)
                {
                    if (b.FloatValue == 1d) floatUnit = a.Unit;
                    else if (b.FloatValue != 0d)
                    {
                        dst.SetFloat(double.NaN);
                        return;
                    }
                }

                dst.SetFloat(Math.Pow(a.FloatValue, b.FloatValue), floatUnit);
                return;
            case Float or Integer when b.Kind is Float or Integer:
                var rightNumber = b.AsNumeric;
                if (b.HasUnit)
                {
                    dst.SetFloat(double.NaN);
                    return;
                }

                var mixedUnit = UnitNone;
                if (a.HasUnit)
                {
                    if (rightNumber == 1d) mixedUnit = a.Unit;
                    else if (rightNumber != 0d)
                    {
                        dst.SetFloat(double.NaN);
                        return;
                    }
                }

                dst.SetFloat(Math.Pow(a.AsNumeric, rightNumber), mixedUnit);
                return;
            case Nothing:
                dst.SetNothing();
                return;
        }

        if (b.Kind is Nothing)
        {
            dst.SetNothing();
            return;
        }

        if (b.HasUnit)
        {
            dst.SetFloat(double.NaN);
            return;
        }

        var right = b.AsNumeric;
        if (double.IsNaN(right))
        {
            dst.SetFloat(double.NaN);
            return;
        }

        var unit = UnitNone;
        if (a.HasUnit)
        {
            if (right == 1d) unit = a.Unit;
            else if (right != 0d)
            {
                dst.SetFloat(double.NaN);
                return;
            }
        }

        var left = a.AsNumeric;
        if (double.IsNaN(left)) dst.SetFloat(double.NaN);
        else dst.SetFloat(Math.Pow(left, right), unit);
    }
    internal static void GesVmFloorDivide(this GesVmState vmState, ushort destinationRegister, in GesVmValue a, in GesVmValue b)
    {
        var dst = new GesVmValue();
        GesVmFloorDivide(ref dst, in a, in b, vmState);
        vmState.SetValue(destinationRegister, in dst);
    }
    private static void GesVmFloorDivide(ref GesVmValue dst, in GesVmValue a, in GesVmValue b, GesVmState state)
    {
        switch (a.Kind)
        {
            case Integer when b.Kind is Integer:
                if (TryQuotientUnit(in a, in b, out var integerUnit))
                {
                    var result = Math.Floor((double)a.IntegerValue / b.IntegerValue);
                    if (double.IsFinite(result) && result is >= long.MinValue and <= long.MaxValue) dst.SetInteger((long)result, integerUnit);
                    else dst.SetFloat(result, integerUnit);
                }
                else dst.SetFloat(double.NaN);

                return;
            case Float when b.Kind is Float:
                if (TryQuotientUnit(in a, in b, out var floatUnit))
                {
                    var result = Math.Floor(a.FloatValue / b.FloatValue);
                    if (double.IsFinite(result) && result is >= long.MinValue and <= long.MaxValue) dst.SetInteger((long)result, floatUnit);
                    else dst.SetFloat(result, floatUnit);
                }
                else dst.SetFloat(double.NaN);

                return;
            case Float or Integer when b.Kind is Float or Integer:
                if (TryQuotientUnit(in a, in b, out var mixedUnit))
                {
                    var result = Math.Floor(a.AsNumeric / b.AsNumeric);
                    if (double.IsFinite(result) && result is >= long.MinValue and <= long.MaxValue) dst.SetInteger((long)result, mixedUnit);
                    else dst.SetFloat(result, mixedUnit);
                }
                else dst.SetFloat(double.NaN);

                return;
            case Integer or Float when b.Kind is Percentage:
                if (TryQuotientUnit(in a, in b, out var percentageRightUnit))
                {
                    var result = Math.Floor(a.AsNumeric / b.FloatValue);
                    if (double.IsFinite(result) && result is >= long.MinValue and <= long.MaxValue) dst.SetInteger((long)result, percentageRightUnit);
                    else dst.SetFloat(result, percentageRightUnit);
                }
                else dst.SetFloat(double.NaN);

                return;
            case Percentage when b.Kind is Integer or Float or Percentage:
                if (TryQuotientUnit(in a, in b, out var percentageLeftUnit))
                {
                    var result = Math.Floor(a.FloatValue / b.AsNumeric);
                    if (double.IsFinite(result) && result is >= long.MinValue and <= long.MaxValue) dst.SetInteger((long)result, percentageLeftUnit);
                    else dst.SetFloat(result, percentageLeftUnit);
                }
                else dst.SetFloat(double.NaN);

                return;
            case Vector or Point:
                if (b.Kind is Nothing) dst.SetNothing();
                else dst.SetFloat(double.NaN);
                return;
            case Integer or Float or Percentage when b.Kind is Vector or Point:
                dst.SetFloat(double.NaN);
                return;
            case Nothing:
                dst.SetNothing();
                return;
        }

        if (b.Kind is Nothing)
        {
            dst.SetNothing();
            return;
        }

        if (b.Kind is Vector or Point || !TryQuotientUnit(in a, in b, out var unit))
        {
            dst.SetFloat(double.NaN);
            return;
        }

        var left = a.AsNumeric;
        if (double.IsNaN(left))
        {
            dst.SetFloat(double.NaN);
            return;
        }

        var right = b.AsNumeric;
        if (double.IsNaN(right))
        {
            dst.SetFloat(double.NaN);
            return;
        }

        var floorResult = Math.Floor(left / right);
        if (double.IsFinite(floorResult) && floorResult is >= long.MinValue and <= long.MaxValue) dst.SetInteger((long)floorResult, unit);
        else dst.SetFloat(floorResult, unit);
    }
    internal static void GesVmModulo(this GesVmState vmState, ushort destinationRegister, in GesVmValue a, in GesVmValue b)
    {
        var dst = new GesVmValue();
        GesVmModulo(ref dst, in a, in b, vmState);
        vmState.SetValue(destinationRegister, in dst);
    }
    private static void GesVmModulo(ref GesVmValue dst, in GesVmValue a, in GesVmValue b, GesVmState state)
    {
        switch (a.Kind)
        {
            case Integer when b.Kind is Integer:
                if (TrySameUnit(in a, in b, out var integerUnit))
                {
                    var rightInteger = b.IntegerValue;
                    if (rightInteger == 0)
                    {
                        dst.SetFloat(double.NaN, integerUnit);
                        return;
                    }

                    long integerModuloResult;
                    if (a.IntegerValue == long.MinValue && rightInteger == -1)
                    {
                        integerModuloResult = 0;
                    }
                    else
                    {
                        integerModuloResult = a.IntegerValue % rightInteger;
                        if (integerModuloResult != 0 && (integerModuloResult < 0 && rightInteger > 0 || integerModuloResult > 0 && rightInteger < 0)) integerModuloResult += rightInteger;
                    }

                    dst.SetInteger(integerModuloResult, integerUnit);
                }
                else dst.SetFloat(double.NaN);

                return;
            case Float when b.Kind is Float:
                if (TrySameUnit(in a, in b, out var floatUnit))
                {
                    var leftFloat = a.FloatValue;
                    var rightFloat = b.FloatValue;
                    if (double.IsNaN(leftFloat) || double.IsNaN(rightFloat) || double.IsInfinity(leftFloat) || rightFloat == 0d)
                    {
                        dst.SetFloat(double.NaN, floatUnit);
                        return;
                    }

                    if (double.IsInfinity(rightFloat))
                    {
                        dst.SetFloat(leftFloat, floatUnit);
                        return;
                    }

                    var floatModuloResult = leftFloat % rightFloat;
                    if (floatModuloResult != 0d && (floatModuloResult < 0d && rightFloat > 0d || floatModuloResult > 0d && rightFloat < 0d)) floatModuloResult += rightFloat;
                    dst.SetFloat(floatModuloResult, floatUnit);
                }
                else dst.SetFloat(double.NaN);

                return;
            case Float or Integer when b.Kind is Float or Integer:
                if (TrySameUnit(in a, in b, out var mixedUnit))
                {
                    var leftNumber = a.AsNumeric;
                    var rightNumber = b.AsNumeric;
                    if (double.IsNaN(leftNumber) || double.IsNaN(rightNumber) || double.IsInfinity(leftNumber) || rightNumber == 0d)
                    {
                        dst.SetFloat(double.NaN, mixedUnit);
                        return;
                    }

                    if (double.IsInfinity(rightNumber))
                    {
                        dst.SetFloat(leftNumber, mixedUnit);
                        return;
                    }

                    var mixedModuloResult = leftNumber % rightNumber;
                    if (mixedModuloResult != 0d && (mixedModuloResult < 0d && rightNumber > 0d || mixedModuloResult > 0d && rightNumber < 0d)) mixedModuloResult += rightNumber;
                    dst.SetFloat(mixedModuloResult, mixedUnit);
                }
                else dst.SetFloat(double.NaN);

                return;
            case Nothing:
                dst.SetNothing();
                return;
        }

        if (b.Kind is Nothing)
        {
            dst.SetNothing();
            return;
        }

        if (a.Kind is Vector or Point || b.Kind is Vector or Point || !TrySameUnit(in a, in b, out var unit))
        {
            dst.SetFloat(double.NaN);
            return;
        }

        var left = a.AsNumeric;
        if (double.IsNaN(left))
        {
            dst.SetFloat(double.NaN);
            return;
        }

        var right = b.AsNumeric;
        if (double.IsNaN(right) || double.IsInfinity(left) || right == 0d)
        {
            dst.SetFloat(double.NaN, unit);
            return;
        }

        if (double.IsInfinity(right))
        {
            dst.SetFloat(left, unit);
            return;
        }

        var fallbackModuloResult = left % right;
        if (fallbackModuloResult != 0d && (fallbackModuloResult < 0d && right > 0d || fallbackModuloResult > 0d && right < 0d)) fallbackModuloResult += right;
        dst.SetFloat(fallbackModuloResult, unit);
    }
    internal static void GesVmRemainder(this GesVmState vmState, ushort destinationRegister, in GesVmValue a, in GesVmValue b)
    {
        var dst = new GesVmValue();
        GesVmRemainder(ref dst, in a, in b, vmState);
        vmState.SetValue(destinationRegister, in dst);
    }
    private static void GesVmRemainder(ref GesVmValue dst, in GesVmValue a, in GesVmValue b, GesVmState state)
    {
        switch (a.Kind)
        {
            case Integer when b.Kind is Integer:
                if (TrySameUnit(in a, in b, out var integerUnit))
                {
                    var rightInteger = b.IntegerValue;
                    if (rightInteger == 0)
                    {
                        dst.SetFloat(double.NaN, integerUnit);
                        return;
                    }

                    var integerRemainderResult = a.IntegerValue == long.MinValue && rightInteger == -1
                        ? 0
                        : a.IntegerValue % rightInteger;
                    dst.SetInteger(integerRemainderResult, integerUnit);
                }
                else dst.SetFloat(double.NaN);

                return;
            case Float when b.Kind is Float:
                if (TrySameUnit(in a, in b, out var floatUnit))
                {
                    var leftFloat = a.FloatValue;
                    var rightFloat = b.FloatValue;
                    if (double.IsNaN(leftFloat) || double.IsNaN(rightFloat) || double.IsInfinity(leftFloat) || rightFloat == 0d)
                    {
                        dst.SetFloat(double.NaN, floatUnit);
                        return;
                    }

                    dst.SetFloat(double.IsInfinity(rightFloat) ? leftFloat : leftFloat % rightFloat, floatUnit);
                }
                else dst.SetFloat(double.NaN);

                return;
            case Float or Integer when b.Kind is Float or Integer:
                if (TrySameUnit(in a, in b, out var mixedUnit))
                {
                    var leftNumber = a.AsNumeric;
                    var rightNumber = b.AsNumeric;
                    if (double.IsNaN(leftNumber) || double.IsNaN(rightNumber) || double.IsInfinity(leftNumber) || rightNumber == 0d)
                    {
                        dst.SetFloat(double.NaN, mixedUnit);
                        return;
                    }

                    dst.SetFloat(double.IsInfinity(rightNumber) ? leftNumber : leftNumber % rightNumber, mixedUnit);
                }
                else dst.SetFloat(double.NaN);

                return;
            case Nothing:
                dst.SetNothing();
                return;
        }

        if (b.Kind is Nothing)
        {
            dst.SetNothing();
            return;
        }

        if (a.Kind is Vector or Point || b.Kind is Vector or Point || !TrySameUnit(in a, in b, out var unit))
        {
            dst.SetFloat(double.NaN);
            return;
        }

        var left = a.AsNumeric;
        if (double.IsNaN(left))
        {
            dst.SetFloat(double.NaN);
            return;
        }

        var right = b.AsNumeric;
        if (double.IsNaN(right) || double.IsInfinity(left) || right == 0d)
        {
            dst.SetFloat(double.NaN, unit);
            return;
        }

        dst.SetFloat(double.IsInfinity(right) ? left : left % right, unit);
    }
    internal static void GesVmMin(this GesVmState vmState, ushort destinationRegister, in GesVmValue a, in GesVmValue b)
    {
        var dst = new GesVmValue();
        GesVmMin(ref dst, in a, in b, vmState);
        vmState.SetValue(destinationRegister, in dst);
    }
    private static void GesVmMin(ref GesVmValue dst, in GesVmValue a, in GesVmValue b, GesVmState state)
    {
        switch (a.Kind)
        {
            case Integer when b.Kind is Integer:
                if (!TrySameUnit(in a, in b, out _)) dst.SetFloat(double.NaN);
                else dst = b.IntegerValue < a.IntegerValue ? b : a;
                return;
            case Float when b.Kind is Float:
                if (!TrySameUnit(in a, in b, out _) || double.IsNaN(a.FloatValue) || double.IsNaN(b.FloatValue)) dst.SetFloat(double.NaN);
                else dst = b.FloatValue < a.FloatValue ? b : a;
                return;
            case Float or Integer when b.Kind is Float or Integer:
                if (!TrySameUnit(in a, in b, out _))
                {
                    dst.SetFloat(double.NaN);
                    return;
                }

                var fastLeft = a.AsNumeric;
                var fastRight = b.AsNumeric;
                if (double.IsNaN(fastLeft) || double.IsNaN(fastRight)) dst.SetFloat(double.NaN);
                else dst = fastRight < fastLeft ? b : a;
                return;
            case Nothing:
                dst.SetNothing();
                return;
        }

        if (b.Kind is Nothing)
        {
            dst.SetNothing();
            return;
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
            if (!TrySameUnit(in a, in b, out _) || double.IsNaN(leftNumber) || double.IsNaN(rightNumber))
            {
                dst.SetFloat(double.NaN);
                return;
            }

            dst = rightNumber < leftNumber ? b : a;
            return;
        }

        if (leftRank != rightRank)
        {
            dst = rightRank < leftRank ? b : a;
            return;
        }

        var comparison = 0;
        switch (a.Kind)
        {
            case Text when b.Kind is Text:
                leftText ??= a.TextValue;
                rightText ??= b.TextValue;
                comparison = StringComparer.Ordinal.Compare(rightText, leftText);
                break;
            case Tag when b.Kind is Tag:
                leftText ??= a.TextValue;
                rightText ??= b.TextValue;
                comparison = StringComparer.Ordinal.Compare(rightText, leftText);
                break;
            case Vector when b.Kind is Vector:
            case Point when b.Kind is Point:
                if (a.Unit != b.Unit)
                {
                    comparison = b.Unit.CompareTo(a.Unit);
                    break;
                }

                if (a.ObjectValue is GesVmValueVectorPoint leftTriplet && b.ObjectValue is GesVmValueVectorPoint rightTriplet)
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
    }
    internal static void GesVmMax(this GesVmState vmState, ushort destinationRegister, in GesVmValue a, in GesVmValue b)
    {
        var dst = new GesVmValue();
        GesVmMax(ref dst, in a, in b, vmState);
        vmState.SetValue(destinationRegister, in dst);
    }
    private static void GesVmMax(ref GesVmValue dst, in GesVmValue a, in GesVmValue b, GesVmState state)
    {
        switch (a.Kind)
        {
            case Integer when b.Kind is Integer:
                if (!TrySameUnit(in a, in b, out _)) dst.SetFloat(double.NaN);
                else dst = b.IntegerValue > a.IntegerValue ? b : a;
                return;
            case Float when b.Kind is Float:
                if (!TrySameUnit(in a, in b, out _) || double.IsNaN(a.FloatValue) || double.IsNaN(b.FloatValue)) dst.SetFloat(double.NaN);
                else dst = b.FloatValue > a.FloatValue ? b : a;
                return;
            case Float or Integer when b.Kind is Float or Integer:
                if (!TrySameUnit(in a, in b, out _))
                {
                    dst.SetFloat(double.NaN);
                    return;
                }

                var fastLeft = a.AsNumeric;
                var fastRight = b.AsNumeric;
                if (double.IsNaN(fastLeft) || double.IsNaN(fastRight)) dst.SetFloat(double.NaN);
                else dst = fastRight > fastLeft ? b : a;
                return;
            case Nothing:
                dst.SetNothing();
                return;
        }

        if (b.Kind is Nothing)
        {
            dst.SetNothing();
            return;
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
            if (!TrySameUnit(in a, in b, out _) || double.IsNaN(leftNumber) || double.IsNaN(rightNumber))
            {
                dst.SetFloat(double.NaN);
                return;
            }

            dst = rightNumber > leftNumber ? b : a;
            return;
        }

        if (leftRank != rightRank)
        {
            dst = rightRank > leftRank ? b : a;
            return;
        }

        var comparison = 0;
        switch (a.Kind)
        {
            case Text when b.Kind is Text:
                leftText ??= a.TextValue;
                rightText ??= b.TextValue;
                comparison = StringComparer.Ordinal.Compare(rightText, leftText);
                break;
            case Tag when b.Kind is Tag:
                leftText ??= a.TextValue;
                rightText ??= b.TextValue;
                comparison = StringComparer.Ordinal.Compare(rightText, leftText);
                break;
            case Vector when b.Kind is Vector:
            case Point when b.Kind is Point:
                if (a.Unit != b.Unit)
                {
                    comparison = b.Unit.CompareTo(a.Unit);
                    break;
                }

                if (a.ObjectValue is GesVmValueVectorPoint leftTriplet && b.ObjectValue is GesVmValueVectorPoint rightTriplet)
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
    }
    internal static void GesVmNegate(this GesVmState vmState, ushort destinationRegister, in GesVmValue a)
    {
        var dst = new GesVmValue();
        GesVmNegate(ref dst, in a, vmState);
        vmState.SetValue(destinationRegister, in dst);
    }
    private static void GesVmNegate(ref GesVmValue dst, in GesVmValue a, GesVmState state)
    {
        switch (a.Kind)
        {
            case Integer:
                if (a.IntegerValue == long.MinValue) dst.SetFloat(-(double)a.IntegerValue, a.Unit);
                else dst.SetInteger(-a.IntegerValue, a.Unit);
                return;
            case Float:
                dst.SetFloat(-a.FloatValue, a.Unit);
                return;
            case Percentage:
                dst.SetPercentage(-a.FloatValue);
                return;
            case Vector:
                if (a.ObjectValue is GesVmValueVectorPoint vector)
                {
                    dst.SetVector(-vector.X, -vector.Y, -vector.Z, a.Unit);
                }
                else dst.SetFloat(double.NaN);

                return;
            case Point:
                dst.SetFloat(double.NaN);
                return;
            case Nothing:
                dst.SetNothing();
                return;
            default:
                var number = a.AsNumeric;
                if (double.IsNaN(number)) dst.SetFloat(double.NaN);
                else dst.SetFloat(-number);
                return;
        }
    }
    internal static void GesVmAbs(this GesVmState vmState, ushort destinationRegister, in GesVmValue a)
    {
        var dst = new GesVmValue();
        GesVmAbs(ref dst, in a, vmState);
        vmState.SetValue(destinationRegister, in dst);
    }
    private static void GesVmAbs(ref GesVmValue dst, in GesVmValue a, GesVmState state)
    {
        switch (a.Kind)
        {
            case Integer:
                if (a.IntegerValue == long.MinValue) dst.SetFloat(-(double)a.IntegerValue, a.Unit);
                else dst.SetInteger(Math.Abs(a.IntegerValue), a.Unit);
                return;
            case Float:
                dst.SetFloat(Math.Abs(a.FloatValue), a.Unit);
                return;
            case Percentage:
                dst.SetPercentage(Math.Abs(a.FloatValue));
                return;
            case Vector:
                if (a.ObjectValue is not GesVmValueVectorPoint vector)
                {
                    dst.SetFloat(double.NaN);
                    return;
                }

                var length = Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y + vector.Z * vector.Z);
                if (double.IsNaN(length)) dst.SetFloat(double.NaN);
                else dst.SetFloat(length, a.Unit);
                return;
            case Point:
                dst.SetFloat(double.NaN);
                return;
            case Nothing:
                dst.SetNothing();
                return;
            default:
                var number = a.AsNumeric;
                dst.SetFloat(double.IsNaN(number) ? double.NaN : Math.Abs(number));
                return;
        }
    }
    internal static void GesVmClamp(this GesVmState vmState, ushort destinationRegister, in GesVmValue value, in GesVmValue min, in GesVmValue max)
    {
        var dst = new GesVmValue();
        GesVmClamp(ref dst, in value, in min, in max, vmState);
        vmState.SetValue(destinationRegister, in dst);
    }
    private static void GesVmClamp(ref GesVmValue dst, in GesVmValue value, in GesVmValue min, in GesVmValue max, GesVmState state)
    {
        switch (value.Kind)
        {
            case Integer when min.Kind is Integer && max.Kind is Integer:
            {
                if (!TrySameUnit(in value, in min, in max, out var integerUnit))
                {
                    dst.SetFloat(double.NaN);
                    return;
                }

                var lower = min.IntegerValue <= max.IntegerValue ? min.IntegerValue : max.IntegerValue;
                var upper = min.IntegerValue <= max.IntegerValue ? max.IntegerValue : min.IntegerValue;
                var result = value.IntegerValue < lower ? lower : value.IntegerValue > upper ? upper : value.IntegerValue;
                dst.SetInteger(result, integerUnit);
                return;
            }
            case Float when min.Kind is Float && max.Kind is Float:
            {
                if (!TrySameUnit(in value, in min, in max, out var floatUnit))
                {
                    dst.SetFloat(double.NaN);
                    return;
                }

                var lower = min.FloatValue <= max.FloatValue ? min.FloatValue : max.FloatValue;
                var upper = min.FloatValue <= max.FloatValue ? max.FloatValue : min.FloatValue;
                if (double.IsNaN(value.FloatValue) || double.IsNaN(lower) || double.IsNaN(upper))
                {
                    dst.SetFloat(double.NaN, floatUnit);
                    return;
                }

                var result = value.FloatValue < lower ? lower : value.FloatValue > upper ? upper : value.FloatValue;
                dst.SetFloat(result, floatUnit);
                return;
            }
            case Float or Integer when min.Kind is Float or Integer && max.Kind is Float or Integer:
            {
                if (!TrySameUnit(in value, in min, in max, out var mixedUnit))
                {
                    dst.SetFloat(double.NaN);
                    return;
                }

                var x = value.AsNumeric;
                var a = min.AsNumeric;
                var b = max.AsNumeric;
                var lower = a <= b ? a : b;
                var upper = a <= b ? b : a;
                if (double.IsNaN(x) || double.IsNaN(lower) || double.IsNaN(upper))
                {
                    dst.SetFloat(double.NaN, mixedUnit);
                    return;
                }

                var result = x < lower ? lower : x > upper ? upper : x;
                dst.SetFloat(result, mixedUnit);
                return;
            }
            case Nothing:
                dst.SetNothing();
                return;
            case Vector or Point:
                if (min.Kind is Nothing || max.Kind is Nothing) dst.SetNothing();
                else dst.SetFloat(double.NaN);
                return;
        }

        if (min.Kind is Nothing || max.Kind is Nothing)
        {
            dst.SetNothing();
            return;
        }

        if (min.Kind is Vector or Point || max.Kind is Vector or Point || !TrySameUnit(in value, in min, in max, out var unit))
        {
            dst.SetFloat(double.NaN);
            return;
        }

        var raw = value.AsNumeric;
        var minimum = min.AsNumeric;
        var maximum = max.AsNumeric;
        var lowerFallback = minimum <= maximum ? minimum : maximum;
        var upperFallback = minimum <= maximum ? maximum : minimum;
        if (double.IsNaN(raw) || double.IsNaN(lowerFallback) || double.IsNaN(upperFallback))
        {
            dst.SetFloat(double.NaN, unit);
            return;
        }

        var fallbackResult = raw < lowerFallback ? lowerFallback : raw > upperFallback ? upperFallback : raw;
        dst.SetFloat(fallbackResult, unit);
    }
    internal static void GesVmNaturalLog(this GesVmState vmState, ushort destinationRegister, in GesVmValue a)
    {
        var dst = new GesVmValue();
        GesVmNaturalLog(ref dst, in a, vmState);
        vmState.SetValue(destinationRegister, in dst);
    }
    private static void GesVmNaturalLog(ref GesVmValue dst, in GesVmValue a, GesVmState state)
    {
        switch (a.Kind)
        {
            case Integer:
                if (a.HasUnit) dst.SetFloat(double.NaN);
                else if (a.IntegerValue < 0) dst.SetFloat(double.NaN);
                else if (a.IntegerValue == 0) dst.SetFloat(double.NegativeInfinity);
                else dst.SetFloat(Math.Log(a.IntegerValue));
                return;
            case Float:
                if (a.HasUnit)
                {
                    dst.SetFloat(double.NaN);
                    return;
                }

                var floatValue = a.FloatValue;
                if (double.IsNaN(floatValue) || floatValue < 0d || double.IsNegativeInfinity(floatValue)) dst.SetFloat(double.NaN);
                else if (double.IsPositiveInfinity(floatValue)) dst.SetFloat(double.PositiveInfinity);
                else dst.SetFloat(Math.Log(floatValue));
                return;
            case Percentage:
                var percentageValue = a.FloatValue;
                if (double.IsNaN(percentageValue) || percentageValue < 0d || double.IsNegativeInfinity(percentageValue)) dst.SetFloat(double.NaN);
                else if (double.IsPositiveInfinity(percentageValue)) dst.SetFloat(double.PositiveInfinity);
                else dst.SetFloat(Math.Log(percentageValue));
                return;
            case Nothing:
                dst.SetNothing();
                return;
            default:
                if (a.HasUnit)
                {
                    dst.SetFloat(double.NaN);
                    return;
                }

                var number = a.AsNumeric;
                if (double.IsNaN(number) || number < 0d || double.IsNegativeInfinity(number)) dst.SetFloat(double.NaN);
                else if (double.IsPositiveInfinity(number)) dst.SetFloat(double.PositiveInfinity);
                else dst.SetFloat(Math.Log(number));
                return;
        }
    }
    internal static void GesVmExp(this GesVmState vmState, ushort destinationRegister, in GesVmValue a)
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
    internal static void GesVmFloor(this GesVmState vmState, ushort destinationRegister, in GesVmValue a)
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
    internal static void GesVmCeil(this GesVmState vmState, ushort destinationRegister, in GesVmValue a)
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
    internal static void GesVmTruncate(this GesVmState vmState, ushort destinationRegister, in GesVmValue a)
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
    internal static void GesVmRoundHalfEven(this GesVmState vmState, ushort destinationRegister, in GesVmValue a)
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
    internal static void GesVmRoundHalfUp(this GesVmState vmState, ushort destinationRegister, in GesVmValue a)
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
    internal static void GesVmRoundHalfDown(this GesVmState vmState, ushort destinationRegister, in GesVmValue a)
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
        var sign = Math.Sign(value);
        var absolute = Math.Abs(value);
        var floor = Math.Floor(absolute);
        var fraction = absolute - floor;
        var roundedAbsolute = fraction > 0.5d ? floor + 1d : floor;
        vmState.SetInteger(destinationRegister, ToIntegerSaturated(sign < 0 ? -roundedAbsolute : roundedAbsolute));
    }
    internal static void GesVmDegreeToRadians(this GesVmState vmState, ushort destinationRegister, in GesVmValue a)
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
    internal static void GesVmDegreeFromRadians(this GesVmState vmState, ushort destinationRegister, in GesVmValue a)
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
    internal static void GesVmWrapDegree(this GesVmState vmState, ushort destinationRegister, in GesVmValue a)
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
    private static long ToIntegerSaturated(double number)
    {
        if (double.IsNaN(number)) return 0;
        if (double.IsPositiveInfinity(number) || number > long.MaxValue) return long.MaxValue;
        if (double.IsNegativeInfinity(number) || number < long.MinValue) return long.MinValue;
        return (long)Math.Truncate(number);
    }
    internal static void GesVmTerm(this GesVmState vmState, ushort destinationRegister, in GesVmValue source, in GesVmValue termValue)
    {
        var dst = new GesVmValue();
        GesVmTerm(ref dst, in source, in termValue, vmState);
        vmState.SetValue(destinationRegister, in dst);
    }
    private static void GesVmTerm(ref GesVmValue dst, in GesVmValue source, in GesVmValue termValue, GesVmState state)
    {
        long index;
        bool ret;
        switch (termValue.Kind)
        {
            case Integer:
                index = termValue.IntegerValue;
                ret = true;
                break;
            case Float or Percentage:
                switch (termValue.FloatValue)
                {
                    case double.NaN:
                        index = 0;
                        ret = false;
                        break;
                    case <= long.MinValue:
                        index = long.MinValue;
                        ret = true;
                        break;
                    case >= long.MaxValue:
                        index = long.MaxValue;
                        ret = true;
                        break;
                    default:
                        index = (long)termValue.FloatValue;
                        ret = true;
                        break;
                }

                break;
            case GameEventScriptBytecodeTypeKind.Boolean:
                index = termValue.IsTrue ? 1 : 0;
                ret = true;
                break;
            default:
                if (termValue.IsNumeric)
                {
                    switch (termValue.AsNumeric)
                    {
                        case Double.NaN:
                            index = 0;
                            ret = false;
                            break;
                        case <= long.MinValue:
                            index = long.MinValue;
                            ret = true;
                            break;
                        case >= long.MaxValue:
                            index = long.MaxValue;
                            ret = true;
                            break;
                        default:
                            index = (long)termValue.AsNumeric;
                            ret = true;
                            break;
                    }

                    break;
                }

                index = 0;
                ret = false;
                break;
        }

        if (source.Kind is not Series || source.ObjectValue is not GesVmSeries series || !ret) dst.SetNothing();
        else series.TryGetTerm(index, ref dst);
    }
}
