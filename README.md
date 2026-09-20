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
- [`implementation/csharp`](implementation/csharp/README.md) contains the reference Runtime, Compiler, C# adapters, Conformance and CLI, with build and embedding instructions.
- [`implementation/swift`](implementation/swift/README.md) contains the Swift Runtime, Compiler, SwiftBridge, Conformance and CLI packages and their Xcode workspace.
- [`dotnet ges` CLI](implementation/csharp/tools/GameEventScript.Tool/README.md) contains the separate .NET tool entry point and local installation instructions.
- [`ges` CLI](implementation/swift/GameEventScriptTool/README.md) contains the native Swift command and installation instructions.
- [`docs`](docs) contains guides and supporting documentation.
- [`tools`](tools) contains editor support and repository tooling.
- [`BACKLOG.md`](BACKLOG.md) contains deliberately deferred project work and is not a normative specification.

The C# implementation provides Runtime as `GameEventScript.Runtime`,
the optional `GameEventScript.Compiler`, C#-specific
`GameEventScript.CSharpBridge` adapters, and the separate
`GameEventScript.Conformance` parser and runner. Compiler and CSharpBridge
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

To build the C# solution and install or update the `dotnet ges` CLI for your user, run
`./scripts/install-csharp-tool.sh`. See the
[CLI guide](implementation/csharp/tools/GameEventScript.Tool/README.md)
for installation into a separate directory.
Use `./scripts/uninstall-csharp-tool.sh` to remove the global C# tool, or pass
`--tool-path DIRECTORY` to remove an installation from that directory. Repeated
uninstallation succeeds when the tool is already absent.

The [C# distribution guide](docs/guide/distribution/CSharp.md) documents package
contents, the staged Unity DLL set, reproducibility, and the deliberately gated
NuGet release workflow.

## Swift implementation

The Swift entry points also live in `scripts`:

```bash
./scripts/build-swift.sh
./scripts/test-swift.sh
./scripts/test-swift-tool.sh
./scripts/test-swift-bridge.sh
./scripts/test-swift-performance.sh
./scripts/format-swift.sh
python3 scripts/verify-swift-api.py
python3 scripts/verify-swift-bytecode.py
./scripts/open-swift-xcode.sh
```

`build-swift.sh` builds each package independently in Release using only Swift;
`--configuration debug` selects Debug. Outputs stay under `artifacts/swift`.
`format-swift.sh` checks formatting without modifying files; `--fix` applies it.
The full test script additionally needs .NET 10 to produce interoperability
fixtures. Performance checks require the calibrated hardware/toolchain profile.
See the [Swift guide](implementation/swift/README.md) for details.

Install or update the native Swift `ges` command with
`./scripts/install-swift-tool.sh`; remove it with `./scripts/uninstall-swift-tool.sh`.
Both accept `--tool-path DIRECTORY` and otherwise use `$HOME/.local/bin`.
See the [Swift CLI guide](implementation/swift/GameEventScriptTool/README.md).
SwiftPM registry publication remains deferred.

## License

Game Event Script is licensed under the
[Apache License 2.0](LICENSE). Copyright 2026 Stephan Schlöpke.

The repository-wide header and attribution policy is documented in
[Licensing](LICENSING.md).
