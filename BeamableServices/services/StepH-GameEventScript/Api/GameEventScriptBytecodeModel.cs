#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace StepH.GameEventScript.Api;

public enum GameEventScriptBytecodeOpCode : byte
{
    LoadNothing,
    LoadTrue,
    LoadFalse,
    LoadInteger,
    LoadFloat,
    LoadText,
    LoadTag,
    LoadHandler,
    MoveSlot,
    Or,
    Xor,
    And,
    Equal,
    NotEqual,
    ApproxEqual,
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
    PrimitiveIntegerEqual,
    PrimitiveIntegerNotEqual,
    PrimitiveIntegerLess,
    PrimitiveIntegerGreater,
    PrimitiveIntegerLessOrEqual,
    PrimitiveIntegerGreaterOrEqual,
    PrimitiveIntegerAdd,
    PrimitiveIntegerSubtract,
    PrimitiveIntegerMultiply,
    PrimitiveIntegerDivide,
    PrimitiveIntegerFloorDivide,
    PrimitiveIntegerModulo,
    PrimitiveIntegerRemainder,
    Default,
    Contains,
    ContainsValue,
    StartsWith,
    EndsWith,
    Intersect,
    Combine,
    Except,
    Zip,
    UnaryNegate,
    UnaryNot,
    UnaryHasValue,
    UnaryEmpty,
    UnaryLength,
    UnaryChance,
    UnaryKeys,
    UnaryValues,
    UnaryEntries,
    UnaryAbs,
    UnaryNaturalLog,
    Variadic,
    Clamp,
    Random,
    Range,
    RangeWithStep,
    Dice,
    RandomPush,
    RandomPushConstant,
    RandomPop,
    CastNothing,
    CastBoolean,
    CastInteger,
    CastFloat,
    CastNumber,
    CastPercentage,
    CastDegree,
    CastMeter,
    CastSecond,
    CastVector,
    CastPoint,
    CastUuid,
    CastSequence,
    CastSeries,
    CastEnvelope,
    CastRef,
    CastTag,
    CastText,
    CastList,
    CastRange,
    CastMessage,
    CastHandler,
    CastDictionary,
    CastSet,
    CastDice,
    CastOptional,
    CastCustom,
    TypeConstructor,
    PredicateTest,
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
    TypeCheckNothing,
    TypeCheckTag,
    TypeCheckText,
    TypeCheckPercentage,
    TypeCheckDegree,
    TypeCheckMeter,
    TypeCheckSecond,
    TypeCheckVector,
    TypeCheckPoint,
    TypeCheckFloat,
    TypeCheckInteger,
    TypeCheckBoolean,
    TypeCheckUuid,
    TypeCheckOptional,
    TypeCheckSequence,
    TypeCheckSeries,
    TypeCheckEnvelope,
    TypeCheckList,
    TypeCheckRange,
    TypeCheckMessage,
    TypeCheckHandler,
    TypeCheckRef,
    TypeCheckDictionary,
    TypeCheckSet,
    TypeCheckDice,
    TypeCheckCustom,
    Pipeline,
    GeneratedCollection,
    GuardedChoice,
    Power,
    ShortCircuitOr,
    ShortCircuitAnd,
    ShortCircuitImplies,
    Nop,
    BindParameter,
    Jump,
    JumpIfTrue,
    JumpIfFalse,
    JumpIfNotTrue,
    EnterScope,
    ExitScope,
    ReturnNothing,
    Return,
    EmitMessage,
    EmitMessageWithTags,
    PublishMessage,
    PublishMessageWithTags,
    EmitMessageValue,
    EmitMessageValueWithTags,
    PublishMessageValue,
    PublishMessageValueWithTags,
    ForRange,
    ForCollection
}

public enum GameEventScriptBytecodeCallableKind
{
    Predicate,
    Function
}

public enum GameEventScriptBytecodeInstructionUnit : byte
{
    None = 0,
    Degree = 1,
    Meter = 2,
    Second = 3,
    Percentage = 4
}

[StructLayout(LayoutKind.Explicit, Size = 12)]
public struct GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode opCode, ushort dest = 0, ushort a = 0, ushort b = 0, ushort c = 0, ushort d = 0, byte unitAndFlags = 0)
{
    [FieldOffset(0)]
    public GameEventScriptBytecodeOpCode OpCode = opCode;

    [FieldOffset(1)]
    public readonly byte UnitAndFlags = unitAndFlags;

    [FieldOffset(2)]
    public readonly ushort Dest_U16 = dest;

