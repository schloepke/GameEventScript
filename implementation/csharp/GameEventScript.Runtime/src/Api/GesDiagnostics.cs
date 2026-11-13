// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

// ReSharper disable UnusedMember.Global

namespace GameEventScript.Api;

/// <summary>Identifies the pipeline stage in which a diagnostic originated; the numeric values are stable.</summary>
public enum GesDiagnosticPhase
{
    /// <summary>Source tokenization or grammar recognition.</summary>
    Parse = 1,
    /// <summary>Semantic validation of parsed sources.</summary>
    Validate = 2,
    /// <summary>Lowering, resource analysis or bytecode generation.</summary>
    Compile = 3,
    /// <summary>Reading or validating a <c>.gesb</c> image or Program; codes are <c>decode.</c> followed by a <see cref="GesProgramFormatErrorCode"/> name.</summary>
    Decode = 4,
    /// <summary>Resolving a Program against a host: limits, extensions, external types and the call graph.</summary>
    Link = 5,
    /// <summary>Execution of script handlers, native handlers, extensions or the publish sink.</summary>
    Runtime = 6
}

/// <summary>Defines the stable diagnostic code strings; a code is compared together with its <see cref="GesDiagnosticPhase"/>, never through the message text.</summary>
public static class GesDiagnosticCodes
{
    /// <summary>Initialization failed because a configured runtime safety limit was exhausted.</summary>
    public const string RuntimeInitializationLimitReached = "runtime.initializationLimitReached";
    /// <summary>Malformed source syntax.</summary>
    public const string ParseSyntax = "parse.syntax";
    /// <summary>Source exceeds the portable expression or statement nesting limit.</summary>
    public const string ParseSourceNestingExceeded = "parse.sourceNestingExceeded";
    /// <summary>A record type is declared more than once, or also declared by an external type.</summary>
    public const string ValidateDuplicateType = "validate.duplicateType";
    /// <summary>Two predicates share the same signature (name plus ordered argument labels).</summary>
    public const string ValidateDuplicatePredicate = "validate.duplicatePredicate";
    /// <summary>Two functions share the same signature (name plus ordered argument labels).</summary>
    public const string ValidateDuplicateFunction = "validate.duplicateFunction";
    /// <summary>A predicate and a function share a base name.</summary>
    public const string ValidatePredicateFunctionConflict = "validate.predicateFunctionConflict";
    /// <summary>A call names no existing predicate or function.</summary>
    public const string ValidateMissingCallable = "validate.missingCallable";
    /// <summary>A predicate does not yield a Boolean or nothing, or an <c>is</c> test names no predicate with the required parameter count.</summary>
    public const string ValidateInvalidPredicate = "validate.invalidPredicate";
    /// <summary>A predicate call matches a predicate name but none of its argument-label signatures.</summary>
    public const string ValidateWrongPredicateArity = "validate.wrongPredicateArity";
    /// <summary>A function call matches a function name but none of its argument-label signatures.</summary>
    public const string ValidateWrongFunctionArity = "validate.wrongFunctionArity";
    /// <summary>A handler declares the same parameter name or label more than once.</summary>
    public const string ValidateDuplicateHandlerParameter = "validate.duplicateHandlerParameter";
    /// <summary>A predicate, function or record declares the same parameter name or label more than once.</summary>
    public const string ValidateDuplicateDefinitionParameter = "validate.duplicateDefinitionParameter";
    /// <summary>A message construction or send repeats an argument label.</summary>
    public const string ValidateDuplicatePublishArgument = "validate.duplicatePublishArgument";
    /// <summary>A binding name is declared twice in the same scope.</summary>
    public const string ValidateDuplicateVariable = "validate.duplicateVariable";
    /// <summary>A program-wide constant is declared more than once.</summary>
    public const string ValidateDuplicateConstant = "validate.duplicateConstant";
    /// <summary>A lexical binding shadows a visible ancestor binding.</summary>
    public const string ValidateShadowedVariable = "validate.shadowedVariable";
    /// <summary>A bare name has no visible binding in its own source.</summary>
    public const string ValidateUnresolvedVariable = "validate.unresolvedVariable";
    /// <summary>A name, label, member, map key or module name violates its spelling rules, or sources declare mismatching modules.</summary>
    public const string ValidateInvalidIdentifierCase = "validate.invalidIdentifierCase";
    /// <summary>A message name violates the message spelling rules.</summary>
    public const string ValidateInvalidMessageCase = "validate.invalidMessageCase";
    /// <summary>A type name, type constructor, conversion target or send delay type is unknown or invalid.</summary>
    public const string ValidateInvalidTypeConstructor = "validate.invalidTypeConstructor";

