<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# C# implementation

[Generated C# API reference](../../docs/guide/api/CSharp.md)


The C# implementation is the reference implementation for the shared,
language-neutral contracts. Runtime, Compiler, C# adapters and Conformance are
separate libraries; the CLI is a separate application.

| Project / package | Responsibility | Dependencies |
| --- | --- | --- |
| [`GameEventScript.Runtime`](GameEventScript.Runtime) | Immutable values and Programs, `.gesb` codecs/validation, GESA dumping, Host, VM, random streams, extensions and external types | .NET Standard library |
| [`GameEventScript.Compiler`](GameEventScript.Compiler) | Source lexer/parser, validation, lowering, optimization, register allocation, immutable Programs and debug sections | Runtime |
| [`GameEventScript.CSharpBridge`](GameEventScript.CSharpBridge) | Delegate adapters, Reflection/attribute bindings, CLR value conversion and optional automatic host pumping | Runtime |
| [`GameEventScript.SyntaxHighlighter`](GameEventScript.SyntaxHighlighter/README.md) | GES/GESA UTF-16 spans, TextMate scopes, incremental states and ANSI rendering | .NET Standard library |
| [`GameEventScript.Conformance`](GameEventScript.Conformance/README.md) | Shared Markdown parser, compiler/runtime orchestration, fixture verification and result reports | Runtime and Compiler |
| [`GameEventScript.Tool`](GameEventScript.Tool/README.md) / `dotnet ges` | Compile/check/run/dump, interactive console, filesystem and terminal adapters | Runtime, Compiler, CSharpBridge, SyntaxHighlighter and PrettyPrompt |

All five libraries target **.NET Standard 2.1** and use **C# 12**. Runtime,
Compiler and Conformance are synchronous, fileless and independent of a test
framework. CLR-specific adapters belong to CSharpBridge. Compiler and
CSharpBridge each depend only on Runtime, so a product executing precompiled
`.gesb` files can ship Runtime with optional C# adapters without shipping the
compiler or test infrastructure.

The public entry points use `GameEventScript.Api`; values use
`GameEventScript.Runtime.Values`. The compiler's builder also lives in
`GameEventScript.Api`, even though its assembly is separate. C# adapters and
Conformance use `GameEventScript.CSharpBridge` and `GameEventScript.Conformance`.

## Module layout

Each module owns its production project in `src` and its native test project in
`tests`. The repository solution groups them by module, so a Runtime or Compiler
change and its implementation tests are visible together. Build or test one
module directly by passing its `.csproj` to `dotnet build` or `dotnet test`.

```text
implementation/csharp/
  GameEventScript.Runtime/       src/ + tests/
  GameEventScript.Compiler/      src/ + tests/
  GameEventScript.CSharpBridge/  src/ + tests/
  GameEventScript.Tool/          src/ + tests/
  GameEventScript.Conformance/   src/ + tests/ + fixtures/ + worker/ + fixture-exporter/
  verification/                 Repository.Tests, consumers, package tool, test support
```

Conformance remains a separate module above Runtime and Compiler. The shared
Markdown corpus and portable fixtures stay in repository-level `conformance/`.
`verification/Tests.props` supplies common MSTest settings and linked helpers;
it contains no test cases. The six test projects each discover their own tests
exactly once. Some native Runtime and Bridge tests use Compiler to build inputs;
these are test-only dependencies and do not change the product boundaries.

## Build and verify

### Solution and toolchain

Open [`GameEventScript.sln`](../../GameEventScript.sln) from the repository root
in an IDE supporting the repository's .NET SDK. The solution includes the
libraries, CLI, native tests and supporting verification projects.

Development uses the exact **.NET SDK 10.0.201** pinned in
[`global.json`](../../global.json). The tests and CLI target **.NET 8** and also
need its runtime installed. The SDK version does not change the libraries'
`.NET Standard 2.1` target.

Run these commands from the repository root. The scripts also resolve the
checkout correctly when invoked by absolute path from another directory:

| Command | Purpose |
| --- | --- |
| `./scripts/clean.sh [--dry-run] [--artifacts-only]` | Remove repository build outputs; preview with `--dry-run` ([scope](../../README.md#clean-build-outputs)) |
| `./scripts/build-csharp.sh` | Build the complete solution in Release |
| `./scripts/test-csharp.sh` | Run shared Conformance and native non-performance tests in the default Debug configuration |
| `./scripts/format-csharp.sh` | Verify solution formatting without modifying files |
| `./scripts/test-csharp-performance.sh` | Run Release allocation and elapsed-time gates; timing measurements require the matching calibrated profile |
| `./scripts/install-csharp-tool.sh` | Build the solution and install/update the global `dotnet ges` CLI |
| `./scripts/uninstall-csharp-tool.sh` | Remove the C# CLI installation; succeeds when already absent |
| `./scripts/pack-csharp.sh 0.1.0-rc1` | Build canonical NuGet and symbol packages for the four public libraries |
| `./scripts/release-csharp-dry-run.sh 0.1.0-rc1` | Pack, stage DLL sets and verify artifact consumption without publishing |
| `./scripts/verify-csharp-reproducibility.sh 0.1.0-rc1` | Build packages independently twice and verify byte-identical results |

Normal library build and test intermediates use project-local `bin`/`obj`
directories. Packages, staged DLL sets, CLI build outputs and generated reports
stay under ignored `artifacts`. Handwritten C# follows
[`CodeStyle.md`](CodeStyle.md) and the root [`.editorconfig`](../../.editorconfig).
Public XML documentation and the approved API surface are checked by the build
and native tests.

### Conformance and native tests

For Release verification including the independent allocation gates:

```bash
dotnet test GameEventScript.sln \
  --configuration Release --filter "TestCategory!=Performance|TestCategory=Allocation"
```

Portable behavior is authored in the shared [Markdown corpus](../../conformance).
The Conformance module’s test project adapts the fileless parser and runner to MSTest.
Each product module owns a separate native test project.
Native tests cover C# adapters, implementation details, public API boundaries,
CLI/process integration and measurements that require the CLR.

The ordinary Conformance run verifies behavior and report structure; its
performance fields echo reference values and are **not measurements**. Actual
managed allocation checks run in Release under the
`csharp-dotnet-release-managed` profile, including an independent zero-allocation
VM hot-path gate. Elapsed-time benchmarks additionally require their calibrated
hardware/toolchain profile. See the [test guide](verification/README.md)
for filters, measurement scope and reference updates.

A complete corpus run writes `ConformanceResults.json`, `ConformanceReport.md`
and `CSharpReferenceResults.received.json` below
`artifacts/conformance/received`. Received results never automatically replace
approved references. The shared
[CapabilityMatrix](../../conformance/cross-language/CapabilityMatrix.md) records
corpus identity and acceptance status across implementations.

## Use the CLI

Install or update from the checkout:

```bash
./scripts/install-csharp-tool.sh
dotnet ges --help
dotnet ges check game.ges
dotnet ges compile handlers.ges definitions.ges -o artifacts/game.gesb
dotnet ges dump artifacts/game.gesb
dotnet ges run artifacts/game.gesb --args 12 Hello 34
dotnet ges run --interactive --color
```

The package is `GameEventScript.Tool`; the installed executable is `dotnet-ges`,
invoked as **`dotnet ges`**. The global tool directory (normally
`$HOME/.dotnet/tools`) must be on `PATH`. The native Swift CLI is named `ges`, so
both implementations can be installed together.

Both installation and removal support `--tool-path DIRECTORY` for an isolated
installation. Run the installer again to update it, or
`./scripts/uninstall-csharp-tool.sh` to remove the global installation. For a
custom directory, use the same `--tool-path` when uninstalling.

By default, `run` drains initialization and sends `Main(args)` with a List of
Text values. `--interactive` selects the event console instead. Full command
syntax, scenarios, console handlers, multiline input and inspection commands
are documented in the [CLI guide](GameEventScript.Tool/README.md).

## Compile source text

For a local embedding, add a project reference to the compiler. Replace the
path with the checkout's location:

```xml
<ItemGroup>
  <ProjectReference Include="/path/to/GameEventScript/implementation/csharp/GameEventScript.Compiler/src/GameEventScript.Compiler.csproj" />
</ItemGroup>
```

Runtime is brought in transitively. The builder accepts source text and optional
diagnostic source names; file access remains with the embedding:

```csharp
using GameEventScript.Api;

var program = GameEventScriptBuilder.Create()
    .AddScript("""
        module example
        function double(_ value as :Number) be value + value
        on Start(value) { emit Done(value: double(value)) }
        """, "example.ges")
    .Compile();
byte[] bytes = GameEventScriptProgramWriter.ToArray(program);
// The embedding can save bytes, or load program directly into a Host.
```

Repeated `AddScript` calls compile sources jointly. `Compile()` can be repeated;
previously returned Programs remain immutable. Debug symbols, source maps and
source archives are included by default. Use
`.WithDebugInfo(GameEventScriptDebugInfoOptions.None)` for compact output,
`.WithProgramVersion(...)` to set the portable version, and
`.WithExternalTypeCatalog(...)` for declarative external types. Runtime bindings
remain separate. `GameEventScriptCompileException.Diagnostics` exposes stable
diagnostic codes and structured context, including source locations.

## Consume the Runtime

A product that receives precompiled Programs needs only the Runtime reference:

```xml
<ItemGroup>
  <ProjectReference Include="/path/to/GameEventScript/implementation/csharp/GameEventScript.Runtime/src/GameEventScript.Runtime.csproj" />
</ItemGroup>
```

The following example loads bytes supplied by the embedding, subscribes to
`Done(value)` and sends `Start(value)` to the program above. The native callback
prints `Done(value: 84)`:

```csharp
using System;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;

static (GameEventScriptStartResult Start, GameEventScriptExecutionResult? Execution) Execute(byte[] bytes)
{
    var program = GameEventScriptProgramReader.Read(bytes);
    var host = GameEventScriptHost.CreateBuilder().WithRandomSeed(123).Build();
    var output = GameEventScriptMessageSignature.Create("Done", ["value"]);
    var subscription = host.Subscribe(output, new Output());
    var instance = host.Load(program);
    try
    {
        var startup = host.Start();
        if (!host.IsReady) return (startup, null);

        host.Receive(GameEventScriptMessage.Create("Start", [
            new GameEventScriptMessageArgument("value", GesValue.GesInteger(42))
        ]));
        return (startup, host.RunToCompletion());
    }
    finally
    {
        instance.Detach();
        subscription.Unsubscribe();
    }
}

sealed class Output : IGameEventScriptNativeMessageHandler
{
    public void Handle(GameEventScriptMessage message, GameEventScriptContext context)
    {
        // Console I/O belongs to this embedding callback, not Runtime.
        Console.WriteLine(message);
    }
}
```

Call `Execute(bytes)` and inspect the startup and execution results. The initial
flow is Build, Load all Programs, register native handlers, then Start. Start
initializes the group without draining its emitted messages. A later Load with initialization returns
an instance with a pending StartResult; ordinary pumping completes that init.
An init failure removes that instance and its pending deliveries while the host
remains ready. [Host runtime](../../specs/HostRuntime.md) defines the full startup and output rules.
`ExecuteFrame(opcodeBudget)` supports bounded stepping and resumption. Retain
the Host and instance between frames when embedding it in a game loop. Dropping
an instance or subscription handle does not detach it; use `Detach()` or
`Unsubscribe()` explicitly. A Host is serial and requires one caller at a time.
The immutable Program can be reused in multiple independent Hosts.

Add `GameEventScript.CSharpBridge` when you need delegate handlers, CLR-backed
external types, attribute-based extensions or `GameEventScriptCSharpHostRunner`
for synchronization and automatic pumping. It does not add a Compiler dependency.
The [public API specification](../../specs/PublicApi.md) documents the portable
contracts and C# mapping; [Host runtime](../../specs/HostRuntime.md) owns lifecycle,
ordering, execution limits and diagnostic behavior.

## Distribution

Runtime, Compiler, CSharpBridge and SyntaxHighlighter are packaged independently with a shared
release version. Conformance stays internal; the CLI is not published on NuGet.
Local packages are written to
`artifacts/csharp/packages/<version>`; the release dry run also stages versioned DLL sets
below `artifacts/csharp/dll/<version>` for Runtime, Compiler and Unity adapters.
XML documentation and portable symbols accompany the
assemblies. CLI packaging is handled separately by its installer.

Use the [C# distribution guide](../../docs/guide/distribution/CSharp.md) for local
NuGet consumption, direct DLL references, package validation, reproducibility
and the gated publication workflow. The dry run never publishes. The Unity DLL
set is checked with an external .NET consumer; validation in a real Unity project
remains deferred.

The [documentation index](../../docs/README.md) links the owning language-neutral
specifications. Deferred work is tracked only in [`BACKLOG.md`](../../BACKLOG.md).

## Explicit message JSON exchange

The Runtime provides `GameEventScriptMessageJson` for optional product transport.
Use `Serialize`/`Deserialize` in C#, or `serialize`/`deserialize` in Swift, for
messages; the corresponding `SerializeValue`/`serializeValue` methods encode a
standalone value. Decoding reconstructs immutable data and never runs Record or
native constructors. External objects export as typed Record snapshots. Local
Publish does not serialize; configure encoding explicitly at your I/O boundary.
The [message format](../../specs/MessageFormat.md) defines the envelope and errors.
