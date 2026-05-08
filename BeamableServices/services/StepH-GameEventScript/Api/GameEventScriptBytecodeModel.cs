#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.Api;

public enum GameEventScriptBytecodeConstantKind
{
    Nothing,
    Boolean,
    Integer,
    Float,
    Percentage,
    Text,
    Tag,
    Handler
}

public sealed class GameEventScriptBytecodeConstant : IEquatable<GameEventScriptBytecodeConstant>
{
    private GameEventScriptBytecodeConstant(
        GameEventScriptBytecodeConstantKind kind,
        string? text = null,
        long integer = 0,
        double number = 0d,
        bool boolean = false,
        GameEventScriptNumericUnit? unit = null,
        bool isNaN = false,
        bool isInfinity = false,
        bool isNegativeInfinity = false,
        IReadOnlyList<string>? labels = null)
    {
        Kind = kind;
        Text = text;
        Integer = integer;
        Number = number;
        Boolean = boolean;
        Unit = unit;
        IsNaN = isNaN;
        IsInfinity = isInfinity;
        IsNegativeInfinity = isNegativeInfinity;
        Labels = labels?.Select(GameEventScriptMessageSignature.NormalizeParameterName).ToArray() ?? [];
    }

    public GameEventScriptBytecodeConstantKind Kind { get; }

    public string? Text { get; }

    public long Integer { get; }

    public double Number { get; }

    public bool Boolean { get; }

    public GameEventScriptNumericUnit? Unit { get; }

    public bool IsNaN { get; }

    public bool IsInfinity { get; }

    public bool IsNegativeInfinity { get; }

    public IReadOnlyList<string> Labels { get; }

    public static GameEventScriptBytecodeConstant Nothing()
        => new(GameEventScriptBytecodeConstantKind.Nothing);

    public static GameEventScriptBytecodeConstant FromBoolean(bool value)
        => new(GameEventScriptBytecodeConstantKind.Boolean, boolean: value);

    public static GameEventScriptBytecodeConstant FromInteger(long value, GameEventScriptNumericUnit? unit = null)
        => new(GameEventScriptBytecodeConstantKind.Integer, integer: value, unit: unit);

    public static GameEventScriptBytecodeConstant FromFloat(
        double value,
        GameEventScriptNumericUnit? unit = null,
        bool isNaN = false,
        bool isInfinity = false,
        bool isNegativeInfinity = false)
        => new(GameEventScriptBytecodeConstantKind.Float, number: value, unit: unit, isNaN: isNaN, isInfinity: isInfinity, isNegativeInfinity: isNegativeInfinity);

    public static GameEventScriptBytecodeConstant FromPercentage(double ratio)
        => new(GameEventScriptBytecodeConstantKind.Percentage, number: ratio);

    public static GameEventScriptBytecodeConstant FromText(string value)
        => new(GameEventScriptBytecodeConstantKind.Text, text: value ?? string.Empty);

    public static GameEventScriptBytecodeConstant FromTag(string value)
        => new(GameEventScriptBytecodeConstantKind.Tag, text: value ?? string.Empty);

    public static GameEventScriptBytecodeConstant FromHandler(string messageName, IReadOnlyList<string> labels)
        => new(GameEventScriptBytecodeConstantKind.Handler, text: GameEventScriptMessageSignature.NormalizeMessageName(messageName), labels: labels);

    public bool Equals(GameEventScriptBytecodeConstant? other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Kind == other.Kind &&
               string.Equals(Text, other.Text, StringComparison.Ordinal) &&
               Integer == other.Integer &&
               Number == other.Number &&
               Boolean == other.Boolean &&
               Unit == other.Unit &&
               IsNaN == other.IsNaN &&
               IsInfinity == other.IsInfinity &&
               IsNegativeInfinity == other.IsNegativeInfinity &&
               Labels.SequenceEqual(other.Labels, StringComparer.Ordinal);
    }

    public override bool Equals(object? obj)
        => obj is GameEventScriptBytecodeConstant other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Kind);
        hash.Add(Text, StringComparer.Ordinal);
        hash.Add(Integer);
        hash.Add(Number);
        hash.Add(Boolean);
        hash.Add(Unit);
        hash.Add(IsNaN);
        hash.Add(IsInfinity);
        hash.Add(IsNegativeInfinity);
        foreach (var label in Labels)
        {
            hash.Add(label, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }

    public override string ToString()
        => Kind switch
        {
            GameEventScriptBytecodeConstantKind.Nothing => "nothing",
            GameEventScriptBytecodeConstantKind.Boolean => Boolean ? "True" : "False",
            GameEventScriptBytecodeConstantKind.Integer => Unit is { } unit
                ? $"{Integer.ToString(System.Globalization.CultureInfo.InvariantCulture)}{unit.ToSuffix()}"
                : Integer.ToString(System.Globalization.CultureInfo.InvariantCulture),
            GameEventScriptBytecodeConstantKind.Float when IsNaN => "NaN",
            GameEventScriptBytecodeConstantKind.Float when IsInfinity => IsNegativeInfinity ? "-Infinity" : "Infinity",
            GameEventScriptBytecodeConstantKind.Float => Unit is { } unit
                ? $"{Number.ToString(System.Globalization.CultureInfo.InvariantCulture)}{unit.ToSuffix()}"
                : Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            GameEventScriptBytecodeConstantKind.Percentage => $"{(Number * 100d).ToString(System.Globalization.CultureInfo.InvariantCulture)}%",
            GameEventScriptBytecodeConstantKind.Text => Text ?? string.Empty,
            GameEventScriptBytecodeConstantKind.Tag => $":{Text}",
            GameEventScriptBytecodeConstantKind.Handler => $"{Text}({string.Join(",", Labels)})",
            _ => Kind.ToString()
        };
}

