<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Swift performance baseline

The shared workloads and correctness expectations live in
[`conformance/suites/performance`](../../conformance/suites/performance).
Their Swift profile is `swift-6.4-release-macos26-arm64-m3max`. It was calibrated
on Apple M3 Max, macOS 26.6.2 (25G83), Apple Swift 6.4
(`swiftlang-6.4.0.34.1`, clang `2100.3.34.1`), SDK 27.0, Release, on 2026-09-19.
A selective refresh on 2026-10-10 resolves the reviewed
[0.4.0 findings](../../docs/development/Release040Performance.md): compact value
storage, mandatory loading validation and the reproduced compiler-time baseline.
Only the documented affected metrics changed; zero-dispatch budgets stay zero.
A full refresh on 2026-10-14 follows the manually owned register buffer and
runtime fast paths. Three independent production Release processes supplied the
references; a separate verification process checks the resulting budgets.
Existing allocation upper bounds were never increased, including the two
slightly higher measured references described below.
This is an implementation regression baseline. Other hardware/toolchains need
an explicitly calibrated profile; raw C# and Swift timings are not equivalence
criteria. The adapter rejects Debug builds and incompatible profile hosts.

## Verify

```bash
./scripts/test-swift-performance.sh
```

This builds Release and runs all shared performance documents, including their
GESA snapshots. Before measurement, each workload must pass ordinary correctness
verification. No reference is changed by this command. The process fails on a
regression, unavailable instrumentation, wrong profile or malformed measurement.

For a complete corpus report with measured performance, use the built tool:

```bash
python3 conformance/runner/main.py run \
  --performance-profile swift-6.4-release-macos26-arm64-m3max \
  --output artifacts/swift/measured-conformance/ConformanceResults.json \
  --adapter artifacts/swift/conformance/release/ges-conformance-adapter
```

Ordinary `scripts/test-swift.sh` is independent of the measurement host and
selects ordinary correctness cases with `--kind supported` and additionally checks
the 31 performance scenarios without measurement, producing `PerformanceCorrectness.json`. Python's runner tests
exercise performance-result validation using controlled readings; those tests
do not count as allocation evidence. Performance verification must
run separately, without concurrent benchmarks or builds.

## Measurement scope

