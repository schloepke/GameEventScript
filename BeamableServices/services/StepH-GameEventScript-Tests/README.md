<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Test hierarchy

The test sources are covered by the Game Event Script
[Apache License 2.0](../StepH-GameEventScript/LICENSE) and its
[licensing policy](../StepH-GameEventScript/LICENSING.md).

The test project has two semantic roots:

- `Conformance` contains the portable Markdown suites, parser fixtures and
  generated reports. Its root also contains the C# conformance runner, parser,
  report and test-framework adapters, so IDE test trees expose them directly as
  `Conformance` rather than as native implementation tests.
  `Conformance/CrossLanguage` contains the compact accepted C# reference and
  the live language/capability matrix. Shared parser bootstrap inputs are
  integrity-protected by `Conformance/Fixtures/MarkdownV1/manifest.tsv`.
- `Native` contains the remaining C#-specific tests, grouped by purpose:
  `Api`, `ApiSurface`, `BinaryFormat`, `Compiler`, `Core`, `CSharpBridge`, and
  `Runtime`.

Portable public behavior is added to Markdown first. A native test remains only
for a language adapter, implementation detail, performance/allocation property,
or bootstrap behavior that cannot use the corpus as its sole oracle. The
current audit is recorded in
`../StepH-GameEventScript/NativeTestRetention.md`.
