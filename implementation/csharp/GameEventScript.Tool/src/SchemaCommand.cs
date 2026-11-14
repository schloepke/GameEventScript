// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using GameEventScript.Api;
using GameEventScript.Generator;
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
  dotnet ges schema dump <schema.gess> [more.gess ...] --root <Name> [--param <name>=<literal> ...] --data <data.bin> [--baseline <record.bin>]
  dotnet ges schema layout <schema.gess> [more.gess ...] (--root <Name> ... | --all-roots | --wire <group> | --model <group>)
                           [--param <name>=<literal> ...] [-o <layout.json>]
  dotnet ges schema generate <schema.gess> [more.gess ...] (--root <Name> ... | --all-roots | --wire <group> | --model <group>)
                             [--param <name>=<literal> ...]
                             --language csharp|swift [--namespace <A.B>] [--access public|internal] [--values]
                             [--checked-exclusivity] [--bindings] [--documents] [--models <namespace>] [--extend-namespace <A>]
                             [-o <file> | -d <dir> [--split schema|file]] [--check]

Commands:
  check      Read schemas and report the first error with its line and column.
  format     Print a schema source in the recommended layout. Comments are dropped.
  hash       Print the schema hash and, with parameters, the binding hash.
  canonical  Print the canonical form that the schema hash runs over.
  dump       List each field of binary data with its bit offset, width and value.
  layout     Print the language-neutral generation layout of the roots and every type they reach as JSON.
  generate   Generate C# or Swift code that writes and reads the records of the roots and every type they reach.

