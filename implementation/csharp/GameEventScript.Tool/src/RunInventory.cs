// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using GameEventScript.Api;
using static GameEventScript.Api.GameEventScriptBindingSegment;

namespace GameEventScript.Tool;

internal sealed class RunInventory(GameEventScriptHost host, RunWorkspace workspace, int nextId = 1)
{
    private readonly List<LoadedProgram> _programs = [];
    private readonly List<NativeHandler> _nativeHandlers = [];

    internal int NextId { get; private set; } = nextId;

    internal GameEventScriptInstance Load(GameEventScriptProgram program, IReadOnlyList<string> paths, int? id = null)
    {
        var instance = host.Load(program);
        var assignedId = id ?? NextId;
        _programs.Add(new LoadedProgram(assignedId, instance, [.. paths]));
        NextId = Math.Max(NextId, assignedId + 1);
        return instance;
    }

    internal void Unload(string selector)
    {
        if (int.TryParse(selector.TrimStart('@'), out var id) && workspace.Drafts.ContainsKey(id) && !ActivePrograms().Any(entry => entry.Id == id))
        {
            RemoveDraft(selector);
            return;
        }
        var entry = SelectProgram(selector, ":unload");
        RemoveDraft(selector);
        entry.Instance.Detach();
        _programs.Remove(entry);
    }

    internal int UnloadAll()
    {
        var count = ActivePrograms().Length;
        foreach (var entry in _programs) entry.Instance.Detach();
        _programs.Clear();
        return count;
    }

    internal GameEventScriptSubscription SubscribeMessageName(string name, IGameEventScriptNativeMessageHandler handler)
    {
        var subscription = host.SubscribeMessageName(name, handler);
        _nativeHandlers.Add(new NativeHandler(name, subscription));
        return subscription;
    }

    internal void WritePrograms()
    {
        var programs = ActivePrograms();
        Console.Error.WriteLine();
        Console.Error.WriteLine($"Loaded programs ({programs.Length}):");
        foreach (var entry in programs)
        {
            Console.Error.WriteLine($"  {entry.Id}  {(entry.Id == 0 ? "scratch [no file]" : entry.Instance.Program.ModuleName)}  version={entry.Instance.Program.ProgramVersion}  handlers={Handlers(entry).Count()}{DraftStatus(entry.Id)}");
            Console.Error.WriteLine("      files: " + string.Join(", ", entry.Paths.Select(path => JsonSerializer.Serialize(path, ToolJsonContext.Default.String))));
        }
        foreach (var draft in workspace.Drafts.Values.Where(d => !programs.Any(e => e.Id == d.Id)))
            Console.Error.WriteLine($"  {draft.Id}  {(draft.Id == 0 ? "scratch [no file]" : string.Join(", ", draft.Paths))}{DraftStatus(draft.Id)}");
        if (programs.Length == 0 && workspace.Drafts.Count == 0) Console.Error.WriteLine("  No programs loaded. Use :load <file>.");
        Console.Error.WriteLine();
    }

    internal void WriteHandlers()
    {
        var lines = new List<string>();
        foreach (var handler in _nativeHandlers)
        {
            if (handler.Subscription.IsSubscribed) lines.Add($"  native  {handler.Name}(...) [name-only]");
        }
        foreach (var entry in ActivePrograms())
        {
            var strings = entry.Instance.Program.StringConstants;
            foreach (var binding in Handlers(entry))
            {
                var name = strings.Resolve(binding.Name);
                var signature = binding.Kind == GameEventScriptBinaryBindKind.MessageNameHandler
                    ? name + "(...) [name-only]"
                    : GameEventScriptMessageSignature.CreateSignatureId(name, binding.ArgumentNames.Select(index => strings.Resolve(index))) + " [signature]";
                var tags = Tags(" matching ", binding.RequiredTags, strings) + Tags(" without ", binding.ExcludedTags, strings);
                lines.Add($"  {entry.Id}  {entry.Instance.Program.ModuleName}  {signature}{tags}");
            }
        }
        Console.Error.WriteLine();
        Console.Error.WriteLine($"Registered handlers ({lines.Count}):");
        foreach (var line in lines) Console.Error.WriteLine(line);
        Console.Error.WriteLine();
    }

    internal void WriteDump(string selector, bool color)
    {
        if (RunWorkspace.IsScratch(selector)) selector = "0";
        var draft = FindDraft(selector);
        if (selector == "0" && draft is null) throw new ArgumentException("No scratch found. Use :edit to create one.");
        if (draft is not null && draft.Applied is null) throw new ArgumentException("No successful compile is available for this draft.");
        if (draft?.Pending == true) Console.Error.WriteLine("Draft differs from the last successful compile; showing the previous compiled program.");
        var text = SelectProgram(selector, ":dump").Instance.Program.Dump();
        if (!Console.IsErrorRedirected) text = ToolTextDisplay.ExpandTabs(text);
        Console.Error.WriteLine();
        Console.Error.Write(color ? RunHighlighting.RenderAssembly(text) : text);
        Console.Error.WriteLine();
    }