    /// <summary>
    /// A tag clause contains the literal <c>nothing</c>.
    /// </summary>
    public const string ValidateInvalidTag = "validate.invalidTag";
    /// <summary>The compiler cannot lower a construct to bytecode.</summary>
    public const string CompileUnsupportedConstruct = "compile.unsupportedConstruct";
    /// <summary>A lowered intrinsic or handler form has the wrong number of arguments or parameters.</summary>
    public const string CompileInvalidArity = "compile.invalidArity";
    /// <summary>Lowering cannot resolve a constant, callable, predicate or record constructor.</summary>
    public const string CompileUnresolvedSymbol = "compile.unresolvedSymbol";
    /// <summary>A value that lowering must encode exceeds its operand range.</summary>
    public const string CompileNumericLimitExceeded = "compile.numericLimitExceeded";
    /// <summary>The synchronous call graph contains recursion, which is not allowed.</summary>
    public const string CompileCyclicCallGraph = "compile.cyclicCallGraph";
    /// <summary>Register, call-depth or call-target metadata cannot be computed within the portable limits.</summary>
    public const string CompileInvalidResourceMetadata = "compile.invalidResourceMetadata";
    /// <summary>An internal compiler invariant failed.</summary>
    public const string CompileInvariantViolation = "compile.invariantViolation";
    /// <summary>The Program requires more registers than the host allows.</summary>
    public const string LinkRequiredRegisterCountExceeded = "link.requiredRegisterCountExceeded";
    /// <summary>The Program requires a deeper call stack than the host allows.</summary>
    public const string LinkRequiredCallStackDepthExceeded = "link.requiredCallStackDepthExceeded";
    /// <summary>
    /// Loading requires an initialization message but the host's logical-message queue is full.
    /// </summary>
    public const string LinkInitializationQueueFull = "link.initializationQueueFull";
    /// <summary>An extension binding has a malformed <c>namespace.function</c> name.</summary>
    public const string LinkInvalidExtensionReference = "link.invalidExtensionReference";
    /// <summary>The host registry has no extension function for a referenced signature.</summary>
    public const string LinkMissingExtension = "link.missingExtension";
    /// <summary>The host registry has no constructor for a referenced external type.</summary>
    public const string LinkMissingExternalTypeConstructor = "link.missingExternalTypeConstructor";
    /// <summary>A registered external type constructor does not match the referenced signature.</summary>
    public const string LinkMismatchedExternalTypeConstructor = "link.mismatchedExternalTypeConstructor";
    /// <summary>The Program's synchronous call graph contains a cycle.</summary>
    public const string LinkCyclicCallGraph = "link.cyclicCallGraph";
    /// <summary>The Program violates a call-graph or binding invariant that linking requires.</summary>
    public const string LinkInvalidProgram = "link.invalidProgram";
    /// <summary>The virtual machine was asked to start a message while it is not ready.</summary>
    public const string RuntimeVmStateConflict = "runtime.vmStateConflict";
    /// <summary>The virtual machine could not prepare a handler entry, for example because of an invalid entry address.</summary>
    public const string RuntimePreparationFailed = "runtime.preparationFailed";
    /// <summary>The instruction pointer left the code segment.</summary>
    public const string RuntimeInstructionPointerOutOfRange = "runtime.instructionPointerOutOfRange";
    /// <summary>The virtual machine fetched an opcode it cannot execute.</summary>
    public const string RuntimeIllegalOpcode = "runtime.illegalOpcode";
    /// <summary>Register storage could not grow to the required size.</summary>
    public const string RuntimeRegisterOverflow = "runtime.registerOverflow";
    /// <summary>A call exceeded the call stack capacity.</summary>
    public const string RuntimeCallStackOverflow = "runtime.callStackOverflow";
    /// <summary>A random scope pop would cross the active handler, native or extension boundary.</summary>
    public const string RuntimeRandomStackUnderflow = "runtime.randomStackUnderflow";
    /// <summary>A successfully returning native or extension callback left random scopes open.</summary>
    public const string RuntimeRandomScopeImbalance = "runtime.randomScopeImbalance";
    /// <summary>A record constructor call references a missing or malformed constructor binding or passes the wrong argument count.</summary>
    public const string RuntimeInvalidRecordConstructor = "runtime.invalidRecordConstructor";
    /// <summary>An extension call references a missing, mismatched or unbound extension binding.</summary>
    public const string RuntimeInvalidExtensionBinding = "runtime.invalidExtensionBinding";
    /// <summary>An external type construction or field access references a missing or mismatched binding.</summary>
    public const string RuntimeInvalidExternalTypeBinding = "runtime.invalidExternalTypeBinding";
    /// <summary>A message or handler construction has a signature shape that does not match its argument registers.</summary>
    public const string RuntimeInvalidMessageShape = "runtime.invalidMessageShape";
    /// <summary>A series construction names an unknown built-in series kind.</summary>
    public const string RuntimeInvalidSeriesKind = "runtime.invalidSeriesKind";
    /// <summary>Execution failed with an unexpected exception or entered an error state without a diagnostic.</summary>
    public const string RuntimeUnhandledFailure = "runtime.unhandledFailure";
    /// <summary>A native handler threw an exception other than a <see cref="GesExtensionFaultException"/>.</summary>
    public const string RuntimeNativeHandlerFailure = "runtime.nativeHandlerFailure";
    /// <summary>The publish sink threw; the outbound attempt is rejected while local dispatch is unchanged.</summary>
    public const string RuntimePublishSinkFailure = "runtime.publishSinkFailure";
    /// <summary>
    /// An extension function threw an exception other than a deliberately reported
    /// <see cref="GesExtensionFaultException"/>.
    /// </summary>
    public const string RuntimeExtensionCallFailed = "runtime.extensionCallFailed";
    /// <summary>
    /// An external-type constructor threw an exception other than a deliberately
    /// reported <see cref="GesExtensionFaultException"/>.
    /// </summary>
    public const string RuntimeExternalConstructorFailed = "runtime.externalConstructorFailed";
    /// <summary>
    /// An <see cref="IGesExternalValue"/> field access threw an exception
    /// other than a deliberately reported <see cref="GesExtensionFaultException"/>.
    /// </summary>
    public const string RuntimeExternalFieldAccessFailed = "runtime.externalFieldAccessFailed";

