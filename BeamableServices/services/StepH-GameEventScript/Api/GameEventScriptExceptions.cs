using System;
using System.Collections.Generic;
using System.Linq;
// ReSharper disable UnusedMember.Global

namespace StepH.GameEventScript.Api;

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
    public GameEventScriptCompileException(string message) : base(message)
    {
        Errors = [];
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
    public GameEventScriptCompileException(IReadOnlyList<GameEventScriptCompileError> errors) : base(BuildMessage(errors))
    {
        Errors = errors ?? throw new ArgumentNullException(nameof(errors));
    }

    /// <summary>
    /// Gets a collection of compiler errors encountered during the processing of a game event script.
    /// </summary>
    /// <remarks>
    /// This property contains detailed information about each compiler error,
    /// such as the error message, module name, symbol, symbol kind, error kind,
    /// and the source location where the error occurred. The collection is empty
    /// if no errors were encountered.
    /// </remarks>
    public IReadOnlyList<GameEventScriptCompileError> Errors { get; }

    private static string BuildMessage(IReadOnlyList<GameEventScriptCompileError>? errors)
        => errors is null || errors.Count == 0
            ? "GameEventScript compilation failed."
            : $"GameEventScript compilation failed with {errors.Count} error(s):{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", errors.Select(it => it.ToString()))}";
}


/// <summary>
/// Represents a detailed error encountered during the compilation of a Game Event Script.
/// This error provides comprehensive information about the issue, including its location,
/// type, and associated symbol, to assist in diagnosing and resolving the problem.
/// </summary>
/// <remarks>
/// Instances of this class are used to encapsulate specific issues found during the script
/// compilation process. Each error is classified by its kind, symbol, and source location,
/// aiding developers in pinpointing and addressing problems in their scripts.
/// </remarks>
/// <param name="Message">
/// A descriptive message explaining the nature of the compilation error.
/// </param>
/// <param name="ModuleName">
/// The name of the module in which the error was detected.
/// </param>
/// <param name="Symbol">
/// The specific symbol associated with the error, if applicable.
/// </param>
/// <param name="SymbolKind">
/// The kind of symbol (e.g., Type, Rule, Select, etc.) involved in the error.
/// </param>
/// <param name="Kind">
/// The classification of the error (e.g., Syntax, MissingRuleOrSelect, DuplicateType, etc.).
/// </param>
/// <param name="SourceLocation">
/// The precise source location where the error occurred, including file name and optional
/// line/column details.
/// </param>
public sealed record GameEventScriptCompileError(
    string Message,
    string ModuleName,
    string Symbol,
    GameEventScriptSymbolKind SymbolKind,
    GameEventScriptCompileErrorKind Kind,
    GameEventScriptSourceLocation SourceLocation)
{
    /// <summary>
    /// Returns a string representation of the current GameEventScriptCompileError instance,
    /// including detailed information such as the module name, error message, and source location.
    /// </summary>
    /// <returns>A string describing the error, formatted with module name, message, and source location.</returns>
    public override string ToString() => $"Module '{ModuleName}': {Message} [{SourceLocation}]";
}


/// <summary>
/// Defines the different types of errors that can occur during the compilation process of a Game Event Script.
/// These errors represent specific issues detected in the script's syntax, structure, or logical definition.
/// </summary>
/// <remarks>
/// This enumeration categorizes the various kinds of compile-time issues encountered by the Game Event Script compiler.
/// Each value corresponds to a unique type of error, facilitating precise error reporting and debugging.
/// </remarks>
public enum GameEventScriptCompileErrorKind
{
    /// <summary>
    /// Represents a syntax error encountered during the compilation of a Game Event Script.
    /// </summary>
    /// <remarks>
    /// This error indicates that the script contains invalid syntax that does not conform to the expected language grammar.
    /// Such issues may include missing punctuation, mismatched delimiters, or other structural problems in the script
    /// that prevent successful parsing and compilation.
    /// </remarks>
    Syntax,

