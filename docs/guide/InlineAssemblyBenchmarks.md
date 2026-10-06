<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Collection opcode measurements with inline assembly

The first four comparisons show a measurable benefit from the existing `Count`,
`HasAny`, `HasAll` and `First` opcodes on List inputs. They do not support removing
these opcodes in favour of explicit iterator loops. This is an exploratory C#
measurement, not a portable performance requirement or a Swift result.

## Reproduce

Run from the repository root, with the .NET SDK available:

```sh
python3 scripts/benchmark-inline-assembly.py
```

`--prepare-only` builds without measuring. The runner writes its generated
project, build output and `results.json` below `artifacts/inline-assembly-benchmark`.
The [common driver](../../examples/inline-assembly/benchmarks/driver.ges) and
eight operation bodies in its directory are ordinary GES source. Select one operation file together with `driver.ges`;
compiling all alternatives together would duplicate the function definition.
The [native harness](../../scripts/inline-assembly-benchmark.cs) uses public APIs.

Both alternatives use an ASM function with the same signature, call frame and
outer repetition loop. Compilation, host construction, input creation, binary
size calculation and semantic controls are outside timing. Each case warms up
three times, then measures five batches of 2,000 calls. The table reports median
elapsed time per call, including the common loop and amortized message delivery.
There is no subtraction of a separately measured baseline.

Inputs are prebuilt Lists of 8, 128 and 4,096 elements. `HasAny` sees a true value
only at the end; `HasAll` sees only true values. These timings deliberately cover
full scans, not early exit distributions. Controls separately check empty input,
Nothing, mixed Boolean/Nothing values, numbers, NaN and a quantity with a unit
against a native oracle. Each batch must complete and deliver exactly one expected
result. The oracle uses GES truthiness, not Boolean-only truthiness.

## Recorded run

Measured on 2026-10-06, Apple M3 Max, Arm64, Darwin 25.6.0, .NET 8.0.6,
Release, `DOTNET_TieredCompilation=0`. Measurements ran without concurrent builds.
They describe this machine and runtime; reruns can vary.

| Operation | List length | Dedicated opcode, ns/call | Iterator loop, ns/call | Loop / opcode |
| --- | ---: | ---: | ---: | ---: |
| Count | 8 | 82.5 | 416.9 | 5.05× |
| Count | 128 | 70.7 | 4,844.5 | 68.53× |
| Count | 4,096 | 70.5 | 151,115.8 | 2142.23× |
| HasAny | 8 | 84.5 | 314.9 | 3.73× |
| HasAny | 128 | 156.3 | 3,107.0 | 19.87× |
| HasAny | 4,096 | 2,143.1 | 97,317.0 | 45.41× |
| HasAll | 8 | 83.1 | 306.0 | 3.68× |
| HasAll | 128 | 155.2 | 3,125.2 | 20.13× |
| HasAll | 4,096 | 2,156.7 | 96,033.2 | 44.53× |
| First | 8 | 80.3 | 114.9 | 1.43× |
| First | 128 | 78.6 | 109.1 | 1.39× |
| First | 4,096 | 79.3 | 107.0 | 1.35× |

All dedicated variants measured 0.068 allocated bytes/call; every iterator variant
measured 32.068 bytes/call, using the maximum allocation total among the five
samples. The common 0.068 is 136 bytes amortized over the 2,000-call batch; it is
not evidence of a fractional per-instruction allocation. The iterator adds 32
bytes on each call. Allocation counts use `GC.GetAllocatedBytesForCurrentThread`;
they are requested managed bytes, not retained heap size or native memory.

The following sizes include the common driver and operation function. GESB sizes
exclude debug information. Register counts are the Program's required register
count, not a measurement of process memory.

| Variant | Code instructions | GESB bytes | Registers | Frames for one call, budget 1, length 4,096 |
| --- | ---: | ---: | ---: | ---: |
| count / opcode | 14 | 561 | 9 | 15 |
| count / loop | 20 | 657 | 12 | 16,402 |
| any / opcode | 14 | 561 | 9 | 15 |
| any / loop | 20 | 657 | 11 | 12,305 |
| all / opcode | 14 | 561 | 9 | 15 |
| all / loop | 20 | 657 | 11 | 12,306 |
| first / opcode | 14 | 561 | 9 | 15 |
| first / loop | 16 | 593 | 10 | 17 |

The untimed frame control executes one call with `ExecuteFrame(1)` and verifies
its result. The dedicated opcodes keep their internal traversal atomic: frame
budget 1 does not interrupt a `HasAny` or `HasAll` scan halfway through a List.
The expanded loop can pause between its VM instructions. Thus equal results do
not imply identical latency, opcode-limit consumption or cancellation points.
Timing disables `MaxExecutionSteps` and `MaxLoopIterations` for both variants;
other limits use their defaults. This permits long batches but is not advice
for configuring an application host.

## Interpretation

- `Count` on an existing List reads stored length in constant time. Counting an
  iterator is linear. Its large ratio is primarily an algorithmic difference,
  not an isolated measure of dispatch overhead.
- `HasAny` and `HasAll` traverse the same full inputs in both variants. The native
  opcode loop avoids per-element VM dispatch, register manipulation and iterator
  protocol work. Their combined cost matters even if dispatch alone was small
  in a wider application profile.
- `First` is constant time in both variants. Here the extra iterator creation and
  instructions produce a smaller elapsed-time difference and 32 extra allocated
  bytes per call.

There is no dedicated `Sum` opcode to compare: `[:sum]` already lowers to
iteration and arithmetic. No opcode is added or removed as part of this study.
`Last`, `Single`, `Take`/`Drop`, distinct and ordering remain unmeasured candidates;
these four pairs cannot establish their tradeoffs. Likewise, Range/Series inputs,
short-circuit-heavy workloads, Swift and application-level workloads need their
own comparisons before making broader changes.
