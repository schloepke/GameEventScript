#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.Compiler;

public sealed class GameEventScriptBytecode
{
    internal GameEventScriptBytecode(
        GameEventScriptCompilationOptions options,
        IReadOnlyList<string> stringPool,
        IReadOnlyList<GameEventScriptValue> constantPool,
        IReadOnlyList<string> signatures,
        IReadOnlyList<GameEventScriptExtensionReference> externalReferences,
        IReadOnlyList<IReadOnlyList<string>> namedArgumentLayouts,
        IReadOnlyList<string> typeMetadata,
        IReadOnlyList<GameEventScriptBytecodeProgram> programs,
        GameEventScriptModule? sourceModule = null)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        StringPool = stringPool ?? throw new ArgumentNullException(nameof(stringPool));
        ConstantPool = constantPool ?? throw new ArgumentNullException(nameof(constantPool));
        Signatures = signatures ?? throw new ArgumentNullException(nameof(signatures));
        ExternalReferences = externalReferences ?? throw new ArgumentNullException(nameof(externalReferences));
        NamedArgumentLayouts = namedArgumentLayouts ?? throw new ArgumentNullException(nameof(namedArgumentLayouts));
        TypeMetadata = typeMetadata ?? throw new ArgumentNullException(nameof(typeMetadata));
        Programs = programs ?? throw new ArgumentNullException(nameof(programs));
        SourceModule = sourceModule;
    }

    public GameEventScriptCompilationOptions Options { get; }

    public IReadOnlyList<string> StringPool { get; }

    public IReadOnlyList<GameEventScriptValue> ConstantPool { get; }

    public IReadOnlyList<string> Signatures { get; }

    public IReadOnlyList<GameEventScriptExtensionReference> ExternalReferences { get; }

    public IReadOnlyList<IReadOnlyList<string>> NamedArgumentLayouts { get; }

    public IReadOnlyList<string> TypeMetadata { get; }

    public IReadOnlyList<GameEventScriptBytecodeProgram> Programs { get; }

    internal GameEventScriptModule? SourceModule { get; }
}
