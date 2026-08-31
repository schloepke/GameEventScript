using StepH.GameEventScript.Api;
using StepH.GameEventScript.CSharpBridge;
using StepH.GameEventScript.Runtime.VM;
using StepH.GameEventScript.Runtime.Values;
using static StepH.GameEventScript.Api.GameEventScriptMessage;

namespace StepH_GameEventScript_Tests;

[TestClass]
public sealed class GameEventScriptHostSteppingTests
{
    [TestMethod]
    public void NativeOnlyHostReceivesAndEmitsLocally()
    {
        var calls = new List<string>();
        var host = GameEventScriptHost.CreateBuilder().Build();
        host.Subscribe("Start", [], (_, context) => { calls.Add("start"); context.Emit("Next"); });
        host.Subscribe("Next", [], (_, _) => calls.Add("next"));

        Assert.IsTrue(host.Receive(Create("Start")));
        var result = host.RunToCompletion();

        Assert.AreEqual(GameEventScriptExecutionState.Completed, result.State);
        CollectionAssert.AreEqual(new[] { "start", "next" }, calls);
    }

    [TestMethod]
    public void HostReusesOneContext()
    {
        var contexts = new List<GameEventScriptContext>();
        var host = GameEventScriptHost.CreateBuilder().Build();
        host.Subscribe("Start", [], (_, context) => contexts.Add(context));

        Assert.IsTrue(host.Receive(Create("Start")));
        host.RunToCompletion();
        Assert.IsTrue(host.Receive(Create("Start")));
        host.RunToCompletion();

        Assert.HasCount(2, contexts);
        Assert.AreSame(contexts[0], contexts[1]);
    }

    [TestMethod]
    public void MultipleProgramsAreLoadedAdditivelyAndRunSerially()
    {
        var first = Compile("on Start { emit First }");
        var second = Compile("on Start { emit Second }");
        var calls = new List<string>();
        var host = GameEventScriptHost.CreateBuilder().Build();
        host.Load(first);
        host.Load(second);
        host.Subscribe("First", [], (_, _) => calls.Add("first"));
        host.Subscribe("Second", [], (_, _) => calls.Add("second"));

        Assert.IsTrue(host.Receive(Create("Start")));
        host.RunToCompletion();

        CollectionAssert.AreEqual(new[] { "first", "second" }, calls);
    }

    [TestMethod]
    public void SameProgramRunsIndependentlyInTwoHosts()
    {
        var program = Compile("on Roll { emit Result(value: random from 1 to 100) }");
        var first = 0L;
        var second = 0L;
        Parallel.Invoke(
            () => first = ExecuteOnce(program, 11),
            () => second = ExecuteOnce(program, 11));
        Assert.AreEqual(first, second);
    }

    [TestMethod]
    public void LoadedProgramsShareOneFullyResetHostVmState()
    {
        var host = GameEventScriptHost.CreateBuilder().Build();
        host.Load(Compile("on First { emit Done }"));
        var vmState = host.VmState;
        host.Load(Compile("on Second { emit Done }"));
        host.Subscribe("Done", [], (_, _) => { });

        Assert.AreSame(vmState, host.VmState);
        host.Receive(Create("First"));
        host.Receive(Create("Second"));
        host.RunToCompletion();

        Assert.IsNotNull(vmState);
        Assert.AreEqual(GesVmState.StateValue.Ready, vmState.State);
        Assert.IsNull(vmState.ActiveProgram);
        Assert.IsNull(vmState.ProcessingMessage);
        Assert.IsTrue(vmState.RegisterValues.All(value => value.IsNothing));
    }

