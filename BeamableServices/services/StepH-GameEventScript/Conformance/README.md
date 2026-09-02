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
Runner, report, and received-output behavior belongs to the subsequent
Conformance runner steps and consumes only the normalized public model.

Semantic test metadata uses ordinary `yaml` fences so standard Markdown tooling
can highlight it. The required root field `gesBlock` classifies each such block
as `case` or `expect`; custom trailing fence info strings are intentionally not
accepted.
