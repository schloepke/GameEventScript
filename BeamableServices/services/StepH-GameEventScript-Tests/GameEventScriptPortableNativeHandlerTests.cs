using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptMessage;

namespace StepH_GameEventScript_Tests;

[TestClass]
public sealed class GameEventScriptPortableNativeHandlerTests
{
    [TestMethod]
    public void NativeOnlyHostUsesPortableHandlerObjects()
    {
        var calls = new List<string>();
        var host = GameEventScriptHost.CreateBuilder().Build();
        host.Subscribe("Start", [], new RecordingHandler(calls, "start", "Next"));
        host.Subscribe("Next", [], new RecordingHandler(calls, "next"));

        Assert.IsTrue(host.Receive(Create("Start")));
        var result = host.RunToCompletion();

        Assert.AreEqual(GameEventScriptExecutionState.Completed, result.State);
        CollectionAssert.AreEqual(new[] { "start", "next" }, calls);
    }

    [TestMethod]
    public void PortableSubscriptionKeepsQueuedSnapshotAndIsIdempotent()
    {
        var calls = new List<string>();
        var host = GameEventScriptHost.CreateBuilder().Build();
        var subscription = host.Subscribe("Start", [], new RecordingHandler(calls, "called"));

        Assert.IsTrue(subscription.IsSubscribed);
        Assert.IsTrue(host.Receive(Create("Start")));
        Assert.IsTrue(subscription.Unsubscribe());
        Assert.IsFalse(subscription.IsSubscribed);
        Assert.IsFalse(subscription.Unsubscribe());

        host.RunToCompletion();

        CollectionAssert.AreEqual(new[] { "called" }, calls);
        Assert.IsFalse(host.Receive(Create("Start")));
    }

    private sealed class RecordingHandler(
        List<string> calls,
        string call,
        string? emit = null) : IGameEventScriptNativeMessageHandler
    {
        public void Handle(GameEventScriptMessage message, GameEventScriptContext context)
        {
            calls.Add(call);
            if (emit is not null) context.Emit(emit);
        }
    }
}
