# Portable diagnostics specification

This document defines the language-neutral diagnostic contract shared by the
compiler, `.gesb` decoder, dynamic linker, host, and VM.

## Diagnostic shape

Every diagnostic contains these required fields:

- `phase`: `parse` (1), `validate` (2), `compile` (3), `decode` (4),
  `link` (5), or `runtime` (6);
- `code`: a stable ASCII identifier such as `validate.missingCallable`;
- `message`: a human-readable explanation that may change and must never decide
  portable conformance.

It may additionally contain `symbol`, `symbolKind`, `sourceLocation`,
`programName`, `handlerName`, and `technicalDetails`. Missing data is represented
by absence/null, never by a magic string. `technicalDetails` is intended for
logs and may contain platform-specific exception information; portable tests
must not match it.

Source locations use the compiler's portable source contract: source name plus
1-based Unicode-scalar line/column positions. Program and handler identify the
runtime/link context. Symbol kinds are the numerically specified
`GameEventScriptSymbolKind` values.

## Stable codes

Parse uses `parse.syntax`. Validation uses the following semantic codes:

- `validate.duplicateType`, `validate.duplicatePredicate`,
  `validate.duplicateFunction`, `validate.predicateFunctionConflict`;
- `validate.missingCallable`, `validate.invalidPredicate`,
  `validate.wrongPredicateArity`, `validate.wrongFunctionArity`;
- `validate.duplicateHandlerParameter`,
  `validate.duplicateDefinitionParameter`,
  `validate.duplicatePublishArgument`, `validate.duplicateVariable`,
  `validate.shadowedVariable`;
- `validate.invalidIdentifierCase`, `validate.invalidMessageCase`,
  `validate.invalidTypeConstructor`.

Compiler lowering/resource analysis uses `compile.unsupportedConstruct`,
`compile.invalidArity`, `compile.unresolvedSymbol`,
`compile.numericLimitExceeded`, `compile.cyclicCallGraph`,
`compile.invalidResourceMetadata`, and `compile.invariantViolation`.

Every existing `GameEventScriptProgramFormatErrorCode` maps losslessly to
`decode.<lowerCamelErrorCode>`, for example `InvalidMagic` maps to
`decode.invalidMagic`. The format exception keeps byte offset, section type, and
entry index as its specialized structured fields as well.

Linking uses `link.requiredRegisterCountExceeded`,
`link.requiredCallStackDepthExceeded`, `link.invalidExtensionReference`,
`link.missingExtension`, `link.missingExternalTypeConstructor`,
`link.mismatchedExternalTypeConstructor`, `link.cyclicCallGraph`, and
`link.invalidProgram`.

Runtime uses the constants declared by `GameEventScriptDiagnosticCodes`. They
cover VM preparation/state, instruction and capacity failures, invalid linked
bindings and value shapes, unhandled VM/native failures, and publish-sink
failures. New codes may be added; an existing code must not be repurposed.

## Transport and execution

Exceptions are only the C# transport mechanism. `GameEventScriptCompileException`,
`GameEventScriptProgramFormatException`, and
`GameEventScriptDynamicLinkException` expose portable diagnostic data; callers
must branch on phase/code rather than exception message or CLR subtype details.

Runtime handler failures abort and reset only the failing handler. Remaining
handlers from the immutable subscription snapshot continue in deterministic
order. The observer receives every runtime diagnostic. The execution result has
state `RuntimeError` and carries the first handler diagnostic observed during
that pump call. Successful, paused, and runtime-limit results carry no
diagnostic.

A publish-sink exception remains a rejected outbound attempt: local dispatch is
unchanged, the observer receives `runtime.publishSinkFailure`, and the sink
failure alone does not turn the execution result into `RuntimeError`.

Diagnostics are allocated only on error paths. Successful queue dispatch,
handler selection, VM stepping/resume, and execution-result creation retain the
zero-allocation hot-path contract after warmup.

## Conformance expectations

Portable Markdown/YAML error expectations match `phase` plus `code` and may additionally
match symbol/source/program/handler fields. `messageContains` is forbidden.
Runtime steps use `expectedRuntimeDiagnostics` in observer order. Human-readable
messages and technical details are deliberately excluded from pass/fail rules.
