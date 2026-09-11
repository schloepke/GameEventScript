<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Game Event Script

Game Event Script is a portable, deterministic scripting language and serial
message host for game logic. The current C# implementation is the reference for
the language-neutral contracts. Additional language implementations are tracked
in [`BACKLOG.md`](BACKLOG.md).

The canonical documentation starts at [Documentation](docs/README.md).
Portable behavior is defined by the normative specifications and executable
Markdown conformance corpus; the C# API is one language-specific mapping.

## Repository layout

- [`specs`](specs) contains the normative language-neutral specifications.
- [`conformance`](conformance) contains the shared executable corpus and fixtures.
- [`implementation/csharp`](implementation/csharp) contains the current reference implementation and native tests.
- [`ges` CLI](implementation/csharp/tools/StepH.GameEventScript.Tool/README.md) contains the separate .NET tool entry point and local installation instructions.
- [`docs`](docs) contains guides and supporting documentation.
- [`tools`](tools) contains editor support and repository tooling.
- [`BACKLOG.md`](BACKLOG.md) contains deliberately deferred project work and is not a normative specification.

The C# implementation provides the portable `StepH.GameEventScript` Runtime,
the optional `StepH.GameEventScript.Compiler`, C#-specific
`StepH.GameEventScript.CSharpBridge` adapters, and the separate
`StepH.GameEventScript.Conformance` parser and runner. Compiler and CSharpBridge
each depend only on Runtime; Conformance depends on Runtime and Compiler.
Products executing precompiled `.gesb` files need no Compiler package.

## C# reference implementation

The repository includes a conventional solution and small reproducible entry
points:

```bash
./scripts/build-csharp.sh
./scripts/test-csharp.sh
./scripts/test-csharp-performance.sh
./scripts/format-csharp.sh
./scripts/pack-csharp.sh
./scripts/release-csharp-dry-run.sh 0.1.0-rc.1
./scripts/verify-csharp-reproducibility.sh 0.1.0-rc.1
```

Normal build and test output remains below project-local `bin`/`obj` directories.
Packages and generated reports are written only below the ignored `artifacts`
directory.

To build the C# solution and install or update the `ges` CLI for your user, run
`./scripts/install-csharp-tool.sh`. See the
[CLI guide](implementation/csharp/tools/StepH.GameEventScript.Tool/README.md)
for installation into a separate directory.

The [C# distribution guide](docs/guide/distribution/CSharp.md) documents package
contents, the staged Unity DLL set, reproducibility, and the deliberately gated
NuGet release workflow.

## License

Game Event Script is licensed under the
[Apache License 2.0](LICENSE). Copyright 2026 Stephan Schlöpke.

The repository-wide header and attribution policy is documented in
[Licensing](LICENSING.md).
