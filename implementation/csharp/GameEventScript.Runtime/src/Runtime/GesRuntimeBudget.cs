// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Runtime.CompilerServices;
using GameEventScript.Api;

namespace GameEventScript.Runtime;

internal sealed class GesRuntimeBudget(GameEventScriptContext context, GameEventScriptRuntimeLimits limits)
{
    private long _executionSteps;
    private long _loopIterations;
    private int _callDepth;
    private bool _exhausted;

    public GameEventScriptRuntimeLimits Limits { get; } = limits;

    public bool IsExhausted => _exhausted;

    public bool IsStepping => false;

    public void Reset()
    {
        _executionSteps = 0;
        _loopIterations = 0;
        _callDepth = 0;
        _exhausted = false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReserveExecutionSlice(int requestedSteps, string detail)
    {
        if (_exhausted || requestedSteps <= 0) return 0;
        var limit = Limits.MaxExecutionSteps;
        if (limit <= 0) return requestedSteps;
        var remaining = limit - _executionSteps;
        if (remaining <= 0)
        {
            MarkExhausted("MaxExecutionSteps", detail, limit);
            return 0;
        }

        var reserved = (int)Math.Min(requestedSteps, remaining);
        _executionSteps += reserved;
        return reserved;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CompleteExecutionSlice(int executedSteps, int reservedSteps, bool stillProcessing, string detail)
    {
        if (Limits.MaxExecutionSteps <= 0 || _exhausted) return;
        if (executedSteps < reservedSteps) _executionSteps -= reservedSteps - executedSteps;
        if (stillProcessing && _executionSteps >= Limits.MaxExecutionSteps)
            MarkExhausted("MaxExecutionSteps", detail, Limits.MaxExecutionSteps);
    }

    public bool ConsumeLoopIterationIfAvailable(string detail)
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

    public bool EnterCallIfAvailable(string detail)
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

    public bool CheckRangeLengthWithinLimit(long length, string detail)
    {
        var limit = Limits.MaxRangeItems;
        if (limit <= 0 || length <= limit)
        {
            return true;
        }

        ReportLimit("MaxRangeItems", detail, limit);
        return false;
    }

    public bool CheckGeneratedCollectionItemCountWithinLimit(long count)
    {
        if (_exhausted) return false;
        var limit = Limits.MaxGeneratedCollectionItems;
        if (limit <= 0 || count <= limit)
        {
            return true;
        }

        MarkExhausted("MaxGeneratedCollectionItems", "Generated collection exceeds the configured item limit.", limit);
        return false;
    }

    public bool CheckDiceWithinLimit(int diceCount, int sideCount)
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

    public void Exhaust(string limitName, string detail, int limit)
    {
        if (_exhausted) return;
        MarkExhausted(limitName, detail, limit);
    }

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

}
