// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using GameEventScript.Api;
using GameEventScript.Syntax;

namespace GameEventScript.Tool;

internal static class SchemaCommand
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private const string HelpText = """
Usage:
  dotnet ges schema check <schema.gess> [more.gess ...] [-q]
  dotnet ges schema format <schema.gess> [-o <output.gess>]
  dotnet ges schema hash <schema.gess> [more.gess ...] --root <Name> [--param <name>=<literal> ...]
  dotnet ges schema canonical <schema.gess> [more.gess ...] --root <Name>
  dotnet ges schema dump <schema.gess> [more.gess ...] --root <Name> [--param <name>=<literal> ...] --data <data.bin>

Commands:
  check      Read schemas and report the first error with its line and column.
  format     Print a schema source in the recommended layout. Comments are dropped.
  hash       Print the schema hash and, with parameters, the binding hash.
  canonical  Print the canonical form that the schema hash runs over.
  dump       List each field of binary data with its bit offset, width and value.

Options:
  --root <Name>             The schema, message or message set.
  --param <name>=<literal>  A parameter value such as worldWidth=5000m; repeat for each one.
  --data <path>             The binary data of dump.
  -o, --output <path>       Write the formatted source to a UTF-8 file instead of stdout.
  -q, --quiet               Hide the success report of check.
  -h, --help                Show this help.
""";

    internal static int Run(string[] arguments)
    {
        if (arguments is [] or ["--help"] or ["-h"])
        {
            Console.WriteLine(HelpText);
            return arguments.Length == 0 ? 2 : 0;
        }

        var command = arguments[0];
        if (command is not ("check" or "format" or "hash" or "canonical" or "dump")) return UsageError($"Unknown schema command '{command}'.");
        var paths = new List<string>();
        string? root = null;
        string? data = null;
        string? output = null;
        var quiet = false;
        var parameters = new List<(string Name, string Literal)>();
        for (var index = 1; index < arguments.Length; index++)
        {
            var argument = arguments[index];
            if (argument is "--help" or "-h")
            {
                Console.WriteLine(HelpText);
                return 0;
            }

            if (argument is "-q" or "--quiet" && command == "check") quiet = true;
            else if (argument is "--root" or "--data" or "--param" or "-o" or "--output")
            {
                var applies = argument switch
                {
                    "--root" => command is "hash" or "canonical" or "dump",
                    "--param" => command is "hash" or "dump",
                    "--data" => command == "dump",
                    _ => command == "format"
                };
                if (!applies) return UsageError($"Unknown schema {command} option '{argument}'.");
                if (index + 1 == arguments.Length) return UsageError($"Specify a value after {argument}.");
                var next = arguments[++index];
                if (argument == "--root") root = next;
                else if (argument == "--data") data = next;
                else if (argument is "-o" or "--output") output = next;
                else
                {
                    var separator = next.IndexOf('=');
                    if (separator <= 0) return UsageError("A parameter is written name=literal.");
                    var name = next[..separator];
                    if (parameters.Any(parameter => parameter.Name == name)) return UsageError($"The parameter '{name}' is given twice.");
                    parameters.Add((name, next[(separator + 1)..]));
                }
            }
            else if (argument.StartsWith('-')) return UsageError($"Unknown schema {command} option '{argument}'.");
            else if (string.IsNullOrWhiteSpace(argument)) return UsageError("Schema paths must not be empty.");
            else paths.Add(argument);
        }

        if (paths.Count == 0) return UsageError("Specify a schema file.");
        if (command == "format" && paths.Count > 1) return UsageError("Format accepts exactly one schema file.");
        if (command is "hash" or "canonical" or "dump" && root is null) return UsageError($"{command} requires --root.");
        if (command == "dump" && data is null) return UsageError("dump requires --data.");
        if (output is not null)
        {
            if (string.IsNullOrWhiteSpace(output)) return UsageError("The output path must not be empty.");
            var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (string.Equals(Path.GetFullPath(paths[0]), Path.GetFullPath(output), comparison)) return UsageError("Input and output must be different files.");
        }

        try
        {
            if (command == "format")
            {
                var text = StrictUtf8.GetString(File.ReadAllBytes(paths[0]));
                var syntax = GesSchemaSourceReader.Read(text);
                if (syntax.Error is { } error)
                {
                    Console.Error.WriteLine($"{paths[0]}({error.Line},{error.Column}): error {error.Code}: {error.Message}");
                    return 1;
                }

                var printed = GesSchemaFormatter.Print(syntax);
                if (output is null) Console.Write(printed);
                else ToolFileOutput.Write(Path.GetFullPath(output), StrictUtf8.GetBytes(printed));
                return 0;
            }

            var sources = new GesSchemaSource[paths.Count];
            for (var index = 0; index < sources.Length; index++) sources[index] = new GesSchemaSource(paths[index], StrictUtf8.GetString(File.ReadAllBytes(paths[index])));
            var set = GesSchemaSet.Parse(sources);
            switch (command)
            {
                case "check":
                    if (!quiet) Console.WriteLine(paths.Count == 1 ? $"Checked {paths[0]}." : $"Checked {paths.Count} schemas.");
                    return 0;
                case "canonical":
                    Console.Write(set.Schema(root!).CanonicalForm);
                    return 0;
            }

            var schema = set.Schema(root!);
            var values = new GesSchemaParameters();
            foreach (var (name, literal) in parameters)
            {
                if (!schema.Parameters.Any(parameter => parameter.Name == name))
                {
                    Console.Error.WriteLine($"schema: error schema.unknownName: The root '{root}' has no parameter '{name}'.");
                    return 1;
                }

                values.With(name, literal);
            }

            if (command == "hash" && parameters.Count == 0)
            {
                Console.WriteLine(schema.Hash.ToString("x16"));
                return 0;
            }

            var bound = schema.Bind(values);
            if (command == "hash")
            {
                Console.WriteLine(bound.Hash.ToString("x16"));
                Console.WriteLine(bound.BindingHash.ToString("x16"));
                return 0;
            }

            var dump = GesSchemaDump.Read(bound, File.ReadAllBytes(data!));
            Console.Write(dump.Text);
            return dump.IsValid ? 0 : 1;
        }
        catch (GesSchemaException exception)
        {
            var place = exception.Line > 0 ? $"{exception.SourceName}({exception.Line},{exception.Column})" : "schema";
            Console.Error.WriteLine($"{place}: error {exception.Code}: {exception.Message}");
            return 1;
        }
        catch (DecoderFallbackException exception)
        {
            Console.Error.WriteLine($"error cli.invalidEncoding: {exception.Message}");
            return 1;
        }
        catch (ArgumentException exception)
        {
            return UsageError(exception.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"error cli.io: {exception.Message}");
            return 1;
        }
    }

    private static int UsageError(string message)
    {
        Console.Error.WriteLine($"error cli.usage: {message} Run 'dotnet ges schema --help' for usage.");
        return 2;
    }
}
