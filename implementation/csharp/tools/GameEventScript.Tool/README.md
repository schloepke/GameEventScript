<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Game Event Script CLI

`GameEventScript.Tool` is a separate .NET console application packaged as
a .NET tool with command name `dotnet-ges`, invoked as `dotnet ges`. It targets
.NET 8 and references the portable Runtime, Compiler, and C# bridge. Terminal and filesystem integration
belong to this application or the C# adapters; the libraries do not depend on
the tool.

The entry point supports `dotnet ges compile`, `dotnet ges check`,
`dotnet ges run`, `dotnet ges dump`, `dotnet ges --help`, `dotnet ges -h`, and
`dotnet ges --version`. `run --interactive` opens an event console. No arguments
display help. The provisional tool package is not published.

## Compile scripts

```sh
dotnet ges compile game.ges
dotnet ges compile game.ges -o artifacts/game.gesb
dotnet ges compile game.ges --output artifacts/game.gesb --no-debug
dotnet ges compile handlers.ges definitions.ges -o artifacts/game.gesb
dotnet ges compile "scripts/*.ges" -o artifacts/game.gesb --verbose
dotnet ges compile game.ges --quiet
dotnet ges compile --help
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
| `1` | Compilation, decoding, linking, runtime, runtime-limit, or file I/O failure |
| `2` | Invalid command or arguments |
| `0`–`255` | For a completed `run`, the script-selected ErrorCode (default `0`), unless a CLI failure overrides it |

## Check sources

```sh
dotnet ges check game.ges
dotnet ges check handlers.ges definitions.ges
dotnet ges check "scripts/*.ges" --verbose
dotnet ges check game.ges --quiet
dotnet ges check --help
```

`check` runs the same complete compiler pipeline as `compile`, including semantic
validation, optimization, and Program validation. It does not encode or write a
`.gesb` file, load a host, or execute initialization or other handlers. Existing
files remain untouched. This command accepts source text, not `.gesb` input.

Source ordering, wildcard expansion, deduplication, strict UTF-8 decoding, source
names, and compiler diagnostics follow `compile`. Multiple sources do not need an
output path. `--output` and `--no-debug` are not check options. The default success
report lists sources, module, and elapsed time; `--verbose` adds the same Program
details as `compile`, and `--quiet` suppresses success output.

Checking verifies the source and Program contract; it does not establish that a
particular host can satisfy extension imports or runtime resource requirements.
Like `compile`, it has no configurable external-type catalog. Use `run` to also
perform linking and execution in the CLI's host.

## Run a program

```sh
dotnet ges run game.ges
dotnet ges run handlers.ges definitions.ges
dotnet ges run game.ges --arg hello --arg world --seed 42
dotnet ges run --color game.ges --args 12 Hello 34
dotnet ges run game.ges --args 12 Hello 34 --color
dotnet ges run --color game.ges -- 12 Hello 34
dotnet ges run script1.gesb script2.gesb
dotnet ges run "programs/*.gesb" --arg hello
dotnet ges run game.ges --scenario scenario.ges --seed 42
dotnet ges run script1.gesb script2.gesb --scenario scenario.ges
dotnet ges run --interactive --color
dotnet ges run game.ges --interactive --color
dotnet ges run script1.gesb script2.gesb --interactive --verbose
dotnet ges run game.ges --max-messages 1000 --max-steps 200000
dotnet ges run --help
```

`run` compiles one or more source files together in memory, or reads and validates
one or more `.gesb` files. Each binary is loaded as an independent Program into
one shared host in input order. The `.gesb` extension is recognized without regard
to case; other input paths are treated as source text. Mixing binary and source
inputs is a usage error. File-name wildcard expansion, ordering, and path
deduplication follow `compile`, including for binaries. No binary is written and
input files are not modified.

All inputs are compiled/decoded and linked before any script runs. Every Program
is loaded at normal priority; input order defines registration order and queued
initialization order. Programs can therefore send messages to handlers from later
inputs, including during initialization. Constants and functions are scoped to
their compiled Program; binary composition does not merge compilation scopes.

### Main and console endpoints

By default, the CLI calls Host.Start for the complete initial group, then drains
its queued ordinary messages and sends exactly one untagged `Main(args)` message.
An initial failure prevents Main and discards that group's queued outputs. All matching
handlers in all loaded Programs receive that same logical message, in the host's
normal dispatch order. A helper Program need not declare Main. At least one
handler must match: an exact `Main(args)` handler or a message-name Main handler,
without required tags. Otherwise `cli.missingMain` is reported before any
initialization executes. An `undeliverable` handler does not count as an entry
point. No interactive fallback is started implicitly.

`args` is always a List of Text values, in command-line order. No implicit literal
conversion is applied: `12`, `true`, and `[1, 2]` remain Text. Use `parse arg` or
`arg as :Number` in the script when conversion is needed. Without arguments the
List is empty. The three forms may be combined:

| Form | Behavior |
| --- | --- |
| `--arg <text>` | Append exactly one Text value, even if it looks like an option; repeat as needed |
| `--args <text> ...` | Append one or more Text values until the next option, then resume ordinary CLI parsing |
| `-- <text> ...` | Append all remaining tokens as Text values, without any further option or file interpretation |

`--args` accepts negative values such as `-12`, `-.5`, and `-1e3` (without converting
them), and a lone `-`. Other tokens beginning with a dash start option parsing;
unknown options such as `--colro` are errors. Use `--arg --color` or `-- --color`
to pass an option-looking value literally. After an option ends an `--args` group,
use another `--args` to begin a new group. Empty strings are valid arguments when
supplied as a shell argument (`""` or `''`); a bare `--args` with no values is an error.
A trailing `--` with no values is allowed for Main.

For `run`, `--` introduces Main arguments rather than program paths. Prefix a
program path beginning with a dash with `./`, for example `./-game.ges`.
All argument forms are available only in Main mode, not with `--scenario` or
`--interactive`. `--help`/`-h` is also accepted among run options, but is forwarded
as data after `--` or as the value of `--arg`.

The CLI subscribes three native handlers before loading any Program:

| Message | CLI behavior |
| --- | --- |
| `ConsoleOut(...)` | Concatenate all argument values in order without separators, followed by a newline, to stdout |
| `ConsoleErr(...)` | Write the same representation to stderr |
| `ErrorCode(code)` | Set the script's final process exit code; does not terminate execution |

For example:

```ges
on Main(args) {
    emit ConsoleOut("Arguments: ", args)
    emit ConsoleOut("Hello ", 12)
    emit ConsoleErr("Problem found: ", #invalid)
    emit ErrorCode(code: 7)
}
```

```sh
dotnet ges run game.ges --arg hello --arg world > results.txt
```

The file receives the ConsoleOut lines; ConsoleErr remains on stderr. Both console
endpoints accept any number and kind of arguments. Argument labels are allowed
and ignored for display; their values retain their order. With no arguments the
endpoint writes an empty line. Neither endpoint inserts spaces or other separators
between arguments; include these explicitly in Text values. For example,
`ConsoleOut("f(", 12, ") = ", 24)` prints `f(12) = 24` followed by a newline.
Root Text values remain unquoted; other values use
the existing GES text representation. Text may contain newlines. This is
human-readable output, not a framed serialization format.

`ErrorCode(7)` and `ErrorCode(code: 7)` are equivalent. Exactly one argument is
required, unlabeled or labeled `code`. Its value must be a unitless Number with
an integer value from `0` to `255`, or `nothing`. The default code is `0` (success);
nonzero codes indicate script-defined failures. `nothing` resets it to `0`.
Invalid arguments report `cli.errorCodeArgument` and make the run fail.
The last **delivered** ErrorCode message wins, across all Programs and inputs.
Setting a code does not cancel handlers, stop the queue, or exit an interactive
session. Compile, decode, link, runtime, runtime-limit, interactive-input, and
I/O failures always produce CLI failure code `1`, regardless of the script code.
CLI usage errors still return `2`. Codes `1` and `2` selected by a script are also
valid, so diagnostics distinguish them from CLI failures.

These are ordinary message handlers, not new language syntax or reserved language
names. They are available during initialization, Main, scenarios, and interactive
input. FIFO delivery applies; publishing also reaches each native handler exactly
once. ConsoleErr alone does not change the exit code, and tags do not implicitly
set it. Use separate ConsoleErr and ErrorCode messages when reporting a failure.

### Color output and editing

`--color` explicitly enables ANSI colors, including when output is redirected.
Without it, output contains no CLI-added ANSI codes. An existing `NO_COLOR`
environment variable disables colors even with this option.

ConsoleOut leaves root Text untouched and colors Number/Percentage/Quantity
values blue. Structured values receive syntax colors (for example, nested strings
green and tags cyan). ConsoleErr colors its entire line red. In interactive mode,
`--verbose` event traces (emit, publish, and dispatch) appear yellow with `--color`.
Outside interactive mode, traces remain plain. Error diagnostics and status
messages retain their plain representation.

With `--interactive --color`, when stdin, stdout, and stderr are terminals, the
line editor highlights input as it is typed. It supports cursor editing, session
history with Up/Down, and Ctrl+N to insert a newline; Enter submits the entire input.
The editor uses a tab width of four columns.
Shift+Enter and Alt/Option+Enter also insert a newline if the terminal reports
the modifier. Ghostty's default Shift+Enter encoding is recognized without a
custom keybinding. The editor decodes both the extended xterm and CSI-u forms
of Shift/Alt+Enter. In Warp, configure Option as Meta to use Option+Enter.
Some terminals send the same input for modified Enter as for ordinary Enter,
which submits immediately. Use Ctrl+N in those terminals.
Ctrl+C cancels the current input, and Ctrl+D on empty input exits. Prompts and
rendered input use stderr. Redirected streams fall back to the normal line reader;
script output can still be colored with `--color`.

The CLI-only editor uses [PrettyPrompt](https://github.com/waf/PrettyPrompt/tree/v4.1.1).
Highlighting uses the bundled GES and GESA TextMate grammars and tolerates incomplete input;
compilation remains the authority for valid GES. Runtime and Compiler acquire no
terminal dependencies. Dependency licenses and source links are included in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

The process finishes when initialization and Main processing complete or a runtime
boundary stops execution. `--scenario` and `--interactive` replace Main; they are
mutually exclusive and cannot be combined with Main argument options.

### Trigger events with a scenario

For example, `game.ges` can contain:

```ges
module game

on Start(value) {
    emit ConsoleOut(value: value + 1)
}
```

Create `scenario.ges` using ordinary GES syntax:

```ges
module scenario

on initialization {
    emit Start(value: 41)
}
```

Run them with:

```sh
dotnet ges run game.ges --scenario scenario.ges --seed 42
```

Stdout contains:

```text
42
```

A completion summary is written to stderr. Use `--quiet` / `-q` to omit that
summary while retaining ConsoleOut, ConsoleErr, and errors. `--verbose` / `-v`
additionally traces emits, publishes, and handler entry on stderr. Quiet and
verbose cannot be combined.

The scenario is a separate Program, so it may use its own module, constants, and
functions. Those definitions are not shared with the main compilation; Programs
communicate through messages. Repeat `--scenario` to compile multiple scenario
source files together into that one Program; file-name patterns and deduplication
also apply within the scenario group. A scenario must be source text.

The scenario is loaded after all input Programs, before execution starts. Its
initialization is queued after theirs; later messages use the normal FIFO host
queue and handler registration order. Scenario handlers can therefore observe
messages emitted by any input Program's initialization. Several emits inside a scenario
handler enqueue several messages; they do not synchronously finish each message
before the next emit. Scenarios can send subsequent events from response handlers
when that sequencing is needed. Scenario events are ordinary GES emits, not
separate native `Host.Receive` calls. No automatic Main message is sent in this mode.

### Interactive event console

```sh
dotnet ges run --interactive --color
dotnet ges run game.ges --interactive --color
dotnet ges run script1.gesb script2.gesb --interactive --seed 42
```

Initial program files are optional in interactive mode. A session started with
`dotnet ges run --interactive` can load them later with `:load`.

After initialization drains, the CLI reads one line of ordinary GES handler-body
statements at a time. For example:

```text
ges> :help
ges> :load "programs/game.gesb"
ges> emit Start(value: 41)
42
ges> emit ConsoleOut(value: [1, 2, 3])
[1, 2, 3]
ges> :quit
```

Each line is compiled with the existing compiler inside a temporary
`on initialization` handler, loaded into the same host, and pumped before the next
line is read. The temporary Program is then detached. The main Programs and random
generator persist; local variables and declarations from an input do not. Plain or redirected
input must fit on one line, optionally using braces and semicolons. The colored
terminal editor also accepts multiline input using Ctrl+N between lines and Enter
to submit the complete input. For example, type `if true {`, press Ctrl+N, type
`emit ConsoleOut("Hello")`, press Ctrl+N, type `}`, then press Enter. Additional
handler declarations outside the generated initialization handler are rejected.
Use `emit ConsoleOut(expression)` to display an expression result; a bare
expression retains ordinary GES expression-statement semantics.

Interactive commands are:

| Command | Behavior |
| --- | --- |
| `:help` | Show commands, message examples, Text conversion, editing keys, and session behavior |
| `:help load` | Show loading details and path examples |
| `:help list`, `:help handler`, `:help dump`, `:help source` | Show inspection behavior and program selection details |
| `:load <file>` | Add one `.ges` or `.gesb` Program and immediately pump its initialization |
| `:list` | List persistent program instances with their `@ID`, module, version, files, and registered script-handler count |
| `:handler` | List registered script and native handlers, their argument signatures, and required/excluded tags |
| `:dump <module>` | Show the GESA dump of the loaded Program with that exact module name |
| `:dump @<ID>` | Select a Program by session ID, including anonymous or duplicate modules |
| `:source <module\|@ID>` | Show only embedded source documents, each with a filename heading |
| `:quit` | End the session; EOF also exits |

Help, prompts, and load reports go to stderr. Prompts and the introductory message
appear only when stdin is a terminal; redirected UTF-8 input is accepted without
prompts, including an optional initial BOM. `--quiet` suppresses load reports and
the final summary, while explicitly requested help remains visible.

Inspection output also goes to stderr and remains visible with `--quiet`.
Help, lists, dumps, and source displays have a blank line before and after their output.
For example:

```text
ges> :list

Loaded programs (1):
  @1  game.combat  version=0  handlers=2
      files: "game.gesb"

ges> :handler

Registered handlers (5):
  native  ConsoleOut(...) [name-only]
  native  ConsoleErr(...) [name-only]
  native  ErrorCode(...) [name-only]
  @1  game.combat  Main(args) [signature]
  @1  game.combat  Hit(damage) [signature] matching #enemy without #silent

ges> :dump game.combat
ges> :dump @1
ges> :source game.combat
ges> :source @1
```

IDs are stable within a session and assigned in successful load order. Jointly
compiled source files form one Program; each binary and each `:load` creates its
own entry. Repeated loads are shown separately. Sources without a module
declaration retain their compiler-generated `anonymous.*` module name and can
also be selected by ID. A duplicate module name makes `:dump` or `:source`
report the matching IDs instead of choosing one. Unknown modules and invalid
arguments reject the command, allow further input, and make the final exit code 1.

`:handler` lists the native console subscriptions first, followed by script
handlers in program-load and binding order. `[signature]` matches the displayed
ordered argument labels, including `_` for unlabeled arguments; `(...) [name-only]`
accepts any argument signature. The list excludes initialization handlers, which
already ran once and are not registered for later messages, as well as functions,
predicates, outbound message bindings, and detached temporary console inputs.

These commands inspect the loaded session without executing handlers, pumping
messages, or consuming random values. `:dump` uses the same GESA dumper as
`dotnet ges dump` on the Program already in memory; it does not reload the file or need
the original sources. Embedded source/debug information is included when present.

`:source` reads only the loaded Program's source archive, in archive order. It shows
the original source text, including comments, with a `// Source: "filename"` heading
for each document. It does not show bytecode, bindings, or other dump segments.
Without an embedded archive (for example, a binary compiled with `--no-debug`),
it prints a notice and leaves the session successful. It never re-reads files or
reconstructs source from bytecode.

With `--color`, `:source` highlights GES, and `:dump` highlights GESA directives,
opcodes, registers, and operands while using GES colors for embedded source blocks
and source lines. Coloring also works with redirected output; `NO_COLOR` disables
it. Terminal source/dump displays expand tabs to four-column stops before adding
colors. Redirected output preserves the original tab characters; otherwise the
displayed text is unchanged apart from ANSI color codes.

`:load` uses the entire remainder of its line as one file path, relative to the
process working directory. Optional surrounding single/double quotes are removed;
double that surrounding quote to represent it within the path. Backslashes are
literal, so Windows paths do not require escape sequences. There is no shell
expansion, wildcard expansion, or multiple-file form. To load combined source
files, compile them together first and load their resulting `.gesb`.

Sources use the same compiler and strict UTF-8 decoding as `run`; binaries use
the complete Reader/validator. A load is additive: its handlers stay active for
subsequent inputs, previously loaded Programs remain available, and loading the
same file again adds another independent instance. Functions and constants remain
scoped to their compiled Program. Initialization may send messages to the existing
Programs, but `Main` is not automatically invoked. To invoke it explicitly:

```ges
emit Main(args: ["12", "Hello"])
```

A read, compile, decode, or link failure rejects the new load before its handlers
are registered or initialization runs. Existing Programs remain usable. Invalid
commands, syntax/link errors, and failed loads allow another input but make the
session's final exit code `1`. A runtime error, output failure, or runtime limit,
including during loaded initialization, ends the session immediately after the
pump returns; pending work is not resumed. No automatic replacement or unload is
performed by `:load`.

### Output, limits, and host configuration

Only ConsoleOut values go to stdout. General event traces are shown on stderr with
`--verbose`, retaining argument order and tags. `[not queued]` indicates an emit
or publish had no local enqueue; that alone is not an error. No outbound sink is
configured, so publications only reach local subscribers. Nothing is sent to an
external service. Root Text arguments in verbose traces use JSON quoting/escaping;
other values use the existing GES text representation. Trace rendering is distinct
from ConsoleOut and ConsoleErr output and defines no portable serialization format.

`--seed` accepts a signed decimal Int64. The same seed and programs reproduce the
host's random sequence. Without it, the normal host seed generation applies.
All Programs, the scenario, and interactive inputs share this host's random generator.

Runtime limits remain active. `--max-messages` sets `MaxProcessedEventsPerRun`
(default 64) per pump, including initialization and native console messages.
The initial Start phase, its subsequent ordinary-message drain, Main, and each
interactive input are separate pumps; their message-processing limits reset
independently. A scenario joins the initial Start group after the input Programs.
The run summary includes work from both Start and ordinary pumping. `--max-steps` sets
`MaxExecutionSteps` per handler (default 100,000). Both options require positive
Int32 values; all other limits retain their Runtime defaults. Reaching a limit
reports `cli.runtimeLimit` with the limit name and value and exits with code `1`.
The CLI does not automatically resume past a safety boundary or drain a cyclic
message chain indefinitely. Already printed events are not rolled back.

Only the three native CLI handlers are registered; no custom extensions or
external types are registered. Programs requiring unavailable imports fail during
linking with the existing `link.*` diagnostic. Diagnostics go to stderr with stable
codes and available source, Program, handler, symbol, or binary context. A runtime
diagnostic makes the process fail even if the host subsequently becomes idle;
normal host behavior may still execute sibling handlers before returning.

## Dump a binary

```sh
dotnet ges dump artifacts/game.gesb
dotnet ges dump artifacts/game.gesb --addresses
dotnet ges dump artifacts/game.gesb -o artifacts/game.gesa
dotnet ges dump artifacts/game.gesb > artifacts/game.gesa
dotnet ges dump --help
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
When stdout is a terminal, tabs are displayed at four-column stops. Files and
redirected stdout retain the original tab characters.

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
dotnet ges --help
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
package. Subsequent invocations update the new package normally.

The package installs the command `dotnet-ges`, which the .NET CLI resolves as
`dotnet ges`. Updating an existing `GameEventScript.Tool` installation replaces
its old `ges` command automatically; no manual uninstall is needed. The package
ID stays `GameEventScript.Tool`. Direct invocation as `dotnet-ges` also works.

The script can also be invoked by its absolute path from any working directory.
To install or update in a separate directory instead of globally:

```sh
./scripts/install-csharp-tool.sh --tool-path ./artifacts/csharp/tool/install
./artifacts/csharp/tool/install/dotnet-ges --version
```

Relative installation paths are resolved against the caller's working directory.
For global use, ensure that `$HOME/.dotnet/tools` is on `PATH`. With
`--tool-path`, add that directory to `PATH` to use `dotnet ges`, or invoke
`dotnet-ges` by its path as shown above.

## Uninstall from the repository

Remove the global C# `dotnet ges` installation for the current user:

```sh
./scripts/uninstall-csharp-tool.sh
```

For an installation made with `--tool-path`, select the same directory:

```sh
./scripts/uninstall-csharp-tool.sh --tool-path ./artifacts/csharp/tool/install
```

Relative paths are resolved against the caller's working directory, exactly as
with the installer. The script removes `GameEventScript.Tool` and any remaining
legacy `StepH.GameEventScript.Tool` from the selected scope. Other tools and the
installation directory remain intact. An absent GES installation is a successful
no-op; lookup or uninstall errors still fail. No build, package creation, or
network download is required.

## Pack and install manually

```sh
dotnet pack implementation/csharp/tools/GameEventScript.Tool --configuration Release
dotnet tool install GameEventScript.Tool --version 0.1.0 --add-source ./artifacts/csharp/tool/packages --tool-path ./artifacts/csharp/tool/install
./artifacts/csharp/tool/install/dotnet-ges --help
```

On Windows the installed command is `dotnet-ges.exe`. Build intermediates, binaries,
and packages for this tool are written below `artifacts/csharp/tool`. This local
tool package is separate from the four libraries produced by the existing
library release and reproducibility scripts.

For project-local use, install into a .NET tool manifest instead of supplying
`--tool-path`. The invocation is then `dotnet ges --help` or
`dotnet tool run dotnet-ges --help`. Global installations also support
`dotnet ges --help` when the tool directory is on `PATH`.

The tool is licensed under Apache-2.0; the package includes the full license.
