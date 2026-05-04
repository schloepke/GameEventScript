using System;
using StepH.GameEventScript.Runtime;

namespace StepH.GameEventScript.Api;

/// <summary>
/// Provides a builder for creating and configuring instances of <see cref="GameEventScriptHost"/>.
/// This builder enables customization of components such as random generators, diagnostic collection,
/// message observers, extension registries, and runtime limits for the <see cref="GameEventScriptHost"/>.
/// </summary>
public sealed class GameEventScriptHostBuilder
{
    private GameEventScriptRandomGenerator? _random;
    private IGameEventScriptDiagnosticCollector? _diagnosticCollector;
    private Action<GameEventScriptMessage>? _publishedMessageObserver;
    private IGameEventScriptExtensionRegistry _extensionRegistry = GameEventScriptEmptyExtensionRegistry.Instance;
    private GameEventScriptRuntimeLimits _runtimeLimits = GameEventScriptRuntimeLimits.Default;
    private GameEventScriptDispatchMode _dispatchMode = GameEventScriptDispatchMode.Manual;
    private GameEventScriptDispatcher? _dispatcher;

    /// <summary>
    /// Configures the <see cref="GameEventScriptHostBuilder"/> to use the specified
    /// <see cref="GameEventScriptRandomGenerator"/> for generating random values during script execution.
    /// </summary>
    /// <param name="random">
    /// An instance of <see cref="GameEventScriptRandomGenerator"/> to be used as the random generator.
    /// </param>
    /// <returns>
    /// The current instance of <see cref="GameEventScriptHostBuilder"/>, allowing for further configuration chaining.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if the <paramref name="random"/> parameter is null.
    /// </exception>
    public GameEventScriptHostBuilder WithRandom(GameEventScriptRandomGenerator random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        return this;
    }

    /// <summary>
    /// Configures the <see cref="GameEventScriptHostBuilder"/> to use the provided
    /// <see cref="IGameEventScriptDiagnosticCollector"/> for collecting diagnostic information
    /// during script execution.
    /// </summary>
    /// <param name="diagnosticCollector">
    /// An instance of <see cref="IGameEventScriptDiagnosticCollector"/> to be used for gathering diagnostics.
    /// </param>
    /// <returns>
    /// The current instance of <see cref="GameEventScriptHostBuilder"/>, allowing further configuration chaining.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if the <paramref name="diagnosticCollector"/> parameter is null.
    /// </exception>
    public GameEventScriptHostBuilder WithDiagnosticCollector(IGameEventScriptDiagnosticCollector diagnosticCollector)
    {
        _diagnosticCollector = diagnosticCollector ?? throw new ArgumentNullException(nameof(diagnosticCollector));
        return this;
    }

    /// <summary>
    /// Configures the <see cref="GameEventScriptHostBuilder"/> to use the specified
    /// observer for monitoring published messages during script execution.
    /// </summary>
    /// <param name="publishedMessageObserver">
    /// An action delegate to be invoked whenever a <see cref="GameEventScriptMessage"/> is published.
    /// </param>
    /// <returns>
    /// The current instance of <see cref="GameEventScriptHostBuilder"/>, enabling additional configuration chaining.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the <paramref name="publishedMessageObserver"/> parameter is null.
    /// </exception>
    public GameEventScriptHostBuilder WithPublishedMessageObserver(Action<GameEventScriptMessage> publishedMessageObserver)
    {
        _publishedMessageObserver = publishedMessageObserver ?? throw new ArgumentNullException(nameof(publishedMessageObserver));
        return this;
    }

    /// <summary>
    /// Configures the <see cref="GameEventScriptHostBuilder"/> to use the specified
    /// <see cref="IGameEventScriptExtensionRegistry"/> for managing extensions during script execution.
    /// </summary>
    /// <param name="registry">
    /// An instance of <see cref="IGameEventScriptExtensionRegistry"/> to be used as the extension registry.
    /// </param>
    /// <returns>
    /// The current instance of <see cref="GameEventScriptHostBuilder"/>, allowing for further configuration chaining.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if the <paramref name="registry"/> parameter is null.
    /// </exception>
    public GameEventScriptHostBuilder WithRegistry(IGameEventScriptExtensionRegistry registry)
    {
        _extensionRegistry = registry ?? throw new ArgumentNullException(nameof(registry));
        return this;
    }

    /// <summary>
    /// Configures the <see cref="GameEventScriptHostBuilder"/> to use the specified
    /// <see cref="GameEventScriptRuntimeLimits"/> for controlling runtime constraints during script execution.
    /// </summary>
    /// <param name="runtimeLimits">
    /// An instance of <see cref="GameEventScriptRuntimeLimits"/> that defines the runtime limitations for script execution,
    /// such as the maximum number of processed events per run.
    /// </param>
    /// <returns>
    /// The current instance of <see cref="GameEventScriptHostBuilder"/>, enabling further configuration chaining.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if the <paramref name="runtimeLimits"/> parameter is null.
    /// </exception>
    public GameEventScriptHostBuilder WithRuntimeLimits(GameEventScriptRuntimeLimits runtimeLimits)
    {
        _runtimeLimits = runtimeLimits ?? throw new ArgumentNullException(nameof(runtimeLimits));
        return this;
    }

    /// <summary>
    /// Configures how the host drains its internal message queue after messages are published.
    /// </summary>
    /// <param name="dispatchMode">The dispatch mode used by the built host.</param>
    /// <returns>The current builder instance.</returns>
    public GameEventScriptHostBuilder WithDispatchMode(GameEventScriptDispatchMode dispatchMode)
    {
        _dispatchMode = dispatchMode;
        return this;
    }

    /// <summary>
    /// Configures the host to drain queued messages on a background dispatch pump.
    /// </summary>
    /// <returns>The current builder instance.</returns>
    public GameEventScriptHostBuilder WithAutomaticDispatch()
    {
        _dispatchMode = GameEventScriptDispatchMode.Automatic;
        return this;
    }

    /// <summary>
    /// Configures the host to drain queued messages on the provided shared dispatcher.
    /// </summary>
    /// <param name="dispatcher">The dispatcher that should pump this host.</param>
    /// <returns>The current builder instance.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if the <paramref name="dispatcher"/> parameter is null.
    /// </exception>
    public GameEventScriptHostBuilder WithAutomaticDispatch(GameEventScriptDispatcher dispatcher)
    {
        _dispatchMode = GameEventScriptDispatchMode.Automatic;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        return this;
    }

    /// <summary>
    /// Builds an instance of <see cref="GameEventScriptHost"/> using the current configuration of the
    /// <see cref="GameEventScriptHostBuilder"/>.
    /// </summary>
    /// <returns>
    /// A new instance of <see cref="GameEventScriptHost"/> configured with the specified
    /// random generator, diagnostic collector, message observer, extension registry, and runtime limits.
    /// </returns>
    public GameEventScriptHost Build() => new(_random ?? GameEventScriptRandomGenerator.Create(), _diagnosticCollector, _publishedMessageObserver, _extensionRegistry, _runtimeLimits, _dispatchMode, _dispatcher);
}
