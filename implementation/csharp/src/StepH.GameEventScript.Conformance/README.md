<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Portable Conformance package

`StepH.GameEventScript.Conformance` implements the authoring side of
the [Conformance Markdown specification](../../../../specs/Conformance/MarkdownFormat.md). It is a separate optional assembly and package so users
can run the same suites for self-tests without coupling the portable Core to
Conformance infrastructure.

The public entry point is `ConformanceMarkdownParser.Parse`. It accepts UTF-8
bytes or text and returns a fully validated immutable `ConformanceDocument`.
Invalid input throws `ConformanceParseException` with stable diagnostics and
UTF-8 byte/source positions. The parser performs no filesystem access,
execution, threading, async work, or environment discovery.

The implementation contains a structural Markdown scanner and a strict parser
for the documented YAML subset. Their intermediate nodes are internal. Core
Host, VM, Runtime, Compiler, and API code must never depend on this namespace.

`ConformanceRunner` consumes only the normalized public model and provides
synchronous case, document, and corpus execution. Its explicit environment
declares implementation identity, capabilities, portable registries, and an
optional performance profile/provider. Structured immutable results preserve
passed/failed/skipped/error, mismatches, diagnostics, runtime-limit events,
snapshot output, and performance measurements. `ConformanceResultJsonWriter`
creates the canonical machine result, `ConformanceMarkdownReportWriter` creates
the informative aggregate report, and `ConformanceReceivedMarkdownWriter`
creates a byte-preserving approval candidate without filesystem access.

Semantic test metadata uses ordinary `yaml` fences so standard Markdown tooling
can highlight it. The required root field `gesBlock` classifies each such block
as `case` or `expect`; custom trailing fence info strings are intentionally not
accepted.

The complete portable corpus lives under
`conformance/suites`. Its 75 suites contain 1,042 semantic cases and seven
independent bytecode snapshots. Every case
has an explicit stable ID, kind, and level and is exposed independently through
the C# test adapter. Markdown is the sole normative authoring format for
conformance cases.

`program.binary-format` is also the executable `.gesb` V1 fixture manifest.
The immutable resources live below `conformance/fixtures/GesbV1`; adapters map
stable resource IDs to packaged bytes, while the portable parser and runner
remain fileless and networkless.

The C# adapter also discovers the five performance cases independently for an
explicit, non-parallel measurement run. It writes the canonical result JSON, a
human-readable report, and a suite-local received Markdown approval candidate
under `artifacts/conformance/received`; bytecode-snapshot cases produce a
separate received candidate there as well. These generated files never replace
the normative source suite automatically.

The C# Markdown adapter collects the independently executed case results and
writes `ConformanceResults.json` plus `ConformanceReport.md` after a complete
corpus run. Report generation does not execute the corpus a second time; the
separate identity test only validates suite and case counts and stable IDs.

The [Conformance environment](../../../../specs/Conformance/Environment.md) defines the fixed extension and external-type
catalog that each language runner implements. The normative
[Conformance Coverage](../../../../specs/Conformance/Coverage.md) maps
portable behavior to stable case IDs and gates the reduction of
language-specific tests. Script cases can configure all four publish-sink
modes, exact observer traces, native-only Hosts, repeated independent Hosts and
a closed set of ID-based lifecycle actions.

The repository hierarchy is intentionally split: `conformance` contains the
portable suites, parser fixtures, binary resources, and cross-language
references. The C# `Conformance` test directory contains only its runner,
parser, report, and test-framework adapters. `Native` contains the remaining
C#-specific tests, grouped by implementation purpose.
