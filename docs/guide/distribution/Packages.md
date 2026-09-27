<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Library packages and releases

GES libraries share one release version and Git tag. There are no independently
versioned Swift module repositories.

The repository's release badges show the latest non-prerelease NuGet package
and GitHub release. Swift Package Index badges report its observed Swift-version
and platform build compatibility, not shared Conformance acceptance. Consult the
[release notes](https://github.com/schloepke/GameEventScript/releases) for migration
guidance and the [changelog](../../../CHANGELOG.md) for unreleased changes.
See the [version policy](#version-policy) for compatible updates and contract changes.

> **Since: 0.2.0 — package icons**

NuGet packages embed the Pixel-Duo logo as `icon.png`; no external image URL is
required. The release package check verifies both the manifest reference and
the embedded image against the checked-in brand asset. Existing published
versions retain their original package contents; icons are included starting with 0.2.0.

| Distribution | Public products |
| --- | --- |
| NuGet | `GameEventScript.Runtime`, `GameEventScript.Compiler`, `GameEventScript.CSharpBridge`, `GameEventScript.SyntaxHighlighter` |
| SwiftPM | `GameEventScriptRuntime`, `GameEventScriptCompiler`, `GameEventScriptSwiftBridge`, `GameEventScriptSyntaxHighlighter` |

Compiler and each native Bridge depend on their Runtime. An application can use
Runtime alone. SyntaxHighlighter is optional and depends on no other GES module. Conformance stays in the repository for development and CI. CLI
executables are not part of the public library package distribution; their local
installers remain available. See [standalone CLI downloads](Tools.md) for
installation and release verification. Deferred distribution work is tracked in
[BACKLOG.md](../../../BACKLOG.md).

## Version policy

GES uses `MAJOR.MINOR.PATCH` release numbers. This section owns the release
numbering policy; language, API and binary behavior remain defined by their
owning specifications.

### Before 1.0

| Release | Allowed changes | Example |
| --- | --- | --- |
| Patch | Compatible corrections to the existing contract; no new language or API features and no incompatible contract changes | `0.2.0` → `0.2.1` |
| Minor | New features, compatible improvements, or deliberate contract changes with migration guidance | `0.2.1` → `0.3.0` |

A 0.x release is a usable development release, not automatically a release
candidate for 1.0. There is no general compatibility guarantee between 0.x minor
versions. Consumers should pin an exact version or constrain updates to the
selected minor line and review migration notes before changing that line.

A correction may change behavior that violated the documented contract. It must
not silently redefine that contract under the label of a bug fix. If a fix
requires an incompatible contract change, it belongs in the next minor release.
Document observable corrections in the changelog, including any impact on users
who relied on the incorrect behavior.

### From 1.0 onward

GES follows [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html)
for its declared compatibility surface:

| Release | Allowed changes | Example |
| --- | --- | --- |
| Patch | Backward-compatible fixes | `1.0.0` → `1.0.1` |
| Minor | Backward-compatible additions and deprecations | `1.0.1` → `1.1.0` |
| Major | Incompatible changes to the declared contracts | `1.1.0` → `2.0.0` |

The owning specifications define
[source compatibility](../../../specs/Language.md#source-compatibility-across-releases),
[recompilation and determinism](../../../specs/Semantics/Determinism.md#compatibility-and-recompilation),
and [binary acceptance](../../../specs/BinaryFormat.md#release-and-format-compatibility).
The concrete 1.0 language/API scope and any pre-1.0 bytecode redesign still need
final review. These future guarantees do not freeze today's 0.x API; pre-1.0
binaries have no guaranteed support in 1.0. Retain source for recompilation.

### Mutation and persistence review for 1.0

The review found no known requirement for a breaking change arising from
application-owned mutation and persistence. The existing native integration
boundaries support this model. The owning rule is
[state ownership](../../../specs/Language.md#state-ownership).
This conclusion does not settle the separate source, API or binary compatibility
reviews.

| Boundary reviewed | Result and remaining responsibility |
| --- | --- |
| Immutable Program and script data | Application state stays outside the reusable Program and `.gesb`; no storage handles or persistence schema are added to them. |
| Native handlers and extensions | Existing synchronous callbacks can perform game-state changes. The application defines their effects and error recovery. |
| External values | Field access must preserve the logical-value stability required during a handler. An extension changing the game does not authorize changing the same exposed logical value underneath a running or paused handler. |
| Startup and failures | Initialization stages GES outputs; it does not undo external effects already performed by callbacks. Applications needing all-or-nothing changes must provide their own staging or compensation. |
| Frames and multiple hosts | Pausing a script does not create an application transaction. Separate hosts do not establish a global order over shared mutable application state; the embedding supplies synchronization and ordering. |
| Persistence and replay | Product JSON transports data snapshots, not a saved VM or restored native binding. Application schema migration, recovery, duplicate-action handling and reproduction of native inputs remain application concerns. |

The remaining risks are integration responsibilities: partially completed native
operations, changing external observations, and concurrent access to shared game
state. GES does not resolve them by adding mutable language state. Revisit this
review if the project later proposes script-owned storage, transactional native
calls, or persistence of live execution state.

See [Host runtime](../../../specs/HostRuntime.md),
[External-type API](../../../specs/PublicApi.md#external-type-api),
[Determinism](../../../specs/Semantics/Determinism.md) and
[Message format](../../../specs/MessageFormat.md) for the existing contracts.

### Shared versions, formats and prereleases

- Public library packages and CLI artifacts for a release use the same GES
  version and source tag. Upgrade an application's GES libraries together.
  A future language implementation joins the current release line; it does not
  restart at 1.0 or imply that earlier versions existed for that implementation.
- The package version, `.gesb` format version and application-owned
  `ProgramVersion` are separate identifiers. A package release does not
  automatically increment the binary format version. Binary compatibility and
  rejection rules belong to [Binary format](../../../specs/BinaryFormat.md) and
  [Bytecode](../../../specs/Bytecode.md), not to an inference from package numbers.
- Prerelease suffixes such as `0.3.0-rc.1` identify candidates for the named
  release. They are optional; a verified development release can be published
  directly as `0.3.0`. Candidates precede the corresponding final version and
  may change before that final release.
- Published versions and tags are immutable. Corrections require a new version;
  never replace an existing release's package contents or move its tag.
- Record user-visible changes under `Unreleased`, then publish them with the
  release. Contract changes require migration notes explaining the affected
  behavior and the action consumers need to take. Documentation availability
  notes describe the current behavior's release, not a full compatibility matrix.

## SwiftPM and Xcode

The repository-root `Package.swift` is the public entry point. It declares four
library products using the existing source directories; it does not depend on
the sibling development packages, expose Conformance, or include the CLI.

> **Since: 0.2.0 — root-package native tests**

The root package also declares three test targets that reuse the existing
SwiftBridge, bridge-macro and SyntaxHighlighter test sources. They are not
library products and are not compiled when an application consumes the libraries.
Run them from a repository checkout with:

```sh
swift test --scratch-path artifacts/swift/root-tests --disable-build-manifest-caching --configuration release
```

The highlighter tests read the shared Markdown fixture under
`conformance/highlighting`; no Conformance library dependency is added.

In Xcode, select **File → Add Package Dependencies**, enter
`https://github.com/schloepke/GameEventScript.git`, select a published version,
and add the desired products to your app target. SwiftPM downloads the repository,
but a Runtime-only dependency does not build or link Compiler or SwiftBridge.

For a consuming Swift package, after the corresponding version has been released:

```swift
platforms: [.macOS(.v10_15)],
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
and local SwiftBridge manifests explicitly require macOS 10.15 to match their
SwiftSyntax macro dependency. Consuming packages must declare this or a newer
macOS target. This is a macOS deployment minimum, not an Apple-only restriction.
The CLI/Conformance tools separately require macOS 10.15.4.

There is no upload to Apple or a Swift registry in this flow. Publishing a Git
version tag makes the root package available to SwiftPM; it does not itself
create a GitHub release or publish NuGet packages. The individual packages
under `implementation/swift` remain local development and verification entry points.

## Verify a release without publishing

```sh
./scripts/release-csharp-dry-run.sh 0.1.0-rc1
./scripts/verify-csharp-reproducibility.sh 0.1.0-rc1
python3 scripts/test-publish-csharp-packages.py
python3 scripts/test-swift-package.py
```

The C# dry run creates four canonical NuGet packages, matching symbols and DLL
sets, then compiles and executes independent consumers. See [C# distribution](CSharp.md).
The publication-selection tests substitute a fake `dotnet`; they never contact
NuGet. They verify that stale, internal and tool packages cannot enter the upload
list and that missing artifacts stop the entire upload before its first request.

The SwiftPM check copies the current root manifest, library/test sources and
required Markdown fixture into an
isolated Git repository under `artifacts/swift-package`, creates a local test
tag, and resolves that version from two independent consumers. It checks all
four products, the dependency graph and the explicit macOS minimum in both macro
manifests, compiles/serializes/executes a Program,
and runs that Program with Runtime alone. It also rejects builds of Compiler or
Bridge in the Runtime-only consumer and compilation of test targets/support in
either consuming application. It executes the three root test targets in the
isolated checkout. No tag or commit is added to the working repository and
nothing is published; SwiftPM may fetch the official SwiftSyntax dependency.

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

Select the release number using the [version policy](#version-policy).

Update applicable `Since: Unreleased` notes in specifications and guides to the
selected release version. Retain only the version of the currently described
behavior; do not add previous definitions to the specification.

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
