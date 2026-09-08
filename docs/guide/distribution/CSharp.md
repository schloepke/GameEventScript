<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# C# distribution

The C# reference implementation produces three independently consumable
packages. Their current IDs are provisional until the public publisher identity
and final package coordinates are chosen:

- `StepH.GameEventScript` contains the portable compiler, Program codec, Host,
  and runtime.
- `StepH.GameEventScript.CSharpBridge` contains optional C# filesystem, delegate,
  reflection, dictionary, and automatic-runner adapters and depends only on the
  Core package.
- `StepH.GameEventScript.Conformance` contains the portable Markdown parser,
  runner, and report writers and depends only on the Core package.

All three libraries target `netstandard2.1`. Their NuGet packages contain the
Apache-2.0 license, repository README, XML API documentation, deterministic
assemblies, and separate portable-PDB symbol packages.

## Local release dry run

The repository pins the build SDK in `global.json`. The complete local dry run
uses the same entry point as CI:

```bash
./scripts/release-csharp-dry-run.sh 0.1.0-rc.1
```

It builds canonical unsigned NuGet and symbol packages, validates their
metadata and dependency graph, stages DLL sets, and runs two external consumer
checks: one through local NuGet restore and one through direct references to the
Unity DLL set. No command in the dry run publishes an artifact.

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
    StepH.GameEventScript.CSharpBridge.<version>.nupkg
    StepH.GameEventScript.CSharpBridge.<version>.snupkg
    StepH.GameEventScript.Conformance.<version>.nupkg
    StepH.GameEventScript.Conformance.<version>.snupkg
  dll/<version>/
    core/
    unity/
    conformance/
```

## Unity DLL use

The initial Unity integration is the `dll/<version>/unity` set. It contains
`StepH.GameEventScript` and `StepH.GameEventScript.CSharpBridge`; their DLLs are
the runtime inputs, while matching XML and portable PDB files improve editor and
debugger behavior. A consuming Unity project must support `netstandard2.1` and
must import both assemblies together.

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
