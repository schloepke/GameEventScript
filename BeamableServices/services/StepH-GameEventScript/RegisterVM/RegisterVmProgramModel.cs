#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using StepH.GameEventScript.Compiler;

namespace StepH.GameEventScript.RegisterVM;

internal enum RegisterVmProgramOpCode
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

internal enum RegisterVmCallableKind
{
    Rule,
    Select
}

internal enum RegisterVmCastKind
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

internal readonly record struct RegisterVmProgramInstruction(
    RegisterVmProgramOpCode OpCode,
    int A = -1,
    int B = -1,
    RegisterVmValue Constant = default,
    RegisterVmCastKind CastKind = default,
    RegisterVmCallableKind CallableKind = default,
    RegisterVmExpressionProgram? ExpressionProgram = null,
    RegisterVmPipelineProgram? PipelineProgram = null,
    RegisterVmGeneratedCollectionProgram? GeneratedCollectionProgram = null,
    RegisterVmGuardedChoiceProgram? GuardedChoiceProgram = null,
    string? DiagnosticName = null,
    string? DiagnosticArgumentName = null,
    string[]? Names = null,
    int[]? Slots = null);

internal sealed class RegisterVmExpressionProgram(
    RegisterVmProgramInstruction[] instructions,
    int maxStackDepth)
{
    public RegisterVmProgramInstruction[] Instructions { get; } = instructions ?? throw new ArgumentNullException(nameof(instructions));

    public int MaxStackDepth { get; } = maxStackDepth;
}

internal enum RegisterVmSelectorKind
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

internal sealed class RegisterVmSelectorProgram(
    RegisterVmSelectorKind kind,
    int identifierSlot,
    RegisterVmExpressionProgram? expressionProgram,
    string? edgeMode = null,
    RegisterVmExpressionProgram? secondaryExpressionProgram = null,
    string? secondaryMode = null,
    int count = 0,
    int secondaryIdentifierSlot = -1,
    bool flag = false,
    DicePatternNode? dicePattern = null,
    ObjectMatchPatternNode? objectPattern = null)
{
    public RegisterVmSelectorKind Kind { get; } = kind;

    public int IdentifierSlot { get; } = identifierSlot;

    public RegisterVmExpressionProgram? ExpressionProgram { get; } = expressionProgram;

    public string? EdgeMode { get; } = edgeMode;

    public RegisterVmExpressionProgram? SecondaryExpressionProgram { get; } = secondaryExpressionProgram;

    public string? SecondaryMode { get; } = secondaryMode;

    public int Count { get; } = count;

    public int SecondaryIdentifierSlot { get; } = secondaryIdentifierSlot;

    public bool Flag { get; } = flag;

    public DicePatternNode? DicePattern { get; } = dicePattern;

    public ObjectMatchPatternNode? ObjectPattern { get; } = objectPattern;
}

internal sealed class RegisterVmPipelineProgram(
    RegisterVmExpressionProgram sourceProgram,
    RegisterVmSelectorProgram[] prefixSelectors,
    RegisterVmSelectorProgram terminalSelector)
{
    public RegisterVmExpressionProgram SourceProgram { get; } = sourceProgram ?? throw new ArgumentNullException(nameof(sourceProgram));

    public RegisterVmSelectorProgram[] PrefixSelectors { get; } = prefixSelectors ?? throw new ArgumentNullException(nameof(prefixSelectors));

    public RegisterVmSelectorProgram TerminalSelector { get; } = terminalSelector ?? throw new ArgumentNullException(nameof(terminalSelector));
}

internal sealed class RegisterVmGeneratedCollectionProgram(
    string collectionType,
    int identifierSlot,
    IterationSourceNode source,
    RegisterVmExpressionProgram? predicateProgram,
    RegisterVmExpressionProgram projectionProgram)
{
    public string CollectionType { get; } = collectionType ?? throw new ArgumentNullException(nameof(collectionType));

    public int IdentifierSlot { get; } = identifierSlot;

    public IterationSourceNode Source { get; } = source ?? throw new ArgumentNullException(nameof(source));

    public RegisterVmExpressionProgram? PredicateProgram { get; } = predicateProgram;

    public RegisterVmExpressionProgram ProjectionProgram { get; } = projectionProgram ?? throw new ArgumentNullException(nameof(projectionProgram));
}

internal sealed class RegisterVmGuardedChoiceProgram(
    RegisterVmExpressionProgram[] valuePrograms,
    RegisterVmExpressionProgram[] conditionPrograms,
    RegisterVmExpressionProgram otherwiseProgram)
{
    public RegisterVmExpressionProgram[] ValuePrograms { get; } = valuePrograms ?? throw new ArgumentNullException(nameof(valuePrograms));

    public RegisterVmExpressionProgram[] ConditionPrograms { get; } = conditionPrograms ?? throw new ArgumentNullException(nameof(conditionPrograms));

    public RegisterVmExpressionProgram OtherwiseProgram { get; } = otherwiseProgram ?? throw new ArgumentNullException(nameof(otherwiseProgram));
}

internal sealed class RegisterVmPublishLayout(
    string messageName,
    string signatureId,
    string[] argumentNames,
    RegisterVmExpressionProgram[] argumentPrograms)
{
    public string MessageName { get; } = messageName ?? throw new ArgumentNullException(nameof(messageName));

    public string SignatureId { get; } = signatureId ?? throw new ArgumentNullException(nameof(signatureId));

    public string[] ArgumentNames { get; } = argumentNames ?? throw new ArgumentNullException(nameof(argumentNames));

    public RegisterVmExpressionProgram[] ArgumentPrograms { get; } = argumentPrograms ?? throw new ArgumentNullException(nameof(argumentPrograms));
}
