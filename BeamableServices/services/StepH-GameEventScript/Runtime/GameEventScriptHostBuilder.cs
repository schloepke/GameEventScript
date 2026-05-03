#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.Runtime;

public sealed class GameEventScriptHostBuilder
{
    public const int DefaultMaxProcessedEventsPerRun = 64;
    public const int DefaultScriptHandlerPriority = 0;
    public const int DefaultExternalHandlerPriority = 100;
    
    private GameEventScriptRandomGenerator? _random;
    private IGameEventScriptDiagnosticCollector? _diagnosticCollector;
    private Action<GameEventScriptMessage>? _publishedMessageObserver;
    private IGameEventScriptExtensionRegistry _extensionRegistry = GameEventScriptEmptyExtensionRegistry.Instance;
    private GameEventScriptRuntimeLimits _runtimeLimits = GameEventScriptRuntimeLimits.Default;
    private int _maxProcessedEventsPerRun = DefaultMaxProcessedEventsPerRun;
    private int _scriptHandlerPriority = DefaultScriptHandlerPriority;
    private int _externalHandlerPriority = DefaultExternalHandlerPriority;

    public GameEventScriptHostBuilder WithRandom(GameEventScriptRandomGenerator random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        return this;
    }

    public GameEventScriptHostBuilder WithDiagnosticCollector(IGameEventScriptDiagnosticCollector diagnosticCollector)
    {
        _diagnosticCollector = diagnosticCollector ?? throw new ArgumentNullException(nameof(diagnosticCollector));
        return this;
    }

    public GameEventScriptHostBuilder WithPublishedMessageObserver(Action<GameEventScriptMessage> publishedMessageObserver)
    {
        _publishedMessageObserver = publishedMessageObserver ?? throw new ArgumentNullException(nameof(publishedMessageObserver));
        return this;
    }

    public GameEventScriptHostBuilder WithRegistry(IGameEventScriptExtensionRegistry registry)
    {
        _extensionRegistry = registry ?? throw new ArgumentNullException(nameof(registry));
        return this;
    }

    public GameEventScriptHostBuilder WithMaxProcessedEventsPerRun(int maxProcessedEventsPerRun)
    {
        if (maxProcessedEventsPerRun <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxProcessedEventsPerRun), "MaxProcessedEventsPerRun must be greater than zero.");
        }

        _maxProcessedEventsPerRun = maxProcessedEventsPerRun;
        return this;
    }

    public GameEventScriptHostBuilder WithRuntimeLimits(GameEventScriptRuntimeLimits runtimeLimits)
    {
        _runtimeLimits = runtimeLimits ?? throw new ArgumentNullException(nameof(runtimeLimits));
        return this;
    }

    public GameEventScriptHostBuilder WithDefaultScriptHandlerPriority(int priority)
        => WithScriptHandlerPriority(priority);

    public GameEventScriptHostBuilder WithScriptHandlerPriority(int priority)
    {
        _scriptHandlerPriority = priority;
        return this;
    }

    public GameEventScriptHostBuilder WithExternalHandlerPriority(int priority)
    {
        _externalHandlerPriority = priority;
        return this;
    }

    public GameEventScriptHost Build()
        => new(_random ?? GameEventScriptRandomGenerator.Create(), _diagnosticCollector, _publishedMessageObserver, _extensionRegistry, _runtimeLimits, _maxProcessedEventsPerRun, _scriptHandlerPriority, _externalHandlerPriority);
}
