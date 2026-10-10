<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Generated binary roundtrip properties

`scripts/test-binary-roundtrip-properties.py` generates valid GESB V1 programs
with a small independent encoder following `specs/BinaryFormat.md` and the
instruction encodings in `specs/Bytecode.md`. It neither calls a product compiler
nor derives expected bytes from either product writer. The existing executable
reader probes test both implementations; no portable library API is added.

## Properties and scope

For each generated input, both readers must accept it and both writers must
produce the exact independently encoded canonical bytes. This detects field
loss even when both implementations share a bug or a lossy rewrite is stable.
The probes also check that reading and writing the result is a fixed point.
Finally, actual C# output is sent to Swift and actual Swift output to C#; both
exchanges must reproduce the same independently expected bytes.

The default 192 cases cover:

- One zero-argument handler with one to eight registers and varied literal loads.
- Int64 boundary bit patterns, positive/negative floating zero, subnormal and
  finite boundary binary64 values, and a nonexact decimal fraction.
- Empty, duplicate, Unicode-scalar-distinct, astral, multiline and NUL-containing
  strings; varying index-list contents and empty/duplicate lists.
- Varying UInt64 ProgramVersion metadata.
- All 16 combinations of debug symbols, source map, source archive and build
  metadata. Source hashes, UTF-8 byte offsets and line boundaries are computed
  independently; maps and archives agree when both exist.
- Zero, one and two opaque optional sections, preserving their relative order.
- Canonical inputs and reversed known-section order, with canonical output
  expected in both cases.

This is bounded structural property testing, not exhaustive Program generation,
compiler parity, execution semantics or release-to-release compatibility. The
existing Markdown cases, Golden bytes, numeric-text exchange and binary runtime
interoperability checks remain intact. Found defects must gain minimized
permanent Conformance cases before product changes.

## CI and reproduction

The seed defaults to `0x47455350`; randomness uses the specified LCG shared with
the fuzz harness. CI uses existing adapters with `--skip-build`, adding no new
Swift build or target. Batches have at most 64 cases and use the existing
30-second process watchdog and bounded reader options. Negative controls reject
missing results, rejected generated programs, changed bytes and substitution
with another valid generated program. Generator controls check reproducibility,
all optional-section combinations and all register counts. They also decode the
generated instructions to verify every advertised Int64 and binary64 boundary
pattern, including with the minimum 48-case run and two different seeds. Float
selection advances independently of the instruction-kind selection.

```sh
python3 scripts/test-binary-roundtrip-controls.py
python3 scripts/test-binary-roundtrip-properties.py --skip-build
python3 scripts/test-binary-roundtrip-properties.py --skip-build --seed 0x47455351 --cases 1024
```

Without `--skip-build`, the script builds the existing adapters first. Artifacts
live under `artifacts/swift/binary-roundtrip-properties`: saved batches include
the seed, case index, exact input and expected canonical bytes. Adapter input,
output and logs include both exchange directions. Only a complete successful
run writes `summary.json`, containing seed, input digest, case count and duration.
A batch can be replayed directly with either adapter's
`--binary-fuzz <input.txt> <output.txt>` mode. Swift CI retains these artifacts
on success or failure. Extended local runs do not increase the PR sample size.
