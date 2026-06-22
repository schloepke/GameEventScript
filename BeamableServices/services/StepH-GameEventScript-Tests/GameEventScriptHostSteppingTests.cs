using StepH.GameEventScript;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using static StepH.GameEventScript.Api.GameEventScriptMessage;

namespace StepH_GameEventScript_Tests;

[TestClass]
public sealed class GameEventScriptHostSteppingTests
{
    [TestMethod]
    public void SessionPublishesAndStepsThroughItsOwnQueue()
    {
        var calls = new List<string>();
        var host = GameEventScriptHost.CreateBuilder().Build();
        host.Subscribe("Start", [], (_, context) =>
        {
            calls.Add("start");
            context.Emit("Next");
        });
        host.Subscribe("Next", [], (_, _) => calls.Add("next"));

        var session = host.StartSession();

        Assert.IsTrue(session.Dispatch(Create("Start")));
        var step = session.Update(100);

        Assert.AreEqual(GameEventScriptRunState.Completed, step.State);
        CollectionAssert.AreEqual(new[] { "start", "next" }, calls);
    }

    [TestMethod]
    public void SessionKeepsOneContextAcrossMultipleDispatches()
    {
        var contexts = new List<GameEventScriptSession>();
        var host = GameEventScriptHost.CreateBuilder().Build();
        host.Subscribe("Start", [], (_, context) => contexts.Add(context));

        var session = host.StartSession();

        Assert.IsTrue(session.DispatchToCompletion(Create("Start")));
        Assert.IsTrue(session.DispatchToCompletion(Create("Start")));

        Assert.HasCount(2, contexts);
        Assert.AreSame(contexts[0], contexts[1]);
        Assert.AreSame(contexts[0], session);
    }

    [TestMethod]
    public void WildcardMessageSignatureMatchesByNameWithoutLegacyDispatchKind()
    {
        var calls = new List<string>();
        var host = GameEventScriptHost.CreateBuilder().Build();
        host.Subscribe(
            new GameEventScriptMessageHandlerDescriptor(
                GameEventScriptMessageSignature.Create("Ping", []),
                (message, _) => calls.Add(message.SignatureId),
                matchArguments: false));
        host.Subscribe(
            GameEventScriptMessageSignature.Create("Ping", ["message"]),
            (_, _) => calls.Add("exact"));

        var session = host.StartSession();

        Assert.IsTrue(session.DispatchToCompletion(Create("Ping", ("amount", GameEventScriptValueFactory.GesInteger(7)))));
        CollectionAssert.AreEqual(new[] { "Ping(amount)" }, calls);
    }

    [TestMethod]
    public void SessionStartQueuesInitializationHandlersBeforeExternalMessages()
    {
        var bytecode = GameEventScriptBuilder.Create()
            .AddScript(
                """
                on initialization {
                  emit Ready
                }

                on Start {
                  emit Started
                }
                """)
            .CompileModule();
        var calls = new List<string>();
        var host = GameEventScriptHost.CreateBuilder()
            .Build()
            .Load(bytecode);
        host.Subscribe("Ready", [], (_, _) => calls.Add("ready"));
        host.Subscribe("Started", [], (_, _) => calls.Add("started"));

        var session = host.StartSession();

        Assert.IsTrue(session.DispatchToCompletion(Create("Start")));
        CollectionAssert.AreEqual(new[] { "ready", "started" }, calls);
    }

