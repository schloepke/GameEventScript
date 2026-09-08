<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Portable Conformance Coverage

This is the normative coverage index from behavior to stable Conformance
Markdown case IDs. Keep IDs stable. A language-specific test may be removed or
reduced only after the behavior it protects has a passing portable case listed
here. Add or update this matrix in the same change as new portable behavior.

## Portable semantic coverage

| Behavior | Stable portable case IDs |
| --- | --- |
| Reader and canonical writer validate deep acyclic graphs and reject deep cycles within V1 limits without ending the embedding process | all four `program.call-graph-depth` cases: shallow/deep valid chains and invalid cycles, with deep fixtures containing 21,845 routines and 65,535 instructions |
| Exact folded and runtime `div`, `mod`, and `rem` above 2^53, with negative operands, Int64 limits, and binary roundtrip | all 30 `runtime.numeric.integer-precision` cases |
| Number cast Int64 and Quantity identity, including explicit dynamic random seeds and parent-stream restoration | `runtime.numeric.number-casts/identity-*`, `runtime.numeric.number-casts/random-seed-*`, each with `direct` and `binary` variants |
| Folded and runtime text conversion of signed infinity, NaN, invalid text, and finite numbers | `runtime.numeric.number-casts/text-*`, each with `direct` and `binary` variants |
| Conformance comparison rejects wrong numeric storage and infinite units, recursively, in exact and ULP modes | all 48 `api.conformance-comparison` cases, including exact Int64 range fields, nested records/messages, NaN, duplicate keys, and positive tolerance controls |
| Binary resources agree with executable call depth and live frame register requirements | `program.binary-format/valid-direct-call`, `program.binary-format/valid-nested-call`, `program.binary-format/underdeclared-depth-direct-call`, `program.binary-format/underdeclared-depth-nested-call`, `program.binary-format/underdeclared-registers-direct-call`, `program.binary-format/underdeclared-registers-nested-call` |
| Exactly reaching the processed-message limit while draining all work emits no limit observation, for native/script handlers and completion/frame pumps | the eight `runtime.host-observer/processed-limit-*` cases; pending-work control: `runtime.host-observer/emit-and-runtime-limit-order` |
| Message normalization, signature construction and matching | `api.messages/case-0001` through `api.messages/case-0012`, `api.messages/signature-equality`, `api.messages/signature-create-message` |
| Signature, Message and Handler equality/hash contracts | `api.messages/signature-equality`, `api.messages/signature-parameter-inequality`, `api.messages/signature-arity-inequality`, `api.messages/message-equality`, `api.messages/message-argument-inequality`, `api.messages/message-tag-inequality`, `api.messages/unlabeled-message-equality`, `api.messages/handler-equality`, `api.messages/handler-inequality` |
| Explicit ordered arguments and rejection of unordered/duplicate forms | `api.messages/case-0001`, `api.messages/case-0004`, `api.messages/case-0005`, `api.messages/case-0006` |
| Public Value kinds, readers, flags and equality/hash | `api.values/primitives`, `api.values/measured-float`, `api.values/percentage`, `api.values/vector`, `api.values/point`, `api.values/text`, `api.values/tag-case-sensitive`, `api.values/nan-is-nothing` |
| Value container ordering, duplicate keys and defensive copies | `api.values/list-defensive-copy`, `api.values/dice-defensive-copy`, `api.values/map-ordering-and-copy`, `api.values/record` |
| Integer/float Range storage and Message values | `api.values/integer-range`, `api.values/float-range`, `api.values/message-value` |
| List, Map, Vector, Point, Dice and Range construction | `runtime.atomic.create-values/case-0002` through `runtime.atomic.create-values/case-0007` |
| Record and external-value construction | `runtime.atomic.custom-types/case-0001`, `runtime.atomic.custom-types/case-0003` |
| Collection/member/index semantics and canonical map-key spellings | `runtime.atomic.member-index-access/case-0008` through `runtime.atomic.member-index-access/case-0016`, `compile.syntax-errors/case-0042` |
| Random known-answer vectors, non-consuming bounds, valid nested streams, explicit dynamic-seed conversion, invalid-seed parent restoration, and cleanup after runtime limits | `runtime.atomic.random/case-0002` through `runtime.atomic.random/case-0007`, `runtime.random-dice-ranges/case-0005`, `runtime.random-dice-ranges/case-0018`, `compile.build-errors/case-0038`, `compile.build-errors/case-0039`, `compile.bytecode-lowering/case-0012` |
| Quantity application, checks, and unit removal through `:Quantity(none)` | `runtime.types-and-values.numeric-and-units/case-0036` |
| Compiler handler/program resource metadata | `compile.binary-compiler/case-0001`, `api.messages/case-0007` |
| Direct recursive call rejection | `compile.build-errors/case-0001` |
| Callable overload identity by ordered labels, without type dispatch | `runtime.predicates-functions/case-0010`, `compile.build-errors/case-0049` |
| Predicate/function name separation and ambiguous predicate shorthand | `compile.build-errors/case-0021`, `compile.build-errors/case-0050` |
| No lexical shadowing, with sibling branch-name reuse | `compile.build-errors/case-0051` through `compile.build-errors/case-0053`, `runtime.control-flow/case-0013` |
| Indirect cyclic call graph rejection at the untrusted `.gesb` boundary | `program.binary-format/invalid-indirect-call-cycle` |
| Native-only Host | `runtime.host-lifecycle/native-only` |
| Same immutable Program in independent Hosts | `runtime.host-lifecycle/shared-program-multiple-hosts` |
| Multiple Programs and deterministic load order | `runtime.host-dispatch/case-0007` |
| VM reset between handlers/messages | `runtime.host-lifecycle/vm-reset-between-handlers` |
| Receive, local Emit and local-plus-outbound Publish | `runtime.atomic.external-access/case-0008`, `runtime.publish-tags/case-0001` |
| Publish with absent, accepting, rejecting and throwing sink | `runtime.host-observer/publish-without-sink`, `runtime.host-observer/publish-accepted`, `runtime.host-observer/publish-rejected`, `runtime.host-observer/publish-sink-exception` |
| Full Publish result | the four `runtime.host-observer/publish-*` cases |
| Ordered Emit/Publish/Dispatch/limit/diagnostic observer events | `runtime.host-observer/publish-sink-exception`, `runtime.host-observer/emit-and-runtime-limit-order` |
| Load/Detach/Subscribe/Unsubscribe during dispatch | `runtime.host-lifecycle/detach-unsubscribe-snapshot`, `runtime.host-lifecycle/load-subscribe-after-dispatch` |
| Idempotent Subscription/Instance handle results | `runtime.host-lifecycle/subscription-handle-state`, `runtime.host-lifecycle/instance-handle-state` |
| Enqueue-time subscription snapshot | `runtime.host-lifecycle/detach-unsubscribe-snapshot` |
| Initialization once per instance and load/queue ordering | `runtime.host-dispatch/case-0008`, `runtime.host-lifecycle/load-subscribe-after-dispatch`, `runtime.host-lifecycle/initialization-queue-order` |
| Loading while a handler is paused preserves VM state | `runtime.host-lifecycle/load-while-paused` |
| Native message-name matching | `runtime.host-lifecycle/native-message-name-subscription` |
| Native handlers are atomic under frame budgets | `runtime.host-lifecycle/native-handler-frame-atomicity` |
| Queue limits count logical messages and preserve observer order | `runtime.control-flow/case-0006`, `runtime.host-observer/queue-limit-observer-order` |
| Runtime limits reset per script handler | `runtime.host-lifecycle/runtime-limits-per-handler` |
| Pause/resume under frame budget | `runtime.host-dispatch/case-0009` |
| External types without Reflection | `runtime.atomic.custom-types/case-0003` |
| Missing and mismatched external runtime constructors | `compile.external-type-linking/missing-runtime-constructor`, `compile.external-type-linking/mismatched-runtime-constructor` |
| Portable external-type catalog duplicate rejection | `api.external-types/duplicate-type-name` |
| Canonical binding, message-name and embedded-source dumps | `compile.program-dumps/compact-bindings`, `compile.program-dumps/message-name-and-source` |
| Fixed extension environment (`echo`, `fail`, `floor`, navigation) | `runtime.atomic.external-access/case-0010`, `runtime.atomic.control-flow/case-0002`, `runtime.extensions-sequences/case-0001` |

