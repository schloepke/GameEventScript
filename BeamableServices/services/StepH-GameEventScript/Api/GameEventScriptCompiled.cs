#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Runtime;

namespace StepH.GameEventScript.Api;

public sealed class GameEventScriptCompiled
{
    internal GameEventScriptCompiled(
        GameEventScriptCompileOptions options,
        string moduleName,
        IReadOnlyList<string> stringPool,
        IReadOnlyList<string> signatures,
        IReadOnlyList<GameEventScriptExtensionReference> externalReferences,
        IReadOnlyList<GameEventScriptExternalTypeConstructorReference> externalTypeConstructorReferences,
        IReadOnlyList<IReadOnlyList<string>> namedArgumentLayouts,
        IReadOnlyDictionary<string, GameEventScriptBytecodeCallable> callables,
        IReadOnlyDictionary<string, IReadOnlyList<GameEventScriptBytecodeHandler>> handlers,
        IReadOnlyDictionary<string, GameEventScriptBytecodeTypeDefinition> typeDefinitions,
        IReadOnlyList<GameEventScriptBytecodeInstruction>? code = null,
        int maxFrameSlots = 0,
        IReadOnlyList<GameEventScriptBytecodeOperationLayout>? operationLayouts = null,
        IReadOnlyList<GameEventScriptBytecodeDiagnosticLayout>? diagnosticLayouts = null,
        IReadOnlyList<GameEventScriptBytecodePublishLayoutEntry>? publishLayouts = null,
        IReadOnlyList<GameEventScriptBytecodeIterationSourceLayout>? iterationSourceLayouts = null,
        IReadOnlyList<GameEventScriptBytecodeLoopLayout>? loopLayouts = null,
        IReadOnlyList<GameEventScriptBytecodeSeededRandomBlockLayout>? seededRandomBlockLayouts = null,
        IReadOnlyList<GameEventScriptBytecodeDicePatternLayout>? dicePatternLayouts = null,
        IReadOnlyList<GameEventScriptBytecodeObjectMatchPatternLayout>? objectMatchPatternLayouts = null,
        IReadOnlyList<GameEventScriptBytecodeSelectorLayout>? selectorLayouts = null,
        IReadOnlyList<GameEventScriptBytecodePipelineLayout>? pipelineLayouts = null,
        IReadOnlyList<GameEventScriptBytecodeGeneratedCollectionLayout>? generatedCollectionLayouts = null,
        IReadOnlyList<GameEventScriptBytecodeGuardedChoiceLayout>? guardedChoiceLayouts = null)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        ModuleName = string.IsNullOrWhiteSpace(moduleName)
            ? throw new ArgumentException("Module name must be non-empty.", nameof(moduleName))
            : moduleName;
        StringPool = CopyList(stringPool, nameof(stringPool));
        Signatures = CopyList(signatures, nameof(signatures));
        ExternalReferences = CopyList(externalReferences, nameof(externalReferences));
        ExternalTypeConstructorReferences = CopyList(externalTypeConstructorReferences, nameof(externalTypeConstructorReferences));
        NamedArgumentLayouts = CopyNestedStringLayouts(namedArgumentLayouts, nameof(namedArgumentLayouts));
        Callables = CopyDictionary(callables, nameof(callables));
        Handlers = CopyHandlerDictionary(handlers, nameof(handlers));
        TypeDefinitions = CopyDictionary(typeDefinitions, nameof(typeDefinitions));
        Code = code?.ToArray() ?? [];
        MaxFrameSlots = Math.Max(1, maxFrameSlots);
        OperationLayouts = operationLayouts?.ToArray() ?? [];
        DiagnosticLayouts = diagnosticLayouts?.ToArray() ?? [];
        PublishLayouts = publishLayouts?.ToArray() ?? [];
        IterationSourceLayouts = iterationSourceLayouts?.ToArray() ?? [];
        LoopLayouts = loopLayouts?.ToArray() ?? [];
        SeededRandomBlockLayouts = seededRandomBlockLayouts?.ToArray() ?? [];
        DicePatternLayouts = dicePatternLayouts?.ToArray() ?? [];
        ObjectMatchPatternLayouts = objectMatchPatternLayouts?.ToArray() ?? [];
        SelectorLayouts = selectorLayouts?.ToArray() ?? [];
        PipelineLayouts = pipelineLayouts?.ToArray() ?? [];
        GeneratedCollectionLayouts = generatedCollectionLayouts?.ToArray() ?? [];
        GuardedChoiceLayouts = guardedChoiceLayouts?.ToArray() ?? [];
    }

    public GameEventScriptCompileOptions Options { get; }

    public string ModuleName { get; }

    public IReadOnlyList<string> StringPool { get; }

    public IReadOnlyList<string> Signatures { get; }

    public IReadOnlyList<GameEventScriptExtensionReference> ExternalReferences { get; }

    public IReadOnlyList<GameEventScriptExternalTypeConstructorReference> ExternalTypeConstructorReferences { get; }

    public IReadOnlyList<IReadOnlyList<string>> NamedArgumentLayouts { get; }

    public IReadOnlyDictionary<string, GameEventScriptBytecodeCallable> Callables { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<GameEventScriptBytecodeHandler>> Handlers { get; }

    public IReadOnlyDictionary<string, GameEventScriptBytecodeTypeDefinition> TypeDefinitions { get; }

    public IReadOnlyList<GameEventScriptBytecodeInstruction> Code { get; }

    public int MaxFrameSlots { get; }

    public IReadOnlyList<GameEventScriptBytecodeOperationLayout> OperationLayouts { get; }

    public IReadOnlyList<GameEventScriptBytecodeDiagnosticLayout> DiagnosticLayouts { get; }

    public IReadOnlyList<GameEventScriptBytecodePublishLayoutEntry> PublishLayouts { get; }

    public IReadOnlyList<GameEventScriptBytecodeIterationSourceLayout> IterationSourceLayouts { get; }

    public IReadOnlyList<GameEventScriptBytecodeLoopLayout> LoopLayouts { get; }

    public IReadOnlyList<GameEventScriptBytecodeSeededRandomBlockLayout> SeededRandomBlockLayouts { get; }

    public IReadOnlyList<GameEventScriptBytecodeDicePatternLayout> DicePatternLayouts { get; }

    public IReadOnlyList<GameEventScriptBytecodeObjectMatchPatternLayout> ObjectMatchPatternLayouts { get; }

    public IReadOnlyList<GameEventScriptBytecodeSelectorLayout> SelectorLayouts { get; }

    public IReadOnlyList<GameEventScriptBytecodePipelineLayout> PipelineLayouts { get; }

    public IReadOnlyList<GameEventScriptBytecodeGeneratedCollectionLayout> GeneratedCollectionLayouts { get; }

    public IReadOnlyList<GameEventScriptBytecodeGuardedChoiceLayout> GuardedChoiceLayouts { get; }

    private static IReadOnlyList<T> CopyList<T>(IReadOnlyList<T> source, string parameterName)
        => (source ?? throw new ArgumentNullException(parameterName)).ToArray();

    private static IReadOnlyList<IReadOnlyList<string>> CopyNestedStringLayouts(
        IReadOnlyList<IReadOnlyList<string>> source,
        string parameterName)
        => (source ?? throw new ArgumentNullException(parameterName))
            .Select(layout => (IReadOnlyList<string>)(layout?.ToArray() ?? throw new ArgumentException("Nested string layouts must not be null.", parameterName)))
            .ToArray();

    private static IReadOnlyDictionary<string, T> CopyDictionary<T>(
        IReadOnlyDictionary<string, T> source,
        string parameterName)
    {
        _ = source ?? throw new ArgumentNullException(parameterName);
        var copy = new SortedDictionary<string, T>(StringComparer.Ordinal);
        foreach (var pair in source)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                throw new ArgumentException("Dictionary keys must be non-empty.", parameterName);
            }

            copy[pair.Key] = pair.Value;
        }

        return copy;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<GameEventScriptBytecodeHandler>> CopyHandlerDictionary(
        IReadOnlyDictionary<string, IReadOnlyList<GameEventScriptBytecodeHandler>> source,
        string parameterName)
    {
        _ = source ?? throw new ArgumentNullException(parameterName);
        var copy = new SortedDictionary<string, IReadOnlyList<GameEventScriptBytecodeHandler>>(StringComparer.Ordinal);
        foreach (var pair in source)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                throw new ArgumentException("Handler dictionary keys must be non-empty.", parameterName);
            }

            copy[pair.Key] = pair.Value?.ToArray() ?? throw new ArgumentException("Handler lists must not be null.", parameterName);
        }

        return copy;
    }
}
