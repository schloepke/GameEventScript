#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Collections.Generic;

namespace StepH.Flow.EventScript;

public abstract record EventScriptNode;

public sealed record EventScriptProgram(IReadOnlyList<EventHandlerNode> Handlers) : EventScriptNode;
public sealed record EventHandlerNode(string Message, IReadOnlyList<string> Parameters, IReadOnlyList<StatementNode> Statements, bool IsExternal = false) : EventScriptNode;
public abstract record StatementNode : EventScriptNode;
public sealed record EmitStatementNode(string Message, IReadOnlyList<ExpressionNode> Arguments) : StatementNode;
public sealed record LetStatementNode(string Identifier, ExpressionNode Expression) : StatementNode;
public sealed record IfStatementNode(ExpressionNode Condition, IReadOnlyList<StatementNode> ThenStatements, IReadOnlyList<StatementNode> ElseStatements) : StatementNode;
public sealed record ForStatementNode(string Identifier, ExpressionNode Source, IReadOnlyList<StatementNode> Statements) : StatementNode;
public sealed record ExpressionStatementNode(ExpressionNode Expression) : StatementNode;
public abstract record ExpressionNode : EventScriptNode;
public sealed record IdentifierExpressionNode(string Name) : ExpressionNode;
public sealed record BooleanLiteralExpressionNode(bool Value) : ExpressionNode;
public sealed record NumberLiteralExpressionNode(decimal Value, string RawText) : ExpressionNode;
public sealed record StringLiteralExpressionNode(string Value) : ExpressionNode;
public sealed record UnaryExpressionNode(string Operator, ExpressionNode Operand) : ExpressionNode;
public sealed record RandomExpressionNode(ExpressionNode FromExpression, ExpressionNode ToExpression) : ExpressionNode;
public sealed record DiceExpressionNode(int DiceCount, int SideCount, DiceModifierNode? Modifier) : ExpressionNode;
public abstract record DiceModifierNode : EventScriptNode;
public sealed record KeepHighestModifierNode(int Count) : DiceModifierNode;
public sealed record DropLowestModifierNode(int Count) : DiceModifierNode;
public sealed record BinaryExpressionNode(ExpressionNode Left, string Operator, ExpressionNode Right) : ExpressionNode;
public sealed record MemberAccessExpressionNode(ExpressionNode Target, string Member) : ExpressionNode;
public sealed record CollectionAccessExpressionNode(ExpressionNode Target, CollectionSelectorNode Selector) : ExpressionNode;
public abstract record CollectionSelectorNode : EventScriptNode;
public sealed record ExpressionSelectorNode(ExpressionNode Expression) : CollectionSelectorNode;
public sealed record CountSelectorNode : CollectionSelectorNode;
public sealed record PredicateSelectorNode(string Operator, string Identifier, ExpressionNode Predicate) : CollectionSelectorNode;
public sealed record FilterSelectorNode(string Identifier, ExpressionNode Predicate) : CollectionSelectorNode;
public sealed record SumSelectorNode(string Identifier, ExpressionNode Projection) : CollectionSelectorNode;
public sealed record SelectSelectorNode(string Identifier, ExpressionNode Projection) : CollectionSelectorNode;