    internal void WriteSource(string selector, bool color)
    {
        if (RunWorkspace.IsScratch(selector)) selector = "0";
        var draft = FindDraft(selector);
        if (selector == "0" && draft is null) throw new ArgumentException("No scratch found. Use :edit to create one.");
        if (draft is not null)
        {
            for (var index = 0; index < draft.Text.Length; index++)
            {
                var header = "// Source: " + JsonSerializer.Serialize(draft.Paths[index], ToolJsonContext.Default.String);
                Console.Error.WriteLine(color ? RunHighlighting.Paint(header, 90) : header);
                var text = Console.IsErrorRedirected ? draft.Text[index] : ToolTextDisplay.ExpandTabs(draft.Text[index]);
                Console.Error.WriteLine(color ? RunHighlighting.Render(text) : text);
            }
            return;
        }
        var entry = SelectProgram(selector, ":source");
        Console.Error.WriteLine();
        if (entry.Instance.Program.SourceArchive is not { Sources.Count: > 0 } archive)
        {
            Console.Error.WriteLine($"No embedded sources available for {entry.Id} ({entry.Instance.Program.ModuleName}). Compile with source archive/debug information to include them.");
            Console.Error.WriteLine();
            return;
        }
        foreach (var source in archive.Sources)
        {
            var header = "// Source: " + JsonSerializer.Serialize(source.SourceName, ToolJsonContext.Default.String);
            Console.Error.WriteLine(color ? RunHighlighting.Paint(header, 90) : header);
            var text = source.ResolveText();
            if (!Console.IsErrorRedirected) text = ToolTextDisplay.ExpandTabs(text);
            Console.Error.Write(color ? RunHighlighting.Render(text) : text);
            if (!text.EndsWith('\n') && !text.EndsWith('\r')) Console.Error.WriteLine();
            Console.Error.WriteLine();
        }
    }

    internal LoadedProgram SelectProgram(string selector, string command)
    {
        if (selector.Length == 0 || selector.Any(char.IsWhiteSpace)) throw new ArgumentException($"Specify one module name or program ID after {command}. Use :list to see loaded programs.");
        var programs = ActivePrograms();
        LoadedProgram[] matches;
        if (selector.StartsWith('@') || selector.All(character => character is >= '0' and <= '9'))
        {
            if (!int.TryParse(selector.AsSpan(selector.StartsWith('@') ? 1 : 0), NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id < 0)
                throw new ArgumentException($"A program ID must be a positive integer, for example {command} 1.");
            matches = programs.Where(entry => entry.Id == id).ToArray();
        }
        else matches = programs.Where(entry => string.Equals(entry.Instance.Program.ModuleName, selector, StringComparison.Ordinal)).ToArray();
        if (matches.Length == 0) throw new ArgumentException($"No loaded program matches '{selector}'. Use :list.");
        if (matches.Length > 1)
        {
            var choices = string.Join(", ", matches.Select(entry => $"{entry.Id}"));
            throw new ArgumentException($"Module '{selector}' is loaded more than once. Use {command} with one of: {choices}.");
        }
        return matches[0];
    }

    internal RunWorkspace.Draft? FindDraft(string selector)
    {
        if (int.TryParse(selector.TrimStart('@'), out var id)) return workspace.Drafts.GetValueOrDefault(id);
        return workspace.Drafts.GetValueOrDefault(SelectProgram(selector, ":source").Id);
    }

    private string DraftStatus(int id) => workspace.Drafts.TryGetValue(id, out var draft) ? (draft.Dirty ? " *" : "") + (draft.Pending ? " [draft not applied]" : "") : "";

    internal int PromoteScratch(string path)
    {
        var id = NextId++;
        var index = _programs.FindIndex(entry => entry.Id == 0);
        if (index >= 0) _programs[index] = _programs[index] with { Id = id, Paths = [path] };
        return id;
    }

    internal void RemoveDraft(string selector)
    {
        var draft = FindDraft(selector);
        if (draft?.Dirty == true) throw new ArgumentException("Unsaved changes. Save before unloading, or use :quit! to discard the session.");
        if (draft is not null) workspace.Drafts.Remove(draft.Id);
    }

    internal LoadedProgram[] ActivePrograms() => _programs.Where(entry => entry.Instance.IsAttached).ToArray();

    private static IEnumerable<GameEventScriptBinaryBindEntry> Handlers(LoadedProgram entry)
        => entry.Instance.Program.Bindings.Entries.Where(binding => binding.Kind is GameEventScriptBinaryBindKind.MessageHandler or GameEventScriptBinaryBindKind.MessageNameHandler
            && entry.Instance.Program.StringConstants.Resolve(binding.Name) != "initialization");

    private static string Tags(string prefix, GameEventScriptReadOnlyArray<ushort> tags, GameEventScriptStringConstantSegment strings)
        => tags.Count == 0 ? string.Empty : prefix + string.Join(", ", tags.Select(index => "#" + strings.Resolve(index)));

    internal sealed record LoadedProgram(int Id, GameEventScriptInstance Instance, string[] Paths);
    private sealed record NativeHandler(string Name, GameEventScriptSubscription Subscription);
}
