#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.Runtime;

public sealed class GseContext
{
    private readonly Action<GseMessage> _publish;

    public GseContext(
        GseRandomGenerator random,
        Action<GseMessage> publish,
        IGseDiagnosticCollector? diagnosticCollector = null,
        bool publishCallbackRecordsDiagnostics = false,
        GseRuntimeLimits? runtimeLimits = null,
        IGseExtensionRegistry? extensionRegistry = null)
    {
        Random = random ?? throw new ArgumentNullException(nameof(random));
        _publish = publish ?? throw new ArgumentNullException(nameof(publish));
        DiagnosticCollector = diagnosticCollector;
        PublishCallbackRecordsDiagnostics = publishCallbackRecordsDiagnostics;
        RuntimeLimits = runtimeLimits ?? GseRuntimeLimits.Default;
        RuntimeBudget = new GseRuntimeBudget(this, RuntimeLimits);
        ExtensionRegistry = extensionRegistry ?? GseEmptyExtensionRegistry.Instance;
    }

    public GseRandomGenerator Random { get; }

    public IGseDiagnosticCollector? DiagnosticCollector { get; }

    public GseRuntimeLimits RuntimeLimits { get; }

    public IGseExtensionRegistry ExtensionRegistry { get; }

    internal bool PublishCallbackRecordsDiagnostics { get; }

    internal GseRuntimeBudget RuntimeBudget { get; }

    public void Publish(GseMessage message)
    {
        if (message is null || string.IsNullOrWhiteSpace(message.Name))
        {
            return;
        }

        _publish(message);
    }

    public void Publish(string message, IReadOnlyDictionary<string, GseValue> args)
        => Publish(GseMessage.Message(message, args));

    public void Publish(string message)
        => Publish(GseMessage.Message(message));

    public void Publish(string message, params (string name, GseValue value)[] args)
        => Publish(GseMessage.Message(message, args));

    public void RecordDiagnostic(GseDiagnosticEventKind kind, string name, IReadOnlyDictionary<string, GseValue> arguments, string? detail = null)
        => DiagnosticCollector?.Record(kind, name, arguments, detail);
}
