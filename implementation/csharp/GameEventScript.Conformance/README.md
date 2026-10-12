<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Internal C# Conformance tooling

The legacy C# Markdown parser, runner, reports and MSTest corpus adapter have
been removed. The [independent Python runner](../../../conformance/runner/README.md)
owns parsing, expectations and verdicts. This directory contains only internal
implementation tooling, with no published Conformance library:

- [adapter](adapter/README.md): executable JSON observation adapter plus its
  protocol and measurement controls. Depends on Compiler and Runtime.
- `fixtures`: fixed native external-type/callback bindings shared by the adapter
  and fixture exporter.
- [fixture-exporter](fixture-exporter/README.md): source-to-binary export and
  cross-language codec probes. Python supplies source-only requests; no expected
  execution results are exported.

Run correctness and managed allocation checks from the repository root:

```sh
sh scripts/test-csharp-conformance.sh
```

Hardware-specific elapsed-time profiles remain opt-in through
`scripts/test-csharp-performance.sh`. Reports stay below `artifacts/conformance/python`.
Native compiler process/stack and allocation tests now live with Compiler;
their worker is `../GameEventScript.Compiler/worker`. Swift uses its own internal
adapter with the same independent Python runner.
