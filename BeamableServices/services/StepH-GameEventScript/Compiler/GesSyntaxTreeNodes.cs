using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.Compiler;

// Abstract nodes for the syntax tree

internal abstract record StatementNode : ScriptNode;

internal abstract record ExpressionNode : ScriptNode;

internal abstract record CollectionSelectorNode : ScriptNode;

internal abstract record ObjectMatchValueNode : ScriptNode;

internal abstract record DicePatternNode : ScriptNode;

internal abstract record IterationSourceNode : ScriptNode;

internal abstract record ScriptNode
{
    public GameEventScriptSourceLocation? SourceRange { get; init; }
}

// Root node of the syntax tree

internal sealed record ParsedScript(string ModuleName, string SourceName, IReadOnlyList<TypeDefinitionNode> TypeDefinitions, IReadOnlyList<PredicateDefinitionNode> PredicateDefinitions, IReadOnlyList<FunctionDefinitionNode> FunctionDefinitions, IReadOnlyList<EventHandlerNode> Handlers) : ScriptNode;

// Type/Predicate/Function/Handler nodes

internal sealed record ParameterNode(string? ExternalLabel, string LocalName, string? DeclaredType = null) : ScriptNode
{
    public string SignatureLabel => ExternalLabel ?? GameEventScriptMessageSignature.UnlabeledParameterName;
}

internal sealed record ArgumentNode(string? Label, ExpressionNode Expression) : ScriptNode
{
    public string Name => Label ?? GameEventScriptMessageSignature.UnlabeledParameterName;
}

internal sealed record ArgumentListNode(IReadOnlyList<ArgumentNode> Arguments) : ScriptNode
{
    public static readonly ArgumentListNode Empty = new([]);

    public int Count => Arguments.Count;

    public IReadOnlyList<ExpressionNode> Expressions => Arguments.Select(argument => argument.Expression).ToArray();
}

internal sealed record EventHandlerNode(
    string Message,
    IReadOnlyList<ParameterNode> ParameterList,
    IReadOnlyList<StatementNode> Statements,
    IReadOnlyList<string>? RequiredTags = null,
    IReadOnlyList<string>? ExcludedTags = null) : ScriptNode
{
    public IReadOnlyList<string> Parameters => ParameterList.Select(parameter => parameter.LocalName).ToArray();

    public IReadOnlyList<string> SignatureLabels => ParameterList.Select(parameter => parameter.SignatureLabel).ToArray();

    public IReadOnlyList<string> MatchingTags { get; } = RequiredTags ?? [];

    public IReadOnlyList<string> WithoutTags { get; } = ExcludedTags ?? [];
}

internal sealed record TypeDefinitionNode(string Name, IReadOnlyList<TypeFieldDefinitionNode> Fields) : ScriptNode;
internal sealed record TypeFieldDefinitionNode(string Name, string TypeName, ExpressionNode? MinimumExpression, ExpressionNode? MaximumExpression, ExpressionNode? ComputedExpression) : ScriptNode;
internal sealed record PredicateDefinitionNode(string Name, IReadOnlyList<ParameterNode> ParameterList, ExpressionNode Expression) : ScriptNode
{
    public IReadOnlyList<string> Parameters => ParameterList.Select(parameter => parameter.LocalName).ToArray();
}

internal sealed record FunctionDefinitionNode(string Name, IReadOnlyList<ParameterNode> ParameterList, ExpressionNode Expression) : ScriptNode
{
    public IReadOnlyList<string> Parameters => ParameterList.Select(parameter => parameter.LocalName).ToArray();
}

// Statement nodes

internal enum PublishStatementKind
{
    Emit,
    Publish
}

internal sealed record PublishStatementNode(PublishStatementKind Kind, ExpressionNode MessageExpression, IReadOnlyList<ExpressionNode> TagExpressions) : StatementNode;
internal sealed record LetStatementNode(string Identifier, string? DeclaredType, ExpressionNode Expression) : StatementNode;
internal sealed record StatementBodyNode(bool IsBlock, IReadOnlyList<StatementNode> Statements) : ScriptNode;
internal sealed record IfStatementNode(ExpressionNode Condition, StatementBodyNode ThenBody, StatementBodyNode? ElseBody) : StatementNode;
internal sealed record ForStatementNode(string Identifier, IterationSourceNode Source, StatementBodyNode Body) : StatementNode;
internal sealed record SeededRandomStatementNode(ExpressionNode SeedExpression, StatementBodyNode Body) : StatementNode;
internal sealed record ExpressionStatementNode(ExpressionNode Expression) : StatementNode;

