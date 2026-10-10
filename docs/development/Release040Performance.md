<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# 0.4.0 performance validation findings

Status: resolved; targeted fixes, reviewed references and both performance gates pass.

The original findings below are retained to explain the reference changes.
See the resolution section for the current disposition.

The 2026-10-10 development run used product sources at
`0d9646d20f80bcf00dbb88de759c7b8b27eca473`, plus the new release-evidence wrapper
and documentation changes. No runtime, compiler or baseline changes were made
for this run. The machine was Apple M3 Max, macOS 26.6.2 (25G83), .NET SDK
10.0.201 / test runtime 8.0.6, and Apple Swift 6.4
(`swiftlang-6.4.0.34.1`, clang `2100.3.34.1`). C# and Swift ran sequentially in
Release. These dirty-tree measurements identify issues; they are not a clean
final-candidate release qualification.

## C#

42 of 45 test executions passed; three shared performance cases exceeded their
AST-build allocation budgets. All 16 shared zero-allocation workloads and all
10 text-conversion workloads passed. Native performance/allocation tests also
passed. The failing metrics are:

| Case in `performance.core` | Metric | Allowed KiB | Measured KiB |
| --- | --- | ---: | ---: |
| `case-0002` — range select and aggregate | `ast-build.allocated` | 15.109 | 15.813 |
| `case-0003` | `ast-build.allocated` | 19.094 | 19.695 |
| `case-0005` — mixed pipelines, control flow and messages | `ast-build.allocated` | 84.055 | 85.023 |

## Swift

30 of 36 cases passed, including five bytecode snapshots; six measured workloads
failed. All 16 warmed dispatch workloads retained **zero requested bytes and
zero allocation events**. Failing metrics:

| Case | Metric | Allowed | Measured |
| --- | --- | ---: | ---: |
| `performance.core/case-0001` | `compile.elapsed` (ms) | 0.26770875 | 0.344042 |
| `performance.core/case-0002` | `run.allocations` | 14700 | 16000 |
| `performance.core/case-0004` | `program-load.allocated` (B) | 16527 | 17945 |
| `performance.core/case-0004` | `run.allocations` | 7350 | 9000 |
| `performance.core/case-0005` | `run.allocations` | 38850 | 40000 |
| `performance.text-conversion/format-percentage-1000` | `run.allocated` (B) | 561750 | 575000 |
| `performance.text-conversion/format-percentage-1000` | `run.allocations` | 11550 | 12000 |
| `performance.text-conversion/format-percentage-1000` | `run.per-invoke-allocated` (B) | 562 | 575 |
| `performance.text-conversion/format-percentage-2000` | `run.allocated` (B) | 1123500 | 1150000 |
| `performance.text-conversion/format-percentage-2000` | `run.allocations` | 23100 | 24000 |
| `performance.text-conversion/format-percentage-2000` | `run.per-invoke-allocated` (B) | 562 | 575 |

Allocation counts and bytes are separate metrics. The timing observation alone
does not establish its cause; investigate under controlled load. These results
also do not establish which earlier change introduced any allocation increase.

## Original follow-up

Investigate compiler allocation growth, Swift collection/pipeline allocation
counts, program loading and Percentage formatting against the reviewed baseline.
Resolve regressions or explicitly review a justified baseline change, then run
both gates against the final clean candidate. Do not automatically accept these
measurements as new references. Performance optimization beyond this
investigation remains separate from release-evidence tooling.

