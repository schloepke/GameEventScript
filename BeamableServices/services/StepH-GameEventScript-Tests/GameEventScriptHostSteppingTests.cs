using StepH.GameEventScript;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using static StepH.GameEventScript.Api.GameEventScriptMessage;

namespace StepH_GameEventScript_Tests;

[TestClass]
public sealed class GameEventScriptHostSteppingTests
{
    [TestMethod]
    public void PublishCapturesSubscriptionSnapshotAtPublishTime()
    {
        var calls = new List<string>();
        var host = GameEventScriptHost.CreateBuilder().Build();
        host.Subscribe("Start", [], (_, _) => calls.Add("first"));

        Assert.IsTrue(host.Publish(Create("Start")));
        host.Subscribe("Start", [], (_, _) => calls.Add("late"));

        var step = host.Update(100);

        Assert.AreEqual(GameEventScriptRunState.Completed, step.State);
        CollectionAssert.AreEqual(new[] { "first" }, calls);

        Assert.IsTrue(host.PublishToCompletion(Create("Start")));
        CollectionAssert.AreEqual(new[] { "first", "first", "late" }, calls);
    }

    [TestMethod]
    public void RuntimePublishWithoutSubscriberIsDroppedBeforeLaterSubscription()
    {
        var accepted = new List<bool>();
        var calls = new List<string>();
        var host = GameEventScriptHost.CreateBuilder().Build();
        host.Subscribe("Start", [], (_, context) =>
        {
            accepted.Add(context.Publish("Later"));
            host.Subscribe("Later", [], (_, _) => calls.Add("later"));
        });

        Assert.IsTrue(host.PublishToCompletion(Create("Start")));

        CollectionAssert.AreEqual(new[] { false }, accepted);
        CollectionAssert.AreEqual(Array.Empty<string>(), calls);

        Assert.IsTrue(host.PublishToCompletion(Create("Start")));

        CollectionAssert.AreEqual(new[] { false, true }, accepted);
        CollectionAssert.AreEqual(new[] { "later" }, calls);
    }

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

        var steps = DrainWithTinyBudget(host);

        Assert.IsTrue(steps.Any(step => step.State == GameEventScriptRunState.Paused));
        Assert.AreEqual(GameEventScriptRunState.Completed, steps[^1].State);
        Assert.IsGreaterThan(0, steps.Sum(step => step.ExecutedOpcodes));
        Assert.AreEqual(2, steps.Sum(step => step.PublishedMessages));
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

    [TestMethod]
    public void ManualHostCanResumeInsideLoopBody()
    {
        var bytecode = GameEventScriptBuilder.Create()
            .AddScript(
                """
                on Start {
                  for item from 1 to 4 {
                    publish Tick(value: item)
                  }
                }
                """)
            .Compile();
        var published = new List<long>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(message => published.Add(message.Arguments["value"].AsInteger()))
            .Build()
            .Load(bytecode);

        host.Publish(Create("Start"));

        var steps = DrainWithTinyBudget(host);

        Assert.IsTrue(steps.Any(step => step.State == GameEventScriptRunState.Paused));
        CollectionAssert.AreEqual(new long[] { 1, 2, 3, 4 }, published);
    }

    [TestMethod]
    public void ManualHostCanResumeAcrossNestedExpressionWork()
    {
        var bytecode = GameEventScriptBuilder.Create()
            .AddScript(
                """
                rule high(value) means value >= 2
                select boost(_ value) means value + 1

                on Start(values, seed) {
                  let total be values[:filter value where value is high][:select value -> boost(value)][:sum value -> value]
                  let seeded be :random with seed :list[:select item from 1 to 3 -> :random from 1 to 6]
                  let label be 'high' when total > 6, otherwise 'low'
                  publish Done(total: total, first: seeded[1], label: label)
                }
                """)
            .Compile();
        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishedMessageObserver(published.Add)
            .Build()
            .Load(bytecode);

        host.Publish(Create(
            "Start",
            ("values", GameEventScriptValueFactory.GesList(Enumerable.Range(1, 3).Select(value => GameEventScriptValueFactory.GesInteger(value)))),
            ("seed", GameEventScriptValueFactory.GesInteger(7))));

        var steps = DrainWithTinyBudget(host);

        Assert.IsTrue(steps.Any(step => step.State == GameEventScriptRunState.Paused));
        Assert.HasCount(1, published);
        Assert.AreEqual(GameEventScriptValueFactory.GesInteger(7), published[0].Arguments["total"]);
        Assert.AreEqual(GameEventScriptValueFactory.GesText("high"), published[0].Arguments["label"]);
    }

    [TestMethod]
    public void ManualHostKeepsExternalSubscriberAtomicDuringStepping()
    {
        var calls = new List<string>();
        var host = GameEventScriptHost.CreateBuilder()
            .Build();
        host.Subscribe("Start", [], (_, context) =>
        {
            calls.Add("external");
            context.Publish("Done");
        });
        host.Subscribe("Done", [], (_, _) => calls.Add("done"));

        host.Publish(Create("Start"));

        var step = host.Update(1);

        Assert.AreEqual(GameEventScriptRunState.Completed, step.State);
        CollectionAssert.AreEqual(new[] { "external", "done" }, calls);
    }

    [TestMethod]
    public void BeginRunStepsWithoutBackgroundWorker()
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

        using var run = host.BeginRun(Create("Start"));
        var steps = new List<GameEventScriptRunStepResult>();
        while (!run.IsCompleted)
        {
            steps.Add(run.Step(1));
        }

        Assert.IsTrue(steps.Any(step => step.State == GameEventScriptRunState.Paused));
        Assert.AreEqual(GameEventScriptRunState.Completed, steps[^1].State);
        CollectionAssert.AreEqual(new[] { "A", "B" }, published);
    }

    private static List<GameEventScriptRunStepResult> DrainWithTinyBudget(GameEventScriptHost host)
    {
        var steps = new List<GameEventScriptRunStepResult>();
        GameEventScriptRunStepResult step;
        do
        {
            step = host.Update(1);
            steps.Add(step);
        }
        while (step.State != GameEventScriptRunState.Completed &&
               step.State != GameEventScriptRunState.RuntimeLimitReached);

        return steps;
    }
}