    [TestMethod]
    public void ExecuteFramePausesAndResumesScriptWithoutStartingNextMessage()
    {
        var program = Compile("on Start { for item from 1 to 4 { emit Tick(value: item) } }");
        var values = new List<long>();
        var host = GameEventScriptHost.CreateBuilder().Build();
        host.Load(program);
        host.Subscribe("Tick", ["value"], (message, _) => values.Add(message.Arguments.GetAsInteger("value")));
        host.Receive(Create("Start"));

        var paused = false;
        while (!host.IsIdle)
        {
            var frame = host.ExecuteFrame(1);
            paused |= frame.State == GameEventScriptExecutionState.Paused;
        }

        Assert.IsTrue(paused);
        CollectionAssert.AreEqual(new long[] { 1, 2, 3, 4 }, values);
    }

    [TestMethod]
    public void PublishDeliversLocallyAndToOutboundSink()
    {
        var sink = new RecordingSink(true);
        var local = new List<string>();
        GameEventScriptPublishResult result = default;
        var host = GameEventScriptHost.CreateBuilder().WithPublishSink(sink).Build();
        host.Subscribe("Start", [], (_, context) => result = context.Publish("Shared"));
        host.Subscribe("Shared", [], (_, _) => local.Add("shared"));

        host.Receive(Create("Start"));
        host.RunToCompletion();

        Assert.IsTrue(result.LocalAccepted);
        Assert.IsTrue(result.OutboundAttempted);
        Assert.IsTrue(result.OutboundAccepted);
        CollectionAssert.AreEqual(new[] { "shared" }, local);
        CollectionAssert.AreEqual(new[] { "Shared" }, sink.Messages.Select(message => message.Name).ToArray());
    }

    [TestMethod]
    public void ReceiveNeverForwardsOutbound()
    {
        var sink = new RecordingSink(true);
        var host = GameEventScriptHost.CreateBuilder().WithPublishSink(sink).Build();
        host.Subscribe("Start", [], (_, _) => { });
        Assert.IsTrue(host.Receive(Create("Start")));
        host.RunToCompletion();
        Assert.IsEmpty(sink.Messages);
    }

    [TestMethod]
    public void PublishWithoutSinkReportsLocalOnlyAcceptance()
    {
        GameEventScriptPublishResult result = default;
        var host = GameEventScriptHost.CreateBuilder().Build();
        host.Subscribe("Start", [], (_, context) => result = context.Publish("Shared"));
        host.Subscribe("Shared", [], (_, _) => { });

        host.Receive(Create("Start"));
        host.RunToCompletion();

        Assert.IsTrue(result.LocalAccepted);
        Assert.IsFalse(result.OutboundAttempted);
        Assert.IsFalse(result.OutboundAccepted);
        Assert.IsTrue(result.AnyAccepted);
    }

    [TestMethod]
    public void RejectedPublishSinkDoesNotDamageLocalDispatch()
    {
        GameEventScriptPublishResult result = default;
        var localCalls = 0;
        var host = GameEventScriptHost.CreateBuilder().WithPublishSink(new RecordingSink(false)).Build();
        host.Subscribe("Start", [], (_, context) => result = context.Publish("Shared"));
        host.Subscribe("Shared", [], (_, _) => localCalls++);

        host.Receive(Create("Start"));
        host.RunToCompletion();

        Assert.AreEqual(1, localCalls);
        Assert.IsTrue(result.LocalAccepted);
        Assert.IsTrue(result.OutboundAttempted);
        Assert.IsFalse(result.OutboundAccepted);
        Assert.IsTrue(result.AnyAccepted);
    }

    [TestMethod]
    public void PublishSinkExceptionIsReportedAsRejectedAndObserved()
    {
        GameEventScriptPublishResult result = default;
        GameEventScriptPublishResult observed = default;
        var observer = TestRuntimeObserver.ObservePublishResults(value => observed = value);
        var host = GameEventScriptHost.CreateBuilder()
            .WithRuntimeObserver(observer)
            .WithPublishSink(new ThrowingSink())
            .Build();
        host.Subscribe("Start", [], (_, context) => result = context.Publish("Shared"));
        host.Subscribe("Shared", [], (_, _) => { });

        host.Receive(Create("Start"));
        host.RunToCompletion();

        Assert.IsTrue(result.LocalAccepted);
        Assert.IsTrue(result.OutboundAttempted);
        Assert.IsFalse(result.OutboundAccepted);
        Assert.AreEqual(result.LocalAccepted, observed.LocalAccepted);
        Assert.AreEqual(result.OutboundAttempted, observed.OutboundAttempted);
        Assert.AreEqual(result.OutboundAccepted, observed.OutboundAccepted);
    }

