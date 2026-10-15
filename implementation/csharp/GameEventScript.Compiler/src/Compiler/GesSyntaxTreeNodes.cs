// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using GameEventScript.Api;

namespace GameEventScript.Compiler;

// Abstract nodes for the syntax tree

internal abstract record GesStatementNode : GesScriptNode;

internal abstract record GesExpressionNode : GesScriptNode;

internal abstract record GesCollectionSelectorNode : GesScriptNode
{
    // Single bindings use the selector identifier; only multiple bindings need storage.
    public IReadOnlyList<string> BindingNames { get; init; } = [];
}

internal abstract record GesObjectMatchValueNode : GesScriptNode;

internal abstract record GesDicePatternNode : GesScriptNode;

internal abstract record GesIterationSourceNode : GesScriptNode;

internal abstract record GesScriptNode
{
    // Parser construction assigns metadata before attaching a fresh node to its
    // parent. Subsequent compiler passes retain copy-on-rewrite semantics.
    public GesSourceLocation? SourceRange { get; set; }

    // Parser-only source depth, including parentheses removed from the semantic tree.
    public int SourceNesting { get; set; }
}

// Root node of the syntax tree

internal sealed record GesParsedScript(
    string ModuleName,
    string SourceName,
    IReadOnlyList<GesConstantDefinitionNode> ConstantDefinitions,
    IReadOnlyList<GesTypeDefinitionNode> TypeDefinitions,
    IReadOnlyList<GesPredicateDefinitionNode> PredicateDefinitions,
    IReadOnlyList<GesFunctionDefinitionNode> FunctionDefinitions,
    IReadOnlyList<GesEventHandlerNode> Handlers) : GesScriptNode;

// Type/Predicate/Function/Handler nodes

internal sealed record GesConstantDefinitionNode(string Name, GesExpressionNode Value) : GesScriptNode;

internal sealed record GesParameterNode(string? ExternalLabel, string LocalName, string? DeclaredType = null) : GesScriptNode
{
    public string SignatureLabel => ExternalLabel ?? GesMessageSignature.UnlabeledParameterName;
}

internal sealed record GesArgumentNode(string? Label, GesExpressionNode Expression) : GesScriptNode
{
    public string Name => Label ?? GesMessageSignature.UnlabeledParameterName;
}

internal sealed record GesArgumentListNode(IReadOnlyList<GesArgumentNode> Arguments) : GesScriptNode
{
    public static readonly GesArgumentListNode Empty = new([]);

    public int Count => Arguments.Count;

    public IReadOnlyList<GesExpressionNode> Expressions { get; } = GesSyntaxTreeNodeLists.ToExpressions(Arguments);
}

internal static class GesSyntaxTreeNodeLists
{
    public static IReadOnlyList<string> ToParameterNames(IReadOnlyList<GesParameterNode> parameters)
    {
        if (parameters.Count == 0)
        {
            return [];
        }

        var names = new string[parameters.Count];
        for (var index = 0; index < names.Length; index++)
        {
            names[index] = parameters[index].LocalName;
        }

        return names;
    }

    public static IReadOnlyList<string> ToSignatureLabels(IReadOnlyList<GesParameterNode> parameters)
    {
        if (parameters.Count == 0)
        {
            return [];
        }

        var labels = new string[parameters.Count];
        for (var index = 0; index < labels.Length; index++)
        {
            labels[index] = parameters[index].SignatureLabel;
        }

        return labels;
    }

    public static IReadOnlyList<GesExpressionNode> ToExpressions(IReadOnlyList<GesArgumentNode> arguments)
    {
        if (arguments.Count == 0)
        {
            return [];
        }

        var expressions = new GesExpressionNode[arguments.Count];
        for (var index = 0; index < expressions.Length; index++)
        {
            expressions[index] = arguments[index].Expression;
        }

        return expressions;
    }
}

internal enum GesEventHandlerDispatchKind
{
    ExactSignature,
    MessageName
}

