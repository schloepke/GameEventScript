// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global

using System;
using System.Collections.Generic;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.Api;

/// <summary>
/// Represents the execution context for a Game Event Script, providing utilities
/// and configurations required for handling script execution, diagnostics, and message
/// publishing in the runtime environment.
/// </summary>
public sealed class GameEventScriptContext
{
    private readonly Func<GameEventScriptMessage, bool> _publish;

    /// <summary>
    /// Provides the execution context for running game event scripts, managing runtime constraints,
    /// diagnostics, random generation, and extension mechanisms.
    /// </summary>
    public GameEventScriptContext(GameEventScriptRandomGenerator random, Func<GameEventScriptMessage, bool> publish, IGameEventScriptDiagnosticCollector? diagnosticCollector = null,
        GameEventScriptRuntimeLimits? runtimeLimits = null, IGameEventScriptExtensionRegistry? extensionRegistry = null)
        : this(random, publish, diagnosticCollector, runtimeLimits, extensionRegistry, stepController: null)
    {
    }

    internal GameEventScriptContext(GameEventScriptRandomGenerator random, Func<GameEventScriptMessage, bool> publish, IGameEventScriptDiagnosticCollector? diagnosticCollector,
        GameEventScriptRuntimeLimits? runtimeLimits, IGameEventScriptExtensionRegistry? extensionRegistry, GameEventScriptStepController? stepController)
    {
        Random = random ?? throw new ArgumentNullException(nameof(random));
        _publish = publish ?? throw new ArgumentNullException(nameof(publish));
        DiagnosticCollector = diagnosticCollector;
        RuntimeLimits = runtimeLimits ?? GameEventScriptRuntimeLimits.Default;
        RuntimeBudget = new GesRuntimeBudget(this, RuntimeLimits, stepController);
        ExtensionRegistry = extensionRegistry ?? GameEventScriptEmptyExtensionRegistry.Instance;
    }

    /// <summary>
    /// Provides access to the random number generator used within the Game Event Script context.
    /// Used primarily for generating random values as part of the script execution workflow.
    /// </summary>
    public GameEventScriptRandomGenerator Random { get; }

    /// <summary>
    /// Provides access to the diagnostic collector used for recording events
    /// and diagnostic information during the execution of a Game Event Script.
    /// Facilitates tracking and analyzing runtime behaviors, such as event dispatch,
    /// parameter binding, and runtime limit enforcement.
    /// </summary>
    public IGameEventScriptDiagnosticCollector? DiagnosticCollector { get; }

    /// <summary>
    /// Defines the runtime constraints applied during script execution within the Game Event Script framework.
    /// This property ensures that execution adheres to specified limits, such as maximum steps, loop iterations,
    /// call depth, and other resource boundaries, to prevent excessive resource usage or infinite loops.
    /// </summary>
    public GameEventScriptRuntimeLimits RuntimeLimits { get; }

    /// <summary>
    /// Provides access to the registry of extensions available within the game event script execution context.
    /// This registry allows the resolution of custom extension functions, enabling extensibility and dynamic behavior
    /// in game scripts by integrating additional functionalities beyond the built-in capabilities.
    /// </summary>
    public IGameEventScriptExtensionRegistry ExtensionRegistry { get; }

    /// <summary>
    /// Publishes a specified game event script message, forwarding it to the configured publishing mechanism.
    /// The message must have a valid non-empty name to be published.
    /// </summary>
    /// <param name="message">The game event script message to be published. Must not be null, and its name must not be empty or whitespace.</param>
    public bool Publish(GameEventScriptMessage message)
    {
        return !string.IsNullOrWhiteSpace(message.Name) && _publish(message);
    }

    /// <summary>
    /// Publishes a game event script message using a string identifier.
    /// The specified message is forwarded to the configured publishing mechanism.
    /// </summary>
    /// <param name="message">The string identifier of the game event script message to be published. Must not be null, empty, or consist solely of whitespace.</param>
    public bool Publish(string message) => Publish(GameEventScriptMessage.Create(message));

    /// <summary>
    /// Publishes a game event script message with the specified name and arguments to the runtime environment.
    /// </summary>
    /// <param name="message">The name of the message to be published.</param>
    /// <param name="args">A dictionary containing the arguments for the message, where keys are parameter names and values are their corresponding script values.</param>
    public bool Publish(string message, IReadOnlyDictionary<string, GameEventScriptValue> args) => Publish(GameEventScriptMessage.Create(message, args));

    /// <summary>
    /// Publishes a message within the game event script execution context, allowing for the
    /// delivery of events to other systems or subsystems.
    /// </summary>
    /// <param name="message">The name or identifier of the message to be published.</param>
    /// <param name="args">A collection of arguments associated with the message, each represented
    /// by a name and a corresponding <see cref="GameEventScriptValue"/>.</param>
    public bool Publish(string message, params (string name, GameEventScriptValue value)[] args) => Publish(GameEventScriptMessage.Create(message, args));

    /// <summary>
    /// Records a diagnostic event within the game event script runtime, capturing details
    /// about the specific event kind, associated name, any arguments, and additional details
    /// as needed.
    /// </summary>
    /// <param name="kind">The type of diagnostic event being recorded.</param>
    /// <param name="name">The identifier or name associated with the diagnostic event.</param>
    /// <param name="arguments">The collection of arguments related to the event.</param>
    /// <param name="detail">An optional detailed description of the event.</param>
    public void RecordDiagnostic(GameEventScriptDiagnosticEventKind kind, string name, IReadOnlyDictionary<string, GameEventScriptValue> arguments, string? detail = null)
        => DiagnosticCollector?.Record(kind, name, arguments, detail);

    internal GesRuntimeBudget RuntimeBudget { get; }
}
