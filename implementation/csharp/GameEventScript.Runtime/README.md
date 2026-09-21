<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# GameEventScript.Runtime

Portable values, Programs, binary codecs, Host and VM. The production project
has no Compiler or Conformance dependency.

- `src/GameEventScript.Runtime.csproj`: library project.
- `tests/GameEventScript.Runtime.Tests.csproj`: native implementation tests.

Tests can use Compiler to prepare inputs without adding product dependencies.
Portable behavior is verified by the separate Conformance module against the
shared [Markdown corpus](../../../conformance).

See the [C# guide](../README.md) for build commands, API examples and distribution,
and the [test guide](../verification/README.md) for the full verification workflow.
