<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# CLI compute benchmark

[`compute-benchmark.ges`](compute-benchmark.ges) is a finite workload that needs
no arguments, native extensions, random seed or custom execution limits. Run
the commands below from the repository root, using installed Release CLIs from
the same source revision where possible.

```sh
ges run docs/guide/cli/compute-benchmark.ges
dotnet ges run docs/guide/cli/compute-benchmark.ges
```

The script chains 48 compute messages. Each batch constructs 128 immutable
Records, calculating each score through 16 accumulator iterations. It also
exercises square roots, computed Record fields, filtering, sorting, projections,
grouping, map lookups, fold/reduce, and formatting/parsing a report. About 1.89
million opcodes execute across 6,144 candidate Records. The normal message and
per-handler step limits are sufficient. There are no delays or per-item console
writes.

Every batch verifies its report roundtrip and grouping count. The final integer
checksum is independently calculated from the score recurrence, excluding scores
divisible by three and summing each batch's total, count, highest and lowest
score. Floating-point square roots are exercised but excluded from the exact
checksum. Successful output is:

```text
Benchmark OK: rounds=48, candidates=6144, checksum=2069276181
```

A failed assertion reports an error and exits with code 1. Keep Hyperfine's
default failure handling enabled so failures cannot masquerade as fast runs.

## Source-to-completion comparison

```sh
hyperfine -N --warmup 3 --runs 20 \
  'dotnet ges run --quiet docs/guide/cli/compute-benchmark.ges' \
  'ges run --quiet docs/guide/cli/compute-benchmark.ges'
```

This measures process startup, source reading, compilation, linking and execution.
Different compiler builds may produce different instruction sequences even when
the observable result is identical.

## Same-bytecode comparison

Compile once, then give both runtimes **the same binary**:

```sh
ges compile docs/guide/cli/compute-benchmark.ges \
  -o artifacts/cli/compute-benchmark/workload.gesb

hyperfine -N --warmup 3 --runs 20 \
  'dotnet ges run --quiet artifacts/cli/compute-benchmark/workload.gesb' \
  'ges run --quiet artifacts/cli/compute-benchmark/workload.gesb'
```

This removes compilation from the measurement, but still includes process startup,
binary reading/validation, linking and final console output. `--quiet` suppresses
CLI status reports; the script retains its single success line. `-N` avoids
Hyperfine's extra shell. Each measured command starts a fresh process: warmups
warm filesystem/OS caches, not the managed runtime inside later processes.

This is a mixed CLI workload, not a pure VM benchmark or a cross-platform
performance guarantee. Compare on the same machine under similar load, and
record tool versions/build configuration. The repository's dedicated performance
and allocation checks remain the regression gates.

## Opcode and source-line profiling

> **Since: Unreleased**

Use the same binary to collect comparable execution counts and locate expensive
opcode families or source lines:

```sh
ges run artifacts/cli/compute-benchmark/workload.gesb --profile artifacts/profiles/swift.md
dotnet ges run artifacts/cli/compute-benchmark/workload.gesb --profile artifacts/profiles/csharp.md
```

`--profile <report.md>` writes a Markdown file, creating missing directories and
replacing a previous report. It works with source input, multiple binaries and
`--scenario`. Interactive profiling is not supported. Normal script output and
exit codes remain unchanged; the report is also written after a runtime failure
or limit, so it can contain partial execution. An unwritable report is a CLI I/O
failure. Profile paths must end in `.md` and must not name an input file.

The report contains program-instance IDs, all executed opcode kinds, and the top
100 source-line and instruction rows by measured time. Each row shows instruction
starts, total milliseconds, share of measured VM time and mean nanoseconds.
Additional mean-nanosecond columns separate State (processing check), Budget
(exhaustion check), Slice (reserved-count check), Fetch (instruction access and
pointer increment), Execute (dispatch and opcode body), and Advance (completed
counter and loop transition). A Loop phases table includes terminal checks that
never reach an instruction; these are not assigned to an opcode or source line.
Budget reservation and completion outside the loop are excluded.
Source-line counts aggregate opcode starts, not source-statement invocations.
Addresses and instance IDs remain available without debug information; their
source is marked `[unmapped]`. Duplicate modules remain separate instances. A
source map identifies the line containing the start of the mapped source span.

Initialization and synchronous extension calls are included. Compilation, loading,
native message handlers and waits between execution slices are excluded. Script
calls use exclusive accounting: callee instructions receive their own time.

Profiling perturbs execution. Bookkeeping is excluded from timed intervals, but
clock reads and callback transitions remain. Six timed phases mean substantially
more measurement overhead than a single timer per opcode. Tiny phases may be
dominated by that overhead. Compare repeated
profiles on the same machine and use unprofiled benchmarks for overall speed.
A wall-clock-sensitive script may observe different delayed-message readiness
under instrumentation. Counts after an opcode fault can differ from the Host's
completed-opcode counter. The report measures neither allocations nor CPU time.
