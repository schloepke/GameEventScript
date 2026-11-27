// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Stable, language-neutral diagnostic codes shared by both ports. Compare `GesDiagnostic.code` against these
/// constants instead of diagnostic text.
public enum GesDiagnosticCodes {
    /// Initialization failed because a configured runtime safety limit was exhausted.
    public static let runtimeInitializationLimitReached = "runtime.initializationLimitReached"

    /// Defines the parse syntax value.
    public static let parseSyntax = "parse.syntax"

    /// Source exceeds the portable expression or statement nesting limit.
    public static let parseSourceNestingExceeded = "parse.sourceNestingExceeded"

    /// Defines the validate duplicate type value.
    public static let validateDuplicateType = "validate.duplicateType"

    /// Defines the validate duplicate predicate value.
    public static let validateDuplicatePredicate = "validate.duplicatePredicate"

    /// Defines the validate duplicate function value.
    public static let validateDuplicateFunction = "validate.duplicateFunction"

    /// Defines the validate predicate function conflict value.
    public static let validatePredicateFunctionConflict = "validate.predicateFunctionConflict"

    /// Defines the validate missing callable value.
    public static let validateMissingCallable = "validate.missingCallable"

    /// Defines the validate invalid predicate value.
    public static let validateInvalidPredicate = "validate.invalidPredicate"

    /// Defines the validate wrong predicate arity value.
    public static let validateWrongPredicateArity = "validate.wrongPredicateArity"

    /// Defines the validate wrong function arity value.
    public static let validateWrongFunctionArity = "validate.wrongFunctionArity"

    /// Defines the validate duplicate handler parameter value.
    public static let validateDuplicateHandlerParameter = "validate.duplicateHandlerParameter"

    /// Defines the validate duplicate definition parameter value.
    public static let validateDuplicateDefinitionParameter = "validate.duplicateDefinitionParameter"

    /// Defines the validate duplicate publish argument value.
    public static let validateDuplicatePublishArgument = "validate.duplicatePublishArgument"

    /// Defines the validate duplicate variable value.
    public static let validateDuplicateVariable = "validate.duplicateVariable"

    /// Defines the validate duplicate constant value.
    public static let validateDuplicateConstant = "validate.duplicateConstant"

    /// Defines the validate shadowed variable value.
    public static let validateShadowedVariable = "validate.shadowedVariable"

    /// Defines the validate unresolved variable value: a bare name without a visible binding of its own source.
    public static let validateUnresolvedVariable = "validate.unresolvedVariable"

    /// Defines the validate invalid identifier case value.
    public static let validateInvalidIdentifierCase = "validate.invalidIdentifierCase"

    /// Defines the validate invalid message case value.
    public static let validateInvalidMessageCase = "validate.invalidMessageCase"

    /// Defines the validate invalid type constructor value.
    public static let validateInvalidTypeConstructor = "validate.invalidTypeConstructor"

    /// A tag clause contains the literal `nothing`.
    public static let validateInvalidTag = "validate.invalidTag"

    /// A `without` tag filter uses `|` alternatives, which only `matching` filters allow.
    public static let validateInvalidTagFilter = "validate.invalidTagFilter"

    /// An `@Name` names no handle type of the compilation's handle types.
    public static let validateUnknownHandleType = "validate.unknownHandleType"

    /// A handle type is used where it is not allowed: `as @Name` outside a parameter or field, `@Name~` or `h.version`
    /// of a type without versions, or a union mixing handle and other types.
    public static let validateInvalidHandleUse = "validate.invalidHandleUse"

    /// A projected handle type names no projection of the type's state, projects a composite, stands beside the same type
    /// without projection in a union, or reads a member that is no field of its projection.
    public static let validateInvalidProjection = "validate.invalidProjection"

    /// A projected handle passes to a wider view, or `as` converts it to a schema its projection does not narrow.
    public static let validateHandleWidening = "validate.handleWidening"

    /// Defines the compile unsupported construct value.
    public static let compileUnsupportedConstruct = "compile.unsupportedConstruct"

    /// Defines the compile invalid arity value.
    public static let compileInvalidArity = "compile.invalidArity"

    /// Defines the compile unresolved symbol value.
    public static let compileUnresolvedSymbol = "compile.unresolvedSymbol"

    /// Defines the compile numeric limit exceeded value.
    public static let compileNumericLimitExceeded = "compile.numericLimitExceeded"

    /// Defines the compile cyclic call graph value.
    public static let compileCyclicCallGraph = "compile.cyclicCallGraph"

    /// Defines the compile invalid resource metadata value.
    public static let compileInvalidResourceMetadata = "compile.invalidResourceMetadata"

    /// Defines the compile invariant violation value.
    public static let compileInvariantViolation = "compile.invariantViolation"

    /// Defines the link required register count exceeded value.
    public static let linkRequiredRegisterCountExceeded = "link.requiredRegisterCountExceeded"