// Expression nodes

internal sealed record IdentifierExpressionNode(string Name) : ExpressionNode;
internal sealed record TagLiteralExpressionNode(string Name) : ExpressionNode;
internal sealed record HandlerLiteralExpressionNode(string Message, IReadOnlyList<ParameterNode> ParameterList) : ExpressionNode
{
    public IReadOnlyList<string> Parameters => ParameterList.Select(parameter => parameter.LocalName).ToArray();

    public IReadOnlyList<string> SignatureLabels => ParameterList.Select(parameter => parameter.SignatureLabel).ToArray();
}

internal sealed record MessageLiteralExpressionNode(string Message, ArgumentListNode ArgumentList) : ExpressionNode
{
    public IReadOnlyList<ArgumentNode> Arguments => ArgumentList.Arguments;
}

internal sealed record CallExpressionNode(string Name, ArgumentListNode ArgumentList) : ExpressionNode
{
    public IReadOnlyList<ExpressionNode> Arguments => ArgumentList.Expressions;
}

internal sealed record ExtensionCallExpressionNode(string ExtensionName, string FunctionName, ArgumentListNode ArgumentList) : ExpressionNode
{
    public IReadOnlyList<ArgumentNode> Arguments => ArgumentList.Arguments;
}

internal sealed record TypeConstructorExpressionNode(string TypeName, ArgumentListNode ArgumentList) : ExpressionNode
{
    public IReadOnlyList<ArgumentNode> Arguments => ArgumentList.Arguments;
}

internal sealed record BooleanLiteralExpressionNode(bool Value) : ExpressionNode;
internal sealed record IntegerLiteralExpressionNode(long Value) : ExpressionNode;
internal sealed record FloatLiteralExpressionNode(double Value) : ExpressionNode;
internal sealed record PercentageLiteralExpressionNode(double PercentValue) : ExpressionNode;
internal sealed record UnitIntegerLiteralExpressionNode(long Value, string UnitName) : ExpressionNode;
internal sealed record UnitFloatLiteralExpressionNode(double Value, string UnitName) : ExpressionNode;
internal sealed record TextLiteralExpressionNode(string Value) : ExpressionNode;
internal sealed record ListLiteralExpressionNode(IReadOnlyList<ExpressionNode> Items) : ExpressionNode;
internal sealed record SetLiteralExpressionNode(IReadOnlyList<ExpressionNode> Items) : ExpressionNode;
internal sealed record DictionaryLiteralExpressionNode(IReadOnlyList<DictionaryEntryNode> Entries) : ExpressionNode;
internal sealed record SequenceLiteralExpressionNode(IReadOnlyList<ExpressionNode> Items) : ExpressionNode;
internal sealed record DictionaryEntryNode(string Key, ExpressionNode Value) : ScriptNode;
internal sealed record UnaryExpressionNode(string Operator, ExpressionNode Operand) : ExpressionNode;
internal sealed record VariadicTaggedExpressionNode(string Operator, IReadOnlyList<ExpressionNode> Arguments) : ExpressionNode;
internal sealed record ClampExpressionNode(ExpressionNode Value, ExpressionNode Minimum, ExpressionNode Maximum) : ExpressionNode;
internal sealed record RangeExpressionNode(ExpressionNode FromExpression, ExpressionNode ToExpression, ExpressionNode? StepExpression) : ExpressionNode;
internal sealed record RandomExpressionNode(ExpressionNode FromExpression, ExpressionNode ToExpression) : ExpressionNode;
internal sealed record SeededRandomExpressionNode(ExpressionNode SeedExpression, ExpressionNode BodyExpression) : ExpressionNode;
internal sealed record DiceExpressionNode(int DiceCount, int SideCount) : ExpressionNode;
internal sealed record GeneratedCollectionExpressionNode(string CollectionType, string Identifier, IterationSourceNode Source, ExpressionNode? Predicate, ExpressionNode Projection) : ExpressionNode;
internal sealed record GuardedChoiceExpressionNode(IReadOnlyList<GuardedChoiceBranchNode> Branches, ExpressionNode OtherwiseExpression) : ExpressionNode;
internal sealed record GuardedChoiceBranchNode(ExpressionNode ValueExpression, ExpressionNode ConditionExpression) : ScriptNode;
internal sealed record BinaryExpressionNode(ExpressionNode Left, string Operator, ExpressionNode Right) : ExpressionNode;
internal sealed record PredicateCallExpressionNode(ExpressionNode Value, string RuleName) : ExpressionNode;
internal sealed record ExtensionPredicateExpressionNode(ExpressionNode Value, string ExtensionName, string FunctionName) : ExpressionNode;
internal sealed record TypeCheckExpressionNode(ExpressionNode Value, string TypeName) : ExpressionNode;
internal sealed record TypeCastExpressionNode(ExpressionNode Value, string TypeName) : ExpressionNode;
internal sealed record DiceCountPatternNode(int Count, ExpressionNode? Face) : DicePatternNode;
internal sealed record MemberAccessExpressionNode(ExpressionNode Target, string Member) : ExpressionNode;
internal sealed record CollectionAccessExpressionNode(ExpressionNode Target, CollectionSelectorNode Selector) : ExpressionNode;

