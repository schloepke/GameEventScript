<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Bounded binary reader fuzzing

This is a verification harness, not an additional language contract or a
replacement for the canonical and invalid Markdown binary fixtures.
`scripts/test-binary-reader-fuzz.py` feeds identical mutated fixture bytes to the
existing C# fixture exporter and Swift Conformance executable. No candidate is
executed as bytecode. Portable libraries gain no filesystem or process APIs.

## Reproducibility and coverage

The default seed is `0x47455342`. A specified UInt32 LCG generates 1,024 mutations
of six existing valid runtime, debug, opaque, nested-call, result-send and
literal-parsing fixtures. Mutation families cover truncation, bit flips, random
word replacement, section headers, payload counts, insertion and deletion.
Length-changing mutations repair the outer size field when possible so deeper
reader paths are exercised. Six unchanged seeds, a malformed header and an
oversized input qualify acceptance, rejection and the file-size limit.

Only the reader's documented format error is an expected rejection. Unexpected
exceptions, crashes, timeout, incomplete responses and failed controls fail the
run. Accepted inputs must reach a stable canonical Reader/Writer fixed point;
both implementations must agree on acceptance and canonical bytes. Error-code
precedence for inputs with multiple defects is not compared; existing Markdown
fixtures remain the oracle for exact diagnostic codes and context.

## Bounds and CI

Each reader uses limits of 32 KiB input, 64 sections, 16 KiB source archives,
16 KiB retained opaque data, 2,048 instructions/strings/index lists and 256
bindings. One deliberate 32 KiB + 1 input checks rejection. Limits are product
reader limits, not an OS-level memory quota. Batches contain at most 128 inputs
and each adapter process has a 30-second watchdog. This is a bounded regression
sample, not exhaustive fuzzing or a timing benchmark.

`test-swift.sh` runs the harness with `--skip-build` after building both adapters.
It adds no Swift package or build invocation. Swift CI uploads the reports and
exact inputs even on failure. The harness controls exercise failing fake
processes, timeout handling, missing output and always-accept/reject adapters.

```sh
python3 scripts/test-binary-reader-fuzz-controls.py
python3 scripts/test-binary-reader-fuzz.py --skip-build
# Explicit larger local run; not the PR default:
python3 scripts/test-binary-reader-fuzz.py --skip-build --seed 0x47455343 --cases 10000
```

Artifacts under `artifacts/swift/binary-reader-fuzz` retain per-batch metadata,
base64 input bytes, adapter output and logs. `summary.json` exists only after a
successful complete run and records the seed, counts, input hash and duration.
The output row count locates the first unfinished input after a crash. Replay a
saved batch with either adapter's `--binary-fuzz <input.txt> <output.txt>` mode.
Promote discovered defects to small permanent Markdown Conformance fixtures
before changing product behavior. Generated valid-program properties are covered separately by
[Binary roundtrip properties](BinaryRoundtripProperties.md).
