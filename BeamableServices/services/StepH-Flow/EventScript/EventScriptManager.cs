#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.Flow.EventScript.Linker;
using StepH.Flow.EventScript.Parser;
using StepH.Flow.EventScript.RegisterVM;

namespace StepH.Flow.EventScript;

public static class EventScriptManager
{
    public static RegisterCompiledEventScript Compile(RegisterEventScriptCompilationOptions? options = null, params string[] inputs)
        => RegisterEventScriptCompiler.Compile(EventScriptLinkBuilder.LinkModules(ParseModules(inputs)), options);
    
    public static RegisterCompiledEventScript Compile(string input, RegisterEventScriptCompilationOptions? options = null)
        => RegisterEventScriptCompiler.Compile(EventScriptLinkBuilder.LinkModules(ParseModule(input)), options);

    public static EventScriptModule ParseModule(string input, string? sourceName = null) => EventScriptParser.Parse(input, sourceName);

    public static IReadOnlyList<EventScriptModule> ParseModules(params string[] input) => input.Select(script => EventScriptParser.Parse(script)).ToList();

    public static LinkedEventScriptModule LinkScripts(params string[] inputs) => EventScriptLinkBuilder.LinkModules(ParseModules(inputs));

    public static LinkedEventScriptModule LinkModules(params EventScriptModule[] inputs) => EventScriptLinkBuilder.LinkModules(inputs);

}

// From here error / exception structure for compiling / linking etc.

public class EventScriptCompilationException(string message) : Exception(message);

public sealed class EventScriptSyntaxException(IReadOnlyList<EventScriptSyntaxError> errors) : EventScriptCompilationException(BuildMessage(errors))
{
    public IReadOnlyList<EventScriptSyntaxError> Errors { get; } = errors ?? throw new ArgumentNullException(nameof(errors));

    private static string BuildMessage(IReadOnlyList<EventScriptSyntaxError> errors)
        => errors.Count == 0
            ? "EventScript syntax analysis failed."
            : $"EventScript syntax analysis failed with {errors.Count} error(s):{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", errors.Select(it => it.ToString()))}";
}

public sealed class EventScriptLinkageException(IReadOnlyList<EventScriptLinkageError> errors) : EventScriptCompilationException(BuildMessage(errors))
{
    public IReadOnlyList<EventScriptLinkageError> Errors { get; } = errors ?? throw new ArgumentNullException(nameof(errors));

    private static string BuildMessage(IReadOnlyList<EventScriptLinkageError> errors)
        => errors.Count == 0
            ? "EventScript linkage failed."
            : $"EventScript linkage failed with {errors.Count} error(s):{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", errors.Select(it => it.ToString()))}";
}

public sealed record EventScriptSyntaxError(
    string Message,
    string ModuleName,
    EventScriptSyntaxErrorKind Kind,
    EventScriptSourceLocation SourceLocation)
{
    public override string ToString() => $"Module '{ModuleName}': {Message} [{SourceLocation}]";
}

public sealed record EventScriptLinkageError(
    string Message,
    string ModuleName,
    string Symbol,
    EventScriptSymbolKind SymbolKind,
    EventScriptLinkageErrorKind Kind,
    EventScriptSourceLocation SourceLocation)
{
    public override string ToString() => $"Module '{ModuleName}': {Message} [{SourceLocation}]";
}

public enum EventScriptLinkageErrorKind
{
    DuplicateType,
    DuplicateRule,
    DuplicateSelect,
    RuleSelectConflict,
    MissingRuleOrSelect,
    InvalidRulePredicate,
    WrongRuleArity,
    WrongSelectArity,
    DuplicateHandlerParameter,
    DuplicateDefinitionParameter,
    DuplicatePublishArgument,
    DuplicateVariable,
    InvalidIdentifierCase,
    InvalidMessageCase,
    InvalidTypeConstructor
}

public enum EventScriptSymbolKind
{
    Type,
    Rule,
    Select,
    Handler,
    Message,
    Variable,
    GlobalDefinition
}

public enum EventScriptSyntaxErrorKind
{
    Lexer,
    Parser
}

public sealed record EventScriptSourceLocation(
    string SourceName,
    int? Line = null,
    int? Column = null,
    int? EndLine = null,
    int? EndColumn = null,
    string ModuleName = "UnknownModule")
{
    public override string ToString()
    {
        var locationName = string.IsNullOrWhiteSpace(ModuleName)
            ? SourceName
            : $"{ModuleName}@{SourceName}";

        if (Line is null && Column is null)
        {
            return locationName;
        }

        if (Column is null)
        {
            return $"{locationName} (line {Line})";
        }

        if (EndLine is null && EndColumn is null)
        {
            return $"{locationName} (line {Line}, col {Column})";
        }

        if (EndLine == Line && EndColumn == Column)
        {
            return $"{locationName} (line {Line}, col {Column})";
        }

        if (EndLine == Line)
        {
            return $"{locationName} (line {Line}, col {Column}-{EndColumn})";
        }

        return $"{locationName} (line {Line}, col {Column} to line {EndLine}, col {EndColumn})";
    }
}
