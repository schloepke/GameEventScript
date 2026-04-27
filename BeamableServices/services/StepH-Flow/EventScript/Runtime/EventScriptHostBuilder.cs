#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using StepH.Flow.EventScript.Interpreter;
using StepH.Flow.EventScript.Types;

namespace StepH.Flow.EventScript.Runtime;

public sealed class EventScriptHostBuilder
{
    public const int DefaultMaxProcessedEventsPerRun = 64;
    public const int DefaultScriptHandlerPriority = 0;
    public const int DefaultExternalHandlerPriority = 100;
    
    private EventScriptRandomGenerator? _random;
    private IEventScriptDiagnosticCollector? _diagnosticCollector;
    private EventScriptRuntimeLimits _runtimeLimits = EventScriptRuntimeLimits.Default;
    private int _maxProcessedEventsPerRun = DefaultMaxProcessedEventsPerRun;
    private int _scriptHandlerPriority = DefaultScriptHandlerPriority;
    private int _externalHandlerPriority = DefaultExternalHandlerPriority;

    public EventScriptHostBuilder WithRandom(EventScriptRandomGenerator random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        return this;
    }

    public EventScriptHostBuilder WithDiagnosticCollector(IEventScriptDiagnosticCollector diagnosticCollector)
    {
        _diagnosticCollector = diagnosticCollector ?? throw new ArgumentNullException(nameof(diagnosticCollector));
        return this;
    }

    public EventScriptHostBuilder WithMaxProcessedEventsPerRun(int maxProcessedEventsPerRun)
    {
        if (maxProcessedEventsPerRun <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxProcessedEventsPerRun), "MaxProcessedEventsPerRun must be greater than zero.");
        }

        _maxProcessedEventsPerRun = maxProcessedEventsPerRun;
        return this;
    }

    public EventScriptHostBuilder WithRuntimeLimits(EventScriptRuntimeLimits runtimeLimits)
    {
        _runtimeLimits = runtimeLimits ?? throw new ArgumentNullException(nameof(runtimeLimits));
        return this;
    }

    public EventScriptHostBuilder WithDefaultScriptHandlerPriority(int priority)
        => WithScriptHandlerPriority(priority);

    public EventScriptHostBuilder WithScriptHandlerPriority(int priority)
    {
        _scriptHandlerPriority = priority;
        return this;
    }

    public EventScriptHostBuilder WithExternalHandlerPriority(int priority)
    {
        _externalHandlerPriority = priority;
        return this;
    }

    public EventScriptHost Build()
        => new(_random ?? EventScriptRandomGenerator.Create(), _diagnosticCollector, _runtimeLimits, _maxProcessedEventsPerRun, _scriptHandlerPriority, _externalHandlerPriority);
}
