// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text;

// ReSharper disable UnusedMember.Global

namespace StepH.GameEventScript.Api;

/// <summary>
/// Defines the supported game event script diagnostic phase values.
/// </summary>
public enum GameEventScriptDiagnosticPhase
{
    /// <summary>
    /// Identifies the parse value.
    /// </summary>
    Parse = 1,
    /// <summary>
    /// Identifies the validate value.
    /// </summary>
    Validate = 2,
    /// <summary>
    /// Identifies the compile value.
    /// </summary>
    Compile = 3,
    /// <summary>
    /// Identifies the decode value.
    /// </summary>
    Decode = 4,
    /// <summary>
    /// Identifies the link value.
    /// </summary>
    Link = 5,
    /// <summary>
    /// Identifies the runtime value.
    /// </summary>
    Runtime = 6
}

/// <summary>
/// Represents a game event script diagnostic codes.
/// </summary>
public static class GameEventScriptDiagnosticCodes
{
    /// <summary>
    /// Defines the parse syntax value.
    /// </summary>
    public const string ParseSyntax = "parse.syntax";
    /// <summary>
    /// Defines the validate duplicate type value.
    /// </summary>
    public const string ValidateDuplicateType = "validate.duplicateType";
    /// <summary>
    /// Defines the validate duplicate predicate value.
    /// </summary>
    public const string ValidateDuplicatePredicate = "validate.duplicatePredicate";
    /// <summary>
    /// Defines the validate duplicate function value.
    /// </summary>
    public const string ValidateDuplicateFunction = "validate.duplicateFunction";
    /// <summary>
    /// Defines the validate predicate function conflict value.
    /// </summary>
    public const string ValidatePredicateFunctionConflict = "validate.predicateFunctionConflict";
    /// <summary>
    /// Defines the validate missing callable value.
    /// </summary>
    public const string ValidateMissingCallable = "validate.missingCallable";
    /// <summary>
    /// Defines the validate invalid predicate value.
    /// </summary>
    public const string ValidateInvalidPredicate = "validate.invalidPredicate";
    /// <summary>
    /// Defines the validate wrong predicate arity value.
    /// </summary>
    public const string ValidateWrongPredicateArity = "validate.wrongPredicateArity";
    /// <summary>
    /// Defines the validate wrong function arity value.
    /// </summary>
    public const string ValidateWrongFunctionArity = "validate.wrongFunctionArity";
    /// <summary>
    /// Defines the validate duplicate handler parameter value.
    /// </summary>
    public const string ValidateDuplicateHandlerParameter = "validate.duplicateHandlerParameter";
    /// <summary>
    /// Defines the validate duplicate definition parameter value.
    /// </summary>
    public const string ValidateDuplicateDefinitionParameter = "validate.duplicateDefinitionParameter";
    /// <summary>
    /// Defines the validate duplicate publish argument value.
    /// </summary>
    public const string ValidateDuplicatePublishArgument = "validate.duplicatePublishArgument";
    /// <summary>
    /// Defines the validate duplicate variable value.
    /// </summary>
    public const string ValidateDuplicateVariable = "validate.duplicateVariable";
    /// <summary>
    /// Defines the validate shadowed variable value.
    /// </summary>
    public const string ValidateShadowedVariable = "validate.shadowedVariable";
    /// <summary>
    /// Defines the validate invalid identifier case value.
    /// </summary>
    public const string ValidateInvalidIdentifierCase = "validate.invalidIdentifierCase";
    /// <summary>
    /// Defines the validate invalid message case value.
    /// </summary>
    public const string ValidateInvalidMessageCase = "validate.invalidMessageCase";
    /// <summary>
    /// Defines the validate invalid type constructor value.
    /// </summary>
    public const string ValidateInvalidTypeConstructor = "validate.invalidTypeConstructor";
    /// <summary>
    /// Defines the compile unsupported construct value.
    /// </summary>
    public const string CompileUnsupportedConstruct = "compile.unsupportedConstruct";
    /// <summary>
    /// Defines the compile invalid arity value.
    /// </summary>
    public const string CompileInvalidArity = "compile.invalidArity";
    /// <summary>
    /// Defines the compile unresolved symbol value.
    /// </summary>
    public const string CompileUnresolvedSymbol = "compile.unresolvedSymbol";
    /// <summary>
    /// Defines the compile numeric limit exceeded value.
    /// </summary>
    public const string CompileNumericLimitExceeded = "compile.numericLimitExceeded";
    /// <summary>
    /// Defines the compile cyclic call graph value.
    /// </summary>
    public const string CompileCyclicCallGraph = "compile.cyclicCallGraph";
    /// <summary>
    /// Defines the compile invalid resource metadata value.
    /// </summary>
    public const string CompileInvalidResourceMetadata = "compile.invalidResourceMetadata";
    /// <summary>
    /// Defines the compile invariant violation value.
    /// </summary>
    public const string CompileInvariantViolation = "compile.invariantViolation";
    /// <summary>
    /// Defines the link required register count exceeded value.
    /// </summary>
    public const string LinkRequiredRegisterCountExceeded = "link.requiredRegisterCountExceeded";
    /// <summary>
    /// Defines the link required call stack depth exceeded value.
    /// </summary>
    public const string LinkRequiredCallStackDepthExceeded = "link.requiredCallStackDepthExceeded";
    /// <summary>
    /// Defines the link invalid extension reference value.
    /// </summary>
    public const string LinkInvalidExtensionReference = "link.invalidExtensionReference";
    /// <summary>
    /// Defines the link missing extension value.
    /// </summary>
    public const string LinkMissingExtension = "link.missingExtension";
    /// <summary>
    /// Defines the link missing external type constructor value.
    /// </summary>
    public const string LinkMissingExternalTypeConstructor = "link.missingExternalTypeConstructor";
    /// <summary>
    /// Defines the link mismatched external type constructor value.
    /// </summary>
    public const string LinkMismatchedExternalTypeConstructor = "link.mismatchedExternalTypeConstructor";
    /// <summary>
    /// Defines the link cyclic call graph value.
    /// </summary>
    public const string LinkCyclicCallGraph = "link.cyclicCallGraph";
    /// <summary>
    /// Defines the link invalid program value.
    /// </summary>
    public const string LinkInvalidProgram = "link.invalidProgram";
    /// <summary>
    /// Defines the runtime vm state conflict value.
    /// </summary>
    public const string RuntimeVmStateConflict = "runtime.vmStateConflict";
    /// <summary>
    /// Defines the runtime preparation failed value.
    /// </summary>
    public const string RuntimePreparationFailed = "runtime.preparationFailed";
    /// <summary>
    /// Defines the runtime instruction pointer out of range value.
    /// </summary>
    public const string RuntimeInstructionPointerOutOfRange = "runtime.instructionPointerOutOfRange";
    /// <summary>
    /// Defines the runtime illegal opcode value.
    /// </summary>
    public const string RuntimeIllegalOpcode = "runtime.illegalOpcode";
    /// <summary>
    /// Defines the runtime register overflow value.
    /// </summary>
    public const string RuntimeRegisterOverflow = "runtime.registerOverflow";
    /// <summary>
    /// Defines the runtime call stack overflow value.
    /// </summary>
    public const string RuntimeCallStackOverflow = "runtime.callStackOverflow";
    /// <summary>
    /// Defines the runtime random stack underflow value.
    /// </summary>
    public const string RuntimeRandomStackUnderflow = "runtime.randomStackUnderflow";
    /// <summary>
    /// Defines the runtime random scope imbalance value.
    /// </summary>
    public const string RuntimeRandomScopeImbalance = "runtime.randomScopeImbalance";
    /// <summary>
    /// Defines the runtime invalid record constructor value.
    /// </summary>
    public const string RuntimeInvalidRecordConstructor = "runtime.invalidRecordConstructor";
    /// <summary>
    /// Defines the runtime invalid extension binding value.
    /// </summary>
    public const string RuntimeInvalidExtensionBinding = "runtime.invalidExtensionBinding";
    /// <summary>
    /// Defines the runtime invalid external type binding value.
    /// </summary>
    public const string RuntimeInvalidExternalTypeBinding = "runtime.invalidExternalTypeBinding";
    /// <summary>
    /// Defines the runtime invalid message shape value.
    /// </summary>
    public const string RuntimeInvalidMessageShape = "runtime.invalidMessageShape";
    /// <summary>
    /// Defines the runtime invalid series kind value.
    /// </summary>
    public const string RuntimeInvalidSeriesKind = "runtime.invalidSeriesKind";
    /// <summary>
    /// Defines the runtime unhandled failure value.
    /// </summary>
    public const string RuntimeUnhandledFailure = "runtime.unhandledFailure";
    /// <summary>
    /// Defines the runtime native handler failure value.
    /// </summary>
    public const string RuntimeNativeHandlerFailure = "runtime.nativeHandlerFailure";
    /// <summary>
    /// Defines the runtime publish sink failure value.
    /// </summary>
    public const string RuntimePublishSinkFailure = "runtime.publishSinkFailure";