    /// Defines the link required call stack depth exceeded value.
    public static let linkRequiredCallStackDepthExceeded = "link.requiredCallStackDepthExceeded"

    /// Loading requires an initialization message but the host's logical-message queue is full.
    public static let linkInitializationQueueFull = "link.initializationQueueFull"

    /// Defines the link invalid extension reference value.
    public static let linkInvalidExtensionReference = "link.invalidExtensionReference"

    /// Defines the link missing extension value.
    public static let linkMissingExtension = "link.missingExtension"

    /// Defines the link missing external type constructor value.
    public static let linkMissingExternalTypeConstructor = "link.missingExternalTypeConstructor"

    /// Defines the link mismatched external type constructor value.
    public static let linkMismatchedExternalTypeConstructor = "link.mismatchedExternalTypeConstructor"

    /// The Program reads the state of a handle type that has no registered resolver.
    public static let linkMissingHandleResolver = "link.missingHandleResolver"

    /// The handle type registered with a resolver has another schema hash than the Program's.
    public static let linkMismatchedHandleType = "link.mismatchedHandleType"

    /// Defines the link cyclic call graph value.
    public static let linkCyclicCallGraph = "link.cyclicCallGraph"

    /// Defines the link invalid program value.
    public static let linkInvalidProgram = "link.invalidProgram"

    /// Defines the runtime vm state conflict value.
    public static let runtimeVmStateConflict = "runtime.vmStateConflict"

    /// Defines the runtime preparation failed value.
    public static let runtimePreparationFailed = "runtime.preparationFailed"

    /// Defines the runtime instruction pointer out of range value.
    public static let runtimeInstructionPointerOutOfRange = "runtime.instructionPointerOutOfRange"

    /// Defines the runtime illegal opcode value.
    public static let runtimeIllegalOpcode = "runtime.illegalOpcode"

    /// Defines the runtime register overflow value.
    public static let runtimeRegisterOverflow = "runtime.registerOverflow"

    /// Defines the runtime call stack overflow value.
    public static let runtimeCallStackOverflow = "runtime.callStackOverflow"

    /// Defines the runtime random stack underflow value.
    public static let runtimeRandomStackUnderflow = "runtime.randomStackUnderflow"

    /// Defines the runtime random scope imbalance value.
    public static let runtimeRandomScopeImbalance = "runtime.randomScopeImbalance"

    /// Defines the runtime invalid record constructor value.
    public static let runtimeInvalidRecordConstructor = "runtime.invalidRecordConstructor"

    /// Defines the runtime invalid extension binding value.
    public static let runtimeInvalidExtensionBinding = "runtime.invalidExtensionBinding"

    /// Defines the runtime invalid external type binding value.
    public static let runtimeInvalidExternalTypeBinding = "runtime.invalidExternalTypeBinding"

    /// Defines the runtime invalid message shape value.
    public static let runtimeInvalidMessageShape = "runtime.invalidMessageShape"

    /// Defines the runtime invalid series kind value.
    public static let runtimeInvalidSeriesKind = "runtime.invalidSeriesKind"

    /// Defines the runtime unhandled failure value.
    public static let runtimeUnhandledFailure = "runtime.unhandledFailure"

    /// Defines the runtime native handler failure value.
    public static let runtimeNativeHandlerFailure = "runtime.nativeHandlerFailure"

    /// Defines the runtime publish sink failure value.
    public static let runtimePublishSinkFailure = "runtime.publishSinkFailure"

    /// An extension function threw an exception other than a deliberately reported <see cref="GesExtensionFaultException"/>.
    public static let runtimeExtensionCallFailed = "runtime.extensionCallFailed"

    /// An external-type constructor threw an exception other than a deliberately reported <see cref="GesExtensionFaultException"/>.
    public static let runtimeExternalConstructorFailed = "runtime.externalConstructorFailed"

    /// An <see cref="IGesExternalValue"/> field access threw an exception other than a deliberately reported <see cref="GesExtensionFaultException"/>.
    public static let runtimeExternalFieldAccessFailed = "runtime.externalFieldAccessFailed"

    /// A handle resolver threw or reported a failure, or a state read found no resolver for its type.
    public static let runtimeHandleResolveFailed = "runtime.handleResolveFailed"

    /// A handle resolver answered without a result, with two results, or with a state that does not fit the state
    /// schema.
    public static let runtimeInvalidHandleState = "runtime.invalidHandleState"

    /// A passed or computed handle version differs from the version the handle carries or from the content hash.
    public static let runtimeHandleVersionMismatch = "runtime.handleVersionMismatch"

    /// A handle resolver answered that the asking party may not see an object; reported to the runtime observer only, the
    /// read gives `nothing` and the handler continues.
    public static let runtimeHandleNotVisible = "runtime.handleNotVisible"
}
