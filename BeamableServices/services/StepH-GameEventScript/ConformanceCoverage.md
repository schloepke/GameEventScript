# Portable Conformance Coverage

This is the normative migration index from behavior to stable Conformance
Markdown case IDs. Keep IDs stable. A language-specific test may be removed or
reduced only after the behavior it protects has a passing portable case listed
here. Add or update this matrix in the same change as new portable behavior.

## Portable semantic coverage

| Behavior | Stable portable case IDs |
| --- | --- |
| Message normalization, signature construction and matching | `api.messages/case-0001` through `api.messages/case-0006` |
| Explicit ordered arguments and rejection of unordered/duplicate forms | `api.messages/case-0001`, `api.messages/case-0004`, `api.messages/case-0005`, `api.messages/case-0006` |
| Value kinds and primitive/custom value semantics | `runtime.atomic.create-values/case-0009`, `runtime.types-and-values/case-0035` |
| List, Map, Vector, Point, Dice and Range construction | `runtime.atomic.create-values/case-0002` through `runtime.atomic.create-values/case-0007` |
| Record and external-value construction | `runtime.atomic.custom-types/case-0001`, `runtime.atomic.custom-types/case-0003` |
| Collection/member/index semantics | `runtime.atomic.member-index-access/case-0008` through `runtime.atomic.member-index-access/case-0016` |
| Random known-answer vectors and nested streams | `runtime.atomic.random/case-0002` through `runtime.atomic.random/case-0005` |
| Compiler handler/program resource metadata | `compile.binary-compiler/case-0001`, `api.messages/case-0007` |
| Direct recursive call rejection | `compile.build-errors/case-0001` |
| Native-only Host | `runtime.host-lifecycle/native-only` |
| Same immutable Program in independent Hosts | `runtime.host-lifecycle/shared-program-multiple-hosts` |
| Multiple Programs and deterministic load order | `runtime.host-dispatch/case-0007` |
| VM reset between handlers/messages | `runtime.host-lifecycle/vm-reset-between-handlers` |
| Receive, local Emit and local-plus-outbound Publish | `runtime.atomic.external-access/case-0008`, `runtime.publish-tags/case-0001` |
| Publish with absent, accepting, rejecting and throwing sink | `runtime.host-observer/publish-without-sink`, `runtime.host-observer/publish-accepted`, `runtime.host-observer/publish-rejected`, `runtime.host-observer/publish-sink-exception` |
| Full Publish result | the four `runtime.host-observer/publish-*` cases |
| Ordered Emit/Publish/Dispatch/limit/diagnostic observer events | `runtime.host-observer/publish-sink-exception`, `runtime.host-observer/emit-and-runtime-limit-order` |
| Load/Detach/Subscribe/Unsubscribe during dispatch | `runtime.host-lifecycle/detach-unsubscribe-snapshot`, `runtime.host-lifecycle/load-subscribe-after-dispatch` |
| Enqueue-time subscription snapshot | `runtime.host-lifecycle/detach-unsubscribe-snapshot` |
| Initialization once per instance and load ordering | `runtime.host-dispatch/case-0008`, `runtime.host-lifecycle/load-subscribe-after-dispatch` |
| Queue limits count logical messages | `runtime.control-flow/case-0006` |
| Runtime limits reset per script handler | `runtime.host-lifecycle/runtime-limits-per-handler` |
| Pause/resume under frame budget | `runtime.host-dispatch/case-0009` |
| External types without Reflection | `runtime.atomic.custom-types/case-0003` |
| Fixed extension environment (`echo`, `fail`, `floor`, navigation) | `runtime.atomic.external-access/case-0010`, `runtime.atomic.control-flow/case-0002`, `runtime.extensions-sequences/case-0001` |

Indirect cyclic bytecode cannot be produced by valid GES source: forward calls
are rejected before a multi-node source cycle can be formed. Its trust-boundary
test therefore remains the internal builder/validator test
`GesBinaryBuilderTests.BuildRejectsIndirectRecursiveCall` until 5.9 introduces
invalid `.gesb` resources. At that point the fixture's stable portable ID must
replace this paragraph before the C#-specific builder test is reduced.

## Intentionally language-specific coverage

The following tests remain implementation tests even when the underlying
semantics also has a portable case:

- C# public API snapshots;
- Reflection, attributes and CLR conversion adapters;
- C# auto-runner synchronization and threading;
- internal builder/rewriter structure and optimizer pass tests;
- concrete C# struct layouts;
- C# allocation measurements and platform/runtime benchmark profiles.

These tests must not become required behavior for Swift, Kotlin or C++ ports.
