#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace StepH.GameEventScript.Api;

public enum GameEventScriptBytecodeOpCode : byte
{
    Nop = 0x00,
    LoadNothing = 0x01,
    LoadTrue = 0x02,
    LoadFalse = 0x03,
    LoadInteger = 0x04,
    LoadFloat = 0x05,
    LoadText = 0x06,
    LoadTag = 0x07,
    MoveSlot = 0x08,
    BindParameter = 0x09,
    Jump = 0x0A,
    JumpIfTrue = 0x0B,
    JumpIfFalse = 0x0C,
    JumpIfNotTrue = 0x0D,
    ReturnNothing = 0x0E,
    Return = 0x0F,

    Or = 0x10,
    And = 0x11,
    Xor = 0x12,
    Equal = 0x13,
    NotEqual = 0x14,
    ApproxEqual = 0x15,
    Less = 0x16,
    Greater = 0x17,
    LessOrEqual = 0x18,
    GreaterOrEqual = 0x19,
    Add = 0x1A,
    Subtract = 0x1B,
    Multiply = 0x1C,
    Divide = 0x1D,
    Power = 0x1E,
    Default = 0x1F,

    PrimitiveIntegerEqual = 0x20,
    PrimitiveIntegerNotEqual = 0x21,
    PrimitiveIntegerLess = 0x22,
    PrimitiveIntegerGreater = 0x23,
    PrimitiveIntegerLessOrEqual = 0x24,
    PrimitiveIntegerGreaterOrEqual = 0x25,
    PrimitiveIntegerAdd = 0x26,
    PrimitiveIntegerSubtract = 0x27,
    PrimitiveIntegerMultiply = 0x28,
    PrimitiveIntegerDivide = 0x29,
    PrimitiveIntegerFloorDivide = 0x2A,
    PrimitiveIntegerModulo = 0x2B,
    PrimitiveIntegerRemainder = 0x2C,
    IntegerDivide = 0x2D,
    Modulo = 0x2E,
    Remainder = 0x2F,

    UnaryNegate = 0x30,
    UnaryNot = 0x31,
    UnaryHasValue = 0x32,
    UnaryEmpty = 0x33,
    UnaryLength = 0x34,
    UnaryChance = 0x35,
    UnaryAbs = 0x36,
    UnaryNaturalLog = 0x37,
    Clamp = 0x38,
    Random = 0x39,
    Dice = 0x3A,
    RandomPush = 0x3B,
    RandomPushConstant = 0x3C,
    RandomPop = 0x3D,
    Range = 0x3E,
    RangeWithStep = 0x3F,

    Contains = 0x40,
    ContainsValue = 0x41,
    StartsWith = 0x42,
    EndsWith = 0x43,
    Intersect = 0x44,
    Combine = 0x45,
    Except = 0x46,
    Zip = 0x47,
    UnaryKeys = 0x48,
    UnaryValues = 0x49,
    UnaryEntries = 0x4A,
    ShortCircuitOr = 0x4B,
    ShortCircuitAnd = 0x4C,
    ShortCircuitImplies = 0x4D,

    CastNothing = 0x50,
    CastBoolean = 0x51,
    CastInteger = 0x52,
    CastFloat = 0x53,
    CastNumber = 0x54,
    CastPercentage = 0x55,
    CastDegree = 0x56,
    CastMeter = 0x57,
    CastSecond = 0x58,
    CastVector = 0x59,
    CastPoint = 0x5A,
    CastUuid = 0x5B,
    CastOptional = 0x5C,
    CastCustom = 0x5D,

    CastSequence = 0x60,
    CastSeries = 0x61,
    CastEnvelope = 0x62,
    CastRef = 0x63,
    CastTag = 0x64,
    CastText = 0x65,
    CastList = 0x66,
    CastRange = 0x67,
    CastMessage = 0x68,
    CastHandler = 0x69,
    CastDictionary = 0x6A,
    CastSet = 0x6B,
    CastDice = 0x6C,

    TypeCheckNothing = 0x70,
    TypeCheckBoolean = 0x71,
    TypeCheckInteger = 0x72,
    TypeCheckFloat = 0x73,
    TypeCheckPercentage = 0x74,
    TypeCheckDegree = 0x75,
    TypeCheckMeter = 0x76,
    TypeCheckSecond = 0x77,
    TypeCheckVector = 0x78,
    TypeCheckPoint = 0x79,
    TypeCheckUuid = 0x7A,
    TypeCheckOptional = 0x7B,
    TypeCheckTag = 0x7C,
    TypeCheckText = 0x7D,
    TypeCheckCustom = 0x7E,

    TypeCheckSequence = 0x80,
    TypeCheckSeries = 0x81,
    TypeCheckEnvelope = 0x82,
    TypeCheckList = 0x83,
    TypeCheckRange = 0x84,
    TypeCheckMessage = 0x85,
    TypeCheckHandler = 0x86,
    TypeCheckRef = 0x87,
    TypeCheckDictionary = 0x88,
    TypeCheckSet = 0x89,
    TypeCheckDice = 0x8A,

    LoadHandler = 0x90,
    TypeConstructor = 0x91,
    PredicateTest = 0x92,
    MemberAccess = 0x93,
    IndexedAccess = 0x94,
    BuildList = 0x95,
    BuildSequence = 0x96,
    BuildSet = 0x97,
    BuildDictionary = 0x98,
    BuildMessage = 0x99,
    BindHandler = 0x9A,
    CallExtension = 0x9B,
    Call = 0x9C,
    Variadic = 0x9D,

    EnterScope = 0xA0,
    ExitScope = 0xA1,
    EmitMessage = 0xA2,
    EmitMessageWithTags = 0xA3,
    PublishMessage = 0xA4,
    PublishMessageWithTags = 0xA5,
    EmitMessageValue = 0xA6,
    EmitMessageValueWithTags = 0xA7,
    PublishMessageValue = 0xA8,
    PublishMessageValueWithTags = 0xA9,

    RangeIterator = 0xB0,
    RangeIteratorWithStep = 0xB1,
    RangeIteratorShort = 0xB2,
    CollectionIterator = 0xB3,
    IteratorNext = 0xB4,
    IteratorClose = 0xB5,
    CollectionBuilderList = 0xB6,
    CollectionBuilderSet = 0xB7,
    CollectionBuilderAdd = 0xB8,
    CollectionBuilderFinish = 0xB9,

    Pipeline = 0xD0
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

[JsonConverter(typeof(GameEventScriptBytecodeInstructionJsonConverter))]
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
