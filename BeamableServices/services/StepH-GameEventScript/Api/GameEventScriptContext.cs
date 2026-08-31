#pragma warning disable CS1591 // Public architecture is documented in HostArchitecture.md.

using System;
using System.Collections.Generic;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Runtime.Values;

namespace StepH.GameEventScript.Api;

/// <summary>
/// Host-owned context shared by native and GameEventScript handlers.
/// </summary>
public sealed class GameEventScriptContext
{
    private readonly GameEventScriptHost _host;
    private readonly IGameEventScriptRuntimeObserver? _runtimeObserver;

    internal GameEventScriptContext(
        GameEventScriptHost host,
        GameEventScriptRandomGenerator random,
        GameEventScriptRuntimeLimits runtimeLimits,
        IGameEventScriptExtensionRegistry extensionRegistry,
        IGameEventScriptRuntimeObserver? runtimeObserver)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        Random = random ?? throw new ArgumentNullException(nameof(random));
        RuntimeLimits = runtimeLimits ?? throw new ArgumentNullException(nameof(runtimeLimits));
        ExtensionRegistry = extensionRegistry ?? GameEventScriptEmptyExtensionRegistry.Instance;
        _runtimeObserver = runtimeObserver;
        RuntimeBudget = new GesRuntimeBudget(this, RuntimeLimits);
    }

    public GameEventScriptRandomGenerator Random { get; }
    public GameEventScriptRuntimeLimits RuntimeLimits { get; }
    public IGameEventScriptExtensionRegistry ExtensionRegistry { get; }
    public int PendingMessageCount => _host.PendingMessageCount;
    public bool IsIdle => _host.IsIdle;

    internal GesRuntimeBudget RuntimeBudget { get; }

    public bool Emit(GameEventScriptMessage message)
        => !string.IsNullOrWhiteSpace(message.Name) && _host.EmitFromContext(message);

    public bool Emit(string message) => Emit(GameEventScriptMessage.Create(message));
    public bool Emit(string message, IReadOnlyDictionary<string, GesValue> args) => Emit(GameEventScriptMessage.Create(message, args));
    public bool Emit(string message, params (string name, GesValue value)[] args) => Emit(GameEventScriptMessage.Create(message, args));

    public GameEventScriptPublishResult Publish(GameEventScriptMessage message)
        => string.IsNullOrWhiteSpace(message.Name) ? default : _host.PublishFromContext(message);

    public GameEventScriptPublishResult Publish(string message) => Publish(GameEventScriptMessage.Create(message));
    public GameEventScriptPublishResult Publish(string message, IReadOnlyDictionary<string, GesValue> args) => Publish(GameEventScriptMessage.Create(message, args));
    public GameEventScriptPublishResult Publish(string message, params (string name, GesValue value)[] args) => Publish(GameEventScriptMessage.Create(message, args));

    internal void BeginHandler() => RuntimeBudget.Reset();

    internal void RecordRuntimeLimitReached(string limitName, string detail, int limit)
        => _runtimeObserver?.RuntimeLimitReached(limitName, detail, limit);
}
