#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using StepH.GameEventScript.Parser;

namespace StepH.GameEventScript.RegisterVM;

internal enum RegisterFastOpCode
{
    LoadConstant,
    LoadSlot,
    Or,
    Xor,
    And,
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
    Default,
    Contains,
    ContainsValue,
    StartsWith,
    EndsWith,
    Intersect,
    Combine,
    Except,
    Zip,
    Unary,
    Variadic,
    Clamp,
    Random,
    Range,
    Dice,
    SeededRandom,
    Cast,
    TypeConstructor,
    RulePredicate,
    MemberAccess,
    IndexedAccess,
    BuildList,
    BuildSequence,
    BuildSet,
    BuildDictionary,
    BuildMessage,
    BindHandler,
    CallExtension,
    Call,
    TypeCheck,
    Pipeline,
    GeneratedCollection,
    GuardedChoice
}

internal enum RegisterFastCallableKind
{
    Rule,
    Select
}

internal enum RegisterFastCastKind
{
    Boolean,
    Integer,
    Decimal,
    Number,
    Percentage,
    Degree,
    Meter,
    Second,
    Sequence
}

internal readonly record struct RegisterFastInstruction(
    RegisterFastOpCode OpCode,
    int A = -1,
    int B = -1,
    RegisterFastValue Constant = default,
    RegisterFastCastKind CastKind = default,
    RegisterFastCallableKind CallableKind = default,
    RegisterFastExpressionProgram? ExpressionProgram = null,
    RegisterFastPipelineProgram? PipelineProgram = null,
    RegisterFastGeneratedCollectionProgram? GeneratedCollectionProgram = null,
    RegisterFastGuardedChoiceProgram? GuardedChoiceProgram = null,
    string? DiagnosticName = null,
    string? DiagnosticArgumentName = null,
    string[]? Names = null,
    int[]? Slots = null);

internal sealed class RegisterFastExpressionProgram(
    RegisterFastInstruction[] instructions,
    int maxStackDepth)
{
    public RegisterFastInstruction[] Instructions { get; } = instructions ?? throw new ArgumentNullException(nameof(instructions));

    public int MaxStackDepth { get; } = maxStackDepth;
}

internal enum RegisterFastSelectorKind
{
    Filter,
    Select,
    Predicate,
    Sum,
    Average,
    Count,
    Edge,
    Min,
    Max,
    Dictionary,
    Contains,
    Sort,
    Distinct,
    GroupBy,
    OrderBy,
    Reverse,
    SequenceSlice,
    Pattern,
    ObjectMatch,
    TakePattern,
    Choose,
    Draw,
    Shuffle
}

internal sealed class RegisterFastSelectorProgram(
    RegisterFastSelectorKind kind,
    int identifierSlot,
    RegisterFastExpressionProgram? expressionProgram,
    string? edgeMode = null,
    RegisterFastExpressionProgram? secondaryExpressionProgram = null,
    string? secondaryMode = null,
    int count = 0,
    int secondaryIdentifierSlot = -1,
    bool flag = false,
    DicePatternNode? dicePattern = null,
    ObjectMatchPatternNode? objectPattern = null)
{
    public RegisterFastSelectorKind Kind { get; } = kind;

    public int IdentifierSlot { get; } = identifierSlot;

    public RegisterFastExpressionProgram? ExpressionProgram { get; } = expressionProgram;

    public string? EdgeMode { get; } = edgeMode;

    public RegisterFastExpressionProgram? SecondaryExpressionProgram { get; } = secondaryExpressionProgram;

    public string? SecondaryMode { get; } = secondaryMode;

    public int Count { get; } = count;

    public int SecondaryIdentifierSlot { get; } = secondaryIdentifierSlot;

    public bool Flag { get; } = flag;

    public DicePatternNode? DicePattern { get; } = dicePattern;

    public ObjectMatchPatternNode? ObjectPattern { get; } = objectPattern;
}

internal sealed class RegisterFastPipelineProgram(
    RegisterFastExpressionProgram sourceProgram,
    RegisterFastSelectorProgram[] prefixSelectors,
    RegisterFastSelectorProgram terminalSelector)
{
    public RegisterFastExpressionProgram SourceProgram { get; } = sourceProgram ?? throw new ArgumentNullException(nameof(sourceProgram));

    public RegisterFastSelectorProgram[] PrefixSelectors { get; } = prefixSelectors ?? throw new ArgumentNullException(nameof(prefixSelectors));

    public RegisterFastSelectorProgram TerminalSelector { get; } = terminalSelector ?? throw new ArgumentNullException(nameof(terminalSelector));
}

internal sealed class RegisterFastGeneratedCollectionProgram(
    string collectionType,
    int identifierSlot,
    IterationSourceNode source,
    RegisterFastExpressionProgram? predicateProgram,
    RegisterFastExpressionProgram projectionProgram)
{
    public string CollectionType { get; } = collectionType ?? throw new ArgumentNullException(nameof(collectionType));

    public int IdentifierSlot { get; } = identifierSlot;

    public IterationSourceNode Source { get; } = source ?? throw new ArgumentNullException(nameof(source));

    public RegisterFastExpressionProgram? PredicateProgram { get; } = predicateProgram;

    public RegisterFastExpressionProgram ProjectionProgram { get; } = projectionProgram ?? throw new ArgumentNullException(nameof(projectionProgram));
}

internal sealed class RegisterFastGuardedChoiceProgram(
    RegisterFastExpressionProgram[] valuePrograms,
    RegisterFastExpressionProgram[] conditionPrograms,
    RegisterFastExpressionProgram otherwiseProgram)
{
    public RegisterFastExpressionProgram[] ValuePrograms { get; } = valuePrograms ?? throw new ArgumentNullException(nameof(valuePrograms));

    public RegisterFastExpressionProgram[] ConditionPrograms { get; } = conditionPrograms ?? throw new ArgumentNullException(nameof(conditionPrograms));

    public RegisterFastExpressionProgram OtherwiseProgram { get; } = otherwiseProgram ?? throw new ArgumentNullException(nameof(otherwiseProgram));
}

internal sealed class RegisterFastPublishLayout(
    string messageName,
    string signatureId,
    string[] argumentNames,
    RegisterFastExpressionProgram[] argumentPrograms)
{
    public string MessageName { get; } = messageName ?? throw new ArgumentNullException(nameof(messageName));

    public string SignatureId { get; } = signatureId ?? throw new ArgumentNullException(nameof(signatureId));

    public string[] ArgumentNames { get; } = argumentNames ?? throw new ArgumentNullException(nameof(argumentNames));

    public RegisterFastExpressionProgram[] ArgumentPrograms { get; } = argumentPrograms ?? throw new ArgumentNullException(nameof(argumentPrograms));
}