public enum GameEventScriptBytecodeOpCode
{
    LoadConstant,
    LoadSlot,
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
    Unary,
    Variadic,
    Clamp,
    Random,
    Range,
    Dice,
    SeededRandom,
    Cast,
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
    TypeCheck,
    Pipeline,
    GeneratedCollection,
    GuardedChoice,
    Power,
    ShortCircuitOr,
    ShortCircuitAnd,
    ShortCircuitImplies,
    Nop,
    BindParameter,
    CoerceSlot,
    CopySlot,
    Jump,
    JumpIfTrue,
    JumpIfFalse,
    JumpIfNotTrue,
    EnterScope,
    ExitScope,
    Return,
    PublishValue,
    PublishMessageValue,
    ForRange,
    ForCollection,
    SeededRandomBlock
}

public enum GameEventScriptBytecodeCallableKind
{
    Predicate,
    Function
}

public enum GameEventScriptBytecodeCastKind
{
    Boolean,
    Integer,
    Float,
    Number,
    Percentage,
    Degree,
    Meter,
    Second,
    Vector,
    Point,
    Uuid,
    Sequence,
    Series,
    Envelope,
    Ref
}

public readonly record struct GameEventScriptBytecodeInstruction(
    GameEventScriptBytecodeOpCode OpCode,
    int Dest = -1,
    int A = -1,
    int B = -1,
    int C = -1,
    int Target = -1,
    int Target2 = -1,
    int Data = -1);

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
        GameEventScriptBytecodeCastKind castKind = default,
        int externalReferenceIndex = -1,
        int namedArgumentLayoutIndex = -1,
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
        CastKind = castKind;
        ExternalReferenceIndex = externalReferenceIndex;
        NamedArgumentLayoutIndex = namedArgumentLayoutIndex;
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

    public GameEventScriptBytecodeCastKind CastKind { get; }

    public int ExternalReferenceIndex { get; }

    public int NamedArgumentLayoutIndex { get; }

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

public sealed class GameEventScriptBytecodePublishLayoutEntry
{
    public GameEventScriptBytecodePublishLayoutEntry(
        GameEventScriptBytecodePublishKind kind,
        string? messageName = null,
        string? signatureId = null,
        IReadOnlyList<string>? argumentNames = null,
        IReadOnlyList<int>? argumentSlots = null,
        int messageSlot = -1,
        IReadOnlyList<int>? tagSlots = null)
    {
        Kind = kind;
        MessageName = messageName;
        SignatureId = signatureId;
        ArgumentNames = argumentNames?.ToArray() ?? [];
        ArgumentSlots = argumentSlots?.ToArray() ?? [];
        MessageSlot = messageSlot;
        TagSlots = tagSlots?.ToArray() ?? [];
    }

    public GameEventScriptBytecodePublishKind Kind { get; }

    public string? MessageName { get; }

    public string? SignatureId { get; }

    public IReadOnlyList<string> ArgumentNames { get; }

    public IReadOnlyList<int> ArgumentSlots { get; }

    public int MessageSlot { get; }

    public IReadOnlyList<int> TagSlots { get; }
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

public sealed class GameEventScriptBytecodeSeededRandomBlockLayout
{
    public GameEventScriptBytecodeSeededRandomBlockLayout(int seedSlot)
    {
        SeedSlot = seedSlot;
    }

    public int SeedSlot { get; }
}

public enum GameEventScriptBytecodeDicePatternKind
{
    Count,
    FullHouse,
    Straight
}

public sealed class GameEventScriptBytecodeDicePatternLayout
{
    public GameEventScriptBytecodeDicePatternLayout(
        GameEventScriptBytecodeDicePatternKind kind,
        int count = 0,
        int faceEntryAddress = -1)
    {
        Kind = kind;
        Count = count;
        FaceEntryAddress = faceEntryAddress;
    }

    public GameEventScriptBytecodeDicePatternKind Kind { get; }

    public int Count { get; }

