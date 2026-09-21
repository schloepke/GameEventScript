// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using GameEventScript.Api;
using static GameEventScript.Api.GameEventScriptBindingSegment;

namespace GameEventScript.Tool;

internal sealed class RunInventory(GameEventScriptHost host, int nextId = 1)
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
        var entry = SelectProgram(selector, ":unload");
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
            Console.Error.WriteLine($"  @{entry.Id}  {entry.Instance.Program.ModuleName}  version={entry.Instance.Program.ProgramVersion}  handlers={Handlers(entry).Count()}");
            Console.Error.WriteLine("      files: " + string.Join(", ", entry.Paths.Select(path => JsonSerializer.Serialize(path))));
        }
        if (programs.Length == 0) Console.Error.WriteLine("  No programs loaded. Use :load <file>.");
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
                lines.Add($"  @{entry.Id}  {entry.Instance.Program.ModuleName}  {signature}{tags}");
            }
        }
        Console.Error.WriteLine();
        Console.Error.WriteLine($"Registered handlers ({lines.Count}):");
        foreach (var line in lines) Console.Error.WriteLine(line);
        Console.Error.WriteLine();
    }

    internal void WriteDump(string selector, bool color)
    {
        var text = SelectProgram(selector, ":dump").Instance.Program.Dump();
        if (!Console.IsErrorRedirected) text = ToolTextDisplay.ExpandTabs(text);
        Console.Error.WriteLine();
        Console.Error.Write(color ? RunHighlighting.RenderAssembly(text) : text);
        Console.Error.WriteLine();
    }

    internal void WriteSource(string selector, bool color)
    {
        var entry = SelectProgram(selector, ":source");
        Console.Error.WriteLine();
        if (entry.Instance.Program.SourceArchive is not { Sources.Count: > 0 } archive)
        {
            Console.Error.WriteLine($"No embedded sources available for @{entry.Id} ({entry.Instance.Program.ModuleName}). Compile with source archive/debug information to include them.");
            Console.Error.WriteLine();
            return;
        }
        foreach (var source in archive.Sources)
        {
            var header = "// Source: " + JsonSerializer.Serialize(source.SourceName);
            Console.Error.WriteLine(color ? RunHighlighting.Paint(header, 90) : header);
            var text = source.ResolveText();
            if (!Console.IsErrorRedirected) text = ToolTextDisplay.ExpandTabs(text);
            Console.Error.Write(color ? RunHighlighting.Render(text) : text);
            if (!text.EndsWith('\n') && !text.EndsWith('\r')) Console.Error.WriteLine();
            Console.Error.WriteLine();
        }
    }

    private LoadedProgram SelectProgram(string selector, string command)
    {
        if (selector.Length == 0 || selector.Any(char.IsWhiteSpace)) throw new ArgumentException($"Specify one module name or @program-id after {command}. Use :list to see loaded programs.");
        var programs = ActivePrograms();
        LoadedProgram[] matches;
        if (selector.StartsWith('@'))
        {
            if (!int.TryParse(selector.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
                throw new ArgumentException($"A program selector uses @ followed by a positive integer, for example {command} @1.");
            matches = programs.Where(entry => entry.Id == id).ToArray();
        }
        else matches = programs.Where(entry => string.Equals(entry.Instance.Program.ModuleName, selector, StringComparison.Ordinal)).ToArray();
        if (matches.Length == 0) throw new ArgumentException($"No loaded program matches '{selector}'. Use :list.");
        if (matches.Length > 1)
        {
            var choices = string.Join(", ", matches.Select(entry => $"@{entry.Id}"));
            throw new ArgumentException($"Module '{selector}' is loaded more than once. Use {command} with one of: {choices}.");
        }
        return matches[0];
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
