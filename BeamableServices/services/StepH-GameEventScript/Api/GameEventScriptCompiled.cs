#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using StepH.GameEventScript.Runtime;

namespace StepH.GameEventScript.Api;

public sealed class GameEventScriptCompiled
{
    internal GameEventScriptCompiled(
        GameEventScriptCompileOptions options,
        IReadOnlyList<string> stringPool,
        IReadOnlyList<GameEventScriptBytecodeConstant> constantPool,
        IReadOnlyList<string> signatures,
        IReadOnlyList<GameEventScriptExtensionReference> externalReferences,
        IReadOnlyList<GameEventScriptExternalTypeConstructorReference> externalTypeConstructorReferences,
        IReadOnlyList<IReadOnlyList<string>> namedArgumentLayouts,
        IReadOnlyList<string> typeMetadata,
        IReadOnlyDictionary<string, GameEventScriptBytecodeCallable> callables,
        IReadOnlyDictionary<string, IReadOnlyList<GameEventScriptBytecodeHandler>> handlers,
        IReadOnlyDictionary<string, GameEventScriptBytecodeTypeDefinition> typeDefinitions,
        IReadOnlyList<GameEventScriptBytecodeInstruction>? code = null,
        int maxFrameSlots = 0,
        IReadOnlyList<GameEventScriptBytecodeOperationLayout>? operationLayouts = null,
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
        StringPool = stringPool ?? throw new ArgumentNullException(nameof(stringPool));
        ConstantPool = constantPool ?? throw new ArgumentNullException(nameof(constantPool));
        Signatures = signatures ?? throw new ArgumentNullException(nameof(signatures));
        ExternalReferences = externalReferences ?? throw new ArgumentNullException(nameof(externalReferences));
        ExternalTypeConstructorReferences = externalTypeConstructorReferences ?? throw new ArgumentNullException(nameof(externalTypeConstructorReferences));
        NamedArgumentLayouts = namedArgumentLayouts ?? throw new ArgumentNullException(nameof(namedArgumentLayouts));
        TypeMetadata = typeMetadata ?? throw new ArgumentNullException(nameof(typeMetadata));
        Callables = callables ?? throw new ArgumentNullException(nameof(callables));
        Handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
        TypeDefinitions = typeDefinitions ?? throw new ArgumentNullException(nameof(typeDefinitions));
        Code = code ?? [];
        MaxFrameSlots = Math.Max(1, maxFrameSlots);
        OperationLayouts = operationLayouts ?? [];
        PublishLayouts = publishLayouts ?? [];
        IterationSourceLayouts = iterationSourceLayouts ?? [];
        LoopLayouts = loopLayouts ?? [];
        SeededRandomBlockLayouts = seededRandomBlockLayouts ?? [];
        DicePatternLayouts = dicePatternLayouts ?? [];
        ObjectMatchPatternLayouts = objectMatchPatternLayouts ?? [];
        SelectorLayouts = selectorLayouts ?? [];
        PipelineLayouts = pipelineLayouts ?? [];
        GeneratedCollectionLayouts = generatedCollectionLayouts ?? [];
        GuardedChoiceLayouts = guardedChoiceLayouts ?? [];
    }

    public GameEventScriptCompileOptions Options { get; }

    public IReadOnlyList<string> StringPool { get; }

    public IReadOnlyList<GameEventScriptBytecodeConstant> ConstantPool { get; }

    public IReadOnlyList<string> Signatures { get; }

    public IReadOnlyList<GameEventScriptExtensionReference> ExternalReferences { get; }

    public IReadOnlyList<GameEventScriptExternalTypeConstructorReference> ExternalTypeConstructorReferences { get; }

    public IReadOnlyList<IReadOnlyList<string>> NamedArgumentLayouts { get; }

    public IReadOnlyList<string> TypeMetadata { get; }

    public IReadOnlyDictionary<string, GameEventScriptBytecodeCallable> Callables { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<GameEventScriptBytecodeHandler>> Handlers { get; }

    public IReadOnlyDictionary<string, GameEventScriptBytecodeTypeDefinition> TypeDefinitions { get; }

    public IReadOnlyList<GameEventScriptBytecodeInstruction> Code { get; }

    public int MaxFrameSlots { get; }

    public IReadOnlyList<GameEventScriptBytecodeOperationLayout> OperationLayouts { get; }

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
}