    /// <summary>
    /// Performs the decode operation.
    /// </summary>
    /// <param name="code">The code value.</param>
    /// <returns>The result of the operation.</returns>
    public static string Decode(GameEventScriptProgramFormatErrorCode code)
    {
        var name = code.ToString();
        return "decode." + char.ToLowerInvariant(name[0]) + name[1..];
    }
}

/// <summary>
/// Represents a game event script diagnostic.
/// </summary>
/// <param name="Phase">The phase value.</param>
/// <param name="Code">The code value.</param>
/// <param name="Message">The message value.</param>
/// <param name="Symbol">The symbol value.</param>
/// <param name="SymbolKind">The symbol kind value.</param>
/// <param name="SourceLocation">The source location value.</param>
/// <param name="ProgramName">The program name value.</param>
/// <param name="HandlerName">The handler name value.</param>
/// <param name="TechnicalDetails">The technical details value.</param>
public sealed record GameEventScriptDiagnostic(
    GameEventScriptDiagnosticPhase Phase,
    string Code,
    string Message,
    string? Symbol = null,
    GameEventScriptSymbolKind SymbolKind = GameEventScriptSymbolKind.Unknown,
    GameEventScriptSourceLocation? SourceLocation = null,
    string? ProgramName = null,
    string? HandlerName = null,
    string? TechnicalDetails = null)
{
    /// <summary>
    /// Returns the to string result for this value.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public override string ToString() => $"[{Code}] {Message}";
}

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

