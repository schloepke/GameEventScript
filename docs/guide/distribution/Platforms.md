<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Platform support and release evidence

For 0.4.0, support is scoped to the product and verification level below.
The tables describe the configured checks, not a claim that an unreleased
candidate has already passed them. A release must retain successful evidence
for its exact source commit before claiming the corresponding support.

**Conformance** verifies the shared language/runtime contract. **Consumer**
checks build and run applications using the distributed packages. **CLI** checks
execute the relocated tool on its native platform. A CLI smoke test or a
successful library build is not full Conformance on that platform.

## Libraries and development toolchains

| Product | Declared package contract | CI host and toolchain | Verification |
| --- | --- | --- | --- |
| C# Runtime, Compiler, CSharpBridge, Syntax | .NET Standard 2.1 | Linux x64, `ubuntu-latest`; SDK **10.0.201**, .NET **8.0.x** runtime | Full C# Conformance and native tests, allocation gate, .NET 8 package consumers, reproducibility: [C# CI](../../../.github/workflows/csharp-ci.yml), job `verify` |
| Swift Runtime, Compiler, SwiftBridge, Syntax | Swift tools **6.0**; root package declares macOS **10.15.4** | macOS ARM64, `macos-15`; runner-provided Swift/Xcode, exact Swift identity recorded in the job log | Native Swift Conformance, C# binary interoperability, native tests and API checks: [Swift CI](../../../.github/workflows/swift-ci.yml), job `native`; fresh tagged package consumers and root tests: job `distribution` |
| Standalone Swift Syntax | Swift tools **6.0** | Linux x64, `swift:6.0` container | Highlighter package tests only: Swift CI job `highlighter-linux` |

The pinned .NET SDK is defined in [global.json](../../../global.json).
Swift's package tools version is a manifest requirement, not a claim that every
product is tested with every Swift compiler from 6.0 onward. The macOS deployment
target likewise does not mean that tests run on macOS 10.15.4. The local Runtime,
Compiler and Syntax packages have no explicit Apple platform floor;
the public root package contains the Bridge, while the CLI is a separate package. See
[Package.swift](../../../Package.swift) for the actual distribution contract.

The complete Swift corpus is currently checked on macOS ARM64; the complete C#
corpus is checked on Linux x64. Other hosts below have narrower package/CLI
verification. Swift Package Index results are additional build observations,
not substitutes for these checks. Unity, iOS, Android, Windows Swift and other
embeddings have no dedicated full Conformance jobs and are not independently
qualified by this matrix. Portability of the core does not establish those
platforms' integration, threading, AOT or deployment behavior.

## Standalone CLI downloads

These archives include their runtime requirements and do not require an installed
Swift or .NET SDK. The [CLI distribution workflow](../../../.github/workflows/cli-distribution.yml)
builds each target and executes its relocated archive before the `verify` job
accepts the complete set.

| Implementation / target | Native verification host | Build toolchain |
| --- | --- | --- |
| C# `win-x64` | `windows-2022` | .NET SDK 10.0.201 |
| C# `win-arm64` | `windows-11-arm` | .NET SDK 10.0.201 |
| Swift `osx-x64` | `macos-15-intel` | Runner-provided Swift/Xcode |
| Swift `osx-arm64` | `macos-15` | Runner-provided Swift/Xcode |
| Swift `linux-x64` | Plain Ubuntu 22.04 x64 container on `ubuntu-22.04` | Swift **6.4.0**, matching Static Linux SDK |
| Swift `linux-arm64` | Plain Ubuntu 22.04 ARM64 container on `ubuntu-22.04-arm` | Swift **6.4.0**, matching Static Linux SDK |

The Linux compiler container and SDK are pinned by digest/checksum in the
workflow. Linux smoke checks run without Swift or .NET installed. The supported
macOS download baseline is macOS 15; package deployment targets do not lower
that tested download baseline. Other Linux distributions are not verified by
these Ubuntu checks. Archive safety, complete target coverage and revision-bound
verification receipts are enforced by the distribution scripts; publication is
a separate guarded step. See [CLI downloads](Tools.md).

## NuGet CLI tools

The portable `GameEventScript.Tool` targets .NET 8. The AOT tool family supplies
six native RIDs; installation uses .NET SDK 10 or newer. Its native package
checks are separate from the standalone downloads above.

| Package target | Native package consumption host |
| --- | --- |
| Portable tool | `ubuntu-22.04` x64 |
| `win-x64` | `windows-2022` |
| `win-arm64` | `windows-11-arm` |
| `linux-x64` | `ubuntu-22.04` |
| `linux-arm64` | `ubuntu-22.04-arm` |
| `osx-x64` | `macos-15-intel` |
| `osx-arm64` | `macos-15` |

[C# Tool Packages](../../../.github/workflows/csharp-tool-packages.yml) uses
SDK 10.0.201 and .NET 8.0.x, builds and consumes each target in job `package`,
and validates the complete release set in `verify`. Linux AOT targets are glibc
targets, not musl/Alpine targets. Native package consumption is not a full corpus
run on every RID.

## WebAssembly

The [Website workflow](../../../.github/workflows/website.yml), job `card-game`,
builds and tests the Card Lab with pinned Swift **6.4.0** and its matching WASM
SDK, plus Node **24**. Pins live in
[setup-wasm.py](../../../examples/card-game/scripts/setup-wasm.py).
This verifies the Card Lab's embedding and WASM execution, not every root
SwiftPM product or every browser. Safari/Chrome UI checks performed locally are
not an automated browser support matrix. There is no distributed WASM CLI.

## Release qualification

For each release candidate, record the revision, workflow run links, actual
toolchain versions and any limitations in the release validation evidence:

1. Require successful **C# CI / verify** and a full **Swift CI / verify** run,
   including `native`, `distribution` and `highlighter-linux`. A documentation-only
   skipped native run does not qualify a release; dispatch Swift CI manually to
   force the full checks.
2. Run **C# Release Candidate** with publication disabled. Its package and
   [release-upgrade checks](../../../conformance/cross-language/ReleaseUpgrade.md)
   must succeed. Require all native **C# Tool Packages** targets and their
   aggregate `verify` result.
3. Run **CLI Distribution** with the intended version and publication disabled;
   require its complete `verify` result. Require **Website / card-game** for the
   Card Lab shipped from the same revision.
4. Require the separate [performance and allocation evidence](Performance.md)
   for both implementations; hosted correctness checks do not replace measured
   regression checks.
5. Review this matrix against the manifests, pinned SDKs and workflow matrices.
   Add a corresponding build-and-execute job before declaring a new target
   supported. Record moving runner/toolchain versions rather than treating a
   runner label as a compiler pin. Missing, cancelled or failed required evidence
   blocks the corresponding release claim.

Path-filtered PR jobs may not run on every commit. Dispatch the affected
workflows on the final candidate when evidence is absent; previous-revision
success is not sufficient. These requirements reuse existing checks and add no
native Swift work to ordinary PR CI. Performance measurements use separate,
hardware-matched profiles and do not expand this platform support matrix.
