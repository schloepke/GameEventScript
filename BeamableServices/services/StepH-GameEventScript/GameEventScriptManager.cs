#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Linker;
using StepH.GameEventScript.Parser;
using StepH.GameEventScript.RegisterVM;
using GseLinkBuilder = StepH.GameEventScript.Linker.GseLinkBuilder;

namespace StepH.GameEventScript;

public static class GameEventScriptManager
{
    public static RegisterCompiledGse Compile(RegisterGseCompilationOptions? options = null, params string[] inputs)
        => RegisterGseCompiler.Compile(GseLinkBuilder.LinkModules(ParseModules(inputs)), options);
    
    public static RegisterCompiledGse Compile(string input, RegisterGseCompilationOptions? options = null)
        => RegisterGseCompiler.Compile(GseLinkBuilder.LinkModules(ParseModule(input)), options);

    public static GseModule ParseModule(string input, string? sourceName = null) => GseParser.Parse(input, sourceName);

    public static IReadOnlyList<GseModule> ParseModules(params string[] input) => input.Select(script => GseParser.Parse(script)).ToList();

    public static LinkedGseModule LinkScripts(params string[] inputs) => GseLinkBuilder.LinkModules(ParseModules(inputs));

    public static LinkedGseModule LinkModules(params GseModule[] inputs) => GseLinkBuilder.LinkModules(inputs);

}

// From here error / exception structure for compiling / linking etc.

public class GseCompilationException(string message) : Exception(message);

public sealed class GseSyntaxException(IReadOnlyList<GseSyntaxError> errors) : GseCompilationException(BuildMessage(errors))
{
    public IReadOnlyList<GseSyntaxError> Errors { get; } = errors ?? throw new ArgumentNullException(nameof(errors));

    private static string BuildMessage(IReadOnlyList<GseSyntaxError> errors)
        => errors.Count == 0
            ? "GameEventScript syntax analysis failed."
            : $"GameEventScript syntax analysis failed with {errors.Count} error(s):{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", errors.Select(it => it.ToString()))}";
}

public sealed class GseLinkageException(IReadOnlyList<GseLinkageError> errors) : GseCompilationException(BuildMessage(errors))
{
    public IReadOnlyList<GseLinkageError> Errors { get; } = errors ?? throw new ArgumentNullException(nameof(errors));

    private static string BuildMessage(IReadOnlyList<GseLinkageError> errors)
        => errors.Count == 0
            ? "GameEventScript linkage failed."
            : $"GameEventScript linkage failed with {errors.Count} error(s):{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", errors.Select(it => it.ToString()))}";
}

public sealed record GseSyntaxError(
    string Message,
    string ModuleName,
    GseSyntaxErrorKind Kind,
    GseSourceLocation SourceLocation)
{
    public override string ToString() => $"Module '{ModuleName}': {Message} [{SourceLocation}]";
}

public sealed record GseLinkageError(
    string Message,
    string ModuleName,
    string Symbol,
    GseSymbolKind SymbolKind,
    GseLinkageErrorKind Kind,
    GseSourceLocation SourceLocation)
{
    public override string ToString() => $"Module '{ModuleName}': {Message} [{SourceLocation}]";
}

public enum GseLinkageErrorKind
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

public enum GseSymbolKind
{
    Type,
    Rule,
    Select,
    Handler,
    Message,
    Variable,
    GlobalDefinition
}

public enum GseSyntaxErrorKind
{
    Syntax
}

public sealed record GseSourceLocation(
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
