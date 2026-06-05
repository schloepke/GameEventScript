using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeInstructionUnit;
using static StepH.GameEventScript.BytecodeExecutor.VmMathConstants;

namespace StepH.GameEventScript.BytecodeExecutor;

[SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
internal static class VmRegisterMath
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmAdd(ref this VmValue dst, ref VmValue a, ref VmValue b, ref GameEventScriptTextTable textTable)
    {
        switch (a.Kind)
        {
            case Integer when b.Kind is Integer:
                if (TrySameUnit(ref a, ref b, out var unit)) dst.SetInteger(a.IntegerValue + b.IntegerValue, unit);
                else dst.SetFloat(double.NaN);
                break;
            case Float when b.Kind is Float:
                if (TrySameUnit(ref a, ref b, out unit)) dst.SetFloat(a.FloatValue + b.FloatValue, unit);
                else dst.SetFloat(double.NaN);
                break;
            case Float or Integer when b.Kind is Float or Integer:
                if (TrySameUnit(ref a, ref b, out unit)) dst.SetFloat(a.AsNumeric + b.AsNumeric, unit);
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
                if (a.ObjectValue is VmFloatTriplet av && b.ObjectValue is VmFloatTriplet bv && TrySameUnit(ref a, ref b, out var vectorUnit)) dst.SetVector(av.X + bv.X, av.Y + bv.Y, av.Z + bv.Z, vectorUnit);
                else dst.SetFloat(double.NaN);
                break;
            case Point when b.Kind is Vector:
                if (a.ObjectValue is VmFloatTriplet ap && b.ObjectValue is VmFloatTriplet bvv && TrySameUnit(ref a, ref b, out var pointUnit)) dst.SetPoint(ap.X + bvv.X, ap.Y + bvv.Y, ap.Z + bvv.Z, pointUnit);
                else dst.SetFloat(double.NaN);
                break;
            case Integer or Float or Percentage when b.Kind is Vector or Point:
                dst.SetFloat(double.NaN);
                break;
            case Text when b.Kind is not Nothing:
                dst.SetText(a.ReadTextOrTag() + b.ConvertToText());
                break;
            case not Nothing when b.Kind is Text:
                dst.SetText(a.ConvertToText() + b.ReadTextOrTag());
                break;
            case List when b.Kind is not Nothing && a.ObjectValue is VmListObject aList:
            {
                var list = new VmListObject(dst.OwningState, aList.Length + 1);
                for (var i = 0; i < aList.Length; i++) list.Items[i] = aList.Items[i];
                list.Items[aList.Length] = b;
                dst.SetList(list);
                break;
            }
            case not List and not Nothing when b.Kind is List && b.ObjectValue is VmListObject rightList:
            {
                var list = new VmListObject(dst.OwningState, rightList.Length + 1);
                list.Items[0] = a;
                for (var i = 0; i < rightList.Length; i++) list.Items[i + 1] = rightList.Items[i];
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
                    else if (TrySameUnit(ref a, ref b, out unit)) dst.SetFloat(aNum + bNum, unit);
                    else dst.SetFloat(double.NaN);
                }

                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmSubtract(ref this VmValue dst, ref VmValue a, ref VmValue b, ref GameEventScriptTextTable textTable)
    {
        switch (a.Kind)
        {
            case Integer when b.Kind is Integer:
                if (TrySameUnit(ref a, ref b, out var unit)) dst.SetInteger(a.IntegerValue - b.IntegerValue, unit);
                else dst.SetFloat(double.NaN);
                break;
            case Float when b.Kind is Float:
                if (TrySameUnit(ref a, ref b, out unit)) dst.SetFloat(a.FloatValue - b.FloatValue, unit);
                else dst.SetFloat(double.NaN);
                break;
            case Float or Integer when b.Kind is Float or Integer:
                if (TrySameUnit(ref a, ref b, out unit)) dst.SetFloat(a.AsNumeric - b.AsNumeric, unit);
                else dst.SetFloat(double.NaN);
                break;
            case Integer when b.Kind is Percentage:
                dst.SetFloat(a.IntegerValue - a.IntegerValue * b.FloatValue, a.Unit);
                break;
            case Float when b.Kind is Percentage:
                dst.SetFloat(a.FloatValue - a.FloatValue * b.FloatValue, a.Unit);
                break;
            case Percentage when b.Kind is Percentage:
                dst.SetPercentage(a.FloatValue - b.FloatValue);
                break;
            case Vector when b.Kind is Vector:
            {
                if (a.ObjectValue is VmFloatTriplet av && b.ObjectValue is VmFloatTriplet bv && TrySameUnit(ref a, ref b, out var vectorUnit)) dst.SetVector(av.X - bv.X, av.Y - bv.Y, av.Z - bv.Z, vectorUnit);
                else dst.SetFloat(double.NaN);
                return;
            }
            case Vector:
                if (b.Kind is not Nothing) dst.SetFloat(double.NaN);
                else dst.SetNothing();
                return;
            case Point when b.Kind is Vector:
            {
                if (a.ObjectValue is VmFloatTriplet ap && b.ObjectValue is VmFloatTriplet bv && TrySameUnit(ref a, ref b, out var pointUnit)) dst.SetPoint(ap.X - bv.X, ap.Y - bv.Y, ap.Z - bv.Z, pointUnit);
                else dst.SetFloat(double.NaN);
                return;
            }
            case Point when b.Kind is Point:
            {
                if (a.ObjectValue is VmFloatTriplet ap && b.ObjectValue is VmFloatTriplet bp && TrySameUnit(ref a, ref b, out var pointUnit)) dst.SetVector(ap.X - bp.X, ap.Y - bp.Y, ap.Z - bp.Z, pointUnit);
                else dst.SetFloat(double.NaN);
                return;
            }
            case Point:
                if (b.Kind is not Nothing) dst.SetFloat(double.NaN);
                else dst.SetNothing();
                return;
            case Integer or Float or Percentage when b.Kind is Vector or Point:
                dst.SetFloat(double.NaN);
                return;
            case List when b.Kind is List or Dice and not Nothing or not Map && a.ObjectValue is VmListObject aList:
            {
                var removeList = b.Kind is List && b.ObjectValue is VmListObject bList ? bList : null;
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
                        var candidate = removeList is not null ? removeList.Items[j] : removeDice is not null ? dst.OwningState.CreateInteger(removeDice[j]) : b;
                        if (!candidate.EqualsValue(ref aList.Items[i])) continue;
                        removed[j] = true;
                        shouldRemove = true;
                        break;
                    }

                    if (!shouldRemove) resultLength++;
                }

                var list = new VmListObject(dst.OwningState, resultLength);
                var index = 0;
                Array.Clear(removed, 0, removed.Length);
                for (var i = 0; i < aList.Length; i++)
                {
                    var shouldRemove = false;
                    for (var j = 0; j < removeCount; j++)
                    {
                        if (removed[j]) continue;
                        var candidate = removeList is not null ? removeList.Items[j] : removeDice is not null ? dst.OwningState.CreateInteger(removeDice[j]) : b;
                        if (!candidate.EqualsValue(ref aList.Items[i])) continue;
                        removed[j] = true;
                        shouldRemove = true;
                        break;
                    }

                    if (!shouldRemove) list.Items[index++] = aList.Items[i];
                }

                dst.SetList(list);
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

                dst.SetDice(dice);
                return;
            }
            case Dice when b.Kind is List && a.ObjectValue is int[] aDice && b.ObjectValue is VmListObject removeList:
            {
                var removed = new bool[removeList.Length];
                var resultLength = 0;
                for (var i = 0; i < aDice.Length; i++)
                {
                    var item = dst.OwningState.CreateInteger(aDice[i]);
                    var shouldRemove = false;
                    for (var j = 0; j < removeList.Length; j++)
                    {
                        if (removed[j] || !item.EqualsValue(ref removeList.Items[j])) continue;
                        removed[j] = true;
                        shouldRemove = true;
                        break;
                    }

                    if (!shouldRemove) resultLength++;
                }

                var list = new VmListObject(dst.OwningState, resultLength);
                var index = 0;
                Array.Clear(removed, 0, removed.Length);
                for (var i = 0; i < aDice.Length; i++)
                {
                    var item = dst.OwningState.CreateInteger(aDice[i]);
                    var shouldRemove = false;
                    for (var j = 0; j < removeList.Length; j++)
                    {
                        if (removed[j] || !item.EqualsValue(ref removeList.Items[j])) continue;
                        removed[j] = true;
                        shouldRemove = true;
                        break;
                    }

                    if (!shouldRemove) list.Items[index++].SetInteger(aDice[i]);
                }

                dst.SetList(list);
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

                dst.SetDice(dice);
                return;
            }
            case Map when b.Kind is not Nothing && a.ObjectValue is VmMapObject aMap:
            {
                var keys = new HashSet<string>(StringComparer.Ordinal);
                if (b.Kind is Map && b.ObjectValue is VmMapObject bMap)
                {
                    foreach (var key in bMap.Entries.Keys)
                    {
                        keys.Add(key);
                    }
                }
                else if (b.Kind is Text or Tag)
                {
                    keys.Add(b.ReadTextOrTag());
                }
                else if (b.Kind is List && b.ObjectValue is VmListObject keyList)
                {
                    for (var i = 0; i < keyList.Length; i++)
                    {
                        var keyValue = keyList.Items[i];
                        if (keyValue.Kind is not (Text or Tag))
                        {
                            dst.SetNothing();
                            return;
                        }

                        keys.Add(keyValue.ReadTextOrTag());
                    }
                }
                else
                {
                    dst.SetNothing();
                    return;
                }

                var map = new Dictionary<string, VmValue>(StringComparer.Ordinal);
                foreach (var pair in aMap.Entries)
                {
                    if (!keys.Contains(pair.Key))
                    {
                        map[pair.Key] = pair.Value;
                    }
                }

                dst.SetMap(new VmMapObject(dst.OwningState, map));
                return;
            }
            case not List and not Map and not Nothing when b.Kind is List or Map:
                dst.SetNothing();
                return;
            case Percentage:
                if (b.Kind is not Nothing) dst.SetFloat(double.NaN);
                else dst.SetNothing();
                break;
            case Dice:
                dst.SetNothing();
                break;
            case Nothing:
                dst.SetNothing();
                return;
            default:
                if (b.Kind is Nothing)
                {
                    dst.SetNothing();
                }
                else
                {
                    var aNum = a.AsNumeric;
                    if (double.IsNaN(aNum))
                    {
                        dst.SetFloat(double.NaN);
                    }
                    else
                    {
                        var bNum = b.AsNumeric;
                        if (double.IsNaN(bNum)) dst.SetFloat(double.NaN);
                        else if (b.Kind is Percentage) dst.SetFloat(aNum - aNum * bNum, a.Unit);
                        else if (TrySameUnit(ref a, ref b, out unit)) dst.SetFloat(aNum - bNum, unit);
                        else dst.SetFloat(double.NaN);
                    }
                }

                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmMultiply(ref this VmValue dst, ref VmValue a, ref VmValue b, ref GameEventScriptTextTable textTable)
    {
        switch (a.Kind)
        {
            case Integer when b.Kind is Integer:
                if (TryProductUnit(ref a, ref b, out var unit)) dst.SetFloat((double)a.IntegerValue * b.IntegerValue, unit);
                else dst.SetFloat(double.NaN);
                return;
            case Float when b.Kind is Float:
                if (TryProductUnit(ref a, ref b, out unit)) dst.SetFloat(a.FloatValue * b.FloatValue, unit);
                else dst.SetFloat(double.NaN);
                return;
            case Float or Integer when b.Kind is Float or Integer:
                if (TryProductUnit(ref a, ref b, out unit)) dst.SetFloat(a.AsNumeric * b.AsNumeric, unit);
                else dst.SetFloat(double.NaN);
                return;
            case Integer when b.Kind is Percentage:
                dst.SetFloat(a.IntegerValue * b.FloatValue, a.Unit);
                return;
            case Float when b.Kind is Percentage:
                dst.SetFloat(a.FloatValue * b.FloatValue, a.Unit);
                return;
            case Percentage when b.Kind is Percentage:
                dst.SetPercentage(a.FloatValue * b.FloatValue);
                return;
            case Percentage when b.Kind is Integer:
                var percentageIntegerResult = a.FloatValue * b.IntegerValue;
                dst.SetFloat(percentageIntegerResult, b.Unit);
                return;
            case Percentage when b.Kind is Float:
                var percentageFloatResult = a.FloatValue * b.FloatValue;
                dst.SetFloat(percentageFloatResult, b.Unit);
                return;
            case Percentage when b.Kind is Vector:
                if (b.ObjectValue is VmFloatTriplet percentageVector) dst.SetVector(a.FloatValue * percentageVector.X, a.FloatValue * percentageVector.Y, a.FloatValue * percentageVector.Z, b.Unit);
                else dst.SetFloat(double.NaN);
                return;
            case Percentage when b.Kind is Point:
                dst.SetFloat(double.NaN);
                return;
            case Vector when b.Kind is Integer:
                if (a.ObjectValue is VmFloatTriplet vectorInt && TryProductUnit(ref a, ref b, out unit)) dst.SetVector(vectorInt.X * b.IntegerValue, vectorInt.Y * b.IntegerValue, vectorInt.Z * b.IntegerValue, unit);
                else dst.SetFloat(double.NaN);
                return;
            case Vector when b.Kind is Float:
                if (a.ObjectValue is VmFloatTriplet vectorFloat && double.IsFinite(b.FloatValue) && TryProductUnit(ref a, ref b, out unit)) dst.SetVector(vectorFloat.X * b.FloatValue, vectorFloat.Y * b.FloatValue, vectorFloat.Z * b.FloatValue, unit);
                else dst.SetFloat(double.NaN);
                return;
            case Vector when b.Kind is Percentage:
                if (a.ObjectValue is VmFloatTriplet vectorPercent) dst.SetVector(vectorPercent.X * b.FloatValue, vectorPercent.Y * b.FloatValue, vectorPercent.Z * b.FloatValue, a.Unit);
                else dst.SetFloat(double.NaN);
                return;
            case Vector when b.Kind is Nothing:
                dst.SetNothing();
                return;
            case Vector:
                var vectorScalar = b.AsNumeric;
                if (a.ObjectValue is VmFloatTriplet vector && !double.IsNaN(vectorScalar) && double.IsFinite(vectorScalar) && TryProductUnit(ref a, ref b, out unit))
                    dst.SetVector(vector.X * vectorScalar, vector.Y * vectorScalar, vector.Z * vectorScalar, unit);
                else dst.SetFloat(double.NaN);
                return;
            case Point when b.Kind is Nothing:
                dst.SetNothing();
                return;
            case Point:
                dst.SetFloat(double.NaN);
                return;
            case Integer or Float when b.Kind is Vector:
                var scalar = a.AsNumeric;
                if (b.ObjectValue is VmFloatTriplet rightVector && double.IsFinite(scalar) && TryProductUnit(ref a, ref b, out unit)) dst.SetVector(scalar * rightVector.X, scalar * rightVector.Y, scalar * rightVector.Z, unit);
                else dst.SetFloat(double.NaN);
                return;
            case Integer or Float when b.Kind is Point:
                dst.SetFloat(double.NaN);
                return;
            case Percentage:
                if (b.Kind is Nothing)
                {
                    dst.SetNothing();
                    return;
                }

                var percentageScalar = b.AsNumeric;
                if (double.IsNaN(percentageScalar))
                {
                    dst.SetFloat(double.NaN);
                    return;
                }

                var percentageResult = a.FloatValue * percentageScalar;
                if (b.HasUnit) dst.SetFloat(percentageResult, b.Unit);
                else dst.SetFloat(percentageResult);
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

        var fallbackLeft = a.AsNumeric;
        if (double.IsNaN(fallbackLeft))
        {
            dst.SetFloat(double.NaN);
            return;
        }

        switch (b.Kind)
        {
            case Vector:
            {
                if (b.ObjectValue is VmFloatTriplet vector && double.IsFinite(fallbackLeft) && TryProductUnit(ref a, ref b, out var vectorUnit)) dst.SetVector(fallbackLeft * vector.X, fallbackLeft * vector.Y, fallbackLeft * vector.Z, vectorUnit);
                else dst.SetFloat(double.NaN);
                return;
            }
            case Point:
            {
                dst.SetFloat(double.NaN);
                return;
            }
        }

        var bNum = b.AsNumeric;
        if (double.IsNaN(bNum) || !TryProductUnit(ref a, ref b, out var fallbackUnit))
        {
            dst.SetFloat(double.NaN);
            return;
        }

        dst.SetFloat(fallbackLeft * bNum, b.Kind is Percentage ? a.Unit : fallbackUnit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmDivide(ref this VmValue dst, ref VmValue a, ref VmValue b, ref GameEventScriptTextTable textTable)
    {
        switch (a.Kind)
        {
            case Integer when b.Kind is Integer:
                if (TryQuotientUnit(ref a, ref b, out var unit)) dst.SetFloat((double)a.IntegerValue / b.IntegerValue, unit);
                else dst.SetFloat(double.NaN);
                return;
            case Float when b.Kind is Float:
                if (TryQuotientUnit(ref a, ref b, out unit)) dst.SetFloat(a.FloatValue / b.FloatValue, unit);
                else dst.SetFloat(double.NaN);
                return;
            case Float or Integer when b.Kind is Float or Integer:
                if (TryQuotientUnit(ref a, ref b, out unit)) dst.SetFloat(a.AsNumeric / b.AsNumeric, unit);
                else dst.SetFloat(double.NaN);
                return;
            case Percentage when b.Kind is Percentage:
                dst.SetFloat(a.FloatValue / b.FloatValue);
                return;
            case Percentage when b.Kind is Integer or Float:
                if (b.HasUnit || !TryQuotientUnit(ref a, ref b, out unit))
                {
                    dst.SetFloat(double.NaN);
                    return;
                }

                dst.SetPercentage(a.FloatValue / b.AsNumeric);
                return;
            case Integer or Float when b.Kind is Percentage:
                if (TryQuotientUnit(ref a, ref b, out unit)) dst.SetFloat(a.AsNumeric / b.FloatValue, unit);
                else dst.SetFloat(double.NaN);
                return;
            case Vector when b.Kind is Integer:
                if (a.ObjectValue is VmFloatTriplet vectorInt && b.IntegerValue != 0 && TryQuotientUnit(ref a, ref b, out unit)) dst.SetVector(vectorInt.X / b.IntegerValue, vectorInt.Y / b.IntegerValue, vectorInt.Z / b.IntegerValue, unit);
                else dst.SetFloat(double.NaN);
                return;
            case Vector when b.Kind is Float:
                if (a.ObjectValue is VmFloatTriplet vectorFloat && double.IsFinite(b.FloatValue) && b.FloatValue != 0d && TryQuotientUnit(ref a, ref b, out unit))
                    dst.SetVector(vectorFloat.X / b.FloatValue, vectorFloat.Y / b.FloatValue, vectorFloat.Z / b.FloatValue, unit);
                else dst.SetFloat(double.NaN);
                return;
            case Vector when b.Kind is Percentage:
                if (a.ObjectValue is VmFloatTriplet vectorPercent && b.FloatValue != 0d) dst.SetVector(vectorPercent.X / b.FloatValue, vectorPercent.Y / b.FloatValue, vectorPercent.Z / b.FloatValue, a.Unit);
                else dst.SetFloat(double.NaN);
                return;
            case Vector when b.Kind is Nothing:
                dst.SetNothing();
                return;
            case Vector:
                var vectorDivisor = b.AsNumeric;
                if (a.ObjectValue is VmFloatTriplet vector && !double.IsNaN(vectorDivisor) && double.IsFinite(vectorDivisor) && vectorDivisor != 0d && TryQuotientUnit(ref a, ref b, out unit))
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
                if (double.IsNaN(percentageDivisor) || b.HasUnit || !TryQuotientUnit(ref a, ref b, out unit))
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

        if (!TryQuotientUnit(ref a, ref b, out var fallbackUnit))
        {
            dst.SetFloat(double.NaN);
            return;
        }

        var fallbackResult = aNum / bNum;
        if (a.Kind is Percentage) dst.SetPercentage(fallbackResult);
        else dst.SetFloat(fallbackResult, fallbackUnit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmPower(ref this VmValue dst, ref VmValue a, ref VmValue b, ref GameEventScriptTextTable textTable)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmFloorDivide(ref this VmValue dst, ref VmValue a, ref VmValue b, ref GameEventScriptTextTable textTable)
    {
        switch (a.Kind)
        {
            case Integer when b.Kind is Integer:
                if (TryQuotientUnit(ref a, ref b, out var integerUnit))
                {
                    var result = Math.Floor((double)a.IntegerValue / b.IntegerValue);
                    if (double.IsFinite(result) && result is >= long.MinValue and <= long.MaxValue) dst.SetInteger((long)result, integerUnit);
                    else dst.SetFloat(result, integerUnit);
                }
                else dst.SetFloat(double.NaN);

                return;
            case Float when b.Kind is Float:
                if (TryQuotientUnit(ref a, ref b, out var floatUnit))
                {
                    var result = Math.Floor(a.FloatValue / b.FloatValue);
                    if (double.IsFinite(result) && result is >= long.MinValue and <= long.MaxValue) dst.SetInteger((long)result, floatUnit);
                    else dst.SetFloat(result, floatUnit);
                }
                else dst.SetFloat(double.NaN);

                return;
            case Float or Integer when b.Kind is Float or Integer:
                if (TryQuotientUnit(ref a, ref b, out var mixedUnit))
                {
                    var result = Math.Floor(a.AsNumeric / b.AsNumeric);
                    if (double.IsFinite(result) && result is >= long.MinValue and <= long.MaxValue) dst.SetInteger((long)result, mixedUnit);
                    else dst.SetFloat(result, mixedUnit);
                }
                else dst.SetFloat(double.NaN);

                return;
            case Integer or Float when b.Kind is Percentage:
                if (TryQuotientUnit(ref a, ref b, out var percentageRightUnit))
                {
                    var result = Math.Floor(a.AsNumeric / b.FloatValue);
                    if (double.IsFinite(result) && result is >= long.MinValue and <= long.MaxValue) dst.SetInteger((long)result, percentageRightUnit);
                    else dst.SetFloat(result, percentageRightUnit);
                }
                else dst.SetFloat(double.NaN);

                return;
            case Percentage when b.Kind is Integer or Float or Percentage:
                if (TryQuotientUnit(ref a, ref b, out var percentageLeftUnit))
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

        if (b.Kind is Vector or Point || !TryQuotientUnit(ref a, ref b, out var unit))
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmModulo(ref this VmValue dst, ref VmValue a, ref VmValue b, ref GameEventScriptTextTable textTable)
    {
        switch (a.Kind)
        {
            case Integer when b.Kind is Integer:
                if (TrySameUnit(ref a, ref b, out var integerUnit))
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
                if (TrySameUnit(ref a, ref b, out var floatUnit))
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
                if (TrySameUnit(ref a, ref b, out var mixedUnit))
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

        if (a.Kind is Vector or Point || b.Kind is Vector or Point || !TrySameUnit(ref a, ref b, out var unit))
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmRemainder(ref this VmValue dst, ref VmValue a, ref VmValue b, ref GameEventScriptTextTable textTable)
    {
        switch (a.Kind)
        {
            case Integer when b.Kind is Integer:
                if (TrySameUnit(ref a, ref b, out var integerUnit))
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
                if (TrySameUnit(ref a, ref b, out var floatUnit))
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
                if (TrySameUnit(ref a, ref b, out var mixedUnit))
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

        if (a.Kind is Vector or Point || b.Kind is Vector or Point || !TrySameUnit(ref a, ref b, out var unit))
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmMin(ref this VmValue dst, ref VmValue a, ref VmValue b, ref GameEventScriptTextTable textTable)
    {
        switch (a.Kind)
        {
            case Integer when b.Kind is Integer:
                if (!TrySameUnit(ref a, ref b, out _)) dst.SetFloat(double.NaN);
                else dst = b.IntegerValue < a.IntegerValue ? b : a;
                return;
            case Float when b.Kind is Float:
                if (!TrySameUnit(ref a, ref b, out _) || double.IsNaN(a.FloatValue) || double.IsNaN(b.FloatValue)) dst.SetFloat(double.NaN);
                else dst = b.FloatValue < a.FloatValue ? b : a;
                return;
            case Float or Integer when b.Kind is Float or Integer:
                if (!TrySameUnit(ref a, ref b, out _))
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
                leftText = a.IsStoragePointer ? textTable.Resolve((ushort)a.IntegerValue) : a.ObjectValue as string ?? string.Empty;
                leftRank = 2;
                break;
            case Tag:
                leftText = a.IsStoragePointer ? textTable.Resolve((ushort)a.IntegerValue) : a.ObjectValue as string ?? string.Empty;
                leftNumber = ResolveNumericTagValue(leftText);
                leftIsNumeric = leftText is "infinity" or "negativeinfinity" or "pi" or "e" or "tau" or "phi";
                leftRank = 1;
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
                rightText = b.IsStoragePointer ? textTable.Resolve((ushort)b.IntegerValue) : b.ObjectValue as string ?? string.Empty;
                rightRank = 2;
                break;
            case Tag:
                rightText = b.IsStoragePointer ? textTable.Resolve((ushort)b.IntegerValue) : b.ObjectValue as string ?? string.Empty;
                rightNumber = ResolveNumericTagValue(rightText);
                rightIsNumeric = rightText is "infinity" or "negativeinfinity" or "pi" or "e" or "tau" or "phi";
                rightRank = 1;
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
            if (!TrySameUnit(ref a, ref b, out _) || double.IsNaN(leftNumber) || double.IsNaN(rightNumber))
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
                leftText ??= a.IsStoragePointer ? textTable.Resolve((ushort)a.IntegerValue) : a.ObjectValue as string ?? string.Empty;
                rightText ??= b.IsStoragePointer ? textTable.Resolve((ushort)b.IntegerValue) : b.ObjectValue as string ?? string.Empty;
                comparison = StringComparer.Ordinal.Compare(rightText, leftText);
                break;
            case Tag when b.Kind is Tag:
                leftText ??= a.IsStoragePointer ? textTable.Resolve((ushort)a.IntegerValue) : a.ObjectValue as string ?? string.Empty;
                rightText ??= b.IsStoragePointer ? textTable.Resolve((ushort)b.IntegerValue) : b.ObjectValue as string ?? string.Empty;
                comparison = StringComparer.Ordinal.Compare(rightText, leftText);
                break;
            case Vector when b.Kind is Vector:
            case Point when b.Kind is Point:
                if (a.Unit != b.Unit)
                {
                    comparison = b.Unit.CompareTo(a.Unit);
                    break;
                }

                if (a.ObjectValue is VmFloatTriplet leftTriplet && b.ObjectValue is VmFloatTriplet rightTriplet)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmMax(ref this VmValue dst, ref VmValue a, ref VmValue b, ref GameEventScriptTextTable textTable)
    {
        switch (a.Kind)
        {
            case Integer when b.Kind is Integer:
                if (!TrySameUnit(ref a, ref b, out _)) dst.SetFloat(double.NaN);
                else dst = b.IntegerValue > a.IntegerValue ? b : a;
                return;
            case Float when b.Kind is Float:
                if (!TrySameUnit(ref a, ref b, out _) || double.IsNaN(a.FloatValue) || double.IsNaN(b.FloatValue)) dst.SetFloat(double.NaN);
                else dst = b.FloatValue > a.FloatValue ? b : a;
                return;
            case Float or Integer when b.Kind is Float or Integer:
                if (!TrySameUnit(ref a, ref b, out _))
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
                leftText = a.IsStoragePointer ? textTable.Resolve((ushort)a.IntegerValue) : a.ObjectValue as string ?? string.Empty;
                leftRank = 2;
                break;
            case Tag:
                leftText = a.IsStoragePointer ? textTable.Resolve((ushort)a.IntegerValue) : a.ObjectValue as string ?? string.Empty;
                leftNumber = ResolveNumericTagValue(leftText);
                leftIsNumeric = leftText is "infinity" or "negativeinfinity" or "pi" or "e" or "tau" or "phi";
                leftRank = 1;
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
                rightText = b.IsStoragePointer ? textTable.Resolve((ushort)b.IntegerValue) : b.ObjectValue as string ?? string.Empty;
                rightRank = 2;
                break;
            case Tag:
                rightText = b.IsStoragePointer ? textTable.Resolve((ushort)b.IntegerValue) : b.ObjectValue as string ?? string.Empty;
                rightNumber = ResolveNumericTagValue(rightText);
                rightIsNumeric = rightText is "infinity" or "negativeinfinity" or "pi" or "e" or "tau" or "phi";
                rightRank = 1;
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
            if (!TrySameUnit(ref a, ref b, out _) || double.IsNaN(leftNumber) || double.IsNaN(rightNumber))
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
                leftText ??= a.IsStoragePointer ? textTable.Resolve((ushort)a.IntegerValue) : a.ObjectValue as string ?? string.Empty;
                rightText ??= b.IsStoragePointer ? textTable.Resolve((ushort)b.IntegerValue) : b.ObjectValue as string ?? string.Empty;
                comparison = StringComparer.Ordinal.Compare(rightText, leftText);
                break;
            case Tag when b.Kind is Tag:
                leftText ??= a.IsStoragePointer ? textTable.Resolve((ushort)a.IntegerValue) : a.ObjectValue as string ?? string.Empty;
                rightText ??= b.IsStoragePointer ? textTable.Resolve((ushort)b.IntegerValue) : b.ObjectValue as string ?? string.Empty;
                comparison = StringComparer.Ordinal.Compare(rightText, leftText);
                break;
            case Vector when b.Kind is Vector:
            case Point when b.Kind is Point:
                if (a.Unit != b.Unit)
                {
                    comparison = b.Unit.CompareTo(a.Unit);
                    break;
                }

                if (a.ObjectValue is VmFloatTriplet leftTriplet && b.ObjectValue is VmFloatTriplet rightTriplet)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmNegate(ref this VmValue dst, ref VmValue a, ref GameEventScriptTextTable textTable)
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
                if (a.ObjectValue is VmFloatTriplet vector)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmAbs(ref this VmValue dst, ref VmValue a, ref GameEventScriptTextTable textTable)
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
                if (a.ObjectValue is not VmFloatTriplet vector)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmClamp(ref this VmValue dst, ref VmValue value, ref VmValue min, ref VmValue max, ref GameEventScriptTextTable textTable)
    {
        switch (value.Kind)
        {
            case Integer when min.Kind is Integer && max.Kind is Integer:
            {
                if (!TrySameUnit(ref value, ref min, ref max, out var integerUnit))
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
                if (!TrySameUnit(ref value, ref min, ref max, out var floatUnit))
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
                if (!TrySameUnit(ref value, ref min, ref max, out var mixedUnit))
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

        if (min.Kind is Vector or Point || max.Kind is Vector or Point || !TrySameUnit(ref value, ref min, ref max, out var unit))
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmNaturalLog(ref this VmValue dst, ref VmValue a, ref GameEventScriptTextTable textTable)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmRandom(ref this VmValue dst, ref VmValue from, ref VmValue to, GameEventScriptRandomGenerator randomGenerator, ref GameEventScriptTextTable textTable)
    {
        switch (from.Kind)
        {
            case Integer when to.Kind is Integer:
                if (TrySameUnit(ref from, ref to, out var unit)) dst.SetInteger(randomGenerator.NextInclusiveInteger(from.IntegerValue, to.IntegerValue), unit);
                else dst.SetFloat(double.NaN);
                return;
            case Float when to.Kind is Float:
                if (TrySameUnit(ref from, ref to, out unit))
                {
                    var left = from.FloatValue;
                    var right = to.FloatValue;
                    if (double.IsFinite(left) && double.IsFinite(right)) dst.SetFloat(randomGenerator.NextInclusiveFloat(left, right), unit);
                    else dst.SetFloat(double.NaN);
                }
                else dst.SetFloat(double.NaN);

                return;
            case Float or Integer when to.Kind is Float or Integer:
                if (TrySameUnit(ref from, ref to, out unit))
                {
                    var left = from.AsNumeric;
                    var right = to.AsNumeric;
                    if (double.IsFinite(left) && double.IsFinite(right)) dst.SetFloat(randomGenerator.NextInclusiveFloat(left, right), unit);
                    else dst.SetFloat(double.NaN);
                }
                else dst.SetFloat(double.NaN);

                return;
            case Percentage when to.Kind is Percentage:
                if (double.IsFinite(from.FloatValue) && double.IsFinite(to.FloatValue)) dst.SetFloat(randomGenerator.NextInclusiveFloat(from.FloatValue, to.FloatValue));
                else dst.SetFloat(double.NaN);
                return;
            case Percentage when to.Kind is Integer or Float:
                if (to.HasUnit)
                {
                    dst.SetFloat(double.NaN);
                    return;
                }

                var percentageLeft = from.FloatValue;
                var numericRight = to.AsNumeric;
                if (double.IsFinite(percentageLeft) && double.IsFinite(numericRight)) dst.SetFloat(randomGenerator.NextInclusiveFloat(percentageLeft, numericRight));
                else dst.SetFloat(double.NaN);
                return;
            case Integer or Float when to.Kind is Percentage:
                if (from.HasUnit)
                {
                    dst.SetFloat(double.NaN);
                    return;
                }

                var numericLeft = from.AsNumeric;
                var percentageRight = to.FloatValue;
                if (double.IsFinite(numericLeft) && double.IsFinite(percentageRight)) dst.SetFloat(randomGenerator.NextInclusiveFloat(numericLeft, percentageRight));
                else dst.SetFloat(double.NaN);
                return;
            case Nothing:
                dst.SetNothing();
                return;
            default:
                if (to.Kind is Nothing)
                {
                    dst.SetNothing();
                    return;
                }

                if (!TrySameUnit(ref from, ref to, out unit))
                {
                    dst.SetFloat(double.NaN);
                    return;
                }

                var fallbackLeft = from.AsNumeric;
                var fallbackRight = to.AsNumeric;
                if (double.IsFinite(fallbackLeft) && double.IsFinite(fallbackRight)) dst.SetFloat(randomGenerator.NextInclusiveFloat(fallbackLeft, fallbackRight), unit);
                else dst.SetFloat(double.NaN);
                return;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TrySameUnit(ref VmValue a, ref VmValue b, out GameEventScriptBytecodeInstructionUnit unit)
    {
        if (a.Unit == b.Unit)
        {
            unit = a.Unit;
            return true;
        }

        unit = UnitNone;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TrySameUnit(ref VmValue a, ref VmValue b, ref VmValue c, out GameEventScriptBytecodeInstructionUnit unit)
    {
        if (a.Unit == b.Unit && b.Unit == c.Unit)
        {
            unit = a.Unit;
            return true;
        }

        unit = UnitNone;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryProductUnit(ref VmValue a, ref VmValue b, out GameEventScriptBytecodeInstructionUnit unit)
    {
        if (a.HasUnit && b.HasUnit)
        {
            unit = UnitNone;
            return false;
        }

        unit = a.Unit is UnitNone ? b.Unit : a.Unit;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryQuotientUnit(ref VmValue a, ref VmValue b, out GameEventScriptBytecodeInstructionUnit unit)
    {
        switch (a.HasUnit)
        {
            case false when !b.HasUnit:
                unit = UnitNone;
                return true;
            case true when !b.HasUnit:
                unit = a.Unit;
                return true;
            default:
                unit = UnitNone;
                return a.Unit == b.Unit;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmTerm(ref this VmValue dst, ref VmValue source, ref VmValue termSlot)
    {
        long index;
        bool ret;
        switch (termSlot.Kind)
        {
            case Integer:
                index = termSlot.IntegerValue;
                ret = true;
                break;
            case Float or Percentage:
                switch (termSlot.FloatValue)
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
                        index = (long)termSlot.FloatValue;
                        ret = true;
                        break;
                }

                break;
            case GameEventScriptBytecodeTypeKind.Boolean:
                index = termSlot.IsTrue ? 1 : 0;
                ret = true;
                break;
            default:
                if (termSlot.IsNumeric)
                {
                    switch (termSlot.AsNumeric)
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
                            index = (long)termSlot.AsNumeric;
                            ret = true;
                            break;
                    }

                    break;
                }

                index = 0;
                ret = false;
                break;
        }

        if (source.Kind is not Series || source.ObjectValue is not GameEventScriptSeriesValue series || !ret) dst.SetNothing();
        else dst.BindArguments(series.GetTerm(index));
    }
}