Options:
  --root <Name>             The schema, message or message set; layout and generate accept several.
  --all-roots               Make every declaration of the files a root of layout or generate.
  --wire <group>            Make the wire roots of the group the roots, in declaration order; wire alone is the group default.
  --model <group>           Generate plain model types of the model roots of the group instead of wire code.
  --language <name>         The language that generate writes: csharp or swift.
  --namespace <A.B>         The namespace of the generated types; Swift declares it as nested enums.
  --access <level>          The visibility of the generated types: public (default) or internal.
  --values                  Also convert generated records and models to and from GES values and messages.
  --checked-exclusivity     Keep the dynamic exclusivity checks of generated Swift classes.
  --extend-namespace <A>    Extend this leading part of the Swift namespace, which another generation of the module
                            declares, instead of declaring it; wire code with --models does so for the part it shares.
  --bindings                Also offer generated model types to scripts as external types; only with --model.
  --documents               Also read model roots from GES document text; includes --values, only with --model.
  --models <namespace>      Share the packed types of the model code in that namespace ("" for the global one) and
                            convert wire records directly to and from the models of their schemas; only wire code.
  -d, --out-dir <dir>       Write the code as several files into a directory that generate owns, with a manifest.
  --split <kind>            With --out-dir, one file per declaration (schema, the default) or per schema file (file).
  --check                   Compare the generated code with the output file or directory instead of writing it;
                            changed, missing and surplus files fail.
  --param <name>=<literal>  A parameter value such as worldWidth=5000m; repeat for each one.
  --data <path>             The binary data of dump.
  --baseline <path>         The full record that makes dump read the data as a delta.
  -o, --output <path>       Write the formatted source, the layout or the code to a UTF-8 file instead of stdout.
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
        if (command is not ("check" or "format" or "hash" or "canonical" or "dump" or "layout" or "generate")) return UsageError($"Unknown schema command '{command}'.");
        var paths = new List<string>();
        string? root = null;
        var roots = new List<string>();
        var allRoots = false;
        string? wireGroup = null;
        string? modelGroup = null;
        var check = false;
        var withValues = false;
        var checkedExclusivity = false;
        string? extendNamespace = null;
        var bindings = false;
        var documents = false;
        string? language = null;
        string? space = null;
        string? access = null;
        string? data = null;
        string? baseline = null;
        string? output = null;
        string? outDir = null;
        string? split = null;
        string? models = null;
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
            else if (argument == "--all-roots" && command is "layout" or "generate") allRoots = true;
            else if (argument == "--check" && command == "generate") check = true;
            else if (argument == "--values" && command == "generate") withValues = true;
            else if (argument == "--checked-exclusivity" && command == "generate") checkedExclusivity = true;
            else if (argument == "--bindings" && command == "generate") bindings = true;
            else if (argument == "--documents" && command == "generate") documents = true;
            else if (argument is "--root" or "--data" or "--baseline" or "--param" or "-o" or "--output" or "--language" or "--namespace" or "--access" or "--wire" or "--model" or "-d" or "--out-dir" or "--split" or "--models" or "--extend-namespace")
            {
                var applies = argument switch
                {
                    "--root" => command is "hash" or "canonical" or "dump" or "layout" or "generate",
                    "--wire" or "--model" => command is "layout" or "generate",
                    "--param" => command is "hash" or "dump" or "layout" or "generate",
                    "--data" or "--baseline" => command == "dump",
                    "--language" or "--namespace" or "--access" or "-d" or "--out-dir" or "--split" or "--models" or "--extend-namespace" => command == "generate",
                    _ => command is "format" or "layout" or "generate"
                };
                if (!applies) return UsageError($"Unknown schema {command} option '{argument}'.");
                if (index + 1 == arguments.Length) return UsageError($"Specify a value after {argument}.");
                var next = arguments[++index];
                if (argument is "--wire" or "--model")
                {
                    if (wireGroup is not null || modelGroup is not null) return UsageError("Specify one --wire or --model group.");
                    if (argument == "--wire") wireGroup = next;
                    else modelGroup = next;
                }
                else if (argument == "--language") language = next;
                else if (argument == "--namespace") space = next;
                else if (argument == "--access") access = next;
                else if (argument == "--root" && command is "layout" or "generate")
                {
                    if (roots.Contains(next)) return UsageError($"The root '{next}' is given twice.");
                    roots.Add(next);
                }
                else if (argument == "--root") root = next;
                else if (argument == "--data") data = next;
                else if (argument == "--baseline") baseline = next;
                else if (argument is "-o" or "--output") output = next;
                else if (argument is "-d" or "--out-dir") outDir = next;
                else if (argument == "--split") split = next;
                else if (argument == "--models") models = next;
                else if (argument == "--extend-namespace") extendNamespace = next;
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
        if (command is "layout" or "generate" && (roots.Count > 0 ? 1 : 0) + (allRoots ? 1 : 0) + (wireGroup is null && modelGroup is null ? 0 : 1) != 1)
            return UsageError($"{command} requires one of --root, --all-roots, --wire and --model.");
        if (modelGroup is not null && checkedExclusivity) return UsageError("--checked-exclusivity concerns wire code, not --model.");
        if (bindings && modelGroup is null) return UsageError("--bindings concerns model code and requires --model.");
        if (documents && modelGroup is null) return UsageError("--documents concerns model code and requires --model.");
        if (models is not null && modelGroup is not null) return UsageError("--models concerns wire code, not --model.");
        if (extendNamespace is not null && language != "swift") return UsageError("--extend-namespace concerns Swift, whose namespaces are enums; C# merges namespaces.");
        if (command == "generate" && language is not ("csharp" or "swift")) return UsageError("generate requires --language csharp or --language swift.");
        if (access is not (null or "public" or "internal")) return UsageError("--access is public or internal.");
        if (output is not null && outDir is not null) return GenerateUsage("-o writes one file and --out-dir a directory; give one of them.");
        if (split is not null && outDir is null) return GenerateUsage("--split divides the files of --out-dir.");
        if (split is not (null or "schema" or "file")) return UsageError("--split is schema or file.");
        if (outDir is not null && string.IsNullOrWhiteSpace(outDir)) return UsageError("The output directory must not be empty.");
        if (check && output is null && outDir is null) return UsageError("--check requires -o or --out-dir with what to compare.");
        if (output is not null)
        {
            if (string.IsNullOrWhiteSpace(output)) return UsageError("The output path must not be empty.");
            var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var outputPath = Path.GetFullPath(output);
            if (paths.Any(path => string.Equals(Path.GetFullPath(path), outputPath, comparison))) return UsageError("Input and output must be different files.");
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
                case "layout" or "generate":
                    var options = command == "layout" ? null : new GesSchemaGenerateOptions
                    {
                        Language = language == "swift" ? GesSchemaLanguage.Swift : GesSchemaLanguage.CSharp,
                        Namespace = space ?? "",
                        Access = access == "internal" ? GesSchemaAccess.Internal : GesSchemaAccess.Public,
                        Values = withValues,
                        CheckedExclusivity = checkedExclusivity,
                        ExtendNamespace = extendNamespace,
                        Bindings = bindings,
                        Documents = documents,
                        Split = outDir is null ? GesSchemaSplit.None : split == "file" ? GesSchemaSplit.File : GesSchemaSplit.Schema,
                        Models = models
                    };
                    if (wireGroup is not null || modelGroup is not null)
                    {
                        var group = wireGroup ?? modelGroup!;
                        roots = [.. wireGroup is not null ? GesSchemaGenerator.WireRoots(set, group) : GesSchemaGenerator.ModelRoots(set, group)];
                        if (roots.Count == 0)
                        {
                            Console.Error.WriteLine($"schema: error generate.usage: The files have no {(wireGroup is not null ? "wire" : "model")} root in the group '{group}'.");
                            return 1;
                        }
                    }

                    return Layout(set, allRoots ? set.Schemas.Select(schema => schema.Name).ToList() : roots, parameters, output, outDir, options, check, modelGroup is not null);
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

            var dump = baseline is null ? GesSchemaDump.Read(bound, File.ReadAllBytes(data!)) : GesSchemaDump.Read(bound, File.ReadAllBytes(data!), File.ReadAllBytes(baseline));
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

    // Prints the layout, or with options the generated code; with check it compares the code with the output file.
    private static int Layout(GesSchemaSet set, List<string> roots, List<(string Name, string Literal)> parameters, string? output, string? outDir, GesSchemaGenerateOptions? options, bool check, bool model)
    {
        var values = new GesSchemaParameters();
        foreach (var (name, literal) in parameters)
        {
            if (model || !roots.Any(root => set.Schema(root).Parameters.Any(parameter => parameter.Name == name)))
            {
                Console.Error.WriteLine($"schema: error schema.unknownName: No root reaches the parameter '{name}'.");
                return 1;
            }

            values.With(name, literal);
        }

        string text;
        try
        {
            if (outDir is not null)
            {
                var files = model ? GesSchemaGenerator.GenerateModelFiles(set, roots, options!) : GesSchemaGenerator.GenerateFiles(set, roots, values, options!);
                return check ? ToolGeneratedDirectory.Check(outDir, files) : ToolGeneratedDirectory.Write(outDir, files);
            }

            text = model
                ? options is null ? GesSchemaGenerator.ModelLayout(set, roots) : GesSchemaGenerator.GenerateModels(set, roots, options)
                : options is null ? GesSchemaGenerator.Layout(set, roots, values) : GesSchemaGenerator.Generate(set, roots, values, options);
        }
        catch (GesGeneratorException exception)
        {
            Console.Error.WriteLine($"schema: error {exception.Code}: {exception.Message}");
            return 1;
        }

        if (check)
        {
            var path = Path.GetFullPath(output!);
            if (File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(StrictUtf8.GetBytes(text))) return 0;
            Console.Error.WriteLine(File.Exists(path) ? $"error cli.outdated: {output} differs from the generated code." : $"error cli.outdated: {output} does not exist.");
            return 1;
        }

        if (output is null) Console.Write(text);
        else ToolFileOutput.Write(Path.GetFullPath(output), StrictUtf8.GetBytes(text));
        return 0;
    }

    private static int GenerateUsage(string message)
    {
        Console.Error.WriteLine($"schema: error generate.usage: {message}");
        return 1;
    }

    private static int UsageError(string message)
    {
        Console.Error.WriteLine($"error cli.usage: {message} Run 'dotnet ges schema --help' for usage.");
        return 2;
    }
}
