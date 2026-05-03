#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using StepH.GameEventScript.BytecodeVM;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.Api;

public sealed class GameEventScriptCompiled
{
    internal GameEventScriptCompiled(
        GameEventScriptCompileOptions options,
        IReadOnlyList<string> stringPool,
        IReadOnlyList<GameEventScriptValue> constantPool,
        IReadOnlyList<string> signatures,
        IReadOnlyList<GameEventScriptExtensionReference> externalReferences,
        IReadOnlyList<IReadOnlyList<string>> namedArgumentLayouts,
        IReadOnlyList<string> typeMetadata,
        IReadOnlyList<GameEventScriptBytecodeProgram> programs,
        IReadOnlyDictionary<string, IReadOnlyList<BytecodeVmCompiledHandler>> handlers,
        IReadOnlyDictionary<string, BytecodeVmTypeDefinition> typeDefinitions,
        int maxStackDepth)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        StringPool = stringPool ?? throw new ArgumentNullException(nameof(stringPool));
        ConstantPool = constantPool ?? throw new ArgumentNullException(nameof(constantPool));
        Signatures = signatures ?? throw new ArgumentNullException(nameof(signatures));
        ExternalReferences = externalReferences ?? throw new ArgumentNullException(nameof(externalReferences));
        NamedArgumentLayouts = namedArgumentLayouts ?? throw new ArgumentNullException(nameof(namedArgumentLayouts));
        TypeMetadata = typeMetadata ?? throw new ArgumentNullException(nameof(typeMetadata));
        Programs = programs ?? throw new ArgumentNullException(nameof(programs));
        Handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
        TypeDefinitions = typeDefinitions ?? throw new ArgumentNullException(nameof(typeDefinitions));
        MaxStackDepth = Math.Max(1, maxStackDepth);
    }

    public GameEventScriptCompileOptions Options { get; }

    public IReadOnlyList<string> StringPool { get; }

    public IReadOnlyList<GameEventScriptValue> ConstantPool { get; }

    public IReadOnlyList<string> Signatures { get; }

    public IReadOnlyList<GameEventScriptExtensionReference> ExternalReferences { get; }

    public IReadOnlyList<IReadOnlyList<string>> NamedArgumentLayouts { get; }

    public IReadOnlyList<string> TypeMetadata { get; }

    public IReadOnlyList<GameEventScriptBytecodeProgram> Programs { get; }

    internal IReadOnlyDictionary<string, IReadOnlyList<BytecodeVmCompiledHandler>> Handlers { get; }

    internal IReadOnlyDictionary<string, BytecodeVmTypeDefinition> TypeDefinitions { get; }

    internal int MaxStackDepth { get; }
}
