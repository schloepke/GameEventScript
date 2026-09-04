using System;
using System.Collections.Generic;

namespace StepH.GameEventScript.Api;

/// <summary>
/// Configures and creates independent serial <see cref="GameEventScriptHost"/> instances.
/// </summary>
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

    /// <summary>
    /// Configures the synchronous observer used for runtime events and diagnostics.
    /// </summary>
    /// <param name="observer">The observer. Its callbacks must not throw and must not recursively pump the host.</param>
    /// <returns>This builder.</returns>
    public GameEventScriptHostBuilder WithRuntimeObserver(IGameEventScriptRuntimeObserver observer)
    {
        _observer = observer ?? throw new ArgumentNullException(nameof(observer));
        return this;
    }

    /// <summary>
    /// Configures the single synchronous outbound sink used by <c>Publish</c>.
    /// </summary>
    /// <param name="publishSink">The outbound handoff point. Network and asynchronous delivery remain outside the portable Core.</param>
    /// <returns>This builder.</returns>
    public GameEventScriptHostBuilder WithPublishSink(IGameEventScriptPublishSink publishSink)
    {
        _publishSink = publishSink ?? throw new ArgumentNullException(nameof(publishSink));
        return this;
    }

    /// <summary>
    /// Configures the extension functions resolved while programs are linked by <see cref="GameEventScriptHost.Load"/>.
    /// </summary>
    /// <param name="registry">The host runtime extension registry.</param>
    /// <returns>This builder.</returns>
    public GameEventScriptHostBuilder WithRegistry(IGameEventScriptExtensionRegistry registry)
    {
        _extensionRegistry = registry ?? throw new ArgumentNullException(nameof(registry));
        return this;
    }

    /// <summary>
    /// Configures the external-type constructors resolved while programs are linked.
    /// </summary>
    /// <param name="registry">The host runtime external-type registry.</param>
    /// <returns>This builder.</returns>
    public GameEventScriptHostBuilder WithExternalTypeRegistry(IGameEventScriptExternalTypeRegistry registry)
    {
        _externalTypeRegistry = registry ?? throw new ArgumentNullException(nameof(registry));
        return this;
    }

    /// <summary>
    /// Configures the immutable safety limits copied into subsequently built hosts.
    /// </summary>
    /// <param name="limits">The portable runtime limits.</param>
    /// <returns>This builder.</returns>
    public GameEventScriptHostBuilder WithRuntimeLimits(GameEventScriptRuntimeLimits limits)
    {
        _limits = limits ?? throw new ArgumentNullException(nameof(limits));
        return this;
    }

    /// <summary>
    /// Creates a host with its own queue, context, random state, and lazily allocated VM state.
    /// </summary>
    /// <returns>A new independent host. Configured registries, sink, and observer references are shared.</returns>
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