// Collection selector nodes

internal sealed record ExpressionSelectorNode(ExpressionNode Expression) : CollectionSelectorNode;
internal sealed record PatternSelectorNode(DicePatternNode Pattern) : CollectionSelectorNode;
internal sealed record ObjectMatchSelectorNode(ObjectMatchPatternNode Pattern) : CollectionSelectorNode;
internal sealed record TakePatternSelectorNode(DicePatternNode Pattern) : CollectionSelectorNode;
internal sealed record SequenceSliceSelectorNode(string Operation, string Scope, int Count) : CollectionSelectorNode;
internal sealed record SeriesTermSelectorNode(ExpressionNode IndexExpression) : CollectionSelectorNode;
internal sealed record PredicateSelectorNode(string Operator, string Identifier, ExpressionNode Predicate) : CollectionSelectorNode;
internal sealed record CountSelectorNode(string Identifier, ExpressionNode Predicate) : CollectionSelectorNode;
internal sealed record ChooseSelectorNode(int Count, bool AtRandom, string? Identifier, ExpressionNode? Predicate, string? WeightIdentifier, ExpressionNode? WeightExpression) : CollectionSelectorNode;
internal sealed record DrawSelectorNode(int Count) : CollectionSelectorNode;
internal sealed record ShuffleSelectorNode : CollectionSelectorNode;
internal sealed record ReverseSelectorNode : CollectionSelectorNode;
internal sealed record EdgeSelectorNode(string Mode, string? Identifier, ExpressionNode? Predicate) : CollectionSelectorNode;
internal sealed record FilterSelectorNode(string Identifier, ExpressionNode Predicate) : CollectionSelectorNode;
internal sealed record SumSelectorNode(string Identifier, ExpressionNode Projection) : CollectionSelectorNode;
internal sealed record AverageSelectorNode(string Identifier, ExpressionNode Projection) : CollectionSelectorNode;
internal sealed record SelectSelectorNode(string Identifier, ExpressionNode Projection) : CollectionSelectorNode;
internal sealed record DictionarySelectorNode(string Identifier, ExpressionNode KeyProjection, ExpressionNode? ValueProjection) : CollectionSelectorNode;
internal sealed record MinSelectorNode(string Identifier, ExpressionNode Projection) : CollectionSelectorNode;
internal sealed record MaxSelectorNode(string Identifier, ExpressionNode Projection) : CollectionSelectorNode;
internal sealed record ContainsSelectorNode(string Mode, ExpressionNode ValueExpression) : CollectionSelectorNode;
internal sealed record SortSelectorNode(string Direction) : CollectionSelectorNode;
internal sealed record DistinctSelectorNode(string? Identifier, ExpressionNode? Projection) : CollectionSelectorNode;
internal sealed record GroupBySelectorNode(string Identifier, ExpressionNode Projection) : CollectionSelectorNode;
internal sealed record OrderBySelectorNode(string Direction, string Identifier, ExpressionNode Projection) : CollectionSelectorNode;

// Dice Pattern nodes

internal sealed record DiceFullHousePatternNode : DicePatternNode;
internal sealed record DiceStraightPatternNode : DicePatternNode;

// Object matcher nodes

internal sealed record ObjectMatchPatternNode(IReadOnlyList<ObjectMatchEntryNode> Entries) : ScriptNode;
internal sealed record ObjectMatchEntryNode(string Key, ObjectMatchValueNode Value) : ScriptNode;
internal sealed record ObjectMatchExpressionValueNode(ExpressionNode Expression) : ObjectMatchValueNode;
internal sealed record ObjectMatchNestedValueNode(ObjectMatchPatternNode Pattern) : ObjectMatchValueNode;

// Iteration source nodes

internal sealed record CollectionIterationSourceNode(ExpressionNode Expression) : IterationSourceNode;
internal sealed record RangeIterationSourceNode(RangeExpressionNode RangeExpression) : IterationSourceNode;