The native executable uses a small C instrumentation target; Runtime and Compiler
do not depend on it; instrumentation is linked only into the internal adapter. It observes successful
allocation events through Apple's exported
[`libmalloc` logger](https://github.com/apple-oss-distributions/libmalloc/blob/main/src/malloc.c).
This is a platform instrumentation interface, not a Runtime dependency or a
public API guaranteed across future SDKs. A missing hook, an already installed
foreign logger or failed controls stops measurement.

- Allocation metrics sum **requested bytes**, including allocations freed during
  the measured interval. They do not report retained heap size, allocator size
  classes, RSS or peak memory. Successful reallocations count the new requested
  size as another allocation event.
- Scope is libmalloc events on the synchronous calling thread. Direct VM mappings,
  private allocators bypassing those entry points, other threads and stack storage
  are outside coverage. Swift uses ARC; freed objects still contribute to the sum.
- Controls check empty intervals, malloc/calloc/realloc, zone/aligned allocations,
  allocations freed within the interval and escaping Swift objects, Arrays and
  Strings. Counter overflow, nesting and hook replacement invalidate a sample.
- Each workload has five samples in fresh Hosts. Declared compiler and execution
  warmups are discarded. Elapsed time uses a monotonic clock and the median;
  allocations use the maximum independently of timing. Instrumentation is active
  during timing, so timings include its overhead.
- `compile.*` includes builder/source parsing, validation, lowering, optimization,
  Program validation and any requested binary roundtrip. `program-load.*` includes
  a fresh Host, linking and initialization. These scopes are defined independently
  of C#'s internal compilation phases.
- `run.*` includes enqueueing reused inputs and the declared completion or frame
  pumping. Compilation, linking, initialization, warmup, input creation and reports
  are outside this interval. Optional observers count callbacks without allocations.
- Per-invocation metrics divide by the declared iteration count. Each iteration
  executes all declared Steps. Counters for instructions, messages, emits,
  publishes, pauses and callbacks must match a prepared iteration scaled by that
  count; measurement cannot pass by skipping work.

`ConformanceResults.json` retains adapter evidence with environment identity,
all samples, allocation counts and work counters, together with Python-owned
measured/reference/allowed values and verdicts. Explicit calibration writes
`SwiftPerformance.measurement.json`, adding the exact Markdown hashes for baseline
preparation. No expectations or budgets are sent to the Swift adapter.
All generated evidence stays below ignored `artifacts`.

## Baseline review and refresh

Three independent process runs establish the baseline. Time references are the
median of the three process medians; allocation references and allocation counts are the largest values
observed in any sample. `run.allocations` additionally guards the number of
allocation events, including a hypothetical zero-byte request. Time tolerance is 25%, with a 0.05 ms absolute floor
(divided by iteration count for per-invocation values). Nonzero allocation budgets
allow 5%, rounded upward; zero remains exactly zero. Bounds are explicit in Markdown.

```bash
./scripts/test-swift-performance.sh --calibrate artifacts/swift/calibration-1
./scripts/test-swift-performance.sh --calibrate artifacts/swift/calibration-2
./scripts/test-swift-performance.sh --calibrate artifacts/swift/calibration-3
python3 scripts/prepare-swift-performance-baseline.py \
  artifacts/swift/calibration-1/SwiftPerformance.measurement.json \
  artifacts/swift/calibration-2/SwiftPerformance.measurement.json \
  artifacts/swift/calibration-3/SwiftPerformance.measurement.json
```

Calibration records evidence; it is not a regression pass. The preparation script
verifies matching source hashes and environments and writes reviewable
`*.received.md` candidates under `artifacts/swift/performance-baseline`. It never
edits the corpus. Inspect unexplained increases, review the candidate diff, then
copy approved profile changes into the original Markdown. Re-run regression
verification in a fresh process. A zero-dispatch target cannot be relaxed by the
preparation script.

All sixteen warmed dispatch cases still measure **zero bytes and zero allocations**.
The 2026-10-14 refresh compares the checked-in references with three fresh
process measurements (each containing five samples):

| Swift workload (1,000 invocations) | Run time, old → new (ms) | Requested run bytes, old → new |
| --- | ---: | ---: |
| Arithmetic | 1.918416 → 0.794375 | 192000 → 128000 |
| Range/select | 28.8175 → 8.206708 | 16641000 → 7988000 |
| Record construction/casts | 2.791708 → 1.347667 | 2984000 → 416000 |
| Filter/select/sum | 11.859583 → 3.283125 | 2465000 → 1380000 |
| Mixed pipelines/control/messages | 426.603 → 63.067334 | 4333000 → 4124000 |

These are comparisons with historical corpus references, not an isolated
before/after attribution to the latest commit or a cross-language ranking.
No run-allocation count increased. Range/select loading grows from 13544 to
13670 requested bytes (+126 B, 0.93%). Mixed-workload compilation grows from
864263 to 875838 bytes (+11575 B, 1.34%). These are load/compile costs, not
per-invocation VM costs. Exact causality was not isolated; their existing upper
bounds remain 14222 and 907477 bytes respectively.

Evidence is retained under `artifacts/swift/performance-refresh-{1,2,3}`;
reviewed candidates under `artifacts/swift/performance-refresh-candidates`;
fresh verification under `artifacts/swift/performance-after-refresh`.

Performance measurements use the production Release build made by
`scripts/test-swift-performance.sh`. Run that script after `swift test`, rather
than measuring its existing executable: a Release test build uses
`-enable-testing`, which exposes internal storage and changes Swift's optimization
of runtime access checks. Its timing is not comparable to this production profile.