/// <summary>
/// Defines the different kinds of symbols used within a Game Event Script.
/// This enumeration is used to classify the functional or semantic roles
/// of various components in a script, enabling the compiler and validation logic
/// to process and verify them appropriately.
/// </summary>
/// <remarks>
/// Each value in this enum corresponds to a distinct type of script element,
/// such as type definitions, predicates, functions, event handlers, or variables. These classifications
/// are critical for parsing, symbol resolution, and error reporting during
/// the compilation and validation phases of Game Event Scripts.
/// </remarks>
public enum GameEventScriptSymbolKind
{
    /// <summary>
    /// Represents an unidentified or unclassified symbol encountered during the processing of a Game Event Script.
    /// </summary>
    /// <remarks>
    /// This value indicates that the symbol does not match any predefined category within the GameEventScriptSymbolKind enum.
    /// It is commonly used as a fallback or default classification when a symbol cannot be resolved or identified.
    /// This may occur due to incomplete, missing, or corrupted script content.
    /// </remarks>
    Unknown,

    /// <summary>
    /// Represents a type definition within a Game Event Script.
    /// </summary>
    /// <remarks>
    /// This symbol kind is used to identify custom type declarations or
    /// references to predefined types within the script. Type definitions
    /// play a key role in dictating the structure, relationships, and constraints
    /// of data used across the script. Ensuring proper type declaration and
    /// usage is crucial for successful compilation and runtime behavior.
    /// </remarks>
    Type,

    /// <summary>
    /// Represents a predicate defined within a Game Event Script.
    /// </summary>
    /// <remarks>
    /// A predicate specifies reusable boolean logic within the script.
    /// </remarks>
    Predicate,

    /// <summary>
    /// Represents a function defined within a Game Event Script.
    /// </summary>
    /// <remarks>
    /// A function specifies reusable value-producing logic within the script.
    /// </remarks>
    Function,

