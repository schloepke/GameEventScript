using StepH.GameEventScript.Api;
using StepH.GameEventScript.CSharpBridge;
using StepH.GameEventScript.Runtime.VM;
using static StepH.GameEventScript.Api.GameEventScriptMessage;

namespace StepH_GameEventScript_Tests.Native.Runtime;

[TestClass]
public sealed class GameEventScriptHostSteppingTests
{
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

}
