#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;

namespace StepH.GameEventScript.Api;

public class GameEventScriptCompileException : Exception
{
    public GameEventScriptCompileException(string message) : base(message)
    {
        Errors = [];
    }

    public GameEventScriptCompileException(IReadOnlyList<GameEventScriptCompileError> errors) : base(BuildMessage(errors))
    {
        Errors = errors ?? throw new ArgumentNullException(nameof(errors));
    }

    public IReadOnlyList<GameEventScriptCompileError> Errors { get; }

    private static string BuildMessage(IReadOnlyList<GameEventScriptCompileError>? errors)
        => errors is null || errors.Count == 0
            ? "GameEventScript compilation failed."
            : $"GameEventScript compilation failed with {errors.Count} error(s):{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", errors.Select(it => it.ToString()))}";
}

public sealed record GameEventScriptCompileError(
    string Message,
    string ModuleName,
    string Symbol,
    GameEventScriptSymbolKind SymbolKind,
    GameEventScriptCompileErrorKind Kind,
    GameEventScriptSourceLocation SourceLocation)
{
    public override string ToString() => $"Module '{ModuleName}': {Message} [{SourceLocation}]";
}

public enum GameEventScriptCompileErrorKind
{
    Syntax,
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

public enum GameEventScriptSymbolKind
{
    Unknown,
    Type,
    Rule,
    Select,
    Handler,
    Message,
    Variable,
    GlobalDefinition
}