    [TestMethod]
    public void SubscriptionAndDetachUseEnqueueTimeSnapshots()
    {
        var calls = new List<string>();
        var program = Compile("on Start { emit Script }");
        var host = GameEventScriptHost.CreateBuilder().Build();
        var instance = host.Load(program);
        var subscription = host.Subscribe("Start", [], (_, _) => calls.Add("native"));
        host.Subscribe("Script", [], (_, _) => calls.Add("script"));

        Assert.IsTrue(host.Receive(Create("Start")));
        Assert.IsTrue(instance.Detach());
        Assert.IsTrue(subscription.Unsubscribe());
        host.RunToCompletion();

        CollectionAssert.AreEqual(new[] { "native", "script" }, calls);
        Assert.IsFalse(host.Receive(Create("Start")));
        Assert.IsFalse(instance.Detach());
        Assert.IsFalse(subscription.Unsubscribe());
    }

    [TestMethod]
    public void InitializationRunsOncePerLoadedInstanceInLoadOrder()
    {
        var calls = new List<string>();
        var host = GameEventScriptHost.CreateBuilder().Build();
        host.Subscribe("Ready", [], (_, _) => calls.Add("ready"));
        var program = Compile("on initialization { emit Ready }");

        host.Load(program);
        host.Load(program);
        host.RunToCompletion();
        host.RunToCompletion();

        CollectionAssert.AreEqual(new[] { "ready", "ready" }, calls);
    }

    [TestMethod]
    public void InitializationIsQueuedBetweenOlderAndNewerMessages()
    {
        var calls = new List<string>();
        var host = GameEventScriptHost.CreateBuilder()
            .WithPublishSink(new TestPublishSink(_ => calls.Add("ready")))
            .Build();
        host.Subscribe("Before", [], (_, _) => calls.Add("before"));
        host.Subscribe("After", [], (_, _) => calls.Add("after"));
        host.Receive(Create("Before"));
        host.Load(Compile("on initialization { publish Ready }"));
        host.Receive(Create("After"));

        host.RunToCompletion();

        CollectionAssert.AreEqual(new[] { "before", "ready", "after" }, calls);
    }

    [TestMethod]
    public void SubscribeDuringDispatchOnlyAffectsLaterMessages()
    {
        var lateCalls = 0;
        var host = GameEventScriptHost.CreateBuilder().Build();
        host.Subscribe("Start", [], (_, _) => host.Subscribe("Start", [], (_, _) => lateCalls++));

        host.Receive(Create("Start"));
        host.RunToCompletion();
        Assert.AreEqual(0, lateCalls);

        host.Receive(Create("Start"));
        host.RunToCompletion();
        Assert.AreEqual(1, lateCalls);
    }

    [TestMethod]
    public void LoadDuringDispatchOnlyAffectsLaterMessages()
    {
        var scriptCalls = 0;
        var program = Compile("on Start { emit Script } ");
        var host = GameEventScriptHost.CreateBuilder().Build();
        host.Subscribe("Start", [], (_, _) => host.Load(program));
        host.Subscribe("Script", [], (_, _) => scriptCalls++);

        host.Receive(Create("Start"));
        host.RunToCompletion();
        Assert.AreEqual(0, scriptCalls);

        host.Receive(Create("Start"));
        host.RunToCompletion();
        Assert.AreEqual(1, scriptCalls);
    }

