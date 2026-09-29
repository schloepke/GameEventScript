<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# CLI installation and downloads

> **Since: 0.2.0 — standalone CLI archives**

Download the CLI from the assets of a [GameEventScript GitHub release](https://github.com/schloepke/GameEventScript/releases).
Standalone archives are introduced with 0.2.0; older releases contain only the
library packages. CLI downloads are separate from NuGet and
SwiftPM library products. You do not need a development SDK to run them.

## Install through Homebrew (macOS and Linux)

The [official project tap](https://github.com/schloepke/homebrew-gameeventscript)
installs the native Swift CLI for macOS 15+ or Linux on ARM64/x64:

```sh
brew install schloepke/gameeventscript/ges
ges --version

# Update or remove:
brew update
brew upgrade ges
brew uninstall ges
```

Homebrew must already be installed; no Swift toolchain or .NET installation is
needed. The formula selects a published release archive and verifies its SHA-256.
If you previously installed `ges` manually, use `which -a ges` to check which
installation your PATH selects. The tap does not remove other installations.

## Build with SwiftPM

> **Since: 0.3.0 — public SwiftPM executable**

The root package exposes `ges` alongside the four libraries. With a compatible
Swift toolchain installed, clone the release and build/run the CLI:

```sh
git clone --branch 0.3.0 --depth 1 https://github.com/schloepke/GameEventScript.git
cd GameEventScript
swift run --scratch-path artifacts/swift/public-cli --disable-build-manifest-caching --configuration release ges --help
```

This compiles from source; it does not download a prebuilt executable. Library
consumers do not need to build the CLI. Homebrew remains the recommended installation
for macOS/Linux users who want a prebuilt tool without a Swift toolchain.

## Install through NuGet

> **Since: 0.3.0 — NuGet CLI tools**

Starting with 0.3.0, choose one of these packages:

```sh
# Portable .NET tool (requires the .NET 8 runtime):
dotnet tool install --global GameEventScript.Tool

# Native AOT tool (.NET SDK 10+ required for installation):
dotnet tool install --global GameEventScript.Tool.Aot
```

Both install the command `dotnet-ges`; use `dotnet ges` or invoke `dotnet-ges`
directly. The AOT executable needs no .NET runtime after installation. Its package
selects Windows, macOS or glibc-based Linux for x64/ARM64 automatically. Linux
binaries are built on Ubuntu 22.04; musl/Alpine is not an AOT target. The portable
package is the alternative for environments without a matching native package.

Update the selected package with `dotnet tool update --global PACKAGE_ID`.
They are alternatives: before switching, uninstall the old package with
`dotnet tool uninstall --global PACKAGE_ID`, then install the other. Use
`--tool-path DIRECTORY` instead of `--global` for a custom installation directory.
The GES version is shared with the libraries. Releases before 0.3.0 do not
include these packages.

AOT package installation requires the .NET 10 tool protocol. Native execution is
independent of that SDK; the `dotnet ges` convenience spelling still needs the
`dotnet` launcher. Swift downloads remain available separately.

## Choose a download

Replace `VERSION` with the selected release version, without a leading `v`.

| Implementation | System | Architectures | Archive | Command |
| --- | --- | --- | --- | --- |
| C# | Windows | x64, ARM64 | `ges-csharp-VERSION-win-ARCH.zip` | `dotnet-ges.exe` |
| Swift | Linux | x64, ARM64 | `ges-swift-VERSION-linux-ARCH.tar.gz` | `ges` |
| Swift | macOS 15 or newer | Intel x64, Apple Silicon ARM64 | `ges-swift-VERSION-osx-ARCH.tar.gz` | `ges` |

Starting with 0.3.0, releases provide C# archives for Windows and Swift archives for macOS/Linux.
Release 0.2.0 also contains C# macOS/Linux archives; those historical downloads
remain available. The NuGet tools continue to support all three operating systems.

The Windows C# download includes the .NET 8 runtime and targets Windows 10/11.
No separate .NET installation is required when invoking `dotnet-ges.exe`
directly. If the .NET SDK is already installed and the archive's `bin` directory
is on `PATH`, `dotnet ges` also resolves this command. Keep every file in `bin`
together: the executable alone is not sufficient.

The Swift Linux download is statically linked using the official Static Linux
SDK, including the Swift runtime and Foundation. It does not require a Swift
installation. Native smoke tests run on Ubuntu 22.04 on each architecture.
The macOS download uses system libraries and requires no Xcode installation.
Local builds from source retain their separately documented deployment minimums.

These initial archives are not Developer ID notarized or Authenticode signed.
The operating system may ask for confirmation on first launch. SHA-256 checksums
detect download corruption; they are not an independent publisher signature.

## Verify, install and update

Download the matching archive and `ges-cli-VERSION-SHA256SUMS.txt` from the same
GitHub release. On Linux run `sha256sum --ignore-missing -c ges-cli-VERSION-SHA256SUMS.txt`;
on macOS run `shasum -a 256 <archive>` and compare its digest with the matching line.
On Windows use `Get-FileHash <archive> -Algorithm SHA256` in PowerShell.

Extract into a version-specific directory of your choice. On macOS/Linux:

```sh
mkdir -p "$HOME/.local/opt"
tar -xzf ges-swift-VERSION-osx-arm64.tar.gz -C "$HOME/.local/opt"
"$HOME/.local/opt/ges-swift-VERSION-osx-arm64/bin/ges" --version
export PATH="$HOME/.local/opt/ges-swift-VERSION-osx-arm64/bin:$PATH"
```

Select the filename for your implementation, system and architecture. Add the
`PATH` line to your shell configuration for a persistent installation. For C#,
use the matching directory and `dotnet-ges` command. Both commands can coexist.

On Windows, extract the ZIP with Explorer or `Expand-Archive`, execute
`bin\dotnet-ges.exe --version`, and add that complete `bin` path to your user
`PATH` through Environment Variables. No administrator installation is needed.

To update, verify and extract the new archive into a new directory, test
`--version`, then point `PATH` at its `bin` directory. Do not overlay old and new
runtime files. Remove the previous directory after switching. To uninstall,
remove the `PATH` entry and the extracted directory. The repository's source-build
installers continue to manage their own installations separately.

```sh
ges check example.ges
ges compile example.ges
ges run example.gesb --args Hello 42
ges run --interactive --color
```

Substitute `dotnet-ges` for the C# download. Both CLIs implement the same language
and binary format; see the C# and Swift CLI guides in the documentation website.

## Maintainer build and verification

From the repository root, prepare a target on its matching native system without publishing
(the C# example runs on Windows, the Swift example on macOS ARM64):

```sh
python3 scripts/package-cli.py csharp 0.3.0 win-x64
python3 scripts/verify-cli-archive.py csharp 0.3.0 win-x64 --directory artifacts/cli/0.3.0
python3 scripts/package-cli.py swift 0.3.0 osx-arm64
python3 scripts/verify-cli-archive.py swift 0.3.0 osx-arm64 --directory artifacts/cli/0.3.0
```

For Linux Swift builds, install the pinned Static Linux SDK from the workflow
and pass `--swift-sdks-path <directory>` to the packager. The open-source Swift
compiler and Static SDK must match; Xcode's compiler does not support that SDK.
All generated sources, temporary workspaces, archives and receipts stay under
`artifacts/cli`. Each archive contains the selected release version, Git revision,
installation instructions, project license and applicable dependency notices.
Swift version injection occurs only in a disposable source copy.

Verification extracts into a different path containing spaces, clears toolchain
discovery, and exercises version/help, check, compile, binary execution, dump,
Unicode arguments, redirected interactive input, delayed delivery and exit codes.
Only a successful native run creates a receipt. Linux archives are additionally
tested in a clean Ubuntu container without .NET or Swift installed. The final
collection requires every target's receipt, revision and archive hash to match.

## GitHub release workflow

The **CLI Distribution** workflow checks affected pull requests. It can also be
started manually with a version and `publish=false` to prepare downloadable CI
artifacts. Each target builds independently; no registry upload occurs.

To publish, merge and verify the intended release, create its matching `VERSION`
or `vVERSION` Git tag and a GitHub release for that tag, then run the workflow from
the tag with the same version and `publish=true`. Configure the `cli-release`
GitHub environment with release approval requirements before enabling publication.
The publication job uses GitHub's `GITHUB_TOKEN` with `contents: write`, accepts
only the complete verified set from that run, and attaches archives plus the
combined checksum file to the existing release. It never changes the release
text, creates a release automatically or overwrites an existing download.

After publishing the Swift archives, update `Formula/ges.rb` in the
[Homebrew tap](https://github.com/schloepke/homebrew-gameeventscript) with the
release version, four download URLs and their published SHA-256 values.
Merge the tap update after its four-platform installation tests pass; `brew update`
then makes the release available to users. This is currently a separate manual
step, not performed by the CLI Distribution workflow.

Platform signing/notarization remains separate follow-up work. No CLI executable is added to the root SwiftPM products. NuGet tool
publication uses the separate C# release process below.

## Prepare and publish NuGet tools

The **C# Tool Packages** workflow checks affected pull requests and is reused by
**C# Release Candidate**. It builds the portable package plus six AOT targets on
native runners. Each job installs its package from an isolated local NuGet feed
and runs process checks; macOS/Linux AOT jobs also run real terminal checks.
The local feed exclusively supplies GES packages; Microsoft SDK shim dependencies
may be downloaded from NuGet.org.

For local dry runs, run these from a compatible build host:

```sh
python3 scripts/pack-csharp-tool.py 0.0.0-local.1 portable
python3 scripts/pack-csharp-tool.py 0.0.0-local.1 osx-arm64
```

Output is under `artifacts/csharp/tool-packages/VERSION/TARGET`. The release set
contains `GameEventScript.Tool`, `GameEventScript.Tool.Aot`, and six
`GameEventScript.Tool.Aot.RID` implementation packages. The latter are internal
installation dependencies; users install the top-level package. All eight must
have the same version. Conformance is never included.

Use **C# Release Candidate** on the matching tag with `publish=false` to prepare
and verify the complete set. Enable `publish` only when ready. The existing
protected `nuget` environment, `NUGET_PUBLISH_ENABLED` variable, and Trusted
Publishing policy for `csharp-release-candidate.yml` govern publication. The
policy's `GameEventScript.*` scope must cover both tool IDs and all RID packages.
The workflow validates the full library/tool set before the first upload, publishes
native dependencies before the AOT pointer, and never publishes from PRs.
Preparing packages does not publish or create a release.
