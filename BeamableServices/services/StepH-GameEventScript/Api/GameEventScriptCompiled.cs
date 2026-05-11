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
        IReadOnlyList<IReadOnlyList<ushort>> uShortListPool,
        IReadOnlyList<GameEventScriptExtensionReference> externalReferences,
        IReadOnlyList<GameEventScriptExternalTypeConstructorReference> externalTypeConstructorReferences,
        IReadOnlyDictionary<string, GameEventScriptBytecodeCallable> callables,
        IReadOnlyDictionary<string, IReadOnlyList<GameEventScriptBytecodeHandler>> handlers,
        IReadOnlyDictionary<string, GameEventScriptBytecodeTypeDefinition> typeDefinitions,
        IReadOnlyList<GameEventScriptBytecodeInstruction>? code = null,
        int maxFrameSlots = 0,
        IReadOnlyList<GameEventScriptBytecodeOperationLayout>? operationLayouts = null,
        GameEventScriptBytecodeDebugSegment? debugSegment = null,
        IReadOnlyList<GameEventScriptBytecodePipelinePattern>? pipelinePatternPool = null,
        IReadOnlyList<GameEventScriptBytecodePipelineObjectPattern>? pipelineObjectPatternPool = null,
        IReadOnlyList<GameEventScriptBytecodePipelineSelector>? pipelineSelectorPool = null,
        IReadOnlyList<GameEventScriptBytecodePipeline>? pipelinePool = null)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        ModuleName = string.IsNullOrWhiteSpace(moduleName)
            ? throw new ArgumentException("Module name must be non-empty.", nameof(moduleName))
            : moduleName;
        StringPool = CopyList(stringPool, nameof(stringPool));
        UShortListPool = CopyNestedUShortLayouts(uShortListPool, nameof(uShortListPool));
        ExternalReferences = CopyList(externalReferences, nameof(externalReferences));
        ExternalTypeConstructorReferences = CopyList(externalTypeConstructorReferences, nameof(externalTypeConstructorReferences));
        Callables = CopyDictionary(callables, nameof(callables));
        Handlers = CopyHandlerDictionary(handlers, nameof(handlers));
        TypeDefinitions = CopyDictionary(typeDefinitions, nameof(typeDefinitions));
        Code = code?.ToArray() ?? [];
        MaxFrameSlots = Math.Max(1, maxFrameSlots);
        OperationLayouts = operationLayouts?.ToArray() ?? [];
        DebugSegment = debugSegment ?? GameEventScriptBytecodeDebugSegment.Empty;
        PipelinePatternPool = pipelinePatternPool?.ToArray() ?? [];
        PipelineObjectPatternPool = pipelineObjectPatternPool?.ToArray() ?? [];
        PipelineSelectorPool = pipelineSelectorPool?.ToArray() ?? [];
        PipelinePool = pipelinePool?.ToArray() ?? [];
    }

    public GameEventScriptCompileOptions Options { get; }

    public string ModuleName { get; }

    public IReadOnlyList<string> StringPool { get; }

    public IReadOnlyList<IReadOnlyList<ushort>> UShortListPool { get; }

    public IReadOnlyList<GameEventScriptExtensionReference> ExternalReferences { get; }

    public IReadOnlyList<GameEventScriptExternalTypeConstructorReference> ExternalTypeConstructorReferences { get; }

    public IReadOnlyDictionary<string, GameEventScriptBytecodeCallable> Callables { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<GameEventScriptBytecodeHandler>> Handlers { get; }

    public IReadOnlyDictionary<string, GameEventScriptBytecodeTypeDefinition> TypeDefinitions { get; }

    public IReadOnlyList<GameEventScriptBytecodeInstruction> Code { get; }

    public int MaxFrameSlots { get; }

    public IReadOnlyList<GameEventScriptBytecodeOperationLayout> OperationLayouts { get; }

    public GameEventScriptBytecodeDebugSegment DebugSegment { get; }

    public IReadOnlyList<GameEventScriptBytecodePipelinePattern> PipelinePatternPool { get; }

    public IReadOnlyList<GameEventScriptBytecodePipelineObjectPattern> PipelineObjectPatternPool { get; }

    public IReadOnlyList<GameEventScriptBytecodePipelineSelector> PipelineSelectorPool { get; }

    public IReadOnlyList<GameEventScriptBytecodePipeline> PipelinePool { get; }

    private static IReadOnlyList<T> CopyList<T>(IReadOnlyList<T> source, string parameterName)
        => (source ?? throw new ArgumentNullException(parameterName)).ToArray();

    private static IReadOnlyList<IReadOnlyList<ushort>> CopyNestedUShortLayouts(
        IReadOnlyList<IReadOnlyList<ushort>> source,
        string parameterName)
        => (source ?? throw new ArgumentNullException(parameterName))
            .Select(layout => (IReadOnlyList<ushort>)(layout?.ToArray() ?? throw new ArgumentException("Nested ushort layouts must not be null.", parameterName)))
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
