# Portable Conformance package

`StepH.GameEventScript.Conformance` implements the authoring side of
`ConformanceMarkdownV1.md`. It is currently compiled into the Core project so
that users can parse the same suites for self-tests, but it is deliberately an
isolated package boundary for later extraction into its own monorepo module.

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
