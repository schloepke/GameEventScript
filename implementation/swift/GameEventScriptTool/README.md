<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Native Swift CLI

Install the published CLI on macOS 15+ or Linux (ARM64/x64) through Homebrew:

```sh
brew install schloepke/gameeventscript/ges
```

See [CLI installation and downloads](../../../docs/guide/distribution/Tools.md)
for updates, uninstalling and direct release archives. No Swift SDK is needed
for these installations. The build instructions below use a repository checkout.

`GameEventScriptTool` is a separate SwiftPM executable package. It installs `ges`
and depends on the local Runtime and Compiler packages, Foundation, and a small
POSIX terminal adapter. It has no .NET or third-party package dependency.
Runtime and Compiler remain independent of terminal and filesystem services.

## Build, install and update

Use Swift 6.0 or newer on macOS 10.15.4 or newer, or on Linux. The CLI requires
macOS 10.15.4 for Foundation's throwing file-handle I/O APIs; this minimum applies
only to the executable package. macOS is verified by the native tests
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

The [CLI command contract](../../csharp/GameEventScript.Tool/README.md)
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

Use `:help`, `:help load`, `:help reload` and `:help dump` for details. The console supports
`:load <file>`, `:list`, `:handler`, `:dump <module|ID>`, `:source <module|ID>`
as well as `:unload <module|ID>`, `:unloadAll`, `:reload` and `:quit`. Successful loads retain their handlers and receive stable session
IDs. Each ordinary input executes in a temporary initialization handler and is
then detached; local variables/functions do not persist. Use `:load` for
persistent handler declarations. Recoverable compile/link/load errors leave the
session usable but set its final exit status to 1. Runtime failures end it.

`:unload` detaches one Program; `:unloadAll` detaches every Program. Native console
handlers, random state and script exit code remain. `:reload` re-reads active
Programs from their original files on a fresh host, preserving source groups,
load order and active IDs. It runs initialization again without calling Main,
restarts a configured seed and resets the script exit code. Preparation failures
preserve the old session; runtime failures end it. IDs are never reused. Use
`:unloadAll` followed by `:reload` for an empty fresh host. The full contract is in
the [shared CLI documentation](../../csharp/GameEventScript.Tool/README.md#unloading-and-reloading-programs).

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
`python3 scripts/sync-highlighter-grammars.py`; normal Swift builds need no generator.
The Xcode workspace includes this executable package and a shared
`GameEventScriptTool` scheme. Select **My Mac** and use **Cmd+B** to build,
**Cmd+R** to launch `ges`, or **Cmd+U** to run the native CLI tests. Run defaults
to `--help`; change arguments under **Product → Scheme → Edit Scheme → Run →
Arguments**, and configure a working directory under **Run → Options** when
using relative file paths. Tests do not inherit the CLI launch arguments.
Use an actual terminal for full interactive terminal editing; Xcode's debug
console is not a terminal replacement. The package is a build/test definition
for the executable, not a published library. The CLI exposes no additional
portable library API.

### Delayed messages

`run` waits for delayed messages and exits only when no work remains. Scripts
that keep scheduling messages can run until interrupted with Ctrl+C. Runtime
pumps themselves never sleep; the command-line adapter waits between pumps.

The event console returns its prompt after current runnable work. While waiting
for input it processes messages as they become due and restores the current
input and cursor after terminal output. Delayed sends use monotonic time, with
durations rounded upward to whole microseconds; actual dispatch also depends on
when the host can pump.

Program selectors accept a module name or a positive numeric session ID, such as
`:source 1`, `:dump 1` and `:unload 1`. The legacy `@1` spelling remains accepted
for compatibility. IDs remain stable across unloading and reloading; they are not
positions in the current list.

## External editing and scratch programs

Edit source drafts without changing original files

  :edit                 Create or reopen scratch 0 in the configured editor.
  :edit 1               Edit a loaded source program; binaries are read-only.
  :edit 1 2             Select source 2 of a jointly compiled program.
  :source [ID|module]   Show the current draft, or embedded source if not edited.
  :dump [ID|module]     Show the last successful compile, with a stale-draft notice.
  :save                 Save all modified file-backed drafts.
  :save 1               Save one program's modified source files.
  :save 0 "file.ges"    Save scratch as a normal program with a fresh positive ID.
  :quit                 Exit only if no unsaved changes remain.
  :quit!                Discard unsaved changes and exit.

:source and :dump without a selector refer to scratch 0. A missing scratch is an
error; :edit creates one. A saved scratch becomes a normal program; the next
:edit creates a new scratch. Module names remain independent of scratch IDs.
:edit reads the editor's temporary file, compiles the draft, then restarts the
host using current memory sources/programs. Initializations run again; queued
messages are discarded and a configured random seed restarts. Preparation errors
keep the running host; initialization/runtime errors end the session. External
effects cannot be rolled back. Main is not called automatically.

Failed drafts remain editable and saveable. :list marks unsaved drafts with *
and drafts not successfully applied with [draft not applied]. :reload preserves
unsaved editor drafts; clean file-backed programs are reread from disk. Save refuses external
file changes and existing scratch destinations. Unload refuses unsaved drafts.
EOF with unsaved drafts reports a failure for redirected input; terminal EOF
keeps the prompt open. Use :quit! to discard explicitly.

Editor selection uses GES_EDITOR, then VISUAL, then EDITOR (blank values are skipped).
Without configuration, the default is nano on macOS/Linux and notepad.exe on Windows.
For a GES-only setting, use GES_EDITOR='code --wait' or GES_EDITOR=nano. Command
arguments support quoted paths, without shell expansion. GUI editors must wait
until the file closes. Message pumping pauses while the editor is open. Only
:save writes original files. Errors are red with --color unless NO_COLOR is set.

Both interactive prompts indent continuation lines without adding `...` tokens.

The public root SwiftPM package also exposes `ges` from 0.3.0. See
[SwiftPM source builds and Homebrew installation](../../../docs/guide/distribution/Tools.md).
