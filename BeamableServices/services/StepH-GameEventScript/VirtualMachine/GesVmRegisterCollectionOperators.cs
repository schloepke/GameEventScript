using System;
using System.Collections.Generic;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmRegisterCollectionOperators
{
    internal static void GesVmLength(ref this GesVmValue dst, ref GesVmValue a, ref GameEventScriptTextTable textTable)
    {
        switch (a.Kind)
        {
            case Text or Tag when a.IsStoragePointer:
                dst.SetInteger(textTable.Resolve((ushort)a.IntegerValue).Length);
                break;
            case Text or Tag when a is { IsStorageObject: true, ObjectValue: string text }:
                dst.SetInteger(text.Length);
                break;
            case List or Map or Dice or GameEventScriptBytecodeTypeKind.Range:
                dst.SetInteger(a.IntegerValue);
                break;
            case Nothing:
                dst.SetInteger(0);
                break;
            default:
                dst.SetNothing();
                break;
        }
    }
    internal static void GesVmStartsWith(ref this GesVmValue dst, ref GesVmValue a, ref GesVmValue b, ref GameEventScriptTextTable textTable)
    {
        switch (a.Kind)
        {
            case Text or Tag when b.Kind is Text or Tag:
                dst.SetBoolean(a.ReadTextOrTag().StartsWith(b.ReadTextOrTag(), StringComparison.Ordinal));
                return;
            case Nothing:
                dst.SetNothing();
                return;
            case not (List or Dice or GameEventScriptBytecodeTypeKind.Range):
                dst.SetBoolean(false);
                return;
        }

        if (b.Kind is not (List or Dice or GameEventScriptBytecodeTypeKind.Range))
        {
            dst.SetBoolean(false);
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
                dst.SetBoolean(false);
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
                dst.SetBoolean(false);
                return;
        }

        if (b.IntegerValue > a.IntegerValue)
        {
            dst.SetBoolean(false);
            return;
        }

        var leftInteger = leftRange?.From ?? 0;
        var rightInteger = rightRange?.From ?? 0;
        var leftFloat = leftFloatRange?.From ?? 0d;
        var rightFloat = rightFloatRange?.From ?? 0d;

        for (var i = 0; i < b.IntegerValue; i++)
        {
            var left = dst.OwningState.CreateNothing();
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

            var right = dst.OwningState.CreateNothing();
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

            if (left.EqualsValue(ref right)) continue;
            dst.SetBoolean(false);
            return;
        }

        dst.SetBoolean(true);
    }
    internal static void GesVmEndsWith(ref this GesVmValue dst, ref GesVmValue a, ref GesVmValue b, ref GameEventScriptTextTable textTable)
    {
        switch (a.Kind)
        {
            case Text or Tag when b.Kind is Text or Tag:
                dst.SetBoolean(a.ReadTextOrTag().EndsWith(b.ReadTextOrTag(), StringComparison.Ordinal));
                return;
            case Nothing:
                dst.SetNothing();
                return;
            case not (List or Dice or GameEventScriptBytecodeTypeKind.Range):
                dst.SetBoolean(false);
                return;
        }

        if (b.Kind is not (List or Dice or GameEventScriptBytecodeTypeKind.Range))
        {
            dst.SetBoolean(false);
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
                dst.SetBoolean(false);
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
                dst.SetBoolean(false);
                return;
        }

        if (b.IntegerValue > a.IntegerValue)
        {
            dst.SetBoolean(false);
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
            var left = dst.OwningState.CreateNothing();
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

            var right = dst.OwningState.CreateNothing();
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

            if (left.EqualsValue(ref right)) continue;
            dst.SetBoolean(false);
            return;
        }

        dst.SetBoolean(true);
    }
    internal static void GesVmContains(ref this GesVmValue dst, ref GesVmValue a, ref GesVmValue b, ref GameEventScriptTextTable textTable)
    {
        switch (b.Kind)
        {
            case Text or Tag when a.Kind is Text or Tag:
                dst.SetBoolean(b.ReadTextOrTag().Contains(a.ReadTextOrTag(), StringComparison.Ordinal));
                return;
            case Text or Tag:
                dst.SetBoolean(false);
                return;
            case List when b.ObjectValue is GesVmValue[] list:
                for (var i = 0; i < list.Length; i++)
                {
                    if (!list[i].EqualsValue(ref a)) continue;
                    dst.SetBoolean(true);
                    return;
                }

                dst.SetBoolean(false);
                return;
            case Stream when b.ObjectValue is IGesVmStream stream:
                var item = dst.OwningState.CreateNothing();
                try
                {
                    while (stream.TryNext(ref item))
                    {
                        if (!item.EqualsValue(ref a)) continue;
                        dst.SetBoolean(true);
                        return;
                    }

                    dst.SetBoolean(false);
                    return;
                }
                finally
                {
                    if (stream is IDisposable disposable) disposable.Dispose();
                }
            case Dice when b.ObjectValue is int[] dice:
                if (a.Kind is not Integer || a.Unit is not GameEventScriptBytecodeInstructionUnit.UnitNone)
                {
                    dst.SetBoolean(false);
                    return;
                }

                for (var i = 0; i < dice.Length; i++)
                {
                    if (dice[i] != a.IntegerValue) continue;
                    dst.SetBoolean(true);
                    return;
                }

                dst.SetBoolean(false);
                return;
            case Map when b.ObjectValue is GesVmValueMap map:
                if (a.Kind is not (Text or Tag))
                {
                    dst.SetBoolean(false);
                    return;
                }

                var key = a.ReadTextOrTag();
                dst.SetBoolean(!key.StartsWith("_", StringComparison.Ordinal) && map.ContainsKey(key));
                return;
            case Vector or Point when b.ObjectValue is GesVmValueVectorPoint triplet:
                if (!a.IsNumeric)
                {
                    dst.SetBoolean(false);
                    return;
                }

                var value = dst.OwningState.CreateNothing();
                value.SetFloat(triplet.X, b.Unit);
                if (value.EqualsValue(ref a))
                {
                    dst.SetBoolean(true);
                    return;
                }

                value.SetFloat(triplet.Y, b.Unit);
                if (value.EqualsValue(ref a))
                {
                    dst.SetBoolean(true);
                    return;
                }

                value.SetFloat(triplet.Z, b.Unit);
                dst.SetBoolean(value.EqualsValue(ref a));
                return;
            case GameEventScriptBytecodeTypeKind.Range when b.ObjectValue is GesVmValueRangeInteger range:
                if (a.Kind is not Integer || a.Unit is not GameEventScriptBytecodeInstructionUnit.UnitNone || range.Step == 0)
                {
                    dst.SetBoolean(false);
                    return;
                }

                dst.SetBoolean(range.Step > 0
                    ? a.IntegerValue >= range.From && a.IntegerValue <= range.To && unchecked((ulong)a.IntegerValue - (ulong)range.From) % (ulong)range.Step == 0UL
                    : a.IntegerValue <= range.From && a.IntegerValue >= range.To && unchecked((ulong)range.From - (ulong)a.IntegerValue) % unchecked(0UL - (ulong)range.Step) == 0UL);
                return;
            case GameEventScriptBytecodeTypeKind.Range when b.ObjectValue is GesVmValueRangeFloat range:
                if (!a.IsNumeric || range.Step == 0d)
                {
                    dst.SetBoolean(false);
                    return;
                }

                var number = a.AsNumeric;
                if (!double.IsFinite(number))
                {
                    dst.SetBoolean(false);
                    return;
                }

                if (range.Step > 0d)
                {
                    var quotient = (number - range.From) / range.Step;
                    dst.SetBoolean(number >= range.From && number <= range.To && quotient == Math.Truncate(quotient));
                    return;
                }

                var descendingQuotient = (range.From - number) / -range.Step;
                dst.SetBoolean(number <= range.From && number >= range.To && descendingQuotient == Math.Truncate(descendingQuotient));
                return;
            case Nothing:
                dst.SetNothing();
                return;
            default:
                dst.SetBoolean(false);
                return;
        }
    }
    internal static void GesVmContainsAny(ref this GesVmValue dst, ref GesVmValue a, ref GesVmValue b, ref GameEventScriptTextTable textTable)
        => GesVmContainsAnyAll(ref dst, ref a, ref b, ref textTable, requireAll: false);
    internal static void GesVmContainsAll(ref this GesVmValue dst, ref GesVmValue a, ref GesVmValue b, ref GameEventScriptTextTable textTable)
        => GesVmContainsAnyAll(ref dst, ref a, ref b, ref textTable, requireAll: true);
    internal static void GesVmHasAny(ref this GesVmValue dst, ref GesVmValue source)
        => GesVmHasAnyAll(ref dst, ref source, requireAll: false);
    internal static void GesVmHasAll(ref this GesVmValue dst, ref GesVmValue source)
        => GesVmHasAnyAll(ref dst, ref source, requireAll: true);
    private static void GesVmHasAnyAll(ref GesVmValue dst, ref GesVmValue source, bool requireAll)
    {
        switch (source.Kind)
        {
            case Nothing:
                dst.SetNothing();
                return;
            case Stream when source.ObjectValue is IGesVmStream stream:
            {
                var item = dst.OwningState.CreateNothing();
                try
                {
                    while (stream.TryNext(ref item))
                    {
                        if (item.Kind is Text or Tag) item.UpdatedTextTruthinessCache();
                        if (item.IsTrue)
                        {
                            if (!requireAll)
                            {
                                dst.SetBoolean(true);
                                return;
                            }
                        }
                        else if (requireAll)
                        {
                            dst.SetBoolean(false);
                            return;
                        }
                    }

                    dst.SetBoolean(requireAll);
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
                            dst.SetBoolean(true);
                            return;
                        }
                    }
                    else if (requireAll)
                    {
                        dst.SetBoolean(false);
                        return;
                    }
                }

                dst.SetBoolean(requireAll);
                return;
            case Dice when source.ObjectValue is int[] dice:
                if (dice.Length == 0)
                {
                    dst.SetBoolean(requireAll);
                    return;
                }

                if (!requireAll)
                {
                    for (var i = 0; i < dice.Length; i++)
                    {
                        if (dice[i] == 0) continue;
                        dst.SetBoolean(true);
                        return;
                    }

                    dst.SetBoolean(false);
                    return;
                }

                for (var i = 0; i < dice.Length; i++)
                {
                    if (dice[i] != 0) continue;
                    dst.SetBoolean(false);
                    return;
                }

                dst.SetBoolean(true);
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
                            dst.SetBoolean(true);
                            return;
                        }
                    }
                    else if (requireAll)
                    {
                        dst.SetBoolean(false);
                        return;
                    }
                }

                dst.SetBoolean(requireAll);
                return;
            }
            case Text or Tag:
            {
                var text = source.ReadTextOrTag();
                if (text.Length == 0)
                {
                    dst.SetBoolean(requireAll);
                    return;
                }

                var item = dst.OwningState.CreateNothing();
                for (var i = 0; i < text.Length; i++)
                {
                    item.SetText(text[i].ToString());
                    item.UpdatedTextTruthinessCache();
                    if (item.IsTrue)
                    {
                        if (!requireAll)
                        {
                            dst.SetBoolean(true);
                            return;
                        }
                    }
                    else if (requireAll)
                    {
                        dst.SetBoolean(false);
                        return;
                    }
                }

                dst.SetBoolean(requireAll);
                return;
            }
            case Vector or Point when source.ObjectValue is GesVmValueVectorPoint triplet:
                if (triplet.X != 0d && double.IsFinite(triplet.X))
                {
                    if (!requireAll)
                    {
                        dst.SetBoolean(true);
                        return;
                    }
                }
                else if (requireAll)
                {
                    dst.SetBoolean(false);
                    return;
                }

                if (triplet.Y != 0d && double.IsFinite(triplet.Y))
                {
                    if (!requireAll)
                    {
                        dst.SetBoolean(true);
                        return;
                    }
                }
                else if (requireAll)
                {
                    dst.SetBoolean(false);
                    return;
                }

                if (triplet.Z != 0d && double.IsFinite(triplet.Z))
                {
                    dst.SetBoolean(true);
                    return;
                }

                dst.SetBoolean(false);
                return;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeInteger range:
            {
                var length = StepH.GameEventScript.Types.GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
                var value = range.From;
                for (var i = 0L; i < length; i++)
                {
                    if (value != 0)
                    {
                        if (!requireAll)
                        {
                            dst.SetBoolean(true);
                            return;
                        }
                    }
                    else if (requireAll)
                    {
                        dst.SetBoolean(false);
                        return;
                    }

                    value += range.Step;
                }

                dst.SetBoolean(requireAll);
                return;
            }
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeFloat range:
            {
                var length = StepH.GameEventScript.Types.GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
                var value = range.From;
                for (var i = 0L; i < length; i++)
                {
                    if (value != 0d && double.IsFinite(value))
                    {
                        if (!requireAll)
                        {
                            dst.SetBoolean(true);
                            return;
                        }
                    }
                    else if (requireAll)
                    {
                        dst.SetBoolean(false);
                        return;
                    }

                    value += range.Step;
                }

                dst.SetBoolean(requireAll);
                return;
            }
            default:
                dst.SetBoolean(false);
                return;
        }
    }
    private static void GesVmContainsAnyAll(ref GesVmValue dst, ref GesVmValue a, ref GesVmValue b, ref GameEventScriptTextTable textTable, bool requireAll)
    {
        if (b.Kind is Nothing)
        {
            dst.SetNothing();
            return;
        }

        if (b.Kind is Stream && b.ObjectValue is IGesVmStream stream)
        {
            GesVmContainsAnyAllStream(ref dst, ref a, stream, requireAll);
            return;
        }

        var candidate = dst.OwningState.CreateNothing();
        var probe = dst.OwningState.CreateNothing();

        switch (a.Kind)
        {
            case List when a.ObjectValue is GesVmValue[] list:
                for (var i = 0; i < list.Length; i++)
                {
                    candidate = list[i];
                    probe.GesVmContains(ref candidate, ref b, ref textTable);
                    if (probe.IsTrue)
                    {
                        if (!requireAll)
                        {
                            dst.SetBoolean(true);
                            return;
                        }
                    }
                    else if (requireAll)
                    {
                        dst.SetBoolean(false);
                        return;
                    }

                }

                dst.SetBoolean(requireAll);
                return;
            case Dice when a.ObjectValue is int[] dice:
                for (var i = 0; i < dice.Length; i++)
                {
                    candidate.SetInteger(dice[i]);
                    probe.GesVmContains(ref candidate, ref b, ref textTable);
                    if (probe.IsTrue)
                    {
                        if (!requireAll)
                        {
                            dst.SetBoolean(true);
                            return;
                        }
                    }
                    else if (requireAll)
                    {
                        dst.SetBoolean(false);
                        return;
                    }

                }

                dst.SetBoolean(requireAll);
                return;
            case Text or Tag:
            {
                var text = a.ReadTextOrTag();
                for (var i = 0; i < text.Length; i++)
                {
                    candidate.SetText(text[i].ToString());
                    probe.GesVmContains(ref candidate, ref b, ref textTable);
                    if (probe.IsTrue)
                    {
                        if (!requireAll)
                        {
                            dst.SetBoolean(true);
                            return;
                        }
                    }
                    else if (requireAll)
                    {
                        dst.SetBoolean(false);
                        return;
                    }

                }

                dst.SetBoolean(requireAll);
                return;
            }
            case GameEventScriptBytecodeTypeKind.Range when a.ObjectValue is GesVmValueRangeInteger range:
            {
                var length = StepH.GameEventScript.Types.GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
                var value = range.From;
                for (var i = 0L; i < length; i++)
                {
                    candidate.SetInteger(value);
                    value += range.Step;
                    probe.GesVmContains(ref candidate, ref b, ref textTable);
                    if (probe.IsTrue)
                    {
                        if (!requireAll)
                        {
                            dst.SetBoolean(true);
                            return;
                        }
                    }
                    else if (requireAll)
                    {
                        dst.SetBoolean(false);
                        return;
                    }

                }

                dst.SetBoolean(requireAll);
                return;
            }
            case GameEventScriptBytecodeTypeKind.Range when a.ObjectValue is GesVmValueRangeFloat range:
            {
                var length = StepH.GameEventScript.Types.GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
                var value = range.From;
                for (var i = 0L; i < length; i++)
                {
                    candidate.SetFloat(value);
                    value += range.Step;
                    probe.GesVmContains(ref candidate, ref b, ref textTable);
                    if (probe.IsTrue)
                    {
                        if (!requireAll)
                        {
                            dst.SetBoolean(true);
                            return;
                        }
                    }
                    else if (requireAll)
                    {
                        dst.SetBoolean(false);
                        return;
                    }

                }

                dst.SetBoolean(requireAll);
                return;
            }
            default:
                dst.SetBoolean(requireAll);
                return;
        }
    }
    private static void GesVmContainsAnyAllStream(ref GesVmValue dst, ref GesVmValue a, IGesVmStream stream, bool requireAll)
    {
        var candidates = new GesVmValue[8];
        var candidateCount = 0;

        void AddCandidate(GesVmValue value)
        {
            if (candidateCount == candidates.Length) Array.Resize(ref candidates, candidates.Length << 1);
            candidates[candidateCount++] = value;
        }

        var candidate = dst.OwningState.CreateNothing();
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
                var text = a.ReadTextOrTag();
                for (var i = 0; i < text.Length; i++)
                {
                    candidate.SetText(text[i].ToString());
                    AddCandidate(candidate);
                }

                break;
            }
            case GameEventScriptBytecodeTypeKind.Range when a.ObjectValue is GesVmValueRangeInteger range:
            {
                var length = StepH.GameEventScript.Types.GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
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
                var length = StepH.GameEventScript.Types.GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
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
                dst.SetBoolean(requireAll);
                return;
            }

            var item = dst.OwningState.CreateNothing();
            if (!requireAll)
            {
                while (stream.TryNext(ref item))
                {
                    for (var i = 0; i < candidateCount; i++)
                    {
                        if (!item.EqualsValue(ref candidates[i])) continue;
                        dst.SetBoolean(true);
                        return;
                    }
                }

                dst.SetBoolean(false);
                return;
            }

            var found = new bool[candidateCount];
            var foundCount = 0;
            while (stream.TryNext(ref item))
            {
                for (var i = 0; i < candidateCount; i++)
                {
                    if (found[i] || !item.EqualsValue(ref candidates[i])) continue;
                    found[i] = true;
                    foundCount++;
                }

                if (foundCount != candidateCount) continue;
                dst.SetBoolean(true);
                return;
            }

            dst.SetBoolean(false);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }
    internal static void GesVmContainsValue(ref this GesVmValue dst, ref GesVmValue a, ref GesVmValue b, ref GameEventScriptTextTable textTable)
    {
        switch (b.Kind)
        {
            case Map or Custom when b.ObjectValue is GesVmValueMap map:
                for (var i = 0; i < map.StorageLength; i++)
                {
                    if (!map.IsVisibleAt(i)) continue;
                    var mapValue = map.ValueAt(i);
                    if (!mapValue.EqualsValue(ref a)) continue;
                    dst.SetBoolean(true);
                    return;
                }

                dst.SetBoolean(false);
                return;
            case Vector or Point when b.ObjectValue is GesVmValueVectorPoint triplet:
                if (!a.IsNumeric)
                {
                    dst.SetBoolean(false);
                    return;
                }

                var value = dst.OwningState.CreateNothing();
                value.SetFloat(triplet.X, b.Unit);
                if (value.EqualsValue(ref a))
                {
                    dst.SetBoolean(true);
                    return;
                }

                value.SetFloat(triplet.Y, b.Unit);
                if (value.EqualsValue(ref a))
                {
                    dst.SetBoolean(true);
                    return;
                }

                value.SetFloat(triplet.Z, b.Unit);
                dst.SetBoolean(value.EqualsValue(ref a));
                return;
            case Nothing:
                dst.SetNothing();
                return;
            default:
                dst.SetBoolean(false);
                return;
        }
    }
    internal static void GesVmUnion(ref this GesVmValue dst, ref GesVmValue a, ref GesVmValue b, ref GameEventScriptTextTable textTable)
    {
        if (a.Kind is Nothing || b.Kind is Nothing)
        {
            dst.SetNothing();
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
                        var map = new Dictionary<string, GesVmValue>(aMap.StorageLength + bMap.StorageLength, StringComparer.Ordinal);
                        for (var i = 0; i < aMap.StorageLength; i++) map[aMap.KeyAt(i)] = aMap.ValueAt(i);
                        for (var i = 0; i < bMap.StorageLength; i++) map[bMap.KeyAt(i)] = bMap.ValueAt(i);
                        dst.SetMap(new GesVmValueMap(dst.OwningState, map));
                        return;
                    }
                    case List when b.ObjectValue is GesVmValue[] keys:
                    {
                        var map = new Dictionary<string, GesVmValue>(aMap.StorageLength + keys.Length, StringComparer.Ordinal);
                        for (var i = 0; i < aMap.StorageLength; i++) map[aMap.KeyAt(i)] = aMap.ValueAt(i);
                        for (var i = 0; i < keys.Length; i++)
                        {
                            var keyValue = keys[i];
                            if (keyValue.Kind is not (Text or Tag))
                            {
                                dst.SetNothing();
                                return;
                            }

                            var key = keyValue.ReadTextOrTag();
                            if (map.ContainsKey(key)) continue;
                            var flag = dst.OwningState.CreateBoolean(true);
                            map[key] = flag;
                        }

                        dst.SetMap(new GesVmValueMap(dst.OwningState, map));
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
                        var list = dst.OwningState.CreateList(aList.Length + bList.Length);
                        for (var i = 0; i < aList.Length; i++) list[i] = aList[i];
                        for (var i = 0; i < bList.Length; i++) list[aList.Length + i] = bList[i];
                        dst.SetList(list);
                        return;
                    }
                    case Dice when b.ObjectValue is int[] bDice:
                    {
                        var list = dst.OwningState.CreateList(aList.Length + bDice.Length);
                        for (var i = 0; i < aList.Length; i++) list[i] = aList[i];
                        for (var i = 0; i < bDice.Length; i++) list[aList.Length + i].SetInteger(bDice[i]);
                        dst.SetList(list);
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
                        dst.SetDice(dice);
                        return;
                    }
                    case List when b.ObjectValue is GesVmValue[] bList:
                    {
                        var list = dst.OwningState.CreateList(aDice.Length + bList.Length);
                        for (var i = 0; i < aDice.Length; i++) list[i].SetInteger(aDice[i]);
                        for (var i = 0; i < bList.Length; i++) list[aDice.Length + i] = bList[i];
                        dst.SetList(list);
                        return;
                    }
                }

                break;
            }
        }

        dst.SetNothing();
    }
    internal static void GesVmIntersect(ref this GesVmValue dst, ref GesVmValue a, ref GesVmValue b, ref GameEventScriptTextTable textTable)
    {
        if (a.Kind is Nothing || b.Kind is Nothing)
        {
            dst.SetNothing();
            return;
        }

        switch (a.Kind)
        {
            case Map when a.ObjectValue is GesVmValueMap aMap:
            {
                var keys = new HashSet<string>(StringComparer.Ordinal);
                switch (b.Kind)
                {
                    case Map when b.ObjectValue is GesVmValueMap bMap:
                    {
                        for (var i = 0; i < bMap.StorageLength; i++) keys.Add(bMap.KeyAt(i));
                        break;
                    }
                    case List when b.ObjectValue is GesVmValue[] keyList:
                    {
                        for (var i = 0; i < keyList.Length; i++)
                        {
                            var keyValue = keyList[i];
                            if (keyValue.Kind is not (Text or Tag))
                            {
                                dst.SetNothing();
                                return;
                            }

                            keys.Add(keyValue.ReadTextOrTag());
                        }
                        break;
                    }
                    default:
                        dst.SetNothing();
                        return;
                }
                var map = new Dictionary<string, GesVmValue>(StringComparer.Ordinal);
                for (var i = 0; i < aMap.StorageLength; i++)
                {
                    var key = aMap.KeyAt(i);
                    if (keys.Contains(key)) map[key] = aMap.ValueAt(i);
                }
                dst.SetMap(new GesVmValueMap(dst.OwningState, map));
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
                                if (removed[j] || !leftList[i].EqualsValue(ref rightList[j])) continue;
                                removed[j] = true;
                                resultLength++;
                                break;
                            }
                        }
                        var list = dst.OwningState.CreateList(resultLength);
                        var index = 0;
                        Array.Clear(removed, 0, removed.Length);
                        for (var i = 0; i < leftList.Length; i++)
                        {
                            for (var j = 0; j < rightList.Length; j++)
                            {
                                if (removed[j] || !leftList[i].EqualsValue(ref rightList[j])) continue;
                                removed[j] = true;
                                list[index++] = leftList[i];
                                break;
                            }
                        }
                        dst.SetList(list);
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
                        var list = dst.OwningState.CreateList(resultLength);
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
                        dst.SetList(list);
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
                        dst.SetDice(dice);
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
                        var list = dst.OwningState.CreateList(resultLength1);
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
                        dst.SetList(list);
                        return;
                }
                break;
            }
        }

        dst.SetNothing();
    }
    internal static void GesVmZip(ref this GesVmValue dst, ref GesVmValue a, ref GesVmValue b, ref GameEventScriptTextTable textTable)
    {
        if (a.Kind is not List || b.Kind is not List || a.ObjectValue is not GesVmValue[] aList || b.ObjectValue is not GesVmValue[] bList)
        {
            dst.SetNothing();
            return;
        }

        var length = Math.Min(aList.Length, bList.Length);
        var list = dst.OwningState.CreateList(length);
        for (var i = 0; i < length; i++)
        {
            list[i].SetMap(new GesVmValueMap(dst.OwningState, new Dictionary<string, GesVmValue>
            {
                ["left"] = aList[i],
                ["right"] = bList[i]
            }));
        }

        dst.SetList(list);
    }
    internal static void GesVmValues(ref this GesVmValue dst, ref GesVmValue a)
    {
        switch (a.Kind)
        {
            case Map or Custom when a.ObjectValue is GesVmValueMap map:
                dst.SetList(map.ValueList);
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
                var values = dst.OwningState.CreateList(valueKeyCount);
                for (var i = 0; i < valueKeyCount; i++) values[i].BindArguments(valueEntries[valueKeys[i]]);
                dst.SetList(values);
                break;
            default:
                dst.SetNothing();
                break;
        }
    }
    internal static void GesVmKeys(ref this GesVmValue dst, ref GesVmValue a)
    {
        switch (a.Kind)
        {
            case Map or Custom when a.ObjectValue is GesVmValueMap map:
                dst.SetList(map.KeyList);
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
                var keys = dst.OwningState.CreateList(keyCount);
                for (var i = 0; i < keyCount; i++) keys[i].SetTag(keyKeys[i]);
                dst.SetList(keys);
                break;
            default:
                dst.SetNothing();
                break;
        }
    }
    internal static void GesVmEntries(ref this GesVmValue dst, ref GesVmValue a)
    {
        switch (a.Kind)
        {
            case Map or Custom when a.ObjectValue is GesVmValueMap map:
                dst.SetList(map.EntryList);
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
                var entries = dst.OwningState.CreateList(entryKeyCount);
                for (var i = 0; i < entryKeyCount; i++)
                {
                    var value = dst.OwningState.CreateNothing();
                    value.BindArguments(entryEntries[entryKeys[i]]);
                    entries[i].SetMap(new GesVmValueMap(dst.OwningState, new Dictionary<string, GesVmValue> { ["key"] = dst.OwningState.CreateTag(entryKeys[i]), ["value"] = value }));
                }

                dst.SetList(entries);
                break;
            default:
                dst.SetNothing();
                break;
        }
    }

}
