using System;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmRegisterCollectionOperators
{
    internal static void GesVmLength(this GesVmState vmState, ushort dst, in GesVmValue a)
    {
        switch (a.Kind)
        {
            case Text or Tag when a.IsStoragePointer:
                vmState.SetInteger(dst, vmState.FetchStringByPointer((ushort)a.IntegerValue).Length);
                break;
            case Text or Tag when a is { IsStorageObject: true, ObjectValue: string text }:
                vmState.SetInteger(dst, text.Length);
                break;
            case List or Map or Dice or GameEventScriptBytecodeTypeKind.Range:
                vmState.SetInteger(dst, a.IntegerValue);
                break;
            case Nothing:
                vmState.SetInteger(dst, 0);
                break;
            default:
                vmState.SetNothing(dst);
                break;
        }
    }

    internal static void GesVmStartsWith(this GesVmState vmState, ushort dst, in GesVmValue a, in GesVmValue b)
    {
        switch (a.Kind)
        {
            case Text or Tag when b.Kind is Text or Tag:
                vmState.SetBoolean(dst, a.TextValue.StartsWith(b.TextValue, StringComparison.Ordinal));
                return;
            case Nothing:
                vmState.SetNothing(dst);
                return;
            case not (List or Dice or GameEventScriptBytecodeTypeKind.Range):
                vmState.SetBoolean(dst, false);
                return;
        }

        if (b.Kind is not (List or Dice or GameEventScriptBytecodeTypeKind.Range))
        {
            vmState.SetBoolean(dst, false);
            return;
        }

        GesVmValue[]? leftList = null;
        GesVmValue[]? rightList = null;
        int[]? leftDice = null;
        int[]? rightDice = null;
        GesVmValueRangeInteger? leftRange = null;
        GesVmValueRangeInteger? rightRange = null;
        GesVmValueRangeFloat? leftFloatRange = null;
        GesVmValueRangeFloat? rightFloatRange = null;

        switch (a.Kind)
        {
            case List when a.ObjectValue is GesVmValue[] value:
                leftList = value;
                break;
            case Dice when a.ObjectValue is int[] value:
                leftDice = value;
                break;
            case GameEventScriptBytecodeTypeKind.Range when a.ObjectValue is GesVmValueRangeInteger value:
                leftRange = value;
                break;
            case GameEventScriptBytecodeTypeKind.Range when a.ObjectValue is GesVmValueRangeFloat value:
                leftFloatRange = value;
                break;
            default:
                vmState.SetBoolean(dst, false);
                return;
        }

        switch (b.Kind)
        {
            case List when b.ObjectValue is GesVmValue[] value:
                rightList = value;
                break;
            case Dice when b.ObjectValue is int[] value:
                rightDice = value;
                break;
            case GameEventScriptBytecodeTypeKind.Range when b.ObjectValue is GesVmValueRangeInteger value:
                rightRange = value;
                break;
            case GameEventScriptBytecodeTypeKind.Range when b.ObjectValue is GesVmValueRangeFloat value:
                rightFloatRange = value;
                break;
            default:
                vmState.SetBoolean(dst, false);
                return;
        }

        if (b.IntegerValue > a.IntegerValue)
        {
            vmState.SetBoolean(dst, false);
            return;
        }

        var leftInteger = leftRange?.From ?? 0;
        var rightInteger = rightRange?.From ?? 0;
        var leftFloat = leftFloatRange?.From ?? 0d;
        var rightFloat = rightFloatRange?.From ?? 0d;

        for (var i = 0; i < b.IntegerValue; i++)
        {
            var left = vmState.CreateNothing();
            if (leftList is not null) left = leftList[i];
            else if (leftDice is not null) left.SetInteger(leftDice[i]);
            else if (leftRange is not null)
            {
                left.SetInteger(leftInteger);
                leftInteger += leftRange.Step;
            }
            else if (leftFloatRange is not null)
            {
                left.SetFloat(leftFloat);
                leftFloat += leftFloatRange.Step;
            }

            var right = vmState.CreateNothing();
            if (rightList is not null) right = rightList[i];
            else if (rightDice is not null) right.SetInteger(rightDice[i]);
            else if (rightRange is not null)
            {
                right.SetInteger(rightInteger);
                rightInteger += rightRange.Step;
            }
            else if (rightFloatRange is not null)
            {
                right.SetFloat(rightFloat);
                rightFloat += rightFloatRange.Step;
            }

            if (left.EqualsValue(right)) continue;
            vmState.SetBoolean(dst, false);
            return;
        }

        vmState.SetBoolean(dst, true);
    }

    internal static void GesVmEndsWith(this GesVmState vmState, ushort dst, in GesVmValue a, in GesVmValue b)
    {
        switch (a.Kind)
        {
            case Text or Tag when b.Kind is Text or Tag:
                vmState.SetBoolean(dst, a.TextValue.EndsWith(b.TextValue, StringComparison.Ordinal));
                return;
            case Nothing:
                vmState.SetNothing(dst);
                return;
            case not (List or Dice or GameEventScriptBytecodeTypeKind.Range):
                vmState.SetBoolean(dst, false);
                return;
        }

        if (b.Kind is not (List or Dice or GameEventScriptBytecodeTypeKind.Range))
        {
            vmState.SetBoolean(dst, false);
            return;
        }

        GesVmValue[]? leftList = null;
        GesVmValue[]? rightList = null;
        int[]? leftDice = null;
        int[]? rightDice = null;
        GesVmValueRangeInteger? leftRange = null;
        GesVmValueRangeInteger? rightRange = null;
        GesVmValueRangeFloat? leftFloatRange = null;
        GesVmValueRangeFloat? rightFloatRange = null;

        switch (a.Kind)
        {
            case List when a.ObjectValue is GesVmValue[] value:
                leftList = value;
                break;
            case Dice when a.ObjectValue is int[] value:
                leftDice = value;
                break;
            case GameEventScriptBytecodeTypeKind.Range when a.ObjectValue is GesVmValueRangeInteger value:
                leftRange = value;
                break;
            case GameEventScriptBytecodeTypeKind.Range when a.ObjectValue is GesVmValueRangeFloat value:
                leftFloatRange = value;
                break;
            default:
                vmState.SetBoolean(dst, false);
                return;
        }

        switch (b.Kind)
        {
            case List when b.ObjectValue is GesVmValue[] value:
                rightList = value;
                break;
            case Dice when b.ObjectValue is int[] value:
                rightDice = value;
                break;
            case GameEventScriptBytecodeTypeKind.Range when b.ObjectValue is GesVmValueRangeInteger value:
                rightRange = value;
                break;
            case GameEventScriptBytecodeTypeKind.Range when b.ObjectValue is GesVmValueRangeFloat value:
                rightFloatRange = value;
                break;
            default:
                vmState.SetBoolean(dst, false);
                return;
        }

        if (b.IntegerValue > a.IntegerValue)
        {
            vmState.SetBoolean(dst, false);
            return;
        }

        var leftOffset = a.IntegerValue - b.IntegerValue;
        var leftInteger = leftRange is not null ? leftRange.From + leftRange.Step * leftOffset : 0;
        var rightInteger = rightRange?.From ?? 0;
        var leftFloat = leftFloatRange is not null ? leftFloatRange.From + leftFloatRange.Step * leftOffset : 0d;
        var rightFloat = rightFloatRange?.From ?? 0d;

        for (var i = 0; i < b.IntegerValue; i++)
        {
            var leftIndex = leftOffset + i;
            var left = vmState.CreateNothing();
            if (leftList is not null) left = leftList[leftIndex];
            else if (leftDice is not null) left.SetInteger(leftDice[leftIndex]);
            else if (leftRange is not null)
            {
                left.SetInteger(leftInteger);
                leftInteger += leftRange.Step;
            }
            else if (leftFloatRange is not null)
            {
                left.SetFloat(leftFloat);
                leftFloat += leftFloatRange.Step;
            }

            var right = vmState.CreateNothing();
            if (rightList is not null) right = rightList[i];
            else if (rightDice is not null) right.SetInteger(rightDice[i]);
            else if (rightRange is not null)
            {
                right.SetInteger(rightInteger);
                rightInteger += rightRange.Step;
            }
            else if (rightFloatRange is not null)
            {
                right.SetFloat(rightFloat);
                rightFloat += rightFloatRange.Step;
            }

            if (left.EqualsValue(right)) continue;
            vmState.SetBoolean(dst, false);
            return;
        }

        vmState.SetBoolean(dst, true);
    }

    internal static void GesVmContains(this GesVmState vmState, ushort dst, in GesVmValue a, in GesVmValue b)
    {
        switch (b.Kind)
        {
            case Text or Tag when a.Kind is Text or Tag:
                vmState.SetBoolean(dst, b.TextValue.Contains(a.TextValue, StringComparison.Ordinal));
                return;
            case Text or Tag:
                vmState.SetBoolean(dst, false);
                return;
            case List when b.ObjectValue is GesVmValue[] list:
                for (var i = 0; i < list.Length; i++)
                {
                    if (!list[i].EqualsValue(a)) continue;
                    vmState.SetBoolean(dst, true);
                    return;
                }

                vmState.SetBoolean(dst, false);
                return;
            case Stream when b.ObjectValue is IGesVmStream stream:
                var item = vmState.CreateNothing();
                try
                {
                    while (stream.TryNext(ref item))
                    {
                        if (!item.EqualsValue(a)) continue;
                        vmState.SetBoolean(dst, true);
                        return;
                    }

                    vmState.SetBoolean(dst, false);
                    return;
                }
                finally
                {
                    if (stream is IDisposable disposable) disposable.Dispose();
                }
            case Dice when b.ObjectValue is int[] dice:
                if (a.Kind is not Integer || a.Unit is not GameEventScriptBytecodeInstructionUnit.UnitNone)
                {
                    vmState.SetBoolean(dst, false);
                    return;
                }

                for (var i = 0; i < dice.Length; i++)
                {
                    if (dice[i] != a.IntegerValue) continue;
                    vmState.SetBoolean(dst, true);
                    return;
                }

                vmState.SetBoolean(dst, false);
                return;
            case Map when b.ObjectValue is GesVmValueMap map:
                if (a.Kind is not (Text or Tag))
                {
                    vmState.SetBoolean(dst, false);
                    return;
                }

                var key = a.TextValue;
                vmState.SetBoolean(dst, !key.StartsWith("_", StringComparison.Ordinal) && map.ContainsKey(key));
                return;
            case Vector or Point when b.ObjectValue is GesVmValueVectorPoint triplet:
                if (!a.IsNumeric)
                {
                    vmState.SetBoolean(dst, false);
                    return;
                }

                var value = vmState.CreateNothing();
                value.SetFloat(triplet.X, b.Unit);
                if (value.EqualsValue(a))
                {
                    vmState.SetBoolean(dst, true);
                    return;
                }

                value.SetFloat(triplet.Y, b.Unit);
                if (value.EqualsValue(a))
                {
                    vmState.SetBoolean(dst, true);
                    return;
                }

                value.SetFloat(triplet.Z, b.Unit);
                vmState.SetBoolean(dst, value.EqualsValue(a));
                return;
            case GameEventScriptBytecodeTypeKind.Range when b.ObjectValue is GesVmValueRangeInteger range:
                if (a.Kind is not Integer || a.Unit is not GameEventScriptBytecodeInstructionUnit.UnitNone || range.Step == 0)
                {
                    vmState.SetBoolean(dst, false);
                    return;
                }

                vmState.SetBoolean(dst, range.Step > 0
                    ? a.IntegerValue >= range.From && a.IntegerValue <= range.To && unchecked((ulong)a.IntegerValue - (ulong)range.From) % (ulong)range.Step == 0UL
                    : a.IntegerValue <= range.From && a.IntegerValue >= range.To && unchecked((ulong)range.From - (ulong)a.IntegerValue) % unchecked(0UL - (ulong)range.Step) == 0UL);
                return;
            case GameEventScriptBytecodeTypeKind.Range when b.ObjectValue is GesVmValueRangeFloat range:
                if (!a.IsNumeric || range.Step == 0d)
                {
                    vmState.SetBoolean(dst, false);
                    return;
                }

                var number = a.AsNumeric;
                if (!double.IsFinite(number))
                {
                    vmState.SetBoolean(dst, false);
                    return;
                }

                if (range.Step > 0d)
                {
                    var quotient = (number - range.From) / range.Step;
                    vmState.SetBoolean(dst, number >= range.From && number <= range.To && quotient == Math.Truncate(quotient));
                    return;
                }

                var descendingQuotient = (range.From - number) / -range.Step;
                vmState.SetBoolean(dst, number <= range.From && number >= range.To && descendingQuotient == Math.Truncate(descendingQuotient));
                return;
            case Nothing:
                vmState.SetNothing(dst);
                return;
            default:
                vmState.SetBoolean(dst, false);
                return;
        }
    }

    internal static void GesVmHasAnyAll(this GesVmState vmState, ushort dst, in GesVmValue source, bool requireAll)
    {
        switch (source.Kind)
        {
            case Nothing:
                vmState.SetNothing(dst);
                return;
            case Stream when source.ObjectValue is IGesVmStream stream:
            {
                var item = vmState.CreateNothing();
                try
                {
                    while (stream.TryNext(ref item))
                    {
                        if (item.Kind is Text or Tag) item.UpdatedTextTruthinessCache();
                        if (item.IsTrue)
                        {
                            if (!requireAll)
                            {
                                vmState.SetBoolean(dst, true);
                                return;
                            }
                        }
                        else if (requireAll)
                        {
                            vmState.SetBoolean(dst, false);
                            return;
                        }
                    }

                    vmState.SetBoolean(dst, requireAll);
                    return;
                }
                finally
                {
                    if (stream is IDisposable disposable) disposable.Dispose();
                }
            }
            case List when source.ObjectValue is GesVmValue[] list:
                for (var i = 0; i < list.Length; i++)
                {
                    var item = list[i];
                    if (item.Kind is Text or Tag) item.UpdatedTextTruthinessCache();
                    if (item.IsTrue)
                    {
                        if (!requireAll)
                        {
                            vmState.SetBoolean(dst, true);
                            return;
                        }
                    }
                    else if (requireAll)
                    {
                        vmState.SetBoolean(dst, false);
                        return;
                    }
                }

                vmState.SetBoolean(dst, requireAll);
                return;
            case Dice when source.ObjectValue is int[] dice:
                if (dice.Length == 0)
                {
                    vmState.SetBoolean(dst, requireAll);
                    return;
                }

                if (!requireAll)
                {
                    for (var i = 0; i < dice.Length; i++)
                    {
                        if (dice[i] == 0) continue;
                        vmState.SetBoolean(dst, true);
                        return;
                    }

                    vmState.SetBoolean(dst, false);
                    return;
                }

                for (var i = 0; i < dice.Length; i++)
                {
                    if (dice[i] != 0) continue;
                    vmState.SetBoolean(dst, false);
                    return;
                }

                vmState.SetBoolean(dst, true);
                return;
            case Map or Custom when source.ObjectValue is GesVmValueMap map:
            {
                var list = map.ValueList;
                for (var i = 0; i < list.Length; i++)
                {
                    var item = list[i];
                    if (item.Kind is Text or Tag) item.UpdatedTextTruthinessCache();
                    if (item.IsTrue)
                    {
                        if (!requireAll)
                        {
                            vmState.SetBoolean(dst, true);
                            return;
                        }
                    }
                    else if (requireAll)
                    {
                        vmState.SetBoolean(dst, false);
                        return;
                    }
                }

                vmState.SetBoolean(dst, requireAll);
                return;
            }
            case Text or Tag:
            {
                var text = source.TextValue;
                if (text.Length == 0)
                {
                    vmState.SetBoolean(dst, requireAll);
                    return;
                }

                var item = vmState.CreateNothing();
                for (var i = 0; i < text.Length; i++)
                {
                    item.SetText(text[i].ToString());
                    item.UpdatedTextTruthinessCache();
                    if (item.IsTrue)
                    {
                        if (!requireAll)
                        {
                            vmState.SetBoolean(dst, true);
                            return;
                        }
                    }
                    else if (requireAll)
                    {
                        vmState.SetBoolean(dst, false);
                        return;
                    }
                }

                vmState.SetBoolean(dst, requireAll);
                return;
            }
            case Vector or Point when source.ObjectValue is GesVmValueVectorPoint triplet:
                if (triplet.X != 0d && double.IsFinite(triplet.X))
                {
                    if (!requireAll)
                    {
                        vmState.SetBoolean(dst, true);
                        return;
                    }
                }
                else if (requireAll)
                {
                    vmState.SetBoolean(dst, false);
                    return;
                }

                if (triplet.Y != 0d && double.IsFinite(triplet.Y))
                {
                    if (!requireAll)
                    {
                        vmState.SetBoolean(dst, true);
                        return;
                    }
                }
                else if (requireAll)
                {
                    vmState.SetBoolean(dst, false);
                    return;
                }

                if (triplet.Z != 0d && double.IsFinite(triplet.Z))
                {
                    vmState.SetBoolean(dst, true);
                    return;
                }

                vmState.SetBoolean(dst, false);
                return;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeInteger range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
                var value = range.From;
                for (var i = 0L; i < length; i++)
                {
                    if (value != 0)
                    {
                        if (!requireAll)
                        {
                            vmState.SetBoolean(dst, true);
                            return;
                        }
                    }
                    else if (requireAll)
                    {
                        vmState.SetBoolean(dst, false);
                        return;
                    }

                    value += range.Step;
                }

                vmState.SetBoolean(dst, requireAll);
                return;
            }
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeFloat range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
                var value = range.From;
                for (var i = 0L; i < length; i++)
                {
                    if (value != 0d && double.IsFinite(value))
                    {
                        if (!requireAll)
                        {
                            vmState.SetBoolean(dst, true);
                            return;
                        }
                    }
                    else if (requireAll)
                    {
                        vmState.SetBoolean(dst, false);
                        return;
                    }

                    value += range.Step;
                }

                vmState.SetBoolean(dst, requireAll);
                return;
            }
            default:
                vmState.SetBoolean(dst, false);
                return;
        }
    }

    internal static void GesVmContainsAnyAll(this GesVmState vmState, ushort dst, in GesVmValue a, in GesVmValue b, bool requireAll)
    {
        if (b.Kind is Nothing)
        {
            vmState.SetNothing(dst);
            return;
        }

        if (b.Kind is Stream && b.ObjectValue is IGesVmStream stream)
        {
            vmState.GesVmContainsAnyAllStream(dst, a, stream, requireAll);
            return;
        }

        var candidate = vmState.CreateNothing();
        switch (a.Kind)
        {
            case List when a.ObjectValue is GesVmValue[] list:
                for (var i = 0; i < list.Length; i++)
                {
                    candidate = list[i];
                    if (vmState.ContainsHelper(candidate, b) == true)
                    {
                        if (!requireAll)
                        {
                            vmState.SetBoolean(dst, true);
                            return;
                        }
                    }
                    else if (requireAll)
                    {
                        vmState.SetBoolean(dst, false);
                        return;
                    }
                }

                vmState.SetBoolean(dst, requireAll);
                return;
            case Dice when a.ObjectValue is int[] dice:
                for (var i = 0; i < dice.Length; i++)
                {
                    candidate.SetInteger(dice[i]);
                    if (vmState.ContainsHelper(candidate, b) == true)
                    {
                        if (!requireAll)
                        {
                            vmState.SetBoolean(dst, true);
                            return;
                        }
                    }
                    else if (requireAll)
                    {
                        vmState.SetBoolean(dst, false);
                        return;
                    }
                }

                vmState.SetBoolean(dst, requireAll);
                return;
            case Text or Tag:
            {
                var text = a.TextValue;
                for (var i = 0; i < text.Length; i++)
                {
                    candidate.SetText(text[i].ToString());
                    if (vmState.ContainsHelper(candidate, b) == true)
                    {
                        if (!requireAll)
                        {
                            vmState.SetBoolean(dst, true);
                            return;
                        }
                    }
                    else if (requireAll)
                    {
                        vmState.SetBoolean(dst, false);
                        return;
                    }
                }

                vmState.SetBoolean(dst, requireAll);
                return;
            }
            case GameEventScriptBytecodeTypeKind.Range when a.ObjectValue is GesVmValueRangeInteger range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
                var value = range.From;
                for (var i = 0L; i < length; i++)
                {
                    candidate.SetInteger(value);
                    value += range.Step;
                    if (vmState.ContainsHelper(candidate, b) == true)
                    {
                        if (!requireAll)
                        {
                            vmState.SetBoolean(dst, true);
                            return;
                        }
                    }
                    else if (requireAll)
                    {
                        vmState.SetBoolean(dst, false);
                        return;
                    }
                }

                vmState.SetBoolean(dst, requireAll);
                return;
            }
            case GameEventScriptBytecodeTypeKind.Range when a.ObjectValue is GesVmValueRangeFloat range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
                var value = range.From;
                for (var i = 0L; i < length; i++)
                {
                    candidate.SetFloat(value);
                    value += range.Step;
                    if (vmState.ContainsHelper(candidate, b) == true)
                    {
                        if (!requireAll)
                        {
                            vmState.SetBoolean(dst, true);
                            return;
                        }
                    }
                    else if (requireAll)
                    {
                        vmState.SetBoolean(dst, false);
                        return;
                    }
                }

                vmState.SetBoolean(dst, requireAll);
                return;
            }
            default:
                vmState.SetBoolean(dst, requireAll);
                return;
        }
    }

    private static void GesVmContainsAnyAllStream(this GesVmState vmState, ushort dst, in GesVmValue a, IGesVmStream stream, bool requireAll)
    {
        var candidates = new GesVmValue[8];
        var candidateCount = 0;

        void AddCandidate(GesVmValue value)
        {
            if (candidateCount == candidates.Length) Array.Resize(ref candidates, candidates.Length << 1);
            candidates[candidateCount++] = value;
        }

        var candidate = vmState.CreateNothing();
        switch (a.Kind)
        {
            case List when a.ObjectValue is GesVmValue[] list:
                for (var i = 0; i < list.Length; i++) AddCandidate(list[i]);
                break;
            case Dice when a.ObjectValue is int[] dice:
                for (var i = 0; i < dice.Length; i++)
                {
                    candidate.SetInteger(dice[i]);
                    AddCandidate(candidate);
                }

                break;
            case Text or Tag:
            {
                var text = a.TextValue;
                for (var i = 0; i < text.Length; i++)
                {
                    candidate.SetText(text[i].ToString());
                    AddCandidate(candidate);
                }

                break;
            }
            case GameEventScriptBytecodeTypeKind.Range when a.ObjectValue is GesVmValueRangeInteger range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
                var value = range.From;
                for (var i = 0L; i < length; i++)
                {
                    candidate.SetInteger(value);
                    value += range.Step;
                    AddCandidate(candidate);
                }

                break;
            }
            case GameEventScriptBytecodeTypeKind.Range when a.ObjectValue is GesVmValueRangeFloat range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
                var value = range.From;
                for (var i = 0L; i < length; i++)
                {
                    candidate.SetFloat(value);
                    value += range.Step;
                    AddCandidate(candidate);
                }

                break;
            }
        }

        try
        {
            if (candidateCount == 0)
            {
                vmState.SetBoolean(dst, requireAll);
                return;
            }

            var item = vmState.CreateNothing();
            if (!requireAll)
            {
                while (stream.TryNext(ref item))
                {
                    for (var i = 0; i < candidateCount; i++)
                    {
                        if (!item.EqualsValue(candidates[i])) continue;
                        vmState.SetBoolean(dst, true);
                        return;
                    }
                }

                vmState.SetBoolean(dst, false);
                return;
            }

            var found = new bool[candidateCount];
            var foundCount = 0;
            while (stream.TryNext(ref item))
            {
                for (var i = 0; i < candidateCount; i++)
                {
                    if (found[i] || !item.EqualsValue(candidates[i])) continue;
                    found[i] = true;
                    foundCount++;
                }

                if (foundCount != candidateCount) continue;
                vmState.SetBoolean(dst, true);
                return;
            }

            vmState.SetBoolean(dst, false);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }

    internal static void GesVmContainsValue(this GesVmState vmState, ushort dst, in GesVmValue a, in GesVmValue b)
    {
        switch (b.Kind)
        {
            case Map or Custom when b.ObjectValue is GesVmValueMap map:
                for (var i = 0; i < map.StorageLength; i++)
                {
                    if (!map.IsVisibleAt(i)) continue;
                    var mapValue = map.ValueAt(i);
                    if (!mapValue.EqualsValue(a)) continue;
                    vmState.SetBoolean(dst, true);
                    return;
                }

                vmState.SetBoolean(dst, false);
                return;
            case Vector or Point when b.ObjectValue is GesVmValueVectorPoint triplet:
                if (!a.IsNumeric)
                {
                    vmState.SetBoolean(dst, false);
                    return;
                }

                var value = vmState.CreateNothing();
                value.SetFloat(triplet.X, b.Unit);
                if (value.EqualsValue(a))
                {
                    vmState.SetBoolean(dst, true);
                    return;
                }

                value.SetFloat(triplet.Y, b.Unit);
                if (value.EqualsValue(a))
                {
                    vmState.SetBoolean(dst, true);
                    return;
                }

                value.SetFloat(triplet.Z, b.Unit);
                vmState.SetBoolean(dst, value.EqualsValue(a));
                return;
            case Nothing:
                vmState.SetNothing(dst);
                return;
            default:
                vmState.SetBoolean(dst, false);
                return;
        }
    }

    internal static void GesVmUnion(this GesVmState vmState, ushort dst, in GesVmValue a, in GesVmValue b)
    {
        if (a.Kind is Nothing || b.Kind is Nothing)
        {
            vmState.SetNothing(dst);
            return;
        }

        switch (a.Kind)
        {
            case Map when a.ObjectValue is GesVmValueMap aMap:
            {
                switch (b.Kind)
                {
                    case Map when b.ObjectValue is GesVmValueMap bMap:
                    {
                        var map = new GesVmValueMapBuilder(vmState, aMap.StorageLength + bMap.StorageLength);
                        for (var i = 0; i < aMap.StorageLength; i++) map.Set(aMap.KeyAt(i), aMap.ValueAt(i));
                        for (var i = 0; i < bMap.StorageLength; i++) map.Set(bMap.KeyAt(i), bMap.ValueAt(i));
                        vmState.SetMap(dst, map.ToMap());
                        return;
                    }
                    case List when b.ObjectValue is GesVmValue[] keys:
                    {
                        var map = new GesVmValueMapBuilder(vmState, aMap.StorageLength + keys.Length);
                        for (var i = 0; i < aMap.StorageLength; i++) map.Set(aMap.KeyAt(i), aMap.ValueAt(i));
                        for (var i = 0; i < keys.Length; i++)
                        {
                            var keyValue = keys[i];
                            if (keyValue.Kind is not (Text or Tag))
                            {
                                vmState.SetNothing(dst);
                                return;
                            }

                            var key = keyValue.ReadTextOrTag();
                            if (map.ContainsKey(key)) continue;
                            var flag = vmState.CreateBoolean(true);
                            map.Set(key, flag);
                        }

                        vmState.SetMap(dst, map.ToMap());
                        return;
                    }
                }

                break;
            }
            case List when a.ObjectValue is GesVmValue[] aList:
            {
                switch (b.Kind)
                {
                    case List when b.ObjectValue is GesVmValue[] bList:
                    {
                        var list = vmState.CreateList(aList.Length + bList.Length);
                        for (var i = 0; i < aList.Length; i++) list[i] = aList[i];
                        for (var i = 0; i < bList.Length; i++) list[aList.Length + i] = bList[i];
                        vmState.SetList(dst, list);
                        return;
                    }
                    case Dice when b.ObjectValue is int[] bDice:
                    {
                        var list = vmState.CreateList(aList.Length + bDice.Length);
                        for (var i = 0; i < aList.Length; i++) list[i] = aList[i];
                        for (var i = 0; i < bDice.Length; i++) list[aList.Length + i].SetInteger(bDice[i]);
                        vmState.SetList(dst, list);
                        return;
                    }
                }

                break;
            }
            case Dice when a.ObjectValue is int[] aDice:
            {
                switch (b.Kind)
                {
                    case Dice when b.ObjectValue is int[] bDice:
                    {
                        var dice = new int[aDice.Length + bDice.Length];
                        Array.Copy(aDice, dice, aDice.Length);
                        Array.Copy(bDice, 0, dice, aDice.Length, bDice.Length);
                        vmState.SetDice(dst, dice);
                        return;
                    }
                    case List when b.ObjectValue is GesVmValue[] bList:
                    {
                        var list = vmState.CreateList(aDice.Length + bList.Length);
                        for (var i = 0; i < aDice.Length; i++) list[i].SetInteger(aDice[i]);
                        for (var i = 0; i < bList.Length; i++) list[aDice.Length + i] = bList[i];
                        vmState.SetList(dst, list);
                        return;
                    }
                }

                break;
            }
        }

        vmState.SetNothing(dst);
    }

    internal static void GesVmIntersect(this GesVmState vmState, ushort dst, in GesVmValue a, in GesVmValue b)
    {
        if (a.Kind is Nothing || b.Kind is Nothing)
        {
            vmState.SetNothing(dst);
            return;
        }

        switch (a.Kind)
        {
            case Map when a.ObjectValue is GesVmValueMap aMap:
            {
                var map = new GesVmValueMapBuilder(vmState, aMap.StorageLength);
                switch (b.Kind)
                {
                    case Map when b.ObjectValue is GesVmValueMap bMap:
                    {
                        var ai = 0;
                        var bi = 0;
                        while (ai < aMap.StorageLength && bi < bMap.StorageLength)
                        {
                            var comparison = string.CompareOrdinal(aMap.KeyAt(ai), bMap.KeyAt(bi));
                            if (comparison == 0)
                            {
                                map.Set(aMap.KeyAt(ai), aMap.ValueAt(ai));
                                ai++;
                                bi++;
                            }
                            else if (comparison < 0)
                            {
                                ai++;
                            }
                            else
                            {
                                bi++;
                            }
                        }

                        break;
                    }
                    case List when b.ObjectValue is GesVmValue[] keyList:
                    {
                        var keys = new string[keyList.Length];
                        for (var i = 0; i < keyList.Length; i++)
                        {
                            var keyValue = keyList[i];
                            if (keyValue.Kind is not (Text or Tag))
                            {
                                vmState.SetNothing(dst);
                                return;
                            }

                            keys[i] = keyValue.ReadTextOrTag();
                        }

                        Array.Sort(keys, StringComparer.Ordinal);
                        var ai = 0;
                        var bi = 0;
                        while (ai < aMap.StorageLength && bi < keys.Length)
                        {
                            var comparison = string.CompareOrdinal(aMap.KeyAt(ai), keys[bi]);
                            if (comparison == 0)
                            {
                                map.Set(aMap.KeyAt(ai), aMap.ValueAt(ai));
                                ai++;
                                bi++;
                                while (bi < keys.Length && string.Equals(keys[bi], keys[bi - 1], StringComparison.Ordinal)) bi++;
                            }
                            else if (comparison < 0)
                            {
                                ai++;
                            }
                            else
                            {
                                bi++;
                            }
                        }

                        break;
                    }
                    default:
                        vmState.SetNothing(dst);
                        return;
                }

                vmState.SetMap(dst, map.ToMap());
                return;
            }
            case List when a.ObjectValue is GesVmValue[] leftList:
            {
                switch (b.Kind)
                {
                    case List when b.ObjectValue is GesVmValue[] rightList:
                    {
                        var removed = new bool[rightList.Length];
                        var resultLength = 0;
                        for (var i = 0; i < leftList.Length; i++)
                        {
                            for (var j = 0; j < rightList.Length; j++)
                            {
                                if (removed[j] || !leftList[i].EqualsValue(rightList[j])) continue;
                                removed[j] = true;
                                resultLength++;
                                break;
                            }
                        }

                        var list = vmState.CreateList(resultLength);
                        var index = 0;
                        Array.Clear(removed, 0, removed.Length);
                        for (var i = 0; i < leftList.Length; i++)
                        {
                            for (var j = 0; j < rightList.Length; j++)
                            {
                                if (removed[j] || !leftList[i].EqualsValue(rightList[j])) continue;
                                removed[j] = true;
                                list[index++] = leftList[i];
                                break;
                            }
                        }

                        vmState.SetList(dst, list);
                        return;
                    }
                    case Dice when b.ObjectValue is int[] rightDice:
                    {
                        var removed = new bool[rightDice.Length];
                        var resultLength = 0;
                        for (var i = 0; i < leftList.Length; i++)
                        {
                            ref var item = ref leftList[i];
                            if (item.Kind is not Integer || item.Unit is not GameEventScriptBytecodeInstructionUnit.UnitNone) continue;
                            for (var j = 0; j < rightDice.Length; j++)
                            {
                                if (removed[j] || item.IntegerValue != rightDice[j]) continue;
                                removed[j] = true;
                                resultLength++;
                                break;
                            }
                        }

                        var list = vmState.CreateList(resultLength);
                        var index = 0;
                        Array.Clear(removed, 0, removed.Length);
                        for (var i = 0; i < leftList.Length; i++)
                        {
                            ref var item = ref leftList[i];
                            if (item.Kind is not Integer || item.Unit is not GameEventScriptBytecodeInstructionUnit.UnitNone) continue;
                            for (var j = 0; j < rightDice.Length; j++)
                            {
                                if (removed[j] || item.IntegerValue != rightDice[j]) continue;
                                removed[j] = true;
                                list[index++] = item;
                                break;
                            }
                        }

                        vmState.SetList(dst, list);
                        return;
                    }
                }

                break;
            }
            case Dice when a.ObjectValue is int[] leftDice:
            {
                switch (b.Kind)
                {
                    case Dice when b.ObjectValue is int[] rightDice:
                        var removed = new bool[rightDice.Length];
                        var resultLength = 0;
                        for (var i = 0; i < leftDice.Length; i++)
                        {
                            for (var j = 0; j < rightDice.Length; j++)
                            {
                                if (removed[j] || leftDice[i] != rightDice[j]) continue;
                                removed[j] = true;
                                resultLength++;
                                break;
                            }
                        }

                        var dice = new int[resultLength];
                        var index = 0;
                        Array.Clear(removed, 0, removed.Length);
                        for (var i = 0; i < leftDice.Length; i++)
                        {
                            for (var j = 0; j < rightDice.Length; j++)
                            {
                                if (removed[j] || leftDice[i] != rightDice[j]) continue;
                                removed[j] = true;
                                dice[index++] = leftDice[i];
                                break;
                            }
                        }

                        vmState.SetDice(dst, dice);
                        return;
                    case List when b.ObjectValue is GesVmValue[] rightList:
                        var removed1 = new bool[rightList.Length];
                        var resultLength1 = 0;
                        for (var i = 0; i < leftDice.Length; i++)
                        {
                            for (var j = 0; j < rightList.Length; j++)
                            {
                                ref var item = ref rightList[j];
                                if (removed1[j] || item.Kind is not Integer || item.Unit is not GameEventScriptBytecodeInstructionUnit.UnitNone || item.IntegerValue != leftDice[i]) continue;
                                removed1[j] = true;
                                resultLength1++;
                                break;
                            }
                        }

                        var list = vmState.CreateList(resultLength1);
                        var index1 = 0;
                        Array.Clear(removed1, 0, removed1.Length);
                        for (var i = 0; i < leftDice.Length; i++)
                        {
                            for (var j = 0; j < rightList.Length; j++)
                            {
                                ref var item = ref rightList[j];
                                if (removed1[j] || item.Kind is not Integer || item.Unit is not GameEventScriptBytecodeInstructionUnit.UnitNone || item.IntegerValue != leftDice[i]) continue;
                                removed1[j] = true;
                                list[index1++].SetInteger(leftDice[i]);
                                break;
                            }
                        }

                        vmState.SetList(dst, list);
                        return;
                }

                break;
            }
        }

        vmState.SetNothing(dst);
    }

    internal static void GesVmZip(this GesVmState vmState, ushort dst, in GesVmValue a, in GesVmValue b)
    {
        if (a.Kind is not List || b.Kind is not List || a.ObjectValue is not GesVmValue[] aList || b.ObjectValue is not GesVmValue[] bList)
        {
            vmState.SetNothing(dst);
            return;
        }

        var length = Math.Min(aList.Length, bList.Length);
        var list = vmState.CreateList(length);
        for (var i = 0; i < length; i++)
        {
            var pair = new GesVmValueMapBuilder(vmState, 2);
            pair.Set("left", aList[i]);
            pair.Set("right", bList[i]);
            list[i].SetMap(pair.ToMap());
        }

        vmState.SetList(dst, list);
    }

    internal static void GesVmValues(this GesVmState vmState, ushort dst, in GesVmValue a)
    {
        switch (a.Kind)
        {
            case Map or Custom when a.ObjectValue is GesVmValueMap map:
                vmState.SetList(dst, map.ValueList);
                break;
            case Custom when a.ObjectValue is GameEventScriptValue externalValue:
                var valueEntries = externalValue.AsMap();
                var valueKeys = new string[valueEntries.Count];
                var valueKeyCount = 0;
                foreach (var key in valueEntries.Keys)
                {
                    if (key.StartsWith("_", StringComparison.Ordinal)) continue;
                    valueKeys[valueKeyCount++] = key;
                }

                Array.Sort(valueKeys, 0, valueKeyCount, StringComparer.Ordinal);
                var values = vmState.CreateList(valueKeyCount);
                for (var i = 0; i < valueKeyCount; i++) values[i].BindArguments(valueEntries[valueKeys[i]]);
                vmState.SetList(dst, values);
                break;
            default:
                vmState.SetNothing(dst);
                break;
        }
    }

    internal static void GesVmKeys(this GesVmState vmState, ushort dst, in GesVmValue a)
    {
        switch (a.Kind)
        {
            case Map or Custom when a.ObjectValue is GesVmValueMap map:
                vmState.SetList(dst, map.KeyList);
                break;
            case Custom when a.ObjectValue is GameEventScriptValue externalValue:
                var keyEntries = externalValue.AsMap();
                var keyKeys = new string[keyEntries.Count];
                var keyCount = 0;
                foreach (var key in keyEntries.Keys)
                {
                    if (key.StartsWith("_", StringComparison.Ordinal)) continue;
                    keyKeys[keyCount++] = key;
                }

                Array.Sort(keyKeys, 0, keyCount, StringComparer.Ordinal);
                var keys = vmState.CreateList(keyCount);
                for (var i = 0; i < keyCount; i++) keys[i].SetTag(keyKeys[i]);
                vmState.SetList(dst, keys);
                break;
            default:
                vmState.SetNothing(dst);
                break;
        }
    }

    internal static void GesVmEntries(this GesVmState vmState, ushort dst, in GesVmValue a)
    {
        switch (a.Kind)
        {
            case Map or Custom when a.ObjectValue is GesVmValueMap map:
                vmState.SetList(dst, map.EntryList);
                break;
            case Custom when a.ObjectValue is GameEventScriptValue externalValue:
                var entryEntries = externalValue.AsMap();
                var entryKeys = new string[entryEntries.Count];
                var entryKeyCount = 0;
                foreach (var key in entryEntries.Keys)
                {
                    if (key.StartsWith("_", StringComparison.Ordinal)) continue;
                    entryKeys[entryKeyCount++] = key;
                }

                Array.Sort(entryKeys, 0, entryKeyCount, StringComparer.Ordinal);
                var entries = vmState.CreateList(entryKeyCount);
                for (var i = 0; i < entryKeyCount; i++)
                {
                    var value = vmState.CreateNothing();
                    value.BindArguments(entryEntries[entryKeys[i]]);
                    var entry = new GesVmValueMapBuilder(vmState, 2);
                    entry.Set("key", vmState.CreateTag(entryKeys[i]));
                    entry.Set("value", value);
                    entries[i].SetMap(entry.ToMap());
                }

                vmState.SetList(dst, entries);
                break;
            default:
                vmState.SetNothing(dst);
                break;
        }
    }

    private static bool? ContainsHelper(this GesVmState vmState, in GesVmValue a, in GesVmValue b)
    {
        switch (b.Kind)
        {
            case Text or Tag when a.Kind is Text or Tag:
                return b.TextValue.Contains(a.TextValue, StringComparison.Ordinal);
            case Text or Tag:
                return false;
            case List when b.ObjectValue is GesVmValue[] list:
                for (var i = 0; i < list.Length; i++)
                {
                    if (!list[i].EqualsValue(a)) continue;
                    return true;
                }

                return false;
            case Stream when b.ObjectValue is IGesVmStream stream:
                var item = vmState.CreateNothing();
                try
                {
                    while (stream.TryNext(ref item))
                    {
                        if (!item.EqualsValue(a)) continue;
                        return true;
                    }

                    return false;
                }
                finally
                {
                    if (stream is IDisposable disposable) disposable.Dispose();
                }
            case Dice when b.ObjectValue is int[] dice:
                if (a.Kind is not Integer || a.Unit is not GameEventScriptBytecodeInstructionUnit.UnitNone) return false;
                for (var i = 0; i < dice.Length; i++)
                {
                    if (dice[i] != a.IntegerValue) continue;
                    return true;
                }

                return false;
            case Map when b.ObjectValue is GesVmValueMap map:
                if (a.Kind is not (Text or Tag)) return false;
                var key = a.TextValue;
                return !key.StartsWith("_", StringComparison.Ordinal) && map.ContainsKey(key);
            case Vector or Point when b.ObjectValue is GesVmValueVectorPoint triplet:
                if (!a.IsNumeric) return false;
                var value = vmState.CreateNothing();
                value.SetFloat(triplet.X, b.Unit);
                if (value.EqualsValue(a)) return true;
                value.SetFloat(triplet.Y, b.Unit);
                if (value.EqualsValue(a)) return true;
                value.SetFloat(triplet.Z, b.Unit);
                return value.EqualsValue(a);
            case GameEventScriptBytecodeTypeKind.Range when b.ObjectValue is GesVmValueRangeInteger range:
                if (a.Kind is not Integer || a.Unit is not GameEventScriptBytecodeInstructionUnit.UnitNone || range.Step == 0) return false;
                return range.Step > 0
                    ? a.IntegerValue >= range.From && a.IntegerValue <= range.To && unchecked((ulong)a.IntegerValue - (ulong)range.From) % (ulong)range.Step == 0UL
                    : a.IntegerValue <= range.From && a.IntegerValue >= range.To && unchecked((ulong)range.From - (ulong)a.IntegerValue) % unchecked(0UL - (ulong)range.Step) == 0UL;
            case GameEventScriptBytecodeTypeKind.Range when b.ObjectValue is GesVmValueRangeFloat range:
                if (!a.IsNumeric || range.Step == 0d) return false;
                var number = a.AsNumeric;
                if (!double.IsFinite(number)) return false;
                if (range.Step > 0d)
                {
                    var quotient = (number - range.From) / range.Step;
                    return number >= range.From && number <= range.To && quotient == Math.Truncate(quotient);
                }

                var descendingQuotient = (range.From - number) / -range.Step;
                return number <= range.From && number >= range.To && descendingQuotient == Math.Truncate(descendingQuotient);
            case Nothing:
                return null;
            default:
                return false;
        }
    }
    
}