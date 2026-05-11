#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;

namespace StepH.GameEventScript.Api;


public enum GameEventScriptBytecodeCallableKind
{
    Predicate,
    Function
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

public sealed class GameEventScriptBytecodeDebugSegment(IReadOnlyList<GameEventScriptBytecodeDebugDiagnosticSite>? diagnosticSites = null)
{
    public static GameEventScriptBytecodeDebugSegment Empty { get; } = new();

    public IReadOnlyList<GameEventScriptBytecodeDebugDiagnosticSite> DiagnosticSites { get; } = diagnosticSites?.ToArray() ?? [];

    public bool IsEmpty => DiagnosticSites.Count == 0;
}

public sealed class GameEventScriptBytecodeDebugDiagnosticSite(GameEventScriptBytecodeDiagnosticKind kind, GameEventScriptBytecodeDiagnosticTiming timing, int address, int slot, string name)
{
    public GameEventScriptBytecodeDiagnosticKind Kind { get; } = kind;

    public GameEventScriptBytecodeDiagnosticTiming Timing { get; } = timing;

    public int Address { get; } = address;

    public int Slot { get; } = slot;

    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));
}

public enum GameEventScriptBytecodePipelinePatternKind
{
    Count,
    FullHouse,
    Straight
}

public sealed class GameEventScriptBytecodePipelinePattern(
    GameEventScriptBytecodePipelinePatternKind kind,
    int count = 0,
    int faceEntryAddress = -1)
{
    public GameEventScriptBytecodePipelinePatternKind Kind { get; } = kind;

    public int Count { get; } = count;

    public int FaceEntryAddress { get; } = faceEntryAddress;
}

public enum GameEventScriptBytecodePipelineObjectPatternValueKind
{
    Expression,
    Nested
}

public sealed class GameEventScriptBytecodePipelineObjectPatternEntry(
    string key,
    GameEventScriptBytecodePipelineObjectPatternValueKind valueKind,
    int expressionEntryAddress = -1,
    int nestedPatternIndex = -1)
{
    public string Key { get; } = key ?? throw new ArgumentNullException(nameof(key));

    public GameEventScriptBytecodePipelineObjectPatternValueKind ValueKind { get; } = valueKind;

    public int ExpressionEntryAddress { get; } = expressionEntryAddress;

    public int NestedPatternIndex { get; } = nestedPatternIndex;
}

public sealed class GameEventScriptBytecodePipelineObjectPattern(IReadOnlyList<GameEventScriptBytecodePipelineObjectPatternEntry>? entries)
{
    public IReadOnlyList<GameEventScriptBytecodePipelineObjectPatternEntry> Entries { get; } = entries?.ToArray() ?? [];
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

public sealed class GameEventScriptBytecodePipelineSelector(
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
    public GameEventScriptBytecodePipelineSelectorKind Kind { get; } = kind;

    public int IdentifierSlot { get; } = identifierSlot;

    public string? EdgeMode { get; } = edgeMode;

    public string? SecondaryMode { get; } = secondaryMode;

    public int Count { get; } = count;

    public int SecondaryIdentifierSlot { get; } = secondaryIdentifierSlot;

    public bool Flag { get; } = flag;

    public int ExpressionEntryAddress { get; } = expressionEntryAddress;

    public int SecondaryExpressionEntryAddress { get; } = secondaryExpressionEntryAddress;

    public int PipelinePatternIndex { get; } = pipelinePatternIndex;

    public int ObjectPatternIndex { get; } = objectPatternIndex;
}

public sealed class GameEventScriptBytecodePipeline(
    int sourceSlot,
    IReadOnlyList<int>? prefixSelectorIndexes,
    int terminalSelectorIndex)
{
    public int SourceSlot { get; } = sourceSlot;

    public IReadOnlyList<int> PrefixSelectorIndexes { get; } = prefixSelectorIndexes?.ToArray() ?? [];

    public int TerminalSelectorIndex { get; } = terminalSelectorIndex;
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

    private GameEventScriptBytecodeTypeFieldDefinition(
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