    [TestMethod]
    public void InitializationEndpointCannotBeDispatchedAsExternalMessage()
    {
        var host = GameEventScriptHost.CreateBuilder().Build();
        var session = host.StartSession();

        Assert.IsFalse(session.Dispatch(Create("initialization")));
    }

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
    public void RuntimeEmitWithoutSubscriberIsDroppedBeforeLaterSubscription()
    {
        var accepted = new List<bool>();
        var calls = new List<string>();
        var host = GameEventScriptHost.CreateBuilder().Build();
        host.Subscribe("Start", [], (_, context) =>
        {
            accepted.Add(context.Emit("Later"));
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
    public void EmitAndPublishWithTagsUseHandlerMatchingFilters()
    {
        var bytecode = GameEventScriptBuilder.Create()
            .AddScript(
                """
                on Start {
                  let dynamicTags be [:radio, :command, :radio]
                  emit Local(value: 1) with :local
                  emit Local(value: 2) with :local, :blocked
                  emit Local(value: 3) with :other
                  emit Remote(value: 4) with dynamicTags
                }

                on Local(value) matching :local without :blocked {
                  emit Seen(value: value)
                }

                on Local(value) without :local {
                  emit Seen(value: value + 100)
                }

                on Remote(value) matching :radio, :command {
                  emit Seen(value: value + 10)
                }

                on Remote(value) matching :missing {
                  emit Seen(value: 999)
                }
                """)
            .CompileModule();
        var seen = new List<long>();
        var messages = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithRuntimeObserver(StepH_GameEventScript_Tests.TestRuntimeObserver.ObserveOutputs(messages.Add))
            .Build()
            .Load(bytecode);
        host.Subscribe("Seen", ["value"], (message, _) => seen.Add(message.Arguments["value"].AsInteger()));

        Assert.IsTrue(host.PublishToCompletion(Create("Start")));

        CollectionAssert.AreEqual(new long[] { 1, 103, 14 }, seen);
        var remote = messages.Single(message => message.Name == "Remote");
        CollectionAssert.AreEqual(new[] { "radio", "command" }, remote.Tags.ToArray());
    }

    [TestMethod]
    public void ContextPublishUsesHookWhileEmitStaysLocal()
    {
        var outbound = new List<GameEventScriptMessage>();
        var calls = new List<string>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishHook(message =>
            {
                outbound.Add(message);
                return true;
            })
            .Build();

        host.Subscribe("Start", [], (_, context) =>
        {
            Assert.IsTrue(context.Emit("Local"));
            Assert.IsTrue(context.Publish(Create("Remote").WithTags(":radio")));
        });
        host.Subscribe("Local", [], (_, _) => calls.Add("local"));
        host.Subscribe("Remote", [], (_, _) => calls.Add("remote"));

        Assert.IsTrue(host.PublishToCompletion(Create("Start")));

        CollectionAssert.AreEqual(new[] { "local" }, calls);
        Assert.HasCount(1, outbound);
        Assert.AreEqual("Remote", outbound[0].Name);
        CollectionAssert.AreEqual(new[] { "radio" }, outbound[0].Tags.ToArray());
    }

    [TestMethod]
    public void CSharpSubscribersCanFilterByMessageTags()
    {
        var calls = new List<string>();
        var host = GameEventScriptHost.CreateBuilder().Build();
        var signal = GameEventScriptMessageSignature.Create("Signal", []);
        host.Subscribe(signal, (_, _) => calls.Add("radio"), matchingTags: ["radio"]);
        host.Subscribe(signal, (_, _) => calls.Add("clear"), matchingTags: null, withoutTags: ["blocked"]);

        Assert.IsTrue(host.PublishToCompletion(Create("Signal").WithTags(":radio")));
        Assert.IsFalse(host.PublishToCompletion(Create("Signal").WithTags(":blocked")));

        CollectionAssert.AreEqual(new[] { "radio", "clear" }, calls);
    }

    [TestMethod]
    public void ScriptPublishUsesHookAndDoesNotDeliverLocallyWhenHookRedirects()
    {
        var bytecode = GameEventScriptBuilder.Create()
            .AddScript(
                """
                on Start {
                  emit Local
                  publish Remote with :radio
                }

                on Remote {
                  emit ShouldNotRun
                }
                """)
            .CompileModule();
        var outbound = new List<GameEventScriptMessage>();
        var observed = new List<string>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithRuntimeObserver(StepH_GameEventScript_Tests.TestRuntimeObserver.ObserveOutputs(message => observed.Add(message.Name)))
            .WithPublishHook(message =>
            {
                outbound.Add(message);
                return true;
            })
            .Build()
            .Load(bytecode);

        Assert.IsTrue(host.PublishToCompletion(Create("Start")));

        CollectionAssert.AreEqual(new[] { "Local", "Remote" }, observed);
        Assert.HasCount(1, outbound);
        Assert.AreEqual("Remote", outbound[0].Name);
        CollectionAssert.AreEqual(new[] { "radio" }, outbound[0].Tags.ToArray());
    }

    [TestMethod]
    public void ManualHostStepsScriptEmitStatementsByOpcodeBudget()
    {
        var bytecode = GameEventScriptBuilder.Create()
            .AddScript(
                """
                on Start {
                  emit A
                  emit B
                }
                """)
            .CompileModule();
        var published = new List<string>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithRuntimeObserver(StepH_GameEventScript_Tests.TestRuntimeObserver.ObserveOutputs(message => published.Add(message.Name)))
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
                  emit Middle
                }

                on Middle {
                  emit Done
                }
                """)
            .CompileModule();
        var published = new List<string>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithRuntimeObserver(StepH_GameEventScript_Tests.TestRuntimeObserver.ObserveOutputs(message => published.Add(message.Name)))
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
                  emit Done
                }
                """)
            .CompileModule();
        var completed = new ManualResetEventSlim(false);
        var host = GameEventScriptHost.CreateBuilder()
            .WithAutomaticDispatch()
            .WithRuntimeObserver(TestRuntimeObserver.ObserveOutputs(message =>
            {
                if (message.Name == "Done")
                {
                    completed.Set();
                }
            }))
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
                  emit Done
                }
                """)
            .CompileModule();
        using var dispatcher = GameEventScriptDispatcher.Create(workerCount: 1);
        var completed = new CountdownEvent(2);
        var first = GameEventScriptHost.CreateBuilder()
            .WithAutomaticDispatch(dispatcher)
            .WithRuntimeObserver(TestRuntimeObserver.ObserveOutputs(message =>
            {
                if (message.Name == "Done")
                {
                    completed.Signal();
                }
            }))
            .Build()
            .Load(bytecode);
        var second = GameEventScriptHost.CreateBuilder()
            .WithAutomaticDispatch(dispatcher)
            .WithRuntimeObserver(TestRuntimeObserver.ObserveOutputs(message =>
            {
                if (message.Name == "Done")
                {
                    completed.Signal();
                }
            }))
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
                  emit Middle
                }

                on Middle {
                  emit Done
                }
                """)
            .CompileModule();
        var published = new List<string>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithRuntimeLimits(new GameEventScriptRuntimeLimits { MaxProcessedEventsPerRun = 1 })
            .WithRuntimeObserver(StepH_GameEventScript_Tests.TestRuntimeObserver.ObserveOutputs(message => published.Add(message.Name)))
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
                    emit Tick(value: item)
                  }
                }
                """)
            .CompileModule();
        var published = new List<long>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithRuntimeObserver(StepH_GameEventScript_Tests.TestRuntimeObserver.ObserveOutputs(message => published.Add(message.Arguments["value"].AsInteger())))
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
                predicate high(value) be value >= 2
                function boost(_ value) be value + 1

                on Start(values, seed as :number) {
                  let total be values[:filter value where value is high][:select value => boost(value)][:sum value => value]
                  let seeded be :random with seed :list[:select item from 1 to 3 => :random from 1 to 6]
                  let label be 'high' when total > 6, otherwise 'low'
                  emit Done(total: total, first: seeded[1], label: label)
                }
                """)
            .CompileModule();
        var published = new List<GameEventScriptMessage>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithRuntimeObserver(StepH_GameEventScript_Tests.TestRuntimeObserver.ObserveOutputs(published.Add))
            .Build()
            .Load(bytecode);

        host.Publish(Create(
            "Start",
            ("values", GameEventScriptValueFactory.GesList(Enumerable.Range(1, 3).Select(value => GameEventScriptValueFactory.GesInteger(value)))),
            ("seed", GameEventScriptValueFactory.GesInteger(7))));

        var steps = DrainWithTinyBudget(host);

        Assert.IsTrue(steps.Any(step => step.State == GameEventScriptRunState.Paused));
        Assert.HasCount(1, published);
        Assert.AreEqual(7, published[0].Arguments["total"].AsInteger());
        Assert.AreEqual("high", published[0].Arguments["label"].Text);
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
            context.Emit("Done");
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
                  emit A
                  emit B
                }
                """)
            .CompileModule();
        var published = new List<string>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithRuntimeObserver(StepH_GameEventScript_Tests.TestRuntimeObserver.ObserveOutputs(message => published.Add(message.Name)))
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
