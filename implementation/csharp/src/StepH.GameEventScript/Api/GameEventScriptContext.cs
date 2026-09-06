// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using StepH.GameEventScript.Runtime;

namespace StepH.GameEventScript.Api;

/// <summary>
/// Host-owned context shared by native and GameEventScript handlers.
/// </summary>
public sealed class GameEventScriptContext
{
    private readonly GameEventScriptHost _host;
    private readonly IGameEventScriptRuntimeObserver? _runtimeObserver;

    internal GameEventScriptContext(GameEventScriptHost host, GameEventScriptRandomGenerator random, GameEventScriptRuntimeLimits runtimeLimits, IGameEventScriptExtensionRegistry extensionRegistry, IGameEventScriptRuntimeObserver? runtimeObserver)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        Random = random ?? throw new ArgumentNullException(nameof(random));
        RuntimeLimits = runtimeLimits ?? throw new ArgumentNullException(nameof(runtimeLimits));
        ExtensionRegistry = extensionRegistry ?? GameEventScriptEmptyExtensionRegistry.Instance;
        _runtimeObserver = runtimeObserver;
        RuntimeBudget = new GesRuntimeBudget(this, RuntimeLimits);
    }

    /// <summary>
    /// Gets the host-owned random generator currently scoped to this callback.
    /// </summary>
    public GameEventScriptRandomGenerator Random { get; }
    /// <summary>
    /// Gets the immutable runtime limits configured for the owning host.
    /// </summary>
    public GameEventScriptRuntimeLimits RuntimeLimits { get; }
    /// <summary>
    /// Gets the extension registry used when programs are linked into the owning host.
    /// </summary>
    public IGameEventScriptExtensionRegistry ExtensionRegistry { get; }
    /// <summary>
    /// Gets the number of logical messages waiting in the host queue.
    /// </summary>
    public int PendingMessageCount => _host.PendingMessageCount;
    /// <summary>
    /// Gets a value indicating whether the host has neither active nor queued work.
    /// </summary>
    public bool IsIdle => _host.IsIdle;

    internal GesRuntimeBudget RuntimeBudget { get; }

    /// <summary>
    /// Enqueues a message for local host dispatch without invoking the publish sink.
    /// </summary>
    /// <param name="message">The immutable message to enqueue.</param>
    /// <returns><see langword="true"/> when the local queue accepted the message; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is <see langword="null"/>.</exception>
    public bool Emit(GameEventScriptMessage message)
    {
        _ = message ?? throw new ArgumentNullException(nameof(message));
        return !string.IsNullOrWhiteSpace(message.Name) && _host.EmitFromContext(message);
    }

    /// <summary>
    /// Creates and locally enqueues an argumentless message.
    /// </summary>
    /// <param name="message">The portable message name.</param>
    /// <returns><see langword="true"/> when the local queue accepted the message; otherwise <see langword="false"/>.</returns>
    public bool Emit(string message) => Emit(GameEventScriptMessage.Create(message));
    /// <summary>
    /// Creates and locally enqueues a message with ordered arguments.
    /// </summary>
    /// <param name="message">The portable message name.</param>
    /// <param name="arguments">The ordered argument pairs; their order is part of the message signature.</param>
    /// <returns><see langword="true"/> when the local queue accepted the message; otherwise <see langword="false"/>.</returns>
    public bool Emit(string message, IReadOnlyList<GameEventScriptMessageArgument> arguments) => Emit(GameEventScriptMessage.Create(message, arguments));

    /// <summary>
    /// Enqueues a message locally and then offers it to the configured synchronous publish sink.
    /// </summary>
    /// <param name="message">The immutable message to publish.</param>
    /// <returns>The independent local and outbound acceptance result.</returns>
    /// <remarks>A sink rejection or exception never rolls back successful local enqueue.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is <see langword="null"/>.</exception>
    public GameEventScriptPublishResult Publish(GameEventScriptMessage message)
    {
        _ = message ?? throw new ArgumentNullException(nameof(message));
        return string.IsNullOrWhiteSpace(message.Name) ? default : _host.PublishFromContext(message);
    }

    /// <summary>
    /// Creates and publishes an argumentless message.
    /// </summary>
    /// <param name="message">The portable message name.</param>
    /// <returns>The independent local and outbound acceptance result.</returns>
    public GameEventScriptPublishResult Publish(string message) => Publish(GameEventScriptMessage.Create(message));
    /// <summary>
    /// Creates and publishes a message with ordered arguments.
    /// </summary>
    /// <param name="message">The portable message name.</param>
    /// <param name="arguments">The ordered argument pairs; their order is part of the message signature.</param>
    /// <returns>The independent local and outbound acceptance result.</returns>
    public GameEventScriptPublishResult Publish(string message, IReadOnlyList<GameEventScriptMessageArgument> arguments) => Publish(GameEventScriptMessage.Create(message, arguments));

    internal GameEventScriptRandomGenerator.ScopeBoundary BeginHandler()
    {
        RuntimeBudget.Reset();
        return Random.MarkScopeBoundary();
    }

    internal GameEventScriptRandomGenerator.ScopeBoundary BeginRandomBoundary()
        => Random.MarkScopeBoundary();

    internal GameEventScriptRandomGenerator.ScopeBoundaryFault EndRandomBoundary(GameEventScriptRandomGenerator.ScopeBoundary boundary)
    {
        var fault = Random.ReleaseScopeBoundary(boundary);
        if (fault == GameEventScriptRandomGenerator.ScopeBoundaryFault.LimitExceeded)
        {
            RuntimeBudget.Exhaust(
                nameof(GameEventScriptRuntimeLimits.MaxRandomScopeDepth),
                "Random scope depth exceeds the configured limit.",
                RuntimeLimits.MaxRandomScopeDepth);
        }
        return fault;
    }

    internal void RecordRandomScopeLimitReached()
        => RuntimeBudget.Exhaust(
            nameof(GameEventScriptRuntimeLimits.MaxRandomScopeDepth),
            "Random scope depth exceeds the configured limit.",
            RuntimeLimits.MaxRandomScopeDepth);

    internal void RecordRuntimeLimitReached(string limitName, string detail, int limit)
        => _runtimeObserver?.RuntimeLimitReached(limitName, detail, limit);
}
