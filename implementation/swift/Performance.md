<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Swift performance baseline

The shared workloads and correctness expectations live in
[`conformance/suites/performance`](../../conformance/suites/performance).
Their Swift profile is `swift-6.4-release-macos26-arm64-m3max`. It was calibrated
on Apple M3 Max, macOS 26.6.2 (25G83), Apple Swift 6.4
(`swiftlang-6.4.0.34.1`, clang `2100.3.34.1`), SDK 27.0, Release, on 2026-09-19.
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
artifacts/swift/conformance/release/ges-conformance \
  --corpus conformance/suites --binary-fixtures conformance/fixtures \
  --output artifacts/swift/measured-conformance --performance
```

Ordinary `scripts/test-swift.sh` is independent of the measurement host and
continues to report the optional performance capability as unavailable. Its
native tests exercise performance-result validation using controlled readings;
those tests do not count as allocation evidence. Performance verification must
run separately, without concurrent benchmarks or builds.

## Measurement scope

The native executable uses a small C instrumentation target; Runtime, Compiler,
and the portable Conformance library do not depend on it. It observes successful
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

`SwiftPerformance.measurement.json` records environment identity, exact Markdown
hashes, all samples, allocation counts and work counters. Normal Conformance JSON
and Markdown also contain measured/reference/allowed values and pass/fail results.
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

All sixteen warmed dispatch cases measured **zero bytes and zero allocations**,
including exact/name matching, observers off/on, frame pause/resume and paired
1000/2000-iteration workloads. Text parsing/casts measured **81–107 requested
bytes per invocation**; Percentage formatting measured **535 bytes**. These
nonzero costs come from current temporary byte arrays, filtered digits and text
formatting. They remain visible as Swift-specific regression budgets and are not
claimed as zero-allocation text conversion.