internal sealed record GesEventHandlerNode(
    string Message,
    GesEventHandlerDispatchKind DispatchKind,
    IReadOnlyList<GesParameterNode> ParameterList,
    IReadOnlyList<GesStatementNode> Statements,
    IReadOnlyList<string>? RequiredTags = null,
    IReadOnlyList<string>? ExcludedTags = null) : GesScriptNode
{
    public IReadOnlyList<string> Parameters { get; } = GesSyntaxTreeNodeLists.ToParameterNames(ParameterList);

    public IReadOnlyList<string> SignatureLabels { get; } = GesSyntaxTreeNodeLists.ToSignatureLabels(ParameterList);

    public IReadOnlyList<string> MatchingTags { get; } = RequiredTags ?? [];

    public IReadOnlyList<string> WithoutTags { get; } = ExcludedTags ?? [];
}

internal sealed record GesTypeDefinitionNode(string Name, IReadOnlyList<GesTypeFieldDefinitionNode> Fields) : GesScriptNode;
internal sealed record GesTypeFieldDefinitionNode(string Name, string TypeName, GesExpressionNode? MinimumExpression, GesExpressionNode? MaximumExpression, GesExpressionNode? ComputedExpression, string? ConstructorLabel) : GesScriptNode
{
    public bool IsConstructorParameter => ConstructorLabel is not null;
}
internal sealed record GesPredicateDefinitionNode(string Name, IReadOnlyList<GesParameterNode> ParameterList, GesExpressionNode Expression) : GesScriptNode
{
    public IReadOnlyList<string> Parameters { get; } = GesSyntaxTreeNodeLists.ToParameterNames(ParameterList);
}

internal sealed record GesFunctionDefinitionNode(string Name, IReadOnlyList<GesParameterNode> ParameterList, GesExpressionNode Expression) : GesScriptNode
{
    public IReadOnlyList<string> Parameters { get; } = GesSyntaxTreeNodeLists.ToParameterNames(ParameterList);
}

// Statement nodes

internal enum GesPublishStatementKind
{
    Emit,
    Publish
}

internal sealed record GesSendExpressionNode(GesPublishStatementKind Kind, GesExpressionNode Message, IReadOnlyList<GesExpressionNode> Tags, GesExpressionNode? Delay) : GesExpressionNode;
internal sealed record GesPublishStatementNode(GesPublishStatementKind Kind, GesExpressionNode MessageExpression, IReadOnlyList<GesExpressionNode> TagExpressions) : GesStatementNode;
internal sealed record GesLetStatementNode(string Identifier, GesExpressionNode Expression) : GesStatementNode;
internal sealed record GesStatementBodyNode(bool IsBlock, IReadOnlyList<GesStatementNode> Statements) : GesScriptNode;
internal sealed record GesIfConditionNode(GesExpressionNode Expression, string? Binding = null) : GesScriptNode;
internal sealed record GesIfStatementNode(IReadOnlyList<GesIfConditionNode> Conditions, GesStatementBodyNode ThenBody, GesStatementBodyNode? ElseBody) : GesStatementNode;
internal sealed record GesForStatementNode(string Identifier, GesIterationSourceNode Source, GesStatementBodyNode Body) : GesStatementNode
{
    public IReadOnlyList<string> BindingNames { get; init; } = [];
}
internal sealed record GesSeededRandomStatementNode(GesExpressionNode SeedExpression, GesStatementBodyNode Body) : GesStatementNode;
internal sealed record GesExpressionStatementNode(GesExpressionNode Expression) : GesStatementNode;

// Expression nodes

internal enum GesUnaryOperator
{
    Parse,
    Negate,
    Not,
    HasValue,
    Empty,
    Chance,
    Keys,
    Values,
    Entries,
    Abs,
    NaturalLog,
    Exp,
    Floor,
    Ceil,
    Truncate,
    RoundHalfEven,
    RoundHalfUp,
    RoundHalfDown,
    DegreeToRadians,
    DegreeFromRadians,
    WrapDegree,
    Sin,
    Cos,
    Tan,
    Asin,
    Acos,
    Atan
}

