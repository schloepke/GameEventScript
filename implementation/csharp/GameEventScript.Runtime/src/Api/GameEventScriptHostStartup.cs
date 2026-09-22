// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace GameEventScript.Api;

public sealed partial class GameEventScriptHost
{
    private readonly MessageRingQueue _startupQueue = new(4);
    private GameEventScriptStartResult? _startResult;
    private GameEventScriptStartResult? _initializationFailure;
    private int _initializationOpcodes, _initializationEmits, _initializationPublishes;
    private bool _starting;
    private bool _pumping;
    private bool _executingScript;
    private GameEventScriptInstance? _initializationInstance;
    private GameEventScriptDiagnostic? _initializationDiagnostic;
    private List<DeferredPublication>? _initializationPublications;
    private List<DeferredPublication>? _startupPublications;

    /// <summary>Gets whether the initial load group started successfully. Pending messages and later instance failures do not change this value.</summary>
    public bool IsReady => _startResult is { State: GameEventScriptStartState.Ready };

    /// <summary>Initializes the complete initial load group in load order without dispatching ordinary messages.</summary>
    /// <returns>The shared startup outcome. Repeated calls return the original result without running initialization again.</returns>
    /// <remarks>Register all programs and native handlers before calling this method. A failed start is terminal; create a new host to retry.</remarks>
    /// <exception cref="InvalidOperationException">The host is already executing a callback or initialization.</exception>
    public GameEventScriptStartResult Start()
    {
        if (_pumping || _starting) throw new InvalidOperationException("A host cannot be started recursively.");
        if (_startResult is { } existing) return existing;
        var opcodes = 0;
        var processed = 0;
        var emitted = 0;
        var published = 0;
        _starting = true;
        _pumping = true;
        try
        {
            while (_startupQueue.Count > 0 || _hasActiveMessage)
            {
                var execution = ExecuteFrameCore(int.MaxValue);
                opcodes += execution.ExecutedOpcodes;
                processed += execution.ProcessedMessages;
                emitted += execution.EmittedMessages;
                published += execution.PublishedMessages;
                if (execution.State is GameEventScriptExecutionState.RuntimeError or GameEventScriptExecutionState.RuntimeLimitReached)
                {
                    var state = execution.State == GameEventScriptExecutionState.RuntimeError
                        ? GameEventScriptStartState.RuntimeError : GameEventScriptStartState.RuntimeLimitReached;
                    var failure = new GameEventScriptStartResult(state, execution.Diagnostic ?? _initializationFailure?.Diagnostic ?? InitializationLimitDiagnostic(null), opcodes, processed, emitted, published);
                    _startResult = failure;
                    while (_instances is { } instance)
                    {
                        var prior = instance.StartResult;
                        instance.StartResult = new(state, failure.Diagnostic, prior?.ExecutedOpcodes ?? 0, prior?.ProcessedMessages ?? 0,
                            prior?.EmittedMessages ?? 0, prior?.PublishedMessages ?? 0);
                        instance.Detach();
                    }
                    _queue.Clear();
                    _delayed?.Clear();
                    _startupQueue.Clear();
                    _startupPublications?.Clear();
                    return failure;
                }
            }
            var ready = new GameEventScriptStartResult(GameEventScriptStartState.Ready, null, opcodes, processed, emitted, published);
            for (var instance = _instances; instance is not null; instance = instance.NextRegistration)
                instance.StartResult ??= new(GameEventScriptStartState.Ready);
            _startResult = ready;
            FlushPublications(_startupPublications);
            return ready;
        }
        finally
        {
            _starting = false;
            _pumping = false;
        }
    }

    private void CompleteInitialization()
    {
        if (_initializationInstance is not { } instance) return;
        instance.StartResult = new(GameEventScriptStartState.Ready, null, _initializationOpcodes, 1, _initializationEmits, _initializationPublishes);
        _initializationInstance = null;
        _initializationDiagnostic = null;
        if (_starting && _initializationPublications is { Count: > 0 } publications)
        {
            _startupPublications ??= new();
            _startupPublications.AddRange(publications);
            publications.Clear();
        }
        else FlushPublications(_initializationPublications);
    }

    private void FailInitialization(GameEventScriptStartState state)
    {
        if (_initializationInstance is not { } instance) return;
        instance.StartResult = new(state, _initializationDiagnostic ?? InitializationLimitDiagnostic(instance), _initializationOpcodes, 0, _initializationEmits, _initializationPublishes);
        _initializationFailure = instance.StartResult;
        instance.Detach();
        _initializationPublications?.Clear();
        _initializationInstance = null;
        _initializationDiagnostic = null;
        _hasActiveMessage = false;
        _queue.RemoveFailedRecipients();
        RemoveFailedDelayed();
    }

    private static GameEventScriptDiagnostic InitializationLimitDiagnostic(GameEventScriptInstance? instance)
        => new(GameEventScriptDiagnosticPhase.Runtime, GameEventScriptDiagnosticCodes.RuntimeInitializationLimitReached, "Initialization exceeded a runtime limit.",
            ProgramName: instance?.Program.ModuleName, HandlerName: "initialization()");

    private void FlushPublications(List<DeferredPublication>? publications)
    {
        if (publications is null) return;
        foreach (var publication in publications)
        {
            var attempted = _publishSink is not null;
            var accepted = false;
            if (attempted)
            {
                try { accepted = _publishSink!.Publish(publication.Message); }
                catch (Exception exception)
                {
                    _observer?.RuntimeError(new GameEventScriptDiagnostic(GameEventScriptDiagnosticPhase.Runtime,
                        GameEventScriptDiagnosticCodes.RuntimePublishSinkFailure, "Publish sink failed.",
                        ProgramName: publication.Instance.Program.ModuleName, HandlerName: "initialization()",
                        TechnicalDetails: GameEventScriptRuntimeExceptionText.Describe(exception)));
                }
            }
            _observer?.MessagePublished(publication.Message, new(publication.LocalAccepted, attempted, accepted));
        }
        publications.Clear();
    }

    private readonly record struct DeferredPublication(GameEventScriptMessage Message, bool LocalAccepted, GameEventScriptInstance Instance);
}
