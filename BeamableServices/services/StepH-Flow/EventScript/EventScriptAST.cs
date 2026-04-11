#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Collections.Generic;

namespace StepH.Flow.EventScript;

public class ProgramNode
{
    public List<EventHandlerNode> Handlers { get; set; } = new();
}

public class EventHandlerNode
{
    public string Name { get; set; } = string.Empty;
    public List<string> Parameters { get; set; } = new();
    public List<StatementNode> Body { get; set; } = new();
}

public abstract class StatementNode
{
}

public class EmitStatement : StatementNode
{
    public string EventName { get; set; } = string.Empty;
    public List<ExpressionNode> Arguments { get; set; } = new();
}

public class LetStatement : StatementNode
{
    public string Identifier { get; set; } = string.Empty;
    public ExpressionNode Value { get; set; } = null!;
}

public class IfStatement : StatementNode
{
    public ExpressionNode Condition { get; set; } = null!;
    public List<StatementNode> ThenBlock { get; set; } = new();
    public List<StatementNode>? ElseBlock { get; set; }
}

public class ForStatement : StatementNode
{
    public string Variable { get; set; } = string.Empty;
    public ExpressionNode Collection { get; set; } = null!;
    public List<StatementNode> Body { get; set; } = new();
}

public class ExpressionStatement : StatementNode
{
    public ExpressionNode Expression { get; set; } = null!;
}

public abstract class ExpressionNode
{
}

public class IdentifierExpression : ExpressionNode
{
    public string Name { get; set; } = string.Empty;
}

public class LiteralExpression : ExpressionNode
{
    public object Value { get; set; } // Could be string, double, bool
}

public class BinaryExpression : ExpressionNode
{
    public string Operator { get; set; } = string.Empty;
    public ExpressionNode Left { get; set; } = null!;
    public ExpressionNode Right { get; set; } = null!;
}

public class UnaryExpression : ExpressionNode
{
    public string Operator { get; set; } = string.Empty;
    public ExpressionNode Operand { get; set; } = null!;
}

public class MemberAccessExpression : ExpressionNode
{
    public ExpressionNode Object { get; set; } = null!;
    public string Property { get; set; } = string.Empty;
}

public class FunctionCallExpression : ExpressionNode
{
    public string FunctionName { get; set; } = string.Empty;
    public List<ExpressionNode> Arguments { get; set; } = new();
}