internal enum GesIntrinsicFunction
{
    Atan2,
    Hypot,
    Distance,
    DistanceSquared,
    LengthSquared,
    Normalize,
    Dot,
    Cross,
    AngleBetween
}

internal enum GesBinaryOperator
{
    Or,
    Xor,
    And,
    Implies,
    Equal,
    NotEqual,
    Less,
    Greater,
    LessOrEqual,
    GreaterOrEqual,
    Add,
    Subtract,
    Multiply,
    Divide,
    IntegerDivide,
    Modulo,
    Remainder,
    Power,
    Default,
    Contains,
    ContainsValue,
    StartsWith,
    EndsWith,
    Union,
    Intersect,
    Zip
}

internal static class GesOperatorText
{
    public static string ToSourceText(this GesUnaryOperator op)
        => op switch
        {
            GesUnaryOperator.Parse => "parse",
            GesUnaryOperator.Negate => "-",
            GesUnaryOperator.Not => "!",
            GesUnaryOperator.HasValue => "has value",
            GesUnaryOperator.Empty => "empty",
            GesUnaryOperator.Chance => "chance",
            GesUnaryOperator.Keys => "keys",
            GesUnaryOperator.Values => "values",
            GesUnaryOperator.Entries => "entries",
            GesUnaryOperator.Abs => "abs",
            GesUnaryOperator.NaturalLog => "ln",
            GesUnaryOperator.Exp => "exp",
            GesUnaryOperator.Floor => "floor",
            GesUnaryOperator.Ceil => "ceil",
            GesUnaryOperator.Truncate => "truncate",
            GesUnaryOperator.RoundHalfEven => "round half even",
            GesUnaryOperator.RoundHalfUp => "round half up",
            GesUnaryOperator.RoundHalfDown => "round half down",
            GesUnaryOperator.DegreeToRadians => "rad",
            GesUnaryOperator.DegreeFromRadians => "deg",
            GesUnaryOperator.WrapDegree => "wrap degree",
            GesUnaryOperator.Sin => "sin",
            GesUnaryOperator.Cos => "cos",
            GesUnaryOperator.Tan => "tan",
            GesUnaryOperator.Asin => "asin",
            GesUnaryOperator.Acos => "acos",
            GesUnaryOperator.Atan => "atan",
            _ => op.ToString()
        };

    public static string ToSourceText(this GesIntrinsicFunction function)
        => function switch
        {
            GesIntrinsicFunction.Atan2 => "atan2",
            GesIntrinsicFunction.Hypot => "hypot",
            GesIntrinsicFunction.Distance => "distance",
            GesIntrinsicFunction.DistanceSquared => "distance squared",
            GesIntrinsicFunction.LengthSquared => "length squared",
            GesIntrinsicFunction.Normalize => "normalize",
            GesIntrinsicFunction.Dot => "dot",
            GesIntrinsicFunction.Cross => "cross",
            GesIntrinsicFunction.AngleBetween => "angle between",
            _ => function.ToString()
        };

    public static string ToSourceText(this GesBinaryOperator op)
        => op switch
        {
            GesBinaryOperator.Or => "or",
            GesBinaryOperator.Xor => "xor",
            GesBinaryOperator.And => "and",
            GesBinaryOperator.Implies => "->",
            GesBinaryOperator.Equal => "=",
            GesBinaryOperator.NotEqual => "<>",
            GesBinaryOperator.Less => "<",
            GesBinaryOperator.Greater => ">",
            GesBinaryOperator.LessOrEqual => "<=",
            GesBinaryOperator.GreaterOrEqual => ">=",
            GesBinaryOperator.Add => "+",
            GesBinaryOperator.Subtract => "-",
            GesBinaryOperator.Multiply => "*",
            GesBinaryOperator.Divide => "/",
            GesBinaryOperator.IntegerDivide => "div",
            GesBinaryOperator.Modulo => "mod",
            GesBinaryOperator.Remainder => "rem",
            GesBinaryOperator.Power => "^",
            GesBinaryOperator.Default => "default",
            GesBinaryOperator.Contains => "in",
            GesBinaryOperator.ContainsValue => "in values of",
            GesBinaryOperator.StartsWith => "starts with",
            GesBinaryOperator.EndsWith => "ends with",
            GesBinaryOperator.Union => "|",
            GesBinaryOperator.Intersect => "&",
            GesBinaryOperator.Zip => "zip",
            _ => op.ToString()
        };
}

