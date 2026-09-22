// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

namespace GameEventScript.Tool;

internal static class RunConsoleHelp
{
    internal const string Inspection = """
Inspect the current session

  :list                 List loaded programs, module names, versions, files, and handler counts.
  :handler              List registered script and native handlers with signatures and tag filters.
  :dump <module>        Show the loaded program as GESA, using its exact module name.
  :dump @1              Select a program by its session ID from :list.
  :source <module|@ID>  Show only the program's embedded source files.

Each successful persistent load receives a stable @ID. Jointly compiled sources
are one program; each loaded binary or :load adds a separate program instance.
Sources without a module declaration use the compiler's generated anonymous.*
name; @ID is a convenient alternative. If a module name occurs more than once,
:dump and :source require an @ID instead of choosing an instance implicitly.

:source shows the original source text with a filename heading for each document.
If the program has no source archive, it reports that without failing the session.
Use --color for GES source colors and GESA dump colors, including embedded GES.
NO_COLOR disables both. No source text is reconstructed from bytecode.
Terminal displays use four-column tab stops; redirected output retains tabs.

:handler includes ConsoleOut, ConsoleErr, and ErrorCode. (...) [name-only] matches
any argument signature; [signature] matches the shown ordered argument labels.
Matching and excluded tags are shown. Completed initialization handlers, functions,
predicates, outbound message bindings, and temporary console inputs are not listed.

These commands write to stderr, even with --quiet, with a blank line before and
after their output. They do not execute handlers or re-read source/binary files.
An invalid command or missing/ambiguous module is recoverable but makes the final
session exit code 1, like other rejected console commands.
""";

    internal const string Lifecycle = """
Manage loaded programs

  :unload <module|@ID>  Detach one program, selected as with :dump.
  :unloadAll           Detach every program; keep native console handlers.
  :reload              Re-read active programs and initialize them on a fresh host.

Unloaded programs disappear from :list and :handler. Their IDs are never reused.
An ambiguous module name requires an @ID. Unloading preserves the host's random
state and script exit code. Use :unloadAll followed by :reload for an empty fresh host.

:reload reads the original files again, keeping load order, jointly compiled source
groups, and active @IDs. All programs are loaded before initialization is pumped.
Main is not called. Unloaded programs are not restored. The host uses the original
limits and seed options; a fixed --seed restarts its sequence. ErrorCode resets to
zero unless initialization sets it again. Native console handlers are registered once.

Read/compile/decode/link failures preserve the entire existing session. Runtime
errors or limits during initialization end the session. Rejected commands make the
final session exit code 1 even after a successful reload. :reload and :unloadAll
accept no arguments. --quiet hides successful load/unload/reload status reports.
""";

    internal const string Load = """
:load <file.ges|file.gesb>

Add one program to the current host and immediately run its initialization.
Previously loaded programs remain active. Main is not called automatically.
Source files compile in memory; binaries are validated and linked before execution.

Examples:
  :load extra.ges
  :load "programs/combat rules.gesb"
  emit Main(args: ["12", "Hello"])

Paths are relative to the process working directory. The entire remainder of the
line is one path; surrounding single/double quotes are optional. Backslashes are
literal; double a surrounding quote to include that quote in a quoted path.
Wildcards, shell expansion, and multiple paths are not supported by :load.
Compile related sources together with 'dotnet ges compile' and load the resulting binary.

Loading is additive: loading the same file again creates another active instance.
It does not replace the previous instance or share its functions/constants.
A read/compile/decode/link failure leaves the existing session usable. Runtime
errors or limits during initialization end the session. Rejected inputs/load
commands make the final exit code 1, even if later commands succeed.
""";

    internal const string Overview = """
GES event console

Commands:
  :help                 Show this help.
  :help load            Show load behavior and path examples.
  :help dump            Show program/handler inspection and source/dump details.
  :load <file>          Add one .ges or .gesb program; run its initialization.
  :unload <module|@ID>  Detach one loaded program.
  :unloadAll            Detach all programs; keep native console handlers.
  :reload               Re-read active programs on a fresh host. Use :help reload for details.
  :list                 List loaded programs/modules and their @IDs.
  :handler              List registered script and native handlers.
  :dump <module|@ID>    Show a loaded program as GESA, e.g. :dump game or :dump @1.
  :source <module|@ID>  Show only embedded source files; --color highlights source and dumps.
  :quit                 End the session. EOF also exits.

Send messages and inspect values:
  emit Start(value: 41)
  emit Main(args: ["12", "Hello"])
  emit ConsoleOut("Result: ", 1 + 2)
  emit after 0.2s ConsoleOut("Later")
  emit ConsoleErr("Problem found")
  emit ErrorCode(code: 7)
  emit ErrorCode(nothing)

Main(args) receives a List of Text from the CLI. Parse explicitly when needed:
  let value be parse "12"; emit ConsoleOut(value)
ConsoleOut writes to stdout, ConsoleErr to stderr. Both concatenate values without
separators and append a newline; include spaces in your Text values when needed.
ErrorCode selects the final process status (0..255); nothing resets it to 0.
These messages do not end a session.

Session behavior:
  Loaded program handlers and the host's random state persist between inputs.
  Each GES input has its own local scope; local variables/functions do not persist.
  Use :load for persistent handlers. Main is never invoked automatically here.
  Syntax/link/load errors reject the input and allow another command; final exit is 1.
  Runtime errors, output failures, and runtime limits end the session.

Editing:
  The editor and terminal source/dump displays use a tab width of four columns.
  Delayed output is pumped while waiting for input; unfinished input is preserved.
  Enter submits. Plain/redirected input accepts one line (braces/semicolons allowed).
  With --color and terminal streams: live syntax colors, cursor editing, Up/Down
  history, Ctrl+N to insert a newline, Ctrl+C to cancel, Ctrl+D on empty input to quit.
  Shift+Enter and Alt/Option+Enter also insert a newline when the terminal reports
  their modifiers. Ghostty's default Shift+Enter is supported without remapping.
  In Warp, configure Option as Meta to use Option+Enter. If a terminal sends
  ordinary Enter for a modified key, use Ctrl+N instead.
  --verbose shows emit/publish/dispatch traces; interactive color traces are yellow.
  --quiet hides completion/load/unload/reload reports. Help, prompts, and diagnostics use stderr.
""";

    internal static bool Write(string topic)
    {
        var help = topic switch
        {
            "" => Overview,
            "load" or ":load" => Load,
            "unload" or ":unload" or "unloadAll" or ":unloadAll" or "reload" or ":reload" => Lifecycle,
            "list" or ":list" or "handler" or ":handler" or "dump" or ":dump" or "source" or ":source" => Inspection,
            _ => null
        };
        if (help is null)
        {
            Console.Error.WriteLine($"error cli.consoleCommand: Unknown help topic '{topic}'. Use :help, :help load, or :help dump.");
            return false;
        }
        Console.Error.WriteLine();
        Console.Error.WriteLine(help);
        Console.Error.WriteLine();
        return true;
    }
}
