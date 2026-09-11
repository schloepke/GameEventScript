<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# C# distribution

The C# reference implementation produces four independently consumable
packages. Their current IDs are provisional until the public publisher identity
and final package coordinates are chosen:

- `StepH.GameEventScript` contains the portable Runtime, Program codec and
  validator, Host, shared values, and runtime literal parser.
- `StepH.GameEventScript.Compiler` contains the source builder, compiler,
  and compilation options/errors and depends only on Runtime.
- `StepH.GameEventScript.CSharpBridge` contains optional C# delegate, reflection,
  dictionary, and automatic-runner adapters and depends only on Runtime.
- `StepH.GameEventScript.Conformance` contains the portable Markdown parser,
  runner, and report writers and depends on Runtime and Compiler.

For precompiled `.gesb` execution, reference Runtime and optionally CSharpBridge.
Source compilation additionally requires Compiler. The direct entry points are
`GameEventScriptBuilder.Create()` for compilation and
`GameEventScriptHost.CreateBuilder()` for host construction.
File reading belongs to the CLI or embedding, which supplies text through
`AddScript(text, sourceName)`; the previous Bridge `AddFile` helper is removed.

All four libraries target `netstandard2.1`. Their NuGet packages contain the
Apache-2.0 license, repository README, XML API documentation, deterministic
assemblies, and separate portable-PDB symbol packages.

## Local release dry run

The repository pins the build SDK in `global.json`. The complete local dry run
uses the same entry point as CI:

```bash
./scripts/release-csharp-dry-run.sh 0.1.0-rc.1
```

It builds canonical unsigned NuGet and symbol packages, validates their
metadata and dependency graph, stages DLL sets, and runs external consumers
through local NuGet restore and direct DLL references. Each route compiles a fixture and then loads and executes it in a
separate application referencing only Runtime and CSharpBridge. That application
also rejects Compiler or Conformance DLLs, dependency-manifest entries, and
loaded assemblies. No command in the dry run publishes an artifact.

NuGet assigns random internal Core Properties names during ordinary packing.
The repository package tool rewrites this packaging-only metadata, ZIP entry
order, and timestamps into a canonical form. Package normalization is permitted
only before signing. A signed archive is rejected.

Two independent package runs can be compared byte for byte with:

```bash
./scripts/verify-csharp-reproducibility.sh 0.1.0-rc.1
```

Generated files exist only under the ignored `artifacts/` tree:

```text
artifacts/csharp/
  packages/
    StepH.GameEventScript.<version>.nupkg
    StepH.GameEventScript.<version>.snupkg
    StepH.GameEventScript.Compiler.<version>.nupkg
    StepH.GameEventScript.Compiler.<version>.snupkg
    StepH.GameEventScript.CSharpBridge.<version>.nupkg
    StepH.GameEventScript.CSharpBridge.<version>.snupkg
    StepH.GameEventScript.Conformance.<version>.nupkg
    StepH.GameEventScript.Conformance.<version>.snupkg
  dll/<version>/
    runtime/
    compiler/
    unity/
    conformance/
```

The `runtime` set contains Runtime alone; `compiler` contains Runtime and
Compiler; `unity` contains Runtime and CSharpBridge; `conformance` contains
Runtime, Compiler, and Conformance. All sets include matching XML and PDB files.

## Unity DLL use

The initial Unity integration is the `dll/<version>/unity` set. It contains
`StepH.GameEventScript` and `StepH.GameEventScript.CSharpBridge`; their DLLs are
the runtime inputs, while matching XML and portable PDB files improve editor and
debugger behavior. A consuming Unity project must support `netstandard2.1` and
must import both assemblies together when using the Bridge. Precompiled
execution needs no Compiler DLL; source compilation additionally uses the
Compiler DLL from the `compiler` set.

The repository verifies the exact staged pair through an external project with
ordinary assembly references. This proves that the staged dependency set loads
and executes without project references or Unity dependencies. A real Unity
project remains the final engine compatibility gate because only Unity can
validate its selected scripting backend, API compatibility level, and asset
importer. Unity follow-up work is tracked in `BACKLOG.md`.

## CI and publishing boundary

`C# CI` runs formatting, non-performance Conformance/native tests, the
zero-allocation hot-path gate, the release dry run, and an independent
reproducibility comparison. Performance references are measured separately by
the manually triggered `C# Performance Gate` on a self-hosted macOS/ARM64 runner
matching the checked-in performance profile.

`C# Release Candidate` prepares and uploads private workflow artifacts. NuGet
publishing is additionally gated by all of the following:

- the manual `publish` input;
- repository variable `NUGET_PUBLISH_ENABLED=true`;
- the protected `nuget` GitHub environment;
- a NuGet trusted-publishing policy for this repository/workflow/environment;
- secret `NUGET_USER`, used by the official NuGet login action to request a
  short-lived API key through GitHub OIDC.

The repository variable is absent or false by default, so the publishing job
cannot run. The final package IDs and publisher identity must be approved before
that gate is enabled. Language-port and publication follow-up work is tracked in
`BACKLOG.md`.