    /// <summary>
    /// The reserved code prefix for internally raised runtime diagnostics. Codes
    /// passed to <see cref="GesExtensionFaultException"/> must not use it.
    /// </summary>
    internal const string RuntimeCodePrefix = "runtime.";

    /// <summary>Returns the decode-phase diagnostic code for a format error code: <c>decode.</c> followed by the lower-camel-case name.</summary>
    /// <param name="code">The binary format error code.</param>
    /// <returns>The diagnostic code, for example <c>decode.invalidMagic</c> for <see cref="GesProgramFormatErrorCode.InvalidMagic"/>.</returns>
    public static string Decode(GesProgramFormatErrorCode code)
    {
        var name = code.ToString();
        return "decode." + char.ToLowerInvariant(name[0]) + name[1..];
    }
}

/// <summary>
/// Formats a caught exception into non-normative diagnostic text for
/// <see cref="GesDiagnostic.TechnicalDetails"/>. The result is never a
/// portable comparison value; only the diagnostic phase, code, and structured
/// fields are normative.
/// </summary>
internal static class GesRuntimeExceptionText
{
    /// <summary>
    /// Describes an unexpected exception, including its type, message, and stack
    /// trace, for logging and development diagnosis.
    /// </summary>
    /// <param name="exception">The caught exception.</param>
    /// <returns>Non-normative, platform-specific exception text.</returns>
    internal static string Describe(Exception exception) => exception.ToString();
}

