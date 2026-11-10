// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using GameEventScript.Api;

namespace GameEventScript.Tool;

internal static class DocumentCommand
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private const string HelpText = """
Usage:
  dotnet ges document check <data.gesd> [more.gesd ...] [-q]
  dotnet ges document format <data.gesd> [-o <output.gesd>] [--color]

Commands:
  check   Read GES documents and report the first error of each with its line and column.
  format  Print a GES document in the recommended layout.

Options:
  -o, --output <path>  Write the formatted document to a UTF-8 file instead of stdout.
  --color              Color terminal output (unless NO_COLOR is set).
  -q, --quiet          Hide the success report of check.
  -h, --help           Show this help.
  --                   Treat remaining arguments as file names.

Reading follows the strict document rules: unquoted Text, duplicate keys and
invalid literals are errors. Formatting drops comments and layout; the output
reads back to an equal document. An existing output file is replaced only after
successful reading. The schema reference is kept, but schema files are not read.
""";

    internal static int Run(string[] arguments)
    {
        if (arguments is [] or ["--help"] or ["-h"])
        {
            Console.WriteLine(HelpText);
            return arguments.Length == 0 ? 2 : 0;
        }
        if (arguments[0] is not ("check" or "format")) return UsageError($"Unknown document command '{arguments[0]}'.");
        var check = arguments[0] == "check";
        var paths = new List<string>();
        string? outputPath = null;
        var quiet = false;
        var color = false;
        var parseOptions = true;
        for (var index = 1; index < arguments.Length; index++)
        {
            var argument = arguments[index];
            if (parseOptions && argument == "--") parseOptions = false;
            else if (parseOptions && argument is "--help" or "-h")
            {
                Console.WriteLine(HelpText);
                return 0;
            }
            else if (parseOptions && !check && argument is "-o" or "--output")
            {
                if (outputPath is not null || index + 1 == arguments.Length || arguments[index + 1].StartsWith('-'))
                    return UsageError("Specify one output path after -o or --output. Prefix a path starting with '-' with './'.");
                outputPath = arguments[++index];
            }
            else if (parseOptions && check && argument is "-q" or "--quiet") quiet = true;
            else if (parseOptions && !check && argument == "--color") color = Environment.GetEnvironmentVariable("NO_COLOR") is null;
            else if (parseOptions && argument.StartsWith('-')) return UsageError($"Unknown document {arguments[0]} option '{argument}'.");
            else if (string.IsNullOrWhiteSpace(argument)) return UsageError("Document paths must not be empty.");
            else paths.Add(argument);
        }

        if (paths.Count == 0) return UsageError("Specify a document file.");
        if (!check && paths.Count > 1) return UsageError("Format accepts exactly one document file.");
        if (outputPath is not null && string.IsNullOrWhiteSpace(outputPath)) return UsageError("The output path must not be empty.");
        string? activePath = null;
        try
        {
            if (check)
            {
                var failed = 0;
                // An unreadable file fails like an invalid one; the remaining files are still checked.
                foreach (var path in paths)
                {
                    try
                    {
                        if (Read(path) is null) failed++;
                    }
                    catch (DecoderFallbackException exception)
                    {
                        Console.Error.WriteLine($"{path}: error cli.invalidEncoding: {exception.Message}");
                        failed++;
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                    {
                        Console.Error.WriteLine($"error cli.io: {exception.Message}");
                        failed++;
                    }
                }
                if (!quiet && failed == 0) Console.WriteLine(paths.Count == 1 ? $"Checked {paths[0]}." : $"Checked {paths.Count} documents.");
                return failed == 0 ? 0 : 1;
            }

            activePath = paths[0];
            if (outputPath is not null)
            {
                outputPath = Path.GetFullPath(outputPath);
                var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                if (string.Equals(Path.GetFullPath(paths[0]), outputPath, comparison)) return UsageError("Input and output must be different files.");
            }
            if (Read(paths[0]) is not { } document) return 1;
            var text = document.Print();
            if (outputPath is not null)
            {
                ToolFileOutput.Write(outputPath, StrictUtf8.GetBytes(text));
                Console.WriteLine($"Formatted {paths[0]} -> {outputPath}.");
                return 0;
            }
            if (!Console.IsOutputRedirected) text = ToolTextDisplay.ExpandTabs(text);
            Console.Write(color && !Console.IsOutputRedirected ? RunHighlighting.RenderDocument(text) : text);
            return 0;
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
    }

    // Reads one document and reports its error as `path(line,column): error code: message`.
    private static GesDocument? Read(string path)
    {
        var document = ToolDocuments.Read(StrictUtf8.GetString(File.ReadAllBytes(path)), out var error);
        if (error is { } found) Console.Error.WriteLine(ToolDocuments.Format(path, found));
        return document;
    }

    private static int UsageError(string message)
    {
        Console.Error.WriteLine($"error cli.usage: {message} Run 'dotnet ges document --help' for usage.");
        return 2;
    }
}
