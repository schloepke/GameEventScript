// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text;
using GameEventScript.Api;

namespace GameEventScript.Tool;

internal static class CompileCommand
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private const string CheckHelpText = """
Usage:
  dotnet ges check <source.ges> [more.ges ...] [--schema <file.gess> ...] [-v | -q]

Options:
  --schema <file.gess> Load a schema set and declare its handle types. Repeatable.
  -v, --verbose       Include bindings, dependencies, bytecode size, and resource requirements.
  -q, --quiet         Suppress success output. Errors are still reported.
  -h, --help          Show this help.
  --                  Treat remaining arguments as file names.

Sources are compiled and validated together without writing a binary or executing
handlers. Source order, UTF-8 decoding, and file-name patterns match 'dotnet ges compile'.
Checking does not resolve host extensions, external types or handle resolvers against a runtime registry.
""";

    private const string HelpText = """
Usage:
  dotnet ges compile <source.ges> [more.ges ...] [-o <output.gesb>] [--schema <file.gess> ...] [--no-debug] [-v | -q]

Options:
  -o, --output <path>  Output file. Required for multiple sources; otherwise defaults to source.gesb.
  --schema <file.gess> Load a schema set and declare its handle types. Repeatable.
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
The schema files of --schema are read as one set; file-name patterns expand as for sources.
""";

    internal static int Run(string[] arguments, bool checkOnly = false)
    {
        if (arguments is ["--help"] or ["-h"])
        {
            Console.WriteLine(checkOnly ? CheckHelpText : HelpText);
            return 0;
        }

        var sourceArguments = new List<string>();
        var schemaArguments = new List<string>();
        string? outputPath = null;
        var debugInfo = GesDebugInfoOptions.All;
        var verbose = false;
        var quiet = false;
        var parseOptions = true;
        for (var index = 0; index < arguments.Length; index++)
        {
            var argument = arguments[index];
            if (parseOptions && argument == "--")
            {
                parseOptions = false;
            }
            else if (!checkOnly && parseOptions && argument is "-o" or "--output")
            {
                if (outputPath is not null || index + 1 == arguments.Length || arguments[index + 1].StartsWith('-'))
                    return UsageError("Specify one output path after -o or --output. Prefix a path starting with '-' with './'.");
                outputPath = arguments[++index];
            }
            else if (parseOptions && argument == ToolSchemas.Option)
            {
                if (index + 1 == arguments.Length || arguments[index + 1].StartsWith('-') || string.IsNullOrWhiteSpace(arguments[index + 1]))
                    return UsageError("Specify a schema path after --schema; prefix a path starting with '-' with './'.");
                schemaArguments.Add(arguments[++index]);
            }
            else if (!checkOnly && parseOptions && argument == "--no-debug")
            {
                debugInfo = GesDebugInfoOptions.None;
            }
            else if (parseOptions && argument is "-v" or "--verbose")
            {
                verbose = true;
            }
            else if (parseOptions && argument is "-q" or "--quiet")
            {
                quiet = true;
            }
            else if (parseOptions && argument.StartsWith('-'))
            {
                return UsageError($"Unknown {(checkOnly ? "check" : "compile")} option '{argument}'.");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(argument)) return UsageError("Source paths must not be empty.");
                sourceArguments.Add(argument);
            }
        }

        if (sourceArguments.Count == 0) return UsageError("Specify at least one source file to compile.");
        if (outputPath is not null && string.IsNullOrWhiteSpace(outputPath)) return UsageError("The output path must not be empty.");
        if (verbose && quiet) return UsageError("--verbose and --quiet cannot be combined.");

        string? activeSourcePath = null;
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var sourcePaths = CompileSources.Expand(sourceArguments);
            if (!checkOnly)
            {
                if (sourcePaths.Count > 1 && outputPath is null) return UsageError("Specify -o or --output when compiling multiple source files.");
                outputPath = Path.GetFullPath(outputPath ?? Path.ChangeExtension(sourcePaths[0], ".gesb"));
                var outputPathComparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                foreach (var sourcePath in sourcePaths)
                    if (string.Equals(Path.GetFullPath(sourcePath), outputPath, outputPathComparison)) return UsageError("Source and output must be different files.");
            }

            var handleTypes = ToolSchemas.Load(schemaArguments, ref activeSourcePath);
            var builder = ToolSchemas.Builder(handleTypes).WithDebugInfo(debugInfo);
            foreach (var sourcePath in sourcePaths)
            {
                activeSourcePath = sourcePath;
                builder.AddScript(StrictUtf8.GetString(File.ReadAllBytes(sourcePath)), sourcePath);
            }
            var program = builder.Compile();
            if (checkOnly)
            {
                stopwatch.Stop();
                if (!quiet) CompileReport.WriteCheck(Console.Out, program, sourcePaths, stopwatch.Elapsed, verbose);
                return 0;
            }
            var bytes = program.Encode();
            ToolFileOutput.Write(outputPath!, bytes);
            stopwatch.Stop();
            if (!quiet) CompileReport.Write(Console.Out, program, sourcePaths, outputPath!, bytes.Length, stopwatch.Elapsed, verbose);
            return 0;
        }
        catch (GesCompileException exception)
        {
            foreach (var diagnostic in exception.Diagnostics)
            {
                var location = diagnostic.SourceLocation;
                var prefix = location?.SourceName ?? "compile";
                if (location?.Line is { } line) prefix += $"({line},{location.Column ?? 1})";
                Console.Error.WriteLine($"{prefix}: error {diagnostic.Code}: {diagnostic.Message}");
            }
            return 1;
        }
        catch (GesSchemaException exception)
        {
            ToolSchemas.Report(exception);
            return 1;
        }
        catch (DecoderFallbackException exception)
        {
            Console.Error.WriteLine($"{activeSourcePath}: error cli.invalidEncoding: {exception.Message}");
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
        int UsageError(string message)
        {
            Console.Error.WriteLine($"error cli.usage: {message} Run 'dotnet ges {(checkOnly ? "check" : "compile")} --help' for usage.");
            return 2;
        }
    }

}
