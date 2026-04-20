#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Collections.Generic;

namespace StepH.Flow.EventScript.Parser;

// Abstract nodes for the syntax tree

public abstract record EventScriptNode;
public abstract record StatementNode : EventScriptNode;
public abstract record ExpressionNode : EventScriptNode;
public abstract record CollectionSelectorNode : EventScriptNode;
public abstract record ObjectMatchValueNode : EventScriptNode;
public abstract record DicePatternNode : EventScriptNode;

// Root node of the syntax tree

public sealed record EventScriptModule(string ModuleName, string SourceName, IReadOnlyList<TypeDefinitionNode> TypeDefinitions, IReadOnlyList<RuleDefinitionNode> RuleDefinitions, IReadOnlyList<SelectDefinitionNode> SelectDefinitions, IReadOnlyList<EventHandlerNode> Handlers) : EventScriptNode;

// Type/Rule/Select/Handler nodes

public sealed record EventHandlerNode(string Message, IReadOnlyList<string> Parameters, IReadOnlyList<StatementNode> Statements) : EventScriptNode;
public sealed record TypeDefinitionNode(string Name, IReadOnlyList<TypeFieldDefinitionNode> Fields) : EventScriptNode;
public sealed record TypeFieldDefinitionNode(string Name, string TypeName, ExpressionNode? MinimumExpression, ExpressionNode? MaximumExpression, ExpressionNode? ComputedExpression) : EventScriptNode;
public sealed record RuleDefinitionNode(string Name, IReadOnlyList<string> Parameters, ExpressionNode Expression) : EventScriptNode;
public sealed record SelectDefinitionNode(string Name, IReadOnlyList<string> Parameters, ExpressionNode Expression) : EventScriptNode;

// Statement nodes

public sealed record PublishStatementNode(string Message, IReadOnlyList<NamedArgumentNode> Arguments) : StatementNode;
public sealed record NamedArgumentNode(string Name, ExpressionNode Expression) : EventScriptNode;
public sealed record LetStatementNode(string Identifier, string? DeclaredType, ExpressionNode Expression) : StatementNode;
public sealed record StatementBodyNode(bool IsBlock, IReadOnlyList<StatementNode> Statements) : EventScriptNode;
public sealed record IfStatementNode(ExpressionNode Condition, StatementBodyNode ThenBody, StatementBodyNode? ElseBody) : StatementNode;
public sealed record ForStatementNode(string Identifier, ExpressionNode Source, StatementBodyNode Body) : StatementNode;
public sealed record SeededRandomStatementNode(ExpressionNode SeedExpression, StatementBodyNode Body) : StatementNode;
public sealed record ExpressionStatementNode(ExpressionNode Expression) : StatementNode;

// Expression nodes

public sealed record IdentifierExpressionNode(string Name) : ExpressionNode;
public sealed record TagLiteralExpressionNode(string Name) : ExpressionNode;
public sealed record CallExpressionNode(string Name, IReadOnlyList<ExpressionNode> Arguments) : ExpressionNode;
public sealed record BooleanLiteralExpressionNode(bool Value) : ExpressionNode;
public sealed record IntegerLiteralExpressionNode(long Value) : ExpressionNode;
public sealed record DecimalLiteralExpressionNode(decimal Value) : ExpressionNode;
public sealed record PercentageLiteralExpressionNode(decimal PercentValue) : ExpressionNode;
public sealed record TextLiteralExpressionNode(string Value) : ExpressionNode;
public sealed record ListLiteralExpressionNode(IReadOnlyList<ExpressionNode> Items) : ExpressionNode;
public sealed record SetLiteralExpressionNode(IReadOnlyList<ExpressionNode> Items) : ExpressionNode;
public sealed record DictionaryLiteralExpressionNode(IReadOnlyList<DictionaryEntryNode> Entries) : ExpressionNode;
public sealed record DictionaryEntryNode(string Key, ExpressionNode Value) : EventScriptNode;
public sealed record UnaryExpressionNode(string Operator, ExpressionNode Operand) : ExpressionNode;
public sealed record VariadicTaggedExpressionNode(string Operator, IReadOnlyList<ExpressionNode> Arguments) : ExpressionNode;
public sealed record ClampExpressionNode(ExpressionNode Value, ExpressionNode Minimum, ExpressionNode Maximum) : ExpressionNode;
public sealed record RandomExpressionNode(ExpressionNode FromExpression, ExpressionNode ToExpression) : ExpressionNode;
public sealed record SeededRandomExpressionNode(ExpressionNode SeedExpression, ExpressionNode BodyExpression) : ExpressionNode;
public sealed record DiceExpressionNode(int DiceCount, int SideCount) : ExpressionNode;
public sealed record GeneratedCollectionExpressionNode(string CollectionType, string Identifier, ExpressionNode FromExpression, ExpressionNode ToExpression, ExpressionNode? StepExpression, ExpressionNode? Predicate, ExpressionNode Projection) : ExpressionNode;
public sealed record GuardedChoiceExpressionNode(IReadOnlyList<GuardedChoiceBranchNode> Branches, ExpressionNode OtherwiseExpression) : ExpressionNode;
public sealed record GuardedChoiceBranchNode(ExpressionNode ValueExpression, ExpressionNode ConditionExpression) : EventScriptNode;
public sealed record BinaryExpressionNode(ExpressionNode Left, string Operator, ExpressionNode Right) : ExpressionNode;
public sealed record RulePredicateExpressionNode(ExpressionNode Value, string RuleName) : ExpressionNode;
public sealed record TypeCheckExpressionNode(ExpressionNode Value, string TypeName) : ExpressionNode;
public sealed record TypeCastExpressionNode(ExpressionNode Value, string TypeName) : ExpressionNode;
public sealed record DiceCountPatternNode(int Count, ExpressionNode? Face) : DicePatternNode;
public sealed record MemberAccessExpressionNode(ExpressionNode Target, string Member) : ExpressionNode;
public sealed record CollectionAccessExpressionNode(ExpressionNode Target, CollectionSelectorNode Selector) : ExpressionNode;

