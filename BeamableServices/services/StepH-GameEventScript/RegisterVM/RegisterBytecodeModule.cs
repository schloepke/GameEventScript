#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.RegisterVM;

internal sealed class RegisterBytecodeModule(
    RegisterVmCompilationOptions options,
    IReadOnlyList<string> stringPool,
    IReadOnlyList<GseValue> constantPool,
    IReadOnlyList<string> signatures,
    IReadOnlyList<GseExtensionReference> externalReferences,
    IReadOnlyList<IReadOnlyList<string>> namedArgumentLayouts,
    IReadOnlyList<string> typeMetadata,
    IReadOnlyList<RegisterProgram> programs)
{
    public RegisterVmCompilationOptions Options { get; } = options ?? throw new ArgumentNullException(nameof(options));

    public IReadOnlyList<string> StringPool { get; } = stringPool ?? throw new ArgumentNullException(nameof(stringPool));

    public IReadOnlyList<GseValue> ConstantPool { get; } = constantPool ?? throw new ArgumentNullException(nameof(constantPool));

    public IReadOnlyList<string> Signatures { get; } = signatures ?? throw new ArgumentNullException(nameof(signatures));

    public IReadOnlyList<GseExtensionReference> ExternalReferences { get; } = externalReferences ?? throw new ArgumentNullException(nameof(externalReferences));

    public IReadOnlyList<IReadOnlyList<string>> NamedArgumentLayouts { get; } = namedArgumentLayouts ?? throw new ArgumentNullException(nameof(namedArgumentLayouts));

    public IReadOnlyList<string> TypeMetadata { get; } = typeMetadata ?? throw new ArgumentNullException(nameof(typeMetadata));

    public IReadOnlyList<RegisterProgram> Programs { get; } = programs ?? throw new ArgumentNullException(nameof(programs));
}
