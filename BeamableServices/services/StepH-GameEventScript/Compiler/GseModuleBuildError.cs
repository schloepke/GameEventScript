namespace StepH.GameEventScript.Compiler;

/// <summary>
/// Represents a specific error encountered during the build process of a GameEventScript module.
/// </summary>
/// <param name="Message">A detailed message describing the nature of the error.</param>
/// <param name="ModuleName">The name of the module in which the error occurred.</param>
/// <param name="Symbol">The name of the symbol that caused the error.</param>
/// <param name="SymbolKind">The kind of the symbol related to the error, such as Type, Rule, or Select.</param>
/// <param name="Kind">The specific type of build error that occurred, represented by the <see cref="GseModuleBuildErrorKind"/> enum.</param>
/// <param name="SourceLocation">The location in the source code where the error occurred, represented by a <see cref="GseSourceLocation"/> object.</param>
public sealed record GseModuleBuildError(string Message, string ModuleName, string Symbol, GseSymbolKind SymbolKind, GseModuleBuildErrorKind Kind, GseSourceLocation SourceLocation)
{
    /// <summary>
    /// Returns a string representation of the current <see cref="GseModuleBuildError"/> object.
    /// </summary>
    /// <returns>
    /// A formatted string describing the build error, including the module name, error message, and source location.
    /// </returns>
    public override string ToString() => $"Module '{ModuleName}': {Message} [{SourceLocation}]";
}

/// <summary>
/// Defines the various kinds of errors that can occur during the build process of a GameEventScript module.
/// </summary>
/// <remarks>
/// This enumeration categorizes specific build errors to assist in identifying and handling issues encountered
/// while compiling a GameEventScript module. These errors include conflicts, invalid syntax, or logical discrepancies
/// within the module.
/// </remarks>
/// <example>
/// The <see cref="GseModuleBuildErrorKind"/> enum is used in conjunction with <see cref="GseModuleBuildError"/> to describe
/// specific types of build-related errors in detailed diagnostic outputs.
/// </example>
public enum GseModuleBuildErrorKind
{
    /// <summary>
    /// Indicates that a duplicate type was found during the compilation of a GameEventScript module.
    /// </summary>
    /// <remarks>
    /// This error occurs when a type with the same name is defined more than once within a
    /// module or across multiple modules being compiled. Duplicate type definitions can
    /// cause ambiguity and lead to inconsistent behavior or runtime errors.
    /// </remarks>
    DuplicateType,

    /// <summary>
    /// Indicates that a duplicate rule was encountered during the compilation of a GameEventScript module.
    /// </summary>
    /// <remarks>
    /// This error occurs when a rule with the same identifier is defined more than once within a module or
    /// across multiple modules being compiled. Duplicate rules can result in logical ambiguities or
    /// unpredictable behavior in the module's execution.
    /// </remarks>
    DuplicateRule,

    /// <summary>
    /// Indicates that a duplicate select statement was encountered during the compilation of a GameEventScript module.
    /// </summary>
    /// <remarks>
    /// This error occurs when multiple select statements with the same identifier are defined
    /// within the same scope or across multiple compilable units. Duplicate select definitions
    /// can lead to conflicts and ambiguous behavior during event script execution.
    /// </remarks>
    DuplicateSelect,

    /// <summary>
    /// Indicates a conflict between a rule and a select element during the compilation of a GameEventScript module.
    /// </summary>
    /// <remarks>
    /// This error occurs when a rule and a select statement within the module are incompatible or improperly defined
    /// in a way that triggers logical inconsistency. Addressing this issue typically involves reviewing the rule
    /// and select constructs to ensure their definitions align with the module's requirements and constraints.
    /// </remarks>
    RuleSelectConflict,

    /// <summary>
    /// Indicates that a required rule or select statement is missing during the compilation of a GameEventScript module.
    /// </summary>
    /// <remarks>
    /// This error occurs when a module does not define at least one rule or select statement, which are essential
    /// components for the module's behavior. Rules and select statements form the logical structure of the module,
    /// and their absence renders the module incomplete or non-functional.
    /// </remarks>
    MissingRuleOrSelect,

    /// <summary>
    /// Indicates that an invalid predicate was encountered within a rule definition
    /// during the compilation of a GameEventScript module.
    /// </summary>
    /// <remarks>
    /// This error occurs when the condition or expression specified as a predicate
    /// for a rule is invalid or does not conform to the expected syntax, semantics,
    /// or type requirements. Invalid predicates may lead to rules being improperly
    /// evaluated or ignored at runtime.
    /// </remarks>
    InvalidRulePredicate,

    /// <summary>
    /// Indicates that a rule was defined with an incorrect number of arguments during the
    /// compilation of a GameEventScript module.
    /// </summary>
    /// <remarks>
    /// This error occurs when the arity (number of parameters) of a rule does not match the
    /// expected definition. Rules in the module must adhere to their declared parameter count,
    /// and any deviation can lead to runtime issues or inconsistencies in the module’s behavior.
    /// </remarks>
    WrongRuleArity,

    /// <summary>
    /// Indicates that an incorrect number of arguments was provided to a `select` statement
    /// during the compilation of a GameEventScript module.
    /// </summary>
    /// <remarks>
    /// This error occurs when the arity (number of arguments) specified in a `select` clause
    /// does not match the expected number based on its definition or usage context.
    /// Such mismatches can lead to logical issues or failure in the module's behavior.
    /// </remarks>
    WrongSelectArity,

    /// <summary>
    /// Indicates that a duplicate handler parameter was detected during the compilation of a GameEventScript module.
    /// </summary>
    /// <remarks>
    /// This error occurs when a handler within the module is defined with parameters that have identical names.
    /// Duplicate parameter names in handler definitions can lead to ambiguity in parameter resolution
    /// and prevent successful compilation of the module.
    /// </remarks>
    DuplicateHandlerParameter,