/// <summary>
/// A portable diagnostic of a source, binary, linking or runtime failure. <see cref="Phase"/> and <see cref="Code"/> identify it;
/// the optional fields add structured context and are absent when unknown.
/// </summary>
/// <param name="Phase">The pipeline stage that reported the diagnostic.</param>
/// <param name="Code">The stable machine-readable code, such as <c>validate.missingCallable</c>.</param>
/// <param name="Message">The human-readable explanation; it may change and is not a comparison key.</param>
/// <param name="Symbol">The affected symbol, such as a name or signature ID, when available.</param>
/// <param name="SymbolKind">The role of <paramref name="Symbol"/>.</param>
/// <param name="SourceLocation">The source range that caused the diagnostic, when available.</param>
/// <param name="ProgramName">The module name of the affected Program; the host fills it for handler failures when absent.</param>
/// <param name="HandlerName">The signature of the affected handler; the host fills it for handler failures when absent.</param>
/// <param name="TechnicalDetails">Non-normative implementation details for logs, such as exception text or byte positions.</param>
public sealed record GesDiagnostic(
    GesDiagnosticPhase Phase,
    string Code,
    string Message,
    string? Symbol = null,
    GesDiagnosticSymbolKind SymbolKind = GesDiagnosticSymbolKind.Unknown,
    GesSourceLocation? SourceLocation = null,
    string? ProgramName = null,
    string? HandlerName = null,
    string? TechnicalDetails = null)
{
    /// <summary>Formats the diagnostic as <c>[code] message</c> for display.</summary>
    /// <returns>The code in brackets followed by the message.</returns>
    public override string ToString() => $"[{Code}] {Message}";
}

/// <summary>Identifies the role of the optional symbol named by a diagnostic.</summary>
public enum GesDiagnosticSymbolKind
{
    /// <summary>No specific symbol category is available.</summary>
    Unknown,

    /// <summary>A source record type or an external type.</summary>
    Type,

    /// <summary>A declared predicate.</summary>
    Predicate,

    /// <summary>A declared function.</summary>
    Function,

    /// <summary>A message handler.</summary>
    Handler,

    /// <summary>A message signature or a message being sent.</summary>
    Message,

    /// <summary>A lexical variable binding.</summary>
    Variable,

    /// <summary>A program-wide definition, such as a constant, a module or a callable referenced by name.</summary>
    GlobalDefinition
}

/// <summary>
/// Base class of exceptions that carry a runtime <see cref="GesDiagnostic"/> and end the current handler with that diagnostic.
/// The exception message is the diagnostic's message.
/// </summary>
public abstract class GesFatalRuntimeException : Exception
{
    /// <summary>Creates the exception for a diagnostic.</summary>
    /// <param name="diagnostic">The runtime diagnostic to report.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="diagnostic"/> is null.</exception>
    protected GesFatalRuntimeException(GesDiagnostic diagnostic) : base(RequireDiagnostic(diagnostic).Message)
    {
        Diagnostic = diagnostic;
    }

    /// <summary>Creates the exception for a diagnostic with an underlying cause.</summary>
    /// <param name="diagnostic">The runtime diagnostic to report.</param>
    /// <param name="innerException">The underlying exception, if any.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="diagnostic"/> is null.</exception>
    protected GesFatalRuntimeException(GesDiagnostic diagnostic, Exception? innerException) : base(RequireDiagnostic(diagnostic).Message, innerException)
    {
        Diagnostic = diagnostic;
    }