    /// <summary>
    /// Represents a handler definition within a Game Event Script.
    /// </summary>
    /// <remarks>
    /// A handler specifies the logic or actions to be executed when a certain game event occurs.
    /// It is a key part in defining the behavior and responses to triggers within the script framework.
    /// </remarks>
    Handler,

    /// <summary>
    /// Represents a message symbol used within a Game Event Script.
    /// </summary>
    /// <remarks>
    /// This symbol indicates a textual or informational component in the script, often used for communication
    /// or descriptive purposes during the execution of game events. It may correspond to user-defined messages
    /// or system-generated content within the script's structure.
    /// </remarks>
    Message,

    /// <summary>
    /// Represents a variable defined or referenced within a Game Event Script.
    /// </summary>
    /// <remarks>
    /// This symbol is used to identify variables in the script, which may hold values that can influence the behavior
    /// or state of the events being processed. Variables can be either local to specific script contexts or global,
    /// depending on their declaration.
    /// </remarks>
    Variable,

    /// <summary>
    /// Represents a global definition within a Game Event Script.
    /// </summary>
    /// <remarks>
    /// A global definition typically denotes a script-level declaration that is accessible throughout the entire
    /// scope of the script. This symbol kind is used to define global variables, constants, or other constructs
    /// that are not tied to a specific local or nested context within the script.
    /// </remarks>
    GlobalDefinition
}

/// <summary>
/// Represents a fatal runtime exception that occurs specifically during the execution
/// of a Game Event Script. This exception is meant to signal critical issues that
/// cannot be recovered from within the execution of a script's runtime logic.
/// </summary>
/// <remarks>
/// This class provides constructors to initialize the exception with an optional
/// message and an inner exception, allowing for detailed context when the exception
/// is thrown. It serves as a base class for more specific fatal runtime exceptions
/// in the Game Event Script framework.
/// </remarks>
public abstract class GameEventScriptFatalRuntimeException : Exception
{
    /// <inheritdoc />
    protected GameEventScriptFatalRuntimeException(GameEventScriptDiagnostic diagnostic) : base(RequireDiagnostic(diagnostic).Message)
    {
        Diagnostic = diagnostic;
    }

    /// <inheritdoc />
    protected GameEventScriptFatalRuntimeException(GameEventScriptDiagnostic diagnostic, Exception? innerException) : base(RequireDiagnostic(diagnostic).Message, innerException)
    {
        Diagnostic = diagnostic;
    }

    /// <summary>
    /// Gets the diagnostic.
    /// </summary>
    public GameEventScriptDiagnostic Diagnostic { get; }

    private static GameEventScriptDiagnostic RequireDiagnostic(GameEventScriptDiagnostic? diagnostic)
        => diagnostic ?? throw new ArgumentNullException(nameof(diagnostic));
}

/// <summary>
/// Represents an exception thrown when a dynamic linking error occurs
/// during the execution of a Game Event Script.
/// This exception specifically indicates issues related to the binding or resolution
/// of external references required for script execution.
/// </summary>
/// <remarks>
/// This exception is typically used to signal problems encountered when attempting
/// to dynamically bind extensions or external references in the context of the script's
/// runtime environment. It provides relevant error details to help diagnose and resolve
/// issues during the linking process.
/// </remarks>
public sealed class GameEventScriptDynamicLinkException : Exception
{
    /// <summary>
    /// Initializes a new instance of Game Event Script Dynamic Link Exception.
    /// </summary>
    /// <param name="diagnostic">The diagnostic value.</param>
    public GameEventScriptDynamicLinkException(GameEventScriptDiagnostic diagnostic) : base(RequireDiagnostic(diagnostic).Message)
    {
        if (diagnostic.Phase != GameEventScriptDiagnosticPhase.Link)
            throw new ArgumentException("A dynamic-link exception requires a link diagnostic.", nameof(diagnostic));
        Diagnostic = diagnostic;
    }

    /// <summary>
    /// Gets the diagnostic.
    /// </summary>
    public GameEventScriptDiagnostic Diagnostic { get; }

    private static GameEventScriptDiagnostic RequireDiagnostic(GameEventScriptDiagnostic? diagnostic)
        => diagnostic ?? throw new ArgumentNullException(nameof(diagnostic));
}
