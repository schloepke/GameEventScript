<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Portable diagnostics specification

> **Since: 0.1.0**

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

Parse uses `parse.syntax` for malformed syntax and `parse.sourceNestingExceeded`
for the portable source limits defined in [Language](Language.md#source-nesting-limits).
Validation uses the following semantic codes:

- `validate.duplicateType`, `validate.duplicatePredicate`,
  `validate.duplicateFunction`, `validate.predicateFunctionConflict`;
- `validate.missingCallable`, `validate.invalidPredicate`,
  `validate.wrongPredicateArity`, `validate.wrongFunctionArity`;
- `validate.duplicateHandlerParameter`,
  `validate.duplicateConstant`,
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
`link.invalidProgram`. `link.initializationQueueFull` rejects loading when the
host cannot enqueue the instance's initialization snapshot. It carries the
Program's module name in `programName`; its atomicity and retry rules are defined
in [Host runtime](HostRuntime.md#loading-and-startup).

Runtime uses these stable codes:

- `runtime.vmStateConflict`, `runtime.preparationFailed`,
  `runtime.instructionPointerOutOfRange`, and `runtime.illegalOpcode`;
- `runtime.registerOverflow`, `runtime.callStackOverflow`,
  `runtime.randomStackUnderflow`, and `runtime.randomScopeImbalance`;
- `runtime.invalidRecordConstructor`, `runtime.invalidExtensionBinding`,
  `runtime.invalidExternalTypeBinding`, `runtime.invalidMessageShape`, and
  `runtime.invalidSeriesKind`;
- `runtime.unhandledFailure`, `runtime.nativeHandlerFailure`, and
  `runtime.publishSinkFailure`;
- `runtime.extensionCallFailed`, `runtime.externalConstructorFailed`, and
  `runtime.externalFieldAccessFailed`.

`runtime.randomStackUnderflow` reports an attempted pop across an active runtime
boundary, while `runtime.randomScopeImbalance` reports scopes left open by a
successfully returning native or extension callback. Exceeding
`MaxRandomScopeDepth` is a runtime-limit event rather than a diagnostic. New
codes may be added; an existing code must not be repurposed.

If an extension callback fails, random-scope cleanup still restores its parent
stream, but must not replace the callback's original failure diagnostic with a
scope-cleanup diagnostic.

An extension function, external-type constructor, or external-value field
accessor may throw `GameEventScriptExtensionFaultException` to deliberately
report a defined failure with its own caller-chosen code, message, and optional
symbol; that code must not use the reserved `runtime.` prefix and is therefore
never one of the codes enumerated above. Any other exception from the same
boundary is unanticipated and is reported with the matching
`runtime.extensionCallFailed`, `runtime.externalConstructorFailed`, or
`runtime.externalFieldAccessFailed` code instead. Both outcomes end only the
current handler; the host remains reusable for subsequently enqueued messages,
as for any other runtime error. Neither ever evaluates to `nothing`.

Native handlers may likewise deliberately report a caller-chosen fault with
`GameEventScriptExtensionFaultException`. Unanticipated native-handler exceptions
use `runtime.nativeHandlerFailure`.

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

For handler diagnostics, the Host and VM fill missing `programName` and
`handlerName` from the active Program and handler before reporting the error,
including failures during VM preparation and native callbacks. Native handlers
have a dispatch signature but no owning Program, so they supply `handlerName`
without inventing a `programName`.
Context already supplied by the diagnostic is preserved independently for each
field. The original diagnostic remains immutable; unavailable context stays
absent.

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

Initialization safety-limit failures expose `runtime.initializationLimitReached`
in StartResult, with the instance context when available. Limit name and bound
remain structured runtime-limit observations. Ordinary init faults preserve their
original runtime phase/code. The owning lifecycle rules are in HostRuntime.
