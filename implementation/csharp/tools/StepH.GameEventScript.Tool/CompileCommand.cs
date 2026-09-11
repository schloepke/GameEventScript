// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.CSharpBridge;

namespace StepH.GameEventScript.Tool;

internal static class CompileCommand
{
    private const string HelpText = """
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
""";

    internal static int Run(string[] arguments)
    {
        if (arguments is ["--help"] or ["-h"])
        {
            Console.WriteLine(HelpText);
            return 0;
        }

        var sourceArguments = new List<string>();
        string? outputPath = null;
        var debugInfo = GameEventScriptDebugInfoOptions.All;
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
            else if (parseOptions && argument is "-o" or "--output")
            {
                if (outputPath is not null || index + 1 == arguments.Length || arguments[index + 1].StartsWith('-'))
                    return UsageError("Specify one output path after -o or --output. Prefix a path starting with '-' with './'.");
                outputPath = arguments[++index];
            }
            else if (parseOptions && argument == "--no-debug")
            {
                debugInfo = GameEventScriptDebugInfoOptions.None;
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
                return UsageError($"Unknown compile option '{argument}'.");
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
            if (sourcePaths.Count > 1 && outputPath is null) return UsageError("Specify -o or --output when compiling multiple source files.");
            outputPath = Path.GetFullPath(outputPath ?? Path.ChangeExtension(sourcePaths[0], ".gesb"));
            var outputPathComparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            foreach (var sourcePath in sourcePaths)
                if (string.Equals(Path.GetFullPath(sourcePath), outputPath, outputPathComparison)) return UsageError("Source and output must be different files.");

            var builder = GameEventScriptBuilder.Create().WithDebugInfo(debugInfo);
            foreach (var sourcePath in sourcePaths)
            {
                activeSourcePath = sourcePath;
                builder.AddFile(sourcePath);
            }
            var program = builder.Compile();
            var bytes = GameEventScriptProgramWriter.ToArray(program);
            ToolFileOutput.Write(outputPath, bytes);
            stopwatch.Stop();
            if (!quiet) CompileReport.Write(Console.Out, program, sourcePaths, outputPath, bytes.Length, stopwatch.Elapsed, verbose);
            return 0;
        }
        catch (GameEventScriptCompileException exception)
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
    }

    private static int UsageError(string message)
    {
        Console.Error.WriteLine($"error cli.usage: {message} Run 'ges compile --help' for usage.");
        return 2;
    }

}
