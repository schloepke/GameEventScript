#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using System.Runtime.CompilerServices;

namespace StepH.GameEventScript.Runtime;

internal sealed class GesRuntimeBudget(GameEventScriptSession context, GameEventScriptRuntimeLimits limits)
{
    private long _executionSteps;
    private long _loopIterations;
    private int _callDepth;
    private bool _exhausted;

    public GameEventScriptRuntimeLimits Limits { get; } = limits;

    public bool IsExhausted => _exhausted;

    public bool IsStepping => false;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryConsumeExecutionStep(string detail)
    {
        if (_exhausted)
        {
            return false;
        }

        var limit = Limits.MaxExecutionSteps;
        if (limit <= 0)
        {
            return true;
        }

        if (_executionSteps >= limit)
        {
            MarkExhausted("MaxExecutionSteps", detail, limit);
            return false;
        }

        _executionSteps++;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryConsumeExecutionSteps(int count, string detail)
    {
        if (_exhausted)
        {
            return false;
        }

        if (count <= 1)
        {
            count = 1;
        }

        var limit = Limits.MaxExecutionSteps;
        if (limit <= 0)
        {
            return true;
        }

        if (_executionSteps > limit - count)
        {
            MarkExhausted("MaxExecutionSteps", detail, limit);
            return false;
        }

        _executionSteps += count;
        return true;
    }

    public bool TryConsumeLoopIteration(string detail)
    {
        if (_exhausted)
        {
            return false;
        }

        var limit = Limits.MaxLoopIterations;
        if (limit > 0 && _loopIterations >= limit)
        {
            MarkExhausted("MaxLoopIterations", detail, limit);
            return false;
        }

        _loopIterations++;
        return true;
    }

    public bool TryEnterCall(string detail)
    {
        if (_exhausted)
        {
            return false;
        }

        var limit = Limits.MaxCallDepth;
        if (limit > 0 && _callDepth >= limit)
        {
            MarkExhausted("MaxCallDepth", detail, limit);
            return false;
        }

        _callDepth++;
        return true;
    }

    public void ExitCall()
    {
        if (_callDepth > 0)
        {
            _callDepth--;
        }
    }

    public bool TryCheckRangeLength(long length, string detail)
    {
        var limit = Limits.MaxRangeItems;
        if (limit <= 0 || length <= limit)
        {
            return true;
        }

        ReportLimit("MaxRangeItems", detail, limit);
        return false;
    }

    public bool TryCheckGeneratedCollectionItemCount(int count, string detail)
    {
        var limit = Limits.MaxGeneratedCollectionItems;
        if (limit <= 0 || count <= limit)
        {
            return true;
        }

        ReportLimit("MaxGeneratedCollectionItems", detail, limit);
        return false;
    }

    public bool TryCheckDice(int diceCount, int sideCount)
    {
        if (Limits.MaxDiceCount > 0 && diceCount > Limits.MaxDiceCount)
        {
            ReportLimit("MaxDiceCount", $"Dice count {diceCount} exceeds the configured limit.", Limits.MaxDiceCount);
            return false;
        }

        if (Limits.MaxDiceSides > 0 && sideCount > Limits.MaxDiceSides)
        {
            ReportLimit("MaxDiceSides", $"Dice side count {sideCount} exceeds the configured limit.", Limits.MaxDiceSides);
            return false;
        }

        return true;
    }

    public void ReportLimit(string limitName, string detail, int limit)
        => context.RecordRuntimeLimitReached(limitName, detail, limit);

    private void MarkExhausted(string limitName, string detail, int limit)
    {
        _exhausted = true;
        ReportLimit(limitName, detail, limit);
    }
}

internal static class GesRuntimeLimitUtilities
{
    public static long GetRangeLength(long from, long to, long step)
    {
        if (step == 0)
        {
            return 0;
        }

        return GameEventScriptRangeMath.GetLength(from, to, step);
    }

    public static long GetRangeLength(double from, double to, double step)
        => GameEventScriptRangeMath.GetLength(from, to, step);

    public static bool TryGetRangeLength(GameEventScriptValue value, out long length)
    {
        if (value is GameEventScriptRangeValue range)
        {
            length = range.IsIntegerRange
                ? GetRangeLength(range.From, range.To, range.Step)
                : GetRangeLength(range.FromNumber, range.ToNumber, range.StepNumber);
            return true;
        }

        length = 0;
        return false;
    }

}
