#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using StepH.GameEventScript.BytecodeVM;

namespace StepH.GameEventScript.Api;

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

internal sealed class BytecodeVmExpressionProgram(BytecodeVmProgramInstruction[] instructions, int maxStackDepth)
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
    BytecodeVmDicePattern? dicePattern = null,
    BytecodeVmObjectMatchPattern? objectPattern = null)
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

    public BytecodeVmDicePattern? DicePattern { get; } = dicePattern;

    public BytecodeVmObjectMatchPattern? ObjectPattern { get; } = objectPattern;
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
    BytecodeVmIterationSourceProgram source,
    BytecodeVmExpressionProgram? predicateProgram,
    BytecodeVmExpressionProgram projectionProgram)
{
    public string CollectionType { get; } = collectionType ?? throw new ArgumentNullException(nameof(collectionType));

    public int IdentifierSlot { get; } = identifierSlot;

    public BytecodeVmIterationSourceProgram Source { get; } = source ?? throw new ArgumentNullException(nameof(source));

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

internal enum BytecodeVmIterationSourceKind
{
    Collection,
    Range
}

internal sealed class BytecodeVmIterationSourceProgram(
    BytecodeVmIterationSourceKind kind,
    BytecodeVmExpressionProgram? collectionProgram,
    BytecodeVmExpressionProgram? rangeFromProgram,
    BytecodeVmExpressionProgram? rangeToProgram,
    BytecodeVmExpressionProgram? rangeStepProgram)
{
    public BytecodeVmIterationSourceKind Kind { get; } = kind;

    public BytecodeVmExpressionProgram? CollectionProgram { get; } = collectionProgram;

    public BytecodeVmExpressionProgram? RangeFromProgram { get; } = rangeFromProgram;

    public BytecodeVmExpressionProgram? RangeToProgram { get; } = rangeToProgram;

    public BytecodeVmExpressionProgram? RangeStepProgram { get; } = rangeStepProgram;
}

internal abstract class BytecodeVmDicePattern;

internal sealed class BytecodeVmFullHousePattern : BytecodeVmDicePattern;

internal sealed class BytecodeVmStraightPattern : BytecodeVmDicePattern;

internal sealed class BytecodeVmDiceCountPattern(int count, BytecodeVmExpressionProgram? faceProgram) : BytecodeVmDicePattern
{
    public int Count { get; } = count;

    public BytecodeVmExpressionProgram? FaceProgram { get; } = faceProgram;
}

internal sealed class BytecodeVmObjectMatchPattern(BytecodeVmObjectMatchEntry[] entries)
{
    public BytecodeVmObjectMatchEntry[] Entries { get; } = entries ?? throw new ArgumentNullException(nameof(entries));
}

internal sealed class BytecodeVmObjectMatchEntry(string key, BytecodeVmObjectMatchValue value)
{
    public string Key { get; } = key ?? throw new ArgumentNullException(nameof(key));

    public BytecodeVmObjectMatchValue Value { get; } = value ?? throw new ArgumentNullException(nameof(value));
}

internal abstract class BytecodeVmObjectMatchValue;

internal sealed class BytecodeVmObjectMatchExpressionValue(BytecodeVmExpressionProgram expressionProgram) : BytecodeVmObjectMatchValue
{
    public BytecodeVmExpressionProgram ExpressionProgram { get; } = expressionProgram ?? throw new ArgumentNullException(nameof(expressionProgram));
}

internal sealed class BytecodeVmObjectMatchNestedValue(BytecodeVmObjectMatchPattern pattern) : BytecodeVmObjectMatchValue
{
    public BytecodeVmObjectMatchPattern Pattern { get; } = pattern ?? throw new ArgumentNullException(nameof(pattern));
}

internal enum BytecodeVmStatementKind
{
    Let,
    Publish,
    If,
    ForRange,
    ForCollection,
    Expression,
    SeededRandom
}

internal sealed class BytecodeVmStatementProgram(BytecodeVmStatement[] statements, bool createsScope)
{
    public BytecodeVmStatement[] Statements { get; } = statements ?? throw new ArgumentNullException(nameof(statements));

    public bool CreatesScope { get; } = createsScope;
}

internal sealed class BytecodeVmStatement(
    BytecodeVmStatementKind kind,
    string? name = null,
    string? declaredType = null,
    BytecodeVmExpressionProgram? expressionProgram = null,
    BytecodeVmPublishLayout? publishLayout = null,
    BytecodeVmStatementProgram? thenProgram = null,
    BytecodeVmStatementProgram? elseProgram = null,
    BytecodeVmStatementProgram? bodyProgram = null,
    BytecodeVmIterationSourceProgram? iterationSource = null,
    string? diagnosticName = null)
{
    public BytecodeVmStatementKind Kind { get; } = kind;

    public string? Name { get; } = name;

    public string? DeclaredType { get; } = declaredType;

    public BytecodeVmExpressionProgram? ExpressionProgram { get; } = expressionProgram;

    public BytecodeVmPublishLayout? PublishLayout { get; } = publishLayout;

    public BytecodeVmStatementProgram? ThenProgram { get; } = thenProgram;

    public BytecodeVmStatementProgram? ElseProgram { get; } = elseProgram;

    public BytecodeVmStatementProgram? BodyProgram { get; } = bodyProgram;

    public BytecodeVmIterationSourceProgram? IterationSource { get; } = iterationSource;

    public string? DiagnosticName { get; } = diagnosticName;
}

internal sealed class BytecodeVmTypeDefinition(
    string name,
    BytecodeVmTypeFieldDefinition[] fields)
{
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    public IReadOnlyList<BytecodeVmTypeFieldDefinition> Fields { get; } = fields ?? throw new ArgumentNullException(nameof(fields));
}

internal sealed class BytecodeVmTypeFieldDefinition(
    string name,
    string typeName,
    BytecodeVmExpressionProgram? minimumProgram,
    BytecodeVmExpressionProgram? maximumProgram,
    BytecodeVmExpressionProgram? computedProgram)
{
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    public string TypeName { get; } = typeName ?? throw new ArgumentNullException(nameof(typeName));

    public BytecodeVmExpressionProgram? MinimumProgram { get; } = minimumProgram;

    public BytecodeVmExpressionProgram? MaximumProgram { get; } = maximumProgram;

    public BytecodeVmExpressionProgram? ComputedProgram { get; } = computedProgram;
}

internal sealed class BytecodeVmExecutionPlan
{
    private readonly IReadOnlyDictionary<string, int> _slots;

    private BytecodeVmExecutionPlan(
        IReadOnlyDictionary<string, int> slots,
        BytecodeVmStatementProgram statementProgram,
        int maxStackDepth)
    {
        _slots = slots;
        StatementProgram = statementProgram ?? throw new ArgumentNullException(nameof(statementProgram));
        SlotCount = slots.Count;
        MaxStackDepth = maxStackDepth;
    }

    public BytecodeVmStatementProgram StatementProgram { get; }

    public int SlotCount { get; }

    public int MaxStackDepth { get; }

    public static BytecodeVmExecutionPlan Create(
        IReadOnlyDictionary<string, int> slots,
        BytecodeVmStatementProgram statementProgram,
        int maxStackDepth)
        => new(new Dictionary<string, int>(slots, StringComparer.Ordinal), statementProgram, maxStackDepth);

    public bool TryGetSlot(string name, out int slot)
        => _slots.TryGetValue(name, out slot);
}
