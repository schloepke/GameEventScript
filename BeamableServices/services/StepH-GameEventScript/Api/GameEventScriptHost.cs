#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using StepH.GameEventScript.Runtime;

namespace StepH.GameEventScript.Api;

public sealed class GameEventScriptHost
{
    private const int NormalPriority = 0;

    private readonly GameEventScriptRuntimeHost _runtime;

    internal GameEventScriptHost(
        GameEventScriptRandomGenerator random,
        IGameEventScriptRuntimeObserver? runtimeObserver,
        IGameEventScriptExtensionRegistry? extensionRegistry,
        IGameEventScriptExternalTypeRegistry? externalTypeRegistry,
        GameEventScriptRuntimeLimits? runtimeLimits,
        GameEventScriptDispatchMode dispatchMode = GameEventScriptDispatchMode.Manual,
        IGameEventScriptDispatcher? dispatcher = null,
        Func<GameEventScriptMessage, bool>? publishHook = null)
    {
        _runtime = new GameEventScriptRuntimeHost(
            random,
            runtimeObserver,
            extensionRegistry,
            externalTypeRegistry,
            runtimeLimits,
            dispatchMode,
            dispatcher,
            publishHook);
    }

    public static GameEventScriptHostBuilder CreateBuilder() => new();

    public GameEventScriptHost Load(IGameEventScriptModule module, int priority = NormalPriority)
    {
        _runtime.Load(module, priority);
        return this;
    }

    public GameEventScriptHost Subscribe(string message, IReadOnlyCollection<string> parameterNames, Action<GameEventScriptMessage, GameEventScriptSession> handler, int priority = NormalPriority)
    {
        _runtime.Subscribe(message, parameterNames, handler, priority);
        return this;
    }

    public GameEventScriptHost Subscribe(GameEventScriptMessageSignature signature, Action<GameEventScriptMessage, GameEventScriptSession> handler, int priority = NormalPriority)
    {
        _runtime.Subscribe(signature, handler, priority);
        return this;
    }

    public GameEventScriptHost Subscribe(GameEventScriptMessageHandlerDescriptor handler, int priority = NormalPriority)
    {
        _runtime.Subscribe(handler, priority);
        return this;
    }

    public GameEventScriptHost Subscribe(
        GameEventScriptMessageSignature signature,
        Action<GameEventScriptMessage, GameEventScriptSession> handler,
        IReadOnlyCollection<string>? matchingTags,
        IReadOnlyCollection<string>? withoutTags = null,
        int priority = NormalPriority)
    {
        _runtime.Subscribe(signature, handler, matchingTags, withoutTags, priority);
        return this;
    }

    public GameEventScriptHost Subscribe(IGameEventScriptModule module, int priority = NormalPriority)
    {
        _runtime.Subscribe(module, priority);
        return this;
    }

    public GameEventScriptSession StartSession()
        => _runtime.StartSession();

    public bool Publish(GameEventScriptMessage message)
        => _runtime.Publish(message);

    public bool PublishToCompletion(GameEventScriptMessage message)
        => _runtime.PublishToCompletion(message);

    public GameEventScriptRunStepResult Update(int maxOpcodes)
        => _runtime.Update(maxOpcodes);

    public GameEventScriptRun BeginRun(GameEventScriptMessage message)
        => _runtime.BeginRun(message);
}
