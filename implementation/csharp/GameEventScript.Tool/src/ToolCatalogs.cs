// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using GameEventScript.Api;

namespace GameEventScript.Tool;

// The catalogs of `document check --catalog @Type root.gesd`. Each option adds a root to the catalog of its type; the
// root is named by the path as given and its files are read relative to the root file's directory. Catalogs are read in
// the order their types first appear, each with every earlier catalog as a dependency, so a catalog comes after the
// catalogs it pins (specs/Documents.md#reading-a-catalog).
internal static class ToolCatalogs
{
    internal const string Option = "--catalog";

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    // Reads every catalog and reports each error as `path(line,column): error code: message`; returns the number of
    // catalogs that failed, or -1 after a usage error was reported.
    internal static int Check(IReadOnlyList<(string Type, string Path)> options, ToolDocumentSchemas schemas, bool quiet)
    {
        var order = new List<string>();
        var roots = new Dictionary<string, List<GesCatalogRoot>>(StringComparer.Ordinal);
        foreach (var (type, path) in options)
        {
            if (!roots.TryGetValue(type, out var list))
            {
                roots.Add(type, list = []);
                order.Add(type);
            }
            if (list.Exists(root => root.Name == path))
            {
                Console.Error.WriteLine($"error cli.usage: The root '{path}' of @{type} is given twice. Run 'dotnet ges document --help' for usage.");
                return -1;
            }
            list.Add(new GesCatalogRoot(path, Path.GetFileName(path)));
        }

        var read = new List<GesCatalog>();
        var failed = 0;
        foreach (var name in order)
        {
            if (schemas.HandleType(name) is not { } type)
            {
                Console.Error.WriteLine($"{roots[name][0].Name}: error schema.unknownName: The schemas of --schema declare no handle type @{name}.");
                failed++;
                continue;
            }
            try
            {
                var catalog = GesCatalog.Read(type, roots[name], new Files(), read);
                read.Add(catalog);
                if (!quiet) Console.WriteLine($"Checked catalog @{name}: {Count(catalog.Entries.Count)}.");
            }
            catch (GesCatalogException exception)
            {
                foreach (var diagnostic in exception.Diagnostics)
                    Console.Error.WriteLine($"{Place(diagnostic.Location)}: error {diagnostic.Code}: {diagnostic.Message}");
                failed++;
            }
            catch (ArgumentException exception)
            {
                Console.Error.WriteLine($"{roots[name][0].Name}: error cli.usage: {exception.Message}");
                failed++;
            }
        }
        return failed;
    }

    private static string Count(int entries) => entries == 1 ? "1 entry" : $"{entries} entries";

    // A location as a file path: the root's directory joined with the root-relative path.
    private static string Place(GesSourceLocation location)
    {
        var path = Join(location.ModuleName, location.SourceName);
        return location.Line > 0 ? $"{path}({location.Line},{location.Column})" : path;
    }

    private static string Join(string root, string path)
    {
        var directory = Path.GetDirectoryName(root);
        return string.IsNullOrEmpty(directory) ? path : Path.Combine(directory, path);
    }

    // Reads a root-relative path below the directory of the root file; a missing or unreadable file is absent.
    private sealed class Files : IGesCatalogFiles
    {
        public string? Read(string root, string path)
        {
            try
            {
                return StrictUtf8.GetString(File.ReadAllBytes(Join(root, path)));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DecoderFallbackException)
            {
                return null;
            }
        }
    }
}
