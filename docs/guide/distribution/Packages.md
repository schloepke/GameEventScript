<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Library packages and releases

GES libraries share one release version and Git tag. There are no independently
versioned Swift module repositories.

| Distribution | Public products |
| --- | --- |
| NuGet | `GameEventScript.Runtime`, `GameEventScript.Compiler`, `GameEventScript.CSharpBridge` |
| SwiftPM | `GameEventScriptRuntime`, `GameEventScriptCompiler`, `GameEventScriptSwiftBridge` |

Compiler and each native Bridge depend on their Runtime. An application can use
Runtime alone. Conformance stays in the repository for development and CI. CLI
executables are not part of the public library package distribution; their local
installers remain available. Standalone CLI release downloads are tracked in
[BACKLOG.md](../../../BACKLOG.md).

## SwiftPM and Xcode

The repository-root `Package.swift` is the public entry point. It declares three
library products using the existing source directories; it does not depend on
the sibling development packages, expose Conformance, or include the CLI.

In Xcode, select **File → Add Package Dependencies**, enter
`https://github.com/schloepke/GameEventScript.git`, select a published version,
and add the desired products to your app target. SwiftPM downloads the repository,
but a Runtime-only dependency does not build or link Compiler or SwiftBridge.

For a consuming Swift package, after the corresponding version has been released:

```swift
dependencies: [
    .package(url: "https://github.com/schloepke/GameEventScript.git", from: "0.1.0")
],
targets: [
    .target(name: "MyApp", dependencies: [
        .product(name: "GameEventScriptRuntime", package: "GameEventScript")
    ])
]
```

For a prerelease, select that exact version, for example
`.package(url: "https://github.com/schloepke/GameEventScript.git", exact: "0.1.0-rc1")`.
These version numbers are examples, not a claim that they have been published.
Use `import GameEventScriptRuntime` and optionally the Compiler/SwiftBridge imports.

Swift 6.0 or newer is required. macOS is the currently CI-verified platform;
other platform support must be verified before being advertised. The root library
manifest does not inherit the CLI/Conformance tools' macOS 10.15.4 requirement.

There is no upload to Apple or a Swift registry in this flow. Publishing a Git
version tag makes the root package available to SwiftPM. The individual packages
under `implementation/swift` remain local development and verification entry points.

## Verify a release without publishing

```sh
./scripts/release-csharp-dry-run.sh 0.1.0-rc1
./scripts/verify-csharp-reproducibility.sh 0.1.0-rc1
python3 scripts/test-publish-csharp-packages.py
python3 scripts/test-swift-package.py
```

The C# dry run creates three canonical NuGet packages, matching symbols and DLL
sets, then compiles and executes independent consumers. See [C# distribution](CSharp.md).
The publication-selection tests substitute a fake `dotnet`; they never contact
NuGet. They verify that stale, internal and tool packages cannot enter the upload
list and that missing artifacts stop the entire upload before its first request.

The SwiftPM check copies the current root manifest and library sources into an
isolated Git repository under `artifacts/swift-package`, creates a local test
tag, and resolves that version from two independent consumers. It checks all
three products and the dependency graph, compiles/serializes/executes a Program,
and runs that Program with Runtime alone. It also rejects builds of Compiler or
Bridge in the Runtime-only consumer. No tag or commit is added to the working
repository, and no remote is contacted by this check.

The manually dispatched **C# Release Candidate** workflow runs the C# tests and
dry run, plus the SwiftPM consumer gate on macOS. With `publish=false`, it only
uploads GitHub workflow artifacts. Their access follows repository visibility.
Ordinary PR CI also verifies packaging, consumers and publication selection.

## One-time NuGet and GitHub setup

1. Choose the NuGet package owner (user or organization) and confirm the three
   package IDs can be published by that owner. Package `Authors` metadata does
   not determine NuGet ownership.
2. On nuget.org, create a Trusted Publishing policy for repository owner
   `schloepke`, repository `GameEventScript`, workflow filename
   `csharp-release-candidate.yml`, and environment `nuget`. Update these values
   if repository ownership changes. Scope the policy to `GameEventScript.*`,
   permitting both new packages and new versions.
3. In GitHub, create the `nuget` environment with the required release reviewer
   and restrict deployment to the intended version tags. Merely naming an
   environment in YAML does not configure approval rules.
4. Add `NUGET_USER` as a repository or `nuget` environment secret. Its value is
   the NuGet login username, not an email address or API key. The official
   `NuGet/login` action obtains a short-lived key through GitHub OIDC.
5. Keep the repository variable `NUGET_PUBLISH_ENABLED` absent/false until ready
   to enable publication. Set it to `true` when this setup is complete.

See the official [Trusted Publishing instructions](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing).
Neither a website/domain nor author-signing the NuGet archives is required by
this workflow. Canonical packaging must precede any optional signing.

## Publish a shared version

Before the release preparation PR is merged, move the applicable `Unreleased`
entries in [CHANGELOG.md](../../../CHANGELOG.md) into a section named for the
selected version and release date, leaving an empty `Unreleased` section for
future changes. Review migration guidance alongside the implementation. Use
that versioned section as the basis for the GitHub Release description; generated
PR lists may supplement it.

1. Merge the reviewed changes and verify the C# and Swift checks for the selected
   commit. Run the release workflow with `publish=false` and the intended version.
2. Once the release is approved, create and push a Git tag such as `0.1.0-rc1`
   (or `v0.1.0-rc1`) on that same commit. This is already the SwiftPM publication
   step; do not create public version tags merely to test packaging.
3. Dispatch **C# Release Candidate** against that tag, enter version `0.1.0-rc1`
   without the optional `v`, and select `publish=true`. Publication from a branch
   or a different version tag is rejected. The workflow must exist on the default
   branch before it can be dispatched normally through GitHub Actions.
4. After both preparation jobs succeed, approve deployment to `nuget`. The upload
   job downloads the verified artifacts from the same run and submits only the
   three selected-version library packages and their symbols.
5. Verify NuGet validation/indexing and consumption through Xcode/NuGet. A GitHub
   Release can then document the version and link to the packages.

Swift tags and NuGet uploads are separate publication operations, not a
transaction. A failed NuGet upload does not withdraw a Swift tag, and some NuGet
packages may already have been accepted. Inspect the outcome before retrying;
the script deliberately does not silently skip duplicate versions. Never move a
published version tag or overwrite its intended release contents.
