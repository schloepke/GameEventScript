#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using StepH.GameEventScript.Runtime;

namespace StepH.GameEventScript.Api;

public sealed class GameEventScriptHost
{
    private const int NormalPriority = 0;

    private readonly GameEventScriptRuntimeHost _runtime;
    private readonly IGameEventScriptRuntimeGate? _runtimeGate;

    internal GameEventScriptHost(
        GameEventScriptRandomGenerator random,
        IGameEventScriptRuntimeObserver? runtimeObserver,
        IGameEventScriptExtensionRegistry? extensionRegistry,
        IGameEventScriptExternalTypeRegistry? externalTypeRegistry,
        GameEventScriptRuntimeLimits? runtimeLimits,
        IGameEventScriptDispatcher? dispatcher = null,
        IGameEventScriptRuntimeGate? runtimeGate = null,
        Func<GameEventScriptMessage, bool>? publishHook = null)
    {
        _runtimeGate = runtimeGate;
        _runtime = new GameEventScriptRuntimeHost(
            random,
            runtimeObserver,
            extensionRegistry,
            externalTypeRegistry,
            runtimeLimits,
            dispatcher,
            runtimeGate,
            publishHook);
    }

    public static GameEventScriptHostBuilder CreateBuilder() => new();

    public GameEventScriptHost Load(IGameEventScriptModule module, int priority = NormalPriority)
    {
        EnterCore();
        try { _runtime.Load(module, priority); }
        finally { ExitCore(); }
        return this;
    }

    public GameEventScriptHost Subscribe(string message, IReadOnlyCollection<string> parameterNames, Action<GameEventScriptMessage, GameEventScriptSession> handler, int priority = NormalPriority)
    {
        EnterCore();
        try { _runtime.Subscribe(message, parameterNames, handler, priority); }
        finally { ExitCore(); }
        return this;
    }

    public GameEventScriptHost Subscribe(GameEventScriptMessageSignature signature, Action<GameEventScriptMessage, GameEventScriptSession> handler, int priority = NormalPriority)
    {
        EnterCore();
        try { _runtime.Subscribe(signature, handler, priority); }
        finally { ExitCore(); }
        return this;
    }

    public GameEventScriptHost Subscribe(GameEventScriptMessageHandlerDescriptor handler, int priority = NormalPriority)
    {
        EnterCore();
        try { _runtime.Subscribe(handler, priority); }
        finally { ExitCore(); }
        return this;
    }

    public GameEventScriptHost Subscribe(
        GameEventScriptMessageSignature signature,
        Action<GameEventScriptMessage, GameEventScriptSession> handler,
        IReadOnlyCollection<string>? matchingTags,
        IReadOnlyCollection<string>? withoutTags = null,
        int priority = NormalPriority)
    {
        EnterCore();
        try { _runtime.Subscribe(signature, handler, matchingTags, withoutTags, priority); }
        finally { ExitCore(); }
        return this;
    }

    public GameEventScriptHost Subscribe(IGameEventScriptModule module, int priority = NormalPriority)
    {
        EnterCore();
        try { _runtime.Subscribe(module, priority); }
        finally { ExitCore(); }
        return this;
    }

    public GameEventScriptSession StartSession() => RunCore(_runtime.StartSession);

    public bool Publish(GameEventScriptMessage message) => RunCore(() => _runtime.Publish(message));

    public bool PublishToCompletion(GameEventScriptMessage message) => RunCore(() => _runtime.PublishToCompletion(message));

    public GameEventScriptRun? Dispatch() => RunCore(_runtime.Dispatch);

    public GameEventScriptRunStepResult Update(int maxOpcodes) => RunCore(() => _runtime.Update(maxOpcodes));

    public GameEventScriptRun BeginRun(GameEventScriptMessage message) => RunCore(() => _runtime.BeginRun(message));

    private T RunCore<T>(Func<T> action)
    {
        EnterCore();
        try { return action(); }
        finally { ExitCore(); }
    }

    private void EnterCore() => _runtimeGate?.Enter();

    private void ExitCore() => _runtimeGate?.Exit();
}
