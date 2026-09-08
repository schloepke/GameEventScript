// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime.Values;

namespace StepH.GameEventScript.CSharpBridge;

internal sealed class GameEventScriptCSharpNativeMessageHandler(Action<GameEventScriptMessage, GameEventScriptContext> handler) : IGameEventScriptNativeMessageHandler
{
    private readonly Action<GameEventScriptMessage, GameEventScriptContext> _handler =
        handler ?? throw new ArgumentNullException(nameof(handler));

    public void Handle(GameEventScriptMessage message, GameEventScriptContext context)
        => _handler(message, context);
}

/// <summary>
/// Optional C# adapter that serializes host access and pumps accepted messages on a shared worker.
/// </summary>
public sealed class GameEventScriptCSharpHostRunner : IDisposable
{
    private readonly GameEventScriptHost _host;
    private readonly GameEventScriptCSharpDispatcher _dispatcher;
    private readonly object _gate = new();
    private bool _scheduled;
    private bool _disposed;

    private GameEventScriptCSharpHostRunner(GameEventScriptHost host, GameEventScriptCSharpDispatcher dispatcher)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <summary>
    /// Wraps a host with serialized access and automatic pumping on the shared C# dispatcher.
    /// Schedules any already queued messages, initialization, or paused script execution.
    /// </summary>
    /// <param name="host">The host whose access becomes owned by the returned runner.</param>
    /// <returns>A runner using <see cref="GameEventScriptCSharpDispatcher.Shared"/>.</returns>
    /// <remarks>Direct host access must stop when ownership is transferred. Pending callbacks may start before this method returns.</remarks>
    public static GameEventScriptCSharpHostRunner Create(GameEventScriptHost host)
    {
        var runner = new GameEventScriptCSharpHostRunner(host, GameEventScriptCSharpDispatcher.Shared);
        lock (runner._gate)
        {
            if (!runner._host.IsIdle) runner.Schedule();
        }
        return runner;
    }

    /// <summary>
    /// Thread-safely enqueues a message and schedules one pump job when accepted.
    /// </summary>
    /// <param name="message">The immutable message to receive locally.</param>
    /// <returns>Whether the underlying host accepted the message.</returns>
    public bool Receive(GameEventScriptMessage message)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            var accepted = _host.Receive(message);
            if (accepted) Schedule();
            return accepted;
        }
    }

    /// <summary>
    /// Thread-safely loads a program and schedules its initialization handlers, if present.
    /// </summary>
    /// <param name="program">The immutable portable program.</param>
    /// <param name="priority">The dispatch priority assigned to its handlers.</param>
    /// <returns>The host-local program instance.</returns>
    public GameEventScriptInstance Load(GameEventScriptProgram program, int priority = 0)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            var instance = _host.Load(program, priority);
            if (!_host.IsIdle) Schedule();
            return instance;
        }
    }

    /// <summary>
    /// Thread-safely subscribes a C# delegate to an exact ordered message signature.
    /// </summary>
    /// <param name="message">The message name.</param>
    /// <param name="parameterNames">The ordered signature labels.</param>
    /// <param name="handler">The synchronous callback executed while the runner owns its serialization gate.</param>
    /// <param name="priority">The dispatch priority.</param>
    /// <returns>The host-local subscription.</returns>
    public GameEventScriptSubscription Subscribe(string message, IReadOnlyCollection<string> parameterNames, Action<GameEventScriptMessage, GameEventScriptContext> handler, int priority = 0)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            return _host.Subscribe(
                message,
                parameterNames,
                new GameEventScriptCSharpNativeMessageHandler(handler),
                priority);
        }
    }

    /// <summary>
    /// Thread-safely detaches a loaded program instance.
    /// </summary>
    /// <param name="instance">The instance handle to detach.</param>
    /// <returns>Whether this call performed the detach.</returns>
    public bool Detach(GameEventScriptInstance instance)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            return instance.Detach();
        }
    }

    /// <summary>
    /// Thread-safely removes a native subscription.
    /// </summary>
    /// <param name="subscription">The subscription handle to remove.</param>
    /// <returns>Whether this call performed the removal.</returns>
    public bool Unsubscribe(GameEventScriptSubscription subscription)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            return subscription.Unsubscribe();
        }
    }

    /// <summary>
    /// Prevents future runner operations and suppresses pending automatic pumps.
    /// The wrapped host and shared dispatcher are not disposed.
    /// </summary>
    public void Dispose()
    {
        lock (_gate) _disposed = true;
    }

    private void Schedule()
    {
        if (_scheduled) return;
        _scheduled = true;
        _dispatcher.Enqueue(Pump);
    }

    private void Pump()
    {
        lock (_gate)
        {
            if (_disposed) { _scheduled = false; return; }
            try { _host.RunToCompletion(); }
            finally
            {
                _scheduled = false;
                if (!_host.IsIdle) Schedule();
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(GameEventScriptCSharpHostRunner));
    }
}

