<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Game Event Script CLI

`GameEventScript.Tool` is a separate .NET console application packaged as
a .NET tool with command name `ges`. It targets .NET 8 and references the
portable Runtime, Compiler, and C# bridge. Terminal and filesystem integration
belong to this application or the C# adapters; the libraries do not depend on
the tool.

The entry point supports `ges compile`, `ges dump`, `ges --help`, `ges -h`, and
`ges --version`. No arguments display help. Additional commands and an
interactive event console are tracked in the repository backlog. The
provisional tool package is not published.

## Compile scripts

```sh
ges compile game.ges
ges compile game.ges -o artifacts/game.gesb
ges compile game.ges --output artifacts/game.gesb --no-debug
ges compile handlers.ges definitions.ges -o artifacts/game.gesb
ges compile "scripts/*.ges" -o artifacts/game.gesb --verbose
ges compile game.ges --quiet
ges compile --help
```

The command compiles one or more UTF-8 source files together using the existing
compiler and writes one canonical `.gesb` V1 file. An optional initial UTF-8 BOM is
accepted; invalid UTF-8 is rejected. Paths are relative to the working directory.
Each supplied source path is retained as its source name in diagnostics and
debug metadata. All sources share one compilation: constants, functions, and
other definitions can be referenced across files. The compiler's existing
module rules apply, including agreement of explicit module identifiers.

Explicit inputs retain their argument order. A file-name wildcard such as
`"scripts/*.ges"` or `"part?.ges"` expands at its argument position, with matches
sorted using ordinal string comparison. Wildcards in directory components and
recursive `**` patterns are rejected; unmatched patterns are errors. Quote a
pattern to have the CLI expand it. If the shell expands it first, the CLI keeps
the order supplied by the shell. Matching uses the platform's file-name rules.
Repeated normalized absolute paths are included once, retaining their first
spelling and position; comparison ignores case on Windows. Symbolic-link aliases
are not resolved for deduplication.

For a single resolved input, omitting `-o` or `--output` uses the source path with
its extension replaced by `.gesb`. Multiple inputs require an explicit output
path. Output directories are created as needed. Output must differ from every
input path. A successful compilation replaces an existing output
file. Compilation and encoding finish before writing; a temporary file in the
output directory is then moved into place, so compilation failures do not
truncate an existing output.

Debug symbols, source maps, and the source archive are included by default.
`--no-debug` omits all three. Program version remains the compiler default `0`.
External type catalogs are not configurable through this command yet.

Options may appear before, between, or after source paths. Use `--` before a source
name beginning with `-`, or prefix it with `./`. For output paths beginning with
`-`, use `./` so they cannot be mistaken for an option.

The default success summary on stdout includes the resolved source list, output
path, module, number of handlers, file size in bytes, included debug sections,
and elapsed time. The duration covers source expansion, reading, compilation,
encoding, and writing; it excludes process startup and printing the report.

`--verbose` / `-v` additionally reports the binary format version, bytecode
instruction count and size, required registers and call stack depth, message
bindings, required extension signatures, and required external type signatures.
Handler counts include exact-signature and message-name handlers. Outbound
bindings describe compiled message signatures, not the number of runtime emits.
All counts and dependencies come from the final optimized Program. Bytecode
size counts the V1 instruction data at 16 bytes per instruction, excluding the
section header and instruction-count field. Total file size includes all sections.

`--quiet` / `-q` suppresses success output but still reports errors. Quiet and
verbose cannot be combined. Verbosity and timing do not change the output bytes.

Compiler diagnostics go to stderr with their stable code and, when available,
source path, line, and column, for example `game.ges(2,10): error parse.syntax: …`.
File failures use `cli.io`, invalid UTF-8 uses `cli.invalidEncoding`, and compile
argument errors use `cli.usage`.

| Exit code | Meaning |
| --- | --- |
| `0` | Success, help, or version output |
| `1` | Compilation, source/binary decoding, or file I/O failure |
| `2` | Invalid command or arguments |

## Dump a binary

