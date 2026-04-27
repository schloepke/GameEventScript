using System;
using System.Collections.Generic;
using StepH.Flow.EventScript.Interpreter;
using StepH.Flow.EventScript.Parser;
using StepH.Flow.EventScript.Runtime;
using StepH.Flow.EventScript.Types;

namespace StepH.Flow.EventScript.Experimental;

internal sealed class ExperimentalCompiledExecutionContext
{
    private readonly Stack<Dictionary<string, EventScriptValue>> _scopes = new();
    private readonly EventScriptContext _context;
    private readonly bool _diagnosticsEnabled;

    public ExperimentalCompiledExecutionContext(EventScriptContext context, bool diagnosticsEnabled)
    {
        _context = context;
        _diagnosticsEnabled = diagnosticsEnabled;
        _scopes.Push(new Dictionary<string, EventScriptValue>(StringComparer.Ordinal));
    }

    public void PushScope() => _scopes.Push(new Dictionary<string, EventScriptValue>(StringComparer.Ordinal));

    public void PopScope()
    {
        if (_scopes.Count == 1)
        {
            return;
        }

        _scopes.Pop();
    }

    public void Define(string name, EventScriptValue value)
        => _scopes.Peek()[name] = value;

    public EventScriptValue Resolve(string name)
    {
        foreach (var scope in _scopes)
        {
            if (scope.TryGetValue(name, out var value))
            {
                return value;
            }
        }

        return EventScriptValue.Nothing;
    }

    public bool TryConsumeExecutionStep(string detail)
        => _context.RuntimeBudget.TryConsumeExecutionStep(detail);

    public bool TryConsumeLoopIteration(string detail)
        => _context.RuntimeBudget.TryConsumeLoopIteration(detail);

    public bool TryEnterCall(string detail)
        => _context.RuntimeBudget.TryEnterCall(detail);

    public void ExitCall()
        => _context.RuntimeBudget.ExitCall();

    public bool TryCheckRangeLength(EventScriptValue range, string detail)
    {
        if (!EventScriptRuntimeLimitUtilities.TryGetRangeLength(range, out var length))
        {
            return true;
        }

        return _context.RuntimeBudget.TryCheckRangeLength(length, detail);
    }

    public bool TryCheckMaterializedValue(EventScriptValue value, string detail)
        => TryCheckRangeLength(value, detail);

    public bool TryCheckGeneratedCollectionItemCount(int count, string detail)
        => _context.RuntimeBudget.TryCheckGeneratedCollectionItemCount(count, detail);

    public bool TryCheckDice(DiceExpressionNode diceExpression)
        => _context.RuntimeBudget.TryCheckDice(diceExpression);

    public void Publish(string message, IReadOnlyDictionary<string, EventScriptValue> arguments)
    {
        var publishedMessage = new EventScriptMessage(message, arguments);
        _context.Publish(publishedMessage);
        if (_context.PublishCallbackRecordsDiagnostics)
        {
            return;
        }

        EventScriptInvocationKernel.RecordDiagnostic(
            _context,
            _diagnosticsEnabled,
            EventScriptDiagnosticEventKind.EventPublished,
            publishedMessage.Name,
            publishedMessage.Arguments,
            $"Published '{publishedMessage.Name}'");
    }

    public void RecordDiagnostic(EventScriptDiagnosticEventKind kind, string name, IReadOnlyDictionary<string, EventScriptValue> arguments, string? detail = null)
        => EventScriptInvocationKernel.RecordDiagnostic(_context, _diagnosticsEnabled, kind, name, arguments, detail);
}
