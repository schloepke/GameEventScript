#pragma warning disable CS1591 // Public architecture is documented in HostArchitecture.md.

using System;
using System.Collections.Generic;
using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.CSharpBridge;

internal sealed class GameEventScriptCSharpNativeMessageHandler(
    Action<GameEventScriptMessage, GameEventScriptContext> handler) : IGameEventScriptNativeMessageHandler
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

    public static GameEventScriptCSharpHostRunner Create(GameEventScriptHost host)
        => new(host, GameEventScriptCSharpDispatcher.Shared);

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

    public GameEventScriptSubscription Subscribe(
        string message,
        IReadOnlyCollection<string> parameterNames,
        Action<GameEventScriptMessage, GameEventScriptContext> handler,
        int priority = 0)
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

    public bool Detach(GameEventScriptInstance instance)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            return instance.Detach();
        }
    }

    public bool Unsubscribe(GameEventScriptSubscription subscription)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            return subscription.Unsubscribe();
        }
    }

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

public static class GameEventScriptCSharpHostExtensions
{
    public static GameEventScriptSubscription Subscribe(
        this GameEventScriptHost host,
        string message,
        IReadOnlyCollection<string> parameterNames,
        Action<GameEventScriptMessage, GameEventScriptContext> handler,
        int priority = 0)
        => RequireHost(host).Subscribe(
            message,
            parameterNames,
            new GameEventScriptCSharpNativeMessageHandler(handler),
            priority);

    public static GameEventScriptSubscription Subscribe(
        this GameEventScriptHost host,
        GameEventScriptMessageSignature signature,
        Action<GameEventScriptMessage, GameEventScriptContext> handler,
        int priority = 0)
        => RequireHost(host).Subscribe(
            signature,
            new GameEventScriptCSharpNativeMessageHandler(handler),
            priority);

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

    public static GameEventScriptCSharpHostRunner RunAutomatically(this GameEventScriptHost host)
        => GameEventScriptCSharpHostRunner.Create(RequireHost(host));

    private static GameEventScriptHost RequireHost(GameEventScriptHost? host)
        => host ?? throw new ArgumentNullException(nameof(host));
}