    /// <summary>
    /// Indicates an error caused by the presence of duplicate type definitions in the Game Event Script.
    /// </summary>
    /// <remarks>
    /// This error occurs when the script defines multiple types with the same name within the same scope.
    /// Duplicate type definitions can lead to ambiguity and are not permitted by the compiler.
    /// Developers must ensure that type names are unique within a given context to avoid this error.
    /// </remarks>
    DuplicateType,

    /// <summary>
    /// Represents an error caused by the presence of duplicate rules in a Game Event Script.
    /// </summary>
    /// <remarks>
    /// This error indicates that the script defines multiple rules with the same name or signature,
    /// which leads to conflicts and ambiguity during the compilation process.
    /// To resolve this issue, ensure that all rules in the script have unique names or signatures.
    /// </remarks>
    DuplicateRule,

    /// <summary>
    /// Represents an error caused by multiple select statements being defined with identical signatures in a Game Event Script.
    /// </summary>
    /// <remarks>
    /// This error indicates that the script contains redundant or conflicting select statements, resulting in ambiguity or
    /// unintended behavior during compilation. Each select statement within a script must be uniquely defined to ensure
    /// proper execution and avoid logical conflicts.
    /// </remarks>
    DuplicateSelect,

    /// <summary>
    /// Represents a conflict between a rule and a select statement in the Game Event Script.
    /// </summary>
    /// <remarks>
    /// This error indicates that the script contains a logical conflict where a rule and a select statement
    /// are defined in a way that makes them incompatible or mutually exclusive. Such issues typically arise
    /// from structural or logical errors in the script's design, requiring adjustments to resolve the conflict
    /// and achieve consistent behavior.
    /// </remarks>
    RuleSelectConflict,

    /// <summary>
    /// Represents an error indicating that a required rule or select statement is missing during
    /// the compilation of a Game Event Script.
    /// </summary>
    /// <remarks>
    /// This error occurs when the script does not define a rule or select statement needed for proper
    /// execution. A rule or select statement is essential for defining the logic and flow in the script,
    /// and its absence prevents successful compilation.
    /// </remarks>
    MissingRuleOrSelect,

    /// <summary>
    /// Indicates that a rule within the Game Event Script contains an invalid predicate.
    /// </summary>
    /// <remarks>
    /// This error occurs when the predicate associated with a rule is not valid, according to the requirements
    /// of the script's logic or semantic rules. Common causes may include unsupported operations,
    /// invalid references, or expressions that cannot be evaluated in the context of the rule's execution.
    /// </remarks>
    InvalidRulePredicate,

    /// <summary>
    /// Indicates an arity mismatch for a rule encountered during the compilation of a Game Event Script.
    /// </summary>
    /// <remarks>
    /// This error occurs when the number of arguments provided to a rule does not match the expected arity
    /// defined for that rule. It suggests that the script includes a rule invocation with either too few or too
    /// many arguments, leading to a compilation failure.
    /// </remarks>
    WrongRuleArity,

    /// <summary>
    /// Represents an error that occurs when a select statement in a Game Event Script
    /// has an incorrect number of arguments.
    /// </summary>
    /// <remarks>
    /// This error indicates that the select statement does not match the expected arity,
    /// meaning the number of arguments provided to the select does not align with the
    /// predefined requirements or function signature for that select in the script.
    /// </remarks>
    WrongSelectArity,

    /// <summary>
    /// Indicates an error where multiple handler parameters with the same name are defined in a Game Event Script.
    /// </summary>
    /// <remarks>
    /// This error occurs when a script defines duplicate parameters for a handler, which leads to ambiguity
    /// during compilation. Each parameter name within a handler must be unique to ensure proper script execution.
    /// </remarks>
    DuplicateHandlerParameter,