internal sealed record GesIdentifierExpressionNode(string Name) : GesExpressionNode;
internal sealed record GesTagLiteralExpressionNode(string Name) : GesExpressionNode;
internal sealed record GesConstantReferenceExpressionNode(string Name) : GesExpressionNode;
internal sealed record GesHandlerLiteralExpressionNode(string Message, IReadOnlyList<GesParameterNode> ParameterList) : GesExpressionNode
{
    public IReadOnlyList<string> Parameters { get; } = GesSyntaxTreeNodeLists.ToParameterNames(ParameterList);

    public IReadOnlyList<string> SignatureLabels { get; } = GesSyntaxTreeNodeLists.ToSignatureLabels(ParameterList);
}

internal sealed record GesMessageLiteralExpressionNode(string Message, GesArgumentListNode ArgumentList) : GesExpressionNode
{
    public IReadOnlyList<GesArgumentNode> Arguments => ArgumentList.Arguments;
}

internal sealed record GesCallExpressionNode(string Name, GesArgumentListNode ArgumentList) : GesExpressionNode
{
    public IReadOnlyList<GesExpressionNode> Arguments => ArgumentList.Expressions;
}

internal sealed record GesExtensionCallExpressionNode(string ExtensionName, string FunctionName, GesArgumentListNode ArgumentList) : GesExpressionNode
{
    public IReadOnlyList<GesArgumentNode> Arguments => ArgumentList.Arguments;
}

internal sealed record GesTypeConstructorExpressionNode(string TypeName, GesArgumentListNode ArgumentList) : GesExpressionNode
{
    public IReadOnlyList<GesArgumentNode> Arguments => ArgumentList.Arguments;
}

internal sealed record GesBooleanLiteralExpressionNode(bool Value) : GesExpressionNode;
internal sealed record GesNothingLiteralExpressionNode : GesExpressionNode;
internal sealed record GesIntegerLiteralExpressionNode(long Value, bool HasDecimalPoint = false) : GesExpressionNode;
internal sealed record GesFloatLiteralExpressionNode(double Value) : GesExpressionNode;
internal sealed record GesPercentageLiteralExpressionNode(double RatioValue) : GesExpressionNode;
internal sealed record GesUnitIntegerLiteralExpressionNode(long Value, string UnitName, bool HasDecimalPoint = false) : GesExpressionNode;
internal sealed record GesUnitFloatLiteralExpressionNode(double Value, string UnitName) : GesExpressionNode;
internal sealed record GesTextLiteralExpressionNode(string Value) : GesExpressionNode;
internal sealed record GesListLiteralExpressionNode(IReadOnlyList<GesExpressionNode> Items) : GesExpressionNode;
internal sealed record GesMapLiteralExpressionNode(IReadOnlyList<GesMapEntryNode> Entries) : GesExpressionNode;
internal sealed record GesMapEntryNode(string Key, GesExpressionNode Value) : GesScriptNode;
internal sealed record GesUnaryExpressionNode(GesUnaryOperator Operator, GesExpressionNode Operand) : GesExpressionNode;
internal sealed record GesIntrinsicCallExpressionNode(GesIntrinsicFunction Function, IReadOnlyList<GesExpressionNode> Arguments) : GesExpressionNode;
internal sealed record GesVariadicTaggedExpressionNode(string Operator, IReadOnlyList<GesExpressionNode> Arguments) : GesExpressionNode;
internal sealed record GesBetweenExpressionNode(GesExpressionNode Value, GesExpressionNode Minimum, GesExpressionNode Maximum) : GesExpressionNode;

