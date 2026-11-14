<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# CLI compute benchmark

[`compute-benchmark.ges`](compute-benchmark.ges) is a finite workload that needs
no arguments, native extensions, random seed or custom execution limits. Run
the commands below from the repository root, using installed Release CLIs from
the same source revision where possible.

```sh
ges run examples/compute-benchmark/compute-benchmark.ges
dotnet ges run examples/compute-benchmark/compute-benchmark.ges
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

Two questions need two measurements:

- **Cold start: process start + first run.** Hyperfine starts a new process for every measurement. That is what a
  command-line call costs, but under the .NET JIT every such run executes the Runtime's own code as unoptimized tier-0
  code, so it does not show what a long-running client sees.
- **Steady state: in-process runs after warm-up.** `ges bench` and `dotnet ges bench` load a compiled program once,
  warm it up and time each further run inside the same process, which is what a game client or server executes after a
  few seconds. See [In-process benchmark](../../implementation/csharp/GameEventScript.Tool/README.md#in-process-benchmark).

## Cold start: source to completion

```sh
hyperfine -N --warmup 3 --runs 20 \
  'dotnet ges run --quiet examples/compute-benchmark/compute-benchmark.ges' \
  'ges run --quiet examples/compute-benchmark/compute-benchmark.ges'
```

This measures process startup, source reading, compilation, linking and execution.
Different compiler builds may produce different instruction sequences even when
the observable result is identical.

## Cold start: same bytecode

Compile once, then give both runtimes **the same binary**:

```sh
ges compile examples/compute-benchmark/compute-benchmark.ges \
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

## Steady state: in-process runs

Compile once, then measure the same binary in both CLIs:

```sh
ges compile examples/compute-benchmark/compute-benchmark.ges \
  -o artifacts/cli/compute-benchmark/workload.gesb

ges bench artifacts/cli/compute-benchmark/workload.gesb
dotnet ges bench artifacts/cli/compute-benchmark/workload.gesb --min-time 2000
```

Both load the program once and report the load time, then the minimum, median, mean, 95th percentile, maximum and
standard deviation of 50 timed runs after 20 warm-up runs, and a checksum of the console output that must be equal in
both. `--min-time` keeps warming up until the .NET JIT has had time to reach its optimized tiers; `--per-iteration`
lists every run, which shows the tiering over time, and `--json` prints the same data for scripts.

```sh
python3 scripts/bench-compare.py
```

builds the Swift CLI in Release and four .NET configurations for this machine — the JIT with default tiering, the JIT
with `DOTNET_TieredCompilation=0`, a ReadyToRun publish and a NativeAOT publish — runs `bench` on one compiled binary
with each, requires equal checksums and prints one table of load time, median, 95th percentile and allocations per run.
Its JSON results stay in `artifacts/bench`.

## Opcode and source-line profiling

> **Since: Unreleased**

Use the same binary to collect comparable execution counts and locate expensive
opcode families or source lines:

```sh
ges run artifacts/cli/compute-benchmark/workload.gesb --profile artifacts/profiles/swift.md
dotnet ges run artifacts/cli/compute-benchmark/workload.gesb --profile artifacts/profiles/csharp.md
```

For report contents, timing boundaries and interpretation, see the general CLI
documentation: [Swift](../../implementation/swift/GameEventScriptTool/README.md#opcode-profiling)
or [C#](../../implementation/csharp/GameEventScript.Tool/README.md#opcode-profiling).