    /// <summary>
    /// Gets the runtime diagnostic that this exception reports.
    /// </summary>
    public GesDiagnostic Diagnostic { get; }

    private static GesDiagnostic RequireDiagnostic(GesDiagnostic? diagnostic)
        => diagnostic ?? throw new ArgumentNullException(nameof(diagnostic));
}

/// <summary>
/// A deliberate, portable runtime fault that an extension function, native handler, external-type
/// constructor, or <see cref="IGesExternalValue"/> implementation throws
/// to report a defined failure. It ends the current handler with a diagnosable
/// runtime error instead of silently evaluating to <c>nothing</c>; the host remains
/// reusable for subsequently enqueued messages, as for any other runtime error.
/// </summary>
/// <remarks>
/// This is distinct from an unanticipated exception raised by a bug in the same
/// callback: that case is reported as <see cref="GesDiagnosticCodes.RuntimeExtensionCallFailed"/>,
/// <see cref="GesDiagnosticCodes.RuntimeExternalConstructorFailed"/>,
/// <see cref="GesDiagnosticCodes.RuntimeExternalFieldAccessFailed"/>, or
/// <see cref="GesDiagnosticCodes.RuntimeNativeHandlerFailure"/> and
/// never requires this type.
/// </remarks>
public sealed class GesExtensionFaultException : GesFatalRuntimeException
{
    /// <summary>
    /// Creates a deliberate runtime fault with a caller-chosen code.
    /// </summary>
    /// <param name="code">
    /// A stable code in the caller's own namespace, for example <c>"myExtension.outOfStock"</c>.
    /// It must not be empty or consist only of whitespace.
    /// It must not start with the reserved <c>"runtime."</c> prefix used by internally
    /// raised runtime diagnostics.
    /// </param>
    /// <param name="message">A human-readable explanation. It may change and must never decide portable conformance.</param>
    /// <param name="symbol">An optional symbol identifying the failing construct.</param>
    /// <param name="innerException">
    /// An optional original cause. It is never part of the diagnostic's normative
    /// fields; when present, it is rendered into <see cref="GesDiagnostic.TechnicalDetails"/>
    /// only, which is non-normative logging text.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="code"/> or <paramref name="message"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="code"/> is empty, consists only of whitespace, or starts with the reserved <c>runtime.</c> prefix.</exception>
    public GesExtensionFaultException(string code, string message, string? symbol = null, Exception? innerException = null)
        : base(BuildDiagnostic(code, message, symbol, innerException), innerException)
    {
    }

    private static GesDiagnostic BuildDiagnostic(string code, string message, string? symbol, Exception? innerException)
    {
        if (code is null) throw new ArgumentNullException(nameof(code));
        if (message is null) throw new ArgumentNullException(nameof(message));
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Extension fault codes must not be empty or whitespace.", nameof(code));
        if (code.StartsWith(GesDiagnosticCodes.RuntimeCodePrefix, StringComparison.Ordinal))
            throw new ArgumentException($"Extension fault codes must not use the reserved '{GesDiagnosticCodes.RuntimeCodePrefix}' prefix.", nameof(code));
        return new GesDiagnostic(
            GesDiagnosticPhase.Runtime,
            code,
            message,
            Symbol: symbol,
            TechnicalDetails: innerException is null ? null : GesRuntimeExceptionText.Describe(innerException));
    }
}

/// <summary>
/// Wraps an unanticipated exception caught at the extension, external-type
/// constructor, or external-value boundary with the failing construct's identity.
/// It is a <see cref="GesFatalRuntimeException"/> so every existing
/// boundary that already special-cases that base type handles it without change.
/// </summary>
internal sealed class GesCallbackFailureException : GesFatalRuntimeException
{
    internal GesCallbackFailureException(string code, string message, string? symbol, Exception innerException)
        : base(BuildDiagnostic(code, message, symbol, innerException), innerException)
    {
    }