internal sealed record GesClampExpressionNode(GesExpressionNode Value, GesExpressionNode Minimum, GesExpressionNode Maximum) : GesExpressionNode;
internal sealed record GesRangeExpressionNode(GesExpressionNode FromExpression, GesExpressionNode ToExpression, GesExpressionNode? StepExpression) : GesExpressionNode;
internal sealed record GesRandomExpressionNode(GesExpressionNode FromExpression, GesExpressionNode ToExpression) : GesExpressionNode;
internal sealed record GesSeededRandomExpressionNode(GesExpressionNode SeedExpression, GesExpressionNode BodyExpression) : GesExpressionNode;
internal sealed record GesDiceExpressionNode(int DiceCount, int SideCount) : GesExpressionNode;
internal sealed record GesSeriesExpressionNode(GesBytecodeSeriesKind SeriesKind) : GesExpressionNode;
internal sealed record GesGeneratedCollectionExpressionNode(string CollectionType, string Identifier, GesIterationSourceNode Source, GesExpressionNode? Predicate, GesExpressionNode Projection) : GesExpressionNode;
internal sealed record GesGuardedChoiceExpressionNode(IReadOnlyList<GesGuardedChoiceBranchNode> Branches, GesExpressionNode OtherwiseExpression) : GesExpressionNode;
internal sealed record GesGuardedChoiceBranchNode(GesExpressionNode ValueExpression, GesExpressionNode ConditionExpression) : GesScriptNode;
internal sealed record GesBinaryExpressionNode(GesExpressionNode Left, GesBinaryOperator Operator, GesExpressionNode Right) : GesExpressionNode;
internal sealed record GesPredicateCallExpressionNode(GesExpressionNode? Value, string PredicateName) : GesExpressionNode;
internal sealed record GesExtensionPredicateExpressionNode(GesExpressionNode Value, string ExtensionName, string FunctionName) : GesExpressionNode;
internal sealed record GesTypeCheckExpressionNode(GesExpressionNode Value, string TypeName) : GesExpressionNode;
internal sealed record GesNothingCheckExpressionNode(GesExpressionNode Value) : GesExpressionNode;
internal sealed record GesTypeCastExpressionNode(GesExpressionNode Value, string TypeName) : GesExpressionNode;
internal sealed record GesDiceCountPatternNode(int Count, GesExpressionNode? Face) : GesDicePatternNode;
internal sealed record GesMemberAccessExpressionNode(GesExpressionNode Target, string Member) : GesExpressionNode;
internal sealed record GesCombinedCollectionExpressionNode(string Operation, IReadOnlyList<GesExpressionNode> Sources) : GesExpressionNode;
internal sealed record GesForeachSelectorNode(string Identifier, GesExpressionNode Expression) : GesCollectionSelectorNode;

internal sealed record GesCollectionAccessExpressionNode(GesExpressionNode Target, GesCollectionSelectorNode Selector) : GesExpressionNode;

// Collection selector nodes

