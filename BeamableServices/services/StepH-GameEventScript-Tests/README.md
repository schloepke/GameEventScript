# Test hierarchy

The test project has two semantic roots:

- `Conformance` contains the portable Markdown suites, parser fixtures and
  generated reports. Its root also contains the C# conformance runner, parser,
  report and test-framework adapters, so IDE test trees expose them directly as
  `Conformance` rather than as native implementation tests.
- `Native` contains the remaining C#-specific tests, grouped by purpose:
  `Api`, `ApiSurface`, `BinaryFormat`, `Compiler`, `Core`, `CSharpBridge`, and
  `Runtime`.

Portable public behavior is added to Markdown first. A native test remains only
for a language adapter, implementation detail, performance/allocation property,
or bootstrap behavior that cannot use the corpus as its sole oracle. The
current audit is recorded in
`../StepH-GameEventScript/NativeTestRetention.md`.