    [FieldOffset(4)]
    public ushort A_U16 = a;

    [FieldOffset(6)]
    public ushort B_U16 = b;

    [FieldOffset(8)]
    public ushort C_U16 = c;

    [FieldOffset(10)]
    public ushort D_U16 = d;

    [FieldOffset(4)]
    public short A_I16;

    [FieldOffset(6)]
    public short B_I16;

    [FieldOffset(8)]
    public short C_I16;

    [FieldOffset(10)]
    public short D_I16;

    [FieldOffset(4)]
    public int A_I32;

    [FieldOffset(8)]
    public int B_I32;

    [FieldOffset(4)]
    public uint A_U32;

    [FieldOffset(8)]
    public uint B_U32;

    [FieldOffset(4)]
    public long I64;

    [FieldOffset(4)]
    public ulong U64;

    [FieldOffset(4)]
    public double F64;
}


public sealed class GameEventScriptBytecodeOperationLayout
{
    public GameEventScriptBytecodeOperationLayout(
        GameEventScriptBytecodeOpCode opCode,
        string? name = null,
        string? argumentName = null,
        IReadOnlyList<string>? names = null,
        IReadOnlyList<int>? argumentSlots = null,
        IReadOnlyList<int>? parameterSlots = null,
        IReadOnlyList<string?>? declaredTypes = null,
        GameEventScriptBytecodeCallableKind callableKind = GameEventScriptBytecodeCallableKind.Function,
        int externalReferenceIndex = -1,
        int nameListIndex = -1,
        int expressionEntryAddress = -1,
        int secondaryExpressionEntryAddress = -1,
        int count = 0,
        bool flag = false)
    {
        OpCode = opCode;
        Name = name;
        ArgumentName = argumentName;
        Names = names?.ToArray() ?? [];
        ArgumentSlots = argumentSlots?.ToArray() ?? [];
        ParameterSlots = parameterSlots?.ToArray() ?? [];
        DeclaredTypes = declaredTypes?.ToArray() ?? [];
        CallableKind = callableKind;
        ExternalReferenceIndex = externalReferenceIndex;
        NameListIndex = nameListIndex;
        ExpressionEntryAddress = expressionEntryAddress;
        SecondaryExpressionEntryAddress = secondaryExpressionEntryAddress;
        Count = count;
        Flag = flag;
    }

    public GameEventScriptBytecodeOpCode OpCode { get; }

    public string? Name { get; }

    public string? ArgumentName { get; }

    public IReadOnlyList<string> Names { get; }

    public IReadOnlyList<int> ArgumentSlots { get; }

    public IReadOnlyList<int> ParameterSlots { get; }

    public IReadOnlyList<string?> DeclaredTypes { get; }

    public GameEventScriptBytecodeCallableKind CallableKind { get; }

    public int ExternalReferenceIndex { get; }

    public int NameListIndex { get; }

    public int ExpressionEntryAddress { get; }

    public int SecondaryExpressionEntryAddress { get; }

    public int Count { get; }

    public bool Flag { get; }
}

public enum GameEventScriptBytecodeDiagnosticKind
{
    LetEvaluated,
    ExpressionEvaluatedToNothing
}

public enum GameEventScriptBytecodeDiagnosticTiming
{
    BeforeInstruction,
    AfterInstruction
}

public sealed class GameEventScriptBytecodeDiagnosticLayout
{
    public GameEventScriptBytecodeDiagnosticLayout(
        GameEventScriptBytecodeDiagnosticKind kind,
        GameEventScriptBytecodeDiagnosticTiming timing,
        int address,
        int slot,
        string name)
    {
        Kind = kind;
        Timing = timing;
        Address = address;
        Slot = slot;
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public GameEventScriptBytecodeDiagnosticKind Kind { get; }

    public GameEventScriptBytecodeDiagnosticTiming Timing { get; }

    public int Address { get; }

    public int Slot { get; }

    public string Name { get; }
}

public sealed class GameEventScriptBytecodeIterationSourceLayout
{
    public GameEventScriptBytecodeIterationSourceLayout(
        GameEventScriptBytecodeIterationSourceKind kind,
        int collectionSlot = -1,
        int rangeFromSlot = -1,
        int rangeToSlot = -1,
        int rangeStepSlot = -1)
    {
        Kind = kind;
        CollectionSlot = collectionSlot;
        RangeFromSlot = rangeFromSlot;
        RangeToSlot = rangeToSlot;
        RangeStepSlot = rangeStepSlot;
    }

