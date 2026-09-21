<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# GameEventScript.CSharpBridge

Optional C# adapters for delegates, Reflection, CLR values and host pumping.
The production project depends only on Runtime.

- `src/GameEventScript.CSharpBridge.csproj`: library project.
- `tests/GameEventScript.CSharpBridge.Tests.csproj`: native implementation tests.

Tests can use Compiler to prepare inputs without adding product dependencies.
Portable behavior is verified by the separate Conformance module against the
shared [Markdown corpus](../../../conformance).

See the [C# guide](../README.md) for build commands, API examples and distribution,
and the [test guide](../verification/README.md) for the full verification workflow.