    /// <summary>
    /// Indicates that a duplicate parameter definition was found during the compilation of a GameEventScript module.
    /// </summary>
    /// <remarks>
    /// This error occurs when a parameter with the same name is defined more than once within the same scope
    /// in a module. Duplicate parameter definitions can cause compilation failures or unexpected behavior during execution.
    /// </remarks>
    DuplicateDefinitionParameter,

    /// <summary>
    /// Indicates that a duplicate publish argument was found during the compilation of a GameEventScript module.
    /// </summary>
    /// <remarks>
    /// This error occurs when the same publish argument is defined more than once within the scope of a module.
    /// Duplicate publish arguments can lead to conflicts in the module's publish logic and potentially result
    /// in unexpected runtime behavior or invalid state transitions.
    /// </remarks>
    DuplicatePublishArgument,

    /// <summary>
    /// Indicates that a duplicate variable was found during the compilation of a GameEventScript module.
    /// </summary>
    /// <remarks>
    /// This error occurs when a variable with the same name is declared multiple times within the
    /// same scope or across overlapping scopes. Duplicate variable declarations can lead to
    /// ambiguity, shadowing issues, or unintended behavior during script execution.
    /// </remarks>
    DuplicateVariable,

    /// <summary>
    /// Indicates that an identifier does not adhere to the expected capitalization style during
    /// the compilation of a GameEventScript module.
    /// </summary>
    /// <remarks>
    /// This error arises when an identifier (such as a variable, function, or type name) is not
    /// formatted according to the required casing convention (e.g., camelCase, PascalCase).
    /// Adhering to consistent casing rules improves code readability and reduces errors caused
    /// by mismatched naming conventions.
    /// </remarks>
    InvalidIdentifierCase,

    /// <summary>
    /// Indicates that a message identifier uses an invalid or inconsistent casing style during the compilation
    /// of a GameEventScript module.
    /// </summary>
    /// <remarks>
    /// This error is triggered when a message identifier does not conform to the expected casing convention.
    /// Proper casing ensures uniformity and avoids potential issues with identifier recognition, especially
    /// in case-sensitive environments.
    /// </remarks>
    InvalidMessageCase,

    /// <summary>
    /// Indicates that an invalid type constructor was encountered during the compilation of a GameEventScript module.
    /// </summary>
    /// <remarks>
    /// This error arises when a type constructor does not conform to the expected format or definition required
    /// in the context of a module. Invalid type constructors may result from incorrect syntax, unsupported types,
    /// or violations of specific module construction rules.
    /// </remarks>
    InvalidTypeConstructor
}

/// <summary>
/// Represents kinds of symbols that can be used in a GameEventScript module.
/// </summary>
/// <remarks>
/// This enumeration defines the various types of symbols that may appear in the context of
/// GameEventScript processing. These symbols are used to categorize entities such as types, rules,
/// selections, handlers, messages, variables, and global definitions.
/// </remarks>
public enum GseSymbolKind
{
    /// <summary>
    /// Represents a type symbol within a GameEventScript module.
    /// </summary>
    /// <remarks>
    /// This symbol category is used to define new types or references to types in the context of
    /// GameEventScript processing. Types are foundational entities that help structure data
    /// and logic within a module, enabling the creation of reusable, modular components.
    /// </remarks>
    Type,

    /// <summary>
    /// Represents a user-defined rule within a GameEventScript module.
    /// </summary>
    /// <remarks>
    /// Rules are a fundamental component of GameEventScript that define specific conditions
    /// or actions to be evaluated and executed during the script's runtime. Each rule may
    /// reference other entities, such as variables or handlers, to implement complex logic
    /// and behavior. Incorrect or conflicting usage of rules may cause compilation errors
    /// or runtime misbehavior.
    /// </remarks>
    Rule,

    /// <summary>
    /// Represents a selection symbol in a GameEventScript module.
    /// </summary>
    /// <remarks>
    /// This symbol is used to define a selection, which typically represents a choice or option within
    /// the logic of a GameEventScript module. A selection can be employed to drive decision-making
    /// processes, often in conjunction with rules or other defined entities.
    /// </remarks>
    Select,

    /// <summary>
    /// Represents a handler symbol in a GameEventScript module.
    /// </summary>
    /// <remarks>
    /// A handler symbol defines logic that responds to specific events or conditions within
    /// a GameEventScript module. Handlers are key components in the event-driven structure
    /// of the module, enabling customized behavior during execution.
    /// </remarks>
    Handler,

    /// <summary>
    /// Represents a message symbol within a GameEventScript module.
    /// </summary>
    /// <remarks>
    /// This symbol category is used to identify and process message definitions
    /// or references in the context of GameEventScript compilation. Messages are
    /// typically utilized for communication or signaling mechanisms between entities
    /// within the script.
    /// </remarks>
    Message,

    /// <summary>
    /// Represents a variable symbol in a GameEventScript module.
    /// </summary>
    /// <remarks>
    /// A variable symbol is used to define and reference a named data entity within a
    /// GameEventScript module. Variables are essential for storing and manipulating data
    /// during script execution. This symbol type helps to manage variable declarations
    /// and usages throughout the module's lifecycle.
    /// </remarks>
    Variable,

    /// <summary>
    /// Represents a symbol that defines a globally accessible construct in a GameEventScript module.
    /// </summary>
    /// <remarks>
    /// GlobalDefinition is used to categorize symbols that are available across all contexts and
    /// scopes within a module. These definitions provide shared functionality or data that can
    /// be utilized by various components without requiring explicit local declarations.
    /// </remarks>
    GlobalDefinition
}