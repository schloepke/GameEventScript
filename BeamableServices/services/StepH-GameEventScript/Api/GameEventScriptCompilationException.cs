using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Compiler;

namespace StepH.GameEventScript.Api;

/// <summary>
/// Represents an exception that occurs during the compilation of GameEventScript.
/// </summary>
public class GameEventScriptCompilationException(string message) : Exception(message);

/// <summary>
/// Represents an exception that occurs during the syntax analysis of GameEventScript.
/// </summary>
public sealed class GameEventScriptSyntaxException(IReadOnlyList<GameEventScriptSyntaxError> errors) : GameEventScriptCompilationException(BuildMessage(errors))
{
    /// <summary>
    /// Retrieves the collection of syntax errors identified during the GameEventScript analysis.
    /// </summary>
    /// <remarks>
    /// This collection contains all the syntax-related issues encountered in the parsing phase of the script,
    /// with each error providing details such as the associated module, message, and specific source location.
    /// The property cannot be null and ensures access to all errors relevant to the detected syntax anomalies.
    /// </remarks>
    public IReadOnlyList<GameEventScriptSyntaxError> Errors { get; } = errors ?? throw new ArgumentNullException(nameof(errors));

    private static string BuildMessage(IReadOnlyList<GameEventScriptSyntaxError> errors)
        => errors.Count == 0
            ? "GameEventScript syntax analysis failed."
            : $"GameEventScript syntax analysis failed with {errors.Count} error(s):{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", errors.Select(it => it.ToString()))}";
}

/// <summary>
/// Represents an exception that occurs during the build process of a GameEventScript module.
/// </summary>
public sealed class GameEventScriptModuleBuildException(IReadOnlyList<GameEventScriptModuleBuildError> errors) : GameEventScriptCompilationException(BuildMessage(errors))
{
    /// <summary>
    /// Gets the collection of errors that occurred during the GameEventScript module build process.
    /// </summary>
    /// <remarks>
    /// Each item in the collection represents a specific build error, providing details including
    /// the module name, symbol, kind of error, and its source location. This property is guaranteed
    /// to be non-null and will contain all the errors associated with the current module build exception.
    /// </remarks>
    public IReadOnlyList<GameEventScriptModuleBuildError> Errors { get; } = errors ?? throw new ArgumentNullException(nameof(errors));

    private static string BuildMessage(IReadOnlyList<GameEventScriptModuleBuildError> errors)
        => errors.Count == 0
            ? "GameEventScript module build failed."
            : $"GameEventScript module build failed with {errors.Count} error(s):{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", errors.Select(it => it.ToString()))}";
}