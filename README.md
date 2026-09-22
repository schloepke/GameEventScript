<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Game Event Script

[![NuGet](https://img.shields.io/nuget/v/GameEventScript.Runtime)](https://www.nuget.org/packages/GameEventScript.Runtime)
[![GitHub release](https://img.shields.io/github/v/release/schloepke/GameEventScript)](https://github.com/schloepke/GameEventScript/releases/latest)

Game Event Script is a portable, deterministic scripting language and serial
message host for game logic. C# and Swift implement the shared
language-neutral contracts, with C# as the reference implementation. Additional
language implementations are tracked in [`BACKLOG.md`](BACKLOG.md).

The canonical documentation starts at [Documentation](docs/README.md).
Portable behavior is defined by the normative specifications and executable
Markdown conformance corpus; the C# API is one language-specific mapping.

## Install and use

The badges above show the latest non-prerelease NuGet package and GitHub release
once published. See [release notes](https://github.com/schloepke/GameEventScript/releases)
for published changes and migration guidance, and the [changelog](CHANGELOG.md)
for changes planned for the next release. All libraries share the GES release version;
language and API contracts may change during 0.x development.

SwiftPM consumes version tags from this repository; a tag alone does not create
a GitHub release or publish NuGet packages. For use from a checkout, see the
[C#](implementation/csharp/README.md) and [Swift](implementation/swift/README.md)
implementation guides.

| Use case | NuGet package | SwiftPM product |
| --- | --- | --- |
| Execute precompiled `.gesb` programs | `GameEventScript.Runtime` | `GameEventScriptRuntime` |
| Also compile GES source text | `GameEventScript.Compiler` | `GameEventScriptCompiler` |
| Native callbacks and type adapters | `GameEventScript.CSharpBridge` | `GameEventScriptSwiftBridge` |

Compiler and each native Bridge depend only on Runtime. Precompiled applications
can omit Compiler; the native Bridge is optional. CLI and Conformance are not
part of the public library packages.

### C# / NuGet

The libraries target .NET Standard 2.1. For a .NET 8 or newer console application,
install all three packages to run the example below:

```bash
dotnet new console -n GesExample
cd GesExample
dotnet add package GameEventScript.Runtime
dotnet add package GameEventScript.Compiler
dotnet add package GameEventScript.CSharpBridge
```

Without `--version`, these commands select the latest available non-prerelease
packages. To reproduce a specific release, add `--version <release-version>` to
each command and use the same version for all three libraries.

Replace `Program.cs` with:

```csharp
using System;
using GameEventScript.Api;
using GameEventScript.CSharpBridge;

var program = GameEventScriptBuilder.Create()
    .AddScript("""
        module example
        function double(_ value as :Number) be value + value
        on Start() { emit Done(value: double(21)) }
        """, "example.ges")
    .Compile();

var host = GameEventScriptHost.CreateBuilder().Build();
host.Subscribe("Done", ["value"], (message, _) =>
    Console.WriteLine(message.Arguments.GetAsInteger("value")));
host.Load(program);

if (host.Start().State != GameEventScriptStartState.Ready)
    throw new InvalidOperationException("Host startup failed.");

host.Receive(GameEventScriptMessage.Create("Start"));
var result = host.RunToCompletion();
if (result.State != GameEventScriptExecutionState.Completed)
    throw new InvalidOperationException($"Execution stopped: {result.State}");
```

Run `dotnet run`; the native callback prints `42`. Compiler, startup and runtime
failures have structured diagnostics; see the [C# embedding guide](implementation/csharp/README.md#compile-source-text).

### Swift / SwiftPM and Xcode

In Xcode, choose **File → Add Package Dependencies**, enter
`https://github.com/schloepke/GameEventScript.git`, and select **Exact Version**
with the version you want from the
[published releases](https://github.com/schloepke/GameEventScript/releases).
Add the Runtime, Compiler and SwiftBridge products to your app target
for the example below. The public package requires Swift 6.0 or newer; macOS is
the currently CI-verified platform.

For a SwiftPM executable, use this `Package.swift` and put the example in
`Sources/GesExample/main.swift`. Replace `<release-version>` with the chosen
published version number, without an optional leading `v` from the Git tag.
SwiftPM requires a concrete version here; the badge does not substitute it:

```swift
// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "GesExample",
    dependencies: [
        .package(url: "https://github.com/schloepke/GameEventScript.git", exact: "<release-version>")
    ],
    targets: [
        .executableTarget(name: "GesExample", dependencies: [
            .product(name: "GameEventScriptRuntime", package: "GameEventScript"),
            .product(name: "GameEventScriptCompiler", package: "GameEventScript"),
            .product(name: "GameEventScriptSwiftBridge", package: "GameEventScript")
        ])
    ]
)
```

```swift
import GameEventScriptRuntime
import GameEventScriptCompiler
import GameEventScriptSwiftBridge

let program = try GameEventScriptBuilder.create()
    .addScript("""
        module example
        function double(_ value as :Number) be value + value
        on Start() { emit Done(value: double(21)) }
        """, sourceName: "example.ges")
    .compile()

let host = try GameEventScriptHost.createBuilder().build()
let done = try GameEventScriptMessageSignature(name: "Done", parameters: ["value"])
_ = try host.subscribe(done) { message, _ in
    print(try Int.fromGesValue(message.arguments[0]))
}
_ = try host.load(program)

guard try host.start().state == .ready else {
    fatalError("Host startup failed.")
}

host.receive(try GameEventScriptMessage(name: "Start"))
let result = try host.runToCompletion()
guard result.state == .completed else {
    fatalError("Execution stopped: \(result.state)")
}
```

Run `swift run GesExample`; the native callback prints `42`. In an Xcode app,
call the example from a throwing function on the host's serial execution path.
See the [Swift embedding guide](implementation/swift/README.md#compile-source-text)
for structured diagnostics and integration details.

Both examples compile source, register a native receiver, load the program,
start the host and explicitly send `Start()`. Load all initial programs before
calling `Start` / `start`; then pump messages with `RunToCompletion` /
`runToCompletion`, or use frame stepping in a game loop. The host retains loaded
programs and subscriptions until explicitly detached or unsubscribed.
See the [Host contract](specs/HostRuntime.md) for lifecycle and ordering guarantees.

## Repository layout

- [`specs`](specs) contains the normative language-neutral specifications.
- [`conformance`](conformance) contains the shared executable corpus and fixtures.
- [`implementation/csharp`](implementation/csharp/README.md) contains the reference Runtime, Compiler, C# adapters, Conformance and CLI, with build and embedding instructions.
- [`implementation/swift`](implementation/swift/README.md) contains the Swift Runtime, Compiler, SwiftBridge, Conformance and CLI packages and their Xcode workspace.
- [`dotnet ges` CLI](implementation/csharp/GameEventScript.Tool/README.md) contains the separate .NET tool entry point and local installation instructions.
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
./scripts/release-csharp-dry-run.sh <release-version>
./scripts/verify-csharp-reproducibility.sh <release-version>
```

Replace `<release-version>` with the version being prepared; these verification
commands do not publish a release.

Normal build and test output remains below project-local `bin`/`obj` directories.
Packages and generated reports are written only below the ignored `artifacts`
directory.

To build the C# solution and install or update the `dotnet ges` CLI for your user, run
`./scripts/install-csharp-tool.sh`. See the
[CLI guide](implementation/csharp/GameEventScript.Tool/README.md)
for installation into a separate directory.
Use `./scripts/uninstall-csharp-tool.sh` to remove the global C# tool, or pass
`--tool-path DIRECTORY` to remove an installation from that directory. Repeated
uninstallation succeeds when the tool is already absent.

The [C# distribution guide](docs/guide/distribution/CSharp.md) documents package
contents, the staged Unity DLL set, reproducibility, and the deliberately gated
NuGet release workflow.
The [package release guide](docs/guide/distribution/Packages.md) covers the three
public libraries per language, publisher setup and the shared version/tag flow.
Conformance is internal; CLI tools are not published through NuGet or SwiftPM.

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
The root `Package.swift` exposes Runtime, Compiler and SwiftBridge through one
Git-based SwiftPM dependency. See the [Xcode installation instructions](docs/guide/distribution/Packages.md#swiftpm-and-xcode).
Verify the distribution with `python3 scripts/test-swift-package.py`; no registry
upload is required.

## License

Game Event Script is licensed under the
[Apache License 2.0](LICENSE). Copyright 2026 Stephan Schlöpke.

The repository-wide header and attribution policy is documented in
[Licensing](LICENSING.md).

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
