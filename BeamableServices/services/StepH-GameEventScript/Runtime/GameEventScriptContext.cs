#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.Runtime;

public sealed class GameEventScriptContext
{
    private readonly Action<GameEventScriptMessage> _publish;

    public GameEventScriptContext(
        GameEventScriptRandomGenerator random,
        Action<GameEventScriptMessage> publish,
        IGameEventScriptDiagnosticCollector? diagnosticCollector = null,
        bool publishCallbackRecordsDiagnostics = false,
        GameEventScriptRuntimeLimits? runtimeLimits = null,
        IGameEventScriptExtensionRegistry? extensionRegistry = null)
    {
        Random = random ?? throw new ArgumentNullException(nameof(random));
        _publish = publish ?? throw new ArgumentNullException(nameof(publish));
        DiagnosticCollector = diagnosticCollector;
        PublishCallbackRecordsDiagnostics = publishCallbackRecordsDiagnostics;
        RuntimeLimits = runtimeLimits ?? GameEventScriptRuntimeLimits.Default;
        RuntimeBudget = new GameEventScriptRuntimeBudget(this, RuntimeLimits);
        ExtensionRegistry = extensionRegistry ?? GameEventScriptEmptyExtensionRegistry.Instance;
    }

    public GameEventScriptRandomGenerator Random { get; }

    public IGameEventScriptDiagnosticCollector? DiagnosticCollector { get; }

    public GameEventScriptRuntimeLimits RuntimeLimits { get; }

    public IGameEventScriptExtensionRegistry ExtensionRegistry { get; }

    internal bool PublishCallbackRecordsDiagnostics { get; }

    internal GameEventScriptRuntimeBudget RuntimeBudget { get; }

    public void Publish(GameEventScriptMessage message)
    {
        if (message is null || string.IsNullOrWhiteSpace(message.Name))
        {
            return;
        }

        _publish(message);
    }

    public void Publish(string message, IReadOnlyDictionary<string, GameEventScriptValue> args)
        => Publish(GameEventScriptMessage.Message(message, args));

    public void Publish(string message)
        => Publish(GameEventScriptMessage.Message(message));

    public void Publish(string message, params (string name, GameEventScriptValue value)[] args)
        => Publish(GameEventScriptMessage.Message(message, args));

    public void RecordDiagnostic(GameEventScriptDiagnosticEventKind kind, string name, IReadOnlyDictionary<string, GameEventScriptValue> arguments, string? detail = null)
        => DiagnosticCollector?.Record(kind, name, arguments, detail);
}