// Collection selector nodes

public sealed record ExpressionSelectorNode(ExpressionNode Expression) : CollectionSelectorNode;
public sealed record PatternSelectorNode(DicePatternNode Pattern) : CollectionSelectorNode;
public sealed record ObjectMatchSelectorNode(ObjectMatchPatternNode Pattern) : CollectionSelectorNode;
public sealed record TakePatternSelectorNode(DicePatternNode Pattern) : CollectionSelectorNode;
public sealed record SequenceSliceSelectorNode(string Operation, string Scope, int Count) : CollectionSelectorNode;
public sealed record PredicateSelectorNode(string Operator, string Identifier, ExpressionNode Predicate) : CollectionSelectorNode;
public sealed record CountSelectorNode(string Identifier, ExpressionNode Predicate) : CollectionSelectorNode;
public sealed record ChooseSelectorNode(int Count, bool AtRandom, string? Identifier, ExpressionNode? Predicate, string? WeightIdentifier, ExpressionNode? WeightExpression) : CollectionSelectorNode;
public sealed record DrawSelectorNode(int Count) : CollectionSelectorNode;
public sealed record ShuffleSelectorNode : CollectionSelectorNode;
public sealed record ReverseSelectorNode : CollectionSelectorNode;
public sealed record EdgeSelectorNode(string Mode, string? Identifier, ExpressionNode? Predicate) : CollectionSelectorNode;
public sealed record FilterSelectorNode(string Identifier, ExpressionNode Predicate) : CollectionSelectorNode;
public sealed record SumSelectorNode(string Identifier, ExpressionNode Projection) : CollectionSelectorNode;
public sealed record AverageSelectorNode(string Identifier, ExpressionNode Projection) : CollectionSelectorNode;
public sealed record SelectSelectorNode(string Identifier, ExpressionNode Projection) : CollectionSelectorNode;
public sealed record DictionarySelectorNode(string Identifier, ExpressionNode KeyProjection, ExpressionNode? ValueProjection) : CollectionSelectorNode;
public sealed record MinSelectorNode(string Identifier, ExpressionNode Projection) : CollectionSelectorNode;
public sealed record MaxSelectorNode(string Identifier, ExpressionNode Projection) : CollectionSelectorNode;
public sealed record ContainsSelectorNode(string Mode, ExpressionNode ValueExpression) : CollectionSelectorNode;
public sealed record SortSelectorNode(string Direction) : CollectionSelectorNode;
public sealed record DistinctSelectorNode(string? Identifier, ExpressionNode? Projection) : CollectionSelectorNode;
public sealed record GroupBySelectorNode(string Identifier, ExpressionNode Projection) : CollectionSelectorNode;
public sealed record OrderBySelectorNode(string Direction, string Identifier, ExpressionNode Projection) : CollectionSelectorNode;

// Dice Pattern nodes

public sealed record DiceFullHousePatternNode : DicePatternNode;
public sealed record DiceStraightPatternNode : DicePatternNode;

// Object matcher nodes

public sealed record ObjectMatchPatternNode(IReadOnlyList<ObjectMatchEntryNode> Entries) : EventScriptNode;
public sealed record ObjectMatchEntryNode(string Key, ObjectMatchValueNode Value) : EventScriptNode;
public sealed record ObjectMatchExpressionValueNode(ExpressionNode Expression) : ObjectMatchValueNode;
public sealed record ObjectMatchNestedValueNode(ObjectMatchPatternNode Pattern) : ObjectMatchValueNode;
