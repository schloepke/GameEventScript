// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json;
using GameEventScript.Generator;

namespace GameEventScript.Tool;

// A directory that `schema generate --out-dir` owns. Writing replaces changed files, removes the files that the previous
// manifest names and that are no longer generated, and leaves other files alone with a warning. Hidden files and Unity's
// .meta files are never reported, so temporary files and engine metadata can live beside the code.
internal static class ToolGeneratedDirectory
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    // An unchanged file keeps its time stamp, so that builds do not compile it again.
    private static void WriteChanged(string path, string text)
    {
        var bytes = StrictUtf8.GetBytes(text);
        if (File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes)) return;
        ToolFileOutput.Write(path, bytes);
    }

    internal static int Write(string directory, IReadOnlyList<GesGeneratedFile> files)
    {
        var root = Path.GetFullPath(directory);
        var owned = Owned(Path.Combine(root, GesManifestName(files)));
        Directory.CreateDirectory(root);
        var generated = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in files) generated.Add(file.Path);
        // The manifest, the last file, follows the removal of the files it no longer names: a run that fails before keeps
        // the previous manifest, which still owns them.
        for (var index = 0; index < files.Count - 1; index++) WriteChanged(Path.Combine(root, files[index].Path), files[index].Text);
        foreach (var name in Entries(root))
        {
            if (generated.Contains(name)) continue;
            var path = Path.Combine(root, name);
            if (owned.Contains(name) && File.Exists(path)) File.Delete(path);
            else Console.Error.WriteLine($"warning cli.unowned: {Path.Combine(directory, name)} is not generated and was left alone.");
        }

        WriteChanged(Path.Combine(root, files[files.Count - 1].Path), files[files.Count - 1].Text);

        return 0;
    }

    internal static int Check(string directory, IReadOnlyList<GesGeneratedFile> files)
    {
        var root = Path.GetFullPath(directory);
        var generated = new HashSet<string>(StringComparer.Ordinal);
        var failed = false;
        foreach (var file in files)
        {
            generated.Add(file.Path);
            var path = Path.Combine(root, file.Path);
            if (!File.Exists(path)) Report($"{Path.Combine(directory, file.Path)} does not exist.");
            else if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(StrictUtf8.GetBytes(file.Text))) Report($"{Path.Combine(directory, file.Path)} differs from the generated code.");
        }

        if (Directory.Exists(root))
            foreach (var name in Entries(root))
                if (!generated.Contains(name)) Report($"{Path.Combine(directory, name)} is not generated.");
        return failed ? 1 : 0;

        void Report(string message)
        {
            Console.Error.WriteLine("error cli.outdated: " + message);
            failed = true;
        }
    }

    // The manifest is the last file.
    private static string GesManifestName(IReadOnlyList<GesGeneratedFile> files) => files[files.Count - 1].Path;

    // The entries of the directory that a report names, ordered by name: neither hidden nor Unity metadata.
    private static List<string> Entries(string root)
    {
        var names = new List<string>();
        if (!Directory.Exists(root)) return names;
        foreach (var entry in Directory.EnumerateFileSystemEntries(root))
        {
            var name = Path.GetFileName(entry);
            if (name.StartsWith('.') || name.EndsWith(".meta", StringComparison.Ordinal)) continue;
            names.Add(name);
        }

        names.Sort(StringComparer.Ordinal);
        return names;
    }

    // The plain file names that a previous manifest lists; none when it is missing or unreadable, so that nothing is removed.
    private static HashSet<string> Owned(string manifest)
    {
        var owned = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(manifest));
            if (!document.RootElement.TryGetProperty("files", out var list) || list.ValueKind != JsonValueKind.Array) return owned;
            foreach (var item in list.EnumerateArray())
                if (item.GetString() is { } name && name.Length > 0 && name != "." && name != ".." && name.IndexOfAny(['/', '\\']) < 0) owned.Add(name);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
        }

        return owned;
    }
}
