#pragma warning disable CS1591 // Public architecture is documented in Documentation/Specification/HostRuntime.md.

using System;
using System.Collections.Generic;

namespace StepH.GameEventScript.Api;

public sealed class GameEventScriptHostBuilder
{
    private long? _randomSeed;
    private double[]? _randomSequence;
    private long? _randomSequenceFallbackSeed;
    private IGameEventScriptRuntimeObserver? _observer;
    private IGameEventScriptExtensionRegistry _extensionRegistry = GameEventScriptEmptyExtensionRegistry.Instance;
    private IGameEventScriptExternalTypeRegistry _externalTypeRegistry = GameEventScriptEmptyExternalTypeRegistry.Instance;
    private GameEventScriptRuntimeLimits _limits = GameEventScriptRuntimeLimits.Default;
    private IGameEventScriptPublishSink? _publishSink;

    /// <summary>Configures the deterministic seed used to create this host's private random generator.</summary>
    /// <param name="seed">The complete signed 64-bit seed.</param>
    /// <returns>This builder.</returns>
    public GameEventScriptHostBuilder WithRandomSeed(long seed)
    {
        _randomSeed = seed;
        _randomSequence = null;
        _randomSequenceFallbackSeed = null;
        return this;
    }

    /// <summary>Configures a copied start sequence followed by a non-deterministically seeded private generator.</summary>
    /// <param name="values">Values consumed before the private fallback generator is used.</param>
    /// <returns>This builder.</returns>
    public GameEventScriptHostBuilder WithRandomSequence(IReadOnlyList<double> values)
        => WithRandomSequence(values, null);

    /// <summary>Configures a copied start sequence followed by a deterministically seeded private generator.</summary>
    /// <param name="values">Values consumed before the private fallback generator is used.</param>
    /// <param name="fallbackSeed">The fallback generator seed.</param>
    /// <returns>This builder.</returns>
    public GameEventScriptHostBuilder WithRandomSequence(IReadOnlyList<double> values, long fallbackSeed)
        => WithRandomSequence(values, (long?)fallbackSeed);

    private GameEventScriptHostBuilder WithRandomSequence(IReadOnlyList<double> values, long? fallbackSeed)
    {
        _ = values ?? throw new ArgumentNullException(nameof(values));
        _randomSequence = new double[values.Count];
        for (var index = 0; index < values.Count; index++) _randomSequence[index] = values[index];
        _randomSequenceFallbackSeed = fallbackSeed;
        _randomSeed = null;
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

    public GameEventScriptHost Build()
    {
        var maxRandomScopeDepth = _limits.MaxRandomScopeDepth;
        var random = _randomSequence is { } sequence
            ? GameEventScriptRandomGenerator.FromSequenceForHost(sequence, _randomSequenceFallbackSeed, maxRandomScopeDepth)
            : _randomSeed is { } seed
                ? GameEventScriptRandomGenerator.FromSeedForHost(seed, maxRandomScopeDepth)
                : GameEventScriptRandomGenerator.CreateForHost(maxRandomScopeDepth);
        return new GameEventScriptHost(random, _observer, _extensionRegistry, _externalTypeRegistry, _limits, _publishSink);
    }
}
