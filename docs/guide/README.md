<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Game Event Script guides

This directory contains non-normative learning material: introductions, tutorials, recipes, examples, and integration guidance.

Guides optimize for understanding and may omit edge cases. Exact behavior is defined only by the documents linked from the [documentation index](../README.md). A guide must not introduce syntax, API behavior, or runtime guarantees that are absent from the owning specification.

## Start here

- [Learn Game Event Script](Language.md): run a script, work with values and
  collections, and connect message-driven rules.
- [Embed GES in C#](CSharp.md): install packages, compile a rule, receive its
  output and manage the host lifecycle.
- [Embed GES in Swift](Swift.md): add SwiftPM products, run the same rule and
  bind native data.

These guides track the development checkout. Unreleased features are marked;
consult the [changelog](../../CHANGELOG.md) when targeting a published release.

## Distribution

- [Library packages and releases](distribution/Packages.md) describes public
  NuGet/SwiftPM products, Xcode consumption and the shared release procedure.
- [C# distribution](distribution/CSharp.md) describes reproducible NuGet,
  symbol, and DLL artifacts, the Unity DLL path, and the guarded release
  workflow.
- [CLI tool](../../implementation/csharp/GameEventScript.Tool/README.md)
  describes the current `dotnet ges` entry point and local .NET tool installation.

The [Swift CLI guide](../../implementation/swift/GameEventScriptTool/README.md)
describes the native `ges` command, installation and terminal console.

## Clean build outputs

```sh
./scripts/clean.sh --dry-run          # Preview directories and file-data sizes.
./scripts/clean.sh                   # Delete all known repository build outputs.
./scripts/clean.sh --artifacts-only   # Delete only artifacts (also accepts --dry-run).
```

The script requires Python 3 and works from any directory. It removes `artifacts`,
root-level `bin`/`obj`/`TestResults`/`.build`, C# `bin`/`obj`/`TestResults` directories,
and SwiftPM `.build` directories. This includes generated packages, reports,
benchmark runs, local tool installations and Xcode outputs stored under `artifacts`.
Build and test scripts recreate their outputs on the next run.

Tracked content makes cleanup fail before any deletion. Symbolic output-directory
links are skipped, and links inside deleted outputs are never followed. Sources,
Conformance fixtures/references, Git data, `.swiftpm`/IDE configuration, global
package caches and tools installed outside this repository remain intact. Stop
running builds and tests before cleaning. Verify cleanup safety with
`python3 scripts/test-clean.py`; these tests use disposable directories.
