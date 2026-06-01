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

public sealed class GameEventScriptBytecodeTypeDefinition(
    string name,
    GameEventScriptBytecodeTypeFieldDefinition[] fields)
{
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    public IReadOnlyList<GameEventScriptBytecodeTypeFieldDefinition> Fields { get; } = fields ?? throw new ArgumentNullException(nameof(fields));

    public int ConstructorEntryAddress { get; internal set; } = -1;
}

public sealed class GameEventScriptBytecodeTypeFieldDefinition
{
    internal GameEventScriptBytecodeTypeFieldDefinition(
        string name,
        string typeName,
        string? constructorLabel)
        : this(name, typeName, constructorLabel, -1, -1, -1)
    {
    }

    private GameEventScriptBytecodeTypeFieldDefinition(
        string name,
        string typeName,
        string? constructorLabel,
        int minimumEntryAddress,
        int maximumEntryAddress,
        int computedEntryAddress)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
        ConstructorLabel = constructorLabel;
        MinimumEntryAddress = minimumEntryAddress;
        MaximumEntryAddress = maximumEntryAddress;
        ComputedEntryAddress = computedEntryAddress;
    }

    public string Name { get; }

    public string TypeName { get; }

    public string? ConstructorLabel { get; }

    public bool IsConstructorParameter => ConstructorLabel is not null;

    public int MinimumEntryAddress { get; internal set; }

    public int MaximumEntryAddress { get; internal set; }

    public int ComputedEntryAddress { get; internal set; }
}

public enum GameEventScriptBytecodeHandlerDispatchKind
{
    ExactSignature,
    MessageName
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
        int returnSlot = -1)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Kind = kind;
        Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        SignatureLabels = signatureLabels ?? throw new ArgumentNullException(nameof(signatureLabels));
        ParameterTypes = NormalizeParameterTypes(parameterTypes, parameters);
        EntryAddress = entryAddress;
        ReturnSlot = returnSlot;
    }

    public string Name { get; }

    public GameEventScriptBytecodeCallableKind Kind { get; }

    public IReadOnlyList<string> Parameters { get; }

    public IReadOnlyList<string> SignatureLabels { get; }

    public IReadOnlyList<string?> ParameterTypes { get; }

    public int EntryAddress { get; internal set; }

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
