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
            .WithPublishedMessageObserver(published.Add)
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
}