    /// <summary>
    /// Indicates a duplication error caused by multiple parameters with the same name or definition in a Game Event Script.
    /// </summary>
    /// <remarks>
    /// This error occurs when a parameter is defined more than once within a specific context where unique parameter definitions are required.
    /// Such duplication can lead to ambiguity in the interpretation or execution of the script and must be resolved for successful compilation.
    /// </remarks>
    DuplicateDefinitionParameter,

    /// <summary>
    /// Represents an error indicating a duplicate argument in a publishing statement within a Game Event Script.
    /// </summary>
    /// <remarks>
    /// This error occurs when multiple arguments with the same name are specified in a publishing statement,
    /// leading to ambiguity or conflicts that prevent successful compilation of the script.
    /// Ensure that each argument name within the publishing statement is unique.
    /// </remarks>
    DuplicatePublishArgument,

    /// <summary>
    /// Represents an error caused by the declaration of a variable with a name that duplicates an existing variable
    /// within the same scope during the compilation of a Game Event Script.
    /// </summary>
    /// <remarks>
    /// This error indicates that the script contains multiple variables with the same identifier within the same scope,
    /// leading to ambiguity or potential conflicts in variable resolution. To resolve this issue, ensure that all variable
    /// names within the same scope are unique.
    /// </remarks>
    DuplicateVariable,

    /// <summary>
    /// Indicates an error caused by an identifier in the script not conforming to the expected case formatting.
    /// </summary>
    /// <remarks>
    /// This error typically occurs when identifiers, such as variable names, type names, or method names,
    /// do not follow the required case sensitivity or naming conventions enforced by the script compiler.
    /// Ensuring proper capitalization and adherence to naming standards can resolve this issue.
    /// </remarks>
    InvalidIdentifierCase,

    /// <summary>
    /// Represents an error where a message case is invalid in a Game Event Script.
    /// </summary>
    /// <remarks>
    /// This error occurs when a message identifier or case usage in the script
    /// does not conform to the expected conventions or requirements. Common causes
    /// may include incorrect capitalization or formatting of message cases, leading
    /// to unresolved or improperly referenced messages during compilation.
    /// </remarks>
    InvalidMessageCase,

    /// <summary>
    /// Represents an error indicating the use of an invalid type constructor during the compilation of a Game Event Script.
    /// </summary>
    /// <remarks>
    /// This error occurs when a type constructor used in the script is not recognized or does not adhere to the expected
    /// conventions and requirements of the scripting language. It may involve unsupported type definitions or incorrect
    /// usage of type constructors, resulting in a failure to compile the script.
    /// </remarks>
    InvalidTypeConstructor
}


/// <summary>
/// Defines the different kinds of symbols used within a Game Event Script.
/// This enumeration is used to classify the functional or semantic roles
/// of various components in a script, enabling the compiler and validation logic
/// to process and verify them appropriately.
/// </summary>
/// <remarks>
/// Each value in this enum corresponds to a distinct type of script element,
/// such as type definitions, rules, event handlers, or variables. These classifications
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
    /// Represents a rule defined within a Game Event Script.
    /// </summary>
    /// <remarks>
    /// A rule specifies conditional logic or directives that define how certain
    /// events or scenarios should be handled within the script. It typically includes
    /// criteria and corresponding actions that dictate the script's behavior
    /// in response to specific game events.
    /// </remarks>
    Rule,

    /// <summary>
    /// Represents a selection construct within a Game Event Script.
    /// </summary>
    /// <remarks>
    /// This symbol indicates a decision-making or branching mechanism within the script,
    /// allowing for the dynamic execution of specific logic paths based on conditions or input.
    /// It plays a key role in determining the flow control and behavior of the script
    /// during runtime based on specified criteria.
    /// </remarks>
    Select,

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
    protected GameEventScriptFatalRuntimeException(string message) : base(message)
    {
    }

    /// <inheritdoc />
    protected GameEventScriptFatalRuntimeException(string message, Exception? innerException) : base(message, innerException)
    {
    }
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
public sealed class GameEventScriptDynamicLinkException(string message) : Exception(message);