Original local logs and receipts are retained below ignored
`artifacts/release-performance/run-ncztwpo9` (C#) and
`artifacts/release-performance/run-jzim3mrn` (Swift). The C# TRX contains the
failure details; the Swift directory also contains all samples and work counters.
The C# collection control rerun in `artifacts/release-performance/run-sz9g5qdq`
retains all three shared result reports and reproduces the same three allocation
failures; it does not replace the failed result with a pass.
Use the [release performance procedure](../guide/distribution/Performance.md)
to reproduce and retain fresh evidence. This source document records the findings and their resolution; it does not
replace generated release reports.

## Resolution and reference review

C# commit `d2797ede` introduced the three AST allocation failures: the immediately
preceding revision passes the same five core workloads on the same host. Every
AST node acquired a binding-list field, and every single binder acquired a List.
Binding storage now belongs only to selectors and `for` nodes. The existing
identifier represents a single binder without a list; multiple binders preserve
their ordered list. Scope validation also avoids temporary single-element arrays
and boxed enumeration. Language behavior, shadowing and bytecode are unchanged.

| C# AST allocation (KiB) | Before fix | After fix |
| --- | ---: | ---: |
| `case-0001` | 22.961 | 22.266 |
| `case-0002` | 15.813 | 15.164 |
| `case-0003` | 19.695 | 19.102 |
| `case-0004` | 31.313 | 29.625 |
| `case-0005` | 85.023 | 80.047 |

Only two C# reference/maximum pairs change: 15.109 to 15.164 KiB and 19.094 to
19.102 KiB. The remaining approximately 56/8 bytes beyond the old bounds are
consistent with required binder metadata and compact-loop/choice parser state.
No timing, VM allocation or other C# budget is relaxed.

Swift's register redesign in `780eb908` uses compact scalar/reference storage
instead of the previous large associated-value enum. Measured against the
original calibration revision on the same host:

| Workload | Allocation events per invocation, old → current | Requested bytes per invocation, old → current |
| --- | ---: | ---: |
| Range/select | 14 → 16 | 16529 → 7793 |
| Filter/select/sum | 7 → 9 | 2465 → 1185 |
| Mixed pipelines/control/messages | 37 → 40 | 4333 → 2629 |

The additional reference payloads explain the event-count increases while
aggregate bytes fall substantially. Percentage formatting adds one reference
payload: 535 to 567 bytes after the register redesign, then 575 after moving the
scalar count into the text payload in `8af00599`. The formatting algorithm did
not change. These allocations are accepted costs of the reviewed storage
architecture, not reasons to restore the larger register representation.

Normal Swift iterator instructions now reuse the fixed operand description.
This removes 210 bytes from `case-0004` program loading (17945 to 17735 bytes).
An isolated diagnostic experiment attributes another 1248 bytes to the internal
VM-value escape validator introduced in `ef4e44f9`. That validator remains fully
active in the product. Its necessary loading cost is included in the reviewed
reference rather than removed to satisfy the old budget.

Timing is qualified separately: the original `2f6fd35f` calibration source also
exceeds its old `case-0001` compiler-time bound on the current measurement host.
Consequently that observation alone is not evidence of a new compiler regression.
Nine fresh-process measurements per revision, with alternating order in the
six additional pairs, give medians of **0.396042 ms old / 0.396709 ms current**.
The ranges are 0.214459–0.443500 ms and 0.229000–0.444708 ms respectively. The
approximately 0.2% median difference does not establish a meaningful compiler
regression; both revisions show substantial host-sensitive variation. The new
reference is 0.396709 ms with the unchanged 25% tolerance policy. Other timing
budgets and all zero-dispatch allocation budgets remain unchanged.

The baseline preparation helper checked all nine current measurement identities,
source hashes and environments. Only eleven Swift metrics were accepted: this
one compiler time, three execution allocation counts, one loading byte count,
and three Percentage metrics at each of 1000/2000 invocations. Allocations use
the maximum across processes and the existing 5% allowance. The reviewed C#
references passed three consecutive complete performance gates. Raw comparisons
and prepared candidates are retained under ignored `artifacts/performance-refresh`.
The cross-language corpus reference is regenerated because budget edits change
its source hash; scenario expectations are unchanged.


## Verification after resolution

The complete C# correctness run passes 2483 test executions. The Swift native
compiler corpus passes 1953/1953 cases; strict non-timing acceptance passes 1922
and intentionally skips 31 performance measurements. Independent Runtime
verification passes 1558/1558 C#-compiled scenarios. Native package/CLI checks,
numeric-text and product-JSON interchange, binary-reader fuzzing and binary
roundtrip property checks also pass. The corpus identity gate was rerun after
regenerating its reference hash.

The combined development performance gate passes all 45 C# test executions,
31 measured C# workloads and 31 measured Swift workloads (36 Swift cases with
five GESA checks). All sixteen warmed dispatch workloads retain zero requested
bytes and allocation events. The development receipt is retained under ignored
`artifacts/release-performance/run-8s_b6p20`; it covers the reviewed dirty tree,
not a tagged release. A final clean revision must still retain its own generated
receipt using the release procedure. Ordinary Swift CI has no added work.