    public GameEventScriptBytecodeIterationSourceKind Kind { get; }

    public int CollectionSlot { get; }

    public int RangeFromSlot { get; }

    public int RangeToSlot { get; }

    public int RangeStepSlot { get; }
}

public sealed class GameEventScriptBytecodeLoopLayout
{
    public GameEventScriptBytecodeLoopLayout(int identifierSlot, int iterationSourceLayoutIndex)
    {
        IdentifierSlot = identifierSlot;
        IterationSourceLayoutIndex = iterationSourceLayoutIndex;
    }

    public int IdentifierSlot { get; }

    public int IterationSourceLayoutIndex { get; }
}

public enum GameEventScriptBytecodePipelinePatternKind
{
    Count,
    FullHouse,
    Straight
}

public sealed class GameEventScriptBytecodePipelinePattern
{
    public GameEventScriptBytecodePipelinePattern(
        GameEventScriptBytecodePipelinePatternKind kind,
        int count = 0,
        int faceEntryAddress = -1)
    {
        Kind = kind;
        Count = count;
        FaceEntryAddress = faceEntryAddress;
    }

    public GameEventScriptBytecodePipelinePatternKind Kind { get; }

    public int Count { get; }

    public int FaceEntryAddress { get; }
}

public enum GameEventScriptBytecodePipelineObjectPatternValueKind
{
    Expression,
    Nested
}

public sealed class GameEventScriptBytecodePipelineObjectPatternEntry
{
    public GameEventScriptBytecodePipelineObjectPatternEntry(
        string key,
        GameEventScriptBytecodePipelineObjectPatternValueKind valueKind,
        int expressionEntryAddress = -1,
        int nestedPatternIndex = -1)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        ValueKind = valueKind;
        ExpressionEntryAddress = expressionEntryAddress;
        NestedPatternIndex = nestedPatternIndex;
    }

    public string Key { get; }

    public GameEventScriptBytecodePipelineObjectPatternValueKind ValueKind { get; }

    public int ExpressionEntryAddress { get; }

    public int NestedPatternIndex { get; }
}

public sealed class GameEventScriptBytecodePipelineObjectPattern
{
    public GameEventScriptBytecodePipelineObjectPattern(IReadOnlyList<GameEventScriptBytecodePipelineObjectPatternEntry>? entries)
    {
        Entries = entries?.ToArray() ?? [];
    }

    public IReadOnlyList<GameEventScriptBytecodePipelineObjectPatternEntry> Entries { get; }
}

public enum GameEventScriptBytecodePipelineSelectorKind
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
    SeriesTerm,
    Pattern,
    ObjectMatch,
    TakePattern,
    Choose,
    Draw,
    Shuffle
}

public sealed class GameEventScriptBytecodePipelineSelector
{
    public GameEventScriptBytecodePipelineSelector(
        GameEventScriptBytecodePipelineSelectorKind kind,
        int identifierSlot = -1,
        string? edgeMode = null,
        string? secondaryMode = null,
        int count = 0,
        int secondaryIdentifierSlot = -1,
        bool flag = false,
        int expressionEntryAddress = -1,
        int secondaryExpressionEntryAddress = -1,
        int pipelinePatternIndex = -1,
        int objectPatternIndex = -1)
    {
        Kind = kind;
        IdentifierSlot = identifierSlot;
        EdgeMode = edgeMode;
        SecondaryMode = secondaryMode;
        Count = count;
        SecondaryIdentifierSlot = secondaryIdentifierSlot;
        Flag = flag;
        ExpressionEntryAddress = expressionEntryAddress;
        SecondaryExpressionEntryAddress = secondaryExpressionEntryAddress;
        PipelinePatternIndex = pipelinePatternIndex;
        ObjectPatternIndex = objectPatternIndex;
    }

    public GameEventScriptBytecodePipelineSelectorKind Kind { get; }

    public int IdentifierSlot { get; }

    public string? EdgeMode { get; }

    public string? SecondaryMode { get; }

    public int Count { get; }

    public int SecondaryIdentifierSlot { get; }

    public bool Flag { get; }

    public int ExpressionEntryAddress { get; }

    public int SecondaryExpressionEntryAddress { get; }

    public int PipelinePatternIndex { get; }

    public int ObjectPatternIndex { get; }
}

public sealed class GameEventScriptBytecodePipeline
{
    public GameEventScriptBytecodePipeline(
        int sourceSlot,
        IReadOnlyList<int>? prefixSelectorIndexes,
        int terminalSelectorIndex)
    {
        SourceSlot = sourceSlot;
        PrefixSelectorIndexes = prefixSelectorIndexes?.ToArray() ?? [];
        TerminalSelectorIndex = terminalSelectorIndex;
    }

