// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;

namespace GameEventScript.Tool;

internal sealed class RunSession(GameEventScriptHost host, RunObserver observer)
{
    internal RunInventory Inventory { get; } = new(host);

    private long _processedMessages;
    private long _opcodes;
    private long _emits;
    private long _publishes;

    internal bool Pump()
    {
        if (!host.IsReady)
        {
            var start = host.Start();
            _processedMessages += start.ProcessedMessages;
            _opcodes += start.ExecutedOpcodes;
            _emits += start.EmittedMessages;
            _publishes += start.PublishedMessages;
            if (start.State != GameEventScriptStartState.Ready) return false;
        }
        var result = host.RunToCompletion();
        _processedMessages += result.ProcessedMessages;
        _opcodes += result.ExecutedOpcodes;
        _emits += result.EmittedMessages;
        _publishes += result.PublishedMessages;
        if (observer.OutputError is { } outputError)
        {
            Console.Error.WriteLine($"error cli.io: {outputError.Message}");
            return false;
        }
        return !observer.Failed && result.State == GameEventScriptExecutionState.Completed;
    }

    internal void WriteSummary()
        => Console.Error.WriteLine($"Run completed: {_processedMessages} messages, {_opcodes} opcodes, {_emits} emits, {_publishes} publishes.");
}
