<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Cross-language conformance acceptance

This document defines how C#, Swift, Kotlin, Go, Rust, and C++ prove that they
parsed and executed the same Conformance Markdown V1 corpus. Unity uses the
accepted C# library; Unity integration tests may be reported separately but do
not define a different language implementation.

The normative authoring and execution contracts remain
[Markdown format](MarkdownFormat.md) and [Runner](Runner.md). This document defines
corpus identity, bootstrap parser fixtures, capability reporting, and acceptance
rules.

## Shared inputs

Every port consumes the same files below without copying or translating them:

- `conformance/suites/**/*.md`;
- `conformance/fixtures/GesbV1/*` through their
  resource IDs and hashes in the Markdown corpus;
- `conformance/fixtures/MarkdownV1/*` through
  `manifest.tsv`.

File paths are packaging details. Suite IDs, full case IDs, resource IDs, and
the bytes authenticated by the manifests are portable identities. A port may
load the files from a filesystem, package resources, or caller-provided memory.

## Corpus identity

The full canonical result (`ConformanceResults.json`, see [Runner](Runner.md))
records under `sourceDocuments` the SHA-256 of the exact bytes of every authored
Markdown document it ran, keyed by suite ID. Two results describe the same
corpus when their `sourceDocuments` maps are equal. Binary resources need no
separate entry: their resource IDs and expected SHA-256 values are part of the
hashed Markdown, and the runner verifies the resolved bytes before decoding them.

## Comparison and acceptance

A candidate port always emits its full canonical `ConformanceResults.json` and
its human-readable report. Comparison uses full case IDs, never a testframework
class, method, display name, or execution position. Implementation identity,
diagnostics, technical detail, actual assembler, and measured performance values
in the full result do not participate in the comparison.

Before comparing case outcomes, tooling verifies matching schema versions and
equal `sourceDocuments`. It then builds maps by full case ID and rejects missing,
additional, duplicate, or kind/level-inconsistent entries.

For an accepted complete port:

- every case without a missing optional capability is `passed` with
  `conformance.passed`;
- absence of a required Core capability is always an `error` and fails
  acceptance; it is never converted into a skip;
- a case may be `skipped` only when at least one of its declared optional
  capabilities is absent, the full result uses
  `conformance.runner.missingOptionalCapability`, and the missing set exactly
  reflects the advertised capability set;
- advertising an optional capability makes all cases requiring it mandatory;
- `failed` and `error` outcomes always fail acceptance.

The C# implementation supports every V1 Core and optional capability, so its
complete run passes every case. During development an incomplete port can still
publish a useful report and capability row, but it is not a complete-port
acceptance until every Core capability is present.

Bytecode snapshot cases compare their canonical GESA against the expected block
inside the shared Markdown before they can pass. Performance cases likewise execute
their portable correctness portion first. Their profile IDs, reference values,
and measurements may differ by language/platform; only the resulting portable
case status participates in cross-language acceptance.

Numeric formatting cases must allow the output freedom defined in
[Number semantics](../Semantics/Numbers.md#numeric-text-output-and-roundtrip).
Portable cases can cast formatted text back to Number in the script and compare
the result with an independently specified exact numeric expectation. Include
fixed decimal and exponent text inputs as well, so a parser and formatter with
the same defect cannot validate each other. Exact roundtrip assertions use
Int64 identity or zero Binary64 ULP tolerance after value canonicalization.
Quantity units and Percentage numeric ratios are asserted explicitly.
Percentage cases additionally verify exact Percentage kind and ratio after
Number and Text roundtrips, including zero, negative values, and values at or
above `100%`. Numeric-to-Percentage casts must not reinterpret integral ratios
or ratios outside `[-1, 1]` as percentage magnitudes.

The runner continues to compare `:Text` values exactly. It must not silently
parse or normalize arbitrary text to accommodate formatting differences. A
case requiring one fixed spelling of implementation-selected numeric text is
not a portable formatting oracle. Conformance transport's canonical numeric
payload strings remain governed by their separate encoding rules.

## Parser bootstrap fixtures

`Fixtures/MarkdownV1/manifest.tsv` is intentionally simpler than Markdown or
YAML so it can test a new parser without depending on that parser. Its exact
format is described beside the manifest. Every port must:

1. verify each fixture's exact SHA-256;
2. parse every `valid` fixture and compare suite and local case IDs;
3. reject every `invalid` fixture and compare the first stable diagnostic code.

Corpus execution provides the broader normalized-model check: a parser that
interprets a valid field differently cannot produce the same outcomes for all
stable case IDs. New parser-boundary ambiguities should first receive a shared
bootstrap fixture and manifest row, then a semantic corpus case where possible.

## Capability matrix

The live implementation matrix is
`conformance/cross-language/CapabilityMatrix.md`.
Changing a cell requires an attached canonical full result for the current
corpus, identified by its `sourceDocuments`. An implementation without that evidence is not marked as
supported. Unity's reuse of the C# DLL is shown separately from independent
Unity integration acceptance.

The matrix is informative status tracking. Capability meaning, case
requirements, and skip/error behavior come exclusively from the normalized
corpus and the runner contract.
