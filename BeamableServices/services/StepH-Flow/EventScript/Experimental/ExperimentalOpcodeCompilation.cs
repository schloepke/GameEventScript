#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;

namespace StepH.Flow.EventScript.Experimental;

public enum EventScriptOpcodeCompilationErrorKind
{
    InvalidInput,
    DuplicateDefinitionParameter,
    DuplicateHandlerParameter,
    DuplicatePublishArgument,
    DuplicateVariable,
    UnsupportedSyntax,
    InternalCompilerError
}

public sealed record EventScriptOpcodeCompilationError(
    string Message,
    string ModuleName,
    string Symbol,
    EventScriptSymbolKind SymbolKind,
    EventScriptOpcodeCompilationErrorKind Kind,
    EventScriptSourceLocation SourceLocation)
{
    public override string ToString() => $"Module '{ModuleName}': {Message} [{SourceLocation}]";
}

public sealed class EventScriptOpcodeCompilationException(IReadOnlyList<EventScriptOpcodeCompilationError> errors)
    : EventScriptCompilationException(BuildMessage(errors))
{
    public IReadOnlyList<EventScriptOpcodeCompilationError> Errors { get; } =
        errors ?? throw new ArgumentNullException(nameof(errors));

    private static string BuildMessage(IReadOnlyList<EventScriptOpcodeCompilationError> errors)
        => errors.Count == 0
            ? "EventScript opcode compilation failed."
            : $"EventScript opcode compilation failed with {errors.Count} error(s):{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", errors.Select(it => it.ToString()))}";
}
