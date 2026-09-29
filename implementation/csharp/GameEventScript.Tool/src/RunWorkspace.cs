// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text;
using GameEventScript.Api;

namespace GameEventScript.Tool;

// Editable source snapshots are session state, never part of the portable Host.
internal sealed class RunWorkspace
{
    internal sealed class Draft(int id, string[] paths, string[] text, byte[][] baseline)
    {
        internal int Id = id;
        internal string[] Paths = paths;
        internal string[] Text = text;
        internal byte[][] Baseline = baseline;
        internal string[]? Applied;
        internal bool Dirty => Id == 0 ? Text.Any(value => value.Length != 0) : Text.Where((value, index) => value != Decode(Baseline[index])).Any();
        internal bool Pending => Applied is null || !Text.SequenceEqual(Applied);
        internal Draft ReadFromDisk()
        {
            var bytes = Paths.Select(File.ReadAllBytes).ToArray();
            return new Draft(Id, [.. Paths], bytes.Select(Decode).ToArray(), bytes);
        }
        internal GameEventScriptProgram Compile()
        {
            var builder = GameEventScriptBuilder.Create();
            for (var index = 0; index < Text.Length; index++) builder.AddScript(Text[index], Paths[index]);
            return builder.Compile();
        }
    }

    internal readonly Dictionary<int, Draft> Drafts = [];
    internal static bool IsScratch(string selector) => selector is "" or "0" or "@0";
    internal static string Decode(byte[] bytes)
    {
        var text = new UTF8Encoding(false, true).GetString(bytes);
        return text.StartsWith('\uFEFF') ? text[1..] : text;
    }