    public int SourceSlot { get; }

    public IReadOnlyList<int> PrefixSelectorIndexes { get; }

    public int TerminalSelectorIndex { get; }
}

public sealed class GameEventScriptBytecodeGeneratedCollectionLayout
{
    public GameEventScriptBytecodeGeneratedCollectionLayout(
        string collectionType,
        int identifierSlot,
        int iterationSourceLayoutIndex,
        int predicateEntryAddress = -1,
        int projectionEntryAddress = -1)
    {
        CollectionType = collectionType ?? throw new ArgumentNullException(nameof(collectionType));
        IdentifierSlot = identifierSlot;
        IterationSourceLayoutIndex = iterationSourceLayoutIndex;
        PredicateEntryAddress = predicateEntryAddress;
        ProjectionEntryAddress = projectionEntryAddress;
    }

    public string CollectionType { get; }

    public int IdentifierSlot { get; }

    public int IterationSourceLayoutIndex { get; }

    public int PredicateEntryAddress { get; }

    public int ProjectionEntryAddress { get; }
}

public sealed class GameEventScriptBytecodeGuardedChoiceLayout
{
    public GameEventScriptBytecodeGuardedChoiceLayout(
        IReadOnlyList<int>? valueEntryAddresses,
        IReadOnlyList<int>? conditionEntryAddresses,
        int otherwiseEntryAddress = -1)
    {
        ValueEntryAddresses = valueEntryAddresses?.ToArray() ?? [];
        ConditionEntryAddresses = conditionEntryAddresses?.ToArray() ?? [];
        OtherwiseEntryAddress = otherwiseEntryAddress;
    }

    public IReadOnlyList<int> ValueEntryAddresses { get; }

    public IReadOnlyList<int> ConditionEntryAddresses { get; }

    public int OtherwiseEntryAddress { get; }
}

internal sealed record class GameEventScriptBytecodeStackInstruction(
    GameEventScriptBytecodeOpCode OpCode,
    int A = -1,
    int B = -1,
    int ConstantIndex = -1,
    GameEventScriptBytecodeCallableKind CallableKind = GameEventScriptBytecodeCallableKind.Function,
    string? DiagnosticName = null,
    string? DiagnosticArgumentName = null,
    string[]? Names = null,
    int[]? Slots = null,
    string?[]? DeclaredTypes = null);

public enum GameEventScriptBytecodeIterationSourceKind
{
    Collection,
    Range
}

public enum GameEventScriptBytecodePublishKind
{
    Emit,
    Publish
}

public sealed class GameEventScriptBytecodeTypeDefinition(
    string name,
    GameEventScriptBytecodeTypeFieldDefinition[] fields)
{
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    public IReadOnlyList<GameEventScriptBytecodeTypeFieldDefinition> Fields { get; } = fields ?? throw new ArgumentNullException(nameof(fields));
}

public sealed class GameEventScriptBytecodeTypeFieldDefinition
{
    internal GameEventScriptBytecodeTypeFieldDefinition(
        string name,
        string typeName)
        : this(name, typeName, -1, -1, -1)
    {
    }

    internal GameEventScriptBytecodeTypeFieldDefinition(
        string name,
        string typeName,
        int minimumEntryAddress,
        int maximumEntryAddress,
        int computedEntryAddress)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
        MinimumEntryAddress = minimumEntryAddress;
        MaximumEntryAddress = maximumEntryAddress;
        ComputedEntryAddress = computedEntryAddress;
    }

    public string Name { get; }

    public string TypeName { get; }

    public int MinimumEntryAddress { get; internal set; }

    public int MaximumEntryAddress { get; internal set; }

    public int ComputedEntryAddress { get; internal set; }
}

public enum GameEventScriptBytecodeHandlerDispatchKind
{
    ExactSignature,
    MessageEnvelope
}