The complete portable `.gesb` fixture manifest is
`program.binary-format` together with `program.call-graph-depth`. It covers canonical and noncanonical valid Programs,
opaque optional data, stable structural errors, stable semantic validation
errors, bounded resource resolution, rewrite identity, and runtime execution.

Cross-language acceptance, corpus identity, shared parser bootstrap fixtures,
and the compact C# reference result are defined in
[Cross-language acceptance](CrossLanguageAcceptance.md). The comparison is keyed exclusively by the
stable IDs in this coverage index and the executable corpus.

## Intentionally language-specific coverage

Shared allocation workloads live in all 16 `performance.low-allocation` cases.
They pair exact and message-name dispatch, counting-observer off/on, completion
with one handler and frames with two handlers, and 1000/2000 iterations. Their
zero bound exercises the existing warmed allocation contract. Native adapters
provide the measurements; a correctness-only corpus pass does not qualify an
allocation profile. The C# managed-thread evidence does not qualify other heaps
or Kotlin/Swift runtimes.

The following tests remain implementation tests even when the underlying
semantics also has a portable case:

- C# public API snapshots;
- Reflection, attributes and CLR conversion adapters;
- C# auto-runner synchronization and threading;
- internal builder/rewriter structure and optimizer pass tests;
- concrete C# struct layouts and defensive-array implementation checks;
- C# allocation measurements and platform/runtime benchmark profiles.

These tests must not become required behavior for Swift, Kotlin or C++ ports.
The test-project structure and retention rule are documented in the
[C# test README](../../implementation/csharp/tests/StepH.GameEventScript.Tests/README.md).
