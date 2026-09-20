<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Native Swift CLI

`GameEventScriptTool` is a separate SwiftPM executable package. It installs `ges`
and depends on the local Runtime and Compiler packages, Foundation, and a small
POSIX terminal adapter. It has no .NET or third-party package dependency.
Runtime and Compiler remain independent of terminal and filesystem services.

## Build, install and update

Use Swift 6.0 or newer on macOS or Linux. macOS is verified by the native tests
and real pseudoterminal checks; Linux uses the same POSIX adapter.

From the repository root:

```sh
./scripts/install-swift-tool.sh
ges --version
ges --help
```

The installer builds all Swift packages in Release and installs a standalone
executable in `$HOME/.local/bin`. Add that directory to `PATH`. Run the same
script after changing or pulling sources to update it. It does not run tests.
An installation can be relocated without any resource bundle or repository;
the GES/GESA syntax grammars are embedded in the executable. The platform's Swift
runtime libraries are still required where they are not supplied by the OS.

For an isolated installation, including paths with spaces:

```sh
./scripts/install-swift-tool.sh --tool-path ./artifacts/swift/tool/install
./artifacts/swift/tool/install/ges --help
./scripts/uninstall-swift-tool.sh --tool-path ./artifacts/swift/tool/install
```

Relative paths use the caller's working directory. With no options the uninstall
script removes the current user's default installation. An absent installation
is a successful no-op. The scripts retain other tools and the installation
directory. A SHA-256 ownership marker protects an unrelated or modified `ges`
from replacement or removal; the Apache-2.0 license is installed alongside it.
If an older C# tool still owns `ges`, update it with
`./scripts/install-csharp-tool.sh` first, or select a different Swift tool path.
The current tools coexist as `ges` (Swift) and `dotnet ges` (C#).

Without installing:

```sh
swift run --package-path implementation/swift/GameEventScriptTool \
  --scratch-path artifacts/swift/tool --configuration release ges --help
```

## Commands

The [CLI command contract](../../csharp/tools/GameEventScript.Tool/README.md)
applies to both implementations. Substitute `ges` for `dotnet ges` in its
examples. Diagnostic wording and compiler instruction counts can differ;
stable diagnostic codes, exit statuses and script behavior have the same meaning.

```sh
ges compile game.ges
ges compile handlers.ges definitions.ges -o build/game.gesb --verbose
ges check "scripts/*.ges"
ges dump build/game.gesb --addresses
ges run game.ges --args 12 Hello 34
ges run first.gesb second.gesb -- 12 Hello --color
ges run game.ges --scenario scenario.ges --seed 42
ges run --interactive --color
```

`compile` and `check` compile all supplied source documents together, preserving
input order. Filename wildcards expand in ordinal order and duplicate paths are
included once. `check` writes no binary and executes no handlers. `compile`
includes debug symbols, source map and source archive unless `--no-debug` is used.
Multiple source files require an explicit output path. Outputs are replaced only
after successful compilation or decoding; output directories are created as needed.

`run` accepts source files or independently loaded `.gesb` files, including files
produced by the C# compiler. It loads every initial Program, calls `start()` for the complete group, then drains
its queued messages before sending `Main(args)`. A failed initial Start discards
queued startup outputs and prevents Main. All arguments from `--arg`, `--args` and
`--` are Text values in one List. `--args` ends at the next option; everything
following `--` is an argument. Missing Main is an error, not an implicit REPL.
`--scenario` replaces Main with a separately compiled scenario. `--interactive`
replaces it with an event console; these modes accept no Main arguments.

The native handlers are `ConsoleOut(...)`, `ConsoleErr(...)` and `ErrorCode(code)`.
Console values concatenate without separators and end with a newline. ErrorCode
accepts 0–255 or `nothing` to reset to zero; the last delivered code wins without
stopping execution. Runtime/CLI failures override it with 1; invalid command-line
usage returns 2. Diagnostics, event traces and run summaries go to stderr.
Compilation/check reports and ordinary dumps go to stdout.

## Event console

Use `:help`, `:help load` and `:help dump` for details. The console supports
`:load <file>`, `:list`, `:handler`, `:dump <module|@ID>`, `:source <module|@ID>`
and `:quit`. Successful loads retain their handlers and receive stable session
IDs. Each ordinary input executes in a temporary initialization handler and is
then detached; local variables/functions do not persist. Use `:load` for
persistent handler declarations. Recoverable compile/link/load errors leave the
session usable but set its final exit status to 1. Runtime failures end it.

With `--color` and terminal input/output/error streams, the editor offers:

- Live GES syntax coloring; GESA and embedded sources also receive colors.
- Four-column tabs, cursor editing, Up/Down history, Home/End and Ctrl+Left/Right.
- Enter to submit and Ctrl+N for multiline input. Shift/Alt/Option+Enter work
  when the terminal reports modifiers, including Ghostty's extended sequences
  and Warp's Option-as-Meta setting.
- Bracketed paste without submitting pasted newlines, Ctrl+C to cancel the
  current input, and Ctrl+D on empty input to exit.

`NO_COLOR` disables colors and live editing. Redirected input uses strict UTF-8
line reading without prompts, cursor escapes or editing. ConsoleOut preserves
Text and colors numeric values blue; ConsoleErr is red. Verbose interactive
traces are yellow. Terminal settings are restored before execution, on normal
exit, and on termination signals handled by the terminal adapter.

## Verification

```sh
./scripts/test-swift-tool.sh
```

This runs the grammar drift check, native adapter tests and process/pseudoterminal
checks. Tests cover command parsing, atomic files, UTF-8, argument order,
console channels, error codes, additive loading, inspection, multiline editing,
Unicode, history, cancellation, terminal restoration and broken output pipes.
All artifacts remain under `artifacts/swift`. The complete `test-swift.sh` also
runs this gate, then the existing shared Markdown Conformance and interoperability
suites. Portable language behavior continues to use that shared corpus.

Optional direct C#/Swift CLI interoperability verification:

```sh
python3 scripts/test-swift-tool.py artifacts/swift/tool/release/ges \
  --reference artifacts/csharp/tool/bin/Release/net8.0/GameEventScript.Tool.dll
```

The TextMate grammar source remains under `tools/editors/TextMate`. After editing
it, refresh the embedded Swift constants explicitly with
`python3 scripts/sync-swift-cli-grammars.py`; normal Swift builds need no generator.
The Xcode workspace includes this package and a shared `GameEventScriptTool` test
scheme. The CLI exposes no additional portable library API.
