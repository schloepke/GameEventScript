#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using StepH.GameEventScript.Parser;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.Runtime;

internal sealed class EventScriptProjectionEvaluator
{
    private readonly Action _pushScope;
    private readonly Action _popScope;
    private readonly Action<string, EventScriptValue> _define;
    private readonly Func<ExpressionNode, EventScriptValue> _evaluateExpression;

    public EventScriptProjectionEvaluator(
        Action pushScope,
        Action popScope,
        Action<string, EventScriptValue> define,
        Func<ExpressionNode, EventScriptValue> evaluateExpression)
    {
        _pushScope = pushScope ?? throw new ArgumentNullException(nameof(pushScope));
        _popScope = popScope ?? throw new ArgumentNullException(nameof(popScope));
        _define = define ?? throw new ArgumentNullException(nameof(define));
        _evaluateExpression = evaluateExpression ?? throw new ArgumentNullException(nameof(evaluateExpression));
    }

    public EventScriptValue Evaluate(string identifier, ExpressionNode expression, EventScriptValue item)
    {
        _pushScope();
        try
        {
            _define(identifier, item);
            return _evaluateExpression(expression);
        }
        finally
        {
            _popScope();
        }
    }

    public bool EvaluateBoolean(string identifier, ExpressionNode expression, EventScriptValue item)
        => Evaluate(identifier, expression, item).AsBoolean();

    public decimal EvaluatePositiveWeight(string identifier, ExpressionNode expression, EventScriptValue item)
    {
        var value = Evaluate(identifier, expression, item);
        if (!EventScriptValueAlu.TryCoerceNumericForOperation(value, out var weight) || !weight.IsFinite)
        {
            return 0m;
        }

        return weight.Value > 0m ? weight.Value : 0m;
    }
}
