#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.Runtime;

public sealed class GseHostBuilder
{
    public const int DefaultMaxProcessedEventsPerRun = 64;
    public const int DefaultScriptHandlerPriority = 0;
    public const int DefaultExternalHandlerPriority = 100;
    
    private GseRandomGenerator? _random;
    private IGseDiagnosticCollector? _diagnosticCollector;
    private Action<GseMessage>? _publishedMessageObserver;
    private IGseExtensionRegistry _extensionRegistry = GseEmptyExtensionRegistry.Instance;
    private GseRuntimeLimits _runtimeLimits = GseRuntimeLimits.Default;
    private int _maxProcessedEventsPerRun = DefaultMaxProcessedEventsPerRun;
    private int _scriptHandlerPriority = DefaultScriptHandlerPriority;
    private int _externalHandlerPriority = DefaultExternalHandlerPriority;

    public GseHostBuilder WithRandom(GseRandomGenerator random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        return this;
    }

    public GseHostBuilder WithDiagnosticCollector(IGseDiagnosticCollector diagnosticCollector)
    {
        _diagnosticCollector = diagnosticCollector ?? throw new ArgumentNullException(nameof(diagnosticCollector));
        return this;
    }

    public GseHostBuilder WithPublishedMessageObserver(Action<GseMessage> publishedMessageObserver)
    {
        _publishedMessageObserver = publishedMessageObserver ?? throw new ArgumentNullException(nameof(publishedMessageObserver));
        return this;
    }

    public GseHostBuilder WithRegistry(IGseExtensionRegistry registry)
    {
        _extensionRegistry = registry ?? throw new ArgumentNullException(nameof(registry));
        return this;
    }

    public GseHostBuilder WithMaxProcessedEventsPerRun(int maxProcessedEventsPerRun)
    {
        if (maxProcessedEventsPerRun <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxProcessedEventsPerRun), "MaxProcessedEventsPerRun must be greater than zero.");
        }

        _maxProcessedEventsPerRun = maxProcessedEventsPerRun;
        return this;
    }

    public GseHostBuilder WithRuntimeLimits(GseRuntimeLimits runtimeLimits)
    {
        _runtimeLimits = runtimeLimits ?? throw new ArgumentNullException(nameof(runtimeLimits));
        return this;
    }

    public GseHostBuilder WithDefaultScriptHandlerPriority(int priority)
        => WithScriptHandlerPriority(priority);

    public GseHostBuilder WithScriptHandlerPriority(int priority)
    {
        _scriptHandlerPriority = priority;
        return this;
    }

    public GseHostBuilder WithExternalHandlerPriority(int priority)
    {
        _externalHandlerPriority = priority;
        return this;
    }

    public GseHost Build()
        => new(_random ?? GseRandomGenerator.Create(), _diagnosticCollector, _publishedMessageObserver, _extensionRegistry, _runtimeLimits, _maxProcessedEventsPerRun, _scriptHandlerPriority, _externalHandlerPriority);
}