    [TestMethod]
    public void LoadWhileScriptIsPausedDoesNotResetTheActiveVmState()
    {
        var calls = new List<string>();
        var host = GameEventScriptHost.CreateBuilder().Build();
        host.Load(Compile("on Start {\n  let value be 1 + 2 + 3\n  emit First(value: value)\n}"));
        host.Subscribe("First", ["value"], (message, _) => calls.Add($"first:{message.Arguments.GetAsInteger("value")}"));
        host.Subscribe("Second", [], (_, _) => calls.Add("second"));
        host.Receive(Create("Start"));

        Assert.AreEqual(GameEventScriptExecutionState.Paused, host.ExecuteFrame(1).State);
        host.Load(Compile("on Next { emit Second }"));
        host.RunToCompletion();
        host.Receive(Create("Next"));
        host.RunToCompletion();

        CollectionAssert.AreEqual(new[] { "first:6", "second" }, calls);
    }

    [TestMethod]
    public void RuntimeLimitsResetForEveryScriptHandler()
    {
        var ticks = 0;
        var host = GameEventScriptHost.CreateBuilder()
            .WithRuntimeLimits(new GameEventScriptRuntimeLimits { MaxLoopIterations = 2 })
            .Build();
        host.Load(Compile("on Start { for item from 1 to 2 emit Tick }"));
        host.Load(Compile("on Start { for item from 1 to 2 emit Tick }"));
        host.Subscribe("Tick", [], (_, _) => ticks++);

        host.Receive(Create("Start"));
        var result = host.RunToCompletion();

        Assert.AreEqual(GameEventScriptExecutionState.Completed, result.State);
        Assert.AreEqual(4, ticks);
    }

    [TestMethod]
    public void MessageNameSubscriptionMatchesAnySignature()
    {
        var seen = new List<string>();
        var host = GameEventScriptHost.CreateBuilder().Build();
        host.SubscribeMessageName("Ping", (message, _) => seen.Add(message.SignatureId));
        host.Receive(Create("Ping", ("amount", GesValue.GesInteger(7))));
        host.RunToCompletion();
        CollectionAssert.AreEqual(new[] { "Ping(amount)" }, seen);
    }

    [TestMethod]
    public void NativeHandlerIsAtomicForFrameBudget()
    {
        var calls = new List<string>();
        var host = GameEventScriptHost.CreateBuilder().Build();
        host.Subscribe("Start", [], (_, context) => { calls.Add("start"); context.Emit("Done"); });
        host.Subscribe("Done", [], (_, _) => calls.Add("done"));
        host.Receive(Create("Start"));
        var frame = host.ExecuteFrame(1);
        Assert.AreEqual(GameEventScriptExecutionState.Completed, frame.State);
        CollectionAssert.AreEqual(new[] { "start", "done" }, calls);
    }

    [TestMethod]
    public void CSharpAutoRunnerSerializesReceiveAndPumps()
    {
        var completed = new ManualResetEventSlim(false);
        var host = GameEventScriptHost.CreateBuilder().Build();
        using var runner = host.RunAutomatically();
        runner.Subscribe("Start", [], (_, _) => completed.Set());
        Assert.IsTrue(runner.Receive(Create("Start")));
        Assert.IsTrue(completed.Wait(TimeSpan.FromSeconds(2)));
    }

