<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Cross-language numeric text roundtrip gate

This is an integration verification guide, not a separate language contract.
The oracle is the numeric text roundtrip contract in
[Numbers](../../specs/Semantics/Numbers.md#numeric-text-output-and-roundtrip).
Portable behavior cases remain authored in shared Markdown; this additional
gate tests the actual exchange of formatter output between two implementations.

Run from the repository root with .NET 10, Swift and Python 3.10 or later:

```bash
python3 scripts/test-number-text-roundtrip.py
```

The script builds the C# fixture-exporter adapter and Swift Conformance tool in
Release. `--skip-build` reuses already-built adapters; `scripts/test-swift.sh`
uses that option after its builds. Swift CI therefore runs this gate alongside
shared Conformance and independently enforces `scripts/format-swift.sh`.

## Inputs and checks

A fixed SplitMix64 stream with seed `4745534e554d4245` generates 4,096 raw
binary64 bit patterns and signed Int64 values. The generator is implemented in
the Python harness, independently of the GES RNG. Explicit edge inputs include
both zero signs, signed infinities and NaNs, subnormals, the normal/subnormal
boundary, maximum finite values, adjacent representable values at notation
thresholds, familiar decimal fractions, the binary64 exact-integer boundary,
and both Int64 limits.

Each Number is also tested with meters, seconds and degrees. Binary64 inputs
are additionally tested as Percentage ratios. Values cross the adapter boundary
as hexadecimal bits or decimal Int64 strings, never JSON numeric payloads.

Each adapter compiles a small handler with its native compiler and executes
`as :Text`, `as :Number` and `as :Percentage` in its VM. The harness exchanges
C#-formatted text with Swift and Swift-formatted text with C#. It checks exact
kind, unit, Int64 value or binary64 bits against independently computed input
identities after the specified canonicalization. Percentage text must recover
both the exact Number view and the Percentage kind/ratio. NaN becomes Nothing;
signed zero, integral binary64 storage and infinite Percentage inputs follow
normal value canonicalization.

Formatted strings are not required to be equal. No approximate runtime `=` or
ULP tolerance is used. Negative controls require the gate to detect a changed
integer, one-ULP drift, a changed or lost unit, wrong percentage scaling and
loss of the Percentage kind.

## Artifacts

Inputs, both implementations' outputs, exchanged texts, failure details and
`summary.json` live in `artifacts/swift/number-text-roundtrip`. CI uploads this
directory with the Conformance reports. Any mismatch, missing response, adapter
failure or timeout fails the gate. A success summary is written only after both
directions and the negative controls pass.

The development adapters accept `--number-text <input.json> <output.json>`.
They belong to executable verification tooling, not the portable libraries or
user-facing CLI. The corpus identity and approved API snapshots are unaffected.
