// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text;

namespace GameEventScript.Api;

/// <summary>
/// Represents an exception that occurs during the compilation process of a Game Event Script.
/// This exception is specifically used to signal issues detected during the parsing or
/// validation phase of script compilation.
/// </summary>
/// <remarks>
/// This class provides constructors to initialize the exception with a detailed error
/// message or a collection of compilation errors. It serves as a mechanism for propagating
/// critical issues encountered when building or validating a Game Event Script.
/// </remarks>
public class GameEventScriptCompileException : Exception
{
    /// <summary>
    /// Represents an exception thrown when the Game Event Script compilation process encounters an error.
    /// </summary>
    public GameEventScriptCompileException(GameEventScriptDiagnostic diagnostic) : base(RequireDiagnostic(diagnostic).Message)
    {
        if (diagnostic.Phase is not (GameEventScriptDiagnosticPhase.Parse or GameEventScriptDiagnosticPhase.Validate or GameEventScriptDiagnosticPhase.Compile))
            throw new ArgumentException("A compile exception requires a parse, validate, or compile diagnostic.", nameof(diagnostic));
        Diagnostics = [diagnostic];
    }

    /// <summary>
    /// Represents an exception thrown when a compilation error occurs in a Game Event Script.
    /// </summary>
    /// <remarks>
    /// This exception is used to indicate critical issues during the compilation process,
    /// such as syntax errors, validation failures, or other specific problems that prevent
    /// successful script processing. It supports detailed error reporting by encapsulating
    /// a collection of compilation errors.
    /// </remarks>
    public GameEventScriptCompileException(IReadOnlyList<GameEventScriptDiagnostic> diagnostics) : base(BuildMessage(diagnostics))
    {
        _ = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        var copy = new GameEventScriptDiagnostic[diagnostics.Count];
        for (var index = 0; index < copy.Length; index++)
        {
            var diagnostic = diagnostics[index] ?? throw new ArgumentException("Diagnostic list contains null.", nameof(diagnostics));
            if (diagnostic.Phase is not (GameEventScriptDiagnosticPhase.Parse or GameEventScriptDiagnosticPhase.Validate or GameEventScriptDiagnosticPhase.Compile))
                throw new ArgumentException("A compile exception requires parse, validate, or compile diagnostics.", nameof(diagnostics));
            copy[index] = diagnostic;
        }

        Diagnostics = copy;
    }

    /// <summary>
    /// Gets a collection of compiler errors encountered during the processing of a game event script.
    /// </summary>
    /// <remarks>
    /// The immutable snapshot contains portable phase/code data and optional
    /// symbol/source/program context for each compiler error.
    /// </remarks>
    public IReadOnlyList<GameEventScriptDiagnostic> Diagnostics { get; }

    private static GameEventScriptDiagnostic RequireDiagnostic(GameEventScriptDiagnostic? diagnostic)
        => diagnostic ?? throw new ArgumentNullException(nameof(diagnostic));

    private static string BuildMessage(IReadOnlyList<GameEventScriptDiagnostic>? errors)
    {
        if (errors is null || errors.Count == 0)
        {
            return "GameEventScript compilation failed.";
        }

        var builder = new StringBuilder();
        builder
            .Append("GameEventScript compilation failed with ")
            .Append(errors.Count)
            .Append(" error(s):");
        for (var index = 0; index < errors.Count; index++)
        {
            builder
                .Append(Environment.NewLine)
                .Append("- ")
                .Append(errors[index]);
        }

        return builder.ToString();
    }
}
