using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using static StepH.GameEventScript.Api.GameEventScriptMessage;

namespace StepH_GameEventScript_Tests;

[TestClass]
public sealed class GameEventScriptHostQueueLimitTests
{
    [TestMethod]
    public void EmitReportsWhetherMessageWasQueued()
    {
        var accepted = new List<bool>();
        var published = new List<GameEventScriptMessage>();
        var diagnostics = new GameEventScriptDiagnosticTraceCollector();
        var host = GameEventScriptHost.CreateBuilder()
            .WithRuntimeLimits(new GameEventScriptRuntimeLimits
            {
                MaxQueuedMessagesPerRun = 2
            })
            .WithDiagnosticCollector(diagnostics)
            .WithRuntimeObserver(StepH_GameEventScript_Tests.TestRuntimeObserver.ObserveOutputs(published.Add))
            .Build();

        host.Subscribe("Start", [], (_, context) =>
        {
            accepted.Add(context.Emit("A"));
            accepted.Add(context.Emit("B"));
            accepted.Add(context.Emit("C"));
        });
        host.Subscribe("A", [], (_, _) => { });
        host.Subscribe("B", [], (_, _) => { });
        host.Subscribe("C", [], (_, _) => { });

        var inputAccepted = host.PublishToCompletion(Create("Start"));

        Assert.IsTrue(inputAccepted);
        CollectionAssert.AreEqual(new[] { true, true, false }, accepted);
        CollectionAssert.AreEqual(new[] { "A", "B", "C" }, published.Select(message => message.Name).ToArray());
        Assert.IsTrue(diagnostics.Events.Any(diagnostic =>
            diagnostic.Kind == GameEventScriptDiagnosticEventKind.RuntimeLimitReached &&
            diagnostic.Name == nameof(GameEventScriptRuntimeLimits.MaxQueuedMessagesPerRun) &&
            diagnostic.Detail?.Contains("Dropped 'C'", StringComparison.Ordinal) == true));
    }

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

        Assert.IsTrue(host.PublishToCompletion(Create("Start")));

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

        public void MessagePublished(GameEventScriptMessage message, bool accepted)
            => Outputs.Add($"Publish:{message.Name}:{accepted}");

        public void DispatchStarted(GameEventScriptMessage message, string dispatchSignatureId)
            => StartedDispatches.Add(dispatchSignatureId);

        public void DispatchCompleted(GameEventScriptMessage message, string dispatchSignatureId)
            => CompletedDispatches.Add(dispatchSignatureId);

        public void RuntimeLimitReached(string limitName, string detail, int limit)
            => Limits.Add($"{limitName}:{limit}:{detail}");
    }
}
