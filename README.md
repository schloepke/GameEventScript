<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Game Event Script

Game Event Script is a portable, deterministic scripting language and serial
message host for game logic. The current C# implementation is the reference for
the planned Swift, Kotlin, C++, C#, and Unity ports.

The canonical documentation starts at [Documentation](docs/README.md).
Portable behavior is defined by the normative specifications and executable
Markdown conformance corpus; the C# API is one language-specific mapping.

## Repository layout

- [`specs`](specs) contains the normative language-neutral specifications.
- [`conformance`](conformance) contains the shared executable corpus and fixtures.
- [`implementation/csharp`](implementation/csharp) contains the current reference implementation and native tests.
- [`docs`](docs) contains guides and development material.
- [`tools`](tools) contains editor support and repository tooling.

The C# implementation is split into the portable `StepH.GameEventScript` Core,
the optional `StepH.GameEventScript.CSharpBridge` adapters, and the independently
movable `StepH.GameEventScript.Conformance` parser and runner.

## C# reference implementation

The repository includes a conventional solution and small reproducible entry
points:

```bash
./scripts/build-csharp.sh
./scripts/test-csharp.sh
./scripts/test-csharp-performance.sh
./scripts/format-csharp.sh
./scripts/pack-csharp.sh
```

Normal build and test output remains below project-local `bin`/`obj` directories.
Packages and generated reports are written only below the ignored `artifacts`
directory.

## License

Game Event Script is licensed under the
[Apache License 2.0](LICENSE). Copyright 2026 Stephan Schlöpke.

The repository-wide header and attribution policy is documented in
[Licensing](LICENSING.md).
