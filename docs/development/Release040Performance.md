<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# 0.4.0 performance validation findings

Status: **open — measured regression gates do not pass**.

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

## Follow-up before release

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
to reproduce and retain fresh evidence. This source document records the open
findings, not a replacement for generated release reports.
