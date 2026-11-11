// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text;

namespace GameEventScript.Generator;

// One top-level declaration of generated code and the declaration it belongs to. An empty owner is the shared part, such
// as the embedded schema sources; an outside unit stands after the namespace, at file level.
internal sealed class GesCodeUnit(string owner, Action<GesSchemaPlanner.Code> write, bool outside = false)
{
    internal string Owner { get; } = owner;

    internal Action<GesSchemaPlanner.Code> Write { get; } = write;

    internal bool Outside { get; } = outside;
}

// The declarations of generated code with what surrounds them: the header of the single file, the lines before the
// namespace and the namespace itself.
internal sealed class GesCodeDocument
{
    internal string Header = "";
    internal string Prologue = "";
    internal List<string> Space = [];
    internal bool Swift;

    // The Swift access keyword with its space, which declares the namespace enums.
    internal string Access = "";

    // What the namespace holds, "schema" or "model", for the documentation of Swift namespace enums.
    internal string Holds = "";

    // Whether the code depends on parameters, which the header of split files mentions.
    internal bool Parameters;
    internal List<GesCodeUnit> Units = [];
}

// Assembles generated declarations into one file or, split, into a file per declaration or per schema source with the
// shared part in its own file. The single file keeps one blank line between declarations, as split files do.
internal static class GesCodeFiles
{
    internal const string Manifest = ".ges-generated.json";

    internal static string Single(GesCodeDocument document)
    {
        var code = new GesSchemaPlanner.Code();
        code.Raw(document.Header);
        code.Raw(document.Prologue);
        if (document.Swift)
            foreach (var part in document.Space)
            {
                code.Line($"/// The namespace `{part}` of generated {document.Holds} types.");
                code.Open($"{document.Access}enum {part} {{");
            }
        else if (document.Space.Count > 0)
        {
            code.Line("namespace " + string.Join(".", document.Space));
            code.Open("{");
        }

        var first = true;
        foreach (var unit in document.Units)
        {
            if (unit.Outside) continue;
            if (!first) code.Blank();
            first = false;
            code.Raw(Render(unit, code.Depth));
        }

        if (document.Swift)
            foreach (var _ in document.Space) code.Close();
        else if (document.Space.Count > 0) code.Close();
        Outside(code, document.Units);
        return code.ToString();
    }

    // The files of split code, ordered by name, and the manifest last.
    internal static List<GesGeneratedFile> Split(GesCodeDocument document, GesSchemaSplit split, Func<string, string> sourceOf, Func<List<string>, string> manifest)
    {
        if (split == GesSchemaSplit.None) throw new GesGeneratorException(GesGeneratorException.Usage, "Split output needs a split by schema or by file.");
        var extension = document.Swift ? ".swift" : ".g.cs";
        // Swift requires distinct file names within a module, so names carry the namespace: wire and model code of one target
        // live in different namespaces.
        var space = string.Join(".", document.Space);
        var prefix = space.Length > 0 ? space + "." : "";
        var shared = "_" + (space.Length > 0 ? space : "Generated") + extension;

        // Groups by the declaration or the full source name, so that two sources of one file name collide.
        var groups = new List<(string Name, List<GesCodeUnit> Units)>();
        var keys = new Dictionary<string, int>(StringComparer.Ordinal);
        var sharedUnits = new List<GesCodeUnit>();
        foreach (var unit in document.Units)
        {
            if (unit.Owner.Length == 0)
            {
                sharedUnits.Add(unit);
                continue;
            }

            var key = split == GesSchemaSplit.Schema ? unit.Owner : sourceOf(unit.Owner);
            if (!keys.TryGetValue(key, out var index))
            {
                index = groups.Count;
                keys.Add(key, index);
                groups.Add((prefix + (split == GesSchemaSplit.Schema ? unit.Owner : FileName(key)) + extension, []));
            }

            groups[index].Units.Add(unit);
        }

        var files = new List<GesGeneratedFile>();
        if (SharedText(document, sharedUnits) is { } sharedText) files.Add(new GesGeneratedFile(shared, sharedText));
        foreach (var (name, units) in groups) files.Add(new GesGeneratedFile(name, FileText(document, units)));
        var folded = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            if (folded.TryGetValue(Fold(file.Path), out var other))
                throw new GesGeneratorException(GesGeneratorException.NameCollision, $"The generated files '{other}' and '{file.Path}' differ only in case or come from sources of one name.");
            folded.Add(Fold(file.Path), file.Path);
        }

