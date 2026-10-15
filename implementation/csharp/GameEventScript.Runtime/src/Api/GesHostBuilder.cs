// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace GameEventScript.Api;

/// <summary>
/// Configures and creates independent serial <see cref="GameEventScriptHost"/> instances.
/// </summary>
public sealed class GesHostBuilder
{
    private long? _randomSeed;
    private long? _randomEntropySeed;
    private double[]? _randomSequence;
    private long? _randomSequenceFallbackSeed;
    private IGesRuntimeObserver? _observer;
    private IGesProfiler? _profiler;

    /// <summary>Configures optional synchronous opcode instrumentation for subsequently built hosts.</summary>
    /// <param name="profiler">Factory for independent per-instance instrumentation. Callbacks must not throw or reenter a host.</param>
    /// <returns>This builder. The factory is shared; access across hosts must be serialized by the caller.</returns>
    public GesHostBuilder WithProfiler(IGesProfiler profiler)
    {
        _profiler = profiler ?? throw new ArgumentNullException(nameof(profiler));
        return this;
    }

    private IGesExtensionRegistry _extensionRegistry = GameEventScriptEmptyExtensionRegistry.Instance;
    private IGesExternalTypeRegistry _externalTypeRegistry = GameEventScriptEmptyExternalTypeRegistry.Instance;
    private GesRuntimeLimits _limits = GesRuntimeLimits.Default;
    private IGesPublishSink? _publishSink;

    private IGesClock? _clock;

    /// <summary>Configures the monotonic time source borrowed by subsequently built hosts.</summary>
    /// <param name="clock">A serially accessed clock which never moves backwards.</param>
    /// <returns>This builder.</returns>
    public GesHostBuilder WithClock(IGesClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        return this;
    }

    /// <summary>Configures the deterministic seed used to create this host's private random generator.</summary>
    /// <param name="seed">The complete signed 64-bit seed.</param>
    /// <returns>This builder.</returns>
    public GesHostBuilder WithRandomSeed(long seed)
    {
        _randomSeed = seed;
        _randomEntropySeed = null;
        _randomSequence = null;
        _randomSequenceFallbackSeed = null;
        return this;
    }

    /// <summary>Configures this host's deterministic seed by folding <paramref name="entropy"/> through the portable entropy-mixing function, and clears any prior seed or sequence.</summary>
    /// <param name="entropy">A non-empty read-only span of entropy bytes. Supply at least eight bytes for a full-entropy seed.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException"><paramref name="entropy"/> is empty.</exception>
    public GesHostBuilder WithRandomEntropy(ReadOnlySpan<byte> entropy)
    {
        _randomEntropySeed = GesRandomGenerator.SeedFromEntropy(entropy);
        _randomSeed = null;
        _randomSequence = null;
        _randomSequenceFallbackSeed = null;
        return this;
    }

    /// <summary>Configures a copied start sequence followed by a non-deterministically seeded private generator.</summary>
    /// <param name="values">Values consumed before the private fallback generator is used.</param>
    /// <returns>This builder.</returns>
    public GesHostBuilder WithRandomSequence(IReadOnlyList<double> values)
        => WithRandomSequence(values, null);

    /// <summary>Configures a copied start sequence followed by a deterministically seeded private generator.</summary>
    /// <param name="values">Values consumed before the private fallback generator is used.</param>
    /// <param name="fallbackSeed">The fallback generator seed.</param>
    /// <returns>This builder.</returns>
    public GesHostBuilder WithRandomSequence(IReadOnlyList<double> values, long fallbackSeed)
        => WithRandomSequence(values, (long?)fallbackSeed);

    private GesHostBuilder WithRandomSequence(IReadOnlyList<double> values, long? fallbackSeed)
    {
        _ = values ?? throw new ArgumentNullException(nameof(values));
        _randomSequence = new double[values.Count];
        for (var index = 0; index < values.Count; index++) _randomSequence[index] = values[index];
        _randomSequenceFallbackSeed = fallbackSeed;
        _randomSeed = null;
        _randomEntropySeed = null;
        return this;
    }

    /// <summary>
    /// Configures the synchronous observer used for runtime events and diagnostics.
    /// </summary>
    /// <param name="observer">The observer. Its callbacks must not throw and must not recursively pump the host.</param>
    /// <returns>This builder.</returns>
    public GesHostBuilder WithRuntimeObserver(IGesRuntimeObserver observer)
    {
        _observer = observer ?? throw new ArgumentNullException(nameof(observer));
        return this;
    }

    /// <summary>
    /// Configures the single synchronous outbound sink used by <c>Publish</c>.
    /// </summary>
    /// <param name="publishSink">The outbound handoff point. Network and asynchronous delivery remain outside the portable Core.</param>
    /// <returns>This builder.</returns>
    public GesHostBuilder WithPublishSink(IGesPublishSink publishSink)
    {
        _publishSink = publishSink ?? throw new ArgumentNullException(nameof(publishSink));
        return this;
    }

    /// <summary>
    /// Configures the extension functions resolved while programs are linked by <see cref="GameEventScriptHost.Load"/>.
    /// </summary>
    /// <param name="registry">The host runtime extension registry.</param>
    /// <returns>This builder.</returns>
    public GesHostBuilder WithRegistry(IGesExtensionRegistry registry)
    {
        _extensionRegistry = registry ?? throw new ArgumentNullException(nameof(registry));
        return this;
    }

    /// <summary>
    /// Configures the external-type constructors resolved while programs are linked.
    /// </summary>
    /// <param name="registry">The host runtime external-type registry.</param>
    /// <returns>This builder.</returns>
    public GesHostBuilder WithExternalTypeRegistry(IGesExternalTypeRegistry registry)
    {
        _externalTypeRegistry = registry ?? throw new ArgumentNullException(nameof(registry));
        return this;
    }

    /// <summary>
    /// Configures the immutable safety limits copied into subsequently built hosts.
    /// </summary>
    /// <param name="limits">The portable runtime limits.</param>
    /// <returns>This builder.</returns>
    public GesHostBuilder WithRuntimeLimits(GesRuntimeLimits limits)
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
        GesRandomGenerator random;
        if (_randomSequence is { } sequence)
            random = GesRandomGenerator.FromSequenceForHost(sequence, _randomSequenceFallbackSeed, maxRandomScopeDepth);
        else if ((_randomSeed ?? _randomEntropySeed) is { } seed)
            random = GesRandomGenerator.FromSeedForHost(seed, maxRandomScopeDepth);
        else
            random = GesRandomGenerator.CreateForHost(maxRandomScopeDepth);

        return new GameEventScriptHost(random, _observer, _extensionRegistry, _externalTypeRegistry, _limits, _publishSink, _clock, _profiler);
    }
}
