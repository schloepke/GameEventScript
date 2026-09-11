// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using GameEventScript.Api;

namespace GameEventScript.Tool;

internal static class DumpCommand
{
    private const string HelpText = """
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
""";

    internal static int Run(string[] arguments)
    {
        if (arguments is ["--help"] or ["-h"])
        {
            Console.WriteLine(HelpText);
            return 0;
        }

        string? inputPath = null;
        string? outputPath = null;
        var addresses = false;
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
            else if (parseOptions && argument == "--addresses")
            {
                addresses = true;
            }
            else if (parseOptions && argument.StartsWith('-'))
            {
                return UsageError($"Unknown dump option '{argument}'.");
            }
            else if (inputPath is null)
            {
                inputPath = argument;
            }
            else
            {
                return UsageError("Dump accepts exactly one binary file.");
            }
        }

        if (string.IsNullOrWhiteSpace(inputPath)) return UsageError("Specify a binary file to dump.");
        if (outputPath is not null && string.IsNullOrWhiteSpace(outputPath)) return UsageError("The output path must not be empty.");

        try
        {
            var fullInputPath = Path.GetFullPath(inputPath);
            if (outputPath is not null)
            {
                outputPath = Path.GetFullPath(outputPath);
                var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                if (string.Equals(fullInputPath, outputPath, comparison)) return UsageError("Input and output must be different files.");
            }

            var program = GameEventScriptProgramReader.Read(File.ReadAllBytes(inputPath));
            var text = program.Dump(includeInstructionAddresses: addresses);
            var bytes = new UTF8Encoding(false, true).GetBytes(text);
            if (outputPath is null)
            {
                using var stdout = Console.OpenStandardOutput();
                stdout.Write(bytes);
            }
            else
            {
                ToolFileOutput.Write(outputPath, bytes);
                Console.WriteLine($"Dumped {inputPath} -> {outputPath} ({bytes.Length} bytes).");
            }
            return 0;
        }
        catch (GameEventScriptProgramFormatException exception)
        {
            var context = new List<string>();
            if (exception.ByteOffset is { } offset) context.Add($"byteOffset={offset}");
            if (exception.SectionType is { } section) context.Add($"sectionType=0x{section:X4}");
            if (exception.EntryIndex is { } entry) context.Add($"entryIndex={entry}");
            var suffix = context.Count == 0 ? string.Empty : " [" + string.Join(", ", context) + "]";
            Console.Error.WriteLine($"{inputPath}: error {exception.Diagnostic.Code}: {exception.Message}{suffix}");
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
        Console.Error.WriteLine($"error cli.usage: {message} Run 'ges dump --help' for usage.");
        return 2;
    }
}