        files.Sort((left, right) => string.CompareOrdinal(left.Path, right.Path));
        var names = files.ConvertAll(file => file.Path);
        files.Add(new GesGeneratedFile(Manifest, manifest(names)));
        return files;
    }

    // The header of a split file, which leaves the roots, parameters and hashes to the manifest so that a change touches
    // only the files of the declarations it changes.
    private static string Header(GesCodeDocument document)
        => "// <auto-generated>\n"
            + "//     Generated by `ges schema generate`; " + Manifest + " in this directory names the roots, parameters and hashes.\n"
            + "//     Do not edit it; generate it again when a schema" + (document.Parameters ? " or a parameter" : "") + " changes.\n"
            + "// </auto-generated>\n";

    private static string FileText(GesCodeDocument document, List<GesCodeUnit> units)
    {
        var code = new GesSchemaPlanner.Code();
        code.Raw(Header(document));
        code.Raw(document.Prologue);
        var space = document.Space.Count > 0;
        if (space && document.Swift) code.Open($"extension {string.Join(".", document.Space)} {{");
        else if (space)
        {
            code.Line("namespace " + string.Join(".", document.Space));
            code.Open("{");
        }

        for (var index = 0; index < units.Count; index++)
        {
            if (index > 0) code.Blank();
            code.Raw(Render(units[index], code.Depth));
        }

        if (space) code.Close();
        return code.ToString();
    }

    // The namespace and the shared declarations; null when there is neither.
    private static string? SharedText(GesCodeDocument document, List<GesCodeUnit> units)
    {
        var inside = units.FindAll(unit => !unit.Outside);
        var outside = units.FindAll(unit => unit.Outside);
        var declares = document.Swift && document.Space.Count > 0;
        if (inside.Count == 0 && outside.Count == 0 && !declares) return null;
        var code = new GesSchemaPlanner.Code();
        code.Raw(Header(document));
        code.Raw(document.Prologue);
        var opened = 0;
        if (document.Swift)
            for (var index = 0; index < document.Space.Count; index++)
            {
                var part = document.Space[index];
                code.Line($"/// The namespace `{part}` of generated {document.Holds} types.");
                if (index + 1 < document.Space.Count || inside.Count > 0)
                {
                    code.Open($"{document.Access}enum {part} {{");
                    opened++;
                }
                else code.Line($"{document.Access}enum {part} {{}}");
            }
        else if (document.Space.Count > 0 && inside.Count > 0)
        {
            code.Line("namespace " + string.Join(".", document.Space));
            code.Open("{");
            opened++;
        }

        for (var index = 0; index < inside.Count; index++)
        {
            if (index > 0) code.Blank();
            code.Raw(Render(inside[index], code.Depth));
        }

        for (var index = 0; index < opened; index++) code.Close();
        Outside(code, outside);
        return code.ToString();
    }

    private static void Outside(GesSchemaPlanner.Code code, List<GesCodeUnit> units)
    {
        foreach (var unit in units)
        {
            if (!unit.Outside) continue;
            code.Blank();
            code.Raw(Render(unit, 0));
        }
    }

    // A declaration written at the depth of its place, without the blank lines around it.
    private static string Render(GesCodeUnit unit, int depth)
    {
        var code = new GesSchemaPlanner.Code(depth);
        unit.Write(code);
        var text = code.ToString();
        var start = 0;
        while (start < text.Length && text[start] == '\n') start++;
        var end = text.Length;
        while (end - start > 1 && text[end - 1] == '\n' && text[end - 2] == '\n') end--;
        return text.Substring(start, end - start);
    }

    // The file name of a schema source without its directories and without the extension .gess.
    private static string FileName(string source)
    {
        var name = source.Substring(source.LastIndexOfAny(['/', '\\']) + 1);
        return name.EndsWith(".gess", StringComparison.Ordinal) && name.Length > 5 ? name.Substring(0, name.Length - 5) : name;
    }

    // ASCII letters folded to lowercase, as case-insensitive file systems compare names.
    private static string Fold(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var character in name) builder.Append(character is >= 'A' and <= 'Z' ? (char)(character + 32) : character);
        return builder.ToString();
    }
}
