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
  dotnet ges document check <data.gesd> [more.gesd ...] [--schema <file.gess> ...] [-q]
  dotnet ges document check --schema <file.gess> ... --catalog @<Type> <root.gesd> ... [data.gesd ...] [-q]
  dotnet ges document format <data.gesd> [-o <output.gesd>] [--color]

Commands:
  check   Read GES documents and report the first error of each with its line and column.
  format  Print a GES document in the recommended layout.

Options:
  --schema <file.gess> Check each document against the schema its header names; repeatable.
  --catalog @<Type> <root.gesd>
                       Read the catalog of a handle type from a root file; repeatable.
  -o, --output <path>  Write the formatted document to a UTF-8 file instead of stdout.
  --color              Color terminal output (unless NO_COLOR is set).
  -q, --quiet          Hide the success report of check.
  -h, --help           Show this help.
  --                   Treat remaining arguments as file names.

Reading follows the strict document rules: unquoted Text, duplicate keys and
invalid literals are errors. Formatting drops comments and layout; the output
reads back to an equal document. An existing output file is replaced only after
successful reading. The schema reference is kept, but schema files are not read.

With --schema, check reads the given files as one schema set and checks every
document with a header against its schema: :Schema(:Name) names a schema of the
set, :Schema('file.gess') a file relative to the document that must be one of the
given files. A schema that the set does not hold is schema.unknownName. Documents
without a header are only read.

With --catalog, check reads the catalog of each named handle type of the schema
set from its root files and reports every error of it. Imports are read relative
to the directory of their root file and cannot leave it. Catalogs are read in the
order their types first appear; each may pin entries of the catalogs before it.
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
        var schemaArguments = new List<string>();
        var catalogArguments = new List<(string Type, string Path)>();
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
            else if (parseOptions && check && argument == ToolSchemas.Option)
            {
                if (index + 1 == arguments.Length || arguments[index + 1].StartsWith('-') || string.IsNullOrWhiteSpace(arguments[index + 1]))
                    return UsageError("Specify a schema path after --schema; prefix a path starting with '-' with './'.");
                schemaArguments.Add(arguments[++index]);
            }
            else if (parseOptions && check && argument == ToolCatalogs.Option)
            {
                if (index + 2 >= arguments.Length || !arguments[index + 1].StartsWith('@') || arguments[index + 1].Length == 1
                    || arguments[index + 2].StartsWith('-') || string.IsNullOrWhiteSpace(arguments[index + 2]))
                    return UsageError("Specify a handle type and a root file after --catalog, for example '--catalog @Unit units.gesd'.");
                catalogArguments.Add((arguments[index + 1][1..], arguments[index + 2]));
                index += 2;
            }
            else if (parseOptions && !check && argument == "--color") color = Environment.GetEnvironmentVariable("NO_COLOR") is null;
            else if (parseOptions && argument.StartsWith('-')) return UsageError($"Unknown document {arguments[0]} option '{argument}'.");
            else if (string.IsNullOrWhiteSpace(argument)) return UsageError("Document paths must not be empty.");
            else paths.Add(argument);
        }

        if (paths.Count == 0 && catalogArguments.Count == 0) return UsageError("Specify a document file.");
        if (catalogArguments.Count > 0 && schemaArguments.Count == 0) return UsageError("--catalog needs the schemas of --schema.");
        if (!check && paths.Count > 1) return UsageError("Format accepts exactly one document file.");
        if (outputPath is not null && string.IsNullOrWhiteSpace(outputPath)) return UsageError("The output path must not be empty.");
        string? activePath = null;
        try
        {
            if (check)
            {
                var schemas = ToolDocumentSchemas.Load(schemaArguments, ref activePath);
                var failed = 0;
                if (catalogArguments.Count > 0)
                {
                    failed = ToolCatalogs.Check(catalogArguments, schemas!, quiet);
                    if (failed < 0) return 2;
                }
                // An unreadable file fails like an invalid one; the remaining files are still checked.
                foreach (var path in paths)
                {
                    try
                    {
                        var source = StrictUtf8.GetString(File.ReadAllBytes(path));
                        if (Read(path, source) is not { } read || schemas?.Check(path, source, read) == false) failed++;
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
                if (!quiet && failed == 0 && paths.Count > 0) Console.WriteLine(paths.Count == 1 ? $"Checked {paths[0]}." : $"Checked {paths.Count} documents.");
                return failed == 0 ? 0 : 1;
            }

            activePath = paths[0];
            if (outputPath is not null)
            {
                outputPath = Path.GetFullPath(outputPath);
                var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                if (string.Equals(Path.GetFullPath(paths[0]), outputPath, comparison)) return UsageError("Input and output must be different files.");
            }
            if (Read(paths[0], StrictUtf8.GetString(File.ReadAllBytes(paths[0]))) is not { } document) return 1;
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
        catch (GesSchemaException exception)
        {
            ToolSchemas.Report(exception);
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

    // Reads one document and reports its error as `path(line,column): error code: message`.
    private static GesDocument? Read(string path, string text)
    {
        var document = ToolDocuments.Read(text, out var error);
        if (error is { } found) Console.Error.WriteLine(ToolDocuments.Format(path, found));
        return document;
    }

    private static int UsageError(string message)
    {
        Console.Error.WriteLine($"error cli.usage: {message} Run 'dotnet ges document --help' for usage.");
        return 2;
    }
}