/// <summary>
/// Provides delegate-based and automatic-runner conveniences that intentionally remain outside the portable Core.
/// </summary>
public static class GameEventScriptCSharpHostExtensions
{
    /// <summary>
    /// Performs the subscribe operation.
    /// </summary>
    /// <param name="host">The host value.</param>
    /// <param name="message">The message value.</param>
    /// <param name="parameterNames">The parameter names value.</param>
    /// <param name="handler">The handler value.</param>
    /// <param name="priority">The priority value.</param>
    /// <returns>The result of the operation.</returns>
    public static GameEventScriptSubscription Subscribe(this GameEventScriptHost host, string message, IReadOnlyCollection<string> parameterNames, Action<GameEventScriptMessage, GameEventScriptContext> handler, int priority = 0)
        => RequireHost(host).Subscribe(
            message,
            parameterNames,
            new GameEventScriptCSharpNativeMessageHandler(handler),
            priority);

    /// <summary>
    /// Performs the subscribe operation.
    /// </summary>
    /// <param name="host">The host value.</param>
    /// <param name="signature">The signature value.</param>
    /// <param name="handler">The handler value.</param>
    /// <param name="priority">The priority value.</param>
    /// <returns>The result of the operation.</returns>
    public static GameEventScriptSubscription Subscribe(this GameEventScriptHost host, GameEventScriptMessageSignature signature, Action<GameEventScriptMessage, GameEventScriptContext> handler, int priority = 0)
        => RequireHost(host).Subscribe(
            signature,
            new GameEventScriptCSharpNativeMessageHandler(handler),
            priority);

    /// <summary>
    /// Performs the subscribe operation.
    /// </summary>
    /// <param name="host">The host value.</param>
    /// <param name="signature">The signature value.</param>
    /// <param name="handler">The handler value.</param>
    /// <param name="matchingTags">The matching tags value.</param>
    /// <param name="withoutTags">The without tags value.</param>
    /// <param name="priority">The priority value.</param>
    /// <returns>The result of the operation.</returns>
    public static GameEventScriptSubscription Subscribe(
        this GameEventScriptHost host,
        GameEventScriptMessageSignature signature,
        Action<GameEventScriptMessage, GameEventScriptContext> handler,
        IReadOnlyCollection<string>? matchingTags,
        IReadOnlyCollection<string>? withoutTags = null,
        int priority = 0)
        => RequireHost(host).Subscribe(
            signature,
            new GameEventScriptCSharpNativeMessageHandler(handler),
            matchingTags,
            withoutTags,
            priority);

    /// <summary>
    /// Performs the subscribe message name operation.
    /// </summary>
    /// <param name="host">The host value.</param>
    /// <param name="messageName">The message name value.</param>
    /// <param name="handler">The handler value.</param>
    /// <param name="matchingTags">The matching tags value.</param>
    /// <param name="withoutTags">The without tags value.</param>
    /// <param name="priority">The priority value.</param>
    /// <returns>The result of the operation.</returns>
    public static GameEventScriptSubscription SubscribeMessageName(
        this GameEventScriptHost host,
        string messageName,
        Action<GameEventScriptMessage, GameEventScriptContext> handler,
        IReadOnlyCollection<string>? matchingTags = null,
        IReadOnlyCollection<string>? withoutTags = null,
        int priority = 0)
        => RequireHost(host).SubscribeMessageName(
            messageName,
            new GameEventScriptCSharpNativeMessageHandler(handler),
            matchingTags,
            withoutTags,
            priority);

    /// <summary>
    /// Creates a thread-safe C# runner that automatically pumps existing work and later accepted messages on the shared dispatcher.
    /// </summary>
    /// <param name="host">The host whose access becomes owned by the runner.</param>
    /// <returns>The automatic host runner.</returns>
    /// <remarks>Direct host access must stop when ownership is transferred. Pending callbacks may start before this method returns.</remarks>
    public static GameEventScriptCSharpHostRunner RunAutomatically(this GameEventScriptHost host)
        => GameEventScriptCSharpHostRunner.Create(RequireHost(host));

    private static GameEventScriptHost RequireHost(GameEventScriptHost? host)
        => host ?? throw new ArgumentNullException(nameof(host));
}

/// <summary>
/// C#-specific message construction conveniences. Portable code uses ordered
/// <see cref="GameEventScriptMessageArgument"/> values directly.
/// </summary>
public static class GameEventScriptCSharpMessage
{
    /// <summary>
    /// Creates a message from C# tuple arguments while preserving tuple order.
    /// </summary>
    /// <param name="name">The message name.</param>
    /// <param name="arguments">The ordered name/value pairs.</param>
    /// <returns>An immutable portable message.</returns>
    public static GameEventScriptMessage Create(string name, params (string name, GesValue value)[] arguments)
        => GameEventScriptMessage.Create(name, ToOrdered(arguments));

