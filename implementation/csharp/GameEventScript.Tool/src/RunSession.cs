// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;

namespace GameEventScript.Tool;

internal sealed class RunSession
{
    private readonly long? _seed;
    private readonly GameEventScriptRuntimeLimits _limits;
    private readonly bool _verbose, _color, _interactive;
    internal GameEventScriptHost Host { get; private set; }
    internal RunObserver Observer { get; private set; }
    internal RunWorkspace Workspace { get; }
    internal RunInventory Inventory { get; private set; }

    internal RunSession(long? seed, GameEventScriptRuntimeLimits limits, bool verbose, bool color, bool interactive, int nextId = 1, RunWorkspace? workspace = null, IGameEventScriptProfiler? profiler = null)
    {
        Workspace = workspace ?? new RunWorkspace();
        _seed = seed;
        _limits = limits;
        _verbose = verbose;
        _color = color;
        _interactive = interactive;
        Observer = new RunObserver(verbose, color, interactive);
        var builder = GameEventScriptHost.CreateBuilder().WithRuntimeObserver(Observer).WithRuntimeLimits(limits);
        if (profiler is not null) builder.WithProfiler(profiler);
        if (seed is { } configuredSeed) builder.WithRandomSeed(configuredSeed);
        Host = builder.Build();
        Inventory = new RunInventory(Host, Workspace, nextId);
        Observer.SubscribeConsoleHandlers(Inventory);
    }

    internal bool Reload(ref string? activePath, bool fromMemory = false)
    {
        var replacement = new RunSession(_seed, _limits, _verbose, _color, _interactive, Inventory.NextId, Workspace);
        var refreshed = new Dictionary<int, RunWorkspace.Draft>();
        RunWorkspace.Draft Prepare(RunWorkspace.Draft draft)
        {
            if (fromMemory || draft.Id == 0 || draft.Dirty) return draft;
            var current = draft.ReadFromDisk();
            refreshed.Add(draft.Id, current);
            return current;
        }
        // Link the entire group before initialization, preserving the old session on preparation failure.
        foreach (var entry in Inventory.ActivePrograms())
        {
            activePath = entry.Paths[0];
            var program = Workspace.Drafts.TryGetValue(entry.Id, out var draft) ? Prepare(draft).Compile() : fromMemory ? entry.Instance.Program : entry.Paths.Length == 1
                ? RunProgramFiles.ReadOne(activePath)
                : RunProgramFiles.Compile(entry.Paths, ref activePath);
            replacement.Inventory.Load(program, entry.Paths, entry.Id);
        }
        foreach (var draft in Workspace.Drafts.Values.Where(d => !Inventory.ActivePrograms().Any(e => e.Id == d.Id) && (d.Id != 0 || d.Dirty)))
            replacement.Inventory.Load(Prepare(draft).Compile(), draft.Paths, draft.Id);
        if (!replacement.Pump()) return false;
        foreach (var (id, draft) in refreshed) Workspace.Drafts[id] = draft;
        foreach (var draft in Workspace.Drafts.Values.Where(d => replacement.Inventory.ActivePrograms().Any(e => e.Id == d.Id))) draft.Applied = [.. draft.Text];
        Inventory.UnloadAll();
        Host = replacement.Host;
        Observer = replacement.Observer;
        Inventory = replacement.Inventory;
        _processedMessages += replacement._processedMessages;
        _opcodes += replacement._opcodes;
        _emits += replacement._emits;
        _publishes += replacement._publishes;
        return true;
    }

    private long _processedMessages;
    private long _opcodes;
    private long _emits;
    private long _publishes;

    internal bool Pump()
    {
        if (!Host.IsReady)
        {
            var start = Host.Start();
            _processedMessages += start.ProcessedMessages;
            _opcodes += start.ExecutedOpcodes;
            _emits += start.EmittedMessages;
            _publishes += start.PublishedMessages;
            if (start.State != GameEventScriptStartState.Ready) return false;
        }
        GameEventScriptExecutionResult result;
        do
        {
            result = Host.RunToCompletion();
            _processedMessages += result.ProcessedMessages;
            _opcodes += result.ExecutedOpcodes;
            _emits += result.EmittedMessages;
            _publishes += result.PublishedMessages;
            if (Observer.OutputError is { } outputError)
            {
                Console.Error.WriteLine($"error cli.io: {outputError.Message}");
                return false;
            }
            if (Observer.Failed) return false;
            if (!_interactive && result.State == GameEventScriptExecutionState.Waiting && Host.NextMessageDelay is { } delay)
                Thread.Sleep((int)Math.Min(1000, delay / 1000 + (delay % 1000 == 0 ? 0 : 1)));
        } while (!_interactive && result.State == GameEventScriptExecutionState.Waiting);
        return result.State is GameEventScriptExecutionState.Completed or GameEventScriptExecutionState.Waiting;
    }

    internal void WriteSummary()
        => Console.Error.WriteLine($"Run completed: {_processedMessages} messages, {_opcodes} opcodes, {_emits} emits, {_publishes} publishes.");
}
