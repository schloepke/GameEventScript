// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

enum ToolHelp {
    static let overview = #"""
        Game Event Script CLI

        Usage:
          ges [--help | --version]
          ges compile <source.ges> [more.ges ...] [-o <output.gesb>] [--no-debug] [-v | -q]
          ges check <source.ges> [more.ges ...] [-v | -q]
          ges run <source.ges> [more.ges ...] [--scenario <scenario.ges>] [--seed <integer>]
          ges run <program.gesb> [more.gesb ...] [--args <text> ...] [--seed <integer>]
          ges run <program files ...> [--color] -- <text> ...
          ges run --interactive [--color] [program files ...]
          ges dump <program.gesb> [-o <output.gesa>] [--addresses]

        Commands:
          compile     Compile UTF-8 source files together to .gesb. Use 'ges compile --help' for options.
          check       Validate source files without writing a binary. Use 'ges check --help' for options.
          run         Run Main(args), an event scenario, or the event console. Use 'ges run --help' for options.
          dump        Dump a .gesb file as GESA text. Use 'ges dump --help' for options.

        Options:
          -h, --help  Show this help.
          --version   Show the tool version.
        """#
    static let compile = #"""
        Usage:
          ges compile <source.ges> [more.ges ...] [-o <output.gesb>] [--no-debug] [-v | -q]

        Options:
          -o, --output <path>  Output file. Required for multiple sources; otherwise defaults to source.gesb.
          --no-debug          Omit debug symbols, source map, and source archive.
          -v, --verbose       Include bindings, dependencies, bytecode size, and resource requirements.
          -q, --quiet         Suppress successful compilation output. Errors are still reported.
          -h, --help          Show this help.
          --                  Treat remaining arguments as file names.

        Sources are compiled together in argument order. File-name patterns such as
        "scripts/*.ges" expand in ordinal order; directory wildcards and recursion are not supported.
        Repeated paths are included once, keeping their first position.
        Output directories are created as needed. Existing output is replaced after
        successful compilation. Source files must be UTF-8, with an optional UTF-8 BOM.
        """#
    static let check = #"""
        Usage:
          ges check <source.ges> [more.ges ...] [-v | -q]

        Options:
          -v, --verbose       Include bindings, dependencies, bytecode size, and resource requirements.
          -q, --quiet         Suppress success output. Errors are still reported.
          -h, --help          Show this help.
          --                  Treat remaining arguments as file names.

        Sources are compiled and validated together without writing a binary or executing
        handlers. Source order, UTF-8 decoding, and file-name patterns match 'ges compile'.
        Checking does not resolve host extensions or external types against a runtime registry.
        """#
    static let dump = #"""
        Usage:
          ges dump <program.gesb> [-o <output.gesa>] [--addresses]

        Options:
          -o, --output <path>  Write to a UTF-8 file instead of stdout.
          --addresses         Include zero-based instruction addresses.
          -h, --help          Show this help.
          --                  Treat remaining arguments as file names.

        The binary is validated before producing GESA text. Embedded debug information
        is used when available; source files and host bindings are not required.
        Output directories are created as needed. An existing output file is replaced
        only after successful decoding and dump generation.
        Terminal output uses four-column tab stops; files and redirected output retain tabs.
        """#
    static let run = #"""
        Run Game Event Script programs

        Usage:
          ges run [options] <source.ges> [more.ges ...] [-- <text> ...]
          ges run [options] <program.gesb> [more.gesb ...] [-- <text> ...]
          ges run --interactive [--color] [program files ...]

        Execution mode:
          (default)              Finish initialization, then send Main(args) once.
          --scenario <file.ges>  Run a separate scenario instead of Main. Repeat for combined sources.
          --interactive         Open the event console instead of Main. Initial files are optional.

        Main arguments (always Text, in command-line order):
          --arg <text>          Append exactly one value, even if it looks like an option.
          --args <text> ...     Append values until the next option. Negative numbers are values.
          -- <text> ...         Append everything remaining, including option-looking values.
                                No implicit parsing. Use 'parse arg' in the script if needed.

        Output and execution:
          --color               Enable ANSI colors and live input highlighting (unless NO_COLOR is set).
          -v, --verbose         Trace emit, publish, and dispatch on stderr; yellow in colored interactive mode.
          -q, --quiet           Hide completion/load/unload/reload reports; console output and errors remain visible.
          --seed <integer>      Signed 64-bit random seed. Omit for a fresh host seed.
          --max-messages <n>    Maximum processed messages per pump/input (default: 64).
          --max-steps <n>       Maximum execution steps per handler (default: 100000).
          -h, --help            Show this help.

        Examples:
          ges run --color example.ges --args 12 'Hello' 34
          ges run first.gesb second.gesb -- 12 Hello --color
          ges run example.ges --arg hello --arg world
          ges run --interactive --color
          ges run game.ges --scenario scenario.ges --seed 42

        Event console:
          :help                 Show commands, examples, and session behavior.
          :help load            Explain loading a program into the current session.
          :load "extra.gesb"     Add one source or binary file and run its initialization.
          :unload <module|@ID>  Detach one loaded program.
          :unloadAll            Detach all programs; keep native console handlers.
          :reload               Re-read active programs on a fresh host. Use :help reload for details.
          :list                 List loaded programs/modules and their @IDs.
          :handler              List registered script and native handlers.
          :dump <module|@ID>     Show a loaded program as GESA. Use :help dump for details.
          :source <module|@ID>   Show only embedded sources. --color highlights source and dumps.
          :quit                 End the session (or use EOF).

        ConsoleOut(...) writes to stdout; ConsoleErr(...) writes to stderr.
        ErrorCode(code: 0..255) sets the script exit code; nothing resets it to 0.
        All initial Programs load before execution. Sources compile together; binaries
        load independently in input order. Sources and binaries cannot be mixed in one run.
        --scenario and --interactive are mutually exclusive and accept no Main arguments.
        Use './-file.ges' for a program path starting with '-'. No custom extensions or
        external types are registered. Runtime errors/limits exit with 1; usage errors with 2.
        """#
    static let console = #"""
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
          Enter submits. Plain/redirected input accepts one line (braces/semicolons allowed).
          With --color and terminal streams: live syntax colors, cursor editing, Up/Down
          history, Ctrl+N to insert a newline, Ctrl+C to cancel, Ctrl+D on empty input to quit.
          Shift+Enter and Alt/Option+Enter also insert a newline when the terminal reports
          their modifiers. Ghostty's default Shift+Enter is supported without remapping.
          In Warp, configure Option as Meta to use Option+Enter. If a terminal sends
          ordinary Enter for a modified key, use Ctrl+N instead.
          --verbose shows emit/publish/dispatch traces; interactive color traces are yellow.
          --quiet hides completion/load/unload/reload reports. Help, prompts, and diagnostics use stderr.
        """#
    static let lifecycle = #"""
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
        """#
    static let load = #"""
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
        Compile related sources together with 'ges compile' and load the resulting binary.

        Loading is additive: loading the same file again creates another active instance.
        It does not replace the previous instance or share its functions/constants.
        A read/compile/decode/link failure leaves the existing session usable. Runtime
        errors or limits during initialization end the session. Rejected inputs/load
        commands make the final exit code 1, even if later commands succeed.
        """#
    static let inspection = #"""
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
        """#
}
