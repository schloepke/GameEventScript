#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using StepH.GameEventScript.Compiler;

namespace StepH.GameEventScript.BytecodeVM;

internal enum BytecodeVmProgramOpCode
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

internal enum BytecodeVmCallableKind
{
    Rule,
    Select
}

internal enum BytecodeVmCastKind
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

internal readonly record struct BytecodeVmProgramInstruction(
    BytecodeVmProgramOpCode OpCode,
    int A = -1,
    int B = -1,
    BytecodeVmValue Constant = default,
    BytecodeVmCastKind CastKind = default,
    BytecodeVmCallableKind CallableKind = default,
    BytecodeVmExpressionProgram? ExpressionProgram = null,
    BytecodeVmPipelineProgram? PipelineProgram = null,
    BytecodeVmGeneratedCollectionProgram? GeneratedCollectionProgram = null,
    BytecodeVmGuardedChoiceProgram? GuardedChoiceProgram = null,
    string? DiagnosticName = null,
    string? DiagnosticArgumentName = null,
    string[]? Names = null,
    int[]? Slots = null);

internal sealed class BytecodeVmExpressionProgram(
    BytecodeVmProgramInstruction[] instructions,
    int maxStackDepth)
{
    public BytecodeVmProgramInstruction[] Instructions { get; } = instructions ?? throw new ArgumentNullException(nameof(instructions));

    public int MaxStackDepth { get; } = maxStackDepth;
}

internal enum BytecodeVmSelectorKind
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

internal sealed class BytecodeVmSelectorProgram(
    BytecodeVmSelectorKind kind,
    int identifierSlot,
    BytecodeVmExpressionProgram? expressionProgram,
    string? edgeMode = null,
    BytecodeVmExpressionProgram? secondaryExpressionProgram = null,
    string? secondaryMode = null,
    int count = 0,
    int secondaryIdentifierSlot = -1,
    bool flag = false,
    DicePatternNode? dicePattern = null,
    ObjectMatchPatternNode? objectPattern = null)
{
    public BytecodeVmSelectorKind Kind { get; } = kind;

    public int IdentifierSlot { get; } = identifierSlot;

    public BytecodeVmExpressionProgram? ExpressionProgram { get; } = expressionProgram;

    public string? EdgeMode { get; } = edgeMode;

    public BytecodeVmExpressionProgram? SecondaryExpressionProgram { get; } = secondaryExpressionProgram;

    public string? SecondaryMode { get; } = secondaryMode;

    public int Count { get; } = count;

    public int SecondaryIdentifierSlot { get; } = secondaryIdentifierSlot;

    public bool Flag { get; } = flag;

    public DicePatternNode? DicePattern { get; } = dicePattern;

    public ObjectMatchPatternNode? ObjectPattern { get; } = objectPattern;
}

internal sealed class BytecodeVmPipelineProgram(
    BytecodeVmExpressionProgram sourceProgram,
    BytecodeVmSelectorProgram[] prefixSelectors,
    BytecodeVmSelectorProgram terminalSelector)
{
    public BytecodeVmExpressionProgram SourceProgram { get; } = sourceProgram ?? throw new ArgumentNullException(nameof(sourceProgram));

    public BytecodeVmSelectorProgram[] PrefixSelectors { get; } = prefixSelectors ?? throw new ArgumentNullException(nameof(prefixSelectors));

    public BytecodeVmSelectorProgram TerminalSelector { get; } = terminalSelector ?? throw new ArgumentNullException(nameof(terminalSelector));
}

internal sealed class BytecodeVmGeneratedCollectionProgram(
    string collectionType,
    int identifierSlot,
    IterationSourceNode source,
    BytecodeVmExpressionProgram? predicateProgram,
    BytecodeVmExpressionProgram projectionProgram)
{
    public string CollectionType { get; } = collectionType ?? throw new ArgumentNullException(nameof(collectionType));

    public int IdentifierSlot { get; } = identifierSlot;

    public IterationSourceNode Source { get; } = source ?? throw new ArgumentNullException(nameof(source));

    public BytecodeVmExpressionProgram? PredicateProgram { get; } = predicateProgram;

    public BytecodeVmExpressionProgram ProjectionProgram { get; } = projectionProgram ?? throw new ArgumentNullException(nameof(projectionProgram));
}

internal sealed class BytecodeVmGuardedChoiceProgram(
    BytecodeVmExpressionProgram[] valuePrograms,
    BytecodeVmExpressionProgram[] conditionPrograms,
    BytecodeVmExpressionProgram otherwiseProgram)
{
    public BytecodeVmExpressionProgram[] ValuePrograms { get; } = valuePrograms ?? throw new ArgumentNullException(nameof(valuePrograms));

    public BytecodeVmExpressionProgram[] ConditionPrograms { get; } = conditionPrograms ?? throw new ArgumentNullException(nameof(conditionPrograms));

    public BytecodeVmExpressionProgram OtherwiseProgram { get; } = otherwiseProgram ?? throw new ArgumentNullException(nameof(otherwiseProgram));
}

internal sealed class BytecodeVmPublishLayout(
    string messageName,
    string signatureId,
    string[] argumentNames,
    BytecodeVmExpressionProgram[] argumentPrograms)
{
    public string MessageName { get; } = messageName ?? throw new ArgumentNullException(nameof(messageName));

    public string SignatureId { get; } = signatureId ?? throw new ArgumentNullException(nameof(signatureId));

    public string[] ArgumentNames { get; } = argumentNames ?? throw new ArgumentNullException(nameof(argumentNames));

    public BytecodeVmExpressionProgram[] ArgumentPrograms { get; } = argumentPrograms ?? throw new ArgumentNullException(nameof(argumentPrograms));
}
