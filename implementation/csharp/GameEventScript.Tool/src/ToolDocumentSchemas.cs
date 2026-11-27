// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;

namespace GameEventScript.Tool;

// The schemas of `document check --schema`: the given sources read as one schema set, against which each document with
// a header is checked. `:Schema(:Name)` names a schema of the set. `:Schema('file.gess')` names a file relative to the
// document, which must be one of the given sources; the set then names that source by the header's text, as the runtime
// matches a file header against source names, and every other source by its full path, so no two sources collide.
internal sealed class ToolDocumentSchemas
{
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private readonly GesSchemaSource[] _sources;
    private readonly string[] _fullPaths;
    private readonly GesSchemaSet _set;
    private readonly Dictionary<(int Source, string Name), GesSchemaSet> _fileSets = [];

    private ToolDocumentSchemas(GesSchemaSource[] sources)
    {
        _sources = sources;
        _fullPaths = Array.ConvertAll(sources, source => Path.GetFullPath(source.Name));
        _set = GesSchemaSet.Parse(sources);
    }

    // The schema set of `--schema`, or null without the option; a schema error throws as for compile.
    internal static ToolDocumentSchemas? Load(IReadOnlyList<string> arguments, ref string? activePath)
        => ToolSchemas.Sources(arguments, ref activePath) is { } sources ? new ToolDocumentSchemas(sources) : null;

    // The handle type of the set with this name, or null.
    internal GesHandleType? HandleType(string name)
    {
        foreach (var type in _set.HandleTypes)
            if (type.Name == name) return type;
        return null;
    }

    // Checks a readable document against the schema of its header and reports the first error; a document without a header
    // has nothing to check against.
    internal bool Check(string path, string text, GesDocument document)
    {
        try
        {
            switch (document.Schema)
            {
                case null:
                    return true;
                case { Kind: GesDocumentSchemaKind.Type }:
                    _set.CheckDocument(text);
                    return true;
                case { Name: var name }:
                    if (FileSet(path, name) is not { } set)
                    {
                        Console.Error.WriteLine($"{path}: error schema.unknownName: The schema file '{name}' is none of the files given with --schema.");
                        return false;
                    }
                    set.CheckDocument(text);
                    return true;
            }
        }
        catch (GesSchemaException exception)
        {
            var place = exception.Line > 0 && exception.SourceName.Length == 0 ? $"{path}({exception.Line},{exception.Column})"
                : exception.Line > 0 ? $"{exception.SourceName}({exception.Line},{exception.Column})"
                : path;
            Console.Error.WriteLine($"{place}: error {exception.Code}: {exception.Message}");
            return false;
        }
    }

    // The set for a file header, or null when the file is none of the given sources.
    private GesSchemaSet? FileSet(string path, string name)
    {
        var fullPath = Path.GetFullPath(name, Path.GetDirectoryName(Path.GetFullPath(path))!);
        var index = Array.FindIndex(_fullPaths, candidate => string.Equals(candidate, fullPath, PathComparison));
        if (index < 0) return null;
        if (_fileSets.TryGetValue((index, name), out var set)) return set;
        var sources = new GesSchemaSource[_sources.Length];
        for (var other = 0; other < sources.Length; other++)
            sources[other] = new GesSchemaSource(other == index ? name : _fullPaths[other], _sources[other].Text);
        set = GesSchemaSet.Parse(sources);
        _fileSets.Add((index, name), set);
        return set;
    }
}
