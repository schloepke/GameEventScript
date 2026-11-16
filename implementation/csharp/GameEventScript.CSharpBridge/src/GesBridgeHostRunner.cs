// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Threading;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;

namespace GameEventScript.CSharpBridge;

/// <summary>
/// Optional C# adapter that serializes host access and pumps accepted messages on a shared worker.
/// </summary>
public sealed class GesBridgeHostRunner : IDisposable
{
    private readonly GameEventScriptHost _host;
    private readonly GesBridgeDispatcher _dispatcher;
    private readonly object _gate = new();
    private Timer? _timer;
    private bool _scheduled;
    private bool _disposed;
    private GesExecutionResult? _lastResult;
    private readonly Dictionary<long, GesProgramInstance> _instances = [];
    private readonly Dictionary<long, GesSubscription> _subscriptions = [];

    private GesBridgeHostRunner(GameEventScriptHost host, GesBridgeDispatcher dispatcher)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <summary>
    /// Wraps a host with serialized access and automatic pumping on the shared C# dispatcher.
    /// Schedules queued messages, later initialization, or paused execution when the host is ready.
    /// A loading host waits for an explicit call to Start.
    /// </summary>
    /// <param name="host">The host whose access becomes owned by the returned runner.</param>
    /// <returns>A runner using <see cref="GesBridgeDispatcher.Shared"/>.</returns>
    /// <remarks>Direct host access must stop when ownership is transferred. Pending callbacks may start before this method returns.</remarks>
    public static GesBridgeHostRunner Create(GameEventScriptHost host)
    {
        var runner = new GesBridgeHostRunner(host, GesBridgeDispatcher.Shared);
        lock (runner._gate)
        {
            if (runner._host.IsReady && !runner._host.IsIdle) runner.Schedule();
        }
        return runner;
    }

    /// <summary>Gets whether the owned host completed its shared initial start.</summary>
    public bool IsReady { get { lock (_gate) return _host.IsReady; } }

    /// <summary>Gets whether the owned host has no active or queued work.</summary>
    public bool IsIdle { get { lock (_gate) return _host.IsIdle; } }

    /// <summary>Gets the owned host's idle duration in whole microseconds under the runner gate, or null when not idle or after disposal. Reading never schedules work.</summary>
    public long? IdleDurationMicroseconds { get { lock (_gate) return _disposed ? null : _host.IdleDurationMicroseconds; } }

    /// <summary>Gets the latest completed pump result, if any.</summary>
    public GesExecutionResult? LastResult { get { lock (_gate) return _lastResult; } }

