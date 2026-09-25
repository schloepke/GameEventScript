<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Standalone CLI downloads

> **Since: Unreleased — standalone CLI archives**

Download the CLI from the assets of a [GameEventScript GitHub release](https://github.com/schloepke/GameEventScript/releases).
The first release containing these assets is still being prepared; older releases
may contain only the library packages. CLI downloads are separate from NuGet and
SwiftPM library products. You do not need a development SDK to run them.

## Choose a download

Replace `VERSION` with the selected release version, without a leading `v`.

| Implementation | System | Architectures | Archive | Command |
| --- | --- | --- | --- | --- |
| C# | Windows | x64, ARM64 | `ges-csharp-VERSION-win-ARCH.zip` | `dotnet-ges.exe` |
| C# | Linux with glibc | x64, ARM64 | `ges-csharp-VERSION-linux-ARCH.tar.gz` | `dotnet-ges` |
| C# | macOS | Intel x64, Apple Silicon ARM64 | `ges-csharp-VERSION-osx-ARCH.tar.gz` | `dotnet-ges` |
| Swift | Linux | x64, ARM64 | `ges-swift-VERSION-linux-ARCH.tar.gz` | `ges` |
| Swift | macOS 15 or newer | Intel x64, Apple Silicon ARM64 | `ges-swift-VERSION-osx-ARCH.tar.gz` | `ges` |

The C# download includes the .NET 8 runtime. No separate .NET installation is
required when invoking `dotnet-ges` directly. If the .NET SDK is already installed
and the archive's `bin` directory is on `PATH`, `dotnet ges` also resolves this
command. Keep every file in `bin` together: the executable alone is not sufficient.
Linux C# builds use Ubuntu 22.04 and require the normal .NET 8 native dependencies,
including glibc, libstdc++, OpenSSL, ICU and zlib. Alpine/musl is not a C# target.
macOS C# builds require macOS 12 or newer; Windows builds target Windows 10/11.

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

From the repository root, prepare one native target without publishing:

```sh
python3 scripts/package-cli.py csharp 0.2.0-rc.1 osx-arm64
python3 scripts/verify-cli-archive.py csharp 0.2.0-rc.1 osx-arm64 --directory artifacts/cli/0.2.0-rc.1
python3 scripts/package-cli.py swift 0.2.0-rc.1 osx-arm64
python3 scripts/verify-cli-archive.py swift 0.2.0-rc.1 osx-arm64 --directory artifacts/cli/0.2.0-rc.1
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

Homebrew installation and platform signing/notarization remain separate follow-up
work. No CLI executable is added to the root SwiftPM products or public NuGet feed.