public sealed class GameEventScriptBytecodeHandler
{
    internal GameEventScriptBytecodeHandler(
        string message,
        GameEventScriptBytecodeHandlerDispatchKind dispatchKind,
        IReadOnlyList<string> parameters,
        IReadOnlyList<string> signatureLabels,
        int declarationOrder,
        IReadOnlyDictionary<string, int> slots,
        IReadOnlyList<string?>? parameterTypes = null,
        IReadOnlyList<string>? requiredTags = null,
        IReadOnlyList<string>? excludedTags = null,
        int entryAddress = -1)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        DispatchKind = dispatchKind;
        Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        SignatureLabels = signatureLabels ?? throw new ArgumentNullException(nameof(signatureLabels));
        ParameterTypes = NormalizeParameterTypes(parameterTypes, parameters);
        RequiredTags = NormalizeTags(requiredTags);
        ExcludedTags = NormalizeTags(excludedTags);
        DeclarationOrder = declarationOrder;
        Slots = NormalizeSlots(slots);
        EntryAddress = entryAddress;
        LocalSlotCount = GetLocalSlotCount(Slots);
    }

    public string Message { get; }

    public GameEventScriptBytecodeHandlerDispatchKind DispatchKind { get; }

    public IReadOnlyList<string> Parameters { get; }

    public IReadOnlyList<string> SignatureLabels { get; }

    public IReadOnlyList<string?> ParameterTypes { get; }

    public IReadOnlyList<string> RequiredTags { get; }

    public IReadOnlyList<string> ExcludedTags { get; }

    public int DeclarationOrder { get; }

    public int EntryAddress { get; internal set; }

    public int LocalSlotCount { get; }

    public IReadOnlyDictionary<string, int> Slots { get; }

    private static IReadOnlyList<string?> NormalizeParameterTypes(IReadOnlyList<string?>? parameterTypes, IReadOnlyList<string> parameters)
    {
        _ = parameters ?? throw new ArgumentNullException(nameof(parameters));
        if (parameterTypes is null)
        {
            return new string?[parameters.Count];
        }

        if (parameterTypes.Count != parameters.Count)
        {
            throw new ArgumentException("Parameter type hint count must match parameter count.", nameof(parameterTypes));
        }

        return parameterTypes.Select(type => string.IsNullOrWhiteSpace(type) ? null : type).ToArray();
    }

    private static IReadOnlyList<string> NormalizeTags(IReadOnlyList<string>? tags)
        => GameEventScriptMessage.NormalizeTags(tags);

    private static IReadOnlyDictionary<string, int> NormalizeSlots(IReadOnlyDictionary<string, int> slots)
    {
        _ = slots ?? throw new ArgumentNullException(nameof(slots));
        var copy = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var pair in slots)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                throw new ArgumentException("Slot names must be non-empty.", nameof(slots));
            }

            if (pair.Value < 0)
            {
                throw new ArgumentException("Slot indexes must be non-negative.", nameof(slots));
            }

            copy[pair.Key] = pair.Value;
        }

        return copy;
    }

    private static int GetLocalSlotCount(IReadOnlyDictionary<string, int> slots)
        => slots.Count == 0
            ? 0
            : slots.Values.Max() + 1;
}

public sealed class GameEventScriptBytecodeCallable
{
    internal GameEventScriptBytecodeCallable(
        string name,
        GameEventScriptBytecodeCallableKind kind,
        IReadOnlyList<string> parameters,
        IReadOnlyList<string> signatureLabels,
        IReadOnlyList<string?>? parameterTypes = null,
        int entryAddress = -1,
        int localSlotCount = 0,
        int returnSlot = -1)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Kind = kind;
        Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        SignatureLabels = signatureLabels ?? throw new ArgumentNullException(nameof(signatureLabels));
        ParameterTypes = NormalizeParameterTypes(parameterTypes, parameters);
        EntryAddress = entryAddress;
        LocalSlotCount = localSlotCount;
        ReturnSlot = returnSlot;
    }

    public string Name { get; }

    public GameEventScriptBytecodeCallableKind Kind { get; }

    public IReadOnlyList<string> Parameters { get; }

    public IReadOnlyList<string> SignatureLabels { get; }

    public IReadOnlyList<string?> ParameterTypes { get; }

    public int EntryAddress { get; internal set; }

    public int LocalSlotCount { get; internal set; }

    public int ReturnSlot { get; internal set; }

    private static IReadOnlyList<string?> NormalizeParameterTypes(IReadOnlyList<string?>? parameterTypes, IReadOnlyList<string> parameters)
    {
        _ = parameters ?? throw new ArgumentNullException(nameof(parameters));
        if (parameterTypes is null)
        {
            return new string?[parameters.Count];
        }

        if (parameterTypes.Count != parameters.Count)
        {
            throw new ArgumentException("Parameter type hint count must match parameter count.", nameof(parameterTypes));
        }

        return parameterTypes.Select(type => string.IsNullOrWhiteSpace(type) ? null : type).ToArray();
    }
}
