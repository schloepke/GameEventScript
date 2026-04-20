#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.Flow.EventScript.Interpreter;
using StepH.Flow.EventScript.Linker;
using StepH.Flow.EventScript.Parser;

namespace StepH.Flow.EventScript;

public static class EventScriptManager
{
    public static CompiledEventScript Compile(EventScriptInterpreterCompilationOptions? options = null, IEventScriptRandom? defaultRandom = null, params string[] inputs)
        => new(EventScriptLinkBuilder.LinkModules(ParseModules(inputs)), options, defaultRandom);
    
    public static CompiledEventScript Compile(string input, EventScriptInterpreterCompilationOptions options, IEventScriptRandom? defaultRandom = null)
        => new(EventScriptLinkBuilder.LinkModules(ParseModule(input)), options, defaultRandom);

    public static CompiledEventScript Compile(string input, IEventScriptRandom defaultRandom)
        => new(EventScriptLinkBuilder.LinkModules(ParseModule(input)), null, defaultRandom);
    
    public static CompiledEventScript Compile(string input)
        => new(EventScriptLinkBuilder.LinkModules(ParseModule(input)), null, null);
    
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
    DuplicatePublishArgument
}

public enum EventScriptSymbolKind
{
    Type,
    Rule,
    Select,
    Handler,
    GlobalDefinition
}

public enum EventScriptSyntaxErrorKind
{
    Lexer,
    Parser
}

public sealed record EventScriptSourceLocation(string SourceName, int? Line = null, int? Column = null)
{
    public override string ToString() => Line is null && Column is null ? SourceName : Column is null ? $"{SourceName} (line {Line})" : $"{SourceName} (line {Line}, col {Column})";
}