    public int FaceEntryAddress { get; }
}

public enum GameEventScriptBytecodeObjectMatchValueKind
{
    Expression,
    Nested
}

public sealed class GameEventScriptBytecodeObjectMatchEntryLayout
{
    public GameEventScriptBytecodeObjectMatchEntryLayout(
        string key,
        GameEventScriptBytecodeObjectMatchValueKind valueKind,
        int expressionEntryAddress = -1,
        int nestedPatternLayoutIndex = -1)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        ValueKind = valueKind;
        ExpressionEntryAddress = expressionEntryAddress;
        NestedPatternLayoutIndex = nestedPatternLayoutIndex;
    }

    public string Key { get; }

    public GameEventScriptBytecodeObjectMatchValueKind ValueKind { get; }

    public int ExpressionEntryAddress { get; }

    public int NestedPatternLayoutIndex { get; }
}

public sealed class GameEventScriptBytecodeObjectMatchPatternLayout
{
    public GameEventScriptBytecodeObjectMatchPatternLayout(IReadOnlyList<GameEventScriptBytecodeObjectMatchEntryLayout>? entries)
    {
        Entries = entries?.ToArray() ?? [];
    }

    public IReadOnlyList<GameEventScriptBytecodeObjectMatchEntryLayout> Entries { get; }
}

public enum GameEventScriptBytecodeSelectorKind
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

public sealed class GameEventScriptBytecodeSelectorLayout
{
    public GameEventScriptBytecodeSelectorLayout(
        GameEventScriptBytecodeSelectorKind kind,
        int identifierSlot = -1,
        string? edgeMode = null,
        string? secondaryMode = null,
        int count = 0,
        int secondaryIdentifierSlot = -1,
        bool flag = false,
        int expressionEntryAddress = -1,
        int secondaryExpressionEntryAddress = -1,
        int dicePatternLayoutIndex = -1,
        int objectMatchPatternLayoutIndex = -1)
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
        DicePatternLayoutIndex = dicePatternLayoutIndex;
        ObjectMatchPatternLayoutIndex = objectMatchPatternLayoutIndex;
    }

    public GameEventScriptBytecodeSelectorKind Kind { get; }

    public int IdentifierSlot { get; }

    public string? EdgeMode { get; }

    public string? SecondaryMode { get; }

    public int Count { get; }

    public int SecondaryIdentifierSlot { get; }

    public bool Flag { get; }

    public int ExpressionEntryAddress { get; }

    public int SecondaryExpressionEntryAddress { get; }

    public int DicePatternLayoutIndex { get; }

    public int ObjectMatchPatternLayoutIndex { get; }
}

public sealed class GameEventScriptBytecodePipelineLayout
{
    public GameEventScriptBytecodePipelineLayout(
        int sourceSlot,
        IReadOnlyList<int>? prefixSelectorLayoutIndexes,
        int terminalSelectorLayoutIndex)
    {
        SourceSlot = sourceSlot;
        PrefixSelectorLayoutIndexes = prefixSelectorLayoutIndexes?.ToArray() ?? [];
        TerminalSelectorLayoutIndex = terminalSelectorLayoutIndex;
    }

    public int SourceSlot { get; }

    public IReadOnlyList<int> PrefixSelectorLayoutIndexes { get; }

    public int TerminalSelectorLayoutIndex { get; }
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
    GameEventScriptBytecodeCastKind CastKind = default,
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
        string signatureId,
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
        SignatureId = signatureId ?? throw new ArgumentNullException(nameof(signatureId));
        DeclarationOrder = declarationOrder;
        Slots = NormalizeSlots(slots);
        EntryAddress = entryAddress;
        LocalSlotCount = GetLocalSlotCount(Slots);
        Definition = GameEventScriptMessageSignature.Create(message, signatureLabels);
    }

    public string Message { get; }

    public GameEventScriptBytecodeHandlerDispatchKind DispatchKind { get; }

    public IReadOnlyList<string> Parameters { get; }

    public IReadOnlyList<string> SignatureLabels { get; }

    public IReadOnlyList<string?> ParameterTypes { get; }

    public IReadOnlyList<string> RequiredTags { get; }

    public IReadOnlyList<string> ExcludedTags { get; }

    public string SignatureId { get; }

    public int DeclarationOrder { get; }

    public int EntryAddress { get; internal set; }

    public int LocalSlotCount { get; }

    public IReadOnlyDictionary<string, int> Slots { get; }

    public GameEventScriptMessageSignature Definition { get; }

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
        string signatureId,
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
        SignatureId = signatureId ?? throw new ArgumentNullException(nameof(signatureId));
        EntryAddress = entryAddress;
        LocalSlotCount = localSlotCount;
        ReturnSlot = returnSlot;
    }

    public string Name { get; }

    public GameEventScriptBytecodeCallableKind Kind { get; }

    public IReadOnlyList<string> Parameters { get; }

    public IReadOnlyList<string> SignatureLabels { get; }

    public IReadOnlyList<string?> ParameterTypes { get; }

    public string SignatureId { get; }

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