    /// <summary>Starts the initial program group synchronously, then schedules ordinary queued work on success.</summary>
    /// <returns>The initial group startup result.</returns>
    public GesStartResult Start()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            var result = _host.Start();
            if (_host.IsReady && !_host.IsIdle) Schedule();
            return result;
        }
    }


    /// <summary>Synchronously drains the ready host under the serialization gate.</summary>
    /// <returns>The execution result.</returns>
    public GesExecutionResult RunToCompletion()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            var result = _host.RunToCompletion();
            _lastResult = result;
            return result;
        }
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
    /// <returns>A runner-owned registration whose lifecycle uses the runner's gate.</returns>
    public GesBridgeHostRunnerRegistration Load(GameEventScriptProgram program, int priority = 0)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            var instance = _host.Load(program, priority);
            _instances[instance.RegistrationId] = instance;
            if (_host.IsReady && !_host.IsIdle) Schedule();
            return new GesBridgeHostRunnerRegistration(instance.RegistrationId, this);
        }
    }

    /// <summary>
    /// Thread-safely subscribes a C# delegate to an exact ordered message signature.
    /// </summary>
    /// <param name="message">The message name.</param>
    /// <param name="parameterNames">The ordered signature labels.</param>
    /// <param name="handler">The synchronous callback executed while the runner owns its serialization gate.</param>
    /// <param name="priority">The dispatch priority.</param>
    /// <returns>A runner-owned registration whose lifecycle uses the runner's gate.</returns>
    public GesBridgeHostRunnerRegistration Subscribe(string message, IReadOnlyCollection<string> parameterNames, Action<GameEventScriptMessage, GesContext> handler, int priority = 0)
        => Subscribe(GesMessageSignature.Create(message, parameterNames), handler, null, null, priority);

    /// <summary>
    /// Thread-safely subscribes a C# delegate to an exact message signature and optional tag constraints.
    /// </summary>
    /// <param name="signature">The exact ordered message signature.</param>
    /// <param name="handler">The synchronous callback executed while the runner owns its serialization gate.</param>
    /// <param name="matchingTags">Tags all of which must be present, or <see langword="null"/> for no required tags.</param>
    /// <param name="withoutTags">Tags all of which must be absent, or <see langword="null"/> for no exclusions.</param>
    /// <param name="priority">The dispatch priority.</param>
    /// <returns>A runner-owned registration whose lifecycle uses the runner's gate.</returns>
    public GesBridgeHostRunnerRegistration Subscribe(
        GesMessageSignature signature,
        Action<GameEventScriptMessage, GesContext> handler,
        IReadOnlyCollection<string>? matchingTags,
        IReadOnlyCollection<string>? withoutTags = null,
        int priority = 0)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            var subscription = _host.Subscribe(signature, new GesBridgeMessageHandler(handler), matchingTags, withoutTags, priority);
            _subscriptions[subscription.RegistrationId] = subscription;
            return new GesBridgeHostRunnerRegistration(subscription.RegistrationId, this);
        }
    }

    internal bool IsAttached(long registrationId)
    {
        lock (_gate)
        {
            return _instances.TryGetValue(registrationId, out var instance) ? instance.IsAttached
                : _subscriptions.TryGetValue(registrationId, out var subscription) && subscription.IsSubscribed;
        }
    }

    internal GesStartResult? GetStartResult(long registrationId)
    {
        lock (_gate)
        {
            return _instances.TryGetValue(registrationId, out var instance) ? instance.StartResult : null;
        }
    }

    internal bool Detach(long registrationId)
    {
        lock (_gate)
        {
            if (_instances.TryGetValue(registrationId, out var instance))
            {
                _instances.Remove(registrationId);
                return instance.Detach();
            }

            if (!_subscriptions.TryGetValue(registrationId, out var subscription)) return false;
            _subscriptions.Remove(registrationId);
            return subscription.Unsubscribe();
        }
    }

    /// <summary>
    /// Prevents future runner operations and suppresses pending automatic pumps.
    /// The wrapped host and shared dispatcher are not disposed.
    /// </summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _timer?.Dispose();
            _timer = null;
        }
    }

    private void Schedule()
    {
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        if (_scheduled) return;
        _scheduled = true;
        _dispatcher.Enqueue(Pump);
    }

    private void Pump()
    {
        lock (_gate)
        {
            if (_disposed) { _scheduled = false; return; }
            try { if (_host.IsReady && !_host.IsIdle) _lastResult = _host.RunToCompletion(); }
            finally
            {
                _scheduled = false;
                if (_host.IsReady && !_host.IsIdle)
                {
                    if (_lastResult?.State == GesExecutionState.Waiting && _host.NextMessageDelay is { } delay)
                    {
                        _timer ??= new Timer(_ => { lock (_gate) { if (!_disposed) Schedule(); } }, null, Timeout.Infinite, Timeout.Infinite);
                        _timer.Change((int)Math.Min(int.MaxValue, delay / 1000 + (delay % 1000 == 0 ? 0 : 1)), Timeout.Infinite);
                    }
                    else Schedule();
                }
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(GesBridgeHostRunner));
    }
}

/// <summary>
/// A program instance or subscription owned by a <see cref="GesBridgeHostRunner"/>. Every access goes through the runner's
/// serialization gate, so another thread may use it while the runner pumps.
/// </summary>
public sealed class GesBridgeHostRunnerRegistration
{
    private readonly GesBridgeHostRunner _owner;

    internal GesBridgeHostRunnerRegistration(long registrationId, GesBridgeHostRunner owner)
    {
        RegistrationId = registrationId;
        _owner = owner;
    }

    /// <summary>Gets the stable identifier of the registration within the runner's host.</summary>
    public long RegistrationId { get; }

    /// <summary>Gets whether the instance or subscription still contributes to future message snapshots.</summary>
    public bool IsAttached => _owner.IsAttached(RegistrationId);

    /// <summary>Gets the initialization result of a loaded program, or null while pending, after detaching, or for a
    /// subscription.</summary>
    public GesStartResult? StartResult => _owner.GetStartResult(RegistrationId);

    /// <summary>Idempotently detaches the instance or removes the subscription under the runner's gate.</summary>
    /// <returns><see langword="true"/> only when this call removed it.</returns>
    public bool Detach() => _owner.Detach(RegistrationId);
}
