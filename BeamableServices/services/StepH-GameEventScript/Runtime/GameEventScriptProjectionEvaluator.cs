#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using StepH.GameEventScript.Compiler;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.Runtime;

internal sealed class GameEventScriptProjectionEvaluator
{
    private readonly Action _pushScope;
    private readonly Action _popScope;
    private readonly Action<string, GameEventScriptValue> _define;
    private readonly Func<ExpressionNode, GameEventScriptValue> _evaluateExpression;

    public GameEventScriptProjectionEvaluator(
        Action pushScope,
        Action popScope,
        Action<string, GameEventScriptValue> define,
        Func<ExpressionNode, GameEventScriptValue> evaluateExpression)
    {
        _pushScope = pushScope ?? throw new ArgumentNullException(nameof(pushScope));
        _popScope = popScope ?? throw new ArgumentNullException(nameof(popScope));
        _define = define ?? throw new ArgumentNullException(nameof(define));
        _evaluateExpression = evaluateExpression ?? throw new ArgumentNullException(nameof(evaluateExpression));
    }

    public GameEventScriptValue Evaluate(string identifier, ExpressionNode expression, GameEventScriptValue item)
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

    public bool EvaluateBoolean(string identifier, ExpressionNode expression, GameEventScriptValue item)
        => Evaluate(identifier, expression, item).AsBoolean();

    public decimal EvaluatePositiveWeight(string identifier, ExpressionNode expression, GameEventScriptValue item)
    {
        var value = Evaluate(identifier, expression, item);
        if (!GameEventScriptValueAlu.TryCoerceNumericForOperation(value, out var weight) || !weight.IsFinite)
        {
            return 0m;
        }

        return weight.Value > 0m ? weight.Value : 0m;
    }
}
