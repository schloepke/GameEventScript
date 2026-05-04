using StepH.GameEventScript;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using static StepH.GameEventScript.Api.GameEventScriptMessage;

namespace StepH_GameEventScript_Tests;

[TestClass]
public sealed class GameEventScriptHostSteppingTests
{
    [TestMethod]
    public void ManualHostStepsScriptPublishStatementsByOpcodeBudget()
    {
        var bytecode = GameEventScriptBuilder.Create()
            .AddScript(
                """
                on Start {
                  publish A
                  publish B
                }
                """)
            .Compile();
        var published = new List<string>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(message => published.Add(message.Name))
            .Build()
            .Load(bytecode);

        var accepted = host.Publish(Create("Start"));

        Assert.IsTrue(accepted);
        CollectionAssert.AreEqual(Array.Empty<string>(), published);

        var first = host.Update(1);

        Assert.AreEqual(GameEventScriptRunState.Paused, first.State);
        Assert.AreEqual(1, first.ExecutedOpcodes);
        Assert.AreEqual(1, first.ProcessedMessages);
        Assert.AreEqual(1, first.PublishedMessages);
        CollectionAssert.AreEqual(new[] { "A" }, published);

        var second = host.Update(1);

        Assert.AreEqual(GameEventScriptRunState.Completed, second.State);
        Assert.AreEqual(1, second.ExecutedOpcodes);
        Assert.AreEqual(1, second.PublishedMessages);
        CollectionAssert.AreEqual(new[] { "A", "B" }, published);
    }

    [TestMethod]
    public void ManualHostKeepsMessageQueueInsideRun()
    {
        var bytecode = GameEventScriptBuilder.Create()
            .AddScript(
                """
                on Start {
                  publish Middle
                }

                on Middle {
                  publish Done
                }
                """)
            .Compile();
        var published = new List<string>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(message => published.Add(message.Name))
            .Build()
            .Load(bytecode);

        host.Publish(Create("Start"));

        GameEventScriptRunStepResult step;
        do
        {
            step = host.Update(1);
        }
        while (step.State != GameEventScriptRunState.Completed);

        CollectionAssert.AreEqual(new[] { "Middle", "Done" }, published);
    }

    [TestMethod]
    public void AutomaticDispatchPublishesWithoutWaitingForCallerDrain()
    {
        var bytecode = GameEventScriptBuilder.Create()
            .AddScript(
                """
                on Start {
                  publish Done
                }
                """)
            .Compile();
        var completed = new ManualResetEventSlim(false);
        var host = GameEventScriptHost.CreateBuilder()
            .WithAutomaticDispatch()
            .WithPublishedMessageObserver(message =>
            {
                if (message.Name == "Done")
                {
                    completed.Set();
                }
            })
            .Build()
            .Load(bytecode);

        var accepted = host.Publish(Create("Start"));

        Assert.IsTrue(accepted);
        Assert.IsTrue(completed.Wait(TimeSpan.FromSeconds(2)));
    }

    [TestMethod]
    public void AutomaticDispatchCanShareOneDispatcherAcrossHosts()
    {
        var bytecode = GameEventScriptBuilder.Create()
            .AddScript(
                """
                on Start {
                  publish Done
                }
                """)
            .Compile();
        using var dispatcher = GameEventScriptDispatcher.Create(workerCount: 1);
        var completed = new CountdownEvent(2);
        var first = GameEventScriptHost.CreateBuilder()
            .WithAutomaticDispatch(dispatcher)
            .WithPublishedMessageObserver(message =>
            {
                if (message.Name == "Done")
                {
                    completed.Signal();
                }
            })
            .Build()
            .Load(bytecode);
        var second = GameEventScriptHost.CreateBuilder()
            .WithAutomaticDispatch(dispatcher)
            .WithPublishedMessageObserver(message =>
            {
                if (message.Name == "Done")
                {
                    completed.Signal();
                }
            })
            .Build()
            .Load(bytecode);

        Assert.IsTrue(first.Publish(Create("Start")));
        Assert.IsTrue(second.Publish(Create("Start")));

        Assert.IsTrue(completed.Wait(TimeSpan.FromSeconds(2)));
    }

    [TestMethod]
    public void SubscribeDuringDispatchUpdatesFutureDispatchSnapshots()
    {
        var calls = new List<string>();
        var host = GameEventScriptHost.CreateBuilder()
            .Build();
        host.Subscribe("Start", [], (_, _) =>
        {
            calls.Add("first");
            host.Subscribe("Start", [], (_, _) => calls.Add("late"));
        });
        host.Subscribe("Start", [], (_, _) => calls.Add("second"));

        Assert.IsTrue(host.PublishToCompletion(Create("Start")));

        CollectionAssert.AreEqual(new[] { "first", "second" }, calls);

        Assert.IsTrue(host.PublishToCompletion(Create("Start")));

        CollectionAssert.AreEqual(new[] { "first", "second", "first", "second", "late" }, calls);
    }

    [TestMethod]
    public void ManualHostIgnoresProcessedEventLimitBecauseOpcodeBudgetControlsProgress()
    {
        var bytecode = GameEventScriptBuilder.Create()
            .AddScript(
                """
                on Start {
                  publish Middle
                }

                on Middle {
                  publish Done
                }
                """)
            .Compile();
        var published = new List<string>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithRuntimeLimits(new GameEventScriptRuntimeLimits { MaxProcessedEventsPerRun = 1 })
            .WithPublishedMessageObserver(message => published.Add(message.Name))
            .Build()
            .Load(bytecode);

        host.Publish(Create("Start"));

        GameEventScriptRunStepResult step;
        do
        {
            step = host.Update(8);
        }
        while (step.State != GameEventScriptRunState.Completed);

        CollectionAssert.AreEqual(new[] { "Middle", "Done" }, published);
    }
}
