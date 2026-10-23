// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime;

namespace GameEventScript.Tests.Native.Runtime;

internal sealed class TestRuntimeObserver : IGesRuntimeObserver
{
    private readonly Action<GameEventScriptMessage>? _messageEmitted;
    private readonly Action<GameEventScriptMessage>? _messagePublished;
    private readonly Action<GesPublishResult>? _publishResult;
    private readonly Action<string, string, int>? _runtimeLimitReached;
    private readonly Action<GesDiagnostic>? _runtimeError;

    private TestRuntimeObserver(
        Action<GameEventScriptMessage>? messageEmitted,
        Action<GameEventScriptMessage>? messagePublished,
        Action<string, string, int>? runtimeLimitReached = null,
        Action<GesPublishResult>? publishResult = null,
        Action<GesDiagnostic>? runtimeError = null)
    {
        _messageEmitted = messageEmitted;
        _messagePublished = messagePublished;
        _runtimeLimitReached = runtimeLimitReached;
        _publishResult = publishResult;
        _runtimeError = runtimeError;
    }

    public static TestRuntimeObserver ObserveOutputs(Action<GameEventScriptMessage> messageOutput)
        => new(messageOutput ?? throw new ArgumentNullException(nameof(messageOutput)), messageOutput);

    public static TestRuntimeObserver ObserveMessages(
        Action<GameEventScriptMessage>? messageEmitted = null,
        Action<GameEventScriptMessage>? messagePublished = null,
        Action<string, string, int>? runtimeLimitReached = null,
        Action<GesDiagnostic>? runtimeError = null)
        => new(messageEmitted, messagePublished, runtimeLimitReached, runtimeError: runtimeError);

    public static TestRuntimeObserver ObservePublishResults(Action<GesPublishResult> publishResult)
        => new(null, null, null, publishResult);

    public void MessageEmitted(GameEventScriptMessage message, long id, bool accepted)
        => _messageEmitted?.Invoke(message);

    public void MessagePublished(GameEventScriptMessage message, long id, GesPublishResult result)
    {
        _messagePublished?.Invoke(message);
        _publishResult?.Invoke(result);
    }

    public void RuntimeLimitReached(string limitName, string detail, int limit)
        => _runtimeLimitReached?.Invoke(limitName, detail, limit);

    public void RuntimeError(GesDiagnostic diagnostic)
        => _runtimeError?.Invoke(diagnostic);
}

internal sealed class TestRuntimeLimitEvent(string name, string detail, int limit)
{
    public string Name { get; } = name;

    public string Detail { get; } = detail;

    public int Limit { get; } = limit;
}

internal sealed class TestPublishSink(Action<GameEventScriptMessage> publish, bool accepted = true)
    : IGesPublishSink
{
    public bool Publish(GameEventScriptMessage message)
    {
        publish(message);
        return accepted;
    }
}