```sh
ges dump artifacts/game.gesb
ges dump artifacts/game.gesb --addresses
ges dump artifacts/game.gesb -o artifacts/game.gesa
ges dump artifacts/game.gesb > artifacts/game.gesa
ges dump --help
```

The command reads and fully validates one `.gesb` file with the existing Reader,
then uses the portable ProgramDumper to produce the
[GESA representation](../../../../specs/AssemblerFormat.md). It uses embedded
debug information when present and also works with binaries compiled using
`--no-debug`. Original source files, extension implementations, and host bindings
are not needed. The binary is not executed or modified.

Without `-o` or `--output`, stdout contains only GESA text, suitable for piping
or redirection. `--addresses` adds the existing zero-based instruction address
prefixes such as `@0000`; the default omits these prefixes. File and stdout
output use UTF-8 without a BOM and retain the dumper's line endings.

An explicit output path creates parent directories as needed and replaces an
existing file only after decoding and dump generation succeed, using the same
temporary-file replacement as `compile`. It prints a short success message on
stdout instead of the dump. Input and output paths must differ. Prefer `-o` when
preserving a previous dump on failure matters: shell redirection truncates its
destination before the tool starts.

Options may appear before or after the binary path. Use `--` or a `./` prefix
for input names beginning with `-`; use `./` for such output names. The command
accepts one binary and does not expand wildcard patterns.

Malformed or unsupported binaries return exit code `1` and report a stable
`decode.*` code on stderr, with byte offset, section type, and entry index when
provided by the Reader. File errors use `cli.io`, and invalid arguments use
`cli.usage` with exit code `2`. Failed decoding produces no dump text.

## Run from the repository

From the repository root, using the SDK pinned in `global.json` and the .NET 8
runtime:

```sh
dotnet run --project implementation/csharp/tools/GameEventScript.Tool --configuration Release -- --help
```

## Install or update from the repository

On macOS or Linux, run this from the repository root:

```sh
./scripts/install-csharp-tool.sh
ges --help
```

Run the same script again after pulling or editing the source to update the
installation. It builds the entire C# solution in Release configuration, then
packages the tool and installs it for the current user. A build or packaging
failure stops before installation. The script does not run the test suite.

Each invocation creates a unique local prerelease version derived from the
project version, without changing the project file. This ensures that repeated
builds include the current source even when the project version is unchanged.
The script deliberately replaces an installed version with the current checkout,
including when the installed version is newer. Installation uses only the
freshly built package directory; temporary packages are removed afterwards.

An existing `StepH.GameEventScript.Tool` installation in the selected scope is
replaced by `GameEventScript.Tool`. The script first builds and packs the new
tool, then verifies it in a temporary installation before removing the old
package. The command remains `ges`. Subsequent invocations update the new
package normally.

The script can also be invoked by its absolute path from any working directory.
To install or update in a separate directory instead of globally:

```sh
./scripts/install-csharp-tool.sh --tool-path ./artifacts/csharp/tool/install
./artifacts/csharp/tool/install/ges --version
```

Relative installation paths are resolved against the caller's working directory.
For global use, ensure that `$HOME/.dotnet/tools` is on `PATH`.

## Pack and install manually

```sh
dotnet pack implementation/csharp/tools/GameEventScript.Tool --configuration Release
dotnet tool install GameEventScript.Tool --version 0.1.0 --add-source ./artifacts/csharp/tool/packages --tool-path ./artifacts/csharp/tool/install
./artifacts/csharp/tool/install/ges --help
```

On Windows the installed command is `ges.exe`. Build intermediates, binaries,
and packages for this tool are written below `artifacts/csharp/tool`. This local
tool package is separate from the four libraries produced by the existing
library release and reproducibility scripts.

For project-local use, install into a .NET tool manifest instead of supplying
`--tool-path`. The invocation is then `dotnet ges --help` or
`dotnet tool run ges --help`. A global installation uses `ges --help` directly.

The tool is licensed under Apache-2.0; the package includes the full license.
