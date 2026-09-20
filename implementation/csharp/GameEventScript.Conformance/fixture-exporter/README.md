<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Runtime fixture exporter

Development-only adapter for checking a runtime port before its own compiler
exists. It compiles source groups from the shared Markdown corpus using the C#
reference compiler and the existing Conformance external-type catalog.
It exports no expected messages or execution results.

```bash
dotnet run --project implementation/csharp/GameEventScript.Conformance/fixture-exporter \
  --configuration Release --artifacts-path artifacts/swift/csharp-exporter -- \
  conformance/suites artifacts/swift/runtime-fixtures
```

Outputs must be below an `artifacts` directory. `manifest.tsv` has the header
`GES-RUNTIME-FIXTURES-V1`; subsequent rows contain case ID, Program group ID,
binary filename, binary SHA-256, and complete Markdown-document SHA-256.
Sourceless scenarios use empty Program/binary columns. Compile options and
fixture ProgramVersion come from the Markdown. Equal binaries share a hash name.

Swift verifies the hashes and executes the unchanged Markdown expectations.
A stale manifest fails verification. The output is Runtime evidence using C#
compiler inputs, not evidence of a Swift compiler. Performance cases supply
behavior scenarios only; Swift measurement profiles are separate.
