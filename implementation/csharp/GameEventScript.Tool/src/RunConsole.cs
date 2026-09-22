// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using GameEventScript.Api;

namespace GameEventScript.Tool;

internal static class RunConsole
{
    internal static bool Run(RunSession session, bool color, bool quiet)
    {
        var terminal = !Console.IsInputRedirected;
        if (terminal) Console.Error.WriteLine("GES event console. Type :help for commands and examples, :load <file> to add a program, or :quit to exit.");
        using var input = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: false);
        var history = new RunHistory();
        using var prompt = color && terminal && !Console.IsOutputRedirected && !Console.IsErrorRedirected ? new RunPrompt(history) : null;
        var success = true;
        var lineNumber = 0;
        while (true)
        {
            var waiting = session.Host.NextMessageDelay is not null;
            if (terminal && prompt is null && !waiting) Console.Error.Write("ges> ");
            var line = waiting ? RunWaitingInput.Read(session, input, terminal && !Console.IsErrorRedirected, color, history) : prompt is null ? input.ReadLine() : prompt.ReadLine();
            if (line is null) return success;
            history.Add(line);
            lineNumber++;
            if (lineNumber == 1 && line.StartsWith('\uFEFF')) line = line[1..];
            if (string.IsNullOrWhiteSpace(line)) continue;
            var trimmed = line.Trim();
            var separator = 0;
            while (separator < trimmed.Length && !char.IsWhiteSpace(trimmed[separator])) separator++;
            var command = trimmed[..separator];
            var commandArgument = trimmed[separator..].Trim();
            if (command == ":quit" && commandArgument.Length == 0) return success;
            if (command == ":help")
            {
                if (!RunConsoleHelp.Write(commandArgument)) success = false;
                continue;
            }
            if (command == ":list" && commandArgument.Length == 0)
            {
                session.Inventory.WritePrograms();
                continue;
            }
            if (command == ":handler" && commandArgument.Length == 0)
            {
                session.Inventory.WriteHandlers();
                continue;
            }
            if (command is not (":load" or ":dump" or ":source" or ":unload" or ":unloadAll" or ":reload") && command.StartsWith(':') && command.AsSpan(1).IndexOfAnyExceptInRange('a', 'z') < 0)
            {
                Console.Error.WriteLine($"error cli.consoleCommand: Unknown command or arguments '{trimmed}'. Use :help.");
                success = false;
                continue;
            }

            GameEventScriptInstance? instance = null;
            string? activePath = null;
            try
            {
                if (command == ":unload")
                {
                    session.Inventory.Unload(commandArgument);
                    if (!quiet) Console.Error.WriteLine($"Unloaded {commandArgument}.");
                    continue;
                }
                if (command is ":unloadAll" or ":reload")
                {
                    if (commandArgument.Length != 0) throw new ArgumentException($"{command} accepts no arguments.");
                    if (command == ":unloadAll")
                    {
                        var count = session.Inventory.UnloadAll();
                        if (!quiet) Console.Error.WriteLine($"Unloaded {count} programs. Native console handlers remain active.");
                    }
                    else
                    {
                        if (!session.Reload(ref activePath)) return false;
                        if (!quiet) Console.Error.WriteLine("Reloaded all active programs on a fresh host; initialization completed.");
                    }
                    continue;
                }
                if (command == ":dump")
                {
                    session.Inventory.WriteDump(commandArgument, color);
                    continue;
                }
                if (command == ":source")
                {
                    session.Inventory.WriteSource(commandArgument, color);
                    continue;
                }
                if (command == ":load")
                {
                    activePath = LoadPath(commandArgument);
                    session.Inventory.Load(RunProgramFiles.ReadOne(activePath), [activePath]);
                    if (!session.Pump()) return false;
                    if (!quiet) Console.Error.WriteLine($"Loaded {activePath}; initialization completed. Handlers remain active.");
                    continue;
                }
                var sourceName = $"<interactive:{lineNumber}>";
                var source = "on initialization {\n" + line + "\n}\n";
                var program = GameEventScriptBuilder.Create().AddScript(source, sourceName).Compile();
                var handlers = program.Bindings.Entries.Where(binding => binding.Kind is GameEventScriptBinaryBindKind.MessageHandler or GameEventScriptBinaryBindKind.MessageNameHandler).ToArray();
                if (handlers.Length != 1 || program.StringConstants.Resolve(handlers[0].Name) != "initialization")
                {
                    Console.Error.WriteLine("error cli.interactiveInput: Enter handler-body statements, not additional handler declarations.");
                    success = false;
                    continue;
                }
                instance = session.Host.Load(program);
                if (!session.Pump()) return false;
            }
            catch (GameEventScriptCompileException exception)
            {
                foreach (var diagnostic in exception.Diagnostics) RunObserver.WriteDiagnostic(diagnostic, "compile");
                success = false;
            }
            catch (GameEventScriptDynamicLinkException exception)
            {
                RunObserver.WriteDiagnostic(exception.Diagnostic, "link");
                success = false;
            }
            catch (GameEventScriptProgramFormatException exception)
            {
                RunObserver.WriteDiagnostic(exception.Diagnostic, activePath ?? "decode");
                if (exception.ByteOffset is { } offset) Console.Error.WriteLine($"  byteOffset={offset}");
                if (exception.SectionType is { } section) Console.Error.WriteLine($"  sectionType=0x{section:X4}");
                if (exception.EntryIndex is { } entry) Console.Error.WriteLine($"  entryIndex={entry}");
                success = false;
            }
            catch (DecoderFallbackException exception)
            {
                Console.Error.WriteLine($"{activePath}: error cli.invalidEncoding: {exception.Message}");
                success = false;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"{activePath}: error cli.io: {exception.Message}");
                success = false;
            }
            catch (ArgumentException exception)
            {
                Console.Error.WriteLine($"error cli.consoleCommand: {exception.Message}");
                success = false;
            }
            finally
            {
                instance?.Detach();
            }
        }
    }

    private static string LoadPath(string text)
    {
        if (text.Length > 0 && text[0] is '\'' or '"')
        {
            var quote = text[0];
            if (text.Length < 2 || text[^1] != quote) throw new ArgumentException("The quoted :load path must end with a matching quote.");
            text = text[1..^1].Replace(new string(quote, 2), quote.ToString(), StringComparison.Ordinal);
        }
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Specify one file after :load. Use :help load for examples.");
        return text;
    }
}