    [TestMethod]
    public void CSharpAutoRunnerSerializesConcurrentReceives()
    {
        const int messageCount = 32;
        using var completed = new CountdownEvent(messageCount);
        var active = 0;
        var maximumActive = 0;
        var host = GameEventScriptHost.CreateBuilder().Build();
        using var runner = host.RunAutomatically();
        runner.Subscribe("Start", [], (_, _) =>
        {
            var nowActive = Interlocked.Increment(ref active);
            UpdateMaximum(ref maximumActive, nowActive);
            Thread.SpinWait(10_000);
            Interlocked.Decrement(ref active);
            completed.Signal();
        });

        Parallel.For(0, messageCount, _ => Assert.IsTrue(runner.Receive(Create("Start"))));

        Assert.IsTrue(completed.Wait(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(1, maximumActive);
    }

    [TestMethod]
    public void CSharpAutoRunnerSupportsDynamicSubscribeAndDetachWithSnapshots()
    {
        using var nativeCompleted = new CountdownEvent(2);
        using var scriptCompleted = new ManualResetEventSlim(false);
        var nativeCalls = 0;
        var observer = TestRuntimeObserver.ObserveMessages(
            messageEmitted: message =>
            {
                if (message.Name == "ScriptDone") scriptCompleted.Set();
            });
        var host = GameEventScriptHost.CreateBuilder().WithRuntimeObserver(observer).Build();
        using var runner = host.RunAutomatically();

        var first = runner.Subscribe("Native", [], (_, _) =>
        {
            Interlocked.Increment(ref nativeCalls);
            nativeCompleted.Signal();
        });
        Assert.IsTrue(runner.Receive(Create("Native")));
        Assert.IsTrue(runner.Unsubscribe(first));

        var second = runner.Subscribe("Native", [], (_, _) =>
        {
            Interlocked.Increment(ref nativeCalls);
            nativeCompleted.Signal();
        });
        Assert.IsTrue(runner.Receive(Create("Native")));
        Assert.IsTrue(runner.Unsubscribe(second));

        var instance = runner.Load(Compile("on ScriptStart { emit ScriptDone }"));
        Assert.IsTrue(runner.Receive(Create("ScriptStart")));
        Assert.IsTrue(runner.Detach(instance));

        Assert.IsTrue(nativeCompleted.Wait(TimeSpan.FromSeconds(5)));
        Assert.IsTrue(scriptCompleted.Wait(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(2, nativeCalls);
        Assert.IsFalse(runner.Receive(Create("Native")));
        Assert.IsFalse(runner.Receive(Create("ScriptStart")));
    }

    [TestMethod]
    [TestCategory("Performance")]
    public void WarmQueueDispatchFrameResultAndVmResumeDoNotAllocate()
    {
        const int iterations = 1_000;
        var host = GameEventScriptHost.CreateBuilder().Build();
        host.Load(Compile("on Tick { let value be 1 + 2 + 3 }"));
        var message = Create("Tick");

        for (var index = 0; index < 100; index++)
        {
            host.Receive(message);
            while (!host.IsIdle) host.ExecuteFrame(1);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < iterations; index++)
        {
            host.Receive(message);
            while (!host.IsIdle) host.ExecuteFrame(1);
        }

        Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static GameEventScriptProgram Compile(string source)
        => GameEventScriptBuilder.Create().AddScript(source).Compile();

    private static long ExecuteOnce(GameEventScriptProgram program, long seed)
    {
        var value = 0L;
        var host = GameEventScriptHost.CreateBuilder().WithRandom(GameEventScriptRandomGenerator.FromSeed(seed)).Build();
        host.Load(program);
        host.Subscribe("Result", ["value"], (message, _) => value = message.Arguments.GetAsInteger("value"));
        host.Receive(Create("Roll"));
        host.RunToCompletion();
        return value;
    }

    private static void UpdateMaximum(ref int target, int candidate)
    {
        int current;
        do
        {
            current = Volatile.Read(ref target);
            if (candidate <= current) return;
        }
        while (Interlocked.CompareExchange(ref target, candidate, current) != current);
    }

    private sealed class RecordingSink(bool accepted) : IGameEventScriptPublishSink
    {
        public List<GameEventScriptMessage> Messages { get; } = [];
        public bool Publish(GameEventScriptMessage message) { Messages.Add(message); return accepted; }
    }

    private sealed class ThrowingSink : IGameEventScriptPublishSink
    {
        public bool Publish(GameEventScriptMessage message) => throw new InvalidOperationException("sink failed");
    }
}
