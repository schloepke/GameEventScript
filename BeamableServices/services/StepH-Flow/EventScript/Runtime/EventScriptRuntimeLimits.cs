#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using StepH.Flow.EventScript.Parser;
using StepH.Flow.EventScript.Types;

namespace StepH.Flow.EventScript.Runtime;

public sealed class EventScriptRuntimeLimits
{
    public static EventScriptRuntimeLimits Default { get; } = new();

    public int MaxExecutionSteps { get; init; } = 100_000;

    public int MaxLoopIterations { get; init; } = 100_000;

    public int MaxCallDepth { get; init; } = 64;

    public int MaxRangeItems { get; init; } = 10_000;

    public int MaxGeneratedCollectionItems { get; init; } = 10_000;

    public int MaxDiceCount { get; init; } = 1_000;

    public int MaxDiceSides { get; init; } = 1_000_000;
}

internal sealed class EventScriptRuntimeBudget(EventScriptContext context, EventScriptRuntimeLimits limits)
{
    private long _executionSteps;
    private long _loopIterations;
    private int _callDepth;
    private bool _exhausted;

    public EventScriptRuntimeLimits Limits { get; } = limits ?? EventScriptRuntimeLimits.Default;

    public bool IsExhausted => _exhausted;

    public bool TryConsumeExecutionStep(string detail)
    {
        if (_exhausted)
        {
            return false;
        }

        var limit = Limits.MaxExecutionSteps;
        if (limit > 0 && _executionSteps >= limit)
        {
            MarkExhausted("MaxExecutionSteps", detail, limit);
            return false;
        }

        _executionSteps++;
        return true;
    }

    public bool TryConsumeExecutionSteps(int count, string detail)
    {
        if (count <= 1)
        {
            return TryConsumeExecutionStep(detail);
        }

        if (_exhausted)
        {
            return false;
        }

        var limit = Limits.MaxExecutionSteps;
        if (limit > 0 && _executionSteps > limit - count)
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

    public bool TryCheckDice(DiceExpressionNode diceExpression)
    {
        if (Limits.MaxDiceCount > 0 && diceExpression.DiceCount > Limits.MaxDiceCount)
        {
            ReportLimit("MaxDiceCount", $"Dice count {diceExpression.DiceCount} exceeds the configured limit.", Limits.MaxDiceCount);
            return false;
        }

        if (Limits.MaxDiceSides > 0 && diceExpression.SideCount > Limits.MaxDiceSides)
        {
            ReportLimit("MaxDiceSides", $"Dice side count {diceExpression.SideCount} exceeds the configured limit.", Limits.MaxDiceSides);
            return false;
        }

        return true;
    }

    public void ReportLimit(string limitName, string detail, int limit)
        => context.RecordDiagnostic(
            EventScriptDiagnosticEventKind.RuntimeLimitReached,
            limitName,
            EventScriptNamedArguments.Empty,
            $"{detail} Limit: {limit}.");

    private void MarkExhausted(string limitName, string detail, int limit)
    {
        _exhausted = true;
        ReportLimit(limitName, detail, limit);
    }
}

internal static class EventScriptRuntimeLimitUtilities
{
    public static long GetRangeLength(long from, long to, long step)
    {
        if (step == 0)
        {
            return 0;
        }

        if (step > 0)
        {
            if (from > to)
            {
                return 0;
            }

            return ClampRangeLength(((decimal)to - from) / step);
        }

        if (from < to)
        {
            return 0;
        }

        return ClampRangeLength(((decimal)from - to) / -(decimal)step);
    }

    public static bool TryGetRangeLength(EventScriptValue value, out long length)
    {
        if (value is EventScriptRangeValue range)
        {
            length = GetRangeLength(range.From, range.To, range.Step);
            return true;
        }

        length = 0;
        return false;
    }

    private static long ClampRangeLength(decimal zeroBasedDistance)
    {
        var length = decimal.Floor(zeroBasedDistance) + 1m;
        if (length <= 0m)
        {
            return 0;
        }

        return length > long.MaxValue ? long.MaxValue : (long)length;
    }
}
