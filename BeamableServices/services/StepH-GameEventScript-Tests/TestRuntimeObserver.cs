using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;

namespace StepH_GameEventScript_Tests;

internal sealed class TestRuntimeObserver : IGameEventScriptRuntimeObserver
{
    private readonly Action<GameEventScriptMessage>? _messageEmitted;
    private readonly Action<GameEventScriptMessage>? _messagePublished;
    private readonly Action<GameEventScriptPublishResult>? _publishResult;
    private readonly Action<string, string, int>? _runtimeLimitReached;

    private TestRuntimeObserver(
        Action<GameEventScriptMessage>? messageEmitted,
        Action<GameEventScriptMessage>? messagePublished,
        Action<string, string, int>? runtimeLimitReached = null,
        Action<GameEventScriptPublishResult>? publishResult = null)
    {
        _messageEmitted = messageEmitted;
        _messagePublished = messagePublished;
        _runtimeLimitReached = runtimeLimitReached;
        _publishResult = publishResult;
    }

    public static TestRuntimeObserver ObserveOutputs(Action<GameEventScriptMessage> messageOutput)
        => new(messageOutput ?? throw new ArgumentNullException(nameof(messageOutput)), messageOutput);

    public static TestRuntimeObserver ObserveMessages(
        Action<GameEventScriptMessage>? messageEmitted = null,
        Action<GameEventScriptMessage>? messagePublished = null,
        Action<string, string, int>? runtimeLimitReached = null)
        => new(messageEmitted, messagePublished, runtimeLimitReached);

    public static TestRuntimeObserver ObservePublishResults(Action<GameEventScriptPublishResult> publishResult)
        => new(null, null, null, publishResult);

    public void MessageEmitted(GameEventScriptMessage message, bool accepted)
        => _messageEmitted?.Invoke(message);

    public void MessagePublished(GameEventScriptMessage message, GameEventScriptPublishResult result)
    {
        _messagePublished?.Invoke(message);
        _publishResult?.Invoke(result);
    }

    public void DispatchStarted(GameEventScriptMessage message, string dispatchSignatureId)
    {
    }

    public void DispatchCompleted(GameEventScriptMessage message, string dispatchSignatureId)
    {
    }

    public void RuntimeLimitReached(string limitName, string detail, int limit)
        => _runtimeLimitReached?.Invoke(limitName, detail, limit);
}

internal sealed class TestRuntimeLimitEvent(string name, string detail, int limit)
{
    public string Name { get; } = name;

    public string Detail { get; } = detail;

    public int Limit { get; } = limit;
}

internal sealed class TestPublishSink(Action<GameEventScriptMessage> publish, bool accepted = true)
    : IGameEventScriptPublishSink
{
    public bool Publish(GameEventScriptMessage message)
    {
        publish(message);
        return accepted;
    }
}