internal sealed record GesExpressionSelectorNode(GesExpressionNode Expression) : GesCollectionSelectorNode;
internal sealed record GesPatternSelectorNode(GesDicePatternNode Pattern) : GesCollectionSelectorNode;
internal sealed record GesObjectMatchSelectorNode(GesObjectMatchPatternNode Pattern) : GesCollectionSelectorNode;
internal sealed record GesTakePatternSelectorNode(GesDicePatternNode Pattern) : GesCollectionSelectorNode;
internal sealed record GesSequenceSliceSelectorNode(string Operation, string Scope, int Count) : GesCollectionSelectorNode;
internal sealed record GesSeriesTermSelectorNode(GesExpressionNode IndexExpression) : GesCollectionSelectorNode;
internal sealed record GesPredicateSelectorNode(string Operator, string Identifier, GesExpressionNode Predicate) : GesCollectionSelectorNode;
internal sealed record GesCountSelectorNode(string Identifier, GesExpressionNode Predicate) : GesCollectionSelectorNode;
internal sealed record GesChooseSelectorNode(int Count, bool AtRandom, string? Identifier, GesExpressionNode? Predicate, string? WeightIdentifier, GesExpressionNode? WeightExpression) : GesCollectionSelectorNode
{
    public IReadOnlyList<string> WeightBindingNames { get; init; } = [];
}
internal sealed record GesDrawSelectorNode(int Count) : GesCollectionSelectorNode;
internal sealed record GesShuffleSelectorNode : GesCollectionSelectorNode;
internal sealed record GesReverseSelectorNode : GesCollectionSelectorNode;
internal sealed record GesEdgeSelectorNode(string Mode, string? Identifier, GesExpressionNode? Predicate) : GesCollectionSelectorNode;
internal sealed record GesFilterSelectorNode(string Identifier, GesExpressionNode Predicate) : GesCollectionSelectorNode;
internal sealed record GesSumSelectorNode(string Identifier, GesExpressionNode Projection) : GesCollectionSelectorNode;
internal sealed record GesAverageSelectorNode(string Identifier, GesExpressionNode Projection) : GesCollectionSelectorNode;
internal sealed record GesFoldSelectorNode(string Accumulator, string Identifier, GesExpressionNode? Seed, GesExpressionNode Projection) : GesCollectionSelectorNode;
internal sealed record GesSelectSelectorNode(string Identifier, GesExpressionNode Projection) : GesCollectionSelectorNode;
internal sealed record GesMapSelectorNode(string Identifier, GesExpressionNode KeyProjection, GesExpressionNode? ValueProjection) : GesCollectionSelectorNode;
internal sealed record GesMinSelectorNode(string Identifier, GesExpressionNode Projection) : GesCollectionSelectorNode;
internal sealed record GesMaxSelectorNode(string Identifier, GesExpressionNode Projection) : GesCollectionSelectorNode;
internal sealed record GesContainsSelectorNode(string Mode, GesExpressionNode ValueExpression) : GesCollectionSelectorNode;
internal sealed record GesSortSelectorNode(string Direction) : GesCollectionSelectorNode;
internal sealed record GesDistinctSelectorNode(string? Identifier, GesExpressionNode? Projection) : GesCollectionSelectorNode;
internal sealed record GesGroupBySelectorNode(string Identifier, GesExpressionNode Projection) : GesCollectionSelectorNode;
internal sealed record GesOrderBySelectorNode(string Direction, string Identifier, GesExpressionNode Projection) : GesCollectionSelectorNode;

// Dice Pattern nodes

internal sealed record GesDiceFullHousePatternNode : GesDicePatternNode;
internal sealed record GesDiceStraightPatternNode : GesDicePatternNode;

// Object matcher nodes

internal sealed record GesObjectMatchPatternNode(IReadOnlyList<GesObjectMatchEntryNode> Entries) : GesScriptNode;
internal sealed record GesObjectMatchEntryNode(string Key, GesObjectMatchValueNode Value) : GesScriptNode;
internal sealed record GesObjectMatchExpressionValueNode(GesExpressionNode Expression) : GesObjectMatchValueNode;
internal sealed record GesObjectMatchNestedValueNode(GesObjectMatchPatternNode Pattern) : GesObjectMatchValueNode;

// Iteration source nodes

internal sealed record GesCollectionIterationSourceNode(GesExpressionNode Expression) : GesIterationSourceNode;
internal sealed record GesRangeIterationSourceNode(GesRangeExpressionNode RangeExpression) : GesIterationSourceNode;