    private static GesDiagnostic BuildDiagnostic(string code, string message, string? symbol, Exception innerException)
        => new(
            GesDiagnosticPhase.Runtime,
            code,
            message,
            Symbol: symbol,
            TechnicalDetails: GesRuntimeExceptionText.Describe(innerException));
}

/// <summary>
/// Thrown when a Program cannot be linked to a host, for example because of a missing extension or external type
/// constructor or an exceeded register or call-depth limit. It carries one or more link-phase diagnostics.
/// </summary>
public sealed class GesDynamicLinkException : Exception
{
    // The message stays short for a Program with many call sites; Diagnostics holds them all.
    private const int MaxMessageLocations = 8;

    /// <summary>Creates the exception for a failure with a single diagnostic.</summary>
    /// <param name="diagnostic">The link-phase diagnostic.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="diagnostic"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="diagnostic"/> is not in the link phase.</exception>
    public GesDynamicLinkException(GesDiagnostic diagnostic) : base(BuildMessage([RequireDiagnostic(diagnostic)]))
    {
        if (diagnostic.Phase != GesDiagnosticPhase.Link)
            throw new ArgumentException("A dynamic-link exception requires a link diagnostic.", nameof(diagnostic));
        Diagnostic = diagnostic;
        Diagnostics = [diagnostic];
    }

    /// <summary>
    /// Initializes a new instance for a failure with several diagnostics, such as one per call site of a missing
    /// extension. The exception message names the first locations; it is informative text, not a portable comparison value.
    /// </summary>
    /// <param name="diagnostics">The link diagnostics in source order; at least one.</param>
    public GesDynamicLinkException(IReadOnlyList<GesDiagnostic> diagnostics) : base(BuildMessage(diagnostics))
    {
        var copy = new GesDiagnostic[diagnostics.Count];
        for (var index = 0; index < copy.Length; index++)
        {
            var diagnostic = diagnostics[index] ?? throw new ArgumentException("Diagnostic list contains null.", nameof(diagnostics));
            if (diagnostic.Phase != GesDiagnosticPhase.Link)
                throw new ArgumentException("A dynamic-link exception requires link diagnostics.", nameof(diagnostics));
            copy[index] = diagnostic;
        }

        Diagnostic = copy[0];
        Diagnostics = copy;
    }

    /// <summary>
    /// Gets the first diagnostic of <see cref="Diagnostics"/>.
    /// </summary>
    public GesDiagnostic Diagnostic { get; }

    /// <summary>
    /// Gets every diagnostic of the failure. A failure of an import that the source calls, such as a missing extension,
    /// has one diagnostic per call site with its source location, in source order, when the Program has a SourceMap.
    /// </summary>
    public IReadOnlyList<GesDiagnostic> Diagnostics { get; }

    private static string BuildMessage(IReadOnlyList<GesDiagnostic>? diagnostics)
    {
        if (diagnostics is null) throw new ArgumentNullException(nameof(diagnostics));
        if (diagnostics.Count == 0) throw new ArgumentException("A dynamic-link exception requires at least one diagnostic.", nameof(diagnostics));
        var message = (diagnostics[0] ?? throw new ArgumentException("Diagnostic list contains null.", nameof(diagnostics))).Message;
        var locations = new List<string>();
        var others = 0;
        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic?.SourceLocation is not { } location) continue;
            if (locations.Count < MaxMessageLocations) locations.Add(location.ToString());
            else others++;
        }

        if (locations.Count == 0) return message;
        var located = $"{message} at {string.Join(", ", locations)}";
        return others == 0 ? located : $"{located} and {others} more";
    }

    private static GesDiagnostic RequireDiagnostic(GesDiagnostic? diagnostic)
        => diagnostic ?? throw new ArgumentNullException(nameof(diagnostic));
}
