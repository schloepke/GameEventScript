#pragma warning disable CS1591 // Public architecture is documented in Documentation/Specification/HostRuntime.md.

using System;

namespace StepH.GameEventScript.Api;

public sealed class GameEventScriptHostBuilder
{
    private GameEventScriptRandomGenerator? _random;
    private IGameEventScriptRuntimeObserver? _observer;
    private IGameEventScriptExtensionRegistry _extensionRegistry = GameEventScriptEmptyExtensionRegistry.Instance;
    private IGameEventScriptExternalTypeRegistry _externalTypeRegistry = GameEventScriptEmptyExternalTypeRegistry.Instance;
    private GameEventScriptRuntimeLimits _limits = GameEventScriptRuntimeLimits.Default;
    private IGameEventScriptPublishSink? _publishSink;

    public GameEventScriptHostBuilder WithRandom(GameEventScriptRandomGenerator random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        return this;
    }

    public GameEventScriptHostBuilder WithRuntimeObserver(IGameEventScriptRuntimeObserver observer)
    {
        _observer = observer ?? throw new ArgumentNullException(nameof(observer));
        return this;
    }

    public GameEventScriptHostBuilder WithPublishSink(IGameEventScriptPublishSink publishSink)
    {
        _publishSink = publishSink ?? throw new ArgumentNullException(nameof(publishSink));
        return this;
    }

    public GameEventScriptHostBuilder WithRegistry(IGameEventScriptExtensionRegistry registry)
    {
        _extensionRegistry = registry ?? throw new ArgumentNullException(nameof(registry));
        return this;
    }

    public GameEventScriptHostBuilder WithExternalTypeRegistry(IGameEventScriptExternalTypeRegistry registry)
    {
        _externalTypeRegistry = registry ?? throw new ArgumentNullException(nameof(registry));
        return this;
    }

    public GameEventScriptHostBuilder WithRuntimeLimits(GameEventScriptRuntimeLimits limits)
    {
        _limits = limits ?? throw new ArgumentNullException(nameof(limits));
        return this;
    }

    public GameEventScriptHost Build() => new(_random ?? GameEventScriptRandomGenerator.Create(), _observer, _extensionRegistry, _externalTypeRegistry, _limits, _publishSink);
}
