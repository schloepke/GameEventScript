using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using static StepH.GameEventScript.Api.GameEventScriptMessage;

namespace StepH_GameEventScript_Tests;

[TestClass]
public sealed class GameEventScriptHostQueueLimitTests
{
    [TestMethod]
    public void RuntimeObserverReportsMessageEventsDispatchAndQueueLimits()
    {
        var observer = new RecordingRuntimeObserver();
        var host = GameEventScriptHost.CreateBuilder()
            .WithRuntimeLimits(new GameEventScriptRuntimeLimits
            {
                MaxQueuedMessagesPerRun = 2
            })
            .WithRuntimeObserver(observer)
            .Build();

        host.Subscribe("Start", [], (_, context) =>
        {
            context.Emit("A");
            context.Emit("B");
            context.Emit("C");
        });
        host.Subscribe("A", [], (_, _) => { });
        host.Subscribe("B", [], (_, _) => { });
        host.Subscribe("C", [], (_, _) => { });

        Assert.IsTrue(host.Receive(Create("Start")));
        host.RunToCompletion();

        CollectionAssert.AreEqual(
            new[] { "Emit:A:True", "Emit:B:True", "Emit:C:False" },
            observer.Outputs.ToArray());
        CollectionAssert.AreEqual(
            new[] { "Start()", "A()", "B()" },
            observer.StartedDispatches.ToArray());
        CollectionAssert.AreEqual(
            new[] { "Start()", "A()", "B()" },
            observer.CompletedDispatches.ToArray());
        CollectionAssert.AreEqual(
            new[] { "MaxQueuedMessagesPerRun:2:Message queue limit reached. Dropped 'C'." },
            observer.Limits.ToArray());
    }

    private sealed class RecordingRuntimeObserver : IGameEventScriptRuntimeObserver
    {
        public List<string> Outputs { get; } = [];

        public List<string> StartedDispatches { get; } = [];

        public List<string> CompletedDispatches { get; } = [];

        public List<string> Limits { get; } = [];

        public void MessageEmitted(GameEventScriptMessage message, bool accepted)
            => Outputs.Add($"Emit:{message.Name}:{accepted}");

        public void MessagePublished(GameEventScriptMessage message, GameEventScriptPublishResult result)
            => Outputs.Add($"Publish:{message.Name}:{result.AnyAccepted}");

        public void DispatchStarted(GameEventScriptMessage message, string dispatchSignatureId)
            => StartedDispatches.Add(dispatchSignatureId);

        public void DispatchCompleted(GameEventScriptMessage message, string dispatchSignatureId)
            => CompletedDispatches.Add(dispatchSignatureId);

        public void RuntimeLimitReached(string limitName, string detail, int limit)
            => Limits.Add($"{limitName}:{limit}:{detail}");
    }
}
