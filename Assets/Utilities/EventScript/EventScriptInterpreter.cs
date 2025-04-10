using System;
using System.Collections.Generic;

namespace StepH.Utilities.EventScript
{
    public class Interpreter
    {
        private readonly Dictionary<string, EventHandlerNode> eventHandlers = new();
        private readonly Dictionary<string, object> globals = new();

        public void LoadProgram(ProgramNode program)
        {
            foreach (var handler in program.Handlers)
            {
                eventHandlers[handler.Name] = handler;
            }
        }

        public void Emit(string eventName, params object[] args)
        {
            if (!eventHandlers.TryGetValue(eventName, out var handler))
                throw new Exception($"No handler for event: {eventName}");

            var scope = new Dictionary<string, object>();
            for (int i = 0; i < handler.Parameters.Count; i++)
            {
                if (i < args.Length)
                    scope[handler.Parameters[i]] = args[i];
            }

            ExecuteBlock(handler.Body, scope);
        }

        private void ExecuteBlock(List<StatementNode> statements, Dictionary<string, object> scope)
        {
            foreach (var stmt in statements)
            {
                ExecuteStatement(stmt, scope);
            }
        }

        private void ExecuteStatement(StatementNode stmt, Dictionary<string, object> scope)
        {
            switch (stmt)
            {
                case LetStatement letStmt:
                    scope[letStmt.Identifier] = EvaluateExpression(letStmt.Value, scope);
                    break;
                case EmitStatement emitStmt:
                    var args = new List<object>();
                    foreach (var expr in emitStmt.Arguments)
                        args.Add(EvaluateExpression(expr, scope));
                    Emit(emitStmt.EventName, args.ToArray());
                    break;
                case IfStatement ifStmt:
                    var condition = EvaluateExpression(ifStmt.Condition, scope);
                    if (condition is bool b && b)
                        ExecuteBlock(ifStmt.ThenBlock, new Dictionary<string, object>(scope));
                    else if (ifStmt.ElseBlock != null)
                        ExecuteBlock(ifStmt.ElseBlock, new Dictionary<string, object>(scope));
                    break;
                case ForStatement forStmt:
                    var collection = EvaluateExpression(forStmt.Collection, scope);
                    if (collection is IEnumerable<object> enumerable)
                    {
                        foreach (var item in enumerable)
                        {
                            var loopScope = new Dictionary<string, object>(scope)
                            {
                                [forStmt.Variable] = item
                            };
                            ExecuteBlock(forStmt.Body, loopScope);
                        }
                    }
                    else
                    {
                        throw new Exception("Expected iterable collection in for-in loop.");
                    }
                    break;
                case ExpressionStatement exprStmt:
                    _ = EvaluateExpression(exprStmt.Expression, scope);
                    break;
                default:
                    throw new Exception($"Unknown statement type: {stmt.GetType().Name}");
            }
        }

        private object EvaluateExpression(ExpressionNode expr, Dictionary<string, object> scope)
        {
            switch (expr)
            {
                case LiteralExpression lit:
                    return lit.Value;
                case IdentifierExpression id:
                    return scope.TryGetValue(id.Name, out var val) ? val : throw new Exception($"Undefined variable: {id.Name}");
                case BinaryExpression bin:
                    var left = EvaluateExpression(bin.Left, scope);
                    var right = EvaluateExpression(bin.Right, scope);
                    return EvaluateBinary(bin.Operator, left, right);
                case UnaryExpression un:
                    var operand = EvaluateExpression(un.Operand, scope);
                    return EvaluateUnary(un.Operator, operand);
                default:
                    throw new Exception($"Unknown expression type: {expr.GetType().Name}");
            }
        }

        private object EvaluateBinary(string op, object left, object right)
        {
            return op switch
            {
                "+" => (double)left + (double)right,
                "-" => (double)left - (double)right,
                "*" => (double)left * (double)right,
                "/" => (double)left / (double)right,
                "==" => Equals(left, right),
                "!=" => !Equals(left, right),
                ">" => (double)left > (double)right,
                "<" => (double)left < (double)right,
                ">=" => (double)left >= (double)right,
                "<=" => (double)left <= (double)right,
                _ => throw new Exception($"Unsupported binary operator: {op}")
            };
        }

        private object EvaluateUnary(string op, object operand)
        {
            return op switch
            {
                "-" => -(double)operand,
                "!" => !(bool)operand,
                _ => throw new Exception($"Unsupported unary operator: {op}")
            };
        }
    }
}
