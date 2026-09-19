// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Tool;

internal static class RunCommand
{
    private const string HelpText = """
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
  -q, --quiet           Hide completion/load reports; console output and errors remain visible.
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
""";

    internal static int Run(string[] arguments)
    {
        if (arguments is ["--help"] or ["-h"])
        {
            Console.WriteLine(HelpText);
            return 0;
        }

        var inputs = new List<string>();
        var scenarios = new List<string>();
        var mainArguments = new List<GesValue>();
        var suppliedOptions = new HashSet<string>(StringComparer.Ordinal);
        long? seed = null;
        var maxMessages = GameEventScriptRuntimeLimits.Default.MaxProcessedEventsPerRun;
        var maxSteps = GameEventScriptRuntimeLimits.Default.MaxExecutionSteps;
        var verbose = false;
        var quiet = false;
        var interactive = false;
        var color = false;
        var parseOptions = true;
        var argumentGroup = false;
        var mainArgumentsSpecified = false;
        for (var index = 0; index < arguments.Length; index++)
        {
            var argument = arguments[index];
            if (!parseOptions)
            {
                mainArguments.Add(GesValue.GesText(argument));
                continue;
            }
            if (argument == "--")
            {
                parseOptions = false;
                mainArgumentsSpecified = true;
                continue;
            }
            if (argument == "--args")
            {
                if (index + 1 == arguments.Length || IsOption(arguments[index + 1])) return UsageError("Specify at least one value after --args.");
                argumentGroup = true;
                mainArgumentsSpecified = true;
                continue;
            }
            if (argumentGroup && !IsOption(argument))
            {
                mainArguments.Add(GesValue.GesText(argument));
                continue;
            }
            argumentGroup = false;
            if (argument is "--help" or "-h")
            {
                Console.WriteLine(HelpText);
                return 0;
            }
            else if (argument is "--scenario" or "--arg" or "--seed" or "--max-messages" or "--max-steps")
            {
                if (index + 1 == arguments.Length) return UsageError($"Specify a value after {argument}.");
                if (argument is not ("--scenario" or "--arg") && !suppliedOptions.Add(argument)) return UsageError($"Specify {argument} only once.");
                var value = arguments[++index];
                if (argument == "--scenario")
                {
                    if (string.IsNullOrWhiteSpace(value) || value.StartsWith('-')) return UsageError("Specify a scenario path; prefix a path starting with '-' with './'.");
                    scenarios.Add(value);
                }
                else if (argument == "--arg")
                {
                    mainArgumentsSpecified = true;
                    mainArguments.Add(GesValue.GesText(value));
                }
                else if (argument == "--seed")
                {
                    if (!long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed)) return UsageError("The seed must be a signed 64-bit integer.");
                    seed = parsed;
                }
                else
                {
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0) return UsageError($"{argument} requires a positive 32-bit integer.");
                    if (argument == "--max-messages") maxMessages = parsed;
                    else maxSteps = parsed;
                }
            }
            else if (argument == "--color") color = true;
            else if (argument == "--interactive") interactive = true;
            else if (argument is "-v" or "--verbose") verbose = true;
            else if (argument is "-q" or "--quiet") quiet = true;
            else if (argument.StartsWith('-')) return UsageError($"Unknown run option '{argument}'.");
            else
            {
                if (string.IsNullOrWhiteSpace(argument)) return UsageError("Program paths must not be empty.");
                inputs.Add(argument);
            }
        }

        if (inputs.Count == 0 && !interactive) return UsageError("Specify source files or .gesb files to run.");
        if (verbose && quiet) return UsageError("--verbose and --quiet cannot be combined.");
        if (interactive && scenarios.Count > 0) return UsageError("--interactive and --scenario cannot be combined.");
        if (mainArgumentsSpecified && (interactive || scenarios.Count > 0)) return UsageError("--arg, --args, and -- are available only when running Main.");

        string? activePath = null;
        try
        {
            var paths = CompileSources.Expand(inputs);
            var binaryCount = paths.Count(path => string.Equals(Path.GetExtension(path), ".gesb", StringComparison.OrdinalIgnoreCase));
            if (binaryCount > 0 && binaryCount != paths.Count) return UsageError("Run accepts either source files compiled together or binary files loaded separately; inputs cannot be mixed.");
            var programs = new List<GameEventScriptProgram>();
            if (binaryCount > 0)
            {
                foreach (var path in paths)
                {
                    activePath = path;
                    programs.Add(GameEventScriptProgramReader.Read(File.ReadAllBytes(path)));
                }
            }
            else if (paths.Count > 0) programs.Add(RunProgramFiles.Compile(paths, ref activePath));

            IReadOnlyList<string> scenarioPaths = scenarios.Count == 0 ? [] : CompileSources.Expand(scenarios);
            var scenario = scenarioPaths.Count == 0 ? null : RunProgramFiles.Compile(scenarioPaths, ref activePath);
            color = color && !PrettyPrompt.PromptConfiguration.HasUserOptedOutFromColor;
            var observer = new RunObserver(verbose, color, interactive);
            var builder = GameEventScriptHost.CreateBuilder().WithRuntimeObserver(observer)
                .WithRuntimeLimits(new GameEventScriptRuntimeLimits { MaxProcessedEventsPerRun = maxMessages, MaxExecutionSteps = maxSteps });
            if (seed is { } configuredSeed) builder.WithRandomSeed(configuredSeed);
            var host = builder.Build();
            var session = new RunSession(host, observer);
            observer.SubscribeConsoleHandlers(session.Inventory);
            for (var index = 0; index < programs.Count; index++) session.Inventory.Load(programs[index], binaryCount > 0 ? [paths[index]] : paths);
            if (scenario is not null) session.Inventory.Load(scenario, scenarioPaths);
            var runMain = scenario is null && !interactive;
            if (runMain && !programs.Any(HasMainHandler))
            {
                Console.Error.WriteLine("error cli.missingMain: No handler matches Main(args). Add 'on Main(args)', use --scenario, or use --interactive.");
                return 1;
            }
            if (!session.Pump()) return 1;
            if (runMain)
            {
                var message = GameEventScriptMessage.Create("Main", [new GameEventScriptMessageArgument("args", GesValue.GesList(mainArguments.ToArray()))]);
                if (!host.Receive(message))
                {
                    Console.Error.WriteLine("error cli.mainRejected: The host rejected Main(args).");
                    return 1;
                }
                if (!session.Pump()) return 1;
            }
            else if (interactive)
            {
                activePath = "<stdin>";
                if (!RunConsole.Run(host, session, color, quiet)) return 1;
            }
            if (!quiet) session.WriteSummary();
            return observer.ScriptExitCode;
        }
        catch (GameEventScriptCompileException exception)
        {
            foreach (var diagnostic in exception.Diagnostics) RunObserver.WriteDiagnostic(diagnostic, "compile");
            return 1;
        }
        catch (GameEventScriptProgramFormatException exception)
        {
            RunObserver.WriteDiagnostic(exception.Diagnostic, activePath ?? "decode");
            if (exception.ByteOffset is { } offset) Console.Error.WriteLine($"  byteOffset={offset}");
            if (exception.SectionType is { } section) Console.Error.WriteLine($"  sectionType=0x{section:X4}");
            if (exception.EntryIndex is { } entry) Console.Error.WriteLine($"  entryIndex={entry}");
            return 1;
        }
        catch (GameEventScriptDynamicLinkException exception)
        {
            RunObserver.WriteDiagnostic(exception.Diagnostic, "link");
            return 1;
        }
        catch (DecoderFallbackException exception)
        {
            Console.Error.WriteLine($"{activePath}: error cli.invalidEncoding: {exception.Message}");
            return 1;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"error cli.io: {exception.Message}");
            return 1;
        }
        catch (ArgumentException exception)
        {
            return UsageError(exception.Message);
        }
    }

    // Treat a leading minus followed by a digit (or .digit) as a value in --args,
    // without converting it. Unknown option spellings must still produce usage errors.
    private static bool IsOption(string text)
        => text.Length > 1 && text[0] == '-' && !char.IsAsciiDigit(text[1]) && !(text.Length > 2 && text[1] == '.' && char.IsAsciiDigit(text[2]));

    private static bool HasMainHandler(GameEventScriptProgram program)
    {
        foreach (var binding in program.Bindings.Entries)
        {
            if (binding.RequiredTags.Count > 0 || program.StringConstants.Resolve(binding.Name) != "Main") continue;
            if (binding.Kind == GameEventScriptBinaryBindKind.MessageNameHandler) return true;
            if (binding.Kind == GameEventScriptBinaryBindKind.MessageHandler && binding.ArgumentNames.Count == 1 && program.StringConstants.Resolve(binding.ArgumentNames[0]) == "args") return true;
        }
        return false;
    }

    private static int UsageError(string message)
    {
        Console.Error.WriteLine($"error cli.usage: {message} Run 'ges run --help' for usage.");
        return 2;
    }
}