    /// <summary>
    /// Creates a tagged message from C# tuple arguments while preserving tuple order.
    /// </summary>
    /// <param name="name">The message name.</param>
    /// <param name="tags">Optional message tags.</param>
    /// <param name="arguments">The ordered name/value pairs.</param>
    /// <returns>An immutable portable message.</returns>
    public static GameEventScriptMessage Create(string name, IEnumerable<string>? tags, params (string name, GesValue value)[] arguments)
        => GameEventScriptMessage.Create(name, ToOrdered(arguments), tags);

    /// <summary>
    /// Creates a message by arranging dictionary values according to a known ordered signature.
    /// </summary>
    /// <param name="signature">The signature that supplies deterministic argument order.</param>
    /// <param name="arguments">Values keyed by each named signature label.</param>
    /// <param name="tags">Optional message tags.</param>
    /// <returns>An immutable portable message.</returns>
    /// <exception cref="ArgumentException">Thrown for missing, extra, or unlabeled signature arguments.</exception>
    public static GameEventScriptMessage Create(GameEventScriptMessageSignature signature, IReadOnlyDictionary<string, GesValue> arguments, IEnumerable<string>? tags = null)
    {
        _ = signature ?? throw new ArgumentNullException(nameof(signature));
        _ = arguments ?? throw new ArgumentNullException(nameof(arguments));
        if (arguments.Count != signature.Parameters.Count)
        {
            throw new ArgumentException($"Message signature '{signature.SignatureId}' expects {signature.Parameters.Count} argument(s) but received {arguments.Count}.", nameof(arguments));
        }

        var ordered = new GameEventScriptMessageArgument[signature.Parameters.Count];
        for (var index = 0; index < signature.Parameters.Count; index++)
        {
            var parameter = signature.Parameters[index];
            if (string.Equals(parameter, GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal))
            {
                throw new ArgumentException("Dictionary binding cannot represent unlabeled message parameters.", nameof(signature));
            }

            if (!arguments.TryGetValue(parameter, out var value))
            {
                throw new ArgumentException($"Message argument '{parameter}' required by signature '{signature.SignatureId}' is missing.", nameof(arguments));
            }

            ordered[index] = new GameEventScriptMessageArgument(parameter, value);
        }

        return GameEventScriptMessage.Create(signature.Name, ordered, tags);
    }

    internal static GameEventScriptMessageArgument[] ToOrdered((string name, GesValue value)[] arguments)
    {
        _ = arguments ?? throw new ArgumentNullException(nameof(arguments));
        var ordered = new GameEventScriptMessageArgument[arguments.Length];
        for (var index = 0; index < arguments.Length; index++)
        {
            ordered[index] = new GameEventScriptMessageArgument(arguments[index].name, arguments[index].value);
        }

        return ordered;
    }
}

/// <summary>
/// Provides C# tuple and dictionary conveniences for context messaging.
/// </summary>
public static class GameEventScriptCSharpContextExtensions
{
    /// <summary>
    /// Performs the emit operation.
    /// </summary>
    /// <param name="context">The context value.</param>
    /// <param name="message">The message value.</param>
    /// <param name="arguments">The arguments value.</param>
    /// <returns>The result of the operation.</returns>
    public static bool Emit(this GameEventScriptContext context, string message, params (string name, GesValue value)[] arguments)
        => RequireContext(context).Emit(GameEventScriptCSharpMessage.Create(message, arguments));

    /// <summary>
    /// Performs the emit operation.
    /// </summary>
    /// <param name="context">The context value.</param>
    /// <param name="signature">The signature value.</param>
    /// <param name="arguments">The arguments value.</param>
    /// <returns>The result of the operation.</returns>
    public static bool Emit(this GameEventScriptContext context, GameEventScriptMessageSignature signature, IReadOnlyDictionary<string, GesValue> arguments)
        => RequireContext(context).Emit(GameEventScriptCSharpMessage.Create(signature, arguments));

    /// <summary>
    /// Performs the publish operation.
    /// </summary>
    /// <param name="context">The context value.</param>
    /// <param name="message">The message value.</param>
    /// <param name="arguments">The arguments value.</param>
    /// <returns>The result of the operation.</returns>
    public static GameEventScriptPublishResult Publish(this GameEventScriptContext context, string message, params (string name, GesValue value)[] arguments)
        => RequireContext(context).Publish(GameEventScriptCSharpMessage.Create(message, arguments));

    /// <summary>
    /// Performs the publish operation.
    /// </summary>
    /// <param name="context">The context value.</param>
    /// <param name="signature">The signature value.</param>
    /// <param name="arguments">The arguments value.</param>
    /// <returns>The result of the operation.</returns>
    public static GameEventScriptPublishResult Publish(this GameEventScriptContext context, GameEventScriptMessageSignature signature, IReadOnlyDictionary<string, GesValue> arguments)
        => RequireContext(context).Publish(GameEventScriptCSharpMessage.Create(signature, arguments));

    private static GameEventScriptContext RequireContext(GameEventScriptContext? context)
        => context ?? throw new ArgumentNullException(nameof(context));
}
