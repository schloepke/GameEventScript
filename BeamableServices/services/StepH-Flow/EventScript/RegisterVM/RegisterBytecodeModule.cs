#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using StepH.Flow.EventScript.Types;

namespace StepH.Flow.EventScript.RegisterVM;

internal sealed class RegisterBytecodeModule(
    RegisterEventScriptCompilationOptions options,
    IReadOnlyList<string> stringPool,
    IReadOnlyList<EventScriptValue> constantPool,
    IReadOnlyList<string> signatures,
    IReadOnlyList<IReadOnlyList<string>> namedArgumentLayouts,
    IReadOnlyList<string> typeMetadata,
    IReadOnlyList<RegisterProgram> programs)
{
    public RegisterEventScriptCompilationOptions Options { get; } = options ?? throw new ArgumentNullException(nameof(options));

    public IReadOnlyList<string> StringPool { get; } = stringPool ?? throw new ArgumentNullException(nameof(stringPool));

    public IReadOnlyList<EventScriptValue> ConstantPool { get; } = constantPool ?? throw new ArgumentNullException(nameof(constantPool));

    public IReadOnlyList<string> Signatures { get; } = signatures ?? throw new ArgumentNullException(nameof(signatures));

    public IReadOnlyList<IReadOnlyList<string>> NamedArgumentLayouts { get; } = namedArgumentLayouts ?? throw new ArgumentNullException(nameof(namedArgumentLayouts));

    public IReadOnlyList<string> TypeMetadata { get; } = typeMetadata ?? throw new ArgumentNullException(nameof(typeMetadata));

    public IReadOnlyList<RegisterProgram> Programs { get; } = programs ?? throw new ArgumentNullException(nameof(programs));
}