    internal Draft Get(RunInventory inventory, string selector)
    {
        if (IsScratch(selector)) return Drafts.GetValueOrDefault(0) ?? throw new ArgumentException("No scratch found. Use :edit to create one.");
        if (int.TryParse(selector.TrimStart('@'), out var id) && Drafts.TryGetValue(id, out var draft)) return draft;
        var entry = inventory.SelectProgram(selector, ":edit");
        if (Drafts.TryGetValue(entry.Id, out draft)) return draft;
        if (entry.Paths.Any(path => !path.EndsWith(".ges", StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Only loaded .ges source programs can be edited; binary programs are read-only.");
        var sources = entry.Instance.Program.SourceArchive?.Sources;
        if (sources is not { } sourceEntries || sourceEntries.Count != entry.Paths.Length) throw new ArgumentException("The loaded program has no complete source archive. Reload its source files first.");
        var text = sourceEntries.Select(source => source.ResolveText()).ToArray();
        var baseline = entry.Paths.Select(File.ReadAllBytes).ToArray();
        if (!text.SequenceEqual(baseline.Select(Decode))) throw new ArgumentException("Sources changed on disk since loading. Reload before starting an edit.");
        draft = new Draft(entry.Id, [.. entry.Paths], text, baseline) { Applied = [.. text] };
        Drafts.Add(entry.Id, draft);
        return draft;
    }

    internal bool Edit(RunSession session, string argument)
    {
        var words = Words(argument);
        if (words.Count > 2) throw new ArgumentException("Use :edit [program-ID|module] [source-number].");
        var selector = words.Count == 0 ? "0" : words[0];
        if (IsScratch(selector) && !Drafts.ContainsKey(0)) Drafts.Add(0, new Draft(0, ["<scratch>"], [""], [[]]));
        var draft = Get(session.Inventory, selector);
        var index = 0;
        if (words.Count == 2)
        {
            if (!int.TryParse(words[1], out var number) || number < 1 || number > draft.Text.Length) throw new ArgumentException("Source number is outside this program's source list.");
            index = number - 1;
        }
        else if (draft.Text.Length != 1) throw new ArgumentException("Select a source with :edit " + selector + " <source-number>: " + string.Join(", ", draft.Paths.Select((path, i) => $"{i + 1}: {path}")));
        var editor = Environment.GetEnvironmentVariable("GES_EDITOR");
        if (string.IsNullOrWhiteSpace(editor)) editor = Environment.GetEnvironmentVariable("VISUAL");
        if (string.IsNullOrWhiteSpace(editor)) editor = Environment.GetEnvironmentVariable("EDITOR");
        if (string.IsNullOrWhiteSpace(editor)) editor = OperatingSystem.IsWindows() ? "notepad.exe" : "nano";
        var command = Words(editor);
        if (command.Count == 0) throw new ArgumentException("The editor command is empty.");
        var directory = Path.Combine(Path.GetTempPath(), "ges-edit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "edit.ges");
            File.WriteAllText(path, draft.Text[index], new UTF8Encoding(false));
            var start = new ProcessStartInfo(command[0]) { UseShellExecute = false };
            foreach (var option in command.Skip(1)) start.ArgumentList.Add(option);
            start.ArgumentList.Add(path);
            using var process = Process.Start(start) ?? throw new IOException("Could not start the editor.");
            process.WaitForExit();
            draft.Text[index] = Decode(File.ReadAllBytes(path));
            if (process.ExitCode != 0) throw new ArgumentException($"Editor exited with code {process.ExitCode}. The returned draft was retained; it was not reloaded.");
        }
        catch (System.ComponentModel.Win32Exception exception) { throw new IOException("Could not launch editor. Set GES_EDITOR to an installed editor command: " + exception.Message, exception); }
        finally { Directory.Delete(directory, true); }
        if (!draft.Pending || draft.Id == 0 && !draft.Dirty && draft.Applied is null) return true;
        string? activePath = null;
        return session.Reload(ref activePath, fromMemory: true);
    }

    internal void Save(RunInventory inventory, string argument)
    {
        var words = Words(argument);
        if (words.Count > 2) throw new ArgumentException("Use :save [program-ID|module] or :save 0 \"file.ges\".");
        var selected = words.Count == 0 ? Drafts.Values.Where(d => d.Id != 0 && d.Dirty).ToArray() : [Get(inventory, words[0])];
        string? newPath = null;
        if (words.Count == 2)
        {
            if (selected[0].Id != 0) throw new ArgumentException("A new save path is only supported for scratch 0.");
            newPath = Path.GetFullPath(words[1]);
            if (!newPath.EndsWith(".ges", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Save scratch to a .ges source file.");
        }
        if (selected.Any(d => d.Id == 0) && newPath is null) throw new ArgumentException("Scratch 0 requires :save 0 \"file.ges\".");
        var writes = new List<(Draft Draft, int Index, string Path, byte[] Bytes)>();
        foreach (var draft in selected)
        {
            for (var index = 0; index < draft.Text.Length; index++)
            {
                if (draft.Id != 0 && draft.Text[index] == Decode(draft.Baseline[index])) continue;
                var path = newPath ?? Path.GetFullPath(draft.Paths[index]);
                if (writes.Any(write => string.Equals(write.Path, path, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)))
                    throw new ArgumentException("Several modified programs refer to the same file. Save them separately: " + path);
                if (draft.Id == 0 ? File.Exists(path) || Directory.Exists(path) : !File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(draft.Baseline[index]))
                    throw new ArgumentException("Save conflict: the file exists already or changed on disk: " + path);
                writes.Add((draft, index, path, Encoding.UTF8.GetBytes(draft.Text[index])));
            }
        }
        foreach (var write in writes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(write.Path)!);
            var temporary = write.Path + ".ges-save-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllBytes(temporary, write.Bytes);
                File.Move(temporary, write.Path, overwrite: write.Draft.Id != 0);
                write.Draft.Baseline[write.Index] = write.Bytes;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        if (newPath is not null)
        {
            var draft = selected[0];
            Drafts.Remove(0);
            draft.Paths = [newPath];
            draft.Id = inventory.PromoteScratch(newPath);
            Drafts.Add(draft.Id, draft);
            Console.Error.WriteLine($"Saved scratch as program {draft.Id}: {newPath}");
        }
        else Console.Error.WriteLine($"Saved {writes.Count} source file(s).");
        if (words.Count == 0 && Drafts.TryGetValue(0, out var scratch) && scratch.Dirty) Console.Error.WriteLine("Scratch 0 is unsaved. Use :save 0 \"file.ges\".");
    }

    internal bool CanQuit()
    {
        var dirty = Drafts.Values.Where(d => d.Dirty).Select(d => d.Id).Order().ToArray();
        if (dirty.Length == 0) return true;
        Console.Error.WriteLine("Unsaved changes in programs: " + string.Join(", ", dirty) + ". Use :save, or :quit! to discard and exit.");
        if (dirty.Contains(0)) Console.Error.WriteLine("Scratch 0 requires :save 0 \"file.ges\".");
        return false;
    }

    internal static List<string> Words(string text)
    {
        var result = new List<string>();
        var word = new StringBuilder();
        var quote = '\0';
        foreach (var c in text)
        {
            if (quote != '\0') { if (c == quote) quote = '\0'; else word.Append(c); }
            else if (c is '\'' or '"') quote = c;
            else if (char.IsWhiteSpace(c)) { if (word.Length > 0) { result.Add(word.ToString()); word.Clear(); } }
            else word.Append(c);
        }
        if (quote != '\0') throw new ArgumentException("Unterminated quoted argument.");
        if (word.Length > 0) result.Add(word.ToString());
        return result;
    }
}
